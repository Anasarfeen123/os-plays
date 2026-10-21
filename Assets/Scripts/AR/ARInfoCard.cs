using TMPro;
using UnityEngine;

namespace Gridlock.AR
{
    /// <summary>
    /// A small card on the phone HUD that says what was just tapped in the model and how it stands ("Process P2 · burst 6 ·
    /// waiting"), fading out after a few seconds or when something else is tapped.
    /// </summary>
    public class ARInfoCard : MonoBehaviour
    {
        [SerializeField] CanvasGroup group;
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text body;
        [SerializeField] float seconds = 6f;

        float shownAt = float.NegativeInfinity;

        public bool Showing => group.alpha > 0.01f;
        public string Title => title.text;
        public string Body => body.text;

        public void Init(CanvasGroup canvasGroup, TMP_Text titleText, TMP_Text bodyText)
        {
            group = canvasGroup;
            title = titleText;
            body = bodyText;
        }

        void Awake()
        {
            group.alpha = 0f;
            group.blocksRaycasts = false;
        }

        public void Show(string heading, string text)
        {
            title.text = heading;
            body.text = text;
            shownAt = Time.unscaledTime;
        }

        public void Hide() => shownAt = float.NegativeInfinity;

        void Update()
        {
            float age = Time.unscaledTime - shownAt;
            float target = age < seconds ? 1f : 0f;
            group.alpha = Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime * (target > 0f ? 8f : 2.5f));
        }
    }
}
