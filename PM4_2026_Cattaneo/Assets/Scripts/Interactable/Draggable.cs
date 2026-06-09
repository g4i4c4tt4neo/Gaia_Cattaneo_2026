// ============================================================================
// Draggable — Logic / verb layer (IHoldInteractable, hold)
//
// Physics-driven "push this thing along the ground while held" verb. Unlike
// HoldGrabbable (which lifts a prop in front of the camera), Draggable keeps
// the prop on the ground and applies a horizontal follow-force toward the
// GrabAnchor's XZ-projected position. Gravity stays on, vertical motion is
// untouched — the prop slides, tumbles, and bumps into geometry naturally.
//
// Use for heavy crates, movable barricades, drawers — anything that should
// feel weighty and grounded. Heaviness comes from the prop's Rigidbody.mass
// + the maxSpeed cap, NOT from the script faking inertia.
//
// Requires:
//   - Rigidbody (non-kinematic, mass tuned to taste — heavier = harder to push)
//   - Collider on a layer included in the player's RaycastSensor mask
//   - Exactly one GrabAnchor active in the scene
//
// Releasing the key (or looking away, or moving too far) ends the drag with
// no impulse — heavy props don't "throw." They just stop.
// ============================================================================

using System;
using UnityEngine;

namespace Ludocore
{
    /// <summary>Hold-to-push IHoldInteractable. While the interact key is held,
    /// applies a horizontal follow-force toward GrabAnchor.Current's XZ
    /// position. Gravity and vertical motion are unchanged.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Draggable : MonoBehaviour, IHoldInteractable
    {
        //==================== CONFIG =====================
        [Header("Drag")]
        [Tooltip("How aggressively the prop accelerates toward the anchor's XZ position. " +
                 "Higher = snappier push, lower = more inertia.")]
        [SerializeField] private float followStiffness = 8f;

        [Tooltip("Per-fixed-step damping on the horizontal velocity (0..1). " +
                 "Higher = less sliding, slower response.")]
        [Range(0f, 1f)]
        [SerializeField] private float followDamping = 0.5f;

        [Tooltip("Caps horizontal speed while dragged. Tune for prop weight — a " +
                 "heavy crate might cap at 1.5 m/s, a light bin at 4.")]
        [Min(0f)]
        [SerializeField] private float maxSpeed = 2f;

        [Tooltip("If the prop ends up further than this from the anchor (e.g. " +
                 "stuck on geometry while the player walks past), auto-release.")]
        [SerializeField] private float breakDistance = 3f;

        //==================== STATE =====================
        [Header("Debug")]
        [ReadOnly, SerializeField] private bool isDragged;

        private Rigidbody _rb;
        private GrabAnchor _anchor;
        private RigidbodyInterpolation _cachedInterpolation;

        //==================== IHoldInteractable =====================
        // No timer — Duration reported as 0 so a radial UI stays empty.
        // Drag continues until the player releases the key (CancelInteract).
        public float Duration => 0f;
        public bool CanInteract => _rb && !_rb.isKinematic;

        // Drag has no fillable progress. The no-op accessor pair satisfies
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
            if (isDragged) Release();
        }

        private void FixedUpdate()
        {
            if (!isDragged || !_anchor) return;

            // --- Project both positions to the XZ plane — drag is horizontal only.
            Vector3 propXZ = _rb.worldCenterOfMass;
            propXZ.y = 0f;
            Vector3 anchorXZ = _anchor.transform.position;
            anchorXZ.y = 0f;

            Vector3 toTarget = anchorXZ - propXZ;

            // --- Auto-release if the prop is too far behind (stuck on geometry).
            if (toTarget.sqrMagnitude > breakDistance * breakDistance)
            {
                Release();
                return;
            }

            // --- Position drive: damped horizontal velocity toward anchor XZ.
            Vector3 desiredHoriz = toTarget * followStiffness;
            if (desiredHoriz.sqrMagnitude > maxSpeed * maxSpeed)
                desiredHoriz = desiredHoriz.normalized * maxSpeed;

            Vector3 currentVel = _rb.linearVelocity;
            Vector3 currentHoriz = new Vector3(currentVel.x, 0f, currentVel.z);
            Vector3 nextHoriz = Vector3.Lerp(currentHoriz, desiredHoriz, 1f - followDamping);

            // Preserve vertical velocity — gravity / jumps / ledges still work.
            _rb.linearVelocity = new Vector3(nextHoriz.x, currentVel.y, nextHoriz.z);
        }

        //==================== IHoldInteractable IMPLEMENTATION =====================
        public void BeginInteract()
        {
            if (!CanInteract || isDragged) return;

            _anchor = GrabAnchor.Current;
            if (!_anchor)
            {
                Debug.LogWarning($"Draggable: no active GrabAnchor in scene; cannot drag '{name}'.", this);
                return;
            }

            // Interpolate the rendered transform between physics steps so the
            // prop tracks player motion smoothly instead of stepping at 50 Hz.
            _cachedInterpolation = _rb.interpolation;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;

            isDragged = true;
        }

        public void CancelInteract()
        {
            if (isDragged) Release();
        }

        //==================== PRIVATE =====================
        private void Release()
        {
            if (!isDragged) return;

            _rb.interpolation = _cachedInterpolation;
            _anchor = null;
            isDragged = false;
            // No impulse — heavy props don't get thrown by letting go.
        }
    }
}

// ============================================================================
// Setup in a scene
//   1. One-time per scene: under the player camera, add a child GameObject
//      with a GrabAnchor component (shared with Grabbable / HoldGrabbable).
//   2. On any draggable prop:
//      - Add a Rigidbody (non-kinematic). Mass = the dominant heaviness knob.
//        Light bin ~5, crate ~25, heavy chest ~80+.
//      - Make sure its Collider is on a layer included in the player's
//        RaycastSensor mask.
//      - Add this Draggable component.
//      - (Optional) Add a Focusable for the standard prompt UI.
//   3. Tune per object:
//      - followStiffness / followDamping: responsiveness of the push.
//      - maxSpeed: cap on horizontal velocity — the "weight" feel.
//      - breakDistance: how far the prop can lag before the drag auto-ends.
//   4. Default control flow:
//      - Look at prop, hold E to push. Walk forward and the prop slides
//        with you, lagging based on stiffness / mass.
//      - Release E → drag ends, prop coasts to a stop under friction.
//      - Look away or walk past breakDistance → drag auto-ends.
// ============================================================================
