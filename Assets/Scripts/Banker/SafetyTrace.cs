using System.Collections.Generic;

namespace Gridlock.Banker
{
    /// <summary>One step of the safety algorithm: a process whose Need fits in Work finishes and returns its allocation.</summary>
    public class SafetyStep
    {
        public int Process;
        public int[] Need;
        public int[] WorkBefore;
        public int[] WorkAfter;
    }

    /// <summary>
    /// The safety algorithm with every step recorded, for explaining a verdict to the learner.
    /// Uses the same scan order as <see cref="BankerAlgorithm.IsSafe"/>, so the sequences match (covered by tests).
    /// </summary>
    public class SafetyTrace
    {
        public readonly List<SafetyStep> Steps = new List<SafetyStep>();
        /// <summary>Processes that can never finish (empty when the state is safe).</summary>
        public readonly List<int> Stuck = new List<int>();
        public int[] InitialWork;

        public bool Safe => Stuck.Count == 0;

        public static SafetyTrace Run(BankerState state)
        {
            int n = state.ProcessCount, m = state.ResourceCount;
            var trace = new SafetyTrace { InitialWork = (int[])state.Available.Clone() };
            var work = (int[])state.Available.Clone();
            var finish = new bool[n];

            bool progressed = true;
            while (progressed)
            {
                progressed = false;
                for (int i = 0; i < n; i++)
                {
                    if (finish[i]) continue;
                    var need = new int[m];
                    bool fits = true;
                    for (int j = 0; j < m; j++)
                    {
                        need[j] = state.Need(i, j);
                        if (need[j] > work[j]) fits = false;
                    }
                    if (!fits) continue;

                    var before = (int[])work.Clone();
                    for (int j = 0; j < m; j++) work[j] += state.Allocation[i, j];
                    trace.Steps.Add(new SafetyStep { Process = i, Need = need, WorkBefore = before, WorkAfter = (int[])work.Clone() });
                    finish[i] = true;
                    progressed = true;
                }
            }

            for (int i = 0; i < n; i++)
                if (!finish[i]) trace.Stuck.Add(i);
            return trace;
        }
    }
}
