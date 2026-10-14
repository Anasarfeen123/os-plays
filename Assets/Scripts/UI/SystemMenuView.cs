using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gridlock.UI
{
    /// <summary>
    /// One rendering of the system menu (the desktop overlay or the XR panel): a left navigation column and two pages,
    /// SETTINGS and HOW TO PLAY. Entries that do not apply to the scene (CONTROL ROOM in the control room, GUIDED TOUR
    /// where there is none) are hidden.
    /// </summary>
    public class SystemMenuView : MonoBehaviour
    {
        [SerializeField] SystemMenu menu;
        [SerializeField] Button resume, tour, restart, home, settings, help, quit;
        [SerializeField] GameObject settingsPage, helpPage;
        [SerializeField] Color navIdle = new Color(0.12f, 0.16f, 0.26f);
        [SerializeField] Color navActive = new Color(0.35f, 0.85f, 1f);

        public void Init(SystemMenu systemMenu, Button resumeButton, Button tourButton, Button restartButton, Button homeButton,
                         Button settingsButton, Button helpButton, Button quitButton, GameObject settingsContent, GameObject helpContent)
        {
            menu = systemMenu;
            resume = resumeButton;
            tour = tourButton;
            restart = restartButton;
            home = homeButton;
            settings = settingsButton;
            help = helpButton;
            quit = quitButton;
            settingsPage = settingsContent;
            helpPage = helpContent;
        }

        void Awake()
        {
            resume.onClick.AddListener(menu.Close);
            tour.onClick.AddListener(menu.ReplayTour);
            restart.onClick.AddListener(menu.Restart);
            home.onClick.AddListener(menu.Home);
            quit.onClick.AddListener(menu.Quit);
            settings.onClick.AddListener(() => { menu.Click(); ShowSettings(true); });
            help.onClick.AddListener(() => { menu.Click(); ShowSettings(false); });

            tour.gameObject.SetActive(menu.HasTour);
            home.gameObject.SetActive(!menu.IsHome);
            restart.gameObject.SetActive(menu.RestartEvent.GetPersistentEventCount() > 0);
        }

        void OnEnable() => ShowSettings(false);

        void ShowSettings(bool show)
        {
            settingsPage.SetActive(show);
            helpPage.SetActive(!show);
            Tint(settings, show);
            Tint(help, !show);
        }

        void Tint(Button button, bool active)
        {
            button.image.color = active ? navActive : navIdle;
            var label = button.GetComponentInChildren<TMP_Text>();
            if (label != null) label.color = active ? new Color(0.04f, 0.07f, 0.12f) : new Color(0.89f, 0.91f, 0.94f);
        }
    }
}
