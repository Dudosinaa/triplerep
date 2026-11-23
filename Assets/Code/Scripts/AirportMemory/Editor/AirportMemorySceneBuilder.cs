using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class AirportMemorySceneBuilder
{
    private const string ScenePath = "Assets/Level/Scenes/Locations/MindsScenes/AirportCargoMemory.unity";
    private const string MatFolder = "Assets/Art/Materials/AirportMemory";
    private const string PlayerPath = "Assets/Level/Prefabs/Resources/Player.prefab";
    private const string WarehousePath = "Assets/Art/Models/AirportMemory/warehouse6.fbx";
    private const string BoxesFolder = "Assets/PolyOne/Boxes/Prefabs";
    private const string TikhonModelPath = "Assets/Bubba_The_Handyman/URP/Prefabs/Handyman_ver_1.prefab";
    private const string TikhonControllerPath = "Assets/Art/Models/Characters/Handyman Dialogue.controller";

    public static void BuildAndCapture()
    {
        Build();
    }

    [MenuItem("Tools/Airport Memory/Build Warehouse")]
    public static void Build()
    {
        Directory.CreateDirectory("Assets/Level/Scenes/Locations/MindsScenes");
        Directory.CreateDirectory(MatFolder);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "Airport Cargo Warehouse";
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.12f, .14f, .16f);
        RenderSettings.ambientIntensity = 1f;
        RenderSettings.fog = false;

        Material floor = MaterialOf("Floor", new Color(.26f, .29f, .32f));
        Material steel = MaterialOf("Steel", new Color(.13f, .19f, .23f));
        Material black = MaterialOf("Rubber", new Color(.045f, .055f, .065f));
        Material orange = MaterialOf("Safety Orange", new Color(.9f, .42f, .08f));
        Material folder = MaterialOf("Document Folder", new Color(.27f, .39f, .43f));
        Material wood = MaterialOf("Crate", new Color(.5f, .32f, .17f));
        Material blue = MaterialOf("Cargo Blue", new Color(.15f, .38f, .54f));

        PlaceWarehouse();
        // The FBX has no collision mesh. Keep a hidden walkable floor inside its walls.
        var collisionFloor = Cube("Warehouse collision floor", new Vector3(0, -.18f, 0),
            new Vector3(14.5f, .36f, 27f), floor, null);
        collisionFloor.GetComponent<MeshRenderer>().enabled = false;
        var safety = new GameObject("Warehouse safety markings").transform;
        for (int z = -10; z <= 10; z += 4)
            Cube("Safety floor stripe", new Vector3(-6.5f, .015f, z),
                new Vector3(.16f, .02f, 2f), orange, safety);

        PlaceBaseLighting();

        PlaceConveyor(steel, black, orange, blue, wood);

        var desk = new GameObject("Employee desk").transform;
        Cube("Desk top", new Vector3(-4.7f, .91f, -4), new Vector3(3.5f, .15f, 1.8f), steel, desk);
        for (float x = -6.3f; x <= -3f; x += 3.3f)
            for (int z = -5; z <= -4; z++)
                Cube("Desk leg", new Vector3(x, .43f, z), new Vector3(.16f, .86f, .16f), steel, desk);
        Document("1 Family baggage", new Vector3(-5.5f, 1.01f, -4.35f),
            "Семейный багаж", "Накладная F-04. Отправка: 12.06.2004. " +
            "Отправитель: Сергей Карамазов. Получатель: семейный багаж, Нью-Йорк. " +
            "В списке нет технического оборудования.", folder, desk);
        Document("2 Technical container", new Vector3(-4.7f, .04f, 5.4f),
            "Технический контейнер", "Маркировка контейнера: К-17. Дата отправки: 14.06.2004. " +
            "Подпись отправителя: И. Карамазов. Адрес получателя: технический склад, Нью-Йорк. " +
            "Номер К-17 совпадает с маркировкой кассеты из первого воспоминания.", folder, null);
        Document("3 Personal items", new Vector3(3.5f, .59f, 11.2f),
            "Личные вещи", "Дополнительная отправка L-09. Дата: 16.06.2004. " +
            "Отправитель: Михаил Орлов. Получатель: личные вещи, Москва. " +
            "К техническому контейнеру отношения не имеет.", folder, null);

        var terminal = new GameObject("Cargo registration terminal");
        terminal.transform.position = new Vector3(5.2f, 0, 12.35f);
        Material enamel = MaterialOf("Old Terminal Enamel", new Color(.34f, .36f, .31f));
        Material dark = MaterialOf("Old Terminal Trim", new Color(.10f, .11f, .10f));
        Material amber = MaterialOf("Old Terminal Amber", new Color(.7f, .43f, .16f));
        Cube("Steel pedestal", new Vector3(0, .55f, 0), new Vector3(.66f, 1.1f, .58f), enamel, terminal.transform);
        Cube("Bolted base", new Vector3(0, .1f, 0), new Vector3(.9f, .2f, .8f), dark, terminal.transform);
        Cube("CRT casing", new Vector3(0, 1.45f, .05f), new Vector3(1.47f, .96f, .54f), enamel, terminal.transform);
        Cube("Recessed bezel", new Vector3(0, 1.49f, -.239f), new Vector3(1.24f, .69f, .035f), dark, terminal.transform);
        Cube("Amber monochrome display", new Vector3(0, 1.49f, -.261f), new Vector3(1.12f, .56f, .009f),
            GraphicMaterialOf("Cargo Terminal Display", true, 512, 320, -1), terminal.transform);
        Cube("Keyboard shelf", new Vector3(0, .94f, -.39f), new Vector3(1.28f, .08f, .58f), enamel, terminal.transform);
        for (int row = 0; row < 3; row++)
            for (int key = 0; key < 12; key++)
                Cube("Key", new Vector3(-.52f + key * .095f, 1f, -.59f + row * .115f),
                    new Vector3(.073f, .025f, .075f), dark, terminal.transform);
        Cube("Asset tag K-17", new Vector3(.49f, 1.1f, -.69f), new Vector3(.18f, .035f, .045f), amber, terminal.transform);
        terminal.AddComponent<CargoPuzzleTerminal>();

        PlaceAssetBoxes();
        for (int i = 0; i < 2; i++)
        {
            var cart = new GameObject("Baggage trolley " + (i + 1)).transform;
            cart.position = new Vector3(3.6f, 0, i == 0 ? 8f : 11.2f);
            Cube("Platform", new Vector3(0, .5f, 0), new Vector3(2, .15f, 1.1f), steel, cart);
            Cube("Handle", new Vector3(1, 1, 0), new Vector3(.08f, 1, 1.1f), steel, cart);
            for (int side = -1; side <= 1; side += 2)
                Cube("Wheel", new Vector3(0, .25f, side * .55f), new Vector3(.45f, .45f, .12f), black, cart);
        }

        var ambience = new GameObject("Airport announcements, planes and forklifts")
            .AddComponent<AirportAmbientAudio>();
        ambience.Configure(
            AssetDatabase.LoadAssetAtPath<AudioClip>(
                "Assets/Audio/Sound/AirportMemory/CargoAmbience.wav"),
            AssetDatabase.LoadAssetAtPath<AudioClip>(
                "Assets/Audio/Sound/AirportMemory/Announcement.wav"));

        PlacePlayer();
        PlaceTikhon(terminal.GetComponent<CargoPuzzleTerminal>());
        PlaceKeyLighting();
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene, ScenePath);
        var scenes = EditorBuildSettings.scenes.ToList();
        if (!scenes.Any(s => s.path == ScenePath))
        {
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
        Debug.Log("AIRPORT_MEMORY_BUILT: " + ScenePath);
        ValidateAndCapture();
    }

    [MenuItem("Tools/Airport Memory/Validate and Capture")]
    public static void ValidateAndCapture()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var player = UnityEngine.Object.FindFirstObjectByType<FirstPersonPlayer>();
        var terminal = UnityEngine.Object.FindFirstObjectByType<CargoPuzzleTerminal>();
        var warehouse = GameObject.Find("Warehouse hangar — warehouse6.fbx");
        var documents = UnityEngine.Object.FindObjectsByType<ReadableDocument>(FindObjectsSortMode.None);
        var npc = UnityEngine.Object.FindFirstObjectByType<NpcDialogue>();
        var luggage = UnityEngine.Object.FindObjectsByType<ConveyorLuggage>(FindObjectsSortMode.None);
        var boxes = GameObject.Find("Scattered asset boxes");
        if (player == null || terminal == null || warehouse == null || npc == null ||
            documents.Length != 3 || luggage.Length < 3 || boxes == null ||
            boxes.transform.childCount < 16 ||
            UnityEngine.Object.FindFirstObjectByType<AirportExteriorLoader>() != null)
            throw new Exception("Airport scene is missing a required player, interaction, model or document");
        if (!EditorBuildSettings.scenes.Any(s => s.path == ScenePath && s.enabled))
            throw new Exception("Airport scene is not enabled in Build Settings");
        var boxRenderers = boxes.GetComponentsInChildren<MeshRenderer>();
        if (boxRenderers.Length < 16 ||
            boxRenderers.Any(renderer => renderer.GetComponent<Collider>() == null))
            throw new Exception("Imported box meshes must all have solid colliders");
        if (documents.Any(doc => doc.transform.localScale.x > .5f) ||
            Vector3.Distance(documents[0].transform.position, documents[1].transform.position) < 4f ||
            Vector3.Distance(documents[0].transform.position, documents[2].transform.position) < 4f ||
            Vector3.Distance(documents[1].transform.position, documents[2].transform.position) < 4f)
            throw new Exception("The three small documents must be spread across the hangar");
        var carts = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
            .Where(transform => transform.name.StartsWith("Baggage trolley "));
        foreach (var cart in carts)
            foreach (var cartRenderer in cart.GetComponentsInChildren<MeshRenderer>())
                if (boxRenderers.Any(boxRenderer => boxRenderer.bounds.Intersects(cartRenderer.bounds)))
                    throw new Exception("An asset box intersects a baggage trolley");
        var camera = player.GetComponentInChildren<Camera>();
        if (camera == null) throw new Exception("Player camera is missing");

        var oldTarget = camera.targetTexture;
        var oldActive = RenderTexture.active;
        var target = new RenderTexture(1280, 720, 24);
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            image.Apply();
            string path = Path.Combine(Directory.GetParent(Application.dataPath).Parent.FullName,
                "AirportCargoMemory-preview.png");
            File.WriteAllBytes(path, image.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(image);
            Debug.Log("AIRPORT_MEMORY_VALIDATED: " + scene.path + "; preview=" + path);
        }
        finally
        {
            camera.targetTexture = oldTarget;
            RenderTexture.active = oldActive;
            UnityEngine.Object.DestroyImmediate(target);
        }
    }

    private static void PlaceBaseLighting()
    {
        var lighting = new GameObject("Cool warehouse lighting").transform;
        for (int z = -11; z <= 13; z += 6)
        {
            var fixture = CreateLampFixture(lighting, "Cold white ceiling lamp " + z,
                new Vector3(0, 4.25f, z), false);
            var lamp = fixture.gameObject.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.color = new Color(.86f, .91f, 1f);
            lamp.range = 9.5f;
            lamp.intensity = 1.2f;
        }
    }

    private static void PlaceKeyLighting()
    {
        var lighting = new GameObject("Warm light on key objects").transform;
        AddWarmSpot(lighting, "Family waybill", new Vector3(-5.1f, 3.7f, -4.1f),
            new Vector3(-5.5f, 1f, -4.35f));
        AddWarmSpot(lighting, "Technical waybill", new Vector3(-4.5f, 3.7f, 5.3f),
            new Vector3(-4.7f, .03f, 5.4f));
        AddWarmSpot(lighting, "Personal items waybill", new Vector3(3.8f, 3.7f, 11.2f),
            new Vector3(3.5f, .6f, 11.2f));
        AddWarmSpot(lighting, "Cargo terminal", new Vector3(4.6f, 3.8f, 12.1f),
            new Vector3(5.2f, 1.4f, 12.35f));
        AddWarmSpot(lighting, "Victor", new Vector3(4.4f, 3.8f, -1.2f),
            new Vector3(5.2f, 1.2f, -1f));
    }

    private static void AddWarmSpot(Transform parent, string name, Vector3 position,
        Vector3 target)
    {
        var fixture = CreateLampFixture(parent, "Warm ceiling lamp — " + name,
            position, true);
        var spotlight = new GameObject("Warm spotlight — " + name).AddComponent<Light>();
        spotlight.transform.SetParent(fixture, false);
        spotlight.transform.localPosition = Vector3.zero;
        spotlight.transform.rotation = Quaternion.LookRotation(target - fixture.position);
        spotlight.type = LightType.Spot;
        spotlight.color = new Color(1f, .68f, .32f);
        spotlight.intensity = 7f;
        spotlight.range = 5.5f;
        spotlight.spotAngle = 78f;
        spotlight.innerSpotAngle = 36f;
        spotlight.renderMode = LightRenderMode.ForcePixel;

        var bounce = new GameObject("Warm local fill — " + name).AddComponent<Light>();
        bounce.transform.SetParent(fixture, false);
        bounce.transform.localPosition = Vector3.zero;
        bounce.type = LightType.Point;
        bounce.color = new Color(1f, .76f, .46f);
        bounce.intensity = 3.2f;
        bounce.range = 5f;
        bounce.renderMode = LightRenderMode.ForcePixel;
    }

    private static Transform CreateLampFixture(Transform parent, string name,
        Vector3 bulbPosition, bool warm)
    {
        var fixture = new GameObject(name).transform;
        fixture.SetParent(parent);
        fixture.position = bulbPosition;
        var housing = MaterialOf("Lamp Dark Housing", new Color(.07f, .09f, .11f));
        var bulbColor = warm ? new Color(1f, .72f, .34f) :
            new Color(.82f, .91f, 1f);
        var glass = MaterialOf(warm ? "Warm Lamp Glass" : "Cold Lamp Glass", bulbColor);
        glass.shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        glass.color = bulbColor;
        if (glass.HasProperty("_BaseColor")) glass.SetColor("_BaseColor", bulbColor);
        EditorUtility.SetDirty(glass);

        LampPart(PrimitiveType.Cylinder, fixture, "Metal shade",
            new Vector3(0, .13f, 0), new Vector3(.31f, .07f, .31f), housing);
        LampPart(PrimitiveType.Sphere, fixture, "Illuminated bulb",
            Vector3.zero, new Vector3(.19f, .15f, .19f), glass);

        float suspension = Mathf.Max(.08f, 4.72f - bulbPosition.y - .2f);
        LampPart(PrimitiveType.Cylinder, fixture, "Suspension rod",
            new Vector3(0, .2f + suspension * .5f, 0),
            new Vector3(.025f, suspension * .5f, .025f), housing);
        LampPart(PrimitiveType.Cylinder, fixture, "Ceiling mount",
            new Vector3(0, .2f + suspension, 0), new Vector3(.09f, .025f, .09f), housing);
        return fixture;
    }

    private static void LampPart(PrimitiveType shape, Transform parent, string name,
        Vector3 position, Vector3 scale, Material material)
    {
        var part = GameObject.CreatePrimitive(shape);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        part.GetComponent<Renderer>().sharedMaterial = material;
        part.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
    }

    private static void PlaceWarehouse()
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(WarehousePath);
        if (source == null) throw new Exception("Warehouse FBX is missing: " + WarehousePath);
        var warehouse = (GameObject)PrefabUtility.InstantiatePrefab(source);
        warehouse.name = "Warehouse hangar — warehouse6.fbx";
        // The model includes a loaded hand trolley directly under our conveyor.
        // The scene has separate usable trolleys, so remove this overlapping copy.
        var embeddedTrolley = warehouse.transform.Find("27. KERETA");
        if (embeddedTrolley != null) embeddedTrolley.gameObject.SetActive(false);
        var unlitLamps = warehouse.transform.Find("21. lampu");
        if (unlitLamps != null) unlitLamps.gameObject.SetActive(false);
        var renderers = warehouse.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) throw new Exception("Warehouse FBX has no renderers");
        var importedCameras = warehouse.GetComponentsInChildren<Camera>(true);
        foreach (var importedCamera in importedCameras) importedCamera.enabled = false;
        foreach (var importedListener in warehouse.GetComponentsInChildren<AudioListener>(true))
            importedListener.enabled = false;
        foreach (var importedLight in warehouse.GetComponentsInChildren<Light>(true))
            importedLight.enabled = false;
        foreach (var renderer in renderers)
        {
            var materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
                materials[i] = WarehouseMaterialFor(materials[i]);
            renderer.sharedMaterials = materials;

            // The FBX meshes are static. Use their actual geometry so the player can
            // walk between shelves instead of hitting oversized bounding boxes.
            if (renderer is MeshRenderer &&
                !renderer.name.Contains("lantai") &&
                !renderer.name.Contains("cat garis") &&
                !renderer.name.Contains("lampu") &&
                !renderer.name.Contains("kabel"))
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                {
                    var collider = renderer.GetComponent<MeshCollider>();
                    if (collider == null)
                        collider = renderer.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = filter.sharedMesh;
                }
            }
        }
        Bounds bounds = renderers[0].bounds;
        foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        float scale = Mathf.Min(17f / Mathf.Max(.1f, bounds.size.x),
            30f / Mathf.Max(.1f, bounds.size.z));
        warehouse.transform.localScale = Vector3.one * scale;
        warehouse.transform.position = new Vector3(-bounds.center.x * scale,
            -bounds.min.y * scale, -bounds.center.z * scale);
        Debug.Log("WAREHOUSE_BOUNDS: " + bounds + " scale=" + scale +
            " imported cameras=" + importedCameras.Length);
    }

    private static Material WarehouseMaterialFor(Material source)
    {
        string name = source == null ? "Neutral" : source.name.ToLowerInvariant();
        string kind;
        Color color;
        if (name.Contains("lantai")) { kind = "Concrete"; color = new Color(.28f, .31f, .33f); }
        else if (name.Contains("kaca")) { kind = "Glass Blue"; color = new Color(.39f, .61f, .67f); }
        else if (name.Contains("seng") || name.Contains("tembok") || name.Contains("dinding"))
            { kind = "Panel Grey"; color = new Color(.46f, .52f, .55f); }
        else if (name.Contains("kayu")) { kind = "Wood"; color = new Color(.42f, .28f, .17f); }
        else if (name.Contains("kardus")) { kind = "Cardboard"; color = new Color(.56f, .39f, .23f); }
        else if (name.Contains("hitam")) { kind = "Dark Metal"; color = new Color(.075f, .09f, .11f); }
        else if (name.Contains("biru")) { kind = "Blue Steel"; color = new Color(.15f, .31f, .43f); }
        else if (name.Contains("merah")) { kind = "Red"; color = new Color(.56f, .12f, .11f); }
        else if (name.Contains("kuning")) { kind = "Yellow"; color = new Color(.77f, .55f, .12f); }
        else if (name.Contains("hijau")) { kind = "Green"; color = new Color(.18f, .43f, .25f); }
        else if (name.Contains("putih") || name.Contains("cahaya"))
            { kind = "Warm White"; color = new Color(.68f, .69f, .63f); }
        else if (name.Contains("plastik")) { kind = "Plastic"; color = new Color(.42f, .46f, .50f); }
        else if (name.Contains("krum") || name.Contains("besi") || name.Contains("plat"))
            { kind = "Steel"; color = new Color(.25f, .31f, .36f); }
        else { kind = "Neutral"; color = new Color(.43f, .47f, .48f); }
        return MaterialOf("Hangar " + kind, color);
    }

    private static void PlaceAssetBoxes()
    {
        var prefabs = Enumerable.Range(1, 10)
            .Select(i => AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{BoxesFolder}/SM_Boxes_{i:00}.prefab"))
            .ToArray();
        if (prefabs.Any(prefab => prefab == null))
            throw new Exception("Import Free Pack - Boxes through Package Manager before building the warehouse");

        // Keep the conveyor, the terminal, the desk and the document search spots open.
        // Fixed seed gives the same scattered arrangement on every scene rebuild.
        Vector2[] spots =
        {
            new(-5.65f, -11.1f), new(-4.3f, -9.0f), new(-5.8f, -7.2f),
            new(-5.7f, -1.6f), new(-4.1f, .6f), new(-5.8f, 2.5f),
            new(-5.8f, 7.8f), new(-4.2f, 9.3f), new(-5.7f, 11.5f),
            new(5.65f, -11.1f), new(4.0f, -8.6f), new(5.8f, -6.5f),
            new(5.7f, 1.8f), new(4.15f, 3.4f), new(5.8f, 5.3f),
            new(4.15f, 6.1f), new(5.8f, 8.1f), new(5.8f, 11.6f)
        };
        var random = new System.Random(1704);
        var parent = new GameObject("Scattered asset boxes").transform;
        for (int i = 0; i < spots.Length; i++)
        {
            var prefab = prefabs[(i * 7 + 3) % prefabs.Length];
            var box = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            box.name = $"Cargo box {i + 1:00} — {prefab.name}";
            box.transform.SetParent(parent, true);
            box.transform.rotation = Quaternion.Euler(0, random.Next(0, 24) * 15f, 0);
            var renderer = box.GetComponentInChildren<Renderer>();
            if (renderer == null) throw new Exception("Box prefab has no visible mesh: " + prefab.name);

            float desiredWidth = .67f + (float)random.NextDouble() * .33f;
            float sourceWidth = Mathf.Max(renderer.bounds.size.x, renderer.bounds.size.z);
            box.transform.localScale *= desiredWidth / Mathf.Max(.01f, sourceWidth);
            Bounds bounds = renderer.bounds;
            float jitterX = ((float)random.NextDouble() - .5f) * .26f;
            float jitterZ = ((float)random.NextDouble() - .5f) * .26f;
            box.transform.position += new Vector3(
                spots[i].x + jitterX - bounds.center.x,
                .025f - bounds.min.y,
                spots[i].y + jitterZ - bounds.center.z);

            foreach (var filter in box.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null || filter.GetComponent<Collider>() != null) continue;
                var collider = filter.gameObject.AddComponent<BoxCollider>();
                collider.center = filter.sharedMesh.bounds.center;
                collider.size = filter.sharedMesh.bounds.size;
            }
        }
    }

    private static void PlaceConveyor(Material steel, Material rubber, Material orange,
        Material blue, Material wood)
    {
        var conveyor = new GameObject("Moving baggage conveyor").transform;
        Cube("Moving belt", new Vector3(0, .82f, 0), new Vector3(2.45f, .18f, 14f), rubber, conveyor);
        Cube("Left side rail", new Vector3(-1.28f, 1.04f, 0),
            new Vector3(.12f, .35f, 14.2f), steel, conveyor);
        Cube("Right side rail", new Vector3(1.28f, 1.04f, 0),
            new Vector3(.12f, .35f, 14.2f), steel, conveyor);
        for (int z = -6; z <= 6; z += 2)
        {
            Cube("Cross roller", new Vector3(0, .92f, z),
                new Vector3(2.38f, .035f, .08f), steel, conveyor);
            for (int side = -1; side <= 1; side += 2)
                Cube("Support leg", new Vector3(side * 1.12f, .39f, z),
                    new Vector3(.12f, .78f, .12f), steel, conveyor);
        }
        Cube("Loading end", new Vector3(0, .82f, -7.1f),
            new Vector3(2.7f, .22f, .25f), orange, conveyor);
        Cube("Unloading end", new Vector3(0, .82f, 7.1f),
            new Vector3(2.7f, .22f, .25f), orange, conveyor);

        Material red = MaterialOf("Luggage Red", new Color(.64f, .19f, .17f));
        Material yellow = MaterialOf("Luggage Yellow", new Color(.89f, .64f, .20f));
        Material green = MaterialOf("Luggage Green", new Color(.18f, .47f, .38f));
        Material[] colors = { blue, red, yellow, green };
        for (int i = 0; i < 5; i++)
        {
            var luggage = new GameObject(i == 4 ? "Moving cargo parcel" : "Moving suitcase " + (i + 1));
            luggage.transform.SetParent(conveyor);
            Material body = i == 4 ? wood : colors[i];
            if (i == 4)
            {
                Cube("Parcel", new Vector3(0, .26f, 0), new Vector3(.92f, .56f, .64f), body, luggage.transform);
                Cube("Packing tape", new Vector3(0, .55f, 0),
                    new Vector3(.13f, .012f, .65f), yellow, luggage.transform);
            }
            else
            {
                Cube("Suitcase shell", new Vector3(0, .39f, 0),
                    new Vector3(.82f, .78f, .52f), body, luggage.transform);
                Cube("Front trim", new Vector3(0, .39f, -.27f),
                    new Vector3(.76f, .68f, .025f), body, luggage.transform);
                Cube("Handle grip", new Vector3(0, .94f, 0),
                    new Vector3(.32f, .09f, .11f), steel, luggage.transform);
                Cube("Handle left", new Vector3(-.13f, .86f, 0),
                    new Vector3(.055f, .18f, .055f), steel, luggage.transform);
                Cube("Handle right", new Vector3(.13f, .86f, 0),
                    new Vector3(.055f, .18f, .055f), steel, luggage.transform);
                foreach (int side in new[] { -1, 1 })
                    Cube("Wheel", new Vector3(side * .28f, -.025f, 0),
                        new Vector3(.14f, .13f, .16f), rubber, luggage.transform);
            }
            foreach (var collider in luggage.GetComponentsInChildren<Collider>())
                UnityEngine.Object.DestroyImmediate(collider);
            var motion = luggage.AddComponent<ConveyorLuggage>();
            motion.Configure(new Vector3((i % 2 == 0 ? -.34f : .34f), .94f, -6.5f),
                new Vector3((i % 2 == 0 ? -.34f : .34f), .94f, 6.5f),
                1.35f, i / 5f);
        }
    }

    private static void PlacePlayer()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
        if (prefab == null) throw new Exception("Player prefab is missing");
        var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        player.name = "Alexey — player from main";
        player.transform.position = new Vector3(1.8f, .05f, -10);
        player.transform.rotation = Quaternion.Euler(0, 0, 0);
        Disable<PistolController>(player);
        Disable<PlayerStress>(player);
        Disable<SmokingController>(player);
        Disable<SceneSwitch>(player);
        foreach (var transform in player.GetComponentsInChildren<Transform>(true))
            if (transform.name == "Pistol View Model" || transform.name == "Pistol HUD" ||
                transform.name == "Weapon Hint" || transform.name == "Stress Effects")
                transform.gameObject.SetActive(false);
        var camera = player.GetComponentInChildren<Camera>();
        if (camera != null)
        {
            camera.nearClipPlane = .03f;
            camera.farClipPlane = 100f;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.allowHDR = false;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
        }
    }

    private static void PlaceTikhon(CargoPuzzleTerminal terminal)
    {
        var npc = new GameObject("Тихон Инокентьевич — сотрудник грузового терминала");
        npc.transform.position = new Vector3(5.2f, 0, -1);
        npc.transform.rotation = Quaternion.Euler(0, 195, 0);
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(TikhonModelPath);
        if (model == null) throw new Exception("Tikhon character model is missing");
        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(TikhonControllerPath);
        if (controller == null) throw new Exception("Handyman dialogue controller is missing");
        var body = (GameObject)PrefabUtility.InstantiatePrefab(model, npc.transform);
        var animator = body.GetComponent<Animator>();
        var modelCollider = body.GetComponent<CapsuleCollider>();
        modelCollider.enabled = false;
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        PrefabUtility.RecordPrefabInstancePropertyModifications(modelCollider);
        PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
        npc.AddComponent<NpcTalkAnimation>().Configure(animator);
        var collider = npc.AddComponent<CapsuleCollider>();
        collider.center = Vector3.up * .95f;
        collider.height = 1.9f;
        collider.radius = .35f;
        var dialogue = npc.GetComponent<NpcDialogue>();
        if (dialogue == null) dialogue = npc.AddComponent<NpcDialogue>();
        var serialized = new SerializedObject(dialogue);
        serialized.FindProperty("characterName").stringValue = "Тихон Инокентьевич";
        var nodes = serialized.FindProperty("nodes");
        nodes.arraySize = 2;
        SetNode(nodes.GetArrayElementAtIndex(0),
            "Алексей, помоги найти накладную на технический контейнер. " +
            "Три документа спрятаны в ангаре. Сверь номер, дату, отправителя и адрес на терминале.",
            new[] { "Я посмотрю документы.", "Что ты помнишь о контейнере?", "Сначала осмотрюсь." },
            new[] { 1, 1, -1 });
        SetNode(nodes.GetArrayElementAtIndex(1),
            "Помню маркировку К-17 — как на той кассете. Когда найдёшь запись, " +
            "посмотри подпись и склад назначения. Потом поговорим.",
            new[] { "Спасибо, проверю.", "Повтори про кассету." }, new[] { -1, 1 });
        serialized.ApplyModifiedPropertiesWithoutUndo();
        npc.AddComponent<VictorMemoryDialogue>().Configure(terminal, dialogue);
    }

    private static void SetNode(SerializedProperty node, string text, string[] choices, int[] next)
    {
        node.FindPropertyRelative("npcText").stringValue = text;
        var list = node.FindPropertyRelative("choices");
        list.arraySize = choices.Length;
        for (int i = 0; i < choices.Length; i++)
        {
            var choice = list.GetArrayElementAtIndex(i);
            choice.FindPropertyRelative("text").stringValue = choices[i];
            choice.FindPropertyRelative("nextNode").intValue = next[i];
        }
    }

    private static void Document(string name, Vector3 position, string title, string body,
        Material folderMaterial, Transform parent)
    {
        var backing = Cube(name + " folder", position + new Vector3(0, -.015f, 0),
            new Vector3(.46f, .012f, .34f), folderMaterial, parent);
        backing.transform.rotation = Quaternion.Euler(0, 13f, 0);
        int variant = name[0] - '1';
        var object3d = Cube(name, position, new Vector3(.42f, .018f, .3f),
            GraphicMaterialOf("Waybill " + (variant + 1), false, 512, 512, variant), parent);
        object3d.transform.rotation = Quaternion.Euler(0, 13f, 0);
        var doc = object3d.AddComponent<ReadableDocument>();
        var serialized = new SerializedObject(doc);
        serialized.FindProperty("documentTitle").stringValue = title;
        serialized.FindProperty("documentText").stringValue = body;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject Cube(string name, Vector3 position, Vector3 scale,
        Material material, Transform parent)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = material;
        return go;
    }

    private static GameObject Sphere(string name, Vector3 position, Vector3 scale,
        Material material, Transform parent)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = material;
        return go;
    }

    private static void Label(string text, Vector3 position, int size, Color color, Transform parent)
    {
        var label = new GameObject("Label: " + text).AddComponent<TextMesh>();
        label.transform.SetParent(parent, false);
        label.transform.localPosition = position;
        label.transform.localRotation = Quaternion.identity;
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.characterSize = Mathf.Min(.035f, .65f / Mathf.Max(1, text.Length));
        label.fontSize = size;
        label.color = color;
        label.text = text;
        label.gameObject.AddComponent<FacePlayerCamera>();
    }

    private static Material GraphicMaterialOf(string name, bool screen, int width, int height, int variant)
    {
        string texturePath = MatFolder + "/" + name + " Texture.png";
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            { name = name + " Texture" };

        Color background = screen ? new Color(.025f, .075f, .09f) : new Color(.91f, .87f, .72f);
        var pixels = Enumerable.Repeat(background, width * height).ToArray();
        if (screen)
        {
            Color glow = new Color(.72f, .52f, .23f);
            Color dim = new Color(.28f, .22f, .13f);
            PaintRect(pixels, width, height, 22, 22, width - 44, height - 44, dim);
            PaintRect(pixels, width, height, 28, 28, width - 56, height - 56, background);
            PaintRect(pixels, width, height, 48, 230, 265, 7, glow);
            PaintRect(pixels, width, height, 48, 195, 392, 3, dim);
            PaintRect(pixels, width, height, 48, 155, 350, 5, glow);
            PaintRect(pixels, width, height, 48, 123, 240, 4, glow);
            PaintRect(pixels, width, height, 48, 75, 390, 3, dim);
            PaintRect(pixels, width, height, 48, 53, 135, 5, glow);
        }
        else
        {
            Color ink = new Color(.20f, .29f, .33f);
            Color rule = new Color(.53f, .59f, .56f);
            Color stamp = variant == 1 ? new Color(.52f, .16f, .13f) :
                new Color(.24f, .36f, .48f);
            PaintRect(pixels, width, height, 21, 20, width - 42, height - 40,
                new Color(.97f, .95f, .84f));
            PaintRect(pixels, width, height, 35, height - 93, width - 70, 44, ink);
            PaintRect(pixels, width, height, 35, height - 109, width - 70, 3, rule);
            for (int row = 0; row < 7; row++)
            {
                int y = height - 151 - row * 42;
                PaintRect(pixels, width, height, 45, y, 94, 5, ink);
                PaintRect(pixels, width, height, 158, y, 305 - row % 3 * 33, 3, rule);
                PaintRect(pixels, width, height, 158, y - 14, 255 + row % 2 * 35, 2, rule);
            }
            PaintRect(pixels, width, height, 37, 35, 220, 3, ink);
            PaintRect(pixels, width, height, 40, 48, 5, 38, ink);
            PaintRect(pixels, width, height, 328, 33, 145, 58, rule);
            PaintRect(pixels, width, height, 335, 40, 131, 44, new Color(.97f, .95f, .84f));
            int centerX = 398, centerY = 133;
            for (int y = centerY - 48; y <= centerY + 48; y++)
                for (int x = centerX - 48; x <= centerX + 48; x++)
                {
                    int squared = (x - centerX) * (x - centerX) + (y - centerY) * (y - centerY);
                    if (squared >= 39 * 39 && squared <= 44 * 44)
                        pixels[y * width + x] = stamp;
                }
            PaintRect(pixels, width, height, centerX - 28, centerY - 2, 56, 4, stamp);
            PaintRect(pixels, width, height, centerX - 17, centerY - 13, 34, 3, stamp);
        }
        texture.SetPixels(pixels);
        texture.Apply();
        if (!screen || !File.Exists(texturePath))
            File.WriteAllBytes(texturePath, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);
        var importedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (importedTexture == null) throw new Exception("Could not import graphic: " + texturePath);

        string materialPath = MatFolder + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        var shader = Shader.Find(screen ? "Universal Render Pipeline/Unlit" :
            "Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (material == null)
        {
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, materialPath);
        }
        material.shader = shader;
        material.color = Color.white;
        material.mainTexture = importedTexture;
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", importedTexture);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void PaintRect(Color[] pixels, int width, int height,
        int left, int bottom, int rectWidth, int rectHeight, Color color)
    {
        for (int y = Mathf.Max(0, bottom); y < Mathf.Min(height, bottom + rectHeight); y++)
            for (int x = Mathf.Max(0, left); x < Mathf.Min(width, left + rectWidth); x++)
                pixels[y * width + x] = color;
    }

    private static Material MaterialOf(string name, Color color)
    {
        string path = MatFolder + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (material == null)
        {
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        material.color = color;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void Disable<T>(GameObject go) where T : Behaviour
    {
        var behaviour = go.GetComponent<T>();
        if (behaviour != null) behaviour.enabled = false;
    }
}

public sealed class TikhonAnimationImporter : AssetPostprocessor
{
    private void OnPreprocessModel()
    {
        if (assetPath != "Assets/Art/Models/AirportMemory/Tikhon/Standing Arguing.fbx") return;
        var importer = (ModelImporter)assetImporter;
        importer.animationType = ModelImporterAnimationType.Human;
        importer.importAnimation = true;
        var clips = importer.defaultClipAnimations;
        foreach (var clip in clips) clip.loopTime = true;
        importer.clipAnimations = clips;
    }
}
