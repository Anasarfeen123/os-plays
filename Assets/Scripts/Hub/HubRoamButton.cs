using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gridlock.Hub
{
    /// <summary>HUD button that toggles roaming: "STAND UP" in the chair, "SIT DOWN" while walking.</summary>
    [RequireComponent(typeof(Button))]
    public class HubRoamButton : MonoBehaviour
    {
        [SerializeField] HubRoamer roamer;
        [SerializeField] TMP_Text label;
        [SerializeField] string standText = "STAND UP  (R)";
        [SerializeField] string sitText = "SIT DOWN  (R)";

        public void Init(HubRoamer hubRoamer, TMP_Text text, string stand, string sit)
        {
            roamer = hubRoamer;
            label = text;
            standText = stand;
            sitText = sit;
        }

        void Awake()
        {
            GetComponent<Button>().onClick.AddListener(roamer.ToggleRoaming);
            roamer.RoamingChanged += Show;
            Show(roamer.Roaming);
        }

        void OnDestroy()
        {
            if (roamer != null) roamer.RoamingChanged -= Show;
        }

        void Show(bool roaming) => label.text = roaming ? sitText : standText;
    }
}
