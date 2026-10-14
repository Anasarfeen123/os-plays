using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gridlock.Core
{
    /// <summary>
    /// Device testing without walking through the hub: <c>adb shell setprop debug.gridlock.scene GridlockMemory</c>, then start
    /// the app, and it opens that scene straight away (clear it with <c>setprop debug.gridlock.scene ""</c>). Android only.
    /// </summary>
    public static class DebugSceneJump
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Jump()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            string scene = null;
            try
            {
                using var props = new AndroidJavaClass("android.os.SystemProperties");
                scene = props.CallStatic<string>("get", "debug.gridlock.scene");
            }
            catch (System.Exception) { } // hidden API blocked: normal start
            if (string.IsNullOrWhiteSpace(scene) || !Application.CanStreamedLevelBeLoaded(scene)) return;
            Debug.Log($"[GRIDLOCK] debug.gridlock.scene: opening {scene}");
            SceneManager.LoadScene(scene);
#endif
        }
    }
}
