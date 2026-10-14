using System.Collections.Generic;
using Gridlock.Guide;
using TMPro;
using Gridlock.XR;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Hands;

namespace Gridlock.UI
{
    /// <summary>
    /// The pause menu every scene has: RESUME, GUIDED TOUR, RESTART, CONTROL ROOM, SETTINGS, HOW TO PLAY and QUIT.
    /// Opens with Esc (desktop, and Android back), the ≡ MENU button on the HUD, the left controller's ≡ button, the
    /// wrist menu, or Quest's hand-tracking menu gesture (look at the left palm and pinch). While open the game is paused (time and sounds), except the music and the menu's own clicks.
    /// In a headset the menu appears as a panel in front of the player; on a monitor it is a dimmed full-screen overlay.
    /// </summary>
    public class SystemMenu : MonoBehaviour
    {
        [SerializeField] GameObject desktopView;
        [SerializeField] GameObject xrView;
        [SerializeField] XRModeController mode;
        [SerializeField] string homeScene;
        [Tooltip("The control room itself: no CONTROL ROOM, RESTART or GUIDED TOUR entries.")]
        [SerializeField] bool isHome;
        [SerializeField] NarratedTour tour;
        [SerializeField] UnityEvent restart = new UnityEvent();
        [SerializeField] AudioClip openClip;
        [SerializeField] AudioClip closeClip;
        [SerializeField] AudioClip clickClip;

        AudioSource ui;
        float savedTimeScale = 1f;
        bool handMenuWasPressed;
        GameObject openView;

        public static SystemMenu Instance { get; private set; }

        /// <summary>
        /// Set by a scene whose own UI closes on Esc (the CPU lab's simulator panel): while it returns true, Esc belongs to
        /// that UI and does not open the menu.
        /// </summary>
        public static System.Func<bool> EscapeTaken;
        public bool IsOpen => openView != null;
        public bool IsHome => isHome;
        public bool HasTour => tour != null;
        public UnityEvent RestartEvent => restart;

        public void Init(GameObject desktop, GameObject xr, XRModeController xrMode, string home, bool isHomeScene, NarratedTour guidedTour,
                         AudioClip open, AudioClip close, AudioClip click)
        {
            desktopView = desktop;
            xrView = xr;
            mode = xrMode;
            homeScene = home;
            isHome = isHomeScene;
            tour = guidedTour;
            openClip = open;
            closeClip = close;
            clickClip = click;
        }

        void Awake()
        {
            Instance = this;
            ui = gameObject.AddComponent<AudioSource>();
            ui.spatialBlend = 0f;
            ui.playOnAwake = false;
            ui.ignoreListenerPause = true; // the menu must click while the game is paused
            desktopView.SetActive(false);
            xrView.SetActive(false);
            DrawOnTop(xrView);
        }

        /// <summary>
        /// In a headset the menu must never hide inside the scene (the hub's monitor sits within arm's reach), so its UI
        /// ignores the depth buffer: UI and TMP shaders read their ZTest from unity_GUIZTestMode.
        /// </summary>
        static void DrawOnTop(GameObject root)
        {
            var copies = new Dictionary<Material, Material>();
            Material OnTop(Material source)
            {
                if (source == null) return null;
                if (!copies.TryGetValue(source, out var copy))
                {
                    copy = new Material(source) { name = source.name + " (on top)" };
                    copy.SetFloat("unity_GUIZTestMode", (float)CompareFunction.Always);
                    copies[source] = copy;
                }
                return copy;
            }

            foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic is TMP_Text text) text.fontSharedMaterial = OnTop(text.fontSharedMaterial);
                else graphic.material = OnTop(graphic.material);
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (IsOpen) Unpause();
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && (IsOpen || EscapeTaken == null || !EscapeTaken())) Toggle();

            // Quest hand tracking: the system "menu" pinch on the left palm arrives as an aim flag, not a button.
            var hand = MetaAimHand.left;
            bool pressed = hand != null && hand.added &&
                           (((MetaAimFlags)hand.aimFlags.ReadValue()) & MetaAimFlags.MenuPressed) != 0;
            if (pressed && !handMenuWasPressed) Toggle();
            handMenuWasPressed = pressed;
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void Open()
        {
            if (IsOpen) return;
            savedTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            AudioListener.pause = true;

            bool xr = mode != null && mode.XRActive;
            openView = xr ? xrView : desktopView;
            if (xr) PlaceInFrontOfPlayer(openView.transform);
            openView.SetActive(true);
            Play(openClip);
        }

        public void Close()
        {
            if (!IsOpen) return;
            openView.SetActive(false);
            openView = null;
            Unpause();
            Play(closeClip);
        }

        public void Click() => Play(clickClip);

        // ---- Menu entries ----

        public void Home()
        {
            Close();
            Time.timeScale = 1f;
            SceneManager.LoadScene(homeScene);
        }

        public void Restart()
        {
            Close();
            restart.Invoke();
        }

        public void ReplayTour()
        {
            Close();
            if (tour != null) tour.Replay();
        }

        public void Quit()
        {
            Close();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void Unpause()
        {
            Time.timeScale = savedTimeScale;
            AudioListener.pause = false;
        }

        void Play(AudioClip clip)
        {
            if (clip != null) ui.PlayOneShot(clip, 0.7f);
        }

        /// <summary>
        /// Within arm's reach (0.75 m) and a little below the eyes, so a fingertip can poke it as well as a ray reach it.
        /// </summary>
        static void PlaceInFrontOfPlayer(Transform view)
        {
            var cam = Camera.main;
            if (cam == null) return;
            var forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
            forward.Normalize();
            view.position = cam.transform.position + forward * 0.75f - Vector3.up * 0.18f;
            view.rotation = Quaternion.LookRotation(view.position - cam.transform.position); // tilted towards the eyes
        }
    }
}
