using UnityEngine;

// Keeps world-space labels upright and readable as the player walks around them.
public sealed class FacePlayerCamera : MonoBehaviour
{
    [SerializeField, Min(0f)] private float maxVisibleDistance = 4f;
    private Transform playerCamera;
    private Renderer labelRenderer;

    private void Awake()
    {
        labelRenderer = GetComponent<Renderer>();
    }

    private void LateUpdate()
    {
        if (playerCamera == null)
        {
            Camera camera = Camera.main;
            if (camera == null) return;
            playerCamera = camera.transform;
        }

        Vector3 direction = playerCamera.position - transform.position;
        if (labelRenderer != null)
            labelRenderer.enabled = direction.sqrMagnitude <= maxVisibleDistance * maxVisibleDistance;

        direction.y = 0f;
        if (direction.sqrMagnitude > .0001f)
            // TextMesh renders its front on the negative Z side.
            transform.rotation = Quaternion.LookRotation(-direction, Vector3.up);
    }
}
