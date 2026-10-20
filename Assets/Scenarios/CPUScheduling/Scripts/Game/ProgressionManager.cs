using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ProgressionManager : MonoBehaviour
{
    public static ProgressionManager Instance { get; private set; }

    [Header("Player Progress Stats")]
    public int TotalXP = 0;
    public int TotalAttempts = 0;
    public int CorrectAttempts = 0;

    public int Level => (TotalXP / 250) + 1;
    public float Accuracy => TotalAttempts > 0 ? ((float)CorrectAttempts / TotalAttempts * 100f) : 100f;

    public string RankTitle
    {
        get
        {
            if (Level >= 10) return "CPU Scheduling Grandmaster";
            if (Level >= 7) return "OS Kernel Architect";
            if (Level >= 5) return "Process Dispatcher";
            if (Level >= 3) return "Interrupt Specialist";
            if (Level >= 2) return "Kernel Apprentice";
            return "Scheduling Novice";
        }
    }

    public List<Achievement> Achievements = new List<Achievement>();
    // Serializable dictionary to allow Unity serialization of mastery levels
    public SerializableDictionary<string, int> AlgorithmMastery = new SerializableDictionary<string, int>();

    // UI sound feedback
    [Header("UI Sound")]
    public AudioClip clickClip;
    private AudioSource uiAudioSource;

    // Fade settings
    [Header("UI Fade")]
    [SerializeField] private float uiFadeDuration = 0.2f;
    [SerializeField] private GameObject uiPanel;
    [SerializeField] private MonoBehaviour playerMovement;

    public event Action OnProgressUpdated;
    public event Action<Achievement> OnAchievementUnlocked;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(gameObject);

        InitializeAchievements();
        InitializeMastery();
    }

    private void InitializeAchievements()
    {
        Achievements = new List<Achievement>
        {
            new Achievement("first_dispatch", "First Dispatch", "Successfully solve your first scheduling challenge.", ""),
            new Achievement("fcfs_master", "FIFO Champion", "Master First-Come First-Served scheduling without errors.", ""),
            new Achievement("sjf_prodigy", "Greedy Optimizer", "Accurately predict Shortest Job First execution.", ""),
            new Achievement("preemption_master", "Preemption Prodigy", "Master Shortest Remaining Time First (SRTF) preemption.", ""),
            new Achievement("priority_commander", "Priority Commander", "Resolve a high-priority interrupt challenge.", ""),
            new Achievement("rr_time_sharer", "Quantum Time-Sharer", "Solve a Round Robin time-slice challenge.", ""),
            new Achievement("mlfq_architect", "Feedback Architect", "Solve an Expert Multilevel Feedback Queue scenario.", ""),
            new Achievement("speed_demon", "Speed Demon", "Solve a scheduling challenge in under 10 seconds.", ""),
            new Achievement("grandmaster", "Grandmaster Scheduler", "Reach Level 5 and earn over 1,000 XP.", "")
        };
    }

    private void InitializeMastery()
    {
        string[] algos = { "FCFS", "SJF", "SRTF", "Priority", "Round Robin", "MLQ", "MLFQ" };
        foreach (var a in algos)
        {
            if (!AlgorithmMastery.ContainsKey(a))
            {
                AlgorithmMastery[a] = 0; // 0% to 100%
            }
        }
    }

    public ChallengeResultReport EvaluateChallenge(SchedulingChallenge challenge, ChallengeOption selectedOption, float timeTakenSeconds)
    {
        TotalAttempts++;
        var report = new ChallengeResultReport();

        // 1. Run engine to get ground truth
        SchedulingAlgorithm algo = InstantiateAlgorithm(challenge.AlgorithmName, challenge.TimeQuantum);
        report.ExpectedEngineResult = algo.RunToCompletion(challenge.Processes);

        // Ground truth sequence (order of first execution of each process)
        var expectedSeqList = new List<string>();
        foreach (var s in report.ExpectedEngineResult.GanttChart)
        {
            if (!s.IsIdle && !expectedSeqList.Contains(s.ProcessId))
            {
                expectedSeqList.Add(s.ProcessId);
            }
        }
        report.ExpectedSequence = string.Join(" -> ", expectedSeqList);
        report.SelectedSequence = selectedOption != null ? selectedOption.SequenceLabel : "None";

        ChallengeOption correctOption = challenge.Options != null ? challenge.Options.Find(o => o.IsCorrect) : null;

        // 2. DYNAMIC VALIDATION: validate against engine ground truth if sequence is provided,
        //    otherwise validate by verified option correctness flag.
        bool isCorrect = false;
        if (selectedOption != null)
        {
            if (selectedOption.Sequence != null && selectedOption.Sequence.Count > 0 && expectedSeqList.Count > 0)
            {
                isCorrect = SequencesEqual(selectedOption.Sequence, expectedSeqList);
            }
            else
            {
                isCorrect = selectedOption.IsCorrect;
            }
        }

        report.IsCorrect = isCorrect;
        report.Explanation = selectedOption != null
            ? (report.IsCorrect 
                ? (!string.IsNullOrEmpty(selectedOption.Feedback) ? selectedOption.Feedback : challenge.DetailedExplanation) 
                : BuildIncorrectFeedback(selectedOption, correctOption, report.ExpectedSequence, challenge.DetailedExplanation))
            : challenge.DetailedExplanation;

        if (report.IsCorrect)
        {
            CorrectAttempts++;
            challenge.IsCompleted = true;

            // Score calculation
            int baseXP = challenge.BaseXP;
            int timeBonus = Mathf.Max(0, Mathf.RoundToInt((30f - timeTakenSeconds) * 5f));
            int totalScore = baseXP + timeBonus;

            report.BaseXPEarned = baseXP;
            report.TimeBonusEarned = timeBonus;
            report.ScoreEarned = totalScore;

            TotalXP += totalScore;
            challenge.HighScore = Mathf.Max(challenge.HighScore, totalScore);

            // Update Mastery
            if (AlgorithmMastery.ContainsKey(challenge.AlgorithmName))
            {
                AlgorithmMastery[challenge.AlgorithmName] = Mathf.Min(100, AlgorithmMastery[challenge.AlgorithmName] + 35);
            }

            // Check Achievements
            CheckAchievements(challenge, timeTakenSeconds, report);

            AudioManager.Instance?.PlaySfx(AudioManager.SfxType.Success);
        }
        else
        {
            // Encouraging partial XP for learning attempt
            report.BaseXPEarned = 25;
            report.TimeBonusEarned = 0;
            report.ScoreEarned = 25;
            TotalXP += 25;

            AudioManager.Instance?.PlaySfx(AudioManager.SfxType.Error);
        }

        OnProgressUpdated?.Invoke();
        return report;
    }

    private static bool SequencesEqual(List<string> a, List<string> b)
    {
        if (a == null || b == null) return false;
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
        {
            if (!string.Equals(a[i], b[i], StringComparison.Ordinal)) return false;
        }
        return true;
    }

    private static string BuildIncorrectFeedback(ChallengeOption selected, ChallengeOption correct, string expected, string detailedExplanation)
    {
        string correctText = correct != null ? correct.SequenceLabel : expected;
        string explanation = !string.IsNullOrEmpty(detailedExplanation) ? detailedExplanation : "Review the algorithm's execution rule and try again.";
        return $"INCORRECT. You selected: \"{selected?.SequenceLabel}\".\nCorrect answer: {correctText}.\n{explanation}";
    }

    private void CheckAchievements(SchedulingChallenge challenge, float timeTaken, ChallengeResultReport report)
    {
        UnlockAchievement("first_dispatch", report);

        if (timeTaken <= 10f)
        {
            UnlockAchievement("speed_demon", report);
        }

        if (challenge.AlgorithmName.Contains("FCFS")) UnlockAchievement("fcfs_master", report);
        if (challenge.AlgorithmName.Contains("SJF")) UnlockAchievement("sjf_prodigy", report);
        if (challenge.AlgorithmName.Contains("SRTF")) UnlockAchievement("preemption_master", report);
        if (challenge.AlgorithmName.Contains("Priority")) UnlockAchievement("priority_commander", report);
        if (challenge.AlgorithmName.Contains("Round Robin")) UnlockAchievement("rr_time_sharer", report);
        if (challenge.AlgorithmName.Contains("MLFQ")) UnlockAchievement("mlfq_architect", report);

        if (Level >= 5 && TotalXP >= 1000)
        {
            UnlockAchievement("grandmaster", report);
        }
    }

    private void UnlockAchievement(string id, ChallengeResultReport report)
    {
        var ach = Achievements.Find(a => a.Id == id);
        if (ach != null && !ach.IsUnlocked)
        {
            ach.IsUnlocked = true;
            ach.UnlockedTimestamp = DateTime.Now.ToString("HH:mm:ss");
            report.NewlyUnlockedAchievements.Add(ach);
            OnAchievementUnlocked?.Invoke(ach);
        }
    }

    public void AwardXP(int xp)
    {
        TotalXP += xp;
        OnProgressUpdated?.Invoke();
    }

    public void ResetProgress()
    {
        TotalXP = 0;
        TotalAttempts = 0;
        CorrectAttempts = 0;
        InitializeAchievements();
        InitializeMastery();
        OnProgressUpdated?.Invoke();
    }

    private SchedulingAlgorithm InstantiateAlgorithm(string name, int quantum)
    {
        if (name.Contains("FCFS")) return new FCFSAlgorithm();
        if (name.Contains("SJF")) return new SJFAlgorithm();
        if (name.Contains("SRTF")) return new SRTFAlgorithm();
        if (name.Contains("Priority")) return new PriorityAlgorithm(isPreemptive: true);
        if (name.Contains("Round Robin") || name.Contains("RR")) return new RoundRobinAlgorithm(quantum);
        if (name.Contains("MLQ")) return new MLQAlgorithm();
        if (name.Contains("MLFQ")) return new MLFQAlgorithm(new int[] { 2, 4, 8 });
        return new FCFSAlgorithm();
    }
}


