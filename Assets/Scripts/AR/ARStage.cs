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
    /// and tap cars / bays to play exactly as on a monitor. The scene itself never moves: the AR session origin is placed
    /// and scaled around it (<see cref="ARPlacementMath"/>), so all simulation positions stay valid.
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
        [Tooltip("World point of the scene that lands on the tapped spot (the middle of the lot).")]
        [SerializeField] Transform contentPivot;
        [SerializeField] Transform reticle;
        [SerializeField] GameObject dioramaBase;
        [Tooltip("Renderers outside this world-space box are hidden in AR, so the miniature ends cleanly at its base.")]
        [SerializeField] Bounds dioramaBounds = new Bounds(new Vector3(0f, 10f, 4f), new Vector3(66f, 60f, 58f));
        [Header("UI")]
        [SerializeField] GameObject ui;
        [SerializeField] TMP_Text hint;
        [SerializeField] TMP_Text scaleLabel;
        [SerializeField] Button moveButton;
        [SerializeField] Button planesButton;
        [SerializeField] TMP_Text planesLabel;
        [Header("Scale")]
        [Tooltip("World metres per real metre: 60 shows the ~66 m city block about 1.1 m wide (the lot itself ~30 cm).")]
        [SerializeField] float scale = 60f;
        [SerializeField] Vector2 scaleRange = new Vector2(12f, 140f);

        static readonly List<ARRaycastHit> hits = new List<ARRaycastHit>();

        readonly List<Renderer> miniature = new List<Renderer>();
        Stage stage = Stage.Checking;
        Vector3 anchor;       // session-space spot the pivot sits on
        float yaw;            // session yaw of the model's +Z
        bool showPlanes = true;
        bool skipSupportCheck;

        // gesture state
        bool dragging;
        Vector2 dragStart;
        float lastTwist;
        float lastSpread;

        public bool Placed => stage == Stage.Placed;
        public float Scale => scale;

        public void Init(XROrigin xrOrigin, ARRaycastManager raycastManager, ARPlaneManager planeManager, Camera cam, Transform pivot,
                         Transform placementReticle, GameObject basePlate, GameObject overlay, TMP_Text hintText, TMP_Text scaleText,
                         Button move, Button planesToggle, TMP_Text planesText)
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
        }

        /// <summary>Called by <see cref="XRModeController"/> when this phone runs the scene in AR.</summary>
        public void Begin(bool trustSupport)
        {
            skipSupportCheck = trustSupport;
            // The phone's pose is real-world: no seated-VR height offset between the camera and the tracked planes.
            origin.CameraYOffset = 0f;
            if (origin.CameraFloorOffsetObject != null) origin.CameraFloorOffsetObject.transform.localPosition = Vector3.zero;
            ScenePlacement.Ready = false;
            EnhancedTouchSupport.Enable();
            HideOutsideDiorama();
            RenderSettings.fog = false;
            dioramaBase.SetActive(true);
            ShowMiniature(false); // the phone starts inside the life-size scene: show the room until the lot is placed
            UseARHelpText();
            ui.SetActive(true);
            moveButton.onClick.AddListener(PickUp);
            planesButton.onClick.AddListener(TogglePlanes);
            reticle.gameObject.SetActive(false);
            moveButton.gameObject.SetActive(false);
            StartCoroutine(CheckSupport());
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

            if (!found)
            {
                Hint("Slowly move your phone to find the floor or a table");
                return;
            }
            var hit = hits[0];
            reticle.SetPositionAndRotation(hit.pose.position, Quaternion.Euler(0f, arCamera.transform.eulerAngles.y, 0f));
            reticle.localScale = Vector3.one * (0.35f * origin.transform.lossyScale.x); // ~35 cm wide whatever the origin scale
            Hint("Tap to place the parking lot here");

            if (TappedOutsideUI(out _)) PlaceAt(hit.sessionRelativePose.position);
        }

        /// <summary>Put the scene's pivot on a session-space point, facing the camera (tests call this directly).</summary>
        public void PlaceAt(Vector3 sessionPoint)
        {
            anchor = sessionPoint;
            var cameraSession = origin.transform.InverseTransformPoint(arCamera.transform.position);
            yaw = ARPlacementMath.FacingYaw(anchor, cameraSession);
            stage = Stage.Placed;
            ShowMiniature(true);
            reticle.gameObject.SetActive(false);
            moveButton.gameObject.SetActive(true);
            SetPlanesVisible(false);
            Apply();
            Hint("Pinch to resize · twist to rotate · drag to move · tap cars to play");
            ScenePlacement.Ready = true;
        }

        /// <summary>World metres per real metre (bigger = smaller model), kept within the allowed range.</summary>
        public void Rescale(float worldPerMetre)
        {
            scale = Mathf.Clamp(worldPerMetre, scaleRange.x, scaleRange.y);
            if (Placed) Apply();
        }

        /// <summary>Turn the miniature about its pivot.</summary>
        public void Rotate(float degrees)
        {
            yaw += degrees;
            if (Placed) Apply();
        }

        void PickUp()
        {
            stage = Stage.Scanning;
            moveButton.gameObject.SetActive(false);
            SetPlanesVisible(true);
        }

        void Apply()
        {
            // The session origin moves; the pose of anything already tracked is preserved relative to the session.
            var (position, rotation) = ARPlacementMath.OriginFor(contentPivot.position, anchor, yaw, scale);
            origin.transform.SetPositionAndRotation(position, rotation);
            origin.transform.localScale = Vector3.one * scale;
            if (scaleLabel != null) scaleLabel.text = $"1 : {Mathf.RoundToInt(scale)}";
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
                if (t.phase == UnityEngine.InputSystem.TouchPhase.Began) { dragStart = t.screenPosition; dragging = false; }
                if (!dragging && Vector2.Distance(t.screenPosition, dragStart) > Screen.height * 0.03f && !OverUI(t.screenPosition)) dragging = true;
                if (dragging && raycasts != null && raycasts.Raycast(t.screenPosition, hits, TrackableType.PlaneWithinPolygon))
                {
                    anchor = hits[0].sessionRelativePose.position;
                    Apply();
                }
                if (t.phase == UnityEngine.InputSystem.TouchPhase.Ended && !dragging && !OverUI(t.screenPosition)) TapAt(t.screenPosition);
                return;
            }

            // Mouse in the editor / desktop testing.
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && !OverUI(mouse.position.ReadValue())) TapAt(mouse.position.ReadValue());
        }

        /// <summary>Tap on the miniature: select a car or bay, exactly like a click on the monitor.</summary>
        public bool TapAt(Vector2 screen)
        {
            if (!Physics.Raycast(arCamera.ScreenPointToRay(screen), out var hit, 1000f)) return false;
            var target = hit.collider.GetComponentInParent<Inspectable>();
            if (target == null) return false;
            target.Click();
            return true;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------------------------------------------

        void ShowMiniature(bool visible)
        {
            foreach (var r in miniature)
                if (r != null) r.enabled = visible;
        }

        /// <summary>The bottom help bar talks about touch gestures on the miniature, not mouse orbit.</summary>
        void UseARHelpText()
        {
            foreach (var text in FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
            {
                if (text.name != "Help") continue;
                var variant = text.GetComponent<Gridlock.UI.TouchVariant>();
                if (variant != null) variant.enabled = false;
                text.text = "Tap a car, then a bay    Pinch  resize    Twist  rotate    Drag  move";
            }
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
            // New planes appear while placed: keep them hidden unless the grid is switched on.
            if (planes == null || showPlanes) return;
            foreach (var plane in planes.trackables)
                foreach (var r in plane.GetComponentsInChildren<Renderer>())
                    if (r.enabled) r.enabled = false;
        }

        /// <summary>
        /// The life-size scene runs on far past the lot; in AR the miniature ends at its base. Remembers what is left so it
        /// can be shown once placed.
        /// </summary>
        void HideOutsideDiorama()
        {
            miniature.Clear();
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (r.transform.IsChildOf(transform.parent) || !r.enabled) continue; // the AR rig itself (reticle, planes)
                // Outside the block, or bigger than it (the 200 m grass plane): not part of the miniature.
                if (!dioramaBounds.Contains(r.bounds.center) || r.bounds.size.x > dioramaBounds.size.x) r.enabled = false;
                else miniature.Add(r);
            }
            foreach (var r in dioramaBase.GetComponentsInChildren<Renderer>(true)) miniature.Add(r);
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

        static bool OverUI(Vector2 screen)
        {
            if (EventSystem.current == null) return false;
            var data = new PointerEventData(EventSystem.current) { position = screen };
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(data, results);
            foreach (var r in results)
                if (r.module is GraphicRaycaster && r.gameObject.GetComponentInParent<Canvas>().renderMode == RenderMode.ScreenSpaceOverlay) return true;
            return false;
        }

        void Hint(string text)
        {
            if (hint != null && hint.text != text) hint.text = text;
        }
    }
}
