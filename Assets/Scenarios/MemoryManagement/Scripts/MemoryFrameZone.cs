
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class MemoryFrameZone : MonoBehaviour
{
    [SerializeField] private MemoryPlacementManager manager;
    [SerializeField, Range(0, 2)] private int frameIndex;

    private GameObject lastProcessedBox;

    private void OnTriggerStay(Collider other)
    {
        XRGrabInteractable grab = other.GetComponentInParent<XRGrabInteractable>();
        if (grab == null || grab.isSelected)
            return;

        GameObject box = grab.gameObject;

        if (box == lastProcessedBox)
            return;

        lastProcessedBox = box;

        if (manager != null)
            manager.TryPlaceBox(box, frameIndex);
        else
            Debug.LogError("MemoryPlacementManager is not assigned.", this);
    }

    private void OnTriggerExit(Collider other)
    {
        XRGrabInteractable grab = other.GetComponentInParent<XRGrabInteractable>();

        if (grab != null && grab.gameObject == lastProcessedBox)
            lastProcessedBox = null;
    }
}
