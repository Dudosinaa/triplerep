using UnityEngine;

[DisallowMultipleComponent]
public sealed class NpcShotHitbox : MonoBehaviour
{
    [SerializeField] private Transform bone;
    [SerializeField] private Vector3 offset;
    [SerializeField] private Quaternion rotation = Quaternion.identity;

    private void LateUpdate()
    {
        if (bone != null)
            transform.SetPositionAndRotation(bone.TransformPoint(offset), bone.rotation * rotation);
    }
}
