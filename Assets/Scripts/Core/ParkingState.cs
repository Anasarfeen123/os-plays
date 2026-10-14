using System;
using System.Collections.Generic;

namespace Gridlock.Core
{
    /// <summary>
    /// Single-instance resource model for Deadlock Detection mode.
    /// Cars are processes, parking spaces are resources. Pure C#, no UnityEngine dependency.
    /// </summary>
    public class ParkingState
    {
        readonly List<string> cars = new List<string>();
        readonly List<string> resources = new List<string>();
        readonly Dictionary<string, string> ownerOf = new Dictionary<string, string>();   // resource -> car
        readonly Dictionary<string, string> requestOf = new Dictionary<string, string>(); // car -> resource

        public event Action Changed;

        public IReadOnlyList<string> Cars => cars;
        public IReadOnlyList<string> Resources => resources;

        public void AddCar(string car)
        {
            if (cars.Contains(car)) throw new ArgumentException($"Car '{car}' already exists.");
            cars.Add(car);
            Changed?.Invoke();
        }

        public void AddResource(string resource)
        {
            if (resources.Contains(resource)) throw new ArgumentException($"Resource '{resource}' already exists.");
            resources.Add(resource);
            Changed?.Invoke();
        }

        /// <summary>
        /// Car asks for a resource. If free it is allocated and true is returned;
        /// otherwise the car records a pending request (it is now waiting) and false is returned.
        /// </summary>
        public bool Request(string car, string resource)
        {
            RequireCar(car);
            RequireResource(resource);

            if (!ownerOf.TryGetValue(resource, out var owner))
            {
                ownerOf[resource] = car;
                requestOf.Remove(car);
                Changed?.Invoke();
                return true;
            }

            if (owner == car)
            {
                requestOf.Remove(car);
                return true;
            }

            requestOf[car] = resource;
            Changed?.Invoke();
            return false;
        }

        /// <summary>Car releases a resource it holds. Does not auto-grant to waiters; call <see cref="RetryWaiting"/>.</summary>
        public void Release(string car, string resource)
        {
            if (!ownerOf.TryGetValue(resource, out var owner) || owner != car)
                throw new InvalidOperationException($"Car '{car}' does not hold '{resource}'.");
            ownerOf.Remove(resource);
            Changed?.Invoke();
        }

        /// <summary>Releases everything a car holds and cancels its request (e.g. the car leaves or is preempted).</summary>
        public void ReleaseAll(string car)
        {
            RequireCar(car);
            foreach (var r in GetHeld(car)) ownerOf.Remove(r);
            requestOf.Remove(car);
            Changed?.Invoke();
        }

        public void CancelRequest(string car)
        {
            if (requestOf.Remove(car)) Changed?.Invoke();
        }

        /// <summary>Grants any pending request whose resource has become free. Returns cars that were unblocked.</summary>
        public List<string> RetryWaiting()
        {
            var unblocked = new List<string>();
            foreach (var car in cars)
            {
                if (requestOf.TryGetValue(car, out var res) && !ownerOf.ContainsKey(res))
                {
                    ownerOf[res] = car;
                    requestOf.Remove(car);
                    unblocked.Add(car);
                }
            }
            if (unblocked.Count > 0) Changed?.Invoke();
            return unblocked;
        }

        public string GetOwner(string resource) => ownerOf.TryGetValue(resource, out var c) ? c : null;
        public string GetRequest(string car) => requestOf.TryGetValue(car, out var r) ? r : null;
        public bool IsWaiting(string car) => requestOf.ContainsKey(car);

        public List<string> GetHeld(string car)
        {
            var held = new List<string>();
            foreach (var r in resources)
                if (ownerOf.TryGetValue(r, out var o) && o == car) held.Add(r);
            return held;
        }

        void RequireCar(string car)
        {
            if (!cars.Contains(car)) throw new ArgumentException($"Unknown car '{car}'.");
        }

        void RequireResource(string resource)
        {
            if (!resources.Contains(resource)) throw new ArgumentException($"Unknown resource '{resource}'.");
        }
    }
}
