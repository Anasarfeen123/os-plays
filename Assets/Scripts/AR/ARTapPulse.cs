using UnityEngine;

namespace Gridlock.AR
{
    /// <summary>A ring that flashes under whatever the player just tapped in the model, growing and fading out.</summary>
    public class ARTapPulse : MonoBehaviour
    {
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [SerializeField] Renderer ring;
        [SerializeField] float seconds = 0.45f;

        Material material;
        Color colour;
        float started = float.NegativeInfinity;
        float size;

        public void Init(Renderer ringRenderer) => ring = ringRenderer;

        void Awake()
        {
            material = ring.material;
            colour = material.GetColor(BaseColor);
            ring.enabled = false;
        }

        /// <summary>Flash under these world bounds (the tapped collider).</summary>
        public void Play(Bounds bounds)
        {
            transform.position = new Vector3(bounds.center.x, bounds.min.y + 0.02f, bounds.center.z);
            size = Mathf.Max(bounds.size.x, bounds.size.z) * 1.35f;
            started = Time.unscaledTime;
            ring.enabled = true;
        }

        void Update()
        {
            if (!ring.enabled) return;
            float k = (Time.unscaledTime - started) / seconds;
            if (k >= 1f)
            {
                ring.enabled = false;
                return;
            }
            float ease = 1f - (1f - k) * (1f - k);
            transform.localScale = Vector3.one * (size * Mathf.Lerp(0.7f, 1.25f, ease));
            material.SetColor(BaseColor, new Color(colour.r, colour.g, colour.b, colour.a * (1f - k)));
        }
    }
}
