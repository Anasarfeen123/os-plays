using Gridlock.XR;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Gridlock.Hub
{
    /// <summary>
    /// Desktop (and any attached keyboard): ← / A, → / D, 1 2 3 and Enter / Space press the matching desk keys, so
    /// the on-screen keyboard animates too. A left click or a tap on a desk key presses it, and the phone's on-screen
    /// keypad calls <see cref="Press"/>.
    /// </summary>
    public class HubDesktopInput : MonoBehaviour
    {
        [SerializeField] HubKey[] keys;

        public void Init(HubKey[] deskKeys) => keys = deskKeys;

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) Press(HubKey.KeyAction.Left, 0);
                if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) Press(HubKey.KeyAction.Right, 0);
                if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
                    Press(HubKey.KeyAction.Enter, 0);
                if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) Press(HubKey.KeyAction.Slot, 0);
                if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) Press(HubKey.KeyAction.Slot, 1);
                if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) Press(HubKey.KeyAction.Slot, 2);
            }

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) PressAt(mouse.position.ReadValue());
            if (TouchGestures.Tapped(out var tap)) PressAt(tap);
        }

        static void PressAt(Vector2 screen)
        {
            var cam = Camera.main;
            if (cam != null &&
                Physics.Raycast(cam.ScreenPointToRay(screen), out var hit, 5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
                hit.collider.GetComponentInParent<HubKey>()?.Press();
        }

        public void Press(HubKey.KeyAction action, int slot)
        {
            foreach (var key in keys)
                if (key.Action == action && (action != HubKey.KeyAction.Slot || key.Slot == slot))
                {
                    key.Press();
                    return;
                }
        }
    }
}
