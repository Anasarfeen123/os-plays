using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Gridlock.Editor
{
    /// <summary>
    /// Finds content assets nothing uses: not a dependency of the built scenes, of the imported scenario scene the generator
    /// starts from, of the project settings (render pipelines, input, XR), nor named by path in the generator's code.
    /// Code, settings, licenses, tests and TextMesh Pro are never touched.
    /// Batchmode: -executeMethod Gridlock.Editor.GridlockAssetPrune.Report (lists to Logs/unused_assets.txt) or .Delete.
    /// </summary>
    public static class GridlockAssetPrune
    {
        // Imported and leftover content only. The XRI samples, the Kenney kits and the generated materials stay: they come
        // from packages or are remade by the generator.
        static readonly string[] Roots = { "Assets/Scenarios", "Assets/Materials", "Assets/Scenes" };
        static readonly string[] NeverExtensions = { ".cs", ".asmdef", ".asmref", ".txt", ".md", ".json", ".dll", ".so", ".mixer", ".inputactions", ".shader", ".hlsl", ".cginc", ".shadergraph", ".shadersubgraph" };

        [MenuItem("GRIDLOCK/Report Unused Assets")]
        public static void Report() => Run(delete: false);

        public static void Delete() => Run(delete: true);

        static void Run(bool delete)
        {
            var keep = new HashSet<string>();
            void Keep(IEnumerable<string> paths) { foreach (var p in AssetDatabase.GetDependencies(paths.ToArray(), true)) keep.Add(p); }

            Keep(EditorBuildSettings.scenes.Select(s => s.path));
            Keep(new[] { "Assets/Scenarios/MemoryManagement/Scenes/MemoryManagement.unity", "Assets/Scenarios/CPUScheduling/Scenes/CPUScheduling.unity" });
            Keep(Directory.GetFiles("ProjectSettings", "*.asset").Select(p => p.Replace('\\', '/')));
            Keep(AssetDatabase.FindAssets("t:RenderPipelineAsset").Select(AssetDatabase.GUIDToAssetPath));
            Keep(AssetDatabase.FindAssets("t:VolumeProfile").Select(AssetDatabase.GUIDToAssetPath).Where(p => p.StartsWith("Assets/Settings")));
            foreach (var o in PlayerSettings.GetPreloadedAssets()) if (o != null) Keep(new[] { AssetDatabase.GetAssetPath(o) });

            // Files the generator or setup code names by path ("Assets/….ext" literals). Folder literals are not kept whole:
            // the generator only uses what ends up in the scenes, which the dependencies above already cover.
            foreach (var file in Directory.GetFiles("Assets/Editor", "*.cs"))
                foreach (Match m in Regex.Matches(File.ReadAllText(file), "\"(Assets/[^\"{}]+\\.[A-Za-z0-9]+)\""))
                    if (File.Exists(m.Groups[1].Value)) Keep(new[] { m.Groups[1].Value });
            // Folders the generator assembles names under (Kenney kits, music, narration, XRI samples) are kept whole
            // where the generator picks files by computed name.
            foreach (var folder in new[] { "Assets/Audio/Music", "Assets/Audio/Narration", "Assets/Audio/Kenney", "Assets/Audio/Hub", "Assets/UI/Brand", "Assets/Scenarios/MemoryManagement/VR_Hands" })
                if (AssetDatabase.IsValidFolder(folder)) Keep(AssetDatabase.FindAssets("", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath));

            var unused = AssetDatabase.FindAssets("", Roots.Where(AssetDatabase.IsValidFolder).ToArray())
                .Select(AssetDatabase.GUIDToAssetPath).Distinct()
                .Where(p => !AssetDatabase.IsValidFolder(p) && !keep.Contains(p))
                .Where(p => !NeverExtensions.Contains(Path.GetExtension(p).ToLowerInvariant()))
                .Where(p => !Path.GetFileName(p).StartsWith("License", StringComparison.OrdinalIgnoreCase))
                .Where(p => !p.Contains("/Resources/") && !p.Contains("/Editor/") && !p.StartsWith("Assets/Materials/Generated/"))
                .OrderBy(p => p).ToList();
            long bytes = unused.Sum(p => new FileInfo(p).Length);
            File.WriteAllLines("Logs/unused_assets.txt", unused.Select(p => $"{new FileInfo(p).Length / 1024,8} KB  {p}"));
            Debug.Log($"[GRIDLOCK] Unused assets: {unused.Count} files, {bytes / (1024f * 1024f):0.0} MB (Logs/unused_assets.txt)");
            if (!delete) return;

            var failed = new List<string>();
            AssetDatabase.DeleteAssets(unused.ToArray(), failed);
            // Empty folders left behind.
            foreach (var root in Roots.Where(AssetDatabase.IsValidFolder))
                foreach (var dir in Directory.GetDirectories(root, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length))
                    if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any(f => !f.EndsWith(".meta")) && Directory.GetDirectories(dir).Length == 0)
                        AssetDatabase.DeleteAsset(dir.Replace('\\', '/'));
            AssetDatabase.Refresh();
            Debug.Log($"[GRIDLOCK] Deleted {unused.Count - failed.Count} unused assets ({failed.Count} failed)");
        }
    }
}
