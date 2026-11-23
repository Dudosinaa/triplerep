using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(FirstPersonPlayer))]
public sealed class HouseDoorInteraction : MonoBehaviour
{
    public const string OpenText = "E — Открыть дверь";
    public const string CloseText = "E — Закрыть дверь";

    [SerializeField] private Camera view;
    [SerializeField] private TMP_Text label;
    [SerializeField] private CanvasGroup hint;
    private readonly RaycastHit[] hits = new RaycastHit[24];
    private FirstPersonPlayer player;

    public void Configure(Camera camera, TMP_Text text, CanvasGroup group)
    {
        view = camera;
        label = text;
        hint = group;
    }

    private void Awake() => player = GetComponent<FirstPersonPlayer>();
    private void OnDisable()
    {
        if (hint != null) hint.alpha = 0f;
    }

    private void LateUpdate()
    {
        if (GameplayPause.BlocksGameplayInput) return;
        HouseDoor door = player.isActiveAndEnabled && view.isActiveAndEnabled ? FindDoor() : null;
        hint.alpha = door != null ? 1f : 0f;
        if (door == null) return;

        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame && InteractionInput.TryConsume())
            door.Toggle();
        label.text = door.WantsOpen ? CloseText : OpenText;
    }

    private HouseDoor FindDoor()
    {
        int count = Physics.RaycastNonAlloc(view.transform.position, view.transform.forward,
            hits, 2f, ~0, QueryTriggerInteraction.Collide);
        float closest = float.PositiveInfinity;
        HouseDoor target = null;
        for (int i = 0; i < count; i++)
        {
            Collider collider = hits[i].collider;
            if (hits[i].distance >= closest || collider.transform.IsChildOf(transform)) continue;
            HouseDoor door = collider.GetComponentInParent<HouseDoor>();
            if (collider.isTrigger && door == null) continue;
            closest = hits[i].distance;
            target = door;
        }
        return target;
    }
}
