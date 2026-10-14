using UnityEngine;
using UnityEngine.UI;

namespace Gridlock.Hub
{
    /// <summary>
    /// Phone keypad button: the desk keys are a few millimetres tall on a phone screen, so the HUD repeats them as big
    /// buttons that press the real desk key (which still sinks, glows and clicks).
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class HubTouchKey : MonoBehaviour
    {
        [SerializeField] HubDesktopInput input;
        [SerializeField] HubKey.KeyAction action;
        [SerializeField] int slot;

        public void Init(HubDesktopInput desktopInput, HubKey.KeyAction keyAction, int slotIndex)
        {
            input = desktopInput;
            action = keyAction;
            slot = slotIndex;
        }

        void Awake() => GetComponent<Button>().onClick.AddListener(() => input.Press(action, slot));
    }
}
