using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gridlock.Banker
{
    /// <summary>
    /// Banker's Algorithm game mode (spec §9–11). The learner is the parking manager: for each request they GRANT or
    /// DENY, and the real request + safety algorithms judge the decision. Five guided levels, then endless challenges.
    /// </summary>
    public class BankerGame : MonoBehaviour
    {
        const int CorrectPoints = 100, SpeedBonus = 50, WrongPenalty = 50;
        const float SpeedBonusSeconds = 10f;

        [SerializeField] BankerLotView lot;
        [Tooltip("0 = different random challenges every run.")]
        [SerializeField] int seed;

        List<BankerChallenge> tutorial;
        System.Random rng;
        int level;
        int challengeNumber;
        float shownAt;

        public BankerChallenge Current { get; private set; }
        public BankerVerdict Verdict { get; private set; }
        public int Points { get; private set; }
        public int CorrectCount { get; private set; }
        public int WrongCount { get; private set; }
        public int Streak { get; private set; }
        public bool InChallengeMode => level >= tutorial.Count;
        public string Progress => InChallengeMode ? "CHALLENGE MODE" : $"LEVEL {level + 1} OF {tutorial.Count}";

        public event Action<BankerChallenge> ChallengeLoaded;
        /// <summary>Raised when <see cref="Locked"/> changes (the panels grey their buttons out).</summary>
        public event Action LockChanged;

        bool locked;

        /// <summary>While the guided tour narrates, GRANT / DENY / NEXT wait until it is the learner's turn.</summary>
        public bool Locked
        {
            get => locked;
            set
            {
                if (locked == value) return;
                locked = value;
                LockChanged?.Invoke();
            }
        }
        /// <summary>(verdict, points gained or lost)</summary>
        public event Action<BankerVerdict, int> Decided;

        public void Init(BankerLotView lotView) => lot = lotView;

        void Awake()
        {
            tutorial = BankerChallenge.Tutorial();
            rng = seed != 0 ? new System.Random(seed) : new System.Random();
        }

        void Start() => Load();

        public void Grant() { if (!Locked) Decide(true); }
        public void Deny() { if (!Locked) Decide(false); }

        public void Next()
        {
            if (Locked) return;
            if (Verdict == null) return;
            if (!InChallengeMode) Gridlock.Core.Progress.RecordBankerLevel(level + 1); // the control room shows the levels cleared
            level++;
            Load();
        }

        /// <summary>Back to level 1 with a fresh score.</summary>
        public void RestartTutorial()
        {
            level = 0;
            challengeNumber = 0;
            Points = CorrectCount = WrongCount = Streak = 0;
            Load();
        }

        /// <summary>Skip the guided levels.</summary>
        public void JumpToChallenges()
        {
            level = tutorial.Count;
            Load();
        }

        void Load()
        {
            Current = InChallengeMode ? BankerChallenge.Random(rng, ++challengeNumber) : tutorial[level];
            Verdict = null;
            shownAt = Time.time;
            lot.Show(Current);
            ChallengeLoaded?.Invoke(Current);
        }

        void Decide(bool grant)
        {
            if (Verdict != null) return;
            Verdict = Current.Decide(grant);

            int delta;
            if (Verdict.Correct)
            {
                CorrectCount++;
                Streak++;
                delta = CorrectPoints + (InChallengeMode && Time.time - shownAt <= SpeedBonusSeconds ? SpeedBonus : 0);
            }
            else
            {
                WrongCount++;
                Streak = 0;
                delta = -Mathf.Min(WrongPenalty, Points);
            }
            Points += delta;

            if (grant && Verdict.Outcome == RequestOutcome.Safe) lot.AnimateGrant(Current);
            lot.ShowVerdict(Verdict);
            Decided?.Invoke(Verdict, delta);
        }
    }
}
