using TMPro;
using UnityEngine;

namespace Gridlock.Hub
{
    /// <summary>Alarm-style blink for a warning label: on, on, off.</summary>
    [RequireComponent(typeof(TMP_Text))]
    public class HubBlinkText : MonoBehaviour
    {
        [SerializeField] float period = 1.2f;

        TMP_Text text;

        void Awake() => text = GetComponent<TMP_Text>();

        void Update() => text.alpha = Mathf.Repeat(Time.time, period) < period * 0.7f ? 1f : 0.15f;
    }
}
