using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// World-Space Interactive CPU Scheduling Challenge Station.
/// Physically mounted on Wall_West (adjacent to the North Gantt wall, next to the FCFS workstation).
/// Displays structured CPU scheduling questions with exactly FOUR answer options (A, B, C, D).
/// Features first-person [E] interaction, real answer evaluation, step-by-step educational explanations,
/// full quiz progression flow (Start, Select, Feedback, Next, Results, Retry), and score tracking.
/// Preserves the Gantt chart on Wall_North completely untouched.
/// </summary>
public class LiveChallengeController : MonoBehaviour
{
    public static LiveChallengeController Instance { get; private set; }

    [Header("Wall Mounting Location (Wall_West)")]
    [Tooltip("Mounted on the West Wall, adjacent to North Gantt wall, directly beside FCFS workstation")]
    [SerializeField] private Vector3 wallDisplayPosition = new Vector3(-9.70f, 2.10f, 4.30f);
    [SerializeField] private Vector3 wallDisplayRotation = new Vector3(0f, -90f, 0f); // Faces +X towards player into room
    [SerializeField] private Vector2 displayDimensions = new Vector2(5.80f, 3.20f);

    public enum QuizState
    {
        Welcome,
        QuestionActive,
        Feedback,
        Completed
    }

    public QuizState CurrentState { get; private set; } = QuizState.Welcome;
    public bool ChallengeActive => CurrentState == QuizState.QuestionActive || CurrentState == QuizState.Feedback;

    public int CurrentQuestionIndex { get; private set; } = 0;
    public int ScoreXP { get; private set; } = 0;
    public int CorrectAnswersCount { get; private set; } = 0;
    public int TotalQuestionsAnswered { get; private set; } = 0;

    // Hierarchy roots
    private GameObject stationRoot;
    private GameObject screenFrame;
    private GameObject contentRoot;

    // TextMeshPro references
    private TextMeshPro titleTMP;
    private TextMeshPro statusTMP;
    private TextMeshPro categoryTMP;
    private TextMeshPro questionTMP;
    private TextMeshPro processTableTMP;
    private TextMeshPro feedbackTitleTMP;
    private TextMeshPro feedbackExplanationTMP;

    // 4 World-space Option Buttons
    private GameObject[] optionButtonObjs = new GameObject[4];
    private Renderer[] optionRenderers = new Renderer[4];
    private TextMeshPro[] optionTextTMPs = new TextMeshPro[4];
    private BoxCollider[] optionColliders = new BoxCollider[4];
    private OptionInteractionButton[] optionInteractables = new OptionInteractionButton[4];

    // Control buttons (Start, Next, Retry)
    private GameObject controlBtnObj;
    private TextMeshPro controlBtnTMP;
    private Renderer controlBtnRenderer;
    private ControlInteractionButton controlInteractable;

    // Data
    private List<SchedulingChallenge> questionBank = new List<SchedulingChallenge>();
    private SchedulingChallenge currentChallenge;
    private int selectedOptionIndex = -1;
    private bool answerLocked = false;
    private float questionStartTime = 0f;

    public event Action<bool> OnChallengeAnswered;

