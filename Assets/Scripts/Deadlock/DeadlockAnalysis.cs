using System.Collections.Generic;
using System.Linq;
using Gridlock.Core;

namespace Gridlock.Deadlock
{
    /// <summary>What Analyze mode shows for one car.</summary>
    public class CarAnalysis
    {
        public string Car;
        public List<string> Owns = new List<string>();
        public string Wants;
        /// <summary>Car holding <see cref="Wants"/>, or null if the car is not waiting.</summary>
        public string WaitsOn;
        public bool InCycle;

        public string Describe() =>
            $"CAR {Car}\nOwns: {(Owns.Count > 0 ? string.Join(", ", Owns) : "—")}\nWants: {Wants ?? "—"}";
    }

    /// <summary>
    /// Snapshot of a parking state prepared for Analyze mode: per-car hold/wait info, wait-for edges and cycles.
    /// Pure C#, no UnityEngine dependency.
    /// </summary>
    public class DeadlockAnalysis
    {
        public readonly List<CarAnalysis> Cars = new List<CarAnalysis>();
        public readonly List<WaitEdge> Edges = new List<WaitEdge>();
        public readonly HashSet<WaitEdge> CycleEdges = new HashSet<WaitEdge>();
        public readonly DeadlockResult Result;

        public DeadlockAnalysis(ParkingState state)
        {
            var graph = WaitForGraph.FromState(state);
            Result = DeadlockDetector.Detect(graph);
            Edges.AddRange(graph.Edges);
            foreach (var cycle in Result.Cycles)
                foreach (var e in cycle) CycleEdges.Add(e);

            foreach (var car in state.Cars)
            {
                var wants = state.GetRequest(car);
                var owner = wants != null ? state.GetOwner(wants) : null;
                Cars.Add(new CarAnalysis
                {
                    Car = car,
                    Owns = state.GetHeld(car),
                    Wants = wants,
                    WaitsOn = owner != car ? owner : null,
                    InCycle = Result.InvolvedCars.Contains(car),
                });
            }
        }

        public CarAnalysis For(string car) => Cars.FirstOrDefault(c => c.Car == car);

        /// <summary>Plain-language explanation of each waiting relation, cycle edges first.</summary>
        public IEnumerable<string> Explain() =>
            Edges.OrderByDescending(CycleEdges.Contains)
                 .Select(e => $"{e.From} waits for {e.Resource}, held by {e.To}");

        /// <summary>The four Coffman conditions as they hold in this state (for the panel).</summary>
        public IEnumerable<(string condition, bool holds)> CoffmanConditions()
        {
            bool holdAndWait = Cars.Any(c => c.Owns.Count > 0 && c.Wants != null);
            yield return ("Mutual exclusion — each space fits one car", true);
            yield return ("Hold and wait — a car keeps its space while waiting", holdAndWait);
            yield return ("No preemption — a parked car cannot be removed", true);
            yield return ("Circular wait — the wait-for graph has a cycle", Result.HasDeadlock);
        }
    }
}
