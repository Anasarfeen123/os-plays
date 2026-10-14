using System.Linq;
using System.Text;
using Gridlock.Banker;
using Gridlock.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gridlock.UI
{
    /// <summary>
    /// Banker game UI: level, intro, the request, the Allocation / Max / Need table with Available, GRANT / DENY / NEXT,
    /// the verdict with the step-by-step safety check, and the score. Any text field may be left empty for compact
    /// panels (the podium and the floating gate card).
    /// </summary>
    public class BankerPanel : MonoBehaviour
    {
        [SerializeField] BankerGame game;
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text progress;
        [SerializeField] TMP_Text intro;
        [SerializeField] TMP_Text request;
        [SerializeField] TMP_Text table;
        [SerializeField] GameObject verdictPanel;
        [SerializeField] TMP_Text verdictHeadline;
        [SerializeField] TMP_Text verdictBody;
        [SerializeField] TMP_Text score;
        [SerializeField] Button grantButton;
        [SerializeField] Button denyButton;
        [SerializeField] Button nextButton;
        [SerializeField] bool showTrace = true;
        [SerializeField] bool scoreOnTwoLines;

        public void Init(BankerGame bankerGame, TMP_Text titleText, TMP_Text progressText, TMP_Text introText, TMP_Text requestText,
                         TMP_Text tableText, GameObject verdict, TMP_Text headline, TMP_Text body, TMP_Text scoreText,
                         Button grant, Button deny, Button next, bool trace = true, bool twoLineScore = false)
        {
            scoreOnTwoLines = twoLineScore;
            game = bankerGame;
            title = titleText;
            progress = progressText;
            intro = introText;
            request = requestText;
            table = tableText;
            verdictPanel = verdict;
            verdictHeadline = headline;
            verdictBody = body;
            score = scoreText;
            grantButton = grant;
            denyButton = deny;
            nextButton = next;
            showTrace = trace;
        }

        void Awake()
        {
            game.ChallengeLoaded += OnLoaded;
            game.Decided += OnDecided;
            game.LockChanged += () => SetButtons(game.Verdict == null);
            grantButton?.onClick.AddListener(game.Grant);
            denyButton?.onClick.AddListener(game.Deny);
            nextButton?.onClick.AddListener(game.Next);
            if (verdictPanel != null) verdictPanel.SetActive(false);
        }

        void OnDestroy()
        {
            if (game == null) return;
            game.ChallengeLoaded -= OnLoaded;
            game.Decided -= OnDecided;
        }

        void OnLoaded(BankerChallenge c)
        {
            Set(title, c.Title);
            Set(progress, game.Progress);
            Set(intro, c.Intro);
            Set(request, $"{FleetName(c.Process)} requests <b>{BankerChallenge.Vec(c.Request)}</b>");
            Set(table, Table(c));
            if (verdictPanel != null) verdictPanel.SetActive(false);
            SetButtons(deciding: true);
            UpdateScore();
        }

        void OnDecided(BankerVerdict v, int delta)
        {
            if (verdictPanel != null) verdictPanel.SetActive(true);
            var color = v.Correct ? ViewColor.Safe : ViewColor.Deadlock;
            var points = delta > 0 ? $"+{delta}" : delta < 0 ? $"{delta}" : "";
            Set(verdictHeadline, $"<color={ViewColor.Hex(color)}>{v.Headline}</color>  <size=70%>{points}</size>");
            Set(verdictBody, ViewColor.BigArrows(v.Explanation + (showTrace ? Trace(v) : "")));
            SetButtons(deciding: false);
            UpdateScore();
        }

        void SetButtons(bool deciding)
        {
            bool open = !game.Locked;
            if (grantButton != null) grantButton.interactable = deciding && open;
            if (denyButton != null) denyButton.interactable = deciding && open;
            if (nextButton != null) nextButton.interactable = !deciding && open;
        }

        void UpdateScore() =>
            Set(score, $"SCORE {game.Points}{(scoreOnTwoLines ? "\n" : "   ")}<size=75%><color=#22C55E>{game.CorrectCount} right</color>  "
                       + $"<color=#EF4444>{game.WrongCount} wrong</color>  streak {game.Streak}</size>");

        static void Set(TMP_Text t, string s)
        {
            if (t != null) t.text = s;
        }

        static string FleetName(int i) => $"<color={ViewColor.Hex(ViewColor.Fleet[i])}><b>FLEET {BankerChallenge.Fleets[i]}</b></color>";

        /// <summary>Monospaced Allocation / Max / Need table with the requesting fleet's row highlighted.</summary>
        static string Table(BankerChallenge c)
        {
            var s = c.State;
            var sb = new StringBuilder("<mspace=0.62em>");
            sb.AppendLine("<color=#94A3B8>FLEET  ALLOC   MAX    NEED</color>");
            sb.AppendLine("<color=#94A3B8>       N  S   N  S   N  S</color>");
            for (int i = 0; i < s.ProcessCount; i++)
            {
                var row = $"  {BankerChallenge.Fleets[i]}    {s.Allocation[i, 0]}  {s.Allocation[i, 1]}   {s.Max[i, 0]}  {s.Max[i, 1]}   {s.Need(i, 0)}  {s.Need(i, 1)}";
                var line = $"<color={ViewColor.Hex(ViewColor.Fleet[i])}>{row}</color>";
                if (i == c.Process) line = $"<mark=#FBBF2433>{line} ←</mark>";
                sb.AppendLine(line);
            }
            sb.Append($"<color=#FBBF24>  FREE {s.Available[0]}  {s.Available[1]}</color>");
            sb.Append("</mspace>");
            return sb.ToString();
        }

        /// <summary>The safety algorithm, one line per step, run on the lot as if the request were granted.</summary>
        static string Trace(BankerVerdict v)
        {
            if (v.Trace == null) return "";
            var sb = new StringBuilder("\n\n<b>SAFETY CHECK</b> <size=80%>(if granted)</size>\n");
            sb.AppendLine($"Free bays: {BankerChallenge.Vec(v.Trace.InitialWork)}");
            int n = 1;
            foreach (var step in v.Trace.Steps)
                sb.AppendLine($"{n++}. {FleetName(step.Process)} needs {BankerChallenge.Vec(step.Need)}, fits → finishes, "
                              + $"free becomes {BankerChallenge.Vec(step.WorkAfter)}");
            foreach (var stuck in v.Trace.Stuck)
                sb.AppendLine($"<color=#EF4444>STUCK</color> {FleetName(stuck)} can never finish");
            return sb.ToString().TrimEnd();
        }
    }
}
