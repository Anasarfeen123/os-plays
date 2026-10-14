using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class SchedulingUIController : MonoBehaviour
{
    public static SchedulingUIController Instance { get; private set; }

    [Header("Mode Switching")]
    [SerializeField] private GameObject simulatorPanel;
    [SerializeField] private GameObject tutorialPanel;
    [SerializeField] private GameObject gamePanel;
    [SerializeField] private Button simulatorTabButton;
    [SerializeField] private Button tutorialTabButton;
    [SerializeField] private Button gameTabButton;
    [SerializeField] private TutorialUIController tutorialUIController;
    [SerializeField] private GameUIController gameUIController;

    [Header("Root References")]
    [SerializeField] private GameObject uiPanel;
    [SerializeField] private PlayerMovement playerMovement;

    public bool IsUIPanelActive => uiPanel != null && uiPanel.activeSelf;

    [Header("Header Elements")]
    [SerializeField] private Text titleText;
    [SerializeField] private Text timeText;
    [SerializeField] private Text algorithmInfoText;

    [Header("CPU State & Running Process")]
    [SerializeField] private Image cpuStateBadge;
    [SerializeField] private Text cpuStateText;
    [SerializeField] private Image runningProcessBadge;
    [SerializeField] private Text runningProcessText;
    [SerializeField] private Text metricsSummaryText;

    [Header("Ready Queue")]
    [SerializeField] private Transform readyQueueContainer;
    [SerializeField] private GameObject queueItemPrefab;
    [SerializeField] private Text readyQueueSummaryText;

    [Header("Gantt Chart")]
    [SerializeField] private Transform ganttContainer;
    [SerializeField] private GameObject ganttSegmentPrefab;
    [SerializeField] private ScrollRect ganttScrollRect;

    [Header("Process Table")]
    [SerializeField] private Transform tableRowContainer;
    [SerializeField] private GameObject tableRowPrefab;

    [Header("Simulation Controls")]
    [SerializeField] private Button playButton;
    [SerializeField] private Text playButtonText;
    [SerializeField] private Button pauseButton;
    [SerializeField] private Button stepButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button randomizeButton;
    [SerializeField] private Slider speedSlider;
    [SerializeField] private Text speedLabelText;

    [Header("Algorithm Buttons")]
    [SerializeField] private List<Button> algorithmButtons = new List<Button>();

    // Object Pooling for UI elements to prevent GC allocations
    private List<GameObject> queueItemPool = new List<GameObject>();
    private List<GameObject> ganttSegmentPool = new List<GameObject>();
    private List<GameObject> tableRowPool = new List<GameObject>();

    public SimulationController Sim => SimulationController.Instance ?? GetComponent<SimulationController>() ?? FindAnyObjectByType<SimulationController>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(gameObject);
    }

    private void Start()
    {
        if (playerMovement == null)
        {
            var player = GameObject.Find("Player");
            if (player != null) playerMovement = player.GetComponent<PlayerMovement>();
        }

        // Connect to SimulationController
        var sim = Sim;
        if (sim != null)
        {
            sim.OnSimulationTicked += HandleSimulationUpdate;
            sim.OnSimulationReset += HandleSimulationUpdate;
            sim.OnAlgorithmChanged += HandleAlgorithmChanged;
            sim.OnPlayStateChanged += HandlePlayStateChanged;
            sim.OnSimulationCompleted += HandleSimulationCompleted;
        }

        // Mode tab buttons
        if (simulatorTabButton != null) simulatorTabButton.onClick.AddListener(ShowSimulatorMode);
        if (tutorialTabButton != null) tutorialTabButton.onClick.AddListener(ShowTutorialMode);
        if (gameTabButton != null) gameTabButton.onClick.AddListener(ShowGameMode);

        SetupControlListeners();
        ShowSimulatorMode();
        UpdateAllVisuals();
    }

    public void ShowSimulatorMode()
    {
        if (simulatorPanel != null) simulatorPanel.SetActive(true);
        if (tutorialPanel != null) tutorialPanel.SetActive(false);
        if (gamePanel != null) gamePanel.SetActive(false);

        SetTabHighlight(simulatorTabButton, true);
        SetTabHighlight(tutorialTabButton, false);
        SetTabHighlight(gameTabButton, false);

        UpdateAllVisuals();
    }

    public void ShowTutorialMode()
    {
        if (simulatorPanel != null) simulatorPanel.SetActive(false);
        if (tutorialPanel != null) tutorialPanel.SetActive(true);
        if (gamePanel != null) gamePanel.SetActive(false);

        SetTabHighlight(simulatorTabButton, false);
        SetTabHighlight(tutorialTabButton, true);
        SetTabHighlight(gameTabButton, false);

        if (tutorialUIController != null)
        {
            tutorialUIController.RenderCurrentLesson();
        }
    }

    public void ShowGameMode()
    {
        if (simulatorPanel != null) simulatorPanel.SetActive(false);
        if (tutorialPanel != null) tutorialPanel.SetActive(false);
        if (gamePanel != null) gamePanel.SetActive(true);

        SetTabHighlight(simulatorTabButton, false);
        SetTabHighlight(tutorialTabButton, false);
        SetTabHighlight(gameTabButton, true);

        if (gameUIController != null)
        {
            gameUIController.OpenGame();
        }
    }

    private void SetTabHighlight(Button btn, bool active)
    {
        if (btn != null)
        {
            var c = btn.colors;
            c.normalColor = active ? new Color(0.20f, 0.55f, 0.95f) : new Color(0.12f, 0.16f, 0.22f);
            btn.colors = c;
        }
    }

    private void OnDestroy()
    {
        var sim = Sim;
        if (sim != null)
        {
            sim.OnSimulationTicked -= HandleSimulationUpdate;
            sim.OnSimulationReset -= HandleSimulationUpdate;
            sim.OnAlgorithmChanged -= HandleAlgorithmChanged;
            sim.OnPlayStateChanged -= HandlePlayStateChanged;
            sim.OnSimulationCompleted -= HandleSimulationCompleted;
        }
    }

    private void Update()
    {
        if (uiPanel != null && uiPanel.activeSelf)
        {
            bool closePressed = false;
            bool spacePressed = false;
            bool stepPressed = false;
            bool restartPressed = false;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                closePressed = Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.eKey.wasPressedThisFrame;
                spacePressed = Keyboard.current.spaceKey.wasPressedThisFrame;
                stepPressed = Keyboard.current.rightArrowKey.wasPressedThisFrame || Keyboard.current.sKey.wasPressedThisFrame;
                restartPressed = Keyboard.current.rKey.wasPressedThisFrame;
            }
#else
            closePressed = Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.E);
            spacePressed = Input.GetKeyDown(KeyCode.Space);
            stepPressed = Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.S);
            restartPressed = Input.GetKeyDown(KeyCode.R);
