using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Voice tutorial for the CPU Scheduling Lab.
/// Plays a scripted narration sequence with on-screen captions.
///
/// UNITY HAS NO BUILT-IN TEXT-TO-SPEECH, and the project ships no narration audio
/// assets, so the tutorial falls back to timed CAPTIONS + on-screen instructions
/// (the recommended Unity-native fallback). Each step can optionally carry a real
/// AudioClip narration: drop voice clips into the Narration Clips array (or assign
/// them in the Inspector) and they will play in sync with the captions.
///
/// Transport controls: Play / Pause / Replay / Mute, plus a separate voice volume
/// slider that drives AudioManager.voiceVolume.
/// Keyboard: [V] toggles the tutorial panel, [B] replays the current narration.
/// </summary>
public class VoiceTutorialController : MonoBehaviour
{
    public static VoiceTutorialController Instance { get; private set; }

    [System.Serializable]
    public class VoiceStep
    {
        public string caption;
        public float duration = 4f;
        public AudioClip narrationClip; // optional real voice narration

        public VoiceStep(string caption, float duration)
        {
            this.caption = caption;
            this.duration = duration;
        }
    }

    [Header("Narration Script")]
    [SerializeField] private List<VoiceStep> steps = new List<VoiceStep>();

    [Header("Timing")]
    [SerializeField] private float captionLeadIn = 0.6f;

    [Header("3D Integration (optional)")]
    [SerializeField] private string whiteboardName = "Teaching_Whiteboard";

    public bool IsPlaying { get; private set; }
    public bool IsPaused { get; private set; }
    public int CurrentStepIndex { get; private set; }

    private Coroutine playbackCoroutine;
    private GameObject panelRoot;
    private TMP_Text captionText;
    private TMP_Text progressText;
    private Button playBtn;
    private Button pauseBtn;
    private Button replayBtn;
    private Button muteBtn;
    private Button closeBtn;
    private Slider voiceSlider;
    private TMP_Text muteLabel;

    private TextMeshPro whiteboardTMP;

    public event Action<int> OnStepChanged;

    private void Awake()
    {
        // Voice tutorial permanently removed per user instruction
        gameObject.SetActive(false);
        Destroy(gameObject);
        return;
    }

    private void Start()
    {
    }

    // ─────────────────────────────────────────────────────────────
    // Default narration script (caption fallback)
    // ─────────────────────────────────────────────────────────────
    private void BuildDefaultScript()
    {
        steps.Clear();
        steps.Add(new VoiceStep("Welcome to the CPU Scheduling Lab. Walk up to any workstation and press E to select a scheduling algorithm.", 5f));
        steps.Add(new VoiceStep("The CONTROL CONSOLE has three physical buttons: RUN/PAUSE, STEP, and RESET. Press E on each to drive the simulation.", 5f));
        steps.Add(new VoiceStep("Press RUN to watch processes flow from the INCOMING shelf, through the READY QUEUE, onto the CPU CORE, and into the GANTT CHART.", 5f));
        steps.Add(new VoiceStep("Use STEP to advance exactly one time unit. Watch the Gantt board record every CPU burst in real time.", 4.5f));
        steps.Add(new VoiceStep("Compare algorithms: FCFS serves arrival order, SJF picks the shortest job, SRTF and Priority can preempt, and Round Robin shares the CPU in quantum slices.", 6f));
        steps.Add(new VoiceStep("Read the metrics on the arena console: Completion, Turnaround, Waiting and Response times update live for every process.", 4.5f));
        steps.Add(new VoiceStep("Press V at any time to replay this voice tutorial. Happy scheduling!", 4f));
    }

    public void SetScript(List<VoiceStep> customSteps)
    {
        if (customSteps == null || customSteps.Count == 0) return;
        steps = new List<VoiceStep>(customSteps);
        Stop();
    }

    // ─────────────────────────────────────────────────────────────
    // Transport controls
    // ─────────────────────────────────────────────────────────────
    public void Play()
    {
        if (steps.Count == 0) return;
        if (IsPlaying && !IsPaused) return;
        if (IsPaused) { Resume(); return; }

        if (playbackCoroutine != null) StopCoroutine(playbackCoroutine);
        IsPlaying = true;
        IsPaused = false;
        playbackCoroutine = StartCoroutine(PlaybackRoutine());
        UpdateTransportButtons();
        ShowPanel(true);
        AudioManager.Instance?.PlaySfx(AudioManager.SfxType.Click);
    }

