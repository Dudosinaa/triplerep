using System;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

internal sealed partial class FinalLabResonatorBuilder
{
    public static void RepairAppearance()
    {
        FinalLabInvestigationSurvey.RequireBatch();
        var scene = EditorSceneManager.OpenScene(FinalLabBuilder.ScenePath);
        var roots = scene.GetRootGameObjects();
        var module = roots.Single(root => root.name == ModuleName).transform;
        var crate = roots.Single(root => root.name == CrateName).transform;
        var coil = module.Find("K17_CoilAssembly");
        var lod = coil.GetComponent<LODGroup>();
        var detail = coil.Find("Imported induction coil");
        var proxy = coil.Find("Simplified coil at distance");
        Debug.Log($"K17 coil originally has {lod.lodCount} LOD levels: " + string.Join(", ", lod.GetLODs()
            .Select(level => $"{level.screenRelativeTransitionHeight:F3} ({level.renderers.Length} renderers)")));
        ApplyCoilFinish(detail, proxy, lod);
        TuneResonatorLights(module);
        TuneCargoLight(crate);
        var year = module.Find("K17_Nameplate/Print/Inscription 5").GetComponent<TMP_Text>();
        if (year.text is not ("1989" or "1982")) throw new InvalidOperationException("Unexpected K-17 nameplate year.");
        year.text = "1982";

        var rendered = lod.GetLODs().Single().renderers;
        if (proxy.gameObject.activeSelf || rendered.Length == 0 || rendered.Distinct().Count() != rendered.Length ||
            rendered.Any(r => r == null || !r.TryGetComponent<MeshFilter>(out var filter) || filter.sharedMesh == null ||
                r.sharedMaterial.shader.name != "Universal Render Pipeline/Lit" || r.sharedMaterial.GetTexture("_BaseMap") == null) ||
            year.font == null || new[] { module, crate }.SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Any(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) != 0))
            throw new InvalidOperationException("K-17 appearance contains a missing asset reference or a duplicate renderer.");
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save FinalScene.");
        Debug.Log("FINAL_LAB_K17_APPEARANCE_SAVED: one active coil render level, valid URP references and the 1982 nameplate.");
    }

    private static void ApplyCoilFinish(Transform detail, Transform proxy, LODGroup lod)
    {
        var renderers = detail.GetComponentsInChildren<MeshRenderer>().Where(r => r.enabled).ToArray();
        var finish = Material("Induction_Coil_Matte_Copper", new Color(.92f, .87f, .81f), .27f, .23f, CoilTexture());
        finish.SetFloat("_SpecularHighlights", 0);
        finish.SetFloat("_EnvironmentReflections", 0);
        finish.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        finish.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        foreach (var renderer in renderers) renderer.sharedMaterial = finish;
        proxy.gameObject.SetActive(false);
        lod.SetLODs(new[] { new LOD(0, renderers) });
        lod.RecalculateBounds();
    }

    private static void TuneResonatorLights(Transform module)
    {
        var lighting = module.Find("K17_Lighting") ?? Group(module, "K17_Lighting", Vector3.zero);
        Spot(lighting, "Resonator warm worklight", new(.12f, 2.46f, 1.41f), new(0, 1.13f, .22f), new Color(1f, .86f, .69f), 4.70f, 3.60f, 82);
        Spot(lighting, "Copper glint", new(-.37f, 2.10f, .40f), new(-.34f, 1.27f, .02f), new Color(.89f, .94f, 1f), .47f, 1.30f, 67);
    }

    private static void TuneCargoLight(Transform crate) => Spot(crate, "Soft cargo label spill", new(-.96f, 1.74f, -.43f),
        new(-.40f, .44f, .02f), new Color(1f, .88f, .72f), 3.10f, 2.50f, 55);

    private static Material ImportedFinish(Material source, string name)
    {
        string path = Generated + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(source) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Texture2D CoilTexture()
    {
        const string path = Generated + "/Coil_Patinated_Albedo.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null) return existing;

        using var input = new BinaryReader(File.OpenRead(ParentPath + "/page_induction_coil_from_us_patent_76654.glb"));
        if (input.ReadUInt32() != 0x46546C67 || input.ReadUInt32() != 2)
            throw new InvalidOperationException("Invalid induction coil GLB header.");
        input.ReadUInt32();
        int jsonLength = input.ReadInt32();
        if (input.ReadUInt32() != 0x4E4F534A) throw new InvalidOperationException("Missing GLB JSON chunk.");
        var metadata = JsonUtility.FromJson<CoilImageMetadata>(Encoding.UTF8.GetString(input.ReadBytes(jsonLength)));
        input.ReadUInt32();
        if (input.ReadUInt32() != 0x004E4942) throw new InvalidOperationException("Missing GLB binary chunk.");
        var image = metadata.bufferViews[metadata.images[0].bufferView];
        input.BaseStream.Seek(image.byteOffset, SeekOrigin.Current);
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true) { name = "Induction coil aged copper albedo" };
        if (!ImageConversion.LoadImage(texture, input.ReadBytes(image.byteLength))) throw new InvalidOperationException("Cannot read coil albedo.");
        var pixels = texture.GetPixels32();
        for (int i = 0; i < pixels.Length; i++)
        {
            Color original = pixels[i];
            Color.RGBToHSV(original, out float hue, out float saturation, out float value);
            if (hue is > .32f and < .70f && saturation > .22f)
            {
                float oxide = .16f + value * .58f;
                pixels[i] = Color.Lerp(original, new Color(oxide, oxide * .55f, oxide * .32f), Mathf.SmoothStep(.73f, .99f, saturation));
            }
            else if ((hue < .068f || hue > .93f) && saturation > .45f)
            {
                float rail = .22f + value * .25f;
                pixels[i] = Color.Lerp(original, new Color(rail * .82f, rail * .88f, rail * .68f), .94f);
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        EditorUtility.CompressTexture(texture, TextureFormat.DXT1, TextureCompressionQuality.Best);
        AssetDatabase.CreateAsset(texture, path);
        return texture;
    }

    [Serializable]
    private sealed class CoilImageMetadata
    {
        public CoilImage[] images;
        public CoilBufferView[] bufferViews;
    }

    [Serializable]
    private sealed class CoilImage { public int bufferView; }

    [Serializable]
    private sealed class CoilBufferView { public int byteOffset, byteLength; }
}
