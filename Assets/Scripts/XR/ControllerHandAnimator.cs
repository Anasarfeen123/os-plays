using UnityEngine;
using UnityEngine.InputSystem;

namespace Gridlock.XR
{
    /// <summary>
    /// Animated hand on a held controller (the Memory Management scenario's hands, used in every scene): the fingers curl
    /// with the grip and the index finger with the trigger, so the player sees their own hands instead of plastic.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class ControllerHandAnimator : MonoBehaviour
    {
        static readonly int GripId = Animator.StringToHash("Grip");
        static readonly int PinchId = Animator.StringToHash("Pinch");

        [SerializeField] InputActionReference grip;
        [SerializeField] InputActionReference trigger;

        Animator animator;

        public void Init(InputActionReference gripValue, InputActionReference triggerValue)
        {
            grip = gripValue;
            trigger = triggerValue;
        }

        void Awake() => animator = GetComponent<Animator>();

        void Update()
        {
            animator.SetFloat(GripId, Read(grip));
            animator.SetFloat(PinchId, Read(trigger));
        }

        static float Read(InputActionReference reference) =>
            reference != null && reference.action != null && reference.action.enabled ? reference.action.ReadValue<float>() : 0f;
    }
}
