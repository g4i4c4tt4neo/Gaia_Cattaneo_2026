// ============================================================================
// GrabAnchor — Glue / scene singleton
//
// Marker transform that says "this is where grabbed objects float toward."
// Place as a child of the player camera at the desired carry offset and
// rotation. Grabbable reads GrabAnchor.Current as its target each FixedUpdate
// while held — both position and rotation are matched, so moving / rotating
// this transform moves / rotates the carried prop.
//
// Single-active assumption: one anchor per scene at a time. If multiple are
// enabled, the most recent wins and a warning is logged.
// ============================================================================

using UnityEngine;

namespace Ludocore
{
    /// <summary>The transform that grabbed objects follow. Pose (position + rotation)
    /// of this transform drives the carried rigidbody. Also tracks the currently
    /// held Grabbable so other systems (PlayerAttack, UI prompts) can ask
    /// "what is the player holding right now?".</summary>
    public class GrabAnchor : MonoBehaviour
    {
        /// <summary>Currently active anchor in the scene. Null if none enabled.</summary>
        public static GrabAnchor Current { get; private set; }

        //==================== STATE =====================
        [Header("Debug")]
        [ReadOnly, SerializeField] private Grabbable held;

        /// <summary>The Grabbable currently attached to this anchor, or null.</summary>
        public Grabbable Held => held;

        //==================== INPUTS =====================
        /// <summary>Called by Grabbable when it picks up.</summary>
        public void Attach(Grabbable grabbable) => held = grabbable;

        /// <summary>Called by Grabbable when it releases. No-op if it wasn't the one held.</summary>
        public void Detach(Grabbable grabbable)
        {
            if (held == grabbable) held = null;
        }

        //==================== LIFECYCLE =====================
        private void OnEnable()
        {
            if (Current && Current != this)
            {
                Debug.LogWarning(
                    $"GrabAnchor: replacing active anchor on '{Current.name}' with '{name}'. " +
                    "Only one GrabAnchor should be active at a time.", this);
            }
            Current = this;
        }

        private void OnDisable()
        {
            if (Current == this) Current = null;
        }
    }
}

// ============================================================================
// Setup in a scene
//   1. Under the player camera, create an empty child GameObject named e.g.
//      "GrabPoint". Position it where carried objects should float (a sensible
//      starting point: local 0, -0.15, 1.1).
//   2. Add this GrabAnchor component. Only one should be active at a time.
//   3. The anchor's rotation is also matched, so if you want carried objects
//      upright, keep the anchor's rotation aligned with the camera (default).
// ============================================================================
