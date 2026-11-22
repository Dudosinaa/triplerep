using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

internal sealed partial class FinalLabResonatorBuilder
{
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
