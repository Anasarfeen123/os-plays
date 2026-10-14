using UnityEngine;

namespace Gridlock.Hub
{
    /// <summary>Constant rotation (holo rings, planet, fans).</summary>
    public class HubSpin : MonoBehaviour
    {
        [SerializeField] Vector3 degreesPerSecond = new Vector3(0f, 20f, 0f);
        [SerializeField] Space space = Space.Self;

        public void Init(Vector3 speed, Space relativeTo = Space.Self)
        {
            degreesPerSecond = speed;
            space = relativeTo;
        }

        void Update() => transform.Rotate(degreesPerSecond * Time.deltaTime, space);
    }
}
