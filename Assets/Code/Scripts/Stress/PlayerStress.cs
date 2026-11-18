using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;

public class PlayerStress : MonoBehaviour
{
    [Header("Стресс")]
    [Range(0f, 100f)] public float stress = 0f;
    public float increaseSpeed = 10f;
    public float decreaseSpeed = 10f;

    [Header("Виньетка")]
    public Volume volume;
    [Range(0f, 1f)] public float maxVignetteIntensity = 0.45f;
    [Min(0.01f)] public float vignetteSmoothTime = 0.2f;

    [Header("Звук")]
    public AudioClip stressSound;
    [Range(0f, 100f)] public float soundThreshold = 50f;

    [SerializeField] private bool blendWithSceneProfile;
    private Vignette vignette;
    private AudioSource audioSource;
    private float vignetteVelocity;
    private Coroutine smokingRecovery;
    private float nextRecoveryStep;
    private bool calmingForMind;
    private float stressBeforeMind;
    private float? recoveryDelayBeforeMind;
    public bool IsRecoveringAfterSmoking => smokingRecovery != null;

    void Awake()
    {
        BindVolume(volume, blendWithSceneProfile);

        // Stress owns a dedicated source; footsteps and weapon sounds remain independent.
        audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.clip = stressSound;
        audioSource.loop = true;
        audioSource.playOnAwake = false;
    }

    public void BindVolume(Volume targetVolume, bool blendWithScene = false)
    {
        volume = targetVolume;
        blendWithSceneProfile = blendWithScene;
        if (blendWithSceneProfile && volume != null) volume.weight = 0f;
        if (volume != null && volume.profile != null) volume.profile.TryGet(out vignette);
    }

    void Update()
    {
        if (!calmingForMind && !IsRecoveringAfterSmoking)
        {
            if (Keyboard.current != null && Keyboard.current.spaceKey.isPressed)
                stress += increaseSpeed * Time.deltaTime;
            if (Keyboard.current != null && Keyboard.current.minusKey.isPressed)
                stress -= decreaseSpeed * Time.deltaTime;
        }

        stress = Mathf.Clamp(stress, 0f, 100f);

        UpdateStressEffects();
    }

    // Сбрасывает стресс, сохраняя штатное обновление эффектов в Update.
    public void ResetStress()
    {
        StopSmokingRecovery();
        stress = 0f;
        UpdateStressEffects();
    }

    public void BeginSmokingRecovery()
    {
        if (!calmingForMind && !IsRecoveringAfterSmoking && stress > 0f)
            smokingRecovery = StartCoroutine(RecoverStressAfterSmoking(0.1f));
    }

    private IEnumerator RecoverStressAfterSmoking(float delay)
    {
        while (stress > 0f)
        {
            nextRecoveryStep = Time.time + delay;
            yield return new WaitForSeconds(delay);
            stress = Mathf.Max(0f, stress - 10f);
            UpdateStressEffects();
            delay = 0.1f;
        }
        smokingRecovery = null;
    }

    private void StopSmokingRecovery()
    {
        if (smokingRecovery == null) return;
        StopCoroutine(smokingRecovery);
        smokingRecovery = null;
    }

    internal void BeginMindCalm()
    {
        stressBeforeMind = stress;
        recoveryDelayBeforeMind = IsRecoveringAfterSmoking ? Mathf.Max(0f, nextRecoveryStep - Time.time) : (float?)null;
        StopSmokingRecovery();
        calmingForMind = true;
    }

    internal void SetMindCalmProgress(float progress) => stress = Mathf.Lerp(stressBeforeMind, 0f, progress);

    internal void EndMindCalm(bool completed)
    {
        calmingForMind = false;
        stress = completed ? 0f : stressBeforeMind;
        if (!completed && recoveryDelayBeforeMind.HasValue && stress > 0f)
            smokingRecovery = StartCoroutine(RecoverStressAfterSmoking(recoveryDelayBeforeMind.Value));
        recoveryDelayBeforeMind = null;
    }

    private void UpdateStressEffects()
    {
        if (vignette != null)
        {
            if (blendWithSceneProfile)
            {
                vignette.intensity.value = maxVignetteIntensity;
                volume.weight = Mathf.SmoothDamp(volume.weight, stress / 100f,
                    ref vignetteVelocity, vignetteSmoothTime);
            }
            else
            {
                vignette.intensity.value = Mathf.SmoothDamp(vignette.intensity.value,
                    stress / 100f * maxVignetteIntensity, ref vignetteVelocity, vignetteSmoothTime);
            }

        }

        audioSource.clip = stressSound;
        if (stressSound != null && stress >= soundThreshold)
        {
            if (!audioSource.isPlaying)
            {
                audioSource.Play();
            }
        }
        else if (audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }
}
