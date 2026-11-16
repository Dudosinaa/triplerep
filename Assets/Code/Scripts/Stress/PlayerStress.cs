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

    private bool blendWithSceneProfile;
    private Vignette vignette;
    private AudioSource audioSource;
    private float vignetteVelocity;
    private bool isRecoveringAfterSmoking;
    public bool IsRecoveringAfterSmoking => isRecoveringAfterSmoking;

    void Awake()
    {
        if (volume != null && volume.profile != null)
            volume.profile.TryGet(out vignette);

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
        if (!isRecoveringAfterSmoking)
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
        StopAllCoroutines();
        isRecoveringAfterSmoking = false;
        stress = 0f;
        UpdateStressEffects();
    }

    // После клипа снижает стресс шагами, игнорируя ручное управление.
    public IEnumerator RecoverStressAfterSmoking()
    {
        if (isRecoveringAfterSmoking)
        {
            yield break;
        }

        isRecoveringAfterSmoking = true;
        while (stress > 0f)
        {
            yield return new WaitForSeconds(0.1f);
            stress = Mathf.Max(0f, stress - 10f);
            UpdateStressEffects();
        }
        isRecoveringAfterSmoking = false;
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
