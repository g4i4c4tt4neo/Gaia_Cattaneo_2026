// ============================================================================
// IInteractable / IHoldInteractable — contracts
//
// Two small contracts, one per dispatch pattern, so each verb's signature
// describes exactly what it does — no dead fields, no "Mode" flag.
//
//   IInteractable      → TAP verbs.  Key-down completes the action.
//                        Examples: Activatable (lever), Toggleable (door),
//                        Grabbable (tap to pick up, tap to release).
//
//   IHoldInteractable  → HOLD verbs. Key-down begins; key-up / look-away
//                        cancels; the verb ticks progress to 1.0 and can
//                        complete on its own.
//                        Examples: Harvestable (chop tree), HoldGrabbable
//                        (carry while held), Draggable (push crate).
//
// IHoldInteractable IS-A IInteractable, so PlayerInteractor only ever does
// one TryGetComponent<IInteractable>() lookup. A runtime type-check
// (`is IHoldInteractable`) decides whether the held-key path is engaged.
//
// Verb-specific data (resource yield, build cost, carry stiffness) lives on
// the implementing component or on a paired verb interface (IHarvestable,
// IBuildable). Keeping interaction dispatch separate from verb data follows
// Interface Segregation — the interactor stays verb-agnostic, and any future
// verb plugs in by picking one of these two contracts.
// ============================================================================

using System;

namespace Ludocore
{
    /// <summary>Contract for any TAP verb — a single key-down dispatches the
    /// action and the verb owns whatever state machine follows.</summary>
    public interface IInteractable
    {
        bool CanInteract { get; }

        /// <summary>Fired once per key-down while CanInteract is true.</summary>
        void BeginInteract();
    }

    /// <summary>Contract for any HOLD verb — key-down begins, key-up (or focus
    /// loss) cancels, and the verb ticks progress 0..1 until it completes.
    /// Inherits IInteractable so PlayerInteractor can resolve both kinds with
    /// one lookup.</summary>
    public interface IHoldInteractable : IInteractable
    {
        /// <summary>Seconds the player must hold to complete the verb. The verb
        /// itself owns the timer — this is exposed for UI / debug.</summary>
        float Duration { get; }

        /// <summary>Fired each frame the hold is in progress, 0..1. Drives the
        /// radial UI. Verbs that complete via external state (e.g. carry until
        /// release) may never invoke this.</summary>
        event Action<float> OnProgress;

        /// <summary>Cancel an in-flight hold. Called by PlayerInteractor on
        /// key-up, target-change, or focus loss.</summary>
        void CancelInteract();
    }
}
