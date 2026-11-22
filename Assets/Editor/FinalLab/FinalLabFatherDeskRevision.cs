using System;
using System.IO;
using System.Linq;
using FinalLab.Investigation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using Object = UnityEngine.Object;

public static class FinalLabFatherDeskRevision
{
    private const string FolderId = "Father_Folder";
    private const float ViewFov = 58f;
    internal const string Output = "Logs/FinalLab/FatherDesk";

    public static void Apply()
    {
        FinalLabInvestigationSurvey.RequireBatch();
        var source = AssetDatabase.LoadAssetAtPath<TextAsset>(FinalLabEvidenceAssets.Data);
        var catalog = JsonUtility.FromJson<EvidenceCatalog>(source.text);
        bool catalogChanged = false;
        foreach (var record in catalog.documents.Where(r => r.point == 1))
        {
            catalogChanged |= TrimRepeatedDate(record);
            EditPrefab(record);
        }
        if (catalogChanged)
        {
            File.WriteAllText(FinalLabEvidenceAssets.Data, JsonUtility.ToJson(catalog, true));
            AssetDatabase.ImportAsset(FinalLabEvidenceAssets.Data, ImportAssetOptions.ForceSynchronousImport);
        }
        FinalLabFamilyPhoto.ApplyIfRequested();

        var scene = EditorSceneManager.OpenScene(FinalLabBuilder.ScenePath, OpenSceneMode.Single);
        var manager = scene.GetRootGameObjects().Single(o => o.name == FinalLabInvestigationBuilder.RootName)
            .GetComponent<FinalLabInvestigationManager>();
        var point = manager.Points.Single(p => p.Id == "Father");
        ConfigureDesk(point, new FinalLabEvidenceAssets(existingArchiveResources: true));
        foreach (var item in point.Evidence.Where(e => e.Kind == EvidenceKind.Document))
        {
            var record = catalog.documents.Single(r => r.id == item.Id);
            SetBody(item, record.body);
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("FinalScene could not be saved.");
        Debug.Log("FINAL_LAB_FATHER_REVISION_SAVED");
    }

    internal static bool TrimRepeatedDate(EvidenceRecord record)
    {
        if (record.point != 1) return false;
        string repeated = record.date + ".\n";
        if (!record.body.StartsWith(repeated, StringComparison.Ordinal)) return false;
        record.body = record.body[repeated.Length..];
        return true;
    }

    internal static void EditPrefab(EvidenceRecord data)
    {
        string path = FinalLabEvidenceAssets.Root + "/Pages/" + data.id + ".prefab";
        var prefab = PrefabUtility.LoadPrefabContents(path);
        try
        {
            if (SetBody(prefab.GetComponent<InvestigationDocument>(), data.body))
                PrefabUtility.SaveAsPrefabAsset(prefab, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefab);
        }
    }

    internal static bool SetBody(InvestigationDocument item, string revised)
    {
        var text = item.transform.Find("Print/Body").GetComponent<TMP_Text>();
        bool dirty = false;
        if (text.text != revised)
        {
            text.text = revised;
            PrefabUtility.RecordPrefabInstancePropertyModifications(text);
            EditorUtility.SetDirty(text);
            dirty = true;
        }
        if (item.Record.body != revised)
        {
            var serialized = new SerializedObject(item);
            serialized.FindProperty("record").FindPropertyRelative("body").stringValue = revised;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(item);
            dirty = true;
        }
        return dirty;
    }

    internal static Bounds Tabletop(InvestigationPoint point)
    {
        var model = point.transform.Find("Father's secondary desk");
        var renderers = model.GetComponentsInChildren<MeshRenderer>().Where(r => r.enabled).ToArray();
        var whole = FinalLabPropsSurvey.Bounds(renderers);
        var topVertices = renderers
            .Select(r => r.GetComponent<MeshFilter>()).Where(f => f != null && f.sharedMesh != null && f.sharedMesh.isReadable)
            .SelectMany(f => f.sharedMesh.vertices.Select(v => f.transform.TransformPoint(v)))
            .Where(v => v.y >= whole.max.y - 0.033f).ToArray();
        if (topVertices.Length < 4) throw new InvalidOperationException("The tabletop rim could not be sampled from the desk meshes.");
        var bounds = new Bounds(topVertices[0], Vector3.zero);
        foreach (var vertex in topVertices) bounds.Encapsulate(vertex);
        return bounds;
    }

    internal static void ConfigureDesk(InvestigationPoint point, FinalLabEvidenceAssets assets)
    {
        var tabletop = Tabletop(point);
        var root = point.transform;
        var top = root.Find("Father desk collision").GetComponent<BoxCollider>();
        var baseCollider = root.Find("Father desk leg collision").GetComponent<BoxCollider>();
        Vector3 center = new(tabletop.center.x, tabletop.max.y, tabletop.center.z);
        top.transform.position = center - Vector3.up * 0.024f;
        top.size = new Vector3(tabletop.size.x - 0.007f, 0.048f, tabletop.size.z - 0.007f);
        baseCollider.transform.position = new Vector3(center.x, baseCollider.transform.position.y, center.z);

        var marker = root.Find("Desktop contour anchor");
        marker.SetPositionAndRotation(center + Vector3.up * 0.008f, Quaternion.LookRotation(Vector3.down, Vector3.left));
        var old = marker.Find("Objective contour");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var outline = assets.CornerOutline(marker, "Objective contour",
            new Vector2(tabletop.size.z + 0.009f, tabletop.size.x + 0.009f), 0.005f, false, assets.Goal);

        var folder = point.Evidence.FirstOrDefault(e => e.Id == FolderId) ?? ConfigureFolder(root, assets);
        var folderTitle = folder.transform.Find("Label printing/Print/Project").GetComponent<TMP_Text>();
        folderTitle.fontSize = 42;
        folderTitle.textWrappingMode = TextWrappingModes.NoWrap;
        var evidence = point.Evidence.Contains(folder) ? point.Evidence : point.Evidence.Append(folder).ToArray();
        foreach (var e in evidence)
            if (e.transform.position.x < tabletop.min.x || e.transform.position.x > tabletop.max.x ||
                e.transform.position.z < tabletop.min.z || e.transform.position.z > tabletop.max.z)
                throw new InvalidOperationException(e.Id + " is not resting on the identified tabletop.");
        float span = 2 * Mathf.Tan(ViewFov * 0.5f * Mathf.Deg2Rad);
        float height = Mathf.Max(tabletop.size.x / (span * 0.51f), tabletop.size.z / (span * 0.89f));
        point.Overview.SetPositionAndRotation(center + height * Vector3.up, Quaternion.LookRotation(Vector3.down, Vector3.left));
        point.Focus.position = center + new Vector3(tabletop.extents.x * 0.67f, 0.12f, 0);
        point.Configure(point.Id, point.KeyId, point.Objective, "E — Изучить записи отца", point.Hint, point.Reaction,
            false, false, point.Range, point.Overview, point.Focus, outline, new Collider[] { top, baseCollider }, evidence);
        EditorUtility.SetDirty(point);
        Debug.Log($"FINAL_LAB_FATHER_TABLETOP {tabletop.min:F3} .. {tabletop.max:F3}. Camera {point.Overview.position:F3}.");
    }

    private static InvestigationDocument ConfigureFolder(Transform root, FinalLabEvidenceAssets assets)
    {
        var folder = root.Find("Closed workshop folder");
        var front = folder.Find("Front");
        var tag = folder.Find("Blank label");
        if (front == null || tag == null) throw new InvalidOperationException("The existing folder or its label is missing.");
        var pivot = new GameObject("Project Nechist archive folder").transform;
        pivot.SetParent(folder, false);
        pivot.SetPositionAndRotation(front.position, front.rotation);
        front.SetParent(pivot, true);
        tag.SetParent(pivot, true);
        var anchor = new GameObject("Label printing").transform;
        anchor.SetParent(pivot, false);
        anchor.SetPositionAndRotation(tag.position, tag.rotation);
        var canvas = assets.PrintCanvas(anchor, new Vector2(tag.localScale.x, tag.localScale.y), 600, 220);
        Color ink = new(0.22f, 0.17f, 0.115f);
        var title = assets.Label(canvas, "Project", "ПРОЕКТ „НЕЧИСТЬ“", 19, 34, 562, 79, 42, ink, FontStyles.Bold);
        var subtitle = assets.Label(canvas, "Archive", "АРХИВ К-17", 19, 128, 562, 62, 44, ink, FontStyles.Normal);
        title.font = subtitle.font = FolderFont();
        title.alignment = subtitle.alignment = TextAlignmentOptions.Center;
        return assets.AttachEvidence(pivot.gameObject,
            new EvidenceRecord { id = FolderId, title = "ПРОЕКТ „НЕЧИСТЬ“", point = 1 },
            new Vector2(front.localScale.x, front.localScale.y), EvidenceKind.Folder, EvidencePresentation.Lifted);
    }

    private static TMP_FontAsset FolderFont()
    {
        const string path = FinalLabEvidenceAssets.Root + "/Data/Father_Folder_Print.asset";
        var saved = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (saved != null) return saved;
        var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/LiberationSans.ttf");
        var font = TMP_FontAsset.CreateFontAsset(source, 56, 6, GlyphRenderMode.SDFAA, 512, 512,
            AtlasPopulationMode.Dynamic, false);
        font.name = "Father Folder Print";
        if (!font.TryAddCharacters("ПРОЕКТ „НЕЧИСТЬ“АРХИВ К-17", out string missing))
            throw new InvalidOperationException("Missing folder stamp glyphs " + missing);
        font.atlasPopulationMode = AtlasPopulationMode.Static;
        AssetDatabase.CreateAsset(font, path);
        AssetDatabase.AddObjectToAsset(font.material, font);
        foreach (var atlas in font.atlasTextures) AssetDatabase.AddObjectToAsset(atlas, font);
        EditorUtility.SetDirty(font);
        return font;
    }
}
