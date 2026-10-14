using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SRTFAlgorithm : SchedulingAlgorithm
{
    public override string Name => "Shortest Remaining Time First (SRTF)";
    public override string Description => "Preemptive version of SJF where a newly arrived process with a shorter remaining burst time preempts the currently running process.";

    private List<OSProcess> readyQueue = new List<OSProcess>();

    public override void Initialize(List<OSProcess> processes)
    {
        base.Initialize(processes);
        readyQueue.Clear();
    }

    public override bool Step()
    {
        if (IsCompleted) return false;

        // 1. Check for new arrivals at CurrentTime
        var newArrivals = AllProcesses
            .Where(p => p.ArrivalTime == CurrentTime && p.State == ProcessState.New)
            .ToList();

        foreach (var p in newArrivals)
        {
            p.State = ProcessState.Ready;
            readyQueue.Add(p);
        }

        // 2. Preemptive Selection among ready queue and current running process
        var candidates = new List<OSProcess>(readyQueue);
        if (RunningProcess != null)
        {
            candidates.Add(RunningProcess);
        }

        if (candidates.Count > 0)
        {
            // Pick candidate with shortest remaining time
            // Tie break: Arrival time, then already running process, then Id
            var best = candidates
                .OrderBy(p => p.RemainingTime)
                .ThenBy(p => p.ArrivalTime)
                .ThenBy(p => p == RunningProcess ? 0 : 1)
                .ThenBy(p => p.Id)
                .First();

            if (RunningProcess != null && RunningProcess != best)
            {
                // Preempt current running process
                RunningProcess.State = ProcessState.Ready;
                if (!readyQueue.Contains(RunningProcess))
                {
                    readyQueue.Add(RunningProcess);
                }
                RunningProcess = null;
            }

            if (RunningProcess == null)
            {
                RunningProcess = best;
                readyQueue.Remove(best);
                RunningProcess.State = ProcessState.Running;
                if (RunningProcess.StartTime == -1)
                {
                    RunningProcess.StartTime = CurrentTime;
                }
            }
        }

        // 3. Execute CPU cycle
        if (RunningProcess != null)
        {
            CurrentCpuState = CpuState.Running;
            RecordGanttUnit(RunningProcess.Id, RunningProcess.ProcessColor);

            RunningProcess.RemainingTime--;
            if (RunningProcess.RemainingTime <= 0)
            {
                RunningProcess.CompletionTime = CurrentTime + 1;
                RunningProcess.State = ProcessState.Terminated;
                RunningProcess = null;
            }
        }
        else
        {
            CurrentCpuState = CpuState.Idle;
            RecordGanttUnit("IDLE", new Color(0.2f, 0.2f, 0.25f, 0.6f));
        }

        // 4. Advance time
        CurrentTime++;

        // 5. Check completion
        if (AllProcesses.All(p => p.IsCompleted))
        {
            IsCompleted = true;
            CurrentCpuState = CpuState.Idle;
            return false;
        }

        return true;
    }

    public override List<OSProcess> GetReadyQueue()
    {
        return readyQueue
            .OrderBy(p => p.RemainingTime)
            .ThenBy(p => p.ArrivalTime)
            .ThenBy(p => p.Id)
            .ToList();
    }
}

public class PriorityAlgorithm : SchedulingAlgorithm
{
    public override string Name => "Priority Scheduling (Preemptive)";
    public override string Description => "Preemptive algorithm that schedules processes based on priority numbers (lower integer = higher priority).";

    public bool IsPreemptive { get; set; } = true;
    private List<OSProcess> readyQueue = new List<OSProcess>();

    public PriorityAlgorithm(bool isPreemptive = true)
    {
        IsPreemptive = isPreemptive;
    }

    public override void Initialize(List<OSProcess> processes)
    {
        base.Initialize(processes);
        readyQueue.Clear();
    }

    public override bool Step()
    {
        if (IsCompleted) return false;

        // 1. Check for new arrivals at CurrentTime
        var newArrivals = AllProcesses
            .Where(p => p.ArrivalTime == CurrentTime && p.State == ProcessState.New)
            .ToList();

        foreach (var p in newArrivals)
        {
            p.State = ProcessState.Ready;
            readyQueue.Add(p);
        }

        // 2. Selection
        if (IsPreemptive)
        {
            var candidates = new List<OSProcess>(readyQueue);
            if (RunningProcess != null)
            {
                candidates.Add(RunningProcess);
            }

            if (candidates.Count > 0)
            {
                var best = candidates
                    .OrderBy(p => p.Priority)
                    .ThenBy(p => p.ArrivalTime)
                    .ThenBy(p => p == RunningProcess ? 0 : 1)
                    .ThenBy(p => p.Id)
                    .First();

                if (RunningProcess != null && RunningProcess != best)
                {
                    // Preempt
                    RunningProcess.State = ProcessState.Ready;
                    if (!readyQueue.Contains(RunningProcess))
                    {
                        readyQueue.Add(RunningProcess);
                    }
                    RunningProcess = null;
                }

                if (RunningProcess == null)
                {
                    RunningProcess = best;
                    readyQueue.Remove(best);
                    RunningProcess.State = ProcessState.Running;
                    if (RunningProcess.StartTime == -1)
                    {
                        RunningProcess.StartTime = CurrentTime;
                    }
                }
            }
        }
        else
        {
            // Non-preemptive selection
            if (RunningProcess == null && readyQueue.Count > 0)
            {
                readyQueue = readyQueue
                    .OrderBy(p => p.Priority)
                    .ThenBy(p => p.ArrivalTime)
                    .ThenBy(p => p.Id)
                    .ToList();

                RunningProcess = readyQueue[0];
                readyQueue.RemoveAt(0);
                RunningProcess.State = ProcessState.Running;
                if (RunningProcess.StartTime == -1)
                {
                    RunningProcess.StartTime = CurrentTime;
                }
            }
        }

        // 3. Execute CPU cycle
        if (RunningProcess != null)
        {
            CurrentCpuState = CpuState.Running;
            RecordGanttUnit(RunningProcess.Id, RunningProcess.ProcessColor);

            RunningProcess.RemainingTime--;
            if (RunningProcess.RemainingTime <= 0)
            {
                RunningProcess.CompletionTime = CurrentTime + 1;
                RunningProcess.State = ProcessState.Terminated;
                RunningProcess = null;
            }
        }
        else
        {
            CurrentCpuState = CpuState.Idle;
            RecordGanttUnit("IDLE", new Color(0.2f, 0.2f, 0.25f, 0.6f));
        }

        // 4. Advance time
        CurrentTime++;

        // 5. Check completion
        if (AllProcesses.All(p => p.IsCompleted))
        {
            IsCompleted = true;
            CurrentCpuState = CpuState.Idle;
            return false;
        }

        return true;
    }

    public override List<OSProcess> GetReadyQueue()
    {
        return readyQueue
            .OrderBy(p => p.Priority)
            .ThenBy(p => p.ArrivalTime)
            .ThenBy(p => p.Id)
            .ToList();
    }
}
