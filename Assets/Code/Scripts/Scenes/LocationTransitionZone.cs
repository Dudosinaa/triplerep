using UnityEngine;

// A scene-authored volume. It never creates, moves or persists a player.
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider), typeof(Rigidbody))]
public sealed class LocationTransitionZone : MonoBehaviour
{
    [SerializeField, Tooltip("Scene path from the active Build Profiles scene list.")]
    private string targetScenePath;
    [SerializeField, Min(0f)] private float fadeOutDuration = 0.65f;
    [SerializeField, Min(0f)] private float fadeInDuration = 0.65f;
    [SerializeField, Tooltip("Optional Train exit condition. The player can leave after Ivan finishes the chase.")]
    private TrainIvanEncounter waitForIvan;

    public string TargetScenePath => targetScenePath;

    private void Reset()
    {
        GetComponent<BoxCollider>().isTrigger = true;
        var body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
    }

    private void OnTriggerEnter(Collider other) => TryBeginTransition(other);

    private void OnTriggerStay(Collider other)
    {
        // A player already inside the Train exit can leave as soon as Ivan arrives.
        if (waitForIvan != null) TryBeginTransition(other);
    }

    private void TryBeginTransition(Collider other)
    {
        if (!isActiveAndEnabled || !other) return;
        if (waitForIvan != null && !waitForIvan.HasReachedExit) return;
        var player = other.GetComponentInParent<FirstPersonPlayer>();
        // In particular, do not take control away from SceneSwitch's mind-entry animation.
        if (player == null || !player.isActiveAndEnabled) return;
        LocationTransition.TryBegin(player, targetScenePath, fadeOutDuration, fadeInDuration, this);
    }
}
