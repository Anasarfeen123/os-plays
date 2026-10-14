using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Gridlock.Hub
{
    /// <summary>
    /// Exhibit label in the hub room. Only the label nearest the player shows (so neighbouring exhibits never pile up),
    /// fading in as they walk up, and it always faces them.
    /// </summary>
    public class HubPlaque : MonoBehaviour
    {
        static readonly List<HubPlaque> all = new List<HubPlaque>();
        static HubPlaque nearest;
        static int nearestFrame = -1;

        [SerializeField] TMP_Text text;
        [SerializeField] float showWithin = 4.5f;
        [SerializeField] float fullWithin = 2.8f;

        public void Init(TMP_Text label) => text = label;

        void OnEnable() => all.Add(this);
        void OnDisable() => all.Remove(this);

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            var eye = cam.transform.position;

            if (nearestFrame != Time.frameCount) // the first plaque each frame picks the winner
            {
                nearestFrame = Time.frameCount;
                nearest = null;
                float best = float.MaxValue;
                foreach (var p in all)
                {
                    float d = (p.transform.position - eye).sqrMagnitude;
                    if (d < best) { best = d; nearest = p; }
                }
            }

            // Readable from about 1.6 to 3 m; it steps aside when you walk right up to the exhibit.
            float distance = Vector3.Distance(eye, transform.position);
            float target = nearest == this ? Mathf.InverseLerp(showWithin, fullWithin, distance) * Mathf.InverseLerp(1.1f, 1.6f, distance) : 0f;
            text.alpha = Mathf.MoveTowards(text.alpha, target, Time.unscaledDeltaTime * 3f);
            var look = transform.position - eye;
            look.y = 0f;
            if (look.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(look);
        }
    }
}
