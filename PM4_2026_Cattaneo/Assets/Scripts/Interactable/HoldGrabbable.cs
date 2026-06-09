// ============================================================================
// HoldGrabbable — Logic / verb layer (IHoldInteractable, hold)
//
// Physics-driven "carry while you hold the key" verb. Sibling alternative to
// Grabbable (which is tap-toggle). The press-and-release feel is more tense:
// letting go drops the prop, looking away drops the prop, walking into a wall
// past breakDistance drops the prop. Use when carrying should feel fragile
// (surgery, tray-balancing, hot potato).
//
// Mechanically identical to Grabbable while held — rigidbody is velocity-
// driven toward GrabAnchor.Current, rotation is matched. The difference is
// purely in the dispatch contract: IHoldInteractable means PlayerInteractor
// will call CancelInteract() automatically on key-up, target-change, or
// focus-loss, and this verb drops the prop in response.
//
// Drop on any prop that should be carryable with hold-feel. Requires:
//   - Rigidbody (must NOT be kinematic)
//   - Collider on a layer included in the player's RaycastSensor mask
//   - Exactly one GrabAnchor active in the scene
//
// Use Grabbable instead if you want tap-to-grab / tap-to-throw with the
// "carry persists until I decide to release" feel.
// ============================================================================

using System;
using UnityEngine;

namespace Ludocore
{
    /// <summary>Carry-via-physics IHoldInteractable. While the interact key is
    /// held the rigidbody follows GrabAnchor.Current. Releasing the key (or
    /// looking away) drops the prop.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class HoldGrabbable : MonoBehaviour, IHoldInteractable
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

        //==================== STATE =====================
        [Header("Debug")]
        [ReadOnly, SerializeField] private bool isHeld;

        private Rigidbody _rb;
        private GrabAnchor _anchor;
        private bool _cachedUseGravity;
        private float _cachedLinearDamping;
        private float _cachedAngularDamping;
        private RigidbodyInterpolation _cachedInterpolation;

        //==================== IHoldInteractable =====================
        // No timer — Duration is reported as 0 so a radial UI stays empty.
        // Carry continues until the player releases the key (CancelInteract).
        public float Duration => 0f;
        public bool CanInteract => _rb && !_rb.isKinematic;

        // Carry has no fillable progress. The no-op accessor pair satisfies
        // the contract without raising CS0067 (event declared but never invoked).
        // Any radial UI subscription is silently discarded.
        public event Action<float> OnProgress { add { } remove { } }

        //==================== LIFECYCLE =====================
        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
        }

        private void OnDisable()
        {
            // If we get yanked offline mid-carry, drop cleanly.
            if (isHeld) Release();
        }

        private void FixedUpdate()
        {
            if (!isHeld || !_anchor) return;

            // --- Auto-release if the prop has been pushed too far (wall clip, snag).
            Vector3 toTarget = _anchor.transform.position - _rb.worldCenterOfMass;
            if (toTarget.sqrMagnitude > breakDistance * breakDistance)
            {
                Release();
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

        //==================== IHoldInteractable IMPLEMENTATION =====================
        public void BeginInteract()
        {
            if (!CanInteract || isHeld) return;

            _anchor = GrabAnchor.Current;
            if (!_anchor)
            {
                Debug.LogWarning($"HoldGrabbable: no active GrabAnchor in scene; cannot grab '{name}'.", this);
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
        }

        public void CancelInteract()
        {
            if (isHeld) Release();
        }

        //==================== PRIVATE =====================
        private void Release()
        {
            if (!isHeld) return;

            _rb.useGravity = _cachedUseGravity;
            _rb.linearDamping = _cachedLinearDamping;
            _rb.angularDamping = _cachedAngularDamping;
            _rb.interpolation = _cachedInterpolation;

            _anchor = null;
            isHeld = false;
        }
    }
}

// ============================================================================
// Setup in a scene
//   1. One-time per scene: under the player camera, add a child GameObject
//      with a GrabAnchor component (see GrabAnchor.cs for setup). Shared with
//      Grabbable — one anchor serves both.
//   2. On any carryable prop:
//      - Add a Rigidbody (non-kinematic, mass to taste).
//      - Make sure its Collider is on a layer included in the player's
//        RaycastSensor mask (so PlayerInteractor can focus it).
//      - Add this HoldGrabbable component (NOT alongside Grabbable — pick one).
//      - (Optional) Add a Focusable for the standard prompt UI.
//   3. Tune per object:
//      - followStiffness / followDamping: feel of the carry.
//      - breakDistance: lower for fragile / pin-precise, higher for big props.
//   4. Default control flow:
//      - Look at prop, hold E to carry. Anchor pose drives prop pose.
//      - Release E → drop. Look away → drop. Walk into wall hard → drop.
// ============================================================================
