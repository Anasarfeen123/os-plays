
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

    public void TryPlaceBox(GameObject box, int frameIndex)
    {
        if (processing || box == null)
            return;

        if (frameIndex < 0 || frameIndex >= frames.Length)
            return;

        if (referenceIndex >= referenceString.Length)
        {
            Show("Algorithm complete. Select an algorithm to play again.",
                FeedbackType.Normal);
            return;
        }

        int page = GetPageNumber(box);

        if (page < 1)
        {
            PlayIncorrectPlacementSound();
            Show("This box has no valid page number. Check its Page Boxes entry.",
                FeedbackType.Wrong);
            return;
        }

        int expectedPage = CurrentPage();

        if (page != expectedPage)
        {
            PlayIncorrectPlacementSound();
            Show($"WRONG PAGE\nPlace page {expectedPage} next.",
                FeedbackType.Wrong);
            return;
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
            return;
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
                return;
            }

            PutPageInFrame(page, box, frameIndex);
            faults++;
            loadedAt[frameIndex] = referenceIndex;
            lastUsed[frameIndex] = referenceIndex;

            Advance(
                $"CORRECT — PAGE FAULT\nPage {page} loaded into Frame {frameIndex + 1}.");
            return;
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
            return;
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

        box.transform.SetParent(frameSlots[frameIndex], false);
        box.transform.localPosition = Vector3.zero;
        box.transform.localRotation = Quaternion.identity;

        Rigidbody body = box.GetComponent<Rigidbody>();

        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
        }
    }

    private void ReturnBoxToStart(GameObject box)
    {
        if (box == null)
            return;

        Transform originalParent = startParents.TryGetValue(box, out Transform parent)
            ? parent
            : null;

        box.transform.SetParent(originalParent, true);

        if (startPositions.TryGetValue(box, out Vector3 position))
            box.transform.position = position;

        if (startRotations.TryGetValue(box, out Quaternion rotation))
            box.transform.rotation = rotation;

        Rigidbody body = box.GetComponent<Rigidbody>();

        if (body != null)
        {
            body.isKinematic = false;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
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
