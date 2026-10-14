using Gridlock.XR;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Gridlock.Hub
{
    /// <summary>
    /// The desk chair: point at it and select (XR), click or tap it (desktop / phone), or press SIT DOWN to go back to the
    /// desk where the keyboard picks the scenario. In XR the rig moves so the player stands at the desk, facing the monitor.
    /// </summary>
    public class HubSeat : MonoBehaviour
    {
        [SerializeField] XRModeController mode;
        [SerializeField] XROrigin origin;
        [SerializeField] HubRoamer desktop;
        [Tooltip("Where the player's feet go, facing the desk.")]
        [SerializeField] Transform standPoint;

        public void Init(XRModeController xrMode, XROrigin xrOrigin, HubRoamer roamer, Transform stand)
        {
            mode = xrMode;
            origin = xrOrigin;
            desktop = roamer;
            standPoint = stand;
        }

        void Awake()
        {
            if (TryGetComponent<XRSimpleInteractable>(out var interactable))
                interactable.selectEntered.AddListener(_ => Sit());
        }

        public void Sit()
        {
            if (mode != null && mode.XRActive && origin != null)
            {
                // Face the desk first (rotate about the head), then put the head above the stand point.
                float turn = Mathf.DeltaAngle(origin.Camera.transform.eulerAngles.y, standPoint.eulerAngles.y);
                origin.RotateAroundCameraUsingOriginUp(turn);
                var head = origin.Camera.transform.position;
                origin.MoveCameraToWorldLocation(new Vector3(standPoint.position.x, head.y, standPoint.position.z));
            }
            else if (desktop != null) desktop.Sit();
        }
    }
}
