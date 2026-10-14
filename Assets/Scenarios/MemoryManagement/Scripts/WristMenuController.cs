
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

public class WristMenuController : MonoBehaviour
{
    [Header("Comfort Settings")]
    [SerializeField] private Volume comfortVolume;
    [SerializeField] private float maxVignetteIntensity = 0.5f;

    private UnityEngine.Rendering.Universal.Vignette vignette;
    [Header("Wrist Menu Input")]
    [SerializeField] private InputActionReference toggleMenuAction;
    [Header("Menu")]
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button exitButton;
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider fovSlider;
    [SerializeField] private Camera playerCamera;

    private float defaultCameraScale = 1f;

    [Header("Audio")]
    [SerializeField] private AudioSource[] audioSourcesToControl;

    private bool sfxEnabled = true;

    private void Awake()
    {
        if (fovSlider != null)
            fovSlider.onValueChanged.AddListener(SetComfort);
        if (menuPanel != null)
            menuPanel.SetActive(false);

        if (resumeButton != null)
            resumeButton.onClick.AddListener(Resume);

        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.minValue = 0f;
            masterVolumeSlider.maxValue = 1f;
            masterVolumeSlider.value = 1f;
            masterVolumeSlider.onValueChanged.AddListener(SetMasterVolume);
        }

        if (fovSlider != null)
        {
            fovSlider.minValue = 1f;
            fovSlider.maxValue = 1.5f;
            fovSlider.value = 1f;
            fovSlider.onValueChanged.AddListener(SetComfortZoom);
        }

        if (playerCamera != null)
            defaultCameraScale = playerCamera.transform.localPosition.z;

        if (exitButton != null)
            exitButton.onClick.AddListener(ExitSession);
    }

    public void ToggleMenu()
    {
        if (menuPanel != null)
            menuPanel.SetActive(!menuPanel.activeSelf);
    }

    public void Resume()
    {
        if (menuPanel != null)
            menuPanel.SetActive(false);

        Time.timeScale = 1f;
    }

    private void SetMasterVolume(float volume)
    {
        AudioListener.volume = volume;
    }

    private void ExitSession()
    {
        Time.timeScale = 1f;

        // Return to the existing Gridlock Hub if it is in the build.
        int hubIndex = -1;

        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings; i++)
        {
            string path = UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(i);

            if (path.Contains("GridlockHub"))
            {
                hubIndex = i;
                break;
            }
        }

        if (hubIndex >= 0)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(hubIndex);
        }
        else
        {
            Debug.LogWarning("GridlockHub was not found in Build Settings. Session was not exited.");
        }
    }
    public void ToggleWristMenu()
    {
        if (menuPanel == null)
            return;

        bool isOpening = !menuPanel.activeSelf;
        menuPanel.SetActive(isOpening);
    }
    private void OnEnable()
    {
        if (toggleMenuAction != null && toggleMenuAction.action != null)
        {
            toggleMenuAction.action.performed += OnToggleMenu;
            toggleMenuAction.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (toggleMenuAction != null && toggleMenuAction.action != null)
        {
            toggleMenuAction.action.performed -= OnToggleMenu;
            toggleMenuAction.action.Disable();
        }
    }

    private void OnToggleMenu(InputAction.CallbackContext context)
    {
        ToggleWristMenu();
    }
    private void SetComfortZoom(float zoom)
    {
        if (playerCamera == null)
            return;

        playerCamera.fieldOfView = 60f / zoom;
    }
    private void Start()
    {
        if (comfortVolume != null && comfortVolume.profile != null)
        {
            comfortVolume.profile.TryGet(
                out vignette
            );
        }

        if (vignette != null)
            SetComfort(fovSlider.value);
    }

    private void SetComfort(float value)
    {
        if (vignette == null)
            return;

        vignette.intensity.value = value * maxVignetteIntensity;
    }
}
