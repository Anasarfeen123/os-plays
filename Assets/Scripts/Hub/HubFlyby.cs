using UnityEngine;

namespace Gridlock.Hub
{
    /// <summary>A ship that crosses the window now and then, from <c>from</c> to <c>to</c>, then waits.</summary>
    public class HubFlyby : MonoBehaviour
    {
        [SerializeField] Vector3 from;
        [SerializeField] Vector3 to;
        [SerializeField] float travelSeconds = 14f;
        [SerializeField] Vector2 waitRange = new Vector2(6f, 16f);
        [SerializeField] float startDelay = 3f;

        float clock;
        float wait;

        public void Init(Vector3 start, Vector3 end, float seconds, float delay)
        {
            from = start;
            to = end;
            travelSeconds = seconds;
            startDelay = delay;
        }

        void Start()
        {
            clock = -startDelay;
            transform.SetPositionAndRotation(from, Quaternion.LookRotation(to - from));
        }

        void Update()
        {
            clock += Time.deltaTime;
            if (clock < 0f) return;
            if (clock <= travelSeconds)
            {
                float k = clock / travelSeconds;
                transform.position = Vector3.Lerp(from, to, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 2f;
                return;
            }
            if (wait <= 0f) wait = Random.Range(waitRange.x, waitRange.y);
            if (clock > travelSeconds + wait)
            {
                clock = 0f;
                wait = 0f;
                (from, to) = (to, from); // come back the other way next time
                transform.rotation = Quaternion.LookRotation(to - from);
            }
        }
    }
}
