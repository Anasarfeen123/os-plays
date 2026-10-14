using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;

namespace Gridlock.Core
{
    /// <summary>
    /// Applies the display and control settings to the scene: master volume, post effects (bloom, grading), sun shadows,
    /// and the XR rig's turning (snap or smooth, snap angle) and stick walking. Re-applies whenever a setting changes.
    /// One per scene, next to <see cref="AudioRouting"/>.
    /// </summary>
    public class SettingsApplier : MonoBehaviour
    {
        readonly Dictionary<Light, LightShadows> originalShadows = new Dictionary<Light, LightShadows>();

        void Awake() => GameSettings.Changed += Apply;
        void OnDestroy() => GameSettings.Changed -= Apply;
        void Start() => Apply();

        void Apply()
        {
            AudioListener.volume = GameSettings.Get(Setting.Master);

            bool post = GameSettings.IsOn(Setting.PostEffects);
            foreach (var cam in FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (cam.TryGetComponent<UniversalAdditionalCameraData>(out var data)) data.renderPostProcessing = post;

            bool shadows = GameSettings.IsOn(Setting.Shadows);
            foreach (var light in FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!originalShadows.ContainsKey(light)) originalShadows[light] = light.shadows;
                light.shadows = shadows ? originalShadows[light] : LightShadows.None;
            }

            foreach (var snap in FindObjectsByType<SnapTurnProvider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                snap.turnAmount = GameSettings.SnapDegrees;
                snap.debounceTime = 0.3f; // XRI's 0.5 s feels sticky when turning twice
            }
            foreach (var smooth in FindObjectsByType<ContinuousTurnProvider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                smooth.turnSpeed = 90f; // XRI's 60°/s is sluggish for looking around the lot

            // The XRI starter rig switches turning and stick walking per controller; set it by name so the sample
            // assembly need not be referenced. Walking goes on the LEFT stick only: the manager disables turning on any
            // controller that walks, so the right stick must stay free to turn (and teleport when pushed forward).
            bool smoothTurn = GameSettings.IsOn(Setting.SmoothTurn), smoothMove = GameSettings.IsOn(Setting.SmoothMove);
            foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (behaviour == null || behaviour.GetType().Name != "ControllerInputActionManager") continue;
                var type = behaviour.GetType();
                type.GetProperty("smoothTurnEnabled")?.SetValue(behaviour, smoothTurn);
                type.GetProperty("smoothMotionEnabled")?.SetValue(behaviour, smoothMove && IsLeftHand(behaviour.transform));
            }
        }

        static bool IsLeftHand(Transform t)
        {
            for (; t != null; t = t.parent)
                if (t.name.StartsWith("Left")) return true;
            return false;
        }
    }
}
