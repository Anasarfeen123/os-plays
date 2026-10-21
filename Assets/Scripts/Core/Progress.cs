using UnityEngine;

namespace Gridlock.Core
{
    /// <summary>
    /// What the learner has done, kept on the device between sessions (PlayerPrefs): scenarios visited, guided tours
    /// finished, the best score on each scenario's quiz, and the highest Banker level reached. The control room shows it.
    /// Keys are the tours' keys: "deadlock", "banker", "cpu", "memory".
    /// </summary>
    public static class Progress
    {
        public const int QuizLength = 3;

        public static bool Visited(string scene) => PlayerPrefs.GetInt($"progress.visited.{scene}", 0) == 1;
        public static bool TourDone(string key) => PlayerPrefs.GetInt($"progress.tour.{key}", 0) == 1;
        /// <summary>Best quiz score so far, or -1 before the first try.</summary>
        public static int QuizBest(string key) => PlayerPrefs.GetInt($"progress.quiz.{key}", -1);
        public static int BankerLevel => PlayerPrefs.GetInt("progress.banker.level", 0);

        public static void MarkVisited(string scene) => Set($"progress.visited.{scene}", 1);
        public static void MarkTourDone(string key) => Set($"progress.tour.{key}", 1);

        public static void RecordQuiz(string key, int score)
        {
            if (score > QuizBest(key)) Set($"progress.quiz.{key}", score);
        }

        public static void RecordBankerLevel(int level)
        {
            if (level > BankerLevel) Set("progress.banker.level", level);
        }

        /// <summary>Done out of possible steps over all four scenarios: each tour, each quiz question, and Banker's five levels.</summary>
        public static (int done, int total) Overall()
        {
            int done = 0, total = 0;
            foreach (var key in new[] { "deadlock", "banker", "cpu", "memory" })
            {
                total += 1 + QuizLength;
                if (TourDone(key)) done++;
                done += Mathf.Max(0, QuizBest(key));
            }
            total += 5;
            done += Mathf.Min(5, BankerLevel);
            return (done, total);
        }

        /// <summary>Settings → reset progress (and tests).</summary>
        public static void ResetAll()
        {
            foreach (var key in new[] { "deadlock", "banker", "cpu", "memory" })
            {
                PlayerPrefs.DeleteKey($"progress.tour.{key}");
                PlayerPrefs.DeleteKey($"progress.quiz.{key}");
            }
            foreach (var scene in new[] { "GridlockHub", "GridlockParkingLot", "GridlockBanker", "GridlockCPUScheduling", "GridlockMemory" })
                PlayerPrefs.DeleteKey($"progress.visited.{scene}");
            PlayerPrefs.DeleteKey("progress.banker.level");
            PlayerPrefs.Save();
        }

        static void Set(string key, int value)
        {
            PlayerPrefs.SetInt(key, value);
            PlayerPrefs.Save();
        }
    }
}
