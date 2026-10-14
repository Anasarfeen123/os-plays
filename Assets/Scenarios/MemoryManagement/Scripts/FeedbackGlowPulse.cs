
using UnityEngine;
using UnityEngine.UI;

public class FeedbackGlowPulse : MonoBehaviour
{
    [SerializeField] private Image glowImage;

    [Header("Glow Alpha")]
    [SerializeField, Range(0f, 1f)] private float minAlpha = 0.12f;
    [SerializeField, Range(0f, 1f)] private float maxAlpha = 0.45f;

    [Header("Pulse Speed")]
    [SerializeField, Min(0.1f)] private float pulseSpeed = 1.5f;

    private Color glowColor;

    private void Awake()
    {
        if (glowImage == null)
            glowImage = GetComponent<Image>();

        if (glowImage != null)
            glowColor = glowImage.color;
    }

    private void Update()
    {
        if (glowImage == null)
            return;

        float pulse = (Mathf.Sin(Time.unscaledTime * pulseSpeed) + 1f) * 0.5f;
        float alpha = Mathf.Lerp(minAlpha, maxAlpha, pulse);

        glowImage.color = new Color(
            glowColor.r,
            glowColor.g,
            glowColor.b,
            alpha
        );
    }
}
