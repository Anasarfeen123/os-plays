using Gridlock.Core;
using Gridlock.Deadlock;
using TMPro;
using UnityEngine;

namespace Gridlock.UI
{
    /// <summary>"DEADLOCK DETECTED" panel (spec §8). Shown while a circular wait exists; pulses for attention.</summary>
    public class DeadlockAlert : MonoBehaviour
    {
        [SerializeField] SimulationManager simulation;
        [SerializeField] GameObject panel;
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text detail;
        [Tooltip("Optional content shown instead of the alert while there is no deadlock.")]
        [SerializeField] GameObject idleContent;

        public void Init(SimulationManager sim, GameObject alertPanel, TMP_Text titleText, TMP_Text detailText, GameObject idle = null)
        {
            idleContent = idle;
            simulation = sim;
            panel = alertPanel;
            title = titleText;
            detail = detailText;
        }

        void Awake()
        {
            panel.SetActive(false);
            simulation.StateEvaluated += Show;
        }

        void OnDestroy()
        {
            if (simulation != null) simulation.StateEvaluated -= Show;
        }

        void Update()
        {
            if (panel.activeSelf && title != null)
                title.color = Color.Lerp(Color.white, new Color(1f, 0.7f, 0.7f), ViewColor.Pulse);
        }

        void Show(DeadlockResult result)
        {
            panel.SetActive(result.HasDeadlock);
            if (idleContent != null) idleContent.SetActive(!result.HasDeadlock);
            if (!result.HasDeadlock) return;

            detail.text = $"Circular wait detected between {result.InvolvedCars.Count} processes\n"
                        + $"<b>{ViewColor.BigArrows(string.Join("\n", result.DescribeCycles()))}</b>\n"
                        + "<size=80%><color=#FECACA>Break it: select a car in the loop, then TOW CAR</color></size>";
        }
    }
}