    public void Pause()
    {
        if (!IsPlaying || IsPaused) return;
        IsPaused = true;
        StopAllCoroutines();
        playbackCoroutine = null;
        WindowsVoiceSpeaker.Instance?.Stop();
        UpdateTransportButtons();
        AudioManager.Instance?.PlaySfx(AudioManager.SfxType.Click);
    }

    public void Resume()
    {
        if (!IsPlaying || !IsPaused) return;
        IsPaused = false;
        playbackCoroutine = StartCoroutine(PlaybackRoutine());
        UpdateTransportButtons();
        AudioManager.Instance?.PlaySfx(AudioManager.SfxType.Click);
    }

    public void Replay()
    {
        CurrentStepIndex = 0;
        Stop();
        Play();
    }

    public void Stop()
    {
        IsPlaying = false;
        IsPaused = false;
        if (playbackCoroutine != null) StopCoroutine(playbackCoroutine);
        playbackCoroutine = null;
        AudioManager.Instance?.StopVoice();
        WindowsVoiceSpeaker.Instance?.Stop();
        UpdateTransportButtons();
    }

    public void ToggleMute()
    {
        var am = AudioManager.Instance;
        if (am == null) return;
        am.ToggleMuteVoice();
        UpdateMuteLabel();
        AudioManager.Instance?.PlaySfx(AudioManager.SfxType.Click);
    }

    public void SetVoiceVolume(float v)
    {
        AudioManager.Instance?.SetVoiceVolume(v);
        var speaker = WindowsVoiceSpeaker.Instance;
        if (speaker != null) speaker.Volume = v;
    }

        private IEnumerator PlaybackRoutine()
        {
            // Resume from current step
            while (CurrentStepIndex < steps.Count)
            {
                var step = steps[CurrentStepIndex];
                ShowCaption(step);
                WindowsVoiceSpeaker.Instance?.Speak(step.caption);
                OnStepChanged?.Invoke(CurrentStepIndex);

            // If a real narration clip is assigned, play it and wait for it (or its duration).
            if (step.narrationClip != null)
            {
                AudioManager.Instance?.PlayVoiceClip(step.narrationClip);
                float clipWait = Mathf.Max(step.duration, step.narrationClip.length + captionLeadIn);
                yield return WaitForSecondsUnpaused(clipWait);
            }
            else
            {
                // Caption fallback: hold the caption for the step duration.
                yield return WaitForSecondsUnpaused(step.duration);
            }

            CurrentStepIndex++;
        }

        Stop();
        ShowCaption(new VoiceStep("Tutorial complete. Press V to replay.", 3f));
    }

