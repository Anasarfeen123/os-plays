using System.Collections;
using System.Linq;
using Gridlock.Hub;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Gridlock.Tests
{
    /// <summary>
    /// End-to-end runs of the shipped scenes (desktop presentation, since tests run without a headset).
    /// Any error or exception logged during a run fails it.
    /// </summary>
    public class HubFlowTests
    {
        [UnityTest]
        public IEnumerator Hub_Boots_And_DeadlockTile_PullsIntoParkingLot()
        {
            SceneManager.LoadScene("GridlockHub");
            yield return null;

            var tiles = Object.FindObjectsByType<HubTile>(FindObjectsSortMode.None);
            Assert.AreEqual(3, tiles.Length, "three scenario tiles");
            Assert.IsTrue(tiles.All(t => !t.Locked), "all three scenarios are open");
            CollectionAssert.AreEquivalent(new[] { "GridlockParkingLot", "GridlockCPUScheduling", "GridlockMemory" }, tiles.Select(t => t.SceneName));
            var deadlock = tiles.Single(t => t.SceneName == "GridlockParkingLot");

            // Boot: CRT open, typing, tiles slide in.
            yield return new WaitForSeconds(4.5f);
            Assert.That(deadlock.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f).Within(0.01f), "tiles revealed after boot");

            var keys = Object.FindObjectsByType<HubKey>(FindObjectsSortMode.None);
            HubKey Key(HubKey.KeyAction action, int slot = 0) =>
                keys.Single(k => k.Action == action && (action != HubKey.KeyAction.Slot || k.Slot == slot));
            var screen = Object.FindFirstObjectByType<HubScreen>();
            Assert.AreEqual(0, screen.FocusedIndex, "Deadlock has focus after boot");

            Key(HubKey.KeyAction.Right).Press();
            Assert.AreEqual(1, screen.FocusedIndex, "► moves the focus");
            yield return new WaitForSeconds(0.3f);

            Key(HubKey.KeyAction.Slot, 0).Press(); // key 1 jumps back to Deadlock
            Assert.AreEqual(0, screen.FocusedIndex);
            yield return new WaitForSeconds(0.3f);
            Key(HubKey.KeyAction.Enter).Press();
            for (float t = 0f; t < 10f && SceneManager.GetActiveScene().name != "GridlockParkingLot"; t += Time.unscaledDeltaTime)
                yield return null;
            Assert.AreEqual("GridlockParkingLot", SceneManager.GetActiveScene().name, "pulled into the deadlock scene");

            yield return new WaitForSeconds(3f); // fade-in and the scripted demo start
        }

        [UnityTest]
        public IEnumerator BankerScene_Runs()
        {
            SceneManager.LoadScene("GridlockBanker");
            yield return new WaitForSeconds(3f);
            Assert.AreEqual("GridlockBanker", SceneManager.GetActiveScene().name);
        }
    }
}
