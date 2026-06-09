// ============================================================================
// Grabbable — Logic / verb layer (IInteractable, tap-toggle)
//
// Physics-driven "carry this thing" verb. First tap picks the prop up:
// the rigidbody is velocity-driven toward the active GrabAnchor (a transform
// parented under the camera). Second tap throws it forward along the
// anchor's facing — throw force is per-object, so a feather flutters and a
// brick chucks. Set throwForce = 0 for a simple drop.
//
// Drop on any prop that should be carryable. Requires:
//   - Rigidbody (must NOT be kinematic)
//   - Collider on a layer included in the player's RaycastSensor mask
//   - Exactly one GrabAnchor active in the scene
//
// Composes cleanly with Focusable for the standard prompt-UI flow, but does
// not require it. A single tap toggles between grabbed and released — looking
// away while carried does NOT drop the prop. For a "release-on-key-up" feel,
// use HoldGrabbable instead.
// ============================================================================

using UnityEngine;

namespace Ludocore
{
    /// <summary>Carry-via-physics IInteractable. Tap-toggle: first tap picks up,
    /// second tap throws (or drops if throwForce = 0).</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Grabbable : MonoBehaviour, IInteractable
    {
        //==================== CONFIG =====================
        [Header("Carry")]
        [Tooltip("How aggressively the prop accelerates toward the grab anchor. " +
                 "Higher = stiffer follow, lower = floatier.")]
        [SerializeField] private float followStiffness = 18f;

        [Tooltip("Per-fixed-step damping on the carry velocity (0..1). " +
                 "Higher = less wobble, slower response.")]
        [Range(0f, 1f)]
        [SerializeField] private float followDamping = 0.85f;

        [Tooltip("How aggressively the prop rotates to match the anchor's rotation.")]
        [SerializeField] private float rotationStiffness = 25f;

        [Tooltip("If the prop ends up further than this from the anchor (e.g. clipped " +
                 "through a wall, snagged on geometry), auto-release.")]
        [SerializeField] private float breakDistance = 2.5f;

        [Header("Release")]
        [Tooltip("Forward impulse applied along the grab anchor's forward on release. " +
                 "0 = simple drop. Tune per object — light props ~3, heavy ~8+.")]
        [SerializeField] private float throwForce = 5f;

        //==================== STATE =====================
        [Header("Debug")]
        [ReadOnly, SerializeField] private bool isHeld;

        private Rigidbody _rb;
        private GrabAnchor _anchor;
        private bool _cachedUseGravity;
        private float _cachedLinearDamping;
        private float _cachedAngularDamping;
        private RigidbodyInterpolation _cachedInterpolation;

        //==================== IInteractable =====================
        // Stays interactable while held so the second tap can throw.
        // Goes false when kinematic — DropSlot uses that to lock the prop in place.
        public bool CanInteract => _rb && !_rb.isKinematic;

        //==================== LIFECYCLE =====================
        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
        }

        private void OnDisable()
        {
            // If we get yanked offline mid-carry, drop cleanly without a throw.
            if (isHeld) ReleaseInternal(applyThrow: false);
        }

        private void FixedUpdate()
        {
            if (!isHeld || !_anchor) return;

            // --- Auto-release if the prop has been pushed too far (wall clip, snag).
            Vector3 toTarget = _anchor.transform.position - _rb.worldCenterOfMass;
            if (toTarget.sqrMagnitude > breakDistance * breakDistance)
            {
                ReleaseInternal(applyThrow: false);
                return;
            }

            // --- Position drive: damped velocity toward the anchor.
            Vector3 desiredVelocity = toTarget * followStiffness;
            _rb.linearVelocity = Vector3.Lerp(_rb.linearVelocity, desiredVelocity, 1f - followDamping);

            // --- Rotation drive: shortest-path angular velocity to match anchor.rotation.
            Quaternion delta = _anchor.transform.rotation * Quaternion.Inverse(_rb.rotation);
            delta.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;
            if (Mathf.Abs(angle) > 0.01f && !float.IsInfinity(axis.x))
            {
                _rb.angularVelocity = axis.normalized * (angle * Mathf.Deg2Rad * rotationStiffness);
            }
        }

        //==================== IInteractable IMPLEMENTATION =====================
        public void BeginInteract()
        {
            if (!CanInteract) return;

            // Toggle: held → throw, not held → pick up.
            if (isHeld) ReleaseInternal(applyThrow: throwForce > 0f);
            else PickUp();
        }

        /// <summary>External force-release (e.g. DropSlot snapping the prop into place).
        /// Drops cleanly with no throw impulse. Safe to call when not held.</summary>
        public void ForceRelease()
        {
            if (isHeld) ReleaseInternal(applyThrow: false);
        }

        //==================== PRIVATE =====================
        private void PickUp()
        {
            _anchor = GrabAnchor.Current;
            if (!_anchor)
            {
                Debug.LogWarning($"Grabbable: no active GrabAnchor in scene; cannot grab '{name}'.", this);
                return;
            }

            // Cache rigidbody state so release can restore it exactly.
            _cachedUseGravity = _rb.useGravity;
            _cachedLinearDamping = _rb.linearDamping;
            _cachedAngularDamping = _rb.angularDamping;
            _cachedInterpolation = _rb.interpolation;

            _rb.useGravity = false;
            // Heavier damping while carried smooths out collisions with walls.
            _rb.linearDamping = 5f;
            _rb.angularDamping = 5f;
            // Interpolate the rendered transform between physics steps so the
            // prop tracks camera motion smoothly instead of stepping at 50 Hz.
            _rb.interpolation = RigidbodyInterpolation.Interpolate;

            isHeld = true;
            _anchor.Attach(this);
        }

        private void ReleaseInternal(bool applyThrow)
        {
            if (!isHeld) return;

            _rb.useGravity = _cachedUseGravity;
            _rb.linearDamping = _cachedLinearDamping;
            _rb.angularDamping = _cachedAngularDamping;
            _rb.interpolation = _cachedInterpolation;

            if (applyThrow && _anchor)
            {
                _rb.AddForce(_anchor.transform.forward * throwForce, ForceMode.VelocityChange);
            }

            if (_anchor) _anchor.Detach(this);
            _anchor = null;
            isHeld = false;
        }
    }
}

// ============================================================================
// Setup in a scene
//   1. One-time per scene: under the player camera, add a child GameObject
//      with a GrabAnchor component (see GrabAnchor.cs for setup).
//   2. On any carryable prop:
//      - Add a Rigidbody (non-kinematic, mass to taste).
//      - Make sure its Collider is on a layer included in the player's
//        RaycastSensor mask (so PlayerInteractor can focus it).
//      - Add this Grabbable component.
//      - (Optional) Add a Focusable for the standard prompt UI.
//   3. Tune per object:
//      - followStiffness / followDamping: feel of the carry. Heavier props
//        usually want lower stiffness so they lag behind the anchor.
//      - throwForce: 0 = drop, 3 = light toss, 8+ = chuck.
//      - breakDistance: lower for fragile / pin-precise, higher for big props.
//   4. Default control flow (no PlayerInteractor changes needed):
//      - Look at prop, press E to lift.
//      - Move / look around to carry. Anchor rotation drives prop rotation.
//      - Press E again to throw forward (or drop if throwForce = 0). The held
//        prop sits in front of the camera so the raycast keeps focusing it,
//        which is what lets the second press dispatch back to this Grabbable.
//      - If the prop gets pushed past breakDistance (wall clip, snag), it
//        auto-releases as a safety net.
// ============================================================================
