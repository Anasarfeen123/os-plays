using Gridlock.Core;
using UnityEngine;

namespace Gridlock.Deadlock
{
    /// <summary>Turns the cars and parking spaces on a detected circular wait red, and clears them when the deadlock is gone.</summary>
    [RequireComponent(typeof(SimulationManager))]
    public class DeadlockVisualizer : MonoBehaviour
    {
        SimulationManager simulation;

        void Awake()
        {
            simulation = GetComponent<SimulationManager>();
            simulation.StateEvaluated += Show;
        }

        void OnDestroy()
        {
            if (simulation != null) simulation.StateEvaluated -= Show;
        }

        void Show(DeadlockResult result)
        {
            foreach (var car in simulation.State.Cars)
                simulation.GetCar(car)?.SetDeadlocked(result.InvolvedCars.Contains(car));

            foreach (var resource in simulation.State.Resources)
                simulation.GetSpace(resource)?.SetDeadlocked(result.InvolvedResources.Contains(resource));
        }
    }
}
