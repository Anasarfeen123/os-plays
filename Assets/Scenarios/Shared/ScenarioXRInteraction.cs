using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Gridlock.XR;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Headset glue for the imported scenarios' look-and-press objects (<see cref="IInteractable"/>, "[E] to …"): each one
/// becomes an XR simple interactable, so pointing a controller or hand at it and pulling the trigger (or pinching) does
/// what [E] does on a keyboard. Hovering lights the object up and shows its prompt next to it. Objects the scenario creates later are
/// picked up by a periodic scan; hidden ones (the quiz's NEXT button) are bridged in advance, so they answer the moment
/// they appear. The scan is frequent while the scene assembles itself and slows down after that, because searching the
/// whole scene costs the Quest frame time.
/// </summary>
public class ScenarioXRInteraction : MonoBehaviour
{
    [SerializeField] float scanSeconds = 0.5f;
    [Tooltip("After this long in the scene, scan only every slowScanSeconds.")]
    [SerializeField] float settleSeconds = 8f;
    [SerializeField] float slowScanSeconds = 2f;

    readonly HashSet<MonoBehaviour> bridged = new HashSet<MonoBehaviour>();
    readonly List<Collider> colliders = new List<Collider>();
    readonly List<Renderer> renderers = new List<Renderer>();
    static readonly Regex KeyboardWords = new Regex(@"^(Press|Tap)?\s*\[E\](\s*or\s+click)?\s*(to|for|:)?\s*", RegexOptions.IgnoreCase);
    TextMeshPro prompt;
    Transform promptTarget;
    Renderer lit;
    MaterialPropertyBlock highlight;
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    static readonly Color HighlightTint = new Color(0.35f, 0.85f, 1f);

    IEnumerator Start()
    {
        var mode = XRModeController.Current;
        if (mode == null || !mode.XRActive) yield break;
        prompt = BuildPrompt();
        var fast = new WaitForSecondsRealtime(scanSeconds);
        var slow = new WaitForSecondsRealtime(slowScanSeconds);
        float started = Time.realtimeSinceStartup;
        while (true)
        {
            Scan();
            yield return Time.realtimeSinceStartup - started < settleSeconds ? fast : slow;
        }
    }

    void Scan()
    {
        foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (behaviour is not IInteractable target || bridged.Contains(behaviour)) continue;
            bridged.Add(behaviour);

            // Only the colliders that belong to this object, not to an interactable nested inside it.
            colliders.Clear();
            foreach (var c in behaviour.GetComponentsInChildren<Collider>(true))
                if (!c.isTrigger && ReferenceEquals(c.GetComponentInParent<IInteractable>(true), target)) colliders.Add(c);
            if (colliders.Count == 0) continue;

            var interactable = behaviour.GetComponent<XRSimpleInteractable>();
            if (interactable == null)
            {
                interactable = behaviour.gameObject.AddComponent<XRSimpleInteractable>();
                interactable.enabled = false; // re-register with the filtered collider list
                interactable.colliders.Clear();
                interactable.colliders.AddRange(colliders);
                interactable.enabled = true;
            }
            var owner = behaviour;
            interactable.selectEntered.AddListener(_ =>
            {
                Unlight(); // the object may recolour itself (a chosen answer turns green or red)
                if (owner != null && owner.isActiveAndEnabled) target.Interact();
            });
            interactable.hoverEntered.AddListener(_ => Show(owner, target));
            interactable.hoverExited.AddListener(_ => { if (promptTarget == owner.transform) Hide(); });
        }
    }

    void Show(MonoBehaviour owner, IInteractable target)
    {
        Light(owner.GetComponent<Renderer>());
        string text = target.PromptText;
        if (string.IsNullOrEmpty(text)) return;
        prompt.text = KeyboardWords.Replace(text, "").Replace("[E]", "trigger");
        promptTarget = owner.transform;
        prompt.gameObject.SetActive(true);
    }

    void Hide()
    {
        Unlight();
        promptTarget = null;
        prompt.gameObject.SetActive(false);
    }

    /// <summary>Tints the pointed-at object towards cyan (its own colour stays in the material, untouched).</summary>
    void Light(Renderer renderer)
    {
        Unlight();
        if (renderer == null || renderer.sharedMaterial == null || !renderer.sharedMaterial.HasProperty(BaseColor)) return;
        highlight ??= new MaterialPropertyBlock();
        highlight.SetColor(BaseColor, Color.Lerp(renderer.sharedMaterial.GetColor(BaseColor), HighlightTint, 0.35f));
        renderer.SetPropertyBlock(highlight);
        lit = renderer;
    }

    void Unlight()
    {
        if (lit != null) lit.SetPropertyBlock(null);
        lit = null;
    }

    void LateUpdate()
    {
        if (prompt == null || promptTarget == null) return;
        if (!promptTarget.gameObject.activeInHierarchy) { Hide(); return; }
        var cam = Camera.main;
        if (cam == null) return;
        // Just above the object's top, facing the eyes.
        var bounds = new Bounds(promptTarget.position, Vector3.zero);
        promptTarget.GetComponentsInChildren(renderers); // no allocation: this runs every frame while hovering
        foreach (var r in renderers) if (r is not ParticleSystemRenderer) bounds.Encapsulate(r.bounds);
        var p = new Vector3(bounds.center.x, bounds.max.y + 0.12f, bounds.center.z);
        p = Vector3.MoveTowards(p, cam.transform.position, 0.15f);
        prompt.transform.SetPositionAndRotation(p, Quaternion.LookRotation(p - cam.transform.position));
    }

    static TextMeshPro BuildPrompt()
    {
        var go = new GameObject("XR Prompt");
        var text = go.AddComponent<TextMeshPro>();
        text.fontSize = 0.9f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(0.98f, 0.75f, 0.14f);
        text.fontStyle = FontStyles.Bold;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.outlineWidth = 0.2f;
        text.outlineColor = new Color32(5, 8, 14, 255);
        text.rectTransform.sizeDelta = new Vector2(4f, 0.4f);
        go.SetActive(false);
        return text;
    }
}
