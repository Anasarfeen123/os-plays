using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class FCFSAlgorithm : SchedulingAlgorithm
{
    public override string Name => "First-Come, First-Served (FCFS)";
    public override string Description => "Non-preemptive algorithm that executes processes in the exact order of their arrival time.";

    private Queue<OSProcess> readyQueue = new Queue<OSProcess>();

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
            .OrderBy(p => p.Id)
            .ToList();

        foreach (var p in newArrivals)
        {
            p.State = ProcessState.Ready;
            readyQueue.Enqueue(p);
        }

        // 2. Schedule if CPU is currently free
        if (RunningProcess == null && readyQueue.Count > 0)
        {
            RunningProcess = readyQueue.Dequeue();
            RunningProcess.State = ProcessState.Running;
            if (RunningProcess.StartTime == -1)
            {
                RunningProcess.StartTime = CurrentTime;
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
        return readyQueue.ToList();
    }
}

public class SJFAlgorithm : SchedulingAlgorithm
{
    public override string Name => "Shortest Job First (SJF)";
    public override string Description => "Non-preemptive algorithm that selects the waiting process with the smallest initial burst time.";

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
            .OrderBy(p => p.BurstTime)
            .ThenBy(p => p.Id)
            .ToList();

        foreach (var p in newArrivals)
        {
            p.State = ProcessState.Ready;
            readyQueue.Add(p);
        }

        // 2. Schedule if CPU is currently free (pick shortest initial burst time)
        if (RunningProcess == null && readyQueue.Count > 0)
        {
            readyQueue = readyQueue
                .OrderBy(p => p.BurstTime)
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
            .OrderBy(p => p.BurstTime)
            .ThenBy(p => p.ArrivalTime)
            .ThenBy(p => p.Id)
            .ToList();
    }
}
