using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Gridlock.Editor
{
    /// <summary>Dev utility: logs bounds and hierarchy of the imported Kenney models.</summary>
    public static class GridlockAssetProbe
    {
        public static void Dump()
        {
            foreach (var path in Directory.GetFiles("Assets/ThirdParty/Kenney", "*.fbx", SearchOption.AllDirectories).OrderBy(p => p))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path.Replace('\\', '/'));
                var go = (GameObject)Object.Instantiate(prefab);
                var rs = go.GetComponentsInChildren<Renderer>();
                var b = rs[0].bounds;
                foreach (var r in rs) b.Encapsulate(r.bounds);
                var names = string.Join(",", go.GetComponentsInChildren<Transform>().Skip(1).Select(t => t.name));
                var mats = string.Join(",", rs.SelectMany(r => r.sharedMaterials).Where(m => m).Select(m => m.name + ":" + m.shader.name).Distinct());
                Debug.Log($"PROBE {Path.GetFileNameWithoutExtension(path)} center={b.center:F2} size={b.size:F2} scale={go.transform.localScale:F2} children=[{names}] mats=[{mats}]");
                Object.DestroyImmediate(go);
            }
        }
    }
}
