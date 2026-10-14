using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class UIEnhancer : MonoBehaviour
{
    public static UIEnhancer Instance { get; private set; }

    [Header("Fade Settings")]
    [SerializeField] private float fadeDuration = 0.2f;

    [Header("Sound Settings")]
    [SerializeField] private AudioClip clickClip;
    private AudioSource audioSource;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) { Destroy(gameObject); return; }
        DontDestroyOnLoad(gameObject);
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    public void FadeIn(GameObject panel)
    {
        if (panel == null) return;
        var cg = panel.GetComponent<CanvasGroup>();
        if (cg == null) cg = panel.AddComponent<CanvasGroup>();
        StopAllCoroutines();
        StartCoroutine(FadeCanvasGroup(cg, 0f, 1f));
    }

    public void FadeOut(GameObject panel, System.Action onComplete = null)
    {
        if (panel == null) return;
        var cg = panel.GetComponent<CanvasGroup>();
        if (cg == null) cg = panel.AddComponent<CanvasGroup>();
        StopAllCoroutines();
        StartCoroutine(FadeCanvasGroup(cg, cg.alpha, 0f, onComplete));
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup cg, float from, float to, System.Action onComplete = null)
    {
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(from, to, elapsed / fadeDuration);
            yield return null;
        }
        cg.alpha = to;
        onComplete?.Invoke();
    }

    public void PlayClickSound()
    {
        if (clickClip != null && audioSource != null)
        {
            audioSource.PlayOneShot(clickClip);
        }
    }
}
