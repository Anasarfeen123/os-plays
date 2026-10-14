using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace Gridlock.XR
{
    /// <summary>
    /// Face-button shortcuts on Meta Quest Touch controllers, wired per scene by the scene builder:
    /// A / B (right), X / Y (left) and the left menu button. X can require a hold so a restart is never an accident.
    /// The triggers, grips and thumbsticks stay with the XRI rig (select, teleport, turn, move).
    /// </summary>
    public class XRButtonShortcuts : MonoBehaviour
    {
        [SerializeField] UnityEvent buttonA = new UnityEvent();
        [SerializeField] UnityEvent buttonB = new UnityEvent();
        [Tooltip("Fires on X, or after X is held for holdXSeconds when that is above zero.")]
        [SerializeField] UnityEvent buttonX = new UnityEvent();
        [SerializeField] UnityEvent buttonY = new UnityEvent();
        [SerializeField] UnityEvent menuButton = new UnityEvent();
        [SerializeField] float holdXSeconds;

        public UnityEvent ButtonA => buttonA;
        public UnityEvent ButtonB => buttonB;
        public UnityEvent ButtonX => buttonX;
        public UnityEvent ButtonY => buttonY;
        public UnityEvent MenuButton => menuButton;

        public void Init(float xHoldSeconds) => holdXSeconds = xHoldSeconds;

        InputAction a, b, x, y, menu;

        void OnEnable()
        {
            a = Bind("<XRController>{RightHand}/{PrimaryButton}", buttonA);
            b = Bind("<XRController>{RightHand}/{SecondaryButton}", buttonB);
            x = Bind("<XRController>{LeftHand}/{PrimaryButton}", buttonX, holdXSeconds > 0f
                ? $"hold(duration={holdXSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)})"
                : "press");
            y = Bind("<XRController>{LeftHand}/{SecondaryButton}", buttonY);
            menu = Bind("<XRController>{LeftHand}/{MenuButton}", menuButton);
        }

        void OnDisable()
        {
            foreach (var action in new[] { a, b, x, y, menu })
                action?.Dispose();
            a = b = x = y = menu = null;
        }

        static InputAction Bind(string path, UnityEvent target, string interactions = "press")
        {
            var action = new InputAction(type: InputActionType.Button, binding: path, interactions: interactions);
            action.performed += _ => target.Invoke();
            action.Enable();
            return action;
        }
    }
}
