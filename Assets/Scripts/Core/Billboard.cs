using UnityEngine;

namespace Gridlock.Core
{
    /// <summary>
    /// Keeps world-space labels facing the active camera (desktop or XR). Labels sized to be read from the overview
    /// camera can also shrink as a headset player walks up to them, so a car's letter does not tower over the boards.
    /// </summary>
    public class Billboard : MonoBehaviour
    {
        [Tooltip("Rotate around the vertical axis only, so labels stay upright.")]
        [SerializeField] bool yawOnly = true;
        [Tooltip("Closer than this (world metres) the label shrinks with distance, down to 30 % (0: never).")]
        [SerializeField] float shrinkWithin;

        Vector3 baseScale;

        public void ShrinkWithin(float metres) => shrinkWithin = metres;

        void Awake() => baseScale = transform.localScale;

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            var dir = transform.position - cam.transform.position;
            if (shrinkWithin > 0f) transform.localScale = baseScale * Mathf.Clamp(dir.magnitude / shrinkWithin, 0.3f, 1f);
            if (yawOnly) dir.y = 0f;
            if (dir.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(dir);
        }
    }
}
