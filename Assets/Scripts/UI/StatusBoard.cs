using System.Collections.Generic;
using System.Text;
using Gridlock.Core;
using Gridlock.Deadlock;
using TMPro;
using UnityEngine;

namespace Gridlock.UI
{
    /// <summary>
    /// Scoreboard content: a live status line, an event log of recent steps, and the analysis panel
    /// (cycle, plain-language explanation, Coffman conditions) shown while Analyze mode is on.
    /// </summary>
    public class StatusBoard : MonoBehaviour
    {
        const int LogLines = 8;

        [SerializeField] SimulationManager simulation;
        [SerializeField] AnalysisOverlay overlay;
        [SerializeField] TMP_Text status;
        [SerializeField] TMP_Text log;
        [SerializeField] GameObject analysisPanel;
        [SerializeField] TMP_Text analysisText;

        readonly Queue<string> entries = new Queue<string>();

        public void Init(SimulationManager sim, AnalysisOverlay analysis, TMP_Text statusText, TMP_Text logText,
                         GameObject panel, TMP_Text panelText)
        {
            simulation = sim;
            overlay = analysis;
            status = statusText;
            log = logText;
            analysisPanel = panel;
            analysisText = panelText;
        }

        void Awake()
        {
            simulation.StateEvaluated += UpdateStatus;
            simulation.StepApplied += AddEntry;
            simulation.Restarted += ClearLog;
            overlay.ActiveChanged += active => analysisPanel.SetActive(active);
            overlay.AnalysisUpdated += ShowAnalysis;
            analysisPanel.SetActive(false);
            log.text = "";
        }

        void UpdateStatus(DeadlockResult result)
        {
            var state = simulation.State;
            int parked = 0, waiting = 0;
            foreach (var car in state.Cars)
            {
                if (state.GetHeld(car).Count > 0) parked++;
                if (state.IsWaiting(car)) waiting++;
            }

            var verdict = result.HasDeadlock
                ? $"<color={ViewColor.Hex(ViewColor.Deadlock)}>DEADLOCK</color>"
                : waiting > 0 ? $"<color={ViewColor.Hex(ViewColor.Waiting)}>WAITING</color>"
                : $"<color={ViewColor.Hex(ViewColor.Safe)}>RUNNING</color>";
            status.text = $"{verdict}\n<size=55%><color=#94A3B8>Parked {parked}/{state.Cars.Count}  ·  Waiting {waiting}  ·  Free {FreeSpaces()}/{state.Resources.Count}</color></size>";
        }

        int FreeSpaces()
        {
            int free = 0;
            foreach (var r in simulation.State.Resources)
                if (simulation.State.GetOwner(r) == null) free++;
            return free;
        }

        void AddEntry(string message)
        {
            var colored = message.Contains("WAITING") ? $"<color={ViewColor.Hex(ViewColor.Waiting)}>{message}</color>" : message;
            entries.Enqueue(colored);
            while (entries.Count > LogLines) entries.Dequeue();
            log.text = string.Join("\n", entries);
        }

        void ClearLog()
        {
            entries.Clear();
            log.text = "";
        }

        void ShowAnalysis(DeadlockAnalysis analysis)
        {
            var sb = new StringBuilder();
            var red = ViewColor.Hex(ViewColor.Deadlock);
            var green = ViewColor.Hex(ViewColor.Safe);

            sb.AppendLine("<b>WAIT-FOR GRAPH</b>");
            if (analysis.Result.HasDeadlock)
                foreach (var cycle in analysis.Result.DescribeCycles())
                    sb.AppendLine($"<color={red}><b>{ViewColor.BigArrows(cycle)}</b></color>");
            else sb.AppendLine($"<color={green}>No cycle — no deadlock</color>");

            sb.AppendLine();
            foreach (var line in analysis.Explain()) sb.AppendLine("• " + line);
            if (analysis.Edges.Count == 0) sb.AppendLine("• No car is waiting");

            sb.AppendLine();
            sb.AppendLine("<b>COFFMAN CONDITIONS</b>");
            foreach (var (condition, holds) in analysis.CoffmanConditions())
                sb.AppendLine($"<color={(holds ? red : green)}>{(holds ? "■" : "□")}</color> {condition}");
            if (analysis.Result.HasDeadlock)
                sb.Append($"<color={red}><b>All four hold, so the cars are deadlocked</b></color>");

            analysisText.text = sb.ToString();
        }
    }
}
