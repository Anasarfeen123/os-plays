using System;
using UnityEngine;

public enum ProcessState
{
    New,
    Ready,
    Running,
    Waiting,
    Terminated
}

[System.Serializable]
public class OSProcess
{
    [Header("Process Definition")]
    public string Id;
    public int ArrivalTime;
    public int BurstTime;
    public int Priority; // Standard OS convention: Lower integer = higher priority

    [Header("Dynamic Execution State")]
    public int RemainingTime;
    public int StartTime = -1;
    public int CompletionTime = -1;
    public ProcessState State = ProcessState.New;
    public Color ProcessColor = Color.white;

    // Advanced scheduling metadata
    public int QueueLevel = 0;           // For MLQ and MLFQ
    public int QuantumRemaining = 0;      // Current quantum tracker

    // Calculated Performance Metrics
    public int TurnaroundTime => CompletionTime >= 0 ? (CompletionTime - ArrivalTime) : 0;
    public int WaitingTime => CompletionTime >= 0 ? (TurnaroundTime - BurstTime) : 0;
    public int ResponseTime => StartTime >= 0 ? (StartTime - ArrivalTime) : -1;
    public bool IsCompleted => RemainingTime <= 0 && State == ProcessState.Terminated;
    public int ExecutedTime => BurstTime - RemainingTime;

    public OSProcess() { }

    public OSProcess(string id, int arrivalTime, int burstTime, int priority = 0)
    {
        Id = id;
        ArrivalTime = arrivalTime;
        BurstTime = burstTime;
        RemainingTime = burstTime;
        Priority = priority;
        StartTime = -1;
        CompletionTime = -1;
        State = ProcessState.New;
        ProcessColor = GetDefaultColor(id);
    }

    public OSProcess(string id, int arrivalTime, int burstTime, int priority, Color color)
        : this(id, arrivalTime, burstTime, priority)
    {
        ProcessColor = color;
    }

    public OSProcess Clone()
    {
        return new OSProcess(Id, ArrivalTime, BurstTime, Priority, ProcessColor)
        {
            RemainingTime = this.RemainingTime,
            StartTime = this.StartTime,
            CompletionTime = this.CompletionTime,
            State = this.State,
            QueueLevel = this.QueueLevel,
            QuantumRemaining = this.QuantumRemaining
        };
    }

    public void Reset()
    {
        RemainingTime = BurstTime;
        StartTime = -1;
        CompletionTime = -1;
        State = ProcessState.New;
        QueueLevel = 0;
        QuantumRemaining = 0;
    }

    public static Color GetDefaultColor(string id)
    {
        // Deterministic pleasant color palette for processes
        Color[] palette = new Color[]
        {
            new Color(0.25f, 0.65f, 1.0f),  // Sky Blue
            new Color(0.35f, 0.85f, 0.45f), // Emerald Green
            new Color(1.0f, 0.60f, 0.20f),  // Warm Orange
            new Color(0.85f, 0.40f, 0.90f), // Purple Orchid
            new Color(0.95f, 0.35f, 0.45f), // Crimson Rose
            new Color(0.20f, 0.80f, 0.80f), // Cyan Teal
            new Color(1.0f, 0.85f, 0.30f),  // Golden Amber
            new Color(0.60f, 0.50f, 0.95f)  // Periwinkle
        };

        if (string.IsNullOrEmpty(id)) return Color.gray;
        
        // Extract number if format is "P1", "P2", etc.
        if (id.StartsWith("P") && int.TryParse(id.Substring(1), out int num))
        {
            return palette[(num - 1 + palette.Length) % palette.Length];
        }

        int hash = Math.Abs(id.GetHashCode());
        return palette[hash % palette.Length];
    }
}
