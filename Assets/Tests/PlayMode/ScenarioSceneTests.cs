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
        [SetUp]
        public void SkipTours()
        {
            Gridlock.Guide.NarratedTour.SetSeen("cpu", true);
            Gridlock.Guide.NarratedTour.SetSeen("memory", true);
        }

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

        static bool Skewed(Transform t)
        {
            var m = t.localToWorldMatrix;
            Vector3 x = ((Vector3)m.GetColumn(0)).normalized, y = ((Vector3)m.GetColumn(1)).normalized, z = ((Vector3)m.GetColumn(2)).normalized;
            return Mathf.Abs(Vector3.Dot(x, y)) + Mathf.Abs(Vector3.Dot(y, z)) + Mathf.Abs(Vector3.Dot(x, z)) > 0.02f;
        }

        [UnityTest]
        public IEnumerator Cpu_XR_DesksScreensAndQuizAreLaidOutCleanly()
        {
            XRModeController.ForceSimulatorInEditor = true;
            yield return Load("GridlockCPUScheduling");

            // Three desks side by side, not overlapping, with a clear view of the CPU core between them.
            var desks = new[] { "Physical_Control_Console", "Physical_Utility_Podium", "Physical_Process_Editor" }
                .Select(n => GameObject.Find(n).GetComponent<Renderer>().bounds).ToArray();
            for (int i = 0; i < desks.Length; i++)
                for (int j = i + 1; j < desks.Length; j++)
                    Assert.IsFalse(desks[i].Intersects(desks[j]), $"desks {i} and {j} overlap");

            // Nothing on the desks is skewed by a scaled parent; buttons sit on the desk tops.
            foreach (var name in new[] { "ConsoleReadout_TMP", "EditorDisplay_TMP", "ConsoleScreen", "EditorScreen",
                                          "Btn_RunPause", "Btn_Step", "Btn_Reset", "Btn_NextProc", "Btn_IncBurst", "Btn_Randomize", "Btn_Challenge" })
            {
                var t = GameObject.Find(name)?.transform;
                Assert.IsNotNull(t, name);
                Assert.IsFalse(Skewed(t), $"{name} is skewed");
                if (name.StartsWith("Btn_")) Assert.AreEqual(1.0f, t.position.y, 0.1f, $"{name} sits on its desk");
            }

            // The CPU status stands up above the core; the queue titles stand clear of the blocks.
            var status = GameObject.Find("CPU_Status_TMP").transform;
            Assert.Greater(status.position.y, 1.4f);
            Assert.Less(Vector3.Angle(status.up, Vector3.up), 1f, "the CPU status is upright");
            Assert.Greater(GameObject.Find("ReadyQueueHeader_TMP").transform.position.y, 1.1f);

            // A pressed console button comes back to its new place, not to where the arena first put it.
            var step = GameObject.Find("Btn_Step");
            var rest = step.transform.position;
            Call(step.GetComponents<MonoBehaviour>().First(b => b.GetType().GetInterface("IInteractable") != null), "Interact");
            yield return new WaitForSeconds(0.6f);
            Assert.Less(Vector3.Distance(rest, step.transform.position), 0.01f, "the button returns to its rest position");

            // The quiz: no stray START label while its button is hidden, and nothing overlaps on the wall.
            var control = GameObject.Find("ControlBtnLabel_TMP");
            var controlButton = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(t => t.name == "Btn_QuizControl");
            Assert.AreEqual(controlButton.gameObject.activeInHierarchy, control != null && control.activeInHierarchy,
                            "the control label shows exactly when its button does");
            var rows = new[] { "A", "B", "C", "D" }.Select(l => GameObject.Find("Btn_Option_" + l).GetComponent<Renderer>().bounds).ToArray();
            for (int i = 0; i + 1 < rows.Length; i++)
                Assert.IsFalse(rows[i].Intersects(rows[i + 1]), $"answer rows {i} and {i + 1} overlap");
            var feedback = GameObject.Find("FeedbackTitle_TMP").GetComponent<TMPro.TMP_Text>();
            Assert.Less(feedback.transform.position.y + 0.15f, rows[3].min.y + 0.02f, "the feedback line sits below answer D");
        }

        /// <summary>The whole quiz with the controller ray: answer, NEXT, every question, results, retry.</summary>
        [UnityTest]
        public IEnumerator Cpu_XR_QuizPlaysThroughWithTheRay()
        {
            XRModeController.ForceSimulatorInEditor = true;
            yield return Load("GridlockCPUScheduling");
            var quiz = Instance("LiveChallengeController");
            string State() => quiz.GetType().GetProperty("CurrentState")!.GetValue(quiz).ToString();
            int Index() => (int)quiz.GetType().GetProperty("CurrentQuestionIndex")!.GetValue(quiz);
            Assert.AreEqual("QuestionActive", State(), "the wall opens on the first question");

            XRSimpleInteractable Bridge(string name)
            {
                var t = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(x => x.name == name);
                var bridge = t.GetComponent<XRSimpleInteractable>();
                Assert.IsNotNull(bridge, $"{name} answers the controller ray");
                return bridge;
            }

            // Hovering an answer shows its prompt without keyboard words.
            var optionA = Bridge("Btn_Option_A");
            optionA.hoverEntered.Invoke(new UnityEngine.XR.Interaction.Toolkit.HoverEnterEventArgs());
            var prompt = GameObject.Find("XR Prompt")?.GetComponent<TMPro.TMP_Text>();
            Assert.IsNotNull(prompt, "hover prompt");
            StringAssert.DoesNotContain("[E]", prompt.text);
            StringAssert.DoesNotContain("Click", prompt.text);
            optionA.hoverExited.Invoke(new UnityEngine.XR.Interaction.Toolkit.HoverExitEventArgs());

            for (int guard = 0; State() != "Completed" && guard < 100; guard++)
            {
                int index = Index();
                Assert.AreEqual("QuestionActive", State());
                Bridge("Btn_Option_" + "ABCD"[guard % 4]).selectEntered.Invoke(new UnityEngine.XR.Interaction.Toolkit.SelectEnterEventArgs());
                yield return null;
                Assert.AreEqual("Feedback", State(), $"question {index + 1}: the trigger on an answer submits it");

                // The NEXT button was hidden while answering; it must take the ray the moment it appears.
                var next = Bridge("Btn_QuizControl");
                Assert.IsTrue(next.gameObject.activeInHierarchy, "NEXT shows after an answer");
                Assert.IsTrue(GameObject.Find("ControlBtnLabel_TMP"), "with its label");
                next.selectEntered.Invoke(new UnityEngine.XR.Interaction.Toolkit.SelectEnterEventArgs());
                yield return null;
                if (State() != "Completed") Assert.AreEqual(index + 1, Index(), "NEXT moves to the following question");
            }
            Assert.AreEqual("Completed", State(), "the quiz reaches its results");

            Bridge("Btn_QuizControl").selectEntered.Invoke(new UnityEngine.XR.Interaction.Toolkit.SelectEnterEventArgs());
            yield return null;
            Assert.AreEqual("QuestionActive", State(), "RETRY starts again");
            Assert.AreEqual(0, Index());
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

            // The original warehouse reset component is still referenced by its screen buttons. It must no longer undo
            // a valid GRIDLOCK placement after its old delayed reset timer elapses.
            Call(Find("BoxResetManager"), "ResetBoxes");
            yield return new WaitForSeconds(0.6f);
            Assert.AreEqual(slots[0], box.transform.parent, "a legacy reset cannot pull a resident page out of memory");
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
            Assert.IsEmpty(feedback.GetComponents<UnityEngine.EventSystems.BaseRaycaster>(), "the feedback card never catches the ray");

            // Every button on the warehouse screens (nested canvases included: algorithm choice, quiz) takes the XR ray.
            foreach (var button in GameObject.Find("UI").GetComponentsInChildren<UnityEngine.UI.Button>(true))
                Assert.IsNotNull(button.GetComponentInParent<Canvas>(true).GetComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>(),
                                 $"{button.transform.parent.name}/{button.name} can be pressed in the headset");
        }

        /// <summary>
        /// The right controller, posed by the test: tracking off so it stays where it is put, then moved in small steps
        /// so nothing is thrown when it lets go.
        /// </summary>
        static (NearFarInteractorHandle, XROrigin) RightHand()
        {
            var origin = Object.FindFirstObjectByType<XROrigin>();
            var controller = origin.GetComponentsInChildren<Transform>(true).First(t => t.name == "Right Controller");
            controller.gameObject.SetActive(true);
            foreach (var b in controller.GetComponents<Behaviour>()) if (b.GetType().Name.Contains("TrackedPoseDriver")) b.enabled = false;
            // The hand model follows the simulator's own grip pose; here it must follow the posed controller.
            foreach (var g in controller.GetComponentsInChildren<ControllerGripPose>(true)) g.enabled = false;
            return (new NearFarInteractorHandle(controller), origin);
        }

        sealed class NearFarInteractorHandle
        {
            public readonly Transform Controller;
            public readonly UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor Interactor;
            public NearFarInteractorHandle(Transform controller)
            {
                Controller = controller;
                Interactor = controller.GetComponentInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor>(true);
            }

            public IEnumerator MoveTo(Vector3 target)
            {
                var from = Controller.position;
                for (int i = 1; i <= 12; i++)
                {
                    Controller.position = Vector3.Lerp(from, target, i / 12f);
                    yield return new WaitForFixedUpdate();
                }
                for (int i = 0; i < 6; i++) yield return new WaitForFixedUpdate(); // still: no throw on release
            }
        }

        static IEnumerator Carry(NearFarInteractorHandle hand, XRGrabInteractable box, Vector3 dropAt)
        {
            var manager = Object.FindFirstObjectByType<UnityEngine.XR.Interaction.Toolkit.XRInteractionManager>();
            yield return hand.MoveTo(box.transform.position + Vector3.up * 0.2f);
            manager.SelectEnter((UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor)hand.Interactor, box);
            yield return null;
            Assert.IsTrue(box.isSelected, $"{box.name} is in hand");
            yield return hand.MoveTo(dropAt);
            manager.SelectExit((UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor)hand.Interactor, box);
            yield return new WaitForSeconds(0.6f);
        }

        [UnityTest]
        public IEnumerator Memory_XR_GrabBoxes_DropOnFrames()
        {
            XRModeController.ForceSimulatorInEditor = true;
            yield return Load("GridlockMemory");
            var placement = Find("MemoryPlacementManager");
            Call(placement, "SelectFIFO");
            var adapter = Find("MemoryScenarioAdapter");
            var boxes = Field<GameObject[]>(adapter, "boxes");
            var pages = Field<int[]>(adapter, "pages");
            var slots = Field<Transform[]>(placement, "frameSlots");
            var zones = Field<MonoBehaviour[]>(adapter, "zones").OrderBy(z => (int)z.GetType().GetProperty("FrameIndex")!.GetValue(z)).ToArray();
            Bounds Volume(int frame) => (Bounds)zones[frame].GetType().GetProperty("DropVolume")!.GetValue(zones[frame]);
            XRGrabInteractable Box(int page, int nth = 0) => boxes.Where((b, i) => pages[i] == page).ElementAt(nth).GetComponent<XRGrabInteractable>();

            var (hand, _) = RightHand();
            Assert.IsNotNull(hand.Interactor, "the right controller has a near-far interactor");

            // A hand closing on a box finds it (it used to sit on a physics layer the hands could not feel).
            var page1 = Box(1);
            yield return hand.MoveTo(page1.transform.position + Vector3.up * 0.2f);
            var targets = new System.Collections.Generic.List<UnityEngine.XR.Interaction.Toolkit.Interactables.IXRInteractable>();
            hand.Interactor.GetValidTargets(targets);
            CollectionAssert.Contains(targets, page1, "the hand next to the box can grab it");

            // Held boxes sit in the hand: the grab point is the hand model's palm, close by or pulled in with the ray.
            var interactionManager = Object.FindFirstObjectByType<UnityEngine.XR.Interaction.Toolkit.XRInteractionManager>();
            var selector = (UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor)hand.Interactor;
            var palm = hand.Controller.GetComponentsInChildren<Transform>(true).First(t => t.name == "Palm Attach");
            Assert.Less(Vector3.Distance(palm.position, hand.Interactor.attachTransform.position), 0.02f, "the controller grabs at the hand's palm");
            float GapToPalm(XRGrabInteractable b) =>
                b.GetComponentsInChildren<Collider>().Min(c => Vector3.Distance(c.ClosestPoint(hand.Interactor.attachTransform.position), hand.Interactor.attachTransform.position));
            var table1 = page1.transform.position;
            interactionManager.SelectEnter(selector, page1);
            for (int i = 0; i < 10; i++) yield return new WaitForFixedUpdate();
            Assert.Less(GapToPalm(page1), 0.06f, "a box grabbed close by is in the palm");
            interactionManager.SelectExit(selector, page1);
            yield return new WaitForSeconds(2f); // put down near its table: it goes back to its spot
            yield return hand.MoveTo(page1.transform.position + new Vector3(0f, 0.5f, -1.6f));
            hand.Controller.rotation = Quaternion.LookRotation(page1.transform.position - hand.Controller.position);
            interactionManager.SelectEnter(selector, page1);
            for (int i = 0; i < 40; i++) yield return new WaitForFixedUpdate();
            Assert.Less(GapToPalm(page1), 0.06f, "a box grabbed with the ray comes into the palm");
            interactionManager.SelectExit(selector, page1);
            for (float t = 0f; t < 4f && Vector3.Distance(page1.transform.position, table1) >= 0.05f; t += Time.deltaTime) yield return null;

            // Released in front of frame 1's rack, at chest height (the pallet itself is above the head): loaded.
            var chest = Volume(0).center;
            chest.y = 1.2f;
            chest.z = Volume(0).min.z + 0.3f;
            yield return Carry(hand, page1, chest);
            Assert.AreEqual(slots[0], page1.transform.parent, "page 1 is loaded into frame 1");
            Assert.IsFalse(page1.enabled, "a page in memory stays on its pallet");

            // Page 2 on frame 3 while frame 2 is the first empty one: wrong, and the box goes back to its table.
            var page2 = Box(2);
            var table = page2.transform.position;
            var frame3 = Volume(2).center;
            yield return Carry(hand, page2, frame3);
            Assert.AreNotEqual(slots[2], page2.transform.parent, "the wrong frame refuses the page");
            for (float t = 0f; t < 3f && Vector3.Distance(page2.transform.position, table) >= 0.05f; t += Time.deltaTime) yield return null;
            Assert.Less(Vector3.Distance(page2.transform.position, table), 0.05f, "and the box is back on its table");

            // Dropped on the floor away from the rack: back on the table after a moment.
            var floor = Object.FindFirstObjectByType<XROrigin>().transform.position + Vector3.up * 1.0f + Vector3.right * 0.8f;
            yield return Carry(hand, page2, floor);
            for (float t = 0f; t < 6f && Vector3.Distance(page2.transform.position, table) >= 0.05f; t += Time.deltaTime) yield return null;
            Assert.Less(Vector3.Distance(page2.transform.position, table), 0.05f, "a box on the floor returns to its table");

            // The right frame this time.
            yield return Carry(hand, page2, Volume(1).center);
            Assert.AreEqual(slots[1], page2.transform.parent, "page 2 is loaded into frame 2");
        }
    }
}
