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
            // UI writes no depth: the menu stays readable only if it draws after every other world canvas (the hub monitor).
            int menuOrder = xrView.GetComponent<Canvas>().sortingOrder;
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (canvas.isRootCanvas && canvas.renderMode == RenderMode.WorldSpace && !canvas.transform.IsChildOf(xrView.transform))
                    Assert.Greater(menuOrder, canvas.sortingOrder, $"the menu draws over {canvas.name}");
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
    

        [UnityTest]
        public IEnumerator Menu_OnePressOpens_RaysSeeOnlyTheMenu_WristOpensWithController()
        {
            GuideTour.Seen = true;
            SceneManager.LoadScene("GridlockParkingLot");
            yield return new WaitForSeconds(2.5f);
            var menu = SystemMenu.Instance;

            // On Quest one ≡ press can also arrive as Android Back (Esc): two toggles in a row are one press.
            menu.Toggle();
            menu.Toggle();
            Assert.IsTrue(menu.IsOpen, "a doubled press still opens the menu");

            var casters = Object.FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Interactors.Casters.CurveInteractionCaster>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.IsNotEmpty(casters);
            Assert.IsTrue(casters.All(c => c.raycastMask.value == 1 << SystemMenu.MenuLayer), "while open, the podium and the lot cannot catch the ray");
            // XRI filters UI hits by the same mask: every menu graphic must be on a layer the rays still see.
            var menuGraphics = Object.FindObjectsByType<UnityEngine.UI.Graphic>(FindObjectsSortMode.None)
                                     .Where(g => g.canvas != null && g.canvas.rootCanvas.renderMode == RenderMode.WorldSpace && g.raycastTarget &&
                                                 g.canvas.rootCanvas.sortingOrder >= 1000).ToArray(); // the menu draws on top
            Assert.IsNotEmpty(menuGraphics, "the open menu has clickable graphics");
            Assert.IsTrue(menuGraphics.All(g => casters.All(c => (c.raycastMask.value & (1 << g.gameObject.layer)) != 0)),
                          "the rays can hit every menu button");
            yield return new WaitForSecondsRealtime(0.4f);
            menu.Toggle();
            Assert.IsFalse(menu.IsOpen);
            Assert.IsTrue(casters.All(c => c.raycastMask.value != 0), "closing gives the rays the scene back");

            // Controllers: turning the left controller's palm side to the face shows the wrist menu, even while paused.
            var origin = Object.FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
            var left = origin.GetComponentsInChildren<Transform>(true).First(t => t.name == "Left Controller");
            left.gameObject.SetActive(true);
            foreach (var b in left.GetComponents<Behaviour>()) if (b.GetType().Name.Contains("TrackedPoseDriver")) b.enabled = false;
            var head = origin.Camera.transform;
            left.position = head.position + head.forward * 0.35f - Vector3.up * 0.15f;
            left.rotation = Quaternion.FromToRotation(Vector3.right, (head.position - left.position).normalized); // +X towards the eyes
            menu.Open();
            yield return new WaitForSecondsRealtime(0.5f);
            var wrist = origin.GetComponentInChildren<WristMenu>(true);
            Assert.IsTrue(wrist.IsShown, "the wrist menu opens with a controller in hand");
            menu.Close();
            left.rotation = Quaternion.LookRotation(head.forward); // palm towards the other hand again
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.IsFalse(wrist.IsShown);

            // The hands follow the controllers' grip pose.
            foreach (var side in new[] { "Left", "Right" })
                Assert.IsNotNull(origin.GetComponentsInChildren<Transform>(true).First(t => t.name == side + " Hand Model").GetComponent<ControllerGripPose>(),
                                 $"{side} hand on the grip pose");
        }

        static T Private<T>(object target, string field) =>
            (T)target.GetType().GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(target);

        [UnityTest]
        public IEnumerator Settings_EveryOptionApplies_TurnIsEased_PinchIsForgiving()
        {
            GuideTour.Seen = true;
            var saved = System.Enum.GetValues(typeof(Gridlock.Core.Setting)).Cast<Gridlock.Core.Setting>()
                .ToDictionary(s => s, Gridlock.Core.GameSettings.Get);
            try
            {
                SceneManager.LoadScene("GridlockParkingLot");
                yield return new WaitForSeconds(2.5f);
                var menu = SystemMenu.Instance;
                menu.Open();
                yield return null;
                var view = Object.FindObjectsByType<SystemMenuView>(FindObjectsSortMode.None).Single(v => v.GetComponent<Canvas>() != null);

                // Every segmented choice stores its value; every slider stores its value. Each tab is opened first, as a
                // player would (a hidden tab's controls are wired when it is first shown).
                var settingsView = view.GetComponentInChildren<SettingsView>(true);
                view.GetComponentsInChildren<UnityEngine.UI.Button>(true).First(b => b.name == "SETTINGS").onClick.Invoke();
                foreach (var tab in Private<UnityEngine.UI.Button[]>(settingsView, "tabs"))
                {
                    tab.onClick.Invoke();
                    yield return null;
                }
                foreach (var choice in view.GetComponentsInChildren<SettingChoice>(true))
                {
                    var page = Private<GameObject[]>(settingsView, "pages").First(p => choice.transform.IsChildOf(p.transform));
                    Private<UnityEngine.UI.Button[]>(settingsView, "tabs")[System.Array.IndexOf(Private<GameObject[]>(settingsView, "pages"), page)].onClick.Invoke();
                    var setting = Private<Gridlock.Core.Setting>(choice, "setting");
                    var buttons = Private<UnityEngine.UI.Button[]>(choice, "options");
                    var values = Private<float[]>(choice, "values");
                    for (int i = 0; i < buttons.Length; i++)
                    {
                        buttons[i].onClick.Invoke();
                        Assert.AreEqual(values[i], Gridlock.Core.GameSettings.Get(setting), 1e-4f, $"{setting} option {i}");
                    }
                }
                Private<UnityEngine.UI.Button[]>(settingsView, "tabs")[0].onClick.Invoke(); // AUDIO
                foreach (var slider in view.GetComponentsInChildren<SettingSlider>(true))
                {
                    var setting = Private<Gridlock.Core.Setting>(slider, "setting");
                    var control = Private<UnityEngine.UI.Slider>(slider, "slider");
                    control.value = 0.3f;
                    Assert.AreEqual(0.3f, Gridlock.Core.GameSettings.Get(setting), 1e-4f, $"{setting} slider");
                }
                Assert.AreEqual(0.3f, AudioListener.volume, 1e-4f, "MASTER drives the listener volume");
                Gridlock.Core.GameSettings.Set(Gridlock.Core.Setting.Quality, 0f);
                Assert.AreEqual(0.6f, QualitySettings.lodBias, 1e-4f, "GRAPHICS: PERFORMANCE applies");
                Gridlock.Core.GameSettings.Set(Gridlock.Core.Setting.Quality, 2f);
                Assert.AreEqual(1.6f, QualitySettings.lodBias, 1e-4f, "GRAPHICS: QUALITY applies");

                // VR TURNING switches the stick between snap and smooth on the rig.
                bool SmoothTurnOnRig() => Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Where(b => b.GetType().Name == "ControllerInputActionManager")
                    .All(b => (bool)b.GetType().GetProperty("smoothTurnEnabled")!.GetValue(b));
                Gridlock.Core.GameSettings.Set(Gridlock.Core.Setting.SmoothTurn, 1f);
                Assert.IsTrue(SmoothTurnOnRig(), "SMOOTH turning reaches the controllers");
                Gridlock.Core.GameSettings.Set(Gridlock.Core.Setting.SmoothTurn, 0f);
                Assert.IsFalse(SmoothTurnOnRig(), "SNAP turning reaches the controllers");
                menu.Close();

                // A snap is an eased sweep around the head, of the menu's SNAP ANGLE.
                Gridlock.Core.GameSettings.Set(Gridlock.Core.Setting.SnapAngle, 1f); // 45°
                var turn = ComfortTurn.Current;
                Assert.IsNotNull(turn, "the rig turns through ComfortTurn");
                var origin = Object.FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
                var head = origin.Camera.transform;
                float startYaw = head.eulerAngles.y;
                var headPosition = head.position;
                turn.Turn(Gridlock.Core.GameSettings.SnapDegrees);
                yield return null;
                yield return null;
                float partial = Mathf.Abs(Mathf.DeltaAngle(startYaw, head.eulerAngles.y));
                Assert.That(partial, Is.InRange(0.01f, 44f), "the view sweeps, it does not cut");
                yield return new WaitForSeconds(0.4f);
                Assert.AreEqual(45f, Mathf.DeltaAngle(startYaw, head.eulerAngles.y), 0.5f, "a whole snap angle");
                Assert.Less(Vector3.Distance(headPosition, head.position), 0.01f, "around the head");

                // Pinch: starts at 80 % strength and holds down to 50 %; the rays are stabilised.
                foreach (var reader in origin.GetComponentsInChildren<MonoBehaviour>(true).Where(b => b.GetType().Name == "ReleaseThresholdButtonReader"))
                {
                    Assert.AreEqual(0.8f, (float)reader.GetType().GetProperty("pressThreshold")!.GetValue(reader), 1e-4f, reader.name);
                    Assert.AreEqual(0.5f, (float)reader.GetType().GetProperty("releaseThreshold")!.GetValue(reader), 1e-4f, reader.name);
                }
                foreach (var ray in origin.GetComponentsInChildren<NearFarInteractor>(true))
                    Assert.IsTrue(((UnityEngine.XR.Interaction.Toolkit.Interactors.Casters.InteractionCasterBase)ray.farInteractionCaster).enableStabilization, $"{ray.transform.parent.name} ray stabilised");
            }
            finally
            {
                foreach (var pair in saved) Gridlock.Core.GameSettings.Set(pair.Key, pair.Value);
            }
        }
    }
}
