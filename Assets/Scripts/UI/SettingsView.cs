using Gridlock.Core;
using Gridlock.Guide;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gridlock.UI
{
    /// <summary>
    /// The SETTINGS page: AUDIO / DISPLAY / CONTROLS / GAMEPLAY tabs of self-binding rows (<see cref="SettingSlider"/>,
    /// <see cref="SettingChoice"/>), RESET ALL, and SHOW TOURS AGAIN. Every change is saved and applied at once.
    /// </summary>
    public class SettingsView : MonoBehaviour
    {
        static readonly Color TabOn = new Color(0.98f, 0.75f, 0.14f), TabOff = new Color(0.12f, 0.16f, 0.26f);

        [SerializeField] Button[] tabs;
        [SerializeField] GameObject[] pages;
        [SerializeField] Button resetAll;
        [SerializeField] Button showTours;
        [SerializeField] TMP_Text showToursLabel;

        int current;

        public void Init(Button[] tabButtons, GameObject[] tabPages, Button reset, Button tours, TMP_Text toursLabel)
        {
            tabs = tabButtons;
            pages = tabPages;
            resetAll = reset;
            showTours = tours;
            showToursLabel = toursLabel;
        }

        void Awake()
        {
            for (int i = 0; i < tabs.Length; i++)
            {
                int index = i;
                tabs[i].onClick.AddListener(() => Show(index));
            }
            resetAll.onClick.AddListener(() =>
            {
                GameSettings.ResetToDefaults();
                RefreshAll();
            });
            showTours.onClick.AddListener(() =>
            {
                NarratedTour.ForgetAll();
                showToursLabel.text = "TOURS WILL PLAY AGAIN";
            });
        }

        void OnEnable()
        {
            Show(current);
            showToursLabel.text = "SHOW TOURS AGAIN";
        }

        void Show(int index)
        {
            current = index;
            for (int i = 0; i < pages.Length; i++)
            {
                pages[i].SetActive(i == index);
                tabs[i].image.color = i == index ? TabOn : TabOff;
                var label = tabs[i].GetComponentInChildren<TMP_Text>();
                if (label != null) label.color = i == index ? new Color(0.06f, 0.08f, 0.12f) : new Color(0.89f, 0.91f, 0.94f);
            }
        }

        void RefreshAll()
        {
            foreach (var slider in GetComponentsInChildren<SettingSlider>(true)) slider.Refresh();
            foreach (var choice in GetComponentsInChildren<SettingChoice>(true)) choice.Refresh();
        }
    }
}
