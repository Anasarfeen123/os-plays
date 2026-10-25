using System.Collections.Generic;
using System.Linq;
using Gridlock.Banker;
using Gridlock.Core;
using Gridlock.UI;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Gridlock.Editor
{
    /// <summary>
    /// GridlockBanker.unity: the Banker's Algorithm game on the same lot. Zone N = P1–P3, Zone S = P4–P6.
    /// Parked fleet cars are Allocation, cars on the street are remaining Need, cars at the gate are the Request,
    /// empty bays are Available.
    /// </summary>
    public static partial class GridlockSceneBuilder
    {
        const string BankerScenePath = "Assets/Scenes/GridlockBanker.unity";
        const string BankerSceneName = "GridlockBanker";

        const string MappingText =
            "<b>HOW TO READ THE LOT</b>\n\n" +
            "<b>Parked cars</b> = the fleet's <b>Allocation</b>\n" +
            "<b>Cars on the street</b> = its remaining <b>Need</b> (Max − Allocation)\n" +
            "<b>Cars at the gate</b> = the <b>Request</b>\n" +
            "<b>Empty bays</b> = <b>Available</b>\n\n" +
            "Zone N = P1–P3 (north row), Zone S = P4–P6 (south row).\n" +
            "GRANT only if every fleet could still finish afterwards.";

        static void BuildBankerScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildWorld();

            var bayRoot = new GameObject("ParkingSpaces").transform;
            var bays = SpaceIds.Select((id, i) => BuildSpace(id, i, bayRoot, interactive: false)).ToArray();
            var paint = new GameObject("ZonePaint").transform;
            GroundText("ZONE N", paint, new Vector3(LotMinX + 1.35f, 0.02f, TopRowZ), 9f, new Color(1f, 1f, 1f, 0.8f), 90f);
            GroundText("ZONE S", paint, new Vector3(LotMinX + 1.35f, 0.02f, BottomRowZ), 9f, new Color(1f, 1f, 1f, 0.8f), 90f);

            // Fleet cars: UnitsPerFleet per fleet, parked off-screen until the lot view places them.
            var unitRoot = new GameObject("FleetCars").transform;
            var units = new List<CarView>();
            for (int f = 0; f < Cars.Length; f++)
                for (int k = 0; k < BankerChallenge.UnitsPerFleet; k++)
                {
                    var unit = BuildCar(Cars[f].id, $"Fleet{Cars[f].id}_{k + 1}", Cars[f].model, Cars[f].color, unitRoot,
                                        new Vector3(0f, 0f, -80f), Quaternion.identity, full: false);
                    unit.gameObject.SetActive(false);
                    units.Add(unit);
                }

            // Need queue on the west street (two lanes), request gate just inside the entry.
            var slotRoot = new GameObject("Slots").transform;
            var street = new List<Transform>();
            for (int i = 0; i < 8; i++)
                foreach (var laneX in new[] { -StreetX + 2f, -StreetX - 2f })
                    street.Add(Slot($"Street{street.Count + 1}", slotRoot, new Vector3(laneX, 0f, AisleZ + 7.5f + i * 6f), 180f));
            var gateSlots = new[]
            {
                Slot("Gate1", slotRoot, new Vector3(LotMinX + 2.4f, 0f, AisleZ + 1.6f), 90f),
                Slot("Gate2", slotRoot, new Vector3(LotMinX + 2.4f, 0f, AisleZ - 1.6f), 90f),
            };

            var gameGo = new GameObject("BankerGame");
            var lot = gameGo.AddComponent<BankerLotView>();
            lot.Init(units.ToArray(), bays, street.ToArray(), gateSlots);
            var game = gameGo.AddComponent<BankerGame>();
            game.Init(lot);
            gameGo.AddComponent<BankerAudio>().Init(Clip("city_ambience.wav"), Clip("Kenney/confirmation_002.ogg"),
                                                     Clip("Kenney/error_004.ogg"), Clip("Kenney/click_002.ogg"));

            BuildBankerBoard(game);
            BuildGateCard(game);
            BuildBankerPodium(game);
            Transform desktopHud = null;
            var mode = BuildPresentation(hud =>
            {
                BuildBankerHud(hud, game);
                desktopHud = hud.Find("Desktop HUD");
            }, (menu, keys, origin) =>
            {
                keys.Init(xHoldSeconds: 0f);
                UnityEditor.Events.UnityEventTools.AddPersistentListener(keys.ButtonA, game.Grant);
                UnityEditor.Events.UnityEventTools.AddPersistentListener(keys.ButtonB, game.Deny);
                UnityEditor.Events.UnityEventTools.AddPersistentListener(keys.ButtonX, game.Next);
            });
            var tour = BuildBankerTour(game, lot, mode, desktopHud);
            BuildARRig(mode, desktopHud);
            BuildAudioSystems("music_banker");
            BuildSystemMenu(mode, DesktopCameraTransform(), "BANKER'S ALGORITHM", isHome: false, tour, game.RestartTutorial, BankerHowTo,
                            "◄ DEADLOCK MODE", DeadlockSceneName);

            WireTeleportTargets();
            EditorSceneManager.SaveScene(scene, BankerScenePath);
            Debug.Log($"[GRIDLOCK] Built {BankerScenePath}");
        }

        static Transform Slot(string name, Transform parent, Vector3 pos, float yaw)
        {
            var t = Marker(name, parent, pos);
            t.rotation = Quaternion.Euler(0f, yaw, 0f);
            return t;
        }

        static void BuildBankerBoard(BankerGame game)
        {
            var canvas = BoardCanvas("Banker's Algorithm\n<size=80%>Deadlock avoidance · OS Module 4</size>", "DEADLOCK MODE", DeadlockSceneName, out var score);
            var t = canvas.transform;

            var progress = UIText("Progress", t, 50, 200, 1100, 50, "", 34, Muted, TextAlignmentOptions.Left, FontStyles.Bold);
            progress.characterSpacing = 4f;
            var title = UIText("Title", t, 50, 250, 1100, 70, "", 54, Amber, TextAlignmentOptions.Left, FontStyles.Bold);
            FitOneLine(title, 34f);
            var intro = UIText("Intro", t, 50, 330, 1100, 160, "", 34, Muted, TextAlignmentOptions.TopLeft);
            var request = UIText("Request", t, 50, 495, 1100, 80, "", 52, ViewColor.Text, TextAlignmentOptions.Left);
            var table = UIText("Table", t, 50, 590, 1100, 420, "", 44, ViewColor.Text, TextAlignmentOptions.TopLeft);

            var (grant, _) = UIButton("GrantButton", t, 50, 1040, 340, 150, "GRANT", 60, ViewColor.Safe, Color.white);
            var (deny, _) = UIButton("DenyButton", t, 410, 1040, 340, 150, "DENY", 60, ViewColor.Deadlock, Color.white);
            var (next, _) = UIButton("NextButton", t, 770, 1040, 350, 150, "NEXT", 56, new Color(0.22f, 0.27f, 0.38f), ViewColor.Text);

            UIText("Mapping", t, 1230, 215, 1110, 980, MappingText, 38, Muted, TextAlignmentOptions.TopLeft).lineSpacing = 10f;
            var verdict = Bordered("Verdict", t, 1220, 200, 1130, 1000, new Color(0.09f, 0.13f, 0.22f), Amber);
            var headline = UIText("Headline", verdict.transform, 30, 24, 1070, 90, "", 62, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
            var body = UIText("Body", verdict.transform, 30, 130, 1070, 840, "", 34, ViewColor.Text, TextAlignmentOptions.TopLeft);
            body.enableAutoSizing = true;
            body.fontSizeMin = 22f;
            body.fontSizeMax = 34f;

            canvas.AddComponent<BankerPanel>().Init(game, title, progress, intro, request, table, verdict, headline, body, score,
                                                    grant, deny, next, twoLineScore: true);
        }

        static void FitOneLine(TMP_Text text, float minSize)
        {
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.enableAutoSizing = true;
            text.fontSizeMax = text.fontSize;
            text.fontSizeMin = minSize;
        }

        /// <summary>Floating card over the gate cars: decide right where the request is, which is the natural VR interaction.</summary>
        static void BuildGateCard(BankerGame game)
        {
            var canvas = WorldCanvas("GateCard", null, new Vector2(720, 360), 0.006f);
            canvas.transform.position = new Vector3(LotMinX + 2.4f, 5.6f, AisleZ);
            canvas.AddComponent<Billboard>();
            // Drawn after the cars' floating letters (both are transparent): a letter never covers GRANT or DENY.
            canvas.GetComponent<Canvas>().sortingOrder = 10;
            var t = canvas.transform;

            Bordered("Background", t, 0, 0, 720, 360, Opaque(ViewColor.Panel), Amber);
            var request = UIText("Request", t, 24, 18, 672, 80, "", 40, ViewColor.Text, TextAlignmentOptions.Center);
            var verdict = new GameObject("Verdict", typeof(RectTransform));
            Rect(verdict, t, 24, 100, 672, 90);
            var headline = UIText("Headline", verdict.transform, 0, 0, 672, 90, "", 44, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            var (grant, _) = UIButton("GrantButton", t, 24, 214, 320, 120, "GRANT", 52, ViewColor.Safe, Color.white);
            var (deny, _) = UIButton("DenyButton", t, 376, 214, 320, 120, "DENY", 52, ViewColor.Deadlock, Color.white);

            canvas.AddComponent<BankerPanel>().Init(game, null, null, null, request, null, verdict, headline, null, null,
                                                    grant, deny, null, trace: false);
        }

        static void BuildBankerPodium(BankerGame game)
        {
            var canvas = PodiumCanvas("BANKER CONTROL", "DEADLOCK MODE", DeadlockSceneName);
            var t = canvas.transform;
            var request = UIText("Request", t, 30, 96, 760, 60, "", 36, ViewColor.Text, TextAlignmentOptions.Left);
            UIText("Hint", t, 30, 160, 760, 120,
                "Is it safe to grant? Check the board, then decide.\nLook at a controller to see its buttons.", 28, Muted, TextAlignmentOptions.TopLeft);
            var verdict = Bordered("Verdict", t, 20, 156, 780, 130, new Color(0.09f, 0.13f, 0.22f), Amber);
            var headline = UIText("Headline", verdict.transform, 16, 10, 748, 110, "", 40, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

            float bw = 250f;
            var (grant, _) = UIButton("GrantButton", t, 20, 300, bw, 110, "GRANT", 44, ViewColor.Safe, Color.white);
            var (deny, _) = UIButton("DenyButton", t, 285, 300, bw, 110, "DENY", 44, ViewColor.Deadlock, Color.white);
            var (next, _) = UIButton("NextButton", t, 550, 300, bw, 110, "NEXT", 40, new Color(0.22f, 0.27f, 0.38f), ViewColor.Text);
            var score = UIText("Score", t, 30, 432, 760, 80, "", 28, Amber, TextAlignmentOptions.TopLeft, FontStyles.Bold);

            canvas.AddComponent<BankerPanel>().Init(game, null, null, null, request, null, verdict, headline, null, score,
                                                    grant, deny, next, trace: false);
        }

        static void BuildBankerHud(Transform desktopCamera, BankerGame game)
        {
            var (hud, d) = DesktopDock(desktopCamera, "Banker's Algorithm · Deadlock avoidance", "DEADLOCK MODE", DeadlockSceneName);
            const float w = HudWidth, x = HudX;

            var score = UIText("Score", d, x + 22f, 128f, w - 44f, 40f, "", 26, Amber, TextAlignmentOptions.Left, FontStyles.Bold);
            var progress = UIText("Progress", d, x + 22f, 172f, w - 44f, 26f, "", 18, Muted, TextAlignmentOptions.Left, FontStyles.Bold);
            var title = UIText("Title", d, x + 22f, 196f, w - 44f, 40f, "", 28, Amber, TextAlignmentOptions.Left, FontStyles.Bold);
            FitOneLine(title, 16f);
            var intro = UIText("Intro", d, x + 22f, 238f, w - 44f, 110f, "", 19, Muted, TextAlignmentOptions.TopLeft);
            var request = UIText("Request", d, x + 22f, 352f, w - 44f, 44f, "", 25, ViewColor.Text, TextAlignmentOptions.Left);
            var table = UIText("Table", d, x + 22f, 400f, w - 44f, 230f, "", 24, ViewColor.Text, TextAlignmentOptions.TopLeft);

            UIText("Mapping", d, x + 22f, 640f, w - 44f, 310f, MappingText, 18, Muted, TextAlignmentOptions.TopLeft);
            var verdict = Bordered("Verdict", d, x + 16f, 636f, w - 32f, 316f, new Color(0.09f, 0.13f, 0.22f), Amber);
            var headline = UIText("Headline", verdict.transform, 16f, 10f, w - 64f, 40f, "", 28, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
            var body = UIText("Body", verdict.transform, 16f, 54f, w - 64f, 252f, "", 18, ViewColor.Text, TextAlignmentOptions.TopLeft);
            body.enableAutoSizing = true;
            body.fontSizeMin = 11f;
            body.fontSizeMax = 18f;

            float bw = (w - 32f - 20f) / 3f;
            var (grant, _) = UIButton("GrantButton", d, x + 16f, 966f, bw, 76f, "GRANT", 30, ViewColor.Safe, Color.white);
            var (deny, _) = UIButton("DenyButton", d, x + 26f + bw, 966f, bw, 76f, "DENY", 30, ViewColor.Deadlock, Color.white);
            var (next, _) = UIButton("NextButton", d, x + 36f + 2 * bw, 966f, bw, 76f, "NEXT", 28, new Color(0.22f, 0.27f, 0.38f), ViewColor.Text);

            HelpBar(hud.transform, "GRANT or DENY each request    NEXT  continue    Right-drag  orbit    Scroll  zoom",
                    "GRANT or DENY each request    NEXT  continue    Drag  orbit    Pinch  zoom");

            hud.AddComponent<BankerPanel>().Init(game, title, progress, intro, request, table, verdict, headline, body, score,
                                                 grant, deny, next);
        }
    }
}
