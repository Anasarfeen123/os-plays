using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Physical 3D Process Editor station located in the arena.
/// Allows player to select any process and adjust Arrival Time, Burst Time, and Priority
/// via physical 3D world buttons.
/// </summary>
public class PhysicalProcessEditor : MonoBehaviour
{
    public static PhysicalProcessEditor Instance { get; private set; }

    [Header("3D Editor Display")]
    [SerializeField] private TextMeshPro editorDisplayTMP;

    private int selectedProcessIndex = 0;

    public enum EditAction
    {
        NextProcess,
        PrevProcess,
        IncArrival,
        DecArrival,
        IncBurst,
        DecBurst,
        IncPriority,
        DecPriority
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(this);
    }

    private void Start()
    {
        UpdateEditorDisplay();
    }

    public void SelectProcess(string pid)
    {
        var sim = SimulationController.Instance;
        if (sim == null) return;

        for (int i = 0; i < sim.InitialProcesses.Count; i++)
        {
            if (sim.InitialProcesses[i].Id == pid)
            {
                selectedProcessIndex = i;
                UpdateEditorDisplay();
                break;
            }
        }
    }

    public void ExecuteEdit(EditAction action)
    {
        var sim = SimulationController.Instance;
        if (sim == null || sim.InitialProcesses.Count == 0) return;

        selectedProcessIndex = Mathf.Clamp(selectedProcessIndex, 0, sim.InitialProcesses.Count - 1);
        var p = sim.InitialProcesses[selectedProcessIndex];

        switch (action)
        {
            case EditAction.NextProcess:
                selectedProcessIndex = (selectedProcessIndex + 1) % sim.InitialProcesses.Count;
                break;
            case EditAction.PrevProcess:
                selectedProcessIndex = (selectedProcessIndex - 1 + sim.InitialProcesses.Count) % sim.InitialProcesses.Count;
                break;
            case EditAction.IncArrival:
                p.ArrivalTime = (p.ArrivalTime + 1) > 10 ? 0 : p.ArrivalTime + 1;
                sim.RestartSimulation();
                break;
            case EditAction.DecArrival:
                p.ArrivalTime = Mathf.Max(0, p.ArrivalTime - 1);
                sim.RestartSimulation();
                break;
            case EditAction.IncBurst:
                p.BurstTime = (p.BurstTime + 1) > 12 ? 1 : p.BurstTime + 1;
                p.RemainingTime = p.BurstTime;
                sim.RestartSimulation();
                break;
            case EditAction.DecBurst:
                p.BurstTime = Mathf.Max(1, p.BurstTime - 1);
                p.RemainingTime = p.BurstTime;
                sim.RestartSimulation();
                break;
            case EditAction.IncPriority:
                p.Priority = (p.Priority + 1) > 5 ? 1 : p.Priority + 1;
                sim.RestartSimulation();
                break;
            case EditAction.DecPriority:
                p.Priority = Mathf.Max(1, p.Priority - 1);
                sim.RestartSimulation();
                break;
        }

        UpdateEditorDisplay();
    }

    public void UpdateEditorDisplay()
    {
        if (editorDisplayTMP == null) return;

        var sim = SimulationController.Instance;
        if (sim == null || sim.InitialProcesses.Count == 0)
        {
            editorDisplayTMP.text = "<b>PROCESS CONFIG</b>\nNo processes loaded";
            return;
        }

        selectedProcessIndex = Mathf.Clamp(selectedProcessIndex, 0, sim.InitialProcesses.Count - 1);
        var p = sim.InitialProcesses[selectedProcessIndex];

        string colorHex = ColorUtility.ToHtmlStringRGB(p.ProcessColor);
        string text = $"<b>PROCESS CONFIGURATOR</b>\n" +
                      $"Selected: <color=#{colorHex}><b>{p.Id}</b></color> ({selectedProcessIndex + 1}/{sim.InitialProcesses.Count})\n" +
                      $"Arrival Time: <b>{p.ArrivalTime}s</b>  |  Burst Time: <b>{p.BurstTime}s</b>  |  Priority: <b>{p.Priority}</b>\n" +
                      $"<i>(Lower priority number = higher priority)</i>";

        editorDisplayTMP.text = text;
    }

    /// <summary>
    /// Physical 3D button script for process editor actions.
    /// </summary>
    public class ProcessEditButton : MonoBehaviour, IInteractable
    {
        public EditAction action;
        public string actionName = "Edit";
        public Color buttonColor = Color.white;

        private MeshRenderer meshRenderer;
        private Vector3 initialLocalPos;
        private bool isPressed = false;

        private void Awake()
        {
            meshRenderer = GetComponent<MeshRenderer>();
            initialLocalPos = transform.localPosition;
        }

        public string PromptText => $"Press [E] to {actionName}";

        public void Interact()
        {
            if (PhysicalProcessEditor.Instance != null)
            {
                PhysicalProcessEditor.Instance.ExecuteEdit(action);
            }
            AnimatePress();
        }

        private void AnimatePress()
        {
            if (!isPressed && gameObject.activeInHierarchy)
            {
                StartCoroutine(PressRoutine());
            }
        }

        private IEnumerator PressRoutine()
        {
            isPressed = true;
            if (initialLocalPos == Vector3.zero) initialLocalPos = transform.localPosition;
            if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();

            transform.localPosition = initialLocalPos - new Vector3(0, 0.035f, 0);
            if (meshRenderer != null)
            {
                meshRenderer.material.color = Color.white;
            }

            yield return new WaitForSeconds(0.12f);

            transform.localPosition = initialLocalPos;
            if (meshRenderer != null)
            {
                meshRenderer.material.color = buttonColor;
            }
            isPressed = false;
        }
    }
}
