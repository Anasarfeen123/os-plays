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
        // The simulator window closes itself on Esc; the menu must not open on the same press.
        SystemMenu.EscapeTaken = () => simulatorWasOpen;
    }

    void OnDestroy() => SystemMenu.EscapeTaken = null;

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
