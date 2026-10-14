using System;
using System.Collections.Generic;

namespace Gridlock.Banker
{
    public enum RequestOutcome
    {
        /// <summary>Granting keeps the system in a safe state.</summary>
        Safe,
        /// <summary>Resources are available, but granting leaves no safe sequence.</summary>
        Unsafe,
        /// <summary>Not enough resources available right now; the car must wait.</summary>
        MustWait,
        /// <summary>Request exceeds the car's declared maximum claim (Need).</summary>
        ExceedsClaim
    }

    public class RequestEvaluation
    {
        public RequestOutcome Outcome;
        /// <summary>Process indices in a safe completion order (only when <see cref="Outcome"/> is Safe).</summary>
        public List<int> SafeSequence = new List<int>();

        public bool ShouldGrant => Outcome == RequestOutcome.Safe;
    }

    /// <summary>
    /// Multi-instance Banker's Algorithm state. Rows = cars (processes), columns = resource types
    /// (e.g. parking zones with several spaces each). Pure C#, no UnityEngine dependency.
    /// </summary>
    public class BankerState
    {
        public readonly string[] Processes;
        public readonly string[] ResourceTypes;
        public readonly int[] Available;
        public readonly int[,] Max;
        public readonly int[,] Allocation;

        public int ProcessCount => Processes.Length;
        public int ResourceCount => ResourceTypes.Length;

        public BankerState(string[] processes, string[] resourceTypes, int[] available, int[,] max, int[,] allocation)
        {
            int n = processes.Length, m = resourceTypes.Length;
            if (available.Length != m) throw new ArgumentException("Available length must equal resource type count.");
            if (max.GetLength(0) != n || max.GetLength(1) != m) throw new ArgumentException("Max must be processes × resources.");
            if (allocation.GetLength(0) != n || allocation.GetLength(1) != m) throw new ArgumentException("Allocation must be processes × resources.");
            for (int i = 0; i < n; i++)
                for (int j = 0; j < m; j++)
                    if (allocation[i, j] > max[i, j] || allocation[i, j] < 0)
                        throw new ArgumentException($"Allocation[{processes[i]},{resourceTypes[j]}] must be within 0..Max.");

            Processes = processes;
            ResourceTypes = resourceTypes;
            Available = (int[])available.Clone();
            Max = (int[,])max.Clone();
            Allocation = (int[,])allocation.Clone();
        }

        public int Need(int process, int resource) => Max[process, resource] - Allocation[process, resource];

        public int[,] NeedMatrix()
        {
            var need = new int[ProcessCount, ResourceCount];
            for (int i = 0; i < ProcessCount; i++)
                for (int j = 0; j < ResourceCount; j++)
                    need[i, j] = Need(i, j);
            return need;
        }

        public BankerState Clone() => new BankerState(Processes, ResourceTypes, Available, Max, Allocation);

        /// <summary>Applies a request without any safety check. Use after <see cref="BankerAlgorithm.Evaluate"/> says Safe.</summary>
        public void Apply(int process, int[] request)
        {
            for (int j = 0; j < ResourceCount; j++)
            {
                Available[j] -= request[j];
                Allocation[process, j] += request[j];
            }
        }

        /// <summary>Process finishes and returns everything it holds.</summary>
        public void Complete(int process)
        {
            for (int j = 0; j < ResourceCount; j++)
            {
                Available[j] += Allocation[process, j];
                Allocation[process, j] = 0;
                Max[process, j] = 0;
            }
        }

        public string FormatSequence(IEnumerable<int> sequence)
        {
            var names = new List<string>();
            foreach (var i in sequence) names.Add(Processes[i]);
            return string.Join(" → ", names);
        }
    }

    public static class BankerAlgorithm
    {
        /// <summary>Safety algorithm. Returns true and a safe sequence if every process can finish in some order.</summary>
        public static bool IsSafe(BankerState state, out List<int> safeSequence)
        {
            int n = state.ProcessCount, m = state.ResourceCount;
            var work = (int[])state.Available.Clone();
            var finish = new bool[n];
            safeSequence = new List<int>(n);

            bool progressed = true;
            while (progressed)
            {
                progressed = false;
                for (int i = 0; i < n; i++)
                {
                    if (finish[i] || !NeedFits(state, i, work)) continue;
                    for (int j = 0; j < m; j++) work[j] += state.Allocation[i, j];
                    finish[i] = true;
                    safeSequence.Add(i);
                    progressed = true;
                }
            }

            if (safeSequence.Count == n) return true;
            safeSequence.Clear();
            return false;
        }

        /// <summary>
        /// Resource-request algorithm. Does not modify <paramref name="state"/>; it tests the request against a copy.
        /// </summary>
        public static RequestEvaluation Evaluate(BankerState state, int process, int[] request)
        {
            if (request.Length != state.ResourceCount)
                throw new ArgumentException("Request length must equal resource type count.");

            var result = new RequestEvaluation();

            for (int j = 0; j < state.ResourceCount; j++)
            {
                if (request[j] < 0) throw new ArgumentException("Request values must be non-negative.");
                if (request[j] > state.Need(process, j)) { result.Outcome = RequestOutcome.ExceedsClaim; return result; }
            }
            for (int j = 0; j < state.ResourceCount; j++)
                if (request[j] > state.Available[j]) { result.Outcome = RequestOutcome.MustWait; return result; }

            var trial = state.Clone();
            trial.Apply(process, request);
            result.Outcome = IsSafe(trial, out result.SafeSequence) ? RequestOutcome.Safe : RequestOutcome.Unsafe;
            return result;
        }

        static bool NeedFits(BankerState state, int process, int[] work)
        {
            for (int j = 0; j < state.ResourceCount; j++)
                if (state.Need(process, j) > work[j]) return false;
            return true;
        }
    }
}
