using System.Linq;
using Gridlock.Core;
using Gridlock.Hub;
using Gridlock.UI;
using Gridlock.XR;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Object = UnityEngine.Object;

namespace Gridlock.Editor
{
    /// <summary>
    /// Walking around the hub room: invisible collision for walls and furniture, walkable / teleportable floors (main
    /// floor, a ramp over the gallery stairs, the gallery deck), the chair as the "sit down" target, the desktop roaming
    /// camera and the exhibit labels.
    /// </summary>
    public static partial class GridlockSceneBuilder
    {
        static readonly Vector3 StandPoint = ChairPos; // XR: stand where the chair is, within poking reach of the keys

        /// <summary>Invisible collider (no renderer).</summary>
        static GameObject Blocker(string name, Transform parent, Vector3 centre, Vector3 size, float pitch = 0f, float yaw = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(centre, Quaternion.Euler(pitch, yaw, 0f));
            go.AddComponent<BoxCollider>().size = size;
            return go;
        }

        static void Walkable(GameObject go)
        {
            go.AddComponent<HubWalkable>();
            Teleportable(go);
        }

        static void BuildHubCollision(Transform room)
        {
            var root = new GameObject("Collision").transform;
            root.SetParent(room);
            float w = RoomMaxX - RoomMinX, d = RoomMaxZ - RoomMinZ, cz = (RoomMinZ + RoomMaxZ) / 2f;

            // Floors you can walk and teleport on.
            Walkable(Blocker("Floor", root, new Vector3(0f, -0.05f, cz), new Vector3(w, 0.1f, d)));
            float deckZ = (RoomMinZ + GalleryZ) / 2f, deckD = GalleryZ - RoomMinZ;
            Walkable(Blocker("GalleryDeck", root, new Vector3(0f, GalleryH / 2f, deckZ), new Vector3(w, GalleryH, deckD)));
            float run = KitTile, rise = GalleryH, length = Mathf.Sqrt(run * run + rise * rise);
            float slope = Mathf.Atan2(rise, run) * Mathf.Rad2Deg; // the stairs rise towards the back wall (−Z)
            Walkable(Blocker("StairsRamp", root, new Vector3(0f, rise / 2f - 0.04f, GalleryZ + run / 2f), new Vector3(KitTile, 0.08f, length), pitch: slope));

            // Room walls and the gallery rails (the stairs stay open).
            const float wallT = 0.6f, wallH = 5f;
            Blocker("Wall_N", root, new Vector3(0f, wallH / 2f, RoomMaxZ + wallT / 2f), new Vector3(w + 2f, wallH, wallT));
            Blocker("Wall_S", root, new Vector3(0f, wallH / 2f, RoomMinZ - wallT / 2f), new Vector3(w + 2f, wallH, wallT));
            Blocker("Wall_W", root, new Vector3(RoomMinX - wallT / 2f, wallH / 2f, cz), new Vector3(wallT, wallH, d + 2f));
            Blocker("Wall_E", root, new Vector3(RoomMaxX + wallT / 2f, wallH / 2f, cz), new Vector3(wallT, wallH, d + 2f));
            float railLen = (w - KitTile) / 2f;
            foreach (var side in new[] { -1f, 1f })
                Blocker("GalleryRail", root, new Vector3(side * (KitTile / 2f + railLen / 2f), GalleryH + 0.55f, GalleryZ - 0.05f), new Vector3(railLen, 1.1f, 0.12f));

            // Furniture made of kit models (the desk, chair and racks already have colliders of their own).
            var f = new GameObject("Furniture").transform;
            f.SetParent(root);
            void Block(string n, float x, float z, float sx, float sz, float y = 0f, float h = 1.6f) =>
                Blocker(n, f, new Vector3(x, y + h / 2f, z), new Vector3(sx, h, sz));
            Block("LabComputers", RoomMinX + 0.75f, -0.6f, 1.5f, 5.6f, h: 2.2f);
            foreach (var z in new[] { -2.6f, -0.6f, 1.4f }) Block("LabChair", RoomMinX + 2.1f, z, 0.8f, 0.8f, h: 1.2f);
            Block("PlanetTable", -4.4f, 0.9f, 1.6f, 1.6f, h: 1.1f);
            Block("HoloTable", -4.6f, -3.4f, 1.4f, 1.4f, h: 1.1f);
            Block("Containers", 6.7f, -3.1f, 3.4f, 3.2f, h: 2.6f);
            Block("OreSkip", 6.6f, 1.0f, 1.8f, 2.8f, h: 1.4f);
            Block("Showcase", 4.4f, 1.6f, 1.6f, 1.6f, h: 1.4f);
            Block("Pipes", RoomMaxX - 0.45f, -0.8f, 0.7f, 0.7f, h: 3f);
            Block("Pipes", RoomMaxX - 0.45f, 2.6f, 0.7f, 0.7f, h: 3f);
            Block("Structure", 7.0f, 4.6f, 2.6f, 2.0f, h: 3f);
            Block("Barrier", 4.6f, 4.6f, 2.4f, 0.8f, h: 2f);
            Block("Sofa", -5.6f, 4.3f, 2.8f, 1.0f, h: 0.9f);
            Block("Ottoman", -5.6f, 5.6f, 0.9f, 0.9f, h: 0.5f);
            Block("FloorLamp", -7.5f, 4.0f, 0.5f, 0.5f, h: 1.7f);
            Block("Bookcase", RoomMinX + 0.35f, 6.3f, 0.7f, 1.6f, h: 2f);
            foreach (var x in new[] { -3.95f, 3.95f })
            {
                Block("Plant", x, RoomMaxZ - 0.55f, 0.7f, 0.7f, h: 1.4f);
                Block("Speaker", x + Mathf.Sign(x) * 0.9f, RoomMaxZ - 0.45f, 0.7f, 0.7f, h: 1.2f);
            }
            Block("Plant", RoomMinX + 0.6f, 2.2f, 0.7f, 0.7f, h: 1.3f);
            Block("Plant", RoomMaxX - 0.6f, 6.5f, 0.7f, 0.7f, h: 1.3f);
            // On the gallery.
            Block("GalleryTable", 4.2f, RoomMinZ + 1.3f, 3.4f, 2.2f, y: GalleryH, h: 1.0f);
            Block("GalleryComputers", -6.0f, RoomMinZ + 0.6f, 2.6f, 1.0f, y: GalleryH, h: 1.6f);
            Block("GalleryContainers", 7.2f, RoomMinZ + 0.6f, 1.8f, 1.1f, y: GalleryH, h: 2.2f);
            Block("Astronaut", 1.9f, RoomMinZ + 1.2f, 0.7f, 0.7f, y: GalleryH, h: 1.8f);
            Block("Dish", -7.2f, RoomMinZ + 1.1f, 1.4f, 1.4f, y: GalleryH, h: 1.7f);
        }

