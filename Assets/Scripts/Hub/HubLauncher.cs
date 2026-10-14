using System.Collections;
using Gridlock.Core;
using Gridlock.XR;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gridlock.Hub
{
    /// <summary>
    /// The hub's big screen: choosing a scenario pulls the player into the monitor. The screen flares, light streaks
    /// and rings rush out of it while the room's neon powers down, the viewpoint accelerates through the glass and the view whites out into the scenario, which
    /// then fades in. The move is short and straight, which keeps it comfortable in a headset.
    /// </summary>
    public class HubLauncher : MonoBehaviour
    {
        [SerializeField] XRModeController mode;
        [SerializeField] XROrigin xrOrigin;
        [SerializeField] Transform desktopCamera;
        [Tooltip("Centre of the monitor; its forward axis points into the glass, away from the chair.")]
        [SerializeField] Transform screen;
        [SerializeField] Renderer screenGlow;
        [SerializeField] ParticleSystem[] warp;
        [SerializeField] ScreenFader fader;
        [SerializeField] AudioSource whoosh;
        [SerializeField] float duration = 1.7f;

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        bool launching;

        public void Init(XRModeController xrMode, XROrigin origin, Transform desktop, Transform monitor, Renderer glow,
                         ParticleSystem[] streaks, ScreenFader screenFader, AudioSource sound)
        {
            mode = xrMode;
            xrOrigin = origin;
            desktopCamera = desktop;
            screen = monitor;
            screenGlow = glow;
            warp = streaks;
            fader = screenFader;
            whoosh = sound;
        }

        public void Launch(string sceneName)
        {
            if (launching) return;
            launching = true;
            StartCoroutine(PullIntoScreen(sceneName));
        }

        IEnumerator PullIntoScreen(string sceneName)
        {
            var load = SceneManager.LoadSceneAsync(sceneName);
            load.allowSceneActivation = false;

            bool xr = mode != null && mode.XRActive;
            var mover = xr ? xrOrigin.transform : desktopCamera;
            var eye = xr ? xrOrigin.Camera.transform : desktopCamera;
            if (xr) foreach (var b in xrOrigin.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Locomotion.LocomotionProvider>()) b.enabled = false;
            else if (desktopCamera.TryGetComponent<HubRoamer>(out var roamer)) roamer.Freeze(); // its collider would fight the warp

            if (warp != null) foreach (var p in warp) p.Play();
            if (whoosh != null) whoosh.Play();
            var glow = screenGlow != null ? screenGlow.material : null;
            var glowBase = glow != null ? glow.GetColor(BaseColor) : Color.black;

            // Aim a little past the glass so the camera ends up "inside" the screen.
            var startEye = eye.position;
            var through = screen.position + screen.forward * 0.6f;
            StartCoroutine(FadeLate(duration));

            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;
                float ease = k * k * k; // slow start, then a rush
                var wanted = Vector3.LerpUnclamped(startEye, through, ease);
                mover.position += wanted - eye.position;
                if (glow != null) glow.SetColor(BaseColor, Color.Lerp(glowBase, Color.white * 2.5f, k * k));
                HubPulse.GlobalIntensity = Mathf.Lerp(1f, 0.12f, Mathf.Clamp01(k * 2f)); // the room powers down
                yield return null;
            }

            while (load.progress < 0.9f) yield return null;
            load.allowSceneActivation = true;
        }

        IEnumerator FadeLate(float total)
        {
            yield return new WaitForSeconds(total * 0.55f);
            if (fader == null) yield break;
            fader.FadeColor = new Color(0.75f, 0.95f, 1f); // white-out into the scenario, which fades in from the same colour
            yield return fader.FadeTo(1f, total * 0.45f);
        }
    }
}
