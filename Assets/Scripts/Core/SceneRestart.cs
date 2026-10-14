using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gridlock.Core
{
    /// <summary>Menu → RESTART for scenes without their own reset: reloads the active scene.</summary>
    public class SceneRestart : MonoBehaviour
    {
        public void Reload()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
