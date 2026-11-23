using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class FinalLabRefinement
{
    internal const string LogFolder = "Logs/FinalLab/Refinement";
    internal const string LadderMount = "WoodLadder_Mount";
    internal const string ThresholdName = "Archive_OldMetal_Threshold";
    private const string Assets = "Assets/Art/Models/Environment/FinalLab";
    private const string LadderPath = Assets + "/WoodLadder.glb";
    private const string PortalSource = Assets + "/Architecture/realism_bunker_modular.glb";
    internal const string RepairedPortalMeshPath = Assets + "/Architecture/FinalLab_ArchiveDoorway.asset";
    private const string ThresholdMeshPath = Assets + "/Architecture/FinalLab_ArchiveThreshold.asset";
    private const string ThresholdMaterialPath = Assets + "/Materials/FinalLab_OldMetal_Threshold.mat";
    private const string LadderMaterialPath = Assets + "/Materials/FinalLab_WoodLadder.mat";
    private const string Entrance = "01_Entrance_Stairwell";
    private const string Portal = "02_Archive_Corridor/EntranceOpening";
    private const string Module = Portal + "/Bunker_Doorway_Module/wall.046";
    private const float Width = 1.41f;
    private const float Depth = 0.22f;
    private const float Thickness = 0.018f;
    private const float Rise = 0.012f;

    public static void ApplyAndValidate()
    {
        var scene = EditorSceneManager.OpenScene(FinalLabBuilder.ScenePath);
        var root = GameObject.Find(FinalLabBuilder.RootName).transform;
        var lines = new List<string>();
        Physics.SyncTransforms();
        RepairDoorway(root, lines);
        InstallLadder(root, lines);
        Physics.SyncTransforms();
        Verify(root, lines);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("FinalScene could not be saved.");
        FinalLabVerification.ValidateSaved();
        Directory.CreateDirectory(LogFolder);
        File.WriteAllLines(Path.Combine(LogFolder, "refinement-validation.txt"), lines);
        Debug.Log("FINAL_LAB_REFINEMENT_SAVED\n" + string.Join("\n", lines));
    }

    private static void RepairDoorway(Transform root, List<string> lines)
    {
        var filter = root.Find(Module).GetComponent<MeshFilter>();
        var source = AssetDatabase.LoadAllAssetsAtPath(PortalSource).OfType<Mesh>()
            .Single(m => m.name == "wall.046_Wall1.001_0");
        var mainFloor = root.Find("03_Main_Laboratory/MainFloor").GetComponent<BoxCollider>().bounds;
        var archiveFloor = root.Find("02_Archive_Corridor/ArchiveFloor").GetComponent<BoxCollider>().bounds;
        float floorY = mainFloor.max.y;
        float slabBoundary = mainFloor.min.z;
        Require(Mathf.Abs(slabBoundary - archiveFloor.max.z) < 0.001f &&
            Mathf.Abs(floorY - archiveFloor.max.y) < 0.001f, "The concrete slabs meet at the same level.");

        // The imported doorway includes a two-triangle sill exactly over the MainFloor.
        // Keep all the uprights, faces, and header with their source UVs and normals.
        Vector3[] points = source.vertices.Select(filter.transform.TransformPoint).ToArray();
        var sourceIndices = source.GetTriangles(0);
        var kept = new List<int>(sourceIndices.Length);
        for (int i = 0; i < sourceIndices.Length; i += 3)
        {
            if (Enumerable.Range(i, 3).All(n => Mathf.Abs(points[sourceIndices[n]].y - floorY) < 0.001f))
                continue;
            kept.Add(sourceIndices[i]);
            kept.Add(sourceIndices[i + 1]);
            kept.Add(sourceIndices[i + 2]);
        }
        Require(source.subMeshCount == 1 && sourceIndices.Length - kept.Count == 6,
            "Exactly two imported sill triangles overlap MainFloor.");
        var trimmed = Object.Instantiate(source);
        trimmed.name = "ArchiveDoorway_WithoutCoplanarSill";
        trimmed.SetTriangles(kept, 0);
        filter.sharedMesh = SaveMesh(trimmed, RepairedPortalMeshPath);

        Transform threshold = root.Find(Portal + "/" + ThresholdName);
        if (threshold == null)
        {
            threshold = new GameObject(ThresholdName).transform;
            threshold.SetParent(root.Find(Portal), false);
            threshold.gameObject.AddComponent<MeshFilter>();
            threshold.gameObject.AddComponent<MeshRenderer>();
            threshold.gameObject.AddComponent<BoxCollider>();
        }
        float seam = Mathf.Clamp(threshold.position.z, slabBoundary, filter.GetComponent<Renderer>().bounds.max.z);
        threshold.position = new Vector3(0, floorY, seam);
        FitFloorJoint(root, mainFloor, archiveFloor, seam);
        threshold.GetComponent<MeshFilter>().sharedMesh = SaveMesh(CreatePlate(), ThresholdMeshPath);
        threshold.GetComponent<MeshRenderer>().sharedMaterial = ThresholdMaterial();
        var collider = threshold.GetComponent<BoxCollider>();
        collider.center = new Vector3(0, Rise - Thickness * 0.5f, 0);
        collider.size = new Vector3(Width, Thickness, Depth);
        lines.Add($"Removed two imported sill triangles over MainFloor at y={floorY:F4}. The visible concrete joint is under the plate at z={seam:F4}.");
        lines.Add($"Beveled plate centered on the concrete joint. {Width:F2} × {Depth:F2} m, {Thickness:F3} m thick, rising {Rise:F3} m.");
    }

    private static void FitFloorJoint(Transform root, Bounds main, Bounds archive, float seam)
    {
        const float opening = 0.73f;
        var mainFilter = root.Find("03_Main_Laboratory/MainFloor").GetComponent<MeshFilter>();
        var archiveFilter = root.Find("02_Archive_Corridor/ArchiveFloor").GetComponent<MeshFilter>();
        RewriteFloorTop(mainFilter, main.max.y, new Rect(main.min.x, main.min.z, -opening - main.min.x, main.size.z),
            new Rect(opening, main.min.z, main.max.x - opening, main.size.z),
            new Rect(-opening, seam, 2 * opening, main.max.z - seam));
        RewriteFloorTop(archiveFilter, archive.max.y, new Rect(archive.min.x, archive.min.z, archive.size.x, archive.size.z),
            new Rect(-opening, main.min.z, 2 * opening, seam - main.min.z));
    }

    private static void RewriteFloorTop(MeshFilter filter, float top, params Rect[] surfaces)
    {
        Mesh mesh = filter.sharedMesh;
        var oldVertices = mesh.vertices;
        var oldUV = mesh.uv;
        var oldIndices = mesh.triangles;
        var remap = new Dictionary<int, int>();
        var vertices = new List<Vector3>();
        var uv = new List<Vector2>();
        var triangles = new List<int>();
        for (int i = 0; i < oldIndices.Length; i += 3)
        {
            if (Enumerable.Range(i, 3).All(n =>
                Mathf.Abs(filter.transform.TransformPoint(oldVertices[oldIndices[n]]).y - top) < 0.001f))
                continue;
            for (int n = i; n < i + 3; n++)
            {
                int original = oldIndices[n];
                if (!remap.TryGetValue(original, out int updated))
                {
                    remap.Add(original, updated = vertices.Count);
                    vertices.Add(oldVertices[original]);
                    uv.Add(oldUV[original]);
                }
                triangles.Add(updated);
            }
        }
        foreach (Rect area in surfaces.Where(surface => surface.height > 0.001f))
        {
            int start = vertices.Count;
            foreach (var point in new[] {
                new Vector3(area.xMax, top, area.yMin), new Vector3(area.xMin, top, area.yMin),
                new Vector3(area.xMin, top, area.yMax), new Vector3(area.xMax, top, area.yMax) })
            {
                vertices.Add(filter.transform.InverseTransformPoint(point));
                uv.Add(new Vector2(-point.x, point.z) / 2.25f);
            }
            triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
        }
        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        EditorUtility.SetDirty(mesh);
    }

    private static Mesh SaveMesh(Mesh mesh, string path)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }
        EditorUtility.CopySerialized(mesh, existing);
        Object.DestroyImmediate(mesh);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    private static Mesh CreatePlate()
    {
        const float bevel = 0.004f;
        float halfX = Width / 2;
        float halfZ = Depth / 2;
        var vertices = new List<Vector3>(24);
        var uv = new List<Vector2>(24);
        var triangles = new List<int>(36);
        Vector3[] bottom = Ring(halfX, halfZ, Rise - Thickness);
        Vector3[] shoulder = Ring(halfX, halfZ, Rise - bevel);
        Vector3[] top = Ring(halfX - bevel, halfZ - bevel, Rise);
        Quad(top[0], top[1], top[2], top[3]);
        for (int i = 0; i < 4; i++)
        {
            int j = (i + 1) % 4;
            Quad(shoulder[i], shoulder[j], top[j], top[i]);
            Quad(bottom[i], bottom[j], shoulder[j], shoulder[i]);
        }
        Quad(bottom[3], bottom[2], bottom[1], bottom[0]);

        var mesh = new Mesh { name = "Archive_Steel_Plate_4mm_Bevel" };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        return mesh;

        Vector3[] Ring(float x, float z, float y) =>
            new[] { new Vector3(-x, y, -z), new Vector3(-x, y, z), new Vector3(x, y, z), new Vector3(x, y, -z) };

        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int start = vertices.Count;
            foreach (var point in new[] { a, b, c, d })
            {
                vertices.Add(point);
                uv.Add(new Vector2(0.5f + point.x / Width, 0.5f + point.z / Depth));
            }
            triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
        }
    }

    private static Material ThresholdMaterial()
    {
        var material = GetOrCreateLitMaterial(ThresholdMaterialPath);
        if (material.GetTexture("_BaseMap") is not Texture2D texture || AssetDatabase.GetAssetPath(texture) != ThresholdMaterialPath)
        {
            texture = new Texture2D(512, 128, TextureFormat.RGB24, true)
            {
                name = "Steel wear and four slotted screws",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4
            };
            AssetDatabase.AddObjectToAsset(texture, material);
        }
        var screws = new[] { new Vector2(0.045f, 0.24f), new Vector2(0.955f, 0.24f),
            new Vector2(0.045f, 0.76f), new Vector2(0.955f, 0.76f) };
        var pixels = new Color32[texture.width * texture.height];
        for (int y = 0; y < texture.height; y++)
        for (int x = 0; x < texture.width; x++)
        {
            float u = (x + 0.5f) / texture.width;
            float v = (y + 0.5f) / texture.height;
            float mottling = Mathf.PerlinNoise(u * 15f + 12, v * 6f);
            float scuffs = Mathf.PerlinNoise(u * 116f + 5, v * 1.8f);
            float edge = Mathf.Min(Mathf.Min(u, 1 - u) * Width, Mathf.Min(v, 1 - v) * Depth);
            float wear = Mathf.SmoothStep(0, 1, Mathf.Clamp01(1 - edge / 0.009f));
            Color color = Color.Lerp(new Color(0.35f, 0.35f, 0.335f), new Color(0.37f, 0.30f, 0.245f), mottling);
            color *= 0.76f + mottling * 0.29f + scuffs * 0.20f + wear * 0.45f;
            foreach (var screw in screws)
            {
                float dx = (u - screw.x) * Width;
                float dy = (v - screw.y) * Depth;
                float radius = Mathf.Sqrt(dx * dx + dy * dy);
                if (radius < 0.0105f)
                    color = radius > 0.0092f || Mathf.Abs(dy - dx * 0.6f) < 0.0013f
                        ? new Color(0.17f, 0.155f, 0.135f) : new Color(0.53f, 0.49f, 0.43f);
            }
            color.a = 1;
            pixels[y * texture.width + x] = color;
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        EditorUtility.SetDirty(texture);
        material.SetTexture("_BaseMap", texture);
        material.SetFloat("_Metallic", 0.34f);
        material.SetFloat("_Smoothness", 0.28f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material GetOrCreateLitMaterial(string path)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }
        return material;
    }

    private static Material LadderMaterial(Material source)
    {
        var material = GetOrCreateLitMaterial(LadderMaterialPath);
        string colorMap = source.GetTexturePropertyNames()
            .Single(property => property.Contains("baseColor", StringComparison.OrdinalIgnoreCase));
        var wood = source.GetTexture(colorMap);
        Require(wood != null, "The stair tread must have an imported wood albedo.");
        material.SetTexture("_BaseMap", wood);
        material.SetColor("_BaseColor", new Color(1.72f, 1.50f, 1.29f));
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_Smoothness", 0.22f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void InstallLadder(Transform root, List<string> lines)
    {
        var well = root.Find(Entrance);
        var mount = well.Find(LadderMount);
        if (mount == null)
        {
            mount = new GameObject(LadderMount).transform;
            mount.SetParent(well, false);
        }
        mount.localPosition = Vector3.zero;
        mount.localScale = Vector3.one;
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(LadderPath);
        Require(asset != null && PrefabUtility.GetPrefabAssetType(asset) == PrefabAssetType.Model,
            "WoodLadder.glb must import as a model prefab.");
        var model = mount.Find("WoodLadder")?.gameObject;
        if (model == null)
        {
            model = (GameObject)PrefabUtility.InstantiatePrefab(asset, mount);
            model.name = "WoodLadder";
        }
        Require(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(model) == LadderPath, "WoodLadder must stay linked to the source model.");
        var mesh = model.GetComponentsInChildren<MeshFilter>().Single();

        var hatch = GetHatch(well);
        var landing = well.Find("UpperLanding").GetComponent<BoxCollider>().bounds;
        // The source already leans 9 degrees. Trim its lean by 3 degrees to preserve the spawn aisle.
        mount.rotation = Quaternion.LookRotation(-hatch.HandleSide, Vector3.up) * Quaternion.Euler(3, 0, 0);
        var bounds = mesh.GetComponent<MeshRenderer>().bounds;
        float topY = Mathf.Min(hatch.Lid.min.y - 0.027f, hatch.Handle.min.y - 0.012f);
        float scale = (topY - landing.max.y - 0.001f) / bounds.size.y;
        mount.localScale = Vector3.one * scale;
        var points = WorldVertices(mesh);
        bounds = BoundsOf(points);
        var top = BoundsOf(points.Where(p => p.y > bounds.max.y - 0.025f));
        Vector3 topTarget = hatch.Handle.center - hatch.HandleSide * 0.016f;
        mount.position += new Vector3(topTarget.x - top.center.x, landing.max.y + 0.001f - bounds.min.y,
            topTarget.z - top.center.z);

        var wood = well.GetComponentsInChildren<MeshRenderer>()
            .Single(r => r.name == "SketchUp.011_material_2_0").sharedMaterial;
        mesh.GetComponent<MeshRenderer>().sharedMaterial = LadderMaterial(wood);
        Physics.SyncTransforms();
        points = WorldVertices(mesh);
        bounds = BoundsOf(points);
        Vector3 across = Vector3.Cross(Vector3.up, hatch.HandleSide);
        foreach (int side in new[] { -1, 1 })
        {
            var rail = points.Where(p => side * Vector3.Dot(p - bounds.center, across) > 0.13f).ToArray();
            Vector3 foot = BoundsOf(rail.Where(p => p.y < bounds.min.y + 0.025f)).center;
            Vector3 end = BoundsOf(rail.Where(p => p.y > bounds.max.y - 0.025f)).center;
            AddRailCollider(mount, foot, end, side);
            string name = side < 0 ? "Ladder_Brace_Left" : "Ladder_Brace_Right";
            var brace = mount.Find(name)?.gameObject;
            if (brace == null)
            {
                brace = GameObject.CreatePrimitive(PrimitiveType.Cube);
                brace.name = name;
                brace.transform.SetParent(mount, false);
            }
            Vector3 braceStart = end - Vector3.up * 0.045f;
            var wall = Physics.RaycastAll(braceStart, hatch.HandleSide, 0.38f)
                .Where(hit => !hit.collider.transform.IsChildOf(mount)).OrderBy(hit => hit.distance).FirstOrDefault();
            Require(wall.collider != null, "The upper ladder brace must reach the shaft wall.");
            float length = wall.distance + 0.014f;
            brace.transform.SetPositionAndRotation(braceStart + hatch.HandleSide * (length * 0.5f - 0.009f),
                Quaternion.LookRotation(hatch.HandleSide, Vector3.up));
            brace.transform.localScale = new Vector3(0.026f, 0.028f, length) / scale;
            brace.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                Assets + "/Materials/FinalLab_OxidizedSteel.mat");
        }
        lines.Add($"WoodLadder prefab scaled proportionally ×{scale:F3}. Visible wood spans {bounds.size.y:F3} m vertically.");
        lines.Add($"Handle center {hatch.Handle.center:F4}; hinge side {hatch.Hinges.center:F4}; lid gap {hatch.Lid.min.y - bounds.max.y:F3} m.");
    }

    private static void AddRailCollider(Transform mount, Vector3 foot, Vector3 end, int side)
    {
        string name = side < 0 ? "Ladder_LeftRail_Collision" : "Ladder_RightRail_Collision";
        var rail = mount.Find(name);
        if (rail == null)
        {
            rail = new GameObject(name).transform;
            rail.SetParent(mount, false);
            rail.gameObject.AddComponent<BoxCollider>();
        }
        rail.SetPositionAndRotation((foot + end) / 2, Quaternion.FromToRotation(Vector3.up, end - foot));
        float scale = mount.lossyScale.x;
        rail.GetComponent<BoxCollider>().size = new Vector3(0.045f, Vector3.Distance(foot, end), 0.045f) / scale;
    }

    private static (Bounds Lid, Bounds Handle, Bounds Hinges, Vector3 HandleSide) GetHatch(Transform well)
    {
        var parts = well.Find("Hatch").GetComponentsInChildren<MeshRenderer>();
        // The importer loses the source Cyrillic names. Match the grip by its distinct 360-vertex mesh,
        // then use its position relative to the horizontal lid and the three sets of hinge parts.
        var lid = parts.Single(p => p.GetComponent<MeshFilter>().sharedMesh.vertexCount == 24).bounds;
        var handle = parts.Single(p => p.GetComponent<MeshFilter>().sharedMesh.vertexCount == 360).bounds;
        var hinges = BoundsOf(parts.Where(p => p.GetComponent<MeshFilter>().sharedMesh.vertexCount == 165)
            .Select(p => p.bounds.center));
        var direction = Vector3.ProjectOnPlane(handle.center - lid.center, Vector3.up).normalized;
        Require(Vector3.Dot(hinges.center - lid.center, direction) < -0.45f &&
            Vector3.Dot(handle.center - lid.center, direction) > 0.45f,
            "The handle and the three hinges are on opposite sides of the actual lid.");
        return (lid, handle, hinges, direction);
    }

    internal static void Verify(Transform root, List<string> lines)
    {
        var well = root.Find(Entrance);
        var mount = well.Find(LadderMount);
        var ladder = mount.Find("WoodLadder");
        Require(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(ladder.gameObject) == LadderPath,
            "The ladder must be a linked instance of WoodLadder.glb.");
        var hatch = GetHatch(well);
        var mesh = ladder.GetComponentInChildren<MeshFilter>();
        var points = WorldVertices(mesh);
        var bounds = BoundsOf(points);
        var top = BoundsOf(points.Where(p => p.y > bounds.max.y - 0.025f));
        var foot = BoundsOf(points.Where(p => p.y < bounds.min.y + 0.025f));
        var landing = well.Find("UpperLanding").GetComponent<BoxCollider>().bounds;
        Require(Mathf.Abs(landing.max.y - bounds.min.y) < 0.003f &&
            foot.min.x > landing.min.x && foot.max.x < landing.max.x &&
            foot.min.z > landing.min.z && foot.max.z < landing.max.z, "Both feet rest on the existing landing.");
        float lidGap = hatch.Lid.min.y - bounds.max.y;
        Require(Vector3.Dot(top.center - hatch.Lid.center, hatch.HandleSide) > 0.55f &&
            Mathf.Abs(Vector3.Dot(top.center - hatch.Handle.center, hatch.HandleSide)) < 0.04f &&
            lidGap >= 0.015f && lidGap <= 0.08f, "The rails reach the handle edge without intersecting the closed lid.");
        var shaftWalls = well.Find("StairwellWalls").GetComponentsInChildren<BoxCollider>();
        Require(shaftWalls.All(wall => !wall.bounds.Intersects(bounds)),
            "The visible ladder fits inside the shaft without entering its concrete walls.");
        var spawn = FinalLabBuilder.Spawn;
        Require(!Physics.OverlapCapsule(spawn + Vector3.up * 0.28f, spawn + Vector3.up * 1.52f, 0.28f)
            .Any(c => c.transform.IsChildOf(mount)), "The new rails and brackets keep the player spawn clear.");

        var plate = root.Find(Portal + "/" + ThresholdName);
        var plateCollider = plate.GetComponent<BoxCollider>();
        var door = root.Find(Module).GetComponent<MeshFilter>();
        Require(door.sharedMesh.GetIndexCount(0) == 54 && HorizontalTriangles(door).All(t => Mathf.Abs(t.A.y) > 0.001f),
            "The repaired bunker module has no sill at floor height.");
        Require(plateCollider.bounds.max.y > 0.009f && plateCollider.bounds.max.y < 0.015f &&
            plate.GetComponent<MeshRenderer>().sharedMaterial.shader.name == "Universal Render Pipeline/Lit",
            "The plate has visible depth separation, a low collision step and a URP Lit material.");
        var archiveTop = root.Find("02_Archive_Corridor/ArchiveFloor").GetComponent<MeshFilter>();
        var hallTop = root.Find("03_Main_Laboratory/MainFloor").GetComponent<MeshFilter>();
        Require(HasFloorAt(archiveTop, plate.position.z - 0.025f) && !HasFloorAt(archiveTop, plate.position.z + 0.025f) &&
            !HasFloorAt(hallTop, plate.position.z - 0.025f) && HasFloorAt(hallTop, plate.position.z + 0.025f),
            "The visible concrete faces meet directly beneath the plate.");
        foreach (float x in new[] { -0.63f, -0.22f, 0.21f, 0.66f })
        foreach (float offset in new[] { -0.08f, -0.02f, 0.02f, 0.08f })
        {
            float z = plate.position.z + offset;
            Require(Physics.Raycast(new Vector3(x, 0.2f, z), Vector3.down, out var hit, 0.25f) && hit.collider == plateCollider,
                $"Expected continuous plate support at x={x}, z={z}.");
        }

        var floorTriangles = root.GetComponentsInChildren<MeshFilter>()
            .Where(m => m.GetComponent<BoxCollider>() != null && m.GetComponent<Renderer>().bounds.min.y < -0.1f)
            .SelectMany(m => HorizontalTriangles(m).Where(t => Mathf.Abs(t.A.y) < 0.001f &&
                Vector3.Cross(t.B - t.A, t.C - t.A).y > 0)).ToArray();
        int floorSamples = 0;
        (float X, float Z)[] openings = { (0f, 1.15f), (0f, 5.5f), (0f, plate.position.z), (0f, 5.8f),
            (-1.52f, 14.12f), (0.92f, 14.12f) };
        foreach (var opening in openings)
        foreach (float x in new[] { opening.X - 0.37f, opening.X + 0.06f, opening.X + 0.35f })
        foreach (float z in new[] { opening.Z - 0.075f, opening.Z - 0.013f, opening.Z + 0.017f, opening.Z + 0.069f })
        {
            Require(floorTriangles.Count(t => CoversXZ(t.A, t.B, t.C, x, z)) == 1 &&
                Physics.Raycast(new Vector3(x, 0.30f, z), Vector3.down, out _, 0.35f),
                $"Duplicate floor or missing support at x={x}, z={z}.");
            floorSamples++;
        }

        (float Left, float Right, float Near, float Far, float Roof)[] rooms =
        {
            (-0.88f, 0.88f, -4.76f, 0.90f, 3.94f),
            (-1.35f, 1.35f, 1.15f, 5.50f, 2.70f),
            (-5f, 5f, 5.80f, 13.80f, 2.90f),
            (-4.69f, -0.69f, 14.12f, 17.62f, 2.70f),
            (-0.28f, 4.22f, 14.12f, 18.12f, 2.70f)
        };
        int cornerSamples = 0;
        foreach (var room in rooms)
        foreach (int side in new[] { -1, 1 })
        foreach (int end in new[] { -1, 1 })
        foreach (float y in new[] { 0.09f, 1.30f, room.Roof - 0.09f })
        {
            // Aim just on either side of every vertical corner, so a missing face cannot hide behind its neighbor.
            Vector3 from = new(side < 0 ? room.Left + 0.12f : room.Right - 0.12f, y,
                end < 0 ? room.Near + 0.12f : room.Far - 0.12f);
            foreach (Vector3 ray in new[] { new Vector3(side, 0, end * 0.83f), new Vector3(side * 0.83f, 0, end) })
            {
                float limit = room.Roof > 3.5f && end > 0 ? 0.44f : 0.22f;
                Require(Physics.Raycast(from, ray.normalized, limit), $"Unsealed corner at {from:F3}.");
                cornerSamples++;
            }
        }
        lines.Add($"Verified the handle-side alignment, footing, shaft clearance and {lidGap:F3} m lid clearance.");
        lines.Add($"Verified {floorSamples} single-layer concrete floor samples and {cornerSamples} wall corner samples in five zones.");
    }

    private static bool CoversXZ(Vector3 a, Vector3 b, Vector3 c, float x, float z)
    {
        float Side(Vector3 p, Vector3 q) => (q.x - p.x) * (z - p.z) - (q.z - p.z) * (x - p.x);
        float s1 = Side(a, b), s2 = Side(b, c), s3 = Side(c, a);
        return (s1 >= 0 && s2 >= 0 && s3 >= 0) || (s1 <= 0 && s2 <= 0 && s3 <= 0);
    }

    private static bool HasFloorAt(MeshFilter mesh, float z) =>
        HorizontalTriangles(mesh).Any(t => Mathf.Abs(t.A.y) < 0.001f && CoversXZ(t.A, t.B, t.C, 0, z));

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public static void Diagnose()
    {
        EditorSceneManager.OpenScene(FinalLabBuilder.ScenePath);
        Directory.CreateDirectory(LogFolder);
        var root = GameObject.Find(FinalLabBuilder.RootName).transform;
        var lines = new List<string>();
        var hatch = root.Find(Entrance + "/Hatch").GetChild(0).gameObject;
        lines.Add("Hatch prefab: " + PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(hatch));
        foreach (var part in hatch.GetComponentsInChildren<MeshRenderer>())
            lines.Add($"{part.name} | {part.GetComponent<MeshFilter>().sharedMesh.vertexCount} vertices | {Describe(part.bounds)}");

        var ladderAsset = AssetDatabase.LoadAssetAtPath<GameObject>(LadderPath);
        lines.Add($"WoodLadder source: {AssetDatabase.GetMainAssetTypeAtPath(LadderPath)} | prefab {PrefabUtility.GetPrefabAssetType(ladderAsset)}");
        var ladder = (GameObject)PrefabUtility.InstantiatePrefab(ladderAsset);
        foreach (var part in ladder.GetComponentsInChildren<MeshFilter>())
        {
            Vector3[] points = WorldVertices(part);
            var bounds = part.GetComponent<Renderer>().bounds;
            lines.Add($"{part.name} | {part.sharedMesh.vertexCount} vertices | {Describe(bounds)}");
            for (int i = 0; i <= 10; i++)
            {
                float y = Mathf.Lerp(bounds.min.y, bounds.max.y, i / 10f);
                var slice = points.Where(p => Mathf.Abs(p.y - y) < 0.035f).ToArray();
                if (slice.Length > 0) lines.Add($"  y={y:F4} {Describe(BoundsOf(slice))}");
            }
        }
        Object.DestroyImmediate(ladder);

        foreach (var mesh in root.GetComponentsInChildren<MeshFilter>())
        {
            var renderer = mesh.GetComponent<MeshRenderer>();
            if (!renderer.enabled || !renderer.bounds.Intersects(new Bounds(new Vector3(0, 0, 5.62f), new Vector3(1.42f, 0.025f, 0.65f))))
                continue;
            lines.Add($"Doorway floor candidate {mesh.name}: {Describe(renderer.bounds)} | {AssetDatabase.GetAssetPath(mesh.sharedMesh)}");
            foreach (var triangle in HorizontalTriangles(mesh).Where(t => Mathf.Abs(t.A.y) < 0.003f &&
                Mathf.Min(t.A.z, t.B.z, t.C.z) < 5.9f && Mathf.Max(t.A.z, t.B.z, t.C.z) > 5.34f))
                lines.Add($"  triangle {triangle.Index}: {triangle.A:F6} {triangle.B:F6} {triangle.C:F6}");
        }

        File.WriteAllLines(Path.Combine(LogFolder, "diagnosis.txt"), lines);
        Debug.Log("FINAL_LAB_REFINEMENT_DIAGNOSIS complete.");
    }

    private static Vector3[] WorldVertices(MeshFilter filter)
    {
        return filter.sharedMesh.vertices.Select(filter.transform.TransformPoint).ToArray();
    }

    private static Bounds BoundsOf(IEnumerable<Vector3> points)
    {
        using var samples = points.GetEnumerator();
        if (!samples.MoveNext()) throw new InvalidOperationException("No vertices to bound.");
        var bounds = new Bounds(samples.Current, Vector3.zero);
        do { bounds.Encapsulate(samples.Current); } while (samples.MoveNext());
        return bounds;
    }

    private static string Describe(Bounds b) => $"min {b.min:F6} max {b.max:F6} size {b.size:F6}";

    private static IEnumerable<(int Index, Vector3 A, Vector3 B, Vector3 C)> HorizontalTriangles(MeshFilter mesh)
    {
        var points = WorldVertices(mesh);
        var indices = mesh.sharedMesh.triangles;
        for (int i = 0; i < indices.Length; i += 3)
        {
            Vector3 a = points[indices[i]], b = points[indices[i + 1]], c = points[indices[i + 2]];
            if (Mathf.Abs(a.y - b.y) < 0.0001f && Mathf.Abs(a.y - c.y) < 0.0001f &&
                Vector3.Cross(b - a, c - a).sqrMagnitude > 1e-9f)
                yield return (i / 3, a, b, c);
        }
    }
}
