using System;
using UnityEngine;

/// <summary>
/// Real Windows text-to-speech (SAPI via System.Speech).
/// Permanently disabled per user request – self-destructs on Awake.
/// Kept as a stub so VoiceTutorialController (also disabled) still compiles.
/// </summary>
public class WindowsVoiceSpeaker : MonoBehaviour
{
    public static WindowsVoiceSpeaker Instance { get; private set; }

    public bool IsAvailable => false;
    public float Volume { get; set; } = 1f;

    private void Awake()
    {
        // Voice TTS permanently removed per user request
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
        Destroy(gameObject);
    }

    public void Speak(string text) { /* no-op */ }
    public void Stop() { /* no-op */ }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
