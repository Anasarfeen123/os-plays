using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Master coordinator for the 3D physical scheduling arena in the OS Lab.
/// Bridges algorithm workstation selection, SimulationController events,
/// physical 3D process blocks, CPU core execution platform, and 3D Gantt board.
/// Ensures all text elements in the scene are properly scaled and cleans up legacy/oversized objects.
/// </summary>
public class PhysicalArenaManager : MonoBehaviour
{
    public static PhysicalArenaManager Instance { get; private set; }

    [Header("Workstations")]
    [SerializeField] private List<AlgorithmWorkstation> workstations = new List<AlgorithmWorkstation>();

    [Header("Physical Arena Components")]
    [SerializeField] private PhysicalCPUCore cpuCore;
    [SerializeField] private PhysicalReadyQueue readyQueue;
    [SerializeField] private PhysicalGanttBoard ganttBoard;
    [SerializeField] private PhysicalArenaConsole arenaConsole;
    [SerializeField] private PhysicalProcessEditor processEditor;

    private AlgorithmWorkstation activeWorkstation = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureManagerExists()
    {
        // GRIDLOCK: this runs after the app's first scene only (the hub), which must not get an arena. The GRIDLOCK lab
        // scene places its manager itself; the original scene, opened on its own, still gets one here.
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "CPUScheduling") return;
        if (Instance == null && Object.FindAnyObjectByType<PhysicalArenaManager>() == null)
        {
            var arenaRoot = new GameObject("PhysicalArena");
            arenaRoot.AddComponent<PhysicalArenaManager>();
        }
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        CleanUpLegacyObjects();
        CheckAndBuildComponents();
        NormalizeAllSceneText();
    }

    /// <summary>
    /// Removes/disables all legacy objects that should not exist in the 3D physical lab.
    /// </summary>
    public void CleanUpLegacyObjects()
    {
        // 1. Disable old giant SchedulingUI fullscreen Canvas
        var oldUI = GameObject.Find("SchedulingUI");
        if (oldUI != null)
        {
            oldUI.SetActive(false);
            Debug.Log("[PhysicalArenaManager] Disabled legacy SchedulingUI canvas.");
        }

        // 2. Remove old CPU_Scheduling_Station master computer
        var oldStation = GameObject.Find("/OS_Lab/CPU_Scheduling_Station");
        if (oldStation == null) oldStation = GameObject.Find("CPU_Scheduling_Station");
        if (oldStation != null)
        {
            Destroy(oldStation);
            Debug.Log("[PhysicalArenaManager] Removed legacy CPU_Scheduling_Station.");
        }

        // 3. Keep Process_Visualization_Area as the host for the Physical Wall Challenge Station on Wall_West.
        // Clean up any unnamed or orphaned empty objects in the scene.
        var allTransforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var t in allTransforms)
        {
            if (t != null && string.IsNullOrWhiteSpace(t.gameObject.name))
            {
                Destroy(t.gameObject);
            }
        }

        // 4. Remove old SchedulingStation components from workstations (they open the giant UI)
        var oldSchedulingStations = Object.FindObjectsByType<SchedulingStation>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var ss in oldSchedulingStations)
        {
            Destroy(ss);
        }

        // 5. Destroy badly named legacy TMP labels in the scene (preserving station labels and challenge UI)
        var allTMPs = Object.FindObjectsByType<TextMeshPro>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int destroyedCount = 0;
        foreach (var tmp in allTMPs)
        {
            if (tmp == null) continue;
            string n = tmp.gameObject.name;
            if (n == "LABLE" || n == "Text (TMP)" || n == "Text" ||
                n.Contains("Legacy") || (n.Contains("Label") && tmp.fontSize > 4.0f && !n.Contains("StationLabel_TMP") && !n.Contains("Challenge")))
            {
                Destroy(tmp.gameObject);
                destroyedCount++;
            }
        }
        if (destroyedCount > 0)
            Debug.Log($"[PhysicalArenaManager] Cleaned up {destroyedCount} legacy TMP label(s).");
    }

    /// <summary>
    /// Scans all TextMeshPro instances in the scene and enforces proportional, clean font sizes.
    /// </summary>
    public void NormalizeAllSceneText()
    {
        var allTMPs = Object.FindObjectsByType<TextMeshPro>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var tmp in allTMPs)
        {
            if (tmp == null) continue;
            string n = tmp.gameObject.name;

            if (n == "StationLabel_TMP")
            {
                tmp.fontSize = 1.4f;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.fontStyle = FontStyles.Bold;
                var rt = tmp.GetComponent<RectTransform>();
                if (rt != null) rt.sizeDelta = new Vector2(2.5f, 0.5f);
                tmp.transform.localPosition = new Vector3(0, 1.35f, 0.05f);
            }
            else if (n == "StationInfo_TMP")
            {
                tmp.fontSize = 0.75f;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.fontStyle = FontStyles.Normal;
                var rt = tmp.GetComponent<RectTransform>();
                if (rt != null) rt.sizeDelta = new Vector2(2.5f, 0.5f);
                tmp.transform.localPosition = new Vector3(0, -0.22f, 0);
            }
            else if (n == "CPU_Status_TMP")
            {
                var bb = tmp.GetComponent<Billboard3D>();
                if (bb != null) Destroy(bb);
                tmp.fontSize = 0.5f;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.fontStyle = FontStyles.Bold;
                var rt = tmp.GetComponent<RectTransform>();
                if (rt != null) rt.sizeDelta = new Vector2(3.0f, 0.4f);
                tmp.transform.rotation = Quaternion.Euler(90f, 0, 0);
            }
            else if (n == "ReadyQueueHeader_TMP" || n == "IncomingHeader_TMP" || n == "CompletedHeader_TMP")
            {
                var bb = tmp.GetComponent<Billboard3D>();
                if (bb != null) Destroy(bb);
                tmp.fontSize = 0.5f;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.fontStyle = FontStyles.Bold;
                var rt = tmp.GetComponent<RectTransform>();
                if (rt != null) rt.sizeDelta = new Vector2(2.5f, 0.4f);
                tmp.transform.rotation = Quaternion.Euler(90f, 0, 0);
            }
            else if (n == "ConsoleReadout_TMP" || n == "EditorDisplay_TMP")
            {
                tmp.fontSize = 0.4f;
                tmp.alignment = TextAlignmentOptions.Center;
                var rt = tmp.GetComponent<RectTransform>();
                if (rt != null) rt.sizeDelta = new Vector2(1.25f, 0.52f);
            }
            else if (n == "GanttTitle_TMP")
            {
                tmp.fontSize = 0.72f;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.fontStyle = FontStyles.Bold;
                tmp.color = Color.white;
                var rt = tmp.GetComponent<RectTransform>();
                if (rt != null) rt.sizeDelta = new Vector2(10f, 0.9f);
            }
            else if (n == "GanttMetrics_TMP")
            {
                tmp.fontSize = 0.48f;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.fontStyle = FontStyles.Normal;
                tmp.color = new Color(0.6f, 0.85f, 1.0f);
                var rt = tmp.GetComponent<RectTransform>();
                if (rt != null) rt.sizeDelta = new Vector2(10f, 0.6f);
            }
            else if (n == "PID_Text")
            {
                tmp.fontSize = 1.2f;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.fontStyle = FontStyles.Bold;
                var rt = tmp.GetComponent<RectTransform>();
                if (rt != null) rt.sizeDelta = new Vector2(1.0f, 0.35f);
                tmp.transform.localPosition = new Vector3(0, 0.38f, 0);
            }
            else if (n == "Details_Text")
            {
                tmp.fontSize = 0.7f;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.fontStyle = FontStyles.Bold;
                var rt = tmp.GetComponent<RectTransform>();
                if (rt != null) rt.sizeDelta = new Vector2(1.5f, 0.35f);
                tmp.transform.localPosition = new Vector3(0, -0.22f, 0);
            }
            else if (n == "SegmentText")
            {
                tmp.fontSize = 0.7f;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.fontStyle = FontStyles.Bold;
                var rt = tmp.GetComponent<RectTransform>();
                if (rt != null) rt.sizeDelta = new Vector2(0.4f, 0.35f);
            }
            else if (tmp.fontSize > 2.0f)
            {
                tmp.fontSize = 1.2f;
            }
        }
    }

    private void Start()
    {
        CheckAndBuildComponents();

        var sim = SimulationController.Instance;
        if (sim == null)
        {
            var simObj = new GameObject("SimulationController");
            sim = simObj.AddComponent<SimulationController>();
        }

        if (sim != null)
        {
            sim.OnSimulationTicked += HandleSimulationTicked;
            sim.OnSimulationReset += HandleSimulationReset;
            sim.OnAlgorithmChanged += HandleAlgorithmChanged;
            sim.OnPlayStateChanged += HandlePlayStateChanged;
            sim.OnSimulationCompleted += HandleSimulationCompleted;

            UpdatePhysicalArena();
        }

        // Select FCFS workstation as default if none selected
        if (workstations.Count > 0 && activeWorkstation == null)
        {
            var fcfs = workstations.Find(w => w.SelectedAlgorithm == AlgorithmWorkstation.AlgorithmChoice.FCFS);
            if (fcfs != null) SelectAlgorithmFromWorkstation(fcfs);
            else SelectAlgorithmFromWorkstation(workstations[0]);
        }

        CleanupUnwantedButtons();
        EnsurePhysicalButtons();   // Always ensure buttons are present
        EnsureUpgradeButtons();    // Randomize + Voice Tutorial utility podium
        EnsureRuntimeControllers();
        NormalizeAllSceneText();

        // Tutorial Instruction Guide permanently removed per user request
    }

    /// <summary>
    /// Guarantees the 3 console control buttons and 2 editor buttons exist in the 3D scene,
    /// regardless of whether BuildRuntimeArena ran. Finds the console/editor podium and
    /// spawns buttons as direct world-space children if they are missing.
    /// </summary>
    private void EnsurePhysicalButtons()
    {
        // ── Console buttons (Run / Step / Reset) ──────────────────────────────
        GameObject consolePodium = GameObject.Find("Physical_Control_Console");
        if (consolePodium == null)
        {
            // Try to locate via the arenaConsole component's GameObject
            if (arenaConsole != null) consolePodium = arenaConsole.gameObject;
        }

        if (consolePodium != null)
        {
            SpawnButtonIfMissing(consolePodium, "Btn_RunPause",  "RUN / PAUSE",
                new Color(0.15f, 0.85f, 0.35f), PhysicalArenaConsole.ConsoleAction.RunPause,  0);
            SpawnButtonIfMissing(consolePodium, "Btn_Step",      "STEP (+1s)",
                new Color(0.95f, 0.80f, 0.15f), PhysicalArenaConsole.ConsoleAction.Step,      1);
            SpawnButtonIfMissing(consolePodium, "Btn_Reset",     "RESET",
                new Color(0.90f, 0.25f, 0.25f), PhysicalArenaConsole.ConsoleAction.Reset,     2);
        }
        else
        {
            Debug.LogWarning("[PhysicalArenaManager] Could not find Physical_Control_Console to attach buttons.");
        }

        // ── Editor buttons (NextProcess / IncBurst) ───────────────────────────
        GameObject editorPodium = GameObject.Find("Physical_Process_Editor");
        if (editorPodium == null)
        {
            if (processEditor != null) editorPodium = processEditor.gameObject;
        }

        if (editorPodium != null)
        {
            SpawnEditButtonIfMissing(editorPodium, "Btn_NextProc", "SELECT PROCESS",
                new Color(0.30f, 0.65f, 1.00f), PhysicalProcessEditor.EditAction.NextProcess, 0);
            SpawnEditButtonIfMissing(editorPodium, "Btn_IncBurst", "BURST +1s",
                new Color(0.25f, 0.85f, 0.50f), PhysicalProcessEditor.EditAction.IncBurst,   1);
        }
    }

    private void SpawnButtonIfMissing(GameObject podium, string btnName, string label,
        Color color, PhysicalArenaConsole.ConsoleAction action, int slotIndex)
    {
        if (GameObject.Find(btnName) != null) return; // Already exists

        // Place buttons in world-space above the podium top surface
        Vector3 podiumTop = podium.transform.position +
            podium.transform.up * (podium.transform.lossyScale.y * 0.5f + 0.07f);

        // Three slots evenly spaced across the podium width
        float totalWidth = podium.transform.lossyScale.x * 0.55f;
        float step = totalWidth / 2f;
        float offsetX = (slotIndex - 1) * step; // -step, 0, +step

        Vector3 worldPos = podiumTop + podium.transform.right * offsetX;

        GameObject btn = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        btn.name = btnName;
        btn.transform.position = worldPos;
        btn.transform.rotation = podium.transform.rotation;
        btn.transform.localScale = new Vector3(0.20f, 0.05f, 0.20f);

        // Material with emissive glow
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        Material mat = new Material(shader) { color = color };
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", color * 0.6f);
        btn.GetComponent<Renderer>().material = mat;

        // TMP label floating above
        GameObject lblGo = new GameObject("BtnLabel_TMP");
        lblGo.transform.SetParent(btn.transform, false);
        lblGo.transform.localPosition = new Vector3(0, 2.5f, 0);
        lblGo.transform.localRotation = Quaternion.Euler(90f, 0, 0);
        var lbl = lblGo.AddComponent<TextMeshPro>();
        lbl.text = label;
        lbl.fontSize = 0.3f;
        lbl.alignment = TextAlignmentOptions.Center;
        lbl.color = Color.white;
        lbl.fontStyle = FontStyles.Bold;
        var rtLbl = lblGo.GetComponent<RectTransform>();
        rtLbl.sizeDelta = new Vector2(1.4f, 0.28f);

        var btnComp = btn.AddComponent<PhysicalArenaConsole.ConsoleButton>();
        btnComp.action     = action;
        btnComp.actionName = label;
        btnComp.buttonColor = color;

        Debug.Log($"[PhysicalArenaManager] Spawned button: {btnName} at {worldPos}");
    }

    private void SpawnEditButtonIfMissing(GameObject podium, string btnName, string label,
        Color color, PhysicalProcessEditor.EditAction action, int slotIndex)
    {
        if (GameObject.Find(btnName) != null) return;

        Vector3 podiumTop = podium.transform.position +
            podium.transform.up * (podium.transform.lossyScale.y * 0.5f + 0.07f);

        float totalWidth = podium.transform.lossyScale.x * 0.45f;
        float step = totalWidth;
        float offsetX = (slotIndex == 0 ? -step * 0.5f : step * 0.5f);

        Vector3 worldPos = podiumTop + podium.transform.right * offsetX;

        GameObject btn = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        btn.name = btnName;
        btn.transform.position = worldPos;
        btn.transform.rotation = podium.transform.rotation;
        btn.transform.localScale = new Vector3(0.20f, 0.05f, 0.20f);

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        Material mat = new Material(shader) { color = color };
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", color * 0.6f);
        btn.GetComponent<Renderer>().material = mat;

        GameObject lblGo = new GameObject("BtnLabel_TMP");
        lblGo.transform.SetParent(btn.transform, false);
        lblGo.transform.localPosition = new Vector3(0, 2.5f, 0);
        lblGo.transform.localRotation = Quaternion.Euler(90f, 0, 0);
        var lbl = lblGo.AddComponent<TextMeshPro>();
        lbl.text = label;
        lbl.fontSize = 0.3f;
        lbl.alignment = TextAlignmentOptions.Center;
        lbl.color = Color.white;
        lbl.fontStyle = FontStyles.Bold;
        var rtLbl = lblGo.GetComponent<RectTransform>();
        rtLbl.sizeDelta = new Vector2(1.4f, 0.28f);

        var btnComp = btn.AddComponent<PhysicalProcessEditor.ProcessEditButton>();
        btnComp.action      = action;
        btnComp.actionName  = label;
        btnComp.buttonColor = color;

        Debug.Log($"[PhysicalArenaManager] Spawned edit button: {btnName} at {worldPos}");
    }


    /// <summary>
    /// Ensures a small utility podium exists near the control console, hosting
    /// a RANDOMIZE button (fresh randomized process set) and a VOICE TUTORIAL
    /// button (play/pause the narrated guided tour with captions).
    /// </summary>
    private void EnsureUpgradeButtons()
    {
        var consolePodium = GameObject.Find("Physical_Control_Console");
        if (consolePodium == null)
        {
            if (arenaConsole != null) consolePodium = arenaConsole.gameObject;
        }

        // Utility podium mirrored to the right of the control console.
        GameObject utilityPodium = GameObject.Find("Physical_Utility_Podium");
        if (utilityPodium == null)
        {
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null) litShader = Shader.Find("Standard");

            utilityPodium = GameObject.CreatePrimitive(PrimitiveType.Cube);
            utilityPodium.name = "Physical_Utility_Podium";
            Vector3 basePos = consolePodium != null
                ? consolePodium.transform.position + consolePodium.transform.right * (consolePodium.transform.lossyScale.x * 0.5f + 1.4f)
                : new Vector3(1.8f, 0.5f, -2.8f);
            utilityPodium.transform.position = basePos;
            utilityPodium.transform.rotation = consolePodium != null ? consolePodium.transform.rotation : Quaternion.identity;
            utilityPodium.transform.localScale = new Vector3(2.2f, 1.0f, 1.2f);
            var matPodium = new Material(litShader) { color = new Color(0.16f, 0.20f, 0.26f) };
            utilityPodium.GetComponent<Renderer>().material = matPodium;
        }

        // RANDOMIZE button
        SpawnUtilityButtonIfMissing(utilityPodium, "Btn_Randomize", "RANDOMIZE",
            new Color(0.20f, 0.65f, 0.85f), 0, () =>
            {
                var sim = SimulationController.Instance;
                if (sim != null) sim.RandomizeProcesses(5);
                AudioManager.Instance?.PlaySfx(AudioManager.SfxType.Arrival);
            });

        // Removed Voice Tutorial button per user request
        var oldVt = GameObject.Find("Btn_VoiceTutorial");
        if (oldVt != null) Destroy(oldVt);
    }

    private void SpawnUtilityButtonIfMissing(GameObject podium, string btnName, string label, Color color, int slotIndex, UnityEngine.Events.UnityAction onClick)
    {
        if (GameObject.Find(btnName) != null) return;

        Vector3 podiumTop = podium.transform.position
            + podium.transform.up * (podium.transform.lossyScale.y * 0.5f + 0.07f);

        float totalWidth = podium.transform.lossyScale.x * 0.4f;
        float offsetX = (slotIndex == 0 ? -totalWidth * 0.5f : totalWidth * 0.5f);
        Vector3 worldPos = podiumTop + podium.transform.right * offsetX;

        GameObject btn = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        btn.name = btnName;
        btn.transform.position = worldPos;
        btn.transform.rotation = podium.transform.rotation;
        btn.transform.localScale = new Vector3(0.28f, 0.05f, 0.28f);

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        Material mat = new Material(shader) { color = color };
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", color * 0.6f);
        btn.GetComponent<Renderer>().material = mat;

        GameObject lblGo = new GameObject("BtnLabel_TMP");
        lblGo.transform.SetParent(btn.transform, false);
        lblGo.transform.localPosition = new Vector3(0, 2.5f, 0);
        lblGo.transform.localRotation = Quaternion.Euler(90f, 0, 0);
        var lbl = lblGo.AddComponent<TextMeshPro>();
        lbl.text = label;
        lbl.fontSize = 0.3f;
        lbl.alignment = TextAlignmentOptions.Center;
        lbl.color = Color.white;
        lbl.fontStyle = FontStyles.Bold;
        lblGo.GetComponent<RectTransform>().sizeDelta = new Vector2(1.8f, 0.28f);

        var btnComp = btn.AddComponent<UtilityButton>();
        btnComp.onClick = onClick;
        btnComp.actionName = label;
    }

    /// <summary>
    /// Guarantees the AudioManager, VoiceTutorialController and LiveChallengeController
    /// exist at runtime so audio, narration and live challenges are always available.
    /// </summary>
    private void EnsureRuntimeControllers()
    {
        // Audio manager (persistent, procedural SFX)
        if (FindAnyObjectByType<AudioManager>() == null)
        {
            var audioObj = new GameObject("AudioManager");
            audioObj.AddComponent<AudioManager>();
        }

        // Voice tutorial permanently removed

        // Live challenge integration
        if (FindAnyObjectByType<LiveChallengeController>() == null)
        {
            var lcObj = new GameObject("LiveChallengeController");
            lcObj.AddComponent<LiveChallengeController>();
        }

        // [U] toggle for the legacy SchedulingUI canvas (optional overlay)
        if (FindAnyObjectByType<SchedulingUIToggle>() == null)
        {
            var toggleObj = new GameObject("SchedulingUIToggle");
            toggleObj.AddComponent<SchedulingUIToggle>();
        }

        // Windows text-to-speech voice speaker permanently removed per user request
    }

    /// <summary>
    /// Simple world-space button that invokes a callback on interact.
    /// </summary>
    public class UtilityButton : MonoBehaviour, IInteractable
    {
        public UnityEngine.Events.UnityAction onClick;
        public string actionName = "Button";
        public string PromptText => $"Press [E] to {actionName}";
        public void Interact()
        {
            onClick?.Invoke();
        }
    }

    private void CleanupUnwantedButtons()
    {
        // Unwanted button names to destroy
        string[] unwanted = new string[]
        {
            "Btn_QuantumDec", "Btn_QuantumInc", "Btn_AddProc", "Btn_ResetProcs", "Btn_Speed",
            "Btn_PrevProc", "Btn_DecArr", "Btn_IncArr", "Btn_DecBurst", "Btn_DecPri", "Btn_IncPri"
        };

        foreach (var name in unwanted)
        {
            var obj = GameObject.Find(name);
            if (obj != null) Destroy(obj);
        }

        // Reposition remaining 3 control buttons evenly on the console
        var runBtn = GameObject.Find("Btn_RunPause");
        if (runBtn != null)
        {
            runBtn.transform.localPosition = new Vector3(-0.25f, 0.52f, 0.15f);
            runBtn.transform.localScale = new Vector3(0.22f / 2.8f, 0.04f, 0.22f / 1.2f);
        }
        var stepBtn = GameObject.Find("Btn_Step");
        if (stepBtn != null)
        {
            stepBtn.transform.localPosition = new Vector3(0.0f, 0.52f, 0.15f);
            stepBtn.transform.localScale = new Vector3(0.22f / 2.8f, 0.04f, 0.22f / 1.2f);
        }
        var resetBtn = GameObject.Find("Btn_Reset");
        if (resetBtn != null)
        {
            resetBtn.transform.localPosition = new Vector3(0.25f, 0.52f, 0.15f);
            resetBtn.transform.localScale = new Vector3(0.22f / 2.8f, 0.04f, 0.22f / 1.2f);
        }

        // Reposition remaining 2 editor buttons evenly on editor podium
        var nextBtn = GameObject.Find("Btn_NextProc");
        if (nextBtn != null)
        {
            nextBtn.transform.localPosition = new Vector3(-0.15f, 0.52f, 0.15f);
            nextBtn.transform.localScale = new Vector3(0.22f / 2.6f, 0.04f, 0.22f / 1.2f);
        }
        var burstBtn = GameObject.Find("Btn_IncBurst");
        if (burstBtn != null)
        {
            burstBtn.transform.localPosition = new Vector3(0.15f, 0.52f, 0.15f);
            burstBtn.transform.localScale = new Vector3(0.22f / 2.6f, 0.04f, 0.22f / 1.2f);
        }
    }

    private void CheckAndBuildComponents()
    {
        if (cpuCore == null) cpuCore = Object.FindAnyObjectByType<PhysicalCPUCore>();
        if (readyQueue == null) readyQueue = Object.FindAnyObjectByType<PhysicalReadyQueue>();
        if (ganttBoard == null) ganttBoard = Object.FindAnyObjectByType<PhysicalGanttBoard>();
        if (arenaConsole == null) arenaConsole = Object.FindAnyObjectByType<PhysicalArenaConsole>();
        if (processEditor == null) processEditor = Object.FindAnyObjectByType<PhysicalProcessEditor>();

        // Setup workstations
        if (workstations.Count == 0)
        {
            SetupWorkstations();
        }

        if (cpuCore == null || readyQueue == null || arenaConsole == null)
        {
            BuildRuntimeArena();
        }

        // Setup Gantt on whiteboard if needed
        if (ganttBoard == null)
        {
            SetupRuntimeWhiteboard();
        }
    }

    private void SetupWorkstations()
    {
        var stations = new (string name, AlgorithmWorkstation.AlgorithmChoice algo, string title, string desc, Color activeCol)[]
        {
            ("FCFS", AlgorithmWorkstation.AlgorithmChoice.FCFS, "FCFS", "First-Come, First-Served\nNon-preemptive queue order.", new Color(0.2f, 0.65f, 1.0f)),
            ("SRTF", AlgorithmWorkstation.AlgorithmChoice.SRTF, "SRTF", "Shortest Remaining Time First\nPreemptive on arrival.", new Color(0.2f, 0.9f, 0.45f)),
            ("PRIORITY", AlgorithmWorkstation.AlgorithmChoice.Priority, "PRIORITY", "Priority Scheduling\nPreemptive (Lower # = Higher Pri).", new Color(1.0f, 0.65f, 0.15f)),
            ("ROUNDROBIN", AlgorithmWorkstation.AlgorithmChoice.RoundRobin, "ROUND ROBIN", "Round Robin (RR)\nTime Quantum slicing.", new Color(0.9f, 0.35f, 0.95f))
        };

        workstations.Clear();

        foreach (var data in stations)
        {
            GameObject wsObj = GameObject.Find($"/OS_Lab/Workstations/{data.name}");
            if (wsObj == null) wsObj = GameObject.Find(data.name);
            if (wsObj == null) continue;

            // Remove old SchedulingStation
            var oldSS = wsObj.GetComponent<SchedulingStation>();
            if (oldSS != null) Destroy(oldSS);

            var wsComp = wsObj.GetComponent<AlgorithmWorkstation>();
            if (wsComp == null) wsComp = wsObj.AddComponent<AlgorithmWorkstation>();

            MeshRenderer screenRenderer = null;
            Transform displayTrans = wsObj.transform.Find("Monitor/Screen/Display");
            if (displayTrans != null) screenRenderer = displayTrans.GetComponent<MeshRenderer>();
            if (screenRenderer == null)
            {
                Transform screenTrans = wsObj.transform.Find("Monitor/Screen");
                if (screenTrans != null) screenRenderer = screenTrans.GetComponent<MeshRenderer>();
            }

            // Clean old legacy labels under workstation
            var legacyTmps = wsObj.GetComponentsInChildren<TextMeshPro>(true);
            foreach (var lt in legacyTmps)
            {
                if (lt.name.Contains("LABLE") || lt.name.Contains("Text (TMP)") || lt.name == "Text")
                {
                    Destroy(lt.gameObject);
                }
            }

            // Check or create 3D TMP Label
            Transform existingLabel = wsObj.transform.Find("StationLabel_TMP");
            TextMeshPro titleTMP = null;
            TextMeshPro infoTMP = null;

            if (existingLabel == null)
            {
                GameObject labelObj = new GameObject("StationLabel_TMP");
                labelObj.transform.SetParent(wsObj.transform, false);
                labelObj.transform.localPosition = new Vector3(0, 1.35f, 0.05f);
                labelObj.AddComponent<Billboard3D>();

                titleTMP = labelObj.AddComponent<TextMeshPro>();
                titleTMP.text = data.title;
                titleTMP.fontSize = 1.4f;
                titleTMP.alignment = TextAlignmentOptions.Center;
                titleTMP.fontStyle = FontStyles.Bold;
                titleTMP.color = Color.white;
                var rtTitle = labelObj.GetComponent<RectTransform>();
                rtTitle.sizeDelta = new Vector2(2.5f, 0.5f);

                GameObject infoObj = new GameObject("StationInfo_TMP");
                infoObj.transform.SetParent(labelObj.transform, false);
                infoObj.transform.localPosition = new Vector3(0, -0.22f, 0);

                infoTMP = infoObj.AddComponent<TextMeshPro>();
                infoTMP.text = data.desc;
                infoTMP.fontSize = 0.75f;
                infoTMP.alignment = TextAlignmentOptions.Center;
                infoTMP.fontStyle = FontStyles.Normal;
                infoTMP.color = new Color(0.85f, 0.85f, 0.85f);
                var rtInfo = infoObj.GetComponent<RectTransform>();
                rtInfo.sizeDelta = new Vector2(2.5f, 0.5f);
            }
            else
            {
                existingLabel.localPosition = new Vector3(0, 1.35f, 0.05f);
                titleTMP = existingLabel.GetComponent<TextMeshPro>();
                if (titleTMP != null)
                {
                    titleTMP.fontSize = 1.4f;
                    var rt = titleTMP.GetComponent<RectTransform>();
                    if (rt != null) rt.sizeDelta = new Vector2(2.5f, 0.5f);
                }
                var infoChild = existingLabel.Find("StationInfo_TMP");
                if (infoChild != null)
                {
                    infoChild.localPosition = new Vector3(0, -0.22f, 0);
                    infoTMP = infoChild.GetComponent<TextMeshPro>();
                    if (infoTMP != null)
                    {
                        infoTMP.fontSize = 0.75f;
                        var rt = infoTMP.GetComponent<RectTransform>();
                        if (rt != null) rt.sizeDelta = new Vector2(2.5f, 0.5f);
                    }
                }
            }

            // Ensure collider for raycasts
            var deskTrans = wsObj.transform.Find("Desk");
            if (deskTrans != null && deskTrans.GetComponent<Collider>() == null)
            {
                deskTrans.gameObject.AddComponent<BoxCollider>();
            }

            // Light
            Light wsLight = wsObj.GetComponentInChildren<Light>();
            if (wsLight == null)
            {
                GameObject lObj = new GameObject("StationLight");
                lObj.transform.SetParent(wsObj.transform, false);
                lObj.transform.localPosition = new Vector3(0, 1.8f, 0);
                wsLight = lObj.AddComponent<Light>();
                wsLight.type = LightType.Point;
                wsLight.range = 3.5f;
                wsLight.intensity = 1.0f;
                wsLight.color = data.activeCol;
            }

            SetPrivateField(wsComp, "algorithm", data.algo);
            SetPrivateField(wsComp, "algorithmDisplayName", data.title);
            SetPrivateField(wsComp, "algorithmDescription", data.desc);
            SetPrivateField(wsComp, "monitorScreenRenderer", screenRenderer);
            SetPrivateField(wsComp, "labelTMP", titleTMP);
            SetPrivateField(wsComp, "infoTMP", infoTMP);
            SetPrivateField(wsComp, "workstationLight", wsLight);
            SetPrivateField(wsComp, "activeColor", data.activeCol);
            SetPrivateField(wsComp, "inactiveColor", new Color(0.15f, 0.2f, 0.3f));

            workstations.Add(wsComp);
        }
    }

    private void BuildRuntimeArena()
    {
        Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
        if (litShader == null) litShader = Shader.Find("Standard");

        Material matPlatform = new Material(litShader) { color = new Color(0.12f, 0.15f, 0.2f) };
        Material matConsole = new Material(litShader) { color = new Color(0.18f, 0.22f, 0.28f) };
        Material matCPUPlatform = new Material(litShader) { color = new Color(0.08f, 0.1f, 0.14f) };
        matCPUPlatform.EnableKeyword("_EMISSION");
        matCPUPlatform.SetColor("_EmissionColor", new Color(0.1f, 0.6f, 1.0f) * 0.4f);

        Transform root = transform;

        // CPU Platform
        GameObject cpuPlatform = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cpuPlatform.name = "Physical_CPU_Core";
        cpuPlatform.transform.SetParent(root, false);
        cpuPlatform.transform.position = new Vector3(0, 0.35f, 0f);
        cpuPlatform.transform.localScale = new Vector3(1.8f, 0.35f, 1.8f);
        cpuPlatform.GetComponent<Renderer>().material = matCPUPlatform;

        GameObject ringObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ringObj.name = "CoreRing";
        ringObj.transform.SetParent(cpuPlatform.transform, false);
        ringObj.transform.localPosition = new Vector3(0, 1.02f, 0);
        ringObj.transform.localScale = new Vector3(0.75f, 0.05f, 0.75f);
        Material ringMat = new Material(litShader) { color = new Color(0.2f, 0.8f, 1.0f) };
        ringMat.EnableKeyword("_EMISSION");
        ringMat.SetColor("_EmissionColor", new Color(0.2f, 0.8f, 1.0f) * 1.5f);
        ringObj.GetComponent<Renderer>().material = ringMat;

        GameObject socketObj = new GameObject("CoreSocket");
        socketObj.transform.SetParent(cpuPlatform.transform, false);
        socketObj.transform.localPosition = new Vector3(0, 1.7f, 0);

        GameObject coreLightObj = new GameObject("CoreLight");
        coreLightObj.transform.SetParent(cpuPlatform.transform, false);
        coreLightObj.transform.localPosition = new Vector3(0, 2.5f, 0);
        Light coreLight = coreLightObj.AddComponent<Light>();
        coreLight.type = LightType.Point;
        coreLight.range = 5f;
        coreLight.intensity = 1.8f;
        coreLight.color = new Color(0.2f, 0.85f, 1.0f);

        GameObject statusObj = new GameObject("CPU_Status_TMP");
        statusObj.transform.SetParent(root, false);
        statusObj.transform.position = new Vector3(0, 0.385f, -1.15f);
        statusObj.transform.rotation = Quaternion.Euler(90f, 0, 0);
        statusObj.transform.localScale = Vector3.one;
        var statusTMP = statusObj.AddComponent<TextMeshPro>();
        statusTMP.text = "CPU: IDLE (WAITING)";
        statusTMP.fontSize = 0.5f;
        statusTMP.alignment = TextAlignmentOptions.Center;
        statusTMP.fontStyle = FontStyles.Bold;
        statusTMP.color = new Color(0.3f, 0.8f, 1.0f);
        var rtStatus = statusObj.GetComponent<RectTransform>();
        rtStatus.sizeDelta = new Vector2(3.0f, 0.4f);

        cpuCore = cpuPlatform.AddComponent<PhysicalCPUCore>();
        SetPrivateField(cpuCore, "socketTransform", socketObj.transform);
        SetPrivateField(cpuCore, "statusTMP", statusTMP);
        SetPrivateField(cpuCore, "coreLight", coreLight);
        SetPrivateField(cpuCore, "socketRingRenderer", ringObj.GetComponent<MeshRenderer>());
        SetPrivateField(cpuCore, "coreRotor", ringObj.transform);

        // Ready Queue Bay
        GameObject queueBay = new GameObject("Physical_Ready_Queue");
        queueBay.transform.SetParent(root, false);
        queueBay.transform.position = Vector3.zero;

        GameObject qDesk = GameObject.CreatePrimitive(PrimitiveType.Cube);
        qDesk.name = "ReadyQueuePlatform";
        qDesk.transform.SetParent(queueBay.transform, false);
        qDesk.transform.position = new Vector3(0, 0.25f, 2.2f);
        qDesk.transform.localScale = new Vector3(4.5f, 0.25f, 1.2f);
        qDesk.GetComponent<Renderer>().material = matPlatform;

        GameObject qOrigin = new GameObject("QueueOrigin");
        qOrigin.transform.SetParent(queueBay.transform, false);
        qOrigin.transform.position = new Vector3(-1.6f, 0.65f, 2.2f);

        GameObject qHeaderObj = new GameObject("ReadyQueueHeader_TMP");
        qHeaderObj.transform.SetParent(queueBay.transform, false);
        qHeaderObj.transform.position = new Vector3(0, 0.75f, 1.4f);
        qHeaderObj.transform.rotation = Quaternion.Euler(45f, 0, 0);
        qHeaderObj.transform.localScale = Vector3.one;
        var qHeaderTMP = qHeaderObj.AddComponent<TextMeshPro>();
        qHeaderTMP.text = "READY QUEUE";
        qHeaderTMP.fontSize = 1.8f;
        qHeaderTMP.alignment = TextAlignmentOptions.Center;
        qHeaderTMP.fontStyle = FontStyles.Bold;
        qHeaderTMP.color = new Color(0.4f, 0.8f, 1.0f);
        var rtQ = qHeaderObj.GetComponent<RectTransform>();
        rtQ.sizeDelta = new Vector2(6.0f, 1.2f);

        // Incoming Shelf
        GameObject inDesk = GameObject.CreatePrimitive(PrimitiveType.Cube);
        inDesk.name = "IncomingShelf";
        inDesk.transform.SetParent(queueBay.transform, false);
        inDesk.transform.position = new Vector3(-4.5f, 0.25f, 0f);
        inDesk.transform.localScale = new Vector3(2.5f, 0.25f, 2.0f);
        inDesk.GetComponent<Renderer>().material = matPlatform;

        GameObject inOrigin = new GameObject("IncomingOrigin");
        inOrigin.transform.SetParent(queueBay.transform, false);
        inOrigin.transform.position = new Vector3(-5.2f, 0.65f, 0f);

        GameObject inHeaderObj = new GameObject("IncomingHeader_TMP");
        inHeaderObj.transform.SetParent(queueBay.transform, false);
        inHeaderObj.transform.position = new Vector3(-4.5f, 0.75f, -0.6f);
        inHeaderObj.transform.rotation = Quaternion.Euler(45f, 0, 0);
        inHeaderObj.transform.localScale = Vector3.one;
        var inHeaderTMP = inHeaderObj.AddComponent<TextMeshPro>();
        inHeaderTMP.text = "INCOMING (NEW)";
        inHeaderTMP.fontSize = 1.8f;
        inHeaderTMP.alignment = TextAlignmentOptions.Center;
        inHeaderTMP.fontStyle = FontStyles.Bold;
        inHeaderTMP.color = new Color(0.85f, 0.85f, 0.95f);
        var rtIn = inHeaderObj.GetComponent<RectTransform>();
        rtIn.sizeDelta = new Vector2(6.0f, 1.2f);

        // Completed Platform
        GameObject compDesk = GameObject.CreatePrimitive(PrimitiveType.Cube);
        compDesk.name = "CompletedPlatform";
        compDesk.transform.SetParent(queueBay.transform, false);
        compDesk.transform.position = new Vector3(4.5f, 0.25f, 0f);
        compDesk.transform.localScale = new Vector3(2.5f, 0.25f, 2.0f);
        compDesk.GetComponent<Renderer>().material = matPlatform;

        GameObject compOrigin = new GameObject("CompletedOrigin");
        compOrigin.transform.SetParent(queueBay.transform, false);
        compOrigin.transform.position = new Vector3(3.8f, 0.65f, 0f);

        GameObject compHeaderObj = new GameObject("CompletedHeader_TMP");
        compHeaderObj.transform.SetParent(queueBay.transform, false);
        compHeaderObj.transform.position = new Vector3(4.5f, 0.75f, -0.6f);
        compHeaderObj.transform.rotation = Quaternion.Euler(45f, 0, 0);
        compHeaderObj.transform.localScale = Vector3.one;
        var compHeaderTMP = compHeaderObj.AddComponent<TextMeshPro>();
        compHeaderTMP.text = "COMPLETED";
        compHeaderTMP.fontSize = 1.8f;
        compHeaderTMP.alignment = TextAlignmentOptions.Center;
        compHeaderTMP.fontStyle = FontStyles.Bold;
        compHeaderTMP.color = new Color(0.3f, 1.0f, 0.5f);
        var rtComp = compHeaderObj.GetComponent<RectTransform>();
        rtComp.sizeDelta = new Vector2(6.0f, 1.2f);

        readyQueue = queueBay.AddComponent<PhysicalReadyQueue>();
        SetPrivateField(readyQueue, "queueOrigin", qOrigin.transform);
        SetPrivateField(readyQueue, "incomingOrigin", inOrigin.transform);
        SetPrivateField(readyQueue, "completedOrigin", compOrigin.transform);
        SetPrivateField(readyQueue, "queueHeaderTMP", qHeaderTMP);
        SetPrivateField(readyQueue, "incomingHeaderTMP", inHeaderTMP);
        SetPrivateField(readyQueue, "completedHeaderTMP", compHeaderTMP);

        // Console Podium
        GameObject consolePodium = GameObject.CreatePrimitive(PrimitiveType.Cube);
        consolePodium.name = "Physical_Control_Console";
        consolePodium.transform.SetParent(root, false);
        consolePodium.transform.position = new Vector3(-1.8f, 0.5f, -2.8f);
        consolePodium.transform.localScale = new Vector3(2.8f, 1.0f, 1.2f);
        consolePodium.GetComponent<Renderer>().material = matConsole;

        GameObject consoleScreen = GameObject.CreatePrimitive(PrimitiveType.Cube);
        consoleScreen.name = "ConsoleScreen";
        consoleScreen.transform.SetParent(consolePodium.transform, false);
        consoleScreen.transform.localPosition = new Vector3(0, 1.05f, -0.2f);
        consoleScreen.transform.localRotation = Quaternion.Euler(25f, 0, 0);
        consoleScreen.transform.localScale = new Vector3(1.3f / 2.8f, 0.55f / 1.0f, 0.06f / 1.2f);
        Material screenMat = new Material(litShader) { color = new Color(0.05f, 0.08f, 0.12f) };
        screenMat.EnableKeyword("_EMISSION");
        screenMat.SetColor("_EmissionColor", new Color(0.05f, 0.15f, 0.3f));
        consoleScreen.GetComponent<Renderer>().material = screenMat;

        GameObject consoleTMPObj = new GameObject("ConsoleReadout_TMP");
        consoleTMPObj.transform.SetParent(consolePodium.transform, false);
        consoleTMPObj.transform.localPosition = new Vector3(0, 1.08f, -0.22f);
        consoleTMPObj.transform.localRotation = Quaternion.Euler(25f, 0, 0);
        consoleTMPObj.transform.localScale = Vector3.one;
        var consoleTMP = consoleTMPObj.AddComponent<TextMeshPro>();
        consoleTMP.fontSize = 0.85f;
        consoleTMP.alignment = TextAlignmentOptions.Center;
        consoleTMP.color = Color.white;
        consoleTMP.textWrappingMode = TextWrappingModes.NoWrap;

        var rtCon = consoleTMPObj.GetComponent<RectTransform>();
        rtCon.sizeDelta = new Vector2(3.5f, 0.8f);

        arenaConsole = consolePodium.AddComponent<PhysicalArenaConsole>();
        SetPrivateField(arenaConsole, "readoutTMP", consoleTMP);

        // Minimized 3 Circular Push Buttons on Console Desk
        Transform cParent = consolePodium.transform;
        CreateConsoleButton(cParent, "Btn_RunPause", "Run / Pause", new Vector3(-0.25f, 0.52f, 0.15f), new Color(0.15f, 0.85f, 0.35f), PhysicalArenaConsole.ConsoleAction.RunPause);
        CreateConsoleButton(cParent, "Btn_Step", "Step (+1s)", new Vector3(0.0f, 0.52f, 0.15f), new Color(0.95f, 0.8f, 0.15f), PhysicalArenaConsole.ConsoleAction.Step);
        CreateConsoleButton(cParent, "Btn_Reset", "Reset", new Vector3(0.25f, 0.52f, 0.15f), new Color(0.9f, 0.25f, 0.25f), PhysicalArenaConsole.ConsoleAction.Reset);

        // Process Editor Podium
        GameObject editorPodium = GameObject.CreatePrimitive(PrimitiveType.Cube);
        editorPodium.name = "Physical_Process_Editor";
        editorPodium.transform.SetParent(root, false);
        editorPodium.transform.position = new Vector3(2.0f, 0.5f, -2.8f);
        editorPodium.transform.localScale = new Vector3(2.6f, 1.0f, 1.2f);
        editorPodium.GetComponent<Renderer>().material = matConsole;

        GameObject editScreen = GameObject.CreatePrimitive(PrimitiveType.Cube);
        editScreen.name = "EditorScreen";
        editScreen.transform.SetParent(editorPodium.transform, false);
        editScreen.transform.localPosition = new Vector3(0, 1.05f, -0.2f);
        editScreen.transform.localRotation = Quaternion.Euler(25f, 0, 0);
        editScreen.transform.localScale = new Vector3(1.3f / 2.6f, 0.55f / 1.0f, 0.06f / 1.2f);
        editScreen.GetComponent<Renderer>().material = screenMat;

        GameObject editTMPObj = new GameObject("EditorDisplay_TMP");
        editTMPObj.transform.SetParent(editScreen.transform, false);
        editTMPObj.transform.localPosition = new Vector3(0, 0, -0.6f);
        editTMPObj.transform.localScale = Vector3.one;
        var editTMP = editTMPObj.AddComponent<TextMeshPro>();
        editTMP.fontSize = 0.4f;
        editTMP.alignment = TextAlignmentOptions.Center;
        editTMP.color = Color.white;
        var rtEdit = editTMPObj.GetComponent<RectTransform>();
        rtEdit.sizeDelta = new Vector2(1.25f, 0.52f);

        processEditor = editorPodium.AddComponent<PhysicalProcessEditor>();
        SetPrivateField(processEditor, "editorDisplayTMP", editTMP);

        // Minimized 2 Circular Process Edit Buttons
        Transform eParent = editorPodium.transform;
        CreateEditButton(eParent, "Btn_NextProc", "Select Process [P1..Pn]",
            new Vector3(-0.15f, 0.52f, 0.15f), new Color(0.3f, 0.65f, 1.0f),
            PhysicalProcessEditor.EditAction.NextProcess);
        CreateEditButton(eParent, "Btn_IncBurst", "Burst Time (+1s)",
            new Vector3(0.15f, 0.52f, 0.15f), new Color(0.25f, 0.85f, 0.5f),
            PhysicalProcessEditor.EditAction.IncBurst);
    } // End of BuildRuntimeArena


    private void SetupRuntimeWhiteboard()
    {
        GameObject whiteboard = GameObject.Find("/OS_Lab/Teaching_Display/Teaching_Whiteboard");
        if (whiteboard == null) whiteboard = GameObject.Find("Teaching_Whiteboard");
        if (whiteboard == null) return;

        // REMOVE THE WHITE SCREEN: remove MeshRenderer, MeshFilter, and Collider so it is pure wall container
        var mr = whiteboard.GetComponent<MeshRenderer>();
        if (mr != null) Destroy(mr);
        var mf = whiteboard.GetComponent<MeshFilter>();
        if (mf != null) Destroy(mf);
        var col = whiteboard.GetComponent<Collider>();
        if (col != null) Destroy(col);

        whiteboard.transform.position = new Vector3(0f, 2.3f, 9.72f);
        whiteboard.transform.rotation = Quaternion.identity;
        whiteboard.transform.localScale = Vector3.one;

        var segTrans = whiteboard.transform.Find("GanttSegmentsContainer");
        GameObject segContainer = null;
        if (segTrans == null)
        {
            segContainer = new GameObject("GanttSegmentsContainer");
            segContainer.transform.SetParent(whiteboard.transform, false);
            segContainer.transform.localPosition = new Vector3(-2.6f, 0.1f, -0.02f);
            segContainer.transform.localScale = Vector3.one;
        }
        else
        {
            segContainer = segTrans.gameObject;
            segContainer.transform.localPosition = new Vector3(-2.6f, 0.1f, -0.02f);
            segContainer.transform.localScale = Vector3.one;
        }

        Transform titleTrans = whiteboard.transform.Find("GanttTitle_TMP");
        TextMeshPro titleTMP = null;
        if (titleTrans == null)
        {
            GameObject titleObj = new GameObject("GanttTitle_TMP");
            titleObj.transform.SetParent(whiteboard.transform, false);
            titleObj.transform.localPosition = new Vector3(0, 0.75f, -0.02f);
            titleObj.transform.localScale = Vector3.one;
            titleTMP = titleObj.AddComponent<TextMeshPro>();
            titleTMP.text = "CPU EXECUTION TIMELINE (GANTT CHART)";
            titleTMP.fontSize = 0.72f;
            titleTMP.alignment = TextAlignmentOptions.Center;
            titleTMP.fontStyle = FontStyles.Bold;
            titleTMP.color = Color.white;
            var rtTitle = titleObj.GetComponent<RectTransform>();
            rtTitle.sizeDelta = new Vector2(10f, 0.9f);
        }
        else
        {
            titleTMP = titleTrans.GetComponent<TextMeshPro>();
            if (titleTMP != null)
            {
                titleTMP.fontSize = 0.72f;
                titleTMP.color = Color.white;
                var rt = titleTMP.GetComponent<RectTransform>();
                if (rt != null) rt.sizeDelta = new Vector2(10f, 0.9f);
            }
        }

        Transform metricsTrans = whiteboard.transform.Find("GanttMetrics_TMP");
        TextMeshPro metricsTMP = null;
        if (metricsTrans == null)
        {
            GameObject metricsObj = new GameObject("GanttMetrics_TMP");
            metricsObj.transform.SetParent(whiteboard.transform, false);
            metricsObj.transform.localPosition = new Vector3(0, -0.55f, -0.02f);
            metricsObj.transform.localScale = Vector3.one;
            metricsTMP = metricsObj.AddComponent<TextMeshPro>();
            metricsTMP.text = "Walk to any algorithm station (FCFS, SRTF, PRIORITY, ROUND ROBIN) to begin.";
            metricsTMP.fontSize = 0.48f;
            metricsTMP.alignment = TextAlignmentOptions.Center;
            metricsTMP.fontStyle = FontStyles.Normal;
            metricsTMP.color = new Color(0.6f, 0.85f, 1.0f);
            var rtMet = metricsObj.GetComponent<RectTransform>();
            rtMet.sizeDelta = new Vector2(10f, 0.6f);
        }
        else
        {
            metricsTMP = metricsTrans.GetComponent<TextMeshPro>();
            if (metricsTMP != null)
            {
                metricsTMP.fontSize = 0.48f;
                metricsTMP.color = new Color(0.6f, 0.85f, 1.0f);
                var rt = metricsTMP.GetComponent<RectTransform>();
                if (rt != null) rt.sizeDelta = new Vector2(10f, 0.6f);
            }
        }

        ganttBoard = whiteboard.GetComponent<PhysicalGanttBoard>();
        if (ganttBoard == null) ganttBoard = whiteboard.AddComponent<PhysicalGanttBoard>();

        SetPrivateField(ganttBoard, "segmentsContainer", segContainer.transform);
        SetPrivateField(ganttBoard, "boardTitleTMP", titleTMP);
        SetPrivateField(ganttBoard, "boardMetricsTMP", metricsTMP);
    }



    private void CreateConsoleButton(Transform parent, string name, string label, Vector3 localPos, Color color, PhysicalArenaConsole.ConsoleAction action)
    {
        GameObject btnObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        btnObj.name = name;
        btnObj.transform.SetParent(parent, false);
        btnObj.transform.localPosition = localPos;
        Vector3 pLossy = parent.lossyScale;
        btnObj.transform.localScale = new Vector3(
            0.22f / (pLossy.x > 0 ? pLossy.x : 1f),
            0.04f / (pLossy.y > 0 ? pLossy.y : 1f),
            0.22f / (pLossy.z > 0 ? pLossy.z : 1f));

        Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
        if (litShader == null) litShader = Shader.Find("Standard");

        Material mat = new Material(litShader) { color = color };
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", color * 0.4f);
        btnObj.GetComponent<Renderer>().material = mat;

        // Add a label above the button
        GameObject lblObj = new GameObject("BtnLabel_TMP");
        lblObj.transform.SetParent(btnObj.transform, false);
        lblObj.transform.localPosition = new Vector3(0, 3f, 0);
        lblObj.transform.localRotation = Quaternion.Euler(90f, 0, 0);
        var lbl = lblObj.AddComponent<TextMeshPro>();
        lbl.text = label;
        lbl.fontSize = 0.35f;
        lbl.alignment = TextAlignmentOptions.Center;
        lbl.color = Color.white;
        var rtLbl = lblObj.GetComponent<RectTransform>();
        rtLbl.sizeDelta = new Vector2(1.5f, 0.3f);

        var btnComp = btnObj.AddComponent<PhysicalArenaConsole.ConsoleButton>();
        btnComp.action = action;
        btnComp.actionName = label;
        btnComp.buttonColor = color;
    }

    private void CreateEditButton(Transform parent, string name, string label, Vector3 localPos, Color color, PhysicalProcessEditor.EditAction action)
    {
        GameObject btnObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        btnObj.name = name;
        btnObj.transform.SetParent(parent, false);
        btnObj.transform.localPosition = localPos;
        Vector3 pLossy = parent.lossyScale;
        btnObj.transform.localScale = new Vector3(0.22f / (pLossy.x > 0 ? pLossy.x : 1f), 0.04f / (pLossy.y > 0 ? pLossy.y : 1f), 0.22f / (pLossy.z > 0 ? pLossy.z : 1f));

        Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
        if (litShader == null) litShader = Shader.Find("Standard");

        Material mat = new Material(litShader) { color = color };
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", color * 0.4f);
        btnObj.GetComponent<Renderer>().material = mat;

        var btnComp = btnObj.AddComponent<PhysicalProcessEditor.ProcessEditButton>();
        btnComp.action = action;
        btnComp.actionName = label;
        btnComp.buttonColor = color;
    }

    public void SelectAlgorithmFromWorkstation(AlgorithmWorkstation station)
    {
        if (station == null) return;

        foreach (var ws in workstations)
        {
            if (ws != null) ws.SetActiveState(ws == station);
        }

        activeWorkstation = station;

        int quantum = arenaConsole != null ? arenaConsole.RoundRobinQuantum : 4;
        SchedulingAlgorithm algo = station.CreateAlgorithmInstance(quantum);

        var sim = SimulationController.Instance;
        if (sim != null)
        {
            sim.SetAlgorithm(algo);
        }

        if (ganttBoard != null)
        {
            ganttBoard.ClearBoard();
        }

        UpdatePhysicalArena();
    }

    private void HandleSimulationTicked()
    {
        UpdatePhysicalArena();
    }

    private void HandleSimulationReset()
    {
        UpdatePhysicalArena();
        if (ganttBoard != null) ganttBoard.ClearBoard();
    }

    private void HandleAlgorithmChanged(SchedulingAlgorithm algo)
    {
        UpdatePhysicalArena();
        if (ganttBoard != null) ganttBoard.ClearBoard();
    }

    private void HandlePlayStateChanged(bool isRunning)
    {
        UpdatePhysicalArena();
    }

    private void HandleSimulationCompleted()
    {
        UpdatePhysicalArena();
    }

    private void UpdatePhysicalArena()
    {
        var sim = SimulationController.Instance;
        if (sim == null || sim.CurrentAlgorithm == null) return;

        var algo = sim.CurrentAlgorithm;

        if (cpuCore != null)
        {
            cpuCore.SetCpuState(algo.CurrentCpuState, algo.RunningProcess);
        }

        if (readyQueue != null)
        {
            readyQueue.SyncWithAlgorithm(algo, cpuCore);
        }

        if (ganttBoard != null)
        {
            ganttBoard.RenderGanttChart(algo);
        }

        if (arenaConsole != null)
        {
            arenaConsole.UpdateReadout();
        }

        if (processEditor != null)
        {
            processEditor.UpdateEditorDisplay();
        }
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (field != null) field.SetValue(target, value);
    }
}
