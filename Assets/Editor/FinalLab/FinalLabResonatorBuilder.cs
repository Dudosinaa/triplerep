using System;
using System.Linq;
using FinalLab.Investigation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

internal sealed partial class FinalLabResonatorBuilder
{
    internal const string ModuleName = "K17_Resonator_Root";
    internal const string CrateName = "K17_TransportCrate_1992";
    private const string ParentPath = FinalLabResonatorSurvey.Models;
    private const string Generated = ParentPath + "/FinalLab Assembly";
    private readonly FinalLabEvidenceAssets print = new(true);
    private readonly Material steel, olive, copper, ceramic, black, amber, red, green, paper;
    private static readonly Color Ink = new(0.78f, 0.75f, 0.61f);
    private static readonly Color BlueBlack = new(0.14f, 0.15f, 0.13f);

    public static void Apply()
    {
        FinalLabInvestigationSurvey.RequireBatch();
        var scene = EditorSceneManager.OpenScene(FinalLabBuilder.ScenePath);
        var roots = scene.GetRootGameObjects();
        var investigation = roots.Single(root => root.name == "FinalLab_Investigation");
        var props = roots.Single(root => root.name == "FinalLab_Props").transform;
        var manager = investigation.GetComponent<FinalLabInvestigationManager>();
        if (!manager.Points.Select(point => point.Id).SequenceEqual(new[] { "Archive", "Father", "Ivan" }))
            throw new InvalidOperationException("Unexpected initial investigation points.");
        foreach (string name in new[] { "04 K-17 investigation", "K-17 wall-mounted cable paths" })
        {
            var old = investigation.transform.Find(name);
            if (old != null) Object.DestroyImmediate(old.gameObject);
        }
        foreach (var old in roots.Where(root => root.name == ModuleName || root.name == CrateName)) Object.DestroyImmediate(old);
        if (!AssetDatabase.IsValidFolder(Generated)) AssetDatabase.CreateFolder(ParentPath, "FinalLab Assembly");
        var build = new FinalLabResonatorBuilder();
        var module = build.Module();
        build.RoutePower(module, props);
        build.TransportCrate();
        Physics.SyncTransforms();
        FinalLabResonatorValidation.Verify(scene.GetRootGameObjects());
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("FinalScene save failed.");
        Debug.Log("FINAL_LAB_RESONATOR_SAVED");
    }

    private FinalLabResonatorBuilder()
    {
        var patina = AssetDatabase.LoadAssetAtPath<Texture2D>(ParentPath + "/TirgamesAssets/Factory/Textures/CommonMetal01.png");
        steel = Material("Dark_Patinated_Steel", new Color(.35f, .36f, .35f), .57f, .38f, patina);
        olive = Material("Faded_Olive_Enamel", new Color(.51f, .55f, .36f), .27f, .31f, patina);
        copper = Material("Dull_Winding_Copper", new Color(.48f, .26f, .15f), .65f, .40f);
        ceramic = Material("Aged_Ceramic", new Color(.65f, .57f, .43f), .04f, .34f);
        black = Material("Bakelite", new Color(.095f, .085f, .073f), .13f, .35f);
        amber = Material("Muted_Amber", new Color(.52f, .23f, .055f), .09f, .38f, emission: .29f);
        red = Material("Dark_Red", new Color(.57f, .10f, .061f), .14f, .36f, emission: .16f);
        green = Material("Muted_Green", new Color(.24f, .44f, .25f), .07f, .40f, emission: .16f);
        paper = Material("Old_Cargo_Label", new Color(.73f, .64f, .47f), 0, .18f);
    }

