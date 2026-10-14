using UnityEngine;

namespace Gridlock.Hub
{
    /// <summary>Gentle float up and down around the start position (holograms, drones).</summary>
    public class HubBob : MonoBehaviour
    {
        [SerializeField] float amplitude = 0.03f;
        [SerializeField] float frequency = 0.4f;
        [SerializeField] float phase;

        Vector3 origin;

        public void Init(float amp, float freq, float ph)
        {
            amplitude = amp;
            frequency = freq;
            phase = ph;
        }

        void Start() => origin = transform.localPosition;

        void Update() =>
            transform.localPosition = origin + Vector3.up * (amplitude * Mathf.Sin((Time.time * frequency + phase) * Mathf.PI * 2f));
    }
}
