using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Gridlock.AR;
using Gridlock.Banker;
using Gridlock.Core;
using Gridlock.Guide;
using Gridlock.Hub;
using Gridlock.XR;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Gridlock.Tests
{
    /// <summary>
    /// Phone AR in the editor (no camera feed; the test stands in for ARCore's plane hit): every scene waits until it is
    /// placed on a surface and appears there as a model in front of the phone; tours and the demo start only then;
    /// pinch/twist keep it anchored; taps play (a car, a hub card, a lab desk, a page box and its pallet); and a scenario
    /// entered from the hub appears on the same spot without scanning again.
    /// </summary>
    public class ARModeTests
    {
        static readonly Vector3 Table = new Vector3(0f, -0.75f, 1.1f); // a table top, 75 cm below and 1.1 m ahead of the phone

        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            XRModeController.ForceARInEditor = true;
            GameSettings.AutoTours = true;
            Gridlock.Guide.NarratedTour.SetSeen("cpu", true); // the scenario tours
            Gridlock.Guide.NarratedTour.SetSeen("memory", true);
            ARStage.ForgetPlacement();
        }

        [TearDown]
        public void TearDown()
        {
            XRModeController.ForceARInEditor = false;
            ARStage.ForgetPlacement();
            ScenePlacement.Ready = true;
            Time.timeScale = 1f;
            GuideTour.Seen = BankerTour.Seen = true;
            Gridlock.Guide.NarratedTour.SetSeen("cpu", true); // the scenario tours
            Gridlock.Guide.NarratedTour.SetSeen("memory", true);
        }

        static IEnumerator WaitFor(Func<bool> condition, float timeoutSeconds, string what)
        {
            float start = Time.realtimeSinceStartup;
            while (!condition())
            {
                if (Time.realtimeSinceStartup - start > timeoutSeconds) Assert.Fail($"Timed out waiting for {what}");
                yield return null;
            }
        }

        /// <summary>Hold the phone like a player looking down at a table; a neutral backdrop stands in for the camera feed.</summary>
        static ARStage Phone()
        {
            var stage = UnityEngine.Object.FindFirstObjectByType<ARStage>();
            var cam = Camera.main;
            cam.transform.localPosition = Vector3.zero;
            cam.transform.localRotation = Quaternion.Euler(34f, 0f, 0f);
            cam.backgroundColor = new Color(0.36f, 0.33f, 0.3f);
            return stage;
        }

        static void AssertAnchored(ARStage stage)
        {
            var origin = stage.GetComponent<Unity.XR.CoreUtils.XROrigin>().transform;
            var pivot = GameObject.Find("AR Pivot").transform.position;
            Assert.Less(Vector3.Distance(origin.TransformPoint(Table), pivot), 0.01f, "the pivot sits on the tapped surface");
        }

        [UnityTest]
        public IEnumerator Deadlock_WaitsForPlacement_ThenTourStarts()
        {
            GuideTour.Seen = false;
            SceneManager.LoadScene("GridlockParkingLot");
            yield return new WaitForSeconds(1.5f);

            var mode = UnityEngine.Object.FindFirstObjectByType<XRModeController>();
            Assert.IsTrue(mode.ARActive, "phone AR");
            Assert.AreEqual("AR Camera", Camera.main.name);
            Assert.IsTrue(GameObject.Find("AR Guidance").activeInHierarchy, "on-screen AR guidance");
            Assert.IsFalse(ScenePlacement.Ready);
            var xrOrigin = UnityEngine.Object.FindFirstObjectByType<ARStage>().GetComponent<Unity.XR.CoreUtils.XROrigin>();
            Assert.AreEqual(0f, xrOrigin.CameraYOffset, "no height offset: the camera and the planes share ARCore's space");
            Assert.AreEqual(Vector3.zero, xrOrigin.CameraFloorOffsetObject.transform.localPosition);

            var stage = Phone();
            var sim = UnityEngine.Object.FindFirstObjectByType<SimulationManager>();
            var tour = UnityEngine.Object.FindFirstObjectByType<GuideTour>();
            yield return new WaitForSeconds(3f);
            Assert.IsNull(tour.CurrentStep, "the guide waits until the lot is on the table");
            Assert.IsTrue(sim.State.Cars.All(c => sim.State.GetHeld(c).Count == 0), "no cars move before placement");
            GridlockSceneTests.Capture("ar_00_scanning");

            stage.PlaceAt(Table);
            Assert.IsTrue(ScenePlacement.Ready && stage.Placed);
            AssertAnchored(stage);
            var lot = Camera.main.WorldToViewportPoint(new Vector3(0f, 0f, 1f));
            Assert.That(lot.x, Is.InRange(0.3f, 0.7f));
            Assert.That(lot.y, Is.InRange(0.15f, 0.85f));
            Assert.Greater(lot.z, 0f, "the miniature is in front of the phone");

            yield return WaitFor(() => tour.CurrentStep == "welcome", 10f, "the tour to start after placement");
            Time.timeScale = 3f;
            yield return WaitFor(() => sim.State.Cars.Count(c => sim.State.GetHeld(c).Count > 0) == 4 && !sim.CarsMoving, 90f, "cars parking");
            Time.timeScale = 1f;
            yield return null;
            GridlockSceneTests.Capture("ar_01_deadlock_miniature");

            // On a phone the side panel folds away to show the whole lot.
            var dock = UnityEngine.Object.FindFirstObjectByType<Gridlock.UI.CollapsiblePanel>();
            Assert.IsNotNull(dock, "the side panel can fold away");
            var dockRect = (RectTransform)dock.transform;
            float before = dockRect.anchoredPosition.x;
            dock.SetFolded(true);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.Greater(dockRect.anchoredPosition.x, before + 400f, "folded off the right edge");
            GridlockSceneTests.Capture("ar_01b_panel_folded");
            dock.SetFolded(false);
            yield return new WaitForSecondsRealtime(0.4f);

            // Pinch and twist keep the miniature on the same spot.
            stage.Rescale(25f);
            stage.Rotate(30f);
            AssertAnchored(stage);
            Assert.AreEqual(25f, stage.GetComponent<Unity.XR.CoreUtils.XROrigin>().transform.localScale.x, 1e-3f);
            yield return null;
            GridlockSceneTests.Capture("ar_02_bigger_rotated");
        }

        [UnityTest]
        public IEnumerator Deadlock_TapACarInTheMiniature_SelectsIt()
        {
            SceneManager.LoadScene("GridlockParkingLot");
            yield return new WaitForSeconds(1f);
            var stage = Phone();
            stage.PlaceAt(Table);
            yield return null;

            var sim = UnityEngine.Object.FindFirstObjectByType<SimulationManager>();
            var interaction = sim.GetComponent<PlayerInteraction>();
            var car = sim.GetCar("A");
            var screen = (Vector2)Camera.main.WorldToScreenPoint(car.transform.position + Vector3.up * 1f);
            Assert.IsTrue(stage.TapAt(screen), "the tap hits the car in the miniature");
            Assert.AreEqual("A", interaction.SelectedCar);
        }

        [UnityTest]
        public IEnumerator Banker_PlacedOnTable_TourExplains()
        {
            BankerTour.Seen = false;
            SceneManager.LoadScene("GridlockBanker");
            yield return new WaitForSeconds(1.5f);
            var stage = Phone();
            var tour = UnityEngine.Object.FindFirstObjectByType<BankerTour>();
            Assert.IsNull(tour.CurrentStep);
            stage.PlaceAt(Table);
            yield return WaitFor(() => tour.CurrentStep == "banker_fleets", 30f, "the Banker tour");
            yield return new WaitForSeconds(1f);
            GridlockSceneTests.Capture("ar_03_banker_miniature");
        }

        // ---- The rooms: hub, CPU lab, memory warehouse ----

        static MonoBehaviour Find(string typeName) =>
            UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(b => b.GetType().Name == typeName);

        static object Call(object target, string method, params object[] args) =>
            target.GetType().GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(target, args);

        static T Field<T>(object target, string field) =>
            (T)target.GetType().GetField(field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(target);

        static Vector2 ScreenPoint(Vector3 world) => Camera.main.WorldToScreenPoint(world);

        static Vector3 Centre(Component c)
        {
            // A solid part (a trigger volume is invisible and does not take AR taps), else a card's own tap target.
            var col = c.GetComponentsInChildren<Collider>().FirstOrDefault(x => !x.isTrigger) ?? c.GetComponentInChildren<Collider>();
            return col != null ? col.bounds.center : c.transform.position;
        }

        /// <summary>The model is on the table in front of the phone, at its own scale, with the AR overlay showing.</summary>
        static void AssertOnTable(ARStage stage, string scene)
        {
            Assert.IsTrue(stage.Placed && ScenePlacement.Ready, $"{scene}: placed");
            AssertAnchored(stage);
            var centre = Camera.main.WorldToViewportPoint(stage.Settings.bounds.center);
            Assert.Greater(centre.z, 0f, $"{scene}: the model is in front of the phone");
            Assert.That(centre.x, Is.InRange(0.2f, 0.8f), $"{scene}: centred");
            Assert.That(centre.y, Is.InRange(0.1f, 0.9f), $"{scene}: centred");
            Assert.IsTrue(GameObject.Find("AR Diorama Base").activeInHierarchy, $"{scene}: base under the model");
        }

        [UnityTest]
        public IEnumerator Hub_TapCards_EntersScenario_OnTheSameSpot()
        {
            SceneManager.LoadScene("GridlockHub");
            yield return new WaitForSeconds(1.5f);
            Assert.IsTrue(XRModeController.Current.ARActive, "the hub runs in AR on a phone");
            var stage = Phone();
            Assert.IsFalse(stage.Placed);
            GridlockSceneTests.Capture("ar_hub_00_scanning");
            stage.PlaceAt(Table);
            AssertOnTable(stage, "hub");
            var screen = UnityEngine.Object.FindFirstObjectByType<HubScreen>();
            yield return WaitFor(() => screen.Booted, 20f, "the hub screen to boot");
            yield return new WaitForSeconds(1f);
            GridlockSceneTests.Capture("ar_hub_01_control_room");

            // The ceiling is left out and the near walls are cut away: from the phone the desk and the holo screen show.
            var ceiling = GameObject.Find("Room/Shell/Ceiling").GetComponent<Renderer>();
            Assert.IsFalse(ceiling.enabled, "no ceiling on the dollhouse");

            var memory = UnityEngine.Object.FindObjectsByType<HubTile>(FindObjectsSortMode.None).First(t => t.name == "Tile_Memory");
            var tiles = Field<HubTile[]>(screen, "tiles");
            Assert.IsTrue(stage.TapAt(ScreenPoint(Centre(memory))), "a tap reaches the card on the holo screen");
            yield return null;
            Assert.AreEqual(Array.IndexOf(tiles, memory), screen.FocusedIndex, "the first tap focuses the card");
            yield return new WaitForSeconds(0.4f);
            GridlockSceneTests.Capture("ar_hub_02_card_focused");
            Assert.IsTrue(stage.TapAt(ScreenPoint(Centre(memory))), "a second tap enters");

            yield return WaitFor(() => SceneManager.GetActiveScene().name == "GridlockMemory", 25f, "the memory scene to load");
            yield return new WaitForSeconds(2f);
            var next = ARStage.Current;
            Assert.IsNotNull(next, "the warehouse runs in AR too");
            Phone();
            AssertOnTable(next, "memory (from the hub)");
            Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<UnityEngine.XR.ARFoundation.ARSession>(FindObjectsSortMode.None).Length,
                            "one AR session carries on across scenes");
            GridlockSceneTests.Capture("ar_hub_03_arrived_in_memory");
        }

        [UnityTest]
        public IEnumerator Cpu_TapDesk_PicksAlgorithm_ActionButtons()
        {
            SceneManager.LoadScene("GridlockCPUScheduling");
            yield return new WaitForSeconds(1.5f);
            var stage = Phone();
            Assert.IsFalse(((Behaviour)GameObject.Find("Player").GetComponent("PlayerMovement")).enabled, "nobody walks a model");
            stage.PlaceAt(Table);
            AssertOnTable(stage, "cpu");
            yield return new WaitForSeconds(1.5f);
            GridlockSceneTests.Capture("ar_cpu_01_lab");

            var actions = GameObject.Find("AR Guidance/Actions");
            Assert.IsNotNull(actions, "run / step / simulator buttons on the AR HUD");
            Assert.AreEqual(3, actions.transform.childCount);

            var srtf = GameObject.Find("SRTF").GetComponent("AlgorithmWorkstation");
            Assert.IsTrue(stage.TapAt(ScreenPoint(Centre(srtf))), "a tap on the SRTF desk");
            Assert.IsTrue(GameObject.Find("Tap Pulse/Ring").GetComponent<Renderer>().enabled, "a ring flashes under the tapped desk");
            yield return null;
            var simType = AppDomain.CurrentDomain.GetAssemblies().Select(x => x.GetType("SimulationController")).First(t => t != null);
            var sim = simType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)!.GetValue(null);
            Assert.IsNotNull(sim, "the lab's simulation");
            var algorithm = sim.GetType().GetProperty("CurrentAlgorithm")!.GetValue(sim);
            StringAssert.Contains("SRTF", algorithm.GetType().GetProperty("Name")!.GetValue(algorithm).ToString());
            var card = GameObject.Find("AR Guidance/Info Card").GetComponent<ARInfoCard>();
            StringAssert.Contains("SRTF", card.Title, "the info card names the tapped desk's algorithm");


            // Hold to focus: the model grows 2.5× around the held spot, which moves onto the anchor; holding again shows it all.
            var origin = stage.GetComponent<Unity.XR.CoreUtils.XROrigin>().transform;
            var deskPoint = Centre(srtf);
            Assert.IsTrue(stage.FocusAt(ScreenPoint(deskPoint)), "a hold on the desk zooms in");
            yield return new WaitForSecondsRealtime(0.7f);
            Assert.AreEqual(30f / 2.5f, origin.localScale.x, 0.05f, "zoomed in 2.5×");
            var onTable = origin.InverseTransformPoint(new Vector3(deskPoint.x, GameObject.Find("AR Pivot").transform.position.y, deskPoint.z));
            // (The focus is the point the ray meets on the desk, a little off its centre.)
            Assert.Less(Vector3.Distance(onTable, Table), 0.06f, "the held spot sits where the model was placed");
            GridlockSceneTests.Capture("ar_cpu_02_focused");
            stage.Unfocus();
            yield return new WaitForSecondsRealtime(0.7f);
            Assert.AreEqual(30f, origin.localScale.x, 0.05f, "back to the whole lab");
            AssertAnchored(stage);

            // Turned 60°: the right wall hides, the far ones stay.
            stage.Rotate(-60f);
            yield return null;
            yield return null;
            GridlockSceneTests.Capture("ar_cpu_03_turned");
        }

        [UnityTest]
        public IEnumerator Memory_TapBoxThenPallet_LoadsThePage()
        {
            SceneManager.LoadScene("GridlockMemory");
            yield return new WaitForSeconds(1.5f);
            var stage = Phone();
            stage.PlaceAt(Table);
            AssertOnTable(stage, "memory");
            yield return new WaitForSeconds(1f);
            GridlockSceneTests.Capture("ar_memory_01_warehouse");

            var manager = Find("MemoryPlacementManager");
            Call(manager, "SelectFIFO");
            yield return new WaitForSeconds(0.5f);
            var adapter = Find("MemoryScenarioAdapter");
            int first = Field<int[]>(manager, "referenceString")[0];
            var boxes = Field<GameObject[]>(adapter, "boxes");
            var pages = Field<int[]>(adapter, "pages");
            var box = boxes[Array.IndexOf(pages, first)];
            var lying = box.transform.position;

            Assert.IsTrue(stage.TapAt(ScreenPoint(Centre(box.transform))), "a tap on the page box");
            yield return null;
            Assert.AreEqual(box, adapter.GetType().GetProperty("Carried")!.GetValue(adapter), "the box is picked up");
            Assert.Greater(box.transform.position.y, lying.y + 0.3f, "it floats above its spot");
            var infoCard = GameObject.Find("AR Guidance/Info Card").GetComponent<ARInfoCard>();
            Assert.AreEqual($"Page {first}", infoCard.Title, "the info card names the page");
            StringAssert.Contains("Picked up", infoCard.Body, "and tells its new state");
            yield return new WaitForSeconds(0.3f);
            GridlockSceneTests.Capture("ar_memory_02_box_picked");

            var frames = Field<Transform[]>(adapter, "frames");
            // (The frame objects' order and names do not follow the frame numbers: the adapter keeps the map.)
            var firstFrame = frames[Array.IndexOf(Field<int[]>(adapter, "frameIndices"), 0)];
            Assert.IsTrue(stage.TapAt(ScreenPoint(Centre(firstFrame))), "a tap on the first frame's pallet");
            yield return new WaitForSeconds(0.8f);
            var slots = Field<Transform[]>(manager, "frameSlots");
            Assert.AreEqual(slots[0], box.transform.parent, "page loaded into frame 1");
            GridlockSceneTests.Capture("ar_memory_03_loaded");
        }
    }
}
