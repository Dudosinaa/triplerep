using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public sealed partial class FirstMindInteriorBuilder
{
    internal const string ScenePath = "Assets/Level/Scenes/Locations/MindsScenes/FirstMind.unity";
    private const string Data = "Assets/Level/Scenes/Locations/MindsScenes/FirstMindInteriorData";
    private const string Models = "Assets/Art/Models/Environment/FirstMind/";
    private const float Scale = 1.15f;
    private const float Ceiling = 2.54f;
    private const float Left = -5.114f;
    private const float Right = 1.090f;
    private const float Rear = -3.825f;
    private const float NotchX = -3.515f;
    private const float NotchZ = 0.081f;
    private const float Front = 1.881f;
    private const float BathFront = -2.345f;
    private const float KitchenFront = -1.825f;
    private const float SplitX = -2.640f;
    private const float BathDoorX = -3.575f;
    private const float KitchenDoorX = 0.430f;
    private const float DoorGap = 0.99f;
    private const float DoorHead = 2.11f;
    private const float WallpaperRepeatMeters = 1.17f;

    private Scene scene;
    private Transform house;
    private Transform model;
    private Transform structure;
    private Transform living;
    private Transform kitchen;
    private Transform bath;
    private Transform doors;
    private Transform lights;
    private float floorY;
    private Material wood;
    private Material trim;
    private Material cream;
    private Material green;
    private Material tile;
    private Material metal;
    private Material ceiling;
    private Material opal;
    private AudioClip openSound;
    private AudioClip closeSound;
    private AudioClip latchSound;
    private readonly List<(string name, Bounds bounds)> supportedItems = new();

    public static void BuildBatch()
    {
        try
        {
            new FirstMindInteriorBuilder().Build();
            FirstMindInteriorCheck.Begin();
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            FirstMindInteriorCheck.End(1);
        }
    }

    private void Build()
    {
        scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        house = scene.GetRootGameObjects().Single(go => go.name == "House").transform;
        Check(house.Find("Interior") == null, "Open the original forest scene to author the interior");
        if (!AssetDatabase.IsValidFolder(Data))
            AssetDatabase.CreateFolder(Path.GetDirectoryName(Data).Replace('\\', '/'), Path.GetFileName(Data));
        model = house.GetChild(0);
        MeshRenderer frontDoor = Part("_Door1_");
        Vector3 entranceBefore = frontDoor.bounds.center;
        model.localScale *= Scale;
        Vector3 entryShift = entranceBefore - frontDoor.bounds.center;
        model.position += new Vector3(entryShift.x, 0f, entryShift.z);
        PrefabUtility.RecordPrefabInstancePropertyModifications(model);
        Check(Vector2.Distance(XZ(entranceBefore), XZ(frontDoor.bounds.center)) < 0.002f, "The entrance shifted away from the path");

        MeshRenderer floor = Part("_Pol_");
        MeshRenderer shell = Part("_Stena_");
        MeshRenderer doorSample = Part("Box004_Door2");
        MeshRenderer[] windows = Parts().Where(part => NameOf(part).Contains("_Okno")).ToArray();
        floorY = floor.bounds.max.y;
        structure = Group("Structure", house);
        var interior = Group("Interior", house);
        living = Group("LivingRoom", interior);
        kitchen = Group("Kitchen", interior);
        bath = Group("Bathroom", interior);
        doors = Group("Doors", house);
        lights = Group("Lighting", house);
        MakeMaterials();
        var originalDoor = Bake(frontDoor, Matrix4x4.identity);
        var indoorDoor = Bake(doorSample, Matrix4x4.identity);
        PrepareShell(shell, floor);
        BuildPartitions();
        BuildDoors(originalDoor, indoorDoor);
        FurnishLiving();
        FurnishKitchen();
        FurnishBathroom();
        DressWindows(windows);
        AddHatch();
        LightRooms();
        SetupPlayer();
        ValidateAuthoredHouse();

        Vector3 exterior = Combined(model).size;
        AssetDatabase.SaveAssets();
        Check(EditorSceneManager.SaveScene(scene), "Unable to save the furnished FirstMind scene");
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Check(Object.FindObjectsByType<HouseDoor>(FindObjectsSortMode.None).Length == 3, "Doors did not persist after reloading");
        Debug.Log($"FIRST_MIND_INTERIOR_SAVED floor={floorY:F3} scale={Scale:F2} roof bounds={exterior}");
    }

    private void PrepareShell(MeshRenderer shell, MeshRenderer floor)
    {
        MeshFilter filter = shell.GetComponent<MeshFilter>();
        Mesh source = filter.sharedMesh;
        Mesh copy = Object.Instantiate(source);
        copy.name = "Exterior shell with original openings";
        Vector3[] vertices = source.vertices;
        var keep = new List<int>();
        // glTFast may keep the Sketchfab root rotation. The measured plan is in upright scene axes.
        var matrix = Matrix4x4.Scale(Vector3.one / Scale) * Matrix4x4.Translate(-model.position) *
            filter.transform.localToWorldMatrix;
        int[] indices = source.GetTriangles(0);
        for (int i = 0; i < indices.Length; i += 3)
        {
            int a = indices[i], b = indices[i + 1], c = indices[i + 2];
            if (!ShellTriangle(matrix.MultiplyPoint3x4(vertices[a]),
                matrix.MultiplyPoint3x4(vertices[b]), matrix.MultiplyPoint3x4(vertices[c]))) continue;
            keep.Add(a);
            keep.Add(b);
            keep.Add(c);
        }
        Check(keep.Count >= 700 && keep.Count < source.GetIndexCount(0), "Unable to isolate the exterior wall triangles");
        copy.SetTriangles(keep, 0);
        copy.RecalculateBounds();
        Mesh saved = Store(copy, "Adapted Exterior Walls.asset");
        var meshCollider = shell.GetComponent<MeshCollider>();
        filter.sharedMesh = saved;
        meshCollider.sharedMesh = saved;
        PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
        PrefabUtility.RecordPrefabInstancePropertyModifications(meshCollider);

        foreach (MeshRenderer part in Parts().Where(part => NameOf(part).Contains("_Door") || NameOf(part).Contains("_Korob2_")))
        {
            part.gameObject.SetActive(false);
            PrefabUtility.RecordPrefabInstancePropertyModifications(part.gameObject);
        }
        var woodClips = Enumerable.Range(1, 8).Select(i =>
            Load<AudioClip>($"Assets/Footsteps - Essentials/Footsteps_Wood/Footsteps_Wood_Walk/Footsteps_Wood_Walk_{i:00}.wav")).ToArray();
        floor.gameObject.AddComponent<HouseFootstepSurface>().Configure(woodClips);
        Floorboards("Main pine boards", NotchX, Right, Rear, Front);
        Floorboards("Entrance pine boards", Left, NotchX, Rear, NotchZ);
        Panel("Main timber ceiling", structure, NotchX, Right, Rear, Front, Ceiling, ceiling, true);
        Panel("West timber ceiling", structure, Left, NotchX, Rear, NotchZ, Ceiling, ceiling, true);
    }

    private static bool ShellTriangle(Vector3 a, Vector3 b, Vector3 c)
    {
        return Mathf.Min(a.y, b.y, c.y) >= 2.79f ||
            InBand(a.x, b.x, c.x, -5.37f, -5.07f) ||
            InBand(a.x, b.x, c.x, 1.04f, 1.43f) ||
            InBand(a.z, b.z, c.z, -4.09f, -3.78f) ||
            (InBand(a.z, b.z, c.z, 1.83f, 2.14f) && Mathf.Min(a.x, b.x, c.x) >= -3.86f) ||
            (InBand(a.z, b.z, c.z, 0.03f, 0.35f) && Mathf.Max(a.x, b.x, c.x) <= -3.46f) ||
            (InBand(a.x, b.x, c.x, -3.86f, -3.46f) && Mathf.Min(a.z, b.z, c.z) >= 0.035f);
    }

    private static bool InBand(float a, float b, float c, float min, float max) =>
        Mathf.Min(a, b, c) >= min && Mathf.Max(a, b, c) <= max;

    private void BuildPartitions()
    {
        SplitWall("Bathroom front", Left, SplitX, BathFront, BathDoorX, cream);
        SplitWall("Kitchen front", SplitX, Right, KitchenFront, KitchenDoorX, cream);
        AlongZ("Bathroom side", SplitX, -3.12f, KitchenFront, green);
        AlongX("Bathroom rear offset", -3.26f, SplitX, -3.12f, green);
        AlongZ("Rear partition", -3.26f, Rear, -3.12f, green);
        ApplyWallpaperUvs(structure, cream, green);
        BathroomFloor();
    }

    internal static void ApplyWallpaperUvs(Transform structure, Material floral, Material sage)
    {
        // Project from one origin so the pattern continues between coplanar wall pieces.
        Vector3 origin = structure.position;
        Vector3 up = structure.up;
        foreach (MeshRenderer renderer in structure.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (renderer.sharedMaterial != floral && renderer.sharedMaterial != sage) continue;

            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            Transform surface = filter.transform;
            string section = surface.name == "Wall" ? surface.parent.name : surface.name;
            var mesh = Object.Instantiate(filter.sharedMesh);
            mesh.name = section + " wallpaper";
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            var uv = new Vector2[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 point = surface.TransformPoint(vertices[i]) - origin;
                Vector3 normal = surface.TransformDirection(normals[i]);
                bool horizontal = Mathf.Abs(Vector3.Dot(normal, up)) > 0.5f;
                Vector3 uAxis = horizontal ? structure.right : Vector3.Cross(normal, up).normalized;
                Vector3 vAxis = horizontal ? structure.forward : up;
                uv[i] = new Vector2(Vector3.Dot(point, uAxis), Vector3.Dot(point, vAxis)) / WallpaperRepeatMeters;
            }

            mesh.uv = uv;
            mesh.RecalculateTangents();
            filter.sharedMesh = Store(mesh, mesh.name + ".asset");
        }

        foreach (Material material in new[] { floral, sage })
        {
            material.SetTextureScale("_BaseMap", Vector2.one);
            material.SetTextureScale("_MainTex", Vector2.one);
            EditorUtility.SetDirty(material);
        }
    }

    private void SplitWall(string name, float from, float to, float z, float opening, Material material)
    {
        float halfGap = DoorGap * 0.5f / Scale;
        AlongX(name + " left", from, opening - halfGap, z, material);
        AlongX(name + " right", opening + halfGap, to, z, material);
        Block(name + " lintel", structure, At(opening, z, (Ceiling + DoorHead) / 2f),
            new Vector3(DoorGap, Ceiling - DoorHead, 0.14f), material, true);
        foreach (float sign in new[] { -1f, 1f })
        {
            Block("Door jamb", structure, At(opening + sign * halfGap, z, DoorHead / 2f),
                new Vector3(0.055f, DoorHead, 0.188f), trim);
        }
        Block("Door head trim", structure, At(opening, z, DoorHead),
            new Vector3(DoorGap + 0.055f, 0.05f, 0.188f), trim);
    }

    private void AlongX(string name, float from, float to, float z, Material material) =>
        Wall(name, (from + to) / 2f, z, (to - from) * Scale + 0.008f, false, material);

    private void AlongZ(string name, float x, float from, float to, Material material) =>
        Wall(name, x, (from + to) / 2f, (to - from) * Scale + 0.10f, true, material);

    private void Wall(string name, float x, float z, float length, bool rotate, Material material)
    {
        var wall = Group(name, structure);
        wall.SetPositionAndRotation(At(x, z), Quaternion.Euler(0f, rotate ? 90f : 0f, 0f));
        Block("Wall", wall, wall.TransformPoint(Vector3.up * Ceiling / 2f), new Vector3(length, Ceiling, 0.14f),
            material, true, wall.rotation);
        foreach (float side in new[] { -1f, 1f })
            Block("Skirting", wall, wall.TransformPoint(new Vector3(0f, 0.047f, side * 0.074f)),
                new Vector3(length, 0.092f, 0.015f), trim, false, wall.rotation);
    }

    private void BathroomFloor()
    {
        Panel("Bath tiles", bath, Left, -3.26f, Rear, -3.12f, 0.012f, tile, false);
        Panel("Bath tiles at doorway", bath, Left, SplitX, -3.12f, BathFront, 0.012f, tile, false);
    }

    private void BuildDoors(Mesh original, Mesh template)
    {
        var sounds = DoorSounds();
        openSound = sounds[0];
        closeSound = sounds[1];
        latchSound = sounds[2];

        Material exteriorMaterial = Part("_Door1_").sharedMaterial;
        Material interiorMaterial = Part("Box004_Door2").sharedMaterial;
        Bounds entrance = original.bounds;
        float entrancePlane = At(0f, 0.1536f).z;
        Vector3 hinge = new Vector3(entrance.min.x + 0.008f, entrance.min.y, entrancePlane);
        Matrix4x4 recenter = Matrix4x4.Translate(-hinge);
        TransformMesh(original, recenter);
        AddDoor("EntranceDoor", hinge, Quaternion.identity, 96f, original, exteriorMaterial,
            entrance.size.x - 0.026f, entrance.size.y, 0.075f);
        AddInteriorDoor("KitchenDoor", KitchenDoorX, KitchenFront, -94f, Quaternion.Euler(0f, 180f, 0f), template, interiorMaterial);
        AddInteriorDoor("BathroomDoor", BathDoorX, BathFront, 94f, Quaternion.Euler(0f, 180f, 0f), template, interiorMaterial);
        Object.DestroyImmediate(template);
    }

    private void AddInteriorDoor(string name, float x, float z, float angle, Quaternion facing, Mesh sample, Material material)
    {
        Mesh mesh = Object.Instantiate(sample);
        Bounds bounds = mesh.bounds;
        float width = DoorGap - 0.068f;
        float height = DoorHead - 0.041f;
        Vector3 basePoint = new Vector3(bounds.min.x, bounds.min.y, bounds.center.z);
        var factor = new Vector3(width / bounds.size.x, height / bounds.size.y, 0.85f);
        TransformMesh(mesh, Matrix4x4.Scale(factor) * Matrix4x4.Translate(-basePoint));
        Vector3 hinge = At(x + (width / 2f) / Scale, z, 0.015f);
        AddDoor(name, hinge, facing, angle, mesh, material, width - 0.012f, height, 0.052f);
    }

    private void AddDoor(string name, Vector3 hinge, Quaternion facing, float angle, Mesh mesh, Material material,
        float width, float height, float thickness)
    {
        Transform door = Group(name, doors);
        door.SetPositionAndRotation(hinge, facing);
        Transform pivot = Group("DoorPivot", door);
        mesh.name = name + " leaf";
        pivot.gameObject.AddComponent<MeshFilter>().sharedMesh = Store(mesh, mesh.name + ".asset");
        pivot.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
        var leaf = pivot.gameObject.AddComponent<BoxCollider>();
        leaf.center = new Vector3(width * 0.5f + 0.004f, height * 0.5f, 0f);
        leaf.size = new Vector3(width, height - 0.012f, thickness);
        var body = pivot.gameObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;

        var focus = door.gameObject.AddComponent<BoxCollider>();
        focus.isTrigger = true;
        focus.center = new Vector3(width * 0.5f, height * 0.5f, 0f);
        focus.size = new Vector3(width, height, 0.15f);
        var audio = door.gameObject.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 1f;
        audio.rolloffMode = AudioRolloffMode.Logarithmic;
        audio.minDistance = 0.65f;
        audio.maxDistance = 9f;
        audio.dopplerLevel = 0f;
        audio.volume = name == "EntranceDoor" ? 0.69f : 0.53f;
        door.gameObject.AddComponent<HouseDoor>().Configure(pivot, leaf, angle, openSound, closeSound, latchSound);
    }

    private void Panel(string name, Transform parent, float x0, float x1, float z0, float z1,
        float height, Material material, bool down)
    {
        var points = new[] { At(x0,z0,height), At(x0,z1,height), At(x1,z1,height), At(x1,z0,height) };
        var mesh = new Mesh
        {
            name = name,
            vertices = points,
            uv = points.Select(p => new Vector2(p.x, p.z) * 0.74f).ToArray(),
            triangles = down ? new[] { 0, 2, 1, 0, 3, 2 } : new[] { 0, 1, 2, 0, 2, 3 }
        };
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        var go = Group(name, parent).gameObject;
        go.AddComponent<MeshFilter>().sharedMesh = Store(mesh, name + ".asset");
        go.AddComponent<MeshRenderer>().sharedMaterial = material;
    }

    private void Floorboards(string name, float x0, float x1, float z0, float z1)
    {
        var points = new List<Vector3>();
        var uv = new List<Vector2>();
        var tris = new List<int>();
        var random = new System.Random(1983);
        Vector3 min = At(x0, z0, 0.009f);
        Vector3 max = At(x1, z1, 0.009f);
        for (float row = min.z; row < max.z; row += 0.161f)
        {
            float end = Mathf.Min(max.z, row + 0.157f);
            for (float x = min.x; x < max.x;)
            {
                float next = Mathf.Min(max.x, x + Mathf.Lerp(0.74f, 2.31f, (float)random.NextDouble()));
                float edge = Mathf.Max(x + 0.001f, next - 0.0032f);
                float v = Mathf.Lerp(0.1f, 0.77f, (float)random.NextDouble());
                float u = (float)random.NextDouble();
                float uEnd = u + (next - x) * 0.57f;
                int i = points.Count;
                points.Add(new Vector3(x, min.y, row));
                points.Add(new Vector3(x, min.y, end));
                points.Add(new Vector3(edge, min.y, end));
                points.Add(new Vector3(edge, min.y, row));
                uv.Add(new Vector2(u, v));
                uv.Add(new Vector2(u, v + 0.09f));
                uv.Add(new Vector2(uEnd, v + 0.09f));
                uv.Add(new Vector2(uEnd, v));
                tris.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
                x = next;
            }
        }
        var mesh = new Mesh { name = name };
        mesh.SetVertices(points);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        var surface = Group(name, structure).gameObject;
        surface.AddComponent<MeshFilter>().sharedMesh = Store(mesh, name + ".asset");
        surface.AddComponent<MeshRenderer>().sharedMaterial = wood;
    }

    private static Mesh Bake(MeshRenderer renderer, Matrix4x4 target)
    {
        var mesh = Object.Instantiate(renderer.GetComponent<MeshFilter>().sharedMesh);
        TransformMesh(mesh, target * renderer.localToWorldMatrix);
        return mesh;
    }

    private static void TransformMesh(Mesh mesh, Matrix4x4 matrix)
    {
        mesh.vertices = mesh.vertices.Select(matrix.MultiplyPoint3x4).ToArray();
        Matrix4x4 direction = matrix.inverse.transpose;
        mesh.normals = mesh.normals.Select(n => direction.MultiplyVector(n).normalized).ToArray();
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
    }

    private Vector3 At(float x, float z, float aboveFloor = 0f)
    {
        return PlanPoint(model, x, z, floorY + aboveFloor);
    }

    internal static Vector3 PlanPoint(Transform modelRoot, float x, float z, float height) =>
        new Vector3(modelRoot.position.x + Scale * x, height, modelRoot.position.z + Scale * z);
    private static Vector2 XZ(Vector3 point) => new Vector2(point.x, point.z);

    private MeshRenderer[] Parts() => model.GetComponentsInChildren<MeshRenderer>(true);
    private MeshRenderer Part(string token) => Parts().Single(part => NameOf(part).Contains(token));
    private static string NameOf(MeshRenderer renderer) =>
        renderer.name + " " + renderer.GetComponent<MeshFilter>().sharedMesh.name;

    private static Transform Group(string name, Transform parent)
    {
        var group = new GameObject(name).transform;
        group.SetParent(parent, false);
        return group;
    }

    private static Bounds Combined(Transform root)
    {
        Renderer[] parts = root.GetComponentsInChildren<Renderer>();
        Check(parts.Length > 0, "No renderers for " + root.name);
        Bounds bounds = parts[0].bounds;
        foreach (Renderer part in parts) bounds.Encapsulate(part.bounds);
        return bounds;
    }

    private static T Load<T>(string path) where T : Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        Check(asset != null, "Missing asset " + path);
        return asset;
    }

    private static T Store<T>(T asset, string name) where T : Object
    {
        string path = Data + "/" + name;
        T existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
        EditorUtility.CopySerialized(asset, existing);
        if (existing is Mesh mesh) mesh.UploadMeshData(false);
        EditorUtility.SetDirty(existing);
        Object.DestroyImmediate(asset);
        return existing;
    }

    private static void Check(bool condition, string error)
    {
        if (!condition) throw new InvalidOperationException(error);
    }
}
