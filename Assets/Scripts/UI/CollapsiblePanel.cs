using Gridlock.XR;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gridlock.UI
{
    /// <summary>
    /// A HUD panel that slides off the screen edge behind a small tab, so on a phone (where the side panels would cover
    /// half of an AR model) the player can fold it away and bring it back with one tap. On a monitor the tab is hidden.
    /// </summary>
    public class CollapsiblePanel : MonoBehaviour
    {
        [SerializeField] RectTransform panel;
        [Tooltip("How far the panel slides to fold away (+x = to the right).")]
        [SerializeField] float slide;
        [SerializeField] Button tab;
        [SerializeField] TMP_Text tabLabel;
        [SerializeField] string foldGlyph = "►";
        [SerializeField] string unfoldGlyph = "◄";
        [SerializeField] float seconds = 0.25f;

        Vector2 home;
        bool folded;
        float amount; // 0 = shown, 1 = folded away

        public bool Folded => folded;

        public void Init(RectTransform target, float slideBy, Button tabButton, TMP_Text label, string foldText, string unfoldText)
        {
            panel = target;
            slide = slideBy;
            tab = tabButton;
            tabLabel = label;
            foldGlyph = foldText;
            unfoldGlyph = unfoldText;
        }

        void Awake()
        {
            home = panel.anchoredPosition;
            bool touch = TouchGestures.IsTouchDevice;
            tab.gameObject.SetActive(touch);
            tab.onClick.AddListener(Toggle);
            UpdateLabel();
        }

        public void Toggle() => SetFolded(!folded);

        public void SetFolded(bool value)
        {
            folded = value;
            UpdateLabel();
        }

        void UpdateLabel()
        {
            if (tabLabel != null) tabLabel.text = folded ? unfoldGlyph : foldGlyph;
        }

        void Update()
        {
            float target = folded ? 1f : 0f;
            if (Mathf.Approximately(amount, target)) return;
            amount = Mathf.MoveTowards(amount, target, Time.unscaledDeltaTime / seconds);
            float ease = amount * amount * (3f - 2f * amount);
            panel.anchoredPosition = home + new Vector2(slide * ease, 0f);
        }
    }
}
