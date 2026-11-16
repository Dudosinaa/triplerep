using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Adds gameplay to the preserved art scenes without rebuilding their environment or lighting.
public sealed class MergedGameBootstrap : MonoBehaviour
{
    public Vector3 spawnPosition;
    public float spawnYaw;
    public int otherLocationIndex;
    public string locationLabel;
    public bool compactLayout;
    private GameObject gameplay;
    private FirstPersonPlayer movement;
    private Transform player;
    private Transform portal;
    private Vector3 spawn;
    private Text travelHint;
    private Material portalMaterial;
    private VolumeProfile stressProfile;
    private void Start()
    {
        AddEnvironmentColliders();
        Physics.SyncTransforms();
        spawn = FindStandingPosition(spawnPosition);
        foreach (var camera in FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            camera.enabled = false;
            if (camera.CompareTag("MainCamera")) camera.tag = "Untagged";
        }
        foreach (var listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None)) listener.enabled = false;
        var prefab = Resources.Load<GameObject>("MergedGameplay");
        if (prefab == null) throw new InvalidOperationException("MergedGameplay prefab is missing.");
        gameplay = Instantiate(prefab, spawn, Quaternion.Euler(0f, spawnYaw, 0f));
        gameplay.name = "Merged Gameplay";
        movement = gameplay.GetComponentInChildren<FirstPersonPlayer>();
        player = movement.transform;
        movement.Teleport(spawn + Vector3.up * 0.05f);
        var cameraComponent = player.GetComponentInChildren<Camera>();
        cameraComponent.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        PlaceInteractionObjects();
        SetupNpc();
        Physics.SyncTransforms();
        var canvas = CreateHelpCanvas();
        var hintObject = new GameObject("Consciousness Hint", typeof(RectTransform), typeof(TextMeshProUGUI));
        hintObject.transform.SetParent(canvas.transform, false);
        var hint = hintObject.GetComponent<TMP_Text>();
        hint.font = TMP_Settings.defaultFontAsset;
        hint.text = "T — войти в сознание";
        hint.fontSize = 26f;
        hint.alignment = TextAlignmentOptions.Center;
        hint.rectTransform.anchorMin = new Vector2(0.25f, 0.30f);
        hint.rectTransform.anchorMax = new Vector2(0.75f, 0.37f);
        hint.rectTransform.offsetMin = hint.rectTransform.offsetMax = Vector2.zero;
        var sceneSwitch = player.gameObject.AddComponent<SceneSwitch>();
        sceneSwitch.interactionDistance = 2.7f;
        sceneSwitch.hintText = hint;
        var effects = new GameObject("Player Stress Effects");
        effects.transform.SetParent(gameplay.transform, false);
        var volume = effects.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 100f;
        stressProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        var vignette = stressProfile.Add<Vignette>(true);
        vignette.intensity.Override(0f);
        vignette.smoothness.Override(1f);
        volume.sharedProfile = stressProfile;
        var stress = player.gameObject.AddComponent<PlayerStress>();
        stress.increaseSpeed = stress.decreaseSpeed = 20f;
        stress.maxVignetteIntensity = 1f;
        stress.soundThreshold = 70f;
        stress.stressSound = Resources.Load<AudioClip>("MergeStressSound");
        stress.BindVolume(volume, true);
        player.gameObject.AddComponent<SmokingPromptUI>();
        player.gameObject.AddComponent<SmokingController>();
        SetupPortal();
        Debug.Log("MERGE_GAMEPLAY_READY: " + locationLabel);
    }
    private void AddEnvironmentColliders()
    {
        foreach (var filter in FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
        {
            var renderer = filter.GetComponent<MeshRenderer>();
            if (filter.sharedMesh == null || renderer == null || !renderer.enabled || filter.GetComponent<Collider>() != null)
                continue;
            var collider = filter.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh;
        }
    }
    private Vector3 FindStandingPosition(Vector3 requested)
    {
        var offsets = new[] { Vector3.zero, Vector3.forward, Vector3.back, Vector3.right, Vector3.left,
            new Vector3(1f, 0f, 1f), new Vector3(-1f, 0f, -1f), Vector3.forward * 2f, Vector3.right * 2f };
        foreach (var offset in offsets)
        {
            var point = requested + offset;
            foreach (var hit in Physics.RaycastAll(point + Vector3.up * 1.65f, Vector3.down, 5f,
                ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
            {
                if (hit.normal.y < 0.65f) continue;
                var feet = hit.point + Vector3.up * 0.04f;
                if (!Physics.CheckCapsule(feet + Vector3.up * 0.32f, feet + Vector3.up * 1.48f,
                    0.28f, ~0, QueryTriggerInteraction.Ignore)) return feet;
            }
        }
        Debug.LogWarning("No clear spawn floor found; inspect the scene spawn position in Play Mode.", this);
        // Prevent an unbounded fall while a spawn position is being adjusted in the editor.
        var safety = new GameObject("Spawn Safety Collider");
        var box = safety.AddComponent<BoxCollider>();
        box.size = new Vector3(4f, 0.1f, 4f);
        safety.transform.position = requested - Vector3.up * 0.05f;
        return requested + Vector3.up * 0.04f;
    }
    private void PlaceInteractionObjects()
    {
        int itemIndex = 0;
        int targetIndex = 0;
        var itemPositions = new[] { new Vector3(-0.7f, 0.4f, 1f), new Vector3(0.9f, 0.4f, 1.5f),
            new Vector3(-1f, 0.65f, 2.1f), new Vector3(1f, 0.55f, 2.1f) };
        foreach (Transform child in gameplay.transform)
        {
            if (child.GetComponent<ReadableDocument>() == null && child.GetComponent<InspectableItem>() == null &&
                child.GetComponent<ShootingTarget>() == null && child.GetComponent<NpcDialogue>() == null) continue;
            if (compactLayout)
            {
                if (child.GetComponent<ReadableDocument>() != null) child.localPosition = new Vector3(0.7f, 1.2f, 1f);
                else if (child.GetComponent<NpcDialogue>() != null) child.localPosition = new Vector3(0f, 0.05f, 2.3f);
                else if (child.GetComponent<InspectableItem>() != null)
                    child.localPosition = itemPositions[itemIndex++ % itemPositions.Length];
                else if (child.GetComponent<ShootingTarget>() != null)
                    child.localPosition = new Vector3(-1f + targetIndex++, 1.25f, 3f);
            }
            float height = child.localPosition.y;
            var hits = Physics.RaycastAll(new Vector3(child.position.x, spawn.y + 1.6f, child.position.z),
                Vector3.down, 5f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance);
            foreach (var hit in hits)
            {
                if (hit.collider.transform.IsChildOf(gameplay.transform) || hit.normal.y < 0.65f) continue;
                child.position = new Vector3(child.position.x, hit.point.y + height, child.position.z);
                break;
            }
        }
    }
    private void SetupNpc()
    {
        var npc = gameplay.GetComponentInChildren<NpcDialogue>();
        if (npc == null) return;
        var npcPrefab = Resources.Load<GameObject>("MergedNpc");
        if (npcPrefab != null)
        {
            foreach (var renderer in npc.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
            var visual = Instantiate(npcPrefab, npc.transform);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        }
        var point = new GameObject("Consciousness Focus");
        point.transform.SetParent(npc.transform, false);
        point.transform.localPosition = new Vector3(0f, 1.6f, -0.45f);
        point.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        point.AddComponent<FocusPoint>().sceneIndex = 2;
    }
    private Canvas CreateHelpCanvas()
    {
        var go = new GameObject("Location Help", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        go.transform.SetParent(gameplay.transform, false);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        MakeText(go.transform, "Controls", locationLabel + "  |  WASD — идти, Ctrl — присесть, E — взаимодействие, T — сознание\n" +
            "ЛКМ — выстрел, ПКМ — прицел, R — перезарядка, Q — оружие; пробел / минус — стресс, Y — покурить",
            new Vector2(0.02f, 0.87f), new Vector2(0.98f, 0.98f));
        travelHint = MakeText(go.transform, "Travel", "", new Vector2(0.30f, 0.18f), new Vector2(0.70f, 0.24f));
        travelHint.alignment = TextAnchor.MiddleCenter;
        return canvas;
    }
    private static Text MakeText(Transform parent, string name, string text, Vector2 min, Vector2 max)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var label = go.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 23;
        label.color = Color.white;
        label.raycastTarget = false;
        label.text = text;
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return label;
    }
    private void SetupPortal()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Location Exit";
        go.transform.SetParent(gameplay.transform, false);
        portal = go.transform;
        portal.position = spawn + Quaternion.Euler(0f, spawnYaw, 0f) * new Vector3(-1.2f, 0.8f, 0f);
        portal.localScale = new Vector3(0.12f, 1.6f, 0.12f);
        go.GetComponent<Collider>().enabled = false;
        portalMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        portalMaterial.SetColor("_BaseColor", new Color(0.1f, 0.7f, 0.9f));
        go.GetComponent<MeshRenderer>().sharedMaterial = portalMaterial;
    }
    private void Update()
    {
        if (player == null || movement == null) return;
        if (player.position.y < spawn.y - 20f) movement.Teleport(spawn + Vector3.up * 0.05f);
        bool canTravel = movement.enabled && Vector3.Distance(player.position, portal.position) <= 2.5f;
        if (travelHint != null)
            travelHint.text = canTravel ? (otherLocationIndex == 1 ? "F — перейти в дом" : "F — вернуться в метро") : "";
        if (canTravel && Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
            SceneSwitch.LoadSceneIndex(otherLocationIndex);
    }
    private void OnDestroy()
    {
        if (portalMaterial != null) Destroy(portalMaterial);
        if (stressProfile != null)
        {
            foreach (var component in stressProfile.components) if (component != null) Destroy(component);
            Destroy(stressProfile);
        }
    }
}
