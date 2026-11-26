using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class PistolController : MonoBehaviour
{
    private const float ReloadDropDistance = 0.15f;
    private const float ReloadLowerDuration = 0.22f;
    private const float ReloadRaiseDuration = 0.26f;

    [SerializeField] private Camera playerCamera;
    [SerializeField] private FirstPersonPlayer movement;
    [SerializeField] private Transform viewModel;
    [SerializeField] private WeaponDefinition definition;
    [SerializeField] private Text statusText;
    [SerializeField] private int magazineSize = 8;
    [SerializeField] private float shotRange = 60f;
    [SerializeField] private float shotCooldown = 0.22f;
    private readonly RaycastHit[] shotHits = new RaycastHit[16];
    private WeaponMagazine magazine;
    private FirstPersonWeaponView weaponView;
    private bool equipped = false;
    private bool ready;
    private float nextShot;
    private SmokingController smoking;
    private string actionState;
    private float reloadDropProgress;
    private bool equipOnShow = true;
    public bool IsEquipping => actionState == "Equip" || (equipped && equipOnShow);
    public bool IsEquipped => equipped;
    public int Rounds => magazine != null ? magazine.Rounds : 0;
    public int ReserveAmmo => magazine?.Reserve ?? int.MaxValue;
    public bool IsReloading => magazine != null && magazine.IsReloading;
    public FirstPersonWeaponView WeaponView => weaponView;
    public bool CanUseWeapon => ready && isActiveAndEnabled && movement != null && movement.enabled &&
        CanDrawPistol &&
        (smoking == null || !smoking.IsSmoking) &&
        playerCamera != null && playerCamera.enabled && Cursor.lockState == CursorLockMode.Locked;
    private bool CanDrawPistol => gameObject.scene.path == TrainMindSession.TrainPath &&
        (movement == null || movement.CanDrawPistol);

    private void Awake()
    {
        if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>();
        if (movement == null) movement = GetComponent<FirstPersonPlayer>();
        smoking = GetComponent<SmokingController>();
        if (definition == null) definition = Resources.Load<WeaponDefinition>("HandsWeapon");
        magazine = new WeaponMagazine(Mathf.Max(1, magazineSize), 0, unlimitedReserve: true);
        if (playerCamera == null || definition == null || !definition.IsComplete)
        {
            Debug.LogError("PistolController requires a player camera and the Hands weapon definition.", this);
            enabled = false;
            return;
        }
        playerCamera.nearClipPlane = 0.01f;
        if (viewModel == null)
        {
            viewModel = new GameObject("Pistol View Model").transform;
            viewModel.SetParent(playerCamera.transform, false);
        }
        viewModel.localPosition = definition.hipOffset;
        viewModel.localRotation = Quaternion.identity;
        viewModel.localScale = Vector3.one;
        weaponView = viewModel.GetComponent<FirstPersonWeaponView>();
        if (weaponView == null) weaponView = viewModel.gameObject.AddComponent<FirstPersonWeaponView>();
        ready = weaponView.Initialize(definition);
        if (ready) weaponView.MagazineInserted += OnMagazineInserted;
        if (!ready) enabled = false;
        UpdateHud();
    }
    private void Update()
    {
        if (GameplayPause.BlocksGameplayInput) return;
        bool canUse = CanUseWeapon;
        if (!canUse && (actionState != null || IsReloading)) CancelAction();
        weaponView.SetVisible(equipped && canUse);
        if (statusText != null) statusText.enabled = canUse;
        if (!canUse) return;
        if (equipped && equipOnShow)
        {
            equipOnShow = false;
            if (definition.equipClip != null) BeginAction("Equip");
        }
        if (Keyboard.current == null || Mouse.current == null) return;
        if (Keyboard.current.qKey.wasPressedThisFrame) SetEquipped(!equipped);
        if (!equipped) return;
        if (Keyboard.current.rKey.wasPressedThisFrame) TryReload();
        if (Mouse.current.leftButton.wasPressedThisFrame && !InteractionInput.PrimaryConsumedThisFrame) TryFire();
    }
    private void LateUpdate()
    {
        if (GameplayPause.BlocksGameplayInput) return;
        // Observe completion after Animator evaluation. Only the insertion AnimationEvent transfers ammunition.
        bool canUse = CanUseWeapon;
        if (!canUse && (actionState != null || IsReloading)) CancelAction();
        if (weaponView != null) weaponView.SetVisible(equipped && canUse);
        if (statusText != null) statusText.enabled = canUse;
        if (canUse && equipped) TickAction(Time.deltaTime);
    }
    public bool TryFire()
    {
        if (!CanUseWeapon || !equipped || IsReloading || IsEquipping || Time.time < nextShot) return false;
        if (Rounds == 0) { TryReload(); return false; }
        if (!magazine.TryFire()) return false;
        nextShot = Time.time + shotCooldown;
        BeginAction("Shot");
        weaponView.PlayShotSound();
        weaponView.PlayMuzzleFlash();
        var ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (ShotRaycast.TryGetFirstHit(ray, shotRange, ~0, transform, shotHits, out RaycastHit hit))
            hit.collider.GetComponentInParent<IShootingTarget>()?.Hit();
        return true;
    }
    public bool TryReload()
    {
        if (!CanUseWeapon || !equipped || IsEquipping || !magazine.TryBeginReload()) return false;
        // Let any active recoil finish while the holder moves below the camera.
        actionState = null;
        UpdateHud();
        return true;
    }
    private void BeginAction(string state)
    {
        actionState = state;
        weaponView.PlayState(state);
    }
    private void TickAction(float deltaTime)
    {
        if (actionState == null)
        {
            if (IsReloading) MoveReloadView(deltaTime);
            return;
        }

        if (weaponView.IsInState(actionState))
        {
            if (weaponView.NormalizedTime < 1f) return;
        }
        else if (actionState != "Shot" || !weaponView.IsInState("Idle"))
        {
            CancelAction();
            return;
        }

        if (IsReloading)
        {
            if (!magazine.ReloadCommitted)
            {
                Debug.LogError("Reload finished without its magazine-insertion event; no ammunition transferred.", this);
                CancelAction();
                return;
            }
            weaponView.InvalidateReloadEvent();
        }
        weaponView.CompleteAction();
        actionState = null;
    }
    private void MoveReloadView(float deltaTime)
    {
        // With no active clip, an uncommitted reload is lowering; a committed one is returning.
        bool raising = magazine.ReloadCommitted;
        float duration = raising ? ReloadRaiseDuration : ReloadLowerDuration;
        reloadDropProgress = Mathf.MoveTowards(reloadDropProgress, raising ? 0f : 1f, deltaTime / duration);
        weaponView.SetReloadDrop(ReloadDropDistance * Mathf.SmoothStep(0f, 1f, reloadDropProgress));

        if (raising && reloadDropProgress == 0f)
        {
            magazine.EndReload();
            UpdateHud();
        }
        else if (!raising && reloadDropProgress == 1f)
        {
            weaponView.ArmReloadEvent(magazine.ReloadOperationId, empty: true);
            BeginAction("ReloadEmpty");
            weaponView.PlayReloadSound();
        }
    }
    private void OnMagazineInserted(int operationId)
    {
        if (!CanUseWeapon || !equipped || !IsReloading) return;
        magazine.ApplyInsertionEvent(operationId);
    }
    public void SetEquipped(bool value)
    {
        if (value && !CanDrawPistol) return;
        bool changed = equipped != value;
        equipped = value;
        if (changed && equipped) equipOnShow = true;
        if (!equipped)
        {
            equipOnShow = false;
            CancelAction();
            if (weaponView != null) weaponView.SetVisible(false);
        }
        UpdateHud();
    }
    public void CancelAction()
    {
        actionState = null;
        reloadDropProgress = 0f;
        magazine?.CancelReload();
        if (weaponView != null)
        {
            weaponView.InvalidateReloadEvent();
            weaponView.StopReloadSound();
            weaponView.SetReloadDrop(0f);
            weaponView.PlayIdle();
        }
        UpdateHud();
    }

    internal void RestoreCheckpoint(bool wasEquipped, int rounds, int reserve)
    {
        CancelAction();
        magazine?.RestoreAmmo(rounds, reserve);
        equipped = wasEquipped && CanDrawPistol;
        equipOnShow = false;
        nextShot = 0f;
        if (weaponView != null)
        {
            viewModel.localPosition = definition.hipOffset;
            weaponView.SetVisible(equipped && CanUseWeapon);
        }
        UpdateHud();
    }
    private void OnDisable()
    {
        CancelAction();
        if (weaponView != null) weaponView.SetVisible(false);
        if (statusText != null) statusText.enabled = false;
    }
    private void OnDestroy()
    {
        if (weaponView != null) weaponView.MagazineInserted -= OnMagazineInserted;
    }
    private void UpdateHud()
    {
        if (statusText != null) statusText.text = !equipped ? "Q — достать пистолет   ПКМ — приблизить" :
            IsReloading ? "Перезарядка..." : "ЛКМ — выстрел   ПКМ — приблизить   R — перезарядка   Q — убрать";
    }
}
