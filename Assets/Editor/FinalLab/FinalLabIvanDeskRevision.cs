using System;
using System.IO;
using System.Linq;
using FinalLab.Investigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class FinalLabIvanDeskRevision
{
    internal const string Output = "Logs/FinalLab/IvanDesk";
    internal const string PortraitAsset = FinalLabEvidenceAssets.Textures + "Ivan_FatherPhoto_1995_Standing.png";
    private const float OverviewFov = 58;
    private const float OutlinePadding = 0.008f;

    public static void Apply()
    {
        FinalLabInvestigationSurvey.RequireBatch();
        var catalog = JsonUtility.FromJson<EvidenceCatalog>(AssetDatabase.LoadAssetAtPath<TextAsset>(FinalLabEvidenceAssets.Data).text);
        var revised = catalog.documents.Where(record => record.point == 2).ToArray();
        if (revised.Length != 3) throw new InvalidOperationException("Three original Ivan documents are required.");
        bool changed = false;
        foreach (var record in revised)
        {
            string text = DocumentBody(record.id);
            changed |= record.body != text;
            record.body = text;
            FinalLabFatherDeskRevision.EditPrefab(record);
        }
        if (changed)
        {
            string staging = Path.Combine(Output, "FinalLabEvidence.json.tmp");
            File.WriteAllText(staging, JsonUtility.ToJson(catalog, true));
            AssetDatabase.ReleaseCachedFileHandles();
            File.Replace(staging, FinalLabEvidenceAssets.Data, null);
            AssetDatabase.ImportAsset(FinalLabEvidenceAssets.Data, ImportAssetOptions.ForceSynchronousImport);
        }
        ImportPortrait();
        var scene = EditorSceneManager.OpenScene(FinalLabBuilder.ScenePath, OpenSceneMode.Single);
        var roots = scene.GetRootGameObjects();
        var manager = roots.Single(node => node.name == FinalLabInvestigationBuilder.RootName)
            .GetComponent<FinalLabInvestigationManager>();
        var workspace = roots.Single(node => node.name == "FinalLab_Props").transform.Find("02_IvanWorkspace");
        var point = manager.Points.Single(candidate => candidate.Id == "Ivan");
        ConfigureDesk(point, workspace, new FinalLabEvidenceAssets(existingArchiveResources: true));
        foreach (var document in point.Evidence.Where(item => item.Kind == EvidenceKind.Document))
            FinalLabFatherDeskRevision.SetBody(document, DocumentBody(document.Id));
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("FinalScene could not be saved.");
        Debug.Log("FINAL_LAB_IVAN_REVISION_SAVED");
    }

    internal static string DocumentBody(string id) => id switch
    {
        "И1" => string.Join("\n",
            "Отец снова потребовал прекратить работу. Он называет голоса вмешательством в память, а я вижу главное: К-17 действительно получил ответ.",
            "Если нам удастся удержать сигнал, мы докажем, что разум существует за пределами человеческого тела.",
            "Институты будут бороться за наши исследования. Деньги, признание — всё это станет возможным.",
            "Я не позволю страху перечеркнуть годы работы.",
            "Когда-нибудь отец признает, что я был прав."),
        "И2" => string.Join("\n",
            "Я вошёл в техническое помещение метро в 23:51, а вышел в 00:17 следующего дня.",
            "Отец пришёл остановить запуск. Контур вышел из-под контроля, защитный отсек закрылся, а проход начал исчезать.",
            "Я пытался восстановить соединение, но установка больше не отвечала.",
            "Я не видел его смерти. Он остался за закрывшимся проходом, и я больше не смог до него добраться.",
            "Алексею я рассказал не всё.",
            "Если К-17 однажды открыл проход, значит, его можно открыть снова."),
        "И3" => string.Join("\n",
            "После двух лет помех К-17 впервые передал разборчивые слова голосом отца.",
            "Он зовёт меня Ваней, вспоминает нашу мастерскую и разговоры, о которых знали только мы.",
            "Я уверен, что отец всё ещё где-то по ту сторону.",
            "Но голос почти не отвечает на мои вопросы. Вместо этого просит увеличить мощность и завершить соединение.",
            "Возможно, связь просто искажена.",
            "Я слишком много потерял, чтобы остановиться сейчас."),
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown Ivan document.")
    };

    internal static void ConfigureDesk(InvestigationPoint point, Transform workspace, FinalLabEvidenceAssets assets)
    {
        var lamp = PropBounds(workspace.Find("DeskLamp"));
        var instrument = PropBounds(workspace.Find("OldSchoolEquipment"));
        var papers = EvidenceBounds(point.Evidence);
        const float railClearance = OutlinePadding + 0.003f;
        float minimumShift = lamp.max.z + railClearance - papers.min.z;
        float maximumShift = instrument.min.z - railClearance - papers.max.z;
        if (minimumShift > maximumShift)
            throw new InvalidOperationException($"Ivan's papers need {papers.size.z:F3} m, between {lamp.max.z:F3} and {instrument.min.z:F3}.");
        float shift = Mathf.Clamp(0, minimumShift, maximumShift);
        if (Mathf.Abs(shift) > 0.00005f)
        {
            foreach (var item in point.Evidence)
            {
                item.transform.position += shift * Vector3.forward;
                PrefabUtility.RecordPrefabInstancePropertyModifications(item.transform);
            }
            papers = EvidenceBounds(point.Evidence);
        }

        var anchor = point.transform.Find("Desktop contour anchor");
        var rotation = Quaternion.LookRotation(Vector3.down, Vector3.left);
        anchor.SetPositionAndRotation(papers.center + Vector3.up * 0.010f, rotation);
        var old = anchor.Find("Objective contour");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var corners = assets.CornerOutline(anchor, "Objective contour",
            new Vector2(papers.size.z + 2 * OutlinePadding, papers.size.x + 2 * OutlinePadding),
            0.005f, false, assets.Goal);
        float projection = 2 * Mathf.Tan(OverviewFov * Mathf.Deg2Rad * 0.5f);
        float height = Mathf.Max(papers.size.x / (projection * 0.45f), papers.size.z / (projection * 0.82f));
        point.Overview.SetPositionAndRotation(papers.center + height * Vector3.up, rotation);
        point.Focus.position = papers.center + new Vector3(papers.extents.x * 0.65f, 0.12f, 0);
        var desk = workspace.Find("Desk");
        var surfaces = desk.GetComponentsInChildren<Collider>().Where(c => c.enabled).ToArray();
        point.Configure(point.Id, point.KeyId, point.Objective, "E — Осмотреть стол", point.Hint, point.Reaction,
            false, false, point.Range, point.Overview, point.Focus, corners, surfaces, point.Evidence);
        EditorUtility.SetDirty(point);
        Debug.Log($"FINAL_LAB_IVAN_TABLETOP papers {papers.min:F3} .. {papers.max:F3}, camera {point.Overview.position:F3}.");
    }

    internal static Bounds EvidenceBounds(InvestigationDocument[] evidence)
    {
        var first = evidence[0];
        var bounds = new Bounds(first.transform.position, Vector3.zero);
        foreach (var item in evidence)
        foreach (float u in new[] { -0.5f, 0.5f })
        foreach (float v in new[] { -0.5f, 0.5f })
            bounds.Encapsulate(item.transform.TransformPoint(item.Size.x * u, item.Size.y * v, 0));
        return bounds;
    }

    internal static Bounds PropBounds(Transform prop)
    {
        var renderers = prop.GetComponentsInChildren<MeshRenderer>().Where(r => r.enabled).ToArray();
        var points = renderers.Select(r => r.GetComponent<MeshFilter>())
            .Where(f => f != null && f.sharedMesh != null && f.sharedMesh.isReadable)
            .SelectMany(f => f.sharedMesh.vertices.Select(v => f.transform.TransformPoint(v))).ToArray();
        if (points.Length == 0) return FinalLabPropsSurvey.Bounds(renderers);
        var bounds = new Bounds(points[0], Vector3.zero);
        foreach (var vertex in points) bounds.Encapsulate(vertex);
        return bounds;
    }

    private static void ImportPortrait()
    {
        string generated = System.Environment.GetEnvironmentVariable("FINAL_LAB_IVAN_PORTRAIT");
        if (string.IsNullOrEmpty(generated)) return;
        string original = FinalLabEvidenceAssets.Textures + "Ivan_FatherPhoto_1995.png";
        var backup = Path.Combine(Output, "Ivan_FatherPhoto_1995.original.png");
        if (!File.Exists(backup)) File.Copy(original, backup);
        if (!File.Exists(PortraitAsset) || !File.ReadAllBytes(PortraitAsset).SequenceEqual(File.ReadAllBytes(generated)))
        {
            string staging = Path.Combine(Output, "Ivan_FatherPhoto_1995_Standing.tmp");
            File.Copy(generated, staging, true);
            AssetDatabase.ReleaseCachedFileHandles();
            if (File.Exists(PortraitAsset)) File.Replace(staging, PortraitAsset, null);
            else File.Move(staging, PortraitAsset);
        }
        AssetDatabase.ImportAsset(PortraitAsset, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(PortraitAsset);
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.SaveAndReimport();
        var photo = AssetDatabase.LoadAssetAtPath<Texture2D>(PortraitAsset);
        var old = AssetDatabase.LoadAssetAtPath<Texture2D>(original);
        if (photo.width * old.height != old.width * photo.height)
            throw new InvalidOperationException("The Ivan replacement must match the old print's 4:3 aspect.");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(FinalLabEvidenceAssets.Root + "/Materials/Ivan_FatherPhoto_1995.mat");
        mat.SetTexture("_BaseMap", photo);
        EditorUtility.SetDirty(mat);
    }
}
