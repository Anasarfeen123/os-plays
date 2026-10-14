using UnityEngine;

/// <summary>
/// World-space interactable button for quiz control actions (Start, Next Question, Retry).
/// Implements IInteractable so player can look at it with crosshair and press [E] or click.
/// </summary>
public class ControlInteractionButton : MonoBehaviour, IInteractable
{
    public LiveChallengeController controller;
    public string actionPrompt = "Press [E] to Proceed";

    public string PromptText => actionPrompt;

    public void Interact()
    {
        if (controller == null) return;
        if (controller.CurrentState == LiveChallengeController.QuizState.Welcome)
        {
            controller.StartQuiz();
        }
        else if (controller.CurrentState == LiveChallengeController.QuizState.Feedback)
        {
            controller.AdvanceNextQuestion();
        }
        else if (controller.CurrentState == LiveChallengeController.QuizState.Completed)
        {
            controller.RestartQuiz();
        }
    }
}
