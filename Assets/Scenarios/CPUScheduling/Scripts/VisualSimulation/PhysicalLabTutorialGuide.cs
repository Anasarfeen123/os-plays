using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// First-person interactive tutorial and step-by-step instruction guide.
/// Renders a modern glassmorphic HUD panel guiding the player through the CPU scheduling lab.
/// Tracks simulation events in real time and automatically advances steps.
/// Can be toggled or minimized with [H].
/// </summary>
public class PhysicalLabTutorialGuide : MonoBehaviour
{
    public static PhysicalLabTutorialGuide Instance { get; private set; }

    [System.Serializable]
    public class TutorialStep
    {
        public string title;
        public string shortName;
        public string instruction;
        public string tip;
        public bool isCompleted;

        public TutorialStep(string title, string shortName, string instruction, string tip)
        {
            this.title = title;
            this.shortName = shortName;
            this.instruction = instruction;
            this.tip = tip;
            this.isCompleted = false;
        }
    }

    private List<TutorialStep> steps = new List<TutorialStep>();
    private int currentStepIndex = 0;
    private int _tickCount = 0; // tracks simulation ticks since CurrentTime is not exposed
    private bool isExpanded = true;

    // UI References
    private GameObject hudCanvasObj;
    private RectTransform panelRect;
    private GameObject expandedPanel;
    private GameObject minimizedBadge;

    private TextMeshProUGUI stepBadgeTMP;
    private TextMeshProUGUI stepTitleTMP;
    private TextMeshProUGUI instructionTMP;
    private TextMeshProUGUI tipTMP;
    private TextMeshProUGUI checklistTMP;
    private TextMeshProUGUI minimizedTextTMP;

    private Button prevBtn;
    private Button nextBtn;
    private Button toggleBtn;

    private void Awake()
    {
        // Tutorial HUD permanently removed per user instruction
        gameObject.SetActive(false);
        Destroy(gameObject);
        return;
    }

    private void Start()
    {
    }

    private void Update()
    {
        bool togglePressed = false;
        bool prevPressed  = false;
        bool nextPressed  = false;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.hKey.wasPressedThisFrame) togglePressed = true;
            if (Keyboard.current.nKey.wasPressedThisFrame) nextPressed  = true;
            if (Keyboard.current.pKey.wasPressedThisFrame) prevPressed  = true;
        }
#else
        if (Input.GetKeyDown(KeyCode.H)) togglePressed = true;
        if (Input.GetKeyDown(KeyCode.N)) nextPressed  = true;
        if (Input.GetKeyDown(KeyCode.P)) prevPressed  = true;
