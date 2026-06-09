// ============================================================================
// PlayerInteractor — Glue / input layer
//
// One script per player (or camera). Each frame it reads the forward raycast
// sensor, tracks which focusable it is looking at, and dispatches the
// interact key through the IInteractable contract — TAP for verbs that
// implement only IInteractable, HOLD for verbs that also implement
// IHoldInteractable.
//
// Talks to three small contracts and nothing else:
//   IFocusable         — drives show/hide of prompts on the focused object
//   IInteractable      — tap-dispatch on key-down
//   IHoldInteractable  — adds key-up / target-change cancel for hold verbs
//
// One key from the player's POV. Many verbs under the hood — the interactor
// asks the target what it can do and dispatches accordingly. No knowledge of
// trees, resources, buildings, or UI. It just brokers interfaces.
// ============================================================================

using UnityEngine;
using UnityEngine.InputSystem;

namespace Ludocore
{
    /// <summary>Raycast-based interactor. Drives IFocusable transitions and
    /// dispatches IInteractable tap verbs / IHoldInteractable hold verbs on
    /// the interact action.</summary>
    public class PlayerInteractor : MonoBehaviour
    {
        //==================== CONFIG =====================
        [Header("Config")]
        [Tooltip("Raycast sensor that looks forward — usually placed on the camera.")]
        [SerializeField] private RaycastSensor raycastSensor;

        [Tooltip("Input action that drives the interact key — define in your " +
                 "InputActions asset (Player map → Interact, bound to E / Gamepad West).")]
        [SerializeField] private InputActionReference interactAction;

        //==================== STATE =====================
        [Header("Debug")]
        [ReadOnly, SerializeField] private GameObject currentTarget;

        private IFocusable _currentFocus;
        private IHoldInteractable _currentHold;

        //==================== LIFECYCLE =====================
        private void OnEnable()
        {
            if (interactAction) interactAction.action.Enable();
        }

        private void Update()
        {
            UpdateFocus();
            HandleInteract();
        }

        private void OnDisable()
        {
            // Cancel any in-flight hold and drop focus cleanly.
            _currentHold?.CancelInteract();
            _currentHold = null;

            _currentFocus?.SetFocused(false);
            _currentFocus = null;
            currentTarget = null;

            if (interactAction) interactAction.action.Disable();
        }

        //==================== PRIVATE =====================
        private void UpdateFocus()
        {
            IFocusable nextFocus = null;
            GameObject nextTarget = null;

            if (raycastSensor.TryGetNearest(out Signal signal) && signal.Object)
            {
                nextTarget = signal.Object;
                nextTarget.TryGetComponent(out nextFocus);
            }

            // Clear stale reference if the previous focus was destroyed.
            if (_currentFocus is Object prev && !prev) _currentFocus = null;

            if (ReferenceEquals(nextFocus, _currentFocus)) return;

            _currentFocus?.SetFocused(false);
            nextFocus?.SetFocused(true);

            _currentFocus = nextFocus;
            currentTarget = nextTarget;
        }

        private void HandleInteract()
        {
            // Resolve the currently focused IInteractable, if any. This covers
            // both tap and hold verbs (IHoldInteractable : IInteractable).
            IInteractable interactable = null;
            if (currentTarget) currentTarget.TryGetComponent(out interactable);

            // Drop a stale hold reference if the underlying object was destroyed.
            if (_currentHold is Object held && !held) _currentHold = null;

            // Cancel an in-flight hold if the player released, looked away, or
            // it's no longer interactable. Treat "no action wired" as "not held"
            // so a missing reference doesn't trap the held state forever.
            if (_currentHold != null)
            {
                bool sameTarget = ReferenceEquals(_currentHold, interactable);
                bool keyHeld = interactAction && interactAction.action.IsPressed();
                if (!keyHeld || !sameTarget || !_currentHold.CanInteract)
                {
                    _currentHold.CancelInteract();
                    _currentHold = null;
                }
            }

            // Begin a new interaction only on a fresh key-down + valid target.
            if (interactable == null || !interactable.CanInteract) return;
            if (!interactAction || !interactAction.action.WasPressedThisFrame()) return;

            interactable.BeginInteract();

            // If this verb is hold-flavored, latch it so subsequent frames can
            // tick the cancel-on-release path above.
            if (interactable is IHoldInteractable hold) _currentHold = hold;
        }
    }
}

// ============================================================================
// Setup in a scene
//   1. On the player camera: add a RaycastSensor. Set its distance (e.g. 3m),
//      layer mask (which layers can be interacted with), and optional sphere
//      cast radius for forgiving aim.
//   2. Add this PlayerInteractor on the same GameObject. Wire the sensor
//      reference and the interact InputActionReference.
//   3. In your InputActions asset (InputSystem_Actions.inputactions):
//      - The Player map already defines an Interact action (Button).
//      - Bind it to Keyboard / E and Gamepad / West (or your choice).
//      - Do NOT add a "Hold" interaction on the action — the verb-side hold
//        timing lives inside Harvestable / HoldGrabbable / Draggable. The
//        action itself should be a plain button.
//   4. Drag the Interact InputActionReference into `interactAction` on
//      PlayerInteractor.
//   5. For any object that should respond to the player's gaze:
//      - Add a Focusable component (fires focus events)
//      - Add an IInteractable / IHoldInteractable implementation:
//          TAP  → Activatable, Toggleable, Grabbable, BuildingSite
//          HOLD → Harvestable, HoldGrabbable, Draggable
//      - Make sure its collider is on a layer included in the sensor's mask.
//
// Note on input systems
//   This component uses the new Input System (InputActionReference). The
//   project's Active Input Handling (Project Settings → Player) must be
//   "Input System Package (New)" or "Both" for this to fire.
// ============================================================================
