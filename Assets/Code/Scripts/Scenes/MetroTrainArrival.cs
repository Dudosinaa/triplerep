using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MetroTrainArrival : MonoBehaviour
{
    private enum MovementAxis
    {
        X,
        Y,
        Z
    }

    [SerializeField, Tooltip("World axis along which the train moves.")]
    private MovementAxis axis = MovementAxis.Z;
    [SerializeField, Tooltip("Signed travel distance in world units.")]
    private float distance = -160f;
    [SerializeField, Min(0f)] private float startDelay = 3f;
    [SerializeField, Min(0f)] private float moveDuration = 14f;
    [SerializeField] private GameObject block;

    private IEnumerator Start()
    {
        double moveStart = Time.timeAsDouble + Mathf.Max(0f, startDelay);
        float duration = Mathf.Max(0f, moveDuration);
        Vector3 startPosition = transform.position;
        Vector3 direction = axis switch
        {
            MovementAxis.X => Vector3.right,
            MovementAxis.Y => Vector3.up,
            _ => Vector3.forward
        };
        Vector3 endPosition = startPosition + direction * distance;

        while (Time.timeAsDouble < moveStart)
            yield return null;

        while (Time.timeAsDouble < moveStart + duration)
        {
            float progress = (float)((Time.timeAsDouble - moveStart) / duration);
            transform.position = Vector3.Lerp(startPosition, endPosition, Mathf.SmoothStep(0f, 1f, progress));
            yield return null;
        }

        transform.position = endPosition;
        if (block != null)
            block.SetActive(false);
    }
}
