using System;
using System.Linq;
using System.Reflection;
using Gridlock.Core;
using Gridlock.Guide;
using Gridlock.UI;
using Gridlock.XR;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Gridlock.Editor
{
    /// <summary>
    /// The system menu (pause, home, settings, how to play) and the audio plumbing every scene shares: the GRIDLOCK
    /// mixer with Music / Effects / Voice groups, the router that sends every sound through it, and the scene's music cue.
    /// </summary>
    public static partial class GridlockSceneBuilder
    {
        const string MixerPath = "Assets/Audio/GridlockMixer.mixer";
        const float MenuW = 1200f, MenuH = 720f;

        // =====================================================================================================
        // Audio: mixer, routing, music
        // =====================================================================================================

        /// <summary>Mixer asset with Music, Effects and Voice under Master, each volume exposed for the settings sliders.</summary>
        static (AudioMixer mixer, AudioMixerGroup music, AudioMixerGroup effects, AudioMixerGroup voice) EnsureMixer()
        {
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (mixer == null) mixer = CreateMixer();
            AudioMixerGroup Group(string name) => mixer.FindMatchingGroups(name).First(g => g.name == name);
            return (mixer, Group("Music"), Group("Effects"), Group("Voice"));
        }

        /// <summary>
        /// Unity has no public API to author a mixer, so this drives the editor's own AudioMixerController (the same calls the
        /// Audio Mixer window makes). Only runs when the asset does not exist yet.
        /// </summary>
        static AudioMixer CreateMixer()
        {
            const BindingFlags any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            var editor = typeof(UnityEditor.Editor).Assembly;
            var controllerType = editor.GetType("UnityEditor.Audio.AudioMixerController", throwOnError: true);
            var groupType = editor.GetType("UnityEditor.Audio.AudioMixerGroupController", throwOnError: true);
            var pathType = editor.GetType("UnityEditor.Audio.AudioGroupParameterPath", throwOnError: true);

            var controller = controllerType.GetMethod("CreateMixerControllerAtPath", any).Invoke(null, new object[] { MixerPath });
            var master = controllerType.GetProperty("masterGroup", any).GetValue(controller);
            var exposed = controllerType.GetProperty("exposedParameters", any);

            foreach (var (group, parameter) in new[] { ("Music", "MusicVolume"), ("Effects", "EffectsVolume"), ("Voice", "VoiceVolume") })
            {
                var g = controllerType.GetMethod("CreateNewGroup", any).Invoke(controller, new object[] { group, false });
                controllerType.GetMethod("AddChildToParent", any).Invoke(controller, new[] { g, master });

                var volume = groupType.GetMethod("GetGUIDForVolume", any).Invoke(g, null);
                var path = Activator.CreateInstance(pathType, any, null, new[] { g, volume }, null);
                controllerType.GetMethod("AddExposedParameter", any).Invoke(controller, new[] { path });

                // Rename the new exposed parameter so scripts can address it.
                var list = (Array)exposed.GetValue(controller);
                for (int i = 0; i < list.Length; i++)
                {
                    var entry = list.GetValue(i);
                    var guidField = entry.GetType().GetField("guid", any);
                    if (!guidField.GetValue(entry).Equals(volume)) continue;
                    entry.GetType().GetField("name", any).SetValue(entry, parameter);
                    list.SetValue(entry, i);
                }
                exposed.SetValue(controller, list);
            }

            EditorUtility.SetDirty((Object)controller);
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
        }

        /// <summary>Mixer routing and the scene's background music track (Assets/Audio/Music/<paramref name="track"/>.ogg; null for none).</summary>
        static void BuildAudioSystems(string track)
        {
            var (mixer, music, effects, voice) = EnsureMixer();
            var go = new GameObject("Audio");
            go.AddComponent<AudioRouting>().Init(mixer, music, effects, voice);
            go.AddComponent<SettingsApplier>();
            ConfigureAudioImport();
            if (track == null) return;
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Audio/Music/{track}.ogg");
            if (clip == null) throw new InvalidOperationException($"Missing music track {track} (run Tools/synth_music.py)");
            go.AddComponent<MusicCue>().Init(clip);
        }

        /// <summary>
        /// Quest memory and frame time: music and long ambience stream from disk; narration and the scenarios' voice lines
        /// stay compressed in memory; short effects are decoded once. Everything that does not stream loads with its scene,
        /// so the first time a clip plays never stalls a frame on loading it.
        /// </summary>
        static void ConfigureAudioImport()
        {
            void Apply(string folder, Func<string, AudioClipLoadType?> load, float? quality)
            {
                foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { folder }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                    var settings = importer.defaultSampleSettings;
                    if (load(path) is not AudioClipLoadType type) continue;
                    bool preload = type != AudioClipLoadType.Streaming;
                    float q = quality ?? settings.quality;
                    if (settings.loadType == type && settings.preloadAudioData == preload && Mathf.Approximately(settings.quality, q)) continue;
                    settings.loadType = type;
                    settings.compressionFormat = AudioCompressionFormat.Vorbis;
                    settings.quality = q;
                    settings.preloadAudioData = preload;
                    importer.defaultSampleSettings = settings;
                    importer.SaveAndReimport();
                }
            }
            static AudioClipLoadType? BySize(string path)
            {
                long bytes = new System.IO.FileInfo(path).Length;
                return bytes > 1_500_000 ? AudioClipLoadType.Streaming
                    : bytes > 300_000 ? AudioClipLoadType.CompressedInMemory
                    : AudioClipLoadType.DecompressOnLoad;
            }
            Apply("Assets/Audio/Music", _ => AudioClipLoadType.Streaming, 0.7f);
            Apply(NarrationDir, _ => AudioClipLoadType.CompressedInMemory, 0.8f);
            foreach (var folder in new[] { "Assets/Audio", "Assets/Scenarios" })
                Apply(folder, path => path.StartsWith("Assets/Audio/Music/") || path.StartsWith(NarrationDir + "/") ? null : BySize(path), null);
        }

        // =====================================================================================================
        // System menu
        // =====================================================================================================

        /// <summary>
        /// The pause menu: a dimmed overlay on the desktop (under the desktop camera, so XR never shows it) and a floating
        /// panel for the headset. <paramref name="restart"/> is null where there is nothing to restart.
        /// </summary>
        /// <param name="switchLabel">Button that loads the scene's sister mode (Deadlock ⇄ Banker); null for none.</param>
        static SystemMenu BuildSystemMenu(XRModeController mode, Transform desktopCamera, string sceneTitle, bool isHome, NarratedTour tour,
                                          UnityAction restart, string howToPlay, string switchLabel = null, string switchScene = null)
        {
            var root = new GameObject("System Menu");
            var menu = root.AddComponent<SystemMenu>();

            // Desktop: full-screen dim + centred panel, drawn above every HUD.
            var overlay = new GameObject("Menu Overlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            overlay.transform.SetParent(desktopCamera, false);
            var canvas = overlay.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = overlay.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;
            var dim = new GameObject("Dim", typeof(RectTransform), typeof(Image));
            var dimRect = (RectTransform)dim.transform;
            dimRect.SetParent(overlay.transform, false);
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = dimRect.offsetMax = Vector2.zero;
            dim.GetComponent<Image>().color = new Color(0.01f, 0.02f, 0.05f, 0.72f);
            var centre = new GameObject("Panel", typeof(RectTransform));
            var crt = (RectTransform)centre.transform;
            crt.SetParent(overlay.transform, false);
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.pivot = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(MenuW, MenuH);
            BuildMenuView(centre.transform, menu, sceneTitle, howToPlay, switchLabel, switchScene);

            // Headset: a world panel placed in front of the player when opened.
            var xr = WorldCanvas("Menu (XR)", root.transform, new Vector2(MenuW, MenuH), 0.0007f); // 84 × 50 cm, poke and ray
            BuildMenuView(xr.transform, menu, sceneTitle, howToPlay, switchLabel, switchScene);

            menu.Init(overlay, xr, mode, HubSceneName, isHome, tour,
                      Clip("Kenney/maximize_006.ogg"), Clip("Kenney/minimize_006.ogg"), Clip("Kenney/click_002.ogg"));
            if (restart != null) UnityEditor.Events.UnityEventTools.AddPersistentListener(menu.RestartEvent, restart);
            return menu;
        }

        static void BuildMenuView(Transform parent, SystemMenu menu, string sceneTitle, string howToPlay, string switchLabel, string switchScene)
        {
            var frame = Bordered("Frame", parent, 0, 0, MenuW, MenuH, Opaque(ViewColor.Panel), GuideCyan);
            var t = frame.transform;
            Panel("Sidebar", t, 5, 5, 375, MenuH - 10, new Color(0.08f, 0.11f, 0.19f));

            Logo(t, 36, 26, 76);
            var title = UIText("Title", t, 120, 30, 240, 70, "OS PLAYS", 50, Amber, TextAlignmentOptions.Left, FontStyles.Bold);
            title.characterSpacing = 3f;
            FitOneLine(title, 32f);
            var subtitle = UIText("Scene", t, 42, 100, 320, 34, sceneTitle, 22, GuideCyan, TextAlignmentOptions.Left, FontStyles.Bold);
            subtitle.characterSpacing = 3f;
            UIText("Paused", t, 42, 130, 320, 28, "PAUSED", 18, Muted, TextAlignmentOptions.Left, FontStyles.Bold).characterSpacing = 6f;

            // Navigation: a vertical stack, so entries a scene does not use collapse without leaving gaps.
            var nav = new GameObject("Nav", typeof(RectTransform), typeof(VerticalLayoutGroup));
            Rect(nav, t, 40, 176, 300, 510);
            var layout = nav.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlHeight = layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            Button NavButton(string label, Color colour, Color text)
            {
                var (b, _) = UIButton(label, nav.transform, 0, 0, 300, 56, label, 26, colour, text);
                b.gameObject.AddComponent<LayoutElement>().preferredHeight = 56f;
                return b;
            }
            var idle = new Color(0.12f, 0.16f, 0.26f);
            var resume = NavButton("RESUME", Amber, new Color(0.06f, 0.08f, 0.12f));
            var tour = NavButton("GUIDED TOUR", idle, ViewColor.Text);
            var restart = NavButton("RESTART", idle, ViewColor.Text);
            if (switchLabel != null) NavButton(switchLabel, idle, GuideCyan).gameObject.AddComponent<ModeSwitch>().Init(switchScene);
            var home = NavButton("◄ CONTROL ROOM", idle, ViewColor.Text);
            var settings = NavButton("SETTINGS", idle, ViewColor.Text);
            var help = NavButton("HOW TO PLAY", idle, ViewColor.Text);
            var quit = NavButton("QUIT", new Color(0.32f, 0.08f, 0.1f), ViewColor.Text);

            // Pages on the right.
            var settingsPage = BuildSettingsPage(t);
            var helpPage = Group("HelpPage", t);
            UIText("Heading", helpPage.transform, 420, 40, 740, 56, "HOW TO PLAY", 40, ViewColor.Text, TextAlignmentOptions.Left, FontStyles.Bold).characterSpacing = 3f;
            var body = UIText("Body", helpPage.transform, 420, 112, 740, 540, howToPlay, 25, ViewColor.Text, TextAlignmentOptions.TopLeft);
            body.lineSpacing = 6f;
            body.textWrappingMode = TextWrappingModes.Normal;
            body.enableAutoSizing = true;
            body.fontSizeMin = 16f;
            body.fontSizeMax = 25f;

            UIText("Hint", t, 420, MenuH - 54, 740, 34, "Esc  ·  ≡ button  ·  RESUME   to return", 18, Muted, TextAlignmentOptions.Right);

            parent.gameObject.AddComponent<SystemMenuView>().Init(menu, resume, tour, restart, home, settings, help, quit,
                                                                 settingsPage, helpPage);
        }

        static readonly Color RowColor = new Color(0.09f, 0.12f, 0.2f);
        static readonly Color SegmentColor = new Color(0.12f, 0.16f, 0.26f);

        /// <summary>SETTINGS: tabs of self-binding rows (sliders and segmented choices), RESET ALL.</summary>
        static GameObject BuildSettingsPage(Transform t)
        {
            var page = Group("SettingsPage", t);
            var p = page.transform;
            UIText("Heading", p, 420, 40, 400, 56, "SETTINGS", 40, ViewColor.Text, TextAlignmentOptions.Left, FontStyles.Bold).characterSpacing = 3f;
            var (reset, _) = UIButton("ResetAll", p, 1000, 46, 160, 46, "RESET ALL", 20, new Color(0.32f, 0.08f, 0.1f), ViewColor.Text);

            string[] tabNames = { "AUDIO", "DISPLAY", "CONTROLS", "GAMEPLAY" };
            var tabs = new Button[tabNames.Length];
            var pages = new GameObject[tabNames.Length];
            for (int i = 0; i < tabNames.Length; i++)
            {
                tabs[i] = UIButton("Tab" + tabNames[i], p, 420 + i * 187, 112, 177, 54, tabNames[i], 22, SegmentColor, ViewColor.Text).Item1;
                pages[i] = Group(tabNames[i] + "Page", p);
            }

            // AUDIO
            var a = pages[0].transform;
            SliderRow(a, 0, "MASTER", "Everything at once", Setting.Master, SettingSlider.Format.Percent);
            SliderRow(a, 1, "MUSIC", "Background music", Setting.Music, SettingSlider.Format.Percent);
            SliderRow(a, 2, "EFFECTS", "Cars, alarms, clicks, city", Setting.Effects, SettingSlider.Format.Percent);
            SliderRow(a, 3, "NARRATOR", "Guided tour voice", Setting.Voice, SettingSlider.Format.Percent);

            // DISPLAY
            var d = pages[1].transform;
            ChoiceRow(d, 0, "GRAPHICS", "Sharpness and detail vs. speed", Setting.Quality,
                      new[] { "PERFORMANCE", "BALANCED", "QUALITY" }, new[] { 0f, 1f, 2f });
            ChoiceRow(d, 1, "POST EFFECTS", "Glow and colour grading", Setting.PostEffects, new[] { "ON", "OFF" }, new[] { 1f, 0f });
            ChoiceRow(d, 2, "SHADOWS", "Sun shadows on the lot", Setting.Shadows, new[] { "ON", "OFF" }, new[] { 1f, 0f });
            ChoiceRow(d, 3, "SUBTITLES", "Captions under the narrator", Setting.Subtitles, new[] { "ON", "OFF" }, new[] { 1f, 0f });
            ChoiceRow(d, 4, "MIXED REALITY", "Quest: your room, the scene as a table model", Setting.MixedReality, new[] { "OFF", "ON" }, new[] { 0f, 1f });

            // CONTROLS
            var c = pages[2].transform;
            SliderRow(c, 0, "LOOK SPEED", "Mouse, finger and orbit speed", Setting.LookSpeed, SettingSlider.Format.Multiplier);
            ChoiceRow(c, 1, "INVERT LOOK", "Up and down reversed", Setting.InvertLook, new[] { "OFF", "ON" }, new[] { 0f, 1f });
            ChoiceRow(c, 2, "VR TURNING", "Right stick left / right", Setting.SmoothTurn, new[] { "SNAP", "SMOOTH" }, new[] { 0f, 1f });
            ChoiceRow(c, 3, "SNAP ANGLE", "Degrees per snap turn", Setting.SnapAngle, new[] { "30°", "45°", "90°" }, new[] { 0f, 1f, 2f });
            ChoiceRow(c, 4, "VR MOVING", "Left stick: walk, or teleport only", Setting.SmoothMove, new[] { "TELEPORT", "WALK" }, new[] { 0f, 1f });

            // GAMEPLAY
            var g = pages[3].transform;
            ChoiceRow(g, 0, "GUIDED TOURS", "Start on a first visit", Setting.AutoTours, new[] { "AUTO", "OFF" }, new[] { 1f, 0f });
            ChoiceRow(g, 1, "CAR SPEED", "How fast the cars drive", Setting.CarSpeed, new[] { "SLOW", "NORMAL", "FAST" }, new[] { 0f, 1f, 2f });
            RowFrame(g, 2, "REPLAY TOURS", "Play every tour again on the next visit");
            var (tours, toursLabel) = UIButton("ShowTours", g, 760, RowY(2) + 12, 380, 52, "SHOW TOURS AGAIN", 22, new Color(0.22f, 0.27f, 0.38f), ViewColor.Text);

            UIText("Saved", p, 420, RowY(5) + 10, 500, 30, "Changes are saved and applied immediately.", 18, Muted, TextAlignmentOptions.Left);
            page.AddComponent<SettingsView>().Init(tabs, pages, reset, tours, toursLabel);
            return page;
        }

        static float RowY(int i) => 188f + i * 86f;

        /// <summary>A row card with a label and a hint; the control sits on its right half.</summary>
        static void RowFrame(Transform parent, int i, string label, string hint)
        {
            float y = RowY(i);
            Panel("Row", parent, 420, y, 740, 76, RowColor);
            UIText("Label", parent, 440, y + 8, 300, 36, label, 24, ViewColor.Text, TextAlignmentOptions.Left, FontStyles.Bold).characterSpacing = 1f;
            UIText("Hint", parent, 440, y + 42, 320, 28, hint, 16, Muted, TextAlignmentOptions.Left);
        }

        static void SliderRow(Transform parent, int i, string label, string hint, Setting setting, SettingSlider.Format format)
        {
            RowFrame(parent, i, label, hint);
            var (slider, value) = MenuSlider(parent, RowY(i) + 8);
            slider.gameObject.AddComponent<SettingSlider>().Init(setting, slider, value, format);
        }

        static void ChoiceRow(Transform parent, int i, string label, string hint, Setting setting, string[] options, float[] values)
        {
            RowFrame(parent, i, label, hint);
            const float x0 = 760f, total = 380f, gap = 8f;
            float w = (total - gap * (options.Length - 1)) / options.Length;
            var buttons = new Button[options.Length];
            for (int k = 0; k < options.Length; k++)
                buttons[k] = UIButton(options[k], parent, x0 + k * (w + gap), RowY(i) + 12, w, 52, options[k], 20, SegmentColor, ViewColor.Text).Item1;
            var host = new GameObject(label + " Choice", typeof(RectTransform));
            host.transform.SetParent(parent, false);
            host.AddComponent<SettingChoice>().Init(setting, buttons, values);
        }

        /// <summary>Slider with a cyan fill, a round knob and the value on the right (inside a settings row).</summary>
        static (Slider, TMP_Text) MenuSlider(Transform parent, float y)
        {
            var go = new GameObject("Slider", typeof(RectTransform));
            Rect(go, parent, 760, y + 22, 290, 34);

            RectTransform Stretch(string name, Transform under, Vector2 inset)
            {
                var child = new GameObject(name, typeof(RectTransform));
                var rt = (RectTransform)child.transform;
                rt.SetParent(under, false);
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = inset;
                rt.offsetMax = -inset;
                return rt;
            }

            var track = Stretch("Track", go.transform, new Vector2(0f, 11f));
            var trackImage = track.gameObject.AddComponent<Image>();
            trackImage.sprite = roundedSprite;
            trackImage.type = Image.Type.Sliced;
            trackImage.color = new Color(1f, 1f, 1f, 0.12f);

            var fillArea = Stretch("Fill Area", go.transform, new Vector2(0f, 11f));
            var fill = Stretch("Fill", fillArea, Vector2.zero);
            var fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.sprite = roundedSprite;
            fillImage.type = Image.Type.Sliced;
            fillImage.color = GuideCyan;

            var handleArea = Stretch("Handle Slide Area", go.transform, new Vector2(14f, 0f));
            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            var hrt = (RectTransform)handle.transform;
            hrt.SetParent(handleArea, false);
            hrt.sizeDelta = new Vector2(34f, 0f);
            var knob = handle.GetComponent<Image>();
            knob.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            knob.color = Color.white;

            var slider = go.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = hrt;
            slider.targetGraphic = knob;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;

            var value = UIText("Value", parent, 1060, y + 14, 90, 50, "", 24, ViewColor.Text, TextAlignmentOptions.Right, FontStyles.Bold);
            return (slider, value);
        }

        /// <summary>A "≡ MENU" button that opens the scene's system menu.</summary>
        static Button MenuLinkButton(Transform parent, float x, float y, float w, float h, float size)
        {
            var (button, _) = UIButton("MenuButton", parent, x, y, w, h, "≡  MENU", size, new Color(0.16f, 0.21f, 0.32f), ViewColor.Text);
            var link = button.gameObject.AddComponent<SystemMenuLink>();
            UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick, link.Toggle);
            return button;
        }

        // =====================================================================================================
        // How-to-play texts
        // =====================================================================================================

        const string HubHowTo =
            "<b><color=#FBBF24>CHOOSE A SCENARIO</color></b>\n" +
            "Use the keyboard on the desk: <b>◄ ►</b> or <b>1 2 3</b> to choose, <b>ENTER</b> to start.\n\n" +
            "<b>Headset</b>  poke the keys with a fingertip, or point and pull the trigger.\n" +
            "<b>Desktop</b>  arrow keys and Enter, or click the desk keys. Move the mouse to look around.\n" +
            "<b>Phone</b>  tap the keypad or the desk keys.\n\n" +
            "<b><color=#FBBF24>EXPLORE THE ROOM</color></b>\n" +
            "<b>Desktop</b>  press <b>R</b> to stand up: WASD to walk, Shift to run, right-drag to look, click the floor to walk there. " +
            "Click the chair or press R to sit down again.\n" +
            "<b>Headset</b>  left stick to walk, right stick to turn and teleport; up the stairs to the gallery. Point at the chair " +
            "(hands: turn your left palm up, SIT AT DESK) to return.\n" +
            "<b>Phone</b>  STAND UP, then tap the floor to walk and drag to look.\n\n" +
            "<b><color=#FBBF24>01 · DEADLOCK</color></b>\n" +
            "A parking lot where cars (processes) compete for bays (resources). Watch a deadlock form, detect it, break it, " +
            "then learn to avoid it with the Banker's Algorithm.\n" +
            "<b><color=#FBBF24>02 · CPU SCHEDULING</color></b>  a lab where processes queue for one CPU core: FCFS, SRTF, Priority, Round Robin.\n" +
            "<b><color=#FBBF24>03 · MEMORY</color></b>  a warehouse where pallets are frames and boxes are pages: FIFO, LRU, Optimal.\n\n" +
            "Open this menu any time with <b>Esc</b>, the <b>≡ MENU</b> button, or the controller's <b>≡</b> button.";

        const string HeadsetHowTo =
            "<b>Headset</b>  left stick walk, right stick turn (push up to teleport). Look at a controller to see its buttons.\n" +
            "<b>Hands</b>  point + pinch to select; pinch the ground and release to teleport; pinch + flick sideways to turn; " +
            "left palm up for MENU.\n" +
            "<b>Mixed reality</b> (Settings → Display)  your room around a table model: right stick resize, left stick turn, " +
            "click the right stick to bring it back in front.";

        const string DeadlockHowTo =
            "<b><color=#FBBF24>THE IDEA</color></b>\n" +
            "Cars are <b>processes</b>, bays are <b>resources</b>. A car keeps its bay until the next one is free. " +
            "If the waits close into a loop, nobody can move: a <b>deadlock</b>.\n\n" +
            "<b><color=#FBBF24>CONTROLS</color></b>\n" +
            "<b>Select a car, then a bay</b> to send it there (click, tap, or point and pull the trigger).\n" +
            "<b>ANALYZE</b> draws who waits for whom. <b>TOW CAR</b> removes the selected car and breaks the loop.\n" +
            "<b>RESTART</b> replays the demo. <b>GUIDED TOUR</b> explains everything in two minutes.\n\n" +
            "<b>Desktop</b>  right-drag to orbit, scroll to zoom.\n" + HeadsetHowTo;

        const string BankerHowTo =
            "<b><color=#FBBF24>THE IDEA</color></b>\n" +
            "You are the parking manager. Parked cars are a fleet's <b>Allocation</b>, cars on the street its <b>Need</b>, " +
            "cars at the gate the <b>Request</b>, and empty bays are <b>Available</b>.\n" +
            "<b>GRANT</b> only if every fleet could still finish afterwards (a safe sequence exists).\n\n" +
            "<b><color=#FBBF24>CONTROLS</color></b>\n" +
            "<b>GRANT</b> / <b>DENY</b> on the board, the side panel or the card above the gate, then <b>NEXT</b>.\n" +
            "Five guided levels, then endless challenges with a speed bonus.\n\n" +
            "<b>Desktop</b>  right-drag to orbit, scroll to zoom.\n" + HeadsetHowTo;
    }
}
