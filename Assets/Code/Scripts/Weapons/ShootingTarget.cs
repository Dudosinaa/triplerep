using UnityEngine;

public sealed class ShootingTarget : MonoBehaviour
{
    [SerializeField] private Renderer targetRenderer;
    private Color originalColor;
    private float flashEnd;

    private void Awake()
    {
        if (targetRenderer == null) targetRenderer = GetComponent<Renderer>();
        if (targetRenderer != null) originalColor = targetRenderer.material.color;
    }

    public void Hit()
    {
        if (targetRenderer == null) return;
        targetRenderer.material.color = Color.white;
        flashEnd = Time.time + 0.25f;
    }

    private void Update()
    {
        if (flashEnd > 0f && Time.time >= flashEnd)
        {
            targetRenderer.material.color = originalColor;
            flashEnd = 0f;
        }
    }
}
