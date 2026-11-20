using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class HouseFootstepSurface : MonoBehaviour
{
    [SerializeField] private AudioClip[] clips;
    public AudioClip[] Clips => clips;
    public void Configure(AudioClip[] woodClips) => clips = woodClips;
}
