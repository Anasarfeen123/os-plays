using System;
using UnityEngine;

namespace Gridlock.AR
{
    /// <summary>How one scene becomes a tabletop model in phone AR (set by the scene builder).</summary>
    [Serializable]
    public class ARDioramaSettings
    {
        [Tooltip("What the player places, as in \"Tap to place the … here\".")]
        public string subject = "parking lot";
        [Tooltip("The AR pill once the model is down.")]
        public string placedHint = "Pinch to resize · twist to rotate · drag to move · tap cars to play";
        [Tooltip("The bottom help bar in AR.")]
        public string helpText = "Tap a car, then a bay    Pinch  resize    Twist  rotate    Drag  move";
        [Tooltip("Renderers whose centre lies outside this world-space box are hidden in AR, so the model ends at its base.")]
        public Bounds bounds = new Bounds(new Vector3(0f, 10f, 4f), new Vector3(66f, 60f, 58f));
        [Tooltip("World metres per real metre at placement (bigger = smaller model).")]
        public float scale = 60f;
        public Vector2 scaleRange = new Vector2(12f, 140f);
        [Tooltip("Renderers lying entirely at or above this height (the ceiling, its lights) are hidden: a dollhouse.")]
        public float ceilingCut = float.PositiveInfinity;
        [Tooltip("Walls within this distance of a side of the bounds hide while that side faces the phone (0: none).")]
        public float cutawayBand;
        [Tooltip("Only things reaching higher than this count as wall for the cutaway (low furniture stays).")]
        public float cutawayMinHeight = 1.4f;
    }
}
