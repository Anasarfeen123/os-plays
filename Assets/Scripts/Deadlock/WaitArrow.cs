using TMPro;
using UnityEngine;

namespace Gridlock.Deadlock
{
    /// <summary>
    /// Arc arrow for one wait-for edge (From → To), drawn above the cars with a label naming the contested resource.
    /// Uses a vertex-coloured line material so one material serves every arrow.
    /// </summary>
    public class WaitArrow : MonoBehaviour
    {
        const int Segments = 24;

        [SerializeField] LineRenderer line;
        [SerializeField] LineRenderer head;
        [SerializeField] TMP_Text label;
        [SerializeField] float arcHeight = 2.5f;
        [SerializeField] float width = 0.14f;
        [SerializeField] float headLength = 0.9f;

        public void Init(LineRenderer lineRenderer, LineRenderer headRenderer, TMP_Text labelText)
        {
            line = lineRenderer;
            head = headRenderer;
            label = labelText;
        }

        void Awake()
        {
            head.widthCurve = new AnimationCurve(new Keyframe(0f, width * 4.5f), new Keyframe(1f, 0f));
        }

        /// <summary>Lay the arrow out between two anchors and colour it.</summary>
        public void Show(Vector3 from, Vector3 to, string resource, Color color)
        {
            gameObject.SetActive(true);

            // Quadratic Bézier, raised in the middle so arrows clear the cars and each other.
            var mid = (from + to) * 0.5f + Vector3.up * (arcHeight + 0.15f * Vector3.Distance(from, to));
            line.positionCount = Segments + 1;
            for (int i = 0; i <= Segments; i++)
            {
                float t = i / (float)Segments * 0.92f; // stop short; the head covers the rest
                line.SetPosition(i, Bezier(from, mid, to, t));
            }
            line.startWidth = line.endWidth = width;
            line.startColor = line.endColor = color;

            var tip = to;
            var back = Bezier(from, mid, to, 1f - headLength / Mathf.Max(1f, ArcLength(from, mid, to)));
            head.positionCount = 2;
            head.SetPosition(0, back);
            head.SetPosition(1, tip);
            head.startColor = head.endColor = color;

            if (label != null)
            {
                label.transform.position = Bezier(from, mid, to, 0.5f) + Vector3.up * 0.45f;
                label.text = $"wants {resource}";
                label.color = color;
            }
        }

        public void Hide() => gameObject.SetActive(false);

        static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * b + t * t * c;
        }

        static float ArcLength(Vector3 a, Vector3 b, Vector3 c)
        {
            float len = 0f;
            var prev = a;
            for (int i = 1; i <= 8; i++)
            {
                var p = Bezier(a, b, c, i / 8f);
                len += Vector3.Distance(prev, p);
                prev = p;
            }
            return len;
        }
    }
}
