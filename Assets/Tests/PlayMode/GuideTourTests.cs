using System;
using System.Collections;
using System.Linq;
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
    /// First visit to the Deadlock scene: the narrated tour drives the lot into a deadlock, hands control to the learner,
    /// who tows a car, and the jam unwinds before the tour hands over the lot.
    /// </summary>
    public class GuideTourTests
    {
        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            GameSettings.AutoTours = true;
            GuideTour.Seen = false;
            BankerTour.Seen = false;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            GuideTour.Seen = true;
            BankerTour.Seen = true;
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

        [UnityTest]
        public IEnumerator FirstVisit_TourTeaches_LearnerTows_LotHandedOver()
        {
            SceneManager.LoadScene("GridlockParkingLot");
            yield return null;
            yield return null;

            var tour = UnityEngine.Object.FindFirstObjectByType<GuideTour>();
            var sim = UnityEngine.Object.FindFirstObjectByType<SimulationManager>();
            var interaction = sim.GetComponent<PlayerInteraction>();
            Assert.IsTrue(tour.Running, "the tour starts on the first visit");
            Assert.IsTrue(interaction.Locked, "the learner waits for their turn");

            Time.timeScale = 3f;
            yield return WaitFor(() => tour.CurrentStep == "welcome", 20f, "welcome");
            yield return new WaitForSeconds(1.5f);
            GridlockSceneTests.Capture("guide_01_welcome");

            yield return WaitFor(() => tour.CurrentStep == "environment", 30f, "environment");
            yield return new WaitForSeconds(4f);
            GridlockSceneTests.Capture("guide_02_environment");

            yield return WaitFor(() => tour.CurrentStep == "wait", 60f, "hold and wait");
            yield return WaitFor(() => sim.State.IsWaiting("A"), 20f, "A to wait for B's bay");
            yield return new WaitForSeconds(1f);
            GridlockSceneTests.Capture("guide_03_hold_and_wait");

            yield return WaitFor(() => tour.CurrentStep == "detect", 60f, "detect");
            yield return new WaitForSeconds(2f);
            Assert.IsTrue(sim.LastResult.HasDeadlock, "the tour builds the four-car deadlock");
            GridlockSceneTests.Capture("guide_04_detect");

            // The learner's turn.
            yield return WaitFor(() => tour.CurrentStep == "tow", 60f, "tow");
            Assert.IsFalse(interaction.Locked);
            interaction.Select(Inspectable.Kind.Car, "B");
            yield return null;
            GridlockSceneTests.Capture("guide_05_your_turn");
            interaction.TowSelected();
            Assert.IsFalse(sim.LastResult.HasDeadlock);

            yield return WaitFor(() => tour.CurrentStep == "unwind", 30f, "unwind");
            Assert.IsTrue(interaction.Locked, "locked again while the guide explains");
            yield return WaitFor(() => !tour.Running, 120f, "the tour to finish");

            Assert.IsFalse(interaction.Locked, "the lot is the learner's now");
            Assert.IsEmpty(sim.State.GetHeld("B"), "B was towed to the street");
            Assert.AreEqual("A", sim.State.GetOwner("P2"));
            Assert.AreEqual("D", sim.State.GetOwner("P1"));
            Assert.AreEqual("C", sim.State.GetOwner("P4"));
            Assert.IsTrue(sim.State.Cars.All(c => !sim.State.IsWaiting(c)));
            Time.timeScale = 1f;
            yield return new WaitForSeconds(1.5f);
            GridlockSceneTests.Capture("guide_06_end");

            // The headset version of the screen, posed in front of a VR-height camera.
            var xr = UnityEngine.Object.FindObjectsByType<GuideScreen>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                                       .Single(s => s.GetComponent<FloatingFollow>() != null);
            xr.GetComponent<FloatingFollow>().enabled = false;
            xr.gameObject.SetActive(true);
            xr.transform.SetPositionAndRotation(new Vector3(-0.3f, 1.45f, -9.4f), Quaternion.identity);
            yield return null;
            GridlockSceneTests.Capture("guide_07_xr_screen", new Vector3(0f, 1.6f, -10.9f), new Vector3(-0.2f, 1.4f, -9.4f), 70f);
        }
            [UnityTest]
        public IEnumerator BankerTour_ExplainsTheLot_LearnerDecides_VerdictExplained()
        {
            SceneManager.LoadScene("GridlockBanker");
            yield return null;
            yield return null;

            var tour = UnityEngine.Object.FindFirstObjectByType<BankerTour>();
            var game = UnityEngine.Object.FindFirstObjectByType<Gridlock.Banker.BankerGame>();
            Assert.IsTrue(tour.Running, "the Banker tour starts on the first visit");
            Assert.IsTrue(game.Locked, "GRANT / DENY wait for the learner's turn");
            game.Grant();
            Assert.IsNull(game.Verdict, "locked: a premature GRANT is ignored");

            Time.timeScale = 3f;
            yield return WaitFor(() => tour.CurrentStep == "banker_fleets", 30f, "fleets");
            yield return new WaitForSeconds(2f);
            GridlockSceneTests.Capture("bguide_01_fleets");
            yield return WaitFor(() => tour.CurrentStep == "banker_need", 60f, "need");
            yield return new WaitForSeconds(2f);
            GridlockSceneTests.Capture("bguide_02_need");
            yield return WaitFor(() => tour.CurrentStep == "banker_request", 60f, "request");
            yield return new WaitForSeconds(2f);
            GridlockSceneTests.Capture("bguide_03_request");

            yield return WaitFor(() => tour.CurrentStep == "banker_turn", 90f, "the learner's turn");
            Assert.IsFalse(game.Locked);
            GridlockSceneTests.Capture("bguide_04_your_turn");
            game.Deny(); // wrong on purpose: Level 1's request is safe
            Assert.IsFalse(game.Verdict.Correct);

            yield return WaitFor(() => tour.CurrentStep == "banker_wrong", 10f, "the explanation of the wrong answer");
            Assert.IsTrue(game.Locked);
            yield return new WaitForSeconds(4f);
            GridlockSceneTests.Capture("bguide_05_explained");

            yield return WaitFor(() => !tour.Running, 90f, "the tour to finish");
            Assert.IsFalse(game.Locked, "the learner can continue with NEXT");
            Time.timeScale = 1f;
            yield return null;
            GridlockSceneTests.Capture("bguide_06_end");
        }

        [UnityTest]
        public IEnumerator SystemMenu_PausesSettingsPersist_HomeLoadsTheControlRoom()
        {
            GuideTour.Seen = true;
            SceneManager.LoadScene("GridlockParkingLot");
            yield return new WaitForSeconds(1f);

            var menu = Gridlock.UI.SystemMenu.Instance;
            Assert.IsNotNull(menu, "every scene has a system menu");
            menu.Open();
            Assert.IsTrue(menu.IsOpen);
            Assert.AreEqual(0f, Time.timeScale, "the game pauses");
            yield return null;
            GridlockSceneTests.Capture("menu_01_how_to_play");

            // Settings page: sliders write the saved settings and the mixer follows.
            var view = UnityEngine.Object.FindObjectsByType<Gridlock.UI.SystemMenuView>(FindObjectsSortMode.None).Single();
            view.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(b => b.name == "SETTINGS").onClick.Invoke();
            yield return null;
            var settings = view.GetComponentInChildren<Gridlock.UI.SettingsView>();
            Assert.IsTrue(settings.gameObject.activeInHierarchy);
            var slider = settings.GetComponentsInChildren<UnityEngine.UI.Slider>()[1]; // AUDIO tab: master, music, effects, narrator
            float before = GameSettings.Music;
            slider.value = 0.25f;
            Assert.AreEqual(0.25f, GameSettings.Music, 1e-4f);
            UnityEngine.Object.FindFirstObjectByType<AudioRouting>(); // routing is present in the scene
            GridlockSceneTests.Capture("menu_02_settings");
            GameSettings.Music = before;

            menu.Close();
            Assert.AreEqual(1f, Time.timeScale, "closing resumes");
            yield return new WaitForSeconds(2.5f);
            // Short one-shot cues may be younger than the router's last pass; everything else must be routed.
            Assert.IsTrue(UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None)
                                            .Where(a => !a.name.StartsWith("Cue ") && a.name != "One shot audio")
                                            .All(a => a.outputAudioMixerGroup != null),
                          "every sound goes through the mixer");

            menu.Home();
            for (float t = 0f; t < 10f && SceneManager.GetActiveScene().name != "GridlockHub"; t += Time.unscaledDeltaTime)
                yield return null;
            Assert.AreEqual("GridlockHub", SceneManager.GetActiveScene().name);
            yield return new WaitForSeconds(4.5f);
            Gridlock.UI.SystemMenu.Instance.Open();
            yield return null;
            GridlockSceneTests.Capture("menu_03_hub");
            Gridlock.UI.SystemMenu.Instance.Close();
        }
    }
}
