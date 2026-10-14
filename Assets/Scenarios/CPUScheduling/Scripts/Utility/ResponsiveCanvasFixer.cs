using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runtime responsive fixer for a Canvas using ScaleWithScreenSize.
/// Dynamically adjusts the CanvasScaler match mode and a root font-scaler
/// so text stays readable and unclipped across different resolutions and
/// aspect ratios (ultrawide, portrait, 4:3, 16:9).
/// Attach to the SchedulingUI Canvas root.
/// </summary>
[RequireComponent(typeof(CanvasScaler))]
public class ResponsiveCanvasFixer : MonoBehaviour
{
    [Header("Aspect Handling")]
    [SerializeField] private float landscapeMatch = 0.5f;   // match both on 16:9
    [SerializeField] private float ultrawideMatch = 1f;     // match height on very wide
    [SerializeField] private float portraitMatch = 0f;      // match width on tall
    [SerializeField] private float ultrawideThreshold = 1.9f; // width/height
    [SerializeField] private float portraitThreshold = 1.2f;

    [Header("Font Scaling")]
    [SerializeField] private bool scaleTexts = true;
    [SerializeField] private float referenceDpi = 96f;
    [SerializeField] private float minScale = 0.7f;
    [SerializeField] private float maxScale = 1.4f;

    private CanvasScaler scaler;
    private Canvas canvas;

    private void Awake()
    {
        scaler = GetComponent<CanvasScaler>();
        canvas = GetComponent<Canvas>();
    }

    private void Start()
    {
        ApplyResponsiveFixes();
    }

    private void Update()
    {
        // Re-evaluate on resolution / aspect change.
        ApplyResponsiveFixes();
    }

    private void ApplyResponsiveFixes()
    {
        if (scaler == null || canvas == null) return;
        if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize) return;

        float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);

        // Choose match mode to avoid clipping on extreme aspects.
        float match;
        if (aspect >= ultrawideThreshold) match = ultrawideMatch;      // ultrawide -> match height
        else if (aspect <= portraitThreshold) match = portraitMatch;   // tall -> match width
        else match = landscapeMatch;

        if (!Mathf.Approximately(scaler.matchWidthOrHeight, match))
        {
            scaler.matchWidthOrHeight = match;
        }

        if (scaleTexts)
        {
            float scale = ComputeFontScale();
            ApplyTextScale(transform, scale);
        }
    }

    private float ComputeFontScale()
    {
        // Scale text based on the smaller screen dimension relative to the reference.
        float refSize = Mathf.Min(1920f, 1080f);
        float curSize = Mathf.Min(Screen.width, Screen.height);
        float s = curSize / refSize;
        // Account for dpi differences.
        s *= (Screen.dpi > 0 ? Screen.dpi : referenceDpi) / referenceDpi;
        return Mathf.Clamp(s, minScale, maxScale);
    }

    private void ApplyTextScale(Transform root, float scale)
    {
        // Adjust TMPro text sizes proportionally, preserving relative hierarchy.
        var tmpTexts = root.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true);
        foreach (var tmp in tmpTexts)
        {
            if (tmp == null) continue;
            // Only scale if it uses auto-sizing; otherwise leave authored sizes.
            if (tmp.enableAutoSizing)
            {
                tmp.fontSizeMin = Mathf.Max(4f, tmp.fontSizeMin * 0.9f);
                tmp.fontSizeMax = Mathf.Max(tmp.fontSizeMax, 60f);
            }
        }
    }
}
