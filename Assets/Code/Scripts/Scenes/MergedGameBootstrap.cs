using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// Scene services: player spawn and travel. Environment colliders are authored in the scene.
public sealed class MergedGameBootstrap : MonoBehaviour
{
    public Vector3 spawnPosition;
    public float spawnYaw;
    public int otherLocationIndex;
    public string locationLabel;
    private FirstPersonPlayer movement;
    private Transform player;
    private Transform portal;
    private Vector3 spawn;
    private Text travelHint;
    private Material portalMaterial;
    private void Start()
    {
        Physics.SyncTransforms();
        movement = FindFirstObjectByType<FirstPersonPlayer>();
        spawn = FindStandingPosition(movement != null ? movement.transform.position : spawnPosition);
        foreach (var camera in FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            if (movement != null && camera.transform.IsChildOf(movement.transform)) continue;
            camera.enabled = false;
            if (camera.CompareTag("MainCamera")) camera.tag = "Untagged";
        }
        foreach (var listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
            if (movement == null || !listener.transform.IsChildOf(movement.transform)) listener.enabled = false;
        if (movement == null)
        {
            var prefab = Resources.Load<GameObject>("Player");
            if (prefab == null) throw new InvalidOperationException("Player prefab is missing.");
            var instance = Instantiate(prefab, spawn, Quaternion.Euler(0f, spawnYaw, 0f));
            instance.name = "Player";
            movement = instance.GetComponent<FirstPersonPlayer>();
        }
        player = movement.transform;
        movement.Teleport(spawn + Vector3.up * 0.05f);
        player.GetComponentInChildren<Camera>().GetUniversalAdditionalCameraData().renderPostProcessing = true;
        CreateHelpCanvas();
        SetupPortal();
        Debug.Log("MERGE_GAMEPLAY_READY: " + locationLabel);
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
                if (movement != null && hit.collider.transform.IsChildOf(movement.transform)) continue;
                if (hit.normal.y < 0.65f) continue;
                var feet = hit.point + Vector3.up * 0.04f;
                var blockers = Physics.OverlapCapsule(feet + Vector3.up * 0.32f, feet + Vector3.up * 1.48f,
                    0.28f, ~0, QueryTriggerInteraction.Ignore);
                if (!blockers.Any(c => movement == null || !c.transform.IsChildOf(movement.transform))) return feet;
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
    private Canvas CreateHelpCanvas()
    {
        var go = new GameObject("Location Help", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        go.transform.SetParent(transform, false);
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
        go.transform.SetParent(transform, false);
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
    }
}
