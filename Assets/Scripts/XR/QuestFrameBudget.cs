using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using UnityEngine.XR.OpenXR.Features.Extensions.PerformanceSettings;

namespace Gridlock.XR
{
    /// <summary>
    /// Keeps the headset's display settings applied for the whole run. The OpenXR display only starts once the session
    /// is ready (seconds after the first scene's Awake), and a level set before then is dropped, so foveation is
    /// (re)applied as soon as the display runs and after every scene load.
    /// Tuning without a rebuild: <c>adb shell setprop debug.gridlock.fov 1</c> (foveation 0–1),
    /// <c>debug.gridlock.scale 0.9</c> (eye-texture scale) and <c>debug.gridlock.msaa 2</c>, then restart the app.
    /// </summary>
    public class QuestFrameBudget : MonoBehaviour
    {
        static QuestFrameBudget instance;
        static readonly List<XRDisplaySubsystem> Displays = new List<XRDisplaySubsystem>();

        float level;
        bool applied;

        public static void Ensure(float foveationLevel)
        {
            if (instance == null)
            {
                instance = new GameObject("Quest Frame Budget").AddComponent<QuestFrameBudget>();
                DontDestroyOnLoad(instance.gameObject);
            }
            instance.level = Property("debug.gridlock.fov", foveationLevel);
            instance.applied = false;

            float scale = Property("debug.gridlock.scale", 1f);
            if (scale > 0.4f && scale <= 1.5f && !Mathf.Approximately(scale, XRSettings.eyeTextureResolutionScale))
                XRSettings.eyeTextureResolutionScale = scale;
            int msaa = (int)Property("debug.gridlock.msaa", 0f);
            if (msaa is 1 or 2 or 4 && GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
                urp.msaaSampleCount = msaa;
        }

        /// <summary>Android system property as a number (debug overrides), or <paramref name="fallback"/>.</summary>
        static float Property(string key, float fallback)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var props = new AndroidJavaClass("android.os.SystemProperties");
                var value = props.CallStatic<string>("get", key);
                if (float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f))
                    return f;
            }
            catch (System.Exception) { } // hidden API blocked: keep the default
#endif
            return fallback;
        }

        void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
        void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            applied = false;
            Debug.Log($"[GRIDLOCK] Scene {scene.name}");
        }

        void Update()
        {
            if (applied) return;
            SubsystemManager.GetSubsystems(Displays);
            foreach (var display in Displays)
            {
                if (!display.running) continue;
                display.foveatedRenderingFlags = XRDisplaySubsystem.FoveatedRenderingFlags.None;
                display.foveatedRenderingLevel = level;
                // The GPU is the bottleneck on Quest 2 (fill rate); let the runtime clock it up, keep the CPU frugal.
                XrPerformanceSettingsFeature.SetPerformanceLevelHint(PerformanceDomain.Gpu, PerformanceLevelHint.SustainedHigh);
                XrPerformanceSettingsFeature.SetPerformanceLevelHint(PerformanceDomain.Cpu, PerformanceLevelHint.SustainedLow);
                applied = true;
                Debug.Log($"[GRIDLOCK] Foveation {level:0.00}, eye scale {XRSettings.eyeTextureResolutionScale:0.00}, " +
                          $"MSAA {(GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset)?.msaaSampleCount}");
            }
        }
    }
}
