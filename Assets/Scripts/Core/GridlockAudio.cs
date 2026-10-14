using Gridlock.Deadlock;
using UnityEngine;
using UnityEngine.UI;

namespace Gridlock.Core
{
    /// <summary>
    /// Sound design. City ambience bed; per-event cues placed in 3D at the car involved (granted chime, horn when a car is
    /// blocked); a 2D alarm when a deadlock forms; UI clicks and Analyze on/off cues. Engines live on <see cref="CarView"/>.
    /// </summary>
    [RequireComponent(typeof(SimulationManager))]
    public class GridlockAudio : MonoBehaviour
    {
        [Header("Clips")]
        [SerializeField] AudioClip ambience;
        [SerializeField] AudioClip granted;
        [SerializeField] AudioClip horn;
        [SerializeField] AudioClip alarm;
        [SerializeField] AudioClip click;
        [SerializeField] AudioClip analyzeOn;
        [SerializeField] AudioClip analyzeOff;
        [SerializeField] AudioClip restart;
        [SerializeField] AudioClip tow;

        [Header("Mix")]
        [Range(0f, 1f)] [SerializeField] float ambienceVolume = 0.35f;
        [Range(0f, 1f)] [SerializeField] float cueVolume = 0.8f;
        [Range(0f, 1f)] [SerializeField] float uiVolume = 0.6f;

        SimulationManager simulation;
        AnalysisOverlay overlay;
        AudioSource ambienceSource;

        /// <summary>Lower the city bed under narration.</summary>
        public void Duck(bool duck)
        {
            if (ambienceSource != null) ambienceSource.volume = ambienceVolume * (duck ? 0.4f : 1f);
        }
        AudioSource uiSource;

        public void Init(AudioClip ambienceClip, AudioClip grantedClip, AudioClip hornClip, AudioClip alarmClip,
                         AudioClip clickClip, AudioClip analyzeOnClip, AudioClip analyzeOffClip, AudioClip restartClip, AudioClip towClip)
        {
            tow = towClip;
            ambience = ambienceClip;
            granted = grantedClip;
            horn = hornClip;
            alarm = alarmClip;
            click = clickClip;
            analyzeOn = analyzeOnClip;
            analyzeOff = analyzeOffClip;
            restart = restartClip;
        }

        void Awake()
        {
            simulation = GetComponent<SimulationManager>();
            overlay = GetComponent<AnalysisOverlay>();

            ambienceSource = gameObject.AddComponent<AudioSource>();
            ambienceSource.clip = ambience;
            ambienceSource.loop = true;
            ambienceSource.volume = ambienceVolume;
            ambienceSource.spatialBlend = 0f;
            ambienceSource.playOnAwake = false;

            uiSource = gameObject.AddComponent<AudioSource>();
            uiSource.spatialBlend = 0f;
            uiSource.playOnAwake = false;

            simulation.RequestResolved += OnRequest;
            simulation.CarUnblocked += car => PlayAtCar(car, granted, 1f);
            simulation.DeadlockStarted += _ => uiSource.PlayOneShot(alarm, cueVolume);
            simulation.Restarted += () => uiSource.PlayOneShot(restart, uiVolume);
            simulation.CarTowed += car => PlayAtCar(car, tow, 0.8f);
            if (overlay != null) overlay.ActiveChanged += on => uiSource.PlayOneShot(on ? analyzeOn : analyzeOff, uiVolume);

            foreach (var button in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                button.onClick.AddListener(() => uiSource.PlayOneShot(click, uiVolume));
        }

        void Start()
        {
            if (ambience != null) ambienceSource.Play();
        }

        void OnRequest(string car, string resource, bool wasGranted)
        {
            if (wasGranted) PlayAtCar(car, granted, 1f);
            // Each car's horn is pitched a little differently so a chain of blocked cars is audible as separate voices.
            else PlayAtCar(car, horn, 0.9f + 0.07f * (car.Length > 0 ? car[0] - 'A' : 0));
        }

        void PlayAtCar(string carId, AudioClip clip, float pitch)
        {
            var car = simulation.GetCar(carId);
            if (car == null || clip == null) return;

            var go = new GameObject($"Cue {clip.name}");
            go.transform.position = car.transform.position + Vector3.up;
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.pitch = pitch;
            source.volume = cueVolume;
            source.spatialBlend = 0.85f; // mostly 3D, but still audible from the far end of the lot
            source.minDistance = 6f;
            source.maxDistance = 60f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.Play();
            Destroy(go, clip.length / pitch + 0.1f);
        }
    }
}
