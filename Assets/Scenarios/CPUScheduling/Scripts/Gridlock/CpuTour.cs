using System.Collections;
using System.Linq;
using Gridlock.Core;
using Gridlock.Guide;
using Gridlock.XR;
using UnityEngine;

/// <summary>
/// Narrated guided tour of the CPU scheduling lab (about two minutes): processes, the ready queue, the core and the
/// algorithm desks; then the learner presses RUN and watches the Gantt chart fill in; the narrator explains waiting time
/// and suggests comparing the algorithms. Same guide screens, marker and voice as the Deadlock and Banker tours.
/// </summary>
public class CpuTour : NarratedTour
{
    const string TourKey = "cpu";

    public static bool Seen
    {
        get => IsSeen(TourKey);
        set => SetSeen(TourKey, value);
    }

    [SerializeField] ScenarioButtons buttons;
    [Tooltip("Seconds of silence on the hands-on step before the hint, and again before the guide presses RUN itself.")]
    [SerializeField] float runWaitSeconds = 15f;
    [Tooltip("Longest the tour waits for the run to finish before it moves on.")]
    [SerializeField] float runSeconds = 45f;

    protected override string Key => TourKey;
    protected override string HandsOnStep => "cpu_turn";
    protected override bool Counted(string id) => id != "cpu_hint";

    public void Init(ScenarioButtons scenarioButtons) => buttons = scenarioButtons;

    static Transform Find<T>() where T : Component
    {
        var found = FindAnyObjectByType<T>();
        return found != null ? found.transform : null;
    }

    static Transform Desk(string name) =>
        FindObjectsByType<AlgorithmWorkstation>(FindObjectsSortMode.None).FirstOrDefault(w => w.name == name)?.transform;

    /// <summary>A point above a thing in the lab, for the marker (the top of its renderers).</summary>
    static Vector3 Above(Transform t, float lift = 0.35f)
    {
        var renderers = t.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return t.position + Vector3.up * lift;
        var b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        return new Vector3(b.center.x, b.max.y + lift, b.center.z);
    }

    void Point(Transform target, string label)
    {
        if (target != null) marker.Point(Above(target), label);
        else marker.Hide();
    }

    protected override IEnumerator Script()
    {
        var sim = SimulationController.Instance;
        // The arena assembles itself over the first frames.
        float ready = Time.time + 5f;
        yield return new WaitUntil(() => (FindAnyObjectByType<PhysicalCPUCore>() != null && SimulationController.Instance != null) || Time.time > ready);
        sim = SimulationController.Instance;
        yield return new WaitForSeconds(1f);

        Say("cpu_welcome");
        Point(Find<PhysicalCPUCore>(), "CPU CORE");
        yield return EndOfLine();

        Say("cpu_processes");
        Point(Find<PhysicalProcessBlock>(), "PROCESS");
        yield return EndOfLine();

        Say("cpu_queue");
        Point(Find<PhysicalReadyQueue>(), "READY QUEUE");
        yield return EndOfLine();

        Say("cpu_core");
        Point(Find<PhysicalCPUCore>(), "CPU CORE");
        yield return EndOfLine();

        Say("cpu_desks");
        var fcfs = Desk("FCFS");
        if (fcfs != null) fcfs.GetComponent<AlgorithmWorkstation>().Interact(); // the run below uses FCFS
        Point(fcfs, "ALGORITHM");
        yield return EndOfLine();

        // Hands-on: the learner starts the run.
        Say("cpu_turn");
        Point(PhysicalArenaConsole.Instance != null ? PhysicalArenaConsole.Instance.transform : null, "CONSOLE");
        ShowActions(new GuideScreen.Action("RUN", ViewColor.Safe, Run));
        bool Started() => sim != null && (sim.IsRunning || (sim.CurrentAlgorithm != null && sim.CurrentAlgorithm.CurrentTime > 0));
        float deadline = Time.time + LineLength + runWaitSeconds;
        yield return new WaitUntil(() => Started() || Time.time > deadline);
        if (!Started())
        {
            Say("cpu_hint");
            deadline = Time.time + LineLength + runWaitSeconds;
            yield return new WaitUntil(() => Started() || Time.time > deadline);
            if (!Started()) Run();
        }
        HideActions();

        Say("cpu_gantt");
        Point(Find<PhysicalGanttBoard>(), "GANTT CHART");
        if (sim != null) sim.SetSimulationSpeed(3f); // the whole run fits in the tour
        bool Done() => sim == null || sim.CurrentAlgorithm == null || sim.CurrentAlgorithm.IsCompleted;
        float end = Time.time + runSeconds;
        yield return new WaitUntil(() => (Done() && LineElapsed > LineLength) || Time.time > end);
        if (sim != null) sim.SetSimulationSpeed(1f);

        Say("cpu_result");
        marker.Hide();
        yield return EndOfLine();

        Say("cpu_outro");
        Point(Desk("SRTF"), "TRY ANOTHER");
        yield return EndOfLine();
    }

    void Run()
    {
        var sim = SimulationController.Instance;
        if (sim != null && sim.IsRunning) return;
        if (buttons != null) buttons.PressA();
    }

    protected override void OnEnd(bool finished)
    {
        var sim = SimulationController.Instance;
        if (sim != null) sim.SetSimulationSpeed(1f);
    }
}
