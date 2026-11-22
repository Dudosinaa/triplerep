using System;
using System.IO;
using System.Threading.Tasks;
using GLTFast;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class SmokingController : MonoBehaviour
{
    [SerializeField] private string gltfPath = "Smoking/model.gltf";
    [SerializeField, Range(0f, 100f)] private float smokingThreshold = 40f;
    [SerializeField] private TMP_Text hintText;
    private PlayerStress playerStress;
    private FirstPersonPlayer movement;
    private PlayerMovement demoMovement;
    private Transform playerCamera;
    private Animation smokingAnimation;
    private AnimationClip smokingClip;
    private Renderer[] modelRenderers;
    private GameObject modelRoot;
    private GltfImport gltf;
    private bool modelReady;
    private bool destroyed;
    public bool IsSmoking { get; private set; }
    internal bool UsesCamera(Camera camera) => camera.transform == playerCamera;

    private async void Start()
    {
        playerStress = GetComponent<PlayerStress>();
        movement = GetComponent<FirstPersonPlayer>();
        demoMovement = GetComponent<PlayerMovement>();
        var camera = GetComponentInChildren<Camera>();
        if (playerStress == null || camera == null)
        {
            Debug.LogError("SmokingController requires PlayerStress and a player camera.", this);
            enabled = false;
            return;
        }
        playerCamera = camera.transform;
        PrepareHint();
        await LoadOriginalModel();
    }
    private bool CanStart()
    {
        return modelReady && playerStress != null && !IsSmoking &&
            !playerStress.IsRecoveringAfterSmoking && playerStress.stress >= smokingThreshold &&
            CanMove();
    }
    private bool CanMove() =>
        (movement == null || movement.isActiveAndEnabled) &&
        (demoMovement == null || demoMovement.isActiveAndEnabled);

    private void Update()
    {
        bool canStart = CanStart();
        if (hintText != null) hintText.gameObject.SetActive(canStart);
        if (!canStart || Keyboard.current == null || !Keyboard.current.yKey.wasPressedThisFrame) return;
        if (!smokingAnimation.Play(smokingClip.name)) return;
        IsSmoking = true;
        SetModelVisible(true);
        if (hintText != null) hintText.gameObject.SetActive(false);
    }
    private void LateUpdate()
    {
        if (!IsSmoking || smokingAnimation == null) return;
        if (!CanMove())
        {
            FinishSmoking();
            return;
        }
        var state = smokingAnimation[smokingClip.name];
        if (state != null && state.normalizedTime < 1f && state.time < smokingClip.length &&
            smokingAnimation.IsPlaying(smokingClip.name)) return;
        FinishSmoking();
        playerStress.BeginSmokingRecovery();
    }
    private void FinishSmoking()
    {
        if (smokingAnimation != null) smokingAnimation.Stop();
        SetModelVisible(false);
        IsSmoking = false;
    }
    private async Task LoadOriginalModel()
    {
        gltf = new GltfImport();
        try
        {
            var fullPath = Path.Combine(Application.streamingAssetsPath, gltfPath);
            if (!await gltf.Load(fullPath, new ImportSettings { AnimationMethod = AnimationMethod.Legacy }))
                throw new Exception("glTFast could not load the smoking animation.");
            if (destroyed) return;
            modelRoot = new GameObject("Original Smoking Model");
            modelRoot.transform.SetParent(playerCamera, false);
            modelRoot.transform.localPosition = new Vector3(0f, -1.6f, 2.35f);
            modelRoot.transform.localEulerAngles = new Vector3(0f, 180f, 0f);
            var instantiator = new GameObjectInstantiator(gltf, modelRoot.transform);
            if (!await gltf.InstantiateMainSceneAsync(instantiator))
                throw new Exception("glTFast could not instantiate the smoking animation.");
            if (destroyed) return;
            FirstPersonHandsRendererFeature.SetHandsLayer(modelRoot);
            smokingAnimation = instantiator.SceneInstance.LegacyAnimation;
            if (smokingAnimation == null || smokingAnimation.clip == null)
                throw new Exception("The smoking animation has no Legacy Animation clip.");
            smokingClip = smokingAnimation.clip;
            modelRenderers = modelRoot.GetComponentsInChildren<Renderer>(true);
            smokingAnimation.playAutomatically = false;
            smokingAnimation.wrapMode = WrapMode.Once;
            smokingAnimation[smokingClip.name].wrapMode = WrapMode.Once;
            smokingAnimation.Stop();
            SetModelVisible(false);
            modelReady = true;
        }
        catch (Exception exception)
        {
            if (!destroyed) Debug.LogError("SmokingController: " + exception.Message, this);
        }
        finally { if (destroyed) gltf?.Dispose(); }
    }
    private void SetModelVisible(bool visible)
    {
        if (modelRenderers == null) return;
        foreach (var renderer in modelRenderers) if (renderer != null) renderer.enabled = visible;
    }
    private void PrepareHint()
    {
        if (hintText == null)
        {
            var canvas = GetComponentInChildren<Canvas>(true);
            // The preserved StressDemo uses PlayerMovement and a separate scene canvas.
            if (canvas == null && GetComponent<PlayerMovement>() != null) canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null) return;
            var go = new GameObject("Smoking Hint", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(canvas.transform, false);
            hintText = go.GetComponent<TMP_Text>();
            hintText.font = TMP_Settings.defaultFontAsset;
        }
        // A serialized inactive TMP object has not run Awake yet. Initialize it before styling.
        hintText.gameObject.SetActive(true);
        hintText.text = "Покурить — Y";
        var prompt = GetComponent<SmokingPromptUI>();
        if (prompt != null) prompt.Bind(hintText);
        hintText.gameObject.SetActive(false);
    }
    private void OnDisable()
    {
        if (IsSmoking) FinishSmoking();
        if (hintText != null) hintText.gameObject.SetActive(false);
    }
    private void OnDestroy()
    {
        destroyed = true;
        if (modelRoot != null) Destroy(modelRoot);
        if (modelReady) gltf?.Dispose();
    }
}
