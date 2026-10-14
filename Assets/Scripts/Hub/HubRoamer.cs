using System;
using Gridlock.Core;
using Gridlock.XR;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Gridlock.Hub
{
    /// <summary>
    /// The desktop / phone hub camera. <b>Seated</b> (the start): a slight look-around in the chair while the desk keyboard
    /// picks the scenario. <b>Roaming</b> (R or STAND UP): walk the room with collisions. On desktop: WASD / arrows to walk,
    /// Shift to run, right-drag to look, click the floor to walk there. On a phone: drag to look, tap the floor to walk
    /// there. Clicking or tapping the chair, R, or SIT DOWN glides back into the chair.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class HubRoamer : MonoBehaviour
    {
        [SerializeField] Vector2 seatedRange = new Vector2(12f, 7f);
        [SerializeField] float eyeHeight = 1.62f;
        [SerializeField] float walkSpeed = 2.2f, runSpeed = 4.2f;
        [SerializeField] float lookSpeed = 0.18f;
        [SerializeField] HubDesktopInput deskKeys;
        [Tooltip("Where roaming starts: a step back from the chair.")]
        [SerializeField] Vector3 standUpOffset = new Vector3(0f, 0f, -0.7f);

        CharacterController body;
        Vector3 seatPosition;
        Quaternion seatRotation;
        Vector2 seatedLook;
        float yaw, pitch, fall;
        Vector3? walkTarget;
        float sitBlend = -1f;
        Vector3 sitFromPos;
        Quaternion sitFromRot;

        public bool Roaming { get; private set; }
        public event Action<bool> RoamingChanged;

        public void Init(HubDesktopInput keys) => deskKeys = keys;

        void Awake()
        {
            body = GetComponent<CharacterController>();
            body.enabled = false;
            seatPosition = transform.position;
            seatRotation = transform.rotation;
        }

        public void ToggleRoaming()
        {
            if (Roaming) Sit();
            else StandUp();
        }

        public void StandUp()
        {
            if (Roaming) return;
            Roaming = true;
            sitBlend = -1f;
            var flat = Quaternion.Euler(0f, seatRotation.eulerAngles.y, 0f);
            var feet = new Vector3(seatPosition.x, 0f, seatPosition.z) + flat * standUpOffset;
            transform.SetPositionAndRotation(feet + Vector3.up * eyeHeight, flat);
            yaw = flat.eulerAngles.y;
            pitch = 8f;
            fall = 0f;
            body.enabled = true;
            if (deskKeys != null) deskKeys.enabled = false; // WASD / arrows walk now
            RoamingChanged?.Invoke(true);
        }

        /// <summary>Turn the roaming view (degrees; yaw 0 faces the window).</summary>
        public void LookAt(float yawDegrees, float pitchDegrees)
        {
            yaw = yawDegrees;
            pitch = Mathf.Clamp(pitchDegrees, -70f, 70f);
            if (Roaming) transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        /// <summary>The launch warp takes over the camera: stop walking and let it move freely.</summary>
        public void Freeze()
        {
            walkTarget = null;
            sitBlend = -1f;
            body.enabled = false;
            enabled = false;
        }

        /// <summary>Walk to a point on the floor (click / tap to walk); stands up first if needed.</summary>
        public void WalkTo(Vector3 point)
        {
            StandUp();
            walkTarget = point;
        }

        public void Sit()
        {
            if (!Roaming) return;
            Roaming = false;
            walkTarget = null;
            body.enabled = false;
            sitFromPos = transform.position;
            sitFromRot = transform.rotation;
            sitBlend = 0f;
            seatedLook = Vector2.zero;
            if (deskKeys != null) deskKeys.enabled = true;
            RoamingChanged?.Invoke(false);
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame) ToggleRoaming();

            if (sitBlend >= 0f) GlideIntoChair();
            else if (Roaming) Roam(keyboard);
            else SeatedLook();
        }

        void GlideIntoChair()
        {
            sitBlend += Time.unscaledDeltaTime / 0.8f;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(sitBlend));
            transform.SetPositionAndRotation(Vector3.Lerp(sitFromPos, seatPosition, k), Quaternion.Slerp(sitFromRot, seatRotation, k));
            if (sitBlend >= 1f) sitBlend = -1f;
        }

        void SeatedLook()
        {
            Vector2 n;
            if (TouchGestures.IsTouchDevice && Touchscreen.current != null)
            {
                seatedLook -= TouchGestures.Drag * (2.5f / Mathf.Max(1, Screen.height)); // drag the view like a photo
                n = seatedLook = Vector2.ClampMagnitude(seatedLook, 1.2f);
                if (TouchGestures.Tapped(out var tap)) TryTap(tap);
            }
            else
            {
                var mouse = Mouse.current;
                if (mouse == null) return;
                var p = mouse.position.ReadValue();
                n = new Vector2(p.x / Mathf.Max(1, Screen.width) * 2f - 1f, p.y / Mathf.Max(1, Screen.height) * 2f - 1f);
                n = Vector2.ClampMagnitude(n, 1.2f);
            }
            var wanted = seatRotation * Quaternion.Euler(-n.y * seatedRange.y, n.x * seatedRange.x, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, wanted, 1f - Mathf.Exp(-4f * Time.deltaTime));
        }

        void Roam(Keyboard keyboard)
        {
            float speed = keyboard != null && keyboard.shiftKey.isPressed ? runSpeed : walkSpeed;
            var input = Vector2.zero;
            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1f;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1f;
            }

            float look = lookSpeed * GameSettings.LookMultiplier;
            if (TouchGestures.IsTouchDevice && Touchscreen.current != null)
            {
                var drag = TouchGestures.Drag * (180f * GameSettings.LookMultiplier / Mathf.Max(1, Screen.height));
                yaw += drag.x;
                pitch -= drag.y * GameSettings.LookInvert;
                if (TouchGestures.Tapped(out var tap)) TryTap(tap);
            }
            else if (Mouse.current != null)
            {
                var mouse = Mouse.current;
                if (mouse.rightButton.isPressed)
                {
                    var d = mouse.delta.ReadValue();
                    yaw += d.x * look;
                    pitch -= d.y * look * GameSettings.LookInvert;
                }
                bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
                if (mouse.leftButton.wasPressedThisFrame && !overUI) TryTap(mouse.position.ReadValue());
            }
            pitch = Mathf.Clamp(pitch, -70f, 70f);
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

            var flat = Quaternion.Euler(0f, yaw, 0f);
            var move = flat * new Vector3(input.x, 0f, input.y);
            if (move.sqrMagnitude > 0.01f) walkTarget = null; // keys take over from click-to-walk
            else if (walkTarget.HasValue)
            {
                var to = walkTarget.Value - transform.position;
                to.y = 0f;
                if (to.magnitude < 0.15f) walkTarget = null;
                else move = to.normalized * Mathf.Min(1f, to.magnitude);
            }

            fall = body.isGrounded ? -1f : fall - 9.81f * Time.deltaTime;
            body.Move((Vector3.ClampMagnitude(move, 1f) * speed + Vector3.up * fall) * Time.deltaTime);

            if (transform.position.y < -3f) Sit(); // fell out of the room: back to the chair
        }

        /// <summary>Tap / click: the chair sits down; a walkable floor walks there; anything else is left to its own handler.</summary>
        void TryTap(Vector2 screen)
        {
            var cam = GetComponent<Camera>();
            if (cam == null || !Physics.Raycast(cam.ScreenPointToRay(screen), out var hit, 40f, Physics.DefaultRaycastLayers,
                                                QueryTriggerInteraction.Collide)) return;
            var key = hit.collider.GetComponentInParent<HubKey>();
            if (key != null)
            {
                if (Roaming && hit.distance < 5f) key.Press(); // seated, the desk input handles key clicks
                return;
            }
            if (hit.collider.GetComponentInParent<HubSeat>() != null)
            {
                Sit();
                return;
            }
            if (!Roaming) return;
            if (hit.collider.GetComponent<HubWalkable>() != null) WalkTo(hit.point);
        }
    }
}
