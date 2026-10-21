using System.Linq;
using Gridlock.UI;
using Gridlock.XR;
using UnityEngine;

/// <summary>
/// GRIDLOCK glue for the CPU scheduling lab: the controller face buttons (A run / pause, B step, X the simulator window),
/// Esc for the simulator window before the GRIDLOCK menu, and the menu leaving the mouse free while that window is open.
/// </summary>
public class CpuScenarioAdapter : MonoBehaviour
{
    [SerializeField] ScenarioButtons buttons;
    [SerializeField] ScenarioDesktopControls desktop;

    bool simulatorWasOpen;

    static bool SimulatorOpen()
    {
        var ui = SchedulingUIController.Instance;
        return ui != null && ui.isActiveAndEnabled && ui.IsUIPanelActive;
    }

    void Awake()
    {
        if (buttons != null)
        {
            buttons.A += () => Console(PhysicalArenaConsole.ConsoleAction.RunPause);
            buttons.B += () => Console(PhysicalArenaConsole.ConsoleAction.Step);
            buttons.X += ToggleSimulator;
        }
        if (desktop != null) desktop.OwnUIOpen = SimulatorOpen;
        Gridlock.AR.ARStage.InfoProviders.Add(Describe);
        // The simulator window closes itself on Esc; the menu must not open on the same press.
        SystemMenu.EscapeTaken = () => simulatorWasOpen;
    }

    void OnDestroy()
    {
        SystemMenu.EscapeTaken = null;
        Gridlock.AR.ARStage.InfoProviders.Remove(Describe);
    }

    /// <summary>The info card for a tapped process block, algorithm desk or the CPU core (phone AR).</summary>
    static (string title, string body)? Describe(Collider hit)
    {
        var sim = SimulationController.Instance;
        int now = sim != null && sim.CurrentAlgorithm != null ? sim.CurrentAlgorithm.CurrentTime : 0;
        var block = hit.GetComponentInParent<PhysicalProcessBlock>();
        if (block != null && block.Process != null)
        {
            var p = block.Process;
            string state = p.IsCompleted ? $"Finished at t = {p.CompletionTime}, after waiting {p.WaitingTime} ticks."
                         : p.State == ProcessState.Running ? $"Running: {p.RemainingTime} of {p.BurstTime} ticks left."
                         : p.ArrivalTime > now ? $"Arrives at t = {p.ArrivalTime}."
                         : $"Ready, waiting for the CPU ({p.RemainingTime} ticks left).";
            return ($"Process {p.Id}", $"Burst {p.BurstTime} ticks · arrives at t = {p.ArrivalTime} · priority {p.Priority}\n{state}");
        }
        var desk = hit.GetComponentInParent<AlgorithmWorkstation>();
        if (desk != null)
            return (desk.DisplayName, desk.CreateAlgorithmInstance().Description);
        var core = hit.GetComponentInParent<PhysicalCPUCore>();
        if (core != null)
        {
            var running = sim != null && sim.CurrentAlgorithm != null ? sim.CurrentAlgorithm.RunningProcess : null;
            return ("CPU core", running != null ? $"Running {running.Id} at t = {now}." : $"Idle at t = {now}. Press RUN to start.");
        }
        return null;
    }

    void LateUpdate() => simulatorWasOpen = SimulatorOpen();

    static void Console(PhysicalArenaConsole.ConsoleAction action)
    {
        // Nothing chosen at a desk yet: run First Come First Served rather than ignore the button.
        var sim = SimulationController.Instance;
        if (sim != null && sim.CurrentAlgorithm == null)
        {
            var desk = FindObjectsByType<AlgorithmWorkstation>(FindObjectsSortMode.None).FirstOrDefault(w => w.name == "FCFS");
            if (desk != null) desk.Interact();
        }
        if (PhysicalArenaConsole.Instance != null) PhysicalArenaConsole.Instance.ExecuteButtonAction(action);
        AudioManager.Instance?.PlaySfx(AudioManager.SfxType.Click);
    }

    static void ToggleSimulator()
    {
        var toggle = FindAnyObjectByType<SchedulingUIToggle>();
        if (toggle != null) toggle.ToggleUI();
    }
}
