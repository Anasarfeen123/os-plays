using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Readability pass for the imported CPU lab's 3D text (it builds most of its labels at runtime). Wording, layout and font
/// stay as the scenario made them; on top of that:
/// <list type="bullet">
/// <item>text that is small for where it hangs grows (up to <see cref="growLimit"/>×) as far as its box allows;</item>
/// <item>text stretched by a non-uniformly scaled parent (the console readout sat on a 2.8 × 1 desk) gets its proportions back;</item>
/// <item>text on a screen (the console and editor readouts) is boxed to that screen and shrinks to fit it;</item>
/// <item>free-floating labels sit on a card (dark rounded plate, thin cyan edge) that follows the text's size;</item>
/// <item>the challenge station's display (question, answer buttons, labels) was built facing the wall, so the room saw
/// it mirrored and half hidden: it is turned round to face the room.</item>
/// </list>
/// Text on an object (button caps, process blocks, Gantt bars) needs no card and keeps its own size.
/// </summary>
public class ScenarioTextFit : MonoBehaviour
{
    [Tooltip("Font size × world scale a label should reach where its box has room.")]
    [SerializeField] float readableHeight = 0.8f;
    [SerializeField] float growLimit = 2f;
    [SerializeField] float scanSeconds = 0.5f;
    [Tooltip("Wall displays (built at runtime) that must face the room.")]
    [SerializeField] string[] wallMounted = { "StationContent" };
    [Tooltip("Rounded 9-slice sprite for the cards (UISprite).")]
    [SerializeField] Sprite cardSprite;
    [SerializeField] Color cardColor = new Color(0.04f, 0.07f, 0.13f, 0.88f);
    [SerializeField] Color edgeColor = new Color(0.22f, 0.74f, 0.97f, 0.85f);

    class Card
    {
        public TextMeshPro Text;
        public SpriteRenderer Fill, Edge;
        public Bounds Last;
        public string LastText;
    }

    readonly HashSet<TMP_Text> fitted = new HashSet<TMP_Text>();
    readonly List<Card> cards = new List<Card>();

    IEnumerator Start()
    {
        var wait = new WaitForSecondsRealtime(scanSeconds);
        while (true)
        {
            foreach (var name in wallMounted)
            {
                var display = GameObject.Find(name);
                if (display != null) FaceRoom(display.transform); // every scan: the station re-aims it when it rebuilds
            }
            foreach (var text in FindObjectsByType<TextMeshPro>(FindObjectsSortMode.None))
                if (fitted.Add(text)) Fit(text);
            fitted.RemoveWhere(t => t == null);
            cards.RemoveAll(c => c.Text == null);
            yield return wait;
        }
    }

    void LateUpdate()
    {
        foreach (var text in fitted)
            if (text != null) Unstretch(text.transform); // parents that resize as the run goes (the Gantt bars)
        foreach (var card in cards) Resize(card);
    }

    void Fit(TextMeshPro text)
    {
        var rt = text.rectTransform;
        Unstretch(rt);

        float size = text.fontSize, scale = Mathf.Abs(rt.lossyScale.y);
        if (size <= 0f || scale <= 0f) return;
        bool screen = FitToScreen(rt);
        bool small = size * scale < readableHeight;
        if (small || screen)
        {
            text.enableAutoSizing = true;
            text.fontSizeMax = small ? size * Mathf.Min(growLimit, readableHeight / (size * scale)) : size;
            text.fontSizeMin = size * 0.3f;
            if (screen) text.textWrappingMode = TextWrappingModes.Normal;
        }
        bool onFloor = Mathf.Abs(rt.forward.y) > 0.7f; // printed flat on the floor: a card would only be a big slab
        if (!screen && !onFloor && cardSprite != null && !OnObject(rt)) cards.Add(MakeCard(text));
    }

    /// <summary>Text printed on something solid (a button cap, a block, a bar) rather than floating in the air.</summary>
    static bool OnObject(Transform t) =>
        (t.parent != null && t.parent.GetComponent<Renderer>() != null) ||
        Physics.Raycast(t.position - t.forward * 0.01f, t.forward, 0.1f); // a button or panel right behind it

