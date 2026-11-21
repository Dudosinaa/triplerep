using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

internal sealed class FinalLabMaterials
{
    public readonly Material Main, Cracked, Floor, Ceiling, Steel, Diffuser, WarmDiffuser, RedDiffuser;
    private readonly Shader lit;
    private readonly string architecture;
    private readonly string folder;

    public FinalLabMaterials(string assetRoot)
    {
        lit = Shader.Find("Universal Render Pipeline/Lit")
            ?? throw new InvalidOperationException("The URP Lit shader is missing.");
        architecture = assetRoot + "/Architecture";
        folder = assetRoot + "/Materials";
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder(assetRoot, "Materials");

        Main = Concrete("Main", "concrete_wall_003", new Color(0.87f, 0.89f, 0.87f), 0.67f, 0.42f);
        Cracked = Concrete("Cracked", "cracked_concrete_wall", new Color(0.82f, 0.81f, 0.75f), 0.82f, 0.45f);
        Floor = Concrete("Floor", "concrete_floor", new Color(0.81f, 0.82f, 0.80f), 0.72f, 0.50f);
        Ceiling = Concrete("Ceiling", "concrete_wall_003", new Color(0.69f, 0.71f, 0.71f), 0.58f, 0.38f);
        Steel = MaterialAsset("FinalLab_OxidizedSteel", new Color(0.20f, 0.22f, 0.20f), 0.27f, 0.34f);
        Diffuser = Emissive("FinalLab_Fluorescent", new Color(0.66f, 0.71f, 0.68f), new Color(0.93f, 0.99f, 0.91f) * 1.2f);
        WarmDiffuser = Emissive("FinalLab_WarmGlass", new Color(0.65f, 0.45f, 0.24f), new Color(1, 0.73f, 0.4f) * 1.15f);
        RedDiffuser = Emissive("FinalLab_PilotGlass", new Color(0.45f, 0.14f, 0.09f), new Color(0.78f, 0.15f, 0.07f));
    }

    private Material Concrete(string name, string source, Color color, float bump, float smoothness)
    {
        string textures = architecture + "/" + source + "_2k.blend/textures";
        var albedo = Texture(textures + "/" + source + "_diff_2k.jpg");
        string normalPath = textures + "/" + source + "_nor_gl_2k.exr";
        var normalImporter = (TextureImporter)AssetImporter.GetAtPath(normalPath);
        if (normalImporter.textureType != TextureImporterType.NormalMap)
        {
            normalImporter.textureType = TextureImporterType.NormalMap;
            normalImporter.SaveAndReimport();
        }

        string roughnessPath = textures + "/" + source + "_rough_2k";
        roughnessPath += File.Exists(roughnessPath + ".exr") ? ".exr" : ".jpg";
        var material = MaterialAsset("FinalLab_Concrete_" + name, color, 0, smoothness);
        material.SetTexture("_BaseMap", albedo);
        material.SetTexture("_BumpMap", Texture(normalPath));
        material.SetFloat("_BumpScale", bump);
        material.SetTexture("_MetallicGlossMap", PackSmoothness(roughnessPath, source));
        material.EnableKeyword("_NORMALMAP");
        material.EnableKeyword("_METALLICSPECGLOSSMAP");
        EditorUtility.SetDirty(material);
        return material;
    }

    private Texture2D PackSmoothness(string sourcePath, string source)
    {
        string path = folder + "/" + source + "_metallic_smoothness.png";
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null)
            return existing;

        var importer = (TextureImporter)AssetImporter.GetAtPath(sourcePath);
        bool wasReadable = importer.isReadable;
        bool wasSrgb = importer.sRGBTexture;
        var compression = importer.textureCompression;
        try
        {
            importer.isReadable = true;
            importer.sRGBTexture = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            var roughness = Texture(sourcePath);
            var packed = new Texture2D(roughness.width, roughness.height, TextureFormat.RGBA32, false, true);
            try
            {
                var pixels = roughness.GetPixels();
                packed.SetPixels32(pixels.Select(pixel =>
                    new Color32(0, 0, 0, (byte)Mathf.RoundToInt((1f - Mathf.Clamp01(pixel.r)) * 255))).ToArray());
                packed.Apply();
                File.WriteAllBytes(path, packed.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(packed);
            }
        }
        finally
        {
            importer.isReadable = wasReadable;
            importer.sRGBTexture = wasSrgb;
            importer.textureCompression = compression;
            importer.SaveAndReimport();
        }

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var packedImporter = (TextureImporter)AssetImporter.GetAtPath(path);
        packedImporter.sRGBTexture = false;
        packedImporter.alphaSource = TextureImporterAlphaSource.FromInput;
        packedImporter.alphaIsTransparency = false;
        packedImporter.SaveAndReimport();
        return Texture(path);
    }

    private Material Emissive(string name, Color color, Color emission)
    {
        var material = MaterialAsset(name, color, 0, 0.24f);
        material.SetColor("_EmissionColor", emission);
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        material.EnableKeyword("_EMISSION");
        return material;
    }

    private Material MaterialAsset(string name, Color color, float metallic, float smoothness)
    {
        string path = folder + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(lit);
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = lit;
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", smoothness);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Texture2D Texture(string path)
    {
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path)
            ?? throw new InvalidOperationException("Could not import texture " + path);
    }
}
