using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Gridlock.Hub
{
    /// <summary>
    /// A working key on the desk keyboard. Press it with a fingertip (hand-tracking poke), a controller tip, a controller
    /// ray and trigger, the mouse, or the matching key on a real keyboard (<see cref="HubDesktopInput"/>). It sinks,
    /// flashes and clicks, then tells the monitor.
    /// </summary>
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class HubKey : MonoBehaviour
    {
        public enum KeyAction { Left, Right, Enter, Slot }

        [SerializeField] KeyAction action;
        [Tooltip("Tile index for Slot keys (0 = key 1).")]
        [SerializeField] int slot;
        [SerializeField] HubScreen screen;
        [SerializeField] HubKeyboard keyboard;
        [SerializeField] Transform cap;
        [SerializeField] Renderer capRenderer;
        [SerializeField] Color color = new Color(0.4f, 1.6f, 2.2f);
        [SerializeField] AudioSource source;
        [SerializeField] AudioClip clip;

        const float Travel = 0.006f;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        XRSimpleInteractable interactable;
        MaterialPropertyBlock block;
        Vector3 rest;
        float press;
        float lastPress = -1f;
        int hovers;

        public KeyAction Action => action;
        public int Slot => slot;

        public void SetScreen(HubScreen hubScreen) => screen = hubScreen;

        public void Init(KeyAction keyAction, int slotIndex, HubKeyboard board, Transform keyCap, Renderer keyMesh, Color keyColor,
                         AudioSource audioSource, AudioClip keyClip)
        {
            action = keyAction;
            slot = slotIndex;
            keyboard = board;
            cap = keyCap;
            capRenderer = keyMesh;
            color = keyColor;
            source = audioSource;
            clip = keyClip;
        }

        void Awake()
        {
            interactable = GetComponent<XRSimpleInteractable>();
            interactable.selectEntered.AddListener(OnSelect);
            interactable.hoverEntered.AddListener(OnHoverEnter);
            interactable.hoverExited.AddListener(OnHoverExit);
            block = new MaterialPropertyBlock();
            rest = cap.localPosition;
        }

        void OnDestroy()
        {
            interactable.selectEntered.RemoveListener(OnSelect);
            interactable.hoverEntered.RemoveListener(OnHoverEnter);
            interactable.hoverExited.RemoveListener(OnHoverExit);
        }

        void OnSelect(SelectEnterEventArgs _) => Press();
        void OnHoverEnter(HoverEnterEventArgs _) => hovers++;
        void OnHoverExit(HoverExitEventArgs _) => hovers = Mathf.Max(0, hovers - 1);

        public void Press()
        {
            if (Time.unscaledTime - lastPress < 0.2f) return; // one press per poke, even if the finger jitters
            lastPress = Time.unscaledTime;
            press = 1f;
            source.pitch = Random.Range(0.95f, 1.08f);
            source.PlayOneShot(clip);
            keyboard?.UserTyped();
            screen.KeyPressed(action, slot);
        }

        void Update()
        {
            press = Mathf.MoveTowards(press, 0f, Time.deltaTime * 5f);
            float down = press > 0.6f ? 1f : press / 0.6f; // snap down, ease back up
            cap.localPosition = rest + Vector3.down * (Travel * down);

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 2.5f + slot);
            float glow = Mathf.Max(0.35f + 0.15f * pulse, hovers > 0 ? 0.75f : 0f, press);
            block.SetColor(BaseColor, color * glow);
            capRenderer.SetPropertyBlock(block);
        }
    }
}
