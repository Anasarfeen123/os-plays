
using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.UI;

public class QuizManager : MonoBehaviour
{
    [Header("Question Panels")]
    [SerializeField] private GameObject[] questionPanels = new GameObject[6];

    [Header("Correct Option Index for Each Question")]
    [Tooltip("0 = Option1, 1 = Option2, 2 = Option3, 3 = Option4")]
    [SerializeField] private int[] correctAnswers = { 0, 1, 2, 3, 0, 1 };

    [Header("Score Screen")]
    [SerializeField] private GameObject scoreScreen;
    [SerializeField] private TMP_Text resultTitle;
    [SerializeField] private TMP_Text correctQuestionsText;
    [SerializeField] private TMP_Text incorrectQuestionsText;

    [Header("Start Screen")]
    [SerializeField] private GameObject welcomeScreen;
    [SerializeField] private GameObject quizPanel;

    [Header("Floating Feedback UI")]
    [SerializeField] private TMP_Text feedbackText;
    [SerializeField] private Image feedbackBackground;
    [SerializeField] private float feedbackDuration = 2.5f;

    [Header("Feedback Colors")]
    [SerializeField]
    private Color correctColor = new Color(0.05f, 0.45f, 0.20f, 0.95f);

    [SerializeField]
    private Color incorrectColor = new Color(0.65f, 0.08f, 0.10f, 0.95f);

    [SerializeField]
    private Color normalColor = new Color(0.04f, 0.07f, 0.13f, 0.95f);

    [Header("Quiz Answer Sounds")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip correctAnswerSound;
    [SerializeField] private AudioClip incorrectAnswerSound;

    [Header("Final Score Sounds")]
    [SerializeField] private AudioClip failedSound;
    [SerializeField] private AudioClip passedSound;
    [SerializeField] private AudioClip perfectScoreSound1;
    [SerializeField] private AudioClip perfectScoreSound2;

    [Header("Perfect Score Celebration")]
    [SerializeField] private ParticleSystem perfectScoreConfetti;

    private int currentQuestion;
    private int score;
    private bool answered;
    private Coroutine nextQuestionRoutine;

    private readonly List<int> correctQuestions = new();
    private readonly List<int> incorrectQuestions = new();

    private readonly string[] explanations =
    {
        "FIFO means First In, First Out. The page that entered memory earliest is removed first.",
        "FIFO replaces the page that entered memory first, regardless of how recently it was used.",
        "LRU means Least Recently Used. It replaces the page that has not been used for the longest time.",
        "Optimal uses future page references to choose the page whose next use is farthest away, or that will never be used again.",
        "A page fault occurs when a requested page is not currently present in memory.",
        "Page replacement chooses a page to remove when memory frames are full and another page must be loaded."
    };

    private readonly string[] correctOptionTexts =
    {
        "First In First Out",
        "Page that entered first",
        "Least recently used",
        "Optimal",
        "Required page is not in memory",
        "Choose a page to remove when memory needs space"
    };

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    public void StartQuiz()
    {
        if (nextQuestionRoutine != null)
        {
            StopCoroutine(nextQuestionRoutine);
            nextQuestionRoutine = null;
        }

        currentQuestion = 0;
        score = 0;
        answered = false;

        correctQuestions.Clear();
        incorrectQuestions.Clear();

        if (welcomeScreen != null)
            welcomeScreen.SetActive(false);

        if (quizPanel != null)
            quizPanel.SetActive(true);

        if (scoreScreen != null)
            scoreScreen.SetActive(false);

        if (perfectScoreConfetti != null)
        {
            perfectScoreConfetti.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );
        }

        ShowQuestion(0);
        ShowFeedback("Quiz started. Choose your answer!", normalColor);
    }

