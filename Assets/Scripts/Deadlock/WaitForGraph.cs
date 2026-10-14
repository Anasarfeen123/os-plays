using System.Collections.Generic;
using Gridlock.Core;

namespace Gridlock.Deadlock
{
    /// <summary>One wait-for edge: <see cref="From"/> wants <see cref="Resource"/>, which <see cref="To"/> holds.</summary>
    public readonly struct WaitEdge
    {
        public readonly string From;
        public readonly string To;
        public readonly string Resource;

        public WaitEdge(string from, string to, string resource)
        {
            From = from;
            To = to;
            Resource = resource;
        }

        public override string ToString() => $"{From} → {To} (via {Resource})";
    }

    /// <summary>Wait-for graph derived from a resource-allocation state. Works for any number of cars.</summary>
    public class WaitForGraph
    {
        readonly Dictionary<string, List<WaitEdge>> outgoing = new Dictionary<string, List<WaitEdge>>();
        readonly List<string> nodes = new List<string>();
        readonly List<WaitEdge> edges = new List<WaitEdge>();

        public IReadOnlyList<string> Nodes => nodes;
        public IReadOnlyList<WaitEdge> Edges => edges;

        public void AddNode(string car)
        {
            if (outgoing.ContainsKey(car)) return;
            outgoing[car] = new List<WaitEdge>();
            nodes.Add(car);
        }

        public void AddEdge(string from, string to, string resource)
        {
            AddNode(from);
            AddNode(to);
            var e = new WaitEdge(from, to, resource);
            outgoing[from].Add(e);
            edges.Add(e);
        }

        public IReadOnlyList<WaitEdge> EdgesFrom(string car) =>
            outgoing.TryGetValue(car, out var list) ? list : (IReadOnlyList<WaitEdge>)System.Array.Empty<WaitEdge>();

        public static WaitForGraph FromState(ParkingState state)
        {
            var g = new WaitForGraph();
            foreach (var car in state.Cars) g.AddNode(car);
            foreach (var car in state.Cars)
            {
                var wanted = state.GetRequest(car);
                if (wanted == null) continue;
                var owner = state.GetOwner(wanted);
                if (owner != null && owner != car) g.AddEdge(car, owner, wanted);
            }
            return g;
        }
    }
}
