using System.Collections.Generic;
using Gridlock.AR;
using Gridlock.Core;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;

namespace Gridlock.XR
{
    /// <summary>
    /// Settings → MIXED REALITY on a Quest: passthrough shows the real room, and the scene becomes the same tabletop model
    /// as on a phone (<see cref="DioramaView"/>, the scene's <see cref="ARDioramaSettings"/>), standing at table height
    /// in front of the player. Walking, teleporting and turning are off (they would carry the player through the model);
    /// the right stick resizes it, the left stick turns it, clicking the right stick brings it back in front. Floating
    /// panels scale with the model (<see cref="WorldScale"/>), so menus stay at arm's length.
    /// <para>Like phone AR, the scene never moves: the XR origin is placed and scaled around it.</para>
    /// </summary>
    public class QuestMixedReality : MonoBehaviour
    {
        [SerializeField] XROrigin origin;
        [Tooltip("Metres in front of the eyes.")]
        [SerializeField] float distance = 0.75f;
        [Tooltip("Metres below the eyes (about a table for someone standing or sitting).")]
        [SerializeField] float drop = 0.6f;

        /// <summary>World metres per real metre while mixed reality is on (1 otherwise): floating UI scales by it.</summary>
        public static float WorldScale { get; private set; } = 1f;

        /// <summary>Editor tests: mixed reality with the XR Interaction Simulator.</summary>
        public static bool ForceInEditor;

        public bool Active { get; private set; }

        ARStage stage;
        DioramaView view;
        GameObject session;
        Camera eye;
        CameraClearFlags savedClear;
        Color savedBackground;
        bool savedFog;
        Pose savedPose;
        Vector3 savedScale;
        Transform baseParent;
        readonly List<Behaviour> paused = new List<Behaviour>();
        readonly List<(CurveInteractionCaster caster, float distance)> rays = new List<(CurveInteractionCaster, float)>();
        readonly List<(SphereInteractionCaster caster, float radius)> spheres = new List<(SphereInteractionCaster, float)>();

        Vector3 anchor;   // origin-space spot the model's pivot stands on
        float yaw;
        float scale;

        public void Init(XROrigin xrOrigin) => origin = xrOrigin;

        void Start()
        {
            GameSettings.Changed += OnSettingsChanged;
            OnSettingsChanged();
        }

        void OnDestroy()
        {
            GameSettings.Changed -= OnSettingsChanged;
            if (Active) Leave();
        }

        bool Wanted()
        {
            var mode = XRModeController.Current;
            return mode != null && mode.XRActive && GameSettings.IsOn(Setting.MixedReality) &&
                   (QuestPerformance.IsStandaloneHeadset || (Application.isEditor && ForceInEditor)) &&
                   FindAnyObjectByType<ARStage>(FindObjectsInactive.Include) != null;
        }

        void OnSettingsChanged()
        {
            bool want = Wanted();
            if (want && !Active) Enter();
            else if (!want && Active) Leave();
        }

        void Enter()
        {
            stage = FindAnyObjectByType<ARStage>(FindObjectsInactive.Include);
            eye = origin.Camera;

            // Passthrough: an AR session with the camera feature, and a see-through background.
            session = new GameObject("Mixed Reality Session", typeof(ARSession));
            if (!eye.TryGetComponent<ARCameraManager>(out _)) eye.gameObject.AddComponent<ARCameraManager>();
            savedClear = eye.clearFlags;
            savedBackground = eye.backgroundColor;
            eye.clearFlags = CameraClearFlags.SolidColor;
            eye.backgroundColor = new Color(0f, 0f, 0f, 0f);
            savedFog = RenderSettings.fog;
            RenderSettings.fog = false;

            // The scene as a tabletop model, on its base.
            view = new DioramaView(stage.Settings);
            view.Build(origin.transform);
            var plate = stage.DioramaBase;
            baseParent = plate.transform.parent;
            plate.transform.SetParent(null, true);
            plate.SetActive(true);
            foreach (var traffic in FindObjectsByType<AmbientTraffic>(FindObjectsSortMode.None)) traffic.enabled = false;

            // Nothing may move the origin but this: walking, teleporting, turning.
            paused.Clear();
            foreach (var b in origin.GetComponentsInChildren<LocomotionProvider>(true)) Pause(b);
            foreach (var b in origin.GetComponentsInChildren<HandTeleport>(true)) Pause(b);
            foreach (var b in origin.GetComponentsInChildren<ComfortTurn>(true)) Pause(b);

            savedPose = new Pose(origin.transform.position, origin.transform.rotation);
            savedScale = origin.transform.localScale;
            scale = stage.Settings.scale;
            PlaceInFront();

            // Rays and grab spheres are measured in world units: they grow with the model so they still reach.
            rays.Clear();
            spheres.Clear();
            foreach (var c in origin.GetComponentsInChildren<CurveInteractionCaster>(true)) rays.Add((c, c.castDistance));
            foreach (var c in origin.GetComponentsInChildren<SphereInteractionCaster>(true)) spheres.Add((c, c.castRadius));
            Active = true;
            Rescale(scale);
            Debug.Log($"[GRIDLOCK] Mixed reality on: 1 : {scale:F0}");
        }

