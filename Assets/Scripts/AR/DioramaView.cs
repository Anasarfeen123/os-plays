using System.Collections.Generic;
using UnityEngine;

namespace Gridlock.AR
{
    /// <summary>
    /// A life-size scene shown as a tabletop model (phone AR and Quest mixed reality): everything outside the model's
    /// bounds and the ceiling are hidden, and the walls on the sides facing the viewer are cut away so it looks into the
    /// room from any angle (<see cref="ARDioramaSettings"/>). <see cref="Restore"/> undoes it.
    /// </summary>
    public class DioramaView
    {
        readonly ARDioramaSettings settings;
        readonly List<(Renderer renderer, int sides)> walls = new List<(Renderer, int)>();
        /// <summary>Hidden for the model (outside it, the ceiling) or cut away right now (walls on the viewer's side).</summary>
        readonly HashSet<Renderer> hidden = new HashSet<Renderer>();
        readonly List<Renderer> hiddenForGood = new List<Renderer>();
        int lastFacing = -1;

        public DioramaView(ARDioramaSettings diorama) => settings = diorama;

        /// <summary>Hidden for the model or cut away (it must not catch a tap or a ray).</summary>
        public bool IsHidden(Renderer renderer) => hidden.Contains(renderer);

        /// <summary>Sort the scene's renderers: outside or ceiling (hidden), walls by side, the rest stays.</summary>
        /// <param name="exclude">The rig that shows the model (its reticle, planes and base are not part of it).</param>
        public void Build(Transform exclude)
        {
            walls.Clear();
            hidden.Clear();
            hiddenForGood.Clear();
            lastFacing = -1;
            var box = settings.bounds;
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if ((exclude != null && r.transform.IsChildOf(exclude)) || !r.enabled) continue;
                if (r.GetComponentInParent<Canvas>() != null) continue; // world UI draws through its canvas
                var b = r.bounds;
                // Outside the model, bigger than it (a 200 m ground or a starfield), or the ceiling: not part of it.
                if (!box.Contains(b.center) || b.size.x > box.size.x * 1.05f || b.size.z > box.size.z * 1.05f || b.min.y >= settings.ceilingCut - 0.01f)
                {
                    r.enabled = false;
                    hidden.Add(r);
                    hiddenForGood.Add(r);
                    continue;
                }
                if (settings.cutawayBand <= 0f || b.max.y < settings.cutawayMinHeight) continue;
                int sides = 0;
                if (b.center.x - box.min.x < settings.cutawayBand) sides |= 1;
                if (box.max.x - b.center.x < settings.cutawayBand) sides |= 2;
                if (b.center.z - box.min.z < settings.cutawayBand) sides |= 4;
                if (box.max.z - b.center.z < settings.cutawayBand) sides |= 8;
                if (sides != 0) walls.Add((r, sides));
            }
        }

        /// <summary>Hide the walls on the sides that face the viewer (world position).</summary>
        public void UpdateCutaway(Vector3 viewer, bool force)
        {
            if (walls.Count == 0) return;
            var toViewer = viewer - settings.bounds.center;
            toViewer.y = 0f;
            int facing = 0;
            if (toViewer.sqrMagnitude > 1e-6f)
            {
                toViewer.Normalize();
                const float threshold = 0.3f; // about 72°: a side counts as facing until the viewer is well past its corner
                if (-toViewer.x > threshold) facing |= 1;
                if (toViewer.x > threshold) facing |= 2;
                if (-toViewer.z > threshold) facing |= 4;
                if (toViewer.z > threshold) facing |= 8;
            }
            if (!force && facing == lastFacing) return;
            lastFacing = facing;
            foreach (var (r, sides) in walls)
            {
                if (r == null) continue;
                bool cut = (sides & facing) != 0;
                r.enabled = !cut;
                if (cut) hidden.Add(r); else hidden.Remove(r);
            }
        }

        /// <summary>The scene as it was (leaving mixed reality).</summary>
        public void Restore()
        {
            foreach (var r in hiddenForGood)
                if (r != null) r.enabled = true;
            foreach (var (r, _) in walls)
                if (r != null) r.enabled = true;
            hidden.Clear();
            walls.Clear();
            hiddenForGood.Clear();
        }
    }
}
