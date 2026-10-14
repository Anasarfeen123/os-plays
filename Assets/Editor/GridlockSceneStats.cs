using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Gridlock.Editor
{
    /// <summary>
    /// Render-cost summary of each scene (renderers, triangles, shaders, transparent surfaces) for Quest tuning.
    /// Batchmode: -executeMethod Gridlock.Editor.GridlockSceneStats.Dump
    /// </summary>
    public static class GridlockSceneStats
    {
        [MenuItem("GRIDLOCK/Scene Render Stats")]
        public static void Dump()
        {
            foreach (var path in new[] { "Assets/Scenes/GridlockHub.unity", "Assets/Scenes/GridlockParkingLot.unity" })
            {
                EditorSceneManager.OpenScene(path);
                var renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                    .Where(r => r.enabled && !(r is ParticleSystemRenderer)).ToArray();
                long Tris(Renderer r)
                {
                    var mesh = r is SkinnedMeshRenderer s ? s.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh;
                    return mesh == null ? 0 : mesh.triangles.LongLength / 3;
                }
                Debug.Log($"[STATS] {path}: {renderers.Length} renderers, {renderers.Sum(Tris)} triangles, " +
                          $"{renderers.Sum(r => r.sharedMaterials.Length)} material slots, " +
                          $"{renderers.Count(r => r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off)} shadow casters");
                foreach (var g in renderers.SelectMany(r => r.sharedMaterials.Where(m => m != null).Select(m => (r, m)))
                             .GroupBy(x => x.m.shader.name).OrderByDescending(g => g.Count()))
                    Debug.Log($"[STATS]   shader {g.Key}: {g.Count()} slots, {g.Count(x => x.m.renderQueue >= 2500)} transparent");
                foreach (var r in renderers.OrderByDescending(Tris).Take(12))
                    Debug.Log($"[STATS]   heavy {r.name} ({r.transform.parent?.name}): {Tris(r)} tris");
                foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                    Debug.Log($"[STATS]   light {l.name} {l.type} shadows={l.shadows} mode={l.lightmapBakeType}");
            }
        }
    }
}
