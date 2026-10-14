using System;
using System.Collections;
using System.IO;
using System.Linq;
using Gridlock.Core;
using Gridlock.Deadlock;
using Gridlock.XR;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Gridlock.Tests
{
    /// <summary>
    /// End-to-end run of GridlockParkingLot: cars drive in, the four-car circular wait forms, it is detected and
    /// highlighted, Analyze mode draws the wait-for graph, and Restart clears everything.
    /// Set GRIDLOCK_CAPTURE_DIR to also save screenshots of each stage.
    /// </summary>
    public class GridlockSceneTests
    {
        /// <summary>These tests expect the classic scripted demo, not the first-visit guided tour.</summary>
        [SetUp]
        public void SkipGuidedTour() => Gridlock.Guide.GuideTour.Seen = true;

        const string SceneName = "GridlockParkingLot";
        static readonly string CaptureDir = Environment.GetEnvironmentVariable("GRIDLOCK_CAPTURE_DIR");

        [SetUp]
        public void SetUp() => LogAssert.ignoreFailingMessages = true; // OpenXR logs errors when no headset runtime exists

        [TearDown]
        public void TearDown() => Time.timeScale = 1f;

        [UnityTest]
        public IEnumerator FourCarScenario_DeadlocksAndAnalyzes()
        {
            SceneManager.LoadScene(SceneName);
            yield return null;
            yield return null;

            var sim = UnityEngine.Object.FindFirstObjectByType<SimulationManager>();
            var overlay = sim.GetComponent<AnalysisOverlay>();
            Assert.AreEqual(4, sim.State.Cars.Count);
            Assert.AreEqual(6, sim.State.Resources.Count);
            Capture("01_start");

            Time.timeScale = 4f;
            yield return WaitFor(() => sim.State.Cars.Count(c => sim.State.GetHeld(c).Count > 0) == 2, 60f);
            yield return new WaitForSeconds(0.6f);
            Capture("02_driving");

            yield return WaitFor(() => sim.LastResult.HasDeadlock && !sim.CarsMoving, 180f);
            Time.timeScale = 1f;
            yield return new WaitForSeconds(0.3f);

            CollectionAssert.AreEquivalent(new[] { "A", "B", "C", "D" }, sim.LastResult.InvolvedCars);
            Assert.AreEqual("A → B → C → D → A", sim.LastResult.DescribeCycles().Single());
            foreach (var id in new[] { "P1", "P2", "P3", "P4" })
                Assert.AreEqual(sim.GetSpace(id).ParkingPoint.x, sim.GetCar(sim.State.GetOwner(id)).transform.position.x, 0.1f,
                                $"car owning {id} should be parked in it");
            Capture("03_deadlock");
            Capture("03b_board", new Vector3(0f, 7.6f, 0.5f), new Vector3(0f, 7.7f, 13.6f), 50f);

            overlay.SetActive(true);
            yield return null;
            yield return null;
            Assert.AreEqual(4, overlay.Current.CycleEdges.Count);
            Capture("04_analyze");
            Capture("04b_analyze_board", new Vector3(0f, 7.6f, 0.5f), new Vector3(0f, 7.7f, 13.6f), 50f);
            Capture("05_vr_eye", new Vector3(0f, 1.6f, -10.9f), new Vector3(0f, 2.5f, 2f), 90f);
            Capture("06_podium", new Vector3(0f, 1.65f, -10.9f), new Vector3(1.25f, 1.1f, -10f), 70f);

            overlay.SetActive(false);
            overlay.Inspect(null, "P2");
            yield return null;
            Capture("07_inspect_space", new Vector3(-4f, 4f, -6f), new Vector3(0f, 3f, 6.9f), 60f);
            overlay.Inspect(null, null);

            // Recovery by preemption: tow D. C moves into P4, freeing P3 for B, freeing P2 for A.
            var interaction = sim.GetComponent<PlayerInteraction>();
            interaction.Select(Inspectable.Kind.Car, "D");
            Assert.AreEqual("D", interaction.SelectedCar);
            yield return null;
            Capture("08_selected");
            interaction.TowSelected();
            Assert.IsTrue(sim.PlayerControlled);
            Assert.IsFalse(sim.LastResult.HasDeadlock, "towing a car in the cycle breaks the deadlock");

            Time.timeScale = 4f;
            yield return WaitFor(() => !sim.CarsMoving && sim.State.Cars.All(c => !sim.State.IsWaiting(c) && sim.State.GetHeld(c).Count <= 1)
                                       && sim.State.GetOwner("P2") == "A", 120f);
            Assert.AreEqual("A", sim.State.GetOwner("P2"));
            Assert.AreEqual("B", sim.State.GetOwner("P3"));
            Assert.AreEqual("C", sim.State.GetOwner("P4"));
            Assert.IsNull(sim.State.GetOwner("P1"));
            Assert.IsEmpty(sim.State.GetHeld("D"));

            // The learner drives D back in by hand.
            interaction.Select(Inspectable.Kind.Car, "D");
            interaction.Select(Inspectable.Kind.Space, "P1");
            Assert.IsNull(interaction.SelectedCar);
            yield return WaitFor(() => !sim.CarsMoving && sim.GetCar("D").CurrentSpace == sim.GetSpace("P1"), 120f);
            Time.timeScale = 1f;
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual("D", sim.State.GetOwner("P1"));
            Capture("09_recovered");

            sim.Restart();
            yield return null;
            Assert.IsFalse(sim.LastResult.HasDeadlock);
            Assert.IsFalse(overlay.IsActive);
            Assert.IsTrue(sim.State.Resources.All(r => sim.State.GetOwner(r) == null));
        }

        static IEnumerator WaitFor(Func<bool> condition, float timeoutSeconds)
        {
            float start = Time.realtimeSinceStartup;
            while (!condition())
            {
                if (Time.realtimeSinceStartup - start > timeoutSeconds) Assert.Fail("Timed out waiting for scenario state");
                yield return null;
            }
        }

        /// <summary>Renders the main camera, or a temporary camera at the given pose, to a PNG.</summary>
        internal static void Capture(string name, Vector3? position = null, Vector3? lookAt = null, float fov = 60f)
        {
            if (string.IsNullOrEmpty(CaptureDir)) return;
            Directory.CreateDirectory(CaptureDir);

            var main = Camera.main;
            Camera cam = main;
            GameObject temp = null;
            if (position.HasValue)
            {
                temp = new GameObject("Capture Camera");
                cam = temp.AddComponent<Camera>();
                cam.CopyFrom(main);
                cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
                cam.fieldOfView = fov;
                cam.transform.position = position.Value;
                cam.transform.LookAt(lookAt.Value);
            }

            // Overlay canvases are not part of Camera.Render; draw the desktop HUD through the camera for main-camera shots.
            var overlays = position.HasValue ? new Canvas[0]
                : UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
            foreach (var c in overlays) { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = 1f; }

            var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            cam.Render();
            foreach (var c in overlays) c.renderMode = RenderMode.ScreenSpaceOverlay;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(CaptureDir, name + ".png"), tex.EncodeToPNG());
            cam.targetTexture = null;
            RenderTexture.active = null;
            UnityEngine.Object.Destroy(tex);
            rt.Release();
            if (temp != null) UnityEngine.Object.Destroy(temp);
        }
    }
}
