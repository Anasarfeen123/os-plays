using Gridlock.AR;
using Gridlock.Core;
using UnityEngine;
using UnityEngine.XR;

namespace Gridlock.XR
{
    /// <summary>
    /// Picks the presentation at startup:
    /// <list type="bullet">
    /// <item>a running headset (always in the Quest build): the XR rig;</item>
    /// <item>an Android phone: AR — the scene is placed as a tabletop model on a real floor or table, with the touch HUD
    /// on top (falls back to the touch view if the phone has no ARCore);</item>
    /// <item>otherwise the desktop camera (or the scenario's first-person player), with touch controls on a phone.</item>
    /// </list>
    /// In the editor the XR rig (with the XR Interaction Simulator) or AR can be forced on for testing.
    /// </summary>
    public class XRModeController : MonoBehaviour
    {
        [SerializeField] GameObject xrOrigin;
        [SerializeField] GameObject desktopRig;
        [Tooltip("XR Interaction Simulator prefab (XRI sample).")]
        [SerializeField] GameObject simulatorPrefab;
        [Tooltip("AR Session + AR origin (lot scenes only).")]
        [SerializeField] GameObject arRig;
        [Tooltip("Editor only: use the XR rig with the simulator even without a headset.")]
        [SerializeField] bool simulateXRInEditor;

        public bool XRActive { get; private set; }
        public bool ARActive { get; private set; }

        /// <summary>The scene's mode controller once it has picked a presentation (null before its Awake).</summary>
        public static XRModeController Current { get; private set; }

        /// <summary>Editor tests: start every scene with the XR rig and the XR Interaction Simulator.</summary>
        public static bool ForceSimulatorInEditor;
        /// <summary>Editor tests: start lot scenes in phone AR (no camera feed, placement driven by the test).</summary>
        public static bool ForceARInEditor;

        public void Init(GameObject origin, GameObject desktop, GameObject simulator, GameObject ar = null)
        {
            xrOrigin = origin;
            desktopRig = desktop;
            simulatorPrefab = simulator;
            arRig = ar;
        }

        public void SetARRig(GameObject ar) => arRig = ar;

        void Awake()
        {
            // On a standalone headset the OpenXR display may still be starting during the first Awake; it is XR regardless.
            // ARCore also makes XR "active" on a phone: that is never a headset.
            bool headset = (XRSettings.isDeviceActive || QuestPerformance.IsStandaloneHeadset) && !QuestPerformance.IsHandheldAR;
            bool simulate = Application.isEditor && (simulateXRInEditor || ForceSimulatorInEditor) && !headset && simulatorPrefab != null;
            bool phoneAR = arRig != null && !headset && !simulate &&
                           (Application.isEditor ? ForceARInEditor : Application.platform == RuntimePlatform.Android);
            Progress.MarkVisited(gameObject.scene.name);
            XRActive = headset || simulate;
            ARActive = phoneAR;
            Current = this;
            ScenePlacement.Ready = !phoneAR;

            xrOrigin.SetActive(XRActive);
            desktopRig.SetActive(!XRActive);
            if (phoneAR) ARStage.PrepareRig(arRig);
            if (arRig != null) arRig.SetActive(phoneAR);
            if (simulate) Instantiate(simulatorPrefab);

            if (XRActive) QuestPerformance.Apply(xrOrigin);
            else if (phoneAR)
            {
                SetDesktopCamera(false); // the HUD stays; the phone camera becomes the view
                PhonePresentation.Apply(desktopRig);
                PhonePresentation.Apply(arRig);
                arRig.GetComponentInChildren<ARStage>(true).Begin(trustSupport: Application.isEditor);
            }
            else if (TouchGestures.IsTouchDevice) PhonePresentation.Apply(desktopRig);

            Debug.Log($"[GRIDLOCK] Presentation: {(headset ? "XR headset" : simulate ? "XR simulator" : phoneAR ? "phone AR" : TouchGestures.IsTouchDevice ? "phone" : "desktop")}");
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        /// <summary>The phone has no ARCore after all: play in the touch view instead.</summary>
        public void FallBackFromAR()
        {
            if (!ARActive) return;
            ARActive = false;
            arRig.SetActive(false);
            SetDesktopCamera(true);
            ScenePlacement.Ready = true;
            Debug.Log("[GRIDLOCK] AR unavailable on this phone: touch view instead");
        }

        void SetDesktopCamera(bool on)
        {
            foreach (var cam in desktopRig.GetComponentsInChildren<Camera>(true)) cam.enabled = on;
            foreach (var ears in desktopRig.GetComponentsInChildren<AudioListener>(true)) ears.enabled = on;
            foreach (var orbit in desktopRig.GetComponentsInChildren<DesktopCameraController>(true)) orbit.enabled = on;
            // The hub's walking and looking drive the desktop camera; in AR the phone itself is the view.
            foreach (var roam in desktopRig.GetComponentsInChildren<Gridlock.Hub.HubRoamer>(true)) roam.enabled = on;
            foreach (var look in desktopRig.GetComponentsInChildren<Gridlock.Hub.HubDesktopLook>(true)) look.enabled = on;
            foreach (var stand in desktopRig.GetComponentsInChildren<Gridlock.Hub.HubRoamButton>(true)) stand.gameObject.SetActive(on);
            foreach (var sit in desktopRig.GetComponentsInChildren<Gridlock.Hub.HubSitButton>(true)) sit.gameObject.SetActive(on);
        }
    }
}
