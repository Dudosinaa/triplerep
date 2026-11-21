using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public sealed class FirstPersonPlayer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private PistolController pistol;
    [SerializeField] private AudioSource footstepSource;
    [SerializeField] private AudioClip[] footstepClips;

    [Header("Movement")]
    [SerializeField, Min(0.1f)] private float walkSpeed = 4f;
    [SerializeField, Min(0.1f)] private float crouchSpeed = 2f;
    [SerializeField] private float gravity = -20f;
    [SerializeField, Min(0.1f)] private float mouseSensitivity = 0.12f;

    [Header("Crouch")]
    [SerializeField, Min(0.5f)] private float standingHeight = 1.8f;
    [SerializeField, Min(0.5f)] private float crouchingHeight = 1.05f;
    [SerializeField, Min(0.1f)] private float crouchSmoothness = 10f;

    [Header("Camera zoom")]
    [SerializeField, Range(20f, 100f)] private float normalFieldOfView = 70f;
    [SerializeField, Range(20f, 100f)] private float zoomFieldOfView = 45f;
    [SerializeField, Min(0.1f)] private float zoomSmoothness = 10f;

    [Header("Footsteps")]
    [SerializeField, Min(0.1f)] private float walkingStepDistance = 1.8f;
    [SerializeField, Min(0.1f)] private float crouchingStepDistance = 2.3f;

    internal const float KeyInteractionDistance = 3f;
    internal Camera PlayerCamera => playerCamera;

    private InteractionHints interactionHints;
    private AudioClip generatedStep;
    private CharacterController controller;
    private float verticalSpeed;
    private float cameraPitch;
    private float distanceSinceStep;
    private int lastClipIndex = -1;
    private readonly Collider[] standingClearanceHits = new Collider[16];
    private readonly RaycastHit[] keyInteractionHits = new RaycastHit[8];
    private readonly HashSet<DoorKey> collectedKeys = new HashSet<DoorKey>();

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        interactionHints = GetComponent<InteractionHints>();
        if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>();
        if (footstepSource == null) footstepSource = GetComponent<AudioSource>();
        if (pistol == null) pistol = GetComponent<PistolController>();

        if (footstepSource != null && (footstepClips == null || footstepClips.Length == 0))
        {
            var samples = new float[3528];
            var noise = new System.Random(27);
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / 44100f;
                samples[i] = ((float)noise.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 65f) * 0.12f;
            }
            generatedStep = AudioClip.Create("Procedural footsteps", samples.Length, 1, 44100, false);
            generatedStep.SetData(samples, 0);
            footstepClips = new[] { generatedStep };
        }
        controller.height = standingHeight;
        controller.center = Vector3.up * standingHeight * 0.5f;
        if (playerCamera != null) playerCamera.fieldOfView = normalFieldOfView;
    }

    private void OnEnable()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnDisable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Teleport(Vector3 position)
    {
        bool wasEnabled = controller.enabled;
        controller.enabled = false;
        transform.position = position;
        verticalSpeed = 0f;
        distanceSinceStep = 0f;
        controller.enabled = wasEnabled;
    }

    private void Update()
    {
        if (Keyboard.current == null || Mouse.current == null || playerCamera == null) return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        if (Mouse.current.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            return;
        }

        bool crouching = Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed;
        if (!crouching && !CanStand()) crouching = true;

        UpdateCrouch(crouching);
        UpdateView();
        UpdateMovement(crouching);
        UpdateZoom();
    }

    // Reading, inspection and dialogue get first use of E in Update.
    private void LateUpdate()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null || playerCamera == null || !playerCamera.isActiveAndEnabled) return;

        bool pressedE = keyboard.eKey.wasPressedThisFrame;
        if (!pressedE && (interactionHints == null || !interactionHints.CanShow)) return;
        if (!TryGetKeyInteractionHit(out var hit)) return;

        var key = hit.collider.GetComponentInParent<DoorKey>();
        if (key != null && key.isActiveAndEnabled)
        {
            interactionHints?.OfferFocus(key);
            if (pressedE && InteractionInput.TryConsume())
            {
                collectedKeys.Add(key);
                key.gameObject.SetActive(false);
            }
            return;
        }

        var door = hit.collider.GetComponentInParent<KeyDoor>();
        if (door == null || !door.isActiveAndEnabled) return;
        if (door.CanOpen(this)) interactionHints?.OfferFocus(door);
        if (pressedE && InteractionInput.TryConsume()) door.TryOpen(this);
    }

    private bool TryGetKeyInteractionHit(out RaycastHit hit)
    {
        var view = playerCamera.transform;
        var ray = new Ray(view.position, view.forward);
        if (!Physics.Raycast(ray, out hit, KeyInteractionDistance, ~0, QueryTriggerInteraction.Ignore))
            return false;
        if (!hit.collider.transform.IsChildOf(transform))
            return true;

        // Looking down at the key can hit the player's capsule before the key.
        RaycastHit[] hits = keyInteractionHits;
        int count = Physics.RaycastNonAlloc(ray, hits, KeyInteractionDistance, ~0, QueryTriggerInteraction.Ignore);
        if (count == hits.Length)
        {
            hits = Physics.RaycastAll(ray, KeyInteractionDistance, ~0, QueryTriggerInteraction.Ignore);
            count = hits.Length;
        }

        hit = default;
        float closestDistance = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            var candidate = hits[i];
            if (candidate.distance >= closestDistance || candidate.collider == null ||
                candidate.collider.transform.IsChildOf(transform))
                continue;

            hit = candidate;
            closestDistance = candidate.distance;
        }
        return hit.collider != null;
    }

    public bool HasKey(DoorKey key) => key != null && collectedKeys.Contains(key);

    private void UpdateView()
    {
        if (Cursor.lockState != CursorLockMode.Locked) return;

        Vector2 mouseDelta = Mouse.current.delta.ReadValue() * mouseSensitivity;
        transform.Rotate(Vector3.up * mouseDelta.x);
        cameraPitch = Mathf.Clamp(cameraPitch - mouseDelta.y, -85f, 85f);
        playerCamera.transform.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
    }

    private void UpdateMovement(bool crouching)
    {
        Vector2 input = new Vector2(
            (Keyboard.current.dKey.isPressed ? 1f : 0f) - (Keyboard.current.aKey.isPressed ? 1f : 0f),
            (Keyboard.current.wKey.isPressed ? 1f : 0f) - (Keyboard.current.sKey.isPressed ? 1f : 0f));
        input = Vector2.ClampMagnitude(input, 1f);

        Vector3 horizontalVelocity = (transform.right * input.x + transform.forward * input.y) *
                                     (crouching ? crouchSpeed : walkSpeed);
        if (controller.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;
        verticalSpeed += gravity * Time.deltaTime;

        CollisionFlags flags = controller.Move((horizontalVelocity + Vector3.up * verticalSpeed) * Time.deltaTime);
        if ((flags & CollisionFlags.Below) != 0 && verticalSpeed < 0f) verticalSpeed = -2f;

        if (controller.isGrounded && input.sqrMagnitude > 0.01f)
        {
            distanceSinceStep += new Vector2(controller.velocity.x, controller.velocity.z).magnitude * Time.deltaTime;
            float stepDistance = crouching ? crouchingStepDistance : walkingStepDistance;
            if (distanceSinceStep >= stepDistance)
            {
                distanceSinceStep -= stepDistance;
                PlayFootstep();
            }
        }
        else
        {
            distanceSinceStep = 0f;
        }
    }

    private void UpdateCrouch(bool crouching)
    {
        float targetHeight = crouching ? crouchingHeight : standingHeight;
        float height = Mathf.MoveTowards(controller.height, targetHeight, crouchSmoothness * Time.deltaTime);
        controller.height = height;
        controller.center = Vector3.up * height * 0.5f;

        Vector3 cameraPosition = playerCamera.transform.localPosition;
        cameraPosition.y = Mathf.MoveTowards(cameraPosition.y, height - 0.15f, crouchSmoothness * Time.deltaTime);
        playerCamera.transform.localPosition = cameraPosition;
    }

    private bool CanStand()
    {
        if (controller.height >= standingHeight - 0.01f) return true;
        float radius = controller.radius * 0.95f;
        Vector3 bottom = transform.position + Vector3.up * (radius + 0.02f);
        Vector3 top = transform.position + Vector3.up * (standingHeight - radius);
        int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, standingClearanceHits, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider hit = standingClearanceHits[i];
            if (hit != null && hit != controller && !hit.transform.IsChildOf(transform)) return false;
        }
        return true;
    }

    private void UpdateZoom()
    {
        bool zoom = Mouse.current.rightButton.isPressed && (pistol == null || !pistol.IsEquipped);
        float target = zoom ? zoomFieldOfView : normalFieldOfView;
        playerCamera.fieldOfView = Mathf.MoveTowards(playerCamera.fieldOfView, target, zoomSmoothness * 20f * Time.deltaTime);
    }

    private void OnDestroy()
    {
        if (generatedStep != null) Destroy(generatedStep);
    }

    private void PlayFootstep()
    {
        if (footstepSource == null || footstepClips == null || footstepClips.Length == 0) return;
        int index = Random.Range(0, footstepClips.Length);
        if (footstepClips.Length > 1 && index == lastClipIndex) index = (index + 1) % footstepClips.Length;
        if (footstepClips[index] == null) return;

        lastClipIndex = index;
        footstepSource.PlayOneShot(footstepClips[index]);
    }
}
