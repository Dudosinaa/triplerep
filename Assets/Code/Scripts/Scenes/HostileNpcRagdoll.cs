using UnityEngine;

[DisallowMultipleComponent]
public sealed class HostileNpcRagdoll : MonoBehaviour
{
    private const float FallDuration = 2f;

    [SerializeField] private Rigidbody[] bones;
    [SerializeField] private Collider[] boneColliders;

    private bool falling;
    private float remainingFallTime;
    private (Vector3 Position, Quaternion Rotation)[] restPose;

    private void Awake()
    {
        restPose = new (Vector3, Quaternion)[bones.Length];
        for (int i = 0; i < bones.Length; i++)
        {
            Rigidbody bone = bones[i];
            bone.detectCollisions = false;
            restPose[i] = (bone.transform.localPosition, bone.transform.localRotation);
        }
    }

    internal void BeginFall()
    {
        Physics.SyncTransforms();
        foreach (Rigidbody bone in bones)
        {
            bone.isKinematic = false;
            bone.detectCollisions = true;
        }
        foreach (Collider boneCollider in boneColliders)
            boneCollider.enabled = true;
        IgnoreSelfCollisions();

        remainingFallTime = FallDuration;
        falling = true;
    }

    private void OnEnable()
    {
        // Collision exclusions are lost when the Train scene roots are suspended.
        if (falling) IgnoreSelfCollisions();
    }

    private void FixedUpdate()
    {
        if (!falling) return;
        remainingFallTime -= Time.fixedDeltaTime;
        if (remainingFallTime > 0.0001f) return;
        Freeze();
    }

    internal void RestoreAnimatedPose()
    {
        Freeze();
        for (int i = 0; i < bones.Length; i++)
            bones[i].transform.SetLocalPositionAndRotation(restPose[i].Position, restPose[i].Rotation);
    }

    private void Freeze()
    {
        falling = false;
        foreach (Collider boneCollider in boneColliders)
            boneCollider.enabled = false;
        foreach (Rigidbody bone in bones)
        {
            if (!bone.isKinematic)
            {
                bone.linearVelocity = Vector3.zero;
                bone.angularVelocity = Vector3.zero;
                bone.isKinematic = true;
            }
            bone.detectCollisions = false;
        }
    }

    private void IgnoreSelfCollisions()
    {
        for (int i = 0; i < boneColliders.Length; i++)
            for (int j = i + 1; j < boneColliders.Length; j++)
                Physics.IgnoreCollision(boneColliders[i], boneColliders[j]);
    }
}
