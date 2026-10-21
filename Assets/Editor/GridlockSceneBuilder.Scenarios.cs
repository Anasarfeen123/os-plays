using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gridlock.AR;
using Gridlock.Core;
using Gridlock.XR;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Object = UnityEngine.Object;

namespace Gridlock.Editor
{
    /// <summary>
    /// The two imported scenarios, rebuilt into GRIDLOCK scenes from their original scenes in Assets/Scenarios (left as
    /// imported):
    ///  • GridlockCPUScheduling.unity — the CPU scheduling lab (a first-person desktop game);
    ///  • GridlockMemory.unity — the memory management warehouse (a VR game).
    /// Each gets what every GRIDLOCK scene has: the XR rig with the animated hands, controller hint cards, the wrist and
    /// system menus, teleporting, the scene fade, the audio mixer and settings, plus a first-person player for monitors
    /// and phones. Their scripts are in Assembly-CSharp, which this editor assembly cannot reference, so the scenario
    /// adapters are added by type name.
    /// </summary>
    public static partial class GridlockSceneBuilder
    {
        const string CpuSourcePath = "Assets/Scenarios/CPUScheduling/Scenes/CPUScheduling.unity";
        const string CpuScenePath = "Assets/Scenes/GridlockCPUScheduling.unity";
        const string CpuSceneName = "GridlockCPUScheduling";
        const string MemorySourcePath = "Assets/Scenarios/MemoryManagement/Scenes/MemoryManagement.unity";
        const string MemoryScenePath = "Assets/Scenes/GridlockMemory.unity";
        const string MemorySceneName = "GridlockMemory";
        const string CpuThumbPath = "Assets/UI/Hub/CpuThumb.png";
        const string MemoryThumbPath = "Assets/UI/Hub/MemoryThumb.png";

        // =====================================================================================================
        // CPU scheduling lab
        // =====================================================================================================

