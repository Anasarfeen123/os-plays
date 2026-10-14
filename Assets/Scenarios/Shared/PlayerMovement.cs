using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    public float walkSpeed = 5f;
    public float lookSensitivity = 2f;
    public float gravity = -9.81f;
    public float jumpHeight = 1.2f;

    [SerializeField] private Transform cameraTransform;
    
    private CharacterController controller;
    private Vector3 velocity;
    private float cameraPitch = 0f;
    private bool controlActive = true;

    /// <summary>Walk input from touch controls this frame (x strafe, y forward), added to the keys.</summary>
    [System.NonSerialized] public Vector2 ExternalMove;

    /// <summary>Turn the view by degrees (touch drag): yaw turns the body, pitch tilts the camera.</summary>
    public void AddLook(float yawDegrees, float pitchDegrees)
    {
        cameraPitch = Mathf.Clamp(cameraPitch - pitchDegrees, -89f, 89f);
        transform.Rotate(Vector3.up * yawDegrees);
    }

    void Start()
    {
        controller = GetComponent<CharacterController>();
        SetControlActive(true);
        
        if (cameraTransform == null)
        {
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null) cameraTransform = cam.transform;
        }
    }

    void Update()
    {
        if (!controlActive) return;

        Look();
        Move();
    }

    public void SetControlActive(bool active)
    {
        controlActive = active;
        if (active)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void Look()
    {
        if (cameraTransform == null) return;

        float mouseX = 0f;
        float mouseY = 0f;

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            Vector2 delta = Mouse.current.delta.ReadValue() * (lookSensitivity * 0.1f);
            mouseX = delta.x;
            mouseY = delta.y;
        }
#else
        mouseX = Input.GetAxis("Mouse X") * lookSensitivity;
        mouseY = Input.GetAxis("Mouse Y") * lookSensitivity;
#endif

        cameraPitch -= mouseY;
        cameraPitch = Mathf.Clamp(cameraPitch, -89f, 89f);

        cameraTransform.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
    }

    private void Move()
    {
        bool isGrounded = controller.isGrounded;
        
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        float moveX = 0f;
        float moveZ = 0f;
        bool jumpPressed = false;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) moveZ += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) moveZ -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) moveX += 1f;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) moveX -= 1f;
            jumpPressed = Keyboard.current.spaceKey.wasPressedThisFrame;
        }
#else
        moveX = Input.GetAxis("Horizontal");
        moveZ = Input.GetAxis("Vertical");
        jumpPressed = Input.GetButtonDown("Jump");
#endif

        moveX += ExternalMove.x;
        moveZ += ExternalMove.y;
        Vector3 move = transform.right * moveX + transform.forward * moveZ;
        if (move.sqrMagnitude > 1f) move.Normalize();
        controller.Move(move * walkSpeed * Time.deltaTime);

        if (jumpPressed && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}