    private static readonly Color OptionIdle = new Color(0.19f, 0.29f, 0.44f);

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) { Destroy(gameObject); return; }
    }

    private void Start()
    {
        LoadQuestionBank();
        BuildPhysicalWallDisplay();
        StartQuiz(); // Immediately display questions active on the board!
        EnsureConsolePointerButton();
    }

    /// <summary>
    /// Loads the structured questions from the master question database.
    /// </summary>
    public void LoadQuestionBank()
    {
        questionBank = GameChallengeDatabase.GetChallenges();
        if (questionBank == null || questionBank.Count == 0)
        {
            Debug.LogWarning("[LiveChallengeController] Question bank empty; generating fallback questions.");
            questionBank = new List<SchedulingChallenge>();
        }
    }

    /// <summary>
    /// Constructs the sleek, physical world-space monitor display on Wall_West.
    /// </summary>
    public void BuildPhysicalWallDisplay()
    {
        // Check if already built
        stationRoot = GameObject.Find("Challenge_Wall_Station");
        if (stationRoot != null)
        {
            // Reuse existing root
            stationRoot.transform.position = wallDisplayPosition;
            stationRoot.transform.rotation = Quaternion.Euler(wallDisplayRotation);
        }
        else
        {
            // Find existing Visualization_Board to reuse or create new
            var existingVis = GameObject.Find("Visualization_Board");
            if (existingVis != null)
            {
                stationRoot = existingVis.transform.parent != null && existingVis.transform.parent.name == "Process_Visualization_Area"
                    ? existingVis.transform.parent.gameObject
                    : existingVis;
                stationRoot.name = "Challenge_Wall_Station";
                stationRoot.transform.position = wallDisplayPosition;
                stationRoot.transform.rotation = Quaternion.Euler(wallDisplayRotation);
            }
            else
            {
                stationRoot = new GameObject("Challenge_Wall_Station");
                stationRoot.transform.position = wallDisplayPosition;
                stationRoot.transform.rotation = Quaternion.Euler(wallDisplayRotation);
            }
        }

        // Clean any old child objects to build fresh layout
        for (int i = stationRoot.transform.childCount - 1; i >= 0; i--)
        {
            var child = stationRoot.transform.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }

        // Permanently strip any black screens/frames/backplates per user request
        string[] blackThings = { "StationBezel", "DisplayScreen", "StationGlowTrim", "Visualization_Board" };
        foreach (var bt in blackThings)
        {
            var found = GameObject.Find(bt);
            if (found != null && found != stationRoot)
            {
                if (Application.isPlaying) Destroy(found);
                else DestroyImmediate(found);
            }
        }

        // Ensure stationRoot itself does not render any black mesh
        var mr = stationRoot.GetComponent<MeshRenderer>();
        if (mr != null) { if (Application.isPlaying) Destroy(mr); else DestroyImmediate(mr); }
        var mf = stationRoot.GetComponent<MeshFilter>();
        if (mf != null) { if (Application.isPlaying) Destroy(mf); else DestroyImmediate(mf); }
        var col = stationRoot.GetComponent<Collider>();
        if (col != null) { if (Application.isPlaying) Destroy(col); else DestroyImmediate(col); }

        Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
        if (litShader == null) litShader = Shader.Find("Standard");

        // 1. Overhead Soft Station Light (illuminates the wall surface nicely)
        GameObject stationLightObj = new GameObject("StationOverheadLight");
        stationLightObj.transform.SetParent(stationRoot.transform, false);
        stationLightObj.transform.localPosition = new Vector3(0, displayDimensions.y * 0.5f + 0.35f, -0.6f);
        Light stLight = stationLightObj.AddComponent<Light>();
        stLight.type = LightType.Point;
        stLight.range = 8.5f;
        stLight.intensity = 2.4f;
        stLight.color = new Color(0.2f, 0.85f, 1.0f);
        stLight.shadows = LightShadows.None;

        // 2. Content Root directly on the wall for TextMeshPro and interactive buttons
        contentRoot = new GameObject("StationContent");
        contentRoot.transform.SetParent(stationRoot.transform, false);
        contentRoot.transform.localPosition = new Vector3(0, 0, -0.02f);
        contentRoot.transform.localRotation = Quaternion.identity;

        // ── Title Header ──────────────────────────────────────────────
        titleTMP = CreateText(contentRoot.transform, "StationTitle_TMP",
            new Vector3(0f, 1.20f, -0.01f), new Vector2(5.6f, 0.40f),
            "CPU SCHEDULING CHALLENGE STATION", 0.72f, FontStyles.Bold,
            new Color(0.0f, 0.90f, 1.0f), TextAlignmentOptions.Center);

        // ── Status Bar (Progress, XP, Accuracy) ────────────────────────
        statusTMP = CreateText(contentRoot.transform, "StationStatus_TMP",
            new Vector3(0f, 0.98f, -0.01f), new Vector2(5.6f, 0.28f),
            "INTERACTIVE LAB STATION • 4-OPTION QUIZ", 0.38f, FontStyles.Normal,
            new Color(0.65f, 0.75f, 0.88f), TextAlignmentOptions.Center);

        // ── Category / Difficulty Tag ─────────────────────────────────
        categoryTMP = CreateText(contentRoot.transform, "CategoryTag_TMP",
            new Vector3(0f, 0.78f, -0.01f), new Vector2(5.6f, 0.28f),
            "", 0.45f, FontStyles.Bold,
            new Color(1.0f, 0.75f, 0.2f), TextAlignmentOptions.Center);

        // ── Question Body Text (Large, crisp, high legibility) ─────────
        questionTMP = CreateText(contentRoot.transform, "QuestionText_TMP",
            new Vector3(0f, 0.48f, -0.01f), new Vector2(5.5f, 0.55f),
            "Welcome to the CPU Scheduling Challenge Lab.\nTest your operating system scheduling knowledge in first-person 3D.",
            0.54f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
        questionTMP.textWrappingMode = TextWrappingModes.Normal;

        // ── Process Table / Supporting Line ───────────────────────────
        processTableTMP = CreateText(contentRoot.transform, "ProcessTable_TMP",
            new Vector3(0f, 0.20f, -0.01f), new Vector2(5.5f, 0.28f),
            "", 0.40f, FontStyles.Normal,
            new Color(0.35f, 0.85f, 1.0f), TextAlignmentOptions.Center);

        // ── 4 World-Space Answer Option Buttons (A, B, C, D) directly on wall ──
        // GRIDLOCK: rows and the feedback / control line no longer overlap.
        float[] rowY = new float[] { -0.06f, -0.38f, -0.70f, -1.02f };
        char[] letters = new char[] { 'A', 'B', 'C', 'D' };

        for (int i = 0; i < 4; i++)
        {
            int idx = i;
            char letter = letters[i];
            float yPos = rowY[i];

            GameObject btnObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            btnObj.name = $"Btn_Option_{letter}";
            btnObj.transform.SetParent(contentRoot.transform, false);
            btnObj.transform.localPosition = new Vector3(0f, yPos, 0f);
            btnObj.transform.localRotation = Quaternion.identity;
            btnObj.transform.localScale = new Vector3(5.40f, 0.28f, 0.03f);

            var rend = btnObj.GetComponent<Renderer>();
            // GRIDLOCK: a shade lighter than the wall, so the answer rows read as buttons in a headset.
            Material optMat = new Material(litShader) { color = OptionIdle };
            rend.sharedMaterial = optMat;

            // BoxCollider for first-person interaction
            var boxCol = btnObj.GetComponent<BoxCollider>();
            boxCol.isTrigger = false;

            // Text inside option button – positioned on the front surface of the button
            var optTMP = CreateText(contentRoot.transform, $"OptionText_{letter}_TMP",
                new Vector3(0f, yPos, -0.025f), new Vector2(5.20f, 0.28f),
                $"[ {letter} ] Option {letter}", 0.44f, FontStyles.Bold,
                Color.white, TextAlignmentOptions.MidlineLeft);
            optTMP.margin = new Vector4(0.25f, 0f, 0.25f, 0f);

            // Interaction component
            var interactComp = btnObj.AddComponent<OptionInteractionButton>();
            interactComp.controller = this;
            interactComp.optionIndex = idx;
            interactComp.optionLetter = letter;

            optionButtonObjs[i] = btnObj;
            optionRenderers[i] = rend;
            optionTextTMPs[i] = optTMP;
            optionColliders[i] = boxCol;
            optionInteractables[i] = interactComp;
        }

        // ── Feedback Title & Explanation ──────────────────────────────
        feedbackTitleTMP = CreateText(contentRoot.transform, "FeedbackTitle_TMP",
            new Vector3(-0.85f, -1.30f, -0.01f), new Vector2(3.7f, 0.26f),
            "", 0.42f, FontStyles.Bold,
            new Color(0.2f, 0.9f, 0.45f), TextAlignmentOptions.Center);

        feedbackExplanationTMP = CreateText(contentRoot.transform, "FeedbackExplanation_TMP",
            new Vector3(-0.85f, -1.56f, -0.01f), new Vector2(3.7f, 0.32f),
            "", 0.32f, FontStyles.Normal,
            new Color(0.88f, 0.92f, 0.98f), TextAlignmentOptions.Center);
        feedbackExplanationTMP.textWrappingMode = TextWrappingModes.Normal;

        // ── Control Button (Start Quiz / Next Question / Retry) ───────
        controlBtnObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        controlBtnObj.name = "Btn_QuizControl";
        controlBtnObj.transform.SetParent(contentRoot.transform, false);
        controlBtnObj.transform.localPosition = new Vector3(1.9f, -1.42f, 0f);
        controlBtnObj.transform.localRotation = Quaternion.identity;
        controlBtnObj.transform.localScale = new Vector3(1.60f, 0.34f, 0.03f);

        controlBtnRenderer = controlBtnObj.GetComponent<Renderer>();
        Material ctrlMat = new Material(litShader) { color = new Color(0.10f, 0.45f, 0.75f) };
        ctrlMat.EnableKeyword("_EMISSION");
        ctrlMat.SetColor("_EmissionColor", new Color(0.10f, 0.45f, 0.75f) * 0.4f);
        controlBtnRenderer.sharedMaterial = ctrlMat;

        controlBtnTMP = CreateText(contentRoot.transform, "ControlBtnLabel_TMP",
            new Vector3(1.9f, -1.42f, -0.025f), new Vector2(1.5f, 0.30f),
            "START QUIZ", 0.38f, FontStyles.Bold,
            Color.white, TextAlignmentOptions.Center);

        controlInteractable = controlBtnObj.AddComponent<ControlInteractionButton>();
        controlInteractable.controller = this;
    }

    /// <summary>GRIDLOCK: the control button and its label (a separate object) show and hide together.</summary>
    private void SetControlVisible(bool visible)
    {
        if (controlBtnObj != null) controlBtnObj.SetActive(visible);
        if (controlBtnTMP != null) controlBtnTMP.gameObject.SetActive(visible);
    }

    private TextMeshPro CreateText(Transform parent, string name, Vector3 localPos, Vector2 sizeDelta,
        string text, float fontSize, FontStyles style, Color color, TextAlignmentOptions alignment)
    {
        GameObject textObj = new GameObject(name);
        textObj.transform.SetParent(parent, false);
        textObj.transform.localPosition = localPos;
        textObj.transform.localRotation = Quaternion.identity;
        textObj.transform.localScale = Vector3.one;

        var tmp = textObj.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = alignment;

        var rt = textObj.GetComponent<RectTransform>();
        if (rt != null) rt.sizeDelta = sizeDelta;

        return tmp;
    }

    /// <summary>
    /// Displays the welcome / idle screen at the station.
    /// </summary>
    public void ShowWelcomeScreen()
    {
        CurrentState = QuizState.Welcome;
        selectedOptionIndex = -1;
        answerLocked = false;

        titleTMP.text = "CPU SCHEDULING CHALLENGE STATION";
        titleTMP.color = new Color(0.0f, 0.90f, 1.0f);

        statusTMP.text = $"STAND HERE AND PRESS [E] TO INTERACT • TOTAL QUESTIONS: {questionBank.Count}";
        categoryTMP.text = "[ INTERACTIVE CPU SCHEDULING LAB ]";
        categoryTMP.color = new Color(0.35f, 0.85f, 1.0f);

        questionTMP.text = "Test your mastery of CPU Scheduling Algorithms!\nTopics: FCFS, SRTF, Priority, Round Robin, Metrics & Idle Time.\nWalk up to the wall and press [E] on an answer to submit.";
        processTableTMP.text = "Four options (A, B, C, D) are presented with instant feedback and calculation explanations.";

        // Keep option buttons active on welcome screen showing topic overview
        string[] welcomeTopics = new string[] {
            "[ A ] FCFS & CPU Utilization",
            "[ B ] SRTF & Preemption Mechanics",
            "[ C ] Priority & Aging Solutions",
            "[ D ] Round Robin & Time Quantum"
        };
        for (int i = 0; i < 4; i++)
        {
            if (optionButtonObjs[i] != null)
            {
                optionButtonObjs[i].SetActive(true);
                if (optionTextTMPs[i] != null) optionTextTMPs[i].text = welcomeTopics[i];
                SetButtonColor(optionRenderers[i], OptionIdle, false);
            }
        }

        feedbackTitleTMP.text = "";
        feedbackExplanationTMP.text = "";

        // Control button shows START QUIZ
        SetControlVisible(true);
        controlBtnTMP.text = "START QUIZ ➔";
        controlInteractable.actionPrompt = "Press [E] or Click to START QUIZ";
        SetButtonColor(controlBtnRenderer, new Color(0.12f, 0.55f, 0.85f), true);
    }

    /// <summary>
    /// Starts the quiz from the beginning.
    /// </summary>
    public void StartQuiz()
    {
        CurrentQuestionIndex = 0;
        ScoreXP = 0;
        CorrectAnswersCount = 0;
        TotalQuestionsAnswered = 0;

        AudioManager.Instance?.PlaySfx(AudioManager.SfxType.Click);
        LoadQuestion(CurrentQuestionIndex);
    }

    /// <summary>
    /// Loads and presents one question on the wall display.
    /// </summary>
    public void LoadQuestion(int index)
    {
        if (questionBank == null || questionBank.Count == 0) LoadQuestionBank();

        if (index < 0 || index >= questionBank.Count)
        {
            ShowCompletionScreen();
            return;
        }

        CurrentQuestionIndex = index;
        currentChallenge = questionBank[index];
        CurrentState = QuizState.QuestionActive;
        selectedOptionIndex = -1;
        answerLocked = false;
        questionStartTime = Time.time;

        // Validate that challenge has exactly 4 options
        EnsureFourOptions(currentChallenge);

        // Header & Status
        titleTMP.text = "CPU SCHEDULING CHALLENGE STATION";
        int totalQ = questionBank.Count;
        float accuracy = TotalQuestionsAnswered > 0 ? ((float)CorrectAnswersCount / TotalQuestionsAnswered * 100f) : 100f;
        statusTMP.text = $"QUESTION {CurrentQuestionIndex + 1} OF {totalQ}  •  XP EARNED: {ScoreXP}  •  ACCURACY: {accuracy:0}%";

        // Category Tag
        categoryTMP.text = $"[ {currentChallenge.Difficulty.ToString().ToUpper()} • {currentChallenge.AlgorithmName.ToUpper()} ]";
        categoryTMP.color = GetDifficultyColor(currentChallenge.Difficulty);

        // Question text
        questionTMP.text = currentChallenge.Description;

        // Process Table (if question has processes)
        if (currentChallenge.Processes != null && currentChallenge.Processes.Count > 0)
        {
            var pStrings = new List<string>();
            foreach (var p in currentChallenge.Processes)
            {
                pStrings.Add($"{p.Id}(AT={p.ArrivalTime}, BT={p.BurstTime}{(p.Priority > 0 ? $", Pri={p.Priority}" : "")})");
            }
            processTableTMP.text = string.Join("  |  ", pStrings);
        }
        else
        {
            processTableTMP.text = "";
        }

        char[] letters = new char[] { 'A', 'B', 'C', 'D' };
        for (int i = 0; i < 4; i++)
        {
            var opt = currentChallenge.Options[i];
            optionButtonObjs[i].SetActive(true);
            optionColliders[i].enabled = true;

            // Clean option label
            string label = opt.SequenceLabel;
            if (!label.StartsWith("A.") && !label.StartsWith("B.") && !label.StartsWith("C.") && !label.StartsWith("D.") &&
                !label.StartsWith("A ") && !label.StartsWith("B ") && !label.StartsWith("C ") && !label.StartsWith("D "))
            {
                label = $"{letters[i]}. {label}";
            }

            optionTextTMPs[i].text = $"[ {letters[i]} ]  {label}";
            optionTextTMPs[i].color = Color.white;

            // Reset idle material
            SetButtonColor(optionRenderers[i], OptionIdle, false);

            optionInteractables[i].actionPrompt = $"Press [E] or Click: {label}";
        }

        // Hide feedback until answer is selected
        feedbackTitleTMP.text = "";
        feedbackExplanationTMP.text = "";

        // Hide control button during active answering
        SetControlVisible(false);
    }

    /// <summary>
    /// Guarantees that the active question has exactly 4 options.
    /// </summary>
    private void EnsureFourOptions(SchedulingChallenge challenge)
    {
        if (challenge.Options == null) challenge.Options = new List<ChallengeOption>();

        char[] letters = new char[] { 'A', 'B', 'C', 'D' };
        while (challenge.Options.Count < 4)
        {
            int idx = challenge.Options.Count;
            challenge.Options.Add(new ChallengeOption(
                $"{letters[idx]}. Additional scheduling parameter",
                false,
                "Incorrect distractor option."));
        }

        while (challenge.Options.Count > 4)
        {
            challenge.Options.RemoveAt(challenge.Options.Count - 1);
        }

        // Ensure at least one option is marked correct
        if (!challenge.Options.Exists(o => o.IsCorrect))
        {
            challenge.Options[0].IsCorrect = true;
        }
    }

    /// <summary>
    /// Invoked when the player selects one of the 4 answer options (0=A, 1=B, 2=C, 3=D).
    /// </summary>
    public void SelectOption(int optionIndex)
    {
        if (CurrentState != QuizState.QuestionActive || answerLocked || currentChallenge == null) return;
        if (optionIndex < 0 || optionIndex >= currentChallenge.Options.Count) return;

        answerLocked = true;
        selectedOptionIndex = optionIndex;
        CurrentState = QuizState.Feedback;
        TotalQuestionsAnswered++;

        var selectedOption = currentChallenge.Options[optionIndex];
        float timeTaken = Time.time - questionStartTime;

        // Evaluate using ProgressionManager or direct evaluation
        ChallengeResultReport report = null;
        var prog = ProgressionManager.Instance;
        if (prog != null)
        {
            report = prog.EvaluateChallenge(currentChallenge, selectedOption, timeTaken);
        }

        bool isCorrect = report != null ? report.IsCorrect : selectedOption.IsCorrect;

        // Disable colliders on option buttons so player cannot click again
        for (int i = 0; i < 4; i++)
        {
            if (optionColliders[i] != null) optionColliders[i].enabled = false;
        }

        // Find true correct index
        int correctIndex = currentChallenge.Options.FindIndex(o => o.IsCorrect);

        // Visual feedback on options
        if (isCorrect)
        {
            CorrectAnswersCount++;
            int xpEarned = report != null ? report.ScoreEarned : currentChallenge.BaseXP;
            ScoreXP += xpEarned;

            // Highlight selected option in vibrant green
            SetButtonColor(optionRenderers[optionIndex], new Color(0.08f, 0.75f, 0.35f), true);
            optionTextTMPs[optionIndex].color = Color.white;

            feedbackTitleTMP.text = $"CORRECT! 🎉  (+{xpEarned} XP)";
            feedbackTitleTMP.color = new Color(0.15f, 0.95f, 0.45f);

            string explanation = !string.IsNullOrEmpty(selectedOption.Feedback)
                ? selectedOption.Feedback
                : currentChallenge.DetailedExplanation;
            feedbackExplanationTMP.text = explanation;

            AudioManager.Instance?.PlaySfx(AudioManager.SfxType.Success);
        }
        else
        {
            // Highlight selected in red
            SetButtonColor(optionRenderers[optionIndex], new Color(0.85f, 0.20f, 0.20f), true);

            // Highlight the true correct option in gold/emerald
            if (correctIndex >= 0 && correctIndex < 4)
            {
                SetButtonColor(optionRenderers[correctIndex], new Color(0.15f, 0.70f, 0.35f), true);
                char correctLetter = (char)('A' + correctIndex);
                optionTextTMPs[correctIndex].text += "  ✔ [CORRECT ANSWER]";
            }

            feedbackTitleTMP.text = "INCORRECT ❌";
            feedbackTitleTMP.color = new Color(0.95f, 0.30f, 0.30f);

            char correctLetterChar = correctIndex >= 0 ? (char)('A' + correctIndex) : '?';
            string correctLabel = correctIndex >= 0 ? currentChallenge.Options[correctIndex].SequenceLabel : "None";
            feedbackExplanationTMP.text = $"Correct Option: [{correctLetterChar}] {correctLabel}\n{currentChallenge.DetailedExplanation}";

            AudioManager.Instance?.PlaySfx(AudioManager.SfxType.Error);
        }

        // Update status bar with new score
        float accuracy = (float)CorrectAnswersCount / TotalQuestionsAnswered * 100f;
        statusTMP.text = $"QUESTION {CurrentQuestionIndex + 1} OF {questionBank.Count}  •  XP: {ScoreXP}  •  ACCURACY: {accuracy:0}%";

        OnChallengeAnswered?.Invoke(isCorrect);

        // Show Next Question button
        SetControlVisible(true);
        bool isLastQuestion = CurrentQuestionIndex >= questionBank.Count - 1;
        controlBtnTMP.text = isLastQuestion ? "VIEW FINAL RESULTS ➔" : "NEXT QUESTION ➔";
        controlInteractable.actionPrompt = isLastQuestion ? "Press [E] or Click to View Final Results" : "Press [E] or Click for Next Question";
        SetButtonColor(controlBtnRenderer, new Color(0.15f, 0.55f, 0.85f), true);
    }

    /// <summary>
    /// Advances to the next question or completion screen.
    /// </summary>
    public void AdvanceNextQuestion()
    {
        AudioManager.Instance?.PlaySfx(AudioManager.SfxType.Click);
        if (CurrentQuestionIndex < questionBank.Count - 1)
        {
            LoadQuestion(CurrentQuestionIndex + 1);
        }
        else
        {
            ShowCompletionScreen();
        }
    }

    /// <summary>
    /// Displays the quiz summary and final score card.
    /// </summary>
    public void ShowCompletionScreen()
    {
        CurrentState = QuizState.Completed;
        answerLocked = true;

        titleTMP.text = "🏆 QUIZ COMPLETED!";
        titleTMP.color = new Color(1.0f, 0.85f, 0.20f);

        int total = TotalQuestionsAnswered > 0 ? TotalQuestionsAnswered : questionBank.Count;
        float percent = total > 0 ? ((float)CorrectAnswersCount / total * 100f) : 0f;

        statusTMP.text = $"FINAL SCORE: {CorrectAnswersCount} / {total} CORRECT ({percent:0}%)  •  TOTAL XP: +{ScoreXP}";

        string rankBadge = "🌱 Scheduling Novice";
        if (percent >= 90f) rankBadge = "👑 CPU Scheduling Grandmaster";
        else if (percent >= 75f) rankBadge = "⚡ OS Kernel Architect";
        else if (percent >= 50f) rankBadge = "⚙️ Process Dispatcher";

        categoryTMP.text = $"AWARDED RANK: {rankBadge}";
        categoryTMP.color = new Color(0.2f, 0.9f, 0.5f);

        questionTMP.text = $"Outstanding effort! You completed all CPU Scheduling challenges.\nYour final accuracy is {percent:0}% with {ScoreXP} XP earned.\nPress [E] below to restart the quiz and practice again!";
        processTableTMP.text = "";

        // Hide option buttons
        for (int i = 0; i < 4; i++)
        {
            if (optionButtonObjs[i] != null) optionButtonObjs[i].SetActive(false);
        }

        feedbackTitleTMP.text = percent >= 70f ? "EXCELLENT PERFORMANCE!" : "GOOD PRACTICE!";
        feedbackTitleTMP.color = percent >= 70f ? new Color(0.2f, 0.9f, 0.45f) : new Color(1.0f, 0.75f, 0.2f);
        feedbackExplanationTMP.text = "You can replay this quiz anytime from this wall station to improve your score.";

        // Control button shows RETRY QUIZ
        SetControlVisible(true);
        controlBtnTMP.text = "↺ RETRY QUIZ";
        controlInteractable.actionPrompt = "Press [E] or Click to RETRY QUIZ";
        SetButtonColor(controlBtnRenderer, new Color(0.20f, 0.65f, 0.40f), true);
    }

    /// <summary>
    /// Resets quiz progress and begins again from question 1.
    /// </summary>
    public void RestartQuiz()
    {
        AudioManager.Instance?.PlaySfx(AudioManager.SfxType.Click);
        StartQuiz();
    }

    private void SetButtonColor(Renderer rend, Color color, bool emissive)
    {
        if (rend == null) return;
        rend.material.color = color;
        if (emissive)
        {
            rend.material.EnableKeyword("_EMISSION");
            rend.material.SetColor("_EmissionColor", color * 0.45f);
        }
        else
        {
            rend.material.DisableKeyword("_EMISSION");
        }
    }

    private Color GetDifficultyColor(ChallengeDifficulty diff)
    {
        switch (diff)
        {
            case ChallengeDifficulty.Easy: return new Color(0.3f, 0.85f, 1.0f);
            case ChallengeDifficulty.Medium: return new Color(0.3f, 0.95f, 0.5f);
            case ChallengeDifficulty.Hard: return new Color(1.0f, 0.75f, 0.2f);
            case ChallengeDifficulty.Expert: return new Color(0.95f, 0.35f, 0.85f);
            default: return Color.white;
        }
    }

    /// <summary>
    /// Ensures the physical button on the control console points the player to the West Wall challenge station.
    /// </summary>
    private void EnsureConsolePointerButton()
    {
        var podium = GameObject.Find("Physical_Control_Console");
        if (podium == null) return;

        var existingBtn = GameObject.Find("Btn_Challenge");
        if (existingBtn != null)
        {
            var btnComp = existingBtn.GetComponent<ConsolePointerButton>();
            if (btnComp == null)
            {
                Destroy(existingBtn.GetComponent<LiveChallengeController.ChallengeButton>());
                btnComp = existingBtn.AddComponent<ConsolePointerButton>();
            }
            btnComp.controller = this;
            return;
        }

        Vector3 worldPos = podium.transform.position
            + podium.transform.up * (podium.transform.lossyScale.y * 0.5f + 0.07f)
            + podium.transform.forward * (podium.transform.lossyScale.z * 0.5f + 0.30f);

        var btn = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        btn.name = "Btn_Challenge";
        btn.transform.position = worldPos;
        btn.transform.rotation = podium.transform.rotation;
        btn.transform.localScale = new Vector3(0.20f, 0.05f, 0.20f);

        Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
        if (litShader == null) litShader = Shader.Find("Standard");
        var mat = new Material(litShader) { color = new Color(0.95f, 0.55f, 0.15f) };
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", new Color(0.95f, 0.55f, 0.15f) * 0.6f);
        btn.GetComponent<Renderer>().material = mat;

        var lblGo = new GameObject("BtnLabel_TMP");
        lblGo.transform.SetParent(btn.transform, false);
        lblGo.transform.localPosition = new Vector3(0, 2.5f, 0);
        lblGo.transform.localRotation = Quaternion.Euler(90f, 0, 0);
        var lbl = lblGo.AddComponent<TextMeshPro>();
        lbl.text = "CHALLENGE WALL";
        lbl.fontSize = 0.28f;
        lbl.alignment = TextAlignmentOptions.Center;
        lbl.color = Color.white;
        lbl.fontStyle = FontStyles.Bold;
        lblGo.GetComponent<RectTransform>().sizeDelta = new Vector2(1.8f, 0.28f);

        var pBtn = btn.AddComponent<ConsolePointerButton>();
        pBtn.controller = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // Interactable buttons are defined as top-level classes in OptionInteractionButton.cs and ControlInteractionButton.cs

    public class ConsolePointerButton : MonoBehaviour, IInteractable
    {
        public LiveChallengeController controller;
        public string PromptText => "Press [E] to GO TO Challenge Board (West Wall)";

        public void Interact()
        {
            AudioManager.Instance?.PlaySfx(AudioManager.SfxType.Click);
            if (controller != null)
            {
                // Always ensure quiz is active
                if (controller.CurrentState == QuizState.Welcome ||
                    controller.CurrentState == QuizState.Completed)
                    controller.StartQuiz();
            }

            // Teleport player to stand in front of the West Wall quiz board
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                // fallback – find by PlayerMovement component
                var pm = UnityEngine.Object.FindAnyObjectByType<PlayerMovement>();
                if (pm != null) player = pm.gameObject;
            }
            if (player != null)
            {
                // Stand ~1.5 m in front of the board facing it (board faces +X at X=-9.68)
                player.transform.position = new Vector3(-7.8f, 0.1f, 4.3f);
                player.transform.rotation = Quaternion.Euler(0f, -90f, 0f); // face -X toward wall
            }
        }
    }


    // Backward-compatibility wrapper so any legacy reference doesn't break
    public class ChallengeButton : MonoBehaviour, IInteractable
    {
        public LiveChallengeController controller;
        public string PromptText => "Press [E] for Challenge Station";
        public void Interact() => controller?.StartQuiz();
    }
}
