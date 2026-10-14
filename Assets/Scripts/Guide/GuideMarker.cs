using TMPro;
using UnityEngine;

namespace Gridlock.Guide
{
    /// <summary>Bouncing arrow with a label that the tour points at things with ("CAR = PROCESS").</summary>
    public class GuideMarker : MonoBehaviour
    {
        [SerializeField] Transform arrow;
        [SerializeField] TMP_Text label;
        [SerializeField] float bob = 0.35f;

        Transform follow;
        Vector3 offset;
        Vector3 point;

        public void Init(Transform arrowTransform, TMP_Text labelText)
        {
            arrow = arrowTransform;
            label = labelText;
        }

        /// <summary>Point at a fixed spot (the arrow tip lands just above it).</summary>
        public void Point(Vector3 worldPoint, string text)
        {
            follow = null;
            point = worldPoint;
            Show(text);
        }

        /// <summary>Point at something that moves (a car).</summary>
        public void Point(Transform target, Vector3 targetOffset, string text)
        {
            follow = target;
            offset = targetOffset;
            Show(text);
        }

        public void Hide() => gameObject.SetActive(false);

        void Show(string text)
        {
            label.text = text;
            gameObject.SetActive(true);
            LateUpdate();
        }

        void LateUpdate()
        {
            var at = follow != null ? follow.position + offset : point;
            float lift = 0.5f + bob * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f));
            transform.position = at + Vector3.up * lift;

            var cam = Camera.main;
            if (cam == null) return;
            var look = transform.position - cam.transform.position;
            look.y = 0f;
            if (look.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(look);
        }
    }
}
