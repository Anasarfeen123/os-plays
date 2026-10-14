using Gridlock.AR;
using NUnit.Framework;
using UnityEngine;

namespace Gridlock.Tests
{
    public class ARPlacementMathTests
    {
        static void AreClose(Vector3 a, Vector3 b) => Assert.Less(Vector3.Distance(a, b), 1e-3f, $"{a} vs {b}");

        [TestCase(1f, 0f)]
        [TestCase(35f, 0f)]
        [TestCase(35f, 73f)]
        [TestCase(80f, -140f)]
        public void Pivot_AppearsAtTheAnchor(float scale, float yaw)
        {
            var pivot = new Vector3(0f, 0f, 1f);
            var anchor = new Vector3(0.3f, -0.9f, 1.2f);
            var (pos, rot) = ARPlacementMath.OriginFor(pivot, anchor, yaw, scale);
            AreClose(ARPlacementMath.SessionToWorld(anchor, pos, rot, scale), pivot);
        }

        [Test]
        public void ContentLooksScaleTimesSmaller()
        {
            const float scale = 35f;
            var (pos, rot) = ARPlacementMath.OriginFor(Vector3.zero, Vector3.zero, 0f, scale);
            // One session metre covers 35 world metres.
            var a = ARPlacementMath.SessionToWorld(Vector3.zero, pos, rot, scale);
            var b = ARPlacementMath.SessionToWorld(Vector3.right, pos, rot, scale);
            Assert.AreEqual(scale, Vector3.Distance(a, b), 1e-3f);
        }

        [Test]
        public void ModelFrontFacesTheCamera()
        {
            // Camera south of the anchor: the model's +Z points north (away), so its front (−Z) faces the camera.
            Assert.AreEqual(0f, ARPlacementMath.FacingYaw(new Vector3(0f, 0f, 1f), new Vector3(0f, 1.4f, 0f)), 1e-3f);
            Assert.AreEqual(90f, ARPlacementMath.FacingYaw(new Vector3(1f, 0f, 0f), Vector3.zero), 1e-3f);
        }
    }
}
