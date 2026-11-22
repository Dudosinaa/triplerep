using UnityEngine;

[DisallowMultipleComponent]
public sealed class TrainHostileEncounter : MonoBehaviour
{
    [Tooltip("Parent the hostile NPCs under this initially inactive GameObject.")]
    [SerializeField] private GameObject enemies;
    [SerializeField] private TrainCarriageZone pistolUnlockCarriage;

    private bool enabledOnce;
    private bool pistolUnlocked;
    internal bool IsActivated => enabledOnce;
    internal bool CanDrawPistol => enabledOnce && (pistolUnlockCarriage == null || pistolUnlocked);
    internal event System.Action Activated;
    internal event System.Action PistolUnlocked;

    internal HostileTrainNpc[] GetActiveEnemies() => enemies != null && enemies.activeInHierarchy
        ? enemies.GetComponentsInChildren<HostileTrainNpc>()
        : System.Array.Empty<HostileTrainNpc>();

    public void EnableHostileNpcs()
    {
        if (enabledOnce || enemies == null) return;
        enabledOnce = true;
        enemies.SetActive(true);
        Activated?.Invoke();
    }

    private void Update()
    {
        if (!enabledOnce || pistolUnlocked || pistolUnlockCarriage == null ||
            Time.timeScale <= 0f || LocationTransition.IsTransitioning || !pistolUnlockCarriage.PlayerInside)
            return;
        var player = pistolUnlockCarriage.Player.GetComponent<FirstPersonPlayer>();
        if (player == null || !player.isActiveAndEnabled) return;

        pistolUnlocked = true;
        PistolUnlocked?.Invoke();
    }
}
