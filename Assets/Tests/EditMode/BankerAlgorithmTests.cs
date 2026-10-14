using Gridlock.Banker;
using NUnit.Framework;

namespace Gridlock.Tests
{
    public class BankerAlgorithmTests
    {
        /// <summary>Silberschatz OSC 10e, section 8.6.3: 5 processes, resource types A=10, B=5, C=7.</summary>
        static BankerState Textbook() => new BankerState(
            new[] { "P0", "P1", "P2", "P3", "P4" },
            new[] { "A", "B", "C" },
            new[] { 3, 3, 2 },
            new[,] { { 7, 5, 3 }, { 3, 2, 2 }, { 9, 0, 2 }, { 2, 2, 2 }, { 4, 3, 3 } },
            new[,] { { 0, 1, 0 }, { 2, 0, 0 }, { 3, 0, 2 }, { 2, 1, 1 }, { 0, 0, 2 } });

        [Test]
        public void TextbookState_IsSafe_WithKnownSequence()
        {
            var state = Textbook();
            Assert.IsTrue(BankerAlgorithm.IsSafe(state, out var seq));
            Assert.AreEqual("P1 → P3 → P4 → P0 → P2", state.FormatSequence(seq));
        }

        [Test]
        public void NeedIsMaxMinusAllocation()
        {
            var need = Textbook().NeedMatrix();
            Assert.AreEqual(new[,] { { 7, 4, 3 }, { 1, 2, 2 }, { 6, 0, 0 }, { 0, 1, 1 }, { 4, 3, 1 } }, need);
        }

        [Test]
        public void SafeRequest_IsGranted_AndStateUnchanged()
        {
            var state = Textbook();
            var eval = BankerAlgorithm.Evaluate(state, 1, new[] { 1, 0, 2 });

            Assert.AreEqual(RequestOutcome.Safe, eval.Outcome);
            Assert.AreEqual(5, eval.SafeSequence.Count);
            Assert.AreEqual(new[] { 3, 3, 2 }, state.Available, "Evaluate must not mutate the real state.");
        }

        [Test]
        public void UnsafeRequest_IsDenied()
        {
            // P4 asks for (3,3,0): affordable, but leaves Available = (0,0,2) and no process can finish.
            var eval = BankerAlgorithm.Evaluate(Textbook(), 4, new[] { 3, 3, 0 });
            Assert.AreEqual(RequestOutcome.Unsafe, eval.Outcome);
            Assert.IsEmpty(eval.SafeSequence);
        }

        [Test]
        public void TextbookFollowUp_P0AfterP1Grant_IsUnsafe()
        {
            var state = Textbook();
            state.Apply(1, new[] { 1, 0, 2 });
            Assert.AreEqual(RequestOutcome.Unsafe, BankerAlgorithm.Evaluate(state, 0, new[] { 0, 2, 0 }).Outcome);
        }

        [Test]
        public void RequestAboveAvailable_MustWait()
        {
            Assert.AreEqual(RequestOutcome.MustWait, BankerAlgorithm.Evaluate(Textbook(), 0, new[] { 4, 0, 0 }).Outcome);
        }

        [Test]
        public void RequestAboveNeed_ExceedsClaim()
        {
            Assert.AreEqual(RequestOutcome.ExceedsClaim, BankerAlgorithm.Evaluate(Textbook(), 1, new[] { 2, 0, 0 }).Outcome);
        }

        [Test]
        public void UnsafeInitialState_IsDetected()
        {
            var state = new BankerState(
                new[] { "A", "B" }, new[] { "Spaces" },
                new[] { 0 },
                new[,] { { 2 }, { 2 } },
                new[,] { { 1 }, { 1 } });
            Assert.IsFalse(BankerAlgorithm.IsSafe(state, out _));
        }
    }
}
