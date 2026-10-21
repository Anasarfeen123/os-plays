using System;
using Gridlock.UI;
using Gridlock.XR;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem.EnhancedTouch;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// GRIDLOCK glue for the first-person player of the imported scenarios (CPU lab, memory warehouse) on a monitor or phone.
/// <list type="bullet">
/// <item>The GRIDLOCK menu: while it is open the mouse is free and the player stops looking around and clicking.</item>
/// <item>The mouse drives the UI only while the cursor is free; with a locked cursor clicks belong to the crosshair.</item>
/// <item>Phone: a thumbstick bottom-left to walk, drag anywhere else to look, tap something to use it (what [E] does on a
/// keyboard).</item>
/// </list>
/// </summary>
public class ScenarioDesktopControls : MonoBehaviour
{
    [SerializeField] PlayerMovement movement;
    [SerializeField] PlayerInteraction interaction;
    [Tooltip("Degrees of turn per screen height of finger drag.")]
    [SerializeField] float touchLook = 120f;
    [Tooltip("Walking speed per screen height of two-finger drag per second.")]
    [SerializeField] float touchWalk = 9f;
    [SerializeField] Sprite knobSprite;

    /// <summary>The scenario's own full-screen UI is open (it frees the cursor itself; the menu must leave it free).</summary>
    public Func<bool> OwnUIOpen;

    bool menuWasOpen;
    Camera view;
    RectTransform stick, knob;
    int stickFinger = -1;

    bool ar;

    void Start()
    {
        view = movement.GetComponentInChildren<Camera>(true);
        var mode = XRModeController.Current;
        ar = mode != null && mode.ARActive;
        if (ar)
        {
            UseARControls();
            return;
        }
        if (TouchGestures.IsTouchDevice) BuildStick();
    }

    /// <summary>
    /// Phone AR: the scene is a model on the table, so there is no one to walk or look around with. The crosshair HUD goes,
    /// and a tap on the model uses what it hits (what [E] does on a keyboard).
    /// </summary>
    void UseARControls()
    {
        movement.enabled = false;
        interaction.enabled = false;
        foreach (var canvas in movement.GetComponentsInChildren<Canvas>(true))
            if (canvas.isRootCanvas && canvas.name is "PlayerUI" or "PlayerHUD_Canvas") canvas.gameObject.SetActive(false);
        Gridlock.AR.ARStage.TapHandlers.Add(UseTapped);
    }

    static bool UseTapped(Collider hit)
    {
        var target = hit.GetComponentInParent<IInteractable>();
        if (target == null) return false;
        target.Interact();
        return true;
    }

    void OnDestroy() => Gridlock.AR.ARStage.TapHandlers.Remove(UseTapped);

    /// <summary>Base and knob, bottom-left. The base catches UI raycasts, so touches on it never turn the view.</summary>
    void BuildStick()
    {
        var canvas = new GameObject("Thumbstick", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas.transform.SetParent(movement.transform, false);
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 1f;
        RectTransform Disc(string name, Transform parent, float size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = new Vector2(size, size);
            var image = go.GetComponent<Image>();
            image.sprite = knobSprite;
            image.color = color;
            return rt;
        }
        stick = Disc("Base", canvas.transform, 260f, new Color(1f, 1f, 1f, 0.16f));
        stick.anchorMin = stick.anchorMax = stick.pivot = new Vector2(0f, 0f);
        stick.anchoredPosition = new Vector2(70f, 70f);
        knob = Disc("Knob", stick, 110f, new Color(1f, 1f, 1f, 0.55f));
        knob.GetComponent<Image>().raycastTarget = false;
    }

    Vector2 StickInput()
    {
        if (stick == null) return Vector2.zero;
        if (!EnhancedTouchSupport.enabled) EnhancedTouchSupport.Enable();
        Vector2 input = Vector2.zero;
        bool held = false;
        foreach (var touch in ETouch.activeTouches)
        {
            if (stickFinger < 0 && touch.began && RectTransformUtility.RectangleContainsScreenPoint(stick, touch.screenPosition))
                stickFinger = touch.touchId;
            if (touch.touchId != stickFinger) continue;
            if (touch.phase is UnityEngine.InputSystem.TouchPhase.Ended or UnityEngine.InputSystem.TouchPhase.Canceled) break;
            held = true;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(stick, touch.screenPosition, null, out var local);
            input = Vector2.ClampMagnitude((local - stick.rect.center) / (stick.rect.width * 0.5f), 1f);
        }
        if (!held) stickFinger = -1;
        knob.anchoredPosition = input * stick.rect.width * 0.4f;
        return input;
    }

    void Update()
    {
        if (ar) return;
        bool menuOpen = SystemMenu.Instance != null && SystemMenu.Instance.IsOpen;
        if (menuOpen != menuWasOpen)
        {
            menuWasOpen = menuOpen;
            if (menuOpen) movement.SetControlActive(false);
            else if (OwnUIOpen == null || !OwnUIOpen()) movement.SetControlActive(true);
            if (interaction != null) interaction.enabled = !menuOpen;
        }

        var events = EventSystem.current;
        if (events != null && events.currentInputModule is XRUIInputModule module)
            module.enableMouseInput = Cursor.lockState != CursorLockMode.Locked;

        if (TouchGestures.IsTouchDevice && !menuOpen) Touch();
    }

    void Touch()
    {
        float scale = 1f / Mathf.Max(1, Screen.height);
        var drag = TouchGestures.Drag * scale * touchLook;
        if (drag != Vector2.zero) movement.AddLook(drag.x, drag.y);
        var walk = StickInput();
        if (walk == Vector2.zero) walk = TouchGestures.Pan * scale * touchWalk / Mathf.Max(Time.unscaledDeltaTime, 1e-3f); // two fingers still walk
        movement.ExternalMove = Vector2.ClampMagnitude(walk, 1f);

        if (!TouchGestures.Tapped(out var point) || view == null) return;
        if (Physics.Raycast(view.ScreenPointToRay(point), out var hit, 6f))
        {
            var target = hit.collider.GetComponentInParent<IInteractable>();
            target?.Interact();
        }
    }
}
