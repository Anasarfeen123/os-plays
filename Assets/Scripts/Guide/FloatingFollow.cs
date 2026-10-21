using UnityEngine;

namespace Gridlock.Guide
{
    /// <summary>
    /// Keeps a world-space screen floating in front of the player's head without glueing it to the view: it stays put
    /// while the player looks around and only glides back once it drifts more than <see cref="maxAngle"/> out of view
    /// (lazy follow, comfortable in a headset).
    /// </summary>
    public class FloatingFollow : MonoBehaviour
    {
        [SerializeField] float distance = 1.5f;
        [Tooltip("Metres to the right of the view direction (negative = left).")]
        [SerializeField] float side = -0.3f;
        [Tooltip("Metres relative to eye height.")]
        [SerializeField] float height = -0.12f;
        [SerializeField] float maxAngle = 32f;
        [SerializeField] float speed = 2.5f;

        bool placed;
        bool moving;
        Vector3 baseScale;

        void Awake() => baseScale = transform.localScale;

        void OnEnable() => placed = false;

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;

            // Mixed reality scales the world to the model: the panel keeps its real distance and size.
            float k = Gridlock.XR.QuestMixedReality.WorldScale;
            transform.localScale = baseScale * k;
            var head = cam.transform.position;
            var forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.ProjectOnPlane(cam.transform.up, Vector3.up);
            forward.Normalize();
            var right = Vector3.Cross(Vector3.up, forward);
            var desired = head + (forward * distance + right * side + Vector3.up * height) * k;

            if (!placed)
            {
                transform.position = desired;
                placed = true;
            }

            var toPanel = Vector3.ProjectOnPlane(transform.position - head, Vector3.up);
            float off = Vector3.Angle(forward, toPanel);
            float range = Mathf.Abs(toPanel.magnitude - distance * k);
            if (off > maxAngle || range > 0.6f * k) moving = true;
            if (moving)
            {
                transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime));
                if (Vector3.Distance(transform.position, desired) < 0.03f * k) moving = false;
            }

            var look = transform.position - head;
            look.y *= 0.5f; // slight tilt towards the eyes, never edge-on
            if (look.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(look);
        }
    }
}
