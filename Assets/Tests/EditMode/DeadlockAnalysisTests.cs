using System.Linq;
using Gridlock.Core;
using Gridlock.Deadlock;
using NUnit.Framework;

namespace Gridlock.Tests
{
    public class DeadlockAnalysisTests
    {
        static ParkingState FourCarRingPlusBystander()
        {
            var s = new ParkingState();
            foreach (var c in new[] { "A", "B", "C", "D", "E" }) s.AddCar(c);
            for (int i = 1; i <= 6; i++) s.AddResource("P" + i);
            s.Request("A", "P1"); s.Request("B", "P2"); s.Request("C", "P3"); s.Request("D", "P4");
            s.Request("A", "P2"); s.Request("B", "P3"); s.Request("C", "P4"); s.Request("D", "P1");
            s.Request("E", "P5"); s.Request("E", "P1"); // E waits on the cycle but is not part of it
            return s;
        }

        [Test]
        public void CarInfo_ReportsOwnsWantsAndWaitsOn()
        {
            var a = new DeadlockAnalysis(FourCarRingPlusBystander()).For("A");

            CollectionAssert.AreEqual(new[] { "P1" }, a.Owns);
            Assert.AreEqual("P2", a.Wants);
            Assert.AreEqual("B", a.WaitsOn);
            Assert.IsTrue(a.InCycle);
            Assert.AreEqual("CAR A\nOwns: P1\nWants: P2", a.Describe());
        }

        [Test]
        public void BystanderEdge_IsNotACycleEdge()
        {
            var analysis = new DeadlockAnalysis(FourCarRingPlusBystander());

            Assert.AreEqual(5, analysis.Edges.Count);
            Assert.AreEqual(4, analysis.CycleEdges.Count);
            Assert.IsFalse(analysis.For("E").InCycle);
            Assert.IsFalse(analysis.CycleEdges.Any(e => e.From == "E"));
        }

        [Test]
        public void Explain_ListsCycleEdgesFirst()
        {
            var lines = new DeadlockAnalysis(FourCarRingPlusBystander()).Explain().ToList();
            Assert.AreEqual("E waits for P1, held by A", lines.Last());
        }

        [Test]
        public void CircularWaitCondition_FollowsDetection()
        {
            var s = FourCarRingPlusBystander();
            Assert.IsTrue(new DeadlockAnalysis(s).CoffmanConditions().Last().holds);

            s.ReleaseAll("D");
            Assert.IsFalse(new DeadlockAnalysis(s).CoffmanConditions().Last().holds);
        }

        [Test]
        public void IdleCar_HasNoWaitInfo()
        {
            var s = new ParkingState();
            s.AddCar("A");
            s.AddResource("P1");
            var a = new DeadlockAnalysis(s).For("A");
            Assert.IsNull(a.Wants);
            Assert.IsNull(a.WaitsOn);
            Assert.AreEqual("CAR A\nOwns: —\nWants: —", a.Describe());
        }
    }
}
