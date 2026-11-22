using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Animator owns only the imported model below this holder. PistolController moves the holder.
public sealed class FirstPersonWeaponView : MonoBehaviour
{
    private const float ReloadSoundDelay = 0.8f;
    private const float MuzzleLightDuration = 0.05f;
    public Animator Animator { get; private set; }
    public GameObject Model { get; private set; }
    public AnimationClip RuntimeReloadClip => reloadAnimation;
    public AnimationClip RuntimeEmptyReloadClip => emptyReloadAnimation;
    private WeaponDefinition definition;
    private Transform calibration;
    private Vector3 restingCalibrationPosition;
    private AnimationClip reloadAnimation;
    private AnimationClip emptyReloadAnimation;
    private AnimatorOverrideController runtimeController;
    private int activeReloadOperation;
    public event Action<int> MagazineInserted;
    private Avatar generatedAvatar;
    private AudioSource shotSource;
    private AudioSource reloadSource;
    private ParticleSystem[] muzzleFlashParticles;
    private Light muzzleFlashLight;
    private static readonly int Finished = UnityEngine.Animator.StringToHash("ActionFinished");
    private bool idleOnShow;
    private bool initialized;

    public bool Initialize(WeaponDefinition settings)
    {
        if (Model != null) return initialized;
        definition = settings;
        if (definition == null || !definition.IsComplete)
        {
            Debug.LogError("Weapon definition is incomplete. No replacement geometry will be created.", this);
            return false;
        }
        calibration = new GameObject("Weapon Camera Alignment").transform;
        calibration.SetParent(transform, false);
        Model = Instantiate(definition.modelPrefab, calibration, false);
        if (Application.isPlaying) FirstPersonHandsRendererFeature.SetHandsLayer(Model);
        Model.name = definition.displayName;
        Model.transform.localPosition = Vector3.zero;
        Model.transform.localRotation = Quaternion.identity;
        Model.transform.localScale = definition.modelScale;
        definition.idleClip.SampleAnimation(Model, 0f);
        var authoredCamera = Model.GetComponentInChildren<Camera>(true);
        if (authoredCamera == null)
        {
            Debug.LogError("The selected FBX requires its authored camera for first-person alignment.", this);
            Model.SetActive(false);
            return false;
        }
        // Align the authored eye frame once. Its animated camera never controls the player camera.
        Quaternion correction = transform.rotation * Quaternion.Inverse(authoredCamera.transform.rotation);
        calibration.rotation = correction * calibration.rotation;
        calibration.position += transform.position - authoredCamera.transform.position;
        restingCalibrationPosition = calibration.localPosition;
        foreach (var camera in Model.GetComponentsInChildren<Camera>(true))
        {
            camera.enabled = false;
            camera.tag = "Untagged";
        }
        foreach (var listener in Model.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
        foreach (var light in Model.GetComponentsInChildren<Light>(true)) light.enabled = false;
        foreach (var collider in Model.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        foreach (var renderer in Model.GetComponentsInChildren<Renderer>(true))
        {
            if (definition.visibleRendererNames != null && definition.visibleRendererNames.Length > 0 &&
                !definition.visibleRendererNames.Contains(renderer.name))
            {
                // FBX custom-shape widgets and hidden shell props are not part of the visible FPS kit.
                renderer.gameObject.SetActive(false);
                continue;
            }
            var mapped = renderer.sharedMaterials;
            for (int i = 0; i < mapped.Length; i++)
            {
                string name = mapped[i] != null ? mapped[i].name.Replace(" (Instance)", "") : "";
                var binding = definition.materialBindings.FirstOrDefault(b => b.sourceName == name);
                if (binding == null)
                {
                    Debug.LogError("Unmapped weapon material slot: " + renderer.name + "/" + name, renderer);
                    Model.SetActive(false);
                    return false;
                }
                mapped[i] = binding.material;
            }
            renderer.sharedMaterials = mapped;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (renderer is SkinnedMeshRenderer skin) skin.updateWhenOffscreen = true;
        }
        Animator = Model.GetComponent<Animator>();
        if (Animator == null) Animator = Model.AddComponent<Animator>();
        if (Animator.avatar == null)
        {
            generatedAvatar = AvatarBuilder.BuildGenericAvatar(Model, definition.rigRoot);
            Animator.avatar = generatedAvatar;
        }
        if (Animator.avatar == null || !Animator.avatar.isValid)
        {
            Debug.LogError("The selected model Generic avatar is invalid. Use its own rig and clips.", this);
            Model.SetActive(false);
            return false;
        }
        reloadAnimation = Instantiate(definition.reloadClip);
        reloadAnimation.name = definition.reloadClip.name + " (Runtime Events)";
        emptyReloadAnimation = definition.emptyReloadClip == definition.reloadClip ? reloadAnimation : Instantiate(definition.emptyReloadClip);
        runtimeController = new AnimatorOverrideController(definition.controller);
        var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        runtimeController.GetOverrides(overrides);
        for (int i = 0; i < overrides.Count; i++)
        {
            var original = overrides[i].Key;
            if (original == definition.reloadClip) overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(original, reloadAnimation);
            else if (original == definition.emptyReloadClip) overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(original, emptyReloadAnimation);
        }
        runtimeController.ApplyOverrides(overrides);
        Animator.runtimeAnimatorController = runtimeController;
        Animator.fireEvents = true;
        var eventReceiver = Model.AddComponent<WeaponAnimationEvents>();
        eventReceiver.owner = this;
        Animator.applyRootMotion = false;
        Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        Animator.speed = 1f;
        shotSource = CreateAudioSource(definition.shotSound);
        reloadSource = CreateAudioSource(definition.reloadSound);
        if (Application.isPlaying) InitializeMuzzleFlash();
        PlayIdle();
        initialized = true;
        return true;
    }
    public void SetReloadDrop(float distance)
    {
        if (calibration != null)
            calibration.localPosition = restingCalibrationPosition + Vector3.down * distance;
    }
    public void ArmReloadEvent(int operationId, bool empty)
    {
        activeReloadOperation = operationId;
        var original = empty ? definition.emptyReloadClip : definition.reloadClip;
        var runtimeClip = empty ? emptyReloadAnimation : reloadAnimation;
        float phase = empty ? definition.emptyReloadInsertNormalizedTime : definition.reloadInsertNormalizedTime;
        var markers = original.events.Where(e => e.functionName == "OnMagazineInserted").ToArray();
        // Baked clips own their measured seating time. The phase fallback preserves older definitions.
        float insertionTime = markers.Length == 1 ? markers[0].time : Mathf.Clamp01(phase) * runtimeClip.length;
        var events = original.events.Where(e => e.functionName != "OnMagazineInserted").ToList();
        events.Add(new AnimationEvent {
            functionName = "OnMagazineInserted", time = insertionTime,
            intParameter = operationId, messageOptions = SendMessageOptions.RequireReceiver
        });
#if UNITY_EDITOR
        if (!Application.isPlaying) UnityEditor.AnimationUtility.SetAnimationEvents(runtimeClip, events.ToArray());
        else
#endif
            runtimeClip.events = events.ToArray();
    }
    public void ReceiveMagazineInsertion(int operationId)
    {
        if (operationId == 0 || operationId != activeReloadOperation) return;
        if (!IsInState("Reload") && !IsInState("ReloadEmpty")) return;
        MagazineInserted?.Invoke(operationId);
    }
    public void InvalidateReloadEvent() { activeReloadOperation = 0; }
    public void PlayState(string state)
    {
        Animator.SetBool(Finished, false);
        Animator.Play(UnityEngine.Animator.StringToHash("Base Layer." + state), 0, 0f);
        Animator.Update(0f);
    }
    public void PlayIdle()
    {
        idleOnShow = true;
        if (Animator != null && Animator.isActiveAndEnabled)
        {
            PlayState("Idle");
            idleOnShow = false;
        }
    }
    public void CompleteAction() { Animator.SetBool(Finished, true); }
    public void SetVisible(bool visible)
    {
        if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
        if (visible && idleOnShow) PlayIdle();
    }
    public bool IsInState(string state) => Animator.GetCurrentAnimatorStateInfo(0).shortNameHash ==
        UnityEngine.Animator.StringToHash(state);
    public float NormalizedTime => Animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
    public void PlayShotSound()
    {
        if (shotSource != null && shotSource.clip != null)
            shotSource.PlayOneShot(shotSource.clip, definition.shotVolume);
    }

    private void InitializeMuzzleFlash()
    {
        if (definition.muzzleFlashPrefab == null) return;
        var muzzleBone = string.IsNullOrEmpty(definition.muzzleBonePath)
            ? null
            : Model.transform.Find(definition.muzzleBonePath);
        if (muzzleBone == null)
        {
            Debug.LogWarning("Muzzle flash bone was not found: " + definition.muzzleBonePath, this);
            return;
        }

        var prefab = definition.muzzleFlashPrefab;
        var flash = Instantiate(prefab, muzzleBone, false);
        flash.transform.SetLocalPositionAndRotation(definition.muzzleLocalPosition,
            Quaternion.Euler(definition.muzzleLocalEulerAngles) * prefab.transform.localRotation);
        flash.transform.localScale = prefab.transform.localScale * definition.muzzleFlashScale;
        FirstPersonHandsRendererFeature.SetHandsLayer(flash);

        muzzleFlashParticles = flash.GetComponentsInChildren<ParticleSystem>();
        foreach (var particles in muzzleFlashParticles)
        {
            particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.playOnAwake = false;
            main.loop = false;
            var emission = particles.emission;
            emission.enabled = false;
        }

        // The WarFX preview flickers continuously. The shot controls this instance's light.
        var flicker = flash.GetComponentInChildren<WFX_LightFlicker>();
        if (flicker != null) flicker.enabled = false;
        muzzleFlashLight = flash.GetComponentInChildren<Light>();
        if (muzzleFlashLight != null)
            muzzleFlashLight.intensity *= definition.muzzleLightIntensityMultiplier;
        TurnOffMuzzleLight();
    }

    internal void PlayMuzzleFlash()
    {
        if (muzzleFlashParticles == null) return;
        foreach (var particles in muzzleFlashParticles)
        {
            particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.Play(false);
            particles.Emit(1);
        }

        if (muzzleFlashLight == null || muzzleFlashLight.intensity <= 0f) return;
        CancelInvoke(nameof(TurnOffMuzzleLight));
        muzzleFlashLight.enabled = true;
        Invoke(nameof(TurnOffMuzzleLight), MuzzleLightDuration);
    }

    private void TurnOffMuzzleLight()
    {
        if (muzzleFlashLight != null) muzzleFlashLight.enabled = false;
    }

    private void OnDisable()
    {
        if (muzzleFlashParticles == null) return;
        CancelInvoke(nameof(TurnOffMuzzleLight));
        TurnOffMuzzleLight();
        foreach (var particles in muzzleFlashParticles)
            if (particles != null) particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    public void PlayReloadSound()
    {
        CancelInvoke(nameof(PlayReloadClip));
        if (reloadSource != null && reloadSource.clip != null)
            Invoke(nameof(PlayReloadClip), ReloadSoundDelay);
    }
    private void PlayReloadClip() => reloadSource.Play();

    public void StopReloadSound()
    {
        CancelInvoke(nameof(PlayReloadClip));
        if (reloadSource != null) reloadSource.Stop();
    }
    private AudioSource CreateAudioSource(AudioClip clip)
    {
        var source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.clip = clip;
        return source;
    }
    private static void Release(UnityEngine.Object value)
    {
        if (Application.isPlaying) Destroy(value);
        else DestroyImmediate(value);
    }
    private void OnDestroy()
    {
        if (runtimeController != null) Release(runtimeController);
        if (emptyReloadAnimation != null && emptyReloadAnimation != reloadAnimation) Release(emptyReloadAnimation);
        if (reloadAnimation != null) Release(reloadAnimation);
        if (generatedAvatar != null) Release(generatedAvatar);
    }
}
