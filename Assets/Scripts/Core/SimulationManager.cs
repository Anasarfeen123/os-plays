using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gridlock.Deadlock;
using UnityEngine;

namespace Gridlock.Core
{
    /// <summary>One scripted action: a car requests (or releases) a parking space.</summary>
    [Serializable]
    public class ScenarioStep
    {
        public string car;
        public string resource;
        public bool release;

        public ScenarioStep(string car, string resource, bool release = false)
        {
            this.car = car;
            this.resource = resource;
            this.release = release;
        }

        public override string ToString() => release ? $"{car} releases {resource}" : $"{car} requests {resource}";
    }

    /// <summary>
    /// Connects the pure <see cref="ParkingState"/> to the scene. It builds the state from the
    /// <see cref="CarView"/>/<see cref="ParkingSpaceView"/> objects in the scene, keeps their visuals in sync, and
    /// runs <see cref="DeadlockDetector"/> on every state change.
    /// </summary>
    public class SimulationManager : MonoBehaviour
    {
        [Tooltip("Play the scenario automatically on Start.")]
        [SerializeField] bool autoPlay = true;
        [Tooltip("Pause after the cars come to rest before the next step.")]
        [SerializeField] float stepInterval = 1.2f;

        [Tooltip("Point on the street in front of the entry.")]
        [SerializeField] Transform streetPoint;
        [Tooltip("Lot entry, at the start of the driving aisle.")]
        [SerializeField] Transform entryPoint;

        [Tooltip("Default: Level 4 showcase. Each car takes its own space, then requests the next car's space (A → B → C → D → A).")]
        [SerializeField] List<ScenarioStep> scenario = new List<ScenarioStep>
        {
            new ScenarioStep("A", "P1"), new ScenarioStep("B", "P2"),
            new ScenarioStep("C", "P3"), new ScenarioStep("D", "P4"),
            new ScenarioStep("A", "P2"), new ScenarioStep("B", "P3"),
            new ScenarioStep("C", "P4"), new ScenarioStep("D", "P1"),
        };

        readonly Dictionary<string, CarView> cars = new Dictionary<string, CarView>();
        readonly Dictionary<string, ParkingSpaceView> spaces = new Dictionary<string, ParkingSpaceView>();
        // Bay each car is parked in / driving to, and what it held at the last sync (to spot newly granted bays).
        readonly Dictionary<string, string> parkTarget = new Dictionary<string, string>();
        readonly Dictionary<string, List<string>> lastHeld = new Dictionary<string, List<string>>();
        Coroutine scenarioRoutine;

        public ParkingState State { get; private set; }
        public DeadlockResult LastResult { get; private set; } = new DeadlockResult();
        public bool ScenarioFinished { get; private set; }
        /// <summary>True once the learner has taken control (the scripted demo stops).</summary>
        public bool PlayerControlled { get; private set; }

        /// <summary>Raised after every state change, once visuals are synced and detection has run.</summary>
        public event Action<DeadlockResult> StateEvaluated;
        /// <summary>Human-readable description of each applied step, for the event log.</summary>
        public event Action<string> StepApplied;
        /// <summary>A car's request was answered: (car, resource, granted). Not granted means the car now waits.</summary>
        public event Action<string, string, bool> RequestResolved;
        /// <summary>A car was handed a space it was waiting for (after a release).</summary>
        public event Action<string> CarUnblocked;
        /// <summary>Raised once when a deadlock appears (not on every evaluation while it persists).</summary>
        public event Action<DeadlockResult> DeadlockStarted;
        /// <summary>A car was towed away (preempted) to break a deadlock.</summary>
        public event Action<string> CarTowed;
        /// <summary>Raised when the simulation is restarted from an empty lot.</summary>
        public event Action Restarted;

        /// <summary>The guided tour drives the lot itself: no scripted demo on Start or Restart until re-enabled.</summary>
        public void SuppressAutoPlay() => autoPlay = false;
        public void EnableAutoPlay() => autoPlay = true;

        public void Init(Transform street, Transform entry)
        {
            streetPoint = street;
            entryPoint = entry;
        }

        public CarView GetCar(string id) => id != null && cars.TryGetValue(id, out var c) ? c : null;
        public ParkingSpaceView GetSpace(string id) => id != null && spaces.TryGetValue(id, out var s) ? s : null;
        public bool CarsMoving => cars.Values.Any(c => c.IsMoving);

