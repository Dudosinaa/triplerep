using System;
using System.Linq;
using FinalLab.Investigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public static partial class FinalLabPortalBuilder
{
    internal const string ControlRoom = "04_Ivan_ControlRoom";
    internal const string FatherRoom = "05_Father_Containment";
    internal const string ControlGlass = "FinalLab_ControlRoom_TintedGlass";
    internal const string FatherGlass = "FinalLab_FatherRoom_ClearGlass";
    internal const string ControlDoorSteel = "FinalLab_ControlRoom_DoorSteel";
    private const string Materials = FinalLabEvidenceAssets.Root + "/Materials/";

    internal readonly struct Aperture
    {
        public readonly float Left, Right, Bottom, Top, Hall, Room;
        public Aperture(float left, float right, float bottom, float top, float hall, float room)
        {
            Left = left; Right = right; Bottom = bottom; Top = top; Hall = hall; Room = room;
        }
        public float Width => Right - Left;
        public float Height => Top - Bottom;
        public float X => (Left + Right) * 0.5f;
    }

    public static void Apply()
    {
        FinalLabInvestigationSurvey.RequireBatch();
        var scene = EditorSceneManager.OpenScene(FinalLabBuilder.ScenePath);
        var roots = scene.GetRootGameObjects();
        var architecture = roots.Single(o => o.name == "FinalLab_Environment").transform;
        var investigation = roots.Single(o => o.name == FinalLabInvestigationBuilder.RootName)
            .GetComponent<FinalLabInvestigationManager>();
        var slots = new SerializedObject(investigation);
        var points = slots.FindProperty("points");
        var ordered = new[] { "Archive", "Father", "Ivan" }
            .Select(id => investigation.Points.Single(point => point.Id == id)).ToArray();
        foreach (var point in investigation.Points.Where(point => point.Container)) point.SetHighlighted(false);
        points.arraySize = ordered.Length;
        for (int i = 0; i < ordered.Length; i++) points.GetArrayElementAtIndex(i).objectReferenceValue = ordered[i];
        slots.ApplyModifiedPropertiesWithoutUndo();

        var saved = roots.SingleOrDefault(o => o.name == RootName);
        if (saved != null) Object.DestroyImmediate(saved);
        var installation = new GameObject(RootName).transform;
        var metal = FrameMaterial();
        var controlDoor = ReadOpening(architecture, ControlRoom, "DoorOpening");
        var fatherDoor = ReadOpening(architecture, FatherRoom, "DoorOpening");
        FitControlDoor(installation, controlDoor, metal);
        FitFatherDoor(installation, fatherDoor, metal);
        RepairDoorPair(architecture, installation);
        if (architecture.Find(ControlRoom + "/HallPartition/ObservationWindowOpening") != null)
            FitWindow(installation, "ControlRoom_Observation", ReadOpening(architecture, ControlRoom, "ObservationWindowOpening"),
                true, metal, MakeGlass(ControlGlass, true));
        var fatherWindow = ReadOpening(architecture, FatherRoom, "ObservationWindowOpening");
        FitWindow(installation, "FatherRoom_Observation", fatherWindow, false, metal, MakeGlass(FatherGlass, false));
        Physics.SyncTransforms();
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("FinalScene could not be saved.");
        Debug.Log("FINAL_LAB_PORTALS_SAVED");
    }

    internal static Aperture ReadOpening(Transform architecture, string room, string name)
    {
        var opening = architecture.Find(room + "/HallPartition/" + name);
        var rails = opening.GetComponentsInChildren<MeshRenderer>();
        Bounds Read(string suffix) => rails.Single(r => r.name.EndsWith(suffix, StringComparison.Ordinal)).bounds;
        var lintel = Read("_Lintel");
        var left = Read("_LinerLeft");
        var right = Read("_LinerRight");
        var top = Read("_LinerTop");
        float bottom = name == "DoorOpening" ? left.min.y : Read("_LinerSill").max.y;
        return new Aperture(left.max.x, right.min.x, bottom, top.min.y, lintel.min.z, lintel.max.z);
    }

    private static void FitControlDoor(Transform installation, Aperture opening, Material frame)
    {
        var door = Group(installation, "ControlRoom_MetalDoor", new Vector3(opening.X, opening.Bottom, opening.Room));
        var leaf = Model(ControlDoorPath, door, "Imported leaf");
        var renderers = leaf.GetComponentsInChildren<MeshRenderer>().OrderByDescending(r => r.bounds.size.y).ToArray();
        var main = renderers[0];
        var steel = ControlDoorMaterial();
        foreach (var renderer in renderers) renderer.sharedMaterial = renderer == main ? steel : frame;
        var original = main.bounds;
        float scale = (opening.Height - 0.021f) / original.size.y;
        float bodyWidth = original.size.x * scale;
        float roomZ = opening.Room + 0.035f;
        leaf.localScale = Vector3.one * scale;
        Bounds fitted = main.bounds;
        leaf.position += new Vector3(opening.X - fitted.center.x, opening.Bottom + 0.011f - fitted.min.y, roomZ - fitted.center.z);
        Bounds body = main.bounds;
        float leftGap = body.min.x - opening.Left;
        float rightGap = opening.Right - body.max.x;
        const float swingGap = 0.011f;
        Cube(door, "Left steel jamb", new Vector3(opening.Left + (leftGap - swingGap) / 2, opening.Bottom + opening.Height / 2, opening.Room - 0.130f),
            new Vector3(leftGap - swingGap, opening.Height, 0.24f), frame);
        Cube(door, "Right steel jamb", new Vector3(opening.Right - (rightGap - swingGap) / 2, opening.Bottom + opening.Height / 2, opening.Room - 0.130f),
            new Vector3(rightGap - swingGap, opening.Height, 0.24f), frame);
        Cube(door, "Head seal", new Vector3(opening.X, opening.Top - 0.010f, opening.Room - 0.080f),
            new Vector3(bodyWidth + swingGap * 2, 0.020f, 0.10f), frame);
        AddDoorMotion(door, leaf, body, opening, 90, 1f, 0.008f);
    }

    private static void FitFatherDoor(Transform installation, Aperture opening, Material frameMaterial)
    {
        var door = Group(installation, "FatherRoom_HeavyMetalDoor", new Vector3(opening.X, opening.Bottom, opening.Room));
        var frame = Model(FatherDoorPath, door, "Imported stationary frame");
        var staticRenderer = frame.GetComponentsInChildren<MeshRenderer>().Single(r => r.transform.parent.name == "f_2");
        foreach (var renderer in frame.GetComponentsInChildren<MeshRenderer>())
            if (renderer != staticRenderer) renderer.enabled = false;
        Bounds original = staticRenderer.bounds;
        var scale = new Vector3(opening.Width / original.size.x, opening.Height / original.size.y, opening.Width / original.size.x);
        frame.localScale = scale;
        Bounds sized = staticRenderer.bounds;
        frame.position += new Vector3(opening.X - sized.center.x, opening.Bottom - sized.min.y,
            (opening.Room - 0.067f) - sized.center.z);
        var leaf = Model(FatherDoorPath, door, "Imported closed leaf, lock and handle");
        var leafRenderer = leaf.GetComponentsInChildren<MeshRenderer>().Single(r => r.transform.parent.name == "door_2");
        foreach (var renderer in leaf.GetComponentsInChildren<MeshRenderer>())
            if (!renderer.transform.IsChildOf(leafRenderer.transform.parent)) renderer.enabled = false;
        leaf.localScale = scale;
        // Match the native frame's X/Y placement while the leaf clears its indoor lip during the swing.
        leaf.position = frame.position + Vector3.forward * 0.13f;
        Bounds body = leafRenderer.bounds;
        float leftWidth = body.min.x - opening.Left;
        float rightWidth = opening.Right - body.max.x;
        const float railGap = 0.008f;
        Cube(door, "Mounting plate left", new Vector3(opening.Left + (leftWidth - railGap) / 2, (opening.Top + opening.Bottom) / 2, opening.Room - 0.065f),
            new Vector3(leftWidth - railGap, opening.Height, 0.135f), frameMaterial);
        Cube(door, "Mounting plate right", new Vector3(opening.Right - (rightWidth - railGap) / 2, (opening.Top + opening.Bottom) / 2, opening.Room - 0.065f),
            new Vector3(rightWidth - railGap, opening.Height, 0.135f), frameMaterial);
        Cube(door, "Bottom threshold", new Vector3(opening.X, opening.Bottom + 0.015f, opening.Room - 0.018f),
            new Vector3(opening.Width, 0.03f, 0.17f), frameMaterial);
        float head = opening.Top - body.max.y;
        Cube(door, "Head backing plate", new Vector3(opening.X, opening.Top - head / 2, opening.Room - 0.105f),
            new Vector3(opening.Width, head, 0.13f), frameMaterial);
        AddDoorMotion(door, leaf, body, opening, 90, 1.75f, 0.039f);
    }

    private static void AddDoorMotion(Transform node, Transform model, Bounds body, Aperture opening,
        float angle, float duration, float hingeInset)
    {
        var pivot = Group(node, "Right hinge axis", new Vector3(body.max.x - hingeInset, body.min.y, body.center.z));
        model.SetParent(pivot, true);
        var collider = pivot.gameObject.AddComponent<BoxCollider>();
        collider.center = pivot.InverseTransformPoint(body.center);
        collider.size = body.size;
        var rigidbody = pivot.gameObject.AddComponent<Rigidbody>();
        rigidbody.isKinematic = true;
        rigidbody.useGravity = false;
        var focus = Group(node, "Open doorway interaction", new Vector3(opening.X, opening.Bottom + opening.Height * 0.5f, opening.Room + 0.018f));
        var trigger = focus.gameObject.AddComponent<BoxCollider>();
        trigger.size = new Vector3(body.size.x, opening.Height * 0.94f, 0.042f);
        trigger.isTrigger = true;
        trigger.enabled = false;
        node.gameObject.AddComponent<FinalLabMetalDoor>().Configure(pivot, collider, trigger, angle, duration);
        Debug.Log($"{node.name} leaf {BoundsText(body)}, hinge {pivot.position:F3}");
    }

    private static Transform Model(string path, Transform parent, string name)
    {
        var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), parent);
        var holder = Group(parent, name, Vector3.zero);
        holder.localPosition = Vector3.zero;
        model.transform.SetParent(holder, true);
        foreach (var collider in model.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        return holder;
    }

    private static void FitWindow(Transform parent, string name, Aperture opening, bool doubleBay, Material frame, Material pane)
    {
        float z = (opening.Room + opening.Hall) * 0.5f;
        var window = Group(parent, name, new Vector3(opening.X, opening.Bottom, z));
        const float rail = 0.047f;
        const float depth = 0.107f;
        float middle = opening.Bottom + opening.Height / 2;
        Cube(window, "Left steel retainer", new Vector3(opening.Left + rail / 2, middle, z), new Vector3(rail, opening.Height, depth), frame);
        Cube(window, "Right steel retainer", new Vector3(opening.Right - rail / 2, middle, z), new Vector3(rail, opening.Height, depth), frame);
        Cube(window, "Upper steel retainer", new Vector3(opening.X, opening.Top - rail / 2, z), new Vector3(opening.Width - rail * 2, rail, depth), frame);
        Cube(window, "Lower steel retainer", new Vector3(opening.X, opening.Bottom + rail / 2, z), new Vector3(opening.Width - rail * 2, rail, depth), frame);
        float clearHeight = opening.Height - 2 * rail;
        if (doubleBay)
        {
            const float mullion = 0.066f;
            Cube(window, "Central steel mullion", new Vector3(opening.X, middle, z),
                new Vector3(mullion, clearHeight, depth), frame);
            float paneWidth = (opening.Width - 2 * rail - mullion) / 2;
            float offset = (mullion + paneWidth) / 2;
            MakePane(window, "Left smoke glass", new Vector3(opening.X - offset, middle, z), paneWidth, clearHeight, 0.014f, pane);
            MakePane(window, "Right smoke glass", new Vector3(opening.X + offset, middle, z), paneWidth, clearHeight, 0.014f, pane);
        }
        else
        {
            MakePane(window, "Clear protective glass", new Vector3(opening.X, middle, z), opening.Width - 2 * rail, clearHeight, 0.036f, pane);
        }
    }

    private static void MakePane(Transform window, string name, Vector3 center, float width, float height, float thickness, Material material)
    {
        var pane = GameObject.CreatePrimitive(PrimitiveType.Quad);
        pane.name = name;
        pane.transform.SetParent(window, false);
        pane.transform.position = center;
        pane.transform.localScale = new Vector3(width + 0.004f, height + 0.004f, 1);
        Object.DestroyImmediate(pane.GetComponent<MeshCollider>());
        var box = pane.AddComponent<BoxCollider>();
        box.size = new Vector3(1, 1, thickness);
        var renderer = pane.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private static Material FrameMaterial()
    {
        var frame = Asset("FinalLab_Portal_OldSteel");
        frame.SetColor("_BaseColor", new Color(0.16f, 0.167f, 0.164f));
        frame.SetFloat("_Metallic", 0.27f);
        frame.SetFloat("_Smoothness", 0.33f);
        EditorUtility.SetDirty(frame);
        return frame;
    }

    private static Material ControlDoorMaterial()
    {
        const string texturePath = "Assets/Art/Models/Authoring/Lighter/textures/TexturesCom_MetalSheet_Aged_1K_albedo.png";
        var surface = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (surface == null) throw new InvalidOperationException($"Missing aged steel texture at {texturePath}.");
        var material = Asset(ControlDoorSteel);
        material.SetTexture("_BaseMap", surface);
        material.SetColor("_BaseColor", new Color(0.46f, 0.48f, 0.48f));
        material.SetFloat("_Metallic", 0.36f);
        material.SetFloat("_Smoothness", 0.41f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material MakeGlass(string name, bool tinted)
    {
        var material = Asset(name);
        material.SetOverrideTag("RenderType", "Transparent");
        material.SetColor("_BaseColor", tinted ? new Color(0.22f, 0.265f, 0.29f, 0.64f) : new Color(0.68f, 0.72f, 0.735f, 0.17f));
        material.SetTexture("_BaseMap", GlazingGrain());
        material.SetFloat("_Metallic", tinted ? 0.015f : 0.01f);
        material.SetFloat("_Smoothness", tinted ? 0.63f : 0.72f);
        material.SetFloat("_Surface", 1);
        material.SetFloat("_Blend", 0);
        material.SetFloat("_ZWrite", 0);
        material.SetFloat("_Cull", (float)CullMode.Off);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.SetShaderPassEnabled("ShadowCaster", false);
        material.SetShaderPassEnabled("DepthOnly", false);
        material.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Texture2D GlazingGrain()
    {
        const string path = FinalLabEvidenceAssets.Textures + "FinalLab_Glazing_Dust.asset";
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture != null) return texture;
        const int resolution = 192;
        texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, true) { name = "Old glazing dust" };
        var pixels = new Color32[resolution * resolution];
        for (int y = 0; y < resolution; y++)
        for (int x = 0; x < resolution; x++)
        {
            float u = x / (float)resolution, v = y / (float)resolution;
            float edge = Mathf.Min(Mathf.Min(u, 1 - u), Mathf.Min(v, 1 - v));
            float clouds = Mathf.PerlinNoise(u * 7.2f, v * 13.4f);
            float dust = Mathf.PerlinNoise(u * 94, v * 94) * Mathf.PerlinNoise(u * 18.2f, v * 17.3f);
            byte tone = (byte)(219 + 23 * clouds + 10 * dust);
            byte alpha = (byte)Mathf.Clamp(194 + 45 * clouds + 8 * dust + 12 * Mathf.Exp(-edge * 23), 0, 255);
            pixels[y * resolution + x] = new Color32(tone, tone, tone, alpha);
        }
        texture.SetPixels32(pixels);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.Apply();
        AssetDatabase.CreateAsset(texture, path);
        return texture;
    }

    private static Material Asset(string name)
    {
        string path = Materials + name + ".mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static Transform Group(Transform parent, string name, Vector3 world)
    {
        var group = new GameObject(name).transform;
        group.SetParent(parent, false);
        group.position = world;
        return group;
    }

    private static void Cube(Transform parent, string name, Vector3 position, Vector3 size, Material material)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent, false);
        cube.transform.position = position;
        cube.transform.localScale = size;
        cube.GetComponent<MeshRenderer>().sharedMaterial = material;
    }
}
