using Gridlock.XR;
using UnityEngine;

namespace Gridlock.AR
{
    /// <summary>
    /// A world screen too small to read on a tabletop model (the hub monitor is 6 cm wide at 1 : 26): in phone AR it grows
    /// and rises above the model as a hologram, big enough to read and tap. Elsewhere it stays as built.
    /// </summary>
    public class ARHoloScreen : MonoBehaviour
    {
        [SerializeField] float scale = 4f;
        [Tooltip("World offset applied once the screen has grown (it rises out of its monitor).")]
        [SerializeField] Vector3 offset = new Vector3(0f, 2.5f, 0f);

        public void Init(float growBy, Vector3 moveBy)
        {
            scale = growBy;
            offset = moveBy;
        }

        void Start()
        {
            var mode = XRModeController.Current;
            if (mode == null || !mode.ARActive) return;
            transform.localScale *= scale;
            transform.position += offset;
        }
    }
}
