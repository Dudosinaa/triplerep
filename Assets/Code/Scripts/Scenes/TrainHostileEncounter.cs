using UnityEngine;

[DisallowMultipleComponent]
public sealed class TrainHostileEncounter : MonoBehaviour
{
    [Tooltip("Parent the hostile NPCs under this initially inactive GameObject.")]
    [SerializeField] private GameObject enemies;

    private bool enabledOnce;

    public void EnableHostileNpcs()
    {
        if (enabledOnce || enemies == null) return;
        enabledOnce = true;
        enemies.SetActive(true);
    }
}
