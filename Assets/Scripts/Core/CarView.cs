using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Gridlock.Core
{
    /// <summary>
    /// Scene representation of one car (process). Presentation only; state lives in <see cref="ParkingState"/>.
    /// Drives along paths handed to it by <see cref="SimulationManager"/> and shows waiting/deadlock status.
    /// </summary>
    public class CarView : MonoBehaviour
    {
        [SerializeField] string id = "A";
        [SerializeField] Color baseColor = Color.cyan;
        [Tooltip("Renderers of the car model; tinted and made to glow red when deadlocked.")]
        [SerializeField] Renderer[] bodyRenderers;
        [Tooltip("Coloured ring under the car that marks its identity.")]
        [SerializeField] Renderer ring;
        [SerializeField] TMP_Text badge;
        [SerializeField] TMP_Text status;
        [SerializeField] Transform[] wheels;
        [SerializeField] float wheelRadius = 0.35f;
        [SerializeField] float speed = 6f;
        [SerializeField] float turnSpeed = 240f;
        [Tooltip("Looping engine sound; volume and pitch follow the car's speed.")]
        [SerializeField] AudioSource engine;

        readonly Queue<(Vector3 point, bool reverse)> path = new Queue<(Vector3, bool)>();
        Quaternion? finalRotation;
        MaterialPropertyBlock block;
        Vector3 homePosition;
        Quaternion homeRotation;
        bool deadlocked;
        bool selected;
        string waitingFor;
        Vector3 ringScale;
        float currentSpeed;
        float engineLevel;

        public string Id => id;
        public Color BaseColor => baseColor;
        public bool IsMoving => path.Count > 0 || finalRotation.HasValue;
        public ParkingSpaceView CurrentSpace { get; private set; }
        public Vector3 HomePosition => homePosition;
        public Quaternion HomeRotation => homeRotation;
        /// <summary>Anchor for analysis arrows, above the info card.</summary>
        public Vector3 TopAnchor => transform.position + Vector3.up * 4.6f;
        /// <summary>Where the Analyze/inspect card floats; it replaces the badge while shown.</summary>
        public Vector3 CardAnchor => transform.position + Vector3.up * 3.3f;

        public void Init(string carId, Color color, Renderer[] bodies, Renderer ringRenderer, TMP_Text badgeText,
                         TMP_Text statusText, Transform[] wheelTransforms, AudioSource engineSource)
        {
            engine = engineSource;
            id = carId;
            baseColor = color;
            bodyRenderers = bodies;
            ring = ringRenderer;
            badge = badgeText;
            status = statusText;
            wheels = wheelTransforms;
        }

        /// <summary>
        /// Follow <paramref name="waypoints"/> and end up in <paramref name="space"/> (null = home).
        /// When <paramref name="reverseOut"/> is set the first segment is driven backwards (leaving a bay).
        /// </summary>
        public void DriveTo(IEnumerable<Vector3> waypoints, ParkingSpaceView space, Quaternion endRotation, bool reverseOut)
        {
            path.Clear();
            bool first = true;
            foreach (var p in waypoints)
            {
                path.Enqueue((new Vector3(p.x, homePosition.y, p.z), first && reverseOut));
                first = false;
            }
            finalRotation = endRotation;
            CurrentSpace = space;
        }

        /// <summary>Put the car somewhere instantly (no driving), optionally as parked in <paramref name="space"/>.</summary>
        public void Place(Vector3 position, Quaternion rotation, ParkingSpaceView space)
        {
            path.Clear();
            finalRotation = null;
            currentSpeed = 0f;
            CurrentSpace = space;
            transform.SetPositionAndRotation(new Vector3(position.x, homePosition.y, position.z), rotation);
        }

        /// <summary>Jump straight back to the start position (used on restart).</summary>
        public void SnapHome()
        {
            path.Clear();
            finalRotation = null;
            CurrentSpace = null;
            currentSpeed = 0f;
            transform.SetPositionAndRotation(homePosition, homeRotation);
        }

        public void SetDeadlocked(bool value)
        {
            EnsureInit();
            deadlocked = value;
            RefreshStatus();
            Refresh();
        }

        /// <summary>Selected by the learner: brighter, pulsing ring.</summary>
        public void SetSelected(bool value)
        {
            EnsureInit();
            selected = value;
            if (!selected && ring != null) ring.transform.localScale = ringScale;
            Refresh();
        }

        /// <summary>Hide the badge and status while an info card is shown in their place.</summary>
        public void SetCardShown(bool shown)
        {
            if (badge != null) badge.gameObject.SetActive(!shown);
            if (status != null) status.gameObject.SetActive(!shown);
        }

        /// <summary>Resource this car is blocked on, or null.</summary>
        public void SetWaiting(string resource)
        {
            waitingFor = resource;
            RefreshStatus();
        }

        // Setters can be called on a car that has not been activated yet (Banker mode pools inactive cars).
        bool initialized;
        void EnsureInit()
        {
            if (initialized) return;
            initialized = true;
            homePosition = transform.position;
            homeRotation = transform.rotation;
            block = new MaterialPropertyBlock();
            if (ring != null) ringScale = ring.transform.localScale;
        }

        void Awake()
        {
            EnsureInit();
            if (badge != null) badge.text = id;
            RefreshStatus();
            Refresh();
            if (engine != null)
            {
                engine.volume = 0f;
                engine.Play();
                engine.time = Random.value * engine.clip.length; // de-sync the four engines
            }
        }

        void Update()
        {
            Drive(Time.deltaTime);
            UpdateEngine(Time.deltaTime);
            if (deadlocked || selected) Refresh(); // pulse
            if (selected && ring != null) ring.transform.localScale = ringScale * (1.08f + 0.06f * ViewColor.Pulse);
        }

        void UpdateEngine(float dt)
        {
            if (engine == null) return;
            float load = Mathf.Clamp01(currentSpeed / speed);
            float target = IsMoving ? Mathf.Lerp(0.35f, 0.75f, load) : 0f;
            engineLevel = Mathf.MoveTowards(engineLevel, target, dt * (IsMoving ? 2.5f : 0.8f)); // slow fade when parking
            engine.volume = engineLevel;
            engine.pitch = Mathf.Lerp(0.8f, 1.35f, load);
        }

        void Drive(float dt)
        {
            if (path.Count > 0)
            {
                var (target, reverse) = path.Peek();
                var to = target - transform.position;
                to.y = 0f;
                float dist = to.magnitude;

                if (dist < 0.05f)
                {
                    path.Dequeue();
                    return;
                }


                var dir = to / dist;
                var look = Quaternion.LookRotation(reverse ? -dir : dir);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnSpeed * dt);

                // Slow down while turning hard so cornering looks deliberate.
                float align = Mathf.Clamp01(Vector3.Dot(transform.forward * (reverse ? -1f : 1f), dir));
                float step = Mathf.Min(dist, speed * GameSettings.CarSpeedMultiplier * Mathf.Lerp(0.2f, 1f, align * align) * dt);
                transform.position += dir * step;
                currentSpeed = dt > 0f ? step / dt : 0f;
                SpinWheels(reverse ? -step : step);
            }
            else if (finalRotation.HasValue)
            {
                currentSpeed = 0f;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, finalRotation.Value, turnSpeed * dt);
                if (Quaternion.Angle(transform.rotation, finalRotation.Value) < 0.5f)
                {
                    transform.rotation = finalRotation.Value;
                    finalRotation = null;
                }
            }
        }

        void SpinWheels(float distance)
        {
            if (wheels == null) return;
            float degrees = distance / wheelRadius * Mathf.Rad2Deg;
            foreach (var w in wheels)
                if (w != null) w.Rotate(Vector3.right, degrees, Space.Self);
        }

        void Refresh()
        {
            float pulse = ViewColor.Pulse;
            var tint = deadlocked ? Color.Lerp(Color.white, ViewColor.Deadlock, 0.8f) : Color.white;
            var glow = deadlocked ? ViewColor.Deadlock * Mathf.Lerp(0.15f, 0.9f, pulse) : Color.black;
            if (bodyRenderers != null)
                foreach (var r in bodyRenderers) ViewColor.Apply(r, block, tint, glow);

            var ringColor = deadlocked ? ViewColor.Deadlock * Mathf.Lerp(1f, 2f, pulse) : baseColor;
            if (selected) ringColor = Color.Lerp(ringColor, Color.white, 0.35f) * Mathf.Lerp(1.6f, 2.6f, pulse);
            ringColor.a = 1f;
            ViewColor.Apply(ring, block, ringColor);

            if (badge != null) badge.color = deadlocked ? ViewColor.Deadlock : baseColor;
        }

        void RefreshStatus()
        {
            if (status == null) return;
            if (deadlocked)
            {
                status.text = $"DEADLOCKED\n<size=75%>wants {waitingFor}</size>";
                status.color = ViewColor.Deadlock;
            }
            else if (waitingFor != null)
            {
                status.text = $"WAITING\n<size=75%>wants {waitingFor}</size>";
                status.color = ViewColor.Waiting;
            }
            else status.text = "";
        }
    }
}
