using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public sealed class DoorKey : MonoBehaviour
{
    [SerializeField] private PhoneCall requiredCall;
    [SerializeField] private AudioClip pickupClip;

    internal AudioClip PickupClip => pickupClip;
    internal bool CanPickUp => isActiveAndEnabled && (requiredCall == null || requiredCall.IsCompleted);
}
