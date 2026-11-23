using UnityEngine;

public sealed class ForestHouseBoundary : MonoBehaviour
{
    public const string WarningText = "Слишком далеко. Вернитесь к дому.";

    [SerializeField] private Transform player;
    [SerializeField] private BoxCollider houseFootprint;
    [SerializeField] private CanvasGroup warning;
    [SerializeField] private float showDistance = 19f;
    [SerializeField] private float hideDistance = 17f;
    [SerializeField] private float fadeSeconds = 0.38f;

    private bool showing;

    public void Configure(Transform target, BoxCollider footprint, CanvasGroup canvasGroup)
    {
        player = target;
        houseFootprint = footprint;
        warning = canvasGroup;
    }

    public float DistanceFromHouse(Vector3 worldPosition)
    {
        Vector3 offset = houseFootprint.transform.InverseTransformPoint(worldPosition) - houseFootprint.center;
        Vector3 half = houseFootprint.size * 0.5f;
        Vector3 scale = houseFootprint.transform.lossyScale;
        float x = Mathf.Max(0f, Mathf.Abs(offset.x) - half.x) * Mathf.Abs(scale.x);
        float z = Mathf.Max(0f, Mathf.Abs(offset.z) - half.z) * Mathf.Abs(scale.z);
        return Mathf.Sqrt(x * x + z * z);
    }

    private void LateUpdate()
    {
        float distance = DistanceFromHouse(player.position);
        showing = showing ? distance > hideDistance : distance >= showDistance;
        warning.alpha = Mathf.MoveTowards(warning.alpha, showing ? 1f : 0f, Time.deltaTime / fadeSeconds);
    }
}
