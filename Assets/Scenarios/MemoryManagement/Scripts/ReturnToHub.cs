using UnityEngine;
using UnityEngine.SceneManagement;

public class ReturnToHub : MonoBehaviour
{
    public void LoadHub()
    {
        SceneManager.LoadScene("GridlockHub");
    }
}