using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Gridlock.Core;
using Gridlock.Guide;
using Gridlock.XR;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Gridlock.Tests
{
    /// <summary>
    /// First visits to the imported scenarios: the CPU lab tour introduces the lab, hands RUN to the learner and explains
    /// the result; the memory tour has the learner load the first page, then guides the frames full until FIFO evicts.
    /// The tours run sped up (their timing follows Time.time).
    /// </summary>
    public class ScenarioTourTests
    {
        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            GameSettings.AutoTours = true;
            NarratedTour.SetSeen("cpu", false);
            NarratedTour.SetSeen("memory", false);
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            NarratedTour.SetSeen("cpu", true);
            NarratedTour.SetSeen("memory", true);
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

        static NarratedTour Tour(string typeName) =>
            Object.FindObjectsByType<NarratedTour>(FindObjectsSortMode.None).FirstOrDefault(t => t.GetType().Name == typeName);

        static MonoBehaviour Find(string typeName) =>
            Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(b => b.GetType().Name == typeName);

        static object Get(object target, string property) =>
            target.GetType().GetProperty(property, BindingFlags.Public | BindingFlags.Instance)!.GetValue(target);

        static object Call(object target, string method, params object[] args) =>
            target.GetType().GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(target, args);

        [UnityTest]
        public IEnumerator Cpu_FirstVisit_TourHandsOverRun_ExplainsTheResult()
        {
            SceneManager.LoadScene("GridlockCPUScheduling");
            yield return null;
            yield return null;
            var tour = Tour("CpuTour");
            Assert.IsNotNull(tour, "the lab has a guided tour");
            Assert.IsTrue(tour.Running, "it starts on the first visit");
            Assert.IsTrue(Gridlock.UI.SystemMenu.Instance.HasTour, "the menu offers GUIDED TOUR");

            Time.timeScale = 4f;
            yield return WaitFor(() => tour.CurrentStep == "cpu_processes", 30f, "the processes step");
            yield return WaitFor(() => tour.CurrentStep == "cpu_turn", 90f, "the learner's turn");
            Time.timeScale = 1f;
            yield return new WaitForSeconds(0.5f);
            GridlockSceneTests.Capture("tour_cpu_01_your_turn");

            Object.FindFirstObjectByType<ScenarioButtons>().PressA(); // the learner presses RUN
            yield return WaitFor(() => tour.CurrentStep == "cpu_gantt", 10f, "the Gantt step once the run starts");
            var simType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("SimulationController")).First(t => t != null);
            var sim = simType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)!.GetValue(null);
            Assert.IsNotNull(sim, "the lab's simulation");
            Time.timeScale = 4f;
            yield return WaitFor(() => tour.CurrentStep == "cpu_result", 60f, "the result");
            var algorithm = Get(sim, "CurrentAlgorithm");
            Assert.IsNotNull(algorithm, "an algorithm ran");
            Assert.IsTrue((bool)Get(algorithm, "IsCompleted"), "the run finished before the guide explains it");
            yield return WaitFor(() => !tour.Running, 60f, "the end of the tour");
            Time.timeScale = 1f;
            GridlockSceneTests.Capture("tour_cpu_02_end");
        }

        [UnityTest]
        public IEnumerator Memory_FirstVisit_LearnerLoadsAPage_TourShowsFifoEvicting()
        {
            SceneManager.LoadScene("GridlockMemory");
            yield return null;
            yield return null;
            var tour = Tour("MemoryTour");
            Assert.IsNotNull(tour, "the warehouse has a guided tour");
            Assert.IsTrue(tour.Running, "it starts on the first visit");
            var welcome = Find("WelcomeSequenceController");
            Assert.IsFalse((bool)Get(welcome, "WelcomeShowing"), "the tour replaces the old welcome voice-over");

            Time.timeScale = 4f;
            yield return WaitFor(() => tour.CurrentStep == "mem_turn", 90f, "the learner's turn");
            Time.timeScale = 1f;
            GridlockSceneTests.Capture("tour_memory_01_your_turn");

            var manager = Find("MemoryPlacementManager");
            var adapter = Find("MemoryScenarioAdapter");
            int page = (int)Get(manager, "NextPage");
            var box = (GameObject)Call(manager, "BoxFor", page);
            Call(adapter, "PickUp", box);
            Call(adapter, "Place", (int)Get(manager, "EmptyFrame"));
            yield return WaitFor(() => tour.CurrentStep == "mem_fault", 10f, "the page-fault step after the learner's move");

            // Nobody moves now: the guide loads the next pages itself until FIFO has to evict.
            Time.timeScale = 6f;
            yield return WaitFor(() => tour.CurrentStep == "mem_victim", 180f, "the eviction");
            Assert.Greater((int)Get(manager, "ReferenceIndex"), (int)Get(manager, "FrameCount"), "the frames filled and one more page came in");
            Assert.GreaterOrEqual((int)Get(manager, "Faults"), 4);
            yield return WaitFor(() => !tour.Running, 60f, "the end of the tour");
            Time.timeScale = 1f;
            GridlockSceneTests.Capture("tour_memory_02_end");
        }
    }
}
