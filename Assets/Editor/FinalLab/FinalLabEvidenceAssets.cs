using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using FinalLab.Investigation;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using Object = UnityEngine.Object;

internal sealed class FinalLabEvidenceAssets
{
    public const string Root = "Assets/FinalLabInvestigation";
    public const string Data = Root + "/Data/FinalLabEvidence.json";
    public const string Textures = Root + "/Textures/";
    private const string Materials = Root + "/Materials/";
    private const string Pages = Root + "/Pages/";
    private const string Geometry = Root + "/Geometry/";
    public static readonly string[] PhotoNames = {
        "Archive_Photo_1887_Preparation", "Archive_Photo_1887_Aftermath", "Father_FamilyPhoto_1982", "Ivan_FatherPhoto_1995"
    };
    public static readonly string[] Captions = {
        "Опытная группа. 1887 г.", "Помещение после опыта. Ноябрь 1887 г.", "Наш дом. 1982", "Мы ещё всё докажем"
    };

    public readonly TMP_FontAsset Font;
    public readonly Material Goal, Hover, BlueTape, Paper;
    public readonly Mesh Quad;
    public readonly EvidenceCatalog Catalog;

    [Serializable] private sealed class PhotoPaths { public string[] files; }

    public FinalLabEvidenceAssets(bool existingArchiveResources = false)
    {
        if (existingArchiveResources)
        {
            Catalog = JsonUtility.FromJson<EvidenceCatalog>(AssetDatabase.LoadAssetAtPath<TextAsset>(Data).text);
            Font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root + "/Data/FinalLab_Cyrillic_SDF.asset");
            Quad = AssetDatabase.LoadAssetAtPath<Mesh>(Geometry + "Evidence_Quad.asset");
            Goal = AssetDatabase.LoadAssetAtPath<Material>(Materials + "Goal_Yellow.mat");
            Hover = AssetDatabase.LoadAssetAtPath<Material>(Materials + "Hover_Yellow.mat");
            BlueTape = AssetDatabase.LoadAssetAtPath<Material>(Materials + "Faded_Blue_Tape.mat");
            Paper = AssetDatabase.LoadAssetAtPath<Material>(Materials + "Paper_Soviet.mat");
            return;
        }
        foreach (string p in new[] { "Data", "Textures", "Materials", "Pages", "Geometry" })
            if (!AssetDatabase.IsValidFolder(Root + "/" + p)) AssetDatabase.CreateFolder(Root, p);
        ImportPhotos();
        Catalog = ImportCatalog();
        Font = CyrillicFont(Catalog);
        Quad = PageMesh();
        Goal = Unlit("Goal_Yellow", Color32Of(255, 212, 59));
        Hover = Unlit("Hover_Yellow", Color32Of(255, 228, 77));
        BlueTape = Unlit("Faded_Blue_Tape", new Color(0.16f, 0.29f, 0.41f));
        Paper = Sheet("Soviet");
    }

    internal static EvidenceCatalog ImportCatalog()
    {
        string source = Environment.GetEnvironmentVariable("FINAL_LAB_RESEARCH_SPEC");
        if (string.IsNullOrWhiteSpace(source))
        {
            var saved = AssetDatabase.LoadAssetAtPath<TextAsset>(Data);
            if (saved == null) throw new InvalidOperationException("The evidence specification is required for its first import.");
            return JsonUtility.FromJson<EvidenceCatalog>(saved.text);
        }
        var records = new List<EvidenceRecord>();
        string text = File.ReadAllText(source, Encoding.UTF8).Replace("\r\n", "\n");
        var sections = Regex.Matches(text, @"(?ms)^## ДОКУМЕНТ ([АОИК][1-4])[ \t]*\n(.*?)(?=^#{1,2} |\z)");
        foreach (Match section in sections)
        {
            string id = section.Groups[1].Value;
            string content = section.Groups[2].Value;
            string title = Header(content, "Название:");
            string date = Header(content, "Дата:");
            string style = Header(content, "Стиль:");
            const string lead = "Точный текст:";
            int textStart = content.IndexOf(lead, StringComparison.Ordinal);
            if (textStart < 0) throw new InvalidOperationException("Exact text is missing for " + id);
            string body = content[(textStart + lead.Length)..].Trim();
            int end = body.LastIndexOf("».", StringComparison.Ordinal);
            if (end < 0 || !body.StartsWith("«")) throw new InvalidOperationException("The text boundary is missing for " + id);
            body = Regex.Replace(body.Substring(1, end - 1).Trim(), @"\n\s*\n", "\n");
            if (!".!?…".Contains(body[^1])) body += ".";
            if (id == "К2") body = body.Replace("Маршрут: Россия — США.", "Маршрут: Россия — США, Нью-Йорк.");
            int point = "АОИК".IndexOf(id[0]);
            records.Add(new EvidenceRecord
            {
                id = id, title = title, date = date, body = body, style = style, point = point,
                stamp = id == "А4" ? "ДЕЛО ЗАКРЫТО" : ""
            });
            FinalLabFatherDeskRevision.TrimRepeatedDate(records[^1]);
            if (point == 2) records[^1].body = FinalLabIvanDeskRevision.DocumentBody(id);
        }
        if (records.Count != 13 || records.Select(r => r.id).Distinct().Count() != 13)
            throw new InvalidOperationException("Expected thirteen unique source documents.");
        var catalog = new EvidenceCatalog { documents = records.ToArray() };
        File.WriteAllText(Data, JsonUtility.ToJson(catalog, true), new UTF8Encoding(false));
        AssetDatabase.ImportAsset(Data, ImportAssetOptions.ForceSynchronousImport);
        return catalog;
    }

    private static string Header(string content, string label)
    {
        var match = Regex.Match(content, $@"(?m)^\*\*{Regex.Escape(label)}[ \t]+(.+?)\*\*[ \t]*$");
        if (!match.Success) throw new InvalidOperationException($"Missing {label} in a source document.");
        return match.Groups[1].Value;
    }

    private static void ImportPhotos()
    {
        string serialized = Environment.GetEnvironmentVariable("FINAL_LAB_RESEARCH_PHOTOS");
        string[] sources = string.IsNullOrEmpty(serialized) ? null : JsonUtility.FromJson<PhotoPaths>(serialized).files;
        for (int i = 0; i < PhotoNames.Length; i++)
        {
            string path = Textures + PhotoNames[i] + ".png";
            if (!File.Exists(path))
            {
                if (sources == null || sources.Length != PhotoNames.Length || !File.Exists(sources[i]))
                    throw new FileNotFoundException("The original photograph is missing.", path);
                File.Copy(sources[i], path);
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.maxTextureSize != 2048 || importer.textureCompression != TextureImporterCompression.Uncompressed ||
                importer.wrapMode != TextureWrapMode.Clamp || importer.npotScale != TextureImporterNPOTScale.None)
            {
                importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.SaveAndReimport();
            }
        }
    }

    private static TMP_FontAsset CyrillicFont(EvidenceCatalog catalog)
    {
        string path = Root + "/Data/FinalLab_Cyrillic_SDF.asset";
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (existing != null) return existing;
        var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/LiberationSans.ttf");
        var font = TMP_FontAsset.CreateFontAsset(source, 62, 8, GlyphRenderMode.SDFAA, 2048, 2048,
            AtlasPopulationMode.Dynamic, false);
        font.name = "FinalLab Cyrillic SDF";
        var characters = Enumerable.Range(32, 95).Concat(Enumerable.Range(0x0410, 64))
            .Select(x => (char)x).Concat("Ёё«»—–№…’•".AsEnumerable())
            .Concat(string.Concat(catalog.documents.Select(row => row.title + row.date + row.body + row.stamp)))
            .Where(x => !char.IsControl(x)).Distinct().ToArray();
        if (!font.TryAddCharacters(new string(characters), out string missing))
            throw new InvalidOperationException("Cyrillic font misses " + missing);
        font.atlasPopulationMode = AtlasPopulationMode.Static;
        AssetDatabase.CreateAsset(font, path);
        font.material.name = "FinalLab Cyrillic SDF Material";
        AssetDatabase.AddObjectToAsset(font.material, font);
        foreach (var atlas in font.atlasTextures)
        {
            atlas.name = "FinalLab Cyrillic Atlas";
            AssetDatabase.AddObjectToAsset(atlas, font);
        }
        EditorUtility.SetDirty(font);
        return font;
    }

    private Mesh PageMesh()
    {
        string path = Geometry + "Evidence_Quad.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh != null) return mesh;
        mesh = new Mesh { name = "Evidence Front" };
        mesh.vertices = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(-0.5f, 0.5f, 0),
            new Vector3(0.5f, 0.5f, 0), new Vector3(0.5f, -0.5f, 0) };
        mesh.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateNormals();
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    public Material Unlit(string name, Color color, Texture texture = null)
    {
        string path = Materials + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetColor("_BaseColor", color);
        material.SetTexture("_BaseMap", texture);
        EditorUtility.SetDirty(material);
        return material;
    }

    public Transform Plane(Transform parent, string name, Vector3 position, Vector2 size, Material material)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = position;
        obj.transform.localScale = new Vector3(size.x, size.y, 1);
        obj.AddComponent<MeshFilter>().sharedMesh = Quad;
        var renderer = obj.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return obj.transform;
    }

    public GameObject CornerOutline(Transform parent, string name, Vector2 size, float thickness, bool full, Material material)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        float x = size.x / 2, y = size.y / 2;
        if (full)
        {
            Plane(root.transform, "Top", new Vector3(0, y, -0.0044f), new Vector2(size.x, thickness), material);
            Plane(root.transform, "Bottom", new Vector3(0, -y, -0.0044f), new Vector2(size.x, thickness), material);
            Plane(root.transform, "Left", new Vector3(-x, 0, -0.0044f), new Vector2(thickness, size.y), material);
            Plane(root.transform, "Right", new Vector3(x, 0, -0.0044f), new Vector2(thickness, size.y), material);
        }
        else
        {
            float armX = Mathf.Min(0.17f, size.x * 0.23f);
            float armY = Mathf.Min(0.15f, size.y * 0.23f);
            foreach (int side in new[] { -1, 1 })
            foreach (int height in new[] { -1, 1 })
            {
                Plane(root.transform, $"Rail X {side} {height}", new Vector3(side * (x - armX / 2), height * y, -0.0044f),
                    new Vector2(armX, thickness), material);
                Plane(root.transform, $"Rail Y {side} {height}", new Vector3(side * x, height * (y - armY / 2), -0.0044f),
                    new Vector2(thickness, armY), material);
            }
        }
        root.SetActive(false);
        return root;
    }

    public InvestigationDocument AttachEvidence(GameObject obj, EvidenceRecord record, Vector2 size,
        EvidenceKind kind, EvidencePresentation presentation, Collider area = null, InvestigationDocument item = null)
    {
        if (area == null)
        {
            var box = obj.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(size.x, size.y, 0.008f);
            area = box;
        }
        var highlight = CornerOutline(obj.transform, "Cursor Outline", size + Vector2.one * 0.011f, 0.0035f, true, Hover);
        if (item == null) item = obj.AddComponent<InvestigationDocument>();
        item.Configure(record, kind, presentation, size, area, highlight);
        return item;
    }

    public InvestigationDocument DocumentPrefab(EvidenceRecord record, Vector2 size, EvidencePresentation presentation)
    {
        string path = Pages + record.id + ".prefab";
        var card = new GameObject(record.id + " " + record.title);
        string style = record.point == 0 ? "Archive" :
            record.id == "К2" ? "Cargo" : record.id == "О3" || record.id == "И2" ? "Private" :
            record.point == 1 ? "Soviet" : "Technical";
        Plane(card.transform, "Paper", Vector3.zero, size, Sheet(style));
        Transform canvas = PrintCanvas(card.transform, size, 1000, 1320);
        Color ink = new(0.16f, 0.132f, 0.102f);
        Label(canvas, "Edition", $"{record.id}  /  {Edition(style)}", 84, 52, 832, 44, 27, ink, FontStyles.Bold);
        Label(canvas, "Title", record.title, 83, 117, 830, 132, 60, ink, FontStyles.Bold, 44);
        Label(canvas, "Date", record.date, 84, 258, 832, 58, 38, new Color(0.34f, 0.245f, 0.167f),
            style == "Private" ? FontStyles.Italic : FontStyles.Normal);
        var body = Label(canvas, "Body", record.body, 84, 339, 832, 850, style == "Private" ? 47 : 45, ink,
            style == "Private" ? FontStyles.Italic : FontStyles.Normal, 35);
        body.lineSpacing = style == "Archive" ? 7 : 4;
        if (!string.IsNullOrEmpty(record.stamp))
            Label(canvas, "Archive seal", record.stamp, 556, 1199, 347, 62, 39,
                new Color(0.56f, 0.17f, 0.11f), FontStyles.Bold);
        else
            Label(canvas, "Page index", "— " + record.id + " —", 84, 1221, 830, 55, 29, ink, FontStyles.Normal);
        AttachEvidence(card, record, size, EvidenceKind.Document, presentation);
        var saved = PrefabUtility.SaveAsPrefabAsset(card, path);
        Object.DestroyImmediate(card);
        return saved.GetComponent<InvestigationDocument>();
    }

    public InvestigationDocument PhotoPrefab(int index, int point, float width, EvidencePresentation presentation)
    {
        string name = PhotoNames[index];
        string path = Pages + name + ".prefab";
        var photo = AssetDatabase.LoadAssetAtPath<Texture2D>(Textures + name + ".png");
        if (index == 2) photo = AssetDatabase.LoadAssetAtPath<Texture2D>(FinalLabFamilyPhoto.ForestImage) ?? photo;
        if (index == 3) photo = AssetDatabase.LoadAssetAtPath<Texture2D>(FinalLabIvanDeskRevision.PortraitAsset) ?? photo;
        var asset = new GameObject(name);
        float imageWidth = width * 0.90f;
        float imageHeight = imageWidth * photo.height / photo.width;
        var size = new Vector2(width, imageHeight + width * 0.19f);
        Plane(asset.transform, "Photo print backing", Vector3.zero, size, Sheet("Photo"));
        Plane(asset.transform, "Photographic image", new Vector3(0, width * 0.042f, -0.001f),
            new Vector2(imageWidth, imageHeight), Unlit(name, Color.white, photo));
        Transform canvas = PrintCanvas(asset.transform, size, 1000, 860);
        Label(canvas, "Caption", Captions[index], 56, 746, 895, 76, 43, new Color(0.18f, 0.15f, 0.12f),
            index == 3 ? FontStyles.Italic : FontStyles.Normal, 30).alignment = TextAlignmentOptions.Center;
        var record = new EvidenceRecord { id = name, title = Captions[index], point = point, body = "", date = "", style = "Photo" };
        AttachEvidence(asset, record, size, EvidenceKind.Photograph, presentation);
        var saved = PrefabUtility.SaveAsPrefabAsset(asset, path);
        Object.DestroyImmediate(asset);
        return saved.GetComponent<InvestigationDocument>();
    }

    public Transform PrintCanvas(Transform parent, Vector2 size, float px, float py)
    {
        var node = new GameObject("Print", typeof(RectTransform), typeof(Canvas));
        node.transform.SetParent(parent, false);
        node.transform.localPosition = new Vector3(0, 0, -0.0023f);
        node.transform.localScale = new Vector3(size.x / px, size.y / py, size.x / px);
        node.GetComponent<RectTransform>().sizeDelta = new Vector2(px, py);
        node.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        return node.transform;
    }

    public TMP_Text Label(Transform canvas, string name, string value, float x, float y, float width, float height,
        float size, Color color, FontStyles style, float minimum = 0)
    {
        var node = new GameObject(name, typeof(RectTransform));
        node.transform.SetParent(canvas, false);
        var label = node.AddComponent<TextMeshProUGUI>();
        var rect = label.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        label.font = Font;
        label.fontSize = size;
        label.enableAutoSizing = minimum > 0;
        if (minimum > 0) { label.fontSizeMin = minimum; label.fontSizeMax = size; }
        label.alignment = TextAlignmentOptions.TopLeft;
        label.fontStyle = style;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.richText = false;
        label.raycastTarget = false;
        label.overflowMode = TextOverflowModes.Overflow;
        label.color = color;
        label.text = value;
        return label;
    }

    private static string Edition(string style) => style == "Archive" ? "ИСТОРИЧЕСКИЙ АРХИВ" :
        style == "Cargo" ? "ГРУЗОВОЕ ДЕЛО" : style == "Private" ? "ЛИЧНЫЕ ЗАПИСИ" : "ЛАБОРАТОРНЫЕ МАТЕРИАЛЫ";

    private Material Sheet(string style)
    {
        string path = Textures + "Paper_" + style + ".asset";
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture == null)
        {
            const int dim = 384;
            texture = new Texture2D(dim, dim, TextureFormat.RGB24, true) { name = "Paper " + style };
            var basis = style switch
            {
                "Archive" => new Color(0.79f, 0.67f, 0.47f),
                "Private" => new Color(0.79f, 0.75f, 0.64f),
                "Soviet" => new Color(0.77f, 0.71f, 0.55f),
                "Cargo" => new Color(0.74f, 0.78f, 0.68f),
                "Photo" => new Color(0.81f, 0.75f, 0.63f),
                _ => new Color(0.80f, 0.80f, 0.72f)
            };
            var pixels = new Color32[dim * dim];
            for (int y = 0; y < dim; y++)
            for (int x = 0; x < dim; x++)
            {
                float u = x / (float)dim, v = y / (float)dim;
                float edge = Mathf.Min(u, 1 - u, v, 1 - v);
                float age = style == "Archive" ? 0.36f : 0.19f;
                float grain = Mathf.PerlinNoise(u * 142f, v * 190f) - 0.5f;
                float patches = Mathf.PerlinNoise(2 + u * 8f, v * 12f) - 0.47f;
                Color value = basis * (1 - age * Mathf.Exp(-edge * 61) + patches * 0.13f + grain * 0.108f);
                if ((style == "Technical" || style == "Cargo") && ((x % 45 == 13 && x > 12) || y % 36 == 11))
                    value *= 0.94f;
                if (style == "Soviet" && y % 17 == 4) value *= 0.952f;
                if (y == 291 || y == 290) value *= x is > 28 and < 356 ? 0.73f : 1;
                pixels[y * dim + x] = value;
            }
            texture.SetPixels32(pixels);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.Apply();
            AssetDatabase.CreateAsset(texture, path);
        }
        return Unlit("Paper_" + style, Color.white, texture);
    }

    internal static Color Color32Of(byte r, byte g, byte b) => new Color32(r, g, b, 255);
}
