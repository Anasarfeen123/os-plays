using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Renders the Gantt chart timeline physically in 3D world space.
/// Creates 3D colored timeline blocks with start/end markers and process labels,
/// and computes real-time scheduling statistics.
/// </summary>
public class PhysicalGanttBoard : MonoBehaviour
{
    [Header("Board References")]
    [SerializeField] private Transform segmentsContainer;
    [SerializeField] private TextMeshPro boardTitleTMP;
    [SerializeField] private TextMeshPro boardMetricsTMP;

    [Header("Timeline Dimensions")]
    [SerializeField] private float unitWidth = 0.35f;
    [SerializeField] private float barHeight = 0.5f;
    [SerializeField] private float barDepth = 0.08f;

    private List<GameObject> timelineSegmentObjects = new List<GameObject>();

    public void RenderGanttChart(SchedulingAlgorithm algo)
    {
        if (algo == null || segmentsContainer == null) return;

        var timeline = algo.GanttChart;

        // Ensure enough segment objects
        while (timelineSegmentObjects.Count < timeline.Count)
        {
            var segObj = CreateSegmentObject(timelineSegmentObjects.Count);
            timelineSegmentObjects.Add(segObj);
        }

        float xOffset = 0f;

        for (int i = 0; i < timeline.Count; i++)
        {
            var seg = timeline[i];
            var segObj = timelineSegmentObjects[i];
            segObj.SetActive(true);

            float width = Mathf.Max(unitWidth, seg.Duration * unitWidth);

            segObj.transform.localPosition = new Vector3(xOffset + (width / 2f), 0f, -0.05f);
            segObj.transform.localScale = new Vector3(width, barHeight, barDepth);

            var renderer = segObj.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                Color c = seg.IsIdle ? new Color(0.25f, 0.25f, 0.3f) : seg.BlockColor;
                renderer.material.color = c;
                if (!seg.IsIdle)
                {
                    renderer.material.EnableKeyword("_EMISSION");
                    renderer.material.SetColor("_EmissionColor", c * 0.45f);
                }
            }

            var labelTMP = segObj.GetComponentInChildren<TextMeshPro>();
            if (labelTMP != null)
            {
                labelTMP.text = $"{seg.ProcessId}\n[{seg.StartTime} - {seg.EndTime}]";
                labelTMP.color = Color.white;
            }

            xOffset += width + 0.04f;
        }

        // Hide extra pooled segments
        for (int i = timeline.Count; i < timelineSegmentObjects.Count; i++)
        {
            timelineSegmentObjects[i].SetActive(false);
        }

        // Update Title & Metrics
        if (boardTitleTMP != null)
        {
            boardTitleTMP.text = $"CPU EXECUTION TIMELINE — {algo.Name.ToUpper()} [T = {algo.CurrentTime}]";
        }

        if (boardMetricsTMP != null)
        {
            if (algo.IsCompleted)
            {
                var res = new SchedulingResult(algo.Name)
                {
                    Processes = algo.AllProcesses,
                    GanttChart = algo.GanttChart,
                    TotalDuration = algo.CurrentTime
                };
                boardMetricsTMP.text = $"COMPLETED: Avg Wait Time: {res.AverageWaitingTime:F1}s | Avg Turnaround Time: {res.AverageTurnaroundTime:F1}s | CPU Utilization: {res.CpuUtilization:F0}%";
                boardMetricsTMP.color = new Color(0.3f, 1f, 0.4f);
            }
            else
            {
                boardMetricsTMP.text = $"SIMULATING... Current Clock: T = {algo.CurrentTime}s | Active Processes: {algo.AllProcesses.Count}";
                boardMetricsTMP.color = Color.yellow;
            }
        }
    }

    public void ClearBoard()
    {
        for (int i = 0; i < timelineSegmentObjects.Count; i++)
        {
            if (timelineSegmentObjects[i] != null)
            {
                timelineSegmentObjects[i].SetActive(false);
            }
        }

        if (boardMetricsTMP != null)
        {
            boardMetricsTMP.text = "Simulation ready. Walk to any algorithm station or press Run.";
            boardMetricsTMP.color = Color.white;
        }
    }

    private GameObject CreateSegmentObject(int index)
    {
        GameObject segObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        segObj.name = $"GanttSegment_{index}";
        segObj.transform.SetParent(segmentsContainer, false);

        Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
        if (litShader == null) litShader = Shader.Find("Standard");
        segObj.GetComponent<Renderer>().material = new Material(litShader);

        // 3D TextMeshPro label
        GameObject textObj = new GameObject("SegmentText");
        textObj.transform.SetParent(segObj.transform, false);
        textObj.transform.localPosition = new Vector3(0, 0, -0.55f);

        var tmp = textObj.AddComponent<TextMeshPro>();
        tmp.fontSize = 0.75f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;
        var rt = textObj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0.5f, 0.4f);

        return segObj;
    }
}
