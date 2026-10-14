using UnityEngine;
using TMPro;
using System.Collections.Generic;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Handles first-person interaction with IInteractable objects in the 3D lab.
/// Raycasts from the player camera and shows a clean TMP crosshair/prompt HUD overlay.
/// The prompt panel is hidden when not looking at an interactable object.
/// </summary>
public class PlayerInteraction : MonoBehaviour
{
    [Header("Interaction Settings")]
    public float interactionDistance = 4.5f;
    [SerializeField] private Transform cameraTransform;

    [Header("HUD References")]
    [SerializeField] private GameObject promptContainer;
    [SerializeField] private TextMeshProUGUI interactionPromptTMP;
    [SerializeField] private GameObject crosshairObj;

    private IInteractable currentTarget = null;
    private GameObject currentUI = null; // GRIDLOCK: a world-space UI button under the crosshair
    private static readonly List<RaycastResult> uiHits = new List<RaycastResult>();

    void Awake()
    {
        // Clean up any stale PlayerHUD_Canvas or duplicate PromptBG in scene
        var oldCanvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var c in oldCanvases)
        {
            if (c.name == "PlayerHUD_Canvas" && c.gameObject != (promptContainer != null ? promptContainer.transform.root.gameObject : null))
            {
                Destroy(c.gameObject);
            }
        }
    }

    void Start()
    {
        if (cameraTransform == null)
        {
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null) cameraTransform = cam.transform;
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
        }

        EnsureHUD();
    }

    private void EnsureHUD()
    {
        if (promptContainer != null && interactionPromptTMP != null)
        {
            promptContainer.SetActive(false);
            return;
        }

        // Create overlay Canvas
        var canvasObj = new GameObject("PlayerHUD_Canvas");
        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // Crosshair dot
        var dot = new GameObject("Crosshair");
        dot.transform.SetParent(canvasObj.transform, false);
        var img = dot.AddComponent<UnityEngine.UI.Image>();
        img.color = new Color(1f, 1f, 1f, 0.7f);
        var rtDot = dot.GetComponent<RectTransform>();
        rtDot.sizeDelta = new Vector2(6f, 6f);
        rtDot.anchorMin = rtDot.anchorMax = new Vector2(0.5f, 0.5f);
        rtDot.anchoredPosition = Vector2.zero;
        crosshairObj = dot;

        // Prompt Root Container (holds both background and text)
        promptContainer = new GameObject("PromptContainer");
        promptContainer.transform.SetParent(canvasObj.transform, false);
        var rtCont = promptContainer.AddComponent<RectTransform>();
        rtCont.anchorMin = new Vector2(0.5f, 0.42f);
        rtCont.anchorMax = new Vector2(0.5f, 0.42f);
        rtCont.anchoredPosition = new Vector2(0, -35f);
        rtCont.sizeDelta = new Vector2(440f, 40f);

        // Background pill
        var bgImg = promptContainer.AddComponent<UnityEngine.UI.Image>();
        bgImg.color = new Color(0.05f, 0.08f, 0.12f, 0.75f);

        // Prompt text inside container
        var textObj = new GameObject("PromptText");
        textObj.transform.SetParent(promptContainer.transform, false);
        interactionPromptTMP = textObj.AddComponent<TextMeshProUGUI>();
        interactionPromptTMP.fontSize = 15f;
        interactionPromptTMP.alignment = TextAlignmentOptions.Center;
        interactionPromptTMP.color = Color.white;
        interactionPromptTMP.fontStyle = FontStyles.Bold;
        interactionPromptTMP.enableWordWrapping = false;
        var rtText = textObj.GetComponent<RectTransform>();
        rtText.anchorMin = Vector2.zero;
        rtText.anchorMax = Vector2.one;
        rtText.sizeDelta = Vector2.zero;
        rtText.anchoredPosition = Vector2.zero;

        // Start hidden
        promptContainer.SetActive(false);
    }

    void Update()
    {
        UpdateRaycastTarget();

        bool interactPressed = false;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            interactPressed = Keyboard.current.eKey.wasPressedThisFrame;
        }
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            interactPressed = true;
        }
#else
        interactPressed = Input.GetKeyDown(KeyCode.E) || Input.GetMouseButtonDown(0);
#endif

        if (interactPressed && currentUI != null)
        {
            var events = EventSystem.current;
            ExecuteEvents.ExecuteHierarchy(currentUI, new PointerEventData(events), ExecuteEvents.pointerClickHandler);
        }
        else if (interactPressed && currentTarget != null)
        {
            currentTarget.Interact();
        }
    }

    private void UpdateRaycastTarget()
    {
        if (cameraTransform == null) return;

        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
        string prompt = "";

        // GRIDLOCK: world-space UI panels (the memory warehouse's screens) are pressed with the crosshair too, while the
        // cursor is locked; a free cursor clicks them directly.
        currentUI = Cursor.lockState == CursorLockMode.Locked ? UIUnderCrosshair() : null;
        if (currentUI != null)
        {
            currentTarget = null;
            if (promptContainer != null)
            {
                promptContainer.SetActive(true);
                if (interactionPromptTMP != null) interactionPromptTMP.text = "Press [E] or click to select";
            }
            return;
        }

        if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance))
        {
            IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
            if (interactable == null)
                interactable = hit.collider.GetComponent<IInteractable>();

            currentTarget = interactable;

            if (currentTarget != null)
            {
                prompt = !string.IsNullOrEmpty(currentTarget.PromptText)
                    ? currentTarget.PromptText
                    : "Press [E] to Interact";
            }
        }
        else
        {
            currentTarget = null;
        }

        if (promptContainer != null)
        {
            bool hasPrompt = !string.IsNullOrEmpty(prompt);
            promptContainer.SetActive(hasPrompt);
            if (hasPrompt && interactionPromptTMP != null)
            {
                interactionPromptTMP.text = prompt;
            }
        }
    }

    private GameObject UIUnderCrosshair()
    {
        var events = EventSystem.current;
        if (events == null) return null;
        uiHits.Clear();
        events.RaycastAll(new PointerEventData(events) { position = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f) }, uiHits);
        foreach (var hit in uiHits)
        {
            var canvas = hit.gameObject.GetComponentInParent<Canvas>();
            if (canvas == null || canvas.rootCanvas.renderMode != RenderMode.WorldSpace) continue;
            if (hit.distance > interactionDistance * 2f) continue;
            var handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit.gameObject);
            if (handler != null) return handler;
        }
        return null;
    }
}
