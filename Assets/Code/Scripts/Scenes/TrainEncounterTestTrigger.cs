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
        if (encounter == null || !zone.PlayerInside || LocationTransition.IsTransitioning || Time.timeScale == 0f) return;
        encounter.EnableHostileNpcs();
        enabled = false;
    }
}
