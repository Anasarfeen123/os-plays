using Gridlock.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Gridlock.XR
{
    /// <summary>
    /// Desktop (non-headset) camera: orbit with right mouse drag, zoom with the wheel, hover cars/spaces with the
    /// mouse to inspect them and left-click to select them, the same way the XR ray does. On a phone: drag to orbit,
    /// pinch to zoom, two fingers to pan, and a tap inspects and selects.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class DesktopCameraController : MonoBehaviour
    {
        [SerializeField] Vector3 target = new Vector3(5f, 1f, 3.5f);
        [SerializeField] float distance = 37f;
        [SerializeField] float yaw;
        [SerializeField] float pitch = 38f;
        [SerializeField] Vector2 distanceRange = new Vector2(8f, 55f);
        [SerializeField] Vector2 pitchRange = new Vector2(10f, 85f);
        [SerializeField] float orbitSpeed = 0.25f;
        [Tooltip("Degrees per screen height of finger drag.")]
        [SerializeField] float touchOrbit = 180f;
        [Tooltip("How far two-finger panning may move the focus from where it starts.")]
        [SerializeField] float panRange = 18f;

        [Tooltip("Shift the focus right by this fraction of the distance so the lot clears the docked HUD.")]
        [SerializeField] float hudOffset = 0.135f;

        Camera cam;
        Inspectable hovered;
        Vector3 home;
        float homeDistance, homePitch, homeYaw;

        // Camera move requested by the guided tour.
        bool gliding;
        float glideTime, glideSeconds;
        Vector3 fromTarget, toTarget;
        float fromDistance, toDistance, fromPitch, toPitch, fromYaw, toYaw;

        /// <summary>Glide to look at <paramref name="point"/> (the guided tour). The learner can keep orbiting afterwards.</summary>
        public void FocusOn(Vector3 point, float dist, float pitchDegrees, float seconds)
        {
            var right = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
            StartGlide(point + right * dist * hudOffset, dist, pitchDegrees, yaw, seconds);
        }

        /// <summary>Glide back to the starting view.</summary>
        public void FocusHome(float seconds) => StartGlide(home, homeDistance, homePitch, homeYaw, seconds);

        void StartGlide(Vector3 t, float d, float p, float y, float seconds)
        {
            fromTarget = target; fromDistance = distance; fromPitch = pitch; fromYaw = yaw;
            toTarget = t; toDistance = Mathf.Clamp(d, distanceRange.x, distanceRange.y);
            toPitch = Mathf.Clamp(p, pitchRange.x, pitchRange.y); toYaw = y;
            glideTime = 0f;
            glideSeconds = Mathf.Max(0.01f, seconds);
            gliding = true;
        }

        void Glide()
        {
            if (!gliding) return;
            GlideStep();
            Apply(); // here too, because Update returns early when no mouse or touch device exists
        }

        void GlideStep()
        {
            glideTime += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, glideTime / glideSeconds);
            target = Vector3.Lerp(fromTarget, toTarget, k);
            distance = Mathf.Lerp(fromDistance, toDistance, k);
            pitch = Mathf.Lerp(fromPitch, toPitch, k);
            yaw = Mathf.LerpAngle(fromYaw, toYaw, k);
            if (glideTime >= glideSeconds) gliding = false;
        }

        void Awake()
        {
            cam = GetComponent<Camera>();
            home = target;
            homeDistance = distance;
            homePitch = pitch;
            homeYaw = yaw;
            Apply();
        }

        void Update()
        {
            Glide();
            if (TouchGestures.IsTouchDevice && Touchscreen.current != null)
            {
                UpdateTouch();
                return;
            }

            var mouse = Mouse.current;
            if (mouse == null) return;

            if (mouse.rightButton.isPressed)
            {
                var d = mouse.delta.ReadValue();
                float k = orbitSpeed * GameSettings.LookMultiplier;
                yaw += d.x * k;
                pitch = Mathf.Clamp(pitch - d.y * k * GameSettings.LookInvert, pitchRange.x, pitchRange.y);
            }
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
                distance = Mathf.Clamp(distance * (scroll > 0 ? 0.9f : 1.1f), distanceRange.x, distanceRange.y);
            Apply();

            UpdateHover(mouse.position.ReadValue());
            if (mouse.leftButton.wasPressedThisFrame && hovered != null) hovered.Click();
        }

        void UpdateTouch()
        {
            var drag = TouchGestures.Drag * (touchOrbit * GameSettings.LookMultiplier / Mathf.Max(1, Screen.height));
            yaw += drag.x;
            pitch = Mathf.Clamp(pitch - drag.y * GameSettings.LookInvert, pitchRange.x, pitchRange.y);

            float pinch = TouchGestures.Pinch;
            if (!Mathf.Approximately(pinch, 1f))
                distance = Mathf.Clamp(distance / pinch, distanceRange.x, distanceRange.y);

            // Two fingers slide the ground under them, scaled so the lot keeps pace with the fingers at any zoom.
            var pan = TouchGestures.Pan * (distance * 1.2f / Mathf.Max(1, Screen.height));
            if (pan != Vector2.zero)
            {
                var flat = Quaternion.Euler(0f, yaw, 0f);
                target -= flat * new Vector3(pan.x, 0f, pan.y);
                var offset = Vector3.ClampMagnitude(target - home, panRange);
                target = home + offset;
            }
            Apply();

            if (TouchGestures.Tapped(out var tap))
            {
                // TouchGestures already skips HUD taps. The card stays up until the next tap, like hovering.
                UpdateHover(tap, checkUI: false);
                if (hovered != null) hovered.Click();
            }
        }

        void Apply()
        {
            var rot = Quaternion.Euler(pitch, yaw, 0f);
            transform.SetPositionAndRotation(target - rot * Vector3.forward * distance, rot);
        }

        void UpdateHover(Vector2 screen, bool checkUI = true)
        {
            Inspectable hit = null;
            bool overUI = checkUI && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (!overUI && Physics.Raycast(cam.ScreenPointToRay(screen), out var info, 200f))
                hit = info.collider.GetComponentInParent<Inspectable>();

            if (hit == hovered) return;
            if (hovered != null) hovered.SetHovered(false);
            hovered = hit;
            if (hovered != null) hovered.SetHovered(true);
        }
    }
}
