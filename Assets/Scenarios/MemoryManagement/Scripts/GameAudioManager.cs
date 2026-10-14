
using UnityEngine;

public class GameAudioManager : MonoBehaviour
{
    public static GameAudioManager Instance { get; private set; }

    [Header("Audio Source")]
    [SerializeField] private AudioSource audioSource;

    [Header("Placement Sounds")]
    [SerializeField] private AudioClip correctPlacement;
    [SerializeField] private AudioClip incorrectPlacement;

    [Header("Quiz Sounds")]
    [SerializeField] private AudioClip correctAnswer;
    [SerializeField] private AudioClip incorrectAnswer;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    public void PlayCorrectPlacement()
    {
        PlaySound(correctPlacement);
    }

    public void PlayIncorrectPlacement()
    {
        PlaySound(incorrectPlacement);
    }

    public void PlayCorrectAnswer()
    {
        PlaySound(correctAnswer);
    }

    public void PlayIncorrectAnswer()
    {
        PlaySound(incorrectAnswer);
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource == null || clip == null)
            return;

        audioSource.PlayOneShot(clip);
    }
}
