using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR;
using UnityEngine.XR.Management;

namespace Gridlock.XR
{
    /// <summary>
    /// Standalone-headset budget (Meta Quest 2: XR2, 72 Hz). Bloom, SMAA and the full-screen post stack cost
    /// more than the whole lot on that GPU, so on Android the XR camera renders without them (2× MSAA comes from the
    /// Mobile URP asset). Fixed foveated rendering trims the lens edges the player can't see sharply anyway.
    /// </summary>
    public static class QuestPerformance
    {
        /// <summary>Fixed foveation strength (0–1): "high", the usual setting for Quest apps without eye tracking.</summary>
        const float FoveationLevel = 0.75f;

        /// <summary>
        /// Android with an XR loader configured: the Quest build. The phone build ships without one (GridlockPhoneSetup),
        /// so the same code runs the touch presentation there.
        /// </summary>
        public static bool IsStandaloneHeadset =>
            Application.platform == RuntimePlatform.Android && XRGeneralSettings.Instance != null &&
            XRGeneralSettings.Instance.Manager != null && XRGeneralSettings.Instance.Manager.activeLoaders.Count > 0 &&
            !IsHandheldAR;

        /// <summary>The phone build: ARCore is the XR loader, so XR is "running" but there is no headset.</summary>
        public static bool IsHandheldAR =>
            Application.platform == RuntimePlatform.Android && XRGeneralSettings.Instance != null &&
            XRGeneralSettings.Instance.Manager != null &&
            XRGeneralSettings.Instance.Manager.activeLoaders.Any(l => l != null && l.GetType().Name.Contains("ARCore"));

        public static void Apply(GameObject xrOrigin)
        {
            if (!IsStandaloneHeadset) return;

            foreach (var cam in xrOrigin.GetComponentsInChildren<Camera>(true))
            {
                cam.farClipPlane = Mathf.Min(cam.farClipPlane, 240f); // fog is opaque by 220 m
                // Depth precision falls with the near plane: the rig's 1 cm made distant screens and walls z-fight.
                cam.nearClipPlane = Mathf.Max(cam.nearClipPlane, 0.05f);
                cam.allowHDR = false;
                var data = cam.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = false;
                data.antialiasing = AntialiasingMode.None;
                data.renderShadows = true;
            }
            foreach (var volume in Object.FindObjectsByType<Volume>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                volume.enabled = false;

            QuestFrameBudget.Ensure(FoveationLevel);
            Application.targetFrameRate = -1; // the compositor paces frames
            QualitySettings.vSyncCount = 0;
        }
    }
}
