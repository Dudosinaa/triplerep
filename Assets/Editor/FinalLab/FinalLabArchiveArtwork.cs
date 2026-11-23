using System;
using System.Collections.Generic;
using System.Linq;
using FinalLab.Investigation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class FinalLabArchiveArtwork
{
    private static readonly Vector2 Size = new(0.296f, 0.379f);
    private static readonly Color Ink = new(0.225f, 0.154f, 0.103f);
    private const string TexturePath = FinalLabEvidenceAssets.Textures + "Black_Conductor_Three_Rings.asset";
    private const string MeshPath = FinalLabEvidenceAssets.Root + "/Geometry/Archive_Study_Paper.asset";

    public static void UpdateArchive()
    {
        FinalLabInvestigationSurvey.RequireBatch();
        var scene = EditorSceneManager.OpenScene(FinalLabBuilder.ScenePath);
        var manager = scene.GetRootGameObjects().Single(o => o.name == FinalLabInvestigationBuilder.RootName)
            .GetComponent<FinalLabInvestigationManager>();
        var archive = manager.Points.Single(p => p.Id == "Archive");
        var symbol = archive.Evidence.Single(e => e.Kind == EvidenceKind.Symbol);
        Apply(new FinalLabEvidenceAssets(existingArchiveResources: true), symbol.transform);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("The archive update could not be saved.");
        Debug.Log("FINAL_LAB_ARCHIVE_ARTWORK_SAVED");
    }

    internal static InvestigationDocument Apply(FinalLabEvidenceAssets assets, Transform symbol)
    {
        foreach (Transform child in symbol.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
        var drawing = assets.Plane(symbol, "Archival ink study on torn paper", Vector3.zero, Size,
            assets.Unlit("Black_Conductor_Rings", Color.white, InkStudy()));
        drawing.GetComponent<MeshFilter>().sharedMesh = PaperEdge();
        var print = assets.PrintCanvas(symbol, Size, 1000, 1281);
        assets.Label(print, "Study caption", "Знак контакта", 97, 75, 600, 92, 63, Ink, FontStyles.Italic);
        assets.Label(print, "Year", "1887", 773, 92, 150, 80, 43, Ink, FontStyles.Italic);
        assets.Label(print, "Provenance", "Происхождение неизвестно", 98, 1103, 810, 80, 45, Ink, FontStyles.Italic);
        assets.Label(print, "Inner line index", "I", 773, 489, 52, 72, 33, Ink, FontStyles.Italic);
        assets.Label(print, "Outer line index", "III", 166, 875, 61, 58, 33, Ink, FontStyles.Italic);

        var tape = new Vector2(0.050f, 0.019f);
        assets.Plane(symbol, "Faded tape", new Vector3(-0.102f, 0.188f, -0.0034f), tape, assets.BlueTape)
            .localRotation = Quaternion.Euler(0, 0, 11);
        assets.Plane(symbol, "Faded tape 2", new Vector3(0.099f, 0.185f, -0.0034f), tape, assets.BlueTape)
            .localRotation = Quaternion.Euler(0, 0, -13);
        var item = symbol.GetComponent<InvestigationDocument>();
        var area = symbol.GetComponent<BoxCollider>();
        if (area != null) area.size = new Vector3(Size.x, Size.y, 0.008f);
        return assets.AttachEvidence(symbol.gameObject,
            new EvidenceRecord { id = "Archive_Rings", title = "Знак контакта", point = 0 },
            Size, EvidenceKind.Symbol, EvidencePresentation.Mounted, area, item);
    }

    private static Texture2D InkStudy()
    {
        const int width = 960, height = 1232;
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        bool isNew = texture == null;
        if (isNew) texture = new Texture2D(width, height, TextureFormat.RGB24, true);
        else if (texture.width != width || texture.height != height)
            texture.Reinitialize(width, height, TextureFormat.RGB24, true);
        texture.name = "Contact Sign — 1887 ink study";
        var pixels = new Color32[width * height];
        float[] radii = { 0.164f, 0.262f, 0.364f }, gaps = { 32, 165, 278 };
        Color baseColor = new(0.795f, 0.688f, 0.522f);
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            float u = (x + 0.5f) / width, v = (y + 0.5f) / height;
            Vector2 p = new(u, v);
            float grain = Mathf.PerlinNoise(u * 185, v * 213) - 0.5f;
            float fibers = Mathf.PerlinNoise(u * 319, v * 89) - 0.5f;
            float cloud = Mathf.PerlinNoise(u * 5.6f + 11, v * 7.2f) - 0.5f;
            float edge = Mathf.Min(Mathf.Min(u, 1 - u), Mathf.Min(v, 1 - v));
            float edgeAge = Mathf.Exp(-edge * 49) * (0.10f + 0.073f * Mathf.PerlinNoise(u * 19, v * 27));
            float spots = Stain(p, new(0.13f, 0.29f), new(0.069f, 0.083f)) +
                          Stain(p, new(0.80f, 0.76f), new(0.156f, 0.119f)) * 0.46f +
                          Stain(p, new(0.87f, 0.19f), new(0.047f, 0.060f)) * 0.81f;
            float diagonal = v - (0.291f + u * 0.051f);
            float crease = Mathf.Exp(-Mathf.Abs(diagonal) * 260) * 0.057f -
                           Mathf.Exp(-Mathf.Abs(diagonal + 0.0041f) * 165) * 0.058f;
            Color paper = baseColor * (1 + grain * 0.11f + fibers * 0.039f + cloud * 0.15f - edgeAge - spots + crease);

            float cx = u - 0.505f, cy = (v - 0.528f) * height / width;
            float angle = Mathf.Atan2(cy, cx), degree = angle * Mathf.Rad2Deg;
            float distance = Mathf.Sqrt(cx * cx + cy * cy);
            float ink = 0f;
            for (int j = 0; j < radii.Length; j++)
            {
                float opening = Mathf.Abs(Mathf.DeltaAngle(degree, gaps[j])) - (14 + j * 2.5f);
                if (opening < -0.5f) continue;
                float radius = radii[j] + 0.0030f * Mathf.Sin(5 * angle + j) +
                    0.0018f * Mathf.Sin(13 * angle - j) + 0.0010f * Mathf.Sin(29 * angle + j * 2);
                float widthVariation = 0.0020f * Mathf.Sin(3 * angle + j * 2) +
                    0.0008f * Mathf.Sin(19 * angle - j);
                float stroke = 1 - Mathf.SmoothStep(0, 1, (Mathf.Abs(distance - radius) - (0.0038f + widthVariation)) * 890);
                ink = Mathf.Max(ink, stroke * Mathf.Clamp01(opening + 0.5f) * (0.93f + grain * 0.14f));
            }
            float annotation = Line(p, new(0.635f, 0.586f), new(0.744f, 0.645f)) +
                Line(p, new(0.744f, 0.645f), new(0.771f, 0.645f)) +
                Line(p, new(0.306f, 0.350f), new(0.227f, 0.339f)) +
                Line(p, new(0.227f, 0.339f), new(0.206f, 0.339f)) +
                0.64f * (Line(p, new(0.492f, 0.528f), new(0.521f, 0.525f)) +
                    Line(p, new(0.508f, 0.540f), new(0.505f, 0.512f)));
            float bleed = Stain(p, new(0.772f, 0.182f), new(0.011f, 0.015f)) * 2.5f;
            pixels[y * width + x] = Color.Lerp(paper, Ink, Mathf.Clamp01(ink + annotation * 0.7f + bleed));
        }
        texture.SetPixels32(pixels);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Trilinear;
        texture.Apply();
        if (isNew) AssetDatabase.CreateAsset(texture, TexturePath);
        EditorUtility.SetDirty(texture);
        return texture;
    }

    private static float Stain(Vector2 p, Vector2 center, Vector2 size)
    {
        Vector2 q = (p - center) / size;
        float delta = q.sqrMagnitude;
        if (delta > 2.8f) return 0;
        return Mathf.Exp(-delta * 1.54f) * (0.12f + 0.11f * Mathf.PerlinNoise(p.x * 48, p.y * 44));
    }

    private static float Line(Vector2 p, Vector2 begin, Vector2 end)
    {
        Vector2 span = end - begin;
        float t = Mathf.Clamp01(Vector2.Dot(p - begin, span) / span.sqrMagnitude);
        float distance = (p - (begin + span * t)).magnitude;
        return 1 - Mathf.SmoothStep(0, 1, (distance - 0.00065f) * 1050);
    }

    private static Mesh PaperEdge()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        if (existing != null) return existing;
        var uv = new List<Vector2> { new(0.5f, 0.5f) };
        Vector2[] corners = { new(0, 0), new(0, 1), new(1, 1), new(1, 0) };
        for (int side = 0; side < 4; side++)
        for (int step = 0; step < 28; step++)
        {
            Vector2 edge = Vector2.Lerp(corners[side], corners[(side + 1) % 4], step / 28f);
            float ragged = 0.0055f + 0.0042f * Mathf.Sin(step * 2.19f + side * 2) +
                0.0031f * Mathf.Sin(step * 5.28f + side);
            if (side == 2 && step is >= 20 and <= 22) ragged += 0.0135f;
            uv.Add(edge + (new Vector2(0.5f, 0.5f) - edge) * ragged * 2);
        }
        var triangles = new List<int>((uv.Count - 1) * 3);
        for (int i = 1; i < uv.Count; i++)
            triangles.AddRange(new[] { 0, i, i + 1 == uv.Count ? 1 : i + 1 });
        var mesh = new Mesh
        {
            name = "Irregular hand-cut archive paper",
            vertices = uv.Select(p => new Vector3(p.x - 0.5f, p.y - 0.5f, 0)).ToArray(),
            uv = uv.ToArray(),
            triangles = triangles.ToArray()
        };
        mesh.RecalculateNormals();
        AssetDatabase.CreateAsset(mesh, MeshPath);
        return mesh;
    }
}
