
using UnityEngine;

public class WelcomeNarration : MonoBehaviour
{
    [SerializeField] private AudioSource audioSource;

    private void Start()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource != null && audioSource.clip != null)
            audioSource.Play();
        else
            Debug.LogWarning("Welcome narration AudioSource or clip is missing.", this);
    }
}