    public void SelectOption(int optionIndex)
    {
        if (answered || currentQuestion >= questionPanels.Length)
            return;

        if (optionIndex < 0 || optionIndex > 3)
            return;

        if (currentQuestion >= correctAnswers.Length)
            return;

        answered = true;

        bool isCorrect = optionIndex == correctAnswers[currentQuestion];

        if (isCorrect)
        {
            score++;
            correctQuestions.Add(currentQuestion + 1);

            PlaySound(correctAnswerSound);

            ShowFeedback(
                "CORRECT!\n" + explanations[currentQuestion],
                correctColor
            );
        }
        else
        {
            incorrectQuestions.Add(currentQuestion + 1);

            PlaySound(incorrectAnswerSound);

            ShowFeedback(
                "INCORRECT!\nCorrect answer: " +
                correctOptionTexts[currentQuestion] + "\n" +
                explanations[currentQuestion],
                incorrectColor
            );
        }

        nextQuestionRoutine = StartCoroutine(WaitAndContinue());
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }

    private IEnumerator WaitAndContinue()
    {
        yield return new WaitForSecondsRealtime(feedbackDuration);

        nextQuestionRoutine = null;
        NextQuestion();
    }

    private void NextQuestion()
    {
        currentQuestion++;

        if (currentQuestion >= questionPanels.Length)
        {
            ShowResults();
            return;
        }

        ShowQuestion(currentQuestion);
    }

    private void ShowQuestion(int index)
    {
        for (int i = 0; i < questionPanels.Length; i++)
        {
            if (questionPanels[i] != null)
                questionPanels[i].SetActive(i == index);
        }

        answered = false;
    }

    private void ShowFeedback(string message, Color color)
    {
        if (feedbackText != null)
            feedbackText.text = message;

        if (feedbackBackground != null)
            feedbackBackground.color = color;

        Debug.Log(message);
    }

    private void ShowResults()
    {
        for (int i = 0; i < questionPanels.Length; i++)
        {
            if (questionPanels[i] != null)
                questionPanels[i].SetActive(false);
        }

        if (scoreScreen != null)
            scoreScreen.SetActive(true);

        string resultMessage;

        if (score < 4)
        {
            resultMessage = "You Failed Bro!!";
            PlaySound(failedSound);
        }
        else if (score < 6)
        {
            resultMessage = "Passed!";
            PlaySound(passedSound);
        }
        else
        {
            resultMessage = "You Scored Full!!";

            // Play both perfect-score sounds together.
            PlaySound(perfectScoreSound1);
            PlaySound(perfectScoreSound2);

            // Celebrate the perfect score with confetti.
            if (perfectScoreConfetti != null)
            {
                perfectScoreConfetti.Stop(
                    true,
                    ParticleSystemStopBehavior.StopEmittingAndClear
                );

                perfectScoreConfetti.Play();
            }
        }

        if (resultTitle != null)
        {
            resultTitle.text =
                $"Score: {score}/{questionPanels.Length}\n" +
                resultMessage;
        }

        if (correctQuestionsText != null)
        {
            correctQuestionsText.text =
                "Correct: " + FormatQuestions(correctQuestions);
        }

        if (incorrectQuestionsText != null)
        {
            incorrectQuestionsText.text =
                "Incorrect: " + FormatQuestions(incorrectQuestions);
        }

        ShowFeedback(
            $"Quiz complete!\nScore: {score}/{questionPanels.Length}",
            normalColor
        );
    }

    private string FormatQuestions(List<int> questions)
    {
        return questions.Count == 0
            ? "None"
            : "Q" + string.Join(", Q", questions);
    }

    public void ExitQuiz()
    {
        if (nextQuestionRoutine != null)
        {
            StopCoroutine(nextQuestionRoutine);
            nextQuestionRoutine = null;
        }

        CancelInvoke(nameof(NextQuestion));

        if (perfectScoreConfetti != null)
        {
            perfectScoreConfetti.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );
        }

        if (scoreScreen != null)
            scoreScreen.SetActive(false);

        if (quizPanel != null)
            quizPanel.SetActive(false);

        if (welcomeScreen != null)
            welcomeScreen.SetActive(true);

        if (feedbackBackground != null)
            feedbackBackground.color = normalColor;
    }
}
