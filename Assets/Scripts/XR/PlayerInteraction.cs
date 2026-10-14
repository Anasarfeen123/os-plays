using System;
using Gridlock.Core;
using Gridlock.Deadlock;
using UnityEngine;

namespace Gridlock.XR
{
    /// <summary>
    /// The learner's hands-on controls, shared by XR (select with the controller) and desktop (left click):
    /// select a car, then select a bay to make that car request it; TOW CAR preempts the selected car to break a deadlock.
    /// </summary>
    [RequireComponent(typeof(SimulationManager))]
    public class PlayerInteraction : MonoBehaviour
    {
        SimulationManager simulation;
        AnalysisOverlay overlay;

        public string SelectedCar { get; private set; }

        /// <summary>Selected car id (null when nothing is selected).</summary>
        public event Action<string> SelectionChanged;
        /// <summary>Short instruction or refusal text for the control panels.</summary>
        public event Action<string> Feedback;

        void Awake()
        {
            simulation = GetComponent<SimulationManager>();
            overlay = GetComponent<AnalysisOverlay>();
            simulation.Restarted += () => SetSelected(null);
            simulation.DeadlockStarted += _ => { if (SelectedCar == null) Feedback?.Invoke(DefaultHint()); };
        }

        void Start() => Feedback?.Invoke(DefaultHint());

        /// <summary>While the guided tour narrates, the learner's clicks are held back until it is their turn.</summary>
        public bool Locked { get; set; }

        public void Select(Inspectable.Kind kind, string id)
        {
            if (Locked)
            {
                Feedback?.Invoke("The guided tour is running. Your turn comes soon, or press SKIP.");
                return;
            }
            if (kind == Inspectable.Kind.Car)
            {
                SetSelected(SelectedCar == id ? null : id);
                return;
            }

            if (SelectedCar == null)
            {
                Feedback?.Invoke("Select a car first, then the bay it should drive to.");
                return;
            }

            var car = SelectedCar;
            var refusal = simulation.PlayerRequest(car, id);
            if (refusal != null)
            {
                Feedback?.Invoke(refusal);
                return;
            }
            SetSelected(null);
            var waiting = simulation.State.IsWaiting(car);
            Feedback?.Invoke(waiting
                ? $"{id} is taken, so car {car} keeps its bay and WAITS. That is hold-and-wait."
                : $"Car {car} is driving to {id}.");
        }

        public void TowSelected()
        {
            if (Locked) return;
            if (SelectedCar == null)
            {
                Feedback?.Invoke("Select the car to tow first.");
                return;
            }
            var car = SelectedCar;
            var refusal = simulation.Tow(car);
            SetSelected(null);
            Feedback?.Invoke(refusal ?? $"Car {car} is towed away. Watch the waiting cars move up one by one.");
        }

        void SetSelected(string car)
        {
            if (SelectedCar == car) return;
            simulation.GetCar(SelectedCar)?.SetSelected(false);
            SelectedCar = car;
            simulation.GetCar(car)?.SetSelected(true);
            if (overlay != null) overlay.SetSelectedCar(car);
            SelectionChanged?.Invoke(car);
            Feedback?.Invoke(car != null
                ? $"Car {car} selected. Pick a bay for it, or TOW CAR to preempt it."
                : DefaultHint());
        }

        string DefaultHint() => simulation.LastResult.HasDeadlock
            ? "Deadlock! Select a car in the cycle, then press TOW CAR."
            : "Select a car, then a bay, to send it there.";
    }
}
