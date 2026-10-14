using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Gridlock.UI
{
    /// <summary>Button that switches between the Deadlock and Banker scenes.</summary>
    [RequireComponent(typeof(Button))]
    public class ModeSwitch : MonoBehaviour
    {
        [SerializeField] string sceneName;

        public void Init(string scene) => sceneName = scene;

        void Awake() => GetComponent<Button>().onClick.AddListener(() =>
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(sceneName);
        });
    }
}
