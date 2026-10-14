using System.Collections;
using System.Linq;
using System.Reflection;
using Gridlock.UI;
using Gridlock.XR;
using NUnit.Framework;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Gridlock.Tests
{
    /// <summary>
    /// The imported scenarios (CPU scheduling lab, memory warehouse) as GRIDLOCK scenes, on a monitor and in the headset
    /// (XR Interaction Simulator). Their scripts live in Assembly-CSharp, so they are reached by name and reflection.
    /// </summary>
    public class ScenarioSceneTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            XRModeController.ForceSimulatorInEditor = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            foreach (var behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (behaviour != null && behaviour.GetType().Name.Contains("Simulator")) Object.Destroy(behaviour.gameObject);
            yield return null;
            SceneManager.LoadScene("GridlockHub", LoadSceneMode.Single);
            yield return null;
            var hands = new System.Collections.Generic.List<UnityEngine.XR.Hands.XRHandSubsystem>();
            SubsystemManager.GetSubsystems(hands);
            foreach (var subsystem in hands) subsystem.Stop();
            yield return null;
        }

        static MonoBehaviour Find(string typeName) =>
            Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(b => b.GetType().Name == typeName);

        static MonoBehaviour Instance(string typeName)
        {
            var type = System.AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(typeName)).First(t => t != null);
            return (MonoBehaviour)type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)!.GetValue(null);
        }

        static object Call(object target, string method, params object[] args) =>
            target.GetType().GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(target, args);

        static T Field<T>(object target, string field) =>
            (T)target.GetType().GetField(field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(target);

        static IEnumerator Load(string scene)
        {
            SceneManager.LoadScene(scene);
            yield return null;
            yield return new WaitForSeconds(1.5f);
        }

        [UnityTest]
        public IEnumerator Cpu_Desktop_WorkstationSelectsAlgorithm_MenuFreesCursor()
        {
            yield return Load("GridlockCPUScheduling");
            Assert.IsFalse(XRModeController.Current.XRActive);
            var player = GameObject.Find("Player");
            Assert.IsNotNull(player, "first-person player on a monitor");
            Assert.AreEqual(player.transform, Camera.main.transform.parent, "the player's camera is the view");
            Assert.IsNotNull(SystemMenu.Instance, "menu in the lab");
            foreach (var part in new[] { "PhysicalArenaConsole", "PhysicalCPUCore", "PhysicalReadyQueue" })
                Assert.IsNotNull(Find(part), $"the arena is assembled ({part})");

            var sim = Instance("SimulationController");
            var processes = (System.Collections.IList)sim.GetType().GetProperty("InitialProcesses")!.GetValue(sim);
            Assert.Greater(processes.Count, 0, "the lab has processes to schedule");
            var algorithm = sim.GetType().GetProperty("CurrentAlgorithm")!.GetValue(sim);
            var all = (System.Collections.IList)algorithm.GetType().GetProperty("AllProcesses")!.GetValue(algorithm);
            Assert.AreEqual(processes.Count, all.Count, "the chosen algorithm runs those processes (not an empty list)");

            var fcfs = GameObject.Find("FCFS")?.GetComponent("AlgorithmWorkstation");
            Assert.IsNotNull(fcfs, "FCFS workstation");
            Call(fcfs, "Interact");
            yield return null;
            algorithm = sim.GetType().GetProperty("CurrentAlgorithm")!.GetValue(sim);
            StringAssert.Contains("FCFS", algorithm.GetType().GetProperty("Name")!.GetValue(algorithm).ToString());

            // (Batchmode has no window to lock the cursor in, so the player's look state is checked instead.)
            var movement = player.GetComponent("PlayerMovement");
            bool Looking() => Field<bool>(movement, "controlActive");
            Assert.IsTrue(Looking());
            SystemMenu.Instance.Open();
            yield return null;
            yield return null;
            Assert.IsFalse(Looking(), "the menu stops the mouse look and frees the cursor");
            SystemMenu.Instance.Close();
            yield return null;
            yield return null;
            Assert.IsTrue(Looking(), "closing it hands the mouse back to the look");
        }

        [UnityTest]
        public IEnumerator Cpu_XR_InteractablesTakeTheRay_ButtonsWired()
        {
            XRModeController.ForceSimulatorInEditor = true;
            yield return Load("GridlockCPUScheduling");
            Assert.IsTrue(XRModeController.Current.XRActive);
            var origin = Object.FindFirstObjectByType<XROrigin>();
            Assert.AreEqual("Player", origin.tag, "the challenge board moves the XR player");
            Assert.IsFalse(GameObject.Find("Player"), "the desktop player is off");

            var bridge = GameObject.Find("FCFS").GetComponent<XRSimpleInteractable>();
            Assert.IsNotNull(bridge, "the workstation answers the controller ray");
            Assert.Greater(bridge.colliders.Count, 0);

            foreach (var side in new[] { "Left", "Right" })
            {
                var hand = origin.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == side + " Hand Model");
                Assert.IsNotNull(hand, $"{side} animated hand");
                Assert.IsNotNull(hand.GetComponentInChildren<ControllerHandAnimator>(true));
            }

            var keys = origin.GetComponentInChildren<XRButtonShortcuts>(true);
            Assert.AreEqual(nameof(ScenarioButtons.PressA), keys.ButtonA.GetPersistentMethodName(0));
            var sim = Instance("SimulationController");
            keys.ButtonA.Invoke();
            yield return null;
            Assert.IsTrue((bool)sim.GetType().GetProperty("IsRunning")!.GetValue(sim), "A runs the simulation");

            keys.ButtonX.Invoke();
            yield return null;
            var ui = Find("SchedulingUIController");
            Assert.IsTrue(ui.isActiveAndEnabled, "X opens the simulator window");
            Assert.AreEqual(RenderMode.WorldSpace, ui.GetComponent<Canvas>().renderMode, "as a floating panel in the headset");
        }

        [UnityTest]
        public IEnumerator Memory_Desktop_CarryBoxToFrame()
        {
            yield return Load("GridlockMemory");
            Assert.IsFalse(XRModeController.Current.XRActive);
            Assert.IsNull(GameObject.Find("XR Origin (VR)"), "the scenario's own rig is gone");
            var manager = Find("MemoryPlacementManager");
            Call(manager, "SelectFIFO");
            var adapter = Find("MemoryScenarioAdapter");
            int first = Field<int[]>(manager, "referenceString")[0];
            var boxes = Field<GameObject[]>(adapter, "boxes");
            var pages = Field<int[]>(adapter, "pages");
            var box = boxes[System.Array.IndexOf(pages, first)];

            Call(adapter, "PickUp", box);
            Assert.AreEqual(Camera.main.transform, box.transform.parent, "the box is carried in front of the camera");
            Call(adapter, "Place", 0);
            var slots = Field<Transform[]>(manager, "frameSlots");
            Assert.AreEqual(slots[0], box.transform.parent, "page loaded into frame 1");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Memory_XR_RigReplacesTheirs_BoxesGrabbable()
        {
            XRModeController.ForceSimulatorInEditor = true;
            yield return Load("GridlockMemory");
            Assert.IsTrue(XRModeController.Current.XRActive);
            Assert.AreEqual(1, Object.FindObjectsByType<XROrigin>(FindObjectsSortMode.None).Length, "one rig");
            Assert.AreEqual(1, Object.FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.XRInteractionManager>(FindObjectsSortMode.None).Length);
            var adapter = Find("MemoryScenarioAdapter");
            foreach (var box in Field<GameObject[]>(adapter, "boxes"))
            {
                Assert.IsNotNull(box.GetComponent<XRGrabInteractable>(), $"{box.name} grabbable");
                Assert.IsNull(box.GetComponent("MemoryBoxTarget"), "no desktop pick-up in VR");
            }
            var feedback = GameObject.Find("PlayerFeedbackUI");
            yield return new WaitForSeconds(0.5f);
            Assert.Less(Vector3.Distance(feedback.transform.position, Camera.main.transform.position), 3f, "feedback panel follows the headset");
        }
    }
}
