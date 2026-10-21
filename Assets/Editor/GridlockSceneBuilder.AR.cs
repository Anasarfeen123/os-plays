using System;
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
        static readonly Vector3 DioramaPivot = new Vector3(0f, -1.22f, 4f); // bottom of the base slab, under the lot

        /// <summary>The lot scenes: the city block on a grass slab, 1 : 60.</summary>
        static void BuildARRig(XRModeController mode, Transform desktopHud) =>
            BuildARRig(mode, desktopHud, new ARDioramaSettings(), DioramaPivot, root =>
            {
                // A grass slab under the miniature (about 2 cm thick): the life-size ground (200 m of grass) is hidden in AR.
                var plate = Box("Grass Slab", root, new Vector3(0f, -0.62f, 4f), new Vector3(66f, 1.2f, 58f), mats.Grass, keepWorld: true);
                Object.DestroyImmediate(plate.GetComponent<Collider>());
            }, footprint: new Rect(-33f, -25f, 66f, 58f), thickness: 1.2f, label: "OS PLAYS  ·  PARKING LOT");

        /// <summary>
        /// A room as a dollhouse: a dark plinth with a glowing edge and a name plate under its floor, ceiling left out and
        /// walls cut away on the phone's side (<see cref="ARDioramaSettings"/>).
        /// </summary>
        /// <param name="fillLight">Intensity of an extra soft light from above, AR only (0: none), for rooms lit only by small
        /// lamps that look black from a tabletop distance.</param>
        static void BuildRoomARRig(XRModeController mode, Transform hud, ARDioramaSettings settings, Rect floor, float floorY, string label,
                                   Color accent, ScenarioButtons buttons = null, float fillLight = 0f)
        {
            float thickness = 0.02f * settings.scale; // about 2 cm on the table
            var pivot = new Vector3(floor.center.x, floorY - thickness, floor.center.y);
            const float margin = 0.25f;
            var plinthRect = new Rect(floor.xMin - margin, floor.yMin - margin, floor.width + 2f * margin, floor.height + 2f * margin);
            BuildARRig(mode, hud, settings, pivot, root =>
            {
                var plinth = Box("Plinth", root, new Vector3(plinthRect.center.x, floorY - thickness / 2f - 0.01f, plinthRect.center.y),
                                 new Vector3(plinthRect.width, thickness, plinthRect.height), ARPlinthMat(), keepWorld: true);
                Object.DestroyImmediate(plinth.GetComponent<Collider>());
                // Glowing edge along the top of the plinth.
                float edge = 0.06f * settings.scale / 30f;
                var edgeMat = ARGlowMat(accent);
                foreach (var (centre, size) in new[]
                         {
                             (new Vector3(plinthRect.center.x, floorY, plinthRect.yMin), new Vector3(plinthRect.width, edge, edge)),
                             (new Vector3(plinthRect.center.x, floorY, plinthRect.yMax), new Vector3(plinthRect.width, edge, edge)),
                             (new Vector3(plinthRect.xMin, floorY, plinthRect.center.y), new Vector3(edge, edge, plinthRect.height)),
                             (new Vector3(plinthRect.xMax, floorY, plinthRect.center.y), new Vector3(edge, edge, plinthRect.height)),
                         })
                {
                    var strip = Box("Edge", root, centre, size, edgeMat, keepWorld: true);
                    Object.DestroyImmediate(strip.GetComponent<Collider>());
                    strip.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                }
                if (fillLight <= 0f) return;
                var light = new GameObject("AR Fill Light", typeof(Light)).GetComponent<Light>();
                light.transform.SetParent(root, false);
                light.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
                light.type = LightType.Directional;
                light.intensity = fillLight;
                light.color = new Color(0.92f, 0.95f, 1f);
                light.shadows = LightShadows.None;
            }, plinthRect, thickness, label, buttons, labelColor: accent);
        }

        static void BuildARRig(XRModeController mode, Transform desktopHud, ARDioramaSettings settings, Vector3 pivotPosition,
                               Action<Transform> buildBase, Rect footprint, float thickness, string label, ScenarioButtons buttons = null,
                               Color? labelColor = null)
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
            // Real things in front of the model hide it (ARCore depth, on phones that have it; ARCameraBackground scales
            // the real depth by the rig's scale).
            var occlusion = camGo.AddComponent<AROcclusionManager>();
            occlusion.requestedEnvironmentDepthMode = EnvironmentDepthMode.Medium;
            occlusion.requestedOcclusionPreferenceMode = OcclusionPreferenceMode.PreferEnvironmentOcclusion;
            occlusion.environmentDepthTemporalSmoothingRequested = true;
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
            camGo.AddComponent<ARLightEstimation>().Init(camGo.GetComponent<ARCameraManager>(), originGo.transform, settings.outdoorLight);

            var pivot = Marker("AR Pivot", root.transform, pivotPosition);
            var reticle = Reticle(root.transform);

            // The base, its shadow on the real surface and the name plate: shown only in AR.
            var plate = new GameObject("AR Diorama Base").transform;
            plate.SetParent(root.transform, false);
            buildBase(plate);
            ContactShadow(plate, footprint, pivotPosition.y);
            if (!string.IsNullOrEmpty(label)) NamePlate(plate, footprint, pivotPosition.y, thickness, label, labelColor ?? GuideCyan);
            plate.gameObject.SetActive(false);

            // The ring that flashes under a tapped thing (a copy of the placement ring's material, faded out as it grows).
            var pulse = new GameObject("Tap Pulse").transform;
            pulse.SetParent(root.transform, false);
            var pulseRing = GameObject.CreatePrimitive(PrimitiveType.Quad);
            pulseRing.name = "Ring";
            Object.DestroyImmediate(pulseRing.GetComponent<Collider>());
            pulseRing.transform.SetParent(pulse, false);
            pulseRing.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var pulseRenderer = pulseRing.GetComponent<Renderer>();
            pulseRenderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{GeneratedDir}/ARReticle.mat");
            pulseRenderer.shadowCastingMode = ShadowCastingMode.Off;
            var tapPulse = pulse.gameObject.AddComponent<ARTapPulse>();
            tapPulse.Init(pulseRenderer);

            var guidance = ARGuidance(desktopHud, buttons);
            originGo.AddComponent<ARStage>().Init(origin, raycasts, planes, cam, pivot, reticle, plate.gameObject, guidance.Root, guidance.Hint, guidance.Scale,
                                                  guidance.Move, guidance.Planes, guidance.PlanesLabel, guidance.Help, settings);
            originGo.GetComponent<ARStage>().SetTapPulse(tapPulse);

            root.SetActive(false);
            mode.SetARRig(root);
        }

        /// <summary>A soft dark patch on the real surface under the model, so it sits on the table instead of floating.</summary>
        static void ContactShadow(Transform parent, Rect footprint, float y)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Contact Shadow";
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(parent, false);
            quad.transform.SetPositionAndRotation(new Vector3(footprint.center.x, y + 0.02f, footprint.center.y), Quaternion.Euler(90f, 0f, 0f));
            quad.transform.localScale = new Vector3(footprint.width * 1.22f, footprint.height * 1.22f, 1f);
            var r = quad.GetComponent<Renderer>();
            r.sharedMaterial = ARShadowMat();
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        /// <summary>The model's name on the front (−Z) face of its base, readable from where it is placed.</summary>
        static void NamePlate(Transform parent, Rect footprint, float pivotY, float thickness, string label, Color color)
        {
            var go = new GameObject("Name Plate");
            go.transform.SetParent(parent, false);
            float height = thickness * 0.6f; // letters across the middle of the base's front face
            go.transform.SetPositionAndRotation(new Vector3(footprint.center.x, pivotY + thickness * 0.5f, footprint.yMin - 0.03f), Quaternion.identity);
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.font = font;
            tmp.text = label;
            tmp.color = color;
            tmp.fontStyle = FontStyles.Bold;
            tmp.characterSpacing = 8f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 0.1f;
            tmp.fontSizeMax = 400f;
            tmp.rectTransform.sizeDelta = new Vector2(footprint.width * 0.7f, height);
        }

        static Material ARPlinthMat()
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", new Color(0.06f, 0.08f, 0.12f));
            mat.SetFloat("_Smoothness", 0.55f);
            mat.SetFloat("_Metallic", 0.3f);
            return SaveMat("ARPlinth", mat);
        }

        static Material ARGlowMat(Color color)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.SetColor("_BaseColor", color * 1.4f);
            return SaveMat($"ARGlow_{ColorUtility.ToHtmlStringRGB(color)}", mat);
        }

        static Material ARShadowMat()
        {
            var path = $"{GeneratedDir}/ARShadow.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var mat = Transparent(new Material(Shader.Find("Universal Render Pipeline/Unlit")) { mainTexture = ShadowTexture() });
            mat.SetColor("_BaseColor", new Color(0f, 0f, 0f, 0.62f));
            return SaveMat("ARShadow", mat);
        }

        /// <summary>A rounded rectangle that fades out softly at its edges.</summary>
        static Texture2D ShadowTexture()
        {
            const int size = 128;
            var path = $"{GeneratedDir}/ARShadow.png";
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // Distance outside an inner rectangle (0.18 … 0.82), as a fraction of the half size.
                float u = Mathf.Abs((x + 0.5f) / size - 0.5f) * 2f, v = Mathf.Abs((y + 0.5f) / size - 0.5f) * 2f;
                float dx = Mathf.Max(0f, u - 0.64f), dy = Mathf.Max(0f, v - 0.64f);
                float d = Mathf.Sqrt(dx * dx + dy * dy) / 0.36f;
                float a = Mathf.Clamp01(1f - d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a * (3f - 2f * a)));
            }
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
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

        struct ARGuidanceUI
        {
            public GameObject Root;
            public TMP_Text Hint, Scale, PlanesLabel, Help;
            public UnityEngine.UI.Button Move, Planes;
        }

        /// <summary>
        /// The AR overlay on the phone HUD (inactive until AR starts): the instruction pill and MOVE / GRID top centre, the
        /// scene's action buttons bottom right (the headset's face-button actions), and a help line bottom centre for scenes
        /// whose HUD has none. Anchored to the screen's edges, so it sits right on any phone's aspect ratio.
        /// </summary>
        static ARGuidanceUI ARGuidance(Transform hud, ScenarioButtons buttons)
        {
            var root = new GameObject("AR Guidance", typeof(RectTransform));
            var rt = (RectTransform)root.transform;
            rt.SetParent(hud, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            root.transform.SetAsFirstSibling(); // under the scene's own panels and the menu

            var top = Anchored("Top", root.transform, new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(620f, 190f));
            var pill = Bordered("Hint", top, 0, 0, 620, 96, Opaque(ViewColor.Panel), GuideCyan);
            UIText("AR", pill.transform, 20, 10, 80, 76, "AR", 34, GuideCyan, TextAlignmentOptions.Center, FontStyles.Bold);
            var hint = UIText("Text", pill.transform, 100, 8, 400, 80, "Starting AR…", 22, ViewColor.Text, TextAlignmentOptions.Left);
            hint.enableAutoSizing = true;
            hint.fontSizeMin = 14f;
            hint.fontSizeMax = 22f;
            var scale = UIText("Scale", pill.transform, 500, 8, 104, 80, "", 20, Muted, TextAlignmentOptions.Right, FontStyles.Bold);
            var (move, _) = UIButton("MoveLot", top, 0, 110, 300, 70, "MOVE", 26, GuideCyan, new Color(0.04f, 0.07f, 0.12f));
            var (planes, planesLabel) = UIButton("Grid", top, 320, 110, 300, 70, "HIDE GRID", 24, new Color(0.16f, 0.21f, 0.32f), ViewColor.Text);

            if (buttons != null)
            {
                var actions = new[] { (nameof(ScenarioButtons.PressA), (UnityEngine.Events.UnityAction)buttons.PressA),
                                      (nameof(ScenarioButtons.PressB), buttons.PressB), (nameof(ScenarioButtons.PressX), buttons.PressX) }
                    .Where(a => !string.IsNullOrEmpty(buttons.Label(a.Item1))).ToArray();
                if (actions.Length > 0)
                {
                    // A column on the right edge, above the corner: within reach of the right thumb, clear of the help line.
                    const float w = 260f, h = 84f, gap = 14f;
                    float height = actions.Length * h + (actions.Length - 1) * gap;
                    var column = Anchored("Actions", root.transform, new Vector2(1f, 0f), new Vector2(-24f - w / 2f, 110f + height / 2f), new Vector2(w, height));
                    for (int i = 0; i < actions.Length; i++)
                    {
                        var (button, _) = UIButton(actions[i].Item1, column, 0, i * (h + gap), w, h, buttons.Label(actions[i].Item1).ToUpperInvariant(), 26,
                                                   i == 0 ? GuideCyan : new Color(0.16f, 0.21f, 0.32f), i == 0 ? new Color(0.04f, 0.07f, 0.12f) : ViewColor.Text);
                        UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick, actions[i].Item2);
                    }
                }
            }

            // While no surface is found: a phone that sways from side to side over a dotted arc, mid-screen.
            var scan = Anchored("Scan", root.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(360f, 260f));
            for (int i = 0; i < 9; i++)
            {
                float a = Mathf.Lerp(-50f, 50f, i / 8f) * Mathf.Deg2Rad;
                var dot = Panel("Dot", scan, 180f + Mathf.Sin(a) * 150f - 5f, 200f - Mathf.Cos(a) * 60f - 5f, 10, 10, new Color(0.35f, 0.85f, 1f, 0.55f));
                dot.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            }
            var phone = Bordered("Phone", scan, 135, 40, 90, 160, new Color(0.04f, 0.07f, 0.12f, 0.85f), GuideCyan);
            Panel("Screen", phone.transform, 12, 18, 66, 116, new Color(0.35f, 0.85f, 1f, 0.25f));
            foreach (var g in phone.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) g.raycastTarget = false;
            var phoneRect = (RectTransform)phone.transform;
            phoneRect.pivot = new Vector2(0.5f, 0f); // tilts about its bottom edge, like a wrist turning
            phoneRect.anchoredPosition += new Vector2(45f, -160f);
            phone.AddComponent<ARScanAnimation>();
            scan.gameObject.SetActive(false);

            var helpBar = Anchored("Help Bar", root.transform, new Vector2(0.5f, 0f), new Vector2(0f, 46f), new Vector2(1100f, 56f));
            var bar = Panel("Background", helpBar, 0, 0, 1100, 56, new Color(0.04f, 0.06f, 0.1f, 0.82f));
            bar.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            var help = UIText("ARHelp", helpBar, 24, 0, 1052, 56, "", 22, ViewColor.Text, TextAlignmentOptions.Center, FontStyles.Bold);
            help.enableAutoSizing = true;
            help.fontSizeMin = 14f;
            help.fontSizeMax = 22f;

            root.SetActive(false);
            return new ARGuidanceUI { Root = root, Hint = hint, Scale = scale, Move = move, Planes = planes, PlanesLabel = planesLabel, Help = help };
        }

        /// <summary>A box of the given size centred at an offset from an anchor point of the parent; children use top-left pixels.</summary>
        static Transform Anchored(string name, Transform parent, Vector2 anchor, Vector2 offset, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            // The offset places the box's centre for side/bottom anchors and its top edge for the top anchor.
            rt.anchoredPosition = anchor.y >= 1f ? offset - new Vector2(0f, size.y / 2f) : offset;
            return go.transform;
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
