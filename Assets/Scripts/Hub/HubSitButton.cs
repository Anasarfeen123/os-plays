using UnityEngine;

namespace Gridlock.Hub
{
    /// <summary>"SIT AT DESK" on the wrist menu: back to the desk from anywhere in the room.</summary>
    public class HubSitButton : MonoBehaviour
    {
        public void Sit()
        {
            var seat = FindFirstObjectByType<HubSeat>();
            if (seat != null) seat.Sit();
        }
    }
}
