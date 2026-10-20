using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;

/// <summary>
/// GRIDLOCK layout pass for the CPU lab, run once the arena has built itself (it assembles everything at runtime).
/// The arena put screens, readouts and buttons as children of non-uniformly scaled podiums, so in a headset they came
/// out skewed and oversized, two podiums overlapped, labels lay flat on the floor or across the process blocks, and the
/// Gantt chart on the far wall was too small to read. This keeps every object and behaviour and only re-places them:
/// <list type="bullet">
/// <item>three desks facing the player: CONSOLE (run / step / reset) left, UTILITY (randomize, challenge) centre,
/// PROCESS EDITOR right, spaced so the CPU core stays in view between them;</item>
/// <item>each desk's screen, readout and buttons sit on an unscaled panel: square, upright, a tilted screen at the back
/// and the buttons in a row at the front edge;</item>
/// <item>the CPU status stands above the core and the queue titles stand behind their shelves;</item>
/// <item>the Gantt chart is larger, with legible titles.</item>
/// </list>
/// </summary>
public class CpuLabLayout : MonoBehaviour
{
    const float DeskZ = -2.8f, DeskDepth = 0.9f, DeskHeight = 1.0f;

    IEnumerator Start()
    {
        // The arena builds in its Start; its utility buttons a frame later.
        yield return null;
        yield return null;
        Apply();
        var wait = new WaitForSecondsRealtime(1f);
        while (true)
        {
            QuizWall(); // the challenge station rebuilds its display now and then
            yield return wait;
        }
    }

    Transform quizContent;

    /// <summary>The challenge wall's display a little larger and higher: legible from the middle of the lab, nothing near the floor.</summary>
    void QuizWall()
    {
        // Searching the whole scene every second hitches the Quest's frame; look again only once the display was rebuilt.
        if (quizContent != null && quizContent.localScale.x > 1.1f) return;
        var content = quizContent = FindAny("StationContent");
        if (content == null || content.localScale.x > 1.1f) return;
        content.localScale = Vector3.one * 1.15f;
        content.localPosition += Vector3.up * 0.3f;
    }

    public void Apply()
    {
        Desk("Physical_Control_Console", -2.7f, 2.0f, "ConsoleScreen", "ConsoleReadout_TMP", "Btn_RunPause", "Btn_Step", "Btn_Reset");
        Desk("Physical_Utility_Podium", 0f, 1.3f, null, null, "Btn_Randomize", "Btn_Challenge");
        Desk("Physical_Process_Editor", 2.7f, 2.0f, "EditorScreen", "EditorDisplay_TMP", "Btn_NextProc", "Btn_IncBurst");
        Labels();
        Gantt();
    }

    static Transform FindAny(string name) =>
        FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(t => t.name == name);

