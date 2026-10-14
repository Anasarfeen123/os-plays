using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SimulationController : MonoBehaviour
{
    public static SimulationController Instance { get; private set; }

    [Header("Simulation Settings")]
    [Range(0.1f, 10f)]
    public float simulationSpeed = 1.0f; // 1.0 = 1 tick/sec, 2.0 = 2 ticks/sec, etc.
    public bool autoPlayOnStart = false;

    // State
    public bool IsRunning { get; private set; } = false;
    public bool IsPaused { get; private set; } = false;
    public SchedulingAlgorithm CurrentAlgorithm { get; private set; }
    public List<OSProcess> InitialProcesses { get; private set; } = new List<OSProcess>();

    // Events
    public event Action<SchedulingAlgorithm> OnAlgorithmChanged;
    public event Action OnSimulationTicked;
    public event Action OnSimulationCompleted;
    public event Action OnSimulationReset;
    public event Action<bool> OnPlayStateChanged; // true = running, false = paused/stopped
    public event Action<List<OSProcess>> OnProcessesChanged;

    private Coroutine simCoroutine = null;

    private static readonly int[] ProcessPalette = new int[] { 0, 1, 2, 3, 4, 5, 6, 7 };

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            // Duplicate instance: remove only this component so the host
            // GameObject (e.g. the SchedulingUI canvas) is preserved and can
            // safely coexist with the runtime SimulationController.
            Destroy(this);
        }
    }

    void Start()
    {
        if (InitialProcesses == null || InitialProcesses.Count == 0)
        {
            SetDefaultProcesses();
            // GRIDLOCK fix: a workstation may already have chosen an algorithm (before these processes existed), which
            // left it running an empty list: the queue, CPU and Gantt board stayed empty. Load the processes into it.
            if (CurrentAlgorithm != null)
            {
                OnProcessesChanged?.Invoke(InitialProcesses);
                RestartSimulation();
            }
        }

        if (CurrentAlgorithm == null)
        {
            SetAlgorithm(new FCFSAlgorithm());
        }

        if (autoPlayOnStart)
        {
            StartSimulation();
        }
    }

    public void SetDefaultProcesses()
    {
        // Standard benchmark process set as required:
        // P1: AT=0, BT=5, Pri=2
        // P2: AT=1, BT=3, Pri=1
        // P3: AT=2, BT=2, Pri=3
        // P4: AT=4, BT=4, Pri=2
        InitialProcesses = new List<OSProcess>
        {
            new OSProcess("P1", 0, 5, 2, new Color(0.22f, 0.58f, 0.98f)), // Sky Blue
            new OSProcess("P2", 1, 3, 1, new Color(0.20f, 0.82f, 0.48f)), // Emerald
            new OSProcess("P3", 2, 2, 3, new Color(1.00f, 0.58f, 0.18f)), // Orange
            new OSProcess("P4", 4, 4, 2, new Color(0.85f, 0.35f, 0.95f))  // Purple
        };
    }

    public void SetProcesses(List<OSProcess> processes)
    {
        InitialProcesses = new List<OSProcess>();
        foreach (var p in processes)
        {
            InitialProcesses.Add(p.Clone());
        }
        OnProcessesChanged?.Invoke(InitialProcesses);
        RestartSimulation();
    }

    /// <summary>
    /// Generates a fresh randomized process set (arrival, burst, priority) and
    /// restarts the simulation. count is clamped to [2, 8].
    /// </summary>
    public void RandomizeProcesses(int count = 5)
    {
        SetProcesses(GenerateRandomProcesses(count));
    }

    /// <summary>
    /// Generates a randomized set of processes with arrival time, burst time and priority.
    /// Arrival times are sorted ascending; burst times and priorities are varied.
    /// </summary>
    public static List<OSProcess> GenerateRandomProcesses(int count = 5)
    {
        count = Mathf.Clamp(count, 2, 8);
        var processes = new List<OSProcess>();

        int arrivalCursor = 0;
        for (int i = 0; i < count; i++)
        {
            // Stagger arrivals by a small random gap so multiple processes can coincide or queue.
            arrivalCursor += (i == 0) ? 0 : UnityEngine.Random.Range(0, 4);
            int burst = UnityEngine.Random.Range(2, 11);      // 2..10
            int priority = UnityEngine.Random.Range(1, 6);    // 1..5 (lower = higher priority)
            string id = $"P{i + 1}";
            var p = new OSProcess(id, arrivalCursor, burst, priority);
            p.ProcessColor = OSProcess.GetDefaultColor(id);
            processes.Add(p);
        }

        return processes;
    }

    public void SetAlgorithm(SchedulingAlgorithm algorithm)
    {
        if (algorithm == null) return;

        bool wasRunning = IsRunning && !IsPaused;
        PauseSimulation();

        CurrentAlgorithm = algorithm;
        CurrentAlgorithm.Initialize(InitialProcesses);

        OnAlgorithmChanged?.Invoke(CurrentAlgorithm);
        OnSimulationReset?.Invoke();

        if (wasRunning)
        {
            StartSimulation();
        }
    }

    public void StartSimulation()
    {
        if (CurrentAlgorithm == null) return;

        if (CurrentAlgorithm.IsCompleted)
        {
            RestartSimulation();
        }

        IsRunning = true;
        IsPaused = false;
        OnPlayStateChanged?.Invoke(true);

        if (simCoroutine != null) StopCoroutine(simCoroutine);
        simCoroutine = StartCoroutine(SimulationLoop());
    }

    public void PauseSimulation()
    {
        IsPaused = true;
        if (simCoroutine != null)
        {
            StopCoroutine(simCoroutine);
            simCoroutine = null;
        }
        OnPlayStateChanged?.Invoke(false);
    }

    public void ResumeSimulation()
    {
        if (CurrentAlgorithm == null) return;
        if (CurrentAlgorithm.IsCompleted) return;

        IsRunning = true;
        IsPaused = false;
        OnPlayStateChanged?.Invoke(true);

        if (simCoroutine != null) StopCoroutine(simCoroutine);
        simCoroutine = StartCoroutine(SimulationLoop());
    }

    public void RestartSimulation()
    {
        PauseSimulation();
        IsRunning = false;
        IsPaused = false;

        if (CurrentAlgorithm != null)
        {
            CurrentAlgorithm.Initialize(InitialProcesses);
        }

        OnSimulationReset?.Invoke();
        OnSimulationTicked?.Invoke();
    }

    public void StepForward()
    {
        if (CurrentAlgorithm == null) return;

        if (CurrentAlgorithm.IsCompleted)
        {
            return;
        }

        PauseSimulation();
        bool active = CurrentAlgorithm.Step();
        OnSimulationTicked?.Invoke();

        if (!active || CurrentAlgorithm.IsCompleted)
        {
            IsRunning = false;
            OnSimulationCompleted?.Invoke();
        }
    }

    public void SetSimulationSpeed(float speedMultiplier)
    {
        simulationSpeed = Mathf.Clamp(speedMultiplier, 0.1f, 10f);
    }

    private IEnumerator SimulationLoop()
    {
        while (IsRunning && !IsPaused && CurrentAlgorithm != null && !CurrentAlgorithm.IsCompleted)
        {
            float delay = 1.0f / Mathf.Max(0.1f, simulationSpeed);
            yield return new WaitForSeconds(delay);

            bool active = CurrentAlgorithm.Step();
            OnSimulationTicked?.Invoke();

            if (!active || CurrentAlgorithm.IsCompleted)
            {
                IsRunning = false;
                IsPaused = false;
                OnSimulationCompleted?.Invoke();
                OnPlayStateChanged?.Invoke(false);
                break;
            }
        }
        simCoroutine = null;
    }
}
