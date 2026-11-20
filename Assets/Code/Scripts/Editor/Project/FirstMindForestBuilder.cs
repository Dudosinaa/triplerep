using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static partial class FirstMindForestBuilder
{
    private const string ScenePath = "Assets/Level/Scenes/Locations/MindsScenes/FirstMind.unity";
    private const string DataFolder = "Assets/Level/Scenes/Locations/MindsScenes/FirstMindData";
    private const string Models = "Assets/Art/Models/Environment/FirstMind";
    private const string Conifers = "Assets/Forst/Conifers [BOTD]/Render Pipeline Support/URP/Prefabs";
    private const float HouseUnitScale = 0.01f;
    private const float BoundaryRadius = 27f;
    private const float TerrainSize = 160f;
    private const float TerrainHeight = 14f;
    private const float TerrainBottom = -6f;
    private const int Seed = 190726;
    private static readonly string[] TreeTypes = { "Medium", "Small", "Tall", "Bare" };

    public static void BuildBatch()
    {
        try
        {
            Build();
            FirstMindForestPlayValidation.Start(ScenePath);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }

    private static void Build()
    {
        GameObject houseAsset = Load<GameObject>(Models + "/old_russian_house.glb");
        GameObject[] trees = TreeTypes.Select(type =>
            Load<GameObject>($"{Conifers}/PF Conifer {type} BOTD URP.prefab")).ToArray();
        ValidateImports(houseAsset, trees);
        if (!AssetDatabase.IsValidFolder(DataFolder))
            AssetDatabase.CreateFolder(Path.GetDirectoryName(DataFolder).Replace('\\', '/'), Path.GetFileName(DataFolder));

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Transform environment = Group("Environment");
        Transform terrainRoot = Group("Terrain", environment);
        Transform forestRoot = Group("Forest", environment);
        Group("ForestDetails", environment);
        Transform houseRoot = Group("House");
        Transform lighting = Group("Lighting");
        Transform boundaries = Group("Boundaries");

        GameObject house = (GameObject)PrefabUtility.InstantiatePrefab(houseAsset, houseRoot);
        Bounds raw = CombinedBounds(house);
        house.transform.localScale *= HouseUnitScale;
        MeshRenderer wall = HousePart(house, "_Stena_");
        MeshRenderer stairs = HousePart(house, "_Stupeni_");
        MeshRenderer door = HousePart(house, "_Door1_");
        MeshRenderer floor = HousePart(house, "_Pol_");
        Vector3 translation = -wall.bounds.center;
        translation.y = -floor.bounds.min.y - 0.015f;
        house.transform.position = translation;
        PrefabUtility.RecordPrefabInstancePropertyModifications(house.transform);

        Bounds footprint = wall.bounds;
        foreach (MeshRenderer part in house.GetComponentsInChildren<MeshRenderer>())
        {
            if (part == wall || part == stairs || part == door || part == floor || PartName(part).Contains("_Okno"))
                part.gameObject.AddComponent<MeshCollider>().sharedMesh = part.GetComponent<MeshFilter>().sharedMesh;
        }

        Require(stairs.bounds.center.z > door.bounds.max.z && door.bounds.size.y > 1.9f && door.bounds.size.y < 2.1f,
            "Expected an upright two-metre front door behind the steps");
        Vector2 porch = new Vector2(door.bounds.center.x, stairs.bounds.max.z);
        Vector2[] trail =
        {
            porch + Vector2.down * 0.32f, porch + new Vector2(-0.06f, 2.6f),
            porch + new Vector2(-0.50f, 5.4f), porch + new Vector2(-0.29f, 8.5f),
            porch + new Vector2(0.72f, 12.2f), porch + new Vector2(2.15f, 15.5f),
            porch + new Vector2(2.4f, 18.1f)
        };

        Terrain terrain = CreateTerrain(terrainRoot, footprint, trail);
        int[] treeCounts = PlantForest(forestRoot, terrain, trees, footprint, trail);
        ConfigureLighting(lighting);
        Vector3 spawn = OnGround(terrain, porch + Vector2.up * 4.3f) + Vector3.up * 0.02f;
        Transform player = CreatePlayer(spawn, door.bounds.center);
        CreateBoundary(boundaries, footprint, player, CreateWarning());
        ValidateScene(terrain, forestRoot, footprint, player, house, treeCounts);

        Debug.Log($"FIRST_MIND_MEASURES: raw {XYZ(raw.size)}, placed {XYZ(CombinedBounds(house).size)}, " +
            $"walls {XYZ(footprint.size)}, floor {XYZ(floor.bounds.size)}, door {XYZ(door.bounds.size)}, spawn {XYZ(spawn)}");
        Debug.Log("FIRST_MIND_TREES: " + string.Join(", ", TreeTypes.Select((type, i) => $"{type}={treeCounts[i]}")));
        MeasureFurniture();
        Require(EditorSceneManager.SaveScene(scene, ScenePath), "Could not save " + ScenePath);
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Require(Object.FindFirstObjectByType<ForestHouseBoundary>() != null && Object.FindFirstObjectByType<Terrain>() != null,
            "Saved scene could not be reopened with terrain and boundary");
        var savedHouse = GameObject.Find("House").transform.GetChild(0);
        Require(Vector3.Distance(savedHouse.position, translation) < 0.002f &&
            Mathf.Abs(savedHouse.localScale.x - HouseUnitScale * houseAsset.transform.localScale.x) < 0.00001f &&
            GameObject.Find("Forest").transform.childCount == treeCounts.Sum(), "Saved prefab transforms did not persist");
        Debug.Log($"FIRST_MIND_SCENE_SAVED: {ScenePath}");
    }

    private static void ValidateImports(GameObject house, GameObject[] trees)
    {
        Require(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset, "The project is not running URP");
        ValidateMaterials(house);
        foreach (GameObject tree in trees)
        {
            ValidateMaterials(tree);
            var group = tree.GetComponent<LODGroup>();
            Require(group != null, "Missing LODGroup: " + tree.name);
            LOD[] lods = group.GetLODs();
            Require(lods.Length == 3 && lods.All(lod => lod.renderers.Length > 0 && lod.renderers.All(r => r != null)),
                "Missing conifer LOD renderers: " + tree.name);
            Require(lods[2].renderers.All(r => r is BillboardRenderer board && board.billboard != null),
                "Missing billboard asset: " + tree.name);
            Debug.Log($"FIRST_MIND_IMPORT: {tree.name}, LODs {lods.Length}, size {group.size:F1} m");
        }

        foreach (Texture texture in AssetDatabase.LoadAllAssetsAtPath(Models + "/old_russian_house.glb").OfType<Texture>())
            Require(texture.width > 1 && texture.height > 1, "Invalid house texture: " + texture.name);
    }

    private static void ValidateMaterials(GameObject asset)
    {
        Renderer[] renderers = asset.GetComponentsInChildren<Renderer>();
        Require(renderers.Length > 0, "Missing renderers: " + asset.name);
        var materials = renderers.SelectMany(r => r is BillboardRenderer billboard
            ? new[] { billboard.billboard != null ? billboard.billboard.material : null }
            : r.sharedMaterials);
        foreach (Material material in materials.Distinct())
        {
            Require(material != null && material.shader != null && material.shader.name != "Hidden/InternalErrorShader",
                "Missing shader or material: " + asset.name);
            string graphPath = AssetDatabase.GetAssetPath(material.shader);
            if (graphPath.StartsWith("Packages/", StringComparison.Ordinal))
            {
                var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(graphPath);
                graphPath = Path.Combine(package.resolvedPath, graphPath.Substring(package.assetPath.Length + 1));
            }
            bool hasUrpTarget = graphPath.EndsWith(".shadergraph", StringComparison.OrdinalIgnoreCase) &&
                File.ReadAllText(graphPath).Contains("UnityEditor.Rendering.Universal.ShaderGraph.UniversalTarget");
            Require((hasUrpTarget || material.GetTag("RenderPipeline", false) == "UniversalPipeline") &&
                !ShaderUtil.ShaderHasError(material.shader),
                "Expected a working URP shader on " + asset.name + ": " + material.name + " (" + material.shader.name + ")");
        }
    }

    private static void ConfigureLighting(Transform parent)
    {
        GameObject sunlight = Group("Morning Sun", parent).gameObject;
        sunlight.transform.rotation = Quaternion.Euler(18f, -145f, 0f);
        Light sun = sunlight.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.useColorTemperature = true;
        sun.colorTemperature = 5450f;
        sun.intensity = 1.65f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.80f;
        sun.shadowBias = 0.07f;
        sun.shadowNormalBias = 0.32f;
        sunlight.AddComponent<UniversalAdditionalLightData>().softShadowQuality = SoftShadowQuality.Medium;

        var sky = new Material(Shader.Find("Skybox/Procedural")) { name = "Summer Morning Sky" };
        sky.SetColor("_SkyTint", new Color(0.55f, 0.65f, 0.73f));
        sky.SetColor("_GroundColor", new Color(0.29f, 0.32f, 0.28f));
        sky.SetFloat("_AtmosphereThickness", 0.86f);
        sky.SetFloat("_SunSize", 0.031f);
        sky.SetFloat("_SunSizeConvergence", 2.2f);
        sky.SetFloat("_Exposure", 0.93f);
        RenderSettings.skybox = Store(sky, "Summer Morning Sky.mat");
        RenderSettings.sun = sun;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.60f, 0.69f, 0.79f);
        RenderSettings.ambientEquatorColor = new Color(0.44f, 0.51f, 0.56f);
        RenderSettings.ambientGroundColor = new Color(0.32f, 0.30f, 0.26f);
        RenderSettings.reflectionIntensity = 0.44f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.61f, 0.68f, 0.72f);
        RenderSettings.fogDensity = 0.020f;

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.name = "Forest Morning";
        profile.Add<Tonemapping>().mode.Override(TonemappingMode.ACES);
        profile.Add<WhiteBalance>().temperature.Override(-6f);
        var colors = profile.Add<ColorAdjustments>();
        colors.postExposure.Override(0.12f);
        colors.contrast.Override(4f);
        colors.saturation.Override(-4f);
        profile.Add<ShadowsMidtonesHighlights>().shadows.Override(new Vector4(0.978f, 1.004f, 1.034f, 0f));

        var volume = Group("Forest Morning Volume", parent).gameObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = StoreProfile(profile);
    }

    private static Transform CreatePlayer(Vector3 spawn, Vector3 target)
    {
        GameObject player = Group("Player").gameObject;
        Vector3 facing = target - spawn;
        facing.y = 0f;
        player.transform.SetPositionAndRotation(spawn, Quaternion.LookRotation(facing));
        var controller = player.AddComponent<CharacterController>();
        controller.height = 1.84f;
        controller.radius = 0.30f;
        controller.center = Vector3.up * 0.92f;
        controller.slopeLimit = 48f;
        controller.stepOffset = 0.32f;
        controller.skinWidth = 0.035f;
        controller.minMoveDistance = 0f;

        GameObject view = Group("First Person Camera", player.transform).gameObject;
        view.tag = "MainCamera";
        view.transform.localPosition = Vector3.up * 1.69f;
        var camera = view.AddComponent<Camera>();
        camera.fieldOfView = 65f;
        camera.nearClipPlane = 0.045f;
        camera.farClipPlane = 128f;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        view.AddComponent<AudioListener>();

        AudioSource footsteps = player.AddComponent<AudioSource>();
        footsteps.playOnAwake = false;
        footsteps.volume = 0.27f;

        var fps = new SerializedObject(player.AddComponent<FirstPersonPlayer>());
        fps.FindProperty("playerCamera").objectReferenceValue = camera;
        fps.FindProperty("footstepSource").objectReferenceValue = footsteps;
        fps.FindProperty("standingHeight").floatValue = 1.84f;
        fps.FindProperty("normalFieldOfView").floatValue = 65f;
        fps.FindProperty("walkSpeed").floatValue = 3.1f;
        fps.FindProperty("crouchSpeed").floatValue = 1.7f;
        fps.FindProperty("sprintSpeed").floatValue = 4.8f;
        fps.FindProperty("mouseSensitivity").floatValue = 0.10f;
        fps.FindProperty("mouseSmoothingSeconds").floatValue = 0.047f;
        var clips = fps.FindProperty("footstepClips");
        string folder = "Assets/Footsteps - Essentials/Footsteps_DirtyGround/Footsteps_DirtyGround_Walk";
        clips.arraySize = 8;
        for (int i = 0; i < clips.arraySize; i++)
            clips.GetArrayElementAtIndex(i).objectReferenceValue = Load<AudioClip>($"{folder}/Footsteps_DirtyGround_Walk_{i + 1:00}.wav");
        fps.ApplyModifiedPropertiesWithoutUndo();
        return player.transform;
    }

    private static CanvasGroup CreateWarning()
    {
        var ui = new GameObject("UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        ui.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = ui.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.75f;

        var panel = new GameObject("Distance Warning", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        panel.transform.SetParent(ui.transform, false);
        var rect = (RectTransform)panel.transform;
        rect.anchorMin = new Vector2(0.16f, 0.11f);
        rect.anchorMax = new Vector2(0.84f, 0.11f);
        rect.sizeDelta = new Vector2(0f, 58f);
        var image = panel.GetComponent<Image>();
        image.sprite = WarningBackdrop();
        image.raycastTarget = false;

        var label = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.transform.SetParent(panel.transform, false);
        rect = (RectTransform)label.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(18f, 0f);
        rect.offsetMax = new Vector2(-18f, 0f);

        var tmp = label.GetComponent<TextMeshProUGUI>();
        tmp.font = WarningFont();
        tmp.text = ForestHouseBoundary.WarningText;
        tmp.fontSize = 28f;
        tmp.fontSizeMin = 16f;
        tmp.fontSizeMax = 28f;
        tmp.enableAutoSizing = true;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.92f, 0.94f, 0.915f);
        tmp.raycastTarget = false;

        var group = panel.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
        return group;
    }

    private static TMP_FontAsset WarningFont()
    {
        const string path = DataFolder + "/Forest Warning Font.asset";
        TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (existing != null) return existing;
        Font source = Load<Font>("Assets/TextMesh Pro/Fonts/LiberationSans.ttf");
        var font = TMP_FontAsset.CreateFontAsset(source, 70, 8, GlyphRenderMode.SDFAA, 512, 256,
            AtlasPopulationMode.Dynamic, false);
        Require(font.TryAddCharacters(ForestHouseBoundary.WarningText), "Missing Cyrillic glyphs in Liberation Sans");
        font.name = "Forest Warning Font";
        font.atlasPopulationMode = AtlasPopulationMode.Static;
        AssetDatabase.CreateAsset(font, path);
        font.material.name = "Forest Warning SDF";
        AssetDatabase.AddObjectToAsset(font.material, font);
        foreach (Texture2D atlas in font.atlasTextures)
        {
            atlas.name = "Forest Warning Atlas";
            AssetDatabase.AddObjectToAsset(atlas, font);
        }
        return font;
    }

    private static Sprite WarningBackdrop()
    {
        const string path = DataFolder + "/Warning Backdrop.asset";
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing != null) return existing;
        const int width = 192;
        const int height = 32;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = "Warning Gradient" };
        var pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            float h = Mathf.SmoothStep(0, 1, Mathf.Min(x, width - 1 - x) / 58f);
            float v = Mathf.SmoothStep(0, 1, Mathf.Min(y, height - 1 - y) / 9f);
            pixels[y * width + x] = new Color32(12, 23, 24, (byte)(185f * h * v));
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, width, height), Vector2.one * 0.5f);
        sprite.name = "Warning Backdrop";
        AssetDatabase.CreateAsset(sprite, path);
        AssetDatabase.AddObjectToAsset(texture, sprite);
        return sprite;
    }

    private static void CreateBoundary(Transform parent, Bounds bounds, Transform player, CanvasGroup warning)
    {
        Transform footprintObject = Group("House Footprint", parent);
        footprintObject.position = new Vector3(bounds.center.x, 0f, bounds.center.z);
        var footprint = footprintObject.gameObject.AddComponent<BoxCollider>();
        footprint.enabled = false;
        footprint.size = new Vector3(bounds.size.x, 1f, bounds.size.z);
        parent.gameObject.AddComponent<ForestHouseBoundary>().Configure(player, footprint, warning);

        // A single continuous inward-facing surface lets the capsule slide along the rounded corners.
        const int cornerSteps = 20;
        const int count = 4 * (cornerSteps + 1);
        var vertices = new Vector3[count * 2];
        var triangles = new int[count * 6];
        for (int corner = 0; corner < 4; corner++)
        {
            float start = corner * 90f;
            float middle = (start + 45f) * Mathf.Deg2Rad;
            var center = new Vector2(Mathf.Sign(Mathf.Cos(middle)) * footprint.size.x / 2f,
                Mathf.Sign(Mathf.Sin(middle)) * footprint.size.z / 2f);
            for (int step = 0; step <= cornerSteps; step++)
            {
                float angle = (start + step * 90f / cornerSteps) * Mathf.Deg2Rad;
                Vector2 p = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * BoundaryRadius;
                int i = corner * (cornerSteps + 1) + step;
                int a = 2 * i;
                int b = 2 * ((i + 1) % count);
                vertices[a] = new Vector3(p.x, TerrainBottom, p.y);
                vertices[a + 1] = new Vector3(p.x, 14f, p.y);
                int k = i * 6;
                triangles[k] = a;
                triangles[k + 1] = b;
                triangles[k + 2] = a + 1;
                triangles[k + 3] = a + 1;
                triangles[k + 4] = b;
                triangles[k + 5] = b + 1;
            }
        }

        var mesh = new Mesh { name = "Forest Boundary", vertices = vertices, triangles = triangles };
        mesh.RecalculateBounds();
        Transform limit = Group("Forest Limit", footprintObject);
        limit.gameObject.AddComponent<MeshCollider>().sharedMesh = Store(mesh, "Forest Boundary.asset");
    }

    private static void ValidateScene(Terrain terrain, Transform forest, Bounds footprint, Transform player,
        GameObject house, int[] treeCounts)
    {
        Require(treeCounts.All(count => count > 0) && treeCounts.Sum() == forest.childCount, "Incomplete forest");
        Require(treeCounts[0] > treeCounts[1] && treeCounts[0] > treeCounts[2] && treeCounts[3] < treeCounts[2],
            "Forest composition is inconsistent");
        Bounds exterior = CombinedBounds(house);
        Bounds floor = HousePart(house, "_Pol_").bounds;
        Vector3[] corners = {
            new Vector3(floor.min.x, 0, floor.min.z), new Vector3(floor.min.x, 0, floor.max.z),
            new Vector3(floor.max.x, 0, floor.min.z), new Vector3(floor.max.x, 0, floor.max.z)
        };
        float[] gaps = corners.Select(p => floor.min.y - (terrain.SampleHeight(p) + terrain.transform.position.y)).ToArray();
        Require(gaps.All(gap => gap > -0.06f && gap < 0.04f), "House foundation is not seated on level ground");
        float nearestTree = forest.Cast<Transform>().Min(tree => DistanceToFootprint(tree.position, footprint));
        Require(nearestTree > 7f && forest.Cast<Transform>().All(tree => !exterior.Contains(
                new Vector3(tree.position.x, exterior.center.y, tree.position.z))),
            "Trees encroach on the house or reserve clearing");
        Require(terrain.terrainData.size.x == TerrainSize && terrain.GetComponent<TerrainCollider>() != null,
            "Unexpected terrain size or missing terrain collision");
        Require(Mathf.Abs(player.position.y - terrain.SampleHeight(player.position) - terrain.transform.position.y) < 0.03f,
            "Spawn is not on the ground");

        Physics.SyncTransforms();
        Camera camera = player.GetComponentInChildren<Camera>();
        Require(Physics.Raycast(camera.transform.position, camera.transform.forward, out RaycastHit doorHit, 8f) &&
            doorHit.collider.gameObject == HousePart(house, "_Door1_").gameObject, "Spawn is not facing the front door");
        Require(Physics.OverlapCapsule(player.position + Vector3.up * 0.34f,
                player.position + Vector3.up * 1.54f, 0.28f).All(c => c.transform == player),
            "The player spawns in another collider");

        var warning = Object.FindFirstObjectByType<TextMeshProUGUI>();
        Require(warning.text == ForestHouseBoundary.WarningText && warning.font.HasCharacters(warning.text),
            "Warning text or Cyrillic glyphs are missing");
        Require(RenderSettings.fog && RenderSettings.sun != null &&
            camera.GetUniversalAdditionalCameraData().renderPostProcessing, "Scene lighting is incomplete");
        Debug.Log($"FIRST_MIND_CLEARANCE: nearest trunk {nearestTree:F2} m from house bounds; " +
            $"foundation offset {gaps.Min():F3} to {gaps.Max():F3} m");
    }

    private static void MeasureFurniture()
    {
        string[] items = { "Sofa", "old_wooden_table", "old_wooden_cabinet", "1980s_tv_3d_scan", "old_fridge",
            "soviet_vintage_kitchen_cabinet_pbr_lowpoly", "old_ussr_bath", "toilet", "old_dirty_sink_-_low_poly", "ussr_chair" };
        foreach (string item in items)
        {
            GameObject imported = Load<GameObject>($"{Models}/{item}.glb");
            Debug.Log($"FIRST_MIND_FURNITURE: {item} {XYZ(CombinedBounds(imported).size)} [import units]");
        }
    }

    private static Bounds CombinedBounds(GameObject asset)
    {
        Renderer[] renderers = asset.GetComponentsInChildren<Renderer>();
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }

    private static string PartName(MeshRenderer part) => part.name + " " + part.GetComponent<MeshFilter>().sharedMesh.name;

    private static MeshRenderer HousePart(GameObject house, string token) =>
        house.GetComponentsInChildren<MeshRenderer>().Single(part => PartName(part).Contains(token));

    private static Transform Group(string name, Transform parent = null)
    {
        Transform child = new GameObject(name).transform;
        child.SetParent(parent, false);
        return child;
    }

    private static T Load<T>(string path) where T : Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        Require(asset != null, "Could not import " + path);
        return asset;
    }

    private static T Store<T>(T asset, string filename) where T : Object
    {
        string path = DataFolder + "/" + filename;
        T previous = AssetDatabase.LoadAssetAtPath<T>(path);
        if (previous == null)
        {
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
        EditorUtility.CopySerialized(asset, previous);
        Object.DestroyImmediate(asset);
        EditorUtility.SetDirty(previous);
        return previous;
    }

    private static VolumeProfile StoreProfile(VolumeProfile profile)
    {
        string path = DataFolder + "/Forest Morning.asset";
        var previous = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (previous != null)
        {
            foreach (var component in previous.components.ToArray()) Object.DestroyImmediate(component, true);
            previous.components.Clear();
            previous.components.AddRange(profile.components);
            Object.DestroyImmediate(profile);
            profile = previous;
        }
        else AssetDatabase.CreateAsset(profile, path);
        foreach (var component in profile.components)
        {
            component.name = component.GetType().Name;
            AssetDatabase.AddObjectToAsset(component, profile);
        }
        EditorUtility.SetDirty(profile);
        return profile;
    }

    private static string XYZ(Vector3 vector) => $"({vector.x:F3}, {vector.y:F3}, {vector.z:F3})";

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
