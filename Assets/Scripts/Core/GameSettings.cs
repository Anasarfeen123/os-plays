using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gridlock.Core
{
    /// <summary>Every player setting. Values are stored as floats (choices as their index).</summary>
    public enum Setting
    {
        // Audio
        Master, Music, Effects, Voice,
        // Display
        Quality, PostEffects, Shadows, Subtitles,
        // Controls
        LookSpeed, InvertLook, SmoothTurn, SnapAngle, SmoothMove,
        // Gameplay
        AutoTours, CarSpeed,
        // Display (added later: kept last so saved values keep their keys)
        MixedReality,
    }

    /// <summary>
    /// Player settings, saved in PlayerPrefs and applied on start-up and whenever they change (<see cref="SettingsApplier"/>,
    /// <see cref="AudioRouting"/> and the cameras/cars read them).
    /// </summary>
    public static class GameSettings
    {
        static readonly Dictionary<Setting, float> Defaults = new Dictionary<Setting, float>
        {
            { Setting.Master, 1f }, { Setting.Music, 0.6f }, { Setting.Effects, 0.85f }, { Setting.Voice, 1f },
            { Setting.Quality, 1f }, // detail preset: 0 performance, 1 balanced, 2 quality
            { Setting.PostEffects, 1f }, { Setting.Shadows, 1f }, { Setting.Subtitles, 1f },
            { Setting.LookSpeed, 0.5f }, { Setting.InvertLook, 0f }, { Setting.SmoothTurn, 0f }, { Setting.SnapAngle, 1f },
            { Setting.SmoothMove, 1f },
            { Setting.AutoTours, 1f }, { Setting.CarSpeed, 1f },
            { Setting.MixedReality, 0f }, // Quest: passthrough with the scene as a tabletop model
        };

        static readonly Dictionary<Setting, float> values = new Dictionary<Setting, float>();
        static bool loaded;

        /// <summary>Raised after any setting changes.</summary>
        public static event Action Changed;

        public static float Get(Setting setting)
        {
            Load();
            return values[setting];
        }

        public static int GetIndex(Setting setting) => Mathf.RoundToInt(Get(setting));
        public static bool IsOn(Setting setting) => Get(setting) > 0.5f;

        public static void Set(Setting setting, float value)
        {
            Load();
            if (setting == Setting.Quality) value = Mathf.Clamp(Mathf.RoundToInt(value), 0, 2);
            values[setting] = value;
            PlayerPrefs.SetFloat(Key(setting), value);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        /// <summary>Everything back to the defaults (saved).</summary>
        public static void ResetToDefaults()
        {
            Load();
            foreach (var pair in Defaults)
            {
                PlayerPrefs.DeleteKey(Key(pair.Key));
                values[pair.Key] = pair.Value;
            }
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        // ---- Convenience accessors ----

        public static float Music { get => Get(Setting.Music); set => Set(Setting.Music, Mathf.Clamp01(value)); }
        public static float Effects { get => Get(Setting.Effects); set => Set(Setting.Effects, Mathf.Clamp01(value)); }
        public static float Voice { get => Get(Setting.Voice); set => Set(Setting.Voice, Mathf.Clamp01(value)); }
        public static bool AutoTours { get => IsOn(Setting.AutoTours); set => Set(Setting.AutoTours, value ? 1f : 0f); }
        /// <summary>GRAPHICS preset: 0 performance, 1 balanced, 2 quality (applied by <see cref="SettingsApplier"/>).</summary>
        public static int Quality { get => GetIndex(Setting.Quality); set => Set(Setting.Quality, value); }

        /// <summary>Look / orbit speed multiplier, 0.4× to 1.6× (1× in the middle of the slider).</summary>
        public static float LookMultiplier => 0.4f + 1.2f * Get(Setting.LookSpeed);
        public static float LookInvert => IsOn(Setting.InvertLook) ? -1f : 1f;
        public static float SnapDegrees => new[] { 30f, 45f, 90f }[Mathf.Clamp(GetIndex(Setting.SnapAngle), 0, 2)];
        public static float CarSpeedMultiplier => new[] { 0.6f, 1f, 1.6f }[Mathf.Clamp(GetIndex(Setting.CarSpeed), 0, 2)];

        /// <summary>Linear 0..1 to mixer decibels (silence at the bottom of the slider).</summary>
        public static float ToDecibels(float linear) => linear <= 0.0001f ? -80f : Mathf.Log10(linear) * 20f;

        static string Key(Setting setting) => "gridlock." + setting.ToString().ToLowerInvariant();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ApplyOnStart()
        {
            loaded = false;
            Load();
        }

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            foreach (var pair in Defaults)
                values[pair.Key] = PlayerPrefs.GetFloat(Key(pair.Key), pair.Value);
            // GRAPHICS used to store a quality-level index (-1 for the default); those map onto the presets.
            values[Setting.Quality] = Mathf.Clamp(Mathf.RoundToInt(values[Setting.Quality] < 0f ? 1f : values[Setting.Quality]), 0, 2);
        }
    }
}
