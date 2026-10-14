using System.Collections.Generic;
using System.Linq;
using Gridlock.Core;

namespace Gridlock.Deadlock
{
    public class DeadlockResult
    {
        /// <summary>Each cycle as the ordered edges A→B, B→C, ..., X→A.</summary>
        public readonly List<List<WaitEdge>> Cycles = new List<List<WaitEdge>>();
        public readonly HashSet<string> InvolvedCars = new HashSet<string>();
        public readonly HashSet<string> InvolvedResources = new HashSet<string>();

        public bool HasDeadlock => Cycles.Count > 0;

        /// <summary>"A → B → C → A" for each cycle.</summary>
        public IEnumerable<string> DescribeCycles() =>
            Cycles.Select(c => string.Join(" → ", c.Select(e => e.From)) + " → " + c[0].From);
    }

    /// <summary>
    /// Finds circular waits in a wait-for graph with iterative DFS (white/grey/black colouring).
    /// With single-instance resources a cycle in the wait-for graph is necessary and sufficient for deadlock.
    /// </summary>
    public static class DeadlockDetector
    {
        enum Color { White, Grey, Black }

        public static DeadlockResult Detect(ParkingState state) => Detect(WaitForGraph.FromState(state));

        public static DeadlockResult Detect(WaitForGraph graph)
        {
            var result = new DeadlockResult();
            var color = graph.Nodes.ToDictionary(n => n, _ => Color.White);
            var path = new List<WaitEdge>();          // edges on the current DFS path
            var pathIndex = new Dictionary<string, int>(); // node -> index in path where it was entered

            foreach (var start in graph.Nodes)
            {
                if (color[start] != Color.White) continue;

                // Stack of (node, next-edge-index) to avoid recursion depth issues.
                var stack = new Stack<(string node, int next)>();
                stack.Push((start, 0));
                color[start] = Color.Grey;
                pathIndex[start] = 0;

                while (stack.Count > 0)
                {
                    var (node, next) = stack.Pop();
                    var outEdges = graph.EdgesFrom(node);

                    if (next < outEdges.Count)
                    {
                        stack.Push((node, next + 1));
                        var edge = outEdges[next];

                        if (color[edge.To] == Color.Grey)
                        {
                            // Back edge: cycle is path[from edge.To's entry .. end] + this edge.
                            var cycle = path.Skip(pathIndex[edge.To]).ToList();
                            cycle.Add(edge);
                            AddCycle(result, cycle);
                        }
                        else if (color[edge.To] == Color.White)
                        {
                            path.Add(edge);
                            color[edge.To] = Color.Grey;
                            pathIndex[edge.To] = path.Count;
                            stack.Push((edge.To, 0));
                        }
                    }
                    else
                    {
                        color[node] = Color.Black;
                        pathIndex.Remove(node);
                        if (path.Count > 0 && path[path.Count - 1].To == node) path.RemoveAt(path.Count - 1);
                    }
                }
            }

            return result;
        }

        static void AddCycle(DeadlockResult result, List<WaitEdge> cycle)
        {
            result.Cycles.Add(cycle);
            foreach (var e in cycle)
            {
                result.InvolvedCars.Add(e.From);
                result.InvolvedResources.Add(e.Resource);
            }
        }
    }
}
