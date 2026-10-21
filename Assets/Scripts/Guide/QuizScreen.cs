using System;
using Gridlock.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gridlock.Guide
{
    /// <summary>
    /// The quick check after a guided tour, on the guide screen's spot (docked in the HUD, floating in a headset): a
    /// question with three answers; a pick turns green or red, the explanation shows, NEXT moves on; then the score.
    /// The tour drives every copy of the screen at once, so a headset and its spectator view stay in step.
    /// </summary>
    public class QuizScreen : MonoBehaviour
    {
        [SerializeField] NarratedTour tour;
        [SerializeField] GameObject panel;
        [SerializeField] TMP_Text header;
        [SerializeField] TMP_Text question;
        [SerializeField] Button[] options;
        [SerializeField] TMP_Text[] optionLabels;
        [SerializeField] TMP_Text feedback;
        [SerializeField] Button next;
        [SerializeField] TMP_Text nextLabel;

        static readonly Color Neutral = new Color(0.16f, 0.21f, 0.32f);
        const float ScoreHeight = 320f; // the score takes the answers' space

        float questionHeight;

        public bool Showing => panel.activeSelf;

        public void Init(NarratedTour narratedTour, GameObject panelRoot, TMP_Text headerText, TMP_Text questionText, Button[] optionButtons,
                         TMP_Text[] optionTexts, TMP_Text feedbackText, Button nextButton, TMP_Text nextText)
        {
            tour = narratedTour;
            panel = panelRoot;
            header = headerText;
            question = questionText;
            options = optionButtons;
            optionLabels = optionTexts;
            feedback = feedbackText;
            next = nextButton;
            nextLabel = nextText;
        }

        void Awake()
        {
            for (int i = 0; i < options.Length; i++)
            {
                int choice = i;
                options[i].onClick.AddListener(() => tour.Answer(choice));
            }
            next.onClick.AddListener(tour.QuizNext);
            questionHeight = question.rectTransform.sizeDelta.y;
            panel.SetActive(false);
        }

        public void ShowQuestion(NarratedTour.QuizQuestion q, int number, int count)
        {
            panel.SetActive(true);
            header.text = $"QUICK CHECK  ·  {number} / {count}";
            question.enableAutoSizing = true;
            question.alignment = TextAlignmentOptions.TopLeft;
            question.rectTransform.sizeDelta = new Vector2(question.rectTransform.sizeDelta.x, questionHeight);
            question.text = q.question;
            for (int i = 0; i < options.Length; i++)
            {
                bool used = i < q.options.Length;
                options[i].gameObject.SetActive(used);
                if (!used) continue;
                optionLabels[i].text = q.options[i];
                options[i].interactable = true;
                options[i].GetComponent<Image>().color = Neutral;
            }
            feedback.text = "";
            next.gameObject.SetActive(false);
        }

        public void ShowAnswer(NarratedTour.QuizQuestion q, int chosen, bool last)
        {
            for (int i = 0; i < q.options.Length; i++)
            {
                options[i].interactable = false;
                var image = options[i].GetComponent<Image>();
                if (i == q.correct) image.color = ViewColor.Safe;
                else if (i == chosen) image.color = ViewColor.Deadlock;
            }
            bool right = chosen == q.correct;
            feedback.text = (right ? "<color=#22C55E><b>Right.</b></color> " : "<color=#EF4444><b>Not quite.</b></color> ") + q.why;
            next.gameObject.SetActive(true);
            nextLabel.text = last ? "SCORE ►" : "NEXT ►";
        }

        public void ShowScore(int score, int count, int best)
        {
            header.text = "QUICK CHECK  ·  DONE";
            foreach (var option in options) option.gameObject.SetActive(false);
            // The score, big, in the middle of where the answers were.
            question.enableAutoSizing = false;
            question.fontSize = 96f;
            question.alignment = TextAlignmentOptions.Center;
            var colour = score == count ? "#22C55E" : score == 0 ? "#EF4444" : "#FBBF24";
            question.text = $"<color={colour}>{score} / {count}</color>\n<size=40%>{(score == count ? "All right!" : score == 0 ? "Have another look at the tour" : "Nearly there")}</size>";
            question.rectTransform.sizeDelta = new Vector2(question.rectTransform.sizeDelta.x, ScoreHeight);
            feedback.text = best > score ? $"Your best so far: {best} / {count}. The control room keeps your progress."
                                         : "The control room keeps your progress.";
            next.gameObject.SetActive(true);
            nextLabel.text = "CONTINUE ►";
        }

        public void Hide() => panel.SetActive(false);
    }
}
