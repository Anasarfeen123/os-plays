using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum CpuState
{
    Idle,
    Running,
    ContextSwitch
}

[System.Serializable]
public struct GanttSegment
{
    public string ProcessId; // e.g., "P1", "IDLE", "CTX"
    public int StartTime;
    public int EndTime;
    public Color BlockColor;
    public bool IsIdle => ProcessId == "IDLE" || string.IsNullOrEmpty(ProcessId);
    public bool IsContextSwitch => ProcessId == "CTX";
    public int Duration => EndTime - StartTime;

    public GanttSegment(string processId, int startTime, int endTime, Color color)
    {
        ProcessId = processId;
        StartTime = startTime;
        EndTime = endTime;
        BlockColor = color;
    }
}

[System.Serializable]
public class SchedulingResult
{
    public string AlgorithmName;
    public List<OSProcess> Processes;
    public List<GanttSegment> GanttChart;
    public int TotalDuration;

    public float AverageWaitingTime => (Processes != null && Processes.Count > 0)
        ? (float)Processes.Average(p => p.WaitingTime)
        : 0f;

    public float AverageTurnaroundTime => (Processes != null && Processes.Count > 0)
        ? (float)Processes.Average(p => p.TurnaroundTime)
        : 0f;

    public float AverageResponseTime => (Processes != null && Processes.Count > 0)
        ? (float)Processes.Average(p => p.ResponseTime)
        : 0f;

    public float CpuUtilization
    {
        get
        {
            if (TotalDuration <= 0 || GanttChart == null || GanttChart.Count == 0) return 0f;
            int busyTime = GanttChart.Where(s => !s.IsIdle).Sum(s => s.Duration);
            return (float)busyTime / TotalDuration * 100f;
        }
    }

    public SchedulingResult(string name)
    {
        AlgorithmName = name;
        Processes = new List<OSProcess>();
        GanttChart = new List<GanttSegment>();
        TotalDuration = 0;
    }
}
