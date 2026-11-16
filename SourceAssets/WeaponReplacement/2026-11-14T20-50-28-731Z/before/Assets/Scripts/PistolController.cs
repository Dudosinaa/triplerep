using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class PistolController : MonoBehaviour
{
    [SerializeField] private Camera playerCamera;
    [SerializeField] private FirstPersonPlayer movement;
    [SerializeField] private Transform viewModel;
    [SerializeField] private Animator animator;
    [SerializeField] private AnimationClip shotClip;
    [SerializeField] private AnimationClip reloadClip;
    [SerializeField] private Text ammoText;
    [SerializeField] private Text statusText;
    [SerializeField] private int magazineSize = 8;
    [SerializeField] private int reserveAmmo = 32;
    [SerializeField] private float shotRange = 60f;
    [SerializeField] private float shotCooldown = 0.35f;
    [SerializeField] private Vector3 hipPosition = new Vector3(0.24f, -0.23f, 0.4f);
    [SerializeField] private Vector3 aimPosition = new Vector3(0.58f, -0.13f, 0.28f);
    [SerializeField, Range(1f, 2f)] private float aimScale = 1.2f;
    [SerializeField, Min(0f)] private float aimCameraForward = 0.08f;
    [SerializeField, Min(0.1f)] private float aimSpeed = 14f;

    private ProceduralPistol procedural;
    private int rounds;
    private bool equipped = true;
    private bool reloading;
    private float nextShot;
    private Coroutine action;
    private float restingCameraZ;

    public bool IsEquipped => equipped;
    public int Rounds => rounds;
    public int ReserveAmmo => reserveAmmo;
    public bool IsReloading => reloading;

    private void Awake()
    {
        if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>();
        if (movement == null) movement = GetComponent<FirstPersonPlayer>();
        if (playerCamera != null) restingCameraZ = playerCamera.transform.localPosition.z;
        if (animator == null && playerCamera != null)
        {
            if (viewModel == null)
            {
                viewModel = new GameObject("Pistol View Model").transform;
                viewModel.SetParent(playerCamera.transform, false);
            }
            procedural = viewModel.GetComponent<ProceduralPistol>();
            if (procedural == null) procedural = viewModel.gameObject.AddComponent<ProceduralPistol>();
            aimPosition = new Vector3(0f, -0.115f, 0.4f);
            aimScale = 1f;
            playerCamera.nearClipPlane = 0.01f;
        }
        rounds = magazineSize;
        if (viewModel != null)
        {
            viewModel.localPosition = hipPosition;
            viewModel.localScale = Vector3.one;
        }
        UpdateHud();
    }

    private void Update()
    {
        bool canUse = movement != null && movement.enabled && playerCamera != null &&
                      playerCamera.enabled && Cursor.lockState == CursorLockMode.Locked;
        if (!canUse && action != null) CancelAction();
        if (viewModel != null) viewModel.gameObject.SetActive(equipped && canUse);
        if (ammoText != null) ammoText.enabled = canUse;
        if (statusText != null) statusText.enabled = canUse;
        bool aiming = canUse && equipped && !reloading && Mouse.current != null &&
                      Mouse.current.rightButton.isPressed;
        float blend = 1f - Mathf.Exp(-aimSpeed * Time.deltaTime);
        if (viewModel != null)
        {
            viewModel.localPosition = Vector3.Lerp(viewModel.localPosition,
                aiming ? aimPosition : hipPosition, blend);
            viewModel.localScale = Vector3.Lerp(viewModel.localScale,
                Vector3.one * (aiming ? aimScale : 1f), blend);
        }
        if (playerCamera != null)
        {
            Vector3 cameraPosition = playerCamera.transform.localPosition;
            cameraPosition.z = Mathf.Lerp(cameraPosition.z,
                restingCameraZ + (aiming ? aimCameraForward : 0f), blend);
            playerCamera.transform.localPosition = cameraPosition;
        }
        if (!canUse || Keyboard.current == null || Mouse.current == null) return;

        if (Keyboard.current.qKey.wasPressedThisFrame)
        {
            equipped = !equipped;
            if (!equipped) CancelAction();
            UpdateHud();
        }
        if (!equipped) return;

        if (Keyboard.current.rKey.wasPressedThisFrame) StartReload();
        if (Mouse.current.leftButton.wasPressedThisFrame) Fire();
    }

    private void Fire()
    {
        if (reloading || Time.time < nextShot) return;
        if (rounds == 0)
        {
            StartReload();
            return;
        }

        rounds--;
        nextShot = Time.time + shotCooldown;
        if (action != null) StopCoroutine(action);
        action = StartCoroutine(PlayShot());

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        RaycastHit[] hits = Physics.RaycastAll(ray, shotRange, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.IsChildOf(transform)) continue;
            var target = hit.collider.GetComponentInParent<ShootingTarget>();
            if (target != null) target.Hit();
            break;
        }
        UpdateHud();
    }

    private IEnumerator PlayShot()
    {
        if (procedural != null) procedural.Shot();
        if (animator != null) animator.Play("Shot", 0, 0f);
        yield return new WaitForSeconds(shotClip != null ? shotClip.length : shotCooldown);
        if (!reloading && animator != null) animator.Play("Idle", 0, 0f);
        action = null;
    }

    private void StartReload()
    {
        if (reloading || rounds >= magazineSize || reserveAmmo <= 0) return;
        if (action != null) StopCoroutine(action);
        action = StartCoroutine(Reload());
    }

    private IEnumerator Reload()
    {
        reloading = true;
        if (procedural != null) procedural.Reload();
        if (animator != null) animator.Play("Reload", 0, 0f);
        UpdateHud();
        yield return new WaitForSeconds(reloadClip != null ? reloadClip.length : 2.2f);
        int needed = magazineSize - rounds;
        int loaded = Mathf.Min(needed, reserveAmmo);
        rounds += loaded;
        reserveAmmo -= loaded;
        reloading = false;
        if (procedural != null) procedural.CancelReload();
        if (animator != null) animator.Play("Idle", 0, 0f);
        action = null;
        UpdateHud();
    }

    private void CancelAction()
    {
        if (action != null) StopCoroutine(action);
        action = null;
        reloading = false;
        if (animator != null) animator.Play("Idle", 0, 0f);
    }

    private void OnDisable()
    {
        CancelAction();
        if (viewModel != null) viewModel.gameObject.SetActive(false);
    }

    private void UpdateHud()
    {
        if (ammoText != null) ammoText.text = equipped ? $"{rounds} / {reserveAmmo}" : "Оружие убрано";
        if (statusText != null)
            statusText.text = !equipped ? "Q — достать пистолет   ПКМ — приблизить" :
                reloading ? "Перезарядка..." : "ЛКМ — выстрел   ПКМ — прицел   R — перезарядка   Q — убрать";
    }
}
