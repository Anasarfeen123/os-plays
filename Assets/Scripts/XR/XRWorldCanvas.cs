using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Gridlock.XR
{
    /// <summary>
    /// A full-screen overlay UI from an imported scenario (the CPU lab's simulator window) shown in a headset: there is
    /// no screen to overlay, so it becomes a world panel that opens in front of the player each time it is shown, and the
    /// controller rays and fingers can press its buttons. On a monitor it stays a normal overlay.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class XRWorldCanvas : MonoBehaviour
    {
        [SerializeField] Vector2 size = new Vector2(1920f, 1080f);
        [Tooltip("Panel width in metres.")]
        [SerializeField] float width = 1.6f;
        [SerializeField] float distance = 1.35f;

        bool converted;

        void OnEnable()
        {
            var mode = XRModeController.Current;
            if (mode == null || !mode.XRActive) return;
            if (!converted) Convert();
            PlaceInFront();
        }

        void Convert()
        {
            converted = true;
            var canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            var scaler = GetComponent<CanvasScaler>();
            if (scaler != null) scaler.enabled = false; // world canvases keep their own size
            var rect = (RectTransform)transform;
            rect.sizeDelta = size;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localScale = Vector3.one * (width / size.x);
            if (GetComponent<TrackedDeviceGraphicRaycaster>() == null) gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
        }

        void PlaceInFront()
        {
            var cam = Camera.main;
            if (cam == null) return;
            var forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
            forward.Normalize();
            transform.position = cam.transform.position + forward * distance - Vector3.up * 0.1f;
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
        }
    }
}
