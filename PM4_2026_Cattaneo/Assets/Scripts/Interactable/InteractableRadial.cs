// ============================================================================
// InteractableRadial — Presentation layer (generic)
//
// Drives a UI Image's radial fill from the OnProgress event of any sibling
// IHoldInteractable. Drop on any prefab whose interaction is hold-flavored
// (Harvestable, HoldGrabbable, Draggable, etc.) and the radial just works.
//
// Verb-agnostic by design — the only thing this script knows is "fill an
// image from 0..1 driven by IHoldInteractable.OnProgress". Show/hide of the
// prompt label is the verb-UI's job; this is purely the progress ring.
//
// Tap verbs (IInteractable only) have no progress to report, so dropping this
// on an Activatable / BuildingSite / Grabbable does nothing — GetComponent
// returns null and the radial stays hidden. Safe but pointless on tap verbs.
// ============================================================================

using UnityEngine;
using UnityEngine.UI;

namespace Ludocore
{
    /// <summary>Generic radial-fill UI for any sibling IHoldInteractable. The Image
    /// must be set to Type=Filled, Fill Method=Radial 360 in the inspector.</summary>
    public class InteractableRadial : MonoBehaviour
    {
        //==================== CONFIG =====================
        [Header("Config")]
        [Tooltip("UI Image used as the radial. Set Image Type = Filled, Fill Method = Radial 360.")]
        [SerializeField] private Image radialFill;

        //==================== STATE =====================
        private IHoldInteractable _interactable;

        //==================== LIFECYCLE =====================
        private void Awake()
        {
            _interactable = GetComponent<IHoldInteractable>();
            if (radialFill)
            {
                radialFill.fillAmount = 0f;
                radialFill.gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (_interactable != null) _interactable.OnProgress += HandleProgress;
        }

        private void OnDisable()
        {
            if (_interactable != null) _interactable.OnProgress -= HandleProgress;
        }

        //==================== PRIVATE =====================
        private void HandleProgress(float progress)
        {
            if (!radialFill) return;
            radialFill.fillAmount = progress;
            radialFill.gameObject.SetActive(progress > 0f);
        }
    }
}

// ============================================================================
// Setup in a scene
//   1. Add this component to a GameObject that has a sibling IHoldInteractable
//      implementation (e.g. Harvestable). Has no effect on tap-only verbs.
//   2. Under the world-space Canvas (the one HarvestableUI's prompt lives on),
//      add a child UI Image. In the Image inspector:
//      - Image Type   = Filled
//      - Fill Method  = Radial 360
//      - Fill Origin  = Top (or whatever feels right)
//      - Clockwise    = on (optional)
//   3. Wire that Image into the radialFill field.
//   4. Done — the radial fills as the player holds the interact key, hides
//      when canceled, and fills full on completion just before destroy.
// ============================================================================
