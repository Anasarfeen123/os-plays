using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Gridlock.Editor
{
    /// <summary>
    /// Health check for the imported scenario scenes (Assets/Scenarios): opens each one and reports missing scripts,
    /// missing materials and the root objects. Batchmode: -executeMethod Gridlock.Editor.GridlockScenarioProbe.Run
    /// </summary>
    public static class GridlockScenarioProbe
    {
        [MenuItem("GRIDLOCK/Probe Scenario Scenes")]
        public static void Run()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenarios" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var scene = EditorSceneManager.OpenScene(path);
                var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject).ToArray();
                int missingScripts = all.Sum(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount);
                var missingMats = all.SelectMany(g => g.GetComponents<Renderer>()).Count(r => r.sharedMaterials.Any(m => m == null));
                Debug.Log($"[PROBE] {path}: {all.Length} objects, {missingScripts} missing scripts, {missingMats} renderers missing materials");
                foreach (var g in all.Where(g => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(g) > 0).Take(15))
                    Debug.Log($"[PROBE]   missing script on {Path(g.transform)}");
                foreach (var r in scene.GetRootGameObjects())
                    Debug.Log($"[PROBE]   root {r.name} active={r.activeSelf}");
            }
        }

        static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;
    }
}
