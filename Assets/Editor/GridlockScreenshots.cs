using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Gridlock.Editor
{
    /// <summary>
    /// Renders review shots of the hub room into Docs/Screenshots (edit-time, so the monitor shows its tiles without
    /// the boot animation) and of the deadlock lot. Batchmode (needs graphics, so no -nographics):
    /// -executeMethod Gridlock.Editor.GridlockScreenshots.Hub (or .Deadlock, or .All)
    /// </summary>
    public static class GridlockScreenshots
    {
        public static void All()
        {
            Hub();
            Deadlock();
            Phone();
        }

        [MenuItem("GRIDLOCK/Screenshots/Hub Room")]
        public static void Hub()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/GridlockHub.unity");
            var go = new GameObject("ShotCamera", typeof(Camera));
            var cam = go.GetComponent<Camera>();
            cam.farClipPlane = 400f;
            cam.nearClipPlane = 0.02f;
            var data = UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(cam);
            data.renderPostProcessing = true;
            data.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.SubpixelMorphologicalAntiAliasing;

            Shot(cam, "hub_01_seated", new Vector3(0f, 1.2f, -0.9f), new Vector3(0f, 1.15f, 0.45f), 70f);
            Shot(cam, "hub_02_room", new Vector3(2.8f, 2.4f, -2.6f), new Vector3(-0.3f, 0.9f, 0.4f), 75f);
            Shot(cam, "hub_03_window", new Vector3(-1.2f, 1.5f, -0.5f), new Vector3(-12f, 8f, 60f), 70f);
            Shot(cam, "hub_04_desk", new Vector3(-0.5f, 1.15f, -0.75f), new Vector3(-0.6f, 0.78f, 0.05f), 60f);
            Shot(cam, "hub_05_overview", new Vector3(7.5f, 4.3f, -6.5f), new Vector3(-1f, 0.5f, 1.5f), 80f);
            Shot(cam, "hub_06_lab", new Vector3(0.5f, 1.4f, -1.0f), new Vector3(-6f, 1.2f, 0.5f), 75f);
            Shot(cam, "hub_07_back", new Vector3(0f, 1.3f, -0.5f), new Vector3(0f, 1.4f, -7f), 80f);
            Shot(cam, "hub_09_lounge", new Vector3(-2.2f, 1.6f, 1.4f), new Vector3(-6f, 0.6f, 5.5f), 70f);
            Shot(cam, "hub_10_ship_closeup", new Vector3(-90f, 9f, 41f), new Vector3(-90f, 6f, 55f), 60f); // flies towards +X
            Shot(cam, "hub_11_showcase", new Vector3(1.5f, 1.7f, -0.5f), new Vector3(4.4f, 1.2f, 1.6f), 65f);
            Shot(cam, "hub_08_right", new Vector3(-0.5f, 1.4f, -0.8f), new Vector3(7f, 1.2f, 0.5f), 75f);
            Object.DestroyImmediate(go);
        }

        [MenuItem("GRIDLOCK/Screenshots/Deadlock Lot")]
        public static void Deadlock()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/GridlockParkingLot.unity");
            var go = new GameObject("ShotCamera", typeof(Camera));
            var cam = go.GetComponent<Camera>();
            cam.farClipPlane = 400f;
            var data = UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(cam);
            data.renderPostProcessing = true;
            data.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            Shot(cam, "lot_01_overview", new Vector3(0f, 26f, -31f), new Vector3(0f, 0f, 3f), 55f);
            Shot(cam, "lot_02_vr_start", new Vector3(0f, 1.6f, -10.9f), new Vector3(0f, 1f, 5f), 90f);
            Shot(cam, "lot_03_street", new Vector3(19f, 2f, -20f), new Vector3(12f, 1f, 15f), 75f);
            Shot(cam, "lot_04_aerial", new Vector3(-30f, 35f, -40f), new Vector3(0f, 0f, 0f), 60f);
            Object.DestroyImmediate(go);
        }

        /// <summary>
        /// What a 20:9 phone shows: each scene from its desktop camera with the HUD in its touch form (help text
        /// swapped, keypad shown). The overlay HUD is switched to camera space for the capture only; nothing is saved.
        /// </summary>
        [MenuItem("GRIDLOCK/Screenshots/Phone")]
        public static void Phone()
        {
            PhoneShot("Assets/Scenes/GridlockHub.unity", "phone_01_hub");
            PhoneShot("Assets/Scenes/GridlockParkingLot.unity", "phone_02_deadlock");
            PhoneShot("Assets/Scenes/GridlockBanker.unity", "phone_03_banker");
        }

        static void PhoneShot(string scenePath, string name)
        {
            if (!File.Exists(scenePath)) return;
            EditorSceneManager.OpenScene(scenePath);
            var desktop = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .First(c => c.name == "Desktop Camera");
            desktop.gameObject.SetActive(true);
            foreach (var variant in desktop.GetComponentsInChildren<Gridlock.UI.TouchVariant>(true))
            {
                var so = new SerializedObject(variant);
                var show = (Gridlock.UI.TouchVariant.Show)so.FindProperty("show").enumValueIndex;
                variant.gameObject.SetActive(show != Gridlock.UI.TouchVariant.Show.DesktopOnly);
                var text = so.FindProperty("touchText").stringValue;
                if (!string.IsNullOrEmpty(text) && variant.TryGetComponent<TMPro.TMP_Text>(out var tmp)) tmp.text = text;
            }
            var hud = desktop.GetComponentInChildren<Canvas>(true);
            hud.renderMode = RenderMode.ScreenSpaceCamera;
            hud.worldCamera = desktop;
            hud.planeDistance = desktop.nearClipPlane + 0.05f;
            Render(desktop, name, 2400, 1080);
        }

        static void Shot(Camera cam, string name, Vector3 pos, Vector3 lookAt, float fov)
        {
            cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(lookAt - pos));
            cam.fieldOfView = fov;
            Render(cam, name, 1920, 1080);
        }

        static void Render(Camera cam, string name, int width, int height)
        {
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            Directory.CreateDirectory("Docs/Screenshots");
            File.WriteAllBytes($"Docs/Screenshots/{name}.png", tex.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Debug.Log($"[GRIDLOCK] Shot {name}");
        }
    }
}
