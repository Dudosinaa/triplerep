using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

internal static class FinalLabFamilyPhoto
{
    internal const string Image = FinalLabEvidenceAssets.Textures + "Father_FamilyPhoto_1982.png";
    internal const string ForestImage = FinalLabEvidenceAssets.Textures + "Father_FamilyPhoto_1982_Forest.png";
    internal const string Original = FinalLabFatherDeskRevision.Output + "/Father_FamilyPhoto_1982.original.png";

    // These masks are authored in the 1448 x 1086 original print, with Y measured from the top.
    private static readonly Vector2[] ForestMask =
    {
        new(13, 11), new(687, 11), new(620, 57), new(595, 87), new(617, 113),
        new(638, 137), new(630, 164), new(625, 192), new(624, 219), new(633, 251),
        new(655, 283), new(681, 314), new(642, 348), new(598, 366), new(542, 371),
        new(552, 342), new(557, 308), new(560, 273), new(549, 238), new(532, 216),
        new(505, 205), new(474, 205), new(443, 215), new(419, 234), new(412, 263),
        new(409, 302), new(418, 344), new(423, 383), new(406, 418), new(369, 439),
        new(340, 435), new(317, 424), new(291, 424), new(266, 441), new(250, 468),
        new(244, 510), new(226, 556), new(204, 600), new(190, 641), new(169, 696),
        new(150, 749), new(125, 779), new(110, 809), new(13, 826)
    };

    internal static void ApplyIfRequested()
    {
        string edit = Environment.GetEnvironmentVariable("FINAL_LAB_FATHER_FOREST_EDIT");
        if (string.IsNullOrEmpty(edit)) return;
        Directory.CreateDirectory(FinalLabFatherDeskRevision.Output);
        if (!File.Exists(Original)) File.Copy(Image, Original);
        var original = Decode(Original);
        var forest = Decode(edit);
        try
        {
            if (original.width != 1448 || original.height != 1086 ||
                Mathf.Abs((float)forest.width / forest.height - (float)original.width / original.height) > 0.012f)
                throw new InvalidOperationException("The forest edit does not match the original family print aspect.");
            var pixels = original.GetPixels32();
            var aligned = forest.width == original.width && forest.height == original.height ? forest.GetPixels32() : null;
            int changed = 0;
            for (int y = 0; y < original.height; y++)
            for (int x = 0; x < original.width; x++)
            {
                if (x < 13 || x > 689 || y < original.height - 828 || y > original.height - 10) continue;
                var p = new Vector2(x, original.height - 1 - y);
                if (p.x is >= 435 and <= 550 && p.y is >= 235 and <= 393) continue;
                float opacity = Mask(p, ForestMask, 25);
                if (opacity <= 0) continue;
                int i = y * original.width + x;
                Color32 before = pixels[i];
                Color32 background = aligned != null ? aligned[i] :
                    (Color32)forest.GetPixelBilinear((x + 0.5f) / original.width, (y + 0.5f) / original.height);
                pixels[i] = Color32.Lerp(before, background, opacity);
                changed++;
            }
            if (changed < 50000) throw new InvalidOperationException("The forest mask did not cover the background.");
            original.SetPixels32(pixels);
            original.Apply();
            WriteTexture(ForestImage, original.EncodeToPNG());
            byte[] unchanged = File.ReadAllBytes(Original);
            if (!File.ReadAllBytes(Image).SequenceEqual(unchanged)) WriteTexture(Image, unchanged);
            var material = AssetDatabase.LoadAssetAtPath<Material>(
                FinalLabEvidenceAssets.Root + "/Materials/Father_FamilyPhoto_1982.mat");
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ForestImage));
            EditorUtility.SetDirty(material);
            Debug.Log($"FINAL_LAB_FAMILY_PHOTO masked {changed} background pixels without replacing people or the main house.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(original);
            UnityEngine.Object.DestroyImmediate(forest);
        }
    }

    private static void WriteTexture(string destination, byte[] pixels)
    {
        string staging = Path.Combine(FinalLabFatherDeskRevision.Output, Path.GetFileName(destination) + ".tmp");
        File.WriteAllBytes(staging, pixels);
        AssetDatabase.ReleaseCachedFileHandles();
        if (File.Exists(destination)) File.Replace(staging, destination, null);
        else File.Move(staging, destination);
        AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(destination);
        if (importer.maxTextureSize == 2048 && importer.textureCompression == TextureImporterCompression.Uncompressed &&
            importer.wrapMode == TextureWrapMode.Clamp && importer.npotScale == TextureImporterNPOTScale.None) return;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.SaveAndReimport();
    }

    private static Texture2D Decode(string name)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(name)))
            throw new InvalidOperationException("Could not decode " + name);
        return texture;
    }

    private static float Mask(Vector2 p, Vector2[] vertices, float feather)
    {
        // Most pixels are outside the two replacement patches.
        bool inside = false;
        float edge = float.PositiveInfinity;
        for (int i = 0, j = vertices.Length - 1; i < vertices.Length; j = i++)
        {
            Vector2 a = vertices[j], b = vertices[i], span = b - a;
            if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                inside = !inside;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, span) / span.sqrMagnitude);
            edge = Mathf.Min(edge, (p - a - span * t).sqrMagnitude);
        }
        return inside ? Mathf.SmoothStep(0, 1, Mathf.Sqrt(edge) / feather) : 0;
    }
}
