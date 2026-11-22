using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(ParticleSystem))]
public sealed class NpcMuzzleFlash : MonoBehaviour
{
    private ParticleSystem particles;
    private ParticleSystem[] emitters;
    private Light flashLight;

    private void Awake()
    {
        particles = GetComponent<ParticleSystem>();
        flashLight = GetComponentInChildren<Light>(true);
        emitters = GetComponentsInChildren<ParticleSystem>(true);
        foreach (var system in emitters)
        {
            var main = system.main;
            main.loop = false;
            main.playOnAwake = false;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emission = system.emission;
            emission.enabled = false;
        }
        Stop();
    }

    internal void Play()
    {
        Stop();
        foreach (var system in emitters)
        {
            system.Play(false);
            system.Emit(1);
        }
        if (flashLight != null)
        {
            flashLight.enabled = true;
            Invoke(nameof(TurnOffLight), 0.05f);
        }
    }

    internal void Stop()
    {
        if (particles != null)
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        CancelInvoke(nameof(TurnOffLight));
        TurnOffLight();
    }

    private void OnDisable() => Stop();

    private void TurnOffLight()
    {
        if (flashLight != null) flashLight.enabled = false;
    }
}
