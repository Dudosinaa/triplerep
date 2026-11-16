using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    public float speed = 4f;
    public float mouseSensitivity = 2f;
    public Transform playerCamera;

    private CharacterController controller;
    private float verticalRotation = 0f;
    private float gravity = -9.81f;
    private float verticalSpeed = 0f;

    void Start()
    {
        controller = GetComponent<CharacterController>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (Keyboard.current == null || Mouse.current == null || playerCamera == null) return;
        Move();
        LookAround();
    }

    void Move()
    {
        float x = ((Keyboard.current.dKey.isPressed ? 1f : 0f) - (Keyboard.current.aKey.isPressed ? 1f : 0f));
        float z = ((Keyboard.current.wKey.isPressed ? 1f : 0f) - (Keyboard.current.sKey.isPressed ? 1f : 0f));

        Vector3 direction = transform.right * x + transform.forward * z;

        direction = Vector3.ClampMagnitude(direction, 1f);

        if (controller.isGrounded && verticalSpeed < 0)
        {
            verticalSpeed = -2f;
        }

        verticalSpeed += gravity * Time.deltaTime;

        Vector3 movement = direction * speed;
        movement.y = verticalSpeed;

        controller.Move(movement * Time.deltaTime);
    }

    void LookAround()
    {
        float mouseX = Mouse.current.delta.ReadValue().x * mouseSensitivity * 0.06f;
        float mouseY = Mouse.current.delta.ReadValue().y * mouseSensitivity * 0.06f;

        transform.Rotate(0, mouseX, 0);

        verticalRotation -= mouseY;
        verticalRotation = Mathf.Clamp(verticalRotation, -80f, 80f);

        playerCamera.localRotation = Quaternion.Euler(verticalRotation, 0, 0);
    }
}