#endif
        if (togglePressed) ToggleExpanded();
        if (prevPressed)   PrevStep();
        if (nextPressed)   NextStep();
    }

    private void InitializeSteps()
    {
        steps.Clear();

        steps.Add(new TutorialStep(
            "1. Choose an Algorithm Station",
            "Select Algorithm",
            "Walk up to any of the 4 workstation desks in the lab (FCFS, SRTF, PRIORITY, or ROUND ROBIN) and look at the desk to press [E] to activate that algorithm.",
            "Tip: Each station configures the arena with unique preemption and queue rules."
        ));

        steps.Add(new TutorialStep(
            "2. Inspect the Ready Queue",
            "Inspect Processes",
            "Walk over to the arena floor. Observe the INCOMING shelf on your left and the READY QUEUE in the center with processes waiting to run.",
            "Tip: Notice each process block shows its PID (P1, P2...), burst time, and priority."
        ));

        steps.Add(new TutorialStep(
            "3. Run the Simulation",
            "Start CPU Simulation",
            "Approach the Control Console desk. Press [E] on the green RUN / PAUSE button to run automatically, or yellow STEP button to advance 1 second.",
            "Tip: You only need 3 buttons: Green (Run), Yellow (Step), Red (Reset)."
        ));

        steps.Add(new TutorialStep(
            "4. Watch CPU & Gantt Chart",
            "Observe Execution",
            "Watch the central CPU Core dock and execute process blocks. Look up at the North wall to see the real-time Gantt Chart timeline build tick-by-tick!",
            "Tip: Preemptive algorithms will swap blocks off the CPU Core when higher-priority processes arrive."
        ));

        steps.Add(new TutorialStep(
            "5. Compare Performance & Reset",
            "Analyze & Compare",
            "When finished, check the Average Waiting Time and Turnaround Time displayed on the wall. Press the red RESET button, then select another workstation to compare!",
            "Tip: Shortest Remaining Time First (SRTF) often achieves the lowest average waiting time."
        ));
    }

    private void HookSimulationEvents()
    {
        var sim = SimulationController.Instance;
        if (sim != null)
        {
            sim.OnAlgorithmChanged += (algo) =>
            {
                CompleteStep(0);
                if (currentStepIndex == 0) SetStep(1);
            };

            sim.OnSimulationTicked += () =>
            {
                _tickCount++;
                if (_tickCount > 0)
                {
                    CompleteStep(2);
                    CompleteStep(3);
                    if (currentStepIndex < 3) SetStep(3);
                }
            };

            sim.OnPlayStateChanged += (running) =>
            {
                if (running)
                {
                    CompleteStep(2);
                    if (currentStepIndex < 2) SetStep(2);
                }
            };

            sim.OnSimulationCompleted += () =>
            {
                CompleteStep(3);
                CompleteStep(4);
                SetStep(4);
            };

            sim.OnSimulationReset += () =>
            {
                UpdateUI();
            };
        }
    }

    public void CompleteStep(int stepIndex)
    {
        if (stepIndex >= 0 && stepIndex < steps.Count)
        {
            steps[stepIndex].isCompleted = true;
            UpdateUI();
        }
    }

    public void SetStep(int index)
    {
        currentStepIndex = Mathf.Clamp(index, 0, steps.Count - 1);
        UpdateUI();
    }

    public void NextStep()
    {
        if (currentStepIndex < steps.Count - 1)
        {
            currentStepIndex++;
            UpdateUI();
        }
    }

    public void PrevStep()
    {
        if (currentStepIndex > 0)
        {
            currentStepIndex--;
            UpdateUI();
        }
    }

    public void ToggleExpanded()
    {
        isExpanded = !isExpanded;
        if (expandedPanel != null) expandedPanel.SetActive(isExpanded);
        if (minimizedBadge != null) minimizedBadge.SetActive(!isExpanded);
    }

    private void UpdateUI()
    {
        if (currentStepIndex < 0 || currentStepIndex >= steps.Count) return;
        var step = steps[currentStepIndex];

        if (stepBadgeTMP != null)
            stepBadgeTMP.text = $"STEP {currentStepIndex + 1} OF {steps.Count}";

        if (stepTitleTMP != null)
            stepTitleTMP.text = step.title;

        if (instructionTMP != null)
            instructionTMP.text = step.instruction;

        if (tipTMP != null)
            tipTMP.text = step.tip;

        if (minimizedTextTMP != null)
            minimizedTextTMP.text = $"Guide [{currentStepIndex + 1}/{steps.Count}]: {step.shortName} (Press [H])";

        if (checklistTMP != null)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < steps.Count; i++)
            {
                string check = steps[i].isCompleted ? "<color=#22C55E>✓</color>" : "<color=#64748B>○</color>";
                string highlight = (i == currentStepIndex) ? "<b><color=#38BDF8>" : "<color=#94A3B8>";
                string endHighlight = (i == currentStepIndex) ? "</color></b>" : "</color>";
                sb.AppendLine($"{check} {highlight}{steps[i].shortName}{endHighlight}");
            }
            checklistTMP.text = sb.ToString().TrimEnd();
        }

        if (prevBtn != null) prevBtn.interactable = currentStepIndex > 0;
        if (nextBtn != null) nextBtn.interactable = currentStepIndex < steps.Count - 1;
    }

    private void BuildGuideHUD()
    {
        hudCanvasObj = GameObject.Find("PlayerHUD_Canvas");
        if (hudCanvasObj == null)
        {
            hudCanvasObj = new GameObject("PlayerHUD_Canvas");
            var canvas = hudCanvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 95;
            hudCanvasObj.AddComponent<CanvasScaler>();
            hudCanvasObj.AddComponent<GraphicRaycaster>();
        }

        // ── Compact Tutorial Card (top-left, 260×280) ────────────────────
        expandedPanel = new GameObject("TutorialGuidePanel");
        expandedPanel.transform.SetParent(hudCanvasObj.transform, false);
        panelRect = expandedPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot     = new Vector2(0f, 1f);
        panelRect.anchoredPosition = new Vector2(16f, -16f);
        panelRect.sizeDelta        = new Vector2(260f, 280f);

        var bg = expandedPanel.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.08f, 0.14f, 0.92f);

        // ── Header row: badge left, Hide button right ──────────────────
        GameObject headerObj = new GameObject("HeaderBar");
        headerObj.transform.SetParent(expandedPanel.transform, false);
        var rtHdr = headerObj.AddComponent<RectTransform>();
        rtHdr.anchorMin = new Vector2(0f, 1f);
        rtHdr.anchorMax = new Vector2(1f, 1f);
        rtHdr.pivot     = new Vector2(0.5f, 1f);
        rtHdr.anchoredPosition = new Vector2(0f, -6f);
        rtHdr.sizeDelta        = new Vector2(-16f, 26f);

        // Step badge
        GameObject badgeObj = new GameObject("BadgeText");
        badgeObj.transform.SetParent(headerObj.transform, false);
        var rtBadge = badgeObj.AddComponent<RectTransform>();
        rtBadge.anchorMin = Vector2.zero; rtBadge.anchorMax = new Vector2(0.62f, 1f);
        rtBadge.offsetMin = rtBadge.offsetMax = Vector2.zero;
        stepBadgeTMP = badgeObj.AddComponent<TextMeshProUGUI>();
        stepBadgeTMP.fontSize  = 10f;
        stepBadgeTMP.fontStyle = FontStyles.Bold;
        stepBadgeTMP.color     = new Color(0.22f, 0.74f, 0.97f);
        stepBadgeTMP.alignment = TextAlignmentOptions.MidlineLeft;

        // Hide [H] button
        GameObject minBtnObj = new GameObject("HideBtn");
        minBtnObj.transform.SetParent(headerObj.transform, false);
        var rtMin = minBtnObj.AddComponent<RectTransform>();
        rtMin.anchorMin = new Vector2(0.62f, 0f); rtMin.anchorMax = Vector2.one;
        rtMin.offsetMin = rtMin.offsetMax = Vector2.zero;
        toggleBtn = minBtnObj.AddComponent<Button>();
        var minImg = minBtnObj.AddComponent<Image>();
        minImg.color = new Color(0.14f, 0.20f, 0.32f, 0.7f);
        var minTxtObj = new GameObject("Text");
        minTxtObj.transform.SetParent(minBtnObj.transform, false);
        var rtMT = minTxtObj.AddComponent<RectTransform>();
        rtMT.anchorMin = Vector2.zero; rtMT.anchorMax = Vector2.one;
        rtMT.offsetMin = rtMT.offsetMax = Vector2.zero;
        var minTMP = minTxtObj.AddComponent<TextMeshProUGUI>();
        minTMP.text = "Hide [H]"; minTMP.fontSize = 9.5f;
        minTMP.alignment = TextAlignmentOptions.Center;
        minTMP.color = new Color(0.75f, 0.82f, 0.95f);
        toggleBtn.onClick.AddListener(ToggleExpanded);

        // ── Divider ────────────────────────────────────────────────────
        MakeDivider(expandedPanel.transform, -36f);

        // ── Step Title ────────────────────────────────────────────────
        GameObject titleObj = new GameObject("StepTitle");
        titleObj.transform.SetParent(expandedPanel.transform, false);
        var rtTitle = titleObj.AddComponent<RectTransform>();
        rtTitle.anchorMin = new Vector2(0f, 1f); rtTitle.anchorMax = new Vector2(1f, 1f);
        rtTitle.pivot = new Vector2(0.5f, 1f);
        rtTitle.anchoredPosition = new Vector2(0f, -42f);
        rtTitle.sizeDelta = new Vector2(-16f, 24f);
        stepTitleTMP = titleObj.AddComponent<TextMeshProUGUI>();
        stepTitleTMP.fontSize  = 13f;
        stepTitleTMP.fontStyle = FontStyles.Bold;
        stepTitleTMP.color     = Color.white;
        stepTitleTMP.alignment = TextAlignmentOptions.TopLeft;
        stepTitleTMP.enableWordWrapping = true;

        // ── Instruction body ──────────────────────────────────────────
        GameObject descObj = new GameObject("InstructionText");
        descObj.transform.SetParent(expandedPanel.transform, false);
        var rtDesc = descObj.AddComponent<RectTransform>();
        rtDesc.anchorMin = new Vector2(0f, 1f); rtDesc.anchorMax = new Vector2(1f, 1f);
        rtDesc.pivot = new Vector2(0.5f, 1f);
        rtDesc.anchoredPosition = new Vector2(0f, -70f);
        rtDesc.sizeDelta = new Vector2(-16f, 100f);
        instructionTMP = descObj.AddComponent<TextMeshProUGUI>();
        instructionTMP.fontSize  = 10.5f;
        instructionTMP.color     = new Color(0.88f, 0.92f, 0.98f);
        instructionTMP.alignment = TextAlignmentOptions.TopLeft;
        instructionTMP.enableWordWrapping = true;

        // ── Tip row (single compact line) ─────────────────────────────
        GameObject tipObj = new GameObject("TipText");
        tipObj.transform.SetParent(expandedPanel.transform, false);
        var rtTip = tipObj.AddComponent<RectTransform>();
        rtTip.anchorMin = new Vector2(0f, 1f); rtTip.anchorMax = new Vector2(1f, 1f);
        rtTip.pivot = new Vector2(0.5f, 1f);
        rtTip.anchoredPosition = new Vector2(0f, -175f);
        rtTip.sizeDelta = new Vector2(-16f, 50f);
        tipTMP = tipObj.AddComponent<TextMeshProUGUI>();
        tipTMP.fontSize  = 9.5f;
        tipTMP.fontStyle = FontStyles.Italic;
        tipTMP.color     = new Color(0.45f, 0.78f, 1f);
        tipTMP.enableWordWrapping = true;

        // ── Keyboard hint (tiny, muted) ───────────────────────────────
        GameObject hintObj = new GameObject("NavHintText");
        hintObj.transform.SetParent(expandedPanel.transform, false);
        var rtHint = hintObj.AddComponent<RectTransform>();
        rtHint.anchorMin = new Vector2(0f, 0f); rtHint.anchorMax = new Vector2(1f, 0f);
        rtHint.pivot = new Vector2(0.5f, 0f);
        rtHint.anchoredPosition = new Vector2(0f, 46f);
        rtHint.sizeDelta = new Vector2(-16f, 16f);
        var hintTMP = hintObj.AddComponent<TextMeshProUGUI>();
        hintTMP.text = "<color=#3D5068>[P] Prev  ·  [N] Next  ·  [H] Hide</color>";
        hintTMP.fontSize = 8.5f;
        hintTMP.alignment = TextAlignmentOptions.Center;
        hintTMP.enableWordWrapping = false;

        // ── Footer nav bar ─────────────────────────────────────────────
        GameObject footerObj = new GameObject("FooterBar");
        footerObj.transform.SetParent(expandedPanel.transform, false);
        var rtFooter = footerObj.AddComponent<RectTransform>();
        rtFooter.anchorMin = new Vector2(0f, 0f); rtFooter.anchorMax = new Vector2(1f, 0f);
        rtFooter.pivot = new Vector2(0.5f, 0f);
        rtFooter.anchoredPosition = new Vector2(0f, 8f);
        rtFooter.sizeDelta = new Vector2(-16f, 34f);

        prevBtn = CreateNavButton(footerObj.transform, "PrevBtn", "◀ Prev",
            new Vector2(0f, 0f), new Vector2(0.46f, 1f), PrevStep);
        nextBtn = CreateNavButton(footerObj.transform, "NextBtn", "Next ▶",
            new Vector2(0.54f, 0f), new Vector2(1f, 1f), NextStep);

        // ── Minimized badge ───────────────────────────────────────────
        minimizedBadge = new GameObject("MinimizedGuideBadge");
        minimizedBadge.transform.SetParent(hudCanvasObj.transform, false);
        var rtMini = minimizedBadge.AddComponent<RectTransform>();
        rtMini.anchorMin = new Vector2(0f, 1f); rtMini.anchorMax = new Vector2(0f, 1f);
        rtMini.pivot = new Vector2(0f, 1f);
        rtMini.anchoredPosition = new Vector2(16f, -16f);
        rtMini.sizeDelta = new Vector2(220f, 28f);
        var miniBg = minimizedBadge.AddComponent<Image>();
        miniBg.color = new Color(0.05f, 0.08f, 0.14f, 0.88f);
        var miniBtn = minimizedBadge.AddComponent<Button>();
        miniBtn.onClick.AddListener(ToggleExpanded);
        GameObject miniTxtObj = new GameObject("Text");
        miniTxtObj.transform.SetParent(minimizedBadge.transform, false);
        var rtMiniTxt = miniTxtObj.AddComponent<RectTransform>();
        rtMiniTxt.anchorMin = Vector2.zero; rtMiniTxt.anchorMax = Vector2.one;
        rtMiniTxt.offsetMin = new Vector2(8f, 0f); rtMiniTxt.offsetMax = new Vector2(-8f, 0f);
        minimizedTextTMP = miniTxtObj.AddComponent<TextMeshProUGUI>();
        minimizedTextTMP.fontSize  = 10f;
        minimizedTextTMP.fontStyle = FontStyles.Bold;
        minimizedTextTMP.color     = new Color(0.38f, 0.82f, 1f);
        minimizedTextTMP.alignment = TextAlignmentOptions.MidlineLeft;

        minimizedBadge.SetActive(false);
    }

    private void MakeDivider(Transform parent, float yOffset)
    {
        GameObject div = new GameObject("Divider");
        div.transform.SetParent(parent, false);
        var rt = div.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, yOffset);
        rt.sizeDelta = new Vector2(-16f, 1f);
        var img = div.AddComponent<Image>();
        img.color = new Color(0.2f, 0.3f, 0.45f, 0.6f);
    }




    private Button CreateNavButton(Transform parent, string name, string label, Vector2 anchorMin, Vector2 anchorMax, UnityEngine.Events.UnityAction action)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);
        var rt = btnObj.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var img = btnObj.AddComponent<Image>();
        img.color = new Color(0.12f, 0.22f, 0.36f, 0.85f);

        var btn = btnObj.AddComponent<Button>();
        btn.onClick.AddListener(action);

        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(btnObj.transform, false);
        var rtTxt = txtObj.AddComponent<RectTransform>();
        rtTxt.anchorMin = Vector2.zero;
        rtTxt.anchorMax = Vector2.one;
        rtTxt.offsetMin = Vector2.zero;
        rtTxt.offsetMax = Vector2.zero;

        var tmp = txtObj.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 11.5f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        return btn;
    }
}
