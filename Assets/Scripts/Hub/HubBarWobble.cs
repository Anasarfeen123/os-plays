using UnityEngine;

namespace Gridlock.Hub
{
    /// <summary>A usage bar on a decor monitor that drifts like live load.</summary>
    public class HubBarWobble : MonoBehaviour
    {
        [SerializeField] float frequency = 0.3f;
        [SerializeField] float phase;

        RectTransform rect;
        float baseWidth;

        public void Init(float freq, float ph)
        {
            frequency = freq;
            phase = ph;
        }

        void Awake()
        {
            rect = (RectTransform)transform;
            baseWidth = rect.sizeDelta.x;
        }

        void Update()
        {
            float k = 0.75f + 0.25f * Mathf.Sin((Time.time * frequency + phase) * Mathf.PI * 2f) + 0.05f * Mathf.Sin(Time.time * 7.3f + phase);
            rect.sizeDelta = new Vector2(baseWidth * k, rect.sizeDelta.y);
        }
    }
}
