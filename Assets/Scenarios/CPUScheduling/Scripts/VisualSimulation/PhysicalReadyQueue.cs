using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Manages the physical process blocks in the shared simulation arena.
/// Organizes Incoming (New), Ready Queue, and Completed processes on physical platforms,
/// handling smooth 3D movement and preemption transitions.
/// </summary>
public class PhysicalReadyQueue : MonoBehaviour
{
    [Header("Queue Layout Positions")]
    [SerializeField] private Transform queueOrigin;
    [SerializeField] private Transform incomingOrigin;
    [SerializeField] private Transform completedOrigin;

    [Header("Spacing Settings")]
    [SerializeField] private Vector3 slotSpacing = new Vector3(0.55f, 0f, 0f);
    [SerializeField] private Vector3 incomingSpacing = new Vector3(0.55f, 0f, 0f);
    [SerializeField] private Vector3 completedSpacing = new Vector3(0.55f, 0f, 0f);

    [Header("3D Text Headers")]
    [SerializeField] private TextMeshPro queueHeaderTMP;
    [SerializeField] private TextMeshPro incomingHeaderTMP;
    [SerializeField] private TextMeshPro completedHeaderTMP;

    private Dictionary<string, PhysicalProcessBlock> processBlockMap = new Dictionary<string, PhysicalProcessBlock>();
    private string lastRunningProcessId = null; // Track previous running process for arc animation

    private void Start()
    {
        if (queueHeaderTMP != null)
        {
            queueHeaderTMP.fontSize = 1.8f;
            queueHeaderTMP.transform.rotation = Quaternion.Euler(45f, 0, 0);
            var rt = queueHeaderTMP.GetComponent<RectTransform>();
            if (rt != null) rt.sizeDelta = new Vector2(6.0f, 1.2f);
        }
        if (incomingHeaderTMP != null)
        {
            incomingHeaderTMP.fontSize = 1.8f;
            incomingHeaderTMP.transform.rotation = Quaternion.Euler(45f, 0, 0);
            var rt = incomingHeaderTMP.GetComponent<RectTransform>();
            if (rt != null) rt.sizeDelta = new Vector2(6.0f, 1.2f);
        }
        if (completedHeaderTMP != null)
        {
            completedHeaderTMP.fontSize = 1.8f;
            completedHeaderTMP.transform.rotation = Quaternion.Euler(45f, 0, 0);
            var rt = completedHeaderTMP.GetComponent<RectTransform>();
            if (rt != null) rt.sizeDelta = new Vector2(6.0f, 1.2f);
        }
    }

    public void SyncWithAlgorithm(SchedulingAlgorithm algo, PhysicalCPUCore cpuCore)
    {
        if (algo == null) return;

        Vector3 qBasePos = queueOrigin != null ? queueOrigin.position : transform.position;
        Quaternion qBaseRot = queueOrigin != null ? queueOrigin.rotation : transform.rotation;

        Vector3 inBasePos = incomingOrigin != null ? incomingOrigin.position : (qBasePos + new Vector3(-1.8f, 0f, 0.8f));
        Quaternion inBaseRot = incomingOrigin != null ? incomingOrigin.rotation : qBaseRot;

        Vector3 compBasePos = completedOrigin != null ? completedOrigin.position : (qBasePos + new Vector3(1.8f, 0f, 0.8f));
        Quaternion compBaseRot = completedOrigin != null ? completedOrigin.rotation : qBaseRot;

        // 1. Ensure all process blocks exist
        var allProcesses = algo.AllProcesses;
        foreach (var p in allProcesses)
        {
            if (!processBlockMap.TryGetValue(p.Id, out var block) || block == null)
            {
                block = CreateProcessBlock(p, inBasePos, inBaseRot);
                processBlockMap[p.Id] = block;
            }
            block.UpdateVisualData();
        }

        // 2. CPU Docking / Undocking
        if (algo.RunningProcess != null && cpuCore != null)
        {
            if (processBlockMap.TryGetValue(algo.RunningProcess.Id, out var runningBlock))
            {
                if (cpuCore.DockedBlock != runningBlock)
                {
                    cpuCore.DockProcess(runningBlock);
                }
            }
        }
        else if (cpuCore != null)
        {
            cpuCore.UndockProcess();
        }

        // 3. Ready Queue Layout
        var readyList = algo.GetReadyQueue();
        int readyCount = 0;
        string currentRunningId = algo.RunningProcess?.Id;

        for (int i = 0; i < readyList.Count; i++)
        {
            var p = readyList[i];
            if (p == algo.RunningProcess) continue; // On CPU

            if (processBlockMap.TryGetValue(p.Id, out var block))
            {
                Vector3 targetPos = qBasePos + (slotSpacing * readyCount);
                // Show arc animation for the process that was just preempted (was running last tick, now in queue)
                bool justPreempted = (p.Id == lastRunningProcessId && p.Id != currentRunningId);
                block.MoveTo(targetPos, qBaseRot, 0.45f, justPreempted, 0.55f);
                readyCount++;
            }
        }

        // Track running ID for next frame's preemption check
        lastRunningProcessId = currentRunningId;

        // 4. Incoming (Unarrived) Layout
        int incomingCount = 0;
        foreach (var p in allProcesses)
        {
            if (p.State == ProcessState.New && p != algo.RunningProcess)
            {
                if (processBlockMap.TryGetValue(p.Id, out var block))
                {
                    Vector3 targetPos = inBasePos + (incomingSpacing * incomingCount);
                    block.MoveTo(targetPos, inBaseRot, 0.4f);
                    incomingCount++;
                }
            }
        }

        // 5. Completed Layout
        int completedCount = 0;
        foreach (var p in allProcesses)
        {
            if (p.IsCompleted && p != algo.RunningProcess)
            {
                if (processBlockMap.TryGetValue(p.Id, out var block))
                {
                    Vector3 targetPos = compBasePos + (completedSpacing * completedCount);
                    block.MoveTo(targetPos, compBaseRot, 0.5f, true, 0.4f);
                    completedCount++;
                }
            }
        }

        // 6. Header Text Updates
        if (queueHeaderTMP != null)
        {
            queueHeaderTMP.text = $"READY QUEUE ({readyCount})";
        }
        if (incomingHeaderTMP != null)
        {
            incomingHeaderTMP.text = $"INCOMING ({incomingCount})";
        }
        if (completedHeaderTMP != null)
        {
            completedHeaderTMP.text = $"COMPLETED ({completedCount})";
        }
    }

