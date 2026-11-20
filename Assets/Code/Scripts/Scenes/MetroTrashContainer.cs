using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator), typeof(AudioSource))]
public sealed class MetroTrashContainer : MonoBehaviour
{
    [SerializeField] private Collider passageBlocker;
    [SerializeField] private AudioClip activationSound;

    private void OnEnable()
    {
        if (passageBlocker != null) passageBlocker.enabled = true;
        GetComponent<Animator>().Play("Activate", 0, 0f);
        if (activationSound != null) GetComponent<AudioSource>().PlayOneShot(activationSound);
    }
}
