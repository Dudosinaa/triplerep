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

    [System.Serializable]
    private sealed class PlatformDoorPart
    {
        [SerializeField] private MeshFilter original;
        [SerializeField] private Mesh platformMesh;
        [SerializeField] private Mesh oppositeMesh;

        public Transform SeparateSides()
        {
            var opposite = Instantiate(original.gameObject, original.transform.parent, false);
            opposite.name = original.name + " - Opposite Side";
            ApplyMesh(opposite.GetComponent<MeshFilter>(), oppositeMesh);
            ApplyMesh(original, platformMesh);
            return original.transform;
        }

        private static void ApplyMesh(MeshFilter filter, Mesh mesh)
        {
            filter.sharedMesh = mesh;
            if (filter.TryGetComponent(out MeshCollider collider))
                collider.sharedMesh = mesh;
        }
    }

    [Header("Platform doors")]
    [SerializeField, Min(0f)] private float doorDelay = 2f;
    [SerializeField, Min(0f)] private float doorDuration = 0.85f;
    [SerializeField, Min(0f)] private float doorTravel = 0.85f;
    [SerializeField] private PlatformDoorPart[] forwardDoors = System.Array.Empty<PlatformDoorPart>();
    [SerializeField] private PlatformDoorPart[] rearwardDoors = System.Array.Empty<PlatformDoorPart>();

    private IEnumerator Start()
    {
        var forwardLeaves = PrepareLeaves(forwardDoors);
        var rearwardLeaves = PrepareLeaves(rearwardDoors);
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

        yield return new WaitForSeconds(doorDelay);
        var forward = GetClosedPositions(forwardLeaves);
        var rearward = GetClosedPositions(rearwardLeaves);
        Vector3 travel = transform.forward * doorTravel;
        double openStart = Time.timeAsDouble;
        while (Time.timeAsDouble < openStart + doorDuration)
        {
            float progress = (float)((Time.timeAsDouble - openStart) / doorDuration);
            Vector3 offset = travel * Mathf.SmoothStep(0f, 1f, progress);
            MoveLeaves(forwardLeaves, forward, offset);
            MoveLeaves(rearwardLeaves, rearward, -offset);
            yield return null;
        }

        MoveLeaves(forwardLeaves, forward, travel);
        MoveLeaves(rearwardLeaves, rearward, -travel);
    }

    private static Transform[] PrepareLeaves(PlatformDoorPart[] parts)
    {
        var leaves = new Transform[parts.Length];
        for (int i = 0; i < parts.Length; i++)
            leaves[i] = parts[i].SeparateSides();
        return leaves;
    }

    private static Vector3[] GetClosedPositions(Transform[] leaves)
    {
        var positions = new Vector3[leaves.Length];
        for (int i = 0; i < leaves.Length; i++)
            positions[i] = leaves[i].localPosition;
        return positions;
    }

    private static void MoveLeaves(Transform[] leaves, Vector3[] closedPositions, Vector3 worldOffset)
    {
        for (int i = 0; i < leaves.Length; i++)
        {
            Transform leaf = leaves[i];
            leaf.localPosition = closedPositions[i] + leaf.parent.InverseTransformVector(worldOffset);
        }
    }
}
