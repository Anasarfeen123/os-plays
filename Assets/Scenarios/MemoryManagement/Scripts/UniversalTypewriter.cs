
using System.Collections;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Text))]
public class UniversalTypewriter : MonoBehaviour
{
    [Header("Typing Settings")]
    [SerializeField, Min(1f)]
    private float charactersPerSecond = 35f;

    [SerializeField]
    private bool playOnEnable = true;

    private TMP_Text targetText;
    private Coroutine typingCoroutine;
    private string fullText;

    private void Awake()
    {
        targetText = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        if (playOnEnable && targetText != null)
            Play();
    }

    public void Play()
    {
        if (targetText == null)
            targetText = GetComponent<TMP_Text>();

        if (targetText == null)
            return;

        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        fullText = targetText.text;
        typingCoroutine = StartCoroutine(TypeText());
    }

    public void PlayText(string message)
    {
        if (targetText == null)
            targetText = GetComponent<TMP_Text>();

        if (targetText == null)
            return;

        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        targetText.text = message;
        fullText = message;
        typingCoroutine = StartCoroutine(TypeText());
    }

    public void ShowImmediately()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        if (targetText == null)
            targetText = GetComponent<TMP_Text>();

        if (targetText == null)
            return;

        targetText.maxVisibleCharacters = int.MaxValue;
    }

    private IEnumerator TypeText()
    {
        targetText.maxVisibleCharacters = 0;
        targetText.ForceMeshUpdate();

        int totalCharacters = targetText.textInfo.characterCount;

        for (int i = 1; i <= totalCharacters; i++)
        {
            targetText.maxVisibleCharacters = i;
            yield return new WaitForSecondsRealtime(1f / charactersPerSecond);
        }

        targetText.maxVisibleCharacters = int.MaxValue;
        typingCoroutine = null;
    }

    private void OnDisable()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }
    }
}
