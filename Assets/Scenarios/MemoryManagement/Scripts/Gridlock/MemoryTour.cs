using System.Collections;
using Gridlock.Guide;
using UnityEngine;

/// <summary>
/// Narrated guided tour of the memory warehouse (about two minutes): frames, pages and the reference string; FIFO is
/// chosen; the learner loads the first page (the guide does it after a hint if nobody does), keeps loading until the
/// frames are full and the first page has to leave, and the narrator explains the victim and the other policies.
/// Replaces the scenario's own welcome voice-over on a first visit.
/// </summary>
public class MemoryTour : NarratedTour
{
    const string TourKey = "memory";

    public static bool Seen
    {
        get => IsSeen(TourKey);
        set => SetSeen(TourKey, value);
    }

    [SerializeField] MemoryPlacementManager manager;
    [SerializeField] WelcomeSequenceController welcome;
    [Tooltip("Seconds without a move on a hands-on step before the hint, and again before the guide makes the move.")]
    [SerializeField] float moveWaitSeconds = 18f;

    protected override string Key => TourKey;
    protected override string HandsOnStep => "mem_turn";
    protected override bool Counted(string id) => id != "mem_hint";

    public void Init(MemoryPlacementManager placementManager, WelcomeSequenceController welcomeSequence)
    {
        manager = placementManager;
        welcome = welcomeSequence;
    }

    protected override void PrepareFirstRun()
    {
        if (welcome != null) welcome.SkipToAlgorithms();
    }

    protected override void OnBegin()
    {
        if (welcome != null) welcome.SkipToAlgorithms();
    }

    static Vector3 Above(GameObject go, float lift = 0.3f)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return go.transform.position + Vector3.up * lift;
        var b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        return new Vector3(b.center.x, b.max.y + lift, b.center.z);
    }

    void PointAtFrame(int frame, string label) => marker.Point(manager.FrameSlot(frame).position + Vector3.up * 0.45f, label);

    void PointAtNextBox()
    {
        var box = manager.BoxFor(manager.NextPage);
        if (box != null) marker.Point(Above(box), $"PAGE {manager.NextPage}");
    }

    void PointAtScreen(string label)
    {
        var screen = GameObject.Find("UI");
        if (screen != null) marker.Point(screen.transform.position + Vector3.up * 1.1f, label);
    }

    /// <summary>The next page goes into memory as the learner would have done it (the guide's own move).</summary>
    void LoadNextPage()
    {
        var box = manager.BoxFor(manager.NextPage);
        int frame = manager.EmptyFrame;
        if (box != null) manager.TryPlaceBox(box, frame >= 0 ? frame : 0);
    }

    /// <summary>Wait for the learner's next move; after a while a hint, then the guide makes the move itself.</summary>
    IEnumerator NextMove(bool hint)
    {
        int before = manager.ReferenceIndex;
        float deadline = Time.time + LineLength + moveWaitSeconds;
        yield return new WaitUntil(() => manager.ReferenceIndex > before || Time.time > deadline);
        if (manager.ReferenceIndex > before) yield break;
        if (hint)
        {
            Say("mem_hint");
            PointAtNextBox();
            deadline = Time.time + LineLength + moveWaitSeconds;
            yield return new WaitUntil(() => manager.ReferenceIndex > before || Time.time > deadline);
        }
        if (manager.ReferenceIndex == before)
        {
            yield return new WaitWhile(() => manager.Busy);
            LoadNextPage();
        }
        yield return new WaitWhile(() => manager.Busy);
    }

    protected override IEnumerator Script()
    {
        yield return new WaitForSeconds(1.2f); // let the scene fade in

        Say("mem_welcome");
        PointAtFrame(1, "PHYSICAL MEMORY");
        yield return EndOfLine();

        Say("mem_frames");
        PointAtFrame(0, "FRAME 1");
        yield return At(0.45f);
        PointAtFrame(1, "FRAME 2");
        yield return At(0.7f);
        PointAtFrame(2, "FRAME 3");
        yield return EndOfLine();

        Say("mem_pages");
        var first = manager.BoxFor(1);
        if (first != null) marker.Point(Above(first), "PAGE 1");
        yield return At(0.6f);
        PointAtScreen("REFERENCE STRING");
        yield return EndOfLine();

        Say("mem_algorithm");
        manager.SelectFIFO();
        PointAtScreen("FIFO");
        yield return EndOfLine();

        // Hands-on: the first page.
        Say("mem_turn");
        PointAtNextBox();
        yield return NextMove(hint: true);

        Say("mem_fault");
        marker.Hide();
        // Keep loading until the frames are full and one more page has had to push another out.
        while (manager.ReferenceIndex <= manager.FrameCount && manager.NextPage > 0)
        {
            PointAtNextBox();
            yield return NextMove(hint: false);
        }
        yield return EndOfLine();

        Say("mem_victim");
        PointAtFrame(0, "FIRST IN, FIRST OUT");
        yield return EndOfLine();

        Say("mem_outro");
        PointAtScreen("TRY LRU · OPTIMAL");
        yield return EndOfLine();
    }
}