        /// <summary>
        /// The chair becomes the "sit down" target: its geometry stops blocking (the player stands where it is in XR), and
        /// one collider around the backrest takes clicks, taps and XR selects.
        /// </summary>
        static HubSeat BuildSeatTarget(Transform room)
        {
            var chair = room.Find("Chair");
            foreach (var c in chair.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            var target = new GameObject("SitTarget");
            target.transform.SetParent(chair, false);
            target.transform.position = ChairPos + new Vector3(0f, 1.0f, -0.32f);
            target.AddComponent<BoxCollider>().size = new Vector3(0.62f, 1.1f, 0.18f);
            target.AddComponent<XRSimpleInteractable>();
            var stand = new GameObject("StandPoint").transform;
            stand.SetParent(chair, false);
            stand.SetPositionAndRotation(StandPoint, Quaternion.identity); // facing the desk (+Z)
            var seat = target.AddComponent<HubSeat>();
            return seat;
        }

        /// <summary>Exhibit labels that fade in as the player walks up.</summary>
        static void BuildHubPlaques(Transform room)
        {
            var root = new GameObject("Plaques").transform;
            root.SetParent(room);
            void Plaque(Vector3 at, string title, string line)
            {
                var go = new GameObject("Plaque " + title);
                go.transform.SetParent(root, false);
                go.transform.position = at;
                var tmp = go.AddComponent<TextMeshPro>();
                tmp.font = font;
                tmp.text = $"<b>{title}</b>\n<size=62%><color=#CBD5E1>{line}</color></size>";
                tmp.fontSize = 1.7f;
                tmp.color = new Color(0.35f, 0.85f, 1f);
                tmp.alpha = 0f;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.textWrappingMode = TextWrappingModes.Normal;
                tmp.rectTransform.sizeDelta = new Vector2(2.2f, 0.8f);
                tmp.outlineWidth = 0.2f;
                tmp.outlineColor = new Color32(4, 8, 16, 255);
                go.AddComponent<HubPlaque>().Init(tmp);
            }
            Plaque(new Vector3(-4.6f, 2.05f, -3.4f), "DEADLOCK HOLOGRAM", "Four processes, each waiting for the next one: a circular wait.");
            Plaque(new Vector3(-4.4f, 2.6f, 0.9f), "ORBITAL DISPLAY", "Live survey of the gas giant outside the window.");
            Plaque(new Vector3(4.4f, 3.0f, 1.6f), "COURIER SHUTTLE", "Scale model of the station's supply craft.");
            Plaque(new Vector3(RoomMaxX - 1.2f, 3.6f, 0.9f), "PROCESS TABLE", "Which process holds which resource, and who is blocked.");
            Plaque(new Vector3(-2.1f, GalleryH + 2.35f, RoomMinZ + 1.2f), "SERVER RACKS", "The machines running every OS Plays simulation.");
            Plaque(new Vector3(RoomMinX + 1.2f, 2.5f, -0.6f), "RESEARCH LAB", "Computer banks for scheduling experiments.");
            Plaque(new Vector3(4.2f, GalleryH + 1.7f, RoomMinZ + 1.3f), "GALLERY LOUNGE", "Up the stairs: the view over the whole control room.");
            Plaque(new Vector3(0f, 2.9f, RoomMaxZ - 0.8f), "OBSERVATION WINDOW", "Ringed gas giant, its moon, and passing ships.");
        }

        /// <summary>Desktop / phone: the roaming camera replaces the fixed chair look; a STAND UP / SIT DOWN button on the HUD.</summary>
        static HubRoamer BuildDesktopRoamer(Transform desktop, HubDesktopInput deskInput)
        {
            var look = desktop.GetComponent<HubDesktopLook>();
            if (look != null) Object.DestroyImmediate(look);
            var body = desktop.gameObject.AddComponent<CharacterController>();
            body.height = 1.7f;
            body.radius = 0.28f;
            body.center = new Vector3(0f, -0.77f, 0f); // the camera is the eye; the capsule hangs below it
            body.stepOffset = 0.3f;
            body.slopeLimit = 30f;
            var roamer = desktop.gameObject.AddComponent<HubRoamer>();
            roamer.Init(deskInput);

            var hud = desktop.Find("Desktop HUD");
            var (button, label) = UIButton("RoamButton", hud, 12f, 970f, 260f, 48f, "STAND UP  (R)", 22, new Color(0.35f, 0.85f, 1f), new Color(0.04f, 0.07f, 0.12f));
            button.gameObject.AddComponent<HubRoamButton>().Init(roamer, label, "STAND UP  (R)", "SIT DOWN  (R)");
            return roamer;
        }

        /// <summary>The hub's wrist extra under MENU: SIT AT DESK.</summary>
        static void BuildHubWristMenu(Transform menu)
        {
            var (sit, _) = UIButton("SitButton", menu, 20, 140, WristW - 40, 100, "SIT AT DESK", 40, GuideCyan, new Color(0.04f, 0.07f, 0.12f));
            var link = sit.gameObject.AddComponent<HubSitButton>();
            UnityEditor.Events.UnityEventTools.AddPersistentListener(sit.onClick, link.Sit);
        }
    }
}
