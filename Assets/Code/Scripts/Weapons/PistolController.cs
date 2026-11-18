using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class PistolController : MonoBehaviour
{
    [SerializeField] private Camera playerCamera;
    [SerializeField] private FirstPersonPlayer movement;
    [SerializeField] private Transform viewModel;
    [SerializeField] private WeaponDefinition definition;
    [SerializeField] private Text ammoText;
    [SerializeField] private Text statusText;
    [SerializeField] private int magazineSize = 8;
    [SerializeField] private int reserveAmmo = 32;
    [SerializeField] private float shotRange = 60f;
    [SerializeField] private float shotCooldown = 0.35f;
    [SerializeField, Min(0f)] private float aimCameraForward = 0.08f;
    [SerializeField, Min(0.1f)] private float aimSpeed = 14f;
    private WeaponMagazine magazine;
    private FirstPersonWeaponView weaponView;
    private bool equipped = false;
    private bool ready;
    private float nextShot;
    private float restingCameraZ;
    private string actionState;
    private AnimationClip actionClip;
    private float actionClock;
    private bool equipOnShow = true;
    public bool IsEquipping => actionState == "Equip" || (equipped && equipOnShow);
    public bool IsEquipped => equipped;
    public int Rounds => magazine != null ? magazine.Rounds : 0;
    public int ReserveAmmo => magazine != null ? magazine.Reserve : reserveAmmo;
    public bool IsReloading => magazine != null && magazine.IsReloading;
    public FirstPersonWeaponView WeaponView => weaponView;
    public bool CanUseWeapon => ready && isActiveAndEnabled && movement != null && movement.enabled &&
        playerCamera != null && playerCamera.enabled && Cursor.lockState == CursorLockMode.Locked;

    private void Awake()
    {
        if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>();
        if (movement == null) movement = GetComponent<FirstPersonPlayer>();
        if (definition == null) definition = Resources.Load<WeaponDefinition>("HandsWeapon");
        magazine = new WeaponMagazine(Mathf.Max(1, magazineSize), Mathf.Max(0, reserveAmmo));
        if (playerCamera == null || definition == null || !definition.IsComplete)
        {
            Debug.LogError("PistolController requires a player camera and the Hands weapon definition.", this);
            enabled = false;
            return;
        }
        restingCameraZ = playerCamera.transform.localPosition.z;
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
        bool canUse = CanUseWeapon;
        if (!canUse && actionState != null) CancelAction();
        weaponView.SetVisible(equipped && canUse);
        if (ammoText != null) ammoText.enabled = canUse;
        if (statusText != null) statusText.enabled = canUse;
        bool aiming = canUse && equipped && !IsReloading && !IsEquipping && Mouse.current != null && Mouse.current.rightButton.isPressed;
        float blend = 1f - Mathf.Exp(-aimSpeed * Time.deltaTime);
        viewModel.localPosition = Vector3.Lerp(viewModel.localPosition,
            aiming ? weaponView.AimOffset : definition.hipOffset, blend);
        var cameraPosition = playerCamera.transform.localPosition;
        cameraPosition.z = Mathf.Lerp(cameraPosition.z, restingCameraZ + (aiming ? aimCameraForward : 0f), blend);
        playerCamera.transform.localPosition = cameraPosition;
        if (!canUse) return;
        if (equipped && equipOnShow)
        {
            equipOnShow = false;
            if (definition.equipClip != null) BeginAction("Equip", definition.equipClip);
        }
        if (Keyboard.current == null || Mouse.current == null) return;
        if (Keyboard.current.qKey.wasPressedThisFrame) SetEquipped(!equipped);
        if (!equipped) return;
        if (Keyboard.current.rKey.wasPressedThisFrame) TryReload();
        if (Mouse.current.leftButton.wasPressedThisFrame) TryFire();
    }
    private void LateUpdate()
    {
        // Observe completion after Animator evaluation. Only the insertion AnimationEvent transfers ammunition.
        bool canUse = CanUseWeapon;
        if (!canUse && actionState != null) CancelAction();
        if (weaponView != null) weaponView.SetVisible(equipped && canUse);
        if (ammoText != null) ammoText.enabled = canUse;
        if (statusText != null) statusText.enabled = canUse;
        if (canUse && equipped) TickAction(Time.deltaTime);
    }
    public bool TryFire()
    {
        if (!CanUseWeapon || !equipped || IsReloading || IsEquipping || Time.time < nextShot) return false;
        if (Rounds == 0) { TryReload(); return false; }
        if (!magazine.TryFire()) return false;
        nextShot = Time.time + shotCooldown;
        BeginAction("Shot", definition.shotClip);
        weaponView.PlayShotSound();
        var ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        var hits = Physics.RaycastAll(ray, shotRange, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var hit in hits)
        {
            if (hit.collider.transform.IsChildOf(transform)) continue;
            var target = hit.collider.GetComponentInParent<ShootingTarget>();
            if (target != null) target.Hit();
            break;
        }
        UpdateHud();
        return true;
    }
    public bool TryReload()
    {
        if (!CanUseWeapon || !equipped || IsEquipping || !magazine.TryBeginReload()) return false;
        bool empty = Rounds == 0;
        var clip = empty ? definition.emptyReloadClip : definition.reloadClip;
        weaponView.ArmReloadEvent(magazine.ReloadOperationId, empty);
        BeginAction(empty ? "ReloadEmpty" : "Reload", clip);
        UpdateHud();
        return true;
    }
    private void BeginAction(string state, AnimationClip clip)
    {
        actionState = state;
        actionClip = clip;
        actionClock = 0f;
        weaponView.PlayState(state);
    }
    private void TickAction(float deltaTime)
    {
        if (actionState == null) return;
        actionClock += deltaTime;
        float progress;
        if (weaponView.IsInState(actionState)) progress = weaponView.NormalizedTime;
        else if (weaponView.IsInState("Idle") && actionClock >= actionClip.length)
            progress = 1f; // Shot's exit transition may already have run this frame.
        else { CancelAction(); return; }

        if (progress < 1f) return;
        if (IsReloading)
        {
            if (!magazine.ReloadCommitted)
            {
                Debug.LogError("Reload finished without its magazine-insertion event; no ammunition transferred.", this);
                CancelAction();
                return;
            }
            magazine.EndReload();
            weaponView.InvalidateReloadEvent();
        }
        weaponView.CompleteAction();
        actionState = null;
        actionClip = null;
        UpdateHud();
    }
    private void OnMagazineInserted(int operationId)
    {
        if (!CanUseWeapon || !equipped || !IsReloading) return;
        if (magazine.ApplyInsertionEvent(operationId)) UpdateHud();
    }
    public void SetEquipped(bool value)
    {
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
        actionClip = null;
        magazine?.CancelReload();
        if (weaponView != null)
        {
            weaponView.InvalidateReloadEvent();
            weaponView.PlayIdle();
        }
        UpdateHud();
    }
    private void OnDisable()
    {
        CancelAction();
        if (weaponView != null) weaponView.SetVisible(false);
        if (ammoText != null) ammoText.enabled = false;
        if (statusText != null) statusText.enabled = false;
        if (playerCamera != null)
        {
            var position = playerCamera.transform.localPosition;
            position.z = restingCameraZ;
            playerCamera.transform.localPosition = position;
        }
    }
    private void OnDestroy()
    {
        if (weaponView != null) weaponView.MagazineInserted -= OnMagazineInserted;
    }
    private void UpdateHud()
    {
        if (ammoText != null) ammoText.text = equipped ? $"{Rounds} / {ReserveAmmo}" : "Оружие убрано";
        if (statusText != null) statusText.text = !equipped ? "Q — достать пистолет   ПКМ — приблизить" :
            IsReloading ? "Перезарядка..." : "ЛКМ — выстрел   ПКМ — прицел   R — перезарядка   Q — убрать";
    }
}
