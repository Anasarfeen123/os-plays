using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gridlock.Hub
{
    /// <summary>
    /// One scenario card on the hub monitor. The screen itself is not touchable: the desk keyboard moves a focus
    /// between cards (◄ ► or 1 2 3) and ENTER activates the focused one. Cards slide in during boot; the focused card
    /// lifts and glows; activating launches its scene, or, if locked, shakes and buzzes.
    /// </summary>
    public class HubTile : MonoBehaviour
    {
        [SerializeField] HubScreen screen;
        [SerializeField] string sceneName;
        [SerializeField] bool locked;
        [SerializeField] CanvasGroup group;
        [SerializeField] Image border;
        [SerializeField] Image glow;
        [SerializeField] TMP_Text action;
        [SerializeField] Color accent = new Color(0.35f, 0.85f, 1f);

        RectTransform rect;
        Vector2 home;
        bool focused;
        float focusAmount;
        float shake;
        string actionText;

        public string SceneName => sceneName;
        public bool Locked => locked;
        public bool Revealed { get; private set; }

        public void Init(HubScreen hubScreen, string scene, bool isLocked, CanvasGroup canvasGroup, Image frame, Image halo, TMP_Text actionLabel, Color color)
        {
            screen = hubScreen;
            sceneName = scene;
            locked = isLocked;
            group = canvasGroup;
            border = frame;
            glow = halo;
            action = actionLabel;
            accent = color;
        }

        void Awake()
        {
            rect = (RectTransform)transform;
            home = rect.anchoredPosition;
            group.alpha = 0f;
            group.blocksRaycasts = false; // driven by the desk keyboard, never by pointing at the screen
            actionText = action != null ? action.text : "";
        }

        public IEnumerator Reveal(float delay)
        {
            yield return new WaitForSeconds(delay);
            screen.PlayTick();
            const float seconds = 0.45f;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = 1f - Mathf.Pow(1f - t / seconds, 3f); // ease-out
                group.alpha = k;
                rect.anchoredPosition = home + Vector2.down * (90f * (1f - k));
                yield return null;
            }
            group.alpha = 1f;
            rect.anchoredPosition = home;
            Revealed = true;
        }

        public void SetFocused(bool value) => focused = value;

        /// <summary>ENTER on the keyboard while this card has focus.</summary>
        public void Activate()
        {
            if (!Revealed) return;
            if (locked)
            {
                shake = 0.35f;
                screen.TileDenied(this);
                return;
            }
            screen.TileChosen(this);
        }

        void Update()
        {
            focusAmount = Mathf.MoveTowards(focusAmount, focused ? 1f : 0f, Time.deltaTime * 6f);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 5f);
            float s = 1f + 0.05f * Ease(focusAmount);
            rect.localScale = new Vector3(s, s, 1f);

            var b = Color.Lerp(accent * 0.55f, accent, focusAmount);
            b.a = 1f;
            border.color = b;
            if (glow != null) glow.color = new Color(accent.r, accent.g, accent.b, focusAmount * (0.25f + 0.15f * pulse));
            if (action != null)
                action.text = focusAmount > 0.5f ? (locked ? $"<b>{actionText}</b>" : $"<b>► {actionText}</b>  <size=70%>[ENTER]</size>") : actionText;

            if (shake > 0f)
            {
                shake -= Time.deltaTime;
                rect.anchoredPosition = home + Vector2.right * (Mathf.Sin(shake * 80f) * 14f * shake / 0.35f);
                if (shake <= 0f) rect.anchoredPosition = home;
            }
        }

        static float Ease(float x) => x * x * (3f - 2f * x);
    }
}
