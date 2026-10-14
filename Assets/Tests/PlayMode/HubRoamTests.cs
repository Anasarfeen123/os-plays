using System;
using System.Collections;
using System.Linq;
using Gridlock.Core;
using Gridlock.Hub;
using Gridlock.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gridlock.Tests
{
    /// <summary>Walking the control room on the desktop, and the tabbed settings applying to the scene.</summary>
    public class HubRoamTests
    {
        [SetUp]
        public void SetUp() => LogAssert.ignoreFailingMessages = true;

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            GameSettings.ResetToDefaults();
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

        static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        [UnityTest]
        public IEnumerator Desktop_StandUp_Walk_Collide_ClimbToGallery_SitDown()
        {
            SceneManager.LoadScene("GridlockHub");
            yield return new WaitForSeconds(4.5f);
            var roamer = UnityEngine.Object.FindFirstObjectByType<HubRoamer>();
            var seat = roamer.transform.position;
            Assert.IsFalse(roamer.Roaming, "the hub starts seated at the desk");

            // Walk over to the deadlock hologram: its label fades in.
            var holo = new Vector3(-4.6f, 0f, -1.3f); // a couple of metres from the hologram
            roamer.WalkTo(holo);
            Assert.IsTrue(roamer.Roaming);
            yield return WaitFor(() => Flat(roamer.transform.position, holo) < 0.4f, 20f, "walking to the hologram");
            var plaque = UnityEngine.Object.FindObjectsByType<HubPlaque>(FindObjectsSortMode.None)
                                     .Single(p => p.name.Contains("HOLOGRAM")).GetComponent<TMP_Text>();
            yield return new WaitForSeconds(0.8f);
            Assert.Greater(plaque.alpha, 0.5f, "the nearest exhibit's label shows up close");
            Assert.IsTrue(UnityEngine.Object.FindObjectsByType<HubPlaque>(FindObjectsSortMode.None)
                                     .Count(p => p.GetComponent<TMP_Text>().alpha > 0.05f) == 1, "one label at a time");
            roamer.LookAt(180f, 12f); // turn to the hologram
            yield return null;
            GridlockSceneTests.Capture("hub_roam_01_hologram");

            // Walls stop you.
            roamer.WalkTo(new Vector3(-14f, 0f, -2.2f));
            yield return new WaitForSeconds(4f);
            Assert.Greater(roamer.transform.position.x, -8.4f, "cannot walk through the wall");

            // Up the stairs onto the gallery.
            roamer.WalkTo(new Vector3(0f, 0f, -1.8f));
            yield return WaitFor(() => Flat(roamer.transform.position, new Vector3(0f, 0f, -1.8f)) < 0.4f, 25f, "the foot of the stairs");
            roamer.WalkTo(new Vector3(0f, 0.72f, -6.0f));
            yield return WaitFor(() => roamer.transform.position.y > 2.2f && roamer.transform.position.z < -5.2f, 20f, "climbing to the gallery");
            roamer.LookAt(0f, 16f); // the view over the room
            yield return null;
            GridlockSceneTests.Capture("hub_roam_02_gallery");

            roamer.Sit();
            yield return new WaitForSeconds(1.2f);
            Assert.IsFalse(roamer.Roaming);
            Assert.Less(Vector3.Distance(roamer.transform.position, seat), 0.05f, "back in the chair");
        }

        [UnityTest]
        public IEnumerator Settings_Tabs_ApplyToTheScene_AndReset()
        {
            Gridlock.Guide.GuideTour.Seen = true;
            SceneManager.LoadScene("GridlockParkingLot");
            yield return new WaitForSeconds(1f);
            SystemMenu.Instance.Open();
            yield return null;
            var view = UnityEngine.Object.FindObjectsByType<SystemMenuView>(FindObjectsSortMode.None).Single();
            Button Named(string name) => view.GetComponentsInChildren<Button>(true).First(b => b.name == name);
            Named("SETTINGS").onClick.Invoke();
            yield return null;
            GridlockSceneTests.Capture("settings_01_audio");

            Named("TabDISPLAY").onClick.Invoke();
            yield return null;
            Named("OFF").onClick.Invoke(); // the first OFF on the page: post effects
            yield return null;
            Assert.IsFalse(GameSettings.IsOn(Setting.PostEffects));
            Assert.IsFalse(Camera.main.GetComponent<UniversalAdditionalCameraData>().renderPostProcessing, "post effects switched off live");
            GridlockSceneTests.Capture("settings_02_display");

            Named("TabCONTROLS").onClick.Invoke();
            yield return null;
            Named("90°").onClick.Invoke();
            Assert.AreEqual(90f, GameSettings.SnapDegrees);
            GridlockSceneTests.Capture("settings_03_controls");

            Named("TabGAMEPLAY").onClick.Invoke();
            yield return null;
            Named("FAST").onClick.Invoke();
            Assert.AreEqual(1.6f, GameSettings.CarSpeedMultiplier, 1e-4f);
            GridlockSceneTests.Capture("settings_04_gameplay");

            Named("ResetAll").onClick.Invoke();
            yield return null;
            Assert.IsTrue(GameSettings.IsOn(Setting.PostEffects));
            Assert.AreEqual(45f, GameSettings.SnapDegrees);
            Assert.AreEqual(1f, GameSettings.CarSpeedMultiplier, 1e-4f);
            SystemMenu.Instance.Close();
        }
    }
}
