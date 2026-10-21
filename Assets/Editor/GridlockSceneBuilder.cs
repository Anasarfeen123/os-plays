using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gridlock.Core;
using Gridlock.Deadlock;
using Gridlock.UI;
using Gridlock.XR;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.XR.CoreUtils;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Jump;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Gridlock.Editor
{
    /// <summary>
    /// Generates both GRIDLOCK scenes on the same life-size parking lot (six bays, Kenney streets and buildings, XR rig with
    /// teleportation, desktop camera + HUD):
    ///  • GridlockParkingLot.unity — Deadlock Detection mode (this file)
    ///  • GridlockBanker.unity — Banker's Algorithm mode (GridlockSceneBuilder.Banker.cs)
    /// Menu: GRIDLOCK ▸ Build Scenes. Batchmode: -executeMethod Gridlock.Editor.GridlockSceneBuilder.Build
    /// </summary>
    public static partial class GridlockSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/GridlockParkingLot.unity";
        const string DeadlockSceneName = "GridlockParkingLot";
        const string GeneratedDir = "Assets/Materials/Generated";
        const string KenneyDir = "Assets/ThirdParty/Kenney";
        const string XRIDir = "Assets/Samples/XR Interaction Toolkit/3.3.2";

        // ---- Lot layout (metres). Top row faces the aisle southwards, bottom row northwards. ----
        const float BayPitch = 3.2f, BayWidth = 3.0f, BayDepth = 5.4f;
        const float TopRowZ = 6.9f, BottomRowZ = -5.4f, AisleZ = 0.75f;
        const float LotMinX = -8f, LotMaxX = 8f, LotMinZ = -9.5f, LotMaxZ = 11f;
        const float StreetX = 14f, StreetWidth = 8f;
        const float CarLength = 4.4f, KitScaleRoads = 8f, KitScaleBuildings = 12f;

        static readonly string[] SpaceIds = { "P1", "P2", "P3", "P4", "P5", "P6" };
        static readonly (string id, string model, Color color)[] Cars =
        {
            ("A", "van", ViewColor.Fleet[0]), ("B", "suv", ViewColor.Fleet[1]),
            ("C", "taxi", ViewColor.Fleet[2]), ("D", "police", ViewColor.Fleet[3]),
        };

        static readonly Color Amber = new Color(0.98f, 0.75f, 0.14f);
        static readonly Color Slate = new Color(0.12f, 0.16f, 0.26f);
        static readonly Color Muted = new Color(0.58f, 0.64f, 0.72f);

        static Materials mats;
        static TMP_FontAsset font;
        static Sprite roundedSprite;

        class Materials
        {
            public Material Asphalt, Concrete, Grass, Paint, BayFill, Ring, TeleportRing, Line, Dark, Cars, Roads, Buildings, Sky, Fade;
        }

        [MenuItem("GRIDLOCK/Build Scenes")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if ((File.Exists(ScenePath) || File.Exists(BankerScenePath)) &&
                !EditorUtility.DisplayDialog("GRIDLOCK", "Rebuild the GRIDLOCK scenes (hub, deadlock, banker, CPU scheduling, memory)? Manual edits to them are replaced.", "Rebuild", "Cancel"))
                return;
            Build();
        }

        public static void Build()
        {
            font = TMP_Settings.defaultFontAsset;
            if (font == null) throw new InvalidOperationException("TextMesh Pro essentials are missing (Assets/TextMesh Pro).");
            roundedSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            mats = CreateMaterials();

            BuildCpuScene(); // first: they render the hub's tile pictures
            BuildMemoryScene();
            BuildHubScene();
            BuildDeadlockScene();
            BuildBankerScene();
            AddToBuildSettings();
            AssetDatabase.SaveAssets();
        }

        /// <summary>Lighting, post-processing, ground, streets, props and buildings shared by both scenes.</summary>
        static void BuildWorld()
        {
            BuildLighting();
            var env = new GameObject("Environment").transform;
            BuildGround(env);
            BuildStreets(env);
            BuildProps(env);
            BuildStreetLife(env);
            BuildGreenery(env);
            BuildBuildings(env);
            BuildSystems();
        }

        /// <summary>XR interaction manager, UI event system and the scene fade-in shared by every scene.</summary>
        static ScreenFader BuildSystems(bool arriveSound = true)
        {
            new GameObject("XR Interaction Manager", typeof(XRInteractionManager));
            new GameObject("EventSystem", typeof(EventSystem), typeof(XRUIInputModule));
            new GameObject("UI Interaction Reliability").AddComponent<UIInteractionReliability>();
            var fader = new GameObject("Screen Fader").AddComponent<ScreenFader>();
            fader.Init(mats.Fade, arriveSound ? Clip("Hub/arrive.wav") : null);
            return fader;
        }

        static void BuildDeadlockScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildWorld();

            var spaceRoot = new GameObject("ParkingSpaces").transform;
            for (int i = 0; i < SpaceIds.Length; i++) BuildSpace(SpaceIds[i], i, spaceRoot, interactive: true);

            var carRoot = new GameObject("Cars").transform;
            for (int i = 0; i < Cars.Length; i++)
            {
                // Start queued on the west street, facing south towards the entry.
                var home = new Vector3(-StreetX + 2f, 0f, AisleZ + 8f + i * 6.5f);
                BuildCar(Cars[i].id, "Car_" + Cars[i].id, Cars[i].model, Cars[i].color, carRoot, home, Quaternion.Euler(0f, 180f, 0f), full: true);
            }

            var routeRoot = new GameObject("Route").transform;
            var street = Marker("StreetPoint", routeRoot, new Vector3(-StreetX + 2f, 0f, AisleZ));
            var entry = Marker("EntryPoint", routeRoot, new Vector3(LotMinX + 1.5f, 0f, AisleZ));

            var simGo = new GameObject("Simulation");
            var sim = simGo.AddComponent<SimulationManager>();
            sim.Init(street, entry);
            simGo.AddComponent<DeadlockVisualizer>();
            var overlay = simGo.AddComponent<AnalysisOverlay>();
            overlay.Init(BuildArrowTemplate(simGo.transform), BuildCardTemplate(simGo.transform));
            var cityAudio = simGo.AddComponent<GridlockAudio>();
            cityAudio.Init(Clip("city_ambience.wav"), Clip("Kenney/confirmation_002.ogg"),
                Clip("car_horn.wav"), Clip("deadlock_alarm.wav"), Clip("Kenney/click_002.ogg"),
                Clip("Kenney/maximize_006.ogg"), Clip("Kenney/minimize_006.ogg"), Clip("Kenney/back_002.ogg"), Clip("Kenney/error_004.ogg"));
            var interaction = simGo.AddComponent<PlayerInteraction>();

            foreach (var insp in Object.FindObjectsByType<Inspectable>(FindObjectsSortMode.None))
            {
                var so = new SerializedObject(insp);
                so.FindProperty("overlay").objectReferenceValue = overlay;
                so.FindProperty("interaction").objectReferenceValue = interaction;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            BuildScoreboard(sim, overlay, interaction);
            BuildPodium(sim, overlay, interaction);
            Transform desktopHud = null;
            var mode = BuildPresentation(hud =>
            {
                BuildDesktopHud(hud, sim, overlay, interaction);
                desktopHud = hud.Find("Desktop HUD");
            }, (menu, keys, origin) =>
            {
                keys.Init(xHoldSeconds: 0.8f);
                UnityEditor.Events.UnityEventTools.AddPersistentListener(keys.ButtonA, overlay.Toggle);
                UnityEditor.Events.UnityEventTools.AddPersistentListener(keys.ButtonB, interaction.TowSelected);
                UnityEditor.Events.UnityEventTools.AddPersistentListener(keys.ButtonX, sim.Restart);
            });
            var tour = BuildDeadlockTour(sim, overlay, interaction, mode, cityAudio, desktopHud);
            BuildARRig(mode, desktopHud);
            BuildAudioSystems("music_lot");
            BuildSystemMenu(mode, DesktopCameraTransform(), "DEADLOCK DETECTION", isHome: false, tour, sim.Restart, DeadlockHowTo,
                            "BANKER MODE ►", BankerSceneName);

            WireTeleportTargets();
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[GRIDLOCK] Built {ScenePath}");
        }

        // =====================================================================================================
        // Materials
        // =====================================================================================================

        static Materials CreateMaterials()
        {
            Directory.CreateDirectory(GeneratedDir);
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            var particles = Shader.Find("Universal Render Pipeline/Particles/Unlit");

            var m = new Materials
            {
                Asphalt = LitMat("Asphalt", lit, new Color(0.17f, 0.18f, 0.20f), 0.12f),
                Concrete = LitMat("Concrete", lit, new Color(0.62f, 0.63f, 0.62f), 0.05f),
                Grass = LitMat("Grass", lit, new Color(0.30f, 0.48f, 0.24f), 0.0f),
                Paint = LitMat("Paint", lit, new Color(0.93f, 0.93f, 0.90f), 0.1f),
                Dark = LitMat("DarkMetal", lit, new Color(0.09f, 0.11f, 0.15f), 0.45f),
                BayFill = SaveMat("BayFill", Transparent(new Material(unlit))),
                Ring = SaveMat("Ring", Transparent(new Material(unlit) { mainTexture = RingTexture() })),
                Line = SaveMat("WaitArrow", Transparent(new Material(particles))),
                Cars = KitMat("KenneyCars", lit, $"{KenneyDir}/CarKit/Models/Textures/colormap.png", emissive: true),
                Roads = KitMat("KenneyRoads", lit, $"{KenneyDir}/CityRoads/Models/Textures/colormap.png", emissive: false),
                Buildings = KitMat("KenneyBuildings", lit, $"{KenneyDir}/CityCommercial/Models/Textures/colormap.png", emissive: false),
            };
            m.Line.SetColor("_BaseColor", new Color(1.6f, 1.6f, 1.6f, 1f)); // HDR boost so arrows catch a little bloom
            m.TeleportRing = SaveMat("TeleportRing", Transparent(new Material(unlit) { mainTexture = m.Ring.mainTexture }));
            m.TeleportRing.SetColor("_BaseColor", new Color(0.35f, 0.85f, 1f, 1f));
            var fade = Transparent(new Material(unlit));
            fade.SetFloat("_Cull", (float)CullMode.Off);          // seen from inside the sphere
            fade.SetFloat("_ZTest", (float)CompareFunction.Always); // over everything, even a panel at arm's length
            fade.renderQueue = (int)RenderQueue.Overlay;
            m.Fade = SaveMat("ScreenFade", fade);

            var sky = new Material(Shader.Find("Skybox/Procedural"));
            sky.SetFloat("_SunSize", 0.035f);
            sky.SetFloat("_AtmosphereThickness", 0.85f);
            sky.SetColor("_SkyTint", new Color(0.55f, 0.66f, 0.85f));
            sky.SetColor("_GroundColor", new Color(0.35f, 0.38f, 0.36f));
            sky.SetFloat("_Exposure", 1.15f);
            m.Sky = SaveMat("Sky", sky);
            return m;
        }

        static Material LitMat(string name, Shader shader, Color color, float smoothness)
        {
            var mat = new Material(shader);
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            return SaveMat(name, mat);
        }

        static Material KitMat(string name, Shader shader, string texturePath, bool emissive)
        {
            var mat = new Material(shader);
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0.25f);
            if (emissive)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", Color.black);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            return SaveMat(name, mat);
        }

        static Material Transparent(Material mat)
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            return mat;
        }

        static Material SaveMat(string name, Material mat)
        {
            var path = $"{GeneratedDir}/{name}.mat";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        /// <summary>Soft white ring with a faint fill, tinted per car at runtime.</summary>
        static Texture2D RingTexture()
        {
            const int size = 256;
            var path = $"{GeneratedDir}/Ring.png";
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float r = new Vector2(x - size / 2f + 0.5f, y - size / 2f + 0.5f).magnitude / (size / 2f);
                float ring = Mathf.Clamp01(1f - Mathf.Abs(r - 0.86f) / 0.09f);
                float fill = r < 0.86f ? 0.12f : 0f;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Max(ring * ring * (3f - 2f * ring), fill)));
            }
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // =====================================================================================================
        // Environment
        // =====================================================================================================

        static void BuildLighting()
        {
            var sun = new GameObject("Sun", typeof(Light));
            var light = sun.GetComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.95f, 0.86f);
            light.intensity = 1.35f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.75f;
            sun.transform.rotation = Quaternion.Euler(48f, -38f, 0f);

            RenderSettings.skybox = mats.Sky;
            RenderSettings.sun = light;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.70f, 0.82f);
            RenderSettings.ambientEquatorColor = new Color(0.50f, 0.52f, 0.54f);
            RenderSettings.ambientGroundColor = new Color(0.26f, 0.25f, 0.22f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.70f, 0.77f, 0.86f);
            RenderSettings.fogStartDistance = 70f;
            RenderSettings.fogEndDistance = 220f;

            var volume = new GameObject("Post Processing", typeof(Volume)).GetComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = PostProfile();
        }

        static VolumeProfile PostProfile()
        {
            var path = $"{GeneratedDir}/GridlockPost.asset";
            AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(0.8f);
            bloom.scatter.Override(0.6f);
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.Neutral);
            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.15f);
            color.saturation.Override(12f);
            color.contrast.Override(8f);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.18f);

            foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        static void BuildGround(Transform env)
        {
            var grass = Box("Grass", env, new Vector3(0f, -0.06f, 0f), new Vector3(200f, 0.1f, 200f), mats.Grass);
            Teleportable(grass);

            float lotW = LotMaxX - LotMinX, lotD = LotMaxZ - LotMinZ;
            var lot = Box("LotAsphalt", env, new Vector3(0f, -0.05f, (LotMinZ + LotMaxZ) / 2f), new Vector3(lotW, 0.1f, lotD), mats.Asphalt);
            Teleportable(lot);

            // Sidewalk ring with gaps for the entry (west) and exit (east) at the aisle.
            const float walk = 2f, gap = 7f, curb = 0.15f;
            float midZ = (LotMinZ + LotMaxZ) / 2f;
            Teleportable(Box("Sidewalk_N", env, new Vector3(0f, curb / 2f - 0.05f, LotMaxZ + walk / 2f), new Vector3(lotW + 2 * walk, curb + 0.1f, walk), mats.Concrete));
            Teleportable(Box("Sidewalk_S", env, new Vector3(0f, curb / 2f - 0.05f, LotMinZ - walk / 2f), new Vector3(lotW + 2 * walk, curb + 0.1f, walk), mats.Concrete));
            foreach (var (side, x) in new[] { ("W", LotMinX - walk / 2f), ("E", LotMaxX + walk / 2f) })
            {
                float lowLen = AisleZ - gap / 2f - LotMinZ, highLen = LotMaxZ - (AisleZ + gap / 2f);
                Teleportable(Box($"Sidewalk_{side}_S", env, new Vector3(x, curb / 2f - 0.05f, LotMinZ + lowLen / 2f), new Vector3(walk, curb + 0.1f, lowLen), mats.Concrete));
                Teleportable(Box($"Sidewalk_{side}_N", env, new Vector3(x, curb / 2f - 0.05f, LotMaxZ - highLen / 2f), new Vector3(walk, curb + 0.1f, highLen), mats.Concrete));
                Teleportable(Box($"Driveway_{side}", env, new Vector3(x + (side == "W" ? -1f : 1f), -0.05f, AisleZ), new Vector3(walk + 2f, 0.1f, gap), mats.Asphalt));
            }

            // Paint: aisle centre dashes and direction arrows, entry/exit words.
            var paint = new GameObject("Paint").transform;
            paint.SetParent(env);
            for (float x = LotMinX + 2f; x < LotMaxX - 1.5f; x += 2.4f)
                Box("AisleDash", paint, new Vector3(x + 0.6f, 0.005f, AisleZ), new Vector3(1.2f, 0.01f, 0.14f), mats.Paint);
            GroundText("ENTRY", paint, new Vector3(LotMinX + 1.6f, 0.02f, AisleZ - 2.3f), 10f, Color.white, 90f);
            GroundText("EXIT", paint, new Vector3(LotMaxX - 1.6f, 0.02f, AisleZ + 2.3f), 10f, Color.white, -90f);
        }

        static void BuildStreets(Transform env)
        {
            var road = Kenney("CityRoads", "road-straight");
            var root = new GameObject("Streets").transform;
            root.SetParent(env);
            foreach (var x in new[] { -StreetX, StreetX })
                for (float z = -60f; z <= 60f; z += KitScaleRoads)
                {
                    var tile = Place(road, root, new Vector3(x, 0f, z), 90f, KitScaleRoads, mats.Roads);
                    tile.name = "Road";
                    AddCollider(tile);
                    Teleportable(tile);
                }
        }

        static void BuildProps(Transform env)
        {
            var root = new GameObject("Props").transform;
            root.SetParent(env);

            var lamp = Kenney("CityRoads", "light-curved");
            foreach (var (x, z, yaw) in new[] { (LotMinX - 1f, LotMinZ - 0.8f, -90f), (LotMaxX + 1f, LotMinZ - 0.8f, 90f),
                                               (LotMinX - 1f, LotMaxZ + 0.8f, -90f), (LotMaxX + 1f, LotMaxZ + 0.8f, 90f) })
                Place(lamp, root, new Vector3(x, 0.1f, z), yaw, KitScaleRoads, mats.Roads);

            var cone = Kenney("CityRoads", "construction-cone");
            for (int i = 0; i < 4; i++)
                Place(cone, root, new Vector3(LotMaxX + 0.6f, 0.1f, AisleZ - 4.2f - i * 0.9f), i * 23f, KitScaleRoads, mats.Roads);

            Place(Kenney("CityRoads", "road-sign-stop"), root, new Vector3(LotMaxX + 1.2f, 0.1f, AisleZ + 4.1f), 90f, KitScaleRoads, mats.Roads);
            Place(Kenney("CityRoads", "road-sign-warning"), root, new Vector3(LotMinX - 1.2f, 0.1f, AisleZ - 4.1f), -90f, KitScaleRoads, mats.Roads);
            Place(Kenney("CityRoads", "dumpster"), root, new Vector3(LotMaxX + 0.9f, 0.1f, LotMaxZ - 2.5f), 90f, KitScaleRoads, mats.Roads);
            Place(Kenney("CityRoads", "construction-barrier"), root, new Vector3(LotMinX - 0.9f, 0.1f, LotMinZ + 1.5f), 0f, KitScaleRoads, mats.Roads);
        }

        /// <summary>
        /// Life around the lot: traffic driving the east street (the simulation never uses it), street lamps along both
        /// streets, traffic lights at the lot gates, a highway sign and a café terrace.
        /// </summary>
        static void BuildStreetLife(Transform env)
        {
            var root = new GameObject("StreetLife").transform;
            root.SetParent(env);

            // Traffic: two lanes on the east street, one each way.
            string[] models = { "sedan", "hatchback-sports", "delivery", "taxi", "suv", "ambulance", "van", "sedan" };
            var cars = new List<Transform>();
            var lanes = new List<float>();
            var dirs = new List<float>();
            for (int i = 0; i < models.Length; i++)
            {
                bool north = i % 2 == 0;
                float lane = north ? StreetX + 2f : StreetX - 2f;
                var car = DecorCar(models[i], root, new Vector3(lane, 0f, -55f + i * 14f), north ? 0f : 180f);
                cars.Add(car);
                lanes.Add(lane);
                dirs.Add(north ? 1f : -1f);
            }
            var traffic = new GameObject("Traffic").AddComponent<AmbientTraffic>();
            traffic.transform.SetParent(root);
            traffic.Init(cars.ToArray(), lanes.ToArray(), dirs.ToArray(), -62f, 62f);

            // Street lamps along the outer kerbs of both streets.
            var lamp = Kenney("CityRoads", "light-square-double");
            for (float z = -48f; z <= 48f; z += 16f)
                foreach (var (x, yaw) in new[] { (-StreetX - StreetWidth / 2f - 0.8f, 90f), (StreetX + StreetWidth / 2f + 0.8f, -90f) })
                    Place(lamp, root, new Vector3(x, 0f, z), yaw, KitScaleRoads, mats.Roads);

            // Traffic lights at the entry and exit driveways, and a sign pointing in.
            var signal = Kenney("CityRoads", "traffic-light");
            foreach (var (x, yaw) in new[] { (LotMinX - 2.6f, 90f), (LotMaxX + 2.6f, -90f) })
                foreach (var dz in new[] { -4.4f, 4.4f })
                    Place(signal, root, new Vector3(x, 0.1f, AisleZ + dz), yaw, KitScaleRoads, mats.Roads);
            Place(Kenney("CityRoads", "sign-highway"), root, new Vector3(-StreetX - StreetWidth / 2f - 0.6f, 0f, AisleZ + 14f), 0f, KitScaleRoads, mats.Roads);

            // Café terrace on the east pavement, across from the exit.
            var parasol = Kenney("CityCommercial", "detail-parasol-a");
            for (int i = 0; i < 4; i++)
                Place(parasol, root, new Vector3(StreetX + StreetWidth / 2f + 2.2f + (i % 2) * 2.6f, 0f, -8f - i * 3.2f), i * 40f, KitScaleRoads, mats.Buildings);
        }

        /// <summary>
        /// Kenney Nature Kit planting (models keep their own colour materials): a pocket park south of the lot behind the
        /// VR start point, a flower and hedge bed under the scoreboard, and street trees along the kerbs. Static-batched.
        /// </summary>
        static void BuildGreenery(Transform env)
        {
            var root = new GameObject("Greenery").transform;
            root.SetParent(env);
            var rng = new System.Random(21);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            string[] trees = { "tree_oak", "tree_default", "tree_detailed", "tree_fat", "tree_tall", "tree_pineRoundA", "tree_pineRoundC", "tree_cone" };
            string[] bushes = { "plant_bush", "plant_bushLarge", "plant_bushDetailed", "plant_bushSmall" };
            string[] flowers = { "flower_redA", "flower_yellowA", "flower_purpleA" };

            GameObject Plant(string model, Vector3 pos, float scale)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>($"{KenneyDir}/Nature/Models/{model}.fbx") ?? throw new FileNotFoundException(model), root);
                go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, R(0f, 360f), 0f));
                go.transform.localScale = Vector3.one * scale;
                foreach (var t in go.GetComponentsInChildren<Transform>())
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
                return go;
            }

            // Pocket park: a jittered grid of trees either side of a stone path, with a round plaza in the middle.
            float parkTop = LotMinZ - 4f, parkBottom = -46f, parkHalfW = StreetX - StreetWidth / 2f - 1.5f;
            for (float z = parkTop - 2f; z > parkBottom; z -= 6.5f)
                for (float x = -parkHalfW + 2f; x <= parkHalfW - 2f; x += 6f)
                {
                    if (Mathf.Abs(x) < 3f) continue; // keep the path clear
                    var p = new Vector3(x + R(-1.4f, 1.4f), 0f, z + R(-1.4f, 1.4f));
                    if (Vector2.Distance(new Vector2(p.x, p.z), new Vector2(0f, -26f)) < 6.5f) continue; // plaza
                    Plant(trees[rng.Next(trees.Length)], p, R(4f, 5.2f));
                    if (rng.Next(3) == 0) Plant(bushes[rng.Next(bushes.Length)], p + new Vector3(R(1.2f, 2f), 0f, R(-1f, 1f)), R(3f, 4.2f));
                }
            for (float z = parkTop; z > parkBottom; z -= 2.3f)
                Plant("path_stone", new Vector3(0f, 0.04f, z), 4f).transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            Plant("path_stoneCircle", new Vector3(0f, 0.05f, -26f), 9f);
            for (int i = 0; i < 10; i++)
            {
                float a = i * Mathf.PI * 2f / 10f;
                Plant(flowers[i % flowers.Length], new Vector3(Mathf.Cos(a) * 5.4f, 0f, -26f + Mathf.Sin(a) * 5.4f), R(4f, 5f));
            }

            // Hedge along the park's north edge, with a gap for the path.
            for (float x = -parkHalfW; x <= parkHalfW; x += 1.5f)
                if (Mathf.Abs(x) > 1.8f) Plant(bushes[rng.Next(bushes.Length)], new Vector3(x, 0f, parkTop + R(-0.2f, 0.2f)), R(3.4f, 4f));

            // Flower and hedge bed in front of the scoreboard.
            for (float x = -7f; x <= 7f; x += 1.4f)
            {
                Plant(bushes[rng.Next(bushes.Length)], new Vector3(x, 0f, LotMaxZ + 2.6f), R(3.2f, 3.8f));
                Plant(flowers[rng.Next(flowers.Length)], new Vector3(x + 0.7f, 0f, LotMaxZ + 1.9f), R(4f, 5f));
            }

            // Street trees on the inner kerbs, away from the lot gates.
            foreach (var x in new[] { -StreetX + StreetWidth / 2f + 0.9f, StreetX - StreetWidth / 2f - 0.9f })
                for (float z = -44f; z <= 44f; z += 11f)
                    if (z < LotMinZ - 3f || z > LotMaxZ + 3f)
                        Plant(trees[rng.Next(4)], new Vector3(x, 0f, z), R(3.6f, 4.2f));
        }

        /// <summary>A Kenney car normalised to life size (nose +Z, wheels on the ground) with no simulation parts.</summary>
        static Transform DecorCar(string model, Transform parent, Vector3 position, float yaw)
        {
            var car = new GameObject("Traffic_" + model).transform;
            car.SetParent(parent);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(Kenney("CarKit", model), car);
            AssignMaterial(instance, mats.Cars);
            var frontWheel = instance.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name.StartsWith("wheel-front"));
            float flip = frontWheel != null && frontWheel.localPosition.z < 0f ? 180f : 0f;
            var raw = Bounds(instance);
            instance.transform.localRotation = Quaternion.Euler(0f, flip, 0f);
            instance.transform.localScale = Vector3.one * (CarLength / Mathf.Max(raw.size.x, raw.size.z));
            var b = Bounds(instance);
            instance.transform.localPosition = new Vector3(-b.center.x, -b.min.y, -b.center.z);
            car.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            return car;
        }

        static void BuildBuildings(Transform env)
        {
            var root = new GameObject("Buildings").transform;
            root.SetParent(env);
            string[] near = { "building-a", "building-b", "building-c", "building-d", "building-e", "building-f", "building-g", "building-h" };
            string[] far = { "low-detail-building-a", "low-detail-building-b", "low-detail-building-c", "low-detail-building-d", "low-detail-building-e" };
            var rng = new System.Random(7);

            // Street-facing rows on both sides, a row behind the scoreboard, and a skyline further back.
            int n = 0;
            for (float z = -36f; z <= 36f; z += 12f, n++)
            {
                Place(Kenney("CityCommercial", near[n % near.Length]), root, new Vector3(-StreetX - 11f, 0f, z), 90f, KitScaleBuildings, mats.Buildings);
                Place(Kenney("CityCommercial", near[(n + 3) % near.Length]), root, new Vector3(StreetX + 11f, 0f, z), -90f, KitScaleBuildings, mats.Buildings);
            }
            for (float x = -6f; x <= 6f; x += 12f, n++)
                Place(Kenney("CityCommercial", near[n % near.Length]), root, new Vector3(x, 0f, LotMaxZ + 14f), 180f, KitScaleBuildings, mats.Buildings);
            for (int i = 0; i < 14; i++)
            {
                var pos = new Vector3(-70f + i * 10.5f + (float)rng.NextDouble() * 4f, 0f, 62f + (float)rng.NextDouble() * 18f);
                Place(Kenney("CityCommercial", far[i % far.Length]), root, pos, rng.Next(4) * 90f, KitScaleBuildings, mats.Buildings);
            }
        }

        // =====================================================================================================
        // Parking spaces and cars
        // =====================================================================================================

        static ParkingSpaceView BuildSpace(string id, int index, Transform parent, bool interactive)
        {
            int row = index / 3, col = index % 3;
            bool top = row == 0;
            var center = new Vector3((col - 1) * BayPitch, 0f, top ? TopRowZ : BottomRowZ);

            var go = new GameObject("ParkingSpace_" + id);
            go.transform.SetParent(parent);
            go.transform.SetPositionAndRotation(center, Quaternion.Euler(0f, top ? 180f : 0f, 0f)); // forward → aisle

            // Painted bay: side lines + back line.
            for (int s = -1; s <= 1; s += 2)
                Box("Line", go.transform, center + go.transform.right * (s * BayPitch / 2f) + Vector3.up * 0.005f,
                    new Vector3(0.14f, 0.01f, BayDepth), mats.Paint, keepWorld: true);
            Box("BackLine", go.transform, center - go.transform.forward * (BayDepth / 2f) + Vector3.up * 0.005f,
                new Vector3(BayPitch + 0.14f, 0.01f, 0.14f), mats.Paint, keepWorld: true);

            var fill = GameObject.CreatePrimitive(PrimitiveType.Quad);
            fill.name = "Fill";
            Object.DestroyImmediate(fill.GetComponent<Collider>());
            fill.transform.SetParent(go.transform, false);
            fill.transform.localPosition = new Vector3(0f, 0.012f, 0f);
            fill.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            fill.transform.localScale = new Vector3(BayWidth - 0.2f, BayDepth - 0.2f, 1f);
            var fillRenderer = fill.GetComponent<Renderer>();
            fillRenderer.sharedMaterial = mats.BayFill;
            fillRenderer.shadowCastingMode = ShadowCastingMode.Off;

            // Top row: label in the aisle. Bottom row: label behind the bay, nearest the player, so badges never cover it.
            var labelPos = top ? center + go.transform.forward * (BayDepth / 2f + 0.8f) : center - go.transform.forward * (BayDepth / 2f + 0.7f);
            var label = GroundText(id, go.transform, labelPos + Vector3.up * 0.02f, 10f, Color.white, 0f);

            if (interactive)
            {
                var col3 = go.AddComponent<BoxCollider>();
                col3.center = new Vector3(0f, 0.05f, 0f);
                col3.size = new Vector3(BayWidth, 0.1f, BayDepth);
                go.AddComponent<XRSimpleInteractable>();
                go.AddComponent<Inspectable>().Init(Inspectable.Kind.Space, id, null);
            }

            var view = go.AddComponent<ParkingSpaceView>();
            view.Init(id, fillRenderer, label, approach: Mathf.Abs(center.z - AisleZ));
            return view;
        }

        /// <summary>
        /// A Kenney car normalised to life size with identity ring, badge and engine sound. <paramref name="full"/> adds
        /// the deadlock-mode extras (status text, collider, point/select interaction).
        /// </summary>
        static CarView BuildCar(string id, string name, string model, Color color, Transform parent, Vector3 position, Quaternion rotation, bool full)
        {
            var car = new GameObject(name);
            car.transform.SetParent(parent);

            var prefab = Kenney("CarKit", model);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, car.transform);
            instance.name = "Model";
            AssignMaterial(instance, mats.Cars);

            // Normalise: length → CarLength, nose → +Z, centred on the root, wheels on the ground.
            var frontWheel = instance.transform.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name.StartsWith("wheel-front"));
            float yaw = frontWheel != null && frontWheel.localPosition.z < 0f ? 180f : 0f;
            var raw = Bounds(instance);
            float scale = CarLength / Mathf.Max(raw.size.x, raw.size.z);
            instance.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            instance.transform.localScale = Vector3.one * scale;
            var b = Bounds(instance);
            instance.transform.localPosition = new Vector3(-b.center.x, -b.min.y, -b.center.z);

            var wheels = instance.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("wheel")).ToArray();
            var bodies = instance.GetComponentsInChildren<Renderer>();
            foreach (var r in bodies) r.shadowCastingMode = ShadowCastingMode.On;

            var ring = GameObject.CreatePrimitive(PrimitiveType.Quad);
            ring.name = "Ring";
            Object.DestroyImmediate(ring.GetComponent<Collider>());
            ring.transform.SetParent(car.transform, false);
            ring.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            ring.transform.localScale = Vector3.one * 5.6f;
            var ringRenderer = ring.GetComponent<Renderer>();
            ringRenderer.sharedMaterial = mats.Ring;
            ringRenderer.shadowCastingMode = ShadowCastingMode.Off;

            var badge = WorldText("Badge", car.transform, new Vector3(0f, full ? 3.6f : 3.0f, 0f), id, full ? 16f : 11f, color, FontStyles.Bold);
            badge.outlineWidth = 0.22f;
            badge.outlineColor = new Color32(10, 14, 24, 255);

            TextMeshPro status = null;
            if (full)
            {
                status = WorldText("Status", car.transform, new Vector3(0f, 2.55f, 0f), "", 2.9f, ViewColor.Waiting, FontStyles.Bold);
                status.outlineWidth = 0.25f;
                status.outlineColor = new Color32(10, 14, 24, 255);

                var box = car.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, b.size.y / 2f, 0f);
                box.size = new Vector3(b.size.x, b.size.y, b.size.z);
                car.AddComponent<XRSimpleInteractable>();
                car.AddComponent<Inspectable>().Init(Inspectable.Kind.Car, id, null);
            }

            var engine = car.AddComponent<AudioSource>();
            engine.clip = Clip("engine_loop.wav");
            engine.loop = true;
            engine.playOnAwake = false;
            engine.spatialBlend = 1f;
            engine.dopplerLevel = 0f;
            engine.rolloffMode = AudioRolloffMode.Linear;
            engine.minDistance = 4f;
            engine.maxDistance = 45f;

            var view = car.AddComponent<CarView>();
            view.Init(id, color, bodies, ringRenderer, badge, status, wheels, engine);
            car.transform.SetPositionAndRotation(position, rotation);
            return view;
        }

        // =====================================================================================================
        // Analyze-mode templates
        // =====================================================================================================

        static WaitArrow BuildArrowTemplate(Transform parent)
        {
            var go = new GameObject("WaitArrowTemplate");
            go.transform.SetParent(parent);
            var line = LineChild("Line", go.transform);
            var head = LineChild("Head", go.transform);
            var label = WorldText("Label", go.transform, Vector3.zero, "", 4.5f, Color.white, FontStyles.Bold);
            label.outlineWidth = 0.25f;
            label.outlineColor = new Color32(10, 14, 24, 255);
            var arrow = go.AddComponent<WaitArrow>();
            arrow.Init(line, head, label);
            go.SetActive(false);
            return arrow;
        }

        static LineRenderer LineChild(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = mats.Line;
            lr.useWorldSpace = true;
            lr.numCapVertices = 4;
            lr.numCornerVertices = 2;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            return lr;
        }

        static GameObject BuildCardTemplate(Transform parent)
        {
            var canvas = WorldCanvas("CardTemplate", parent, new Vector2(600, 300), 0.0042f);
            canvas.AddComponent<Billboard>();
            Bordered("Background", canvas.transform, 0, 0, 600, 300, Opaque(ViewColor.Panel), Amber);
            var text = UIText("Text", canvas.transform, 30, 18, 540, 264, "", 46, ViewColor.Text, TextAlignmentOptions.TopLeft);
            text.lineSpacing = 6f;
            canvas.SetActive(false);
            return canvas;
        }

        // =====================================================================================================
        // Scoreboard (desktop + overview) and podium (VR start point)
        // =====================================================================================================

        static void BuildScoreboard(SimulationManager sim, AnalysisOverlay overlay, PlayerInteraction interaction)
        {
            var canvas = BoardCanvas("Deadlock Detection\n<size=80%>OS Module 4 · Resource Allocation</size>", "BANKER MODE", BankerSceneName, out var status);

            UIText("LogTitle", canvas.transform, 50, 205, 1100, 60, "EVENT LOG", 40, Muted, TextAlignmentOptions.Left, FontStyles.Bold).characterSpacing = 6f;
            var log = UIText("Log", canvas.transform, 50, 270, 1100, 530, "", 40, ViewColor.Text, TextAlignmentOptions.TopLeft);
            log.lineSpacing = 10f;

            var legend = $"<color={ViewColor.Hex(Amber)}><b>{ViewColor.BigArrows("→")}</b></color> waits for a space held by      "
                         + $"<color={ViewColor.Hex(ViewColor.Deadlock)}><b>{ViewColor.BigArrows("→")}</b></color> part of a circular wait\n"
                         + "Car = process      Parking space = resource      Ring = car identity";
            UIText("Legend", canvas.transform, 50, 790, 1100, 110, legend, 32, Muted, TextAlignmentOptions.TopLeft);
            var boardHint = UIText("Hint", canvas.transform, 50, 915, 1100, 100, "", 38, Amber, TextAlignmentOptions.TopLeft, FontStyles.Bold);

            var (analyze, analyzeLabel) = UIButton("AnalyzeButton", canvas.transform, 50, 1040, 350, 150, "ANALYZE", 56, Amber, new Color(0.06f, 0.08f, 0.12f));
            var (tow, towLabel) = UIButton("TowButton", canvas.transform, 420, 1040, 380, 150, "TOW CAR", 56, ViewColor.Deadlock, Color.white);
            var (restart, _) = UIButton("RestartButton", canvas.transform, 820, 1040, 300, 150, "RESTART", 50, new Color(0.22f, 0.27f, 0.38f), ViewColor.Text);

            // Right column: idle explanation, covered by the alert / analysis panels when they appear.
            var intro = UIText("Intro", canvas.transform, 1230, 215, 1110, 900,
                "<b>HOW IT WORKS</b>\n\nEach car is a <b>process</b>, each bay a <b>resource</b> that fits one car.\n\n"
                + "A car keeps its bay until the next one is free. If that bay is taken it must <b>wait</b>.\n\n"
                + "If the waits close into a loop, nobody can ever move: <color=#EF4444><b>deadlock</b></color>.\n\n"
                + "<b>YOUR TURN</b>  Select a car, then a bay. Can you build a deadlock and break it?",
                40, Muted, TextAlignmentOptions.TopLeft);
            intro.lineSpacing = 8f;

            var alertPanel = Bordered("DeadlockAlert", canvas.transform, 1220, 200, 1130, 340, new Color(0.45f, 0.04f, 0.06f), ViewColor.Deadlock);
            var alertTitle = UIText("Title", alertPanel.transform, 30, 24, 1070, 110, "DEADLOCK DETECTED", 84, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            alertTitle.textWrappingMode = TextWrappingModes.NoWrap;
            var alertDetail = UIText("Detail", alertPanel.transform, 30, 150, 1070, 170, "", 44, Color.white, TextAlignmentOptions.Top);

            var analysisPanel = Bordered("AnalysisPanel", canvas.transform, 1220, 560, 1130, 650, new Color(0.09f, 0.13f, 0.22f), Amber);
            var analysisText = UIText("Text", analysisPanel.transform, 34, 26, 1062, 600, "", 34, ViewColor.Text, TextAlignmentOptions.TopLeft);
            analysisText.enableAutoSizing = true;
            analysisText.fontSizeMin = 22f;
            analysisText.fontSizeMax = 34f;

            canvas.AddComponent<DeadlockAlert>().Init(sim, alertPanel, alertTitle, alertDetail, intro.gameObject);
            canvas.AddComponent<StatusBoard>().Init(sim, overlay, status, log, analysisPanel, analysisText);
            canvas.AddComponent<ControlPanel>().Init(sim, overlay, interaction, analyze, analyzeLabel, tow, towLabel, restart, boardHint);
        }

        static void BuildPodium(SimulationManager sim, AnalysisOverlay overlay, PlayerInteraction interaction)
        {
            var canvas = PodiumCanvas("LOT CONTROL", "BANKER MODE", BankerSceneName);
            var hint = UIText("Hint", canvas.transform, 30, 100, 760, 180,
                "Point at a car or bay to inspect it. Trigger or pinch to select.\nSelect a car, then a bay, to send it there.\nLook at a controller to see its buttons.", 30, Muted, TextAlignmentOptions.TopLeft);

            var alertPanel = Bordered("DeadlockAlert", canvas.transform, 20, 92, 780, 190, new Color(0.45f, 0.04f, 0.06f), ViewColor.Deadlock);
            var alertTitle = UIText("Title", alertPanel.transform, 16, 10, 748, 70, "DEADLOCK DETECTED", 54, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            var alertDetail = UIText("Detail", alertPanel.transform, 16, 80, 748, 100, "", 28, Color.white, TextAlignmentOptions.Top);

            var (analyze, analyzeLabel) = UIButton("AnalyzeButton", canvas.transform, 20, 300, 250, 110, "ANALYZE", 40, Amber, new Color(0.06f, 0.08f, 0.12f));
            var (tow, towLabel) = UIButton("TowButton", canvas.transform, 285, 300, 250, 110, "TOW CAR", 40, ViewColor.Deadlock, Color.white);
            var (restart, _) = UIButton("RestartButton", canvas.transform, 550, 300, 250, 110, "RESTART", 38, new Color(0.22f, 0.27f, 0.38f), ViewColor.Text);
            var podiumHint = UIText("Hint", canvas.transform, 30, 425, 760, 100, "", 28, Amber, TextAlignmentOptions.TopLeft, FontStyles.Bold);

            canvas.AddComponent<DeadlockAlert>().Init(sim, alertPanel, alertTitle, alertDetail, hint.gameObject);
            canvas.AddComponent<ControlPanel>().Init(sim, overlay, interaction, analyze, analyzeLabel, tow, towLabel, restart, podiumHint);
        }

        static Vector3 PlayerStart => new Vector3(0f, 0f, LotMinZ - 1.4f);

        /// <summary>The big world-space board above the north edge: frame, header with title, subtitle, mode switch and a right-aligned status.</summary>
        static GameObject BoardCanvas(string subtitle, string modeLabel, string modeScene, out TextMeshProUGUI status)
        {
            const float scale = 0.0065f;
            var boardRoot = new GameObject("Scoreboard").transform;
            var center = new Vector3(0f, 7.7f, LotMaxZ + 2.6f);
            foreach (var x in new[] { -6.5f, 6.5f })
                Box("Post", boardRoot, new Vector3(x, 1.85f, center.z + 0.35f), new Vector3(0.45f, 3.7f, 0.45f), mats.Dark);
            Box("Backing", boardRoot, center + new Vector3(0f, 0f, 0.25f), new Vector3(2400 * scale + 0.4f, 1250 * scale + 0.4f, 0.3f), mats.Dark);

            var canvas = WorldCanvas("BoardCanvas", boardRoot, new Vector2(2400, 1250), scale);
            canvas.transform.position = center;

            Panel("Background", canvas.transform, 0, 0, 2400, 1250, ViewColor.Panel);
            Panel("Header", canvas.transform, 0, 0, 2400, 170, new Color(0.09f, 0.13f, 0.22f));
            UIText("Title", canvas.transform, 50, 18, 760, 140, "OS PLAYS", 118, Amber, TextAlignmentOptions.Left, FontStyles.Bold).characterSpacing = 8f;
            UIText("Subtitle", canvas.transform, 860, 30, 560, 120, subtitle, 40, Muted, TextAlignmentOptions.Left);
            ModeButton(canvas.transform, 1430, 45, 330, 80, modeLabel, 36, modeScene);
            status = UIText("Status", canvas.transform, 1790, 22, 570, 130, "", 64, ViewColor.Text, TextAlignmentOptions.Right, FontStyles.Bold);
            status.enableAutoSizing = true;
            status.textWrappingMode = TextWrappingModes.NoWrap;
            status.fontSizeMin = 30f;
            status.fontSizeMax = 64f;
            return canvas;
        }

        /// <summary>Lectern beside the VR start point (820 × 540 px canvas, tilted towards the player) with title and mode switch.</summary>
        static GameObject PodiumCanvas(string title, string modeLabel, string modeScene)
        {
            var canvas = Lectern("ControlPodium", 1.25f);
            Bordered("Background", canvas.transform, 0, 0, 820, 540, Opaque(ViewColor.Panel), Amber);
            UIText("Title", canvas.transform, 30, 18, 520, 70, title, 46, Amber, TextAlignmentOptions.Left, FontStyles.Bold).characterSpacing = 4f;
            ModeButton(canvas.transform, 580, 22, 220, 60, modeLabel, 26, modeScene);
            return canvas;
        }

        /// <summary>Stand with an 820 × 540 px canvas tilted towards the VR start point, <paramref name="side"/> metres to its right.</summary>
        static GameObject Lectern(string name, float side)
        {
            var root = new GameObject(name).transform;
            var player = PlayerStart;
            var pos = player + new Vector3(side, 0f, 0.9f);
            var yaw = Quaternion.LookRotation(pos - player).eulerAngles.y;
            root.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));

            var stand = Box("Stand", root, pos + Vector3.up * 0.42f + root.forward * 0.16f, new Vector3(0.45f, 0.84f, 0.3f), mats.Dark);
            stand.transform.rotation = root.rotation;

            var canvas = WorldCanvas(name + "Canvas", root, new Vector2(820, 540), 0.001f);
            canvas.transform.SetPositionAndRotation(pos + Vector3.up * 1.12f, Quaternion.Euler(35f, yaw, 0f));
            return canvas;
        }

        /// <summary>Quest controls cheat sheet on a lectern left of the VR start point. <paramref name="faceButtons"/> is per mode.</summary>
        // =====================================================================================================
        // XR rig + desktop camera
        // =====================================================================================================

        const string HandsRigPath = XRIDir + "/Hands Interaction Demo/Prefabs/XR Origin Hands (XR Rig).prefab";

        /// <summary>
        /// Desktop camera + HUD, and the XR rig (controllers and tracked hands, switched automatically) with GRIDLOCK's
        /// own XR controls: face-button shortcuts, a wrist menu and hand-tracking teleport. <paramref name="buildXRControls"/>
        /// fills the wrist menu canvas and wires the shortcuts for the scene's mode.
        /// </summary>
        static XRModeController BuildPresentation(Action<Transform> buildHud, Action<Transform, XRButtonShortcuts, XROrigin> buildXRControls)
            => BuildPresentation(buildHud, buildXRControls, PlayerStart, Quaternion.identity, seated: false);

        /// <param name="buildXRControls">Null for no GRIDLOCK XR controls (wrist menu, shortcuts, hand teleport).</param>
        /// <param name="seated">Keep the player where they are: only snap turn is left, no walking or teleporting.</param>
        static XRModeController BuildPresentation(Action<Transform> buildHud, Action<Transform, XRButtonShortcuts, XROrigin> buildXRControls,
                                                  Vector3 start, Quaternion facing, bool seated)
        {
            var desktop = new GameObject("Desktop Camera", typeof(Camera), typeof(AudioListener), typeof(DesktopCameraController));
            desktop.tag = "MainCamera";
            var cam = desktop.GetComponent<Camera>();
            cam.fieldOfView = 50f;
            cam.farClipPlane = 400f;
            ConfigureCamera(cam);
            desktop.transform.SetPositionAndRotation(new Vector3(0f, 26f, -31f), Quaternion.Euler(38f, 0f, 0f));
            buildHud(desktop.transform);
            desktop.SetActive(false);

            var rig = BuildXRRig(start, facing, seated, buildXRControls);
            var simulator = AssetDatabase.LoadAssetAtPath<GameObject>($"{XRIDir}/XR Interaction Simulator/XR Interaction Simulator.prefab");
            var mode = new GameObject("XR Mode").AddComponent<XRModeController>();
            mode.Init(rig, desktop, simulator);
            return mode;
        }

        /// <summary>
        /// The XR rig (controllers and tracked hands, switched automatically), inactive until <see cref="XRModeController"/>
        /// picks the headset: floor tracking, no jump (A is a shortcut), animated hands on the controllers, and GRIDLOCK's own
        /// XR controls (<paramref name="buildXRControls"/>; null for just the ≡ menu shortcut).
        /// </summary>
        static GameObject BuildXRRig(Vector3 start, Quaternion facing, bool seated, Action<Transform, XRButtonShortcuts, XROrigin> buildXRControls)
        {
            var rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HandsRigPath)
                            ?? throw new FileNotFoundException($"Missing {HandsRigPath} (XRI Hands Interaction Demo sample)");
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab);
            rig.transform.SetPositionAndRotation(start, facing);
            foreach (var c in rig.GetComponentsInChildren<Camera>(true)) { c.farClipPlane = 400f; ConfigureCamera(c); }
            var origin = rig.GetComponent<XROrigin>();
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor; // Quest guardian floor, real height
            // A is a GRIDLOCK shortcut, not jump.
            foreach (var jump in rig.GetComponentsInChildren<JumpProvider>(true)) jump.gameObject.SetActive(false);
            BuildControllerHands(rig);
            TunePinchAndRays(rig);
            rig.AddComponent<ComfortTurn>().Init(origin);
            if (seated) MakeSeated(rig);
            if (buildXRControls != null) BuildXRControls(rig, buildXRControls);
            else BuildMenuShortcut(rig);
            rig.SetActive(false);
            return rig;
        }

        const string HandsDir = "Assets/Scenarios/MemoryManagement/VR_Hands";
        const string InputActionsPath = XRIDir + "/Starter Assets/XRI Default Input Actions.inputactions";

        /// <summary>
        /// Hands instead of plastic controllers (the memory warehouse's animated hands, now in every scene): the controller
        /// model is hidden and a hand follows the controller, its fingers curling with the grip and the trigger.
        /// </summary>
        static void BuildControllerHands(GameObject rig)
        {
            var actions = AssetDatabase.LoadAllAssetsAtPath(InputActionsPath).OfType<UnityEngine.InputSystem.InputActionReference>().ToArray();
            UnityEngine.InputSystem.InputActionReference Action(string name) =>
                actions.FirstOrDefault(a => a.name == name) ?? throw new InvalidOperationException($"No input action '{name}' in {InputActionsPath}");
            var all = rig.GetComponentsInChildren<Transform>(true);
            foreach (var (side, prefabName, roll) in new[] { ("Left", "LeftHandPrefab", 90f), ("Right", "RightHandPrefab", -90f) })
            {
                var controller = all.FirstOrDefault(t => t.name == side + " Controller") ?? throw new InvalidOperationException($"XR rig has no '{side} Controller'");
                var visual = all.FirstOrDefault(t => t.name == side + " Controller Visual");
                if (visual != null) visual.gameObject.SetActive(false);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{HandsDir}/{prefabName}.prefab")
                             ?? throw new FileNotFoundException($"Missing {HandsDir}/{prefabName}.prefab (Memory Management hands)");
                var hand = (GameObject)PrefabUtility.InstantiatePrefab(prefab, controller);
                PrefabUtility.UnpackPrefabInstance(hand, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                hand.name = side + " Hand Model";
                hand.transform.localPosition = new Vector3(0f, -0.1f, 0f);
                hand.transform.localRotation = Quaternion.Euler(0f, 0f, roll);
                hand.transform.localScale = Vector3.one;
                // The memory scene's own scripts and interactors on the hand are replaced by the rig's.
                foreach (var b in hand.GetComponentsInChildren<MonoBehaviour>(true).Where(b => b == null || b.GetType().Name is "HandAnimation" or "XRInteractorLineVisual" || b is XRBaseInteractor).ToArray())
                    if (b != null) Object.DestroyImmediate(b);
                foreach (var line in hand.GetComponentsInChildren<LineRenderer>(true).ToArray()) Object.DestroyImmediate(line);
                foreach (var col in hand.GetComponentsInChildren<Collider>(true).ToArray()) Object.DestroyImmediate(col); // never push what it holds
                foreach (var body in hand.GetComponentsInChildren<Rigidbody>(true).ToArray()) Object.DestroyImmediate(body);
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(hand);
                foreach (var r in hand.GetComponentsInChildren<Renderer>(true)) { r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; }
                // The memory scene set up the animator on its hand instances, not on the prefabs.
                var animator = hand.GetComponent<Animator>();
                if (animator == null) animator = hand.AddComponent<Animator>();
                animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>($"{HandsDir}/{side}HandController.controller")
                                                     ?? throw new FileNotFoundException($"Missing {HandsDir}/{side}HandController.controller");
                animator.applyRootMotion = false;
                // Culling by the skinned bounds could freeze the fingers mid-curl right in front of the eyes.
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.gameObject.AddComponent<ControllerHandAnimator>().Init(
                    Action($"XRI {side} Interaction/Select Value"), Action($"XRI {side} Interaction/Activate Value"));

                // On the grip pose: fingers wrap the handle axis (grip +Z), palm towards the other hand, the palm centre
                // (the model's grip bone) 3 cm out from the handle's centre line. Measured by closing the hand on a 4 cm
                // cylinder along the grip axis.
                bool right = side == "Right";
                var inGrip = Quaternion.LookRotation(Vector3.down, right ? Vector3.right : Vector3.left);
                var palm = hand.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.EndsWith("_grip"))
                           ?? throw new InvalidOperationException($"{side} hand model has no grip bone");
                var palmInHand = hand.transform.InverseTransformPoint(palm.position);
                hand.AddComponent<ControllerGripPose>().Init(right, new Vector3(right ? 0.03f : -0.03f, 0f, 0f) - inGrip * palmInHand, inGrip);

                // Held things sit in this hand: the controller's grab point is the palm of the hand model (which follows
                // the grip pose), not the ray's origin at the tip of the controller, several centimetres ahead and above.
                var palmAttach = new GameObject("Palm Attach").transform;
                palmAttach.SetParent(hand.transform, false);
                palmAttach.localPosition = palmInHand + new Vector3(0f, -0.035f, 0f); // just off the palm's surface
                palmAttach.rotation = controller.rotation;
                // (The near-far interactor's attach point is driven by its attach controller, which follows this anchor.)
                var attachController = controller.GetComponentInChildren<UnityEngine.XR.Interaction.Toolkit.Attachment.InteractionAttachController>(true);
                if (attachController != null) attachController.transformToFollow = palmAttach;
                else
                {
                    var grabber = controller.GetComponentInChildren<NearFarInteractor>(true);
                    if (grabber != null) grabber.attachTransform = palmAttach;
                }
            }
        }

        /// <summary>
        /// Hand pinch and pointing rays. The hands demo registers a pinch only at 100 % pinch strength and drops it below
        /// 90 %; Quest's pinch strength is noisy and often peaks just under 1, so pinches were missed and pinch-drags let
        /// go on their own. A pinch now starts at 80 % and holds down to 50 %. Every far ray (hands and controllers) is
        /// stabilised so the small hand movement of a pinch or trigger pull does not knock it off a button.
        /// </summary>
        static void TunePinchAndRays(GameObject rig)
        {
            foreach (var reader in rig.GetComponentsInChildren<MonoBehaviour>(true).Where(b => b != null && b.GetType().Name == "ReleaseThresholdButtonReader"))
            {
                var so = new SerializedObject(reader);
                so.FindProperty("m_PressThreshold").floatValue = 0.8f;
                so.FindProperty("m_ReleaseThreshold").floatValue = 0.5f;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            foreach (var nearFar in rig.GetComponentsInChildren<NearFarInteractor>(true))
            {
                if (nearFar.farInteractionCaster is not UnityEngine.XR.Interaction.Toolkit.Interactors.Casters.InteractionCasterBase caster) continue;
                caster.enableStabilization = true;
                caster.positionStabilization = 0.25f;
                caster.angleStabilization = 20f;
                caster.aimTarget = nearFar;
                EditorUtility.SetDirty(caster);
            }
        }

        /// <summary>Removes walking, teleporting, climbing and grab-move from the rig; snap turn stays.</summary>
        static void MakeSeated(GameObject rig)
        {
            foreach (var t in rig.GetComponentsInChildren<Transform>(true))
                if (t.name is "Move" or "Teleportation" or "Grab Move" or "Climb" or "Climb Teleport" or "Gravity" or "Teleport Interactor")
                    t.gameObject.SetActive(false);
        }

        static Transform DesktopCameraTransform() =>
            Object.FindObjectsByType<DesktopCameraController>(FindObjectsInactive.Include, FindObjectsSortMode.None).First().transform;

        /// <summary>Rigs without GRIDLOCK XR controls (the seated hub) still open the system menu with Y or ≡, as everywhere.</summary>
        static void BuildMenuShortcut(GameObject rig)
        {
            var controls = new GameObject("GRIDLOCK XR Controls");
            controls.transform.SetParent(rig.transform, false);
            var keys = controls.AddComponent<XRButtonShortcuts>();
            var link = controls.AddComponent<SystemMenuLink>();
            UnityEditor.Events.UnityEventTools.AddPersistentListener(keys.ButtonY, link.Toggle);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(keys.MenuButton, link.Toggle);
        }

        static void BuildXRControls(GameObject rig, Action<Transform, XRButtonShortcuts, XROrigin> fill)
        {
            var origin = rig.GetComponent<XROrigin>();
            var all = rig.GetComponentsInChildren<Transform>(true);
            Transform Find(string n) => all.FirstOrDefault(t => t.name == n) ?? throw new InvalidOperationException($"XR rig has no '{n}'");
            var leftController = Find("Left Controller");
            var handRoots = new[] { Find("Left Hand"), Find("Right Hand") };
            var handRays = handRoots.SelectMany(h => h.GetComponentsInChildren<NearFarInteractor>(true)).ToArray();
            var teleportation = rig.GetComponentInChildren<TeleportationProvider>(true)
                                ?? throw new InvalidOperationException("XR rig has no TeleportationProvider");

            var controls = new GameObject("GRIDLOCK XR Controls");
            controls.transform.SetParent(rig.transform, false);
            var keys = controls.AddComponent<XRButtonShortcuts>();

            // Landing ring for hand teleport (a flat quad under a yaw-free root).
            var reticle = new GameObject("HandTeleportReticle");
            reticle.transform.SetParent(controls.transform, false);
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Ring";
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(reticle.transform, false);
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            quad.transform.localScale = Vector3.one * 0.9f;
            var quadRenderer = quad.GetComponent<Renderer>();
            quadRenderer.sharedMaterial = mats.TeleportRing;
            quadRenderer.shadowCastingMode = ShadowCastingMode.Off;
            controls.AddComponent<HandTeleport>().Init(handRays, teleportation, reticle.transform, origin);

            // One menu for everything: Y and ≡ both open the game menu. Hands have no buttons, so turning the left palm
            // towards you shows a small wrist panel whose MENU button opens the same menu (plus a scene extra, if any).
            var wrist = new GameObject("Wrist Menu");
            wrist.transform.SetParent(controls.transform, false);
            var panel = WorldCanvas("WristMenuCanvas", wrist.transform, new Vector2(WristW, WristH), 0.0004f); // 24 cm wide
            var link = controls.AddComponent<SystemMenuLink>();
            UnityEditor.Events.UnityEventTools.AddPersistentListener(keys.ButtonY, link.Toggle);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(keys.MenuButton, link.Toggle);
            MenuLinkButton(panel.transform, 20, 20, WristW - 40, 100, 40);

            int before = panel.transform.childCount;
            fill(panel.transform, keys, origin); // may add one row at y 140
            float height = panel.transform.childCount > before ? WristH : 140f;
            ((RectTransform)panel.transform).sizeDelta = new Vector2(WristW, height);
            Bordered("Background", panel.transform, 0, 0, WristW, height, Opaque(ViewColor.Panel), Amber).transform.SetAsFirstSibling();
            wrist.AddComponent<WristMenu>().Init(origin, leftController, panel);
            BuildControllerHints(keys, Find("Left Controller"), Find("Right Controller"));
        }

        /// <summary>Names for the face-button actions on the hint cards, keyed by the wired method.</summary>
        static readonly Dictionary<string, string> ButtonActionNames = new Dictionary<string, string>
        {
            { "Toggle", "Analyze" }, { "TowSelected", "Tow car" }, { "Restart", "Restart" },
            { "Grant", "Grant" }, { "Deny", "Deny" }, { "Next", "Next" },
        };

        /// <summary>
        /// A cheat card on each controller listing this scene's buttons (read from the shortcut wiring, so it can't
        /// drift from what the buttons really do) plus the stick, trigger and grip.
        /// </summary>
        static void BuildControllerHints(XRButtonShortcuts keys, Transform left, Transform right)
        {
            string Action(UnityEngine.Events.UnityEvent e)
            {
                if (e.GetPersistentEventCount() == 0) return null;
                string method = e.GetPersistentMethodName(0);
                if (e.GetPersistentTarget(0) is ScenarioButtons scenario) return scenario.Label(method); // imported scenarios
                return ButtonActionNames.TryGetValue(method, out var n) ? n : null;
            }
            float hold = new SerializedObject(keys).FindProperty("holdXSeconds").floatValue;
            string Line(string button, string action) => action == null ? null : $"<color=#FBBF24><b>{button}</b></color>  {action}";

            string a = Action(keys.ButtonA), b = Action(keys.ButtonB), x = Action(keys.ButtonX);
            var rightLines = new[]
            {
                Line("A", a), Line("B", b),
                Line("Stick", "◄ ► turn,  push up: teleport"), Line("Trigger", "select / press"), Line("Grip", "grab"),
            };
            var leftLines = new[]
            {
                Line("X", x == null ? null : hold > 0f ? $"hold: {x}" : x), Line("Y  ≡", "menu"),
                Line("Stick", "walk"), Line("Trigger", "select / press"),
            };
            HintCard(left, "LeftHints", leftLines.Where(l => l != null).ToArray());
            HintCard(right, "RightHints", rightLines.Where(l => l != null).ToArray());
        }

        static void HintCard(Transform controller, string name, string[] lines)
        {
            const float w = 300f, lineH = 34f, pad = 12f;
            float h = lines.Length * lineH + 2 * pad;
            // A bare canvas (no raycasters): the card must never catch the controller's own ray.
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            go.transform.SetParent(controller, false);
            go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(w, h);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.localScale = Vector3.one * 0.0004f; // 12 cm wide
            var group = go.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = group.blocksRaycasts = false;
            var bg = Bordered("Background", go.transform, 0, 0, w, h, new Color(0.05f, 0.08f, 0.14f, 0.88f), new Color(0.35f, 0.85f, 1f, 0.7f));
            foreach (var g in bg.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
            for (int i = 0; i < lines.Length; i++)
                UIText($"Line{i}", go.transform, pad + 4f, pad + i * lineH, w - 2 * pad, lineH, lines[i], 22, Color.white, TextAlignmentOptions.Left);
            controller.gameObject.AddComponent<ControllerHints>().Init(controller, group);
        }

        const float WristW = 600f, WristH = 260f;

        /// <summary>
        /// Screen-space side panel for desktop mode: the world scoreboard is too far away to read from the overview
        /// camera, so the same components drive a docked HUD. It lives under the desktop camera and is never shown in XR.
        /// </summary>
        const float HudWidth = 520f, HudX = 20f;

        static void BuildDesktopHud(Transform desktopCamera, SimulationManager sim, AnalysisOverlay overlay, PlayerInteraction interaction)
        {
            var (hud, dockT) = DesktopDock(desktopCamera, "Deadlock Detection · OS Module 4", "BANKER MODE", BankerSceneName);
            const float w = HudWidth, x = HudX;

            var status = UIText("Status", dockT, x + 22f, 130f, w - 44f, 80f, "", 34, ViewColor.Text, TextAlignmentOptions.TopLeft, FontStyles.Bold);

            var intro = UIText("Intro", dockT, x + 22f, 220f, w - 44f, 170f,
                "Cars are <b>processes</b>, bays are <b>resources</b>. A car that holds a bay and asks for a taken one must " +
                "<b>wait</b>. If the waits form a loop, no car can ever move: <color=#EF4444><b>deadlock</b></color>.",
                21, Muted, TextAlignmentOptions.TopLeft);
            var alert = Bordered("DeadlockAlert", dockT, x + 16f, 220f, w - 32f, 170f, new Color(0.45f, 0.04f, 0.06f), ViewColor.Deadlock);
            var alertTitle = UIText("Title", alert.transform, 12f, 14f, w - 56f, 56f, "DEADLOCK DETECTED", 40, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            alertTitle.textWrappingMode = TextWrappingModes.NoWrap;
            alertTitle.enableAutoSizing = true;
            alertTitle.fontSizeMin = 24f;
            alertTitle.fontSizeMax = 40f;
            var alertDetail = UIText("Detail", alert.transform, 12f, 74f, w - 56f, 90f, "", 22, Color.white, TextAlignmentOptions.Top);

            UIText("LogTitle", dockT, x + 22f, 408f, w - 44f, 30f, "EVENT LOG", 20, Muted, TextAlignmentOptions.Left, FontStyles.Bold).characterSpacing = 4f;
            var log = UIText("Log", dockT, x + 22f, 440f, w - 44f, 214f, "", 20, ViewColor.Text, TextAlignmentOptions.TopLeft);

            var analysis = Bordered("AnalysisPanel", dockT, x + 16f, 660f, w - 32f, 236f, new Color(0.09f, 0.13f, 0.22f), Amber);
            var analysisText = UIText("Text", analysis.transform, 16f, 10f, w - 64f, 216f, "", 19, ViewColor.Text, TextAlignmentOptions.TopLeft);
            analysisText.enableAutoSizing = true;
            analysisText.fontSizeMin = 12f;
            analysisText.fontSizeMax = 19f;

            var hudHint = UIText("Hint", dockT, x + 22f, 902f, w - 44f, 56f, "", 19, Amber, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            float bw = (w - 32f - 20f) / 3f;
            var (analyze, analyzeLabel) = UIButton("AnalyzeButton", dockT, x + 16f, 966f, bw, 76f, "ANALYZE", 26, Amber, new Color(0.06f, 0.08f, 0.12f));
            var (tow, towLabel) = UIButton("TowButton", dockT, x + 26f + bw, 966f, bw, 76f, "TOW CAR", 26, ViewColor.Deadlock, Color.white);
            var (restart, _) = UIButton("RestartButton", dockT, x + 36f + 2 * bw, 966f, bw, 76f, "RESTART", 26, new Color(0.22f, 0.27f, 0.38f), ViewColor.Text);

            HelpBar(hud.transform, "Click a car, then a bay    Right-drag  orbit    Scroll  zoom    Hover  inspect",
                    "Tap a car, then a bay    Drag  orbit    Pinch  zoom    Two fingers  pan");

            hud.AddComponent<DeadlockAlert>().Init(sim, alert, alertTitle, alertDetail, intro.gameObject);
            hud.AddComponent<StatusBoard>().Init(sim, overlay, status, log, analysis, analysisText);
            hud.AddComponent<ControlPanel>().Init(sim, overlay, interaction, analyze, analyzeLabel, tow, towLabel, restart, hudHint);
        }

        /// <summary>
        /// Screen-space canvas with a right-docked panel holding the title row (GRIDLOCK + mode switch) and subtitle.
        /// Returns the canvas and the dock to fill (coordinates: x from <see cref="HudX"/>, content from y = 130).
        /// </summary>
        static (GameObject hud, Transform dock) DesktopDock(Transform desktopCamera, string subtitle, string modeLabel, string modeScene)
        {
            const float w = HudWidth, x = HudX;
            var hud = new GameObject("Desktop HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            hud.transform.SetParent(desktopCamera, false);
            hud.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = hud.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;

            // Anchor everything to the right edge so it stays docked on any aspect ratio.
            var dock = new GameObject("Dock", typeof(RectTransform));
            var drt = (RectTransform)dock.transform;
            drt.SetParent(hud.transform, false);
            drt.anchorMin = new Vector2(1f, 0f);
            drt.anchorMax = new Vector2(1f, 1f);
            drt.pivot = new Vector2(1f, 1f);
            drt.sizeDelta = new Vector2(w + 40f, 0f);
            drt.anchoredPosition = Vector2.zero;
            var dockT = dock.transform;

            Bordered("Panel", dockT, x - 4f, 16f, w + 8f, 1048f, Opaque(ViewColor.Panel), new Color(0.2f, 0.25f, 0.36f));
            UIText("Title", dockT, x + 22f, 32f, 300f, 60f, "OS PLAYS", 46, Amber, TextAlignmentOptions.Left, FontStyles.Bold).characterSpacing = 3f;
            ModeButton(dockT, x + w - 200f, 38f, 180f, 52f, modeLabel, 22, modeScene);
            UIText("Subtitle", dockT, x + 24f, 92f, w - 40f, 30f, subtitle, 20, Muted, TextAlignmentOptions.Left);
            ModeButton(hud.transform, 16f, 16f, 170f, 52f, "◄ ROOM", 22, HubSceneName); // back to the hub, top-left
            return (hud, dockT);
        }

        /// <param name="touchText">The same help in phone terms (drag, pinch, tap), swapped in on touch screens.</param>
        static void HelpBar(Transform hud, string text, string touchText, float width = 900f)
        {
            MenuLinkButton(hud, 12f, 1026f, 150f, 48f, 22);
            Panel("HelpBackdrop", hud, 172f, 1030f, width, 42f, new Color(0.06f, 0.09f, 0.16f, 0.8f));
            var help = UIText("Help", hud, 188f, 1036f, width - 20f, 30f, text, 20, new Color(1f, 1f, 1f, 0.85f), TextAlignmentOptions.Left);
            help.textWrappingMode = TextWrappingModes.NoWrap;
            help.fontStyle = FontStyles.Bold;
            help.gameObject.AddComponent<TouchVariant>().Init(TouchVariant.Show.Always, touchText);
        }

        /// <summary>Button that loads the other GRIDLOCK mode.</summary>
        static void ModeButton(Transform parent, float x, float y, float w, float h, string label, float size, string scene)
        {
            var (button, _) = UIButton("ModeButton", parent, x, y, w, h, label, size, new Color(0.16f, 0.21f, 0.32f), ViewColor.Text);
            button.gameObject.AddComponent<ModeSwitch>().Init(scene);
        }

        static void ConfigureCamera(Camera cam)
        {
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        }

        // =====================================================================================================
        // Helpers
        // =====================================================================================================

        static AudioClip Clip(string file) =>
            AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Audio/{file}")
            ?? throw new FileNotFoundException($"Missing audio clip {file}");

        static GameObject Kenney(string kit, string model) =>
            AssetDatabase.LoadAssetAtPath<GameObject>($"{KenneyDir}/{kit}/Models/{model}.fbx")
            ?? throw new FileNotFoundException($"Missing Kenney model {kit}/{model}");

        static GameObject Place(GameObject prefab, Transform parent, Vector3 pos, float yaw, float scale, Material mat)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = Vector3.one * scale;
            AssignMaterial(go, mat);
            return go;
        }

        static void AssignMaterial(GameObject go, Material mat)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>())
                r.sharedMaterials = Enumerable.Repeat(mat, r.sharedMaterials.Length).ToArray();
        }

        static void AddCollider(GameObject go)
        {
            var b = Bounds(go);
            var box = go.AddComponent<BoxCollider>();
            box.center = go.transform.InverseTransformPoint(b.center);
            var s = go.transform.lossyScale;
            box.size = new Vector3(b.size.x / s.x, Mathf.Max(b.size.y, 0.1f) / s.y, b.size.z / s.z);
        }

        static Bounds Bounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        static void Teleportable(GameObject go)
        {
            var area = go.AddComponent<TeleportationArea>();
            area.interactionLayers = InteractionLayerMask.GetMask("Teleport");
            area.filterSelectionByHitNormal = true; // floors only, never a wall or the gallery's edge
            area.upNormalToleranceDegrees = 35f;
        }

        /// <summary>
        /// Every teleport target gets the rig's teleportation provider directly. Left to find it at runtime, a target that
        /// woke before XRModeController switched the rig on drew its reticle against a provider with no mediator yet
        /// (XRI NullReferenceException in GetReticleDirection for the first frames in the headset).
        /// </summary>
        static void WireTeleportTargets()
        {
            var provider = Object.FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationProvider>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            if (provider == null) return;
            var mediator = provider.GetComponentInParent<UnityEngine.XR.Interaction.Toolkit.Locomotion.LocomotionMediator>(true);
            if (mediator != null && provider.mediator == null)
            {
                provider.mediator = mediator;
                EditorUtility.SetDirty(provider);
            }
            foreach (var target in Object.FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.BaseTeleportationInteractable>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                target.teleportationProvider = provider;
                EditorUtility.SetDirty(target);
            }
        }

        static Transform Marker(string name, Transform parent, Vector3 pos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent);
            t.position = pos;
            return t;
        }

        static GameObject Box(string name, Transform parent, Vector3 position, Vector3 scale, Material mat, bool keepWorld = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, keepWorld);
            go.transform.position = position;
            if (keepWorld) go.transform.rotation = parent.rotation;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (scale.y < 0.05f) Object.DestroyImmediate(go.GetComponent<Collider>()); // paint has no collider
            return go;
        }

        /// <summary>World-space TMP text that faces the camera.</summary>
        static TextMeshPro WorldText(string name, Transform parent, Vector3 localPos, string text, float size, Color color, FontStyles style)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.font = font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.rectTransform.sizeDelta = new Vector2(8f, 2f);
            go.AddComponent<Billboard>();
            return tmp;
        }

        /// <summary>Text painted flat on the ground, readable from the south.</summary>
        static TextMeshPro GroundText(string text, Transform parent, Vector3 pos, float size, Color color, float yaw)
        {
            var go = new GameObject("Text_" + text);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(90f, yaw, 0f));
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.font = font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.rectTransform.sizeDelta = new Vector2(10f, 2f);
            return tmp;
        }

        static GameObject WorldCanvas(string name, Transform parent, Vector2 size, float scale)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                                    typeof(GraphicRaycaster), typeof(TrackedDeviceGraphicRaycaster));
            go.transform.SetParent(parent, false);
            go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            go.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 3f;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = size;
            rt.localScale = Vector3.one * scale;
            return go;
        }

        /// <summary>Rect anchored to the parent's top-left, in pixels with y growing downwards.</summary>
        static RectTransform Rect(GameObject go, Transform parent, float x, float y, float w, float h)
        {
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        static GameObject Panel(string name, Transform parent, float x, float y, float w, float h, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            Rect(go, parent, x, y, w, h);
            var img = go.GetComponent<Image>();
            img.sprite = roundedSprite;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 0.25f; // larger corner radius on big panels
            img.color = color;
            return go;
        }

        /// <summary>Opaque panel inside a thin coloured border (a translucent panel would let the border tint show through).</summary>
        static GameObject Bordered(string name, Transform parent, float x, float y, float w, float h, Color fill, Color border)
        {
            const float t = 5f;
            var frame = Panel(name, parent, x, y, w, h, border);
            var inner = Panel("Fill", frame.transform, t, t, w - 2 * t, h - 2 * t, fill);
            inner.transform.SetAsFirstSibling();
            return frame;
        }

        static Color Opaque(Color c) => new Color(c.r, c.g, c.b, 1f);

        static TextMeshProUGUI UIText(string name, Transform parent, float x, float y, float w, float h, string text, float size,
                                      Color color, TextAlignmentOptions align, FontStyles style = FontStyles.Normal)
        {
            var go = new GameObject(name, typeof(RectTransform));
            Rect(go, parent, x, y, w, h);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.font = font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.fontStyle = style;
            tmp.raycastTarget = false;
            return tmp;
        }

        static (Button, TMP_Text) UIButton(string name, Transform parent, float x, float y, float w, float h, string label,
                                           float size, Color background, Color textColor)
        {
            var go = Panel(name, parent, x, y, w, h, background);
            var button = go.AddComponent<Button>();
            var colors = button.colors;
            // Hover must read clearly at arm's length in a headset (the ray's dot sits behind on-top menus).
            colors.highlightedColor = new Color(1.6f, 1.6f, 1.6f);
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f);
            button.colors = colors;
            var text = UIText("Label", go.transform, 16, 0, w - 32, h, label, size, textColor, TextAlignmentOptions.Center, FontStyles.Bold);
            text.enableAutoSizing = true;
            text.fontSizeMin = size * 0.5f;
            text.fontSizeMax = size;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return (button, text);
        }

        static void AddToBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes
                .Where(s => s.path != ScenePath && s.path != BankerScenePath && s.path != HubScenePath && s.path != CpuScenePath &&
                            s.path != MemoryScenePath && !s.path.StartsWith("Assets/Scenarios/") && File.Exists(s.path)) // drops template leftovers
                .ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(HubScenePath, true)); // the app starts in the hub room
            scenes.Insert(1, new EditorBuildSettingsScene(ScenePath, true));
            scenes.Insert(2, new EditorBuildSettingsScene(BankerScenePath, true));
            scenes.Insert(3, new EditorBuildSettingsScene(CpuScenePath, true));
            scenes.Insert(4, new EditorBuildSettingsScene(MemoryScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
