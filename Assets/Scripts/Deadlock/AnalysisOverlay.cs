using System;
using System.Collections.Generic;
using Gridlock.Core;
using TMPro;
using UnityEngine;

namespace Gridlock.Deadlock
{
    /// <summary>
    /// Analyze mode (spec §7). Overlays the lot with wait-for arrows (A ──► B, red when part of a cycle) and an
    /// info card above every car ("CAR A / Owns: P1 / Wants: P2"). Also drives hover inspection of single cars/spaces.
    /// </summary>
    [RequireComponent(typeof(SimulationManager))]
    public class AnalysisOverlay : MonoBehaviour
    {
        [SerializeField] WaitArrow arrowTemplate;
        [Tooltip("Inactive template: world-space card with a TMP text child.")]
        [SerializeField] GameObject cardTemplate;
        [SerializeField] Color waitColor = new Color(0.98f, 0.75f, 0.14f);

        readonly List<WaitArrow> arrows = new List<WaitArrow>();
        readonly Dictionary<string, (GameObject root, TMP_Text text)> cards = new Dictionary<string, (GameObject, TMP_Text)>();
        (GameObject root, TMP_Text text) spaceCard;

        SimulationManager simulation;
        DeadlockAnalysis analysis;
        string hoveredCar;
        string hoveredSpace;
        string selectedCar;

        public bool IsActive { get; private set; }
        public DeadlockAnalysis Current => analysis;

        public event Action<bool> ActiveChanged;
        /// <summary>Raised whenever the analysis is recomputed while Analyze mode is on.</summary>
        public event Action<DeadlockAnalysis> AnalysisUpdated;

        public void Init(WaitArrow arrow, GameObject card)
        {
            arrowTemplate = arrow;
            cardTemplate = card;
        }

        public void Toggle() => SetActive(!IsActive);

        public void SetActive(bool active)
        {
            if (IsActive == active) return;
            IsActive = active;
            Rebuild();
            ActiveChanged?.Invoke(active);
        }

        /// <summary>Point-to-inspect: show the card for one car or space while hovered (id null = clear).</summary>
        public void Inspect(string carId, string spaceId)
        {
            hoveredCar = carId;
            hoveredSpace = spaceId;
            Rebuild();
        }

        /// <summary>The learner's selected car keeps its info card open.</summary>
        public void SetSelectedCar(string carId)
        {
            selectedCar = carId;
            Rebuild();
        }

        void Awake()
        {
            simulation = GetComponent<SimulationManager>();
            simulation.StateEvaluated += _ => Rebuild();
            simulation.Restarted += () => SetActive(false);
            arrowTemplate.gameObject.SetActive(false);
            cardTemplate.SetActive(false);
        }

        void Rebuild()
        {
            if (simulation.State == null) return;
            analysis = new DeadlockAnalysis(simulation.State);

            // Arrows only in Analyze mode.
            int used = 0;
            if (IsActive)
            {
                foreach (var edge in analysis.Edges)
                {
                    var arrow = used < arrows.Count ? arrows[used] : NewArrow();
                    arrow.name = $"WaitArrow {edge.From}→{edge.To}";
                    used++;
                }
            }
            for (int i = used; i < arrows.Count; i++) arrows[i].Hide();
            for (int i = 0; i < used; i++) arrows[i].gameObject.SetActive(true);

            // Car cards: all in Analyze mode, otherwise only the hovered car.
            foreach (var car in analysis.Cars)
            {
                bool visible = IsActive || car.Car == hoveredCar || car.Car == selectedCar;
                var card = CardFor(car.Car);
                card.root.SetActive(visible);
                simulation.GetCar(car.Car)?.SetCardShown(visible);
                if (!visible) continue;
                var header = car.InCycle ? $"<color={ViewColor.Hex(ViewColor.Deadlock)}>CAR {car.Car}</color>" : $"CAR {car.Car}";
                var body = car.Describe();
                card.text.text = header + body.Substring(body.IndexOf('\n'))
                                 + (car.WaitsOn != null ? $"\nWaits on: CAR {car.WaitsOn}" : "");
            }

            // Space card on hover.
            var space = simulation.GetSpace(hoveredSpace);
            if (spaceCard.root == null) spaceCard = NewCard("Card Space");
            spaceCard.root.SetActive(space != null);
            if (space != null)
            {
                var owner = simulation.State.GetOwner(space.Id);
                var waiting = new List<string>();
                foreach (var c in simulation.State.Cars)
                    if (simulation.State.GetRequest(c) == space.Id) waiting.Add(c);
                spaceCard.text.text = $"SPACE {space.Id}\nOwner: {(owner != null ? "CAR " + owner : "free")}"
                                      + (waiting.Count > 0 ? $"\nRequested by: {string.Join(", ", waiting)}" : "");
                spaceCard.root.transform.position = space.ParkingPoint + Vector3.up * 5.2f;
            }

            if (IsActive) AnalysisUpdated?.Invoke(analysis);
        }

        void LateUpdate()
        {
            if (analysis == null) return;

            // Cars move, so arrows and cards follow every frame.
            if (IsActive)
            {
                for (int i = 0; i < analysis.Edges.Count && i < arrows.Count; i++)
                {
                    var e = analysis.Edges[i];
                    var from = simulation.GetCar(e.From);
                    var to = simulation.GetCar(e.To);
                    if (from == null || to == null) continue;
                    var color = analysis.CycleEdges.Contains(e) ? ViewColor.Deadlock : waitColor;
                    arrows[i].Show(from.TopAnchor, to.TopAnchor, e.Resource, color);
                }
            }

            foreach (var pair in cards)
            {
                if (!pair.Value.root.activeSelf) continue;
                var car = simulation.GetCar(pair.Key);
                if (car != null) pair.Value.root.transform.position = car.CardAnchor;
            }
        }

        WaitArrow NewArrow()
        {
            var arrow = Instantiate(arrowTemplate, transform);
            arrows.Add(arrow);
            return arrow;
        }

        (GameObject root, TMP_Text text) CardFor(string car)
        {
            if (!cards.TryGetValue(car, out var card))
            {
                card = NewCard("Card " + car);
                cards[car] = card;
            }
            return card;
        }

        (GameObject, TMP_Text) NewCard(string name)
        {
            var go = Instantiate(cardTemplate, transform);
            go.name = name;
            return (go, go.GetComponentInChildren<TMP_Text>(true));
        }
    }
}
