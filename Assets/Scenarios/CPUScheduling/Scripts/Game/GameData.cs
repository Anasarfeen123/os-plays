using System;
using System.Collections.Generic;
using UnityEngine;

public enum ChallengeDifficulty
{
    Easy,
    Medium,
    Hard,
    Expert
}

[System.Serializable]
public class ChallengeOption
{
    public string SequenceLabel; // e.g. "P1 -> P4 -> P3 -> P2"
    public List<string> Sequence; // ["P1", "P4", "P3", "P2"]
    public bool IsCorrect;
    public string Feedback;

    public ChallengeOption() 
    {
        Sequence = new List<string>();
    }

    public ChallengeOption(string label, List<string> sequence, bool isCorrect, string feedback)
    {
        SequenceLabel = label;
        Sequence = sequence ?? new List<string>();
        IsCorrect = isCorrect;
        Feedback = feedback;
    }

    public ChallengeOption(string label, bool isCorrect, string feedback)
    {
        SequenceLabel = label;
        Sequence = new List<string>();
        IsCorrect = isCorrect;
        Feedback = feedback;
    }
}

[System.Serializable]
public class SchedulingChallenge
{
    public string Id;
    public string Title;
    public ChallengeDifficulty Difficulty;
    public string AlgorithmName; // e.g. "FCFS", "SJF", "SRTF", "Priority", "RR", "MLQ", "MLFQ"
    public string Description;
    public List<OSProcess> Processes;
    public int TimeQuantum = 4; // for RR/MLQ
    public int BaseXP = 100;
    public List<ChallengeOption> Options = new List<ChallengeOption>();
    public string DetailedExplanation;
    public bool IsUnlocked = false;
    public bool IsCompleted = false;
    public int HighScore = 0;
}

[System.Serializable]
public class Achievement
{
    public string Id;
    public string Title;
    public string Description;
    public string IconEmoji;
    public bool IsUnlocked;
    public string UnlockedTimestamp;

    public Achievement(string id, string title, string description, string emoji)
    {
        Id = id;
        Title = title;
        Description = description;
        IconEmoji = emoji;
        IsUnlocked = false;
        UnlockedTimestamp = "";
    }
}

[System.Serializable]
public class ChallengeResultReport
{
    public bool IsCorrect;
    public string SelectedSequence;
    public string ExpectedSequence;
    public SchedulingResult ExpectedEngineResult;
    public int ScoreEarned;
    public int BaseXPEarned;
    public int TimeBonusEarned;
    public string Explanation;
    public List<Achievement> NewlyUnlockedAchievements = new List<Achievement>();
}
