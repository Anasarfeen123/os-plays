using UnityEngine;

namespace Gridlock.Core
{
    /// <summary>Tells the music player which track this scene uses.</summary>
    public class MusicCue : MonoBehaviour
    {
        [SerializeField] AudioClip track;

        public void Init(AudioClip clip) => track = clip;

        void Start() => MusicPlayer.Play(track);
    }
}
