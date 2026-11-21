using UnityEngine;

// Temporary stand-in for the story event with Ivan.
[DisallowMultipleComponent]
[RequireComponent(typeof(TrainCarriageZone))]
public sealed class TrainEncounterTestTrigger : MonoBehaviour
{
    [SerializeField] private TrainHostileEncounter encounter;
    private TrainCarriageZone zone;

    private void Awake() => zone = GetComponent<TrainCarriageZone>();

    private void Update()
    {
        if (encounter == null || !zone.PlayerInside || LocationTransition.IsTransitioning) return;
        encounter.EnableHostileNpcs();
        enabled = false;
    }
}
