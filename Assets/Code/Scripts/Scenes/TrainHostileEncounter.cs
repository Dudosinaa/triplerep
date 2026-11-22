using UnityEngine;

[DisallowMultipleComponent]
public sealed class TrainHostileEncounter : MonoBehaviour
{
    [Tooltip("Parent the hostile NPCs under this initially inactive GameObject.")]
    [SerializeField] private GameObject enemies;

    private bool enabledOnce;
    internal bool IsActivated => enabledOnce;
    internal event System.Action Activated;

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
}
