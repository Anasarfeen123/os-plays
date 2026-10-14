using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gridlock.Core;
using UnityEngine;

namespace Gridlock.Banker
{
    /// <summary>
    /// Makes the Banker matrices physical. Parked cars are a fleet's <b>Allocation</b>, cars queued on the street are
    /// its remaining <b>Need</b>, cars at the gate are the current <b>Request</b>, and empty bays are <b>Available</b>.
    /// Bays P1–P3 form Zone N and P4–P6 Zone S.
    /// </summary>
    public class BankerLotView : MonoBehaviour
    {
        [Tooltip("Fleet-major: UnitsPerFleet cars for A, then B, C, D.")]
        [SerializeField] CarView[] units;
        [Tooltip("P1–P6. Index / 3 is the zone.")]
        [SerializeField] ParkingSpaceView[] bays;
        [SerializeField] Transform[] streetSlots;
        [SerializeField] Transform[] gateSlots;
        [SerializeField] float traceStepSeconds = 0.9f;

        readonly List<CarView>[] parked = Enumerable.Range(0, BankerChallenge.Fleets.Length).Select(_ => new List<CarView>()).ToArray();
        readonly List<(CarView unit, int zone)> gate = new List<(CarView, int)>();
        readonly List<CarView> streetCars = new List<CarView>();
        int gateFleet;
        Coroutine traceRoutine;

        public void Init(CarView[] fleetUnits, ParkingSpaceView[] lotBays, Transform[] street, Transform[] gateSpots)
        {
            units = fleetUnits;
            bays = lotBays;
            streetSlots = street;
            gateSlots = gateSpots;
        }

        public void Show(BankerChallenge c)
        {
            if (traceRoutine != null) StopCoroutine(traceRoutine);
            foreach (var u in units)
            {
                if (!u.gameObject.activeSelf) continue;
                u.SetSelected(false);
                u.SetDeadlocked(false);
                u.gameObject.SetActive(false);
            }
            foreach (var b in bays)
            {
                b.SetOwner(null);
                b.SetDeadlocked(false);
            }
            foreach (var list in parked) list.Clear();
            gate.Clear();
            streetCars.Clear();
            gateFleet = c.Process;

            var s = c.State;
            int street = 0, gateSlot = 0;
            for (int f = 0; f < s.ProcessCount; f++)
            {
                int next = 0;
                CarView Take() => units[f * BankerChallenge.UnitsPerFleet + next++];

                for (int j = 0; j < s.ResourceCount; j++)
                    for (int a = 0; a < s.Allocation[f, j]; a++)
                    {
                        var bay = FreeBay(j);
                        var unit = Take();
                        Activate(unit, bay.ParkingPoint, bay.ParkedRotation, bay);
                        bay.SetOwner(unit);
                        parked[f].Add(unit);
                    }

                int requested = 0;
                if (f == c.Process)
                    for (int j = 0; j < s.ResourceCount; j++)
                        for (int r = 0; r < c.Request[j]; r++, requested++)
                        {
                            var slot = gateSlots[gateSlot++];
                            var unit = Take();
                            Activate(unit, slot.position, slot.rotation, null);
                            gate.Add((unit, j));
                        }

                int need = Enumerable.Range(0, s.ResourceCount).Sum(j => s.Need(f, j));
                for (int w = 0; w < need - requested; w++)
                {
                    var slot = streetSlots[street++];
                    var unit = Take();
                    Activate(unit, slot.position, slot.rotation, null);
                    streetCars.Add(unit);
                }
            }
        }

        /// <summary>Correct, safe grant: the gate cars drive into free bays of the zones they asked for.</summary>
        public void AnimateGrant(BankerChallenge c)
        {
            foreach (var (unit, zone) in gate)
            {
                var bay = FreeBay(zone);
                if (bay == null) continue;
                bay.SetOwner(unit);
                unit.DriveTo(new[] { bay.ApproachPoint, bay.ParkingPoint }, bay, bay.ParkedRotation, reverseOut: false);
                parked[c.Process].Add(unit);
            }
            gate.Clear();
        }

        /// <summary>
        /// Replays the safety algorithm on the lot: fleets light up in safe-sequence order; fleets that can never finish
        /// turn red. Requests that fail before the safety check (must wait / over claim) flash the gate cars red.
        /// </summary>
        public void ShowVerdict(BankerVerdict v)
        {
            if (traceRoutine != null) StopCoroutine(traceRoutine);
            if (v.Trace == null)
            {
                foreach (var (unit, _) in gate) unit.SetDeadlocked(true);
                return;
            }
            traceRoutine = StartCoroutine(PlayTrace(v.Trace));
        }

        IEnumerator PlayTrace(SafetyTrace trace)
        {
            yield return new WaitForSeconds(0.4f);
            foreach (var step in trace.Steps)
            {
                foreach (var u in FleetUnits(step.Process)) u.SetSelected(true);
                yield return new WaitForSeconds(traceStepSeconds);
            }
            foreach (int f in trace.Stuck)
            {
                foreach (var u in FleetUnits(f)) u.SetDeadlocked(true);
                foreach (var b in bays)
                    if (b.Owner != null && parked[f].Contains(b.Owner)) b.SetDeadlocked(true);
            }
        }

        IEnumerable<CarView> FleetUnits(int fleet) =>
            fleet == gateFleet ? parked[fleet].Concat(gate.Select(g => g.unit)) : parked[fleet];

        // ---- Lookups for the guided tour's markers ----

        public ParkingSpaceView Bay(int index) => bays[index];
        public CarView ParkedCar(int fleet) => parked[fleet].FirstOrDefault();
        public CarView StreetCar() => streetCars.FirstOrDefault();
        public CarView GateCar() => gate.Count > 0 ? gate[0].unit : null;

        public ParkingSpaceView FreeBay(int zone) =>
            bays.Skip(zone * 3).Take(3).FirstOrDefault(b => b.Owner == null);

        static void Activate(CarView unit, Vector3 position, Quaternion rotation, ParkingSpaceView bay)
        {
            unit.gameObject.SetActive(true);
            unit.Place(position, rotation, bay);
        }
    }
}
