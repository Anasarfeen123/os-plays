using System.Linq;
using System.Text;
using Gridlock.Core;
using TMPro;
using UnityEngine;

namespace Gridlock.Hub
{
    /// <summary>
    /// The learner's progress on the control room monitor: a status strip on each scenario card (tour done, best quiz
    /// score, Banker levels cleared) and the overall count beside SYSTEM ONLINE. Read from <see cref="Progress"/> on load.
    /// </summary>
    public class HubProgressDisplay : MonoBehaviour
    {
        [SerializeField] TMP_Text overall;
        [SerializeField] TMP_Text[] strips;
        [Tooltip("Per strip: the tour keys it reports, comma separated (\"deadlock,banker\").")]
        [SerializeField] string[] keys;

        public void Init(TMP_Text overallText, TMP_Text[] stripTexts, string[] stripKeys)
        {
            overall = overallText;
            strips = stripTexts;
            keys = stripKeys;
        }

        void Start() => Refresh();

        public void Refresh()
        {
            var (done, total) = Progress.Overall();
            overall.text = $"MODULE PROGRESS  <color=#FBBF24>{done} / {total}</color>";
            for (int i = 0; i < strips.Length && i < keys.Length; i++)
                strips[i].text = Describe(keys[i].Split(','));
        }

        static readonly System.Collections.Generic.Dictionary<string, string> Names = new System.Collections.Generic.Dictionary<string, string>
        {
            { "deadlock", "DEADLOCK" }, { "banker", "BANKER" }, { "cpu", "TOUR" }, { "memory", "TOUR" },
        };

        /// <summary>"DEADLOCK  TOUR DONE  QUIZ 3/3    BANKER  TOUR  QUIZ 2/3  LV 3/5", or NOT STARTED.</summary>
        static string Describe(string[] tourKeys)
        {
            bool started = tourKeys.Any(k => Progress.TourDone(k) || Progress.QuizBest(k) >= 0) ||
                           (tourKeys.Contains("banker") && Progress.BankerLevel > 0);
            if (!started) return "<color=#94A3B8>NOT STARTED</color>";
            var text = new StringBuilder();
            foreach (var key in tourKeys)
            {
                if (text.Length > 0) text.Append("    ");
                if (tourKeys.Length > 1) text.Append(Names[key]).Append("  ");
                text.Append(Progress.TourDone(key) ? "<color=#22C55E>TOUR DONE</color>" : "<color=#94A3B8>TOUR</color>");
                int best = Progress.QuizBest(key);
                text.Append("  QUIZ ").Append(best < 0 ? "–" : $"{best}/{Progress.QuizLength}");
                if (key == "banker") text.Append($"  LV {Progress.BankerLevel}/5");
            }
            return text.ToString();
        }
    }
}
