using UnityEngine;

namespace Gridlock.UI
{
    /// <summary>Opens the scene's <see cref="SystemMenu"/> from anywhere (HUD button, wrist menu, controller ≡ button).</summary>
    public class SystemMenuLink : MonoBehaviour
    {
        public void Toggle()
        {
            if (SystemMenu.Instance != null) SystemMenu.Instance.Toggle();
        }
    }
}
