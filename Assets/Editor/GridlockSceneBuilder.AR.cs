using System.IO;
using System.Linq;
using Gridlock.AR;
using Gridlock.Core;
using Gridlock.XR;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Object = UnityEngine.Object;
using TrackedPoseDriver = UnityEngine.InputSystem.XR.TrackedPoseDriver;

namespace Gridlock.Editor
{
    /// <summary>
    /// Phone AR for the lot scenes: AR Session + an AR origin with the phone camera (handheld pose, camera feed), plane
    /// detection and raycasts, a plane-grid prefab, the placement ring, a base slab for the miniature, and the on-screen
    /// guidance. Inactive unless <see cref="XRModeController"/> picks AR (Android phones).
    /// </summary>
    public static partial class GridlockSceneBuilder
    {
        const string PlanePrefabPath = "Assets/Prefabs/AR/ARPlane.prefab";
        static readonly Vector3 DioramaPivot = new Vector3(0f, -0.62f, 4f); // bottom of the base slab, under the lot

        static void BuildARRig(XRModeController mode, Transform desktopHud)
        {
            EnsureARBackgroundFeature();
            var root = new GameObject("AR (phone)");

            var session = new GameObject("AR Session", typeof(ARSession), typeof(ARInputManager));
            session.transform.SetParent(root.transform, false);

            var originGo = new GameObject("XR Origin (AR)");
            originGo.transform.SetParent(root.transform, false);
            var offset = new GameObject("Camera Offset").transform;
            offset.SetParent(originGo.transform, false);
            var camGo = new GameObject("AR Camera", typeof(Camera), typeof(AudioListener), typeof(ARCameraManager), typeof(ARCameraBackground));
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(offset, false);
            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 2000f;
            cam.allowHDR = false;
            var camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.None;
            var pose = camGo.AddComponent<TrackedPoseDriver>();
            pose.positionInput = new InputActionProperty(new InputAction("Position", binding: "<HandheldARInputDevice>/devicePosition"));
            pose.rotationInput = new InputActionProperty(new InputAction("Rotation", binding: "<HandheldARInputDevice>/deviceRotation"));

            var origin = originGo.AddComponent<XROrigin>();
            origin.Camera = cam;
            origin.CameraFloorOffsetObject = offset.gameObject;
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.NotSpecified;
            origin.CameraYOffset = 0f; // ARCore poses are already real: a seated-height offset would float the camera above the planes
            var planes = originGo.AddComponent<ARPlaneManager>();
            planes.planePrefab = PlanePrefab();
            planes.requestedDetectionMode = PlaneDetectionMode.Horizontal;
            var raycasts = originGo.AddComponent<ARRaycastManager>();

            var pivot = Marker("AR Pivot", root.transform, DioramaPivot);
            var reticle = Reticle(root.transform);

            // A grass slab under the miniature: the life-size ground (200 m of grass) is hidden in AR.
            var plate = Box("AR Diorama Base", root.transform, new Vector3(0f, -0.32f, 4f), new Vector3(66f, 0.6f, 58f), mats.Grass);
            Object.DestroyImmediate(plate.GetComponent<Collider>());
            plate.SetActive(false);

            var (ui, hint, scale, move, planesButton, planesLabel) = ARGuidance(desktopHud);
            originGo.AddComponent<ARStage>().Init(origin, raycasts, planes, cam, pivot, reticle, plate, ui, hint, scale, move, planesButton, planesLabel);

            root.SetActive(false);
            mode.SetARRig(root);
        }