        void Awake()
        {
            State = new ParkingState();

            foreach (var car in FindObjectsByType<CarView>(FindObjectsSortMode.None).OrderBy(c => c.Id))
            {
                State.AddCar(car.Id);
                cars.Add(car.Id, car);
            }
            foreach (var space in FindObjectsByType<ParkingSpaceView>(FindObjectsSortMode.None).OrderBy(s => s.Id))
            {
                State.AddResource(space.Id);
                spaces.Add(space.Id, space);
            }

            State.Changed += OnStateChanged;
        }

        void Start()
        {
            OnStateChanged();
            if (autoPlay) scenarioRoutine = StartCoroutine(PlayScenario());
        }

        void Update() => FinishMoves();

        void OnDestroy()
        {
            if (State != null) State.Changed -= OnStateChanged;
        }

        IEnumerator PlayScenario()
        {
            ScenarioFinished = false;
            yield return new WaitUntil(() => ScenePlacement.Ready); // phone AR: not before the lot is on the table
            foreach (var step in scenario)
            {
                yield return new WaitForSeconds(stepInterval);
                yield return new WaitUntil(() => !CarsMoving);
                Apply(step);
            }
            yield return new WaitUntil(() => !CarsMoving);
            ScenarioFinished = true;
        }

        public void Apply(ScenarioStep step)
        {
            try
            {
                string message;
                if (step.release)
                {
                    State.Release(step.car, step.resource);
                    var unblocked = State.RetryWaiting();
                    message = $"{step.car} releases {step.resource}"
                              + (unblocked.Count > 0 ? $" — {string.Join(", ", unblocked)} can proceed" : "");
                    foreach (var car in unblocked) CarUnblocked?.Invoke(car);
                }
                else
                {
                    bool granted = State.Request(step.car, step.resource);
                    RequestResolved?.Invoke(step.car, step.resource, granted);
                    message = granted
                        ? $"{step.car} requests {step.resource} — granted"
                        : $"{step.car} requests {step.resource} — WAITING (held by {State.GetOwner(step.resource)})";
                }
                Debug.Log($"[GRIDLOCK] {message}");
                StepApplied?.Invoke(message);
            }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException)
            {
                Debug.LogError($"[GRIDLOCK] Invalid scenario step '{step}': {e.Message}", this);
            }
        }

        /// <summary>Empty the lot, put every car back at its start and replay the scenario.</summary>
        [ContextMenu("Restart Simulation")]
        public void Restart()
        {
            StopAllCoroutines();
            PlayerControlled = false;
            parkTarget.Clear();
            lastHeld.Clear();
            foreach (var car in cars.Values) car.SnapHome();
            foreach (var car in State.Cars) State.ReleaseAll(car);
            Restarted?.Invoke();
            if (autoPlay) scenarioRoutine = StartCoroutine(PlayScenario());
        }

        // ---------------------------------------------------------------------------------------------------------
        // Learner actions
        // ---------------------------------------------------------------------------------------------------------

        void TakeControl()
        {
            if (PlayerControlled) return;
            PlayerControlled = true;
            if (scenarioRoutine != null) StopCoroutine(scenarioRoutine);
            scenarioRoutine = null;
        }

        /// <summary>The learner directs <paramref name="car"/> to <paramref name="space"/>. Returns a reason if refused.</summary>
        public string PlayerRequest(string car, string space)
        {
            var view = GetCar(car);
            if (view == null || GetSpace(space) == null) return "Unknown car or bay.";
            if (view.IsMoving) return $"Car {car} is still driving. Wait until it stops.";
            if (State.GetOwner(space) == car) return $"Car {car} already holds {space}.";
            TakeControl();
            Apply(new ScenarioStep(car, space));
            return null;
        }

        /// <summary>
        /// Deadlock recovery by preemption: the car loses every bay it holds and is towed back to the street.
        /// Freed bays go to cars waiting for them once the tow has cleared the bay.
        /// </summary>
        public string Tow(string car)
        {
            if (GetCar(car) == null) return "Unknown car.";
            var held = State.GetHeld(car);
            if (held.Count == 0) return $"Car {car} is not holding any bay.";
            TakeControl();

            bool inCycle = LastResult.InvolvedCars.Contains(car);
            State.ReleaseAll(car);
            parkTarget.Remove(car);
            var message = $"{car} towed away (preempted) — frees {string.Join(", ", held)}";
            Debug.Log($"[GRIDLOCK] {message}");
            StepApplied?.Invoke(message);
            CarTowed?.Invoke(car);
            if (inCycle) StepApplied?.Invoke("Circular wait broken");
            StartCoroutine(GrantAfter(1.6f));
            return null;
        }

