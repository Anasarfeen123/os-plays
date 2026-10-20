// GameChallengeController.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GameChallengeController : MonoBehaviour
{
    // UI References (assign in inspector)
    [Header("Challenge List UI")]
    public Transform challengeListContainer; // parent for challenge buttons
    public GameObject challengeButtonPrefab; // prefab with Button & Text

    [Header("Challenge Detail UI")]
    public Text titleText;
    public Text difficultyBadge;
    public Text algoBadge;
    public Text descriptionText;
    public Transform optionsContainer; // parent for option buttons
    public GameObject optionButtonPrefab; // prefab with Button & Text
    public Text timerText;
    public Button submitButton;

    [Header("Result Modal UI")]
    public GameObject resultModal; // root modal panel (inactive by default)
    public Text resultBannerText; // e.g. "CORRECT!" or "INCORRECT"
    public Text resultScoreText; // earned XP info
    public Text resultYourSeqText; // player sequence
    public Text resultExpectedSeqText; // ground‑truth sequence
    public Text resultExplanationText; // why answer is correct / feedback
    public Transform achievementListContainer; // parent for unlocked achievement items
    public GameObject achievementItemPrefab; // prefab with Text (emoji + title)

    // Internal state
    private SchedulingChallenge _currentChallenge;
    private ChallengeOption _selectedOption;
    private float _elapsedTime;
    private bool _timerRunning;
    private List<Button> _optionButtons = new List<Button>();

    private void Start()
    {
        PopulateChallengeList();
        submitButton.onClick.AddListener(OnSubmit);
        resultModal.SetActive(false);
    }

    private void Update()
    {
        if (_timerRunning)
        {
            _elapsedTime += Time.deltaTime;
            timerText.text = $"TIME: {_elapsedTime:0.0}s";
        }
    }

    #region Challenge List
    private void PopulateChallengeList()
    {
        // Clear any existing children
        foreach (Transform child in challengeListContainer)
            Destroy(child.gameObject);

        var challenges = GameChallengeDatabase.GetChallenges();
        foreach (var ch in challenges)
        {
            if (challengeButtonPrefab == null) continue;
            var btnObj = Instantiate(challengeButtonPrefab, challengeListContainer);
            var btn = btnObj.GetComponent<Button>();
            var txt = btnObj.GetComponentInChildren<Text>();
            if (txt != null) txt.text = ch.Title;
            if (btn != null) btn.onClick.AddListener(() => LoadChallenge(ch.Id));
        }
    }

    private void LoadChallenge(string challengeId)
    {
        var challenge = GameChallengeDatabase.GetChallenges().Find(c => c.Id == challengeId);
        if (challenge == null) return;
        _currentChallenge = challenge;
        _selectedOption = null;
        _elapsedTime = 0f;
        timerText.text = "TIME: 0.0s";
        _timerRunning = true;
        // Populate UI fields
        titleText.text = challenge.Title;
        difficultyBadge.text = $"DIFFICULTY: {challenge.Difficulty}";
        algoBadge.text = $"ALGORITHM: {challenge.AlgorithmName}";
        descriptionText.text = challenge.Description;
        PopulateOptions(challenge);
        // Show the game panel (assumes this script lives on the GamePanel)
        gameObject.SetActive(true);
    }
    #endregion

    #region Options UI
    private void PopulateOptions(SchedulingChallenge challenge)
    {
        foreach (Transform child in optionsContainer)
            Destroy(child.gameObject);
        _optionButtons.Clear();
        foreach (var opt in challenge.Options)
        {
            if (optionButtonPrefab == null) continue;
            var btnObj = Instantiate(optionButtonPrefab, optionsContainer);
            var btn = btnObj.GetComponent<Button>();
            var txt = btnObj.GetComponentInChildren<Text>();
            if (txt != null) txt.text = opt.SequenceLabel;
            if (btn != null) btn.onClick.AddListener(() => OnOptionSelected(opt, btn));
            if (btn != null) _optionButtons.Add(btn);
        }
    }

    private void OnOptionSelected(ChallengeOption option, Button btn)
    {
        _selectedOption = option;
        foreach (var b in _optionButtons)
            b.GetComponent<Image>().color = Color.gray;
        btn.GetComponent<Image>().color = new Color(0.2f, 0.6f, 0.2f);
    }
    #endregion

    #region Submission & Result
    private void OnSubmit()
    {
        if (_currentChallenge == null || _selectedOption == null)
        {
            Debug.LogWarning("No challenge or option selected.");
            return;
        }
        _timerRunning = false;
        var report = ProgressionManager.Instance.EvaluateChallenge(_currentChallenge, _selectedOption, _elapsedTime);
        ShowResult(report);
    }

    private void ShowResult(ChallengeResultReport report)
    {
        resultModal.SetActive(true);
        resultBannerText.text = report.IsCorrect ? "CORRECT!" : "INCORRECT";
        resultScoreText.text = $"EARNED SCORE: +{report.ScoreEarned} XP (Base: {report.BaseXPEarned} XP + Time Bonus: {report.TimeBonusEarned} XP)";
        resultYourSeqText.text = $"YOUR ORDER: {report.SelectedSequence}";
        resultExpectedSeqText.text = $"EXPECTED ORDER: {report.ExpectedSequence}";
        resultExplanationText.text = report.Explanation;
        foreach (Transform child in achievementListContainer)
            Destroy(child.gameObject);
        foreach (var ach in report.NewlyUnlockedAchievements)
        {
            var itemObj = Instantiate(achievementItemPrefab, achievementListContainer);
            var txt = itemObj.GetComponentInChildren<Text>();
            txt.text = $"{ach.IconEmoji} {ach.Title}";
        }
    }
    #endregion
}
