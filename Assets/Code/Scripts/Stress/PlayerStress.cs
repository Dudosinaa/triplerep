using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;

public class PlayerStress : MonoBehaviour
{
    [Header("Стресс")]
    [Range(0f, 100f)] public float stress = 0f;

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
    private float vignetteBeforeMind;
    private float vignetteVelocityBeforeMind;
    private float? recoveryDelayBeforeMind;
    private bool atMaximum;
    internal event System.Action MaximumReached;
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
        if (GameplayPause.BlocksGameplayInput) return;
        SetStress(stress);

        UpdateStressEffects();
    }

    // Сбрасывает стресс, сохраняя штатное обновление эффектов в Update.
    public void ResetStress()
    {
        StopSmokingRecovery();
        SetStress(0f);
        UpdateStressEffects();
    }

    public void AddStress(float amount)
    {
        if (calmingForMind || !isActiveAndEnabled) return;
        SetStress(stress + amount);
    }

    private void SetStress(float value)
    {
        stress = Mathf.Clamp(value, 0f, 100f);
        bool reachedMaximum = stress >= 100f && !atMaximum;
        atMaximum = stress >= 100f;
        if (reachedMaximum) MaximumReached?.Invoke();
    }

    internal void RestoreCheckpoint(float value)
    {
        StopSmokingRecovery();
        SetStress(value);
        vignetteVelocity = 0f;
        if (vignette != null)
        {
            if (blendWithSceneProfile) volume.weight = stress / 100f;
            else vignette.intensity.value = stress / 100f * maxVignetteIntensity;
        }
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
            SetStress(stress - 10f);
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
        if (vignette != null)
            vignetteBeforeMind = blendWithSceneProfile ? volume.weight : vignette.intensity.value;
        vignetteVelocityBeforeMind = vignetteVelocity;
        recoveryDelayBeforeMind = IsRecoveringAfterSmoking ? Mathf.Max(0f, nextRecoveryStep - Time.time) : (float?)null;
        StopSmokingRecovery();
        calmingForMind = true;
    }

    internal void SetMindCalmProgress(float progress) => stress = Mathf.Lerp(stressBeforeMind, 0f, progress);

    internal void EndMindCalm(bool completed)
    {
        calmingForMind = false;
        stress = completed ? 0f : stressBeforeMind;
        if (vignette != null)
        {
            if (blendWithSceneProfile) volume.weight = vignetteBeforeMind;
            else vignette.intensity.value = vignetteBeforeMind;
        }
        vignetteVelocity = vignetteVelocityBeforeMind;
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
