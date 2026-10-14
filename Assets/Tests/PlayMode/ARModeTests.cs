using System;
using System.Collections;
using System.Linq;
using Gridlock.AR;
using Gridlock.Banker;
using Gridlock.Core;
using Gridlock.Guide;
using Gridlock.XR;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Gridlock.Tests
{
    /// <summary>
    /// Phone AR in the editor (no camera feed; the test stands in for ARCore's plane hit): the lot waits until it is placed
    /// on a surface, appears there as a miniature in front of the phone, tours and the demo start only then, pinch/twist
    /// keep it anchored, and tapping a car in the miniature selects it.
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
        }

        [TearDown]
        public void TearDown()
        {
            XRModeController.ForceARInEditor = false;
            ScenePlacement.Ready = true;
            Time.timeScale = 1f;
            GuideTour.Seen = BankerTour.Seen = true;
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
    }
}
