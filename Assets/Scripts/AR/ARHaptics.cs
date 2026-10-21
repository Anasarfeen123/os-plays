using UnityEngine;

namespace Gridlock.AR
{
    /// <summary>
    /// Short haptic ticks on the phone through Android's own haptic feedback (the system's touch-vibration setting
    /// applies, and no vibrate permission is needed). Silent elsewhere.
    /// </summary>
    public static class ARHaptics
    {
        // android.view.HapticFeedbackConstants
        const int ClockTick = 4, Confirm = 16, Reject = 17, LongPress = 0;

        /// <summary>A tap landed on something.</summary>
        public static void Tick() => Perform(ClockTick, ClockTick);

        /// <summary>Something happened: the model was placed, a focus zoom began.</summary>
        public static void Success() => Perform(Confirm, LongPress);

        /// <summary>The tap hit nothing that takes taps.</summary>
        public static void Miss() => Perform(Reject, ClockTick);

        static void Perform(int effect, int fallback)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var version = new AndroidJavaClass("android.os.Build$VERSION");
                int constant = version.GetStatic<int>("SDK_INT") >= 30 ? effect : fallback; // CONFIRM / REJECT are API 30+
                activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                {
                    using var window = activity.Call<AndroidJavaObject>("getWindow");
                    using var view = window.Call<AndroidJavaObject>("getDecorView");
                    view.Call<bool>("performHapticFeedback", constant);
                }));
            }
            catch (AndroidJavaException e)
            {
                Debug.LogWarning($"[GRIDLOCK] Haptics unavailable: {e.Message}");
            }
#endif
        }
    }
}
