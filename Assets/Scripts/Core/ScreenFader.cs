using System.Collections;
using UnityEngine;

namespace Gridlock.Core
{
    /// <summary>
    /// Full-view colour fade that works in a headset as well as on a monitor: a small inside-out sphere around the
    /// active camera (a screen-space overlay is not drawn into the eyes in XR). Every scene fades in from the colour of
    /// the hub portal on start; the hub fades out into it when the player is pulled into the screen.
    /// </summary>
    public class ScreenFader : MonoBehaviour
    {
        [SerializeField] Material fadeMaterial;
        [SerializeField] Color color = new Color(0.55f, 0.9f, 1f);
        [SerializeField] bool fadeInOnStart = true;
        [SerializeField] float fadeInSeconds = 1.1f;
        [Tooltip("Played as the scene fades in (arriving from the hub).")]
        [SerializeField] AudioClip arriveSound;

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        Renderer sphere;
        Material material;
        float alpha;

        public Color FadeColor { get => color; set { color = value; Apply(); } }

        public void Init(Material material, AudioClip arrive)
        {
            fadeMaterial = material;
            arriveSound = arrive;
        }

        IEnumerator Start()
        {
            Attach(); // after every Awake, so XRModeController has picked the rig and Camera.main is the one in use
            if (fadeInOnStart)
            {
                alpha = 1f;
                Apply();
                if (arriveSound != null) AudioSource.PlayClipAtPoint(arriveSound, Camera.main != null ? Camera.main.transform.position : Vector3.zero, 0.8f);
                yield return FadeTo(0f, fadeInSeconds);
            }
        }

        public IEnumerator FadeTo(float target, float seconds)
        {
            Attach();
            float from = alpha;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                alpha = Mathf.Lerp(from, target, Mathf.SmoothStep(0f, 1f, t / seconds));
                Apply();
                yield return null;
            }
            alpha = target;
            Apply();
        }

        void Attach()
        {
            var cam = Camera.main;
            if (cam == null || (sphere != null && sphere.transform.parent == cam.transform)) return;
            if (sphere == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "ScreenFade";
                Destroy(go.GetComponent<Collider>());
                sphere = go.GetComponent<Renderer>();
                sphere.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                sphere.receiveShadows = false;
                material = new Material(fadeMaterial);
                sphere.sharedMaterial = material;
            }
            sphere.transform.SetParent(cam.transform, false);
            sphere.transform.localPosition = Vector3.zero;
            sphere.transform.localScale = Vector3.one * Mathf.Max(0.3f, cam.nearClipPlane * 4f);
            Apply();
        }

        void Apply()
        {
            if (sphere == null) return;
            material.SetColor(BaseColor, new Color(color.r, color.g, color.b, alpha));
            sphere.enabled = alpha > 0.001f;
        }
    }
}
