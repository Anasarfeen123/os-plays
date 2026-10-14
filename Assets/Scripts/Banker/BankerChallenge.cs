using System;
using System.Collections.Generic;
using System.Linq;

namespace Gridlock.Banker
{
    /// <summary>
    /// One Banker's Algorithm puzzle: a lot state (Available / Max / Allocation over parking zones) and one fleet's
    /// request. The answer is never stored; <see cref="Decide"/> runs the real request and safety algorithms.
    /// Pure C#, no UnityEngine dependency.
    /// </summary>
    public class BankerChallenge
    {
        public static readonly string[] Fleets = { "A", "B", "C", "D" };
        public static readonly string[] Zones = { "N", "S" };
        public static readonly int[] ZoneCapacity = { 3, 3 };
        /// <summary>Most cars one fleet can show on screen (parked + waiting).</summary>
        public const int UnitsPerFleet = 4;

        public string Title;
        public string Intro;
        public BankerState State;
        public int Process;
        public int[] Request;

        public string Fleet => State.Processes[Process];

        public BankerVerdict Decide(bool grant) => BankerVerdict.For(this, grant);

        /// <summary>Cars a fleet needs on screen: parked + max(waiting on the street, at the gate).</summary>
        public int UnitsNeeded(int process)
        {
            int alloc = 0, need = 0, req = process == Process ? Request.Sum() : 0;
            for (int j = 0; j < State.ResourceCount; j++)
            {
                alloc += State.Allocation[process, j];
                need += State.Need(process, j);
            }
            return alloc + Math.Max(need, req);
        }

        public static string Vec(int[] v) => string.Join(" + ", v.Select((x, j) => $"{x} {Zones[j]}"));

        static BankerState LotState(int[,] max, int[,] allocation)
        {
            var available = new int[Zones.Length];
            for (int j = 0; j < Zones.Length; j++)
            {
                available[j] = ZoneCapacity[j];
                for (int i = 0; i < Fleets.Length; i++) available[j] -= allocation[i, j];
            }
            return new BankerState(Fleets, Zones, available, max, allocation);
        }

        /// <summary>
        /// Five guided levels that tell one story. Level 1 starts from a calm lot; levels 2–5 continue from the lot
        /// after fleet A's request in level 1 was (correctly) granted. Each level introduces one rule of the algorithm.
        /// </summary>
        public static List<BankerChallenge> Tutorial()
        {
            var max = new[,] { { 2, 1 }, { 1, 2 }, { 2, 2 }, { 1, 1 } };
            var start = new[,] { { 1, 0 }, { 0, 1 }, { 1, 1 }, { 0, 0 } };
            var afterLevel1 = new[,] { { 2, 0 }, { 0, 1 }, { 1, 1 }, { 0, 0 } };

            return new List<BankerChallenge>
            {
                new BankerChallenge
                {
                    Title = "LEVEL 1 · The parking manager",
                    Intro = "Every fleet declared up front the most bays it could ever need (Max). Grant a request only if, " +
                            "afterwards, there is still an order in which every fleet can finish: a safe sequence.",
                    State = LotState(max, start), Process = 0, Request = new[] { 1, 0 },
                },
                new BankerChallenge
                {
                    Title = "LEVEL 2 · Keep to the claim",
                    Intro = "A fleet may never take more than it declared. Check its Need = Max − Allocation first.",
                    State = LotState(max, afterLevel1), Process = 3, Request = new[] { 2, 0 },
                },
                new BankerChallenge
                {
                    Title = "LEVEL 3 · Count the empty bays",
                    Intro = "Before thinking about safety, are there enough free bays (Available) for this request right now?",
                    State = LotState(max, afterLevel1), Process = 1, Request = new[] { 1, 0 },
                },
                new BankerChallenge
                {
                    Title = "LEVEL 4 · Free is not the same as safe",
                    Intro = "The bay is free. But if you hand it out, can every fleet still finish in some order?",
                    State = LotState(max, afterLevel1), Process = 2, Request = new[] { 0, 1 },
                },
                new BankerChallenge
                {
                    Title = "LEVEL 5 · The last free bay",
                    Intro = "This request takes the very last free bay. Does an empty lot automatically mean danger?",
                    State = LotState(max, afterLevel1), Process = 0, Request = new[] { 0, 1 },
                },
            };
        }

        /// <summary>
        /// Random challenge for Challenge mode. Starts from a random <b>safe</b> lot and picks a request whose outcome
        /// follows a mix (mostly safe/unsafe, sometimes must-wait/exceeds) so that guessing does not pay.
        /// </summary>
        public static BankerChallenge Random(Random rng, int number)
        {
            var desired = Pick(rng, (RequestOutcome.Safe, 40), (RequestOutcome.Unsafe, 35),
                                    (RequestOutcome.MustWait, 15), (RequestOutcome.ExceedsClaim, 10));
            BankerChallenge fallback = null;

            for (int attempt = 0; attempt < 2000; attempt++)
            {
                var state = RandomSafeState(rng);
                if (state == null) continue;
                int p = rng.Next(Fleets.Length);
                var req = new[] { rng.Next(3), rng.Next(3) };
                if (req.Sum() == 0 || req.Sum() > 2) continue;

                var challenge = new BankerChallenge
                {
                    Title = $"CHALLENGE {number}",
                    Intro = "Random lot. Decide fast: answers within 10 seconds earn a speed bonus.",
                    State = state, Process = p, Request = req,
                };
                if (Enumerable.Range(0, Fleets.Length).Any(i => challenge.UnitsNeeded(i) > UnitsPerFleet)) continue;

                if (BankerAlgorithm.Evaluate(state, p, req).Outcome == desired) return challenge;
                fallback ??= challenge;
            }
            return fallback;
        }