    static void Desk(string podiumName, float x, float width, string screenName, string readoutName, params string[] buttonNames)
    {
        var podium = FindAny(podiumName);
        if (podium == null) return;
        var screen = screenName != null ? FindAny(screenName) : null;
        var readout = readoutName != null ? FindAny(readoutName) : null;
        var buttons = buttonNames.Select(FindAny).Where(b => b != null).ToArray();

        podium.SetPositionAndRotation(new Vector3(x, DeskHeight * 0.5f, DeskZ), Quaternion.identity);
        podium.localScale = new Vector3(width, DeskHeight, DeskDepth);

        // Everything on the desk hangs off an unscaled panel at the desk top.
        var panel = new GameObject(podiumName + "_Panel").transform;
        panel.SetParent(podium.parent, false);
        panel.SetPositionAndRotation(new Vector3(x, DeskHeight, DeskZ), Quaternion.identity);

        if (screen != null)
        {
            var tilt = Quaternion.Euler(22f, 0f, 0f); // top leans away: read from standing height
            var size = new Vector2(width * 0.82f, 0.5f);
            screen.SetParent(panel, false);
            screen.localPosition = new Vector3(0f, 0.33f, DeskDepth * 0.5f - 0.12f);
            screen.localRotation = tilt;
            screen.localScale = new Vector3(size.x, size.y, 0.03f);
            if (readout != null)
            {
                readout.SetParent(panel, false);
                readout.localPosition = screen.localPosition + tilt * new Vector3(0f, 0f, -0.025f);
                readout.localRotation = tilt;
                readout.localScale = Vector3.one;
                if (readout is RectTransform rt) rt.sizeDelta = size * new Vector2(0.92f, 0.86f);
                if (readout.TryGetComponent<TMP_Text>(out var text))
                {
                    text.enableAutoSizing = true;
                    text.fontSizeMin = 0.2f;
                    text.fontSizeMax = 0.9f;
                    text.textWrappingMode = TextWrappingModes.Normal;
                }
            }
        }

        // Buttons in a row along the front edge, where a hand or a ray reaches them first.
        float span = Mathf.Min(width * 0.8f, 0.42f * buttons.Length);
        for (int i = 0; i < buttons.Length; i++)
        {
            var b = buttons[i];
            b.SetParent(panel, false);
            float bx = buttons.Length == 1 ? 0f : Mathf.Lerp(-span * 0.5f, span * 0.5f, i / (float)(buttons.Length - 1));
            b.localPosition = new Vector3(bx, 0.04f, -DeskDepth * 0.5f + 0.2f);
            b.localRotation = Quaternion.identity;
            b.localScale = new Vector3(0.2f, 0.04f, 0.2f);
            // The press animation returns a button to where it was at start-up: it is here now.
            foreach (var behaviour in b.GetComponents<MonoBehaviour>())
            {
                var rest = behaviour.GetType().GetField("initialLocalPos", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                if (rest != null && rest.FieldType == typeof(Vector3)) rest.SetValue(behaviour, b.localPosition);
            }
            // Its label goes on the desk in front of it.
            var label = b.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault();
            if (label == null) continue;
            label.transform.SetParent(panel, false);
            // Printed on the desk just in front of the button, tilted up to the eyes: readable, and never in the way.
            label.transform.localPosition = new Vector3(bx, 0.006f, -DeskDepth * 0.5f + 0.06f);
            label.transform.localRotation = Quaternion.Euler(65f, 0f, 0f);
            label.transform.localScale = Vector3.one;
            label.rectTransform.sizeDelta = new Vector2(0.4f, 0.1f);
            label.enableAutoSizing = true;
            label.fontSizeMin = 0.12f;
            label.fontSizeMax = 0.4f;
            label.textWrappingMode = TextWrappingModes.Normal;
        }
    }

    static void Labels()
    {
        void Stand(string name, Vector3 position, float fontSize, float width)
        {
            var t = FindAny(name);
            if (t == null) return;
            t.SetPositionAndRotation(position, Quaternion.identity); // upright, facing the player's side
            t.localScale = Vector3.one;
            if (t.TryGetComponent<TMP_Text>(out var text))
            {
                text.enableAutoSizing = false;
                text.fontSize = fontSize;
                text.rectTransform.sizeDelta = new Vector2(width, fontSize * 0.2f);
            }
        }
        Stand("CPU_Status_TMP", new Vector3(0f, 1.75f, -0.4f), 1.4f, 3.2f);
        Stand("ReadyQueueHeader_TMP", new Vector3(0f, 1.3f, 2.95f), 1.6f, 4.4f);
        Stand("IncomingHeader_TMP", new Vector3(-4.5f, 1.2f, 1.15f), 1.4f, 2.6f);
        Stand("CompletedHeader_TMP", new Vector3(4.5f, 1.2f, 1.15f), 1.4f, 2.6f);
    }

    static void Gantt()
    {
        var board = FindAny("Teaching_Whiteboard");
        if (board == null) return;
        board.SetPositionAndRotation(new Vector3(0f, 2.6f, 9.7f), Quaternion.identity);
        board.localScale = Vector3.one * 1.6f;
        void Text(string name, float size, float width)
        {
            var t = board.Find(name);
            if (t == null || !t.TryGetComponent<TMP_Text>(out var text)) return;
            text.enableAutoSizing = false;
            text.fontSize = size;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.rectTransform.sizeDelta = new Vector2(width, size * 0.3f);
        }
        Text("GanttTitle_TMP", 1.5f, 11f);
        Text("GanttMetrics_TMP", 0.95f, 11f);
    }
}