        void Pause(Behaviour b)
        {
            if (!b.enabled) return;
            b.enabled = false;
            paused.Add(b);
        }

        void Leave()
        {
            Active = false;
            WorldScale = 1f;
            foreach (var (c, d) in rays) if (c != null) c.castDistance = d;
            foreach (var (c, r) in spheres) if (c != null) c.castRadius = r;
            if (origin != null)
            {
                origin.transform.SetPositionAndRotation(savedPose.position, savedPose.rotation);
                origin.transform.localScale = savedScale;
            }
            foreach (var b in paused) if (b != null) b.enabled = true;
            paused.Clear();
            if (eye != null)
            {
                eye.clearFlags = savedClear;
                eye.backgroundColor = savedBackground;
            }
            RenderSettings.fog = savedFog;
            if (session != null) Destroy(session);
            view?.Restore();
            if (stage != null && stage.DioramaBase != null)
            {
                stage.DioramaBase.SetActive(false);
                stage.DioramaBase.transform.SetParent(baseParent, true);
            }
            foreach (var traffic in FindObjectsByType<AmbientTraffic>(FindObjectsSortMode.None)) traffic.enabled = true;
            Debug.Log("[GRIDLOCK] Mixed reality off");
        }

        /// <summary>The model at table height, an arm's length in front of the eyes, facing the player.</summary>
        public void PlaceInFront()
        {
            var head = origin.transform.InverseTransformPoint(eye.transform.position);
            var forward = origin.transform.InverseTransformDirection(eye.transform.forward);
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
            forward.Normalize();
            // Origin space is real metres (the origin's own scale does not change it). The model's near edge stays at
            // arm's length: its centre goes half its depth further.
            anchor = head + forward * (distance + stage.Settings.bounds.extents.z / scale);
            anchor.y = Mathf.Clamp(head.y - drop, 0.4f, 1.1f);
            yaw = ARPlacementMath.FacingYaw(anchor, head);
            Apply();
        }

        void Rescale(float worldPerMetre)
        {
            scale = Mathf.Clamp(worldPerMetre, stage.Settings.scaleRange.x, stage.Settings.scaleRange.y);
            WorldScale = scale;
            foreach (var (c, d) in rays) if (c != null) c.castDistance = d * scale;
            foreach (var (c, r) in spheres) if (c != null) c.castRadius = r * scale;
            Apply();
        }

        void Apply()
        {
            var (position, rotation) = ARPlacementMath.OriginFor(stage.ContentPivot.position, anchor, yaw, scale);
            origin.transform.SetPositionAndRotation(position, rotation);
            origin.transform.localScale = Vector3.one * scale;
        }

        void Update()
        {
            if (!Active) return;
            var right = Stick(UnityEngine.InputSystem.XR.XRController.rightHand);
            var left = Stick(UnityEngine.InputSystem.XR.XRController.leftHand);
            if (Mathf.Abs(right.y) > 0.2f) Rescale(scale * Mathf.Exp(-right.y * 1.2f * Time.unscaledDeltaTime)); // push up: bigger
            if (Mathf.Abs(left.x) > 0.2f)
            {
                yaw += left.x * 70f * Time.unscaledDeltaTime;
                Apply();
            }
            var click = UnityEngine.InputSystem.XR.XRController.rightHand?.TryGetChildControl<ButtonControl>("thumbstickClicked");
            if (click != null && click.wasPressedThisFrame) PlaceInFront();
            view.UpdateCutaway(eye.transform.position, force: false);
        }

        static Vector2 Stick(InputDevice controller)
        {
            if (controller == null) return Vector2.zero;
            var stick = controller.TryGetChildControl<Vector2Control>("thumbstick") ?? controller.TryGetChildControl<Vector2Control>("primary2DAxis");
            return stick != null ? stick.ReadValue() : Vector2.zero;
        }
    }
}
