using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider), typeof(Rigidbody))]
public sealed class ActivationZone : MonoBehaviour
{
    [SerializeField] private GameObject target;

    private void Reset() => ConfigureTrigger();
    private void Awake() => ConfigureTrigger();

    private void ConfigureTrigger()
    {
        GetComponent<BoxCollider>().isTrigger = true;
        var body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isActiveAndEnabled || target == null) return;
        if (other.GetComponentInParent<FirstPersonPlayer>() != null)
            target.SetActive(true);
    }
}
