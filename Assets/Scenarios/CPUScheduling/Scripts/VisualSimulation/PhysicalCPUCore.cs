using UnityEngine;
using TMPro;

/// <summary>
/// Physical 3D CPU Core platform in the shared simulation arena.
/// Features a glowing execution pedestal, docking socket, point light,
/// and 3D TextMeshPro status indicator (IDLE, RUNNING, CONTEXT SWITCH, PREEMPTED).
/// </summary>
public class PhysicalCPUCore : MonoBehaviour
{
    [Header("Core Socket")]
    [SerializeField] private Transform socketTransform;

    [Header("Visual References")]
    [SerializeField] private TextMeshPro statusTMP;
    [SerializeField] private Light coreLight;
    [SerializeField] private MeshRenderer socketRingRenderer;
    [SerializeField] private Transform coreRotor; // Optional rotating inner core

    private PhysicalProcessBlock dockedBlock;
    private Material socketMaterial;

    private void Awake()
    {
        if (socketTransform == null) socketTransform = transform;
        if (socketRingRenderer != null)
        {
            socketMaterial = socketRingRenderer.material;
        }
    }

    public Transform SocketTransform => socketTransform != null ? socketTransform : transform;
    public PhysicalProcessBlock DockedBlock => dockedBlock;

    public void SetCpuState(CpuState state, OSProcess runningProcess)
    {
        Color stateColor = new Color(0.3f, 0.4f, 0.55f);
        string statusText = "CPU: IDLE";

        switch (state)
        {
            case CpuState.Idle:
                stateColor = new Color(0.3f, 0.45f, 0.6f);
                statusText = "CPU: IDLE (WAITING)";
                break;
            case CpuState.Running:
                stateColor = new Color(0.15f, 0.95f, 0.4f);
                statusText = runningProcess != null ? $"CPU: EXECUTING [{runningProcess.Id}]" : "CPU: RUNNING";
                break;
            case CpuState.ContextSwitch:
                stateColor = new Color(1.0f, 0.65f, 0.1f);
                statusText = "CPU: CONTEXT SWITCH / PREEMPT";
                break;
        }

        if (statusTMP != null)
        {
            statusTMP.text = statusText;
            statusTMP.color = stateColor;
        }

        if (coreLight != null)
        {
            coreLight.color = stateColor;
            coreLight.intensity = (state == CpuState.Running) ? 2.0f : 0.8f;
        }

        if (socketMaterial != null)
        {
            socketMaterial.color = stateColor;
            socketMaterial.EnableKeyword("_EMISSION");
            socketMaterial.SetColor("_EmissionColor", stateColor * 1.2f);
        }
    }

    public void DockProcess(PhysicalProcessBlock block, bool isPreemption = false)
    {
        dockedBlock = block;
        if (dockedBlock != null)
        {
            // Smoothly move into CPU socket with arc
            dockedBlock.MoveTo(SocketTransform.position, SocketTransform.rotation, 0.45f, true, 0.6f);
        }
    }

    public void UndockProcess()
    {
        dockedBlock = null;
    }

    public void PulseClockCycle()
    {
        if (coreLight != null)
        {
            coreLight.intensity = 3.0f;
        }
    }

    private void Update()
    {
        if (coreLight != null && coreLight.intensity > 1.2f)
        {
            coreLight.intensity = Mathf.Lerp(coreLight.intensity, 1.0f, Time.deltaTime * 4f);
        }

        if (coreRotor != null)
        {
            float speed = (dockedBlock != null) ? 120f : 30f;
            coreRotor.Rotate(Vector3.up * (speed * Time.deltaTime));
        }
    }
}
