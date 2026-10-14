
using System.Collections;
using UnityEngine;
using TMPro;

public class AlgorithmCompletionManager : MonoBehaviour
{
    [Header("Screens")]
    [SerializeField] private GameObject algoScreen;
    [SerializeField] private GameObject quizScreen;

    [Header("Completion Message")]
    [SerializeField] private GameObject completionPanel;
    [SerializeField] private TMP_Text completionText;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip completionAndQuizAudio;

    private bool fifoCompleted;
    private bool lruCompleted;
    private bool optimalCompleted;
    private bool completionSequenceStarted;

    public void CompleteFIFO()
    {
        fifoCompleted = true;
        CheckCompletion();
    }

    public void CompleteLRU()
    {
        lruCompleted = true;
        CheckCompletion();
    }

    public void CompleteOptimal()
    {
        optimalCompleted = true;
        CheckCompletion();
    }

    private void CheckCompletion()
    {
        bool allCompleted =
            fifoCompleted && lruCompleted && optimalCompleted;

        if (algoScreen != null)
            algoScreen.SetActive(true);

        if (allCompleted && !completionSequenceStarted)
        {
            completionSequenceStarted = true;
            StartCoroutine(PlayCompletionSequence());
        }
    }

    private IEnumerator PlayCompletionSequence()
    {
        if (quizScreen != null)
            quizScreen.SetActive(false);

        if (completionPanel != null)
            completionPanel.SetActive(true);

        if (completionText != null)
            completionText.text =
                "You have completed all algorithms!";

        if (audioSource != null && completionAndQuizAudio != null)
        {
            audioSource.Stop();
            audioSource.clip = completionAndQuizAudio;
            audioSource.Play();

            yield return new WaitWhile(() => audioSource.isPlaying);
        }

        if (completionPanel != null)
            completionPanel.SetActive(false);

        if (quizScreen != null)
            quizScreen.SetActive(true);
    }
}
