using System.Collections;
using Gridlock.XR;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// GRIDLOCK glue for the memory warehouse.
/// <list type="bullet">
/// <item>Headset: grab a page box (grip, or point and grip) and let go of it on a frame's pallet, or anywhere in front of
/// the rack below that pallet. The placement manager checks the move; a wrong frame explains why and the box goes back
/// to its table, as does a box dropped on the floor.</item>
/// <item>Monitor or phone: look at (or tap) a box to pick it up and at a pallet to put it there, which asks the placement
/// manager exactly as a drop in VR does. A wrong frame keeps the box in hand.</item>
/// </list>
/// Boxes whose page is in memory stay on their pallet until the algorithm evicts them.
/// </summary>
public class MemoryScenarioAdapter : MonoBehaviour
{
    [SerializeField] MemoryPlacementManager manager;
    [SerializeField] GameObject[] boxes;
    [SerializeField] int[] pages;
    [Tooltip("Frame roots (pallets + trigger), with the frame index each one stands for.")]
    [SerializeField] Transform[] frames;
    [SerializeField] int[] frameIndices;
    [Tooltip("The frames' drop volumes (headset).")]
    [SerializeField] MemoryFrameZone[] zones;
    [SerializeField] Transform carryAnchor;
    [Tooltip("A released box at rest this far from its table spot for this long goes back to the table.")]
    [SerializeField] float strayDistance = 0.3f;
    [SerializeField] float straySeconds = 1.2f;

    GameObject carried;
    Transform carriedParent;
    Vector3 carriedPosition;
    Quaternion carriedRotation;

    bool xr;
    Vector3[] tableSpots;
    float[] strayTimers;

    public GameObject Carried => carried;

