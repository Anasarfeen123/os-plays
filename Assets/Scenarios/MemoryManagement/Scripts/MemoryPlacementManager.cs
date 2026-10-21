
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class MemoryPlacementManager : MonoBehaviour
{
    public enum Algorithm { FIFO, LRU, Optimal }

    [System.Serializable]
    public class PageBoxData
    {
        public GameObject box;
        public int pageNumber;
    }

    [Header("Algorithm")]
    [SerializeField] private Algorithm selectedAlgorithm = Algorithm.FIFO;

    [Header("Reference String")]
    [SerializeField] private int[] referenceString = { 1, 2, 3, 4, 1, 2 };

    [Header("Memory Frame Snap Points")]
    [SerializeField] private Transform[] frameSlots = new Transform[3];

    [Header("Physical Page Boxes")]
    [SerializeField] private PageBoxData[] pageBoxes = new PageBoxData[6];

    [Header("Feedback UI")]
    [SerializeField] private TMP_Text feedbackText;
    [SerializeField] private Image feedbackBackground;
    [SerializeField] private float feedbackDuration = 3f;
    [SerializeField]
    private Color normalColor =
        new Color(0.04f, 0.07f, 0.13f, 0.95f);
    [SerializeField]
    private Color correctColor =
        new Color(0.05f, 0.45f, 0.20f, 0.95f);
    [SerializeField]
    private Color wrongColor =
        new Color(0.65f, 0.08f, 0.10f, 0.95f);

    private readonly int[] frames = { -1, -1, -1 };
    private readonly GameObject[] residentBoxes = new GameObject[3];
    private readonly int[] loadedAt = new int[3];
    private readonly int[] lastUsed = new int[3];

    private readonly Dictionary<GameObject, Vector3> startPositions = new();
    private readonly Dictionary<GameObject, Quaternion> startRotations = new();
    private readonly Dictionary<GameObject, Transform> startParents = new();

    private int referenceIndex;
    private int faults;
    private int hits;
    private bool processing;
    private Coroutine feedbackRoutine;

    private enum FeedbackType { Normal, Correct, Wrong }

    private void Awake()
    {
        foreach (PageBoxData data in pageBoxes)
        {
            if (data == null || data.box == null)
                continue;

            GameObject box = data.box;
            startPositions[box] = box.transform.position;
            startRotations[box] = box.transform.rotation;
            startParents[box] = box.transform.parent;
        }

        if (feedbackBackground != null)
            feedbackBackground.color = normalColor;

        Show("Select an algorithm to begin.", FeedbackType.Normal);
    }

    /// <summary>
    /// GRIDLOCK: play a different reference string (pages 1–6, the boxes there are) with the current algorithm. Shows the
    /// whole string, since the screen otherwise only names the next page.
    /// </summary>
    public void SetReferenceString(int[] pages)
    {
        referenceString = pages;
        ResetGame();
        Show($"Reference string: {string.Join(" ", pages)}\nPlace page {CurrentPage()}.", FeedbackType.Normal);
    }

    public int[] ReferenceString => referenceString;

    // GRIDLOCK: read by the guided tour.
    public int ReferenceIndex => referenceIndex;
    public int Faults => faults;
    public int Hits => hits;
    public int NextPage => CurrentPage();
    public int FrameCount => frameSlots.Length;
    public int EmptyFrame => FindEmptyFrame();
    public bool Busy => processing;
    public Transform FrameSlot(int frame) => frameSlots[frame];
    public GameObject BoxFor(int page) => System.Array.Find(pageBoxes, p => p.pageNumber == page)?.box;

    public void SelectFIFO() => SelectAlgorithm(Algorithm.FIFO);
    public void SelectLRU() => SelectAlgorithm(Algorithm.LRU);
    public void SelectOptimal() => SelectAlgorithm(Algorithm.Optimal);

    public void SelectAlgorithm(Algorithm algorithm)
    {
        selectedAlgorithm = algorithm;
        ResetGame();

        Show($"{algorithm} selected.\nPlace page {CurrentPage()}.",
            FeedbackType.Normal);
    }

    /// <summary>
    /// Asks to load <paramref name="box"/>'s page into frame <paramref name="frameIndex"/>. True when that was the right
    /// move (the page is loaded, or it was a hit and the box went back to its shelf); false leaves the box where it is.
    /// </summary>
    public bool TryPlaceBox(GameObject box, int frameIndex)
    {
        if (processing || box == null)
            return false;

        if (frameIndex < 0 || frameIndex >= frames.Length)
            return false;

        if (referenceIndex >= referenceString.Length)
        {
            Show("Algorithm complete. Select an algorithm to play again.",
                FeedbackType.Normal);
            return false;
        }

        int page = GetPageNumber(box);

        if (page < 1)
        {
            PlayIncorrectPlacementSound();
            Show("This box has no valid page number. Check its Page Boxes entry.",
                FeedbackType.Wrong);
            return false;
        }

        int expectedPage = CurrentPage();

        if (page != expectedPage)
        {
            PlayIncorrectPlacementSound();
            Show($"WRONG PAGE\nPlace page {expectedPage} next.",
                FeedbackType.Wrong);
            return false;
        }

        processing = true;

        // A hit: the page is already in memory.
        int existingFrame = FindPage(page);

        if (existingFrame >= 0)
        {
            ReturnBoxToStart(box);
            hits++;
            lastUsed[existingFrame] = referenceIndex;

            Advance(
                $"CORRECT — HIT\nPage {page} is already in Frame {existingFrame + 1}.");
            return true;
        }

        // Empty frames must be filled before replacement.
        int emptyFrame = FindEmptyFrame();

        if (emptyFrame >= 0)
        {
            if (frameIndex != emptyFrame)
            {
                PlayIncorrectPlacementSound();

                Show(
                    $"NOT QUITE\nPlace page {page} in the first empty frame: Frame {emptyFrame + 1}.",
                    FeedbackType.Wrong);

                processing = false;
                return false;
            }

            PutPageInFrame(page, box, frameIndex);
            faults++;
            loadedAt[frameIndex] = referenceIndex;
            lastUsed[frameIndex] = referenceIndex;

            Advance(
                $"CORRECT — PAGE FAULT\nPage {page} loaded into Frame {frameIndex + 1}.");
            return true;
        }

        // Memory is full: calculate the correct victim.
        int victimFrame = GetVictimFrame();

        if (frameIndex != victimFrame)
        {
            PlayIncorrectPlacementSound();

            Show(
                $"INCORRECT FRAME\nPage {page} must replace the page in Frame {victimFrame + 1}.\n{GetVictimReason(victimFrame)}",
                FeedbackType.Wrong);

            processing = false;
            return false;
        }

        int evictedPage = frames[victimFrame];
        GameObject evictedBox = residentBoxes[victimFrame];

        if (evictedBox != null)
            ReturnBoxToStart(evictedBox);

        PutPageInFrame(page, box, victimFrame);
        faults++;
        loadedAt[victimFrame] = referenceIndex;
        lastUsed[victimFrame] = referenceIndex;

        Advance(
            $"CORRECT — PAGE FAULT\nPage {page} replaced page {evictedPage} in Frame {victimFrame + 1}.");
        return true;
    }

    /// <summary>The box's page is loaded in a frame (it sits on that frame's pallet and cannot be taken off by hand).</summary>
    public bool IsResident(GameObject box) => System.Array.IndexOf(residentBoxes, box) >= 0;

    /// <summary>The frame a resident box is in (0-based), or -1.</summary>
    public int FrameOf(GameObject box) => System.Array.IndexOf(residentBoxes, box);

    /// <summary>GRIDLOCK: the box (page) in a frame, or null.</summary>
    public GameObject ResidentIn(int frame) => frame >= 0 && frame < residentBoxes.Length ? residentBoxes[frame] : null;

    /// <summary>GRIDLOCK: the page number a box stands for.</summary>
    public int PageOf(GameObject box) => GetPageNumber(box);

    /// <summary>A box that is not in memory goes back to its shelf (dropped somewhere else, or on the wrong frame).</summary>
    public void ReturnToStart(GameObject box)
    {
        if (box != null && !IsResident(box))
            ReturnBoxToStart(box);
    }

    /// <summary>
    /// Pages in memory are moved by the algorithm, not by hand: a resident box cannot be grabbed until it is evicted,
    /// so the frames always show what the manager holds.
    /// </summary>
    private static void SetGrabbable(GameObject box, bool grabbable)
    {
        var grab = box.GetComponent<XRGrabInteractable>();
        if (grab != null)
            grab.enabled = grabbable;
    }

    private void PlayCorrectPlacementSound()
    {
        if (GameAudioManager.Instance != null)
            GameAudioManager.Instance.PlayCorrectPlacement();
    }

    private void PlayIncorrectPlacementSound()
    {
        if (GameAudioManager.Instance != null)
            GameAudioManager.Instance.PlayIncorrectPlacement();
    }

    private int GetPageNumber(GameObject box)
    {
        foreach (PageBoxData data in pageBoxes)
        {
            if (data != null && data.box == box)
                return data.pageNumber;
        }

        return -1;
    }

    private int CurrentPage()
    {
        return referenceIndex < referenceString.Length
            ? referenceString[referenceIndex]
            : -1;
    }

    private int FindPage(int page)
    {
        for (int i = 0; i < frames.Length; i++)
        {
            if (frames[i] == page)
                return i;
        }

        return -1;
    }

    private int FindEmptyFrame()
    {
        for (int i = 0; i < frames.Length; i++)
        {
            if (frames[i] == -1)
                return i;
        }

        return -1;
    }

    private int GetVictimFrame()
    {
        int victim = 0;

        if (selectedAlgorithm == Algorithm.FIFO)
        {
            for (int i = 1; i < frames.Length; i++)
            {
                if (loadedAt[i] < loadedAt[victim])
                    victim = i;
            }

            return victim;
        }

        if (selectedAlgorithm == Algorithm.LRU)
        {
            for (int i = 1; i < frames.Length; i++)
            {
                if (lastUsed[i] < lastUsed[victim])
                    victim = i;
            }

            return victim;
        }

        int farthest = -1;

        for (int i = 0; i < frames.Length; i++)
        {
            int nextUse = int.MaxValue;

            for (int j = referenceIndex + 1; j < referenceString.Length; j++)
            {
                if (referenceString[j] == frames[i])
                {
                    nextUse = j;
                    break;
                }
            }

            if (nextUse > farthest)
            {
                farthest = nextUse;
                victim = i;
            }
        }

        return victim;
    }

    private string GetVictimReason(int victim)
    {
        int page = frames[victim];

        if (selectedAlgorithm == Algorithm.FIFO)
            return $"FIFO replaces page {page}, which entered memory earliest.";

        if (selectedAlgorithm == Algorithm.LRU)
            return $"LRU replaces page {page}, which was used least recently.";

        for (int i = referenceIndex + 1; i < referenceString.Length; i++)
        {
            if (referenceString[i] == page)
                return $"Optimal replaces page {page}, whose next use is farthest away (position {i + 1}).";
        }

        return $"Optimal replaces page {page}, which is never needed again.";
    }

    private void PutPageInFrame(int page, GameObject box, int frameIndex)
    {
        frames[frameIndex] = page;
        residentBoxes[frameIndex] = box;
        SetGrabbable(box, false);

        // GRIDLOCK: glides onto the pallet instead of jumping there.
        BoxGlide.To(box, frameSlots[frameIndex], Vector3.zero, Quaternion.identity, kinematicAfter: true);
    }

    private void ReturnBoxToStart(GameObject box)
    {
        if (box == null)
            return;

        SetGrabbable(box, true);

        Transform originalParent = startParents.TryGetValue(box, out Transform parent)
            ? parent
            : null;

        Vector3 position = startPositions.TryGetValue(box, out Vector3 p) ? p : box.transform.position;
        Quaternion rotation = startRotations.TryGetValue(box, out Quaternion r) ? r : box.transform.rotation;
        if (originalParent != null)
        {
            position = originalParent.InverseTransformPoint(position);
            rotation = Quaternion.Inverse(originalParent.rotation) * rotation;
        }

        // GRIDLOCK: glides back to its table instead of jumping there.
        BoxGlide.To(box, originalParent, position, rotation, kinematicAfter: false);
    }

    private void Advance(string message)
    {
        PlayCorrectPlacementSound();

        referenceIndex++;

        string next = referenceIndex < referenceString.Length
            ? $"\nNext page: {CurrentPage()}."
            : $"\nSequence complete! Hits: {hits}, Faults: {faults}.";

        Show(message + next, FeedbackType.Correct);
        processing = false;
    }

    public void ResetGame()
    {
        if (feedbackRoutine != null)
        {
            StopCoroutine(feedbackRoutine);
            feedbackRoutine = null;
        }

        for (int i = 0; i < frames.Length; i++)
        {
            if (residentBoxes[i] != null)
                ReturnBoxToStart(residentBoxes[i]);

            frames[i] = -1;
            residentBoxes[i] = null;
            loadedAt[i] = 0;
            lastUsed[i] = 0;
        }

        referenceIndex = 0;
        faults = 0;
        hits = 0;
        processing = false;

        foreach (PageBoxData data in pageBoxes)
        {
            if (data != null && data.box != null)
                ReturnBoxToStart(data.box);
        }

        Show($"Reset.\nPlace page {CurrentPage()}.", FeedbackType.Normal);
    }

    private void Show(string message, FeedbackType type)
    {
        if (feedbackRoutine != null)
        {
            StopCoroutine(feedbackRoutine);
            feedbackRoutine = null;
        }

        if (feedbackText != null)
            feedbackText.text = message;

        if (feedbackBackground != null)
        {
            feedbackBackground.color = type switch
            {
                FeedbackType.Correct => correctColor,
                FeedbackType.Wrong => wrongColor,
                _ => normalColor
            };
        }

        Debug.Log(message);

        if (type != FeedbackType.Normal && feedbackDuration > 0f)
            feedbackRoutine = StartCoroutine(RestoreNormalColor());
    }

    private IEnumerator RestoreNormalColor()
    {
        yield return new WaitForSeconds(feedbackDuration);

        if (feedbackBackground != null)
            feedbackBackground.color = normalColor;

        feedbackRoutine = null;
    }
}
