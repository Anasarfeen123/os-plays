using UnityEngine;

public class SchedulingStation : MonoBehaviour, IInteractable
{
    public enum StationMode { Simulator, Tutorial, Game }
    
    [Header("Station Configuration")]
    [SerializeField] private StationMode stationMode = StationMode.Simulator;
    [SerializeField] private string stationTitle = "CPU Scheduling Terminal";

    public string PromptText => $"Press [E] to Access {stationTitle}";

    public void Interact()
    {
        if (SchedulingUIController.Instance != null)
        {
            SchedulingUIController.Instance.OpenUI();
            
            switch (stationMode)
            {
                case StationMode.Tutorial:
                    SchedulingUIController.Instance.ShowTutorialMode();
                    break;
                case StationMode.Game:
                    SchedulingUIController.Instance.ShowGameMode();
                    break;
                default:
                    SchedulingUIController.Instance.ShowSimulatorMode();
                    break;
            }
        }
        else
        {
            Debug.LogWarning("SchedulingUIController instance not found!");
        }
    }
}

