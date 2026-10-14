using Gridlock.Deadlock;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Gridlock.XR
{
    /// <summary>
    /// Point-to-inspect and select target on a car or a parking space (spec §13). XR rays hover/select it through
    /// <see cref="XRSimpleInteractable"/>; the desktop camera calls <see cref="SetHovered"/>/<see cref="Click"/> from a mouse raycast.
    /// </summary>
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class Inspectable : MonoBehaviour
    {
        public enum Kind { Car, Space }

        [SerializeField] Kind kind;
        [SerializeField] string id;
        [SerializeField] AnalysisOverlay overlay;
        [SerializeField] PlayerInteraction interaction;

        XRSimpleInteractable interactable;
        int hoverCount;

        public void Init(Kind targetKind, string targetId, AnalysisOverlay analysis)
        {
            kind = targetKind;
            id = targetId;
            overlay = analysis;
        }

        void Awake()
        {
            interactable = GetComponent<XRSimpleInteractable>();
            interactable.hoverEntered.AddListener(OnHoverEntered);
            interactable.hoverExited.AddListener(OnHoverExited);
            interactable.selectEntered.AddListener(OnSelectEntered);
        }

        void OnDestroy()
        {
            interactable.hoverEntered.RemoveListener(OnHoverEntered);
            interactable.hoverExited.RemoveListener(OnHoverExited);
            interactable.selectEntered.RemoveListener(OnSelectEntered);
        }

        void OnHoverEntered(HoverEnterEventArgs _) => SetHovered(true);
        void OnHoverExited(HoverExitEventArgs _) => SetHovered(false);
        void OnSelectEntered(SelectEnterEventArgs _) => Click();

        /// <summary>Select this car/bay (XR select or desktop left click).</summary>
        public void Click()
        {
            if (interaction != null) interaction.Select(kind, id);
        }

        /// <summary>Hover counts are reference-counted so two controllers on one target behave.</summary>
        public void SetHovered(bool hovered)
        {
            hoverCount = Mathf.Max(0, hoverCount + (hovered ? 1 : -1));
            if (hoverCount > 0)
                overlay.Inspect(kind == Kind.Car ? id : null, kind == Kind.Space ? id : null);
            else
                overlay.Inspect(null, null);
        }
    }
}
