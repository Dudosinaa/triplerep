using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// Retained location presentation services for a manually placed player.
// Location travel belongs to LocationTransitionZone; mind travel belongs to SceneSwitch.
public sealed class MergedGameBootstrap : MonoBehaviour
{
    public string locationLabel;
    private void Start()
    {
        var movement = FindFirstObjectByType<FirstPersonPlayer>();
        // An empty location is valid. Do not create a player or disable its scene camera/audio.
        if (movement == null) return;
        foreach (var camera in FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            if (camera.transform.IsChildOf(movement.transform)) continue;
            camera.enabled = false;
            if (camera.CompareTag("MainCamera")) camera.tag = "Untagged";
        }
        foreach (var listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
            if (!listener.transform.IsChildOf(movement.transform)) listener.enabled = false;
        var playerCamera = movement.GetComponentInChildren<Camera>();
        if (playerCamera != null) playerCamera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        CreateHelpCanvas();
        Debug.Log("MERGE_GAMEPLAY_READY: " + locationLabel);
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
        var controls = locationLabel + "  |  WASD — идти, Ctrl — присесть, E — взаимодействие, T — сознание, ПКМ — приблизить";
        if (gameObject.scene.path == TrainMindSession.TrainPath)
            controls += "\nЛКМ — выстрел, R — перезарядка, Q — оружие, Y — покурить";
        MakeText(go.transform, "Controls", controls,
            new Vector2(0.02f, 0.87f), new Vector2(0.98f, 0.98f));
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
}
