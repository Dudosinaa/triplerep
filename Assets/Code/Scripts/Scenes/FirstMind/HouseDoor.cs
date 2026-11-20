using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public sealed class HouseDoor : MonoBehaviour
{
    [SerializeField] private Transform pivot;
    [SerializeField] private BoxCollider leaf;
    [SerializeField] private float openAngle;
    [SerializeField] private AudioClip opening;
    [SerializeField] private AudioClip closing;
    [SerializeField] private AudioClip latch;
    private readonly Collider[] overlaps = new Collider[16];
    private AudioSource sound;
    private float progress;
    public bool WantsOpen { get; private set; }
    public bool Open => progress >= 1f;
    public bool Shut => progress <= 0f;

    public void Configure(Transform hinge, BoxCollider panel, float angle, AudioClip openClip, AudioClip closeClip, AudioClip latchClip)
    {
        pivot = hinge;
        leaf = panel;
        openAngle = angle;
        opening = openClip;
        closing = closeClip;
        latch = latchClip;
    }

    private void Awake() => sound = GetComponent<AudioSource>();

    public void Toggle()
    {
        WantsOpen = !WantsOpen;
        sound.clip = WantsOpen ? opening : closing;
        sound.Play();
    }

    private void FixedUpdate()
    {
        float target = WantsOpen ? 1f : 0f;
        if (progress == target) return;

        float next = Mathf.MoveTowards(progress, target, Time.fixedDeltaTime / 0.90f);
        Quaternion rotation = Quaternion.Euler(0f, openAngle * Mathf.SmoothStep(0f, 1f, next), 0f);
        Quaternion worldRotation = transform.rotation * rotation;
        Vector3 center = pivot.position + worldRotation * leaf.center;
        int count = Physics.OverlapBoxNonAlloc(center, leaf.size * 0.5f + Vector3.one * 0.025f,
            overlaps, worldRotation, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
            if (overlaps[i] is CharacterController) return;

        pivot.localRotation = rotation;
        progress = next;
        if (!WantsOpen && Shut) sound.PlayOneShot(latch);
    }
}
