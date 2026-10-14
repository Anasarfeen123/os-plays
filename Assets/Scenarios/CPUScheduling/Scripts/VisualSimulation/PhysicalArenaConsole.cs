using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Physical 3D control console in the simulation arena.
/// Houses physical 3D push-buttons (Run, Step, Reset, Speed, Quantum, Add Process)
/// and a 3D TextMeshPro status readout.
/// </summary>
public class PhysicalArenaConsole : MonoBehaviour
{
    public static PhysicalArenaConsole Instance { get; private set; }

    public enum ConsoleAction
    {
        RunPause,
        Step,
        Reset,
        SpeedCycle,
        QuantumDec,
        QuantumInc,
        AddProcess,
        ResetDefaultProcesses,
        RandomizeProcesses
    }

    [Header("3D Console Readout")]
    [SerializeField] private TextMeshPro readoutTMP;

    private int roundRobinQuantum = 4;
    private float[] speedOptions = new float[] { 1.0f, 2.0f, 4.0f, 0.5f };
    private int currentSpeedIndex = 0;

    public int RoundRobinQuantum => roundRobinQuantum;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(this);
    }

    private void Start()
    {
        UpdateReadout();

        var sim = SimulationController.Instance;
        if (sim != null)
        {
            sim.OnSimulationTicked += UpdateReadout;
            sim.OnSimulationTicked += HandleSimulationTick;
            sim.OnAlgorithmChanged += (algo) => { UpdateReadout(); AudioManager.Instance?.PlaySfx(AudioManager.SfxType.Click); };
            sim.OnPlayStateChanged += (running) => UpdateReadout();
            sim.OnSimulationReset += UpdateReadout;
            sim.OnSimulationCompleted += () => { UpdateReadout(); AudioManager.Instance?.PlaySfx(AudioManager.SfxType.Complete); };
            sim.OnProcessesChanged += (procs) => AudioManager.Instance?.PlaySfx(AudioManager.SfxType.Arrival);
        }
    }

    private void HandleSimulationTick()
    {
        // Subtle per-tick feedback while the simulation runs.
        AudioManager.Instance?.PlaySfx(AudioManager.SfxType.Tick);
    }

    public void ExecuteButtonAction(ConsoleAction action)
    {
        var sim = SimulationController.Instance;
        if (sim == null) return;

        switch (action)
        {
            case ConsoleAction.RunPause:
                if (sim.IsRunning && !sim.IsPaused) sim.PauseSimulation();
                else sim.StartSimulation();
                break;

            case ConsoleAction.Step:
                sim.StepForward();
                break;

            case ConsoleAction.Reset:
                sim.RestartSimulation();
                break;

            case ConsoleAction.SpeedCycle:
                currentSpeedIndex = (currentSpeedIndex + 1) % speedOptions.Length;
                sim.SetSimulationSpeed(speedOptions[currentSpeedIndex]);
                break;

            case ConsoleAction.QuantumDec:
                roundRobinQuantum = Mathf.Max(1, roundRobinQuantum - 1);
                ApplyQuantum();
                break;

            case ConsoleAction.QuantumInc:
                roundRobinQuantum = Mathf.Min(10, roundRobinQuantum + 1);
                ApplyQuantum();
                break;

            case ConsoleAction.AddProcess:
                AddNewProcess(sim);
                break;

            case ConsoleAction.ResetDefaultProcesses:
                sim.SetDefaultProcesses();
                sim.RestartSimulation();
                break;

            case ConsoleAction.RandomizeProcesses:
                sim.RandomizeProcesses(5);
                break;
        }

        AudioManager.Instance?.PlaySfx(AudioManager.SfxType.Click);
        UpdateReadout();
    }

    private void ApplyQuantum()
    {
        var sim = SimulationController.Instance;
        if (sim != null && sim.CurrentAlgorithm is RoundRobinAlgorithm rr)
        {
            rr.TimeQuantum = roundRobinQuantum;
            sim.RestartSimulation();
        }
    }

    private void AddNewProcess(SimulationController sim)
    {
        if (sim == null) return;

        // Build a fresh copy of the current process list
        var newList = new System.Collections.Generic.List<OSProcess>();
        foreach (var existing in sim.InitialProcesses)
            newList.Add(existing.Clone());

        int nextIndex = newList.Count + 1;
        int arr = UnityEngine.Random.Range(0, 6);
        int burst = UnityEngine.Random.Range(2, 9);
        int pri = UnityEngine.Random.Range(1, 5);

        // Pick a unique color from the process palette
        var newProc = new OSProcess($"P{nextIndex}", arr, burst, pri);
        newList.Add(newProc);

        // SetProcesses restarts the simulation cleanly
        sim.SetProcesses(newList);
        Debug.Log($"[Arena] Added P{nextIndex}: AT={arr}, BT={burst}, Pri={pri}");
    }

    public void UpdateReadout()
    {
        if (readoutTMP == null) return;

        var sim = SimulationController.Instance;
        if (sim == null || sim.CurrentAlgorithm == null)
        {
            readoutTMP.text = "<b>ARENA CONSOLE</b>\nReady";
            return;
        }

        string algoName = sim.CurrentAlgorithm.Name;
        string state = sim.IsRunning ? (sim.IsPaused ? "<color=#FFA500>PAUSED</color>" : "<color=#00FF66>RUNNING</color>") : (sim.CurrentAlgorithm.IsCompleted ? "<color=#00E5FF>COMPLETED</color>" : "<color=#AAAAAA>IDLE</color>");
        float speed = speedOptions[currentSpeedIndex];

        string text = $"<b>{algoName}</b>\n" +
                      $"STATUS: {state} | CLOCK: T = {sim.CurrentAlgorithm.CurrentTime}s | SPEED: {speed}x\n" +
                      $"PROCESSES: {sim.InitialProcesses.Count} | TIME QUANTUM: {roundRobinQuantum}s";

        readoutTMP.text = text;
    }

    /// <summary>
    /// Physical 3D button script placed on the console surface.
    /// </summary>
    public class ConsoleButton : MonoBehaviour, IInteractable
    {
        public ConsoleAction action;
        public string actionName = "Button";
        public Color buttonColor = Color.white;

        private MeshRenderer meshRenderer;
        private Vector3 initialLocalPos;
        private bool isPressed = false;

        private void Awake()
        {
            meshRenderer = GetComponent<MeshRenderer>();
            initialLocalPos = transform.localPosition;
        }

        public string PromptText => $"Press [E] to {actionName}";

        public void Interact()
        {
            if (PhysicalArenaConsole.Instance != null)
            {
                PhysicalArenaConsole.Instance.ExecuteButtonAction(action);
            }
            AnimatePress();
        }

        private void AnimatePress()
        {
            if (!isPressed && gameObject.activeInHierarchy)
            {
                StartCoroutine(PressRoutine());
            }
        }

        private IEnumerator PressRoutine()
        {
            isPressed = true;
            if (initialLocalPos == Vector3.zero) initialLocalPos = transform.localPosition;
            if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();

            transform.localPosition = initialLocalPos - new Vector3(0, 0.035f, 0);
            if (meshRenderer != null)
            {
                meshRenderer.material.color = Color.white;
            }

            yield return new WaitForSeconds(0.12f);

            transform.localPosition = initialLocalPos;
            if (meshRenderer != null)
            {
                meshRenderer.material.color = buttonColor;
            }
            isPressed = false;
        }
    }
}