        static void BuildCpuScene()
        {
            var scene = EditorSceneManager.OpenScene(CpuSourcePath, OpenSceneMode.Single);
            var player = Root(scene, "Player");
            player.tag = "Player"; // the challenge board's "go there" button moves whatever is tagged Player
            var view = player.GetComponentInChildren<Camera>(true);
            view.tag = "MainCamera";
            player.SetActive(false); // XRModeController picks it or the headset rig

            // The arena (CPU core, ready queue, console, process editor) is assembled at runtime by its manager. The scene's
            // own PhysicalArena root carries the retired tutorial card, which deletes that whole root on Awake; in the
            // original game a first-scene-only hook then made a fresh manager. Here the fresh manager is simply placed.
            Object.DestroyImmediate(Root(scene, "PhysicalArena"));
            AddScenarioComponent(new GameObject("PhysicalArena"), "PhysicalArenaManager");

            var glue = new GameObject("GRIDLOCK Scenario");
            var buttons = glue.AddComponent<ScenarioButtons>();
            buttons.Init("Run / pause", "Step", "Simulator");

            BuildSystems();
            var (mode, _) = BuildScenarioPresentation(player, buttons);

            // The simulator window ([U] / X) is a screen overlay: a floating panel in the headset.
            Root(scene, "SchedulingUI").AddComponent<XRWorldCanvas>();

            var desktop = AddScenarioComponent(glue, "ScenarioDesktopControls",
                ("movement", player.GetComponent("PlayerMovement")), ("interaction", player.GetComponent("PlayerInteraction")),
                ("knobSprite", AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd")));
            AddScenarioComponent(glue, "ScenarioXRInteraction");
            AddScenarioComponent(glue, "CpuScenarioAdapter", ("buttons", buttons), ("desktop", desktop));
            AddScenarioComponent(glue, "CpuLabLayout"); // desks, screens and labels re-placed once the arena has built
            // Labels sized to be read and kept inside their boxes; floating ones on cards.
            AddScenarioComponent(glue, "ScenarioTextFit", ("cardSprite", AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd")));

            MakeFloorsTeleportable(scene);
            BuildScenarioHud(player.transform);
            BuildRoomARRig(mode, player.transform.Find("GRIDLOCK HUD"), new ARDioramaSettings
            {
                subject = "CPU lab",
                placedHint = "Tap a desk to pick an algorithm, the console to run it",
                helpText = "Tap desks, console and blocks    Pinch  resize    Twist  rotate    Drag  move",
                bounds = new Bounds(new Vector3(0f, 2f, 0f), new Vector3(21.6f, 5f, 21.6f)),
                scale = 30f,
                scaleRange = new Vector2(8f, 70f),
                ceilingCut = 3.6f,
                cutawayBand = 1.2f,
            }, new Rect(-10.25f, -10.25f, 20.5f, 20.5f), 0f, "CPU SCHEDULING LAB", new Color(0.66f, 0.33f, 0.97f), buttons,
               fillLight: 1.3f);
            BuildAudioSystems(null);
            var restart = glue.AddComponent<SceneRestart>();
            BuildSystemMenu(mode, player.transform, "CPU SCHEDULING", isHome: false, tour: null, restart.Reload, CpuHowTo);

            // The arena is assembled at runtime, so the picture is the lab from a high corner.
            var lab = scene.GetRootGameObjects().First(r => r.name == "OS_Lab").GetComponentsInChildren<Transform>(true).First(t => t.name == "Floor")
                .GetComponent<Renderer>().bounds;
            var corner = lab.center + new Vector3(-lab.extents.x * 0.8f, 2.3f, -lab.extents.z * 0.85f);
            RenderThumb(view, CpuThumbPath, corner, Quaternion.LookRotation(lab.center + Vector3.up * 0.6f - corner));
            EditorSceneManager.SaveScene(scene, CpuScenePath);
            Debug.Log($"[GRIDLOCK] Built {CpuScenePath}");
        }

        // =====================================================================================================
        // Memory management warehouse
        // =====================================================================================================

        static void BuildMemoryScene()
        {
            var scene = EditorSceneManager.OpenScene(MemorySourcePath, OpenSceneMode.Single);
            var oldRig = Root(scene, "XR Origin (VR)");
            var (start, facing) = (FloorUnder(oldRig.transform.position), Quaternion.Euler(0f, oldRig.transform.eulerAngles.y, 0f));

            // The scenario's own rig, simulator, interaction manager and event system make way for GRIDLOCK's.
            var removed = new List<GameObject> { oldRig };
            removed.AddRange(scene.GetRootGameObjects().Where(r =>
                r.name is "XRInteractionManager" or "EventSystem" || r.name.StartsWith("XR Device Simulator") ||
                PrefabUtility.IsPrefabAssetMissing(r) || r.GetComponent<EventSystem>() != null));

            var player = BuildFirstPersonPlayer(start, facing);
            var view = player.GetComponentInChildren<Camera>(true);

            var glue = new GameObject("GRIDLOCK Scenario");
            var buttons = glue.AddComponent<ScenarioButtons>();
            buttons.Init(null, null, null); // grabbing and the screens are all there is

            BuildSystems();
            var (mode, rig) = BuildScenarioPresentation(player, buttons);
            RemapReferences(scene, removed, rig.GetComponent<XROrigin>().Camera);
            foreach (var go in removed.Distinct()) Object.DestroyImmediate(go);

            // The boxes were on the scenario's own "Grabable" interaction layer, which GRIDLOCK's hands and controllers do
            // not grab: add Default so the rig can pick them up. Smooth, non-tunnelling physics while they are carried and thrown.
            var grabs = Object.FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            FixEmptyMeshColliders(scene, grabs);
            foreach (var grab in grabs)
            {
                grab.interactionLayers |= UnityEngine.XR.Interaction.Toolkit.InteractionLayerMask.GetMask("Default");
                // Their physics layer (8, "Grabable" in the original project) has no name here and the rig's hands only
                // feel Default: without this, a hand closing on a box found nothing to grab.
                grab.gameObject.layer = 0;
                foreach (var c in grab.GetComponentsInChildren<Collider>(true)) c.gameObject.layer = 0;
                grab.useDynamicAttach = true; // held where the hand closed on it
                // Grabbed with the ray (point and grip), the box comes to the hand instead of hanging at the ray's end.
                grab.farAttachMode = UnityEngine.XR.Interaction.Toolkit.Attachment.InteractableFarAttachMode.Near;
                grab.throwOnDetach = true;
                var body = grab.GetComponent<Rigidbody>();
                if (body == null) continue;
                body.mass = 3f;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            }

            WireScenarioScreens();

            // Desktop and phone: look at (or tap) a box to pick it up, then a pallet to put it there.
            var manager = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                              .FirstOrDefault(b => b.GetType().Name == "MemoryPlacementManager")
                          ?? throw new InvalidOperationException("Memory scene has no MemoryPlacementManager");
            var pageBoxes = new SerializedObject(manager).FindProperty("pageBoxes");
            var boxes = new List<Object>();
            var pages = new List<int>();
            for (int i = 0; i < pageBoxes.arraySize; i++)
            {
                var entry = pageBoxes.GetArrayElementAtIndex(i);
                var box = entry.FindPropertyRelative("box").objectReferenceValue;
                if (box == null) continue;
                boxes.Add(box);
                pages.Add(entry.FindPropertyRelative("pageNumber").intValue);
            }
            var zones = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(b => b.GetType().Name == "MemoryFrameZone").ToArray();
            RegroupFramePallets(zones);
            var adapter = AddScenarioComponent(glue, "MemoryScenarioAdapter", ("manager", manager), ("carryAnchor", view.transform));
            var so = new SerializedObject(adapter);
            SetArray(so.FindProperty("boxes"), boxes.Count, (p, i) => p.objectReferenceValue = boxes[i]);
            SetArray(so.FindProperty("pages"), pages.Count, (p, i) => p.intValue = pages[i]);
            SetArray(so.FindProperty("frames"), zones.Length, (p, i) => p.objectReferenceValue = zones[i].transform.parent);
            SetArray(so.FindProperty("frameIndices"), zones.Length, (p, i) => p.intValue = new SerializedObject(zones[i]).FindProperty("frameIndex").intValue);
            SetArray(so.FindProperty("zones"), zones.Length, (p, i) => p.objectReferenceValue = zones[i]);
            so.ApplyModifiedPropertiesWithoutUndo();

            // The feedback card follows the head: low and to the right, clear of the screens and their buttons.
            var follower = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .First(b => b.GetType().Name == "PlayerFeedbackFollower");
            var followerSo = new SerializedObject(follower);
            followerSo.FindProperty("localOffset").vector3Value = new Vector3(0.62f, -0.42f, 1.4f);
            followerSo.ApplyModifiedPropertiesWithoutUndo();
            AddScenarioComponent(glue, "ScenarioDesktopControls",
                ("movement", player.GetComponent("PlayerMovement")), ("interaction", player.GetComponent("PlayerInteraction")),
                ("knobSprite", AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd")));

            MakeFloorsTeleportable(scene);
            BuildScenarioHud(player.transform);
            BuildRoomARRig(mode, player.transform.Find("GRIDLOCK HUD"), new ARDioramaSettings
            {
                subject = "warehouse",
                placedHint = "Tap a page box, then the pallet of a frame",
                helpText = "Tap a box, then a pallet    Pinch  resize    Twist  rotate    Drag  move",
                bounds = new Bounds(new Vector3(8.1f, 3.3f, 5.4f), new Vector3(17.4f, 7.4f, 12f)),
                scale = 24f,
                scaleRange = new Vector2(6f, 60f),
                ceilingCut = 6f,
                cutawayBand = 1f,
            }, new Rect(-0.1f, -0.1f, 16.4f, 11f), 0f, "MEMORY WAREHOUSE", new Color(0.98f, 0.45f, 0.2f));
            BuildAudioSystems(null);
            var restart = glue.AddComponent<SceneRestart>();
            BuildSystemMenu(mode, player.transform, "MEMORY MANAGEMENT", isHome: false, tour: null, restart.Reload, MemoryHowTo);

            RenderThumb(view, MemoryThumbPath);
            EditorSceneManager.SaveScene(scene, MemoryScenePath);
            Debug.Log($"[GRIDLOCK] Built {MemoryScenePath} (boxes {boxes.Count}, frames {zones.Length})");
        }

        /// <summary>
        /// The warehouse kit's mesh colliders lost their meshes on import, so they collide with nothing: the frame pallets
        /// on the rack let a released box fall straight through to the floor. A frame's pallet gets a box collider around
        /// its model (a pallet is a flat slab); every other empty one is removed (a box around a whole rack or shelf would
        /// wall off the space in front of it).
        /// </summary>
        static void FixEmptyMeshColliders(UnityEngine.SceneManagement.Scene scene,
                                          UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable[] grabs)
        {
            int boxed = 0, removed = 0;
            foreach (var mc in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MeshCollider>(true)).Where(m => m.sharedMesh == null).ToArray())
            {
                var go = mc.gameObject;
                bool framePallet = go.name.StartsWith("EuroPallet") && go.transform.parent != null &&
                                   go.transform.parent.GetComponentsInChildren<MonoBehaviour>(true).Any(b => b != null && b.GetType().Name == "MemoryFrameZone");
                bool hasOther = !framePallet || go.GetComponents<Collider>().Any(c => c != mc);
                Object.DestroyImmediate(mc);
                Bounds? local = null;
                foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh != null))
                {
                    var mb = mf.sharedMesh.bounds;
                    for (int corner = 0; corner < 8; corner++)
                    {
                        var p = mb.center + Vector3.Scale(mb.extents, new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                        p = go.transform.InverseTransformPoint(mf.transform.TransformPoint(p));
                        if (local is Bounds b) { b.Encapsulate(p); local = b; }
                        else local = new Bounds(p, Vector3.zero);
                    }
                }
                if (hasOther || local == null) { removed++; continue; }
                var box = go.AddComponent<BoxCollider>();
                box.center = local.Value.center;
                box.size = local.Value.size;
                boxed++;
            }
            foreach (var grab in grabs) grab.colliders.RemoveAll(c => c == null);
            Debug.Log($"[GRIDLOCK] {scene.path}: {boxed} empty mesh colliders boxed, {removed} removed");
        }

        /// <summary>
        /// In the imported scene two frames' pallets sit under each other's frame object (Frame2's pallets are under the
        /// Frame3 trigger and the other way round), so pointing at a pallet chose the wrong frame. Each pallet joins the
        /// frame whose trigger is above it.
        /// </summary>
        static void RegroupFramePallets(MonoBehaviour[] zones)
        {
            var frameRoots = zones.Select(z => z.transform.parent).Distinct().ToArray();
            foreach (var pallet in frameRoots.SelectMany(r => r.Cast<Transform>()).Where(t => t.name.StartsWith("EuroPallet")).ToArray())
            {
                var renderers = pallet.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) continue;
                var centre = renderers.Aggregate(renderers[0].bounds, (b, r) => { b.Encapsulate(r.bounds); return b; }).center;
                var zone = zones.FirstOrDefault(z =>
                {
                    var t = z.GetComponent<Collider>().bounds;
                    return centre.x >= t.min.x && centre.x <= t.max.x;
                });
                if (zone == null || pallet.parent == zone.transform.parent) continue;
                Debug.Log($"[GRIDLOCK] {pallet.name}: {pallet.parent.name} -> {zone.transform.parent.name}");
                pallet.SetParent(zone.transform.parent, true);
            }
        }

        /// <summary>
        /// Scenario screens for the controller rays. UI raycasts each canvas separately: buttons on a nested canvas (the
        /// warehouse's algorithm and quiz screens) are only found by a raycaster on that nested canvas, so every canvas
        /// with buttons in a world-space hierarchy gets an XR raycaster. Canvases with nothing to press (box and rack
        /// labels, the floating feedback card) stop catching rays, so they never hide a box or a button behind them.
        /// </summary>
        static void WireScenarioScreens()
        {
            int wired = 0, cleared = 0;
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (canvas.GetComponentInParent<XROrigin>(true) != null) continue;
                if (canvas.GetComponentsInParent<Canvas>(true).Last().renderMode != RenderMode.WorldSpace) continue;
                bool Own(Component c) => c.GetComponentInParent<Canvas>(true) == canvas;
                if (canvas.GetComponentsInChildren<Selectable>(true).Any(Own))
                {
                    if (canvas.GetComponent<TrackedDeviceGraphicRaycaster>() == null) canvas.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
                    if (canvas.GetComponent<GraphicRaycaster>() == null) canvas.gameObject.AddComponent<GraphicRaycaster>();
                    wired++;
                    continue;
                }
                foreach (var raycaster in canvas.GetComponents<BaseRaycaster>()) Object.DestroyImmediate(raycaster);
                foreach (var graphic in canvas.GetComponentsInChildren<Graphic>(true).Where(Own)) graphic.raycastTarget = false;
                cleared++;
            }
            Debug.Log($"[GRIDLOCK] screens: {wired} canvases take rays, {cleared} display-only");
        }

        // =====================================================================================================
        // Shared
        // =====================================================================================================

        static GameObject Root(UnityEngine.SceneManagement.Scene scene, string name) =>
            scene.GetRootGameObjects().FirstOrDefault(r => r.name == name)
            ?? throw new InvalidOperationException($"{scene.path} has no root '{name}'");

        /// <summary>
        /// The headset rig next to the scenario's first-person player (which is the monitor/phone view), both starting where
        /// the player stands. A/B/X on the controllers go to <paramref name="buttons"/>.
        /// </summary>
        static (XRModeController, GameObject) BuildScenarioPresentation(GameObject player, ScenarioButtons buttons)
        {
            var start = FloorUnder(player.transform.position);
            var facing = Quaternion.Euler(0f, player.transform.eulerAngles.y, 0f);
            var rig = BuildXRRig(start, facing, seated: false, (panel, keys, origin) =>
            {
                void Wire(UnityEngine.Events.UnityEvent e, UnityEngine.Events.UnityAction press, string label)
                {
                    if (label != null) UnityEditor.Events.UnityEventTools.AddPersistentListener(e, press);
                }
                Wire(keys.ButtonA, buttons.PressA, buttons.Label(nameof(ScenarioButtons.PressA)));
                Wire(keys.ButtonB, buttons.PressB, buttons.Label(nameof(ScenarioButtons.PressB)));
                Wire(keys.ButtonX, buttons.PressX, buttons.Label(nameof(ScenarioButtons.PressX)));
            });
            rig.tag = "Player";
            var simulator = AssetDatabase.LoadAssetAtPath<GameObject>($"{XRIDir}/XR Interaction Simulator/XR Interaction Simulator.prefab");
            var mode = new GameObject("XR Mode").AddComponent<XRModeController>();
            mode.Init(rig, player, simulator);
            return (mode, rig);
        }

        /// <summary>The CPU lab's first-person player (WASD, mouse look, crosshair + [E]) for scenes that had none.</summary>
        static GameObject BuildFirstPersonPlayer(Vector3 feet, Quaternion facing)
        {
            var player = new GameObject("Player");
            player.tag = "Player";
            player.transform.SetPositionAndRotation(feet + Vector3.up * 0.05f, facing);
            var body = player.AddComponent<CharacterController>();
            body.height = 1.8f;
            body.radius = 0.3f;
            body.center = new Vector3(0f, 0.9f, 0f);
            AddScenarioComponent(player, "PlayerMovement");
            AddScenarioComponent(player, "PlayerInteraction");
            var eye = new GameObject("Camera", typeof(Camera), typeof(AudioListener));
            eye.tag = "MainCamera";
            eye.transform.SetParent(player.transform, false);
            eye.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            var cam = eye.GetComponent<Camera>();
            cam.fieldOfView = 70f;
            cam.nearClipPlane = 0.05f;
            ConfigureCamera(cam);
            player.SetActive(false);
            return player;
        }

        /// <summary>The hub tile picture: what the player sees on arrival (rendered once; delete the file to redo it).</summary>
        static void RenderThumb(Camera view, string path) => RenderThumb(view, path, view.transform.position, view.transform.rotation);

        static void RenderThumb(Camera view, string path, Vector3 position, Quaternion rotation)
        {
            if (File.Exists(path)) return;
            const int w = 1040, h = 580;
            var go = new GameObject("Thumb Camera", typeof(Camera));
            var cam = go.GetComponent<Camera>();
            cam.CopyFrom(view); // (copies the pose too)
            go.transform.SetPositionAndRotation(position, rotation);
            cam.fieldOfView = 62f;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            RenderTexture.active = null;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(go);
            AssetDatabase.ImportAsset(path);
        }

        /// <summary>A scenario script (Assembly-CSharp) by name, with serialized fields set.</summary>
        static Component AddScenarioComponent(GameObject go, string typeName, params (string field, Object value)[] fields)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(typeName)).FirstOrDefault(t => t != null)
                       ?? throw new InvalidOperationException($"Scenario script {typeName} is missing (Assets/Scenarios)");
            var component = go.AddComponent(type);
            if (fields.Length == 0) return component;
            var so = new SerializedObject(component);
            foreach (var (field, value) in fields)
            {
                var property = so.FindProperty(field) ?? throw new InvalidOperationException($"{typeName} has no field {field}");
                property.objectReferenceValue = value;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return component;
        }

        static void SetArray(SerializedProperty array, int count, Action<SerializedProperty, int> set)
        {
            array.arraySize = count;
            for (int i = 0; i < count; i++) set(array.GetArrayElementAtIndex(i), i);
        }

        /// <summary>The floor point below <paramref name="p"/> (the scenario rigs float a little above it).</summary>
        static Vector3 FloorUnder(Vector3 p)
        {
            Physics.SyncTransforms();
            // Not the player's own capsule, nor anything on a rig.
            var floor = Physics.RaycastAll(p + Vector3.up * 0.5f, Vector3.down, 5f)
                .Where(h => !h.collider.isTrigger && h.collider is not CharacterController && h.collider.GetComponentInParent<XROrigin>(true) == null)
                .OrderBy(h => h.distance).ToArray();
            return floor.Length > 0 ? floor[0].point : new Vector3(p.x, 0f, p.z);
        }

        /// <summary>Wide, flat, low colliders (floors and ground) become teleport targets.</summary>
        static void MakeFloorsTeleportable(UnityEngine.SceneManagement.Scene scene)
        {
            int count = 0;
            foreach (var col in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Collider>(true)))
            {
                if (col.isTrigger || col.GetComponentInParent<XROrigin>(true) != null) continue;
                var b = col.bounds;
                bool flat = b.size.y < 0.4f && b.size.x * b.size.z > 6f && b.max.y < 0.6f;
                bool named = col.name.IndexOf("floor", StringComparison.OrdinalIgnoreCase) >= 0 || col.name.IndexOf("ground", StringComparison.OrdinalIgnoreCase) >= 0;
                if (!flat && !(named && b.max.y < 0.6f && b.size.x * b.size.z > 1f)) continue;
                if (col.GetComponent<UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationArea>() != null) continue;
                Teleportable(col.gameObject);
                count++;
            }
            if (count == 0) throw new InvalidOperationException($"{scene.path}: no floor to teleport on");
            Debug.Log($"[GRIDLOCK] {scene.path}: {count} teleport floors");
        }

