using UnityEngine;
using UnityEngine.UI;

namespace Gridlock.Banker
{
    /// <summary>Banker mode sound: city ambience, a chime for a correct decision, an error buzz for a wrong one, UI clicks.</summary>
    [RequireComponent(typeof(BankerGame))]
    public class BankerAudio : MonoBehaviour
    {
        [SerializeField] AudioClip ambience;
        [SerializeField] AudioClip correct;
        [SerializeField] AudioClip wrong;
        [SerializeField] AudioClip click;
        [Range(0f, 1f)] [SerializeField] float ambienceVolume = 0.35f;
        [Range(0f, 1f)] [SerializeField] float cueVolume = 0.8f;

        AudioSource ambienceSource;
        AudioSource uiSource;

        public void Init(AudioClip ambienceClip, AudioClip correctClip, AudioClip wrongClip, AudioClip clickClip)
        {
            ambience = ambienceClip;
            correct = correctClip;
            wrong = wrongClip;
            click = clickClip;
        }

        void Awake()
        {
            ambienceSource = gameObject.AddComponent<AudioSource>();
            ambienceSource.clip = ambience;
            ambienceSource.loop = true;
            ambienceSource.volume = ambienceVolume;
            ambienceSource.playOnAwake = false;
            uiSource = gameObject.AddComponent<AudioSource>();
            uiSource.playOnAwake = false;

            GetComponent<BankerGame>().Decided += (v, _) => uiSource.PlayOneShot(v.Correct ? correct : wrong, cueVolume);
            foreach (var button in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                button.onClick.AddListener(() => uiSource.PlayOneShot(click, cueVolume * 0.75f));
        }

        void Start()
        {
            if (ambience != null) ambienceSource.Play();
        }
    }
}
