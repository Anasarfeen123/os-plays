using Gridlock.Core;
using Gridlock.Deadlock;
using Gridlock.XR;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gridlock.UI
{
    /// <summary>
    /// ANALYZE / TOW CAR / RESTART buttons plus a live hint line. Used on the scoreboard, the podium next to the VR start
    /// point and the desktop HUD; all copies drive the same simulation.
    /// </summary>
    public class ControlPanel : MonoBehaviour
    {
        [SerializeField] SimulationManager simulation;
        [SerializeField] AnalysisOverlay overlay;
        [SerializeField] PlayerInteraction interaction;
        [SerializeField] Button analyzeButton;
        [SerializeField] TMP_Text analyzeLabel;
        [SerializeField] Button towButton;
        [SerializeField] TMP_Text towLabel;
        [SerializeField] Button restartButton;
        [SerializeField] TMP_Text hint;

        public void Init(SimulationManager sim, AnalysisOverlay analysis, PlayerInteraction player, Button analyze, TMP_Text analyzeText,
                         Button tow, TMP_Text towText, Button restart, TMP_Text hintText)
        {
            simulation = sim;
            overlay = analysis;
            interaction = player;
            analyzeButton = analyze;
            analyzeLabel = analyzeText;
            towButton = tow;
            towLabel = towText;
            restartButton = restart;
            hint = hintText;
        }

        void Awake()
        {
            analyzeButton.onClick.AddListener(overlay.Toggle);
            towButton.onClick.AddListener(interaction.TowSelected);
            restartButton.onClick.AddListener(simulation.Restart);
            overlay.ActiveChanged += UpdateAnalyzeLabel;
            interaction.SelectionChanged += UpdateTow;
            interaction.Feedback += ShowHint;
            UpdateAnalyzeLabel(overlay.IsActive);
            UpdateTow(interaction.SelectedCar);
        }

        void OnDestroy()
        {
            if (overlay != null) overlay.ActiveChanged -= UpdateAnalyzeLabel;
            if (interaction != null)
            {
                interaction.SelectionChanged -= UpdateTow;
                interaction.Feedback -= ShowHint;
            }
        }

        void UpdateAnalyzeLabel(bool active) => analyzeLabel.text = active ? "HIDE ANALYSIS" : "ANALYZE";

        void UpdateTow(string car)
        {
            towButton.interactable = car != null;
            towLabel.text = car != null ? $"TOW CAR {car}" : "TOW CAR";
        }

        void ShowHint(string text)
        {
            if (hint != null) hint.text = text;
        }
    }
}
