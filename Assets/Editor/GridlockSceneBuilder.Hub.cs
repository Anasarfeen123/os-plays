using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gridlock.AR;
using Gridlock.Core;
using Gridlock.Hub;
using Gridlock.UI;
using Gridlock.XR;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Gridlock.Editor
{
    /// <summary>
    /// GridlockHub.unity, the app's start scene: a sci-fi control room. The player sits in a chair at a desk with a keyboard
    /// and a big monitor that offers three simulations (Deadlock now, two placeholders). Choosing Deadlock pulls the
    /// player into the screen and loads GridlockParkingLot. Outside the window are a ringed planet, stars and passing ships.
    /// </summary>
    public static partial class GridlockSceneBuilder
    {
        const string HubScenePath = "Assets/Scenes/GridlockHub.unity";
        const string HubSceneName = "GridlockHub";
        const string HubThumbPath = "Assets/UI/Hub/DeadlockThumb.png";

        static readonly Color Cyan = new Color(0.35f, 0.85f, 1f);
        static readonly Color NeonCyan = new Color(0.45f, 1.7f, 2.4f);   // HDR: blooms on desktop, still bright on Quest
        static readonly Color NeonAmber = new Color(2.2f, 1.4f, 0.25f);
        static readonly Color NeonRed = new Color(2.2f, 0.25f, 0.3f);

        // Chair, desk and monitor (metres). The player faces +Z towards the monitor and the window.
        static readonly Vector3 ChairPos = new Vector3(0f, 0f, -0.95f);
        static readonly Vector3 MonitorPos = new Vector3(0f, 1.24f, 0.45f);
        const float MonitorScale = 0.00085f; // 1920 × 1080 px → 1.63 × 0.92 m
        // Room: Kenney Space Station tiles at ×2.4 (2.4 m per tile, walls two tiles high). Bounds are the inner wall faces.
        const float KitTile = 2.4f, PropScale = 1.8f;
        const float RoomMinX = -8.4f, RoomMaxX = 8.4f, RoomMinZ = -7.2f, RoomMaxZ = 7.2f, RoomH = 2 * KitTile;
        const float GalleryZ = RoomMinZ + KitTile, GalleryH = 0.3f * KitTile; // raised gallery along the back wall

        class HubMats
        {
            public Material Station, Wall, Floor, Metal, Seat, KeyCap, Neon, Glow, Screen, Stars, Planet, PlanetRing, Moon, Beam, Warp, WarpRing, Ship;
        }

        static HubMats hub;

        static void BuildHubScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            hub = CreateHubMaterials();
            shipIndex = 0;
            BuildHubLighting();
            var fader = BuildSystems(arriveSound: false);
            var faderSo = new SerializedObject(fader);
            faderSo.FindProperty("color").colorValue = Color.black; // the hub wakes from darkness; the launch whites out
            faderSo.ApplyModifiedPropertiesWithoutUndo();

            var room = new GameObject("Room").transform;
            BuildHubShell(room);
            BuildSpaceView(room);
            BuildDesk(room, out var keyboard, out var deskKeys);
            BuildChair(room);
            BuildHoloProjector(room);
            BuildServerRacks(room);
            BuildWallDisplay(room);
            BuildDrone(room);
            BuildHubCollision(room);
            BuildHubPlaques(room);
            var seat = BuildSeatTarget(room);

            var ambience = new GameObject("Ambience", typeof(AudioSource)).GetComponent<AudioSource>();
            ambience.clip = Clip("Hub/hub_ambience.wav");
            ambience.loop = true;
            ambience.playOnAwake = true;
            ambience.volume = 0.55f;
            ambience.spatialBlend = 0f;

            // Free to roam: walk, teleport and turn in XR (start at the desk); the wrist menu has SIT AT DESK.
            var mode = BuildPresentation(BuildHubHud, (menu, keys, xrOrigin) => BuildHubWristMenu(menu),
                                         ChairPos, Quaternion.identity, seated: false);
            // Desktop: start in the chair (slight mouse look); R or STAND UP to walk around.
            var orbit = Object.FindObjectsByType<DesktopCameraController>(FindObjectsInactive.Include, FindObjectsSortMode.None).First();
            var desktop = orbit.transform;
            Object.DestroyImmediate(orbit);
            var eye = ChairPos + new Vector3(0f, 1.2f, 0.05f);
            desktop.SetPositionAndRotation(eye, Quaternion.LookRotation(MonitorPos + Vector3.down * 0.3f - eye)); // monitor and keypad in view
            desktop.GetComponent<Camera>().fieldOfView = 62f;

            var origin = Object.FindObjectsByType<Unity.XR.CoreUtils.XROrigin>(FindObjectsInactive.Include, FindObjectsSortMode.None).First();
            var screen = BuildMonitor(room, mode, origin, desktop, fader, keyboard);
            foreach (var key in deskKeys) key.SetScreen(screen);
            var desktopInput = keyboard.gameObject.AddComponent<HubDesktopInput>();
            desktopInput.Init(deskKeys.ToArray());
            BuildTouchKeypad(desktop.Find("Desktop HUD"), desktopInput);
            BuildRoomARRig(mode, desktop.Find("Desktop HUD"), new ARDioramaSettings
            {
                subject = "control room",
                placedHint = "Tap a card on the big screen, tap it again to enter",
                helpText = "Tap a scenario card    Pinch  resize    Twist  rotate    Drag  move",
                bounds = new Bounds(new Vector3(0f, 2.6f, 0f), new Vector3(18.4f, 6.2f, 16f)),
                scale = 26f,
                scaleRange = new Vector2(8f, 60f),
                ceilingCut = 4.5f,
                cutawayBand = 1f,
            }, new Rect(RoomMinX - 0.4f, RoomMinZ - 0.4f, RoomMaxX - RoomMinX + 0.8f, RoomMaxZ - RoomMinZ + 0.8f), 0f, "CONTROL ROOM", Cyan);
            var roamer = BuildDesktopRoamer(desktop, desktopInput);
            seat.Init(mode, origin, roamer, room.Find("Chair/StandPoint"));
            BuildAudioSystems("music_hub");
            BuildSystemMenu(mode, desktop, "CONTROL ROOM", isHome: true, tour: null, restart: null, HubHowTo);

            EditorSceneManager.SaveScene(scene, HubScenePath);
            Debug.Log($"[GRIDLOCK] Built {HubScenePath}");
        }

        static void NoCollider(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
        }

        // =====================================================================================================
        // Materials and generated textures
        // =====================================================================================================

        static HubMats CreateHubMaterials()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            var particles = Shader.Find("Universal Render Pipeline/Particles/Unlit");

            Material Unlit(string name, Color color, Texture tex = null, bool transparent = false, CullMode cull = CullMode.Back)
            {
                var m = new Material(unlit);
                if (transparent) Transparent(m);
                if (tex != null) m.SetTexture("_BaseMap", tex);
                m.SetColor("_BaseColor", color);
                m.SetFloat("_Cull", (float)cull);
                return SaveMat(name, m);
            }

            Material Additive(string name, Color color, Texture tex)
            {
                var m = Transparent(new Material(particles));
                m.SetFloat("_Blend", 2f); // additive
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.One);
                if (tex != null) m.SetTexture("_BaseMap", tex);
                m.SetColor("_BaseColor", color);
                return SaveMat(name, m);
            }

            var planetLit = new Material(lit);
            planetLit.SetTexture("_BaseMap", PlanetTexture());
            planetLit.SetColor("_BaseColor", Color.white);
            planetLit.SetFloat("_Smoothness", 0.15f);

            return new HubMats
            {
                Station = KitMat("KenneyStation", lit, $"{KenneyDir}/SpaceStation/Models/Textures/colormap.png", emissive: false),
                Wall = LitMat("HubWall", lit, new Color(0.13f, 0.15f, 0.2f), 0.55f),
                Floor = LitMat("HubFloor", lit, new Color(0.05f, 0.06f, 0.08f), 0.8f),
                Metal = LitMat("HubMetal", lit, new Color(0.2f, 0.23f, 0.3f), 0.7f),
                Seat = LitMat("HubSeat", lit, new Color(0.08f, 0.09f, 0.12f), 0.35f),
                KeyCap = Unlit("HubKeyCap", new Color(0.06f, 0.08f, 0.12f)),
                Neon = Unlit("HubNeon", NeonCyan),
                Glow = Unlit("HubGlow", Color.white, mats.Ring.mainTexture, transparent: true),
                Screen = Unlit("HubScreenGlow", new Color(0.03f, 0.08f, 0.14f)),
                Stars = Unlit("HubStars", Color.white, StarfieldTexture(), cull: CullMode.Front),
                Planet = SaveMat("HubPlanet", planetLit),
                PlanetRing = Unlit("HubPlanetRing", new Color(0.9f, 0.75f, 0.55f, 0.55f), mats.Ring.mainTexture, transparent: true, cull: CullMode.Off),
                Moon = LitMat("HubMoon", lit, new Color(0.55f, 0.55f, 0.6f), 0.05f),
                Beam = Unlit("HubBeam", new Color(0.35f, 0.85f, 1f, 0.07f), transparent: true, cull: CullMode.Off),
                Warp = Additive("HubWarpStreak", new Color(0.6f, 1.6f, 2.4f, 1f), null),
                WarpRing = Additive("HubWarpRing", new Color(0.5f, 1.4f, 2.4f, 1f), mats.Ring.mainTexture),
                Ship = LitMat("HubShip", lit, new Color(0.7f, 0.72f, 0.78f), 0.6f),
            };
        }

        /// <summary>Equirectangular starfield with a few coloured nebula clouds.</summary>
        static Texture2D StarfieldTexture()
        {
            const int w = 2048, h = 1024;
            var path = $"{GeneratedDir}/Starfield.png";
            var px = new Color32[w * h];
            var rng = new System.Random(11);
            var clouds = Enumerable.Range(0, 6).Select(_ => (x: rng.Next(w), y: h / 4 + rng.Next(h / 2), r: 120 + rng.Next(260),
                c: rng.Next(2) == 0 ? new Color(0.35f, 0.15f, 0.55f) : new Color(0.1f, 0.25f, 0.5f))).ToArray();
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var col = new Color(0.005f, 0.008f, 0.02f);
                foreach (var cl in clouds)
                {
                    float dx = Mathf.Min(Mathf.Abs(x - cl.x), w - Mathf.Abs(x - cl.x)), dy = y - cl.y;
                    float n = Mathf.PerlinNoise(x * 0.008f, y * 0.008f);
                    col += cl.c * (Mathf.Exp(-(dx * dx + dy * dy) / (2f * cl.r * cl.r)) * 0.35f * n);
                }
                px[y * w + x] = col;
            }
            for (int i = 0; i < 5200; i++)
            {
                int x = rng.Next(w), y = rng.Next(h);
                float b = Mathf.Pow((float)rng.NextDouble(), 3f);
                var tint = rng.Next(5) switch { 0 => new Color(0.7f, 0.8f, 1f), 1 => new Color(1f, 0.9f, 0.75f), _ => Color.white };
                var star = tint * (0.35f + 0.65f * b);
                px[y * w + x] = star;
                if (b > 0.75f) // a few big ones get a soft cross
                    foreach (var (ox, oy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                        px[Mathf.Clamp(y + oy, 0, h - 1) * w + (x + ox + w) % w] = star * 0.45f;
            }
            return SaveTexture(path, w, h, px, TextureWrapMode.Repeat);
        }

        /// <summary>Gas giant bands with turbulence.</summary>
        static Texture2D PlanetTexture()
        {
            const int w = 1024, h = 512;
            var px = new Color32[w * h];
            Color[] palette = { new Color(0.85f, 0.6f, 0.4f), new Color(0.62f, 0.38f, 0.28f), new Color(0.95f, 0.85f, 0.7f), new Color(0.5f, 0.42f, 0.5f) };
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float lat = (float)y / h;
                float turb = Mathf.PerlinNoise(x * 0.01f, y * 0.05f) * 0.08f + Mathf.PerlinNoise(x * 0.04f, y * 0.2f) * 0.02f;
                float band = Mathf.Repeat((lat + turb) * 9f, palette.Length);
                int i = (int)band;
                var c = Color.Lerp(palette[i], palette[(i + 1) % palette.Length], Mathf.SmoothStep(0f, 1f, band - i));
                px[y * w + x] = c;
            }
            // The great storm.
            for (int y = -26; y <= 26; y++)
            for (int x = -60; x <= 60; x++)
            {
                float d = x * x / 3600f + y * y / 676f;
                if (d > 1f) continue;
                int ix = (300 + x + w) % w, iy = 200 + y;
                px[iy * w + ix] = Color.Lerp(px[iy * w + ix], new Color(0.75f, 0.3f, 0.2f), (1f - d) * 0.8f);
            }
            return SaveTexture($"{GeneratedDir}/Planet.png", w, h, px, TextureWrapMode.Repeat);
        }

        static Texture2D SaveTexture(string path, int w, int h, Color32[] px, TextureWrapMode wrap)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = wrap;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // =====================================================================================================
        // Lighting and room shell
        // =====================================================================================================

        static void BuildHubLighting()
        {
            var sun = new GameObject("Starlight", typeof(Light));
            var light = sun.GetComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.85f, 0.9f, 1f);
            light.intensity = 1.1f;
            light.shadows = LightShadows.None; // the interior is lit by its neon; shadows would cost Quest frame time
            sun.transform.rotation = Quaternion.Euler(20f, 50f, 0f); // from behind the player's left: the planet shows a lit limb

            RenderSettings.skybox = null;
            RenderSettings.sun = light;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            // Bright cool ambient: Quest's mobile renderer drops extra lights, so ambient is what lights the walls that
            // face away from the starlight.
            RenderSettings.ambientSkyColor = new Color(0.42f, 0.46f, 0.6f);
            RenderSettings.ambientEquatorColor = new Color(0.3f, 0.33f, 0.45f);
            RenderSettings.ambientGroundColor = new Color(0.12f, 0.12f, 0.17f);
            RenderSettings.fog = false;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;

            var volume = new GameObject("Post Processing", typeof(Volume)).GetComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>($"{GeneratedDir}/GridlockPost.asset") ?? PostProfile();
        }

        static GameObject Neon(string name, Transform parent, Vector3 pos, Vector3 size, Color color, float pulseFreq = 0.2f, float phase = 0f)
        {
            var go = Box(name, parent, pos, size, hub.Neon);
            NoCollider(go);
            go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            go.AddComponent<HubPulse>().Init(color, 0.7f, 1f, pulseFreq, phase);
            return go;
        }

        /// <summary>A Space Station kit piece, static-batched (the room never moves; one draw call per material on Quest).</summary>
        static GameObject Kit(string model, Transform parent, Vector3 pos, float yaw, float scale = KitTile)
        {
            var go = Place(Kenney("SpaceStation", model), parent, pos, yaw, scale, hub.Station);
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            foreach (Transform t in go.GetComponentsInChildren<Transform>()) GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
            return go;
        }

        static void BuildHubShell(Transform room)
        {
            var shell = new GameObject("Shell").transform;
            shell.SetParent(room);
            const float half = 0.3f * KitTile / 2f; // half a wall tile's thickness: wall centres sit just outside the bounds
            int cols = Mathf.RoundToInt((RoomMaxX - RoomMinX) / KitTile), rows = Mathf.RoundToInt((RoomMaxZ - RoomMinZ) / KitTile);

            // Floor: plain plates with detail and panel strips down the middle aisle.
            for (int i = 0; i < cols; i++)
            for (int j = 0; j < rows; j++)
            {
                float x = RoomMinX + KitTile * (i + 0.5f), z = RoomMinZ + KitTile * (j + 0.5f);
                string model = i == cols / 2 ? "floor-panel-straight" : (i + j) % 5 == 0 ? "floor-detail" : "floor";
                Kit(model, shell, new Vector3(x, -0.3f * KitTile, z), i == cols / 2 ? 90f : 0f);
            }

            // Walls, two rows. Front: windows either side of a panoramic opening (three tiles) onto the planet.
            for (int i = 0; i < cols; i++)
            {
                float x = RoomMinX + KitTile * (i + 0.5f);
                bool panorama = Mathf.Abs(x) < KitTile * 1.5f;
                if (!panorama)
                {
                    Kit(i % 2 == 0 ? "wall-window" : "wall-window-banner", shell, new Vector3(x, 0f, RoomMaxZ + half), 180f);
                    Kit("wall", shell, new Vector3(x, KitTile, RoomMaxZ + half), 180f);
                }
                Kit(i == cols / 2 ? "wall-pillar-banner" : i % 3 == 0 ? "wall-pillar" : i % 2 == 0 ? "wall-banner" : "wall", shell,
                    new Vector3(x, 0f, RoomMinZ - half), 0f);
                Kit("wall", shell, new Vector3(x, KitTile, RoomMinZ - half), 0f);
            }
            for (int j = 0; j < rows; j++)
            {
                float z = RoomMinZ + KitTile * (j + 0.5f);
                bool door = j == rows - 2;
                Kit(door ? "wall-door-wide" : j % 2 == 0 ? "wall-banner" : "wall", shell, new Vector3(RoomMinX - half, 0f, z), 90f);
                if (door) Kit("door-double-closed", shell, new Vector3(RoomMinX - 0.05f, 0f, z), 90f);
                Kit(j % 3 == 1 ? "wall-window" : j % 2 == 0 ? "wall-banner" : "wall", shell, new Vector3(RoomMaxX + half, 0f, z), -90f);
                Kit("wall", shell, new Vector3(RoomMinX - half, KitTile, z), 90f);
                Kit("wall", shell, new Vector3(RoomMaxX + half, KitTile, z), -90f);
            }
            foreach (var (ex, ez) in new[] { (RoomMinX, RoomMinZ), (RoomMaxX, RoomMinZ), (RoomMinX, RoomMaxZ), (RoomMaxX, RoomMaxZ) })
                for (int r = 0; r < 2; r++)
                    Kit("wall-corner", shell, new Vector3(ex + Mathf.Sign(ex) * half, r * KitTile, ez + Mathf.Sign(ez) * half), 0f);

            // Panoramic window: sill, header, mullions and a neon frame.
            const float winB = 0.9f;
            float winL = -KitTile * 1.5f, winR = KitTile * 1.5f, fz = RoomMaxZ + half;
            Box("Sill", shell, new Vector3(0f, winB / 2f, fz), new Vector3(winR - winL, winB, 0.3f * KitTile), hub.Metal);
            Box("Header", shell, new Vector3(0f, RoomH - 0.3f, fz), new Vector3(winR - winL, 0.6f, 0.3f * KitTile), hub.Metal);
            foreach (var x in new[] { -KitTile / 2f, KitTile / 2f })
                Box("Mullion", shell, new Vector3(x, RoomH / 2f, fz), new Vector3(0.1f, RoomH, 0.14f), hub.Metal);
            Neon("WindowNeon_B", shell, new Vector3(0f, winB, RoomMaxZ - 0.02f), new Vector3(winR - winL, 0.03f, 0.03f), NeonCyan);
            Neon("WindowNeon_T", shell, new Vector3(0f, RoomH - 0.6f, RoomMaxZ - 0.02f), new Vector3(winR - winL, 0.03f, 0.03f), NeonCyan);
            foreach (var x in new[] { winL, winR })
                Neon("WindowNeon_Side", shell, new Vector3(x, (winB + RoomH - 0.6f) / 2f, RoomMaxZ - 0.02f), new Vector3(0.03f, RoomH - 0.6f - winB, 0.03f), NeonCyan);

            // Ceiling with a grid of light panels and neon trim.
            float w = RoomMaxX - RoomMinX, d = RoomMaxZ - RoomMinZ, cz = (RoomMinZ + RoomMaxZ) / 2f;
            Box("Ceiling", shell, new Vector3(0f, RoomH + 0.05f, cz), new Vector3(w + 1.5f, 0.1f, d + 1.5f), hub.Wall);
            foreach (var x in new[] { -5.4f, 0f, 5.4f })
            foreach (var z in new[] { -4.2f, 0f, 4.2f })
            {
                var panel = Neon("CeilingLight", shell, new Vector3(x, RoomH - 0.01f, z), new Vector3(2.6f, 0.02f, 0.4f), new Color(1.1f, 1.25f, 1.4f));
                panel.GetComponent<HubPulse>().Init(new Color(1.1f, 1.25f, 1.4f), 0.85f, 1f, 0.05f, x + z);
            }
            int n = 0;
            foreach (var y in new[] { 0.02f, RoomH - 0.04f })
            {
                Neon("Trim_W", shell, new Vector3(RoomMinX + 0.02f, y, cz), new Vector3(0.03f, 0.03f, d), NeonCyan, 0.15f, n++ * 0.25f);
                Neon("Trim_E", shell, new Vector3(RoomMaxX - 0.02f, y, cz), new Vector3(0.03f, 0.03f, d), NeonCyan, 0.15f, n++ * 0.25f);
            }
            var halo = Quad("CeilingHalo", shell, new Vector3(ChairPos.x, RoomH - 0.05f, ChairPos.z), Quaternion.Euler(-90f, 0f, 0f), 2.6f, hub.Glow);
            halo.AddComponent<HubPulse>().Init(new Color(0.35f, 0.85f, 1f, 0.9f), 0.5f, 1f, 0.12f, 0f);
            halo.AddComponent<HubSpin>().Init(new Vector3(0f, 0f, 8f));
            var ring = Quad("FloorRing", shell, new Vector3(ChairPos.x, 0.005f, ChairPos.z), Quaternion.Euler(90f, 0f, 0f), 2.6f, hub.Glow);
            ring.AddComponent<HubPulse>().Init(new Color(0.35f, 0.85f, 1f, 0.8f), 0.4f, 1f, 0.2f, 0.5f);
            ring.AddComponent<HubSpin>().Init(new Vector3(0f, 0f, -6f));

            BuildGallery(shell);
            BuildLab(shell);
            BuildEngineering(shell);
            BuildLounge(shell);
        }

        /// <summary>
        /// A Kenney Furniture pack model normalised to life size: that older pack has arbitrary pivots and units, so the
        /// model is scaled to <paramref name="size"/> (its height, or its widest side when <paramref name="byWidth"/>),
        /// centred and stood on the floor at <paramref name="pos"/>. Static-batched.
        /// </summary>
        static GameObject Furnish(string model, Transform parent, Vector3 pos, float yaw, float size, bool byWidth = false) =>
            Normalized("Furniture", model, parent, pos, yaw, size, byWidth, isStatic: true);

        /// <summary>Kenney Space Kit model (same arbitrary pivots as the Furniture pack), normalised like <see cref="Furnish"/>.</summary>
        static GameObject SpaceModel(string model, Transform parent, Vector3 pos, float yaw, float size, bool byWidth = false, bool isStatic = true) =>
            Normalized("Space", model, parent, pos, yaw, size, byWidth, isStatic);

        static GameObject Normalized(string kit, string model, Transform parent, Vector3 pos, float yaw, float size, bool byWidth, bool isStatic)
        {
            var root = new GameObject(model).transform;
            root.SetParent(parent);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>($"{KenneyDir}/{kit}/Models/{model}.fbx") ?? throw new FileNotFoundException(model), root);
            var b = Bounds(inst);
            float current = byWidth ? Mathf.Max(b.size.x, b.size.z) : b.size.y;
            inst.transform.localScale *= size / current;
            b = Bounds(inst);
            inst.transform.position -= new Vector3(b.center.x, b.min.y, b.center.z);
            root.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            if (isStatic)
                foreach (var t in root.GetComponentsInChildren<Transform>()) GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
            return root.gameObject;
        }

        /// <summary>Observation lounge facing the window, plants and speakers by the panorama, a bookcase, desk clutter.</summary>
        static void BuildLounge(Transform shell)
        {
            var l = new GameObject("Lounge").transform;
            l.SetParent(shell);
            var c = new Vector3(-5.6f, 0f, 4.9f);
            Furnish("rugRound", l, c + new Vector3(0f, 0.01f, 0.2f), 0f, 3.4f, byWidth: true);
            Furnish("loungeSofaLong", l, c + new Vector3(0f, 0f, -0.6f), 0f, 0.85f); // faces the panorama
            Furnish("loungeSofaOttoman", l, c + new Vector3(0f, 0f, 0.7f), 0f, 0.42f);
            Furnish("lampRoundFloor", l, c + new Vector3(-1.9f, 0f, -0.9f), 0f, 1.7f);
            Furnish("books", l, c + new Vector3(0.2f, 0.42f, 0.7f), 30f, 0.12f);

            foreach (var x in new[] { -3.95f, 3.95f })
            {
                Furnish("pottedPlant", l, new Vector3(x, 0f, RoomMaxZ - 0.55f), 0f, 1.4f);
                Furnish("speaker", l, new Vector3(x + Mathf.Sign(x) * 0.9f, 0f, RoomMaxZ - 0.45f), 180f, 1.15f);
            }
            Furnish("bookcaseOpen", l, new Vector3(RoomMinX + 0.35f, 0f, 6.3f), 90f, 2.0f);
            Furnish("pottedPlant", l, new Vector3(RoomMinX + 0.6f, 0f, 2.2f), 0f, 1.3f);
            Furnish("pottedPlant", l, new Vector3(RoomMaxX - 0.6f, 0f, 6.5f), 0f, 1.3f);
            Furnish("pottedPlant", l, new Vector3(RoomMaxX - 0.7f, GalleryH, RoomMinZ + 1.6f), 0f, 1.2f);
            Furnish("pottedPlant", l, new Vector3(RoomMinX + 0.7f, GalleryH, RoomMinZ + 1.6f), 0f, 1.2f);

            // Desk clutter.
            Furnish("plantSmall2", l, new Vector3(1.15f, 0.75f, 0.15f), 0f, 0.3f);
            Furnish("trashcan", l, new Vector3(1.45f, 0f, -0.55f), 0f, 0.45f);
        }

        /// <summary>Raised gallery along the back wall: floor plates, rails, central stairs and a small lounge.</summary>
        static void BuildGallery(Transform shell)
        {
            var g = new GameObject("Gallery").transform;
            g.SetParent(shell);
            int cols = Mathf.RoundToInt((RoomMaxX - RoomMinX) / KitTile);
            for (int i = 0; i < cols; i++)
            {
                float x = RoomMinX + KitTile * (i + 0.5f);
                Kit("floor-panel", g, new Vector3(x, 0f, RoomMinZ + KitTile / 2f), 0f);
                if (i == cols / 2)
                    Kit("stairs", g, new Vector3(x, 0f, GalleryZ + KitTile / 2f), 180f);
                else
                    Kit("rail", g, new Vector3(x, GalleryH, GalleryZ - 0.1f), 0f);
            }
            Neon("GalleryEdge", g, new Vector3(0f, GalleryH - 0.02f, GalleryZ + 0.01f), new Vector3(RoomMaxX - RoomMinX, 0.03f, 0.03f), NeonAmber, 0.2f);

            // Lounge on the gallery, right of the stairs.
            var lounge = new Vector3(4.2f, GalleryH, RoomMinZ + 1.3f);
            Kit("table-large", g, lounge, 0f, PropScale);
            foreach (var (dx, dz, yaw) in new[] { (-1.6f, 0f, 90f), (1.6f, 0f, -90f), (-0.6f, 1.0f, 180f), (0.6f, 1.0f, 180f) })
                Kit("chair-cushion-headrest", g, lounge + new Vector3(dx, 0f, dz), yaw, PropScale);
            Kit("computer-screen", g, new Vector3(-6.8f, GalleryH, RoomMinZ + 0.6f), 0f, PropScale);
            Kit("computer-wide", g, new Vector3(-5.2f, GalleryH, RoomMinZ + 0.6f), 0f, PropScale);
            Kit("container-tall", g, new Vector3(7.6f, GalleryH, RoomMinZ + 0.6f), 0f, PropScale);
            Kit("container", g, new Vector3(6.8f, GalleryH, RoomMinZ + 0.55f), 20f, PropScale);
            SpaceModel("astronaut", g, new Vector3(1.9f, GalleryH, RoomMinZ + 1.2f), 180f, 1.75f);
            SpaceModel("satelliteDishLarge", g, new Vector3(-7.2f, GalleryH, RoomMinZ + 1.1f), 150f, 1.7f);
        }

        /// <summary>Left side: a research lab with computer banks, a planet display and a holo table showing a deadlock.</summary>
        static void BuildLab(Transform shell)
        {
            var lab = new GameObject("Lab").transform;
            lab.SetParent(shell);
            foreach (var z in new[] { -2.6f, -0.6f, 1.4f })
            {
                Kit("computer-system", lab, new Vector3(RoomMinX + 0.7f, 0f, z), 90f, PropScale);
                Kit("chair-armrest-headrest", lab, new Vector3(RoomMinX + 2.1f, 0f, z), -90f, PropScale);
            }
            foreach (var z in new[] { -1.6f, 0.4f })
                Kit("display-wall-wide", lab, new Vector3(RoomMinX + 0.05f, 2.0f, z), 90f, PropScale);

            // Planet display with a spinning hologram planet above it.
            var planetTable = new Vector3(-4.4f, 0f, 0.9f);
            Kit("table-display-planet", lab, planetTable - new Vector3(0.25f, 0f, 0.25f) * KitTile, 0f);
            var holo = new GameObject("HoloPlanet").transform;
            holo.SetParent(lab);
            holo.position = planetTable + Vector3.up * 1.75f;
            holo.gameObject.AddComponent<HubBob>().Init(0.05f, 0.25f, 0.4f);
            var sphere = Primitive(PrimitiveType.Sphere, "Planet", holo, holo.position, Vector3.one * 0.55f, hub.Planet);
            sphere.AddComponent<HubSpin>().Init(new Vector3(0f, 25f, 0f));
            var orbit = Quad("Orbit", holo, holo.position, Quaternion.Euler(75f, 0f, 0f), 1.1f, hub.Glow);
            orbit.AddComponent<HubSpin>().Init(new Vector3(0f, 0f, 30f));
            orbit.AddComponent<HubPulse>().Init(new Color(0.35f, 0.85f, 1f, 0.8f), 0.6f, 1f, 0.4f, 0f);
            Primitive(PrimitiveType.Cylinder, "Beam", holo, planetTable + Vector3.up * 1.2f, new Vector3(0.7f, 0.5f, 0.7f), hub.Beam);

            // Holo table: the four-car cycle floating in the air.
            var table = new Vector3(-4.6f, 0f, -3.4f);
            Kit("table-display", lab, table, 0f, PropScale);
            var graph = new GameObject("HoloGraph").transform;
            graph.SetParent(lab);
            graph.position = table + Vector3.up * 1.35f;
            graph.gameObject.AddComponent<HubSpin>().Init(new Vector3(0f, 14f, 0f), Space.World);
            var colors = new[] { ViewColor.Fleet[0], ViewColor.Fleet[1], ViewColor.Fleet[2], ViewColor.Fleet[3] };
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI / 2f;
                var node = Primitive(PrimitiveType.Sphere, "Node", graph, graph.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.45f, Vector3.one * 0.12f, hub.Neon);
                node.AddComponent<HubPulse>().Init(colors[i] * 1.8f, 0.6f, 1f, 0.8f, i * 0.25f);
                var edge = Neon("Edge", graph, graph.position + new Vector3(Mathf.Cos(a + Mathf.PI / 4f), 0f, Mathf.Sin(a + Mathf.PI / 4f)) * 0.32f,
                                new Vector3(0.62f, 0.015f, 0.015f), NeonRed, 1.2f, i * 0.25f);
                edge.transform.rotation = Quaternion.Euler(0f, -(a * Mathf.Rad2Deg + 135f), 0f);
            }
        }

        /// <summary>Right side: engineering bay with stacked containers, an ore skip, pipes and structure frames.</summary>
        static void BuildEngineering(Transform shell)
        {
            var eng = new GameObject("Engineering").transform;
            eng.SetParent(shell);
            Kit("container-wide", eng, new Vector3(7.4f, 0f, -3.4f), 10f, PropScale);
            Kit("container", eng, new Vector3(7.4f, 0.7f * PropScale, -3.4f), 35f, PropScale);
            Kit("container-tall", eng, new Vector3(6.3f, 0f, -3.9f), 0f, PropScale);
            Kit("container-flat-open", eng, new Vector3(5.6f, 0f, -2.2f), 70f, PropScale);
            Kit("skip-rocks", eng, new Vector3(6.6f, 0f, 1.0f), 90f, PropScale);
            Kit("structure", eng, new Vector3(7.0f, 0f, 4.6f), 0f);
            Kit("structure-barrier-high", eng, new Vector3(4.6f, 0f, 4.6f), 0f);

            // Showcase: a spacecraft model turning slowly on its stand, ringed in amber.
            var stand = new Vector3(4.4f, 0f, 1.6f);
            SpaceModel("spaceCraftStand", eng, stand, 0f, 1.25f);
            var turntable = new GameObject("Turntable").transform;
            turntable.SetParent(eng);
            turntable.position = stand + Vector3.up * 1.25f;
            turntable.gameObject.AddComponent<HubSpin>().Init(new Vector3(0f, 12f, 0f));
            SpaceModel("spaceCraft3", turntable, turntable.position, 0f, 2.2f, byWidth: true, isStatic: false);
            var ringGlow = Quad("ShowcaseRing", eng, stand + Vector3.up * 0.02f, Quaternion.Euler(90f, 0f, 0f), 2.6f, hub.Glow);
            ringGlow.AddComponent<HubPulse>().Init(new Color(0.98f, 0.75f, 0.14f, 0.8f), 0.5f, 1f, 0.3f, 0f);
            Kit("container-tall", eng, new Vector3(7.0f, 0f, 4.6f), 0f, PropScale);
            Kit("structure-panel", eng, new Vector3(4.6f, KitTile, 4.6f), 0f);
            foreach (var z in new[] { -0.8f, 2.6f })
            {
                for (int k = 0; k < 4; k++)
                    Kit(k == 2 ? "pipe-ring-colored" : "pipe", eng, new Vector3(RoomMaxX - 0.45f, k * 0.5f * KitTile, z), 0f);
                Neon("PipeGlow", eng, new Vector3(RoomMaxX - 0.45f, 2.5f, z), new Vector3(0.36f, 0.04f, 0.36f), NeonAmber, 0.6f, z);
            }
        }

        static GameObject Quad(string name, Transform parent, Vector3 pos, Quaternion rot, float size, Material mat)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = name;
            NoCollider(q);
            q.transform.SetParent(parent, false);
            q.transform.SetPositionAndRotation(pos, rot);
            q.transform.localScale = Vector3.one * size;
            var r = q.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return q;
        }

        static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, bool collider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (!collider) NoCollider(go);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        // =====================================================================================================
        // Outside: stars, ringed planet, moon, ships
        // =====================================================================================================

        static void BuildSpaceView(Transform room)
        {
            var space = new GameObject("Space").transform;
            space.SetParent(room);

            var stars = Primitive(PrimitiveType.Sphere, "Starfield", space, Vector3.zero, Vector3.one * 300f, hub.Stars);
            stars.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            stars.AddComponent<HubSpin>().Init(new Vector3(0f, 0.15f, 0f));

            var planetRoot = new GameObject("Planet").transform;
            planetRoot.SetParent(space);
            planetRoot.SetPositionAndRotation(new Vector3(-20f, 16f, 95f), Quaternion.Euler(0f, 0f, 18f)); // framed by the panorama
            var planet = Primitive(PrimitiveType.Sphere, "Body", planetRoot, planetRoot.position, Vector3.one * 50f, hub.Planet);
            planet.transform.localRotation = Quaternion.identity;
            planet.AddComponent<HubSpin>().Init(new Vector3(0f, 1.2f, 0f));
            var rings = Quad("Rings", planetRoot, planetRoot.position, planetRoot.rotation * Quaternion.Euler(62f, 0f, 0f), 118f, hub.PlanetRing);
            rings.AddComponent<HubSpin>().Init(new Vector3(0f, 0f, 0.6f));

            var moonPivot = new GameObject("MoonOrbit").transform;
            moonPivot.SetParent(space);
            moonPivot.position = planetRoot.position;
            moonPivot.gameObject.AddComponent<HubSpin>().Init(new Vector3(0f, 2.2f, 0f));
            Primitive(PrimitiveType.Sphere, "Moon", moonPivot, planetRoot.position + new Vector3(52f, 6f, -30f), Vector3.one * 6f, hub.Moon);

            BuildShip(space, new Vector3(-90f, 6f, 55f), new Vector3(90f, 9f, 70f), 16f, 4f, 1f);
            BuildShip(space, new Vector3(80f, 18f, 120f), new Vector3(-80f, 12f, 110f), 26f, 13f, 2.2f);
            BuildShip(space, new Vector3(-60f, -4f, 40f), new Vector3(60f, 2f, 45f), 11f, 24f, 0.6f);
        }

        static int shipIndex;

        /// <summary>A Kenney spacecraft that crosses the window now and then, with a pulsing engine glow.</summary>
        static void BuildShip(Transform parent, Vector3 from, Vector3 to, float seconds, float delay, float scale)
        {
            var ship = new GameObject("Ship").transform;
            ship.SetParent(parent);
            var model = SpaceModel($"spaceCraft{1 + shipIndex++ % 6}", ship, Vector3.zero, ShipModelYaw, 6f * scale, byWidth: true, isStatic: false);
            model.transform.localPosition = Vector3.zero;
            var engine = Primitive(PrimitiveType.Sphere, "Engine", ship, Vector3.zero, new Vector3(0.9f, 0.9f, 0.5f) * scale, hub.Neon);
            engine.transform.localPosition = new Vector3(0f, 0.6f * scale, -3.2f * scale);
            engine.AddComponent<HubPulse>().Init(NeonCyan * 1.4f, 0.6f, 1f, 3f, 0f);
            ship.SetPositionAndRotation(from, Quaternion.LookRotation(to - from)); // parked at its start, outside the window
            ship.gameObject.AddComponent<HubFlyby>().Init(from, to, seconds, delay);
        }

        /// <summary>Kenney spacecraft models face -Z; turn them so the nose leads along the flight path (+Z).</summary>
        const float ShipModelYaw = 180f;

        // =====================================================================================================
        // Desk, keyboard, chair and props
        // =====================================================================================================

        static void BuildDesk(Transform room, out HubKeyboard keyboard, out List<HubKey> deskKeys)
        {
            var desk = new GameObject("Desk").transform;
            desk.SetParent(room);
            Box("Top", desk, new Vector3(0f, 0.725f, -0.05f), new Vector3(2.6f, 0.05f, 0.85f), hub.Metal);
            foreach (var x in new[] { -1.2f, 1.2f })
                Box("Leg", desk, new Vector3(x, 0.35f, -0.05f), new Vector3(0.08f, 0.7f, 0.75f), hub.Wall);
            Box("ModestyPanel", desk, new Vector3(0f, 0.42f, 0.3f), new Vector3(2.3f, 0.5f, 0.04f), hub.Wall);
            Neon("FrontEdge", desk, new Vector3(0f, 0.7f, -0.48f), new Vector3(2.6f, 0.012f, 0.012f), NeonCyan, 0.25f);
            Neon("UnderGlow", desk, new Vector3(0f, 0.69f, -0.05f), new Vector3(2.4f, 0.005f, 0.7f), NeonCyan * 0.25f, 0.1f);

            // Keyboard, close enough to reach from the chair: 14 × 4 decorative keys and a space bar on the left, and a
            // working pad on the right (1 2 3, ◄ ►, ENTER) that drives the monitor. Underglow, and a mouse beside it.
            var kb = new GameObject("Keyboard").transform;
            kb.SetParent(desk);
            kb.SetPositionAndRotation(new Vector3(-0.1f, 0.755f, -0.31f), Quaternion.Euler(-4f, 0f, 0f));
            var kbBase = Box("Base", kb, kb.position, new Vector3(0.86f, 0.02f, 0.21f), hub.Seat);
            kbBase.transform.localRotation = Quaternion.identity;
            kbBase.transform.localPosition = new Vector3(0.12f, 0f, 0f);
            var keys = new List<Renderer>();
            for (int row = 0; row < 4; row++)
            for (int col = 0; col < 14; col++)
            {
                var key = Box("Key", kb, Vector3.zero, new Vector3(0.034f, 0.012f, 0.034f), hub.KeyCap);
                key.transform.localPosition = new Vector3(-0.26f + col * 0.04f + (row % 2) * 0.01f, 0.016f, 0.07f - row * 0.042f);
                key.transform.localRotation = Quaternion.identity;
                NoCollider(key);
                keys.Add(key.GetComponent<Renderer>());
            }
            var space = Box("SpaceBar", kb, Vector3.zero, new Vector3(0.22f, 0.012f, 0.032f), hub.KeyCap);
            space.transform.localPosition = new Vector3(0f, 0.016f, -0.1f);
            space.transform.localRotation = Quaternion.identity;
            NoCollider(space);
            keys.Add(space.GetComponent<Renderer>());
            var glow = Quad("KeyGlow", kb, kb.TransformPoint(new Vector3(0.12f, -0.009f, 0f)), kb.rotation * Quaternion.Euler(90f, 0f, 0f), 1f, hub.Glow);
            glow.transform.localScale = new Vector3(1.15f, 0.4f, 1f);
            glow.AddComponent<HubPulse>().Init(new Color(0.35f, 0.85f, 1f, 0.6f), 0.5f, 1f, 0.3f, 0f);

            var mouse = Box("Mouse", desk, new Vector3(0.62f, 0.765f, -0.3f), new Vector3(0.065f, 0.03f, 0.11f), hub.Seat);
            NoCollider(mouse);
            Neon("MouseStripe", desk, new Vector3(0.62f, 0.781f, -0.28f), new Vector3(0.008f, 0.003f, 0.05f), NeonCyan, 1.2f);

            var keySource = kb.gameObject.AddComponent<AudioSource>();
            keySource.spatialBlend = 1f;
            keySource.minDistance = 0.5f;
            keySource.maxDistance = 6f;
            keySource.rolloffMode = AudioRolloffMode.Linear;
            keySource.volume = 0.45f;
            keySource.playOnAwake = false;
            keyboard = kb.gameObject.AddComponent<HubKeyboard>();
            keyboard.Init(keys.ToArray(), keySource, Clip("Hub/key_click.wav"));

            // The working pad.
            var divider = Neon("PadDivider", kb, Vector3.zero, new Vector3(0.004f, 0.003f, 0.19f), NeonCyan, 0.4f);
            divider.transform.localPosition = new Vector3(0.305f, 0.012f, 0f);
            divider.transform.localRotation = Quaternion.identity;
            var digit = new Color(2.2f, 1.4f, 0.25f);
            var arrow = NeonCyan;
            var enter = new Color(0.3f, 2.2f, 0.6f);
            var padClip = Clip("Hub/key_click.wav");
            deskKeys = new List<HubKey>();
            for (int i = 0; i < 3; i++)
                deskKeys.Add(DeskKey(kb, $"{i + 1}", HubKey.KeyAction.Slot, i, new Vector3(0.36f + i * 0.06f, 0.016f, 0.065f), new Vector2(0.05f, 0.045f), digit, 0.26f, keyboard, keySource, padClip));
            deskKeys.Add(DeskKey(kb, "◄", HubKey.KeyAction.Left, 0, new Vector3(0.37f, 0.016f, 0.008f), new Vector2(0.07f, 0.045f), arrow, 0.3f, keyboard, keySource, padClip));
            deskKeys.Add(DeskKey(kb, "►", HubKey.KeyAction.Right, 0, new Vector3(0.47f, 0.016f, 0.008f), new Vector2(0.07f, 0.045f), arrow, 0.3f, keyboard, keySource, padClip));
            deskKeys.Add(DeskKey(kb, "ENTER", HubKey.KeyAction.Enter, 0, new Vector3(0.42f, 0.016f, -0.056f), new Vector2(0.17f, 0.05f), enter, 0.17f, keyboard, keySource, padClip));

            // A small floating hint over the pad.
            var hint = new GameObject("PadHint").AddComponent<TextMeshPro>();
            hint.transform.SetParent(kb, false);
            hint.transform.localPosition = new Vector3(0.42f, 0.095f, 0.03f);
            hint.transform.rotation = Quaternion.LookRotation(hint.transform.position - (ChairPos + Vector3.up * 1.2f));
            hint.font = font;
            hint.text = "◄ ►  CHOOSE     ENTER  START";
            hint.fontSize = 0.16f;
            hint.fontStyle = FontStyles.Bold;
            hint.characterSpacing = 4f;
            hint.color = new Color(0.55f, 0.95f, 1f, 0.85f);
            hint.alignment = TextAlignmentOptions.Center;
            hint.textWrappingMode = TextWrappingModes.NoWrap;
            hint.rectTransform.sizeDelta = new Vector2(0.3f, 0.04f);
            hint.gameObject.AddComponent<HubBob>().Init(0.006f, 0.5f, 0f);

            // Coffee mug and a status orb, because people live here.
            Primitive(PrimitiveType.Cylinder, "Mug", desk, new Vector3(0.95f, 0.795f, -0.15f), new Vector3(0.08f, 0.05f, 0.08f), hub.Wall);
            Neon("MugBand", desk, new Vector3(0.95f, 0.8f, -0.15f), new Vector3(0.082f, 0.008f, 0.082f), NeonAmber, 0.1f);
            var orb = Primitive(PrimitiveType.Sphere, "StatusOrb", desk, new Vector3(-0.55f, 0.775f, 0.12f), Vector3.one * 0.05f, hub.Neon);
            orb.AddComponent<HubPulse>().Init(NeonAmber, 0.2f, 1f, 0.8f, 0f);
        }

        static void BuildChair(Transform room)
        {
            var chair = new GameObject("Chair").transform;
            chair.SetParent(room);
            var c = ChairPos;
            Box("Seat", chair, c + new Vector3(0f, 0.47f, 0f), new Vector3(0.56f, 0.09f, 0.54f), hub.Seat);
            var back = Box("Back", chair, c + new Vector3(0f, 0.95f, -0.3f), new Vector3(0.54f, 0.78f, 0.08f), hub.Seat);
            back.transform.rotation = Quaternion.Euler(-10f, 0f, 0f);
            var head = Box("Headrest", chair, c + new Vector3(0f, 1.42f, -0.39f), new Vector3(0.34f, 0.16f, 0.08f), hub.Seat);
            head.transform.rotation = Quaternion.Euler(-10f, 0f, 0f);
            var spine = Neon("BackStripe", chair, c + new Vector3(0f, 0.97f, -0.255f), new Vector3(0.02f, 0.62f, 0.01f), NeonCyan, 0.3f);
            spine.transform.rotation = Quaternion.Euler(-10f, 0f, 0f);
            foreach (var s in new[] { -1f, 1f })
            {
                Box("Armrest", chair, c + new Vector3(s * 0.32f, 0.68f, 0.02f), new Vector3(0.07f, 0.04f, 0.42f), hub.Metal);
                Box("ArmPost", chair, c + new Vector3(s * 0.32f, 0.58f, -0.08f), new Vector3(0.04f, 0.18f, 0.04f), hub.Metal);
                Neon("ArmLight", chair, c + new Vector3(s * 0.32f, 0.702f, 0.08f), new Vector3(0.02f, 0.004f, 0.18f), NeonAmber, 0.4f, s);
            }
            Primitive(PrimitiveType.Cylinder, "Column", chair, c + new Vector3(0f, 0.24f, 0f), new Vector3(0.07f, 0.2f, 0.07f), hub.Metal);
            for (int i = 0; i < 5; i++)
            {
                var leg = Box("BaseLeg", chair, c + Quaternion.Euler(0f, i * 72f, 0f) * new Vector3(0f, 0.06f, 0.17f), new Vector3(0.05f, 0.04f, 0.34f), hub.Metal);
                leg.transform.rotation = Quaternion.Euler(0f, i * 72f, 0f);
                Primitive(PrimitiveType.Sphere, "Wheel", chair, c + Quaternion.Euler(0f, i * 72f, 0f) * new Vector3(0f, 0.03f, 0.33f), Vector3.one * 0.06f, hub.Seat);
            }
        }

        static void BuildHoloProjector(Transform room)
        {
            var root = new GameObject("HoloProjector").transform;
            root.SetParent(room);
            var p = new Vector3(-1.05f, 0.75f, -0.3f); // front-left corner of the desk, clear of the side monitor
            Primitive(PrimitiveType.Cylinder, "Base", root, p + Vector3.up * 0.02f, new Vector3(0.18f, 0.02f, 0.18f), hub.Metal);
            var lens = Primitive(PrimitiveType.Cylinder, "Lens", root, p + Vector3.up * 0.042f, new Vector3(0.12f, 0.003f, 0.12f), hub.Neon);
            lens.AddComponent<HubPulse>().Init(NeonCyan, 0.6f, 1f, 1.5f, 0f);
            Primitive(PrimitiveType.Cylinder, "Beam", root, p + Vector3.up * 0.2f, new Vector3(0.16f, 0.16f, 0.16f), hub.Beam);

            var holo = new GameObject("Hologram").transform;
            holo.SetParent(root);
            holo.position = p + Vector3.up * 0.3f;
            holo.gameObject.AddComponent<HubBob>().Init(0.012f, 0.35f, 0f);
            var outer = Quad("RingOuter", holo, holo.position, Quaternion.Euler(70f, 0f, 0f), 0.26f, hub.Glow);
            outer.AddComponent<HubSpin>().Init(new Vector3(0f, 0f, 40f));
            outer.AddComponent<HubPulse>().Init(new Color(0.35f, 0.85f, 1f, 0.9f), 0.6f, 1f, 0.5f, 0f);
            var inner = Quad("RingInner", holo, holo.position, Quaternion.Euler(20f, 50f, 0f), 0.17f, hub.Glow);
            inner.AddComponent<HubSpin>().Init(new Vector3(0f, 0f, -70f));
            inner.AddComponent<HubPulse>().Init(new Color(0.98f, 0.75f, 0.14f, 0.9f), 0.6f, 1f, 0.7f, 0.3f);
            var core = Primitive(PrimitiveType.Cube, "Core", holo, holo.position, Vector3.one * 0.05f, hub.Neon);
            core.AddComponent<HubSpin>().Init(new Vector3(35f, 60f, 20f));
            core.AddComponent<HubPulse>().Init(NeonCyan, 0.5f, 1f, 1f, 0f);

            var label = new GameObject("Label").transform;
            label.SetParent(holo, false);
            label.position = holo.position + Vector3.up * 0.14f;
            label.rotation = Quaternion.LookRotation(label.position - (ChairPos + Vector3.up * 1.2f)); // faces the seat
            {
                var t = new GameObject("Text").AddComponent<TextMeshPro>();
                t.transform.SetParent(label, false);
                t.font = font;
                t.text = "OS PLAYS";
                t.fontSize = 0.32f;
                t.fontStyle = FontStyles.Bold;
                t.characterSpacing = 8f;
                t.color = new Color(0.55f, 0.95f, 1f, 0.9f);
                t.alignment = TextAlignmentOptions.Center;
                t.textWrappingMode = TextWrappingModes.NoWrap;
                t.rectTransform.sizeDelta = new Vector2(0.6f, 0.1f);
            }
        }

        static void BuildServerRacks(Transform room)
        {
            var root = new GameObject("ServerRacks").transform;
            root.SetParent(room);
            var rng = new System.Random(5);
            for (int r = 0; r < 3; r++)
            {
                var pos = new Vector3(-3.2f + r * 0.75f, GalleryH + 1.0f, RoomMinZ + 0.45f);
                Box("Rack", root, pos, new Vector3(0.65f, 2f, 0.6f), hub.Seat);
                for (int i = 0; i < 9; i++)
                {
                    var color = i % 4 == 0 ? NeonAmber : i % 7 == 0 ? NeonRed : new Color(0.3f, 2.2f, 0.6f);
                    var led = Neon("LED", root, pos + new Vector3(-0.22f + (i % 3) * 0.06f, -0.75f + i * 0.18f, 0.305f), new Vector3(0.03f, 0.015f, 0.005f), color);
                    led.GetComponent<HubPulse>().Init(color, 0f, 1f, 0.6f + (float)rng.NextDouble() * 3f, (float)rng.NextDouble()); // blinking activity
                    Neon("Slot", root, pos + new Vector3(0.12f, -0.75f + i * 0.18f, 0.305f), new Vector3(0.22f, 0.006f, 0.004f), NeonCyan * 0.4f, 0.1f, i * 0.1f);
                }
            }
        }

        /// <summary>Wall screen on the right: the deadlock idea at a glance, a cycle that blinks red.</summary>
        static void BuildWallDisplay(Transform room)
        {
            var canvas = WorldCanvas("WallDisplay", room, new Vector2(1400, 800), 0.0011f);
            canvas.transform.SetPositionAndRotation(new Vector3(RoomMaxX - 0.08f, 2.3f, 0.9f), Quaternion.Euler(0f, 90f, 0f));
            var t = canvas.transform;
            Bordered("Frame", t, 0, 0, 1400, 800, new Color(0.02f, 0.05f, 0.09f, 1f), Cyan);
            UIText("Title", t, 50, 30, 1300, 70, "WAIT-FOR GRAPH  //  LIVE", 48, Cyan, TextAlignmentOptions.Left, FontStyles.Bold).characterSpacing = 6f;
            UIText("Graph", t, 50, 170, 1300, 300,
                "<size=100%><b><color=#3B82F6>A</color>  →  <color=#F59E0B>B</color>  →  <color=#22C55E>C</color>  →  <color=#A855F7>D</color>  →  <color=#3B82F6>A</color></b></size>",
                90, ViewColor.Text, TextAlignmentOptions.Center).textWrappingMode = TextWrappingModes.NoWrap;
            var alert = UIText("Alert", t, 50, 520, 1300, 100, "CIRCULAR WAIT DETECTED", 64, new Color(0.94f, 0.27f, 0.27f), TextAlignmentOptions.Center, FontStyles.Bold);
            alert.gameObject.AddComponent<HubBlinkText>();
            UIText("Footer", t, 50, 660, 1300, 100, "process  =  car        resource  =  parking bay", 36, Muted, TextAlignmentOptions.Center);
        }

        static void BuildDrone(Transform room)
        {
            var drone = new GameObject("Drone").transform;
            drone.SetParent(room);
            drone.position = new Vector3(2.4f, 2.3f, 1.6f);
            drone.gameObject.AddComponent<HubBob>().Init(0.08f, 0.22f, 0.3f);
            var body = Primitive(PrimitiveType.Sphere, "Body", drone, drone.position, Vector3.one * 0.22f, hub.Metal);
            body.AddComponent<HubSpin>().Init(new Vector3(0f, 18f, 0f));
            var eye = Primitive(PrimitiveType.Sphere, "Eye", body.transform, drone.position + new Vector3(-0.06f, -0.02f, -0.08f), Vector3.one * 0.07f, hub.Neon);
            eye.AddComponent<HubPulse>().Init(NeonCyan, 0.4f, 1f, 0.9f, 0f);
            var halo = Quad("Rotor", drone, drone.position + Vector3.up * 0.13f, Quaternion.Euler(90f, 0f, 0f), 0.35f, hub.Glow);
            halo.AddComponent<HubSpin>().Init(new Vector3(0f, 0f, 400f));
            halo.AddComponent<HubPulse>().Init(new Color(0.35f, 0.85f, 1f, 0.5f), 0.6f, 1f, 2f, 0f);
        }

        // =====================================================================================================
        // The monitor: scenario tiles, boot sequence and the launch into the screen
        // =====================================================================================================

        /// <summary>
        /// A working desk key: an XR simple interactable that a fingertip pokes downwards (or a ray selects), a cap that
        /// sinks and glows, and a label printed on top.
        /// </summary>
        static HubKey DeskKey(Transform keyboard, string label, HubKey.KeyAction action, int slot, Vector3 localPos, Vector2 size, Color color,
                              float labelSize, HubKeyboard board, AudioSource source, AudioClip clip)
        {
            var root = new GameObject($"Key_{label}");
            root.transform.SetParent(keyboard, false);
            root.transform.localPosition = localPos;
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.012f, 0f); // reaches ~2 cm above the cap: the finger hovers, then presses down
            box.size = new Vector3(size.x, 0.034f, size.y);
            root.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
            var poke = root.AddComponent<UnityEngine.XR.Interaction.Toolkit.Filtering.XRPokeFilter>();
            var cfg = poke.pokeConfiguration.Value;
            cfg.pokeDirection = UnityEngine.XR.Interaction.Toolkit.Filtering.PokeAxis.NegativeY; // pressed downwards
            cfg.interactionDepthOffset = 0.004f;
            cfg.enablePokeAngleThreshold = true;
            cfg.pokeAngleThreshold = 60f;

            var cap = new GameObject("Cap").transform;
            cap.SetParent(root.transform, false);
            var mesh = Box("Mesh", cap, Vector3.zero, new Vector3(size.x - 0.006f, 0.014f, size.y - 0.006f), hub.KeyCap);
            mesh.transform.localPosition = Vector3.zero;
            mesh.transform.localRotation = Quaternion.identity;
            NoCollider(mesh);

            var text = new GameObject("Label").AddComponent<TextMeshPro>();
            text.transform.SetParent(cap, false);
            text.transform.localPosition = new Vector3(0f, 0.0075f, 0f);
            text.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // printed on the cap, readable from the chair
            text.font = font;
            text.text = label;
            text.fontSize = labelSize;
            text.fontStyle = FontStyles.Bold;
            text.color = new Color(0.02f, 0.03f, 0.05f);
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.rectTransform.sizeDelta = size;

            var key = root.AddComponent<HubKey>();
            key.Init(action, slot, board, cap, mesh.GetComponent<Renderer>(), color, source, clip);
            return key;
        }

        static HubScreen BuildMonitor(Transform room, XRModeController mode, Unity.XR.CoreUtils.XROrigin origin, Transform desktopCamera,
                                      ScreenFader fader, HubKeyboard keyboard)
        {
            var root = new GameObject("Monitor").transform;
            root.SetParent(room);
            root.SetPositionAndRotation(MonitorPos, Quaternion.identity); // forward = into the glass
            float sw = 1920 * MonitorScale, sh = 1080 * MonitorScale;

            Box("Bezel", root, MonitorPos + Vector3.forward * 0.045f, new Vector3(sw + 0.08f, sh + 0.08f, 0.06f), hub.Seat);
            Box("Stand", root, new Vector3(0f, 0.75f + (MonitorPos.y - sh / 2f - 0.75f) / 2f, MonitorPos.z + 0.1f), new Vector3(0.12f, MonitorPos.y - sh / 2f - 0.75f + 0.05f, 0.05f), hub.Metal);
            Box("Foot", root, new Vector3(0f, 0.755f, MonitorPos.z + 0.08f), new Vector3(0.45f, 0.012f, 0.22f), hub.Metal);
            foreach (var (n, pos, size) in new[]
                     {
                         ("EdgeT", new Vector3(0f, sh / 2f + 0.04f, 0f), new Vector3(sw + 0.08f, 0.01f, 0.01f)),
                         ("EdgeB", new Vector3(0f, -sh / 2f - 0.04f, 0f), new Vector3(sw + 0.08f, 0.01f, 0.01f)),
                         ("EdgeL", new Vector3(-sw / 2f - 0.04f, 0f, 0f), new Vector3(0.01f, sh + 0.08f, 0.01f)),
                         ("EdgeR", new Vector3(sw / 2f + 0.04f, 0f, 0f), new Vector3(0.01f, sh + 0.08f, 0.01f)),
                     })
                Neon(n, root, MonitorPos + pos + Vector3.forward * 0.01f, size, NeonCyan, 0.35f);

            var glow = Quad("ScreenGlow", root, MonitorPos + Vector3.forward * 0.012f, Quaternion.identity, 1f, hub.Screen);
            glow.transform.localScale = new Vector3(sw, sh, 1f);
            var scan = Quad("Scanline", root, MonitorPos + Vector3.back * 0.004f, Quaternion.identity, 1f, hub.Glow);
            scan.transform.localScale = new Vector3(sw, 0.05f, 1f);
            scan.AddComponent<HubPulse>().Init(new Color(0.35f, 0.85f, 1f, 0.12f), 1f, 1f, 0f, 0f);
            scan.AddComponent<HubScanline>().Init(sh / 2f, -sh / 2f, 4f);

            // Side monitors (decor) angled towards the chair.
            foreach (var s in new[] { -1f, 1f })
                BuildSideMonitor(room, new Vector3(s * 1.2f, 1.12f, 0.2f), s);

            // --- Canvas ---
            var canvas = WorldCanvas("MonitorCanvas", root, new Vector2(1920, 1080), MonitorScale);
            canvas.transform.position = MonitorPos;
            // Phone AR: a 6 cm screen on the tabletop model becomes a hologram above the room, big enough to read and tap.
            canvas.AddComponent<Gridlock.AR.ARHoloScreen>().Init(5f, new Vector3(0f, 3.1f, 0.8f));
            canvas.GetComponent<Canvas>().sortingOrder = 1;

            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(canvas.transform, false);
            content.anchorMin = Vector2.zero;
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 0.5f);
            content.sizeDelta = Vector2.zero;
            var c = content.transform;

            Panel("Background", c, 0, 0, 1920, 1080, new Color(0.015f, 0.04f, 0.08f, 0.93f)).GetComponent<Image>().pixelsPerUnitMultiplier = 1f;
            for (int i = 1; i < 12; i++) // faint grid
                Panel("GridLine", c, i * 160, 110, 2, 970, new Color(0.35f, 0.85f, 1f, 0.04f)).GetComponent<Image>().raycastTarget = false;
            var header = Panel("Header", c, 0, 0, 1920, 110, new Color(0.04f, 0.1f, 0.18f, 1f));
            header.GetComponent<Image>().pixelsPerUnitMultiplier = 1f;
            Panel("HeaderLine", c, 0, 108, 1920, 4, Cyan);
            Logo(c, 36, 10, 92);
            var title = UIText("Title", c, 146, 20, 1300, 76, "", 50, Cyan, TextAlignmentOptions.Left, FontStyles.Bold);
            title.characterSpacing = 6f;
            var clock = UIText("Clock", c, 1500, 20, 370, 76, "", 48, Amber, TextAlignmentOptions.Right, FontStyles.Bold);
            UIText("Subtitle", c, 50, 128, 1820, 44, "OPERATING SYSTEMS  ·  MODULE 4        CHOOSE A SCENARIO", 28, Muted, TextAlignmentOptions.Left).characterSpacing = 4f;

            var thumb = ThumbSprite(HubThumbPath);
            var tiles = new[]
            {
                Tile(c, "Tile_Deadlock", 60, "01", "DEADLOCK", "Cars are processes, bays are resources. Build a circular wait, detect it, then avoid it with the Banker's algorithm.",
                     "DETECTION  ·  BANKER", "ENTER SIMULATION", thumb, DeadlockSceneName, false, Cyan),
                Tile(c, "Tile_CpuScheduling", 680, "02", "CPU SCHEDULING", "Processes queue for one CPU core. Run FCFS, SRTF, Priority and Round Robin and read the Gantt chart.",
                     "FCFS  ·  SRTF  ·  RR", "ENTER SIMULATION", ThumbSprite(CpuThumbPath), CpuSceneName, false, new Color(0.66f, 0.33f, 0.97f)),
                Tile(c, "Tile_Memory", 1300, "03", "MEMORY", "Pallets are frames, boxes are pages. Load them in order and choose the victim like FIFO, LRU and Optimal.",
                     "PAGE REPLACEMENT", "ENTER SIMULATION", ThumbSprite(MemoryThumbPath), MemorySceneName, false, new Color(0.98f, 0.45f, 0.2f)),
            };

            var console = UIText("Console", c, 60, 822, 1240, 190, "", 25, new Color(0.62f, 0.8f, 0.9f), TextAlignmentOptions.BottomLeft);
            console.lineSpacing = 6f;
            UIText("Status", c, 1340, 840, 520, 60, "<color=#22C55E>●</color> SYSTEM ONLINE", 30, Muted, TextAlignmentOptions.Right, FontStyles.Bold);
            var prompt = UIText("Prompt", c, 0, 1018, 1920, 52, "", 30, Amber, TextAlignmentOptions.Center, FontStyles.Bold);
            var flashGo = Panel("Flash", c, 0, 0, 1920, 1080, new Color(0.85f, 0.97f, 1f, 0f));
            var flash = flashGo.GetComponent<Image>();
            flash.raycastTarget = false;
            flash.pixelsPerUnitMultiplier = 1f;

            // --- Audio, warp effects, launcher ---
            var ui = root.gameObject.AddComponent<AudioSource>();
            ui.spatialBlend = 0.5f;
            ui.playOnAwake = false;
            var whoosh = new GameObject("Warp Sound", typeof(AudioSource)).GetComponent<AudioSource>();
            whoosh.transform.SetParent(root, false);
            whoosh.clip = Clip("Hub/warp.wav");
            whoosh.playOnAwake = false;
            whoosh.spatialBlend = 0f;

            var streaks = WarpStreaks(root, sw, sh);
            var tunnel = WarpRings(root);

            var launcher = root.gameObject.AddComponent<HubLauncher>();
            launcher.Init(mode, origin, desktopCamera, root, glow.GetComponent<Renderer>(), new[] { streaks, tunnel }, fader, whoosh);

            var screen = canvas.AddComponent<HubScreen>();
            screen.Init(content, flash, title, console, clock, prompt, tiles, launcher, keyboard, ui,
                        Clip("Hub/hub_boot.wav"), Clip("Kenney/click_002.ogg"), Clip("Hub/ui_hover.wav"), Clip("Hub/ui_select.wav"),
                        Clip("Hub/ui_denied.wav"), Clip("Hub/glitch.wav"));
            foreach (var tile in tiles)
            {
                var so = new SerializedObject(tile);
                so.FindProperty("screen").objectReferenceValue = screen;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            return screen;
        }

        static HubTile Tile(Transform parent, string name, float x, string number, string title, string body, string tag, string action,
                            Sprite thumb, string scene, bool locked, Color accent)
        {
            const float w = 560, h = 640, y = 180;
            var root = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            var rt = Rect(root, parent, x, y, w, h);
            rt.pivot = new Vector2(0.5f, 0.5f); // scale from the centre on hover
            rt.anchoredPosition = new Vector2(x + w / 2f, -(y + h / 2f));
            var t = root.transform;

            var glow = Panel("Glow", t, -18, -18, w + 36, h + 36, new Color(accent.r, accent.g, accent.b, 0f)).GetComponent<Image>();
            glow.raycastTarget = false;
            var border = Panel("Border", t, 0, 0, w, h, accent).GetComponent<Image>();
            Panel("Fill", t, 4, 4, w - 8, h - 8, new Color(0.03f, 0.07f, 0.12f, 1f)).GetComponent<Image>().raycastTarget = false;

            if (thumb != null)
            {
                var img = new GameObject("Thumb", typeof(RectTransform), typeof(Image));
                Rect(img, t, 20, 20, w - 40, 290);
                var i = img.GetComponent<Image>();
                i.sprite = thumb;
                i.preserveAspect = false;
                i.raycastTarget = false;
            }
            else
            {
                var art = Panel("Art", t, 20, 20, w - 40, 290, new Color(accent.r * 0.18f, accent.g * 0.18f, accent.b * 0.18f, 1f));
                art.GetComponent<Image>().raycastTarget = false;
                foreach (var ly in new[] { 22f, 256f }) // thin rules above and below the emblem
                    Panel("Rule", art.transform, 30, ly, w - 100, 3, new Color(accent.r, accent.g, accent.b, 0.4f)).GetComponent<Image>().raycastTarget = false;
                UIText("Lock", art.transform, 0, 40, w - 40, 160, number, 150, new Color(accent.r, accent.g, accent.b, 0.55f), TextAlignmentOptions.Center, FontStyles.Bold);
                UIText("Locked", art.transform, 0, 190, w - 40, 50, "ACCESS LOCKED", 30, new Color(1f, 1f, 1f, 0.6f), TextAlignmentOptions.Center, FontStyles.Bold).characterSpacing = 8f;
            }

            UIText("Number", t, 24, 322, 120, 60, number, 44, accent, TextAlignmentOptions.Left, FontStyles.Bold);
            UIText("Tag", t, 140, 334, w - 164, 40, tag, 22, Muted, TextAlignmentOptions.Right, FontStyles.Bold).characterSpacing = 4f;
            UIText("Title", t, 24, 382, w - 48, 66, title, 50, Color.white, TextAlignmentOptions.Left, FontStyles.Bold).characterSpacing = 3f;
            var desc = UIText("Body", t, 24, 450, w - 48, 110, body, 24, Muted, TextAlignmentOptions.TopLeft);
            desc.lineSpacing = 4f;
            var actionBar = Panel("ActionBar", t, 20, 568, w - 40, 54, new Color(accent.r, accent.g, accent.b, locked ? 0.15f : 0.28f));
            actionBar.GetComponent<Image>().raycastTarget = false;
            var label = UIText("Action", actionBar.transform, 0, 0, w - 40, 54, action, 28, locked ? Muted : Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            label.characterSpacing = 6f;

            var tile = root.AddComponent<HubTile>();
            tile.Init(null, scene, locked, root.GetComponent<CanvasGroup>(), border, glow, label, accent);
            return tile;
        }

        const string LogoPath = "Assets/UI/Brand/OSPlaysLogo.png";

        /// <summary>The OS Plays mark (transparent), <paramref name="size"/> px square at x, y.</summary>
        static Image Logo(Transform parent, float x, float y, float size)
        {
            var go = new GameObject("Logo", typeof(RectTransform), typeof(Image));
            Rect(go, parent, x, y, size, size);
            var image = go.GetComponent<Image>();
            image.sprite = ThumbSprite(LogoPath) ?? throw new FileNotFoundException($"Missing {LogoPath}");
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        static Sprite ThumbSprite(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) return null;
            if (importer.textureType != TextureImporterType.Sprite || importer.maxTextureSize > 1024)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.maxTextureSize = 1024;
                importer.mipmapEnabled = true;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static void BuildSideMonitor(Transform room, Vector3 pos, float side)
        {
            var root = new GameObject("SideMonitor").transform;
            root.SetParent(room);
            root.SetPositionAndRotation(pos, Quaternion.Euler(0f, side * 32f, 0f));
            var bezel = Box("Bezel", root, pos + root.forward * 0.03f, new Vector3(0.74f, 0.46f, 0.04f), hub.Seat);
            bezel.transform.rotation = root.rotation;
            Box("Stand", root, new Vector3(pos.x, 0.82f, pos.z + 0.08f), new Vector3(0.05f, 0.14f, 0.05f), hub.Metal);
            var canvas = WorldCanvas("SideCanvas", root, new Vector2(700, 430), 0.001f);
            canvas.transform.SetPositionAndRotation(pos, root.rotation);
            var t = canvas.transform;
            Bordered("Frame", t, 0, 0, 700, 430, new Color(0.02f, 0.05f, 0.09f, 1f), side < 0 ? Amber : Cyan);
            if (side < 0)
            {
                UIText("Title", t, 24, 16, 650, 50, "RESOURCE MONITOR", 32, Amber, TextAlignmentOptions.Left, FontStyles.Bold).characterSpacing = 4f;
                for (int i = 0; i < 6; i++)
                {
                    UIText("Label", t, 24, 84 + i * 54, 90, 40, $"P{i + 1}", 28, Muted, TextAlignmentOptions.Left, FontStyles.Bold);
                    var bar = Panel("Bar", t, 110, 92 + i * 54, 560, 26, new Color(0.98f, 0.75f, 0.14f, 0.25f));
                    var fill = Panel("Fill", bar.transform, 0, 0, 120 + (i * 97) % 420, 26, new Color(0.98f, 0.75f, 0.14f, 0.9f));
                    fill.AddComponent<HubBarWobble>().Init(0.3f + i * 0.11f, i * 0.7f);
                }
            }
            else
            {
                UIText("Title", t, 24, 16, 650, 50, "PROCESS TABLE", 32, Cyan, TextAlignmentOptions.Left, FontStyles.Bold).characterSpacing = 4f;
                UIText("Table", t, 24, 80, 650, 330,
                    "<mspace=0.6em>PID  STATE     HOLDS  WANTS\n A   <color=#22C55E>running</color>   P1     -\n B   <color=#FBBF24>waiting</color>   P2     P3\n"
                    + " C   <color=#FBBF24>waiting</color>   P3     P4\n D   <color=#EF4444>blocked</color>   P4     P2</mspace>",
                    28, ViewColor.Text, TextAlignmentOptions.TopLeft).lineSpacing = 12f;
            }
        }

        static ParticleSystem WarpStreaks(Transform monitor, float sw, float sh)
        {
            var go = new GameObject("WarpStreaks", typeof(ParticleSystem));
            go.transform.SetParent(monitor, false);
            go.transform.SetPositionAndRotation(MonitorPos + Vector3.back * 0.02f, Quaternion.LookRotation(Vector3.back)); // emit towards the chair
            var ps = go.GetComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 11f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.008f, 0.02f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.6f, 0.95f, 1f), Color.white);
            main.maxParticles = 600;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(160f, 420f);
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(sw, sh, 0.01f);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.06f;
            r.lengthScale = 3f;
            r.sharedMaterial = hub.Warp;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return ps;
        }

        static ParticleSystem WarpRings(Transform monitor)
        {
            var go = new GameObject("WarpTunnel", typeof(ParticleSystem));
            go.transform.SetParent(monitor, false);
            go.transform.SetPositionAndRotation(MonitorPos + Vector3.back * 0.03f, Quaternion.LookRotation(Vector3.back));
            var ps = go.GetComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 2f;
            main.startLifetime = 0.9f;
            main.startSpeed = 2.2f;
            main.startSize = 0.3f;
            main.startColor = new Color(0.5f, 0.9f, 1f, 0.8f);
            main.maxParticles = 30;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 9f;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 0f;
            shape.radius = 0.001f;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.3f, 1f, 9f));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.6f, 0.9f, 1f), 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.2f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.alignment = ParticleSystemRenderSpace.Local; // rings face down the tunnel, towards the viewer
            r.sharedMaterial = hub.WarpRing;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return ps;
        }

        /// <summary>
        /// Phone-only keypad along the bottom of the HUD: ◄ 1 2 3 ► and START, mirroring the desk keys at a size a
        /// thumb can hit (the real keys are millimetres tall on a phone screen).
        /// </summary>
        static void BuildTouchKeypad(Transform hud, HubDesktopInput input)
        {
            const float key = 128f, gap = 14f, start = 250f;
            const float width = 5 * key + 4 * gap + 2 * gap + start;
            var pad = new GameObject("TouchKeypad", typeof(RectTransform));
            var rt = (RectTransform)pad.transform;
            rt.SetParent(hud, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(width + 2 * gap, key + 2 * gap);
            rt.anchoredPosition = new Vector2(0f, 62f);
            pad.AddComponent<TouchVariant>().Init(TouchVariant.Show.TouchOnly, null);
            Bordered("Backdrop", pad.transform, 0f, 0f, width + 2 * gap, key + 2 * gap, new Color(0.04f, 0.07f, 0.13f, 0.72f), new Color(0.2f, 0.75f, 1f, 0.6f));

            var keyColor = new Color(0.1f, 0.2f, 0.32f, 0.95f);
            var cyan = new Color(0.55f, 0.9f, 1f);
            (string label, HubKey.KeyAction action, int slot)[] keys =
            {
                ("◄", HubKey.KeyAction.Left, 0), ("1", HubKey.KeyAction.Slot, 0), ("2", HubKey.KeyAction.Slot, 1),
                ("3", HubKey.KeyAction.Slot, 2), ("►", HubKey.KeyAction.Right, 0),
            };
            float x = gap;
            foreach (var (label, action, slot) in keys)
            {
                var (button, _) = UIButton($"Key {label}", pad.transform, x, gap, key, key, label, 54, keyColor, cyan);
                button.gameObject.AddComponent<HubTouchKey>().Init(input, action, slot);
                x += key + gap;
            }
            var (enter, _) = UIButton("Key START", pad.transform, x + gap, gap, start, key, "START", 46, Amber, new Color(0.06f, 0.08f, 0.12f));
            enter.gameObject.AddComponent<HubTouchKey>().Init(input, HubKey.KeyAction.Enter, 0);
        }

        static void BuildHubHud(Transform desktopCamera)
        {
            var hud = new GameObject("Desktop HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            hud.transform.SetParent(desktopCamera, false);
            hud.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = hud.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;
            HelpBar(hud.transform, "← →  or  1 2 3  choose     Enter  start     R  stand up and walk:  WASD,  right-drag to look,  click the floor",
                    "Tap the keypad or the desk keys     STAND UP, then tap the floor to walk     Drag to look", width: 1300f);
        }
    }
}
