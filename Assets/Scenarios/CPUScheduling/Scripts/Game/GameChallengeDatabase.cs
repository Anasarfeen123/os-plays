using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Master question database for CPU Scheduling challenges.
/// Provides curriculum-aligned questions covering:
/// - FCFS, SJF, SRTF, Priority Scheduling, Round Robin, MLQ, MLFQ
/// - Preemptive vs. Non-preemptive scheduling mechanics
/// - Arrival Time, Burst Time, Waiting Time, Turnaround Time, and Response Time
/// - Gantt chart interpretation, scheduling order, and CPU idle time
/// - Numerical scheduling calculations validated against real engine execution
/// Every question has exactly FOUR options (A, B, C, D) and one unambiguous correct answer with a detailed explanation.
/// </summary>
public static class GameChallengeDatabase
{
    private static List<SchedulingChallenge> challenges;

    public static List<SchedulingChallenge> GetChallenges()
    {
        if (challenges == null) InitializeChallenges();
        return challenges;
    }

    private static void InitializeChallenges()
    {
        challenges = new List<SchedulingChallenge>
        {
            // =========================================================
            // EASY TIER (Foundational concepts & straightforward traces)
            // =========================================================
            new SchedulingChallenge
            {
                Id = "easy_1",
                Title = "Round Robin Quantum Allocation",
                Difficulty = ChallengeDifficulty.Easy,
                AlgorithmName = "Round Robin",
                TimeQuantum = 3,
                Description = "Which CPU scheduling algorithm gives each process a fixed time quantum, preempting it if not completed?",
                BaseXP = 120,
                IsUnlocked = true,
                Processes = new List<OSProcess>
                {
                    new OSProcess("P1", 0, 5, 1, new Color(0.25f, 0.65f, 1.0f)),
                    new OSProcess("P2", 0, 4, 1, new Color(0.35f, 0.85f, 0.45f)),
                    new OSProcess("P3", 0, 2, 1, new Color(1.0f, 0.60f, 0.20f))
                },
                Options = new List<ChallengeOption>
                {
                    new ChallengeOption("A. First-Come, First-Served (FCFS)", false, "Incorrect: FCFS is strictly non-preemptive and runs each process until it voluntarily yields."),
                    new ChallengeOption("B. Round Robin (RR)", true, "CORRECT! Round Robin assigns each ready process a fixed time quantum. If the quantum expires before completion, it is preempted."),
                    new ChallengeOption("C. Shortest Remaining Time First (SRTF)", false, "Incorrect: SRTF selects processes based on shortest remaining burst time, not fixed time quanta."),
                    new ChallengeOption("D. Non-preemptive Priority Scheduling", false, "Incorrect: Non-preemptive priority runs the highest priority process to completion without time slices.")
                },
                DetailedExplanation = "Round Robin assigns each ready process a fixed time slice (quantum). If the process has not finished when its quantum expires, the timer interrupt preempts it and moves it to the end of the ready queue."
            },
            new SchedulingChallenge
            {
                Id = "easy_2",
                Title = "FCFS Non-Preemptive Order",
                Difficulty = ChallengeDifficulty.Easy,
                AlgorithmName = "FCFS",
                Description = "Processes P1 (AT=0, BT=4), P2 (AT=1, BT=3), and P3 (AT=2, BT=2) arrive sequentially. What is the execution order under FCFS?",
                BaseXP = 100,
                IsUnlocked = true,
                Processes = new List<OSProcess>
                {
                    new OSProcess("P1", 0, 4, 1, new Color(0.25f, 0.65f, 1.0f)),
                    new OSProcess("P2", 1, 3, 1, new Color(0.35f, 0.85f, 0.45f)),
                    new OSProcess("P3", 2, 2, 1, new Color(1.0f, 0.60f, 0.20f))
                },
                Options = new List<ChallengeOption>
                {
                    new ChallengeOption("A. P1 -> P2 -> P3", new List<string> { "P1", "P2", "P3" }, true, "CORRECT! FCFS allocates the CPU strictly in arrival order (T=0, T=1, T=2)."),
                    new ChallengeOption("B. P3 -> P2 -> P1", new List<string> { "P3", "P2", "P1" }, false, "Incorrect: That is Shortest Job First order, not First-Come First-Served."),
                    new ChallengeOption("C. P2 -> P1 -> P3", new List<string> { "P2", "P1", "P3" }, false, "Incorrect: P1 arrived at T=0 before P2 arrived at T=1."),
                    new ChallengeOption("D. P1 -> P3 -> P2", new List<string> { "P1", "P3", "P2" }, false, "Incorrect: P2 arrived at T=1 before P3 arrived at T=2.")
                },
                DetailedExplanation = "Under First-Come First-Served (FCFS), jobs are enqueued in strict order of arrival time: P1 (AT=0) runs T=0..4, P2 (AT=1) runs T=4..7, and P3 (AT=2) runs T=7..9."
            },
            new SchedulingChallenge
            {
                Id = "easy_3",
                Title = "Preemption Mechanism Comparison",
                Difficulty = ChallengeDifficulty.Easy,
                AlgorithmName = "SRTF",
                Description = "Which pair of algorithms are capable of preempting a currently running CPU process?",
                BaseXP = 120,
                IsUnlocked = true,
                Processes = new List<OSProcess>
                {
                    new OSProcess("P1", 0, 6, 2, new Color(0.25f, 0.65f, 1.0f)),
                    new OSProcess("P2", 2, 2, 1, new Color(0.35f, 0.85f, 0.45f))
                },
                Options = new List<ChallengeOption>
                {
                    new ChallengeOption("A. FCFS and Non-preemptive SJF", false, "Incorrect: Both FCFS and standard SJF are non-preemptive; a running process retains CPU until completion."),
                    new ChallengeOption("B. SRTF and Preemptive Priority", true, "CORRECT! Both SRTF (preempts on shorter remaining burst) and Preemptive Priority (preempts on higher priority) can interrupt running tasks."),
                    new ChallengeOption("C. FCFS and Round Robin only", false, "Incorrect: FCFS is non-preemptive."),
                    new ChallengeOption("D. Non-preemptive Priority and SJF", false, "Incorrect: Both algorithms in this option are non-preemptive.")
                },
                DetailedExplanation = "Preemptive schedulers can interrupt a running process before its burst ends. SRTF preempts when an incoming task has a shorter remaining burst, while Preemptive Priority preempts when an incoming task has higher priority."
            },

            // =========================================================
            // MEDIUM TIER (Metrics, Convoy Effect, Numerical Waiting Time)
            // =========================================================
            new SchedulingChallenge
            {
                Id = "med_1",
                Title = "The Convoy Effect in FCFS",
                Difficulty = ChallengeDifficulty.Medium,
                AlgorithmName = "FCFS",
                Description = "What is the primary cause and symptom of the 'Convoy Effect' in operating systems?",
                BaseXP = 180,
                IsUnlocked = true,
                Processes = new List<OSProcess>
                {
                    new OSProcess("P1", 0, 10, 1, new Color(0.95f, 0.35f, 0.45f)),
                    new OSProcess("P2", 1, 2, 1, new Color(0.25f, 0.65f, 1.0f)),
                    new OSProcess("P3", 2, 1, 1, new Color(0.35f, 0.85f, 0.45f))
                },
                Options = new List<ChallengeOption>
                {
                    new ChallengeOption("A. Frequent timer interrupts cause CPU cache thrashing", false, "Incorrect: FCFS has no timer interrupts; this describes excessive context switching in RR."),
                    new ChallengeOption("B. Short processes wait extensively behind a long CPU-bound process", true, "CORRECT! In FCFS, when a long process holds the CPU, all subsequent shorter I/O-bound jobs wait, reducing device utilization."),
                    new ChallengeOption("C. Low priority processes starve indefinitely", false, "Incorrect: Starvation occurs in priority scheduling, whereas FCFS guarantees all arrived jobs eventually run."),
                    new ChallengeOption("D. Two processes deadlock waiting for shared memory mutexes", false, "Incorrect: The Convoy Effect is a CPU scheduling queueing bottleneck, not a deadlock condition.")
                },
                DetailedExplanation = "The Convoy Effect happens in FCFS when one heavy CPU-burst task monopolizes the processor. Multiple ready processes with short bursts queue up behind it, leading to inflated average waiting times and poor I/O device utilization."
            },
            new SchedulingChallenge
            {
                Id = "med_2",
                Title = "Numerical Waiting Time under FCFS",
                Difficulty = ChallengeDifficulty.Medium,
                AlgorithmName = "FCFS",
                Description = "P1 (AT=0, BT=4), P2 (AT=1, BT=3), and P3 (AT=2, BT=2) run under FCFS. What is the Waiting Time (WT) of process P2?",
                BaseXP = 200,
                IsUnlocked = true,
                Processes = new List<OSProcess>
                {
                    new OSProcess("P1", 0, 4, 1, new Color(0.25f, 0.65f, 1.0f)),
                    new OSProcess("P2", 1, 3, 1, new Color(0.35f, 0.85f, 0.45f)),
                    new OSProcess("P3", 2, 2, 1, new Color(1.0f, 0.60f, 0.20f))
                },
                Options = new List<ChallengeOption>
                {
                    new ChallengeOption("A. 1 ms", false, "Incorrect: P2 arrived at T=1 and waited until P1 finished at T=4."),
                    new ChallengeOption("B. 3 ms", true, "CORRECT! P1 completes at T=4. P2 arrived at T=1, so P2 waited 4 - 1 = 3 ms before receiving the CPU."),
                    new ChallengeOption("C. 4 ms", false, "Incorrect: 4 ms is P1's burst time and P2's start time, but P2 arrived at T=1, not T=0."),
                    new ChallengeOption("D. 6 ms", false, "Incorrect: 6 ms is P2's Turnaround Time (TAT = CT - AT = 7 - 1 = 6 ms), not Waiting Time.")
                },
                DetailedExplanation = "Gantt timeline: P1 runs T=0..4. P2 starts at T=4 and completes at T=7. Turnaround Time = CT - AT = 7 - 1 = 6 ms. Waiting Time = TAT - BT = 6 - 3 = 3 ms (or StartTime - ArrivalTime = 4 - 1 = 3 ms)."
            },
            new SchedulingChallenge
            {
                Id = "med_3",
                Title = "Turnaround Time Metric Definition",
                Difficulty = ChallengeDifficulty.Medium,
                AlgorithmName = "FCFS",
                Description = "How is Turnaround Time (TAT) mathematically calculated for any CPU scheduling process?",
                BaseXP = 190,
                IsUnlocked = true,
                Processes = new List<OSProcess>
                {
                    new OSProcess("P1", 0, 5, 0, new Color(0.25f, 0.65f, 1.0f)),
                    new OSProcess("P2", 2, 4, 0, new Color(0.35f, 0.85f, 0.45f))
                },
                Options = new List<ChallengeOption>
                {
                    new ChallengeOption("A. TAT = First CPU Allocation Time - Arrival Time", false, "Incorrect: That is the definition of Response Time (RT)."),
                    new ChallengeOption("B. TAT = Completion Time - Arrival Time (CT - AT)", true, "CORRECT! Turnaround Time represents the total interval from process submission/arrival until its final completion."),
                    new ChallengeOption("C. TAT = Turnaround Time - Burst Time", false, "Incorrect: That is the formula for Waiting Time (WT = TAT - BT)."),
                    new ChallengeOption("D. TAT = Burst Time + Priority Level", false, "Incorrect: Priority level is not added to burst time for turnaround calculations.")
                },
                DetailedExplanation = "Turnaround Time (TAT) measures total elapsed life of a process in the system: TAT = Completion Time - Arrival Time (CT - AT). Alternatively, TAT = Waiting Time + Burst Time (WT + BT)."
            },

            // =========================================================
            // HARD TIER (SRTF preemption calculations, Round Robin slices, CPU Idle)
            // =========================================================
            new SchedulingChallenge
            {
                Id = "hard_1",
                Title = "SRTF Dynamic Preemption Trigger",
                Difficulty = ChallengeDifficulty.Hard,
                AlgorithmName = "SRTF",
                Description = "P1 (AT=0, BT=7) executes from T=0. P2 (AT=2, BT=4) arrives at T=2. What occurs at T=2 under SRTF?",
                BaseXP = 300,
                IsUnlocked = true,
                Processes = new List<OSProcess>
                {
                    new OSProcess("P1", 0, 7, 0, new Color(0.25f, 0.65f, 1.0f)),
                    new OSProcess("P2", 2, 4, 0, new Color(0.35f, 0.85f, 0.45f)),
                    new OSProcess("P3", 4, 1, 0, new Color(0.85f, 0.35f, 0.95f))
                },
                Options = new List<ChallengeOption>
                {
                    new ChallengeOption("A. P1 continues because SRTF only schedules upon job termination", false, "Incorrect: That describes non-preemptive SJF, not Shortest Remaining Time First."),
                    new ChallengeOption("B. P2 preempts P1 because remaining BT of P2 (4 ms) < P1 (5 ms)", true, "CORRECT! At T=2, P1 has 7 - 2 = 5 ms remaining. P2 has 4 ms burst. Because 4 < 5, SRTF preempts P1 immediately."),
                    new ChallengeOption("C. Both processes share the CPU with 1 ms time slices", false, "Incorrect: Round Robin uses time slicing, not SRTF."),
                    new ChallengeOption("D. P2 is queued behind P1 because P1 arrived first", false, "Incorrect: SRTF is not FCFS; shorter remaining work always takes precedence.")
                },
                DetailedExplanation = "At T=2, P1 has executed for 2 ms, leaving Remaining Burst = 7 - 2 = 5 ms. Newly arrived P2 has Burst = 4 ms. Because Rem(P2) = 4 < Rem(P1) = 5, SRTF preempts P1 and context switches immediately to P2."
            },
            new SchedulingChallenge
            {
                Id = "hard_2",
                Title = "Numerical Round Robin Completion Time",
                Difficulty = ChallengeDifficulty.Hard,
                AlgorithmName = "Round Robin",
                TimeQuantum = 2,
                Description = "P1 (BT=5), P2 (BT=3), P3 (BT=1) all arrive at T=0 under Round Robin with quantum q=2. When does P2 complete?",
                BaseXP = 320,
                IsUnlocked = true,
                Processes = new List<OSProcess>
                {
                    new OSProcess("P1", 0, 5, 0, new Color(0.25f, 0.65f, 1.0f)),
                    new OSProcess("P2", 0, 3, 0, new Color(0.35f, 0.85f, 0.45f)),
                    new OSProcess("P3", 0, 1, 0, new Color(1.0f, 0.60f, 0.20f))
                },
                Options = new List<ChallengeOption>
                {
                    new ChallengeOption("A. 5 ms", false, "Incorrect: At T=5, P3 completes, not P2."),
                    new ChallengeOption("B. 7 ms", false, "Incorrect: At T=7, P1 finishes its second time quantum slice."),
                    new ChallengeOption("C. 8 ms", true, "CORRECT! P1(0..2, rem 3) -> P2(2..4, rem 1) -> P3(4..5, done) -> P1(5..7, rem 1) -> P2(7..8, done!). P2 completes at T=8 ms."),
                    new ChallengeOption("D. 9 ms", false, "Incorrect: P1 completes at T=9 ms, not P2.")
                },
                DetailedExplanation = "Round Robin trace (q=2): T=0..2: P1 runs (rem 3) -> T=2..4: P2 runs (rem 1) -> T=4..5: P3 runs 1ms (completes at T=5) -> T=5..7: P1 runs (rem 1) -> T=7..8: P2 runs 1ms and completes at T=8 ms. (P1 finishes at T=9 ms)."
            },
            new SchedulingChallenge
            {
                Id = "hard_3",
                Title = "CPU Idle Time & Gantt Gap Analysis",
                Difficulty = ChallengeDifficulty.Hard,
                AlgorithmName = "FCFS",
                Description = "Process P1 arrives at T=2 (Burst=3) and P2 arrives at T=6 (Burst=2). How much total CPU Idle Time occurs from T=0 to T=8?",
                BaseXP = 280,
                IsUnlocked = true,
                Processes = new List<OSProcess>
                {
                    new OSProcess("P1", 2, 3, 0, new Color(0.25f, 0.65f, 1.0f)),
                    new OSProcess("P2", 6, 2, 0, new Color(0.35f, 0.85f, 0.45f))
                },
                Options = new List<ChallengeOption>
                {
                    new ChallengeOption("A. 1 ms", false, "Incorrect: There is also an initial idle period before P1 arrives."),
                    new ChallengeOption("B. 2 ms", false, "Incorrect: This only accounts for T=0..2, omitting the gap between P1 and P2."),
                    new ChallengeOption("C. 3 ms", true, "CORRECT! CPU is idle during T=0..2 (2 ms before P1) and T=5..6 (1 ms between P1 finish and P2 arrival). Total idle = 2 + 1 = 3 ms."),
                    new ChallengeOption("D. 5 ms", false, "Incorrect: 5 ms is total busy execution time (3 ms + 2 ms = 5 ms).")
                },
                DetailedExplanation = "Gantt chart breakdown: T=0..2 is IDLE (2 ms, queue empty). T=2..5 is P1 (3 ms). T=5..6 is IDLE (1 ms, P2 has not arrived yet). T=6..8 is P2 (2 ms). Total CPU Idle Time = 2 + 1 = 3 ms (CPU Utilization = 5/8 = 62.5%)."
            },

            // =========================================================
            // EXPERT TIER (Numerical SRTF Turnaround, Priority Aging, MLFQ)
            // =========================================================
            new SchedulingChallenge
            {
                Id = "exp_1",
                Title = "Priority Starvation & Aging Solution",
                Difficulty = ChallengeDifficulty.Expert,
                AlgorithmName = "Priority",
                Description = "Under Priority Scheduling, what standard OS mechanism prevents indefinite postponement (starvation) of low-priority tasks?",
                BaseXP = 450,
                IsUnlocked = true,
                Processes = new List<OSProcess>
                {
                    new OSProcess("P1", 0, 6, 3, new Color(0.25f, 0.65f, 1.0f)),
                    new OSProcess("P2", 1, 4, 1, new Color(0.35f, 0.85f, 0.45f)),
                    new OSProcess("P3", 2, 5, 2, new Color(0.95f, 0.35f, 0.45f))
                },
                Options = new List<ChallengeOption>
                {
                    new ChallengeOption("A. Memory Compaction", false, "Incorrect: Compaction solves external memory fragmentation, not CPU process starvation."),
                    new ChallengeOption("B. Aging (gradually incrementing priority of waiting processes)", true, "CORRECT! Aging systematically boosts the priority of processes that wait in the ready queue, guaranteeing eventual execution."),
                    new ChallengeOption("C. Busy waiting spinlocks", false, "Incorrect: Busy waiting wastes CPU cycles without solving starvation."),
                    new ChallengeOption("D. Resetting the time quantum to infinity", false, "Incorrect: That converts Round Robin into FCFS and does not resolve priority starvation.")
                },
                DetailedExplanation = "In Priority Scheduling, a steady stream of higher-priority tasks can leave low-priority tasks waiting indefinitely (starvation). The OS combats this via 'Aging': periodically elevating the priority of long-waiting tasks so they eventually reach top priority."
            },
            new SchedulingChallenge
            {
                Id = "exp_2",
                Title = "Numerical SRTF Turnaround Calculation",
                Difficulty = ChallengeDifficulty.Expert,
                AlgorithmName = "SRTF",
                Description = "Processes P1 (AT=0, BT=5), P2 (AT=1, BT=3), and P3 (AT=2, BT=2) run under SRTF. What is the Turnaround Time (TAT) of P1?",
                BaseXP = 480,
                IsUnlocked = true,
                Processes = new List<OSProcess>
                {
                    new OSProcess("P1", 0, 5, 0, new Color(0.25f, 0.65f, 1.0f)),
                    new OSProcess("P2", 1, 3, 0, new Color(0.35f, 0.85f, 0.45f)),
                    new OSProcess("P3", 2, 2, 0, new Color(1.0f, 0.60f, 0.20f))
                },
                Options = new List<ChallengeOption>
                {
                    new ChallengeOption("A. 5 ms", false, "Incorrect: 5 ms is P1's initial burst time, but it was preempted by P2 and P3."),
                    new ChallengeOption("B. 6 ms", false, "Incorrect: At T=6, P3 completes; P1 only resumes execution at T=6."),
                    new ChallengeOption("C. 8 ms", false, "Incorrect: P1 needs 4 more units of CPU after resuming at T=6, reaching T=10."),
                    new ChallengeOption("D. 10 ms", true, "CORRECT! P1 runs 0..1 (rem 4). P2 preempts and runs 1..4 (CT=4). P3 runs 4..6 (CT=6). P1 resumes 6..10 (CT=10). TAT(P1) = CT - AT = 10 - 0 = 10 ms.")
                },
                DetailedExplanation = "Gantt trace: T=0..1: P1 runs (rem 4). T=1: P2 arrives (BT=3 < 4), preempts P1, runs T=1..4 (CT=4). T=4: P3 (BT=2) runs T=4..6 (CT=6). T=6: P1 resumes with remaining 4 ms and completes at T=10. TAT(P1) = CT - AT = 10 - 0 = 10 ms."
            },
            new SchedulingChallenge
            {
                Id = "exp_3",
                Title = "Multilevel Feedback Queue Demotion",
                Difficulty = ChallengeDifficulty.Expert,
                AlgorithmName = "MLFQ",
                Description = "In a Multilevel Feedback Queue (MLFQ), how does the scheduler dynamically penalize CPU-bound processes?",
                BaseXP = 500,
                IsUnlocked = true,
                Processes = new List<OSProcess>
                {
                    new OSProcess("P1", 0, 7, 0, new Color(0.25f, 0.65f, 1.0f)),
                    new OSProcess("P2", 2, 2, 0, new Color(0.35f, 0.85f, 0.45f))
                },
                Options = new List<ChallengeOption>
                {
                    new ChallengeOption("A. Schedulers abort long jobs and restart them during low-load hours", false, "Incorrect: MLFQ does not terminate or kill user processes for taking long CPU bursts."),
                    new ChallengeOption("B. Processes that exhaust their entire time quantum demote to lower-priority queues", true, "CORRECT! MLFQ demotes processes that use up their time quantum to lower priority tiers, reserving top tiers for interactive I/O jobs."),
                    new ChallengeOption("C. The OS doubles their assigned memory allocation to force early completion", false, "Incorrect: Memory allocation does not alter CPU scheduling priority queue assignment."),
                    new ChallengeOption("D. It converts CPU-bound tasks into kernel threads automatically", false, "Incorrect: Kernel thread conversions do not change process behavior or scheduling tiers.")
                },
                DetailedExplanation = "MLFQ adapts to process behavior without requiring advance burst knowledge: processes that surrender the CPU before quantum expiration (I/O-bound) stay in high-priority queues, while CPU-bound tasks that exhaust their slice demote to lower-priority queues with larger quanta."
            }
        };
    }
}