        static BankerState RandomSafeState(Random rng)
        {
            int n = Fleets.Length, m = Zones.Length;
            var alloc = new int[n, m];
            var max = new int[n, m];
            for (int j = 0; j < m; j++)
            {
                int left = ZoneCapacity[j];
                for (int i = 0; i < n; i++)
                {
                    alloc[i, j] = Math.Min(left, rng.Next(2));
                    left -= alloc[i, j];
                }
            }
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < m; j++) max[i, j] = Math.Min(ZoneCapacity[j], alloc[i, j] + rng.Next(3));
                if (max[i, 0] + max[i, 1] == 0) max[i, rng.Next(m)] = 1;
            }
            var state = LotState(max, alloc);
            return BankerAlgorithm.IsSafe(state, out _) ? state : null;
        }

        static T Pick<T>(Random rng, params (T value, int weight)[] options)
        {
            int roll = rng.Next(options.Sum(o => o.weight));
            foreach (var (value, weight) in options)
            {
                if (roll < weight) return value;
                roll -= weight;
            }
            return options[0].value;
        }
    }

    /// <summary>The judged outcome of the learner's GRANT/DENY, with a plain-language explanation and the safety trace.</summary>
    public class BankerVerdict
    {
        public RequestOutcome Outcome;
        public bool PlayerGranted;
        public bool Correct;
        public string Headline;
        public string Explanation;
        /// <summary>Safety trace of the lot <i>as if</i> the request were granted (Safe/Unsafe outcomes only).</summary>
        public SafetyTrace Trace;
        public List<int> SafeSequence = new List<int>();

        public bool CorrectAnswerIsGrant => Outcome == RequestOutcome.Safe;

        public static BankerVerdict For(BankerChallenge c, bool grant)
        {
            var eval = BankerAlgorithm.Evaluate(c.State, c.Process, c.Request);
            var v = new BankerVerdict
            {
                Outcome = eval.Outcome,
                PlayerGranted = grant,
                SafeSequence = eval.SafeSequence,
            };
            v.Correct = grant == v.CorrectAnswerIsGrant;

            var s = c.State;
            var fleet = c.Fleet;
            var req = BankerChallenge.Vec(c.Request);
            var need = BankerChallenge.Vec(Enumerable.Range(0, s.ResourceCount).Select(j => s.Need(c.Process, j)).ToArray());
            var avail = BankerChallenge.Vec(s.Available);

            if (eval.Outcome == RequestOutcome.Safe || eval.Outcome == RequestOutcome.Unsafe)
            {
                var trial = s.Clone();
                trial.Apply(c.Process, c.Request);
                v.Trace = SafetyTrace.Run(trial);
            }

            switch (eval.Outcome)
            {
                case RequestOutcome.Safe:
                    var seq = s.FormatSequence(eval.SafeSequence);
                    v.Headline = grant ? "CORRECT · SAFE" : "TOO CAUTIOUS";
                    v.Explanation = (grant ? "Granted. " : "Granting was safe, so denying only keeps the fleet waiting. ")
                                    + $"After the grant every fleet can still finish in the order {seq}.";
                    break;
                case RequestOutcome.Unsafe:
                    var stuck = string.Join(", ", v.Trace.Stuck.Select(i => s.Processes[i]));
                    v.Headline = grant ? "UNSAFE · REFUSED" : "CORRECT · UNSAFE";
                    v.Explanation = (grant ? "The Banker blocks this grant. " : "Denied, and rightly so. ")
                                    + $"After giving {fleet} {req}, the free bays could not cover the Need of fleet(s) {stuck}. "
                                    + "No safe sequence exists, so the lot could end in deadlock.";
                    break;
                case RequestOutcome.MustWait:
                    v.Headline = grant ? "NOT ENOUGH BAYS" : "CORRECT · MUST WAIT";
                    v.Explanation = $"{fleet} asks for {req} but only {avail} is free right now. "
                                    + $"Fleet {fleet} has to wait until other fleets free bays.";
                    break;
                case RequestOutcome.ExceedsClaim:
                    v.Headline = grant ? "CLAIM EXCEEDED" : "CORRECT · OVER CLAIM";
                    v.Explanation = $"{fleet}'s remaining Need is only {need}, but it asks for {req}. "
                                    + "A process may never exceed its declared Max, so this request is an error.";
                    break;
            }
            return v;
        }
    }
}
