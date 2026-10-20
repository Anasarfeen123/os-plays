using System.Collections;
using System.Linq;
using Gridlock.XR;
using NUnit.Framework;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEngine.UI;

namespace Gridlock.Tests
{
    /// <summary>
    /// Every scene in the headset (XR Interaction Simulator): everything that can be grabbed or pressed is on an interaction
    /// layer the rig's hands and controllers use, every teleport floor answers the teleport ray, every face-button
    /// shortcut is wired to something that exists, and the hands have no colliders that would bump into what they hold.
    /// </summary>
    public class XRControlsAuditTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            XRModeController.ForceSimulatorInEditor = false;
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

        [UnityTest]
        public IEnumerator EverythingIsReachable(
            [Values("GridlockHub", "GridlockParkingLot", "GridlockBanker", "GridlockCPUScheduling", "GridlockMemory")] string scene)
        {
            Gridlock.Guide.GuideTour.Seen = Gridlock.Guide.BankerTour.Seen = true;
            XRModeController.ForceSimulatorInEditor = true;
            SceneManager.LoadScene(scene);
            yield return null;
            yield return new WaitForSeconds(1.5f);

            var origin = Object.FindFirstObjectByType<XROrigin>();
            Assert.IsNotNull(origin, $"{scene}: XR rig");
            var nearFar = origin.GetComponentsInChildren<NearFarInteractor>(true);
            Assert.IsNotEmpty(nearFar, $"{scene}: hands/controllers can point and grab");
            int reach = nearFar.Aggregate(0, (mask, i) => mask | i.interactionLayers.value);
            var teleporters = origin.GetComponentsInChildren<XRRayInteractor>(true).Where(r => r.name.Contains("Teleport")).ToArray();
            int teleportReach = teleporters.Aggregate(0, (mask, i) => mask | i.interactionLayers.value);

            foreach (var interactable in Object.FindObjectsByType<XRBaseInteractable>(FindObjectsSortMode.None))
            {
                if (interactable.GetComponentInParent<XROrigin>() != null) continue;
                bool teleport = interactable is BaseTeleportationInteractable;
                int mask = teleport ? teleportReach : reach;
                Assert.AreNotEqual(0, interactable.interactionLayers.value & mask,
                    $"{scene}: {interactable.name} ({interactable.GetType().Name}) is on interaction layers the rig never uses");
                Assert.IsNotEmpty(interactable.colliders, $"{scene}: {interactable.name} has no collider to hit");
            }

            var keys = origin.GetComponentInChildren<XRButtonShortcuts>(true);
            foreach (var e in new[] { keys.ButtonA, keys.ButtonB, keys.ButtonX, keys.ButtonY, keys.MenuButton })
                for (int i = 0; i < e.GetPersistentEventCount(); i++)
                    Assert.IsNotNull(e.GetPersistentTarget(i), $"{scene}: a face button points at a missing object ({e.GetPersistentMethodName(i)})");

            foreach (var side in new[] { "Left Hand Model", "Right Hand Model" })
            {
                var hand = origin.GetComponentsInChildren<Transform>(true).First(t => t.name == side);
                Assert.IsEmpty(hand.GetComponentsInChildren<Collider>(true), $"{scene}: {side} must not push what it holds");
            }

            // A caption or decorative frame must never be the first ray hit. The shared UI repair leaves hit targets on
            // actual controls only, and gives every world-space control canvas an XR raycaster.
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                                         .Where(c => c.rootCanvas.renderMode == RenderMode.WorldSpace))
            {
                bool Own(Component component) => component.GetComponentInParent<Canvas>(true) == canvas;
                bool hasControl = canvas.GetComponentsInChildren<Selectable>(true).Any(Own);
                if (hasControl)
                    Assert.IsNotNull(canvas.GetComponent<TrackedDeviceGraphicRaycaster>(),
                                     $"{scene}: {canvas.name} controls accept a controller ray");

                foreach (var graphic in canvas.GetComponentsInChildren<Graphic>(true).Where(Own))
                    if (graphic.GetComponentInParent<Selectable>(true) == null)
                        Assert.IsFalse(graphic.raycastTarget,
                                       $"{scene}: display-only {graphic.name} cannot block a button behind it");
            }
        }
    }
}
