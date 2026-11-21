using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public static partial class FinalLabProps
{
    internal const string RootName = "FinalLab_Props";
    private const string Lab = FinalLabPropsSurvey.Lab;
    private const string Furniture = Lab + "/tables + etc/";
    private const string Machines = Lab + "/Machinery/";
    private const string ImportedCable = Lab + "/Cables/";
    private const string Glassware = "Assets/FreeLabAssets/Prefabs/";
    private const string Hospital = FinalLabPropsSurvey.Hospital;
    private const string Materials = Lab + "/Materials/";
    private const string MeshPath = Lab + "/FinalLab_PropGeometry.asset";
    private static readonly List<string> Report = new();
    private static Dictionary<string, Mesh> meshes;
    private static int bakedCables;

    public static void Build()
    {
        var scene = EditorSceneManager.OpenScene(FinalLabBuilder.ScenePath);
        var architecture = GameObject.Find(FinalLabBuilder.RootName).transform;
        Physics.SyncTransforms();
        var layout = new Layout(architecture);
        Report.Clear();
        bakedCables = 0;
        meshes = AssetDatabase.LoadAllAssetsAtPath(MeshPath).OfType<Mesh>().ToDictionary(m => m.name);
        var rootObject = GameObject.Find(RootName);
        var root = (rootObject == null ? new GameObject(RootName) : rootObject).transform;
        var sceneProps = new Dressing(root, layout);
        sceneProps.Place();
        sceneProps.Wire();
        Physics.SyncTransforms();
        Report.Add($"Baked {bakedCables} static routes using CableProceduralMultipoint.");
        Validate(root, layout, Report);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("FinalScene did not save.");
        Directory.CreateDirectory(FinalLabPropsSurvey.Logs);
        File.WriteAllLines(Path.Combine(FinalLabPropsSurvey.Logs, "placement.txt"), Report);
        Debug.Log("FINAL_LAB_PROPS_SAVED " + Report.Count);
    }

    public static void ValidateSaved()
    {
        EditorSceneManager.OpenScene(FinalLabBuilder.ScenePath);
        Physics.SyncTransforms();
        var sceneRoot = GameObject.Find(FinalLabBuilder.RootName).transform;
        var report = new List<string>();
        Validate(GameObject.Find(RootName).transform, new Layout(sceneRoot), report);
        Directory.CreateDirectory(FinalLabPropsSurvey.Logs);
        File.WriteAllLines(Path.Combine(FinalLabPropsSurvey.Logs, "batch-checks.txt"), report);
        Debug.Log("FINAL_LAB_PROPS_VALIDATED\n" + string.Join("\n", report));
    }

    private sealed class Layout
    {
        public readonly Transform Architecture;
        public readonly Room Archive, Hall, Control, Containment;
        public readonly Bounds ArchiveDoor, ControlDoor, ContainmentDoor, ControlWindow, ContainmentWindow;
        public readonly Bounds NorthBeam;

        public Layout(Transform architecture)
        {
            Architecture = architecture;
            Archive = new Room(architecture.Find("02_Archive_Corridor"), "Archive", "Walls/ArchiveWest", "Walls/ArchiveEast",
                Bound("02_Archive_Corridor/ArchiveFloor").min.z, Bound("02_Archive_Corridor/EntranceOpening/ArchiveLintel").min.z,
                "ArchiveCeiling");
            Hall = new Room(architecture.Find("03_Main_Laboratory"), "Main", "Walls/HallWest", "Walls/HallEast",
                Bound("03_Main_Laboratory/Walls/HallEntryWest").max.z, Bound("03_Main_Laboratory/Walls/RearPartitionPier").min.z,
                "Ceiling/HallSlab");
            Control = SideRoom("04_Ivan_ControlRoom", "Control");
            Containment = SideRoom("05_Father_Containment", "Containment");
            ArchiveDoor = Bound("02_Archive_Corridor/EntranceOpening/ArchiveLintel");
            ControlDoor = Bound("04_Ivan_ControlRoom/HallPartition/DoorOpening/ControlPartition_DoorOpening_Lintel");
            ContainmentDoor = Bound("05_Father_Containment/HallPartition/DoorOpening/ContainmentPartition_DoorOpening_Lintel");
            ControlWindow = Bound("04_Ivan_ControlRoom/HallPartition/ObservationWindowOpening/ControlPartition_ObservationWindowOpening_Sill");
            ContainmentWindow = Bound("05_Father_Containment/HallPartition/ObservationWindowOpening/ContainmentPartition_ObservationWindowOpening_Sill");
            NorthBeam = Bound("03_Main_Laboratory/Ceiling/HallCrossbeamNorth");
        }

        private Room SideRoom(string zone, string prefix) => new(Architecture.Find(zone), prefix,
            "Walls/" + prefix + "West", "Walls/" + prefix + "East",
            Bound(zone + "/" + prefix + "Floor").min.z, Bound(zone + "/Walls/" + prefix + "Back").min.z,
            prefix + "Ceiling");

        private Bounds Bound(string path) => Architecture.Find(path).GetComponent<BoxCollider>().bounds;
    }

    private readonly struct Room
    {
        public readonly float Left, Right, Near, Far, Floor, Ceiling;
        public float Center => (Left + Right) * 0.5f;

        public Room(Transform root, string prefix, string west, string east, float near, float far, string ceiling)
        {
            Left = root.Find(west).GetComponent<BoxCollider>().bounds.max.x;
            Right = root.Find(east).GetComponent<BoxCollider>().bounds.min.x;
            Near = near;
            Far = far;
            Floor = root.Find(prefix + "Floor").GetComponent<BoxCollider>().bounds.max.y;
            Ceiling = root.Find(ceiling).GetComponent<BoxCollider>().bounds.min.y;
        }
    }

    private static Transform Group(Transform parent, string name)
    {
        var item = parent.Find(name);
        if (item != null) return item;
        item = new GameObject(name).transform;
        item.SetParent(parent, false);
        return item;
    }

    private static Transform Mark(Transform parent, string name, Vector3 position)
    {
        var item = Group(parent, name);
        item.position = position;
        return item;
    }

    private sealed class Prop
    {
        public readonly Transform Fit, Model;
        public readonly MeshRenderer[] Renderers;
        public Bounds Bounds => FinalLabPropsSurvey.Bounds(Renderers.Where(r => r.enabled));
        public Prop(Transform fit, Transform model)
        {
            Fit = fit;
            Model = model;
            Renderers = model.GetComponentsInChildren<MeshRenderer>();
            if (Renderers.Length == 0) throw new InvalidOperationException($"Missing renderers on {fit.name}.");
        }
    }

    private static Prop Place(Transform group, string name, string path, Vector3 point, Vector3 rotation,
        float height = 0, float width = 0, float depth = 0, Func<MeshRenderer, bool> selection = null, bool exactBounds = false)
    {
        var fit = Group(group, name);
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null) throw new InvalidOperationException("Missing imported asset " + path);
        var model = fit.Find("Model");
        if (model == null)
        {
            model = ((GameObject)PrefabUtility.InstantiatePrefab(asset, fit)).transform;
            model.name = "Model";
        }
        if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(model.gameObject) != path)
            throw new InvalidOperationException($"{name} is linked to the wrong source asset.");

        foreach (var collider in model.GetComponentsInChildren<Collider>()) collider.enabled = false;
        foreach (var light in model.GetComponentsInChildren<Light>()) light.enabled = false;
        foreach (var animator in model.GetComponentsInChildren<Animator>()) animator.enabled = false;
        foreach (var animation in model.GetComponentsInChildren<Animation>()) animation.enabled = false;
        fit.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(rotation));
        fit.localScale = Vector3.one;
        var prop = new Prop(fit, model);
        if (selection != null)
            foreach (var renderer in prop.Renderers) renderer.enabled = selection(renderer);
        var source = exactBounds ? GeometryBounds(prop) : prop.Bounds;
        float scale = 0;
        if (height > 0) scale = height / source.size.y;
        if (width > 0) scale = scale > 0 ? Mathf.Min(scale, width / source.size.x) : width / source.size.x;
        if (depth > 0) scale = scale > 0 ? Mathf.Min(scale, depth / source.size.z) : depth / source.size.z;
        fit.localScale = Vector3.one * (scale > 0 ? scale : 1);
        var fitted = exactBounds ? GeometryBounds(prop) : prop.Bounds;
        fit.position += point - new Vector3(fitted.center.x, fitted.min.y, fitted.center.z);
        Report.Add($"{name} | {Path.GetFileName(path)} | scale {fit.localScale.x:F4} | {fitted.size:F3} m at {point:F3}");
        return prop;
    }

    private static Bounds GeometryBounds(Prop prop)
    {
        Vector3 min = Vector3.one * float.PositiveInfinity;
        Vector3 max = Vector3.one * float.NegativeInfinity;
        foreach (var renderer in prop.Renderers.Where(r => r.enabled))
        {
            var filter = renderer.GetComponent<MeshFilter>();
            Matrix4x4 world = filter.transform.localToWorldMatrix;
            foreach (Vector3 local in filter.sharedMesh.vertices)
            {
                Vector3 vertex = world.MultiplyPoint3x4(local);
                min = Vector3.Min(min, vertex);
                max = Vector3.Max(max, vertex);
            }
        }
        return new Bounds((min + max) * 0.5f, max - min);
    }

    private static Vector3 Surface(Prop prop, float x, float z, float minimumY = float.NegativeInfinity,
        float maximumY = float.PositiveInfinity)
    {
        float best = float.NegativeInfinity;
        foreach (var r in prop.Renderers.Where(r => r.enabled))
        {
            var bound = r.bounds;
            if (x < bound.min.x || x > bound.max.x || z < bound.min.z || z > bound.max.z) continue;
            var mesh = r.GetComponent<MeshFilter>().sharedMesh;
            var points = mesh.vertices.Select(r.transform.TransformPoint).ToArray();
            var indices = mesh.triangles;
            for (int i = 0; i < indices.Length; i += 3)
            {
                Vector3 a = points[indices[i]], b = points[indices[i + 1]], c = points[indices[i + 2]];
                var ab = b - a;
                var ac = c - a;
                float cross = ab.x * ac.z - ab.z * ac.x;
                if (Mathf.Abs(cross) < 0.0000001f) continue;
                float u = ((x - a.x) * ac.z - (z - a.z) * ac.x) / cross;
                float v = (ab.x * (z - a.z) - ab.z * (x - a.x)) / cross;
                if (u < -0.0001f || v < -0.0001f || u + v > 1.0001f) continue;
                float y = a.y + u * ab.y + v * ac.y;
                if (y >= minimumY && y <= maximumY) best = Mathf.Max(best, y);
            }
        }
        if (float.IsNegativeInfinity(best))
            throw new InvalidOperationException($"No support on {prop.Fit.name} at ({x:F3}, {z:F3}).");
        return new Vector3(x, best + 0.001f, z);
    }

    private static void Collider(Prop prop, bool tabletop = false)
    {
        var anchor = Mark(prop.Fit, "Furniture_Collision", Vector3.zero);
        anchor.rotation = Quaternion.identity;
        if (!anchor.TryGetComponent<BoxCollider>(out var collider)) collider = anchor.gameObject.AddComponent<BoxCollider>();
        var bounds = prop.Bounds;
        if (tabletop)
        {
            bounds.size = new Vector3(bounds.size.x, 0.07f, bounds.size.z);
            bounds.center = new Vector3(bounds.center.x, prop.Bounds.max.y - 0.035f, bounds.center.z);
        }
        anchor.position = bounds.center;
        collider.size = (bounds.size - new Vector3(0.012f, 0, 0.012f)) / prop.Fit.lossyScale.x;
    }

    private static Material Lit(string name, Color color, float metallic, float smoothness, Material source = null)
    {
        string path = Materials + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Metallic", metallic);
        m.SetFloat("_Smoothness", smoothness);
        if (source != null)
        {
            foreach (var property in new[] { "_BaseMap", "_MainTex" })
                if (source.HasProperty(property) && source.GetTexture(property) != null)
                {
                    m.SetTexture("_BaseMap", source.GetTexture(property));
                    break;
                }
            if (source.HasProperty("_BumpMap") && source.GetTexture("_BumpMap") != null)
            {
                m.SetTexture("_BumpMap", source.GetTexture("_BumpMap"));
                m.EnableKeyword("_NORMALMAP");
            }
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    private static Material Transparent(string name, Color color, float smoothness)
    {
        var m = Lit(name, color, 0, smoothness);
        m.SetFloat("_Surface", 1);
        m.SetFloat("_Blend", 0);
        m.SetFloat("_Cull", (float)CullMode.Back);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0);
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = (int)RenderQueue.Transparent;
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        return m;
    }

    private static Mesh StoreMesh(Mesh mesh)
    {
        if (meshes.TryGetValue(mesh.name, out var previous))
        {
            EditorUtility.CopySerialized(mesh, previous);
            Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(previous);
            return previous;
        }
        if (meshes.Count == 0) AssetDatabase.CreateAsset(mesh, MeshPath);
        else AssetDatabase.AddObjectToAsset(mesh, MeshPath);
        meshes.Add(mesh.name, mesh);
        return mesh;
    }
}
