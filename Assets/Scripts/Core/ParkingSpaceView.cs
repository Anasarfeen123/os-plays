using TMPro;
using UnityEngine;

namespace Gridlock.Core
{
    /// <summary>
    /// Scene representation of one single-instance resource (a parking bay). Presentation only.
    /// The bay's forward axis points out of the bay into the driving aisle.
    /// </summary>
    public class ParkingSpaceView : MonoBehaviour
    {
        [SerializeField] string id = "P1";
        [Tooltip("Flat quad covering the bay; its colour shows the state.")]
        [SerializeField] Renderer fill;
        [SerializeField] TMP_Text label;
        [Tooltip("Distance from the bay centre to the point in the aisle where cars line up.")]
        [SerializeField] float approachDistance = 4.2f;

        MaterialPropertyBlock block;
        CarView owner;
        bool deadlocked;

        public string Id => id;
        public CarView Owner => owner;

        /// <summary>Where a parked car stands.</summary>
        public Vector3 ParkingPoint => transform.position;
        /// <summary>Point in the aisle in front of the bay.</summary>
        public Vector3 ApproachPoint => transform.position + transform.forward * approachDistance;
        /// <summary>Cars park nose-in, facing away from the aisle.</summary>
        public Quaternion ParkedRotation => Quaternion.LookRotation(-transform.forward);

        public void Init(string spaceId, Renderer fillRenderer, TMP_Text labelText, float approach)
        {
            id = spaceId;
            approachDistance = approach;
            fill = fillRenderer;
            label = labelText;
        }

        public void SetOwner(CarView car)
        {
            owner = car;
            Refresh();
        }

        public void SetDeadlocked(bool value)
        {
            deadlocked = value;
            Refresh();
        }

        void Awake() => Refresh();

        void Update()
        {
            if (deadlocked) Refresh(); // pulse
        }

        void Refresh()
        {
            block ??= new MaterialPropertyBlock();
            Color c;
            if (deadlocked)
            {
                c = ViewColor.Deadlock;
                c.a = Mathf.Lerp(0.35f, 0.8f, ViewColor.Pulse);
            }
            else if (owner != null)
            {
                c = owner.BaseColor;
                c.a = 0.3f;
            }
            else c = new Color(1f, 1f, 1f, 0.04f);

            ViewColor.Apply(fill, block, c);
            if (label != null) label.color = deadlocked ? ViewColor.Deadlock : owner != null ? owner.BaseColor : Color.white;
        }
    }
}
