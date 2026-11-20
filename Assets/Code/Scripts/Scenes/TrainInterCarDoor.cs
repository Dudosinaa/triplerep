using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(MeshRenderer))]
public sealed class TrainInterCarDoor : MonoBehaviour
{
    [SerializeField] private FirstPersonPlayer player;
    [SerializeField, Min(0.1f)] private float activationRadius = 1.8f;
    [SerializeField, Min(0.01f)] private float openDuration = 0.85f;
    [SerializeField, Min(0f)] private float closeDelay = 2f;
    [SerializeField] private Vector3 openOffset;
    [SerializeField] private Transform[] doorDetails = System.Array.Empty<Transform>();

    private Vector3 closedCenter;
    private Vector3 closedPosition;
    private Vector3[] closedDetailPositions;
    private bool wantsOpen;
    private float closeDelayRemaining;
    private float openProgress;

    private void Awake()
    {
        closedCenter = GetComponent<MeshRenderer>().bounds.center;
        closedPosition = transform.localPosition;
        closedDetailPositions = new Vector3[doorDetails.Length];
        for (int i = 0; i < doorDetails.Length; i++)
            closedDetailPositions[i] = doorDetails[i].localPosition;
    }

    private void Update()
    {
        if (IsPlayerNearby())
        {
            wantsOpen = true;
            closeDelayRemaining = closeDelay;
        }
        else if (wantsOpen && openProgress >= 1f)
        {
            closeDelayRemaining -= Time.deltaTime;
            wantsOpen = closeDelayRemaining > 0f;
        }

        float target = wantsOpen ? 1f : 0f;
        if (openProgress == target) return;

        // Keep progress while the Train roots are suspended during a memory visit.
        openProgress = Mathf.MoveTowards(openProgress, target, Time.deltaTime / Mathf.Max(0.01f, openDuration));
        Vector3 movement = openOffset * Mathf.SmoothStep(0f, 1f, openProgress);
        transform.localPosition = closedPosition + movement;
        for (int i = 0; i < doorDetails.Length; i++)
            doorDetails[i].localPosition = closedDetailPositions[i] + movement;
    }

    private bool IsPlayerNearby()
    {
        if (player == null || !player.gameObject.activeInHierarchy) return false;

        // Measure horizontally so approaching from either carriage uses the same radius.
        Vector3 offset = player.transform.position - closedCenter;
        offset.y = 0f;
        return offset.sqrMagnitude <= activationRadius * activationRadius;
    }
}
