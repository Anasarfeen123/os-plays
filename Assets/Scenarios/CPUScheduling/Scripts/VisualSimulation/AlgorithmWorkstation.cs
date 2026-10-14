using UnityEngine;
using TMPro;

/// <summary>
/// Attached to each of the 4 physical algorithm computers in the lab (FCFS, SRTF, PRIORITY, ROUND ROBIN).
/// Allows the player to physically walk up and select the active scheduling algorithm.
/// Updates the workstation monitor glow, 3D text label, and activates the shared arena.
/// </summary>
public class AlgorithmWorkstation : MonoBehaviour, IInteractable
{
    public enum AlgorithmChoice
    {
        FCFS,
        SRTF,
        Priority,
        RoundRobin
    }

    [Header("Algorithm Configuration")]
    [SerializeField] private AlgorithmChoice algorithm = AlgorithmChoice.FCFS;
    [SerializeField] private string algorithmDisplayName = "FCFS";
    [TextArea(2, 4)]
    [SerializeField] private string algorithmDescription = "First-Come First-Served\nNon-preemptive queue order.";

    [Header("Visual References")]
    [SerializeField] private MeshRenderer monitorScreenRenderer;
    [SerializeField] private TextMeshPro labelTMP;
    [SerializeField] private TextMeshPro infoTMP;
    [SerializeField] private Light workstationLight;
    [SerializeField] private Color activeColor = new Color(0.2f, 0.7f, 1f);
    [SerializeField] private Color inactiveColor = new Color(0.15f, 0.2f, 0.3f);

    private Material screenMaterial;
    private bool isSelected = false;

    public AlgorithmChoice SelectedAlgorithm => algorithm;
    public string DisplayName => algorithmDisplayName;

    private void Awake()
    {
        if (monitorScreenRenderer != null)
        {
            screenMaterial = monitorScreenRenderer.material;
        }
    }

    private void Start()
    {
        UpdateVisuals(false);

        // Check if this station matches currently active algorithm on start
        var sim = SimulationController.Instance;
        if (sim != null && sim.CurrentAlgorithm != null)
        {
            bool matches = (algorithm == AlgorithmChoice.FCFS && sim.CurrentAlgorithm is FCFSAlgorithm) ||
                           (algorithm == AlgorithmChoice.SRTF && sim.CurrentAlgorithm is SRTFAlgorithm) ||
                           (algorithm == AlgorithmChoice.Priority && sim.CurrentAlgorithm is PriorityAlgorithm) ||
                           (algorithm == AlgorithmChoice.RoundRobin && sim.CurrentAlgorithm is RoundRobinAlgorithm);
            if (matches)
            {
                SetActiveState(true);
            }
        }
    }

    public string PromptText => $"Press [E] to Select {algorithmDisplayName} Scheduling";

    public void Interact()
    {
        Debug.Log($"[Workstation] Player selected algorithm: {algorithmDisplayName}");

        // Switch algorithm in PhysicalArenaManager and SimulationController
        if (PhysicalArenaManager.Instance != null)
        {
            PhysicalArenaManager.Instance.SelectAlgorithmFromWorkstation(this);
        }
        else if (SimulationController.Instance != null)
        {
            SchedulingAlgorithm algo = CreateAlgorithmInstance();
            SimulationController.Instance.SetAlgorithm(algo);
        }
    }

    public SchedulingAlgorithm CreateAlgorithmInstance(int quantum = 4)
    {
        switch (algorithm)
        {
            case AlgorithmChoice.FCFS:
                return new FCFSAlgorithm();
            case AlgorithmChoice.SRTF:
                return new SRTFAlgorithm();
            case AlgorithmChoice.Priority:
                return new PriorityAlgorithm(true); // Preemptive Priority
            case AlgorithmChoice.RoundRobin:
                return new RoundRobinAlgorithm(quantum);
            default:
                return new FCFSAlgorithm();
        }
    }

    public void SetActiveState(bool active)
    {
        isSelected = active;
        UpdateVisuals(active);
    }

    private void UpdateVisuals(bool active)
    {
        Color color = active ? activeColor : inactiveColor;

        if (screenMaterial != null)
        {
            screenMaterial.color = color;
            screenMaterial.EnableKeyword("_EMISSION");
            screenMaterial.SetColor("_EmissionColor", active ? color * 1.5f : color * 0.2f);
        }

        if (workstationLight != null)
        {
            workstationLight.color = color;
            workstationLight.intensity = active ? 1.8f : 0.4f;
        }

        if (labelTMP != null)
        {
            labelTMP.color = active ? Color.white : new Color(0.7f, 0.7f, 0.8f);
            labelTMP.text = active ? $"[ACTIVE] {algorithmDisplayName}" : algorithmDisplayName;
        }

        if (infoTMP != null)
        {
            infoTMP.text = algorithmDescription;
            infoTMP.color = active ? Color.yellow : Color.gray;
        }
    }
}
