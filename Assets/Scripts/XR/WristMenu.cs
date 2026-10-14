using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace Gridlock.XR
{
    /// <summary>
    /// Small control panel that floats just above the left hand.
    /// Hand tracking: turn the left palm towards your face to show it, then poke its buttons with the right index finger.
    /// Controllers: Y or the menu button toggles it above the left controller; point at it with the right ray and pull the trigger.
    /// </summary>
    public class WristMenu : MonoBehaviour
    {
        [SerializeField] XROrigin origin;
        [SerializeField] Transform leftController;
        [SerializeField] GameObject panel;
        [Tooltip("Palm-to-face alignment needed to show the menu (cosine).")]
        [SerializeField] float showAlignment = 0.72f;
        [Tooltip("Alignment below which a shown menu hides again (cosine).")]
        [SerializeField] float hideAlignment = 0.45f;
        [SerializeField] float showDelay = 0.2f;
        [SerializeField] float followSharpness = 14f;

        static readonly List<XRHandSubsystem> HandSubsystems = new List<XRHandSubsystem>();

        Transform head;
        bool controllerToggled;
        bool palmShown;
        float palmTimer;
        bool snapNext = true;

        Canvas canvas;
        Behaviour[] raycasters;
        bool shown;

        public bool IsShown => shown;

        public void Init(XROrigin xrOrigin, Transform leftControllerTransform, GameObject menuPanel)
        {
            origin = xrOrigin;
            leftController = leftControllerTransform;
            panel = menuPanel;
        }

        void Awake()
        {
            head = origin.Camera != null ? origin.Camera.transform : Camera.main.transform;
            // Hidden by disabling the canvas, not the GameObject, so the panel's components still hear game events.
            canvas = panel.GetComponent<Canvas>();
            raycasters = panel.GetComponents<UnityEngine.EventSystems.BaseRaycaster>();
            SetShown(false);
        }

        /// <summary>Controller toggle (Y / menu button).</summary>
        public void Toggle() => controllerToggled = !controllerToggled;

        public void Hide()
        {
            controllerToggled = false;
            palmShown = false;
        }

        void LateUpdate()
        {
            bool show;
            Vector3 anchor;
            Vector3 lift;

            if (TryGetLeftPalm(out var palm, out var palmNormal))
            {
                controllerToggled = false;
                float alignment = Vector3.Dot(palmNormal, (head.position - palm).normalized);
                if (!palmShown)
                {
                    palmTimer = alignment > showAlignment ? palmTimer + Time.deltaTime : 0f;
                    palmShown = palmTimer >= showDelay;
                }
                else if (alignment < hideAlignment)
                {
                    palmShown = false;
                    palmTimer = 0f;
                }
                show = palmShown;
                anchor = palm + palmNormal * 0.03f;
                lift = Vector3.up * 0.13f;
            }
            else if (leftController != null && leftController.gameObject.activeInHierarchy)
            {
                palmShown = false;
                show = controllerToggled;
                anchor = leftController.position;
                lift = Vector3.up * 0.16f;
            }
            else
            {
                show = false;
                anchor = lift = Vector3.zero;
            }

            if (shown != show)
            {
                SetShown(show);
                snapNext = true;
            }
            if (!show) return;

            // Above the hand, turned to face the eyes, kept upright so text stays level.
            var position = anchor + lift;
            var toPanel = position - head.position;
            toPanel.y *= 0.5f;
            var rotation = Quaternion.LookRotation(toPanel.sqrMagnitude > 1e-4f ? toPanel : head.forward, Vector3.up);
            if (snapNext)
            {
                transform.SetPositionAndRotation(position, rotation);
                snapNext = false;
                return;
            }
            float t = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
            transform.SetPositionAndRotation(Vector3.Lerp(transform.position, position, t), Quaternion.Slerp(transform.rotation, rotation, t));
        }

        void SetShown(bool value)
        {
            shown = value;
            canvas.enabled = value;
            foreach (var r in raycasters) r.enabled = value;
        }

        bool TryGetLeftPalm(out Vector3 position, out Vector3 normal)
        {
            position = normal = Vector3.zero;
            SubsystemManager.GetSubsystems(HandSubsystems);
            foreach (var subsystem in HandSubsystems)
            {
                if (!subsystem.running || !subsystem.leftHand.isTracked) continue;
                if (!subsystem.leftHand.GetJoint(XRHandJointID.Palm).TryGetPose(out var pose)) continue;

                // Joint poses are in session space, the same space as the rig's camera offset.
                var space = origin.CameraFloorOffsetObject != null ? origin.CameraFloorOffsetObject.transform : origin.transform;
                position = space.TransformPoint(pose.position);
                normal = space.TransformDirection(pose.rotation * Vector3.down); // OpenXR palm: +Y is the back of the hand
                return true;
            }
            return false;
        }
    }
}
