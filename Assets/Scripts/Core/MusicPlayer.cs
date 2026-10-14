using System.Collections;
using UnityEngine;

namespace Gridlock.Core
{
    /// <summary>
    /// Background music that survives scene changes and crossfades to each scene's track (<see cref="MusicCue"/>).
    /// It keeps playing while the pause menu is open, and dips under the narrator during guided tours.
    /// </summary>
    public class MusicPlayer : MonoBehaviour
    {
        const float Level = 0.55f, DuckedLevel = 0.3f, FadeSeconds = 2.2f;

        static MusicPlayer instance;

        AudioSource[] decks;
        int live;
        float duck = 1f, duckTarget = 1f;

        public static AudioClip Current => instance != null ? instance.decks[instance.live].clip : null;

        /// <summary>Crossfade to <paramref name="clip"/> (no-op when it is already playing).</summary>
        public static void Play(AudioClip clip)
        {
            if (clip == null) return;
            if (instance == null)
            {
                var go = new GameObject("Music Player");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<MusicPlayer>();
            }
            instance.CrossfadeTo(clip);
        }

        /// <summary>Lower the music while the narrator speaks.</summary>
        public static void Duck(bool on)
        {
            if (instance != null) instance.duckTarget = on ? DuckedLevel / Level : 1f;
        }

        void Awake()
        {
            decks = new AudioSource[2];
            for (int i = 0; i < 2; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.loop = true;
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                s.ignoreListenerPause = true; // the pause menu keeps its music
                s.priority = 8;
                s.volume = 0f;
                decks[i] = s;
            }
        }

        void Update()
        {
            duck = Mathf.MoveTowards(duck, duckTarget, Time.unscaledDeltaTime * 1.5f);
            // Re-route every frame: the mixer belongs to the current scene, so it changes on scene loads.
            foreach (var s in decks)
                if (AudioRouting.MusicGroup != null && s.outputAudioMixerGroup != AudioRouting.MusicGroup)
                    s.outputAudioMixerGroup = AudioRouting.MusicGroup;
        }

        void CrossfadeTo(AudioClip clip)
        {
            if (decks[live].clip == clip && decks[live].isPlaying) return;
            StopAllCoroutines();
            StartCoroutine(Fade(clip));
        }

        IEnumerator Fade(AudioClip clip)
        {
            var from = decks[live];
            live = 1 - live;
            var to = decks[live];
            to.clip = clip;
            to.volume = 0f;
            to.Play();
            float start = from.volume / Mathf.Max(0.001f, duck * Level);
            for (float t = 0f; t < FadeSeconds; t += Time.unscaledDeltaTime)
            {
                float k = t / FadeSeconds;
                to.volume = Level * duck * k;
                from.volume = Level * duck * start * (1f - k);
                yield return null;
            }
            from.Stop();
            from.volume = 0f;
            while (true)
            {
                to.volume = Level * duck;
                yield return null;
            }
        }
    }
}