#endif

            if (closePressed)
            {
                CloseUI();
            }
            else if (spacePressed)
            {
                TogglePlayPause();
            }
            else if (stepPressed)
            {
                StepSimulation();
            }
            else if (restartPressed)
            {
                RestartSimulation();
            }
        }
    }

    public void OpenUI()
    {
        if (uiPanel != null)
        {
            uiPanel.SetActive(true);
            if (playerMovement != null) playerMovement.SetControlActive(false);
            UpdateAllVisuals();
        }
    }

    public void CloseUI()
    {
        if (uiPanel != null)
        {
            uiPanel.SetActive(false);
            if (playerMovement != null) playerMovement.SetControlActive(true);
        }
    }

    private void SetupControlListeners()
    {
        if (playButton != null) playButton.onClick.AddListener(TogglePlayPause);
        if (pauseButton != null) pauseButton.onClick.AddListener(PauseSimulation);
        if (stepButton != null) stepButton.onClick.AddListener(StepSimulation);
        if (restartButton != null) restartButton.onClick.AddListener(RestartSimulation);
        if (randomizeButton != null) randomizeButton.onClick.AddListener(RandomizeProcesses);

        if (speedSlider != null)
        {
            speedSlider.onValueChanged.AddListener(val =>
            {
                if (SimulationController.Instance != null)
                {
                    SimulationController.Instance.SetSimulationSpeed(val);
                }
                if (speedLabelText != null)
                {
                    speedLabelText.text = $"{val:F1}x";
                }
            });
        }
    }

    public void SelectAlgorithmByIndex(int index)
    {
        var sim = Sim;
        if (sim == null) return;

        SchedulingAlgorithm algo = null;
        switch (index)
        {
            case 0: algo = new FCFSAlgorithm(); break;
            case 1: algo = new SJFAlgorithm(); break;
            case 2: algo = new SRTFAlgorithm(); break;
            case 3: algo = new PriorityAlgorithm(isPreemptive: true); break;
            case 4: algo = new RoundRobinAlgorithm(quantum: 4); break;
            case 5: algo = new MLQAlgorithm(); break;
            case 6: algo = new MLFQAlgorithm(new int[] { 2, 4, 8 }); break;
            default: algo = new FCFSAlgorithm(); break;
        }

        sim.SetAlgorithm(algo);
    }

    private void HandleAlgorithmChanged(SchedulingAlgorithm algo)
    {
        if (titleText != null && algo != null)
        {
            titleText.text = $"CPU SCHEDULING SIMULATOR — {algo.Name.ToUpper()}";
        }
        if (algorithmInfoText != null && algo != null)
        {
            algorithmInfoText.text = algo.Description;
        }
        UpdateAllVisuals();
    }

    private void HandlePlayStateChanged(bool isRunning)
    {
        if (playButtonText != null)
        {
            playButtonText.text = isRunning ? "PAUSE" : "START";
        }
    }

    private void HandleSimulationUpdate()
    {
        UpdateAllVisuals();
    }

    private void HandleSimulationCompleted()
    {
        UpdateAllVisuals();
        if (playButtonText != null) playButtonText.text = "FINISHED";
    }

    public void TogglePlayPause()
    {
        var sim = Sim;
        if (sim == null) return;

        if (sim.IsRunning && !sim.IsPaused)
        {
            sim.PauseSimulation();
        }
        else
        {
            sim.StartSimulation();
        }
    }

    public void PauseSimulation()
    {
        Sim?.PauseSimulation();
    }

    public void StepSimulation()
    {
        Sim?.StepForward();
    }

    public void RestartSimulation()
    {
        Sim?.RestartSimulation();
    }

    public void RandomizeProcesses()
    {
        var sim = Sim;
        if (sim != null)
        {
            sim.RandomizeProcesses(5);
            AudioManager.Instance?.PlaySfx(AudioManager.SfxType.Arrival);
        }
    }

    public void SetSpeedMultiplier(float speed)
    {
        var sim = Sim;
        if (sim != null)
        {
            sim.SetSimulationSpeed(speed);
        }
        if (speedSlider != null) speedSlider.value = speed;
        if (speedLabelText != null) speedLabelText.text = $"{speed:F1}x";
    }

    public void UpdateAllVisuals()
    {
        var sim = Sim;
        if (sim == null || sim.CurrentAlgorithm == null) return;

        var algo = sim.CurrentAlgorithm;

        // 1. Time Indicator
        if (timeText != null)
        {
            timeText.text = $"SIMULATION TIME: T = {algo.CurrentTime} ({(algo.IsCompleted ? "COMPLETED" : (sim.IsRunning && !sim.IsPaused ? "RUNNING" : "PAUSED"))})";
        }

        // 2. CPU State
        UpdateCpuStateVisuals(algo);

        // 3. Ready Queue
        UpdateReadyQueueVisuals(algo);

        // 4. Gantt Chart
        UpdateGanttChartVisuals(algo);

        // 5. Process Table & Metrics
        UpdateProcessTableVisuals(algo);
    }

    private void UpdateCpuStateVisuals(SchedulingAlgorithm algo)
    {
        if (cpuStateText != null)
        {
            switch (algo.CurrentCpuState)
            {
                case CpuState.Running:
                    cpuStateText.text = "CPU STATE: RUNNING";
                    if (cpuStateBadge != null) cpuStateBadge.color = new Color(0.15f, 0.75f, 0.35f, 1f);
                    break;
                case CpuState.Idle:
                    cpuStateText.text = algo.IsCompleted ? "CPU STATE: FINISHED" : "CPU STATE: IDLE";
                    if (cpuStateBadge != null) cpuStateBadge.color = new Color(0.5f, 0.5f, 0.55f, 1f);
                    break;
                case CpuState.ContextSwitch:
                    cpuStateText.text = "CPU STATE: CONTEXT SWITCH";
                    if (cpuStateBadge != null) cpuStateBadge.color = new Color(0.95f, 0.6f, 0.1f, 1f);
                    break;
            }
        }

        if (runningProcessText != null)
        {
            if (algo.RunningProcess != null)
            {
                var p = algo.RunningProcess;
                runningProcessText.text = $"PROCESS: {p.Id}  (Remaining: {p.RemainingTime}/{p.BurstTime} | Priority: {p.Priority})";
                if (runningProcessBadge != null)
                {
                    runningProcessBadge.color = p.ProcessColor;
                }
            }
            else
            {
                runningProcessText.text = algo.IsCompleted ? "ALL PROCESSES COMPLETED" : "NO PROCESS RUNNING (IDLE)";
                if (runningProcessBadge != null)
                {
                    runningProcessBadge.color = new Color(0.25f, 0.25f, 0.3f, 0.8f);
                }
            }
        }

        if (metricsSummaryText != null)
        {
            var res = new SchedulingResult(algo.Name)
            {
                Processes = algo.AllProcesses,
                GanttChart = algo.GanttChart,
                TotalDuration = algo.CurrentTime
            };

            metricsSummaryText.text = $"Avg Waiting: {res.AverageWaitingTime:F2} | Avg Turnaround: {res.AverageTurnaroundTime:F2} | Avg Response: {res.AverageResponseTime:F2} | CPU Util: {res.CpuUtilization:F1}%";
        }
    }

    private void UpdateReadyQueueVisuals(SchedulingAlgorithm algo)
    {
        if (readyQueueContainer == null) return;

        var allQueues = algo.GetAllQueues();
        int totalItems = 0;
        foreach (var q in allQueues) totalItems += q.Count;

        if (readyQueueSummaryText != null)
        {
            if (allQueues.Count > 1)
            {
                var sb = new StringBuilder("QUEUES: ");
                for (int i = 0; i < allQueues.Count; i++)
                {
                    sb.Append($"Q{i}: [ {string.Join(", ", allQueues[i].ConvertAll(p => p.Id))} ]  ");
                }
                readyQueueSummaryText.text = sb.ToString();
            }
            else
            {
                readyQueueSummaryText.text = $"READY QUEUE ({totalItems} waiting)";
            }
        }

        EnsurePoolSize(queueItemPool, queueItemPrefab, readyQueueContainer, totalItems);

        int index = 0;
        for (int qIdx = 0; qIdx < allQueues.Count; qIdx++)
        {
            var queue = allQueues[qIdx];
            for (int i = 0; i < queue.Count; i++)
            {
                var p = queue[i];
                var itemObj = queueItemPool[index];
                itemObj.SetActive(true);

                var img = itemObj.GetComponent<Image>();
                if (img != null) img.color = p.ProcessColor;

                var txt = itemObj.GetComponentInChildren<Text>();
                if (txt != null)
                {
                    txt.text = (allQueues.Count > 1)
                        ? $"Q{qIdx}:{p.Id} (Rem:{p.RemainingTime})"
                        : $"{p.Id} [Rem:{p.RemainingTime}|Pri:{p.Priority}]";
                }
                index++;
            }
        }

        for (int i = index; i < queueItemPool.Count; i++)
        {
            queueItemPool[i].SetActive(false);
        }
    }

    private void UpdateGanttChartVisuals(SchedulingAlgorithm algo)
    {
        if (ganttContainer == null) return;

        var timeline = algo.GanttChart;
        EnsurePoolSize(ganttSegmentPool, ganttSegmentPrefab, ganttContainer, timeline.Count);

        for (int i = 0; i < timeline.Count; i++)
        {
            var seg = timeline[i];
            var segObj = ganttSegmentPool[i];
            segObj.SetActive(true);

            var img = segObj.GetComponent<Image>();
            if (img != null)
            {
                img.color = seg.IsIdle ? new Color(0.2f, 0.22f, 0.28f, 0.9f) : seg.BlockColor;
            }

            var texts = segObj.GetComponentsInChildren<Text>();
            // texts[0]: PID, texts[1]: StartTime, texts[2]: EndTime (or single text)
            if (texts.Length >= 3)
            {
                texts[0].text = seg.ProcessId;
                texts[1].text = seg.StartTime.ToString();
                texts[2].text = seg.EndTime.ToString();
            }
            else if (texts.Length > 0)
            {
                texts[0].text = $"{seg.ProcessId}\n[{seg.StartTime}-{seg.EndTime}]";
            }

            // Scale segment width proportionally to duration
            var rt = segObj.GetComponent<RectTransform>();
            if (rt != null)
            {
                float baseWidth = 45f;
                rt.sizeDelta = new Vector2(Mathf.Max(40f, seg.Duration * baseWidth), 60f);
            }
        }

        for (int i = timeline.Count; i < ganttSegmentPool.Count; i++)
        {
            ganttSegmentPool[i].SetActive(false);
        }

        if (ganttScrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            ganttScrollRect.horizontalNormalizedPosition = 1f; // Auto-scroll to current end
        }
    }

    private void UpdateProcessTableVisuals(SchedulingAlgorithm algo)
    {
        if (tableRowContainer == null) return;

        var processes = algo.AllProcesses;
        EnsurePoolSize(tableRowPool, tableRowPrefab, tableRowContainer, processes.Count);

        for (int i = 0; i < processes.Count; i++)
        {
            var p = processes[i];
            var rowObj = tableRowPool[i];
            rowObj.SetActive(true);

            var bg = rowObj.GetComponent<Image>();
            if (bg != null)
            {
                if (p == algo.RunningProcess)
                {
                    bg.color = new Color(0.18f, 0.38f, 0.55f, 0.95f); // Highlight running row
                }
                else if (p.IsCompleted)
                {
                    bg.color = new Color(0.12f, 0.20f, 0.16f, 0.8f); // Completed row
                }
                else
                {
                    bg.color = (i % 2 == 0)
                        ? new Color(0.12f, 0.14f, 0.20f, 0.85f)
                        : new Color(0.09f, 0.11f, 0.16f, 0.85f);
                }
            }

            var txt = rowObj.GetComponentInChildren<Text>();
            if (txt != null)
            {
                string stateStr = p.State.ToString();
                if (p == algo.RunningProcess) stateStr = "RUNNING";
                else if (p.IsCompleted) stateStr = "DONE";

                string stStr = p.StartTime >= 0 ? p.StartTime.ToString() : "-";
                string ctStr = p.CompletionTime >= 0 ? p.CompletionTime.ToString() : "-";
                string tatStr = p.CompletionTime >= 0 ? p.TurnaroundTime.ToString() : "-";
                string wtStr = p.CompletionTime >= 0 ? p.WaitingTime.ToString() : "-";
                string rtStr = p.StartTime >= 0 ? p.ResponseTime.ToString() : "-";

                txt.text = string.Format("{0,-5} | {1,4} | {2,4} | {3,4} | {4,4} | {5,-7} | {6,3} | {7,3} | {8,3} | {9,3} | {10,3}",
                    p.Id, p.ArrivalTime, p.BurstTime, p.RemainingTime, p.Priority, stateStr, stStr, ctStr, tatStr, wtStr, rtStr);
            }
        }

        for (int i = processes.Count; i < tableRowPool.Count; i++)
        {
            tableRowPool[i].SetActive(false);
        }
    }

    private void EnsurePoolSize(List<GameObject> pool, GameObject prefab, Transform parent, int requiredCount)
    {
        if (prefab == null || parent == null) return;

        while (pool.Count < requiredCount)
        {
            var obj = Instantiate(prefab, parent);
            obj.SetActive(false);
            pool.Add(obj);
        }
    }
}