        IEnumerator GrantAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            GrantWaiting();
        }

        void GrantWaiting()
        {
            var wanted = State.Cars.ToDictionary(c => c, c => State.GetRequest(c));
            foreach (var car in State.RetryWaiting())
            {
                var message = $"{car} gets {wanted[car]} — no longer waiting";
                Debug.Log($"[GRIDLOCK] {message}");
                StepApplied?.Invoke(message);
                CarUnblocked?.Invoke(car);
            }
        }

        /// <summary>
        /// A car moves bay to bay: it keeps its old bay until it has driven into the new one, then frees the old one
        /// (hold-and-wait). Freed bays are handed to whoever is waiting for them.
        /// </summary>
        void FinishMoves()
        {
            foreach (var pair in cars)
            {
                var car = pair.Value;
                if (car.IsMoving || !parkTarget.TryGetValue(pair.Key, out var target)) continue;
                var held = State.GetHeld(pair.Key);
                if (held.Count < 2 || car.CurrentSpace == null || car.CurrentSpace.Id != target) continue;

                var freed = held.Where(r => r != target).ToList();
                foreach (var r in freed) State.Release(pair.Key, r);
                var message = $"{pair.Key} moves into {target} and frees {string.Join(", ", freed)}";
                Debug.Log($"[GRIDLOCK] {message}");
                StepApplied?.Invoke(message);
                GrantWaiting();
                return; // state changed; re-evaluate next frame
            }
        }

        void OnStateChanged()
        {
            bool wasDeadlocked = LastResult.HasDeadlock;
            LastResult = DeadlockDetector.Detect(State);
            SyncViews();
            StateEvaluated?.Invoke(LastResult);
            if (LastResult.HasDeadlock && !wasDeadlocked) DeadlockStarted?.Invoke(LastResult);
        }

        void SyncViews()
        {
            foreach (var pair in spaces)
                pair.Value.SetOwner(GetCar(State.GetOwner(pair.Key)));

            foreach (var pair in cars)
            {
                var car = pair.Value;
                var held = State.GetHeld(pair.Key);

                // A newly granted bay becomes where the car is heading; otherwise it stays where it is.
                var previous = lastHeld.TryGetValue(pair.Key, out var p) ? p : new List<string>();
                var gained = held.FirstOrDefault(r => !previous.Contains(r));
                if (gained != null) parkTarget[pair.Key] = gained;
                else if (!parkTarget.TryGetValue(pair.Key, out var t) || !held.Contains(t))
                {
                    if (held.Count > 0) parkTarget[pair.Key] = held[0];
                    else parkTarget.Remove(pair.Key);
                }
                lastHeld[pair.Key] = held;

                var target = parkTarget.TryGetValue(pair.Key, out var id) ? GetSpace(id) : null;
                if (target != car.CurrentSpace) SendCar(car, target);
                car.SetWaiting(State.GetRequest(pair.Key));
            }
        }

        /// <summary>Route: out of the current bay (or in from the street), along the aisle, into the target bay (or home).</summary>
        void SendCar(CarView car, ParkingSpaceView target)
        {
            var from = car.CurrentSpace;
            var route = new List<Vector3>();

            if (from != null) route.Add(from.ApproachPoint);
            else
            {
                if (streetPoint != null) route.Add(streetPoint.position);
                if (entryPoint != null) route.Add(entryPoint.position);
            }

            if (target != null)
            {
                route.Add(target.ApproachPoint);
                route.Add(target.ParkingPoint);
                car.DriveTo(route, target, target.ParkedRotation, reverseOut: from != null);
            }
            else
            {
                if (entryPoint != null) route.Add(entryPoint.position);
                if (streetPoint != null) route.Add(streetPoint.position);
                route.Add(car.HomePosition);
                car.DriveTo(route, null, car.HomeRotation, reverseOut: from != null);
            }
        }
    }
}
