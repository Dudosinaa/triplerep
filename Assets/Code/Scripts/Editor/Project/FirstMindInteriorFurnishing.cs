using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed partial class FirstMindInteriorBuilder
{
    private void MakeMaterials()
    {
        const string boards = "Assets/ALP_Assets/country house01/Textures/";
        wood = Painted("Old pine floorboards", new Color32(223, 184, 146, 255),
            boards + "WoodPlank01.png", null, 0.18f);
        trim = Painted("Brown lacquered pine", new Color32(112, 77, 49, 255),
            boards + "WoodPlank01.png", boards + "WoodPlank01_N.png", 0.28f);
        ceiling = Painted("Warm limewashed ceiling", new Color32(232, 217, 190, 255),
            "Assets/Art/Textures/Home/Wall_textures/plastered_wall_04_diff_4k.png", null, 0.13f);
        const string wallpaper = "Assets/ThirdParty/VintageVictorianWallpapersFree/Textures/Wallpapers/";
        cream = Painted("Faded floral wallpaper", new Color32(231, 219, 194, 255),
            wallpaper + "Wallpaper_13_MultiColor/Wallpaper_13_MultiColor_BaseColor.png", null, 0.12f);
        green = Painted("Muted sage wallpaper", new Color32(195, 197, 176, 255),
            wallpaper + "Wallpaper_12_LightGreen/Wallpaper_12_LightGreen_BaseColor.png", null, 0.14f);
        tile = Painted("Worn bathroom floor tile", new Color32(202, 195, 178, 255), null, null, 0.25f);
        tile.SetTexture("_BaseMap", MakeTileTexture());
        tile.SetTextureScale("_BaseMap", new Vector2(2.4f, 2.4f));
        EditorUtility.SetDirty(tile);
        metal = Painted("Dull painted hatch steel", new Color32(91, 94, 80, 255), null, null, 0.35f);
        metal.SetFloat("_Metallic", 0.55f);
        EditorUtility.SetDirty(metal);
        opal = Painted("Warm opal glass", new Color32(244, 221, 166, 255), null, null, 0.30f);
        opal.EnableKeyword("_EMISSION");
        opal.SetColor("_EmissionColor", new Color(1f, 0.77f, 0.45f) * 0.65f);
        EditorUtility.SetDirty(opal);
    }

    private static Material Painted(string name, Color color, string texture, string normal, float smoothness)
    {
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", smoothness);
        if (texture != null) material.SetTexture("_BaseMap", Load<Texture2D>(texture));
        if (normal != null)
        {
            material.SetTexture("_BumpMap", Load<Texture2D>(normal));
            material.SetFloat("_BumpScale", 0.62f);
            material.EnableKeyword("_NORMALMAP");
        }
        return Store(material, name + ".mat");
    }

    private static Texture2D MakeTileTexture()
    {
        const int size = 128;
        const int tileSize = 32;
        var pixels = new Color32[size * size];
        var random = new System.Random(1982);
        var glaze = new float[16];
        for (int i = 0; i < glaze.Length; i++) glaze[i] = 0.87f + (float)random.NextDouble() * 0.14f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            if (x % tileSize == 0 || y % tileSize == 0) pixels[y * size + x] = new Color32(116, 110, 102, 255);
            else
            {
                float tone = glaze[y / tileSize * 4 + x / tileSize] * (0.94f + 0.11f * Mathf.PerlinNoise(x * 0.11f, y * 0.11f));
                pixels[y * size + x] = new Color(0.66f * tone, 0.71f * tone, 0.67f * tone);
            }
        }
        var texture = new Texture2D(size, size, TextureFormat.RGB24, true) { name = "Small ceramic tiles" };
        texture.SetPixels32(pixels);
        texture.Apply();
        return Store(texture, "Small ceramic tiles.asset");
    }

    private void FurnishLiving()
    {
        var sofa = Place("Sofa", living, "Sofa", -1.50f, -1.29f, 0.007f, new Vector3(0.48f, 0.48f, 0.48f), 90f);
        FitCollider(sofa);
        Sculpt("Wall carpet", living, "an_old_soviet_carpet", At(-1.50f, -1.725f, 0.78f),
            Quaternion.Euler(90f, 0f, 0f), Vector3.one * 2.02f);

        Vector3 stand = At(-1.285f, 1.475f, 0.29f);
        var cabinet = Group("Television stand", living);
        Block("Body", cabinet, stand, new Vector3(0.82f, 0.48f, 0.60f), trim, true);
        Block("Top", cabinet, stand + Vector3.up * 0.25f, new Vector3(0.86f, 0.045f, 0.65f), wood);
        Block("Inset drawer", cabinet, stand + new Vector3(0f, 0.10f, -0.305f), new Vector3(0.72f, 0.14f, 0.022f), trim);
        Cylinder("Drawer pull", cabinet, stand + new Vector3(0f, 0.10f, -0.33f),
            new Vector3(0.026f, 0.014f, 0.026f), metal, Quaternion.Euler(90f, 0f, 0f));
        PlaceTelevision();

        Transform wardrobe = Place("Wooden cupboard", living, "old_wooden_cabinet", -3.16f, 1.20f, 0.007f,
            Vector3.one * 0.32f, -90f);
        FitCollider(wardrobe);

        Transform radioTable = Place("Rounded radio table", living, "scandinavian_rounded_side_table", -0.36f, -0.79f,
            0.007f, Vector3.one * 0.77f, 0f);
        FitCollider(radioTable);
        Place("Vintage radio", radioTable, "vintage_radio", -0.36f, -0.79f,
            Combined(radioTable).max.y - floorY + 0.002f, Vector3.one * 0.225f, 7f);

        Transform desk = Place("Writing desk", living, "old_desk", 0.57f, 0.94f, 0.007f, Vector3.one * 0.0169f, -90f);
        FitCollider(desk);
        Transform chair = Place("Desk chair", living, "ussr_chair", -0.06f, 0.99f, 0.007f, Vector3.one * 0.92f, 90f);
        FitCollider(chair);
        float deskTop = Combined(desk).max.y - floorY;
        Transform lamp = Place("Desk lamp", desk, "old_ussr_lamp", 0.63f, 1.37f, deskTop + 0.003f,
            Vector3.one * 0.0103f, -90f);
        Bounds lampBounds = Combined(lamp);
        AddSpot("Warm reading light", lights, new Vector3(lampBounds.center.x - 0.08f, lampBounds.max.y - 0.16f,
            lampBounds.center.z), Quaternion.Euler(74f, -90f, 0f), 1.4f, 2.25f, 100f, new Color(1f, 0.72f, 0.44f), false);

        Place("Wall clock", living, "old_wall_clock", -0.09f, -1.71f, 1.63f, Vector3.one * 0.86f, 0f);
        Transform bakelite = Group("Bakelite switch", living);
        var switchInstance = Object.Instantiate(Load<GameObject>(Models + "vintage_bakelit_light_switch.glb"), bakelite);
        foreach (var renderer in switchInstance.GetComponentsInChildren<MeshRenderer>())
        {
            string partName = NameOf(renderer);
            renderer.enabled = new[] { "base_", "cover_", "switch_lever_", "screw_1_", "screw_2_" }
                .Any(partName.Contains);
        }
        switchInstance.transform.rotation = Quaternion.Euler(0f, 180f, 0f) * switchInstance.transform.rotation;
        switchInstance.transform.localScale = Vector3.one * 1.37f;
        MoveToSupport(bakelite, At(-4.99f, 0.043f, 1.15f), switchInstance.transform);
    }

    private void FurnishKitchen()
    {
        FitCollider(Place("Kitchen buffet", kitchen, "soviet_vintage_kitchen_cabinet_pbr_lowpoly", -1.78f, -2.16f,
            0.007f, Vector3.one * 0.635f, -90f));
        Transform fridge = Place("Refrigerator", kitchen, "old_fridge", -0.76f, -2.15f, 0.011f, Vector3.one, 180f);
        MakeSolidMaterials(fridge);
        FitCollider(fridge);
        Transform table = Place("Dining table", kitchen, "old_wooden_table", -0.68f, -3.40f,
            0.007f, new Vector3(0.58f, 0.88f, 0.60f), 0f);
        FitTableCollider(table);
        FitCollider(Place("Kitchen chair by cupboard", kitchen, "ussr_chair", -1.55f, -3.39f,
            0.007f, Vector3.one * 0.92f, 90f));
        FitCollider(Place("Kitchen chair by door", kitchen, "ussr_chair", 0.18f, -3.39f,
            0.007f, Vector3.one * 0.92f, -90f));
        float top = Combined(table).max.y - floorY + 0.002f;
        Place("Kettle", table, "kettle", -0.97f, -3.47f, top, Vector3.one * 0.76f, 24f);
        Place("Cooking pot", table, "panela_velha__old_pot", -0.53f, -3.46f, top, Vector3.one * 0.0101f, 15f);
        SmallKitchenProp(table, "cup_cup", "Enamel cup", -0.22f, -3.43f, top, 0.66f);
    }

    private void SmallKitchenProp(Transform parent, string token, string name, float x, float z, float y, float scale)
    {
        GameObject sample = Object.Instantiate(Load<GameObject>(Models + "post-soviet_furniture_set_with_dithering.glb"));
        MeshRenderer part = sample.GetComponentsInChildren<MeshRenderer>().Single(renderer => NameOf(renderer).Contains(token));
        Mesh mesh = Bake(part, Matrix4x4.identity);
        Material material = part.sharedMaterial;
        Object.DestroyImmediate(sample);
        Vector3 center = mesh.bounds.center;
        center.y = mesh.bounds.min.y;
        TransformMesh(mesh, Matrix4x4.Scale(Vector3.one * scale) * Matrix4x4.Translate(-center));
        mesh.name = name;
        var prop = Group(name, parent);
        prop.position = At(x, z, y);
        prop.gameObject.AddComponent<MeshFilter>().sharedMesh = Store(mesh, name + ".asset");
        prop.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
    }

    private void FurnishBathroom()
    {
        FitCollider(Place("Enamel bath", bath, "old_ussr_bath", -4.745f, -3.075f, 0.013f,
            Vector3.one * 0.00955f, -90f), 0.55f);
        Transform toilet = Place("Toilet and cistern", bath, "toilet", -3.935f, -3.49f, 0.013f, Vector3.one * 0.0101f, -90f);
        MakeSolidMaterials(toilet);
        FitCollider(toilet);
        Transform basin = Place("Porcelain basin", bath, "old_dirty_sink_-_low_poly", -2.920f, -2.754f,
            0.487f, Vector3.one * 0.214f, 0f);
        FitCollider(basin);
        Cylinder("Basin pedestal", basin, At(-2.92f, -2.754f, 0.292f), new Vector3(0.19f, 0.28f, 0.21f),
            ceiling, Quaternion.identity, true);
    }

    private void DressWindows(MeshRenderer[] windows)
    {
        var source = Object.Instantiate(Load<GameObject>(Models + "old_curtains.glb"));
        MeshRenderer renderer = source.GetComponentInChildren<MeshRenderer>();
        Material fabric = renderer.sharedMaterial;
        Mesh mesh = Bake(renderer, Matrix4x4.identity);
        var vertices = mesh.vertices;
        var triangles = mesh.GetTriangles(0);
        var selected = new System.Collections.Generic.List<int>();
        var used = new System.Collections.Generic.HashSet<int>();
        for (int i = 0; i < triangles.Length; i += 3)
        {
            int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
            Vector3 middle = (vertices[a] + vertices[b] + vertices[c]) / 3f;
            if (middle.x < -82.5f || middle.x > -10f || middle.z < -32f || middle.z > -26f) continue;
            selected.Add(a); selected.Add(b); selected.Add(c);
            used.Add(a); used.Add(b); used.Add(c);
        }
        Check(selected.Count >= 42, "No curtain fabric panel identified");
        Mesh panel = new Mesh { name = "Original draped curtain panel" };
        var vertexList = used.OrderBy(i => i).ToArray();
        var remap = vertexList.Select((id, i) => (id, i)).ToDictionary(pair => pair.id, pair => pair.i);
        panel.vertices = vertexList.Select(i => vertices[i]).ToArray();
        Vector3[] normals = mesh.normals;
        panel.normals = vertexList.Select(i => normals[i]).ToArray();
        Vector2[] uvs = mesh.uv;
        panel.uv = vertexList.Select(i => uvs[i]).ToArray();
        panel.triangles = selected.Select(i => remap[i]).ToArray();
        panel.RecalculateBounds();
        Bounds sourceBounds = panel.bounds;
        TransformMesh(panel, Matrix4x4.Translate(-sourceBounds.center));
        panel = Store(panel, "Curtain panel.asset");
        Object.DestroyImmediate(mesh);
        Object.DestroyImmediate(source);

        var drapery = Group("Window Curtains", house.Find("Interior"));
        int index = 0;
        foreach (MeshRenderer window in windows)
        {
            Bounds bounds = window.bounds;
            Vector3 p = (bounds.center - model.position) / Scale;
            Vector3 towardRoom = p.x < -4.95f ? Vector3.right : p.x > 0.89f ? Vector3.left :
                p.z < -3.58f ? Vector3.forward : Vector3.back;
            bool side = Mathf.Abs(towardRoom.x) > 0.5f;
            float width = (side ? bounds.size.z : bounds.size.x) + 0.13f;
            float top = Mathf.Min(floorY + Ceiling - 0.052f, bounds.max.y + 0.11f);
            float bottom = bounds.min.y - 0.028f;
            float height = top - bottom;
            Vector3 center = bounds.center + towardRoom * ((side ? bounds.size.x : bounds.size.z) * 0.5f + 0.057f);
            center.y = (top + bottom) / 2f;
            var curtain = Group($"Curtain {++index:00}", drapery);
            curtain.SetPositionAndRotation(center, Quaternion.LookRotation(towardRoom));
            var pleats = Group("Pleated fabric", curtain);
            pleats.localScale = new Vector3(width / sourceBounds.size.x, height / sourceBounds.size.y, 0.060f / sourceBounds.size.z);
            pleats.gameObject.AddComponent<MeshFilter>().sharedMesh = panel;
            pleats.gameObject.AddComponent<MeshRenderer>().sharedMaterial = fabric;
            Cylinder("Curtain rail", curtain, new Vector3(center.x, top + 0.012f, center.z),
                new Vector3(0.028f, (width + 0.085f) * 0.5f, 0.028f), trim,
                curtain.rotation * Quaternion.Euler(0f, 0f, 90f));
        }
        Check(index == 8, "Expected to fit eight original windows");
    }

    private void AddHatch()
    {
        var hatch = Group("BasementHatch", house);
        hatch.position = At(-2.20f, -3.155f, 0.0085f);
        foreach (float end in new[] { -1f, 1f })
        {
            Block("Steel angle", hatch, hatch.position + new Vector3(0f, 0.003f, end * 0.338f), new Vector3(0.86f, 0.008f, 0.035f), metal);
            Block("Steel angle", hatch, hatch.position + new Vector3(end * 0.414f, 0.003f, 0f), new Vector3(0.031f, 0.008f, 0.65f), metal);
            Block("Hinge", hatch, hatch.position + new Vector3(end * 0.26f, 0.006f, -0.329f), new Vector3(0.106f, 0.018f, 0.041f), metal);
            Block("Pull bracket", hatch, hatch.position + new Vector3(end * 0.062f, 0.010f, 0.205f), new Vector3(0.022f, 0.012f, 0.029f), metal);
        }
        Block("Steel lid", hatch, hatch.position, new Vector3(0.796f, 0.004f, 0.637f), metal);
        Block("Recessed pull", hatch, hatch.position + new Vector3(0f, 0.016f, 0.205f), new Vector3(0.136f, 0.010f, 0.017f), metal);
    }

    private void LightRooms()
    {
        CeilingLight("Living room shade", -1.48f, 0.14f, new Color(1f, 0.78f, 0.53f), 5.8f, 4.15f);
        CeilingLight("Kitchen shade", -0.84f, -2.79f, new Color(1f, 0.85f, 0.64f), 4.5f, 3.10f);
        CeilingLight("Bathroom shade", -3.96f, -2.96f, new Color(1f, 0.91f, 0.77f), 3.6f, 2.80f);
    }

    private static void MakeSolidMaterials(Transform item)
    {
        var materials = new System.Collections.Generic.Dictionary<Material, Material>();
        foreach (MeshRenderer renderer in item.GetComponentsInChildren<MeshRenderer>())
        {
            Material[] slots = renderer.sharedMaterials;
            for (int i = 0; i < slots.Length; i++)
            {
                Material source = slots[i];
                if (source.renderQueue < (int)RenderQueue.Transparent) continue;
                if (!materials.TryGetValue(source, out Material solid))
                {
                    solid = new Material(source) { name = item.name + " opaque " + materials.Count };
                    solid.SetOverrideTag("RenderType", "Opaque");
                    solid.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    solid.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    solid.SetFloat("_Surface", 0f);
                    solid.SetFloat("_ZWrite", 1f);
                    solid.SetFloat("_SrcBlend", (float)BlendMode.One);
                    solid.SetFloat("_DstBlend", (float)BlendMode.Zero);
                    solid.SetFloat("_AlphaDstBlend", (float)BlendMode.Zero);
                    solid.SetShaderPassEnabled("DepthOnly", true);
                    solid.SetShaderPassEnabled("ShadowCaster", true);
                    solid.renderQueue = (int)RenderQueue.Geometry;
                    solid = Store(solid, solid.name + ".mat");
                    materials.Add(source, solid);
                }
                slots[i] = solid;
            }
            renderer.sharedMaterials = slots;
        }
    }

    private void CeilingLight(string name, float x, float z, Color color, float intensity, float range)
    {
        Transform mount = Group(name, lights);
        mount.position = At(x, z, Ceiling - 0.09f);
        Cylinder("Mount", mount, mount.position + Vector3.up * 0.038f, new Vector3(0.32f, 0.031f, 0.32f),
            trim, Quaternion.identity);
        Cylinder("Opal shade", mount, mount.position - Vector3.up * 0.027f, new Vector3(0.33f, 0.047f, 0.33f),
            opal, Quaternion.identity);
        AddSpot("Downlight", mount, mount.position - Vector3.up * 0.083f, Quaternion.Euler(90f, 0f, 0f),
            range, intensity, 136f, color, true);
    }

    private static void AddSpot(string name, Transform parent, Vector3 position, Quaternion rotation, float range,
        float intensity, float angle, Color color, bool shadows)
    {
        Transform spot = Group(name, parent);
        spot.SetPositionAndRotation(position, rotation);
        var light = spot.gameObject.AddComponent<Light>();
        light.type = LightType.Spot;
        light.color = color;
        light.intensity = intensity;
        light.spotAngle = angle;
        light.innerSpotAngle = angle * 0.46f;
        light.range = range;
        light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        light.shadowStrength = 0.77f;
        light.shadowBias = 0.092f;
        light.shadowNormalBias = 0.19f;
    }

    private Transform Place(string name, Transform parent, string asset, float x, float z, float y, Vector3 scale, float yaw) =>
        Sculpt(name, parent, asset, At(x, z, y), Quaternion.Euler(0f, yaw, 0f), scale);

    private Transform Sculpt(string name, Transform parent, string asset, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        Transform item = Group(name, parent);
        var modelAsset = Load<GameObject>(Models + asset + ".glb");
        Transform visual = Group("Model", item);
        visual.SetPositionAndRotation(position, rotation);
        visual.localScale = scale;
        Object.Instantiate(modelAsset, visual).name = modelAsset.name;
        MoveToSupport(item, position, visual);
        supportedItems.Add((name, CombinedVisible(item)));
        return item;
    }

    private static void MoveToSupport(Transform actual, Vector3 target, Transform visual = null)
    {
        Bounds bounds = CombinedVisible(actual);
        Vector3 bottom = bounds.center;
        bottom.y = bounds.min.y;
        (visual == null ? actual : visual).position += target - bottom;
    }

    private static Bounds CombinedVisible(Transform root)
    {
        var parts = root.GetComponentsInChildren<MeshRenderer>().Where(r => r.enabled).ToArray();
        Check(parts.Length > 0, "No visible parts for " + root.name);
        var bounds = parts[0].bounds;
        foreach (var part in parts) bounds.Encapsulate(part.bounds);
        return bounds;
    }

    private static void FitCollider(Transform item, float maximumHeight = 100f)
    {
        Bounds area = CombinedVisible(item);
        float height = Mathf.Min(maximumHeight, area.size.y);
        var box = item.gameObject.AddComponent<BoxCollider>();
        box.center = item.InverseTransformPoint(new Vector3(area.center.x, area.min.y + height / 2f, area.center.z));
        box.size = new Vector3(Mathf.Max(0.06f, area.size.x - 0.028f), height, Mathf.Max(0.06f, area.size.z - 0.028f));
    }

    private static void FitTableCollider(Transform table)
    {
        Bounds bounds = CombinedVisible(table);
        AddBox(table, new Vector3(bounds.center.x, bounds.max.y - 0.024f, bounds.center.z),
            new Vector3(bounds.size.x, 0.048f, bounds.size.z));
        foreach (float x in new[] { -1f, 1f })
        foreach (float z in new[] { -1f, 1f })
            AddBox(table, new Vector3(bounds.center.x + x * (bounds.extents.x - 0.070f), bounds.center.y,
                    bounds.center.z + z * (bounds.extents.z - 0.053f)),
                new Vector3(0.074f, bounds.size.y, 0.074f));
    }

    private static void AddBox(Transform item, Vector3 center, Vector3 size)
    {
        var collider = item.gameObject.AddComponent<BoxCollider>();
        collider.center = item.InverseTransformPoint(center);
        collider.size = size;
    }

    private static Transform Block(string name, Transform parent, Vector3 center, Vector3 size, Material material,
        bool solid = false, Quaternion? rotation = null) =>
        Primitive(name, parent, PrimitiveType.Cube, center, size, material, rotation ?? Quaternion.identity, solid);

    private static Transform Cylinder(string name, Transform parent, Vector3 center, Vector3 size, Material material,
        Quaternion rotation, bool solid = false) => Primitive(name, parent, PrimitiveType.Cylinder, center, size, material, rotation, solid);

    private static Transform Primitive(string name, Transform parent, PrimitiveType type, Vector3 center, Vector3 size,
        Material material, Quaternion rotation, bool solid)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(center, rotation);
        go.transform.localScale = size;
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
        if (!solid) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go.transform;
    }

    private void SetupPlayer()
    {
        var player = Object.FindFirstObjectByType<FirstPersonPlayer>();
        var serialized = new SerializedObject(player);
        serialized.FindProperty("useFootstepSurfaces").boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        var canvas = GameObject.Find("UI").transform;
        var group = new GameObject("Door Prompt", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        group.transform.SetParent(canvas, false);
        var rect = (RectTransform)group.transform;
        rect.anchorMin = new Vector2(0.5f, 0.42f);
        rect.anchorMax = rect.anchorMin;
        rect.sizeDelta = new Vector2(390f, 46f);
        var image = group.GetComponent<Image>();
        image.sprite = Load<Sprite>(PathOfForestBackdrop());
        image.raycastTarget = false;

        var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.transform.SetParent(rect, false);
        var labelRect = (RectTransform)label.transform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        var text = label.GetComponent<TextMeshProUGUI>();
        text.font = DoorFont();
        text.text = HouseDoorInteraction.OpenText;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 25f;
        text.color = new Color(0.96f, 0.945f, 0.88f);
        text.raycastTarget = false;
        var hint = group.GetComponent<CanvasGroup>();
        hint.alpha = 0f;
        hint.interactable = false;
        hint.blocksRaycasts = false;
        player.gameObject.AddComponent<HouseDoorInteraction>().Configure(player.GetComponentInChildren<Camera>(), text, hint);
    }

    private static string PathOfForestBackdrop() =>
        "Assets/Level/Scenes/Locations/MindsScenes/FirstMindData/Warning Backdrop.asset";

    private static TMP_FontAsset DoorFont()
    {
        string path = Data + "/Door Prompt Font.asset";
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (existing != null) return existing;
        var font = TMP_FontAsset.CreateFontAsset(Load<Font>("Assets/TextMesh Pro/Fonts/LiberationSans.ttf"),
            68, 8, GlyphRenderMode.SDFAA, 512, 256, AtlasPopulationMode.Dynamic, false);
        Check(font.TryAddCharacters(HouseDoorInteraction.OpenText + HouseDoorInteraction.CloseText), "Missing door prompt glyphs");
        font.name = "Door Prompt Font";
        font.atlasPopulationMode = AtlasPopulationMode.Static;
        AssetDatabase.CreateAsset(font, path);
        font.material.name = "Door Prompt SDF";
        AssetDatabase.AddObjectToAsset(font.material, font);
        AssetDatabase.AddObjectToAsset(font.atlasTexture, font);
        return font;
    }

    private void ValidateAuthoredHouse()
    {
        Check(house.Find("Interior/LivingRoom") && house.Find("Interior/Kitchen") && house.Find("Interior/Bathroom"),
            "The furnished plan must have three rooms");
        foreach (var item in supportedItems)
            Check(item.bounds.min.y >= floorY - 0.027f && item.bounds.max.y < floorY + Ceiling + 0.03f,
                item.name + " is outside the ground floor");
        Transform console = living.Find("Rounded radio table");
        Bounds tableTop = CombinedVisible(console.GetChild(0));
        Bounds radio = CombinedVisible(console.Find("Vintage radio"));
        Check(Mathf.Abs(radio.min.y - tableTop.max.y) < 0.012f && Mathf.Abs(radio.center.x - tableTop.center.x) < 0.01f,
            "Radio is not on its own table");
        Check(kitchen.Find("Dining table/Kettle") != null && kitchen.Find("Dining table/Cooking pot") != null,
            "The cookware must sit on the dining table");

        Physics.SyncTransforms();
        foreach (var door in doors.Cast<Transform>())
        {
            var leaf = door.GetComponentInChildren<BoxCollider>(true);
            Check(leaf != null && door.GetComponent<AudioSource>().spatialBlend > 0.98, door.name + " is missing collision or 3D audio");
        }
    }
}
