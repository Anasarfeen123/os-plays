using System.Collections;
using System.Linq;
using Gridlock.Guide;
using Gridlock.UI;
using Gridlock.XR;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Gridlock.Tests
{
    /// <summary>
    /// The scenes as a headset sees them, using the XR Interaction Simulator: the XR rig (controllers + tracked hands)
    /// replaces the desktop camera, the guide screen floats in front of the player, every XR-facing canvas takes ray and
    /// poke input, and the system menu opens within arm's reach.
    /// </summary>
    public class XRModeTests
    {
        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            XRModeController.ForceSimulatorInEditor = true;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            XRModeController.ForceSimulatorInEditor = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            GuideTour.Seen = BankerTour.Seen = true;

            // The XR Interaction Simulator outlives scene loads and keeps simulated hand/controller devices alive, which
            // would leak into later tests (e.g. the XR Hands package tests count hand subsystems). Remove it entirely.
            foreach (var behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (behaviour != null && behaviour.GetType().Name.Contains("Simulator")) Object.Destroy(behaviour.gameObject);
            yield return null;
            SceneManager.LoadScene("GridlockHub", LoadSceneMode.Single);
            yield return null;
            foreach (var subsystem in Gridlock.Tests.XRModeTests.HandSubsystems()) subsystem.Stop();
            yield return null;
        }

        static System.Collections.Generic.List<UnityEngine.XR.Hands.XRHandSubsystem> HandSubsystems()
        {
            var list = new System.Collections.Generic.List<UnityEngine.XR.Hands.XRHandSubsystem>();
            SubsystemManager.GetSubsystems(list);
            return list;
        }

        static void AssertXRReady(string scene)
        {
            var mode = Object.FindFirstObjectByType<XRModeController>();
            Assert.IsTrue(mode.XRActive, $"{scene}: XR presentation");
            Assert.IsFalse(GameObject.Find("Desktop Camera"), $"{scene}: the desktop camera and HUD are off in XR");
            Assert.IsNotNull(Camera.main, $"{scene}: the XR camera is the main camera");

            Assert.IsNotNull(Object.FindFirstObjectByType<XRUIInputModule>(), $"{scene}: XR UI input module");
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                                         .Where(c => c.renderMode == RenderMode.WorldSpace && c.isRootCanvas &&
                                                     !(c.TryGetComponent<CanvasGroup>(out var g) && !g.blocksRaycasts))) // not the display-only hint cards
                Assert.IsNotNull(canvas.GetComponent<TrackedDeviceGraphicRaycaster>(), $"{scene}: '{canvas.name}' takes XR rays and pokes");

            var pokes = Object.FindObjectsByType<XRPokeInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.IsTrue(pokes.Any(p => p.enableUIInteraction), $"{scene}: a fingertip/controller poke can press UI");
            var rays = Object.FindObjectsByType<NearFarInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.IsTrue(rays.Any(r => r.enableUIInteraction), $"{scene}: rays (controller or hand pinch) can press UI");
            Assert.IsNotNull(Object.FindObjectsByType<SystemMenuLink>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                                   .FirstOrDefault(l => l.GetComponent<XRButtonShortcuts>() != null),
                             $"{scene}: the controller ≡ button opens the system menu");
        }

        static IEnumerator OpenMenuInHeadset(string capture)
        {
            var menu = SystemMenu.Instance;
            menu.Open();
            yield return null;
            var xrView = Object.FindObjectsByType<SystemMenuView>(FindObjectsSortMode.None).Single(v => v.GetComponent<Canvas>() != null);
            float distance = Vector3.Distance(xrView.transform.position, Camera.main.transform.position);
            Assert.That(distance, Is.InRange(0.6f, 0.95f), "the XR menu opens within arm's reach, for poking");
            Assert.AreEqual(0f, Time.timeScale);
            GridlockSceneTests.Capture(capture);
            menu.Close();
            Assert.AreEqual(1f, Time.timeScale);
        }

        [UnityTest]
        public IEnumerator DeadlockScene_InHeadset()
        {
            GuideTour.Seen = false;
            SceneManager.LoadScene("GridlockParkingLot");
            yield return new WaitForSeconds(2.5f);
            AssertXRReady("Deadlock");

            var tour = Object.FindFirstObjectByType<GuideTour>();
            Assert.IsTrue(tour.Running);
            var floating = Object.FindObjectsByType<FloatingFollow>(FindObjectsSortMode.None).Single();
            Assert.IsTrue(floating.gameObject.activeInHierarchy, "the guide screen floats in front of the player");
            float d = Vector3.Distance(floating.transform.position, Camera.main.transform.position);
            Assert.That(d, Is.InRange(1.2f, 1.8f));
            GridlockSceneTests.Capture("xr_01_deadlock_guide");

            yield return OpenMenuInHeadset("xr_02_deadlock_menu");
        }

        [UnityTest]
        public IEnumerator BankerScene_InHeadset()
        {
            BankerTour.Seen = false;
            SceneManager.LoadScene("GridlockBanker");
            yield return new WaitForSeconds(2.5f);
            AssertXRReady("Banker");
            Assert.IsTrue(Object.FindFirstObjectByType<BankerTour>().Running);
            GridlockSceneTests.Capture("xr_03_banker_guide");
            yield return OpenMenuInHeadset("xr_04_banker_menu");
        }

        [UnityTest]
        public IEnumerator Hub_InHeadset()
        {
            SceneManager.LoadScene("GridlockHub");
            yield return new WaitForSeconds(5f);
            AssertXRReady("Hub");
            var areas = Object.FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationArea>(FindObjectsSortMode.None);
            Assert.IsTrue(areas.Any(a => a.name == "Floor") && areas.Any(a => a.name == "GalleryDeck") && areas.Any(a => a.name == "StairsRamp"),
                          "the control room floor, stairs and gallery are teleport targets");
            Assert.IsNotNull(Object.FindFirstObjectByType<Gridlock.Hub.HubSeat>(), "point at the chair to sit at the desk");
            yield return OpenMenuInHeadset("xr_05_hub_menu");
        }
    }
}