        /// <summary>
        /// Serialized references into objects that are about to be removed (the scenario's own XR rig): the headset camera
        /// maps to GRIDLOCK's rig camera, anything else is cleared (and listed in the log).
        /// </summary>
        static void RemapReferences(UnityEngine.SceneManagement.Scene scene, List<GameObject> removed, Camera rigCamera)
        {
            var doomed = new HashSet<Object>();
            foreach (var go in removed)
                foreach (var t in go.GetComponentsInChildren<Transform>(true))
                {
                    doomed.Add(t.gameObject);
                    foreach (var c in t.GetComponents<Component>()) if (c != null) doomed.Add(c);
                }

            foreach (var component in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Component>(true)))
            {
                if (component == null || doomed.Contains(component) || component is Transform) continue;
                var so = new SerializedObject(component);
                var it = so.GetIterator();
                bool changed = false;
                while (it.Next(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference || it.objectReferenceValue == null) continue;
                    var target = it.objectReferenceValue;
                    if (!doomed.Contains(target)) continue;
                    Object replacement = null;
                    if (component is Canvas) { } // world canvases without a camera use whichever main camera is live
                    else if (rigCamera != null)
                    {
                        if (target is Camera) replacement = rigCamera;
                        else if (target is Transform t && t.GetComponent<Camera>() != null) replacement = rigCamera.transform;
                        else if (target is GameObject g && g.GetComponent<Camera>() != null) replacement = rigCamera.gameObject;
                    }
                    Debug.Log($"[GRIDLOCK] {component.GetType().Name}.{it.propertyPath} on {component.name}: {target.name} -> {(replacement != null ? replacement.name : "none")}");
                    it.objectReferenceValue = replacement;
                    changed = true;
                }
                if (changed) so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        /// <summary>A small ≡ MENU button in the corner for monitors and phones (Esc / back open the menu too).</summary>
        static void BuildScenarioHud(Transform player)
        {
            var hud = new GameObject("GRIDLOCK HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            hud.transform.SetParent(player, false);
            var canvas = hud.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;
            var scaler = hud.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;
            var corner = new GameObject("Corner", typeof(RectTransform));
            var rt = (RectTransform)corner.transform;
            rt.SetParent(hud.transform, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-24f, -24f);
            rt.sizeDelta = new Vector2(220f, 104f);
            MenuLinkButton(corner.transform, 0, 0, 220, 64, 26);
            UIText("Hint", corner.transform, 0, 68, 220, 30, "Esc", 18, Muted, TextAlignmentOptions.Center);
        }

        const string CpuHowTo =
            "<b><color=#FBBF24>THE IDEA</color></b>\n" +
            "Processes (the blocks on the arena floor) wait in the <b>ready queue</b> for the one <b>CPU core</b>. The scheduler " +
            "picks who runs next: FCFS, SRTF, Priority or Round Robin. The Gantt board shows the order and the waiting times.\n\n" +
            "<b><color=#FBBF24>CONTROLS</color></b>\n" +
            "Use a <b>workstation desk</b> to choose an algorithm, the <b>console</b> to run, step or reset, the <b>process editor</b> " +
            "to change bursts, and the <b>challenge wall</b> for the quiz.\n" +
            "<b>Desktop</b>  WASD walk, mouse look, <b>E</b> or click to use what the dot points at, <b>U</b> simulator window.\n" +
            "<b>Phone</b>  thumbstick to walk, drag to look, tap to use.\n" +
            "<b>Headset</b>  point and pull the trigger (or pinch) to use. <b>A</b> run / pause, <b>B</b> step, <b>X</b> simulator window.\n" +
            HeadsetHowTo;

        const string MemoryHowTo =
            "<b><color=#FBBF24>THE IDEA</color></b>\n" +
            "The pallets are the <b>frames</b> of physical memory, the numbered boxes are <b>pages</b>. Load the pages in the order " +
            "of the reference string. When every frame is full, the algorithm picks the victim: <b>FIFO</b> the oldest, <b>LRU</b> the " +
            "least recently used, <b>Optimal</b> the one needed furthest in the future.\n\n" +
            "<b><color=#FBBF24>CONTROLS</color></b>\n" +
            "<b>Headset</b>  grab a box with the grip (close by, or point at it) and let go of it on a frame's pallet, or just in " +
            "front of the rack below it. A wrong frame sends the box back to its table. Press the screens with the ray (trigger).\n" +
            "<b>Desktop</b>  WASD walk, mouse look, <b>E</b> or click a box to pick it up, then a pallet to put it there.\n" +
            "<b>Phone</b>  drag to look, two fingers to walk, tap a box, then a pallet.\n" +
            HeadsetHowTo;
    }
}
