using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gridlock.Guide
{
    /// <summary>
    /// A tour's floating screen: step counter, title, live subtitles (spoken words light up as the narrator says them),
    /// a progress bar, NEXT / SKIP, up to two contextual action buttons for the hands-on step (TOW CAR, GRANT / DENY),
    /// and REPLAY / mode switch / CONTINUE at the end. Collapses to a small "GUIDED TOUR" pill when the tour is not running.
    /// One copy floats in XR, one is docked on the desktop.
    /// </summary>
    public class GuideScreen : MonoBehaviour
    {
        /// <summary>A hands-on button the tour puts on the screen.</summary>
        public readonly struct Action
        {
            public readonly string Label;
            public readonly Color Color;
            public readonly System.Action OnClick;
            public readonly bool Interactable;

            public Action(string label, Color color, System.Action onClick, bool interactable = true)
            {
                Label = label;
                Color = color;
                OnClick = onClick;
                Interactable = interactable;
            }
        }

        [SerializeField] NarratedTour tour;
        [SerializeField] GameObject panel;
        [SerializeField] GameObject pill;
        [SerializeField] TMP_Text stepLabel;
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text caption;
        [SerializeField] Image progress;
        [SerializeField] GameObject runningButtons;
        [SerializeField] Button nextButton;
        [SerializeField] Button skipButton;
        [SerializeField] Button[] actionButtons;
        [SerializeField] TMP_Text[] actionLabels;
        [SerializeField] GameObject endButtons;
        [SerializeField] Button replayButton;
        [SerializeField] Button closeButton;
        [SerializeField] Button pillButton;

        readonly System.Action[] actionCallbacks = new System.Action[2];
        string text = "";
        float spokenUntil = -1f;

        public void Init(NarratedTour narratedTour, GameObject panelRoot, GameObject pillRoot, TMP_Text step, TMP_Text titleText,
                         TMP_Text captionText, Image progressFill, GameObject running, Button next, Button skip, Button[] actions,
                         TMP_Text[] actionTexts, GameObject end, Button replay, Button close, Button pillBtn)
        {
            tour = narratedTour;
            panel = panelRoot;
            pill = pillRoot;
            stepLabel = step;
            title = titleText;
            caption = captionText;
            progress = progressFill;
            runningButtons = running;
            nextButton = next;
            skipButton = skip;
            actionButtons = actions;
            actionLabels = actionTexts;
            endButtons = end;
            replayButton = replay;
            closeButton = close;
            pillButton = pillBtn;
        }

        void Awake()
        {
            nextButton.onClick.AddListener(tour.Next);
            skipButton.onClick.AddListener(tour.Skip);
            replayButton.onClick.AddListener(tour.Replay);
            closeButton.onClick.AddListener(Collapse);
            pillButton.onClick.AddListener(tour.Replay);
            for (int i = 0; i < actionButtons.Length; i++)
            {
                int index = i;
                actionButtons[i].onClick.AddListener(() => actionCallbacks[index]?.Invoke());
            }
            HideActions();
            Collapse();
        }

        public void ShowStep(NarratedTour.Step step, int number, int count)
        {
            panel.SetActive(true);
            pill.SetActive(false);
            runningButtons.SetActive(true);
            endButtons.SetActive(false);
            stepLabel.text = $"GUIDED TOUR   ·   {number} / {count}";
            title.text = step.title;
            text = step.text;
            spokenUntil = -1f;
            SetSpoken(0f);
        }

        /// <summary>0..1 of the current line spoken: words already said are bright, the rest dim.</summary>
        public void SetSpoken(float fraction)
        {
            fraction = Mathf.Clamp01(fraction);
            bool subtitles = Gridlock.Core.GameSettings.IsOn(Gridlock.Core.Setting.Subtitles);
            if (caption.gameObject.activeSelf != subtitles) caption.gameObject.SetActive(subtitles);
            if (Mathf.Abs(fraction - spokenUntil) < 0.005f) return;
            spokenUntil = fraction;
            int cut = Mathf.RoundToInt(text.Length * fraction);
            while (cut < text.Length && cut > 0 && text[cut - 1] != ' ') cut++; // whole words only
            caption.text = cut >= text.Length
                ? text
                : $"{text.Substring(0, cut)}<color=#64748B>{text.Substring(cut)}</color>";
        }

        public void SetProgress(float fraction) => progress.fillAmount = Mathf.Clamp01(fraction);

        public void ShowActions(Action[] actions)
        {
            for (int i = 0; i < actionButtons.Length; i++)
            {
                bool used = i < actions.Length;
                actionButtons[i].gameObject.SetActive(used);
                actionCallbacks[i] = used ? actions[i].OnClick : null;
                if (!used) continue;
                actionButtons[i].image.color = actions[i].Color;
                actionLabels[i].text = actions[i].Label;
                actionButtons[i].interactable = actions[i].Interactable;
            }
        }

        public void UpdateAction(int index, string label, bool interactable)
        {
            if (index >= actionButtons.Length) return;
            actionLabels[index].text = label;
            actionButtons[index].interactable = interactable;
        }

        public void HideActions()
        {
            for (int i = 0; i < actionButtons.Length; i++)
            {
                actionButtons[i].gameObject.SetActive(false);
                actionCallbacks[i] = null;
            }
        }

        public void ShowEnd()
        {
            panel.SetActive(true);
            pill.SetActive(false);
            runningButtons.SetActive(false);
            HideActions();
            endButtons.SetActive(true);
            SetSpoken(1f);
            SetProgress(1f);
        }

        public void Collapse()
        {
            panel.SetActive(false);
            pill.SetActive(true);
        }
    }
}
