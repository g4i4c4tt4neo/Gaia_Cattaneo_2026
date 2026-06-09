// ============================================================================
// DropSlot — Logic / receptacle layer
//
// Trigger volume that accepts one specific Grabbable. When the correct prop
// enters its trigger (carried or just dropped on top), the slot:
//   - Force-releases it from the player's grip
//   - Snaps it onto the slot pose (this transform, or an explicit snapPose)
//   - Sets the rigidbody kinematic so it stays put and can't be re-grabbed
//   - Fires a UnityEvent (wire effects in the Inspector)
//   - Raises an optional GameEvent (broadcast to listeners across the scene)
//
// Wrong props are ignored — they pass through the trigger and behave normally.
// Once filled, the slot stops responding until something resets it externally.
// ============================================================================

using UnityEngine;
using UnityEngine.Events;

namespace Ludocore
{
    /// <summary>Trigger receptacle that snaps a specific Grabbable into place
    /// and fires events when the correct prop is delivered.</summary>
    [RequireComponent(typeof(Collider))]
    public class DropSlot : MonoBehaviour
    {
        //==================== CONFIG =====================
        [Header("Slot")]
        [Tooltip("The Grabbable that fits this slot. Anything else is ignored.")]
        [SerializeField] private Grabbable correctObject;

        [Tooltip("Where the prop snaps when accepted. Leave empty to snap onto " +
                 "this slot's own transform.")]
        [SerializeField] private Transform snapPose;

        //==================== STATE =====================
        [Header("Debug")]
        [ReadOnly, SerializeField] private bool isFilled;

        //==================== OUTPUTS =====================
        [Header("Events")]
        [Tooltip("Invoked locally when the correct prop is placed. " +
                 "Wire scene-specific reactions here (VFX, audio, doors).")]
        [SerializeField] private UnityEvent onCorrectPlaced;

        [Tooltip("Optional broadcast event raised when the correct prop is placed. " +
                 "Any GameEventListener in the scene can react.")]
        [SerializeField] private GameEvent correctPlacedEvent;

        public bool IsFilled => isFilled;

        //==================== LIFECYCLE =====================
        private void OnTriggerEnter(Collider other)
        {
            if (isFilled || !correctObject) return;

            // Compound-collider safe: compare on the rigidbody's GameObject.
            Rigidbody rb = other.attachedRigidbody;
            if (!rb || rb.gameObject != correctObject.gameObject) return;

            Place(correctObject, rb);
        }

        //==================== PRIVATE =====================
        private void Place(Grabbable grabbable, Rigidbody rb)
        {
            // Pull the prop out of the player's grip first so the carry loop
            // stops fighting our snap.
            grabbable.ForceRelease();

            Transform target = snapPose ? snapPose : transform;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = target.position;
            rb.rotation = target.rotation;
            // Kinematic = locked in place, and Grabbable.CanInteract goes false
            // so the player can't pick it back up.
            rb.isKinematic = true;

            isFilled = true;
            onCorrectPlaced?.Invoke();
            if (correctPlacedEvent) correctPlacedEvent.Raise();
        }
    }
}

// ============================================================================
// Setup in a scene
//   1. Create an empty GameObject where the prop should land.
//   2. Add a Collider and tick "Is Trigger". Size it to the acceptance volume
//      around the slot (a bit larger than the prop is forgiving).
//   3. Add this DropSlot component.
//   4. Drag the specific Grabbable prop into the "Correct Object" field.
//   5. (Optional) Add a child transform for the exact snap pose (position +
//      rotation) and assign it to "Snap Pose". Otherwise the slot snaps the
//      prop onto its own transform.
//   6. Wire the UnityEvent in the Inspector for local reactions, and/or assign
//      a GameEvent asset so other systems (door openers, score, narrative)
//      can listen via GameEventListener.
//
// Notes
//   - The prop's Rigidbody will be set kinematic on placement, which both
//     locks it visually and disables Grabbable.CanInteract — so it can't be
//     picked back up. To "reset" a slot, set isKinematic = false on the prop
//     and clear the slot's isFilled flag from your own controller.
//   - Wrong objects pass through the trigger unaffected.
// ============================================================================
