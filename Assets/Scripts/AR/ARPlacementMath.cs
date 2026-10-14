using UnityEngine;

namespace Gridlock.AR
{
    /// <summary>
    /// Where to put the AR session origin so the (static, life-size) scene appears as a miniature on a real surface.
    /// Moving and scaling the origin instead of the content keeps every simulation position untouched: a session point
    /// <c>x</c> maps to world as <c>origin + rotation · (scale · x)</c>, so the content looks <c>scale</c> times smaller.
    /// </summary>
    public static class ARPlacementMath
    {
        /// <summary>
        /// Origin pose that makes world point <paramref name="pivot"/> appear at <paramref name="anchor"/> (session space),
        /// with the content's +Z pointing along session yaw <paramref name="sessionYaw"/>, at 1 : <paramref name="scale"/>.
        /// </summary>
        public static (Vector3 position, Quaternion rotation) OriginFor(Vector3 pivot, Vector3 anchor, float sessionYaw, float scale)
        {
            var rotation = Quaternion.Euler(0f, -sessionYaw, 0f);
            return (pivot - rotation * (anchor * scale), rotation);
        }

        /// <summary>Session-space point → world, for an origin at (position, rotation, scale).</summary>
        public static Vector3 SessionToWorld(Vector3 session, Vector3 position, Quaternion rotation, float scale) =>
            position + rotation * (session * scale);

        /// <summary>Yaw that turns the model's front (its −Z side, where the player stands) towards the camera.</summary>
        public static float FacingYaw(Vector3 anchor, Vector3 camera)
        {
            var away = anchor - camera;
            away.y = 0f;
            return away.sqrMagnitude < 1e-6f ? 0f : Mathf.Atan2(away.x, away.z) * Mathf.Rad2Deg;
        }
    }
}
