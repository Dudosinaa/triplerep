using UnityEngine;

[DisallowMultipleComponent]
public sealed class KeyDoor : MonoBehaviour
{
    [SerializeField, InspectorName("Открыта при запуске")]
    private bool startsOpen;
    [SerializeField, Tooltip("Укажите экземпляр ключа из этой сцены.")]
    private DoorKey requiredKey;
    [SerializeField, Tooltip("Угол в градусах вокруг вертикальной оси петель. Знак задаёт направление.")]
    private float openAngle = 90f;
    [SerializeField, Min(0.01f), Tooltip("Длительность открытия в секундах.")]
    private float openDuration = 0.8f;

    private Quaternion closedRotation;
    private bool openingStarted;
    private float progress;

    private void Awake()
    {
        closedRotation = transform.localRotation;
        if (!startsOpen) return;

        openingStarted = true;
        progress = 1f;
        SetRotation(1f);
    }

    internal bool CanOpen(FirstPersonPlayer player) =>
        !openingStarted && player != null && player.HasKey(requiredKey);

    public void TryOpen(FirstPersonPlayer player)
    {
        if (CanOpen(player)) openingStarted = true;
    }

    private void Update()
    {
        if (!openingStarted || progress >= 1f) return;
        progress = Mathf.MoveTowards(progress, 1f, Time.deltaTime / Mathf.Max(0.01f, openDuration));
        SetRotation(Mathf.SmoothStep(0f, 1f, progress));
    }

    private void SetRotation(float fraction)
    {
        // The door root is at the hinge. The panel and its collider are offset children.
        transform.localRotation = closedRotation * Quaternion.AngleAxis(openAngle * fraction, Vector3.up);
    }
}
