using UnityEngine;

/// <summary>
/// Lightweight billboard component to ensure 3D floating labels
/// and indicators always face the player's camera smoothly without distortion.
/// </summary>
public class Billboard3D : MonoBehaviour
{
    private Transform mainCameraTransform;

    private void Start()
    {
        if (Camera.main != null)
        {
            mainCameraTransform = Camera.main.transform;
        }
    }

    private void LateUpdate()
    {
        if (mainCameraTransform == null)
        {
            if (Camera.main != null) mainCameraTransform = Camera.main.transform;
            else return;
        }

        // Face towards the camera, keeping upright orientation
        Vector3 dir = transform.position - mainCameraTransform.position;
        if (dir.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(dir);
        }
    }
}
