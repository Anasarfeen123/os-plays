using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Generates scheduling questions directly from the live simulation state.
/// Computes the ground-truth execution order with the real scheduling engine,
/// then builds a multiple-choice question with one correct answer and
/// plausible distractor sequences derived from common misconceptions.
/// Answers are validated against the engine (see ProgressionManager.EvaluateChallenge).
/// </summary>
public static class ChallengeGenerator
{
    /// <summary>
    /// Builds a challenge from the current SimulationController state.
    /// Returns null if no simulation is available.
    /// </summary>
    public static SchedulingChallenge GenerateFromCurrentSimulation()
    {
        var sim = SimulationController.Instance;
        if (sim == null || sim.CurrentAlgorithm == null) return null;
        return BuildChallenge(sim.InitialProcesses, sim.CurrentAlgorithm.Name, 4);
    }

    /// <summary>
    /// Builds a challenge for a specific process set + algorithm name.
    /// </summary>
    public static SchedulingChallenge BuildChallenge(List<OSProcess> processes, string algorithmName, int timeQuantum = 4)
    {
        if (processes == null || processes.Count == 0) return null;

        var algo = InstantiateAlgorithm(algorithmName, timeQuantum);
        var result = algo.RunToCompletion(processes);

        // Ground truth: order of first execution of each process
        var truth = new List<string>();
        foreach (var seg in result.GanttChart)
        {
            if (!seg.IsIdle && !truth.Contains(seg.ProcessId)) truth.Add(seg.ProcessId);
        }

        var distractors = BuildDistractors(truth, processes, algorithmName);

        var challenge = new SchedulingChallenge
        {
            Id = "live_" + Guid.NewGuid().ToString("N").Substring(0, 8),
            Title = "Live: " + algo.Name,
            Difficulty = GuessDifficulty(algorithmName),
            AlgorithmName = NormalizeAlgorithmKey(algorithmName),
            Description = $"Predict the execution order of {processes.Count} processes under {algo.Name}.",
            TimeQuantum = timeQuantum,
            BaseXP = 150,
            Processes = processes.Select(p => p.Clone()).ToList(),
            DetailedExplanation = BuildExplanation(algo, result, truth)
        };

        var rawOptions = new List<ChallengeOption>();
        var correctSeq = new List<string>(truth);
        rawOptions.Add(new ChallengeOption(
            string.Join(" -> ", correctSeq),
            correctSeq,
            true,
            $"CORRECT! {algo.Name} executes processes in this order: {string.Join(" -> ", correctSeq)}."));

        foreach (var d in distractors)
        {
            rawOptions.Add(new ChallengeOption(
                string.Join(" -> ", d),
                d,
                false,
                $"Incorrect. The correct sequence under {algo.Name} is {string.Join(" -> ", correctSeq)}."));
        }

        // Shuffle options and assign A, B, C, D labels
        var rng = new System.Random();
        var shuffled = rawOptions.OrderBy(x => rng.Next()).ToList();
        char[] optionLetters = { 'A', 'B', 'C', 'D' };
        for (int i = 0; i < shuffled.Count && i < optionLetters.Length; i++)
        {
            shuffled[i].SequenceLabel = $"{optionLetters[i]}. {shuffled[i].SequenceLabel}";
        }

        challenge.Options = shuffled;
        return challenge;
    }

