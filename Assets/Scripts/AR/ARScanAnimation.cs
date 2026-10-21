using UnityEngine;

namespace Gridlock.AR
{
    /// <summary>The phone icon in the scanning prompt: it sways from side to side, the way the player should move the phone.</summary>
    public class ARScanAnimation : MonoBehaviour
    {
        [SerializeField] float sway = 90f;
        [SerializeField] float tilt = 14f;
        [SerializeField] float speed = 1.7f;

        RectTransform rect;
        Vector2 home;

        void Awake()
        {
            rect = (RectTransform)transform;
            home = rect.anchoredPosition;
        }

        void Update()
        {
            float s = Mathf.Sin(Time.unscaledTime * speed);
            rect.anchoredPosition = home + new Vector2(s * sway, Mathf.Abs(s) * -10f);
            rect.localRotation = Quaternion.Euler(0f, 0f, -s * tilt);
        }
    }
}
