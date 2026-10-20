using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Moves a page box to its new place (a frame's pallet, or back to its table) in a short eased glide with a little lift,
/// instead of teleporting it. The game state changes at once; only the box's motion is animated. Grabbing the box
/// stops the glide.
/// </summary>
public class BoxGlide : MonoBehaviour
{
    const float Seconds = 0.35f;
    const float Lift = 0.25f;

    Coroutine running;

    /// <summary>Glides <paramref name="box"/> under <paramref name="parent"/> to the given pose (world pose if parent is null).</summary>
    public static void To(GameObject box, Transform parent, Vector3 position, Quaternion rotation, bool kinematicAfter)
    {
        var glide = box.GetComponent<BoxGlide>();
        if (glide == null) glide = box.AddComponent<BoxGlide>();
        if (glide.running != null) glide.StopCoroutine(glide.running);
        box.transform.SetParent(parent, true);
        if (!glide.isActiveAndEnabled)
        {
            Place(box, parent, position, rotation, kinematicAfter);
            return;
        }
        glide.running = glide.StartCoroutine(glide.Glide(parent, position, rotation, kinematicAfter));
    }

    IEnumerator Glide(Transform parent, Vector3 position, Quaternion rotation, bool kinematicAfter)
    {
        var body = GetComponent<Rigidbody>();
        var grab = GetComponent<XRGrabInteractable>();
        if (body != null)
        {
            if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
            body.isKinematic = true;
        }
        var fromPosition = transform.localPosition;
        var fromRotation = transform.localRotation;
        var up = parent != null ? parent.InverseTransformDirection(Vector3.up) : Vector3.up;
        for (float t = 0f; t < 1f; t += Time.deltaTime / Seconds)
        {
            if (grab != null && grab.isSelected) { running = null; yield break; } // picked up mid-glide
            float e = t * t * (3f - 2f * t);
            transform.localPosition = Vector3.Lerp(fromPosition, position, e) + up * (Lift * 4f * e * (1f - e));
            transform.localRotation = Quaternion.Slerp(fromRotation, rotation, e);
            yield return null;
        }
        Place(gameObject, parent, position, rotation, kinematicAfter);
        running = null;
    }

    static void Place(GameObject box, Transform parent, Vector3 position, Quaternion rotation, bool kinematicAfter)
    {
        box.transform.SetLocalPositionAndRotation(position, rotation);
        var body = box.GetComponent<Rigidbody>();
        if (body == null) return;
        body.isKinematic = kinematicAfter;
        if (!kinematicAfter) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
    }
}