    /// <summary>
    /// Builds plausible wrong sequences from common misconceptions.
    /// </summary>
    private static List<List<string>> BuildDistractors(List<string> truth, List<OSProcess> processes, string algorithmName)
    {
        var result = new List<List<string>>();
        var ids = processes.Select(p => p.Id).ToList();

        // 1. Reverse of truth
        var rev = new List<string>(truth);
        rev.Reverse();
        if (!SameAs(rev, truth)) result.Add(rev);

        // 2. Arrival order (FCFS bias)
        var arrivalOrder = processes.OrderBy(p => p.ArrivalTime).ThenBy(p => p.Id).Select(p => p.Id).ToList();
        if (!SameAs(arrivalOrder, truth)) result.Add(arrivalOrder);

        // 3. Shortest-burst-first bias (SJF bias)
        var burstOrder = processes.OrderBy(p => p.BurstTime).ThenBy(p => p.ArrivalTime).Select(p => p.Id).ToList();
        if (!SameAs(burstOrder, truth)) result.Add(burstOrder);

        // 4. Priority order bias
        var priOrder = processes.OrderBy(p => p.Priority).ThenBy(p => p.ArrivalTime).Select(p => p.Id).ToList();
        if (!SameAs(priOrder, truth)) result.Add(priOrder);

        // 5. Simple swap of first two (if available)
        if (truth.Count >= 2)
        {
            var swapped = new List<string>(truth);
            var tmp = swapped[0];
            swapped[0] = swapped[1];
            swapped[1] = tmp;
            if (!SameAs(swapped, truth)) result.Add(swapped);
        }

        // 6. Alphabetical bias
        var alpha = new List<string>(ids);
        alpha.Sort();
        if (!SameAs(alpha, truth)) result.Add(alpha);

        // De-duplicate and ensure exactly 3 distractors
        var unique = new List<List<string>>();
        foreach (var d in result)
        {
            if (!unique.Any(u => SameAs(u, d)) && !SameAs(d, truth)) unique.Add(d);
        }

        // If fewer than 3, generate additional permutations
        var r = new System.Random();
        int attempts = 0;
        while (unique.Count < 3 && attempts < 20)
        {
            attempts++;
            var perm = ids.OrderBy(x => r.Next()).ToList();
            if (!SameAs(perm, truth) && !unique.Any(u => SameAs(u, perm)))
            {
                unique.Add(perm);
            }
        }
        return unique.Take(3).ToList();
    }

    private static bool SameAs(List<string> a, List<string> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
            if (a[i] != b[i]) return false;
        return true;
    }

    private static ChallengeDifficulty GuessDifficulty(string algorithmName)
    {
        var n = algorithmName.ToUpper();
        if (n.Contains("FCFS")) return ChallengeDifficulty.Easy;
        if (n.Contains("SJF")) return ChallengeDifficulty.Medium;
        if (n.Contains("SRTF")) return ChallengeDifficulty.Hard;
        if (n.Contains("PRIORITY")) return ChallengeDifficulty.Hard;
        if (n.Contains("ROUND ROBIN") || n.Contains("RR")) return ChallengeDifficulty.Hard;
        return ChallengeDifficulty.Expert;
    }

    private static string NormalizeAlgorithmKey(string algorithmName)
    {
        var n = algorithmName.ToUpper();
        if (n.Contains("FCFS")) return "FCFS";
        if (n.Contains("SJF") && n.Contains("SRTF") == false && n.Contains("REMAINING") == false) return "SJF";
        if (n.Contains("SRTF") || n.Contains("REMAINING")) return "SRTF";
        if (n.Contains("PRIORITY")) return "Priority";
        if (n.Contains("ROUND ROBIN") || n.Contains("RR")) return "Round Robin";
        if (n.Contains("MLQ")) return "MLQ";
        if (n.Contains("MLFQ")) return "MLFQ";
        return "FCFS";
    }

    private static string BuildExplanation(SchedulingAlgorithm algo, SchedulingResult result, List<string> truth)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{algo.Name}: execution order {string.Join(" -> ", truth)}.");
        sb.AppendLine($"Total duration: {result.TotalDuration} time units.");
        sb.AppendLine($"Avg Waiting: {result.AverageWaitingTime:F2} | Avg Turnaround: {result.AverageTurnaroundTime:F2} | Avg Response: {result.AverageResponseTime:F2} | CPU Util: {result.CpuUtilization:F1}%.");
        sb.AppendLine(algo.Description);
        return sb.ToString();
    }

    private static SchedulingAlgorithm InstantiateAlgorithm(string name, int quantum)
    {
        var n = name.ToUpper();
        if (n.Contains("FCFS")) return new FCFSAlgorithm();
        if (n.Contains("SRTF") || n.Contains("REMAINING")) return new SRTFAlgorithm();
        if (n.Contains("SJF")) return new SJFAlgorithm();
        if (n.Contains("PRIORITY")) return new PriorityAlgorithm(isPreemptive: true);
        if (n.Contains("ROUND ROBIN") || n.Contains("RR")) return new RoundRobinAlgorithm(quantum);
        if (n.Contains("MLQ")) return new MLQAlgorithm();
        if (n.Contains("MLFQ")) return new MLFQAlgorithm(new int[] { 2, 4, 8 });
        return new FCFSAlgorithm();
    }
}
