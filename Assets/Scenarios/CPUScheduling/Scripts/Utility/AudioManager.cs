using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central audio manager for the CPU Scheduling Lab.
/// Generates all sound effects procedurally at runtime (no external audio assets required),
/// and provides separate Sound-Effect and Voice volume controls with mute toggles.
/// Persists user volume preferences via PlayerPrefs.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    public enum SfxType
    {
        Click,
        Success,
        Error,
        Tick,
        Complete,
        Arrival,
        Preempt,
        Step
    }

    [Header("Master Volume")]
    [Range(0f, 1f)] public float masterVolume = 1f;

    [Header("Sound Effect Volume")]
    [Range(0f, 1f)] public float sfxVolume = 1f;

    [Header("Voice / Narration Volume")]
    [Range(0f, 1f)] public float voiceVolume = 1f;

    public bool MuteSfx { get; private set; }
    public bool MuteVoice { get; private set; }

    public event Action OnSfxVolumeChanged;
    public event Action OnVoiceVolumeChanged;
    public event Action OnMuteChanged;

    private AudioSource sfxSource;
    private AudioSource voiceSource;
    private Dictionary<SfxType, AudioClip> sfxClips = new Dictionary<SfxType, AudioClip>();

    private const float SfxSampleRate = 44100f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.spatialBlend = 0f;

        voiceSource = gameObject.AddComponent<AudioSource>();
        voiceSource.playOnAwake = false;
        voiceSource.spatialBlend = 0f;

        LoadPreferences();
    }

    private void Start()
    {
        GenerateAllSfx();
    }

    private void LoadPreferences()
    {
        masterVolume = PlayerPrefs.GetFloat("Audio_Master", 1f);
        sfxVolume = PlayerPrefs.GetFloat("Audio_Sfx", 1f);
        voiceVolume = PlayerPrefs.GetFloat("Audio_Voice", 1f);
        MuteSfx = PlayerPrefs.GetInt("Audio_MuteSfx", 0) == 1;
        MuteVoice = PlayerPrefs.GetInt("Audio_MuteVoice", 0) == 1;
    }

    private void SavePreferences()
    {
        PlayerPrefs.SetFloat("Audio_Master", masterVolume);
        PlayerPrefs.SetFloat("Audio_Sfx", sfxVolume);
        PlayerPrefs.SetFloat("Audio_Voice", voiceVolume);
        PlayerPrefs.SetInt("Audio_MuteSfx", MuteSfx ? 1 : 0);
        PlayerPrefs.SetInt("Audio_MuteVoice", MuteVoice ? 1 : 0);
    }

    // ─────────────────────────────────────────────────────────────
    // Procedural SFX generation (no external audio files needed)
    // ─────────────────────────────────────────────────────────────
    private void GenerateAllSfx()
    {
        sfxClips[SfxType.Click] = GenerateTone(880f, 0.06f, 0.35f, EnvelopeType.Decay);
        sfxClips[SfxType.Tick] = GenerateTone(1400f, 0.03f, 0.25f, EnvelopeType.Decay);
        sfxClips[SfxType.Step] = GenerateTone(620f, 0.04f, 0.22f, EnvelopeType.Decay);
        sfxClips[SfxType.Success] = GenerateSequence(new[] { 523f, 784f }, 0.12f, 0.4f);
        sfxClips[SfxType.Error] = GenerateSequence(new[] { 330f, 196f }, 0.14f, 0.45f, WaveType.Sawtooth);
        sfxClips[SfxType.Arrival] = GenerateTone(988f, 0.09f, 0.3f, EnvelopeType.Swell);
        sfxClips[SfxType.Preempt] = GenerateSweep(400f, 1200f, 0.18f, 0.4f);
        sfxClips[SfxType.Complete] = GenerateSequence(new[] { 523f, 659f, 784f, 1047f }, 0.14f, 0.5f);
    }

    private enum WaveType { Sine, Square, Sawtooth }
    private enum EnvelopeType { Decay, Swell, Sustain }

    private AudioClip GenerateTone(float frequency, float duration, float amplitude, EnvelopeType envelope)
    {
        return GenerateWave(WaveType.Sine, frequency, duration, amplitude, envelope);
    }

    private AudioClip GenerateWave(WaveType wave, float frequency, float duration, float amplitude, EnvelopeType envelope)
    {
        int samples = Mathf.CeilToInt(duration * SfxSampleRate);
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = i / SfxSampleRate;
            float phase = 2f * Mathf.PI * frequency * t;
            float v = 0f;
            switch (wave)
            {
                case WaveType.Sine: v = Mathf.Sin(phase); break;
                case WaveType.Square: v = Mathf.Sign(Mathf.Sin(phase)) * 0.6f; break;
                case WaveType.Sawtooth: v = (2f * (frequency * t - Mathf.Floor(0.5f + frequency * t))) * 0.5f; break;
            }
            float env = 1f;
            switch (envelope)
            {
                case EnvelopeType.Decay: env = Mathf.Exp(-3f * t / duration); break;
                case EnvelopeType.Swell: env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / duration)); break;
                case EnvelopeType.Sustain: env = Mathf.Clamp01(t / (duration * 0.2f)); break;
            }
            data[i] = v * amplitude * env;
        }
        AudioClip clip = AudioClip.Create("Sfx_" + wave + "_" + frequency, samples, 1, (int)SfxSampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip GenerateSequence(float[] frequencies, float noteDuration, float amplitude)
    {
        return GenerateSequence(frequencies, noteDuration, amplitude, WaveType.Sine);
    }

    private AudioClip GenerateSequence(float[] frequencies, float noteDuration, float amplitude, WaveType wave)
    {
        int noteSamples = Mathf.CeilToInt(noteDuration * SfxSampleRate);
        int total = noteSamples * frequencies.Length;
        float[] data = new float[total];
        for (int n = 0; n < frequencies.Length; n++)
        {
            for (int i = 0; i < noteSamples; i++)
            {
                float t = i / SfxSampleRate;
                float phase = 2f * Mathf.PI * frequencies[n] * t;
                float v = 0f;
                switch (wave)
                {
                    case WaveType.Sine: v = Mathf.Sin(phase); break;
                    case WaveType.Square: v = Mathf.Sign(Mathf.Sin(phase)) * 0.5f; break;
                    case WaveType.Sawtooth: v = (2f * (frequencies[n] * t - Mathf.Floor(0.5f + frequencies[n] * t))) * 0.4f; break;
                }
                float env = Mathf.Exp(-2.5f * t / noteDuration);
                data[n * noteSamples + i] = v * amplitude * env;
            }
        }
        AudioClip clip = AudioClip.Create("Sfx_Seq", total, 1, (int)SfxSampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip GenerateSweep(float fromFreq, float toFreq, float duration, float amplitude)
    {
        int samples = Mathf.CeilToInt(duration * SfxSampleRate);
        float[] data = new float[samples];
        float phase = 0f;
        for (int i = 0; i < samples; i++)
        {
            float t = i / SfxSampleRate;
            float freq = Mathf.Lerp(fromFreq, toFreq, t / duration);
            phase += 2f * Mathf.PI * freq / SfxSampleRate;
            float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / duration));
            data[i] = Mathf.Sin(phase) * amplitude * env;
        }
        AudioClip clip = AudioClip.Create("Sfx_Sweep", samples, 1, (int)SfxSampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // ─────────────────────────────────────────────────────────────
    // Playback
    // ─────────────────────────────────────────────────────────────
    public void PlaySfx(SfxType type)
    {
        if (MuteSfx || sfxSource == null) return;
        if (!sfxClips.TryGetValue(type, out AudioClip clip) || clip == null) return;
        sfxSource.PlayOneShot(clip, sfxVolume * masterVolume);
    }

    public void PlayVoiceClip(AudioClip clip)
    {
        if (MuteVoice || voiceSource == null || clip == null) return;
        voiceSource.PlayOneShot(clip, voiceVolume * masterVolume);
    }

    public void StopVoice()
    {
        if (voiceSource != null) voiceSource.Stop();
    }

    public bool IsVoicePlaying => voiceSource != null && voiceSource.isPlaying;

    // ─────────────────────────────────────────────────────────────
    // Volume / mute controls
    // ─────────────────────────────────────────────────────────────
    public void SetMasterVolume(float v)
    {
        masterVolume = Mathf.Clamp01(v);
        SavePreferences();
    }

    public void SetSfxVolume(float v)
    {
        sfxVolume = Mathf.Clamp01(v);
        SavePreferences();
        OnSfxVolumeChanged?.Invoke();
    }

    public void SetVoiceVolume(float v)
    {
        voiceVolume = Mathf.Clamp01(v);
        SavePreferences();
        OnVoiceVolumeChanged?.Invoke();
    }

    public void ToggleMuteSfx()
    {
        MuteSfx = !MuteSfx;
        SavePreferences();
        OnMuteChanged?.Invoke();
    }

    public void ToggleMuteVoice()
    {
        MuteVoice = !MuteVoice;
        if (MuteVoice) StopVoice();
        SavePreferences();
        OnMuteChanged?.Invoke();
    }
}
