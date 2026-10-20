using Gridlock.Core;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;

namespace Gridlock.XR
{
    /// <summary>
    /// Turning for the headset rig, replacing how the toolkit's providers apply it (their stick inputs, and the SNAP /
    /// SMOOTH switch that enables one or the other, are kept):
    /// <list type="bullet">
    /// <item>Snap: one flick of the stick turns by the menu's SNAP ANGLE (pull back: turn around). The turn is a quick
    /// eased sweep (~0.15 s) instead of a cut, and it does not repeat while the stick is held; centre it to turn again.</item>
    /// <item>Smooth: speed follows the stick on a curve (fine control near the centre) and eases in and out, so starting
    /// and stopping never jerk the view.</item>
    /// <item>Hand-tracking flicks (<see cref="HandTeleport"/>) use the same sweep through <see cref="Turn"/>.</item>
    /// </list>
    /// Always around the head, never while the game is paused.
    /// </summary>
    [DefaultExecutionOrder(-300)] // before the interaction manager moves held objects, so they never lag a turn
    public class ComfortTurn : MonoBehaviour
    {
        [SerializeField] XROrigin origin;
        [Tooltip("Seconds for one snap sweep.")]
        [SerializeField] float snapSeconds = 0.15f;
        [Tooltip("Stick deflection that triggers a snap, and below which it re-arms.")]
        [SerializeField] float snapPress = 0.7f;
        [SerializeField] float snapRelease = 0.3f;
        [Tooltip("Smooth turning: top speed (°/s), stick deadzone, and how fast speed follows the stick (1/s).")]
        [SerializeField] float smoothSpeed = 110f;
        [SerializeField] float deadzone = 0.15f;
        [SerializeField] float smoothResponse = 9f;

        SnapTurnProvider snap;
        ContinuousTurnProvider smooth;
        bool snapArmed = true;
        float sweepLeft;     // degrees still to turn in the current sweep
        float sweepTotal;
        float sweepElapsed;
        float angularVelocity;

        public static ComfortTurn Current { get; private set; }

        /// <summary>True while a snap sweep is under way.</summary>
        public bool Sweeping => !Mathf.Approximately(sweepLeft, 0f);

        public void Init(XROrigin xrOrigin) => origin = xrOrigin;

        void Awake()
        {
            if (origin == null) origin = GetComponent<XROrigin>();
            snap = GetComponentInChildren<SnapTurnProvider>(true);
            smooth = GetComponentInChildren<ContinuousTurnProvider>(true);
            // Their input stays live (the stick actions are switched by the starter rig's per-controller manager);
            // only their abrupt rotation is replaced.
            if (snap != null) snap.enabled = false;
            if (smooth != null) smooth.enabled = false;
        }

        void OnEnable() => Current = this;

        void OnDisable()
        {
            if (Current == this) Current = null;
        }

        /// <summary>Turns by <paramref name="degrees"/> in one eased sweep (a new turn replaces any sweep in progress).</summary>
        public void Turn(float degrees)
        {
            if (Mathf.Approximately(degrees, 0f)) return;
            sweepTotal = sweepLeft = degrees;
            sweepElapsed = 0f;
        }

        void Update()
        {
            if (origin == null || Time.timeScale <= 0f) return;
            float dt = Time.unscaledDeltaTime;
            UpdateSnap(Read(snap != null ? snap.leftHandTurnInput : null) + Read(snap != null ? snap.rightHandTurnInput : null));
            UpdateSmooth(Read(smooth != null ? smooth.leftHandTurnInput : null) + Read(smooth != null ? smooth.rightHandTurnInput : null), dt);
            UpdateSweep(dt);
        }

        static Vector2 Read(XRInputValueReader<Vector2> reader) => reader != null ? reader.ReadValue() : Vector2.zero;

        void UpdateSnap(Vector2 stick)
        {
            float magnitude = stick.magnitude;
            if (magnitude < snapRelease)
            {
                snapArmed = true;
                return;
            }
            if (!snapArmed || magnitude < snapPress) return;

            float degrees = 0f;
            if (Mathf.Abs(stick.x) >= Mathf.Abs(stick.y)) degrees = Mathf.Sign(stick.x) * GameSettings.SnapDegrees;
            else if (stick.y < 0f && snap != null && snap.enableTurnAround) degrees = 180f;
            if (degrees == 0f) return; // forward belongs to teleport
            snapArmed = false;
            Turn(degrees);
        }

        void UpdateSmooth(Vector2 stick, float dt)
        {
            float x = Mathf.Abs(stick.x) < deadzone ? 0f : Mathf.Sign(stick.x) * Mathf.InverseLerp(deadzone, 1f, Mathf.Abs(stick.x));
            float target = smoothSpeed * x * Mathf.Abs(x); // squared: slow and precise near the centre
            angularVelocity = Mathf.Lerp(angularVelocity, target, 1f - Mathf.Exp(-smoothResponse * dt));
            if (Mathf.Abs(angularVelocity) < 0.5f && target == 0f)
            {
                angularVelocity = 0f;
                return;
            }
            origin.RotateAroundCameraUsingOriginUp(angularVelocity * dt);
        }

        void UpdateSweep(float dt)
        {
            if (!Sweeping) return;
            sweepElapsed += dt;
            float t = Mathf.Clamp01(sweepElapsed / Mathf.Max(snapSeconds, 0.01f));
            float eased = t * t * (3f - 2f * t); // smoothstep
            float done = sweepTotal * eased;
            float step = done - (sweepTotal - sweepLeft);
            sweepLeft -= step;
            if (t >= 1f)
            {
                step += sweepLeft;
                sweepLeft = 0f;
            }
            origin.RotateAroundCameraUsingOriginUp(step);
        }
    }
}
