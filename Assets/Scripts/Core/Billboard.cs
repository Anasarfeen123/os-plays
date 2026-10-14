using UnityEngine;

namespace Gridlock.Core
{
    /// <summary>Keeps world-space labels facing the active camera (desktop or XR).</summary>
    public class Billboard : MonoBehaviour
    {
        [Tooltip("Rotate around the vertical axis only, so labels stay upright.")]
        [SerializeField] bool yawOnly = true;

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            var dir = transform.position - cam.transform.position;
            if (yawOnly) dir.y = 0f;
            if (dir.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(dir);
        }
    }
}
