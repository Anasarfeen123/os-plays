using Gridlock.XR;
using UnityEngine;

/// <summary>
/// GRIDLOCK glue for the memory warehouse without a headset. In VR the page boxes are grabbed and dropped on the frame
/// pallets by hand; on a monitor or phone the player looks at (or taps) a box to pick it up and at a pallet to put it
/// there, which asks the placement manager exactly as dropping it in VR does. A wrong frame keeps the box in hand.
/// </summary>
public class MemoryScenarioAdapter : MonoBehaviour
{
    [SerializeField] MemoryPlacementManager manager;
    [SerializeField] GameObject[] boxes;
    [SerializeField] int[] pages;
    [Tooltip("Frame roots (pallets + trigger), with the frame index each one stands for.")]
    [SerializeField] Transform[] frames;
    [SerializeField] int[] frameIndices;
    [SerializeField] Transform carryAnchor;

    GameObject carried;
    Transform carriedParent;
    Vector3 carriedPosition;
    Quaternion carriedRotation;

    public GameObject Carried => carried;

    void Start()
    {
        var mode = XRModeController.Current;
        if (mode != null && mode.XRActive) return; // hands do it in VR
        for (int i = 0; i < boxes.Length; i++)
            if (boxes[i] != null) boxes[i].AddComponent<MemoryBoxTarget>().Init(this, pages[i]);
        for (int i = 0; i < frames.Length; i++)
            if (frames[i] != null) frames[i].gameObject.AddComponent<MemoryFrameTarget>().Init(this, frameIndices[i]);
    }

    void Update()
    {
        // The manager can take a carried box away (it evicted that page): it is no longer in hand.
        if (carried != null && carried.transform.parent != carryAnchor)
        {
            SetColliders(carried, true);
            carried = null;
        }
    }

    static string Verb => TouchGestures.IsTouchDevice ? "Tap" : "Press [E]";

    public string BoxPrompt(int page) => carried == null ? $"{Verb} to pick up page {page}" : $"{Verb} to swap for page {page}";
    public string FramePrompt(int frame) => carried == null ? $"Frame {frame + 1}: pick up a page box first" : $"{Verb} to put the page in frame {frame + 1}";

    public void PickUp(GameObject box)
    {
        if (box == carried) return;
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
        manager.TryPlaceBox(box, frameIndex);
        if (box.transform.parent == carryAnchor) return; // wrong frame: the manager explained why; keep holding it
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

    public string PromptText => adapter.BoxPrompt(page);
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
