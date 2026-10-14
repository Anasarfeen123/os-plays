using UnityEngine;

namespace Gridlock.Hub
{
    /// <summary>
    /// Breathing glow on an unlit neon renderer, via a property block (no material copies, keeps batching).
    /// <see cref="GlobalIntensity"/> dims every pulse at once, so the room can power down during the launch.
    /// </summary>
    public class HubPulse : MonoBehaviour
    {
        public static float GlobalIntensity = 1f;

        [SerializeField] Color color = Color.cyan;
        [SerializeField] float min = 0.65f;
        [SerializeField] float max = 1f;
        [SerializeField] float frequency = 0.25f;
        [SerializeField] float phase;

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        Renderer target;
        MaterialPropertyBlock block;

        public void Init(Color c, float minimum, float maximum, float freq, float ph)
        {
            color = c;
            min = minimum;
            max = maximum;
            frequency = freq;
            phase = ph;
        }

        void Awake()
        {
            target = GetComponent<Renderer>();
            block = new MaterialPropertyBlock();
            GlobalIntensity = 1f;
        }

        void Update()
        {
            float k = Mathf.Lerp(min, max, 0.5f + 0.5f * Mathf.Sin((Time.time * frequency + phase) * Mathf.PI * 2f)) * GlobalIntensity;
            block.SetColor(BaseColor, new Color(color.r * k, color.g * k, color.b * k, color.a));
            target.SetPropertyBlock(block);
        }
    }
}