    Card MakeCard(TextMeshPro text)
    {
        SpriteRenderer Plate(string name, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(text.transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = cardSprite;
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.color = color;
            sr.sortingLayerID = text.sortingLayerID;
            sr.sortingOrder = text.sortingOrder + order; // drawn before the letters
            sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sr.receiveShadows = false;
            return sr;
        }
        var card = new Card { Text = text, Edge = Plate("Card Edge", edgeColor, -2), Fill = Plate("Card", cardColor, -1) };
        Resize(card, force: true);
        return card;
    }

    void Resize(Card card, bool force = false)
    {
        var text = card.Text;
        if (text == null) return;
        if (!force && card.LastText == text.text && card.Last == text.textBounds) return;
        card.LastText = text.text;
        bool empty = string.IsNullOrWhiteSpace(text.GetParsedText());
        card.Fill.enabled = card.Edge.enabled = !empty && text.isActiveAndEnabled;
        if (empty) return;
        text.ForceMeshUpdate();
        var b = text.textBounds;
        card.Last = b;
        // Sliced sprites size in the sprite's units; the sprite is scaled so its corner radius suits the letter size.
        float pad = text.fontSize * 0.12f;
        float unit = text.fontSize * 0.02f; // corner radius tracks the letter size
        var size = new Vector2(b.size.x + pad * 2f, b.size.y + pad * 1.4f);
        foreach (var (sr, grow, depth) in new[] { (card.Edge, pad * 0.18f, 0.012f), (card.Fill, 0f, 0.006f) })
        {
            sr.transform.localPosition = new Vector3(b.center.x, b.center.y, depth * text.fontSize);
            sr.transform.localRotation = Quaternion.identity;
            sr.transform.localScale = Vector3.one * unit;
            sr.size = (size + Vector2.one * grow * 2f) / unit;
        }
    }

    [Tooltip("A point inside the room the wall text should face (the lab floor's centre at eye height).")]
    [SerializeField] Vector3 roomCentre = new Vector3(0f, 1.6f, 0f);

    /// <summary>Turn a wall display round if its front (TMP reads from -forward) faces the wall.</summary>
    void FaceRoom(Transform t)
    {
        var toRoom = Vector3.ProjectOnPlane(roomCentre - t.position, Vector3.up);
        if (Vector3.Dot(-t.forward, toRoom) < 0f) t.Rotate(0f, 180f, 0f, Space.World);
    }

    /// <summary>Equal x and y world scale, so letters are not squashed or stretched by a scaled parent.</summary>
    static void Unstretch(Transform t)
    {
        var lossy = t.lossyScale;
        if (Mathf.Abs(lossy.x) < 1e-5f || Mathf.Abs(lossy.y) < 1e-5f) return;
        float ratio = Mathf.Abs(lossy.y / lossy.x);
        if (Mathf.Abs(ratio - 1f) < 0.05f) return;
        var local = t.localScale;
        t.localScale = new Vector3(local.x * ratio, local.y, local.z * Mathf.Abs(lossy.y / lossy.z));
    }

    /// <summary>
    /// A readout on a display surface (its parent, or a sibling, named "…Screen") gets that surface's size, with a margin,
    /// as its box.
    /// </summary>
    static bool FitToScreen(RectTransform rt)
    {
        if (!(rt.name.Contains("Readout") || rt.name.Contains("Display"))) return false;
        var parent = rt.parent;
        if (parent == null) return false;
        Transform surface = parent.name.EndsWith("Screen") ? parent : null;
        if (surface == null)
            foreach (Transform sibling in parent)
                if (sibling.name.EndsWith("Screen")) { surface = sibling; break; }
        var mesh = surface != null ? surface.GetComponent<MeshFilter>() : null;
        if (mesh == null || mesh.sharedMesh == null) return false;
        var size = mesh.sharedMesh.bounds.size;
        var lossyScreen = surface.lossyScale;
        var lossyText = rt.lossyScale;
        rt.sizeDelta = new Vector2(size.x * Mathf.Abs(lossyScreen.x) * 0.9f / Mathf.Abs(lossyText.x),
                                   size.y * Mathf.Abs(lossyScreen.y) * 0.85f / Mathf.Abs(lossyText.y));
        if (surface == parent) rt.localPosition = new Vector3(0f, 0f, rt.localPosition.z);
        return true;
    }
}
