using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Gridlock.Editor
{
    /// <summary>
    /// Measures each generated scene for its AR diorama: the bounds of what is drawn, the floor height, and the biggest
    /// renderers (walls, ceilings, sky). Batchmode: -executeMethod Gridlock.Editor.GridlockARProbe.Run
    /// </summary>
    public static class GridlockARProbe
    {
        public static void Run()
        {
            foreach (var path in new[] { "Assets/Scenes/GridlockHub.unity", "Assets/Scenes/GridlockCPUScheduling.unity", "Assets/Scenes/GridlockMemory.unity" })
            {
                var scene = EditorSceneManager.OpenScene(path);
                var renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                    .Where(r => r.enabled && !(r is ParticleSystemRenderer) && r.GetComponentInParent<Canvas>() == null).ToArray();
                var all = renderers[0].bounds;
                foreach (var r in renderers) all.Encapsulate(r.bounds);
                Debug.Log($"[ARPROBE] {path}: {renderers.Length} renderers, all bounds {all.center:F1} size {all.size:F1}");
                foreach (var r in renderers.OrderByDescending(r => r.bounds.size.x * r.bounds.size.z).Take(12))
                    Debug.Log($"[ARPROBE]   big {Path(r.transform)} centre {r.bounds.center:F1} size {r.bounds.size:F1}");
                foreach (var r in renderers.Where(r => r.bounds.size.y < 0.6f && r.bounds.size.x * r.bounds.size.z > 20f).Take(8))
                    Debug.Log($"[ARPROBE]   flat {Path(r.transform)} top {r.bounds.max.y:F2} size {r.bounds.size:F1}");
                foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(c => c.isRootCanvas))
                    Debug.Log($"[ARPROBE]   canvas {Path(c.transform)} mode {c.renderMode} active {c.gameObject.activeInHierarchy} pos {c.transform.position:F1}");
                foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    Debug.Log($"[ARPROBE]   camera {Path(cam.transform)} pos {cam.transform.position:F1}");
                foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    Debug.Log($"[ARPROBE]   light {Path(l.transform)} {l.type} intensity {l.intensity:F2} shadows {l.shadows}");
            }
        }

        static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;
    }
}
