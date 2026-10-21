using System;
using System.Collections;
using System.Collections.Generic;
using Gridlock.Core;
using Gridlock.XR;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace Gridlock.AR
{
    /// <summary>
    /// Phone AR: find a floor or table, show a placement ring on it, tap to put the scene down as a miniature. Then pinch
    /// to resize, twist two fingers to rotate, drag one finger to slide it across the surface, MOVE to pick it up again,
    /// and tap things in the model to play exactly as on a monitor. The scene itself never moves: the AR session origin is
    /// placed and scaled around it (<see cref="ARPlacementMath"/>), so all simulation positions stay valid.
    /// <para>Rooms open like a dollhouse: the ceiling is left out and the walls on the phone's side hide as it circles the
    /// model. The AR session outlives the scene, so the next scene appears on the same spot without scanning again.</para>
    /// </summary>
    public class ARStage : MonoBehaviour
    {
        enum Stage { Checking, Scanning, Aiming, Placed, Unsupported }

        [Header("AR")]
        [SerializeField] XROrigin origin;
        [SerializeField] ARRaycastManager raycasts;
        [SerializeField] ARPlaneManager planes;
        [SerializeField] Camera arCamera;
        [Header("Scene")]
        [Tooltip("World point of the scene that lands on the tapped spot (the middle of the floor, under the model).")]
        [SerializeField] Transform contentPivot;
        [SerializeField] Transform reticle;
        [SerializeField] GameObject dioramaBase;
        [SerializeField] ARDioramaSettings settings = new ARDioramaSettings();
        [Header("UI")]
        [SerializeField] GameObject ui;
        [SerializeField] TMP_Text hint;
        [SerializeField] TMP_Text scaleLabel;
        [SerializeField] Button moveButton;
        [SerializeField] Button planesButton;
        [SerializeField] TMP_Text planesLabel;
        [Tooltip("The model's own help line, used when the scene's HUD has none.")]
        [SerializeField] TMP_Text helpLine;
        [SerializeField] ARTapPulse tapPulse;
        [SerializeField] ARInfoCard infoCard;
        [Tooltip("HUD parts that only make sense once the model is down (the hub's keypad).")]
        [SerializeField] GameObject[] hideUntilPlaced = new GameObject[0];

        /// <summary>
        /// Taps on things outside GRIDLOCK's assembly (the imported scenarios' [E] objects): each handler gets the hit
        /// collider and returns true if it used the tap.
        /// </summary>
        public static readonly List<Func<Collider, bool>> TapHandlers = new List<Func<Collider, bool>>();

        /// <summary>
        /// What a tapped thing is and how it stands, for the info card: each provider returns null for colliders that are
        /// not its own (the scenarios register theirs).
        /// </summary>
        public static readonly List<Func<Collider, (string title, string body)?>> InfoProviders =
            new List<Func<Collider, (string title, string body)?>>();

        /// <summary>The current scene's stage while AR runs (null otherwise).</summary>
        public static ARStage Current { get; private set; }

        // Kept across scenes: one AR session, and where the last model stood.
        static ARSession sharedSession;
        static bool hasSpot;
        static Vector3 savedAnchor;
        static float savedYaw;

        static readonly List<ARRaycastHit> hits = new List<ARRaycastHit>();
        static readonly RaycastHit[] tapHits = new RaycastHit[16];

        readonly List<(Renderer renderer, int sides)> walls = new List<(Renderer, int)>();
        Stage stage = Stage.Checking;
        Vector3 anchor;       // session-space spot the pivot sits on
        float yaw;            // session yaw of the model's +Z
        float scale;
        bool showPlanes = true;
        bool skipSupportCheck;
        float placedAt = float.NegativeInfinity;
        GameObject scanPrompt;

        /// <summary>Seconds the model takes to grow into place.</summary>
        const float PopSeconds = 0.5f;

        // Hold to focus: the model grows around the held spot; holding again shows it all.
        const float HoldSeconds = 0.5f, FocusZoom = 2.5f, FocusSeconds = 0.5f;
        bool focused;
        float focusAmount;            // 0 = whole model, 1 = zoomed in on the spot
        Vector3 focusPivot;           // the spot (at floor height) that sits on the anchor when zoomed in
        float pressStarted;
        bool held;                    // this touch already acted as a hold

        // gesture state
        bool dragging;
        Vector2 dragStart;
        float lastTwist;
        float lastSpread;

        public bool Placed => stage == Stage.Placed;
        public float Scale => scale;
        public ARDioramaSettings Settings => settings;

        public void Init(XROrigin xrOrigin, ARRaycastManager raycastManager, ARPlaneManager planeManager, Camera cam, Transform pivot,
                         Transform placementReticle, GameObject basePlate, GameObject overlay, TMP_Text hintText, TMP_Text scaleText,
                         Button move, Button planesToggle, TMP_Text planesText, TMP_Text help, ARDioramaSettings diorama)
        {
            origin = xrOrigin;
            raycasts = raycastManager;
            planes = planeManager;
            arCamera = cam;
            contentPivot = pivot;
            reticle = placementReticle;
            dioramaBase = basePlate;
            ui = overlay;
            hint = hintText;
            scaleLabel = scaleText;
            moveButton = move;
            planesButton = planesToggle;
            planesLabel = planesText;
            helpLine = help;
            settings = diorama;
        }

        public void SetHideUntilPlaced(params GameObject[] parts) => hideUntilPlaced = parts;

        public void SetInfoCard(ARInfoCard card) => infoCard = card;

        /// <summary>The info card for the tapped thing, if any provider knows it; otherwise the card goes away.</summary>
        void ShowInfo(Collider col)
        {
            if (infoCard == null) return;
            foreach (var provider in InfoProviders)
            {
                var info = provider(col);
                if (!info.HasValue) continue;
                infoCard.Show(info.Value.title, info.Value.body);
                return;
            }
            infoCard.Hide();
        }

        void ShowPlacedOnly(bool show)
        {
            foreach (var part in hideUntilPlaced)
                if (part != null) part.SetActive(show);
        }

        /// <summary>The ring that flashes under a tapped thing.</summary>
        public void SetTapPulse(ARTapPulse pulse) => tapPulse = pulse;

        public bool Focused => focused;

        /// <summary>Forget the shared session and the last spot (tests start each scene fresh).</summary>
        public static void ForgetPlacement()
        {
            hasSpot = false;
            if (sharedSession != null) Destroy(sharedSession.gameObject);
            sharedSession = null;
        }

        /// <summary>Called by <see cref="XRModeController"/> when this phone runs the scene in AR.</summary>
        public void Begin(bool trustSupport)
        {
            Current = this;
            skipSupportCheck = trustSupport;
            scale = settings.scale;
            ShareSession();
            // The phone's pose is real-world: no seated-VR height offset between the camera and the tracked planes.
            origin.CameraYOffset = 0f;
            if (origin.CameraFloorOffsetObject != null) origin.CameraFloorOffsetObject.transform.localPosition = Vector3.zero;
            ScenePlacement.Ready = false;
            EnhancedTouchSupport.Enable();
            BuildMiniature();
            RenderSettings.fog = false;
            dioramaBase.SetActive(true);
            Park(); // the phone would start inside the life-size scene: keep it out of sight until the model is placed
            UseARHelpText();
            ui.SetActive(true);
            moveButton.onClick.AddListener(PickUp);
            planesButton.onClick.AddListener(TogglePlanes);
            reticle.gameObject.SetActive(false);
            moveButton.gameObject.SetActive(false);
            planesButton.gameObject.SetActive(false); // the grid shows while scanning; the toggle is for once placed
            ShowPlacedOnly(false);
            var scan = ui.transform.Find("Scan");
            scanPrompt = scan != null ? scan.gameObject : null;
            StartCoroutine(CheckSupport());
            StartCoroutine(WireWorldUI());
        }

        /// <summary>
        /// Screens in the model with buttons take finger taps through the phone camera: each world canvas with a control
        /// gets a touch raycaster and the AR camera as its event camera. Repeated, since scenes build screens at runtime.
        /// </summary>
        IEnumerator WireWorldUI()
        {
            var wait = new WaitForSecondsRealtime(1f);
            var controls = new List<Selectable>();
            while (true)
            {
                foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                {
                    // Nested canvases keep their own list of graphics, so each one with controls needs its own raycaster.
                    if (canvas.rootCanvas.renderMode != RenderMode.WorldSpace || canvas.transform.IsChildOf(transform.parent)) continue;
                    canvas.GetComponentsInChildren(true, controls);
                    if (controls.Count == 0) continue;
                    canvas.worldCamera = arCamera;
                    if (!canvas.TryGetComponent<GraphicRaycaster>(out _)) canvas.gameObject.AddComponent<GraphicRaycaster>();
                }
                yield return wait;
            }
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        /// <summary>
        /// Before a scene's AR rig wakes up: if an earlier scene's session is still running, this scene's own one goes before
        /// it can start (and, on its way out, stop) ARCore, so tracking and the found surfaces carry over.
        /// </summary>
        public static void PrepareRig(GameObject rig)
        {
            if (sharedSession == null) return;
            foreach (var session in rig.GetComponentsInChildren<ARSession>(true))
            {
                if (session == sharedSession) continue;
                session.gameObject.SetActive(false);
                Destroy(session.gameObject);
            }
        }

        /// <summary>The first AR scene's session lives on (with ARCore's map of the room) for the scenes after it.</summary>
        void ShareSession()
        {
            if (sharedSession != null) return;
            var own = transform.parent != null ? transform.parent.GetComponentInChildren<ARSession>(true) : null;
            if (own == null) return;
            sharedSession = own;
            own.transform.SetParent(null, true);
            DontDestroyOnLoad(own.gameObject);
        }

        IEnumerator CheckSupport()
        {
            Hint("Starting AR…");
            if (!skipSupportCheck)
            {
                yield return ARSession.CheckAvailability();
                if (ARSession.state == ARSessionState.NeedsInstall) yield return ARSession.Install();
                if (ARSession.state is ARSessionState.Unsupported or ARSessionState.NeedsInstall)
                {
                    stage = Stage.Unsupported;
                    FindFirstObjectByType<XRModeController>()?.FallBackFromAR();
                    yield break;
                }
            }
            stage = Stage.Scanning;
            if (hasSpot) PlaceAt(savedAnchor, savedYaw); // where the previous scene's model stood
        }

        void Update()
        {
            switch (stage)
            {
                case Stage.Scanning:
                case Stage.Aiming:
                    Aim();
                    break;
                case Stage.Placed:
                    Gestures();
                    bool popping = Time.unscaledTime - placedAt < PopSeconds + 0.1f; // still growing into place
                    float focusTarget = focused ? 1f : 0f;
                    bool focusing = !Mathf.Approximately(focusAmount, focusTarget);
                    focusAmount = Mathf.MoveTowards(focusAmount, focusTarget, Time.unscaledDeltaTime / FocusSeconds);
                    if (popping || focusing) Apply();
                    break;
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        // Finding a surface and placing
        // ---------------------------------------------------------------------------------------------------------

        void Aim()
        {
            var centre = new Vector2(Screen.width / 2f, Screen.height * 0.45f);
            bool found = raycasts != null && raycasts.Raycast(centre, hits, TrackableType.PlaneWithinPolygon);
            stage = found ? Stage.Aiming : Stage.Scanning;
            reticle.gameObject.SetActive(found);
            if (scanPrompt != null) scanPrompt.SetActive(!found);

            if (!found)
            {
                Hint("Slowly move your phone to find the floor or a table");
                return;
            }
            var hit = hits[0];
            reticle.SetPositionAndRotation(hit.pose.position, Quaternion.Euler(0f, arCamera.transform.eulerAngles.y, 0f));
            reticle.localScale = Vector3.one * (0.35f * origin.transform.lossyScale.x); // ~35 cm wide whatever the origin scale
            Hint($"Tap to place the {settings.subject} here");

            if (TappedOutsideUI(out _)) PlaceAt(hit.sessionRelativePose.position);
        }

        /// <summary>Put the scene's pivot on a session-space point, facing the camera (tests call this directly).</summary>
        public void PlaceAt(Vector3 sessionPoint)
        {
            var cameraSession = origin.transform.InverseTransformPoint(arCamera.transform.position);
            PlaceAt(sessionPoint, ARPlacementMath.FacingYaw(sessionPoint, cameraSession));
        }

        void PlaceAt(Vector3 sessionPoint, float sessionYaw)
        {
            anchor = sessionPoint;
            yaw = sessionYaw;
            stage = Stage.Placed;
            reticle.gameObject.SetActive(false);
            if (scanPrompt != null) scanPrompt.SetActive(false);
            moveButton.gameObject.SetActive(true);
            planesButton.gameObject.SetActive(true);
            ShowPlacedOnly(true);
            SetPlanesVisible(false);
            placedAt = Time.unscaledTime;
            focused = false;
            focusAmount = 0f;
            Apply();
            RememberSpot();
            UpdateCutaway(force: true);
            Hint(settings.placedHint);
            ScenePlacement.Ready = true;
            ARHaptics.Success();
        }

        /// <summary>World metres per real metre (bigger = smaller model), kept within the allowed range.</summary>
        public void Rescale(float worldPerMetre)
        {
            scale = Mathf.Clamp(worldPerMetre, settings.scaleRange.x, settings.scaleRange.y);
            if (Placed) Apply();
        }

        /// <summary>Turn the miniature about its pivot.</summary>
        public void Rotate(float degrees)
        {
            yaw += degrees;
            if (!Placed) return;
            Apply();
            RememberSpot();
        }

        void PickUp()
        {
            stage = Stage.Scanning;
            moveButton.gameObject.SetActive(false);
            planesButton.gameObject.SetActive(false);
            ShowPlacedOnly(false);
            SetPlanesVisible(true);
        }

        void Apply()
        {
            // The session origin moves; the pose of anything already tracked is preserved relative to the session.
            // Just placed: the model grows from a third of its size with a little overshoot (an origin scale above the
            // target shows it smaller).
            float k = Mathf.Clamp01((Time.unscaledTime - placedAt) / PopSeconds);
            float grow = 1f + 2.70158f * Mathf.Pow(k - 1f, 3f) + 1.70158f * Mathf.Pow(k - 1f, 2f); // ease-out-back, 0 → 1
            float f = focusAmount * focusAmount * (3f - 2f * focusAmount);
            float shown = scale / Mathf.Lerp(0.35f, 1f, grow) / Mathf.Lerp(1f, FocusZoom, f);
            var pivot = Vector3.Lerp(contentPivot.position, focusPivot, f);
            var (position, rotation) = ARPlacementMath.OriginFor(pivot, anchor, yaw, shown);
            origin.transform.SetPositionAndRotation(position, rotation);
            origin.transform.localScale = Vector3.one * shown;
            if (scaleLabel != null) scaleLabel.text = $"1 : {Mathf.RoundToInt(scale)}";
        }

        /// <summary>The next scene's model goes where this one stands now.</summary>
        void RememberSpot()
        {
            hasSpot = true;
            savedAnchor = anchor;
            savedYaw = yaw;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Gestures once placed
        // ---------------------------------------------------------------------------------------------------------

        void Gestures()
        {
            var touches = Touch.activeTouches;
            if (touches.Count >= 2)
            {
                dragging = false;
                var a = touches[0].screenPosition;
                var b = touches[1].screenPosition;
                float spread = Vector2.Distance(a, b);
                float twist = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
                bool began = touches[0].phase == UnityEngine.InputSystem.TouchPhase.Began || touches[1].phase == UnityEngine.InputSystem.TouchPhase.Began;
                if (!began && lastSpread > 1f)
                {
                    Rescale(scale * lastSpread / Mathf.Max(1f, spread)); // spreading the fingers makes the model bigger
                    Rotate(Mathf.DeltaAngle(twist, lastTwist));
                }
                lastSpread = spread;
                lastTwist = twist;
                return;
            }
            lastSpread = 0f;

            if (touches.Count == 1)
            {
                var t = touches[0];
                if (t.phase == UnityEngine.InputSystem.TouchPhase.Began)
                {
                    dragStart = t.screenPosition;
                    dragging = false;
                    held = false;
                    pressStarted = Time.unscaledTime;
                }
                if (!dragging && Vector2.Distance(t.screenPosition, dragStart) > Screen.height * 0.03f && !OverUI(t.screenPosition)) dragging = true;
                if (!dragging && !held && Time.unscaledTime - pressStarted > HoldSeconds && !OverUI(t.screenPosition))
                {
                    held = true;
                    if (focused) Unfocus();
                    else FocusAt(t.screenPosition);
                }
                if (held) return; // a hold is not also a tap
                if (dragging && raycasts != null && raycasts.Raycast(t.screenPosition, hits, TrackableType.PlaneWithinPolygon))
                {
                    anchor = hits[0].sessionRelativePose.position;
                    Apply();
                    RememberSpot();
                }
                if (t.phase == UnityEngine.InputSystem.TouchPhase.Ended && !dragging && !OverUI(t.screenPosition)) TapAt(t.screenPosition);
                return;
            }

            // Mouse in the editor / desktop testing.
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && !OverUI(mouse.position.ReadValue())) TapAt(mouse.position.ReadValue());
        }

        /// <summary>Zoom in around the spot of the model under this screen point (a hold on the screen).</summary>
        public bool FocusAt(Vector2 screen)
        {
            if (!Placed || !ModelPoint(screen, out var point)) return false;
            focusPivot = new Vector3(point.x, contentPivot.position.y, point.z); // the floor stays on the table
            focused = true;
            Hint("Zoomed in on that spot · hold again to see it all");
            ARHaptics.Success();
            return true;
        }

        /// <summary>Back to the whole model.</summary>
        public void Unfocus()
        {
            if (!focused) return;
            focused = false;
            Hint(settings.placedHint);
            ARHaptics.Tick();
        }

        /// <summary>The nearest visible, solid point of the model under a screen point.</summary>
        bool ModelPoint(Vector2 screen, out Vector3 point)
        {
            int count = Physics.RaycastNonAlloc(arCamera.ScreenPointToRay(screen), tapHits, 5000f, ~0, QueryTriggerInteraction.Ignore);
            Array.Sort(tapHits, 0, count, HitDistance.Instance);
            for (int i = 0; i < count; i++)
            {
                if (!Tappable(tapHits[i].collider)) continue;
                point = tapHits[i].point;
                return true;
            }
            point = default;
            return false;
        }

        /// <summary>
        /// Tap on the miniature: the nearest thing that takes taps gets it (a car or bay, a scenario object, a hub card),
        /// exactly like a click on the monitor. Hidden walls and the base never catch it.
        /// </summary>
        public bool TapAt(Vector2 screen)
        {
            int count = Physics.RaycastNonAlloc(arCamera.ScreenPointToRay(screen), tapHits, 5000f, ~0, QueryTriggerInteraction.Collide);
            Array.Sort(tapHits, 0, count, HitDistance.Instance);
            for (int i = 0; i < count; i++)
            {
                var col = tapHits[i].collider;
                // Trigger volumes (a headset's drop zones) are invisible: only ones made for AR taps take them.
                if (col.isTrigger && col.GetComponentInParent<IARTappable>() == null) continue;
                if (!Tappable(col)) continue;
                bool used = Use(col);
                ShowInfo(col); // after the tap acted: the card tells the new state (a box just picked up)
                if (used)
                {
                    ARHaptics.Tick();
                    if (tapPulse != null) tapPulse.Play(col.bounds);
                    return true;
                }
                if (!col.isTrigger) return false; // something solid in front of anything else
            }
            return false;
        }

        bool Tappable(Collider col)
        {
            // A wall cut away for the view, the ceiling, or anything outside the model cannot block a tap behind it.
            var r = col.GetComponent<Renderer>();
            if (r == null) r = col.GetComponentInParent<Renderer>();
            if (r != null && hiddenSet.Contains(r)) return false;
            return settings.bounds.Contains(col.bounds.center) || col.GetComponentInParent<IARTappable>() != null;
        }

        static bool Use(Collider col)
        {
            var tappable = col.GetComponentInParent<IARTappable>();
            if (tappable != null) { tappable.OnARTap(); return true; }
            var inspectable = col.GetComponentInParent<Inspectable>();
            if (inspectable != null) { inspectable.Click(); return true; }
            foreach (var handler in TapHandlers)
                if (handler(col)) return true;
            return false;
        }

        sealed class HitDistance : IComparer<RaycastHit>
        {
            public static readonly HitDistance Instance = new HitDistance();
            public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
        }

        // ---------------------------------------------------------------------------------------------------------
        // The model: what is shown, the dollhouse cutaway
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Hidden for AR (outside the model, the ceiling) or cut away right now (walls on the phone's side).</summary>
        readonly HashSet<Renderer> hiddenSet = new HashSet<Renderer>();

        /// <summary>
        /// Before placement the session origin sits 20 km away, beyond the camera's far plane: only the camera feed, the
        /// surfaces and the ring show, including anything the scene builds at runtime.
        /// </summary>
        void Park()
        {
            origin.transform.SetPositionAndRotation(contentPivot.position + Vector3.down * 20000f, Quaternion.identity);
            origin.transform.localScale = Vector3.one;
        }

        /// <summary>The bottom help bar talks about touch gestures on the miniature, not mouse orbit.</summary>
        void UseARHelpText()
        {
            bool found = false;
            foreach (var text in FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
            {
                if (text.name != "Help" || text == helpLine) continue;
                var variant = text.GetComponent<Gridlock.UI.TouchVariant>();
                if (variant != null) variant.enabled = false;
                text.text = settings.helpText;
                found = true;
            }
            if (helpLine == null) return;
            helpLine.transform.parent.gameObject.SetActive(!found);
            helpLine.text = settings.helpText;
        }

        void TogglePlanes() => SetPlanesVisible(!showPlanes);

        void SetPlanesVisible(bool visible)
        {
            showPlanes = visible;
            if (planes != null)
                foreach (var plane in planes.trackables)
                    foreach (var r in plane.GetComponentsInChildren<Renderer>()) r.enabled = visible;
            if (planesLabel != null) planesLabel.text = visible ? "HIDE GRID" : "SHOW GRID";
        }

        void LateUpdate()
        {
            if (stage == Stage.Placed) UpdateCutaway(force: false);
            // New planes appear while placed: keep them hidden unless the grid is switched on.
            if (planes == null || showPlanes) return;
            foreach (var plane in planes.trackables)
                foreach (var r in plane.GetComponentsInChildren<Renderer>())
                    if (r.enabled) r.enabled = false;
        }

        int lastFacing = -1;

        /// <summary>Hide the walls on the sides that face the phone, so it looks into the room from any angle.</summary>
        void UpdateCutaway(bool force)
        {
            if (walls.Count == 0) return;
            var toCamera = arCamera.transform.position - settings.bounds.center;
            toCamera.y = 0f;
            int facing = 0;
            if (toCamera.sqrMagnitude > 1e-6f)
            {
                toCamera.Normalize();
                const float threshold = 0.3f; // about 72°: a side counts as facing until the phone is well past its corner
                if (-toCamera.x > threshold) facing |= 1;
                if (toCamera.x > threshold) facing |= 2;
                if (-toCamera.z > threshold) facing |= 4;
                if (toCamera.z > threshold) facing |= 8;
            }
            if (!force && facing == lastFacing) return;
            lastFacing = facing;
            foreach (var (r, sides) in walls)
            {
                if (r == null) continue;
                bool cut = (sides & facing) != 0;
                r.enabled = !cut;
                if (cut) hiddenSet.Add(r); else hiddenSet.Remove(r);
            }
        }

        /// <summary>
        /// The life-size scene runs on far past the model; in AR it ends at its base. Leaves out the ceiling too, and sorts
        /// the walls by the side of the model they stand on.
        /// </summary>
        void BuildMiniature()
        {
            walls.Clear();
            hiddenSet.Clear();
            var box = settings.bounds;
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (r.transform.IsChildOf(transform.parent) || !r.enabled) continue; // the AR rig itself (reticle, planes)
                if (r.GetComponentInParent<Canvas>() != null) continue; // world UI draws through its canvas
                var b = r.bounds;
                // Outside the model, bigger than it (a 200 m ground or a starfield), or the ceiling: not part of it.
                if (!box.Contains(b.center) || b.size.x > box.size.x * 1.05f || b.size.z > box.size.z * 1.05f || b.min.y >= settings.ceilingCut - 0.01f)
                {
                    r.enabled = false;
                    hiddenSet.Add(r);
                    continue;
                }
                if (settings.cutawayBand <= 0f || b.max.y < settings.cutawayMinHeight) continue;
                int sides = 0;
                if (b.center.x - box.min.x < settings.cutawayBand) sides |= 1;
                if (box.max.x - b.center.x < settings.cutawayBand) sides |= 2;
                if (b.center.z - box.min.z < settings.cutawayBand) sides |= 4;
                if (box.max.z - b.center.z < settings.cutawayBand) sides |= 8;
                if (sides != 0) walls.Add((r, sides));
            }
            foreach (var traffic in FindObjectsByType<AmbientTraffic>(FindObjectsSortMode.None)) traffic.gameObject.SetActive(false);
        }

        static bool TappedOutsideUI(out Vector2 position)
        {
            if (TouchGestures.Tapped(out position)) return !OverUI(position);
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                position = mouse.position.ReadValue();
                return !OverUI(position);
            }
            return false;
        }

        /// <summary>A screen-space HUD control, or a world-space UI control in the model, is under this point.</summary>
        static bool OverUI(Vector2 screen)
        {
            if (EventSystem.current == null) return false;
            var data = new PointerEventData(EventSystem.current) { position = screen };
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(data, results);
            foreach (var r in results)
            {
                if (!(r.module is GraphicRaycaster)) continue;
                var canvas = r.gameObject.GetComponentInParent<Canvas>();
                if (canvas == null) continue;
                if (canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay) return true;
                if (r.gameObject.GetComponentInParent<Selectable>() != null) return true; // a button on a screen in the model
            }
            return false;
        }

        void Hint(string text)
        {
            if (hint != null && hint.text != text) hint.text = text;
        }
    }
}
