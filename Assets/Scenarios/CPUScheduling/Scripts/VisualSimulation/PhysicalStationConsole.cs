using UnityEngine;

/// <summary>
/// Physical 3D console buttons on the workstation desk.
/// Allows the player to step, run, reset, or switch algorithms directly in FPP.
/// </summary>
public class PhysicalStationConsole : MonoBehaviour
{
    public enum ConsoleAction
    {
        Step,
        RunPause,
        Reset,
        CycleAlgorithm,
        ToggleDashboard
    }

    [System.Serializable]
    public class PhysicalButton : MonoBehaviour, IInteractable
    {
        public ConsoleAction action;
        public string label;
        public Color normalColor = Color.white;
        public Color pressColor = Color.yellow;

        private MeshRenderer meshRenderer;
        private Vector3 initialPos;
        private bool isPressed = false;

        private void Awake()
        {
            if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
            initialPos = transform.localPosition;
        }

        private void Start()
        {
            if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
            if (initialPos == Vector3.zero) initialPos = transform.localPosition;
        }

        public string PromptText => $"Press [E] to {label}";

        public void Interact()
        {
            ExecuteAction();
            AnimatePress();
        }

        private void ExecuteAction()
        {
            var sim = SimulationController.Instance;
            if (sim == null) return;

            switch (action)
            {
                case ConsoleAction.Step:
                    sim.StepForward();
                    break;
                case ConsoleAction.RunPause:
                    if (sim.IsRunning && !sim.IsPaused) sim.PauseSimulation();
                    else sim.StartSimulation();
                    break;
                case ConsoleAction.Reset:
                    sim.RestartSimulation();
                    break;
                case ConsoleAction.CycleAlgorithm:
                    CycleAlgorithm(sim);
                    break;
                case ConsoleAction.ToggleDashboard:
                    // ToggleDashboard is retired — the 3D physical lab has no fullscreen dashboard.
                    // Interaction is handled directly via physical arena buttons and workstations.
                    Debug.Log("[PhysicalStationConsole] ToggleDashboard is no longer used in the 3D lab.");
                    break;
            }
        }

        private void CycleAlgorithm(SimulationController sim)
        {
            if (sim.CurrentAlgorithm == null) return;

            SchedulingAlgorithm nextAlgo = null;
            string currentName = sim.CurrentAlgorithm.Name;

            if (currentName.Contains("First-Come")) nextAlgo = new SJFAlgorithm();
            else if (currentName.Contains("Shortest Job First")) nextAlgo = new SRTFAlgorithm();
            else if (currentName.Contains("Shortest Remaining Time")) nextAlgo = new PriorityAlgorithm(true);
            else if (currentName.Contains("Priority")) nextAlgo = new RoundRobinAlgorithm(4);
            else if (currentName.Contains("Round Robin")) nextAlgo = new MLQAlgorithm();
            else if (currentName.Contains("Multilevel Queue")) nextAlgo = new MLFQAlgorithm(new int[] { 2, 4, 8 });
            else nextAlgo = new FCFSAlgorithm();

            sim.SetAlgorithm(nextAlgo);
            Debug.Log($"Physical Console switched algorithm to: {nextAlgo.Name}");
        }

        private void AnimatePress()
        {
            if (!isPressed && gameObject.activeInHierarchy)
            {
                StartCoroutine(PressRoutine());
            }
        }

        private System.Collections.IEnumerator PressRoutine()
        {
            isPressed = true;
            if (initialPos == Vector3.zero) initialPos = transform.localPosition;
            if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();

            transform.localPosition = initialPos - new Vector3(0, 0.04f, 0);

            if (meshRenderer != null && meshRenderer.material != null)
            {
                meshRenderer.material.color = pressColor;
            }

            yield return new WaitForSeconds(0.12f);

            transform.localPosition = initialPos;
            if (meshRenderer != null && meshRenderer.material != null)
            {
                meshRenderer.material.color = normalColor;
            }
            isPressed = false;
        }
    }
}
