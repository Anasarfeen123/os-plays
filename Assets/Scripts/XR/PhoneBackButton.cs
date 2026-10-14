using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Gridlock.XR
{
    /// <summary>
    /// Android back gesture in scenes without a system menu: from a scenario back to the hub room (build index 0); from the
    /// hub, quit. Where a <see cref="Gridlock.UI.SystemMenu"/> exists, back opens it instead.
    /// </summary>
    public class PhoneBackButton : MonoBehaviour
    {
        void Update()
        {
            var keyboard = Keyboard.current; // Android reports back as Escape
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame) return;
            if (Gridlock.UI.SystemMenu.Instance != null) return; // the system menu opens on back instead (Home / Quit live there)

            if (SceneManager.GetActiveScene().buildIndex <= 0)
            {
                Application.Quit();
                return;
            }
            Time.timeScale = 1f;
            SceneManager.LoadScene(0);
        }
    }
}
