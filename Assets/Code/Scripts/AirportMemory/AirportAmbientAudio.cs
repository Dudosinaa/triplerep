using System.Collections;
using UnityEngine;

public sealed class AirportAmbientAudio : MonoBehaviour
{
    [SerializeField] private AudioClip cargoAmbience;
    [SerializeField] private AudioClip announcement;

    public void Configure(AudioClip ambience, AudioClip voice)
    {
        cargoAmbience = ambience;
        announcement = voice;
    }

    private void Start()
    {
        if (cargoAmbience != null)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.clip = cargoAmbience;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = .48f;
            source.Play();
        }
        if (announcement != null) StartCoroutine(PlayAnnouncements());
    }

    private IEnumerator PlayAnnouncements()
    {
        var source = gameObject.AddComponent<AudioSource>();
        source.clip = announcement;
        source.spatialBlend = 0f;
        source.volume = .55f;
        while (true)
        {
            yield return new WaitForSeconds(8f);
            source.Play();
            yield return new WaitForSeconds(announcement.length + 32f);
        }
    }
}
