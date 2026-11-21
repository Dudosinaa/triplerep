using System;
using System.Linq;
using FinalLab.Investigation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed partial class FinalLabInvestigationBuilder
{
    internal const string RootName = "FinalLab_Investigation";
    internal const string PropPath = "Assets/Art/Models/Environment/FinalLab/";
    internal static readonly Vector3[] Approaches = {
        new(0.02f, 0, 3.37f), new(-3.00f, 0, 9.60f), new(-2.80f, 0, 11.61f), new(-2.85f, 0, 6.25f)
    };
    private static readonly Quaternion FloorPage = Quaternion.LookRotation(Vector3.down, Vector3.left);
    private static readonly Quaternion WestWallPage = Quaternion.LookRotation(Vector3.left, Vector3.up);
    private readonly FinalLabEvidenceAssets assets;
    private readonly Material steel, wood, copper, ceramic, glass, rust;
    private readonly Transform root, props;
    private static readonly Color Ink = new(0.25f, 0.206f, 0.14f);

    private FinalLabInvestigationBuilder(Transform generatedRoot, Transform existingProps)
    {
        root = generatedRoot;
        props = existingProps;
        assets = new FinalLabEvidenceAssets();
        steel = Lit("Oxidized_Steel", new Color(0.26f, 0.285f, 0.27f), 0.55f, 0.31f);
        copper = Lit("Coil_Copper", new Color(0.58f, 0.27f, 0.143f), 0.64f, 0.41f);
        ceramic = Lit("Phenolic_Terminal", new Color(0.18f, 0.105f, 0.072f), 0.03f, 0.26f);
        glass = Lit("Old_Tube_Glass", new Color(0.32f, 0.388f, 0.405f), 0.19f, 0.76f);
        rust = Lit("Dark_Rust", new Color(0.28f, 0.163f, 0.102f), 0.27f, 0.27f);
        wood = Lit("Workshop_Crate_Timber", new Color(0.63f, 0.51f, 0.35f), 0, 0.19f);
        var ladder = Load<Material>(PropPath + "Materials/FinalLab_WoodLadder.mat");
        wood.SetTexture("_BaseMap", ladder.GetTexture("_BaseMap"));
        wood.SetTexture("_BumpMap", ladder.GetTexture("_BumpMap"));
        if (wood.GetTexture("_BumpMap") != null) wood.EnableKeyword("_NORMALMAP");
        EditorUtility.SetDirty(wood);
    }

    public static void Build()
    {
        FinalLabInvestigationSurvey.RequireBatch();
        var scene = EditorSceneManager.OpenScene(FinalLabBuilder.ScenePath, OpenSceneMode.Single);
        var roots = scene.GetRootGameObjects();
        var generated = roots.SingleOrDefault(x => x.name == RootName);
        var existing = roots.Single(x => x.name == "FinalLab_Props").transform;
        var player = roots.SelectMany(x => x.GetComponentsInChildren<FirstPersonPlayer>()).Single();
        if (generated != null) Object.DestroyImmediate(generated);
        var build = new FinalLabInvestigationBuilder(new GameObject(RootName).transform, existing);
        var points = new[] { build.Archive(), build.Father(), build.Ivan(), build.Container() };
        var display = Child(build.root, "Investigation HUD").gameObject.AddComponent<FinalLabInvestigationUI>();
        display.Configure(build.assets.Font);
        build.root.gameObject.AddComponent<FinalLabInvestigationManager>()
            .Configure(player, player.GetComponentsInChildren<Camera>().Single(), display, points);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("The Final Lab scene could not be saved.");
        Debug.Log("FINAL_LAB_INVESTIGATION_BUILD " + FinalLabBuilder.ScenePath);
    }

    private InvestigationPoint Archive()
    {
        var point = Child(root, "01 Archive wall", new Vector3(-1.329f, 1.54f, 3.38f), WestWallPage);
        var mounted = EvidencePresentation.Mounted;
        var papers = new[]
        {
            Page(point, "А1", new(-0.64f, 0.19f, 0), new(0.253f, 0.336f), mounted, Quaternion.Euler(0, 0, 2)),
            Page(point, "А2", new(-0.31f, 0.16f, 0), new(0.234f, 0.313f), mounted, Quaternion.Euler(0, 0, -5)),
            Page(point, "А3", new( 0.35f, 0.19f, 0), new(0.249f, 0.331f), mounted, Quaternion.Euler(0, 0, 4)),
            Page(point, "А4", new( 0.69f, 0.18f, 0), new(0.257f, 0.342f), mounted, Quaternion.Euler(0, 0, -3))
        };
        foreach (var paper in papers) Tape(paper.transform, paper.Size, true);
        var photographs = new[]
        {
            Photo(point, 0, 0, new(-0.46f, -0.208f, 0), 0.296f, mounted, Quaternion.Euler(0, 0, -3)),
            Photo(point, 1, 0, new( 0.45f, -0.209f, 0), 0.302f, mounted, Quaternion.Euler(0, 0, 4))
        };
        foreach (var photo in photographs) Tape(photo.transform, photo.Size, false);
        var symbol = Child(point, "Символ из трёх незамкнутых колец", new Vector3(0.02f, -0.015f, 0), Quaternion.Euler(0, 0, 2));
        Vector2 symbolSize = new(0.277f, 0.278f);
        assets.Plane(symbol, "Drawing", Vector3.zero, symbolSize, assets.SymbolPaper);
        Tape(symbol, symbolSize, true);
        var mark = assets.AttachEvidence(symbol.gameObject,
            new EvidenceRecord { id = "Archive_Rings", title = "Три незамкнутых кольца", point = 0 },
            symbolSize, EvidenceKind.Symbol, mounted);
        var bracket = assets.CornerOutline(point, "Objective contour", new Vector2(1.78f, 0.87f), 0.006f, false, assets.Goal);
        return Point(point, "Archive", "А1", "Осмотрите архивную стену", "Осмотреть архив",
            "В коридоре слева висят старые архивные документы.",
            "Сто лет назад... Значит, отец был не первым, кто пытался с ней связаться.",
            new Vector3(0.58f, 1.55f, 3.38f), point.position, bracket, papers.Concat(photographs).Append(mark).ToArray(),
            Array.Empty<Collider>(), true);
    }

    private InvestigationPoint Father()
    {
        var point = Child(root, "02 Father's research desk");
        var table = FitModel(point, "Father's secondary desk", PropPath + "tables + etc/old_table (1).glb",
            new Vector3(-4.48f, 0, 9.60f), 0.78f, -90);
        var top = Block(point, "Father desk collision", table.center.WithY(table.max.y - 0.024f),
            new Vector3(table.size.x * 0.94f, 0.048f, table.size.z * 0.97f), null, true);
        Block(point, "Father desk leg collision", table.center.WithY(0.36f),
            new Vector3(table.size.x * 0.79f, 0.72f, table.size.z * 0.91f), null, true);
        float y = table.max.y + 0.004f;
        var lifted = EvidencePresentation.Lifted;
        var papers = new[]
        {
            Page(point, "О1", new(-4.325f, y, 9.30f), new(0.218f, 0.277f), lifted, FloorPage * Quaternion.Euler(0, 0, -5)),
            Page(point, "О2", new(-4.337f, y, 9.58f), new(0.223f, 0.281f), lifted, FloorPage),
            Page(point, "О3", new(-4.33f, y, 9.86f), new(0.218f, 0.28f), lifted, FloorPage * Quaternion.Euler(0, 0, 4)),
            Photo(point, 2, 1, new(-4.665f, y, 9.87f), 0.227f, lifted, FloorPage * Quaternion.Euler(0, 0, 7))
        };
        var dossier = Child(point, "Closed workshop folder", new Vector3(-4.66f, y, 9.36f), FloorPage);
        assets.Plane(dossier, "Front", Vector3.zero, new Vector2(0.247f, 0.242f),
            assets.Unlit("Worn_Brown_Folder", new Color(0.43f, 0.326f, 0.20f)));
        assets.Plane(dossier, "Blank label", new Vector3(-0.035f, 0.055f, -0.001f), new Vector2(0.12f, 0.044f), assets.Paper);
        var pencil = Block(point, "Pencil", new Vector3(-4.559f, y + 0.003f, 9.59f), new Vector3(0.008f, 0.008f, 0.12f), wood);
        pencil.rotation = Quaternion.Euler(0, 31, 0);
        return DeskPoint(point, "Father", "О2", "Изучите записи отца", "Поищите старые записи отца в левой части лаборатории.",
            "Он знал, что эта штука опасна... И всё равно не смог остановить Ивана.",
            new Vector3(-3.75f, 1.49f, 9.61f), new Vector3(-4.47f, y, 9.60f),
            new Vector2(1.03f, 0.68f), papers, new Collider[] { top.GetComponent<Collider>() });
    }

    private InvestigationPoint Ivan()
    {
        var point = Child(root, "03 Ivan's workbench evidence");
        var desk = props.Find("02_IvanWorkspace/Desk");
        var colliders = desk.GetComponentsInChildren<Collider>().Where(c => c.enabled).ToArray();
        float y = FinalLabPropsSurvey.Bounds(desk.GetComponentsInChildren<MeshRenderer>().Where(r => r.enabled)).max.y + 0.004f;
        var lifted = EvidencePresentation.Lifted;
        var papers = new[]
        {
            Page(point, "И1", new(-4.38f, y, 10.937f), new(0.168f, 0.235f), lifted, FloorPage * Quaternion.Euler(0, 0, -2)),
            Page(point, "И2", new(-4.38f, y, 11.126f), new(0.175f, 0.235f), lifted, FloorPage * Quaternion.Euler(0, 0, 2)),
            Page(point, "И3", new(-4.38f, y, 11.315f), new(0.171f, 0.235f), lifted, FloorPage),
            Photo(point, 3, 2, new(-4.658f, y, 11.112f), 0.218f, lifted, FloorPage * Quaternion.Euler(0, 0, -7))
        };
        return DeskPoint(point, "Ivan", "И3", "Осмотрите стол Ивана", "Рабочий стол Ивана находится в основном зале.",
            "Деньги, слава... Он знал, чем всё закончилось, и всё равно продолжал. И этот голос... Неужели он действительно поверил?",
            new Vector3(-3.63f, 1.61f, 11.20f), new Vector3(-4.53f, y, 11.14f),
            new Vector2(0.67f, 0.53f), papers, colliders);
    }

    private InvestigationPoint DeskPoint(Transform point, string id, string key, string goal, string hint, string quote,
        Vector3 overview, Vector3 focus, Vector2 size, InvestigationDocument[] items, Collider[] surfaces)
    {
        var plane = Child(point, "Desktop contour anchor", focus + Vector3.up * 0.007f, FloorPage);
        var bracket = assets.CornerOutline(plane, "Objective contour", size, 0.005f, false, assets.Goal);
        return Point(point, id, key, goal, "Осмотреть записи", hint, quote, overview, focus + Vector3.up * 0.035f,
            bracket, items, surfaces);
    }

    private InvestigationPoint Point(Transform node, string id, string key, string goal, string action, string hint,
        string quote, Vector3 position, Vector3 focus, GameObject outline, InvestigationDocument[] items,
        Collider[] surfaces, bool wall = false, bool container = false)
    {
        var camera = Child(node, "Overview camera marker");
        camera.SetPositionAndRotation(position, Quaternion.LookRotation(focus - position, Vector3.up));
        var target = Child(node, "Approach target");
        target.position = focus;
        var point = node.gameObject.AddComponent<InvestigationPoint>();
        point.Configure(id, key, goal, "E — " + action, hint, quote, wall, container, wall ? 2.65f : 2.23f,
            camera, target, outline, surfaces, items);
        return point;
    }

    private InvestigationDocument Page(Transform parent, string id, Vector3 position, Vector2 size,
        EvidencePresentation presentation, Quaternion rotation)
    {
        var record = assets.Catalog.documents.Single(d => d.id == id);
        return Instance(assets.DocumentPrefab(record, size, presentation), parent, position, rotation);
    }

    private InvestigationDocument Photo(Transform parent, int index, int point, Vector3 position, float width,
        EvidencePresentation presentation, Quaternion rotation) =>
        Instance(assets.PhotoPrefab(index, point, width, presentation), parent, position, rotation);

    private static InvestigationDocument Instance(InvestigationDocument prefab, Transform parent, Vector3 position, Quaternion rotation)
    {
        var obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject, parent);
        obj.transform.SetLocalPositionAndRotation(position, rotation);
        return obj.GetComponent<InvestigationDocument>();
    }

    private void Tape(Transform page, Vector2 size, bool pair)
    {
        var tape = assets.Plane(page, "Faded tape", new Vector3(pair ? -size.x * 0.36f : 0, size.y * 0.512f, -0.0034f),
            new Vector2(size.x * (pair ? 0.19f : 0.24f), 0.022f), assets.BlueTape);
        tape.localRotation = Quaternion.Euler(0, 0, 8);
        if (pair)
            assets.Plane(page, "Faded tape 2", new Vector3(size.x * 0.36f, size.y * 0.512f, -0.0034f),
                new Vector2(size.x * 0.19f, 0.022f), assets.BlueTape).localRotation = Quaternion.Euler(0, 0, -11);
    }

    private static Bounds FitModel(Transform parent, string name, string path, Vector3 floor, float height, float yaw)
    {
        var holder = Child(parent, name, floor, Quaternion.Euler(0, yaw, 0));
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(path), holder);
        foreach (var item in instance.GetComponentsInChildren<Collider>()) item.enabled = false;
        var renderers = instance.GetComponentsInChildren<MeshRenderer>().Where(r => r.enabled).ToArray();
        var source = FinalLabPropsSurvey.Bounds(renderers);
        holder.localScale = Vector3.one * (height / source.size.y);
        var scaled = FinalLabPropsSurvey.Bounds(renderers);
        holder.position += floor - new Vector3(scaled.center.x, scaled.min.y, scaled.center.z);
        return FinalLabPropsSurvey.Bounds(renderers);
    }

    private static Transform Child(Transform parent, string name, Vector3 position = default, Quaternion rotation = default)
    {
        var node = new GameObject(name).transform;
        node.SetParent(parent, false);
        node.SetLocalPositionAndRotation(position, rotation == default ? Quaternion.identity : rotation);
        return node;
    }

    private static Transform Block(Transform parent, string name, Vector3 pos, Vector3 size, Material material, bool solid = false)
    {
        if (material != null) return Primitive(parent, name, PrimitiveType.Cube, pos, size, material, solid);
        var node = Child(parent, name, pos);
        node.gameObject.AddComponent<BoxCollider>().size = size;
        return node;
    }

    private static Transform Primitive(Transform parent, string name, PrimitiveType type, Vector3 pos, Vector3 size,
        Material material, bool solid = false)
    {
        var node = GameObject.CreatePrimitive(type).transform;
        node.name = name;
        node.SetParent(parent, false);
        node.localPosition = pos;
        node.localScale = size;
        node.GetComponent<MeshRenderer>().sharedMaterial = material;
        if (!solid) Object.DestroyImmediate(node.GetComponent<Collider>());
        return node;
    }

    private static T Load<T>(string path) where T : Object =>
        AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("Missing project asset " + path);

    private static Material Lit(string name, Color color, float metallic, float smoothness)
    {
        string path = FinalLabEvidenceAssets.Root + "/Materials/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", smoothness);
        EditorUtility.SetDirty(material);
        return material;
    }
}

internal static class FinalLabInvestigationVector
{
    internal static Vector3 WithY(this Vector3 p, float y) => new(p.x, y, p.z);
}
