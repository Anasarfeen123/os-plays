
using System.Collections;
using UnityEngine;

public class WelcomeSequenceController : MonoBehaviour
{
    [Header("Audio Clips")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip tutorialAudio;
    [SerializeField] private AudioClip memorySystemAudio;

    [Header("Panels")]
    [SerializeField] private GameObject welcomePanel;
    [SerializeField] private GameObject algorithmPanel;

    private bool sequenceStarted;

    public void StartTutorialSequence()
    {
        if (sequenceStarted)
            return;

        sequenceStarted = true;
        welcomePanel.SetActive(false);
        StartCoroutine(PlayTutorialSequence());
    }

    private IEnumerator PlayTutorialSequence()
    {
        yield return PlayClip(tutorialAudio);
        yield return PlayClip(memorySystemAudio);

        algorithmPanel.SetActive(true);
    }

    private IEnumerator PlayClip(AudioClip clip)
    {
        if (clip == null || audioSource == null)
            yield break;

        audioSource.Stop();
        audioSource.clip = clip;
        audioSource.Play();

        yield return new WaitWhile(() => audioSource.isPlaying);
    }
}
