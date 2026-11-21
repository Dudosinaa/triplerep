using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public sealed class TrainCarriageZone : MonoBehaviour
{
    [SerializeField] private CharacterController player;

    [Tooltip("Solid seat colliders that the enemy may keep firing at after seeing the player.")]
    [SerializeField] private Collider[] seatCover = System.Array.Empty<Collider>();

    private BoxCollider volume;

    public CharacterController Player => player;
    public bool PlayerInside => player != null && player.enabled && player.gameObject.activeInHierarchy &&
        Contains(player.transform.TransformPoint(player.center));

    private void Awake() => volume = GetComponent<BoxCollider>();

    private void Reset() => GetComponent<BoxCollider>().isTrigger = true;

    public bool Contains(Vector3 position)
    {
        if (volume == null) volume = GetComponent<BoxCollider>();
        Vector3 point = transform.InverseTransformPoint(position) - volume.center;
        Vector3 half = volume.size * 0.5f;
        return Mathf.Abs(point.x) < half.x && Mathf.Abs(point.y) < half.y && Mathf.Abs(point.z) < half.z;
    }

    public bool IsSeatCover(Collider obstacle) => System.Array.IndexOf(seatCover, obstacle) >= 0;
}