    void Start()
    {
        // The imported algorithm screen invokes BoxResetManager after a short delay. GRIDLOCK resets through
        // MemoryPlacementManager instead; let that one source of truth retain pages that are already in frames.
        foreach (var reset in FindObjectsByType<BoxResetManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            reset.SetExternallyManaged(true);

        var mode = XRModeController.Current;
        xr = mode != null && mode.XRActive;
        if (xr)
        {
            tableSpots = new Vector3[boxes.Length];
            strayTimers = new float[boxes.Length];
            for (int i = 0; i < boxes.Length; i++)
            {
                if (boxes[i] == null) continue;
                tableSpots[i] = boxes[i].transform.position;
                var box = boxes[i];
                var grab = box.GetComponent<XRGrabInteractable>();
                if (grab != null) grab.selectExited.AddListener(_ => StartCoroutine(Dropped(box, grab)));
            }
            return;
        }
        for (int i = 0; i < boxes.Length; i++)
            if (boxes[i] != null) boxes[i].AddComponent<MemoryBoxTarget>().Init(this, pages[i]);
        for (int i = 0; i < frames.Length; i++)
            if (frames[i] != null) frames[i].gameObject.AddComponent<MemoryFrameTarget>().Init(this, frameIndices[i]);
    }

    void Update()
    {
        if (xr)
        {
            WatchStrayBoxes();
            return;
        }
        // The manager can take a carried box away (it evicted that page): it is no longer in hand.
        if (carried != null && carried.transform.parent != carryAnchor)
        {
            SetColliders(carried, true);
            carried = null;
        }
    }

    // ---- Headset ----

    /// <summary>The frame whose drop volume holds <paramref name="point"/>, or null.</summary>
    public MemoryFrameZone ZoneAt(Vector3 point)
    {
        foreach (var zone in zones)
            if (zone != null && zone.Contains(point)) return zone;
        return null;
    }

    IEnumerator Dropped(GameObject box, XRGrabInteractable grab)
    {
        // Capture the intended target immediately. Waiting for a physics tick first used to let a box fall or be thrown
        // out of a valid pallet volume before it was checked, making a visibly correct drop fail intermittently.
        var zone = ZoneAt(Centre(box));
        yield return new WaitForFixedUpdate(); // let XRI complete its detach before we parent or reset the body
        if (grab.isSelected || manager.IsResident(box)) yield break;
        zone ??= ZoneAt(Centre(box));
        if (zone == null) yield break; // put down somewhere else; the floor is watched
        if (manager.TryPlaceBox(box, zone.FrameIndex)) yield break;

        // Wrong frame or wrong page: the manager says why; the box returns to its table.
        yield return new WaitForSeconds(0.35f);
        if (!grab.isSelected) manager.ReturnToStart(box);
    }

    /// <summary>Boxes belong on their table or in a frame: one left lying anywhere else (floor, shelf) goes back.</summary>
    void WatchStrayBoxes()
    {
        for (int i = 0; i < boxes.Length; i++)
        {
            var box = boxes[i];
            if (box == null) continue;
            var grab = box.GetComponent<XRGrabInteractable>();
            var body = box.GetComponent<Rigidbody>();
            bool loose = grab != null && grab.enabled && !grab.isSelected && !manager.IsResident(box);
            if (!loose || Vector3.Distance(box.transform.position, tableSpots[i]) < strayDistance)
            {
                strayTimers[i] = 0f;
                continue;
            }
            // Through the floor: straight back. Still rolling or sliding: back a little later, so it is never lost.
            bool fell = box.transform.position.y < tableSpots[i].y - 2f;
            bool resting = body == null || body.isKinematic || body.linearVelocity.sqrMagnitude < 0.01f;
            strayTimers[i] += Time.deltaTime;
            if (!fell && strayTimers[i] < (resting ? straySeconds : straySeconds * 3f)) continue;
            strayTimers[i] = 0f;
            manager.ReturnToStart(box);
        }
    }

    static Vector3 Centre(GameObject box)
    {
        var body = box.GetComponent<Rigidbody>();
        if (body != null) return body.worldCenterOfMass;
        var bounds = new Bounds(box.transform.position, Vector3.zero);
        foreach (var c in box.GetComponentsInChildren<Collider>()) bounds.Encapsulate(c.bounds);
        return bounds.center;
    }

    // ---- Monitor and phone ----

    static string Verb => TouchGestures.IsTouchDevice ? "Tap" : "Press [E]";

    public string BoxPrompt(GameObject box, int page)
    {
        int frame = manager.FrameOf(box);
        if (frame >= 0) return $"Page {page} is in frame {frame + 1}";
        return carried == null ? $"{Verb} to pick up page {page}" : $"{Verb} to swap for page {page}";
    }

    public string FramePrompt(int frame) => carried == null ? $"Frame {frame + 1}: pick up a page box first" : $"{Verb} to put the page in frame {frame + 1}";

    public void PickUp(GameObject box)
    {
        if (box == carried || manager.IsResident(box)) return;
        if (carried != null) PutBack();
        carried = box;
        carriedParent = box.transform.parent;
        carriedPosition = box.transform.position;
        carriedRotation = box.transform.rotation;
        SetHeld(box, true);
        box.transform.SetParent(carryAnchor, false);
        box.transform.localPosition = new Vector3(0.28f, -0.32f, 0.85f);
        box.transform.localRotation = Quaternion.identity;
    }

    public void Place(int frameIndex)
    {
        if (carried == null) return;
        var box = carried;
        if (!manager.TryPlaceBox(box, frameIndex)) return; // wrong frame: the manager explained why; keep holding it
        carried = null;
        SetColliders(box, true);
    }

    /// <summary>Back where it was picked up from.</summary>
    void PutBack()
    {
        var box = carried;
        carried = null;
        box.transform.SetParent(carriedParent, true);
        box.transform.SetPositionAndRotation(carriedPosition, carriedRotation);
        SetHeld(box, false);
    }

    static void SetHeld(GameObject box, bool held)
    {
        var body = box.GetComponent<Rigidbody>();
        if (body != null)
        {
            if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
            if (held) body.isKinematic = true;
        }
        // Carried boxes must not touch the frame triggers or block the crosshair.
        SetColliders(box, !held);
    }

    static void SetColliders(GameObject box, bool on)
    {
        foreach (var c in box.GetComponentsInChildren<Collider>(true)) c.enabled = on;
    }
}

/// <summary>A page box on a monitor or phone: use it to pick it up.</summary>
public class MemoryBoxTarget : MonoBehaviour, IInteractable
{
    MemoryScenarioAdapter adapter;
    int page;

    public void Init(MemoryScenarioAdapter owner, int pageNumber)
    {
        adapter = owner;
        page = pageNumber;
    }

    public string PromptText => adapter.BoxPrompt(gameObject, page);
    public void Interact() => adapter.PickUp(gameObject);
}

/// <summary>A frame pallet on a monitor or phone: use it to put the carried page there.</summary>
public class MemoryFrameTarget : MonoBehaviour, IInteractable
{
    MemoryScenarioAdapter adapter;
    int frame;

    public void Init(MemoryScenarioAdapter owner, int frameIndex)
    {
        adapter = owner;
        frame = frameIndex;
    }

    public string PromptText => adapter.FramePrompt(frame);
    public void Interact() => adapter.Place(frame);
}