        /// <summary>Translucent cyan grid with a bright outline, shown on every surface ARCore finds.</summary>
        static GameObject PlanePrefab()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PlanePrefabPath));
            var go = new GameObject("AR Plane", typeof(ARPlane), typeof(MeshFilter), typeof(MeshRenderer), typeof(LineRenderer),
                                    typeof(ARPlaneMeshVisualizer));
            // A copy of the bay-ring material, which renders on phones; only the texture and tint change.
            var mat = new Material(mats.Ring) { mainTexture = GridTexture() };
            mat.SetColor("_BaseColor", new Color(0.35f, 0.85f, 1f, 0.45f));
            mat.mainTextureScale = Vector2.one * 10f;
            go.GetComponent<MeshRenderer>().sharedMaterial = SaveMat("ARPlaneGrid", mat);
            var line = go.GetComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.widthMultiplier = 0.006f;
            line.sharedMaterial = mats.Line;
            line.startColor = line.endColor = new Color(0.35f, 0.85f, 1f, 1f);
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, PlanePrefabPath);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static Texture2D GridTexture()
        {
            const int size = 64;
            var path = $"{GeneratedDir}/ARGrid.png";
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                bool line = x < 2 || y < 2;
                bool dot = (x - 32) * (x - 32) + (y - 32) * (y - 32) < 9;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, line ? 0.9f : dot ? 0.6f : 0.12f));
            }
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Placement ring with a "front" notch, lying flat on the found surface.</summary>
        static Transform Reticle(Transform parent)
        {
            var root = new GameObject("Placement Reticle").transform;
            root.SetParent(parent, false);
            var ring = GameObject.CreatePrimitive(PrimitiveType.Quad);
            ring.name = "Ring";
            Object.DestroyImmediate(ring.GetComponent<Collider>());
            ring.transform.SetParent(root, false);
            ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var r = ring.GetComponent<Renderer>();
            var mat = new Material(mats.Ring);
            mat.SetColor("_BaseColor", new Color(0.35f, 0.85f, 1f, 1f));
            r.sharedMaterial = SaveMat("ARReticle", mat);
            r.shadowCastingMode = ShadowCastingMode.Off;
            var arrow = GameObject.CreatePrimitive(PrimitiveType.Quad);
            arrow.name = "Front";
            Object.DestroyImmediate(arrow.GetComponent<Collider>());
            arrow.transform.SetParent(root, false);
            arrow.transform.localPosition = new Vector3(0f, 0.002f, -0.62f);
            arrow.transform.localRotation = Quaternion.Euler(90f, 0f, 180f);
            arrow.transform.localScale = new Vector3(0.22f, 0.24f, 1f);
            arrow.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{GeneratedDir}/GuideArrow.mat");
            return root;
        }

        /// <summary>Instruction pill and buttons on the phone HUD (inactive until AR starts).</summary>
        static (GameObject, TMP_Text, TMP_Text, UnityEngine.UI.Button, UnityEngine.UI.Button, TMP_Text) ARGuidance(Transform hud)
        {
            var root = new GameObject("AR Guidance", typeof(RectTransform));
            Rect(root, hud, 0, 0, 1920, 1080);
            var t = root.transform;
            var pill = Bordered("Hint", t, 780, 16, 580, 96, Opaque(ViewColor.Panel), GuideCyan);
            UIText("AR", pill.transform, 20, 10, 80, 76, "AR", 34, GuideCyan, TextAlignmentOptions.Center, FontStyles.Bold);
            var hint = UIText("Text", pill.transform, 100, 8, 360, 80, "Starting AR…", 22, ViewColor.Text, TextAlignmentOptions.Left);
            hint.enableAutoSizing = true;
            hint.fontSizeMin = 14f;
            hint.fontSizeMax = 22f;
            var scale = UIText("Scale", pill.transform, 460, 8, 104, 80, "", 20, Muted, TextAlignmentOptions.Right, FontStyles.Bold);
            var (move, _) = UIButton("MoveLot", t, 780, 124, 280, 70, "MOVE LOT", 26, GuideCyan, new Color(0.04f, 0.07f, 0.12f));
            var (planes, planesLabel) = UIButton("Grid", t, 1076, 124, 284, 70, "HIDE GRID", 24, new Color(0.16f, 0.21f, 0.32f), ViewColor.Text);
            root.SetActive(false);
            return (root, hint, scale, move, planes, planesLabel);
        }

        /// <summary>URP draws the phone camera feed through AR Foundation's background renderer feature.</summary>
        static void EnsureARBackgroundFeature()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRendererData", new[] { "Assets/Settings" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                if (data == null || data.rendererFeatures.Any(f => f is ARBackgroundRendererFeature)) continue;

                var feature = ScriptableObject.CreateInstance<ARBackgroundRendererFeature>();
                feature.name = "AR Background";
                AssetDatabase.AddObjectToAsset(feature, data);
                data.rendererFeatures.Add(feature);
                // Keep URP's feature map in step with the list (what the renderer inspector's "Add Feature" does).
                var so = new SerializedObject(data);
                var map = so.FindProperty("m_RendererFeatureMap");
                if (map != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId))
                {
                    map.InsertArrayElementAtIndex(map.arraySize);
                    map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssets();
                Debug.Log($"[GRIDLOCK] Added the AR background renderer feature to {path}");
            }
        }
    }
}
