using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GameUIController : MonoBehaviour
{
    public static GameUIController Instance { get; private set; }

    [Header("Main Panels")]
    [SerializeField] private GameObject gameRootPanel;
    [SerializeField] private GameObject arenaPanel;
    [SerializeField] private GameObject resultModalPanel;
    [SerializeField] private GameObject achievementsModalPanel;

    [Header("Profile / Progression Header")]
    [SerializeField] private Text levelRankText;
    [SerializeField] private Text xpScoreText;
    [SerializeField] private Text accuracyText;
    [SerializeField] private Button achievementsButton;

    [Header("Challenge List Sidebar")]
    [SerializeField] private Button allDiffButton;
    [SerializeField] private Button easyDiffButton;
    [SerializeField] private Button medDiffButton;
    [SerializeField] private Button hardDiffButton;
    [SerializeField] private Button expertDiffButton;
    [SerializeField] private Transform challengeListContainer;
    [SerializeField] private GameObject challengeButtonPrefab;

    [Header("Challenge Arena Elements")]
    [SerializeField] private Text challengeTitleText;
    [SerializeField] private Text challengeDifficultyBadge;
    [SerializeField] private Text challengeAlgoBadge;
    [SerializeField] private Text challengeDescText;
    [SerializeField] private Transform processTableContainer;
    [SerializeField] private GameObject processRowPrefab;
    [SerializeField] private Transform optionsContainer;
    [SerializeField] private GameObject optionButtonPrefab;
    [SerializeField] private Text timerText;
    [SerializeField] private Button submitButton;

    [Header("Result / Feedback Modal Elements")]
    [SerializeField] private Image resultBannerBg;
    [SerializeField] private Text resultBannerTitle;
    [SerializeField] private Text scoreBreakdownText;
    [SerializeField] private Text yourSequenceText;
    [SerializeField] private Text expectedSequenceText;
    [SerializeField] private Text metricsComparisonText;
    [SerializeField] private Text detailedExplanationText;
    [SerializeField] private Text achievementNoticeText;
    [SerializeField] private Button nextChallengeButton;
    [SerializeField] private Button retryChallengeButton;
    [SerializeField] private Button inspectInSimButton;
    [SerializeField] private Button closeResultButton;

    [Header("Achievements Modal Elements")]
    [SerializeField] private Transform achievementsContainer;
    [SerializeField] private GameObject achievementCardPrefab;
    [SerializeField] private Button closeAchievementsButton;

    // State
    private List<SchedulingChallenge> allChallenges;
    private ChallengeDifficulty? currentDifficultyFilter = null;
    private SchedulingChallenge activeChallenge = null;
    private ChallengeOption selectedOption = null;
    private float challengeStartTime = 0f;
    private bool isTimerRunning = false;

    private List<GameObject> challengeBtnPool = new List<GameObject>();
    private List<GameObject> processRowPool = new List<GameObject>();
    private List<GameObject> optionBtnPool = new List<GameObject>();
    private List<GameObject> achievementCardPool = new List<GameObject>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(gameObject);
    }

    private void Start()
    {
        allChallenges = GameChallengeDatabase.GetChallenges();

        if (ProgressionManager.Instance != null)
        {
            ProgressionManager.Instance.OnProgressUpdated += UpdateProfileHeader;
        }

        SetupButtonListeners();
        UpdateProfileHeader();
        FilterChallenges(null);
        if (allChallenges.Count > 0)
        {
            LoadChallenge(allChallenges[0]);
        }
    }

    private void OnDestroy()
    {
        if (ProgressionManager.Instance != null)
        {
            ProgressionManager.Instance.OnProgressUpdated -= UpdateProfileHeader;
        }
    }

    private void Update()
    {
        if (isTimerRunning && timerText != null)
        {
            float elapsed = Time.time - challengeStartTime;
            int bonus = Mathf.Max(0, Mathf.RoundToInt((30f - elapsed) * 5f));
            timerText.text = $"⏱️ TIME: {elapsed:F1}s  |  TIME BONUS: +{bonus} XP";
        }
    }

    private void SetupButtonListeners()
    {
        if (allDiffButton != null) allDiffButton.onClick.AddListener(() => FilterChallenges(null));
        if (easyDiffButton != null) easyDiffButton.onClick.AddListener(() => FilterChallenges(ChallengeDifficulty.Easy));
        if (medDiffButton != null) medDiffButton.onClick.AddListener(() => FilterChallenges(ChallengeDifficulty.Medium));
        if (hardDiffButton != null) hardDiffButton.onClick.AddListener(() => FilterChallenges(ChallengeDifficulty.Hard));
        if (expertDiffButton != null) expertDiffButton.onClick.AddListener(() => FilterChallenges(ChallengeDifficulty.Expert));

        if (submitButton != null) submitButton.onClick.AddListener(SubmitAnswer);
        if (achievementsButton != null) achievementsButton.onClick.AddListener(OpenAchievementsModal);
        if (closeAchievementsButton != null) closeAchievementsButton.onClick.AddListener(CloseAchievementsModal);

        if (nextChallengeButton != null) nextChallengeButton.onClick.AddListener(GoToNextChallenge);
        if (retryChallengeButton != null) retryChallengeButton.onClick.AddListener(RetryCurrentChallenge);
        if (inspectInSimButton != null) inspectInSimButton.onClick.AddListener(InspectInSimulator);
        if (closeResultButton != null) closeResultButton.onClick.AddListener(() => resultModalPanel.SetActive(false));
    }

    public void OpenGame()
    {
        if (gameRootPanel != null)
        {
            gameRootPanel.SetActive(true);
            UpdateProfileHeader();
            RebuildChallengeList();
        }
    }

    public void CloseGame()
    {
        if (gameRootPanel != null)
        {
            gameRootPanel.SetActive(false);
        }
    }

    public void FilterChallenges(ChallengeDifficulty? difficulty)
    {
        currentDifficultyFilter = difficulty;
        RebuildChallengeList();
    }

    private void RebuildChallengeList()
    {
        if (challengeListContainer == null || challengeButtonPrefab == null) return;

        var list = (currentDifficultyFilter == null)
            ? allChallenges
            : allChallenges.FindAll(c => c.Difficulty == currentDifficultyFilter.Value);

        EnsurePool(challengeBtnPool, challengeButtonPrefab, challengeListContainer, list.Count);

        for (int i = 0; i < list.Count; i++)
        {
            var ch = list[i];
            var btnObj = challengeBtnPool[i];
            btnObj.SetActive(true);

            var txt = btnObj.GetComponentInChildren<Text>();
            if (txt != null)
            {
                string status = ch.IsCompleted ? "✅ " : "⚪ ";
                txt.text = $"{status}[{ch.Difficulty}] {ch.Title}";
            }

            var btn = btnObj.GetComponent<Button>();
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => LoadChallenge(ch));

            var img = btnObj.GetComponent<Image>();
            if (img != null)
            {
                img.color = (ch == activeChallenge) ? new Color(0.20f, 0.45f, 0.85f) : new Color(0.12f, 0.16f, 0.24f);
            }
        }

        for (int i = list.Count; i < challengeBtnPool.Count; i++)
        {
            challengeBtnPool[i].SetActive(false);
        }
    }

    public void LoadChallenge(SchedulingChallenge challenge)
    {
        if (challenge == null) return;
        activeChallenge = challenge;
        selectedOption = null;

        if (resultModalPanel != null) resultModalPanel.SetActive(false);

        // Header details
        if (challengeTitleText != null) challengeTitleText.text = challenge.Title.ToUpper();
        if (challengeDifficultyBadge != null)
        {
            challengeDifficultyBadge.text = $"DIFFICULTY: {challenge.Difficulty.ToString().ToUpper()}";
            challengeDifficultyBadge.color = GetDifficultyColor(challenge.Difficulty);
        }
        if (challengeAlgoBadge != null) challengeAlgoBadge.text = $"ALGORITHM: {challenge.AlgorithmName.ToUpper()}";
        if (challengeDescText != null) challengeDescText.text = challenge.Description;

        // Process Table
        RenderProcessTable(challenge.Processes);

        // Options
        RenderOptions(challenge.Options);

        // Reset timer
        challengeStartTime = Time.time;
        isTimerRunning = true;

        RebuildChallengeList();
    }

    private void RenderProcessTable(List<OSProcess> processes)
    {
        if (processTableContainer == null || processRowPrefab == null || processes == null) return;

        EnsurePool(processRowPool, processRowPrefab, processTableContainer, processes.Count);

        for (int i = 0; i < processes.Count; i++)
        {
            var p = processes[i];
            var rowObj = processRowPool[i];
            rowObj.SetActive(true);

            var img = rowObj.GetComponent<Image>();
            if (img != null) img.color = (i % 2 == 0) ? new Color(0.12f, 0.15f, 0.22f) : new Color(0.09f, 0.12f, 0.18f);

            var txt = rowObj.GetComponentInChildren<Text>();
            if (txt != null)
            {
                txt.text = string.Format("{0,-5} | Arrival: {1,2} | Burst: {2,2} | Priority: {3,2}",
                    p.Id, p.ArrivalTime, p.BurstTime, p.Priority);
            }
        }

        for (int i = processes.Count; i < processRowPool.Count; i++)
        {
            processRowPool[i].SetActive(false);
        }
    }

    private void RenderOptions(List<ChallengeOption> options)
    {
        if (optionsContainer == null || optionButtonPrefab == null || options == null) return;

        EnsurePool(optionBtnPool, optionButtonPrefab, optionsContainer, options.Count);

        for (int i = 0; i < options.Count; i++)
        {
            var opt = options[i];
            var btnObj = optionBtnPool[i];
            btnObj.SetActive(true);

            var txt = btnObj.GetComponentInChildren<Text>();
            if (txt != null) txt.text = opt.SequenceLabel;

            var img = btnObj.GetComponent<Image>();
            if (img != null) img.color = (opt == selectedOption) ? new Color(0.22f, 0.55f, 0.95f) : new Color(0.14f, 0.18f, 0.26f);

            var btn = btnObj.GetComponent<Button>();
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                selectedOption = opt;
                RefreshOptionSelection();
            });
        }

        for (int i = options.Count; i < optionBtnPool.Count; i++)
        {
            optionBtnPool[i].SetActive(false);
        }
    }

    private void RefreshOptionSelection()
    {
        if (activeChallenge == null) return;
        for (int i = 0; i < activeChallenge.Options.Count && i < optionBtnPool.Count; i++)
        {
            var opt = activeChallenge.Options[i];
            var img = optionBtnPool[i].GetComponent<Image>();
            if (img != null)
            {
                img.color = (opt == selectedOption) ? new Color(0.22f, 0.55f, 0.95f) : new Color(0.14f, 0.18f, 0.26f);
            }
        }
    }

    public void SubmitAnswer()
    {
        if (activeChallenge == null || selectedOption == null) return;

        isTimerRunning = false;
        float elapsed = Time.time - challengeStartTime;

        var prog = ProgressionManager.Instance;
        ChallengeResultReport report = null;

        if (prog != null)
        {
            report = prog.EvaluateChallenge(activeChallenge, selectedOption, elapsed);
        }

        ShowResultModal(report);
        RebuildChallengeList();
    }

    private void ShowResultModal(ChallengeResultReport report)
    {
        if (resultModalPanel == null || report == null) return;
        resultModalPanel.SetActive(true);

        if (report.IsCorrect)
        {
            if (resultBannerBg != null) resultBannerBg.color = new Color(0.10f, 0.38f, 0.20f, 0.98f);
            if (resultBannerTitle != null)
            {
                resultBannerTitle.text = "CORRECT SCHEDULE! DISPATCH MASTER 🎉";
                resultBannerTitle.color = new Color(0.35f, 0.95f, 0.5f);
            }
            if (scoreBreakdownText != null)
            {
                scoreBreakdownText.text = $"EARNED SCORE: +{report.ScoreEarned} XP (Base: {report.BaseXPEarned} XP + Time Bonus: {report.TimeBonusEarned} XP)";
            }
        }
        else
        {
            if (resultBannerBg != null) resultBannerBg.color = new Color(0.45f, 0.15f, 0.18f, 0.98f);
            if (resultBannerTitle != null)
            {
                resultBannerTitle.text = "LEARNING OPPORTUNITY 💡";
                resultBannerTitle.color = new Color(1.0f, 0.45f, 0.45f);
            }
            if (scoreBreakdownText != null)
            {
                scoreBreakdownText.text = $"ATTEMPT SCORE: +{report.ScoreEarned} XP (Review the step-by-step logic below!)";
            }
        }

        if (yourSequenceText != null) yourSequenceText.text = $"YOUR ORDER: {report.SelectedSequence}";
        if (expectedSequenceText != null) expectedSequenceText.text = $"GROUND TRUTH: {report.ExpectedSequence}";

        if (metricsComparisonText != null && report.ExpectedEngineResult != null)
        {
            var res = report.ExpectedEngineResult;
            metricsComparisonText.text = $"Avg Waiting: {res.AverageWaitingTime:F2} | Avg Turnaround: {res.AverageTurnaroundTime:F2} | Avg Response: {res.AverageResponseTime:F2} | CPU Util: {res.CpuUtilization:F1}%";
        }

        if (detailedExplanationText != null) detailedExplanationText.text = $"WHY THIS IS CORRECT:\n{report.Explanation}";

        if (achievementNoticeText != null)
        {
            if (report.NewlyUnlockedAchievements.Count > 0)
            {
                var ach = report.NewlyUnlockedAchievements[0];
                achievementNoticeText.text = $"🏆 UNLOCKED ACHIEVEMENT: {ach.IconEmoji} {ach.Title} — {ach.Description}";
                achievementNoticeText.gameObject.SetActive(true);
            }
            else
            {
                achievementNoticeText.gameObject.SetActive(false);
            }
        }
    }

    public void GoToNextChallenge()
    {
        if (activeChallenge == null) return;
        int idx = allChallenges.IndexOf(activeChallenge);
        if (idx >= 0 && idx < allChallenges.Count - 1)
        {
            LoadChallenge(allChallenges[idx + 1]);
        }
    }

    public void RetryCurrentChallenge()
    {
        if (activeChallenge != null)
        {
            LoadChallenge(activeChallenge);
        }
    }

    public void InspectInSimulator()
    {
        if (activeChallenge == null) return;

        // Switch to simulator with this challenge's processes and algorithm!
        if (SchedulingUIController.Instance != null && SimulationController.Instance != null)
        {
            SchedulingUIController.Instance.ShowSimulatorMode();
            SimulationController.Instance.SetProcesses(activeChallenge.Processes);

            // Select matching algorithm
            if (activeChallenge.AlgorithmName.Contains("FCFS")) SchedulingUIController.Instance.SelectAlgorithmByIndex(0);
            else if (activeChallenge.AlgorithmName.Contains("SJF")) SchedulingUIController.Instance.SelectAlgorithmByIndex(1);
            else if (activeChallenge.AlgorithmName.Contains("SRTF")) SchedulingUIController.Instance.SelectAlgorithmByIndex(2);
            else if (activeChallenge.AlgorithmName.Contains("Priority")) SchedulingUIController.Instance.SelectAlgorithmByIndex(3);
            else if (activeChallenge.AlgorithmName.Contains("Round Robin") || activeChallenge.AlgorithmName.Contains("RR")) SchedulingUIController.Instance.SelectAlgorithmByIndex(4);
            else if (activeChallenge.AlgorithmName.Contains("MLQ")) SchedulingUIController.Instance.SelectAlgorithmByIndex(5);
            else if (activeChallenge.AlgorithmName.Contains("MLFQ")) SchedulingUIController.Instance.SelectAlgorithmByIndex(6);
        }
    }

    public void OpenAchievementsModal()
    {
        if (achievementsModalPanel == null || ProgressionManager.Instance == null) return;
        achievementsModalPanel.SetActive(true);

        var list = ProgressionManager.Instance.Achievements;
        EnsurePool(achievementCardPool, achievementCardPrefab, achievementsContainer, list.Count);

        for (int i = 0; i < list.Count; i++)
        {
            var ach = list[i];
            var cardObj = achievementCardPool[i];
            cardObj.SetActive(true);

            var img = cardObj.GetComponent<Image>();
            if (img != null) img.color = ach.IsUnlocked ? new Color(0.12f, 0.25f, 0.18f) : new Color(0.12f, 0.14f, 0.20f);

            var txts = cardObj.GetComponentsInChildren<Text>();
            if (txts.Length >= 2)
            {
                txts[0].text = $"{ach.IconEmoji} {ach.Title} {(ach.IsUnlocked ? "[UNLOCKED]" : "[LOCKED]")}";
                txts[1].text = ach.Description;
            }
        }

        for (int i = list.Count; i < achievementCardPool.Count; i++)
        {
            achievementCardPool[i].SetActive(false);
        }
    }

    public void CloseAchievementsModal()
    {
        if (achievementsModalPanel != null) achievementsModalPanel.SetActive(false);
    }

    private void UpdateProfileHeader()
    {
        var prog = ProgressionManager.Instance;
        if (prog == null) return;

        if (levelRankText != null) levelRankText.text = $"LEVEL {prog.Level}  |  {prog.RankTitle.ToUpper()}";
        if (xpScoreText != null) xpScoreText.text = $"TOTAL XP: {prog.TotalXP} XP";
        if (accuracyText != null) accuracyText.text = $"ACCURACY: {prog.Accuracy:F1}%";
    }

    private Color GetDifficultyColor(ChallengeDifficulty diff)
    {
        switch (diff)
        {
            case ChallengeDifficulty.Easy: return new Color(0.35f, 0.85f, 0.45f);
            case ChallengeDifficulty.Medium: return new Color(0.25f, 0.65f, 1.0f);
            case ChallengeDifficulty.Hard: return new Color(1.0f, 0.60f, 0.20f);
            case ChallengeDifficulty.Expert: return new Color(0.95f, 0.35f, 0.45f);
            default: return Color.white;
        }
    }

    private void EnsurePool(List<GameObject> pool, GameObject prefab, Transform parent, int required)
    {
        if (prefab == null || parent == null) return;
        while (pool.Count < required)
        {
            var obj = Instantiate(prefab, parent);
            obj.SetActive(false);
            pool.Add(obj);
        }
    }
}
