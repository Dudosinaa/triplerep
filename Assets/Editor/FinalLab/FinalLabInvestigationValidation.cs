using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FinalLab.Investigation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static partial class FinalLabInvestigationValidation
{
    private const string BatchKey = "FinalLab.InvestigationCheck";
    private const string ScopeKey = "FinalLab.InvestigationScope";
    private const string GuiWidthKey = "FinalLab.DeskGameViewWidth";
    private const string NextGuiPass = "FinalLab.DeskRunCompact";
    private const string RestoreGuiSize = "FinalLab.DeskRestoreGameView";
    private const string ShowResonatorInSceneView = "FinalLab.ShowResonatorInSceneView";
    private enum Scope { All, Archive, Father, Ivan, Portals, Cutscene }
    private const string Output = FinalLabInvestigationSurvey.Logs;
    private static IEnumerator checks;
    private static Keyboard keyboard;
    private static Mouse mouse;
    private static InputDevice[] suspendedDesktopInput = Array.Empty<InputDevice>();
    private static InputSettings.BackgroundBehavior previousBackgroundBehavior;
    private static InputSettings.EditorInputBehaviorInPlayMode previousEditorInputBehavior;
    private static int frame = -1;
    private static double deadline;
    private static Scope activeScope;

    static FinalLabInvestigationValidation()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.update += Tick;
    }

    public static void Reopen()
    {
        FinalLabInvestigationSurvey.RequireBatch();
        var scene = EditorSceneManager.OpenScene(FinalLabBuilder.ScenePath);
        var roots = scene.GetRootGameObjects();
        var manager = roots.Single(o => o.name == FinalLabInvestigationBuilder.RootName)
            .GetComponent<FinalLabInvestigationManager>();
        var points = manager.Points;
        Require(points.Select(p => p.Id).SequenceEqual(new[] { "Archive", "Father", "Ivan" }),
            "The three required points and their order survived reopening.");
        Require(!manager.GetComponentsInChildren<InvestigationPoint>(true).Any(p => p.Container), "K-17 has no investigation point.");
        var owners = points;
        var evidence = owners.SelectMany(p => p.Evidence).ToArray();
        var documents = evidence.Where(e => e.Kind == EvidenceKind.Document).ToDictionary(e => e.Id);
        var source = JsonUtility.FromJson<EvidenceCatalog>(AssetDatabase.LoadAssetAtPath<TextAsset>(FinalLabEvidenceAssets.Data).text);
        Require(documents.Count == 10 && source.documents.Length == 13, "Ten active documents and thirteen catalog entries are preserved.");
        foreach (var text in source.documents.Where(text => text.point < points.Length))
        {
            var e = documents[text.id];
            Require(e.Record.title == text.title && e.Record.date == text.date && e.Record.body == text.body &&
                text.body.Length > 100 && owners[text.point].Owns(e.HitArea), text.id + " has the imported title, date, body and hit area.");
            var body = e.transform.Find("Print/Body").GetComponent<TMP_Text>();
            Require(body.text == text.body && body.font.HasCharacters(text.body.Replace("\n", "")),
                text.id + " displays all its text with Cyrillic glyph coverage.");
        }
        foreach (var text in source.documents.Where(text => text.point == 3))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{FinalLabEvidenceAssets.Root}/Pages/{text.id}.prefab");
            var saved = prefab.GetComponent<InvestigationDocument>().Record;
            Require(saved.id == text.id && saved.body == text.body, text.id + " remains available as an unmodified source document.");
        }
        var photos = evidence.Where(e => e.Kind == EvidenceKind.Photograph).ToArray();
        Require(photos.Length == 4 && photos.Select(e => e.Id).SequenceEqual(FinalLabEvidenceAssets.PhotoNames), "All four photographs are serialized.");
        var materials = new HashSet<Material>();
        foreach (var photo in photos)
        {
            var image = photo.transform.Find("Photographic image");
            var material = image.GetComponent<MeshRenderer>().sharedMaterial;
            var texture = material.GetTexture("_BaseMap");
            Require(texture.width >= 1000 && materials.Add(material) &&
                Mathf.Abs(image.localScale.x / image.localScale.y - (float)texture.width / texture.height) < 0.001f,
                photo.Id + " preserves its own high-resolution texture and aspect ratio.");
        }
        var symbol = points[0].Evidence.Single(e => e.Kind == EvidenceKind.Symbol);
        var inkStudy = symbol.transform.Find("Archival ink study on torn paper");
        Require(inkStudy != null && inkStudy.GetComponent<MeshFilter>().sharedMesh.vertexCount > 4 &&
            inkStudy.GetComponent<MeshRenderer>().sharedMaterial.GetTexture("_BaseMap") != null &&
            points[0].GetComponentsInChildren<Transform>(true)
                .All(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0),
            "The archive sculpture, paper texture and all archive scripts are present.");
        VerifyFatherDesk(points[1], roots);
        VerifyIvanDesk(points[2], roots);
        var all = roots.SelectMany(o => o.GetComponentsInChildren<Transform>(true)).ToArray();
        var architecture = roots.Single(o => o.name == "FinalLab_Environment").transform;
        Physics.SyncTransforms();
        FinalLabResonatorValidation.Verify(roots);
        VerifyWalkway();
        VerifyPortalScene(roots, architecture);
        Require(all.Count(t => t.GetComponent<Camera>() != null) == 1 && all.Count(t => t.GetComponent<AudioListener>() != null) == 1,
            "The inspection uses the existing camera and AudioListener.");
        Debug.Log("FINAL_LAB_INVESTIGATION_REOPEN passed.");
    }

    private static void VerifyIvanDesk(InvestigationPoint point, GameObject[] roots)
    {
        var props = roots.Single(o => o.name == "FinalLab_Props").transform.Find("02_IvanWorkspace");
        var lamp = FinalLabIvanDeskRevision.PropBounds(props.Find("DeskLamp"));
        var instrument = FinalLabIvanDeskRevision.PropBounds(props.Find("OldSchoolEquipment"));
        var papers = FinalLabIvanDeskRevision.EvidenceBounds(point.Evidence);
        var outline = point.transform.Find("Desktop contour anchor/Objective contour");
        var renderers = outline.GetComponentsInChildren<MeshRenderer>(true);
        var rails = FinalLabPropsSurvey.Bounds(renderers);
        Require(renderers.Length == 8 && rails.min.z > lamp.max.z && rails.max.z < instrument.min.z &&
            rails.min.x < papers.min.x && rails.max.x > papers.max.x &&
            rails.min.z < papers.min.z && rails.max.z > papers.max.z,
            "Ivan's four yellow corners enclose the papers and the photo, outside the lamp and instrument.");
        Require(point.Overview.forward.y < -0.9999f &&
            Vector3.Distance(new Vector3(point.Overview.position.x, papers.center.y, point.Overview.position.z), papers.center) < 0.002f,
            "Ivan's overview camera looks straight down at the evidence center.");
        var docs = point.Evidence.Where(e => e.Kind == EvidenceKind.Document).ToArray();
        Require(point.Evidence.Length == 4 && docs.Select(e => e.Id).SequenceEqual(new[] { "И1", "И2", "И3" }) &&
            docs.Select(e => e.Record.date).SequenceEqual(new[] { "1989 год", "1999 год", "2001 год" }) &&
            docs.All(e => e.Record.body == FinalLabIvanDeskRevision.DocumentBody(e.Id) && !e.Record.body.Contains(e.Record.date)) &&
            docs[1].Record.body.Contains("в 23:51, а вышел в 00:17 следующего дня") &&
            docs.All(e => !e.Record.body.Contains("23:57")),
            "Ivan's three exact texts, dates and incident times survived reopening.");
        Require(point.Evidence.All(e => e.HitArea is BoxCollider box && box.isTrigger &&
            Vector2.Distance(new Vector2(box.size.x, box.size.y), e.Size) < 0.001f) &&
            point.transform.GetComponentsInChildren<Transform>(true)
                .All(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0),
            "Ivan's four interactive hit areas match their paper sizes and no scripts are missing.");
        var photo = point.Evidence.Single(e => e.Kind == EvidenceKind.Photograph);
        var image = photo.transform.Find("Photographic image");
        var edited = AssetDatabase.LoadAssetAtPath<Texture2D>(FinalLabIvanDeskRevision.PortraitAsset);
        Require((edited == null || image.GetComponent<MeshRenderer>().sharedMaterial.GetTexture("_BaseMap") == edited) &&
            image.GetComponent<MeshFilter>().sharedMesh.uv.Distinct().Count() == 4 &&
            photo.transform.Find("Print/Caption").GetComponent<TMP_Text>().text == "Мы ещё всё докажем",
            "Ivan's portrait is bound to its existing UV and caption.");
    }

    private static void VerifyFatherDesk(InvestigationPoint point, GameObject[] roots)
    {
        var table = FinalLabFatherDeskRevision.Tabletop(point);
        var outline = point.transform.Find("Desktop contour anchor/Objective contour");
        var rails = FinalLabPropsSurvey.Bounds(outline.GetComponentsInChildren<MeshRenderer>(true));
        Require(Vector3.Distance(new Vector3(table.center.x, 0, table.center.z),
                    new Vector3(point.Overview.position.x, 0, point.Overview.position.z)) < 0.003f &&
                point.Overview.forward.y < -0.9999f && point.Overview.position.y > table.max.y + 0.8f,
            "The father camera is centered directly above the physical tabletop.");
        var chair = roots.Single(o => o.name == "FinalLab_Props").transform.Find("06_ControlRoom/Chair (1)");
        var chairBounds = FinalLabPropsSurvey.Bounds(chair.GetComponentsInChildren<MeshRenderer>().Where(r => r.enabled));
        var topCollider = point.transform.Find("Father desk collision").GetComponent<BoxCollider>();
        Require(rails.max.z < chairBounds.min.z && Mathf.Abs(rails.min.z - table.min.z) < 0.018f &&
            Mathf.Abs(rails.max.z - table.max.z) < 0.018f && Mathf.Abs(rails.max.x - table.max.x) < 0.018f &&
            point.Owns(topCollider), "Father's four yellow corners match the tabletop and exclude the chair.");
        var text = point.Evidence.Single(e => e.Kind == EvidenceKind.Folder).GetComponentsInChildren<TMP_Text>();
        Require(point.Evidence.Length == 5 && text.Length == 2 &&
            text.Select(t => t.text).SequenceEqual(new[] { "ПРОЕКТ „НЕЧИСТЬ“", "АРХИВ К-17" }) &&
            text.All(t => t.font.HasCharacters(t.text)) &&
            point.transform.GetComponentsInChildren<Transform>(true)
                .All(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0),
            "The desk has five targets, a printed folder tag and no missing scripts.");
        string[] openings = { "В подвале нашего дома", "К-17 получил устойчивый ответ.", "Мы переезжаем в Америку" };
        var papers = point.Evidence.Where(e => e.Kind == EvidenceKind.Document).ToArray();
        Require(papers.Length == 3 && papers.Zip(openings, (e, start) => e.Record.body.StartsWith(start) &&
            !e.Record.body.StartsWith(e.Record.date)).All(match => match), "The three paper bodies start after their header dates.");
    }

    private static void VerifyWalkway()
    {
        var approach = FinalLabInvestigationBuilder.Approaches;
        var nodes = new[] { approach[0], new Vector3(0, 0, 6.58f), new(-1.90f, 0, 7.02f), approach[3],
            new(-3.54f, 0, 7.55f), approach[1],
            new(-2.61f, 0, 9.65f), new(-2.61f, 0, 11.30f), approach[2] };
        int samples = 0;
        for (int i = 1; i < nodes.Length; i++)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(nodes[i - 1], nodes[i]) / 0.09f));
            for (int j = 1; j <= steps; j++)
            {
                Vector3 p = Vector3.Lerp(nodes[i - 1], nodes[i], j / (float)steps);
                var bottom = p + Vector3.up * 0.30f;
                var hits = Physics.OverlapCapsule(bottom, bottom + Vector3.up * 1.19f,
                    0.27f, ~0, QueryTriggerInteraction.Ignore).Where(c => c.GetComponent<CharacterController>() == null).ToArray();
                if (hits.Length != 0)
                    throw new InvalidOperationException($"The investigation walkway meets {hits[0].name} at {p:F3}.");
                samples++;
            }
        }
        Require(samples > 100, $"Walkway clearance was checked at {samples} standing-player positions.");
    }

    public static void Play() => BeginBatchPlay(Scope.All);

    public static void PlayArchive() => BeginBatchPlay(Scope.Archive);

    public static void PlayFather() => BeginBatchPlay(Scope.Father);

    public static void PlayIvan() => BeginBatchPlay(Scope.Ivan);

    public static void PlayPortals() => BeginBatchPlay(Scope.Portals);

    public static void PlayCutscene() => BeginBatchPlay(Scope.Cutscene);

    private static void BeginBatchPlay(Scope scope)
    {
        Reopen();
        BeginPlay(scope);
    }

    public static void OpenArchiveInEditor() => OpenInEditor(Scope.Archive);

    public static void OpenFatherInEditor() => OpenInEditor(Scope.Father);

    public static void OpenIvanInEditor() => OpenInEditor(Scope.Ivan);

    public static void OpenPortalsInEditor() => OpenInEditor(Scope.Portals);

    public static void OpenCutsceneInEditor() => OpenInEditor(Scope.Cutscene);

    public static void OpenResonatorInEditor()
    {
        SessionState.SetBool(ShowResonatorInSceneView, true);
        OpenInEditor(Scope.Portals);
    }

    private static bool IsDeskScope(Scope scope) => scope is Scope.Father or Scope.Ivan;

    private static bool UsesSizedGameView(Scope scope) => IsDeskScope(scope) || scope is Scope.Portals or Scope.Cutscene;

    private static void OpenInEditor(Scope scope)
    {
        if (Application.isBatchMode) throw new InvalidOperationException("The interactive archive check requires the graphical editor.");
        EditorSceneManager.OpenScene(FinalLabBuilder.ScenePath);
        var gameView = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
        if (gameView != null) EditorWindow.GetWindow(gameView).Focus();
        if (UsesSizedGameView(scope)) SetDeskGameView(1920, 1080);
        BeginPlay(scope);
    }

    private static void SetDeskGameView(int width, int height)
    {
        FinalLabGameViewSize.SetResolution(width, height);
        SessionState.SetInt(GuiWidthKey, width);
    }

    private static void BeginPlay(Scope scope)
    {
        SessionState.SetBool(BatchKey, true);
        SessionState.SetInt(ScopeKey, (int)scope);
        EditorApplication.EnterPlaymode();
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode && !Application.isBatchMode && keyboard != null)
        {
            checks = null;
            RestoreDesktopInput();
            SessionState.EraseBool(BatchKey);
            SessionState.EraseInt(ScopeKey);
            if (UsesSizedGameView(activeScope)) SessionState.SetBool(RestoreGuiSize, true);
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            int nextScope = SessionState.GetInt(NextGuiPass, -1);
            if (nextScope >= 0)
            {
                SessionState.EraseInt(NextGuiPass);
                EditorApplication.delayCall += () =>
                {
                    SetDeskGameView(1366, 768);
                    BeginPlay((Scope)nextScope);
                };
            }
            else if (SessionState.GetBool(RestoreGuiSize, false))
            {
                SessionState.EraseBool(RestoreGuiSize);
                FinalLabGameViewSize.Restore();
                SessionState.EraseInt(GuiWidthKey);
            }
            if (SessionState.GetBool(ShowResonatorInSceneView, false))
            {
                SessionState.EraseBool(ShowResonatorInSceneView);
                EditorApplication.delayCall += () =>
                {
                    var view = SceneView.lastActiveSceneView;
                    if (view == null) return;
                    view.in2DMode = false;
                    view.LookAt(new Vector3(4.37f, 1.28f, 10.94f), Quaternion.LookRotation(new Vector3(1f, -.12f, .21f)), 2.45f, false, true);
                    view.Focus();
                };
            }
            return;
        }
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(BatchKey, false)) return;
        deadline = EditorApplication.timeSinceStartup + 150;
        activeScope = (Scope)SessionState.GetInt(ScopeKey, (int)Scope.All);
        checks = RunPlayChecks(activeScope);
    }

    private static void Tick()
    {
        if (checks == null || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Final Lab tests exceeded 150 seconds.");
            if (!checks.MoveNext()) Finish(null);
        }
        catch (Exception error)
        {
            Finish(error);
        }
    }

    private static IEnumerator RunPlayChecks(Scope scope)
    {
        if (!Application.isBatchMode)
        {
            suspendedDesktopInput = InputSystem.devices.Where(d => d.enabled && (d is Keyboard || d is Mouse)).ToArray();
            foreach (var device in suspendedDesktopInput) InputSystem.DisableDevice(device);
        }
        keyboard = InputSystem.AddDevice<Keyboard>("FinalLabTestKeyboard");
        mouse = InputSystem.AddDevice<Mouse>("FinalLabTestMouse");
        previousBackgroundBehavior = InputSystem.settings.backgroundBehavior;
        previousEditorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        var player = Object.FindFirstObjectByType<FirstPersonPlayer>();
        var camera = player.GetComponentInChildren<Camera>();
        var controller = player.GetComponent<CharacterController>();
        var manager = Object.FindFirstObjectByType<FinalLabInvestigationManager>();
        var ui = Object.FindFirstObjectByType<FinalLabInvestigationUI>();
        var cutscene = Object.FindFirstObjectByType<FinalLabIvanCutsceneController>();
        if (cutscene != null && scope != Scope.Cutscene) cutscene.enabled = false;
        yield return null;
        yield return null;
        if (!Application.isBatchMode && UsesSizedGameView(scope))
        {
            int width = SessionState.GetInt(GuiWidthKey, 0);
            int height = width == 1920 ? 1080 : 768;
            double sizeDeadline = EditorApplication.timeSinceStartup + 4;
            while ((Screen.width != width || Screen.height != height) &&
                EditorApplication.timeSinceStartup < sizeDeadline) yield return null;
            Require(Screen.width == width && Screen.height == height, $"Graphical Game View is actually {width}x{height}.");
        }
        int totalPoints = manager.Points.Length;
        Require(totalPoints == 3 && manager.State == FinalLabInvestigationManager.ViewState.Walking && manager.Completed == 0 &&
            ui.StatusText == "Осмотрено: 0/3" && ui.ObjectiveText == "Цель: Осмотрите архивную стену",
            "The real Play Mode starts in walking and shows the correct 0/3 objective.");
        var orderedProgress = new InvestigationProgress(totalPoints);
        Require(!orderedProgress.Complete(2) && orderedProgress.Count == 0 && !orderedProgress.IsStudied(2) &&
            orderedProgress.Complete(0) && !orderedProgress.Complete(0) && !orderedProgress.Complete(2) &&
            orderedProgress.Count == 1 && orderedProgress.Complete(1) && orderedProgress.Count == 2,
            "Progress rejects future points and never counts a completed point twice.");
        int[] sequence = scope switch
        {
            Scope.Archive => new[] { 0, 0 },
            Scope.Father => new[] { 0, 1, 1 },
            Scope.Ivan => new[] { 0, 1, 2, 2 },
            Scope.Cutscene => new[] { 0, 1, 2 },
            _ => new[] { 0, 0, 1, 2, 2 }
        };
        var interactionPrompt = ui.GetComponentsInChildren<TMP_Text>().Single(label => label.name == "Interaction");
        if (scope is Scope.Portals or Scope.All)
        {
            foreach (var ignored in PlayPortalChecks(player, camera, controller, manager, interactionPrompt)) yield return ignored;
            foreach (var ignored in PlayResonatorChecks(player, camera, controller)) yield return ignored;
        }
        int completedEvents = 0;
        manager.InvestigationCompleted += () => completedEvents++;
        foreach (var index in sequence)
        {
            var point = manager.Points[index];
            int initial = manager.Completed;
            bool repeat = index < 3 && initial == index + 1;
            bool archive = index == 0, father = index == 1, ivan = index == 2;
            bool desk = father || ivan, controlledReading = archive || desk;
            if ((scope is Scope.All or Scope.Ivan or Scope.Portals or Scope.Cutscene) && !repeat)
            {
                for (int future = initial + 1; future < manager.Points.Length; future++)
                {
                    var locked = manager.Points[future];
                    Aim(player, camera, controller, FinalLabInvestigationBuilder.Approaches[future], locked.Focus.position);
                    for (int f = 0; f < 3; f++) yield return null;
                    Require(manager.NearbyPoint() == locked, locked.Id + " is within interaction range for the lock test.");
                    Require(!manager.TryOpen(locked), locked.Id + " rejects a direct inspection request.");
                    Vector3 eye = camera.transform.position;
                    Keys(Key.E);
                    for (int f = 0; f < 3; f++) yield return null;
                    Keys();
                    yield return null;
                    Require(manager.State == FinalLabInvestigationManager.ViewState.Walking && player.enabled &&
                        manager.ActivePoint == null && manager.Completed == initial && manager.ObjectivePoint == point &&
                        interactionPrompt.text.Length == 0 && Vector3.Distance(eye, camera.transform.position) < 0.025f,
                        locked.Id + " ignores E at " + initial + "/" + totalPoints + " and leaves the current objective, camera and movement intact.");
                }
            }
            if (father && !repeat)
            {
                foreach (float side in new[] { -0.58f, 0.63f })
                {
                    Vector3 position = FinalLabInvestigationBuilder.Approaches[index] + side * Vector3.forward;
                    Aim(player, camera, controller, position, point.Focus.position);
                    for (int i = 0; i < 3; i++) yield return null;
                    Require(point.IsHighlighted && manager.NearbyPoint() == point,
                        $"Father's desk stays highlighted and reachable from offset {side:F2}.");
                    Capture(camera, side < 0 ? "Father-approach-left" : "Father-approach-right");
                }
            }
            Aim(player, camera, controller, FinalLabInvestigationBuilder.Approaches[index], point.Focus.position);
            for (int i = 0; i < 3; i++) yield return null;
            if (manager.NearbyPoint() != point)
            {
                var hit = Physics.RaycastAll(camera.transform.position, camera.transform.forward, 3.2f, ~0,
                    QueryTriggerInteraction.Collide).OrderBy(h => h.distance)
                    .Select(h => $"{h.collider.transform.parent?.name}/{h.collider.name} ({h.distance:F3})").ToArray();
                throw new InvalidOperationException($"{point.Id} is unreachable from {camera.transform.position:F3} " +
                    $"toward {camera.transform.forward:F3}. Target {point.Focus.position:F3}. Hits: {string.Join(", ", hit)}.");
            }
            Require(true, point.Id + " is reachable from its approach.");
            Vector3 startingCamera = camera.transform.position;
            CursorLockMode cursor = Cursor.lockState;
            Keys(Key.E);
            for (int f = 0; f < 3; f++) yield return null;
            Keys();
            yield return null;
            Require(manager.ActivePoint == point && !player.enabled, point.Id + " opens using E and freezes the movement controller.");
            while (manager.State == FinalLabInvestigationManager.ViewState.Moving) yield return null;
            Require(manager.State == FinalLabInvestigationManager.ViewState.Overview &&
                Vector3.Distance(camera.transform.position, point.Overview.position) < 0.015f,
                point.Id + " overview reaches its authored camera pose.");
            if (archive)
            {
                Require(ui.ArchiveCountText == (repeat ? "Документы: 4/4" : "Документы: 0/4"),
                    "The archive counter is separate and retains its result on re-entry.");
                if (!repeat)
                    foreach (var ignored in BlockedExit(manager, ui, 0)) yield return ignored;
                else if (!Application.isBatchMode) Capture(camera, "Archive-revisit");
            }
            if (desk)
            {
                Require(camera.transform.forward.y < -0.9999f && ui.ArchiveCountText ==
                    (repeat ? "Документы: 3/3" : "Документы: 0/3"), point.Id + " overview looks straight down and retains its count.");
                CheckDeskFraming(camera, point);
                if (!repeat)
                    foreach (var ignored in BlockedExit(manager, ui, 0)) yield return ignored;
            }
            if (!repeat) Capture(camera, point.Id + "-overview");
            LightAndOcclusion(point, manager, camera, !repeat);
            var selectedItems = repeat ? point.Evidence.Where(e => e.Id == point.KeyId) : point.Evidence;
            if (controlledReading && !repeat)
            {
                var first = point.Evidence.First(e => e.Kind == EvidenceKind.Document);
                selectedItems = point.Evidence.Where(e => e.Kind != EvidenceKind.Document)
                    .Concat(new[] { first })
                    .Concat(point.Evidence.Where(e => e.Kind == EvidenceKind.Document));
            }
            foreach (var evidence in selectedItems)
            {
                int viewedBefore = ReadCount(manager, index);
                Vector3 home = evidence.transform.position;
                Quaternion homeRotation = evidence.transform.rotation;
                Vector3 homeScale = evidence.transform.localScale;
                var pixel = camera.WorldToScreenPoint(home);
                Require(pixel.z > 0 && manager.DocumentAt(camera.ScreenPointToRay(pixel)) == evidence,
                    point.Id + ": the center of " + evidence.Id + " can be selected through the scene.");
                Pointer(pixel);
                for (int f = 0; f < 3; f++) yield return null;
                Require(evidence.IsHighlighted && point.Evidence.Count(e => e.IsHighlighted) == 1,
                    $"{evidence.Id} alone gets hover highlighting (pointer {Mouse.current?.position.ReadValue()} at {pixel:F1}).");
                if (desk)
                {
                    Pointer(new Vector2(5, 5));
                    for (int f = 0; f < 3; f++) yield return null;
                    Require(!evidence.IsHighlighted, evidence.Id + " loses its highlight outside its hit area.");
                    Pointer(pixel);
                }
                Pointer(pixel, true);
                for (int f = 0; f < 3; f++) yield return null;
                Pointer(pixel);
                Require(manager.Selected == evidence, evidence.Id + " selects with left click.");
                while (manager.State == FinalLabInvestigationManager.ViewState.Moving) yield return null;
                Require(manager.State == FinalLabInvestigationManager.ViewState.Detail &&
                    (Vector3.Distance(home, evidence.transform.position) > 0.10f) == evidence.Lifts,
                    evidence.Id + " uses the expected mounted or lifted inspection.");
                Require(manager.Completed == initial, evidence.Id + " cannot advance progress in detail view.");
                if (controlledReading)
                {
                    Require(!ui.MainHudVisible && ui.ArchiveReturnVisible && ui.ArchiveCountText.Length == 0 &&
                        !point.IsHighlighted && point.Evidence.All(e => !e.IsHighlighted),
                        evidence.Id + " only shows the bottom return hint and hides all yellow outlines.");
                    CheckArchiveFraming(camera, evidence);
                    string expectedHint = archive ? "ЛКМ / Esc — Назад к архиву" :
                        evidence.Kind == EvidenceKind.Document ? "ЛКМ / Esc — Положить документ" : "ЛКМ / Esc — Назад к столу";
                    Require(ui.ReadingReturnText == expectedHint && ui.ReadingReturnSize >= 24.8f,
                        evidence.Id + " shows the enlarged return hint without a scroll instruction.");
                }
                if (!repeat && (evidence.Id == point.KeyId ||
                    evidence.Kind == EvidenceKind.Photograph || controlledReading))
                    Capture(camera, evidence.Id + "-detail");
                float fov = camera.fieldOfView;
                Pointer(pixel, false, 120);
                double end = EditorApplication.timeSinceStartup + 0.20;
                while (EditorApplication.timeSinceStartup < end) yield return null;
                Pointer(pixel);
                if (controlledReading)
                {
                    Require(Mathf.Abs(camera.fieldOfView - fov) < 0.001f, evidence.Id + " ignores scrolling up.");
                    Pointer(pixel, false, -120);
                    for (int f = 0; f < 4; f++) yield return null;
                    Require(Mathf.Abs(camera.fieldOfView - fov) < 0.001f, evidence.Id + " ignores scrolling down.");
                }
                else Require(camera.fieldOfView < fov - 1, evidence.Id + " zooms with the mouse wheel.");
                if ((!controlledReading && evidence.Lifts) ||
                    (evidence.Id is "А2" or "А4" or "О2" or "О3" or "И2" or "И3")) Keys(Key.Escape);
                else Pointer(pixel, true);
                for (int f = 0; f < 3; f++) yield return null;
                Keys();
                Pointer(pixel);
                if (evidence.Id is "А4" or "О3" or "И3")
                {
                    yield return null;
                    Keys(Key.Escape);
                    yield return null;
                    Keys();
                }
                while (manager.State == FinalLabInvestigationManager.ViewState.Moving) yield return null;
                Require(manager.State == FinalLabInvestigationManager.ViewState.Overview &&
                    Vector3.Distance(home, evidence.transform.position) < 0.0002f &&
                    Quaternion.Angle(homeRotation, evidence.transform.rotation) < 0.05f &&
                    evidence.transform.localScale == homeScale && manager.Completed == initial,
                    evidence.Id + " returns to the overview and preserves its exact resting pose.");
                if (controlledReading)
                {
                    int count = ReadCount(manager, index);
                    int total = desk ? 3 : 4;
                    int expectedViews = evidence.Kind != EvidenceKind.Document || (evidence.Id is "А1" or "О1" or "И1") && viewedBefore == 1 ||
                        repeat ? viewedBefore : viewedBefore + 1;
                    Require(count == expectedViews && ui.ArchiveCountText == $"Документы: {expectedViews}/{total}" &&
                        !player.enabled && ui.MainHudVisible && !ui.ArchiveReturnVisible && point.IsHighlighted &&
                        Vector3.Distance(camera.transform.position, point.Overview.position) < 0.001f &&
                        Quaternion.Angle(camera.transform.rotation, point.Overview.rotation) < 0.04f,
                        evidence.Id + " restores the exact overview without counting duplicates or optional evidence.");
                    if (evidence.Id == "А3")
                        foreach (var ignored in BlockedExit(manager, ui, 3)) yield return ignored;
                    if (evidence.Id == "А2" && !Application.isBatchMode)
                        foreach (var ignored in BlockedExit(manager, ui, 2)) yield return ignored;
                    if (evidence.Id == "А4" && !Application.isBatchMode) Capture(camera, "Archive-all-read");
                    if (desk && !repeat && evidence.Kind == EvidenceKind.Document && count < 3)
                        foreach (var ignored in BlockedExit(manager, ui, count)) yield return ignored;
                    if (desk && !repeat && count == 3 && !Application.isBatchMode) Capture(camera, point.Id + "-all-read");
                }
            }
            if (scope == Scope.Cutscene && ivan) Require(cutscene.State == FinalLabIvanCutsceneController.Stage.Waiting,
                "Ivan stays hidden during all documents and before the final ESC.");
            Keys(Key.Escape);
            for (int f = 0; f < 3; f++) yield return null;
            Keys();
            while (manager.State == FinalLabInvestigationManager.ViewState.Moving) yield return null;
            if (scope == Scope.Cutscene && ivan)
            {
                Require(manager.Completed == 3 && completedEvents == 1 && manager.ActivePoint == null && !player.enabled,
                    "Final ESC closed Ivan's desk at 3/3 and immediately handed control to the cutscene.");
                foreach (var ignored in ExerciseIvanCutscene(cutscene, player, camera, manager, startingCamera)) yield return ignored;
                yield break;
            }
            int expected = repeat ? initial : index + 1;
            Require(player.enabled && manager.State == FinalLabInvestigationManager.ViewState.Walking &&
                Cursor.lockState == cursor && Vector3.Distance(startingCamera, camera.transform.position) < 0.025f,
                point.Id + " restores movement, camera and cursor state on exit.");
            Require(manager.Completed == expected, point.Id + " exits with " + expected + "/" + totalPoints + ".");
            double fade = EditorApplication.timeSinceStartup + 0.51;
            while (EditorApplication.timeSinceStartup < fade) yield return null;
            Require(ui.StatusText == "Осмотрено: " + expected + "/" + totalPoints &&
                manager.Points.Count(p => p.IsHighlighted) == (expected < totalPoints ? 1 : 0) &&
                (expected < totalPoints ? manager.Points[expected].IsHighlighted : ui.ObjectiveText == "Расследование завершено"),
                "HUD status and objective outline agree at " + expected + "/" + totalPoints + ".");
            if (archive && !repeat && !Application.isBatchMode) Capture(camera, "Archive-next-objective");
            if (ivan && !repeat && !Application.isBatchMode) Capture(camera, "Ivan-next-objective");
        }
        if (scope == Scope.Archive)
            Require(manager.Completed == 1 && ui.ObjectiveText == "Цель: Изучите записи отца",
                "Play Mode finished the archive and handed off to the father desk.");
        else if (scope == Scope.Father)
            Require(manager.Completed == 2 && ui.ObjectiveText == "Цель: Осмотрите стол Ивана",
                "Play Mode finished both points and hands off to Ivan's desk at 2/3.");
        else
        {
            Require(manager.Completed == 3 && completedEvents == 1 && manager.ObjectivePoint == null &&
                ui.ObjectiveText == "Расследование завершено", "The investigation ends once at 3/3 with no K-17 objective.");
            var resonator = GameObject.Find(FinalLabResonatorBuilder.ModuleName).transform;
            Aim(player, camera, controller, resonator.position + Vector3.left * 1.1f, resonator.position + Vector3.up * 1.16f);
            for (int f = 0; f < 3; f++) yield return null;
            Keys(Key.E);
            for (int f = 0; f < 3; f++) yield return null;
            Keys();
            Require(manager.ActivePoint == null && manager.Completed == 3 && interactionPrompt.text.Length == 0,
                "K-17 offers no E prompt and cannot enter the completed investigation.");
            Capture(camera, "Investigation-complete");
        }
    }

    private static int ReadCount(FinalLabInvestigationManager manager, int index) => index switch
    {
        0 => manager.ArchiveViewedCount, 1 => manager.FatherViewedCount, _ => manager.IvanViewedCount
    };

    private static IEnumerable BlockedExit(FinalLabInvestigationManager manager, FinalLabInvestigationUI ui, int count)
    {
        Keys(Key.Escape);
        for (int f = 0; f < 3; f++) yield return null;
        Keys();
        string id = manager.ActivePoint.Id;
        bool desk = id != "Archive";
        int expectedProgress = id == "Father" ? 1 : id == "Ivan" ? 2 : 0;
        string wording = desk ? $"Сначала изучите все записи {(id == "Ivan" ? "Ивана" : "отца")} — {count}/3" :
            $"Сначала изучите все архивные документы — {count}/4";
        Color32 color = ui.ReadingWarningColor;
        byte shade = desk ? (byte)245 : (byte)16;
        Require(manager.State == FinalLabInvestigationManager.ViewState.Overview && manager.Completed == expectedProgress &&
            ui.ArchiveWarningText == wording && color.r == shade && color.g == shade && color.b == shade &&
            ui.ReadingHelpSize >= 22.8f,
            $"{id} blocks Escape at {count}/{(desk ? 3 : 4)} with enlarged instructions and the correct warning color.");
        if (!Application.isBatchMode)
        {
            Capture(Camera.main, id + "-blocked-" + count);
            for (int f = 0; f < 2; f++) yield return null;
        }
    }

    private static void CheckDeskFraming(Camera camera, InvestigationPoint point)
    {
        float originalAspect = camera.aspect;
        foreach (var aspect in new[] { 1920f / 1080f, 1366f / 768f })
        {
            camera.aspect = aspect;
            foreach (var item in point.Evidence)
            foreach (float x in new[] { -0.5f, 0.5f })
            foreach (float y in new[] { -0.5f, 0.5f })
            {
                var p = camera.WorldToViewportPoint(item.transform.TransformPoint(item.Size.x * x, item.Size.y * y, 0));
                if (p.x < 0.02f || p.x > 0.98f || p.y < 0.05f || p.y > 0.73f)
                    throw new InvalidOperationException($"{item.Id} intersects the desktop overview HUD or viewport border at {p:F3}.");
            }
        }
        camera.aspect = originalAspect;
        Require(true, $"All {point.Evidence.Length} items fit under the overview HUD at 1920x1080 and 1366x768.");
    }

    private static void CheckArchiveFraming(Camera camera, InvestigationDocument evidence)
    {
        float originalAspect = camera.aspect;
        foreach (var resolution in new[] { new Vector2(1920, 1080), new Vector2(1366, 768), new Vector2(1024, 768) })
        {
            camera.aspect = resolution.x / resolution.y;
            foreach (float x in new[] { -0.5f, 0.5f })
            foreach (float y in new[] { -0.5f, 0.5f })
            {
                var edge = evidence.transform.TransformPoint(evidence.Size.x * x, evidence.Size.y * y, 0);
                var p = camera.WorldToViewportPoint(edge);
                if (p.x < 0.06f || p.x > 0.94f || p.y < 0.076f || p.y > 0.93f || p.z < camera.nearClipPlane)
                    throw new InvalidOperationException($"{evidence.Id} is outside the safe viewport at {resolution}: {p:F3}.");
            }
        }
        camera.aspect = originalAspect;
        Require(true, evidence.Id + " leaves room for the bottom hint at 1920x1080, 1366x768 and 1024x768.");
    }

    private static void LightAndOcclusion(InvestigationPoint point, FinalLabInvestigationManager manager, Camera camera, bool checkPrint)
    {
        var first = point.Evidence.First();
        var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.transform.position = Vector3.Lerp(camera.transform.position, first.transform.position, 0.56f);
        block.transform.localScale = Vector3.one * 0.08f;
        Physics.SyncTransforms();
        var ray = new Ray(camera.transform.position, first.transform.position - camera.transform.position);
        Require(manager.DocumentAt(ray) == null, point.Id + " evidence cannot be selected through solid geometry.");
        block.GetComponent<Collider>().enabled = false;
        Object.Destroy(block);
        if (!checkPrint) return;
        foreach (var e in point.Evidence.Where(e => e.Kind == EvidenceKind.Document))
        {
            var text = e.transform.Find("Print/Body").GetComponent<TMP_Text>();
            text.ForceMeshUpdate();
            var area = text.rectTransform.rect;
            var bounds = text.textBounds;
            Require(text.textInfo.characterCount > 100 && bounds.min.y >= area.yMin - 5f &&
                bounds.max.x <= area.xMax + 5f,
                $"{e.Id} renders all {text.textInfo.characterCount} characters within the paper body " +
                $"({bounds.min.y:F1} to {bounds.max.y:F1}, area starts at {area.yMin:F1}).");
        }
    }

    private static void Aim(FirstPersonPlayer player, Camera camera, CharacterController controller, Vector3 floor, Vector3 target)
    {
        player.Teleport(floor + Vector3.up * 0.022f);
        Vector3 facing = (target - camera.transform.position).normalized;
        float pitch = -Mathf.Asin(facing.y) * Mathf.Rad2Deg;
        typeof(FirstPersonPlayer).GetField("cameraPitch", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(player, pitch);
        player.transform.rotation = Quaternion.LookRotation(new Vector3(facing.x, 0, facing.z));
        camera.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
        controller.Move(Vector3.down * 0.09f);
        Physics.SyncTransforms();
    }

    private static void Keys(params Key[] code)
    {
        keyboard.MakeCurrent();
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(code));
    }

    private static void Pointer(Vector2 pixel, bool down = false, float scroll = 0)
    {
        mouse.MakeCurrent();
        InputSystem.QueueStateEvent(mouse, new MouseState { position = pixel, scroll = new Vector2(0, scroll) }
            .WithButton(MouseButton.Left, down));
    }

    private static void Capture(Camera camera, string title)
    {
        if (activeScope == Scope.Cutscene && !title.StartsWith("Cutscene-", StringComparison.Ordinal)) return;
        if (!Application.isBatchMode)
        {
            Directory.CreateDirectory(CaptureOutput);
            string resolution = UsesSizedGameView(activeScope) ? $"{Screen.width}x{Screen.height}-" : "";
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(Path.Combine(CaptureOutput, "GUI-" + resolution + title + ".png")));
            return;
        }
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
        var rt = RenderTexture.GetTemporary(1280, 900, 24);
        var previous = RenderTexture.active;
        try
        {
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
            if (!RenderPipeline.SupportsRenderRequest(camera, request)) return;
            RenderPipeline.SubmitRenderRequest(camera, request);
            RenderTexture.active = rt;
            var image = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            File.WriteAllBytes(Path.Combine(CaptureOutput, title + ".png"), image.EncodeToPNG());
            Object.Destroy(image);
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
        }
    }

    private static string CaptureOutput => activeScope switch
    {
        Scope.Father => FinalLabFatherDeskRevision.Output, Scope.Ivan => FinalLabIvanDeskRevision.Output,
        Scope.Portals => FinalLabPortalBuilder.Logs, Scope.Cutscene => "Logs/FinalLab/IvanDialogue", _ => Output
    };

    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
        Debug.Log("FINAL_LAB_CHECK " + message);
    }

    private static void Finish(Exception error)
    {
        checks = null;
        SessionState.EraseBool(BatchKey);
        SessionState.EraseInt(ScopeKey);
        if (error != null) Debug.LogException(error);
        Debug.Log("FINAL_LAB_INVESTIGATION_PLAY_" + (error == null ? "PASSED" : "FAILED"));
        if (Application.isBatchMode) EditorApplication.Exit(error == null ? 0 : 1);
        else
        {
            string phase = UsesSizedGameView(activeScope)
                ? $"{activeScope.ToString().ToUpperInvariant()}_GUI_{Screen.width}x{Screen.height}" : "ARCHIVE_GUI";
            Debug.Log("FINAL_LAB_" + phase + "_" + (error == null ? "PASSED" : "FAILED"));
            RestoreDesktopInput();
            if (IsDeskScope(activeScope))
            {
                bool verifyCompact = error == null && SessionState.GetInt(GuiWidthKey, 0) == 1920;
                if (verifyCompact) SessionState.SetInt(NextGuiPass, (int)activeScope);
                else SessionState.SetBool(RestoreGuiSize, true);
            }
            else if (UsesSizedGameView(activeScope)) SessionState.SetBool(RestoreGuiSize, true);
            EditorApplication.ExitPlaymode();
        }
    }

    private static void RestoreDesktopInput()
    {
        if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
        if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
        keyboard = null;
        mouse = null;
        foreach (var device in suspendedDesktopInput)
            if (device.added) InputSystem.EnableDevice(device);
        suspendedDesktopInput = Array.Empty<InputDevice>();
        InputSystem.settings.backgroundBehavior = previousBackgroundBehavior;
        InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInputBehavior;
    }
}
