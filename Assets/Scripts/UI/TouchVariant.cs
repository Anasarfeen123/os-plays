using Gridlock.XR;
using TMPro;
using UnityEngine;

namespace Gridlock.UI
{
    /// <summary>
    /// HUD element that differs on a phone: swaps in touch wording (the help bar), or exists only on touch screens
    /// (the hub's on-screen keypad) or only on desktop.
    /// </summary>
    public class TouchVariant : MonoBehaviour
    {
        public enum Show { Always, TouchOnly, DesktopOnly }

        [SerializeField] Show show;
        [TextArea] [SerializeField] string touchText;

        public void Init(Show when, string textOnTouch)
        {
            show = when;
            touchText = textOnTouch;
        }

        void Awake()
        {
            bool touch = TouchGestures.IsTouchDevice;
            if ((show == Show.TouchOnly && !touch) || (show == Show.DesktopOnly && touch))
            {
                gameObject.SetActive(false);
                return;
            }
            if (touch && !string.IsNullOrEmpty(touchText) && TryGetComponent<TMP_Text>(out var text)) text.text = touchText;
        }
    }
}
