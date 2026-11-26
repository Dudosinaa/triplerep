using UnityEngine;

[DisallowMultipleComponent]
public sealed class KeyDoor : MonoBehaviour
{
    [SerializeField, InspectorName("Открыта при запуске")]
    private bool startsOpen;
    [SerializeField, InspectorName("Требуется ключ")]
    private bool requiresKey = true;
    [SerializeField, Tooltip("Если включено «Требуется ключ», укажите экземпляр ключа из этой сцены.")]
    private DoorKey requiredKey;
    [SerializeField, Tooltip("Угол в градусах вокруг вертикальной оси петель. Знак задаёт направление.")]
    private float openAngle = 90f;
    [SerializeField, Min(0.01f), Tooltip("Длительность полного открытия или закрытия в секундах.")]
    private float openDuration = 0.8f;
    [SerializeField, InspectorName("Звук открытия")]
    private AudioClip openingClip;
    [SerializeField, Min(0f), Tooltip("Начальная позиция в клипе открытия в секундах.")]
    private float openingSoundStartTime;

    private Quaternion closedRotation;
    private AudioSource openingSource;
    private bool targetOpen;
    private float progress;

    private void Awake()
    {
        closedRotation = transform.localRotation;
        if (openingClip != null)
        {
            openingSource = gameObject.AddComponent<AudioSource>();
            openingSource.playOnAwake = false;
            openingSource.spatialBlend = 1f;
            openingSource.clip = openingClip;
        }

        if (!startsOpen) return;

        targetOpen = true;
        progress = 1f;
        SetRotation(1f);
    }

    internal bool CanInteract(FirstPersonPlayer player) =>
        player != null && (targetOpen || !requiresKey || player.HasKey(requiredKey));

    public void TryOpen(FirstPersonPlayer player)
    {
        if (!targetOpen) TryToggle(player);
    }

    internal void TryToggle(FirstPersonPlayer player)
    {
        if (!CanInteract(player)) return;

        targetOpen = !targetOpen;
        if (targetOpen && openingSource != null)
        {
            openingSource.Stop();
            openingSource.timeSamples = Mathf.Clamp(
                Mathf.RoundToInt(openingSoundStartTime * openingClip.frequency), 0, openingClip.samples - 1);
            openingSource.Play();
        }
    }

    private void Update()
    {
        float target = targetOpen ? 1f : 0f;
        if (progress == target) return;
        progress = Mathf.MoveTowards(progress, target, Time.deltaTime / Mathf.Max(0.01f, openDuration));
        SetRotation(Mathf.SmoothStep(0f, 1f, progress));
    }

    private void SetRotation(float fraction)
    {
        // The door root is at the hinge. The panel and its collider are offset children.
        transform.localRotation = closedRotation * Quaternion.AngleAxis(openAngle * fraction, Vector3.up);
    }
}
