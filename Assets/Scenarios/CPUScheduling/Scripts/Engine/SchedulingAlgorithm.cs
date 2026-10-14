using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public abstract class SchedulingAlgorithm
{
    public abstract string Name { get; }
    public abstract string Description { get; }

    public int CurrentTime { get; protected set; } = 0;
    public bool IsCompleted { get; protected set; } = false;
    public OSProcess RunningProcess { get; protected set; } = null;
    public CpuState CurrentCpuState { get; protected set; } = CpuState.Idle;
    public List<GanttSegment> GanttChart { get; protected set; } = new List<GanttSegment>();
    public List<OSProcess> AllProcesses { get; protected set; } = new List<OSProcess>();

    /// <summary>
    /// Initializes algorithm state with a fresh copy of processes.
    /// </summary>
    public virtual void Initialize(List<OSProcess> processes)
    {
        CurrentTime = 0;
        IsCompleted = false;
        RunningProcess = null;
        CurrentCpuState = CpuState.Idle;
        GanttChart = new List<GanttSegment>();
        AllProcesses = processes.Select(p => p.Clone()).ToList();
        foreach (var p in AllProcesses)
        {
            p.Reset();
        }
    }

    /// <summary>
    /// Executes one discrete simulation time unit (from CurrentTime to CurrentTime + 1).
    /// Returns true if simulation is still active, false if finished.
    /// </summary>
    public abstract bool Step();

    /// <summary>
    /// Returns currently queued processes waiting for CPU execution.
    /// </summary>
    public abstract List<OSProcess> GetReadyQueue();

    /// <summary>
    /// Returns all multilevel queues for multi-queue algorithms.
    /// </summary>
    public virtual List<List<OSProcess>> GetAllQueues()
    {
        return new List<List<OSProcess>> { GetReadyQueue() };
    }

    /// <summary>
    /// Runs the algorithm to completion in batch mode and returns the computed results.
    /// </summary>
    public virtual SchedulingResult RunToCompletion(List<OSProcess> processes)
    {
        Initialize(processes);

        int maxCycles = 10000; // Safeguard against infinite loops
        int cycles = 0;

        while (!IsCompleted && cycles < maxCycles)
        {
            Step();
            cycles++;
        }

        var result = new SchedulingResult(Name)
        {
            Processes = AllProcesses.Select(p => p.Clone()).ToList(),
            GanttChart = new List<GanttSegment>(GanttChart),
            TotalDuration = CurrentTime
        };

        return result;
    }

    /// <summary>
    /// Helper to record 1 time unit to the Gantt Chart, merging contiguous blocks for the same process.
    /// </summary>
    protected void RecordGanttUnit(string processId, Color color)
    {
        int start = CurrentTime;
        int end = CurrentTime + 1;

        if (GanttChart.Count > 0)
        {
            var last = GanttChart[GanttChart.Count - 1];
            if (last.ProcessId == processId && last.EndTime == start)
            {
                GanttChart[GanttChart.Count - 1] = new GanttSegment(processId, last.StartTime, end, color);
                return;
            }
        }

        GanttChart.Add(new GanttSegment(processId, start, end, color));
    }
}
