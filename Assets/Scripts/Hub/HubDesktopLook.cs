using Gridlock.XR;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Gridlock.Hub
{
    /// <summary>
    /// Desktop hub camera: a slight look-around that follows the mouse, as if turning your head in the chair.
    /// On a phone a finger drag turns the head instead, within the same range.
    /// </summary>
    public class HubDesktopLook : MonoBehaviour
    {
        [SerializeField] Vector2 range = new Vector2(12f, 7f);
        [SerializeField] float sharpness = 4f;

        Quaternion home;
        Vector2 look;

        void Start() => home = transform.localRotation;

        void Update()
        {
            Vector2 n;
            if (TouchGestures.IsTouchDevice && Touchscreen.current != null)
            {
                look -= TouchGestures.Drag * (2.5f / Mathf.Max(1, Screen.height)); // drag the view like a photo
                n = look = Vector2.ClampMagnitude(look, 1.2f);
            }
            else
            {
                var mouse = Mouse.current;
                if (mouse == null) return;
                var p = mouse.position.ReadValue();
                n = new Vector2(p.x / Mathf.Max(1, Screen.width) * 2f - 1f, p.y / Mathf.Max(1, Screen.height) * 2f - 1f);
                n = Vector2.ClampMagnitude(n, 1.2f);
            }
            var wanted = home * Quaternion.Euler(-n.y * range.y, n.x * range.x, 0f);
            transform.localRotation = Quaternion.Slerp(transform.localRotation, wanted, 1f - Mathf.Exp(-sharpness * Time.deltaTime));
        }
    }
}
