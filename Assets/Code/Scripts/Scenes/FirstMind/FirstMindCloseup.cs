using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

internal sealed class FirstMindCloseup : IDisposable
{
    private readonly List<Material> materials = new List<Material>();
    private readonly Camera worldView;
    private readonly bool worldViewWasEnabled;
    private readonly int layer;
    private readonly float halfHeight;
    private readonly float halfWidth;
    public Transform Root { get; }
    public Camera Camera { get; }
    public int Layer => layer;

    public FirstMindCloseup(Camera worldView, Scene scene, Vector3 eye, Vector3 lookAt,
        float halfHeight, float halfWidth)
    {
        this.worldView = worldView;
        this.halfHeight = halfHeight;
        this.halfWidth = halfWidth;
        worldViewWasEnabled = worldView.enabled;
        layer = LayerMask.NameToLayer("FirstMindCloseup");
        if (layer < 0) layer = 30;
        Root = new GameObject("First Mind close-up").transform;
        SceneManager.MoveGameObjectToScene(Root.gameObject, scene);
        Root.position = new Vector3(0f, -128f, 0f);

        Camera = new GameObject("Close-up camera").AddComponent<Camera>();
        Camera.transform.SetParent(Root, false);
        Camera.transform.localPosition = eye;
        Camera.transform.localRotation = Quaternion.LookRotation(lookAt - eye);
        Camera.clearFlags = CameraClearFlags.SolidColor;
        Camera.backgroundColor = new Color(.095f, .085f, .070f);
        Camera.orthographic = true;
        Camera.nearClipPlane = .05f;
        Camera.farClipPlane = 24f;
        Camera.cullingMask = 1 << layer;
        Camera.depth = worldView.depth + 1;
        Camera.targetDisplay = worldView.targetDisplay;
        UpdateFraming();
        AddLight("Warm work light", new Vector3(-3, 4, -4), new Color(1f, .83f, .64f), 2.4f);
        AddLight("Fill light", new Vector3(3, 2.5f, -3), new Color(.71f, .83f, 1f), 1.25f);
        worldView.enabled = false;
    }

    public void UpdateFraming()
    {
        Camera.orthographicSize = Mathf.Max(halfHeight, halfWidth / Mathf.Max(.1f, Camera.aspect));
    }

    public Material Material(Color color, bool unlit = false)
    {
        var material = new Material(Shader.Find(unlit ? "Universal Render Pipeline/Unlit" :
            "Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        material.color = color;
        materials.Add(material);
        return material;
    }

    public GameObject Part(string name, PrimitiveType shape, Vector3 position, Vector3 scale,
        Material material, bool pickable = false, Transform parent = null)
    {
        var part = GameObject.CreatePrimitive(shape);
        part.name = name;
        part.layer = layer;
        part.transform.SetParent(parent != null ? parent : Root, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        part.GetComponent<Renderer>().sharedMaterial = material;
        var collider = part.GetComponent<Collider>();
        if (!pickable)
        {
            collider.enabled = false;
            Object.Destroy(collider);
        }
        return part;
    }

    public void Engrave(string text, Vector3 position, float size)
    {
        var label = new GameObject(text).AddComponent<TextMesh>();
        label.gameObject.layer = layer;
        label.transform.SetParent(Root, false);
        label.transform.localPosition = position;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.GetComponent<MeshRenderer>().sharedMaterial = label.font.material;
        label.text = text;
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.fontSize = 52;
        label.characterSize = size;
        label.color = new Color(1f, .88f, .69f);
    }

    public void ShowExistingRadio(MeshRenderer[] sources, Vector3 floorPosition)
    {
        var group = new GameObject("Original radio model").transform;
        group.SetParent(Root, false);
        Bounds bounds = sources[0].bounds;
        foreach (var source in sources) bounds.Encapsulate(source.bounds);

        foreach (var source in sources)
        {
            var original = source.GetComponent<MeshFilter>();
            if (original == null || original.sharedMesh == null || !source.enabled) continue;
            var part = new GameObject(source.name) { layer = layer };
            part.transform.SetParent(group, false);
            part.transform.localPosition = source.transform.position - bounds.center;
            part.transform.localRotation = source.transform.rotation;
            part.transform.localScale = source.transform.lossyScale;
            part.AddComponent<MeshFilter>().sharedMesh = original.sharedMesh;
            part.AddComponent<MeshRenderer>().sharedMaterials = source.sharedMaterials;
        }
        float scale = Mathf.Min(2.65f / Mathf.Max(.01f, bounds.size.x),
            1.18f / Mathf.Max(.01f, bounds.size.y), 1.1f / Mathf.Max(.01f, bounds.size.z));
        group.localScale = Vector3.one * scale;
        group.localPosition = floorPosition + Vector3.up * (bounds.extents.y * scale);
    }

    public LineRenderer Cable(string title, Material material, float thickness)
    {
        var cable = new GameObject(title).AddComponent<LineRenderer>();
        cable.gameObject.layer = layer;
        cable.transform.SetParent(Root, false);
        cable.useWorldSpace = false;
        cable.positionCount = 26;
        cable.widthMultiplier = thickness;
        cable.numCapVertices = 4;
        cable.numCornerVertices = 4;
        cable.sharedMaterial = material;
        return cable;
    }

    private void AddLight(string name, Vector3 position, Color color, float intensity)
    {
        var light = new GameObject(name).AddComponent<Light>();
        light.transform.SetParent(Root, false);
        light.transform.localPosition = position;
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = 12f;
        light.cullingMask = 1 << layer;
    }

    public void Dispose()
    {
        if (worldView != null) worldView.enabled = worldViewWasEnabled;
        if (Root != null)
        {
            Root.gameObject.SetActive(false);
            Object.Destroy(Root.gameObject);
        }
        foreach (var material in materials) Object.Destroy(material);
    }
}
