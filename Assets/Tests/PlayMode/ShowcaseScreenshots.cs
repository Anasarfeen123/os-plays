using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Gridlock.Core;
using Gridlock.Deadlock;
using Gridlock.Guide;
using Gridlock.UI;
using Gridlock.XR;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Gridlock.Tests
{
    /// <summary>
    /// Presentation shots of every scene: scenery from several angles, the scenarios in action (CPU lab running, quiz
    /// states, memory pages being placed) and the headset view with its menu. Writes 1920×1080 PNGs into
    /// GRIDLOCK_CAPTURE_DIR/&lt;scene&gt;/. Explicit: run with -testFilter Gridlock.Tests.ShowcaseScreenshots.
    /// </summary>
    [Explicit]
    public class ShowcaseScreenshots
    {
        static readonly string Root = Environment.GetEnvironmentVariable("GRIDLOCK_CAPTURE_DIR");
        string folder;
        int index;

        [SetUp]
        public void SetUp()
        {
            // A filter like "Gridlock" also selects explicit tests: without a folder to write to, there is nothing to do.
            if (string.IsNullOrEmpty(Root)) Assert.Ignore("Set GRIDLOCK_CAPTURE_DIR to take the showcase shots.");
            LogAssert.ignoreFailingMessages = true;
            GuideTour.Seen = BankerTour.Seen = true;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            XRModeController.ForceSimulatorInEditor = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            foreach (var b in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (b != null && b.GetType().Name.Contains("Simulator")) Object.Destroy(b.gameObject);
            yield return null;
            var hands = new System.Collections.Generic.List<UnityEngine.XR.Hands.XRHandSubsystem>();
            SubsystemManager.GetSubsystems(hands);
            foreach (var subsystem in hands) subsystem.Stop();
            yield return null;
        }

        IEnumerator Load(string scene, string shotFolder, bool xr = false, float settle = 3f)
        {
            XRModeController.ForceSimulatorInEditor = xr;
            folder = Path.Combine(Root, shotFolder);
            Directory.CreateDirectory(folder);
            index = Directory.GetFiles(folder, "*.png").Length;
            SceneManager.LoadScene(scene);
            yield return null;
            yield return new WaitForSecondsRealtime(settle);
        }

        // ---- Capture ----

        /// <summary>From a free camera at <paramref name="position"/> looking at <paramref name="target"/> (no HUD).</summary>
        void Shot(string name, Vector3 position, Vector3 target, float fov = 60f) =>
            Render(name, position, Quaternion.LookRotation(target - position), fov, withHud: false);

        /// <summary>Exactly what the player sees: the main camera, with the desktop HUD drawn in.</summary>
        void View(string name)
        {
            var cam = Camera.main;
            Render(name, cam.transform.position, cam.transform.rotation, cam.stereoEnabled ? 90f : cam.fieldOfView, withHud: true);
        }

        /// <summary>Around <paramref name="objectName"/>'s bounds: from <paramref name="direction"/>, far enough to frame it.</summary>
        void Around(string name, string objectName, Vector3 direction, float margin = 1.3f, float fov = 55f, float lift = 0f)
        {
            var go = GameObject.Find(objectName);
            if (go == null) { Debug.LogWarning($"[SHOWCASE] no {objectName}"); return; }
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            float radius = Mathf.Max(b.extents.magnitude, 0.3f);
            float distance = radius * margin / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
            var target = b.center + Vector3.up * lift;
            Shot(name, target + direction.normalized * distance, target, fov);
        }

        static bool UnderSimulator(Transform t)
        {
            for (; t != null; t = t.parent)
                if (t.name.Contains("Simulator") || t.GetComponents<MonoBehaviour>().Any(b => b != null && b.GetType().Name.Contains("Simulator")))
                    return true;
            return false;
        }

        void Render(string name, Vector3 position, Quaternion rotation, float fov, bool withHud)
        {
            var source = Camera.main;
            var go = new GameObject("Showcase Camera");
            var cam = go.AddComponent<Camera>();
            cam.CopyFrom(source);
            cam.stereoTargetEye = StereoTargetEyeMask.None;
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = Mathf.Max(cam.farClipPlane, 400f);
            go.transform.SetPositionAndRotation(position, rotation);
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;

            var overlays = withHud
                ? Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay && !UnderSimulator(c.transform)).ToArray()
                : new Canvas[0];
            foreach (var c in overlays) { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = 0.5f; }

            var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
            cam.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            RenderTexture.active = null;
            foreach (var c in overlays) c.renderMode = RenderMode.ScreenSpaceOverlay;
            File.WriteAllBytes(Path.Combine(folder, $"{++index:00}_{name}.png"), tex.EncodeToPNG());
            cam.targetTexture = null;
            Object.Destroy(tex);
            rt.Release();
            Object.Destroy(go);
        }

        // ---- Scenario access (imported scenarios live in Assembly-CSharp) ----

        static MonoBehaviour Find(string typeName) =>
            Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(b => b.GetType().Name == typeName);

        /// <summary>The scenario singleton (its scene also holds inactive copies, which <see cref="Find"/> could return).</summary>
        static MonoBehaviour Instance(string typeName)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(typeName)).First(t => t != null);
            return (MonoBehaviour)type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)!.GetValue(null);
        }

        static object Call(object target, string method, params object[] args) =>
            target.GetType().GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(target, args);

        static object Get(object target, string property) =>
            target.GetType().GetProperty(property, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(target);

        static void Interact(string objectName)
        {
            var go = GameObject.Find(objectName);
            var target = go?.GetComponents<MonoBehaviour>().FirstOrDefault(b => b.GetType().GetInterface("IInteractable") != null);
            if (target != null) Call(target, "Interact");
        }

        static bool PressButton(string label)
        {
            var button = Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                .FirstOrDefault(b => b.isActiveAndEnabled && b.GetComponentInChildren<TMP_Text>()?.text.Trim() == label);
            if (button == null) return false;
            button.onClick.Invoke();
            return true;
        }

        IEnumerator HeadsetViews(string scene, string shotFolder, Vector3? look = null)
        {
            yield return Load(scene, shotFolder + "/headset", xr: true, settle: scene == "GridlockHub" ? 9f : 4f);
            var cam = Camera.main;
            View("eye_view");
            if (look.HasValue)
                Shot("eye_view_turned", cam.transform.position, look.Value, 90f);
            SystemMenu.Instance.Open();
            yield return null;
            yield return null;
            View("menu");
            SystemMenu.Instance.Close();
        }

        // ---- Scenes ----

        [UnityTest]
        public IEnumerator Hub()
        {
            yield return Load("GridlockHub", "1_Hub", settle: 9f); // past the monitor's boot sequence
            View("seated_at_the_console");
            Shot("console_close_up", new Vector3(0f, 1.25f, -0.55f), new Vector3(0f, 1.0f, 0.3f), 55f);
            Shot("control_room_wide", new Vector3(3.2f, 2.6f, -3.4f), new Vector3(-0.3f, 0.9f, 0.4f), 75f);
            Shot("control_room_from_behind", new Vector3(0f, 2.2f, -5.5f), new Vector3(0f, 1.1f, 1f), 70f);
            Shot("desk_and_keyboard", new Vector3(-0.5f, 1.15f, -0.75f), new Vector3(-0.6f, 0.78f, 0.05f), 60f);
            Shot("hologram_projector", new Vector3(-0.6f, 1.4f, -0.9f), new Vector3(-1.1f, 1.1f, -0.3f), 55f);
            Shot("window_to_space", new Vector3(-1.2f, 1.5f, -0.5f), new Vector3(-12f, 8f, 60f), 70f);
            Shot("left_wing", new Vector3(0.5f, 1.5f, -1.0f), new Vector3(-6f, 1.2f, 0.5f), 75f);
            Shot("right_wing", new Vector3(-0.5f, 1.5f, -0.8f), new Vector3(7f, 1.2f, 0.5f), 75f);
            Shot("server_racks", new Vector3(-1.2f, 1.6f, -3.6f), new Vector3(-2.5f, 1.4f, -6.7f), 60f);
            Shot("lounge", new Vector3(-2.2f, 1.6f, 1.4f), new Vector3(-6f, 0.6f, 5.5f), 70f);
            Shot("showcase_corner", new Vector3(1.5f, 1.7f, -0.5f), new Vector3(4.4f, 1.2f, 1.6f), 65f);
            Shot("ceiling_and_plaques", new Vector3(0f, 1.4f, -3f), new Vector3(0f, 3.2f, 3f), 75f);
            Shot("from_the_corner", new Vector3(7.5f, 4.3f, -6.5f), new Vector3(-1f, 0.5f, 1.5f), 80f);
            Shot("bird's_eye", new Vector3(0f, 4.6f, -2.5f), new Vector3(0f, 0f, 0.8f), 95f);
            yield return HeadsetViews("GridlockHub", "1_Hub");
        }

        [UnityTest]
        public IEnumerator Deadlock()
        {
            yield return Load("GridlockParkingLot", "2_Deadlock_ParkingLot");
            View("player_view_with_hud");
            Shot("lot_overview", new Vector3(0f, 26f, -31f), new Vector3(0f, 0f, 3f), 55f);
            Shot("aerial_city", new Vector3(-30f, 35f, -40f), Vector3.zero, 60f);
            Shot("top_down_bays", new Vector3(0f, 32f, 0.5f), new Vector3(0f, 0f, 0.6f), 50f);
            Shot("street_level", new Vector3(19f, 2f, -20f), new Vector3(12f, 1f, 15f), 75f);
            Shot("standing_at_the_podium", new Vector3(0f, 1.6f, -10.9f), new Vector3(0f, 2f, 5f), 85f);
            Shot("scoreboard", new Vector3(0f, 7.6f, 0.5f), new Vector3(0f, 7.7f, 13.6f), 50f);
            Shot("cars_queued_on_the_road", new Vector3(-5f, 3f, 6f), new Vector3(-12f, 1f, 20f), 65f);
            Around("car_close_up", "Car_A", new Vector3(1f, 0.5f, -1f), 1.2f);
            Shot("bays_low_angle", new Vector3(7f, 0.6f, -9f), new Vector3(-1f, 0.5f, 4f), 70f);

            // Let the cars drive in and gridlock.
            var sim = Object.FindFirstObjectByType<SimulationManager>();
            Time.timeScale = 4f;
            yield return new WaitForSecondsRealtime(3f);
            Shot("cars_arriving", new Vector3(-14f, 12f, -14f), new Vector3(-2f, 0f, 3f), 60f);
            for (float t = 0f; t < 150f && !(sim.LastResult.HasDeadlock && !sim.CarsMoving); t += Time.unscaledDeltaTime)
                yield return null;
            Time.timeScale = 1f;
            yield return new WaitForSeconds(0.5f);
            View("deadlock_player_view");
            Shot("deadlock_from_above", new Vector3(0f, 22f, -18f), new Vector3(0f, 0f, 1f), 55f);
            Shot("deadlock_scoreboard", new Vector3(0f, 7.6f, 0.5f), new Vector3(0f, 7.7f, 13.6f), 50f);
            Shot("deadlock_ground_level", new Vector3(6f, 1.7f, -9f), new Vector3(-1f, 1f, 2f), 70f);
            yield return HeadsetViews("GridlockParkingLot", "2_Deadlock_ParkingLot", new Vector3(0f, 7f, 13.6f));
        }

        [UnityTest]
        public IEnumerator Banker()
        {
            yield return Load("GridlockBanker", "3_Bankers_Algorithm", settle: 6f);
            View("player_view_with_hud");
            Shot("lot_overview", new Vector3(0f, 26f, -31f), new Vector3(0f, 0f, 3f), 55f);
            Shot("fleets_waiting", new Vector3(-2f, 9f, -4f), new Vector3(-12f, 0f, 16f), 60f);
            Shot("zones_and_bays", new Vector3(8f, 10f, -10f), new Vector3(-3f, 0f, 1f), 60f);
            Shot("scoreboard_matrices", new Vector3(0f, 7.6f, 0.5f), new Vector3(0f, 7.7f, 13.6f), 50f);
            Shot("manager_podium_view", new Vector3(0f, 1.6f, -10.9f), new Vector3(-2f, 2.5f, 2f), 85f);
            Around("fleet_A_car", "FleetA_1", new Vector3(1f, 0.6f, -1f), 1.2f);
            Around("fleet_B_car", "FleetB_1", new Vector3(1f, 0.6f, -1f), 1.2f);
            Shot("street_level", new Vector3(-14f, 1.8f, -6f), new Vector3(-12f, 1f, 18f), 70f);
            Shot("aerial", new Vector3(30f, 35f, -40f), Vector3.zero, 60f);
            yield return HeadsetViews("GridlockBanker", "3_Bankers_Algorithm", new Vector3(0f, 7f, 13.6f));
        }

        [UnityTest]
        public IEnumerator CpuScheduling()
        {
            yield return Load("GridlockCPUScheduling", "4_CPU_Scheduling", settle: 4f);
            View("player_view_with_hud");
            Shot("lab_overview", new Vector3(-6f, 3.4f, -8f), new Vector3(0f, 0.8f, 1.5f), 70f);
            Shot("lab_from_above", new Vector3(0f, 9.5f, -7.5f), new Vector3(0f, 0f, 1.5f), 70f);
            Shot("three_desks", new Vector3(0f, 1.9f, -5.6f), new Vector3(0f, 1f, -2.8f), 65f);
            Around("control_console_desk", "Physical_Control_Console_Panel", new Vector3(0f, 0.9f, -1f), 1.1f, 50f);
            Around("process_editor_desk", "Physical_Process_Editor_Panel", new Vector3(0f, 0.9f, -1f), 1.1f, 50f);
            Shot("cpu_core_and_ready_queue", new Vector3(0f, 3f, -3.8f), new Vector3(0f, 0.6f, 0.8f), 60f);
            Shot("algorithm_workstations", new Vector3(0f, 2.2f, 1.5f), new Vector3(0f, 1f, 5.8f), 75f);
            Around("fcfs_workstation", "FCFS", new Vector3(0.3f, 0.7f, -1f), 1.3f);
            Shot("gantt_teaching_display", new Vector3(0f, 2.6f, 4.8f), new Vector3(0f, 2.8f, 9.7f), 70f);
            Shot("challenge_wall", new Vector3(-6.2f, 2.3f, 4.3f), new Vector3(-9.7f, 2.3f, 4.3f), 70f);

            // Pick SRTF at its desk, then run the simulation.
            Interact("SRTF");
            yield return new WaitForSeconds(0.5f);
            Around("srtf_selected", "SRTF", new Vector3(0.3f, 0.7f, -1f), 1.3f);
            var sim = Instance("SimulationController");
            Call(sim, "StartSimulation");
            Time.timeScale = 2f;
            yield return new WaitForSeconds(5f);
            Shot("running_core_and_queue", new Vector3(0f, 3f, -3.8f), new Vector3(0f, 0.6f, 0.8f), 60f);
            Shot("running_lab", new Vector3(-6f, 3.4f, -8f), new Vector3(0f, 0.8f, 1.5f), 70f);
            yield return new WaitForSeconds(5f);
            Shot("running_gantt_chart", new Vector3(0f, 2.6f, 4.8f), new Vector3(0f, 2.8f, 9.7f), 70f);
            Shot("running_from_console", new Vector3(-2.7f, 1.9f, -4.4f), new Vector3(0f, 0.7f, 0.8f), 70f);
            yield return new WaitForSeconds(8f);
            Time.timeScale = 1f;
            Shot("finished_gantt_chart", new Vector3(0f, 2.6f, 4.8f), new Vector3(0f, 2.8f, 9.7f), 70f);
            Shot("finished_lab", new Vector3(0f, 9.5f, -7.5f), new Vector3(0f, 0f, 1.5f), 70f);

            // The simulator window (U on a keyboard, X on a controller).
            var toggle = Find("SchedulingUIToggle");
            if (toggle != null)
            {
                Call(toggle, "ToggleUI");
                yield return new WaitForSeconds(0.6f);
                View("simulator_window");
                Call(toggle, "ToggleUI");
                yield return null;
            }

            // The quiz wall: a question, a right answer, a wrong answer, the results.
            var quiz = Instance("LiveChallengeController");
            Vector3 wallEye = new Vector3(-6.4f, 2.4f, 4.3f), wall = new Vector3(-9.7f, 2.4f, 4.3f);
            Shot("quiz_question", wallEye, wall, 65f);
            Call(quiz, "SelectOption", 1); // Round Robin: correct
            yield return new WaitForSeconds(0.4f);
            Shot("quiz_correct_answer", wallEye, wall, 65f);
            Call(quiz, "AdvanceNextQuestion");
            yield return null;
            Shot("quiz_question_2", wallEye, wall, 65f);
            for (int option = 0; option < 4; option++)
            {
                Call(quiz, "SelectOption", option);
                yield return null;
                if (Get(quiz, "CurrentState").ToString() == "Feedback") break;
            }
            yield return new WaitForSeconds(0.4f);
            Shot("quiz_feedback_2", wallEye, wall, 65f);
            for (int guard = 0; guard < 40 && Get(quiz, "CurrentState").ToString() != "Completed"; guard++)
            {
                if (Get(quiz, "CurrentState").ToString() == "Feedback") Call(quiz, "AdvanceNextQuestion");
                else Call(quiz, "SelectOption", guard % 4);
                yield return null;
            }
            yield return new WaitForSeconds(0.4f);
            Shot("quiz_results", wallEye, wall, 65f);

            yield return HeadsetViews("GridlockCPUScheduling", "4_CPU_Scheduling", new Vector3(-9.7f, 2.3f, 4.3f));
            Shot("headset_quiz_wall", wallEye + Vector3.right * 0.5f, wall, 70f);
        }

        [UnityTest]
        public IEnumerator Memory()
        {
            yield return Load("GridlockMemory", "5_Memory_Management", settle: 4f);
            View("welcome_screen");
            for (int i = 0; i < 6 && PressButton("Continue"); i++)
            {
                yield return new WaitForSecondsRealtime(0.6f);
                View($"tutorial_step_{i + 1}");
            }
            Shot("warehouse_overview", new Vector3(14.5f, 4.8f, 0.6f), new Vector3(6.5f, 1.2f, 7.5f), 70f);
            Shot("storage_aisle", new Vector3(7.6f, 2.2f, 4.2f), new Vector3(7.6f, 2.2f, 10.5f), 75f);
            Shot("frames_on_the_rack", new Vector3(7.6f, 2.0f, 6.6f), new Vector3(7.6f, 1.9f, 10.1f), 65f);
            Shot("page_boxes_on_the_table", new Vector3(6.4f, 2.3f, 1.0f), new Vector3(6.4f, 1.3f, 2.8f), 60f);
            Shot("packing_area", new Vector3(1.2f, 2.4f, 4.5f), new Vector3(4.5f, 0.8f, 1.8f), 70f);
            Shot("shipping_dock", new Vector3(9.5f, 2.4f, 4.5f), new Vector3(12.6f, 1.2f, 1.5f), 70f);
            Shot("from_the_rack_back", new Vector3(7.6f, 3.4f, 9.4f), new Vector3(6.4f, 1.0f, 2.5f), 75f);

            // FIFO, then the reference string page by page.
            var manager = Find("MemoryPlacementManager");
            var algorithm = manager.GetType().GetNestedType("Algorithm");
            Call(manager, "SelectAlgorithm", Enum.Parse(algorithm, "FIFO"));
            yield return new WaitForSeconds(1f);
            View("fifo_selected");
            for (int step = 0; step < 6; step++)
            {
                int page = (int)Call(manager, "CurrentPage");
                var box = GameObject.Find("Box" + page);
                if (box == null) break;
                bool placed = false;
                for (int frame = 0; frame < 3 && !placed; frame++)
                {
                    for (float t = 0f; t < 3f && (bool)manager.GetType().GetField("processing", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(manager); t += Time.deltaTime)
                        yield return null;
                    placed = (bool)Call(manager, "TryPlaceBox", box, frame);
                    if (!placed && step == 4 && frame == 0)
                    {
                        yield return new WaitForSeconds(0.3f);
                        View("wrong_frame_explained");
                    }
                }
                yield return new WaitForSeconds(1.2f);
                Shot($"step_{step + 1}_page_{page}_on_the_rack", new Vector3(7.6f, 2.0f, 6.6f), new Vector3(7.6f, 1.9f, 10.1f), 65f);
                if (step == 3) View("page_fault_replacement");
            }
            yield return new WaitForSeconds(1f);
            View("algorithm_complete");
            Shot("final_frames_wide", new Vector3(12.5f, 3.2f, 4.5f), new Vector3(7.2f, 1.8f, 10f), 65f);
            yield return HeadsetViews("GridlockMemory", "5_Memory_Management", new Vector3(7.6f, 1.9f, 10.1f));
        }
    }
}
