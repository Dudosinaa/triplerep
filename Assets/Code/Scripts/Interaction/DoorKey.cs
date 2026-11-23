using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public sealed class DoorKey : MonoBehaviour
{
    [SerializeField] private PhoneCall requiredCall;

    internal bool CanPickUp => isActiveAndEnabled && (requiredCall == null || requiredCall.IsCompleted);
}
