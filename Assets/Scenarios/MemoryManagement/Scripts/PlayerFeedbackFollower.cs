
using UnityEngine;

public class PlayerFeedbackFollower : MonoBehaviour
{
    [SerializeField] private Transform playerCamera;
    [SerializeField] private float followSpeed = 10f;

    // Slightly to the player's right, below eye level,
    // and far enough away to avoid blocking the main view.
    [SerializeField]
    private Vector3 localOffset =
        new Vector3(0.65f, -0.15f, 1.5f);

    private WelcomeSequenceController welcome;
    private bool hidden;

    private Canvas[] canvases;
    private Renderer[] renderers;
    private Vector3 baseScale;
    private bool ar;

    private void Start()
    {
        welcome = FindAnyObjectByType<WelcomeSequenceController>();
        canvases = GetComponentsInChildren<Canvas>(true);
        renderers = GetComponentsInChildren<Renderer>(true);
        baseScale = transform.localScale;
        var mode = Gridlock.XR.XRModeController.Current;
        ar = mode != null && mode.ARActive;
    }

    /// <summary>GRIDLOCK: the card floats where the welcome panel's Continue button is, so it waits until that panel closes.</summary>
    private void ShowCard(bool show)
    {
        if (hidden == !show || canvases == null) return;
        hidden = !show;
        foreach (var c in canvases) if (c != null) c.enabled = show;
        foreach (var r in renderers) if (r != null) r.enabled = show;
    }

    private void LateUpdate()
    {
        ShowCard(welcome == null || !welcome.WelcomeShowing);

        // GRIDLOCK: follow whichever view is live (the headset camera, the desktop player's camera, or the AR phone).
        var live = playerCamera != null ? playerCamera.GetComponent<Camera>() : null;
        if ((live == null || !live.isActiveAndEnabled) && Camera.main != null)
            playerCamera = Camera.main.transform;
        if (playerCamera == null)
            return;

        // Phone AR: the AR camera's rig is scaled up to the model's size, and the card stays 30 % as far (and as big) as
        // in a headset, so it floats between the phone and the model instead of inside it. Quest mixed reality scales the
        // rig the same way; there the card keeps its headset distance.
        float k = ar ? playerCamera.lossyScale.x * 0.3f : Gridlock.XR.QuestMixedReality.WorldScale; // Quest mixed reality: same real size
        transform.localScale = baseScale * k;
        Vector3 targetPosition =
            playerCamera.position
            + (playerCamera.right * localOffset.x
            + playerCamera.up * localOffset.y
            + playerCamera.forward * localOffset.z) * k;

        transform.position = Vector3.Lerp(
            transform.position,
            targetPosition,
            followSpeed * Time.deltaTime
        );

        Vector3 direction = transform.position - playerCamera.position;

        if (direction.sqrMagnitude > 0.001f)
        {
            transform.rotation =
                Quaternion.LookRotation(direction, Vector3.up);
        }
    }
}
