using System.Collections;
using System.Linq;
using Gridlock.Core;
using Gridlock.Hub;
using Gridlock.XR;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Gridlock.Tests
{
    /// <summary>
    /// The phone presentation driven by simulated finger touches: HUD buttons through the touch UI module, tap to
    /// select a car, drag to orbit, pinch to zoom, and the hub keypad launching the deadlock scene. Touch events are
    /// queued so they arrive in the player loop's input update, as they do on a phone.
    /// </summary>
    public class PhoneModeTests : InputTestFixture
    {
        /// <summary>These tests expect the classic scripted demo, not the first-visit guided tour.</summary>
        [SetUp]
        public void SkipGuidedTour() => Gridlock.Guide.GuideTour.Seen = Gridlock.Guide.BankerTour.Seen = true;

        Touchscreen screen;

        public override void Setup()
        {
            base.Setup();
            screen = InputSystem.AddDevice<Touchscreen>();
            TouchGestures.ForceTouch = true;
        }

        public override void TearDown()
        {
            TouchGestures.ForceTouch = false;
            if (EnhancedTouchSupport.enabled) EnhancedTouchSupport.Disable();
            base.TearDown();
        }

        IEnumerator Tap(Vector2 position, int finger = 1)
        {
            BeginTouch(finger, position, queueEventOnly: true);
            yield return null;
            yield return null;
            EndTouch(finger, position, queueEventOnly: true);
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator Lot_TouchUI_Tap_Drag_Pinch()
        {
            SceneManager.LoadScene("GridlockParkingLot");
            yield return null;
            yield return new WaitForSeconds(1f);

            var events = Object.FindAnyObjectByType<EventSystem>();
            Assert.IsNotNull(events.GetComponent<InputSystemUIInputModule>(), "touch-aware UI module on a phone");
            var help = Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None).Single(t => t.name == "Help");
            StringAssert.StartsWith("Tap a car", help.text);

            var cam = Camera.main;
            Assert.AreEqual("Desktop Camera", cam.name);
            var interaction = Object.FindAnyObjectByType<PlayerInteraction>();

            // Tap the first car that is on screen, clear of the docked HUD, and not hidden behind something else.
            Vector2? carPoint = null;
            foreach (var car in Object.FindObjectsByType<CarView>(FindObjectsSortMode.None))
            {
                var p = (Vector2)cam.WorldToScreenPoint(car.transform.position + Vector3.up * 0.5f);
                if (p.x < 20 || p.x > Screen.width * 0.55f || p.y < 60 || p.y > Screen.height - 60) continue;
                if (Physics.Raycast(cam.ScreenPointToRay(p), out var hit, 200f) &&
                    hit.collider.GetComponentInParent<CarView>() == car &&
                    hit.collider.GetComponentInParent<Inspectable>() != null)
                {
                    carPoint = p;
                    break;
                }
            }
            Assert.IsTrue(carPoint.HasValue, "a tappable car is in view");
            yield return Tap(carPoint.Value);
            Assert.IsNotNull(interaction.SelectedCar, "a tap selects the car");

            // One finger drag orbits.
            var before = cam.transform.rotation;
            var start = new Vector2(Screen.width * 0.25f, Screen.height * 0.5f);
            BeginTouch(1, start, queueEventOnly: true);
            yield return null;
            for (int i = 1; i <= 10; i++)
            {
                MoveTouch(1, start + new Vector2(i * 20f, 0f), queueEventOnly: true);
                yield return null;
            }
            EndTouch(1, start + new Vector2(200f, 0f), queueEventOnly: true);
            yield return null;
            Assert.Greater(Quaternion.Angle(before, cam.transform.rotation), 5f, "drag orbits the camera");

            // Two fingers spreading apart zoom in.
            var focus = cam.transform.position + cam.transform.forward * 30f;
            float far = Vector3.Distance(cam.transform.position, focus);
            var mid = new Vector2(Screen.width * 0.3f, Screen.height * 0.5f);
            BeginTouch(1, mid - Vector2.right * 40f, queueEventOnly: true);
            BeginTouch(2, mid + Vector2.right * 40f, queueEventOnly: true);
            yield return null;
            for (int i = 1; i <= 10; i++)
            {
                MoveTouch(1, mid - Vector2.right * (40f + i * 12f), queueEventOnly: true);
                MoveTouch(2, mid + Vector2.right * (40f + i * 12f), queueEventOnly: true);
                yield return null;
            }
            EndTouch(1, mid - Vector2.right * 160f, queueEventOnly: true);
            EndTouch(2, mid + Vector2.right * 160f, queueEventOnly: true);
            yield return null;
            Assert.Less(Vector3.Distance(cam.transform.position, focus), far - 2f, "pinch out zooms in");
        }

        [UnityTest]
        public IEnumerator Hub_TouchKeypad_LaunchesDeadlock()
        {
            SceneManager.LoadScene("GridlockHub");
            yield return null;
            yield return new WaitForSeconds(4.5f); // boot sequence

            var pad = GameObject.Find("TouchKeypad");
            Assert.IsNotNull(pad, "keypad shown on a phone");
            var hubScreen = Object.FindFirstObjectByType<HubScreen>();
            Vector2 Key(string label)
            {
                var corners = new Vector3[4]; // overlay canvas: world space is screen pixels
                ((RectTransform)GameObject.Find($"Key {label}").transform).GetWorldCorners(corners);
                return (corners[0] + corners[2]) * 0.5f;
            }

            yield return Tap(Key("►"));
            Assert.AreEqual(1, hubScreen.FocusedIndex, "► on the keypad moves the focus");
            yield return new WaitForSeconds(0.3f);
            yield return Tap(Key("1"));
            Assert.AreEqual(0, hubScreen.FocusedIndex, "1 jumps back to Deadlock");
            yield return new WaitForSeconds(0.3f);
            yield return Tap(Key("START"));
            for (float t = 0f; t < 10f && SceneManager.GetActiveScene().name != "GridlockParkingLot"; t += Time.unscaledDeltaTime)
                yield return null;
            Assert.AreEqual("GridlockParkingLot", SceneManager.GetActiveScene().name, "START pulls the player into the lot");
        }
    }
}
