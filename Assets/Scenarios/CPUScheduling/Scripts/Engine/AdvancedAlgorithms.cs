using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RoundRobinAlgorithm : SchedulingAlgorithm
{
    public override string Name => $"Round Robin (RR, q={TimeQuantum})";
    public override string Description => $"Preemptive algorithm that allocates fixed time slices (quantum = {TimeQuantum}) in a circular FIFO ready queue.";

    public int TimeQuantum { get; set; } = 4;
    private Queue<OSProcess> readyQueue = new Queue<OSProcess>();
    private int currentQuantumUsed = 0;

    public RoundRobinAlgorithm(int quantum = 4)
    {
        TimeQuantum = quantum;
    }

    public override void Initialize(List<OSProcess> processes)
    {
        base.Initialize(processes);
        readyQueue.Clear();
        currentQuantumUsed = 0;
    }

    public override bool Step()
    {
        if (IsCompleted) return false;

        // 1. Check if running process quantum expired at the end of last tick
        OSProcess preemptedProcess = null;
        if (RunningProcess != null && currentQuantumUsed >= TimeQuantum)
        {
            preemptedProcess = RunningProcess;
            preemptedProcess.State = ProcessState.Ready;
            RunningProcess = null;
            currentQuantumUsed = 0;
        }

        // 2. New arrivals at CurrentTime enter ready queue BEFORE the expired process is re-enqueued
        var newArrivals = AllProcesses
            .Where(p => p.ArrivalTime == CurrentTime && p.State == ProcessState.New)
            .OrderBy(p => p.Id)
            .ToList();

        foreach (var p in newArrivals)
        {
            p.State = ProcessState.Ready;
            readyQueue.Enqueue(p);
        }

        // 3. Re-enqueue the expired process (if any)
        if (preemptedProcess != null)
        {
            readyQueue.Enqueue(preemptedProcess);
        }

        // 4. Dispatch next process if CPU is free
        if (RunningProcess == null && readyQueue.Count > 0)
        {
            RunningProcess = readyQueue.Dequeue();
            RunningProcess.State = ProcessState.Running;
            currentQuantumUsed = 0;
            if (RunningProcess.StartTime == -1)
            {
                RunningProcess.StartTime = CurrentTime;
            }
        }

        // 5. Execute CPU cycle
        if (RunningProcess != null)
        {
            CurrentCpuState = CpuState.Running;
            RecordGanttUnit(RunningProcess.Id, RunningProcess.ProcessColor);

            RunningProcess.RemainingTime--;
            currentQuantumUsed++;

            if (RunningProcess.RemainingTime <= 0)
            {
                RunningProcess.CompletionTime = CurrentTime + 1;
                RunningProcess.State = ProcessState.Terminated;
                RunningProcess = null;
                currentQuantumUsed = 0;
            }
        }
        else
        {
            CurrentCpuState = CpuState.Idle;
            RecordGanttUnit("IDLE", new Color(0.2f, 0.2f, 0.25f, 0.6f));
        }

        // 6. Advance time
        CurrentTime++;

        // 7. Check completion
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

public class MLQAlgorithm : SchedulingAlgorithm
{
    public override string Name => "Multilevel Queue (MLQ)";
    public override string Description => "Processes are partitioned into fixed priority queues: Q1 (Priority <= 2, RR q=3) and Q2 (Priority > 2, FCFS).";

    private Queue<OSProcess> q1 = new Queue<OSProcess>(); // High priority RR (q=3)
    private Queue<OSProcess> q2 = new Queue<OSProcess>(); // Low priority FCFS
    private int currentQuantumUsed = 0;
    private int currentQueueIndex = -1; // 0 for Q1, 1 for Q2

    public override void Initialize(List<OSProcess> processes)
    {
        base.Initialize(processes);
        q1.Clear();
        q2.Clear();
        currentQuantumUsed = 0;
        currentQueueIndex = -1;
    }

    public override bool Step()
    {
        if (IsCompleted) return false;

        // 1. Quantum expiration check for Q1 running process
        OSProcess expiredQ1 = null;
        if (RunningProcess != null && currentQueueIndex == 0 && currentQuantumUsed >= 3)
        {
            expiredQ1 = RunningProcess;
            expiredQ1.State = ProcessState.Ready;
            RunningProcess = null;
            currentQuantumUsed = 0;
            currentQueueIndex = -1;
        }

        // 2. New arrivals at CurrentTime
        var newArrivals = AllProcesses
            .Where(p => p.ArrivalTime == CurrentTime && p.State == ProcessState.New)
            .OrderBy(p => p.Id)
            .ToList();

        foreach (var p in newArrivals)
        {
            p.State = ProcessState.Ready;
            if (p.Priority <= 2)
            {
                p.QueueLevel = 0;
                q1.Enqueue(p);
            }
            else
            {
                p.QueueLevel = 1;
                q2.Enqueue(p);
            }
        }

        if (expiredQ1 != null)
        {
            q1.Enqueue(expiredQ1);
        }

        // 3. Preemption check: if running Q2 and Q1 has arrived processes, preempt Q2
        if (RunningProcess != null && currentQueueIndex == 1 && q1.Count > 0)
        {
            // Preempt Q2 process back to front of Q2 (or enqueue)
            RunningProcess.State = ProcessState.Ready;
            var temp = new List<OSProcess> { RunningProcess };
            temp.AddRange(q2);
            q2.Clear();
            foreach (var item in temp) q2.Enqueue(item);
            
            RunningProcess = null;
            currentQuantumUsed = 0;
            currentQueueIndex = -1;
        }

        // 4. Dispatch from highest available queue
        if (RunningProcess == null)
        {
            if (q1.Count > 0)
            {
                RunningProcess = q1.Dequeue();
                currentQueueIndex = 0;
                currentQuantumUsed = 0;
                RunningProcess.State = ProcessState.Running;
                if (RunningProcess.StartTime == -1) RunningProcess.StartTime = CurrentTime;
            }
            else if (q2.Count > 0)
            {
                RunningProcess = q2.Dequeue();
                currentQueueIndex = 1;
                currentQuantumUsed = 0;
                RunningProcess.State = ProcessState.Running;
                if (RunningProcess.StartTime == -1) RunningProcess.StartTime = CurrentTime;
            }
        }

        // 5. Execute CPU cycle
        if (RunningProcess != null)
        {
            CurrentCpuState = CpuState.Running;
            RecordGanttUnit(RunningProcess.Id, RunningProcess.ProcessColor);

            RunningProcess.RemainingTime--;
            currentQuantumUsed++;

            if (RunningProcess.RemainingTime <= 0)
            {
                RunningProcess.CompletionTime = CurrentTime + 1;
                RunningProcess.State = ProcessState.Terminated;
                RunningProcess = null;
                currentQuantumUsed = 0;
                currentQueueIndex = -1;
            }
        }
        else
        {
            CurrentCpuState = CpuState.Idle;
            RecordGanttUnit("IDLE", new Color(0.2f, 0.2f, 0.25f, 0.6f));
        }

        // 6. Advance time
        CurrentTime++;

        // 7. Check completion
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
        var list = new List<OSProcess>(q1);
        list.AddRange(q2);
        return list;
    }

    public override List<List<OSProcess>> GetAllQueues()
    {
        return new List<List<OSProcess>> { q1.ToList(), q2.ToList() };
    }
}

public class MLFQAlgorithm : SchedulingAlgorithm
{
    public override string Name => "Multilevel Feedback Queue (MLFQ)";
    public override string Description => "Adaptive feedback scheduling with 3 priority queues: Q0 (RR q=2), Q1 (RR q=4), Q2 (FCFS). Processes demote upon quantum exhaustion.";

    public int[] Quanta { get; set; } = new int[] { 2, 4, 8 }; // Quanta for Q0, Q1, Q2
    public int BoostInterval { get; set; } = 50; // Priority boost interval

    private List<Queue<OSProcess>> queues = new List<Queue<OSProcess>>();
    private int currentQuantumUsed = 0;
    private int activeQueueIndex = -1;

    public MLFQAlgorithm()
    {
        queues.Add(new Queue<OSProcess>());
        queues.Add(new Queue<OSProcess>());
        queues.Add(new Queue<OSProcess>());
    }

    public MLFQAlgorithm(int[] customQuanta, int boostInterval = 50)
    {
        Quanta = customQuanta;
        BoostInterval = boostInterval;
        for (int i = 0; i < customQuanta.Length; i++)
        {
            queues.Add(new Queue<OSProcess>());
        }
    }

    public override void Initialize(List<OSProcess> processes)
    {
        base.Initialize(processes);
        foreach (var q in queues) q.Clear();
        currentQuantumUsed = 0;
        activeQueueIndex = -1;
    }

    public override bool Step()
    {
        if (IsCompleted) return false;

        // 1. Check quantum expiration for current running process
        OSProcess demotedProcess = null;
        int demotedTargetQueue = -1;
        if (RunningProcess != null && activeQueueIndex >= 0 && activeQueueIndex < Quanta.Length - 1)
        {
            if (currentQuantumUsed >= Quanta[activeQueueIndex])
            {
                // Exhausted quantum in this level -> Demote to next lower queue
                demotedProcess = RunningProcess;
                demotedProcess.State = ProcessState.Ready;
                demotedTargetQueue = activeQueueIndex + 1;
                demotedProcess.QueueLevel = demotedTargetQueue;
                
                RunningProcess = null;
                currentQuantumUsed = 0;
                activeQueueIndex = -1;
            }
        }

        // 2. New arrivals at CurrentTime always enter highest priority queue Q0
        var newArrivals = AllProcesses
            .Where(p => p.ArrivalTime == CurrentTime && p.State == ProcessState.New)
            .OrderBy(p => p.Id)
            .ToList();

        foreach (var p in newArrivals)
        {
            p.State = ProcessState.Ready;
            p.QueueLevel = 0;
            queues[0].Enqueue(p);
        }

        // 3. Enqueue demoted process
        if (demotedProcess != null)
        {
            queues[demotedTargetQueue].Enqueue(demotedProcess);
        }

        // 4. Periodic Priority Boost (if configured)
        if (BoostInterval > 0 && CurrentTime > 0 && CurrentTime % BoostInterval == 0)
        {
            for (int i = 1; i < queues.Count; i++)
            {
                while (queues[i].Count > 0)
                {
                    var p = queues[i].Dequeue();
                    p.QueueLevel = 0;
                    queues[0].Enqueue(p);
                }
            }
            if (RunningProcess != null && activeQueueIndex > 0)
            {
                RunningProcess.QueueLevel = 0;
                activeQueueIndex = 0;
            }
        }

        // 5. Preemption check: if running process is from lower queue and a higher queue is non-empty
        if (RunningProcess != null)
        {
            for (int i = 0; i < activeQueueIndex; i++)
            {
                if (queues[i].Count > 0)
                {
                    // Preempt running process back to its current queue level (without demotion yet since quantum didn't expire)
                    RunningProcess.State = ProcessState.Ready;
                    var temp = new List<OSProcess> { RunningProcess };
                    temp.AddRange(queues[activeQueueIndex]);
                    queues[activeQueueIndex].Clear();
                    foreach (var item in temp) queues[activeQueueIndex].Enqueue(item);

                    RunningProcess = null;
                    currentQuantumUsed = 0;
                    activeQueueIndex = -1;
                    break;
                }
            }
        }

        // 6. Dispatch from highest non-empty queue (Q0 > Q1 > Q2)
        if (RunningProcess == null)
        {
            for (int i = 0; i < queues.Count; i++)
            {
                if (queues[i].Count > 0)
                {
                    RunningProcess = queues[i].Dequeue();
                    activeQueueIndex = i;
                    currentQuantumUsed = 0;
                    RunningProcess.State = ProcessState.Running;
                    if (RunningProcess.StartTime == -1)
                    {
                        RunningProcess.StartTime = CurrentTime;
                    }
                    break;
                }
            }
        }

        // 7. Execute CPU cycle
        if (RunningProcess != null)
        {
            CurrentCpuState = CpuState.Running;
            RecordGanttUnit(RunningProcess.Id, RunningProcess.ProcessColor);

            RunningProcess.RemainingTime--;
            currentQuantumUsed++;

            if (RunningProcess.RemainingTime <= 0)
            {
                RunningProcess.CompletionTime = CurrentTime + 1;
                RunningProcess.State = ProcessState.Terminated;
                RunningProcess = null;
                currentQuantumUsed = 0;
                activeQueueIndex = -1;
            }
        }
        else
        {
            CurrentCpuState = CpuState.Idle;
            RecordGanttUnit("IDLE", new Color(0.2f, 0.2f, 0.25f, 0.6f));
        }

        // 8. Advance time
        CurrentTime++;

        // 9. Check completion
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
        var list = new List<OSProcess>();
        foreach (var q in queues) list.AddRange(q);
        return list;
    }

    public override List<List<OSProcess>> GetAllQueues()
    {
        var result = new List<List<OSProcess>>();
        foreach (var q in queues) result.Add(q.ToList());
        return result;
    }
}
