using System.Linq;
using Gridlock.Core;
using Gridlock.Deadlock;
using NUnit.Framework;

namespace Gridlock.Tests
{
    public class DeadlockDetectorTests
    {
        /// <summary>Car i owns P(i+1) and requests P(i+2), wrapping around: a ring of n cars.</summary>
        static ParkingState Ring(int n)
        {
            var s = new ParkingState();
            for (int i = 0; i < n; i++)
            {
                s.AddCar(((char)('A' + i)).ToString());
                s.AddResource("P" + (i + 1));
            }
            for (int i = 0; i < n; i++) Assert.IsTrue(s.Request(s.Cars[i], "P" + (i + 1)));
            for (int i = 0; i < n; i++) Assert.IsFalse(s.Request(s.Cars[i], "P" + ((i + 1) % n + 1)));
            return s;
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(6)]
        public void RingOfNCars_IsDeadlocked(int n)
        {
            var result = DeadlockDetector.Detect(Ring(n));

            Assert.IsTrue(result.HasDeadlock);
            Assert.AreEqual(1, result.Cycles.Count);
            Assert.AreEqual(n, result.InvolvedCars.Count);
            Assert.AreEqual(n, result.InvolvedResources.Count);
        }

        [Test]
        public void FourCarRing_DescribesCycleInOrder()
        {
            var result = DeadlockDetector.Detect(Ring(4));
            Assert.AreEqual("A → B → C → D → A", result.DescribeCycles().Single());
        }

        [Test]
        public void WaitingChainWithoutCycle_IsNotDeadlocked()
        {
            var s = new ParkingState();
            foreach (var c in new[] { "A", "B", "C" }) s.AddCar(c);
            foreach (var r in new[] { "P1", "P2", "P3" }) s.AddResource(r);
            s.Request("A", "P1");
            s.Request("B", "P2");
            s.Request("C", "P3");
            s.Request("A", "P2"); // A waits on B
            s.Request("B", "P3"); // B waits on C, C is not waiting

            var result = DeadlockDetector.Detect(s);

            Assert.IsFalse(result.HasDeadlock);
            Assert.IsTrue(s.IsWaiting("A"));
            Assert.IsFalse(s.IsWaiting("C"));
        }

        [Test]
        public void CycleWithTailCar_OnlyCycleMembersInvolved()
        {
            var s = Ring(3);
            s.AddCar("D");
            s.AddResource("P4");
            s.Request("D", "P4");
            s.Request("D", "P1"); // D waits on A but is not part of the cycle

            var result = DeadlockDetector.Detect(s);

            Assert.IsTrue(result.HasDeadlock);
            CollectionAssert.AreEquivalent(new[] { "A", "B", "C" }, result.InvolvedCars);
            Assert.IsFalse(result.InvolvedResources.Contains("P4"));
        }

        [Test]
        public void ReleasingResource_BreaksDeadlock()
        {
            var s = Ring(4);
            s.ReleaseAll("D"); // preempt D

            var unblocked = s.RetryWaiting();

            CollectionAssert.Contains(unblocked, "C");
            Assert.IsFalse(DeadlockDetector.Detect(s).HasDeadlock);
        }

        [Test]
        public void WaitForGraph_EdgesPointToOwner()
        {
            var g = WaitForGraph.FromState(Ring(2));
            var edge = g.EdgesFrom("A").Single();
            Assert.AreEqual("B", edge.To);
            Assert.AreEqual("P2", edge.Resource);
        }
    }
}
