using System.Collections;
using System.Linq;
using Gridlock.Core;
using Gridlock.Deadlock;
using Gridlock.XR;
using UnityEngine;

namespace Gridlock.Guide
{
    /// <summary>
    /// Narrated guided tour of the Deadlock scene (about two minutes). It drives the real simulation in step with the
    /// voice: cars = processes, bays = resources, allocation, hold-and-wait, circular wait and deadlock, detection with
    /// the wait-for arrows, then the learner tows a car themselves and watches the jam unwind (recovery by preemption),
    /// and finally a pointer to Banker mode.
    /// </summary>
    public class GuideTour : NarratedTour
    {
        const string TourKey = "deadlock";

        /// <summary>True once the tour has started in this app session (tests set it to get the classic demo).</summary>
        public static bool Seen
        {
            get => IsSeen(TourKey);
            set => SetSeen(TourKey, value);
        }

        [SerializeField] SimulationManager simulation;
        [SerializeField] AnalysisOverlay overlay;
        [SerializeField] PlayerInteraction interaction;
        [SerializeField] GridlockAudio cityAudio;
        [Tooltip("How long the hands-on step waits for the learner before the guide tows a car itself.")]
        [SerializeField] float towWaitSeconds = 15f;

        bool restartingLot;

        protected override string Key => TourKey;
        protected override string HandsOnStep => "tow";
        protected override bool Counted(string id) => id != "tow_auto"; // a short aside within "tow"

        public void Init(SimulationManager sim, AnalysisOverlay analysis, PlayerInteraction player, GridlockAudio city)
        {
            simulation = sim;
            overlay = analysis;
            interaction = player;
            cityAudio = city;
        }

        protected override void Awake()
        {
            base.Awake();
            simulation.Restarted += () => { if (Running && !restartingLot) Stop(finished: false); };
        }

        protected override void PrepareFirstRun() => simulation.SuppressAutoPlay(); // before SimulationManager.Start runs the demo

        protected override void OnBegin()
        {
            simulation.SuppressAutoPlay();
            bool lotInUse = simulation.PlayerControlled || simulation.CarsMoving
                            || simulation.State.Cars.Any(c => simulation.State.GetHeld(c).Count > 0 || simulation.State.IsWaiting(c));
            if (lotInUse)
            {
                restartingLot = true;
                simulation.Restart(); // empty lot, cars back on the street
                restartingLot = false;
            }
            overlay.SetActive(false);
            interaction.Locked = true;
            if (cityAudio != null) cityAudio.Duck(true);
            interaction.SelectionChanged += OnSelection;
        }

        protected override void OnEnd(bool finished)
        {
            interaction.SelectionChanged -= OnSelection;
            interaction.Locked = false;
            simulation.EnableAutoPlay(); // RESTART replays the classic demo from now on
            if (cityAudio != null) cityAudio.Duck(false);
        }

        /// <summary>Early on, the classic demo plays instead; after the deadlock the lot is left as it is.</summary>
        protected override void OnSkip(bool early)
        {
            if (early) simulation.Restart();
        }

        void OnSelection(string car) =>
            UpdateAction(0, car != null ? $"TOW CAR {car}" : "SELECT A RED CAR FIRST", car != null);

        protected override IEnumerator Script()
        {
            var a = simulation.GetCar("A");
            var d = simulation.GetCar("D");
            var p1 = simulation.GetSpace("P1");
            // Aim a little north of the lot's centre so the lot sits in the clear area below the guide panel.
            var lotCentre = (simulation.GetSpace("P2").ParkingPoint + simulation.GetSpace("P5").ParkingPoint) * 0.5f + new Vector3(0f, 0f, 4f);
            var topRow = simulation.GetSpace("P2").ParkingPoint;
            var carRoof = new Vector3(0f, 2.4f, 0f);

            yield return new WaitForSeconds(1.2f); // let the scene fade in

            Say("welcome");
            Focus(lotCentre, 34f, 40f);
            yield return EndOfLine();

            Say("environment");
            Focus(a.transform.position + new Vector3(3f, 0f, 6f), 24f, 40f); // the car sits below the guide panel
            yield return At(0.1f);
            marker.Point(a.transform, carRoof, "CAR  =  PROCESS");
            yield return At(0.5f);
            Focus(p1.ParkingPoint, 22f, 42f);
            marker.Point(p1.ParkingPoint, "BAY  =  RESOURCE");
            yield return EndOfLine();
            marker.Hide();

            Say("allocate");
            Focus(lotCentre, 30f, 44f);
            foreach (var (car, bay) in new[] { ("A", "P1"), ("B", "P2"), ("C", "P3"), ("D", "P4") })
            {
                simulation.Apply(new ScenarioStep(car, bay));
                yield return new WaitForSeconds(1.7f);
            }
            yield return EndOfLine();
            yield return new WaitUntil(() => !simulation.CarsMoving);

            Say("wait");
            Focus(topRow, 24f, 40f);
            marker.Point(a.transform, carRoof, "WAITING");
            yield return At(0.3f);
            simulation.Apply(new ScenarioStep("A", "P2"));
            yield return EndOfLine();
            marker.Hide();

            Say("circle");
            yield return At(0.02f);
            simulation.Apply(new ScenarioStep("B", "P3"));
            yield return At(0.18f);
            simulation.Apply(new ScenarioStep("C", "P4"));
            yield return At(0.34f);
            Focus(lotCentre, 30f, 46f);
            simulation.Apply(new ScenarioStep("D", "P1"));
            yield return EndOfLine(0.8f);

            Say("detect");
            overlay.SetActive(true);
            Focus(lotCentre + Vector3.up * 2f, 34f, 48f);
            yield return EndOfLine();

            // Hands-on: the learner breaks the cycle.
            Say("tow");
            interaction.Locked = false;
            ShowActions(new GuideScreen.Action("SELECT A RED CAR FIRST", ViewColor.Deadlock, () => interaction.TowSelected(), interactable: false));
            OnSelection(interaction.SelectedCar);
            marker.Point(d.transform, carRoof, "TRY ME");
            float deadline = Time.time + LineLength + towWaitSeconds;
            yield return new WaitUntil(() => !simulation.LastResult.HasDeadlock || Time.time > deadline);
            if (simulation.LastResult.HasDeadlock)
            {
                Say("tow_auto");
                interaction.Select(Inspectable.Kind.Car, "D");
                yield return At(0.9f);
                interaction.TowSelected();
            }
            HideActions();
            marker.Hide();
            interaction.Locked = true;

            Say("unwind");
            float unwindDeadline = Time.time + 40f;
            yield return new WaitUntil(() => Unwound() || Time.time > unwindDeadline);
            yield return EndOfLine();
            overlay.SetActive(false);

            Say("outro");
            Focus(lotCentre, 34f, 40f);
            yield return EndOfLine();
        }

        bool Unwound() =>
            !simulation.CarsMoving && simulation.State.Cars.All(c => !simulation.State.IsWaiting(c) && simulation.State.GetHeld(c).Count <= 1);
    }
}
