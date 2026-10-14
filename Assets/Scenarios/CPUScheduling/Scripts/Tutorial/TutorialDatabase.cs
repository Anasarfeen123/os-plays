using System.Collections.Generic;
using UnityEngine;

public static class TutorialDatabase
{
    private static List<ConceptLesson> concepts;
    private static List<AlgorithmLesson> algorithms;

    public static List<ConceptLesson> GetConcepts()
    {
        if (concepts == null) InitializeConcepts();
        return concepts;
    }

    public static List<AlgorithmLesson> GetAlgorithms()
    {
        if (algorithms == null) InitializeAlgorithms();
        return algorithms;
    }

    private static void InitializeConcepts()
    {
        concepts = new List<ConceptLesson>
        {
            new ConceptLesson
            {
                Id = "process",
                Title = "1. What is a Process?",
                Category = "Fundamental Concepts",
                ShortExplanation = "A process is a program in active execution. Unlike passive code on disk, a process has memory state, program counter, and execution status.",
                FormulaOrRule = "Process = Code + Data + PCB (Process Control Block) + Execution State",
                Example = "When you launch a web browser, the OS creates a Process (e.g. PID: 4028) with its own memory space and execution threads.",
                VisualKey = "PROGRAM (Disk) ---> [ OS LOADER ] ---> PROCESS (Memory + CPU)",
                Challenge = new QuizChallenge(
                    "Which of the following best distinguishes a process from a program?",
                    "Program on SSD vs Running Application in RAM",
                    new List<QuizOption>
                    {
                        new QuizOption("A program is dynamic; a process is static.", false, "Incorrect: Programs stored on disk are static files."),
                        new QuizOption("A process is a program in execution with state & resources.", true, "Correct! A process is an active instance of a program with assigned CPU registers, memory, and a PCB."),
                        new QuizOption("They are identical synonyms in all operating systems.", false, "Incorrect: A program is passive code; a process is an active runtime entity.")
                    },
                    "A process is an active execution entity with dedicated state, registers, and memory."
                )
            },
            new ConceptLesson
            {
                Id = "burst",
                Title = "2. What is a CPU Burst?",
                Category = "Fundamental Concepts",
                ShortExplanation = "A CPU burst is the duration of continuous time a process actively computes on the CPU before requesting I/O or terminating.",
                FormulaOrRule = "Process Lifecycle = Alternate cycles of (CPU Burst <--> I/O Burst)",
                Example = "P1 requires 6 ms of computation (CPU Burst = 6), then waits 10 ms for a disk read (I/O Burst = 10).",
                VisualKey = "[ CPU BURST: 6ms ] ===> [ I/O WAIT: 10ms ] ===> [ CPU BURST: 3ms ]",
                Challenge = new QuizChallenge(
                    "If a process performs heavy calculations for 8ms then saves a file to disk, what is its initial CPU burst?",
                    "Scenario: Process calculates math matrices for 8ms, then waits for disk write.",
                    new List<QuizOption>
                    {
                        new QuizOption("8 milliseconds", true, "Correct! The 8ms of continuous computation is the CPU burst duration."),
                        new QuizOption("The entire time until disk write completes", false, "Incorrect: Waiting for disk write is an I/O burst, not a CPU burst."),
                        new QuizOption("0 milliseconds", false, "Incorrect: The process was actively executing for 8ms.")
                    },
                    "CPU burst refers strictly to active compute time on the processor."
                )
            },
            new ConceptLesson
            {
                Id = "ready_queue",
                Title = "3. What is the Ready Queue?",
                Category = "Fundamental Concepts",
                ShortExplanation = "The ready queue is a data structure (FIFO, Priority list, etc.) containing all processes in main memory that are ready and waiting to execute on the CPU.",
                FormulaOrRule = "Ready Queue State: { P in RAM | State == Ready and waiting for CPU dispatch }",
                Example = "P1 is currently executing on CPU. P2 and P3 have arrived and wait in the Ready Queue: [ P2 -> P3 ].",
                VisualKey = "READY QUEUE: [ P3 | Arr=2 ] -> [ P2 | Arr=1 ] =====> [ CPU: P1 ]",
                Challenge = new QuizChallenge(
                    "Where does a newly arrived process wait before being granted the CPU?",
                    "Scenario: A process is loaded from disk into memory and is ready to run.",
                    new List<QuizOption>
                    {
                        new QuizOption("In the Waiting/Blocked Queue", false, "Incorrect: The Waiting Queue holds processes blocked on I/O events."),
                        new QuizOption("In the Ready Queue", true, "Correct! Arrived processes ready for computation reside in the Ready Queue."),
                        new QuizOption("Directly in the CPU registers", false, "Incorrect: Only the currently dispatched process occupies CPU registers.")
                    },
                    "The Ready Queue holds all runnable processes waiting for CPU allocation."
                )
            },
            new ConceptLesson
            {
                Id = "scheduling",
                Title = "4. What is CPU Scheduling?",
                Category = "Fundamental Concepts",
                ShortExplanation = "CPU scheduling is the core OS mechanism that selects which ready process gets CPU time and for how long, maximizing CPU utilization and system responsiveness.",
                FormulaOrRule = "Scheduler Objective: Maximize CPU Utilization & Throughput, Minimize Waiting & Turnaround Time.",
                Example = "When P1 finishes or blocks, the OS Short-Term Scheduler picks P2 from the ready queue using an algorithm like Round Robin or SJF.",
                VisualKey = "READY QUEUE ===> [ CPU SCHEDULER (FCFS/SJF/RR) ] ===> [ CPU CORE ]",
                Challenge = new QuizChallenge(
                    "What is the primary role of the CPU Scheduler (Short-Term Scheduler)?",
                    "Scenario: Multiple processes are loaded in memory competing for 1 CPU core.",
                    new List<QuizOption>
                    {
                        new QuizOption("To select the next ready process to execute on the CPU.", true, "Correct! The CPU scheduler decides process execution order and allocation."),
                        new QuizOption("To compile source code into machine binaries.", false, "Incorrect: That is the compiler's job."),
                        new QuizOption("To power down the computer when idle.", false, "Incorrect: That is power management.")
                    },
                    "The scheduler selects which runnable process gets CPU time next."
                )
            },
            new ConceptLesson
            {
                Id = "preemption",
                Title = "5. What is Preemption?",
                Category = "Fundamental Concepts",
                ShortExplanation = "Preemption is the ability of the OS to forcibly interrupt a running process and return it to the Ready Queue before its CPU burst finishes (e.g. on time slice expiry or higher-priority arrival).",
                FormulaOrRule = "Non-Preemptive: Process holds CPU until completion/I/O.\nPreemptive: OS can interrupt and switch at any time.",
                Example = "In Round Robin, P1 runs for its 4ms quantum. The timer interrupt fires, and the OS preempts P1, moving it to the back of the ready queue.",
                VisualKey = "[ CPU: P1 ] ---( Timer / Interrupt )---> [ Ready Queue: P1 ] & [ CPU: P2 ]",
                Challenge = new QuizChallenge(
                    "What happens in a preemptive scheduling algorithm when a higher-priority process arrives?",
                    "Scenario: P1 (Pri 3) is running. P2 (Pri 1 - Higher) arrives at T=2.",
                    new List<QuizOption>
                    {
                        new QuizOption("P1 is immediately preempted and returned to the ready queue; P2 gets the CPU.", true, "Correct! Preemption allows the OS to yield the CPU to higher-priority tasks immediately."),
                        new QuizOption("P1 always runs to complete termination while P2 waits.", false, "Incorrect: That would be non-preemptive scheduling."),
                        new QuizOption("P1 is deleted permanently from memory.", false, "Incorrect: P1 is only paused and queued.")
                    },
                    "Preemptive scheduling allows interruption of the currently executing process."
                )
            },
            new ConceptLesson
            {
                Id = "context_switch",
                Title = "6. What is Context Switching?",
                Category = "Fundamental Concepts",
                ShortExplanation = "A context switch is the OS procedure of saving the execution state (registers, program counter, stack pointer) of a running process and loading the saved state of the next process.",
                FormulaOrRule = "Context Switch Overhead = Pure OS overhead (no useful user work is performed during switch).",
                Example = "Saving P1's registers to PCB1 (0.01ms) -> Loading P2's registers from PCB2 (0.01ms) -> Resuming P2 execution.",
                VisualKey = "[ Save P1 State to PCB1 ] ===> [ OS Switch Overhead ] ===> [ Restore P2 State from PCB2 ]",
                Challenge = new QuizChallenge(
                    "Why is frequent context switching considered costly in operating systems?",
                    "Scenario: A scheduling algorithm has an extremely tiny time quantum (e.g. 0.001ms).",
                    new List<QuizOption>
                    {
                        new QuizOption("It incurs pure CPU overhead where no process does actual work.", true, "Correct! Time spent saving and restoring PCB registers is overhead time."),
                        new QuizOption("It destroys all data in the computer's hard drive.", false, "Incorrect: Context switching only affects CPU register state in memory."),
                        new QuizOption("It increases the burst time of processes.", false, "Incorrect: Process burst times remain constant.")
                    },
                    "Context switching takes time and consumes CPU cycles without progressing user processes."
                )
            },
            new ConceptLesson
            {
                Id = "waiting_time",
                Title = "7. What is Waiting Time (WT)?",
                Category = "Performance Metrics",
                ShortExplanation = "Waiting Time is the total amount of time a process spends waiting in the Ready Queue to gain access to the CPU.",
                FormulaOrRule = "Waiting Time (WT) = Turnaround Time - Burst Time\nOr: WT = Sum of all ready queue wait intervals",
                Example = "P1 arrives at T=0, has Burst=6. Runs at T=0, finishes at T=6. WT = 6 - 6 = 0.\nP2 arrives at T=1, has Burst=8. Runs at T=6, finishes at T=14. WT = (14 - 1) - 8 = 5.",
                VisualKey = "T=0 [ P1: Runs 0..6 ]\nT=1 [ P2: Waits 1..6 (WT=5) ] -> [ P2: Runs 6..14 ]",
                Challenge = new QuizChallenge(
                    "P3 arrives at T=2 with Burst=7. It executes from T=14 to T=21. What is P3's Waiting Time?",
                    "Process P3: Arrival = 2, Burst = 7, Start = 14, Completion = 21.",
                    new List<QuizOption>
                    {
                        new QuizOption("12 time units", true, "Correct! TAT = 21 - 2 = 19. WT = TAT - Burst = 19 - 7 = 12 (or 14 - 2 = 12)."),
                        new QuizOption("19 time units", false, "Incorrect: 19 is the Turnaround Time (Completion - Arrival)."),
                        new QuizOption("7 time units", false, "Incorrect: 7 is the Burst Time.")
                    },
                    "Waiting Time = Start Time - Arrival Time (for single burst non-preemptive), or TAT - Burst."
                )
            },
            new ConceptLesson
            {
                Id = "turnaround_time",
                Title = "8. What is Turnaround Time (TAT)?",
                Category = "Performance Metrics",
                ShortExplanation = "Turnaround Time is the total interval from the moment a process is submitted (Arrival) to the moment it completes execution.",
                FormulaOrRule = "Turnaround Time (TAT) = Completion Time - Arrival Time\nAlso: TAT = Burst Time + Waiting Time",
                Example = "P2 arrives at T=1 and completes at T=14. Turnaround Time = 14 - 1 = 13 time units.",
                VisualKey = "[ ARRIVAL: T=1 ] ---------------- ( TAT = 13 ) ----------------> [ COMPLETION: T=14 ]",
                Challenge = new QuizChallenge(
                    "P4 arrives at T=3 and completes at T=24. What is its Turnaround Time?",
                    "Process P4: Arrival = 3, Completion = 24.",
                    new List<QuizOption>
                    {
                        new QuizOption("21 time units", true, "Correct! TAT = Completion Time (24) - Arrival Time (3) = 21."),
                        new QuizOption("24 time units", false, "Incorrect: 24 is the Completion Time, not Turnaround Time."),
                        new QuizOption("27 time units", false, "Incorrect: Turnaround time is the difference, not sum.")
                    },
                    "Turnaround Time measures total lifespan of a process in the system."
                )
            },
            new ConceptLesson
            {
                Id = "response_time",
                Title = "9. What is Response Time (RT)?",
                Category = "Performance Metrics",
                ShortExplanation = "Response Time is the time elapsed from process arrival until the CPU begins executing it for the very first time.",
                FormulaOrRule = "Response Time (RT) = First Start Time - Arrival Time",
                Example = "In an interactive OS, when you click a button, Response Time is how fast the system begins processing your request, even if it hasn't completed yet.",
                VisualKey = "[ ARRIVAL: T=1 ] ===== ( RT = 5 ) =====> [ FIRST START: T=6 ] ====> [ Completion: T=14 ]",
                Challenge = new QuizChallenge(
                    "In interactive desktop and mobile systems, why is minimizing Response Time critical?",
                    "Scenario: User UI interaction and quick feedback on button clicks.",
                    new List<QuizOption>
                    {
                        new QuizOption("It ensures the user gets immediate feedback and the system feels responsive.", true, "Correct! Rapid response time gives users instant interactive feedback."),
                        new QuizOption("It reduces the file size of applications.", false, "Incorrect: Response time is purely a scheduling metric."),
                        new QuizOption("It guarantees zero CPU usage.", false, "Incorrect: The CPU is used to respond.")
                    },
                    "Response time measures how quickly the system starts responding to a request."
                )
            }
        };
    }

