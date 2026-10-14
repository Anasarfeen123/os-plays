using UnityEngine;

namespace Gridlock.Hub
{
    /// <summary>A scanline band that sweeps down the monitor glass and loops.</summary>
    public class HubScanline : MonoBehaviour
    {
        [SerializeField] float top = 0.5f;
        [SerializeField] float bottom = -0.5f;
        [SerializeField] float seconds = 3.5f;

        public void Init(float high, float low, float duration)
        {
            top = high;
            bottom = low;
            seconds = duration;
        }

        void Update()
        {
            var p = transform.localPosition;
            p.y = Mathf.Lerp(top, bottom, Mathf.Repeat(Time.time / seconds, 1f));
            transform.localPosition = p;
        }
    }
}
