using UnityEngine;
using TMPro;

/// <summary>
/// A physical 3D process block in the OS Lab simulation arena.
/// Features 3D geometry, high-tech emission materials, and crisp 3D TextMeshPro readouts
/// for PID, Arrival Time, Burst Time, Priority, and live timing statistics.
/// </summary>
public class PhysicalProcessBlock : MonoBehaviour, IInteractable
{
    public OSProcess Process { get; private set; }

    [Header("Visual References")]
    [SerializeField] private MeshRenderer meshRenderer;
    [SerializeField] private TextMeshPro labelPID;
    [SerializeField] private TextMeshPro labelDetails;
    [SerializeField] private Light pointLight;

    // Movement & Animation
    private Vector3 startPos;
    private Vector3 targetPos;
    private Quaternion startRot;
    private Quaternion targetRot;
    private float moveProgress = 1f;
    private float moveDuration = 0.5f;
    private bool useArc = false;
    private float arcHeight = 0.5f;

    private Material blockMaterial;
    private Color baseColor;

    public void Initialize(OSProcess process)
    {
        Process = process;
        baseColor = process.ProcessColor;

        if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null) litShader = Shader.Find("Standard");

            blockMaterial = new Material(litShader);
            blockMaterial.color = baseColor;
            blockMaterial.SetFloat("_Smoothness", 0.7f);
            blockMaterial.EnableKeyword("_EMISSION");
            blockMaterial.SetColor("_EmissionColor", baseColor * 0.35f);
            meshRenderer.material = blockMaterial;
        }

        if (pointLight != null)
        {
            pointLight.color = baseColor;
            pointLight.intensity = 0.8f;
        }

        targetPos = transform.position;
        targetRot = transform.rotation;
        moveProgress = 1f;

        UpdateVisualData();
    }

    public void UpdateVisualData()
    {
        if (Process == null) return;

        if (labelPID != null)
        {
            labelPID.text = Process.Id;
            labelPID.color = Color.white;
        }

        if (labelDetails != null)
        {
            if (Process.IsCompleted)
            {
                labelDetails.text = $"CT: {Process.CompletionTime} | TAT: {Process.TurnaroundTime}\nWT: {Process.WaitingTime}";
                labelDetails.color = new Color(0.4f, 1f, 0.5f);
            }
            else if (Process.State == ProcessState.Running)
            {
                labelDetails.text = $"RUNNING\nRem: {Process.RemainingTime}/{Process.BurstTime}\nPri: {Process.Priority}";
                labelDetails.color = Color.yellow;
            }
            else if (Process.State == ProcessState.Ready)
            {
                labelDetails.text = $"READY\nRem: {Process.RemainingTime}/{Process.BurstTime}\nPri: {Process.Priority}";
                labelDetails.color = Color.white;
            }
            else // New / Not yet arrived
            {
                labelDetails.text = $"ARRIVES: T={Process.ArrivalTime}\nBT: {Process.BurstTime} | Pri: {Process.Priority}";
                labelDetails.color = new Color(0.8f, 0.8f, 0.9f);
            }
        }

        if (blockMaterial != null)
        {
            float emissionMult = 0.25f;
            if (Process.State == ProcessState.Running) emissionMult = 1.0f;
            else if (Process.IsCompleted) emissionMult = 0.15f;

            blockMaterial.SetColor("_EmissionColor", baseColor * emissionMult);
        }
    }

    public void MoveTo(Vector3 newPos, Quaternion newRot, float duration = 0.4f, bool arc = false, float arcHeightVal = 0.4f)
    {
        startPos = transform.position;
        targetPos = newPos;
        startRot = transform.rotation;
        targetRot = newRot;
        moveDuration = Mathf.Max(0.05f, duration);
        moveProgress = 0f;
        useArc = arc;
        arcHeight = arcHeightVal;
    }

    private void Update()
    {
        if (moveProgress < 1f)
        {
            moveProgress += Time.deltaTime / moveDuration;
            float t = Mathf.Clamp01(moveProgress);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            Vector3 current = Vector3.Lerp(startPos, targetPos, smoothT);
            if (useArc)
            {
                // Parabolic jump arc: 4 * h * t * (1 - t)
                float h = 4f * arcHeight * t * (1f - t);
                current.y += h;
            }

            transform.position = current;
            transform.rotation = Quaternion.Slerp(startRot, targetRot, smoothT);
        }

        // Pulse emission slightly when running
        if (Process != null && Process.State == ProcessState.Running && blockMaterial != null)
        {
            float pulse = 0.8f + Mathf.PingPong(Time.time * 2f, 0.4f);
            blockMaterial.SetColor("_EmissionColor", baseColor * pulse);
        }
    }

    public string PromptText
    {
        get
        {
            if (Process == null) return "Process Block";
            return $"Process {Process.Id} (AT: {Process.ArrivalTime}, BT: {Process.BurstTime}, Pri: {Process.Priority}, Rem: {Process.RemainingTime})";
        }
    }

    public void Interact()
    {
        if (Process == null) return;
        Debug.Log($"Interacted with Process {Process.Id}");

        if (PhysicalProcessEditor.Instance != null)
        {
            PhysicalProcessEditor.Instance.SelectProcess(Process.Id);
        }
    }
}
