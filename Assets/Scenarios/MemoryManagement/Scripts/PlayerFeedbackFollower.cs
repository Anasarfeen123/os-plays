
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

    private void Start()
    {
        welcome = FindAnyObjectByType<WelcomeSequenceController>();
        canvases = GetComponentsInChildren<Canvas>(true);
        renderers = GetComponentsInChildren<Renderer>(true);
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

        // GRIDLOCK: follow whichever view is live (the headset camera, or the desktop player's camera).
        if ((playerCamera == null || !playerCamera.gameObject.activeInHierarchy) && Camera.main != null)
            playerCamera = Camera.main.transform;
        if (playerCamera == null)
            return;

        Vector3 targetPosition =
            playerCamera.position
            + playerCamera.right * localOffset.x
            + playerCamera.up * localOffset.y
            + playerCamera.forward * localOffset.z;

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
