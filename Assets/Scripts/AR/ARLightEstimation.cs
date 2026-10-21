using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.ARFoundation;

namespace Gridlock.AR
{
    /// <summary>
    /// Lights the model like the real room around it, from ARCore's light estimate. Every light in the scene follows the
    /// room's brightness (dimmer in a dim room, within limits so the model stays readable) and is tinted by its colour.
    /// Outdoor models (the lots) also turn their sun to where the room's main light comes from, so their shadows fall
    /// the same way as real ones, and take their ambient light from the room's.
    /// </summary>
    public class ARLightEstimation : MonoBehaviour
    {
        [SerializeField] ARCameraManager cameraManager;
        [Tooltip("The AR session origin: the estimate's light direction is in its space.")]
        [SerializeField] Transform sessionOrigin;
        [Tooltip("Outdoor scene: turn the sun to the room's main light and use the room's ambient light (Environmental HDR).")]
        [SerializeField] bool outdoor;
        [SerializeField] Vector2 brightnessRange = new Vector2(0.6f, 1.25f);
        [Tooltip("How much of the room's colour tints the scene (0 = none, 1 = all of it).")]
        [SerializeField, Range(0f, 1f)] float tintAmount = 0.5f;
        [Tooltip("Seconds to settle on a new estimate (the estimate flickers frame to frame).")]
        [SerializeField] float smoothing = 0.6f;

        readonly List<(Light light, float intensity, Color color)> lights = new List<(Light, float, Color)>();
        Light sun;
        Quaternion sunHome;
        Color sky, equator, ground;

        float brightness = 1f, targetBrightness = 1f;
        Color tint = Color.white, targetTint = Color.white;
        Vector3? lightDirection;
        Color? roomSky, roomEquator, roomGround;

        /// <summary>The latest estimate in the scene's terms (tests and the HUD's debug line read it).</summary>
        public float Brightness => brightness;

        public void Init(ARCameraManager manager, Transform origin, bool isOutdoor)
        {
            cameraManager = manager;
            sessionOrigin = origin;
            outdoor = isOutdoor;
        }

        void OnEnable()
        {
            lights.Clear();
            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                lights.Add((light, light.intensity, light.color));
            sun = RenderSettings.sun;
            if (sun != null) sunHome = sun.transform.rotation;
            sky = RenderSettings.ambientSkyColor;
            equator = RenderSettings.ambientEquatorColor;
            ground = RenderSettings.ambientGroundColor;
            if (cameraManager != null)
            {
                cameraManager.requestedLightEstimation = outdoor
                    ? LightEstimation.MainLightDirection | LightEstimation.MainLightIntensity | LightEstimation.AmbientSphericalHarmonics
                    : LightEstimation.AmbientIntensity | LightEstimation.AmbientColor;
                cameraManager.frameReceived += OnFrame;
            }
        }

        void OnDisable()
        {
            if (cameraManager != null) cameraManager.frameReceived -= OnFrame;
            // Leave the scene as authored (the touch view after an AR fallback, the next scene's capture).
            foreach (var (light, intensity, color) in lights)
                if (light != null) { light.intensity = intensity; light.color = color; }
            if (sun != null) sun.transform.rotation = sunHome;
            RenderSettings.ambientSkyColor = sky;
            RenderSettings.ambientEquatorColor = equator;
            RenderSettings.ambientGroundColor = ground;
        }

        static readonly Vector3[] AmbientDirections = { Vector3.up, Vector3.forward, Vector3.down };
        readonly Color[] ambientSamples = new Color[3];

        void OnFrame(ARCameraFrameEventArgs frame)
        {
            var estimate = frame.lightEstimation;
            float? level = null;
            if (estimate.averageBrightness.HasValue)
                level = estimate.averageBrightness.Value; // 0 … 1, about 0.4 in a normally lit room
            else if (estimate.mainLightColor.HasValue)
                level = estimate.mainLightColor.Value.maxColorComponent * 0.5f;
            if (level.HasValue)
                targetBrightness = Mathf.Lerp(brightnessRange.x, brightnessRange.y, Mathf.InverseLerp(0.12f, 0.65f, level.Value));

            Color? colour = estimate.colorCorrection ?? estimate.mainLightColor;
            if (colour.HasValue)
            {
                var c = colour.Value;
                float max = Mathf.Max(c.maxColorComponent, 1e-4f);
                targetTint = Color.Lerp(Color.white, new Color(c.r / max, c.g / max, c.b / max), tintAmount);
            }

            if (!outdoor) return;
            if (estimate.mainLightDirection.HasValue) lightDirection = estimate.mainLightDirection.Value;
            if (estimate.ambientSphericalHarmonics.HasValue)
            {
                estimate.ambientSphericalHarmonics.Value.Evaluate(AmbientDirections, ambientSamples);
                roomSky = ambientSamples[0];
                roomEquator = ambientSamples[1];
                roomGround = ambientSamples[2];
            }
        }

        void Update()
        {
            float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime / Mathf.Max(0.01f, smoothing));
            brightness = Mathf.Lerp(brightness, targetBrightness, k);
            tint = Color.Lerp(tint, targetTint, k);

            foreach (var (light, intensity, color) in lights)
            {
                if (light == null) continue;
                light.intensity = intensity * brightness;
                light.color = color * tint;
            }

            if (!outdoor) return;
            if (sun != null && lightDirection.HasValue && sessionOrigin != null)
            {
                // From session space to the scene; kept at least 25° above the horizon so shadows stay short and readable.
                var dir = sessionOrigin.rotation * lightDirection.Value;
                if (dir.sqrMagnitude > 1e-6f)
                {
                    dir.Normalize();
                    var flat = new Vector3(dir.x, 0f, dir.z);
                    float down = Mathf.Max(-dir.y, Mathf.Sin(25f * Mathf.Deg2Rad));
                    dir = (flat.sqrMagnitude > 1e-6f ? flat.normalized * Mathf.Sqrt(Mathf.Max(0f, 1f - down * down)) : Vector3.zero) + Vector3.down * down;
                    sun.transform.rotation = Quaternion.Slerp(sun.transform.rotation, Quaternion.LookRotation(dir), k);
                }
            }
            if (roomSky.HasValue && RenderSettings.ambientMode == AmbientMode.Trilight)
            {
                // Half the room's ambient light, half the scene's own, so the model keeps its look.
                RenderSettings.ambientSkyColor = Color.Lerp(sky, roomSky.Value, 0.5f);
                RenderSettings.ambientEquatorColor = Color.Lerp(equator, roomEquator.Value, 0.5f);
                RenderSettings.ambientGroundColor = Color.Lerp(ground, roomGround.Value, 0.5f);
            }
        }
    }
}
