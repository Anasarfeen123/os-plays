using UnityEngine;

/// <summary>
/// One memory frame: the pallet (on the rack) where its page box goes. A box released anywhere in the frame's column —
/// on the pallet, or in front of the rack below it, so nobody has to reach above their head — counts as put on that
/// frame. The drop itself is handled by <see cref="MemoryScenarioAdapter"/>, which asks the placement manager.
/// </summary>
public class MemoryFrameZone : MonoBehaviour
{
    [SerializeField] private MemoryPlacementManager manager;
    [SerializeField, Range(0, 2)] private int frameIndex;

    [Tooltip("How far the drop volume reaches below the pallet, above it, and towards / behind the rack (metres).")]
    [SerializeField] private float reachBelow = 1.15f;
    [SerializeField] private float reachAbove = 0.6f;
    [SerializeField] private float reachDepth = 0.9f;

    public int FrameIndex => frameIndex;
    public MemoryPlacementManager Manager => manager;

    /// <summary>The drop volume in world space: the pallet trigger's box, stretched down, up and out from the rack.</summary>
    public Bounds DropVolume
    {
        get
        {
            var trigger = GetComponent<Collider>();
            var b = trigger != null ? trigger.bounds : new Bounds(transform.position, Vector3.one);
            float bottom = b.min.y - reachBelow, top = b.max.y + reachAbove;
            var size = new Vector3(b.size.x, top - bottom, b.size.z + 2f * reachDepth);
            return new Bounds(new Vector3(b.center.x, (top + bottom) * 0.5f, b.center.z), size);
        }
    }

    public bool Contains(Vector3 point) => DropVolume.Contains(point);
}
