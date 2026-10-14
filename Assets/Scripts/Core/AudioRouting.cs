using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

namespace Gridlock.Core
{
    /// <summary>
    /// Sends every sound through the GRIDLOCK mixer so the menu's sliders work: music and narration are routed
    /// explicitly, and anything else (cars, UI, ambience, one-shots created at runtime) lands on the Effects group.
    /// One per scene; applies the saved volumes whenever a setting changes.
    /// </summary>
    public class AudioRouting : MonoBehaviour
    {
        [SerializeField] AudioMixer mixer;
        [SerializeField] AudioMixerGroup music;
        [SerializeField] AudioMixerGroup effects;
        [SerializeField] AudioMixerGroup voice;

        public static AudioRouting Instance { get; private set; }
        public static AudioMixerGroup MusicGroup => Instance != null ? Instance.music : null;
        public static AudioMixerGroup VoiceGroup => Instance != null ? Instance.voice : null;

        public void Init(AudioMixer audioMixer, AudioMixerGroup musicGroup, AudioMixerGroup effectsGroup, AudioMixerGroup voiceGroup)
        {
            mixer = audioMixer;
            music = musicGroup;
            effects = effectsGroup;
            voice = voiceGroup;
        }

        void Awake()
        {
            Instance = this;
            GameSettings.Changed += Apply;
        }

        void OnDestroy()
        {
            GameSettings.Changed -= Apply;
            if (Instance == this) Instance = null;
        }

        IEnumerator Start()
        {
            Apply(); // exposed mixer parameters can only be set once the scene is running
            var wait = new WaitForSecondsRealtime(0.5f);
            while (true)
            {
                RouteStray();
                yield return wait;
            }
        }

        void Apply()
        {
            if (mixer == null) return;
            mixer.SetFloat("MusicVolume", GameSettings.ToDecibels(GameSettings.Music));
            mixer.SetFloat("EffectsVolume", GameSettings.ToDecibels(GameSettings.Effects));
            mixer.SetFloat("VoiceVolume", GameSettings.ToDecibels(GameSettings.Voice));
        }

        /// <summary>Anything not yet routed is a sound effect.</summary>
        void RouteStray()
        {
            foreach (var source in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
                if (source.outputAudioMixerGroup == null) source.outputAudioMixerGroup = effects;
        }
    }
}