    private Transform Module()
    {
        var root = Group(null, ModuleName, new Vector3(4.53f, 0, 10.94f));
        root.localRotation = Quaternion.Euler(0, -90, 0);
        var stand = Group(root, "K17_BaseFrame", Vector3.zero);
        Block(stand, "Welded floor skid", new(0, .05f, -.015f), new(1.47f, .10f, .66f), steel, true);
        Block(stand, "Equipment deck", new(0, .137f, -.01f), new(1.38f, .075f, .60f), olive);
        foreach (float x in new[] { -.64f, .64f })
        {
            Block(stand, "Bolted runner", new(x, .024f, -.015f), new(.095f, .048f, .70f), steel);
            foreach (float z in new[] { -.27f, .28f }) Bolt(stand, new Vector3(x, .106f, z), .022f, Quaternion.identity);
        }
        var body = Group(root, "K17_MainBody", Vector3.zero);
        Block(body, "Olive rear equipment housing", new(0, .63f, -.06f), new(1.35f, 1.03f, .49f), olive, true);
        Block(body, "Top folded seam", new(0, 1.149f, -.105f), new(1.37f, .041f, .41f), steel);
        for (int i = 0; i < 6; i++)
        {
            float y = .315f + i * .048f;
            Block(body, "Staggered cooling louvre", new(.680f, y, -.055f), new(.016f, .020f, .24f), black);
            Block(body, "Louvre lip", new(.688f, y + .009f, -.055f), new(.016f, .013f, .246f), steel);
        }
        var transformer = Group(root, "K17_Transformer", Vector3.zero);
        var chassis = Fit(transformer, "Transformer from the source model", "old_soviet_transformer_low-poly.glb", new(-.355f, .17f, .163f),
            new(.566f, .83f, .255f), Quaternion.identity);
        foreach (var part in chassis.GetComponentsInChildren<MeshRenderer>())
            if (part.sharedMaterial.name is "Doors" or "Main_body")
            {
                var patina = ImportedFinish(part.sharedMaterial, "Transformer_" + part.sharedMaterial.name);
                patina.SetColor("baseColorFactor", new Color(.59f, .65f, .49f));
                part.sharedMaterial = patina;
            }
        Plate(root, "K17_Nameplate", new(-.35f, .875f, .303f), new(.47f, .205f), steel, Ink,
            ("К-17", 78), ("ПРОЕКТ «НЕЧИСТЬ»", 37), ("РЕЗОНАНСНЫЙ МОДУЛЬ", 32), ("ОПЫТНЫЙ ОБРАЗЕЦ № 01", 29), ("1982", 29));
        Plate(root, "K17_HighVoltage", new(-.35f, .590f, .302f), new(.368f, .081f), black, new Color(.84f, .65f, .34f),
            ("ОПАСНО — ВЫСОКОЕ", 39), ("НАПРЯЖЕНИЕ", 41));
        Coil(root);
        Controls(root);
        TuneResonatorLights(root);
        return root;
    }

    private void Coil(Transform root)
    {
        var bay = Group(root, "K17_CoilAssembly", new Vector3(-.35f, 0, 0));
        Block(bay, "Portable module base", new(0, 1.037f, .031f), new(.59f, .069f, .45f), black, true);
        foreach (float x in new[] { -.28f, .28f })
        {
            foreach (float z in new[] { -.17f, .24f })
            {
                Block(bay, "Coil cage upright", new(x, 1.25f, z), new(.032f, .43f, .033f), steel);
                Bolt(bay, new Vector3(x, 1.48f, z), .014f, Quaternion.identity);
            }
            Block(bay, "Coil cage side rail", new(x, 1.466f, .035f), new(.044f, .037f, .451f), steel);
        }
        Block(bay, "Open cage rear crossbar", new(0, 1.465f, -.171f), new(.562f, .035f, .035f), steel);
        var close = Fit(bay, "Imported induction coil", "page_induction_coil_from_us_patent_76654.glb", new(0, 1.069f, .037f),
            new(.47f, .36f, .362f), Quaternion.Euler(0, 90, 0));
        var far = Group(bay, "Simplified coil at distance", Vector3.zero);
        var horizontal = Quaternion.Euler(0, 0, 90);
        Cylinder(far, "Wound copper bobbin", new(0, 1.219f, .037f), .14f, .23f, copper, horizontal);
        foreach (float x in new[] { -.217f, .217f })
        {
            Cylinder(far, "Collector flange", new(x, 1.219f, .037f), .157f, .018f, steel, horizontal);
            Cylinder(far, "Ceramic end disc", new(x, 1.219f, .037f), .12f, .031f, ceramic, horizontal);
        }
        ApplyCoilFinish(close, far, bay.gameObject.AddComponent<LODGroup>());
        var core = Group(bay, "K17_Core", new(0, 1.205f, .228f));
        Cylinder(core, "Ceramic socket", new(0, 0, -.002f), .073f, .028f, ceramic, Quaternion.Euler(90, 0, 0));
        Cylinder(core, "Removable steel core cap", new(0, 0, .020f), .052f, .032f, steel, Quaternion.Euler(90, 0, 0));
        Block(core, "Core extraction key", new(0, 0, .042f), new(.065f, .020f, .023f), copper);
    }
}