    public void ResetQueue()
    {
        foreach (var kvp in processBlockMap)
        {
            if (kvp.Value != null)
            {
                Destroy(kvp.Value.gameObject);
            }
        }
        processBlockMap.Clear();
    }

    private PhysicalProcessBlock CreateProcessBlock(OSProcess process, Vector3 spawnPos, Quaternion spawnRot)
    {
        GameObject blockObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        blockObj.name = $"ProcessBlock_{process.Id}";
        blockObj.transform.SetParent(transform, true);
        blockObj.transform.position = spawnPos;
        blockObj.transform.rotation = spawnRot;
        blockObj.transform.localScale = new Vector3(0.42f, 0.42f, 0.42f);

        // Add BoxCollider for player interaction
        var collider = blockObj.GetComponent<BoxCollider>();
        if (collider == null) collider = blockObj.AddComponent<BoxCollider>();
        collider.size = new Vector3(1.2f, 1.2f, 1.2f); // Generous raycast hit box

        // 3D TextMeshPro for PID on top and front
        GameObject pidObj = new GameObject("PID_Text");
        pidObj.transform.SetParent(blockObj.transform, false);
        pidObj.transform.localPosition = new Vector3(0, 0.45f, 0);
        pidObj.AddComponent<Billboard3D>();

        var pidTMP = pidObj.AddComponent<TextMeshPro>();
        pidTMP.text = process.Id;
        pidTMP.fontSize = 2.4f;
        pidTMP.alignment = TextAlignmentOptions.Center;
        pidTMP.fontStyle = FontStyles.Bold;
        pidTMP.color = Color.white;
        var pidRect = pidObj.GetComponent<RectTransform>();
        pidRect.sizeDelta = new Vector2(2.5f, 0.7f);

        // 3D TextMeshPro for Details below PID
        GameObject detObj = new GameObject("Details_Text");
        detObj.transform.SetParent(pidObj.transform, false);
        detObj.transform.localPosition = new Vector3(0, -0.35f, 0);

        var detTMP = detObj.AddComponent<TextMeshPro>();
        detTMP.text = $"BT: {process.BurstTime} | Pri: {process.Priority}";
        detTMP.fontSize = 1.4f;
        detTMP.alignment = TextAlignmentOptions.Center;
        detTMP.fontStyle = FontStyles.Bold;
        detTMP.color = new Color(0.95f, 0.95f, 0.95f);
        var detRect = detObj.GetComponent<RectTransform>();
        detRect.sizeDelta = new Vector2(3.5f, 0.8f);

        // Light point
        GameObject lightObj = new GameObject("BlockLight");
        lightObj.transform.SetParent(blockObj.transform, false);
        lightObj.transform.localPosition = new Vector3(0, 0.2f, 0);
        var pLight = lightObj.AddComponent<Light>();
        pLight.type = LightType.Point;
        pLight.range = 1.5f;
        pLight.intensity = 0.8f;
        pLight.color = process.ProcessColor;

        var block = blockObj.AddComponent<PhysicalProcessBlock>();

        // Set private fields via reflection or properties
        SetPrivateField(block, "meshRenderer", blockObj.GetComponent<MeshRenderer>());
        SetPrivateField(block, "labelPID", pidTMP);
        SetPrivateField(block, "labelDetails", detTMP);
        SetPrivateField(block, "pointLight", pLight);

        block.Initialize(process);
        return block;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null) field.SetValue(target, value);
    }
}