    private static void InitializeAlgorithms()
    {
        algorithms = new List<AlgorithmLesson>
        {
            new AlgorithmLesson
            {
                Id = "fcfs",
                Name = "1. First-Come, First-Served (FCFS)",
                Definition = "The simplest non-preemptive algorithm that allocates the CPU to processes in the exact chronological order of their arrival.",
                SelectionMechanism = "FIFO Queue: The process at the head of the arrival queue runs to completion before the next process starts.",
                IsPreemptive = false,
                Advantages = "• Extremely simple to implement and understand.\n• Zero scheduling decision overhead.\n• Guaranteed fairness in arrival order.",
                Disadvantages = "• Convoy Effect: Short processes get stuck waiting behind long CPU-heavy processes.\n• High average waiting time.",
                StarvationRisk = "None. Every process is guaranteed to execute once earlier arrivals finish.",
                TypicalUseCase = "Batch systems where execution order does not require interactive responsiveness.",
                Challenge = new QuizChallenge(
                    "Given FCFS with P1 (Arr=0, Bst=10) and P2 (Arr=1, Bst=2), what is P2's waiting time?",
                    "FCFS Execution:\nP1 arrives at 0 (Bst 10)\nP2 arrives at 1 (Bst 2)",
                    new List<QuizOption>
                    {
                        new QuizOption("9 time units", true, "Correct! P1 runs from T=0 to T=10. P2 arrives at T=1, so P2 waits from T=1 to T=10 (WT = 10 - 1 = 9). This is the classic Convoy Effect!"),
                        new QuizOption("0 time units", false, "Incorrect: P2 cannot preempt P1 in FCFS."),
                        new QuizOption("10 time units", false, "Incorrect: P2 arrived at T=1, so it waited 9 time units, not 10.")
                    },
                    "In FCFS, short jobs wait behind long jobs (Convoy Effect)."
                )
            },
            new AlgorithmLesson
            {
                Id = "sjf",
                Name = "2. Shortest Job First (SJF)",
                Definition = "Non-preemptive algorithm that selects the waiting process with the smallest initial burst time.",
                SelectionMechanism = "Greedy Burst Selection: When CPU is free, picks candidate with min(BurstTime). Ties broken by Arrival Time.",
                IsPreemptive = false,
                Advantages = "• Provably optimal: Produces the minimum average waiting time among all non-preemptive algorithms.",
                Disadvantages = "• Requires knowing/predicting future CPU burst times in advance.\n• Long processes may suffer from starvation.",
                StarvationRisk = "Yes. Continuous arrival of short jobs will starve long processes indefinitely.",
                TypicalUseCase = "Long-term batch job queues where job runtimes are estimated from previous runs.",
                Challenge = new QuizChallenge(
                    "At T=6, CPU becomes free. Ready queue has P2(Bst=8), P3(Bst=7), P4(Bst=3). Which runs next?",
                    "SJF Decision at T=6:\nAvailable: P2 (Bst 8), P3 (Bst 7), P4 (Bst 3)",
                    new List<QuizOption>
                    {
                        new QuizOption("P4 (Burst = 3)", true, "Correct! SJF selects the waiting job with the shortest burst time (3 < 7 < 8)."),
                        new QuizOption("P2 (Burst = 8)", false, "Incorrect: P2 has the longest burst time."),
                        new QuizOption("P3 (Burst = 7)", false, "Incorrect: P3 is longer than P4.")
                    },
                    "SJF strictly prioritizes the process with the shortest initial burst duration."
                )
            },
            new AlgorithmLesson
            {
                Id = "srtf",
                Name = "3. Shortest Remaining Time First (SRTF)",
                Definition = "The preemptive version of SJF. If a new process arrives with a shorter remaining burst than the currently executing process, the CPU preempts the running process.",
                SelectionMechanism = "Dynamic min(RemainingTime) evaluated whenever a new process arrives or current completes.",
                IsPreemptive = true,
                Advantages = "• Optimal average waiting time for preemptive systems.\n• Highly responsive for newly arriving short interactive jobs.",
                Disadvantages = "• Frequent preemption causes context switch overhead.\n• Still risks starving long processes.",
                StarvationRisk = "Yes. Ongoing streams of short bursts will indefinitely delay long jobs.",
                TypicalUseCase = "Interactive systems where short tasks need immediate execution.",
                Challenge = new QuizChallenge(
                    "P1 is running with 4ms remaining. P2 arrives with Burst=2ms. What happens under SRTF?",
                    "SRTF State at Arrival:\nP1 (Remaining = 4ms)\nP2 (Arrives with Burst = 2ms)",
                    new List<QuizOption>
                    {
                        new QuizOption("P1 is preempted; P2 takes the CPU immediately.", true, "Correct! Since P2's remaining time (2ms) is strictly less than P1's (4ms), SRTF preempts P1."),
                        new QuizOption("P1 finishes its remaining 4ms while P2 waits.", false, "Incorrect: That would be non-preemptive SJF."),
                        new QuizOption("Both processes execute simultaneously.", false, "Incorrect: Single CPU core executes one process at a time.")
                    },
                    "SRTF preempts whenever a shorter remaining burst enters the ready queue."
                )
            },
            new AlgorithmLesson
            {
                Id = "priority",
                Name = "4. Priority Scheduling",
                Definition = "Allocates the CPU based on priority numbers assigned to each process. (Standard OS rule: Lower integer = Higher priority).",
                SelectionMechanism = "Highest Priority (Lowest integer value). Can be configured as Preemptive or Non-Preemptive.",
                IsPreemptive = true,
                Advantages = "• Critical real-time or system processes can be favored over background tasks.\n• Highly configurable.",
                Disadvantages = "• Indefinite blocking / Starvation of low-priority tasks.\n• Priority inversion issues.",
                StarvationRisk = "High. Solved using 'Aging' (gradually increasing priority of waiting processes).",
                TypicalUseCase = "Real-time systems, OS kernel task scheduling, and priority-driven workloads.",
                Challenge = new QuizChallenge(
                    "P1 has Priority 3 (running). P2 arrives with Priority 1 (High). Under preemptive priority, what occurs?",
                    "Priority Rule: Lower number = Higher priority.\nP1: Pri 3 (Running)\nP2: Pri 1 (Arrived)",
                    new List<QuizOption>
                    {
                        new QuizOption("P2 preempts P1 and claims the CPU immediately.", true, "Correct! Priority 1 is higher than Priority 3, triggering an immediate preemption."),
                        new QuizOption("P1 ignores P2 because it is already on the CPU.", false, "Incorrect: In preemptive priority scheduling, higher priority processes preempt running tasks."),
                        new QuizOption("P2's priority is demoted to 4.", false, "Incorrect: Process priorities are not demoted upon arrival.")
                    },
                    "In preemptive priority scheduling, higher priority processes take immediate precedence."
                )
            },
            new AlgorithmLesson
            {
                Id = "rr",
                Name = "5. Round Robin (RR)",
                Definition = "Designed for time-sharing systems. Each process is assigned a fixed time slice (Time Quantum 'q'). When the quantum expires, the process is preempted and placed at the back of the ready queue.",
                SelectionMechanism = "Circular FIFO Queue + Timer Interrupt every 'q' time units.",
                IsPreemptive = true,
                Advantages = "• Starvation-free: Every process gets a guaranteed regular slice of CPU time.\n• Excellent response time for interactive users.",
                Disadvantages = "• Performance heavily depends on Time Quantum choice (Too small = excessive context switch overhead; Too large = degrades to FCFS).",
                StarvationRisk = "None. Circular cycling ensures all processes progress.",
                TypicalUseCase = "General-purpose operating systems (Windows, Linux, macOS) for user-space threads.",
                Challenge = new QuizChallenge(
                    "If Time Quantum q=4 and P1 has Burst=6, how many CPU bursts will P1 need to complete?",
                    "Round Robin with q=4:\nP1 Burst = 6ms",
                    new List<QuizOption>
                    {
                        new QuizOption("2 separate bursts (runs 4ms, is preempted, then runs final 2ms)", true, "Correct! P1 runs for its 4ms slice (rem=2), goes to queue back, and later runs 2ms to finish."),
                        new QuizOption("1 burst of 6ms continuously", false, "Incorrect: Quantum expires at 4ms."),
                        new QuizOption("4 bursts of 1ms each", false, "Incorrect: Quantum is 4ms, not 1ms.")
                    },
                    "Round Robin slices execution into intervals of at most Time Quantum 'q'."
                )
            },
            new AlgorithmLesson
            {
                Id = "mlq",
                Name = "6. Multilevel Queue (MLQ)",
                Definition = "Partitions the ready queue into multiple separate queues based on process characteristics (e.g. System, Interactive, Batch). Each queue can have its own scheduling algorithm.",
                SelectionMechanism = "Fixed Priority between queues (e.g. Q1 > Q2) + Individual algorithm inside each queue (e.g. Q1=RR, Q2=FCFS).",
                IsPreemptive = true,
                Advantages = "• Tailors scheduling strategy to different classes of workloads (e.g. RR for UI, FCFS for batch).",
                Disadvantages = "• Inflexible: Processes are permanently assigned to one queue and cannot migrate.\n• Lower queues can starve if higher queues are always busy.",
                StarvationRisk = "Yes. Lower priority queues starve if higher queues remain continuously populated.",
                TypicalUseCase = "Workstation OS separating foreground interactive apps from background batch tasks.",
                Challenge = new QuizChallenge(
                    "Under strict fixed-priority Multilevel Queue, what happens if Queue 1 (Interactive) is never empty?",
                    "Queue 1: High Priority (Always has waiting processes)\nQueue 2: Low Priority (Batch jobs)",
                    new List<QuizOption>
                    {
                        new QuizOption("Queue 2 processes will experience starvation and never run.", true, "Correct! Because Q1 has absolute priority, Q2 will never receive CPU time until Q1 is completely empty."),
                        new QuizOption("Queue 2 processes automatically jump to Queue 1.", false, "Incorrect: Standard MLQ does not allow processes to move between queues (that is MLFQ)."),
                        new QuizOption("The CPU splits time 50/50 automatically.", false, "Incorrect: Fixed priority strictly serves the higher queue first.")
                    },
                    "Fixed-priority Multilevel Queue can cause starvation for lower-tier queues without time sharing."
                )
            },
            new AlgorithmLesson
            {
                Id = "mlfq",
                Name = "7. Multilevel Feedback Queue (MLFQ)",
                Definition = "The most sophisticated CPU scheduler. Processes start in the highest priority queue and can dynamically move between queues based on their CPU burst behavior and history.",
                SelectionMechanism = "• New jobs enter Q0 (highest priority, smallest quantum).\n• If a job exhausts its quantum without completing, it is demoted to Q1.\n• CPU-bound jobs sink to lower queues; I/O-bound jobs stay in top queues.\n• Periodic 'Priority Boost' (aging) moves all jobs back to Q0 to prevent starvation.",
                IsPreemptive = true,
                Advantages = "• Automatically learns process behavior without needing burst time predictions.\n• Fast response for interactive jobs + high throughput for batch jobs.\n• Starvation-free with Priority Boost.",
                Disadvantages = "• Most complex to configure (number of queues, quanta per queue, demotion/promotion rules).",
                StarvationRisk = "Prevented through periodic Priority Boost (aging).",
                TypicalUseCase = "Modern production OS schedulers (Solaris, BSD, Linux CFS concepts, Windows NT).",
                Challenge = new QuizChallenge(
                    "How does MLFQ prevent CPU-heavy background jobs from starving while keeping interactive apps fast?",
                    "MLFQ Rules: Short quantum in top queue, demote on quantum exhaustion, periodic priority boost.",
                    new List<QuizOption>
                    {
                        new QuizOption("Interactive jobs finish in top queues quickly, long jobs sink to lower queues, and periodic boosts prevent starvation.", true, "Correct! Short interactive jobs finish in Q0 with instant response; compute-heavy tasks sink to Q2; aging periodically boosts all jobs."),
                        new QuizOption("By permanently deleting long processes after 10 seconds.", false, "Incorrect: Processes must run to legitimate completion."),
                        new QuizOption("By treating all processes with simple FCFS.", false, "Incorrect: MLFQ uses adaptive multi-tier feedback.")
                    },
                    "MLFQ adaptively rewards interactive I/O jobs and uses aging to avoid starvation."
                )
            }
        };
    }
}
