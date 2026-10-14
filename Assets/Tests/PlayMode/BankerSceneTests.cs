using System.Collections;
using System.Linq;
using Gridlock.Banker;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Gridlock.Tests
{
    /// <summary>
    /// End-to-end run of GridlockBanker: the five guided levels (one answered wrongly on purpose), the lot showing
    /// Allocation/Need/Request as cars, a safe grant driving cars into bays, then a random challenge.
    /// </summary>
    public class BankerSceneTests
    {
        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            Gridlock.Guide.BankerTour.Seen = true; // these tests play the levels directly
        }

        [TearDown]
        public void TearDown() => Time.timeScale = 1f;

        static bool CorrectAnswer(BankerChallenge c) =>
            BankerAlgorithm.Evaluate(c.State, c.Process, c.Request).Outcome == RequestOutcome.Safe;

        static int ActiveCars(string fleet) =>
            Object.FindObjectsByType<Core.CarView>(FindObjectsSortMode.None).Count(v => v.Id == fleet);

        [UnityTest]
        public IEnumerator GuidedLevels_JudgedByTheRealAlgorithm()
        {
            SceneManager.LoadScene("GridlockBanker");
            yield return null;
            yield return null;

            var game = Object.FindFirstObjectByType<BankerGame>();
            Assert.IsFalse(game.InChallengeMode);
            Assert.AreEqual("LEVEL 1 OF 5", game.Progress);

            // Level 1 lot: A has 1 parked + 2 to come (1 at the gate), C has 2 parked + 2 waiting on the street.
            Assert.AreEqual(3, ActiveCars("A"));
            Assert.AreEqual(4, ActiveCars("C"));
            GridlockSceneTests.Capture("banker_01_level1");

            game.Grant();
            Assert.IsTrue(game.Verdict.Correct);
            Assert.AreEqual(RequestOutcome.Safe, game.Verdict.Outcome);
            Assert.AreEqual(100, game.Points);
            yield return new WaitForSeconds(3.5f); // gate car drives in, safety trace plays
            GridlockSceneTests.Capture("banker_02_safe_grant");

            // Levels 2 and 3 answered correctly, using the algorithm's own answer.
            for (int i = 0; i < 2; i++)
            {
                game.Next();
                yield return null;
                if (CorrectAnswer(game.Current)) game.Grant(); else game.Deny();
                Assert.IsTrue(game.Verdict.Correct, game.Current.Title);
            }

            // Level 4 (unsafe) answered wrongly on purpose.
            game.Next();
            yield return null;
            Assert.AreEqual("LEVEL 4 OF 5", game.Progress);
            game.Grant();
            Assert.IsFalse(game.Verdict.Correct);
            Assert.AreEqual(RequestOutcome.Unsafe, game.Verdict.Outcome);
            Assert.AreEqual(250, game.Points);
            Assert.AreEqual(0, game.Streak);
            yield return new WaitForSeconds(4f);
            GridlockSceneTests.Capture("banker_03_unsafe_refused");
            GridlockSceneTests.Capture("banker_03b_board", new Vector3(0f, 7.6f, 0.5f), new Vector3(0f, 7.7f, 13.6f), 50f);

            game.Next();
            yield return null;
            game.Grant();
            Assert.IsTrue(game.Verdict.Correct);

            // Challenge mode: random lots, still judged by the algorithm.
            game.Next();
            yield return null;
            Assert.IsTrue(game.InChallengeMode);
            Assert.AreEqual("CHALLENGE 1", game.Current.Title);
            if (CorrectAnswer(game.Current)) game.Grant(); else game.Deny();
            Assert.IsTrue(game.Verdict.Correct);
            Assert.AreEqual(4 * 100 - 50 + 150, game.Points, "challenge answers within 10 s earn the speed bonus");
            yield return new WaitForSeconds(2.5f);
            GridlockSceneTests.Capture("banker_04_challenge");
            GridlockSceneTests.Capture("banker_05_vr_eye", new Vector3(0f, 1.6f, -10.9f), new Vector3(-2f, 2.5f, 2f), 90f);

            game.RestartTutorial();
            yield return null;
            Assert.AreEqual(0, game.Points);
            Assert.AreEqual("LEVEL 1 OF 5", game.Progress);
        }
    }
}
