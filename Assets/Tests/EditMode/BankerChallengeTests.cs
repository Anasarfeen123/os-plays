using System.Linq;
using Gridlock.Banker;
using NUnit.Framework;

namespace Gridlock.Tests
{
    public class BankerChallengeTests
    {
        [Test]
        public void Tutorial_CoversEveryOutcomeInTeachingOrder()
        {
            var outcomes = BankerChallenge.Tutorial()
                .Select(c => BankerAlgorithm.Evaluate(c.State, c.Process, c.Request).Outcome).ToArray();

            Assert.AreEqual(new[] { RequestOutcome.Safe, RequestOutcome.ExceedsClaim, RequestOutcome.MustWait,
                                    RequestOutcome.Unsafe, RequestOutcome.Safe }, outcomes);
        }

        [Test]
        public void Tutorial_StartsSafeAndFitsOnScreen()
        {
            foreach (var c in BankerChallenge.Tutorial())
            {
                Assert.IsTrue(BankerAlgorithm.IsSafe(c.State, out _), c.Title);
                for (int i = 0; i < c.State.ProcessCount; i++)
                    Assert.LessOrEqual(c.UnitsNeeded(i), BankerChallenge.UnitsPerFleet, c.Title);
                for (int j = 0; j < c.State.ResourceCount; j++)
                    Assert.GreaterOrEqual(c.State.Available[j], 0, c.Title);
            }
        }

        [TestCase(true, false)]
        [TestCase(false, true)]
        public void UnsafeLevel_OnlyDenyIsCorrect(bool grant, bool correct)
        {
            var level4 = BankerChallenge.Tutorial()[3];
            var v = level4.Decide(grant);
            Assert.AreEqual(RequestOutcome.Unsafe, v.Outcome);
            Assert.AreEqual(correct, v.Correct);
            Assert.IsFalse(v.Trace.Safe);
            CollectionAssert.IsNotEmpty(v.Trace.Stuck);
        }

        [Test]
        public void SafeVerdict_ExplainsTheSequence()
        {
            var v = BankerChallenge.Tutorial()[4].Decide(true);
            Assert.IsTrue(v.Correct);
            Assert.AreEqual("CORRECT · SAFE", v.Headline);
            StringAssert.Contains("A → B → C → D", v.Explanation);
        }

        [Test]
        public void Deny_OnMustWaitAndOverClaim_IsCorrect()
        {
            var levels = BankerChallenge.Tutorial();
            Assert.IsTrue(levels[1].Decide(false).Correct);
            Assert.IsTrue(levels[2].Decide(false).Correct);
            Assert.IsFalse(levels[2].Decide(true).Correct);
        }

        [Test]
        public void SafetyTrace_MatchesAlgorithmSequence()
        {
            foreach (var c in BankerChallenge.Tutorial())
            {
                Assert.IsTrue(BankerAlgorithm.IsSafe(c.State, out var seq));
                var trace = SafetyTrace.Run(c.State);
                CollectionAssert.AreEqual(seq, trace.Steps.Select(s => s.Process).ToList(), c.Title);
            }
        }

        [Test]
        public void SafetyTrace_WorkGrowsByReturnedAllocation()
        {
            var state = BankerChallenge.Tutorial()[0].State;
            var step = SafetyTrace.Run(state).Steps.First();
            for (int j = 0; j < state.ResourceCount; j++)
                Assert.AreEqual(step.WorkBefore[j] + state.Allocation[step.Process, j], step.WorkAfter[j]);
        }

        [Test]
        public void RandomChallenges_AreSafeStartsWithAMixOfOutcomes()
        {
            var rng = new System.Random(11);
            var outcomes = Enumerable.Range(1, 60).Select(i =>
            {
                var c = BankerChallenge.Random(rng, i);
                Assert.IsTrue(BankerAlgorithm.IsSafe(c.State, out _));
                for (int p = 0; p < c.State.ProcessCount; p++)
                    Assert.LessOrEqual(c.UnitsNeeded(p), BankerChallenge.UnitsPerFleet);
                return BankerAlgorithm.Evaluate(c.State, c.Process, c.Request).Outcome;
            }).ToList();

            Assert.Greater(outcomes.Count(o => o == RequestOutcome.Safe), 10);
            Assert.Greater(outcomes.Count(o => o == RequestOutcome.Unsafe), 8);
            Assert.Greater(outcomes.Count(o => o != RequestOutcome.Safe && o != RequestOutcome.Unsafe), 3);
        }
    }
}
