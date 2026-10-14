using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Gridlock.Tests
{
    /// <summary>
    /// Review shots of the imported scenarios as they look at runtime (their arenas are built in play mode), written to
    /// Docs/Screenshots. Explicit: run with -testFilter Gridlock.Tests.ScenarioScreenshots.
    /// </summary>
    [Explicit]
    public class ScenarioScreenshots
    {
        static void Shoot(Camera source, string name, Vector3 position, Vector3 target)
        {
            const int w = 1600, h = 900;
            var go = new GameObject("Shot");
            var cam = go.AddComponent<Camera>();
            cam.CopyFrom(source);
            cam.fieldOfView = 60f;
            go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position));
            // Overlay UI does not reach a render texture: draw it through this camera for the shot.
            var overlays = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)
                .Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
            foreach (var c in overlays) { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = 0.5f; }
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            RenderTexture.active = null;
            Directory.CreateDirectory("Docs/Screenshots");
            File.WriteAllBytes($"Docs/Screenshots/{name}.png", tex.EncodeToPNG());
            foreach (var c in overlays) c.renderMode = RenderMode.ScreenSpaceOverlay;
            cam.targetTexture = null;
            Object.Destroy(go);
            Object.Destroy(rt);
        }

        /// <summary>Stand 2.6 m from <paramref name="name"/> on the lab-centre side, a little above it.</summary>
        static void ShootObject(Camera source, string shot, string name, Vector3 centre, float distance = 2.6f, float height = 0.7f)
        {
            var go = GameObject.Find(name);
            if (go == null) { Debug.LogWarning($"[SHOT] no {name}"); return; }
            var bounds = new Bounds(go.transform.position, Vector3.zero);
            foreach (var r in go.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
            var dir = Vector3.ProjectOnPlane(centre - bounds.center, Vector3.up);
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.back;
            var pos = bounds.center + dir.normalized * distance + Vector3.up * height;
            Shoot(source, shot, pos, bounds.center);
        }

        [UnityTest]
        public IEnumerator CpuLab()
        {
            SceneManager.LoadScene("GridlockCPUScheduling");
            yield return null;
            yield return new WaitForSeconds(2f);
            var cam = Camera.main;
            var centre = GameObject.Find("Physical_CPU_Core")?.transform.position ?? Vector3.zero;
            Shoot(cam, "cpu_start", cam.transform.position, cam.transform.position + cam.transform.forward);
            Shoot(cam, "cpu_overview", centre + new Vector3(-5f, 2.4f, -6f), centre);
            ShootObject(cam, "cpu_console", "Physical_Control_Console", cam.transform.position);
            ShootObject(cam, "cpu_queue", "Physical_Ready_Queue", cam.transform.position, 3.2f);
            ShootObject(cam, "cpu_gantt", "Teaching_Whiteboard", cam.transform.position, 4f, 0.2f);
            ShootObject(cam, "cpu_challenge", "Challenge_Wall_Station", centre, 3.5f, 0.2f);

            var sim = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).First(b => b.GetType().Name == "SimulationController");
            sim.GetType().GetMethod("StartSimulation").Invoke(sim, null);
            yield return new WaitForSeconds(6f);
            ShootObject(cam, "cpu_running_queue", "Physical_Ready_Queue", cam.transform.position, 3.2f);
            ShootObject(cam, "cpu_running_gantt", "Teaching_Whiteboard", cam.transform.position, 4f, 0.2f);
        }
    
        [UnityTest]
        public IEnumerator CpuLabTextReport()
        {
            SceneManager.LoadScene("GridlockCPUScheduling");
            yield return null;
            yield return new WaitForSeconds(2f);
            var lines = new System.Collections.Generic.List<string>();
            foreach (var t in Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsSortMode.None).OrderBy(t => t.name))
            {
                var rt = t.rectTransform;
                var canvas = t.canvas;
                string space = t is TMPro.TextMeshPro ? "3D" : canvas != null ? canvas.rootCanvas.renderMode.ToString() : "?";
                string text = t.text.Replace("\n", " / ");
                if (text.Length > 50) text = text.Substring(0, 50);
                lines.Add($"{space,-22} {t.name,-28} size={t.fontSize,6:0.##} auto={t.enableAutoSizing} rect={rt.rect.width:0.##}x{rt.rect.height:0.##} " +
                          $"lossy={rt.lossyScale.x:0.###} font={t.font?.name} style={t.fontStyle} wrap={t.textWrappingMode} ovf={t.isTextOverflowing} \"{text}\"  path={Path(t.transform)}");
            }
            File.WriteAllLines("Logs/cpu_text_report.txt", lines);
        }

        static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;
    }
}
