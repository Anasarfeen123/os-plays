using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Gridlock.XR
{
    /// <summary>
    /// Holds a controller hand model on the controller's grip pose. The rig's controller transform follows the aim pose
    /// (the ray, at the tip of the controller), which is ahead of and tilted from where the hand really closes around the
    /// handle; OpenXR's grip pose is the centre of that fist (+Z from the little finger to the thumb, palms facing each
    /// other). Without grip data (e.g. a device that only reports one pose) the hand keeps its offset from the controller.
    /// </summary>
    public class ControllerGripPose : MonoBehaviour
    {
        [SerializeField] bool rightHand;
        [Tooltip("The hand model's pose in grip space.")]
        [SerializeField] Vector3 gripPosition;
        [SerializeField] Quaternion gripRotation = Quaternion.identity;

        InputAction position, rotation, tracking;
        Transform space;
        Vector3 fallbackPosition;
        Quaternion fallbackRotation;

        public void Init(bool right, Vector3 positionInGrip, Quaternion rotationInGrip)
        {
            rightHand = right;
            gripPosition = positionInGrip;
            gripRotation = rotationInGrip;
        }

        void Awake()
        {
            fallbackPosition = transform.localPosition;
            fallbackRotation = transform.localRotation;
            var origin = GetComponentInParent<XROrigin>(true);
            space = origin != null && origin.CameraFloorOffsetObject != null ? origin.CameraFloorOffsetObject.transform
                  : origin != null ? origin.transform : null;
        }

        void OnEnable()
        {
            string hand = rightHand ? "{RightHand}" : "{LeftHand}";
            position = new InputAction(type: InputActionType.Value, binding: $"<XRController>{hand}/devicePosition", expectedControlType: "Vector3");
            rotation = new InputAction(type: InputActionType.Value, binding: $"<XRController>{hand}/deviceRotation", expectedControlType: "Quaternion");
            tracking = new InputAction(type: InputActionType.Button, binding: $"<XRController>{hand}/isTracked");
            position.Enable();
            rotation.Enable();
            tracking.Enable();
            Application.onBeforeRender += Apply;
        }

        void OnDisable()
        {
            Application.onBeforeRender -= Apply;
            position?.Dispose();
            rotation?.Dispose();
            tracking?.Dispose();
            position = rotation = tracking = null;
        }

        void Update() => Apply();

        void Apply()
        {
            if (position == null || space == null) return;
            var r = rotation.ReadValue<Quaternion>();
            // Device controls can exist while the controller is off / outside tracking volume, in which case their pose
            // is the default origin. Read the explicit tracking flag before applying a grip-space hand pose.
            bool tracked = tracking != null && tracking.ReadValue<float>() > 0.5f && Quaternion.Dot(r, r) > 0.5f;
            if (!tracked)
            {
                transform.SetLocalPositionAndRotation(fallbackPosition, fallbackRotation);
                return;
            }
            var gripPositionWorld = space.TransformPoint(position.ReadValue<Vector3>());
            var gripRotationWorld = space.rotation * r;
            transform.SetPositionAndRotation(gripPositionWorld + gripRotationWorld * gripPosition, gripRotationWorld * gripRotation);
        }
    }
}
