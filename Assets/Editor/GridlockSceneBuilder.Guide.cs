using System;
using System.IO;
using System.Linq;
using Gridlock.Core;
using Gridlock.Deadlock;
using Gridlock.Guide;
using Gridlock.XR;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Gridlock.Editor
{
    /// <summary>
    /// The narrated welcome and guided tour. Text comes from Assets/Audio/Narration/narration.json, the same file
    /// Tools/make_narration.py speaks into one WAV per line, so subtitles and voice never drift apart.
    /// </summary>
    public static partial class GridlockSceneBuilder
    {
        const string NarrationDir = "Assets/Audio/Narration";
        static readonly Color GuideCyan = new Color(0.35f, 0.85f, 1f);

        [Serializable]
        class NarrationFile
        {
            public NarrationLine[] lines;
        }

        [Serializable]
        class NarrationLine
        {
            public string id;
            public string title;
            public string text;
        }

        static NarrationLine[] narration;

        static NarrationLine Line(string id)
        {
            narration ??= JsonUtility.FromJson<NarrationFile>(File.ReadAllText($"{NarrationDir}/narration.json")).lines;
            return narration.FirstOrDefault(l => l.id == id) ?? throw new InvalidOperationException($"narration.json has no line '{id}'");
        }

        /// <summary>Spoken clip for a line, or null before Tools/make_narration.py has been run (the tour then times the text).</summary>
        static AudioClip Voice(string id) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{NarrationDir}/{id}.wav");

        // =====================================================================================================
        // Guided tours
        // =====================================================================================================

        static readonly string[] DeadlockTourIds = { "welcome", "environment", "allocate", "wait", "circle", "detect", "tow", "tow_auto", "unwind", "outro" };
        static readonly string[] CpuTourIds =
        {
            "cpu_welcome", "cpu_processes", "cpu_queue", "cpu_core", "cpu_desks", "cpu_turn", "cpu_hint", "cpu_gantt", "cpu_result", "cpu_outro",
        };
        static readonly string[] MemoryTourIds =
        {
            "mem_welcome", "mem_frames", "mem_pages", "mem_algorithm", "mem_turn", "mem_hint", "mem_fault", "mem_victim", "mem_outro",
        };
        static readonly string[] BankerTourIds =
        {
            "banker_welcome", "banker_fleets", "banker_alloc", "banker_need", "banker_request", "banker_safety",
            "banker_turn", "banker_hint", "banker_right", "banker_wrong", "banker_outro",
        };

        /// <summary>Where a tour's end screen sends the learner next.</summary>
        readonly struct TourEnd
        {
            public readonly string ModeLabel, ModeScene, CloseLabel;
            public TourEnd(string modeLabel, string modeScene, string closeLabel)
            {
                ModeLabel = modeLabel;
                ModeScene = modeScene;
                CloseLabel = closeLabel;
            }
        }

        /// <summary>
        /// Adds tour <typeparamref name="T"/> with its two guide screens (floating for XR, docked in the desktop HUD) and the
        /// marker. Returns the tour so the caller can wire its scene.
        /// </summary>
        static T BuildTour<T>(string[] ids, XRModeController mode, Transform desktopHud, TourEnd end) where T : NarratedTour =>
            (T)BuildTour(typeof(T), ids, mode, desktopHud, end);

        /// <summary>A tour whose class lives with an imported scenario (outside this assembly), by type name.</summary>
        static NarratedTour BuildScenarioTour(string typeName, string[] ids, XRModeController mode, Transform desktopHud, TourEnd end,
                                              params (string field, Object value)[] fields)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(typeName)).FirstOrDefault(t => t != null)
                       ?? throw new InvalidOperationException($"Scenario tour {typeName} is missing (Assets/Scenarios)");
            var tour = BuildTour(type, ids, mode, desktopHud, end);
            var so = new SerializedObject(tour);
            foreach (var (field, value) in fields)
                (so.FindProperty(field) ?? throw new InvalidOperationException($"{typeName} has no field {field}")).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            return tour;
        }

        static NarratedTour BuildTour(Type tourType, string[] ids, XRModeController mode, Transform desktopHud, TourEnd end)
        {
            var root = new GameObject("Guided Tour");
            var tour = (NarratedTour)root.AddComponent(tourType);

            var xrCanvas = WorldCanvas("GuideScreen (XR)", root.transform, new Vector2(GuideW, GuideH), 0.001f); // 1.0 × 0.47 m
            xrCanvas.AddComponent<FloatingFollow>();
            var xrScreen = BuildGuideScreen(xrCanvas.transform, tour, end);

            var dock = new GameObject("GuidePanel", typeof(RectTransform));
            var drt = (RectTransform)dock.transform;
            drt.SetParent(desktopHud, false);
            drt.anchorMin = drt.anchorMax = drt.pivot = new Vector2(0f, 1f);
            drt.anchoredPosition = new Vector2(16f, -16f);
            drt.sizeDelta = new Vector2(GuideW, GuideH);
            drt.localScale = Vector3.one * 0.74f;
            var desktopScreen = BuildGuideScreen(dock.transform, tour, end);

            var steps = ids.Select(id =>
            {
                var line = Line(id);
                return new NarratedTour.Step { id = id, title = line.title, text = line.text, clip = Voice(id) };
            }).ToArray();

            var camera = Object.FindObjectsByType<DesktopCameraController>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            tour.InitTour(steps, BuildGuideMarker(root.transform), desktopScreen, xrScreen, mode, camera);
            tour.InitQuiz(Quiz(tour.TourKey), BuildQuizScreen(dock.transform, tour), BuildQuizScreen(xrCanvas.transform, tour));
            return tour;
        }

        static GuideTour BuildDeadlockTour(SimulationManager sim, AnalysisOverlay overlay, PlayerInteraction interaction, XRModeController mode,
                                           GridlockAudio city, Transform desktopHud)
        {
            var tour = BuildTour<GuideTour>(DeadlockTourIds, mode, desktopHud, new TourEnd("BANKER MODE ►", BankerSceneName, "EXPLORE"));
            tour.Init(sim, overlay, interaction, city);
            return tour;
        }

        static BankerTour BuildBankerTour(Gridlock.Banker.BankerGame game, Gridlock.Banker.BankerLotView lot, XRModeController mode, Transform desktopHud)
        {
            var tour = BuildTour<BankerTour>(BankerTourIds, mode, desktopHud, new TourEnd("DEADLOCK MODE ►", DeadlockSceneName, "PLAY LEVEL 1"));
            tour.Init(game, lot);
            return tour;
        }

        const float GuideW = 1000f, GuideH = 470f, QuizH = 560f;

        [Serializable]
        class QuizFile
        {
            public QuizSet[] quizzes;
        }

        [Serializable]
        class QuizSet
        {
            public string key;
            public NarratedTour.QuizQuestion[] questions;
        }

        /// <summary>A tour's quick-check questions from Assets/Data/quizzes.json.</summary>
        static NarratedTour.QuizQuestion[] Quiz(string key)
        {
            var file = JsonUtility.FromJson<QuizFile>(File.ReadAllText("Assets/Data/quizzes.json"));
            return file.quizzes.FirstOrDefault(q => q.key == key)?.questions ?? throw new InvalidOperationException($"quizzes.json has no quiz '{key}'");
        }

        /// <summary>The quick check, on the guide screen's spot: a question, three answers, the explanation, NEXT.</summary>
        static QuizScreen BuildQuizScreen(Transform parent, NarratedTour tour)
        {
            var panel = Bordered("QuizPanel", parent, 0, 0, GuideW, QuizH, Opaque(ViewColor.Panel), Amber);
            var t = panel.transform;
            var header = UIText("Header", t, 30, 20, 940, 40, "", 24, GuideCyan, TextAlignmentOptions.Left, FontStyles.Bold);
            header.characterSpacing = 4f;
            var question = UIText("Question", t, 30, 64, 940, 96, "", 34, ViewColor.Text, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            question.enableAutoSizing = true;
            question.fontSizeMin = 22f;
            question.fontSizeMax = 34f;
            var options = new Button[3];
            var labels = new TMP_Text[3];
            for (int i = 0; i < 3; i++)
            {
                var (button, label) = UIButton($"Option{i + 1}", t, 30, 168 + i * 80, 940, 70, "", 28, new Color(0.16f, 0.21f, 0.32f), ViewColor.Text);
                label.fontSizeMin = 16f;
                label.textWrappingMode = TextWrappingModes.Normal;
                options[i] = button;
                labels[i] = label;
            }
            var feedback = UIText("Feedback", t, 30, 412, 700, 136, "", 25, ViewColor.Text, TextAlignmentOptions.TopLeft);
            feedback.textWrappingMode = TextWrappingModes.Normal;
            var (next, nextLabel) = UIButton("NextButton", t, 750, 476, 220, 66, "NEXT ►", 28, GuideCyan, new Color(0.04f, 0.07f, 0.12f));
            // Hosted on the parent: the panel starts hidden, and an inactive object would never run Awake.
            var screen = parent.gameObject.AddComponent<QuizScreen>();
            screen.Init(tour, panel, header, question, options, labels, feedback, next, nextLabel);
            return screen;
        }

        /// <summary>Guide screen content laid out in a GuideW × GuideH px box under <paramref name="parent"/>.</summary>
        static GuideScreen BuildGuideScreen(Transform parent, NarratedTour tour, TourEnd endInfo)
        {
            var panel = Bordered("Panel", parent, 0, 0, GuideW, GuideH, Opaque(ViewColor.Panel), GuideCyan);
            var t = panel.transform;

            var step = UIText("Step", t, 30, 20, 700, 40, "", 24, GuideCyan, TextAlignmentOptions.Left, FontStyles.Bold);
            step.characterSpacing = 4f;
            var (skip, _) = UIButton("SkipButton", t, 820, 16, 160, 52, "SKIP", 24, new Color(0.16f, 0.21f, 0.32f), ViewColor.Text);
            var title = UIText("Title", t, 30, 66, 940, 64, "", 46, Amber, TextAlignmentOptions.Left, FontStyles.Bold);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.enableAutoSizing = true;
            title.fontSizeMin = 30f;
            title.fontSizeMax = 46f;
            var caption = UIText("Caption", t, 30, 136, 940, 190, "", 34, ViewColor.Text, TextAlignmentOptions.TopLeft);
            caption.textWrappingMode = TextWrappingModes.Normal;
            caption.enableAutoSizing = true;
            caption.fontSizeMin = 22f;
            caption.fontSizeMax = 34f;
            caption.lineSpacing = 6f;

            Panel("ProgressTrack", t, 30, 336, 940, 10, new Color(1f, 1f, 1f, 0.12f));
            var fill = Panel("ProgressFill", t, 30, 336, 940, 10, GuideCyan).GetComponent<Image>();
            fill.sprite = null;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = 0f;

            // Hands-on buttons (colour and label set by the tour), then NEXT.
            var running = Group("Running", t);
            var (action1, label1) = UIButton("Action1", running.transform, 30, 362, 370, 88, "", 38, ViewColor.Safe, Color.white);
            var (action2, label2) = UIButton("Action2", running.transform, 410, 362, 370, 88, "", 38, ViewColor.Deadlock, Color.white);
            var (next, _) = UIButton("NextButton", running.transform, 790, 362, 180, 88, "NEXT ►", 34, new Color(0.22f, 0.27f, 0.38f), ViewColor.Text);

            var end = Group("End", t);
            var (replay, _) = UIButton("ReplayButton", end.transform, 30, 362, 300, 88, "REPLAY TOUR", 32, GuideCyan, new Color(0.04f, 0.07f, 0.12f));
            ModeButton(end.transform, 350, 362, 320, 88, endInfo.ModeLabel, 32, endInfo.ModeScene);
            var (close, _) = UIButton("CloseButton", end.transform, 690, 362, 280, 88, endInfo.CloseLabel, 32, new Color(0.22f, 0.27f, 0.38f), ViewColor.Text);

            // Collapsed state: a small pill that replays the tour.
            var pill = Group("Pill", parent);
            var (pillButton, _) = UIButton("TourButton", pill.transform, 0, 0, 330, 70, "► GUIDED TOUR", 28, GuideCyan, new Color(0.04f, 0.07f, 0.12f));

            // Hosted on the parent: the panel itself starts hidden, and an inactive object would never run Awake.
            var screen = parent.gameObject.AddComponent<GuideScreen>();
            screen.Init(tour, panel, pill, step, title, caption, fill, running, next, skip, new[] { action1, action2 }, new[] { label1, label2 },
                        end, replay, close, pillButton);
            return screen;
        }

        /// <summary>Empty full-size RectTransform used to show/hide a set of controls together.</summary>
        static GameObject Group(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            Rect(go, parent, 0, 0, GuideW, GuideH);
            return go;
        }

        static GuideMarker BuildGuideMarker(Transform parent)
        {
            var go = new GameObject("Guide Marker");
            go.transform.SetParent(parent, false);

            var arrow = GameObject.CreatePrimitive(PrimitiveType.Quad);
            arrow.name = "Arrow";
            Object.DestroyImmediate(arrow.GetComponent<Collider>());
            arrow.transform.SetParent(go.transform, false);
            arrow.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            arrow.transform.localScale = new Vector3(1.4f, 1.5f, 1f); // a quad shows its face to -Z, where the camera is
            var r = arrow.GetComponent<Renderer>();
            var mat = Transparent(new Material(Shader.Find("Universal Render Pipeline/Unlit")) { mainTexture = ArrowTexture() });
            mat.SetColor("_BaseColor", new Color(1.6f, 1.25f, 0.3f, 1f)); // HDR amber, catches the bloom
            r.sharedMaterial = SaveMat("GuideArrow", mat);
            r.shadowCastingMode = ShadowCastingMode.Off;

            var label = new GameObject("Label").AddComponent<TextMeshPro>();
            label.transform.SetParent(go.transform, false);
            label.transform.localPosition = new Vector3(0f, 2.05f, 0f);
            label.font = font;
            label.fontSize = 9f;
            label.fontStyle = FontStyles.Bold;
            label.color = Amber;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.outlineWidth = 0.25f;
            label.outlineColor = new Color32(10, 14, 24, 255);
            label.rectTransform.sizeDelta = new Vector2(12f, 2f);

            var marker = go.AddComponent<GuideMarker>();
            marker.Init(arrow.transform, label);
            return marker;
        }

        /// <summary>Downward chevron arrow with a soft edge.</summary>
        static Texture2D ArrowTexture()
        {
            const int size = 256;
            var path = $"{GeneratedDir}/GuideArrow.png";
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size; // v = 0 bottom (tip), 1 top
                // Shaft in the upper half, triangular head below.
                float shaft = v > 0.45f ? Edge(0.22f - Mathf.Abs(u)) * Edge(0.96f - v) : 0f;
                float head = v <= 0.5f ? Edge(v * 1.7f - Mathf.Abs(u)) * Edge(v - 0.02f) : 0f;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Max(shaft, head)));
            }
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);

            static float Edge(float d) => Mathf.Clamp01(d * 60f);
        }
    }
}