    private IEnumerator WaitForSecondsUnpaused(float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            if (!IsPaused)
            {
                elapsed += Time.deltaTime;
            }
            yield return null;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Caption display
    // ─────────────────────────────────────────────────────────────
    private void ShowCaption(VoiceStep step)
    {
        if (captionText != null) captionText.text = step.caption;
        if (progressText != null) progressText.text = $"NARRATION {Mathf.Min(CurrentStepIndex + 1, steps.Count)} / {steps.Count}";
        if (whiteboardTMP != null) whiteboardTMP.text = step.caption;
    }

    private void FindWhiteboard()
    {
        var wb = GameObject.Find(whiteboardName);
        if (wb != null)
        {
            whiteboardTMP = wb.GetComponent<TextMeshPro>();
            if (whiteboardTMP == null) whiteboardTMP = wb.GetComponentInChildren<TextMeshPro>();
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Overlay panel (screen-space, built at runtime like the tutorial HUD)
    // ─────────────────────────────────────────────────────────────
    private void BuildOverlayPanel()
    {
        var canvasObj = GameObject.Find("VoiceTutorialCanvas");
        if (canvasObj != null)
        {
            panelRoot = canvasObj;
            BindPanelReferences();
            ShowPanel(false);
            return;
        }

        // Root canvas
        canvasObj = new GameObject("VoiceTutorialCanvas");
        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30;
        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObj.AddComponent<GraphicRaycaster>();

        // Bottom caption bar
        var bar = new GameObject("CaptionBar");
        bar.transform.SetParent(canvasObj.transform, false);
        var barRt = bar.AddComponent<RectTransform>();
        barRt.anchorMin = new Vector2(0.10f, 0.07f);
        barRt.anchorMax = new Vector2(0.90f, 0.31f);
        barRt.pivot = new Vector2(0.5f, 0.5f);
        var barImg = bar.AddComponent<Image>();
        barImg.color = new Color(0.05f, 0.08f, 0.12f, 0.92f);

        captionText = CreateTmp("CaptionText", bar.transform, "", 34, Color.white, TextAlignmentOptions.TopLeft);
        var capRt = captionText.rectTransform;
        capRt.anchorMin = new Vector2(0.03f, 0.40f);
        capRt.anchorMax = new Vector2(0.97f, 0.97f);
        capRt.pivot = new Vector2(0.5f, 0.5f);

        progressText = CreateTmp("ProgressText", bar.transform, "", 20, new Color(0.4f, 0.75f, 1f), TextAlignmentOptions.BottomLeft);
        var progRt = progressText.rectTransform;
        progRt.anchorMin = new Vector2(0.03f, 0.05f);
        progRt.anchorMax = new Vector2(0.38f, 0.35f);
        progRt.pivot = new Vector2(0.5f, 0.5f);

        // Transport button row
        var transport = new GameObject("TransportRow");
        transport.transform.SetParent(bar.transform, false);
        var trRt = transport.AddComponent<RectTransform>();
        trRt.anchorMin = new Vector2(0.40f, 0.05f);
        trRt.anchorMax = new Vector2(0.97f, 0.35f);
        trRt.pivot = new Vector2(0.5f, 0.5f);
        var hlg = transport.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 10;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        playBtn = CreateTransportButton("PlayBtn", "PLAY", transport.transform, () => Play());
        pauseBtn = CreateTransportButton("PauseBtn", "PAUSE", transport.transform, () => Pause());
        replayBtn = CreateTransportButton("ReplayBtn", "REPLAY", transport.transform, () => Replay());
        muteBtn = CreateTransportButton("MuteBtn", "MUTE", transport.transform, () => ToggleMute());
        closeBtn = CreateTransportButton("CloseBtn", "CLOSE", transport.transform, () => ShowPanel(false));

        // Voice volume slider
        var sliderObj = new GameObject("VoiceSlider");
        sliderObj.transform.SetParent(canvasObj.transform, false);
        var slRt = sliderObj.AddComponent<RectTransform>();
        slRt.anchorMin = new Vector2(0.10f, 0.015f);
        slRt.anchorMax = new Vector2(0.45f, 0.055f);
        slRt.pivot = new Vector2(0.5f, 0.5f);
        voiceSlider = sliderObj.AddComponent<Slider>();
        var bgImg = sliderObj.AddComponent<Image>();
        bgImg.color = new Color(0.1f, 0.14f, 0.2f, 0.9f);

        var slArea = new GameObject("SlidingArea");
        slArea.transform.SetParent(sliderObj.transform, false);
        var slAreaRt = slArea.AddComponent<RectTransform>();
        SetStretch(slAreaRt);
        var fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(slArea.transform, false);
        var fillAreaRt = fillArea.AddComponent<RectTransform>();
        SetStretch(fillAreaRt);
        var fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        var fillRt = fill.AddComponent<RectTransform>();
        fillRt.sizeDelta = new Vector2(0, 0);
        var fillImg = fill.AddComponent<Image>();
        fillImg.color = new Color(0.2f, 0.7f, 1f);

        var handle = new GameObject("Handle");
        handle.transform.SetParent(slArea.transform, false);
        var handleRt = handle.AddComponent<RectTransform>();
        handleRt.sizeDelta = new Vector2(24, 24);
        var handleImg = handle.AddComponent<Image>();
        handleImg.color = Color.white;

        voiceSlider.fillRect = fillRt;
        voiceSlider.handleRect = handleRt;
        voiceSlider.direction = Slider.Direction.LeftToRight;
        voiceSlider.minValue = 0f;
        voiceSlider.maxValue = 1f;
        voiceSlider.value = AudioManager.Instance != null ? AudioManager.Instance.voiceVolume : 1f;
        var speaker = WindowsVoiceSpeaker.Instance;
        if (speaker != null) speaker.Volume = voiceSlider.value;
        voiceSlider.onValueChanged.AddListener(v => SetVoiceVolume(v));

        muteLabel = CreateTmp("MuteLabel", sliderObj.transform, "VOICE VOLUME", 16, new Color(0.7f, 0.8f, 0.9f), TextAlignmentOptions.MidlineRight);
        var mlRt = muteLabel.rectTransform;
        mlRt.anchorMin = new Vector2(1.02f, 0f);
        mlRt.anchorMax = new Vector2(1.35f, 1f);
        mlRt.pivot = new Vector2(0f, 0.5f);

        panelRoot = canvasObj;
        ShowPanel(false);
        UpdateTransportButtons();
        UpdateMuteLabel();
    }

    private void BindPanelReferences()
    {
        if (panelRoot == null) return;
        captionText = panelRoot.transform.Find("CaptionBar/CaptionText")?.GetComponent<TMP_Text>();
        progressText = panelRoot.transform.Find("CaptionBar/ProgressText")?.GetComponent<TMP_Text>();
        playBtn = panelRoot.transform.Find("CaptionBar/TransportRow/PlayBtn")?.GetComponent<Button>();
        pauseBtn = panelRoot.transform.Find("CaptionBar/TransportRow/PauseBtn")?.GetComponent<Button>();
        replayBtn = panelRoot.transform.Find("CaptionBar/TransportRow/ReplayBtn")?.GetComponent<Button>();
        muteBtn = panelRoot.transform.Find("CaptionBar/TransportRow/MuteBtn")?.GetComponent<Button>();
        closeBtn = panelRoot.transform.Find("CaptionBar/TransportRow/CloseBtn")?.GetComponent<Button>();
        voiceSlider = panelRoot.transform.Find("VoiceSlider")?.GetComponent<Slider>();
        muteLabel = panelRoot.transform.Find("VoiceSlider/MuteLabel")?.GetComponent<TMP_Text>();

        if (playBtn != null) playBtn.onClick.RemoveAllListeners();
        if (pauseBtn != null) pauseBtn.onClick.RemoveAllListeners();
        if (replayBtn != null) replayBtn.onClick.RemoveAllListeners();
        if (muteBtn != null) muteBtn.onClick.RemoveAllListeners();
        if (closeBtn != null) closeBtn.onClick.RemoveAllListeners();
        if (playBtn != null) playBtn.onClick.AddListener(() => Play());
        if (pauseBtn != null) pauseBtn.onClick.AddListener(() => Pause());
        if (replayBtn != null) replayBtn.onClick.AddListener(() => Replay());
        if (muteBtn != null) muteBtn.onClick.AddListener(() => ToggleMute());
        if (closeBtn != null) closeBtn.onClick.AddListener(() => ShowPanel(false));
        if (voiceSlider != null)
        {
            voiceSlider.onValueChanged.RemoveAllListeners();
            voiceSlider.onValueChanged.AddListener(v => SetVoiceVolume(v));
        }
    }

    private TMP_Text CreateTmp(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions alignment)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.enableWordWrapping = true;
        return tmp;
    }

    private Button CreateTransportButton(string name, string label, Transform parent, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(90, 55);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.16f, 0.22f, 0.32f);
        var btn = go.AddComponent<Button>();
        btn.onClick.AddListener(onClick);
        var lbl = CreateTmp("Label", go.transform, label, 28, Color.white, TextAlignmentOptions.Midline);
        SetStretch(lbl.rectTransform);
        return btn;
    }

    private void ShowPanel(bool show)
    {
        if (panelRoot != null) panelRoot.SetActive(show);
    }

    public void TogglePanel()
    {
        if (panelRoot == null) return;
        if (panelRoot.activeSelf)
        {
            Stop();
            ShowPanel(false);
        }
        else
        {
            ShowPanel(true);
            Play();
        }
    }

    private void UpdateTransportButtons()
    {
        // Visual state feedback via color tint on transport buttons
        if (playBtn != null) SetBtnColor(playBtn, IsPlaying && !IsPaused ? new Color(0.2f, 0.6f, 0.35f) : new Color(0.16f, 0.22f, 0.32f));
        if (pauseBtn != null) SetBtnColor(pauseBtn, IsPaused ? new Color(0.7f, 0.5f, 0.15f) : new Color(0.16f, 0.22f, 0.32f));
    }

    private void UpdateMuteLabel()
    {
        var am = AudioManager.Instance;
        if (muteBtn != null)
        {
            var lbl = muteBtn.GetComponentInChildren<TMP_Text>();
            if (lbl != null) lbl.text = (am != null && am.MuteVoice) ? "UNMUTE" : "MUTE";
        }
    }

    private void SetBtnColor(Button btn, Color c)
    {
        var img = btn.GetComponent<Image>();
        if (img != null) img.color = c;
    }

    private void Update()
    {
        bool togglePressed = false;
        bool replayPressed = false;
        // Poll keyboard for tutorial transport shortcuts.
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            togglePressed = Keyboard.current.vKey.wasPressedThisFrame;
            replayPressed = Keyboard.current.bKey.wasPressedThisFrame;
        }
#else
        togglePressed = Input.GetKeyDown(KeyCode.V);
        replayPressed = Input.GetKeyDown(KeyCode.B);
#endif
        if (togglePressed) TogglePanel();
        if (replayPressed && panelRoot != null && panelRoot.activeSelf) Replay();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private static void SetStretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
