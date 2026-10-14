using UnityEngine;

/// <summary>
/// World-space interactable button for quiz answer options (A, B, C, D).
/// Implements IInteractable so player can look at it with crosshair and press [E] or click.
/// </summary>
public class OptionInteractionButton : MonoBehaviour, IInteractable
{
    public LiveChallengeController controller;
    public int optionIndex;
    public char optionLetter;
    public string actionPrompt = "Select Option";

    public string PromptText => actionPrompt;

    public void Interact()
    {
        controller?.SelectOption(optionIndex);
    }
}
