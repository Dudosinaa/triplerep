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
public static class FinalLabInvestigationValidation
{
    private const string BatchKey = "FinalLab.InvestigationCheck";
    private const string Output = FinalLabInvestigationSurvey.Logs;
    private static IEnumerator checks;
    private static Keyboard keyboard;
    private static Mouse mouse;
    private static int frame = -1;
    private static double deadline;

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
        Require(points.Select(p => p.Id).SequenceEqual(new[] { "Archive", "Father", "Ivan", "K17" }), "The four points and their order survived reopening.");
        var evidence = points.SelectMany(p => p.Evidence).ToArray();
        var documents = evidence.Where(e => e.Kind == EvidenceKind.Document).ToDictionary(e => e.Id);
        var source = JsonUtility.FromJson<EvidenceCatalog>(AssetDatabase.LoadAssetAtPath<TextAsset>(FinalLabEvidenceAssets.Data).text);
        Require(documents.Count == 13 && source.documents.Length == 13, "All thirteen documents and unique IDs exist.");
        foreach (var text in source.documents)
        {
            var e = documents[text.id];
            Require(e.Record.title == text.title && e.Record.date == text.date && e.Record.body == text.body &&
                text.body.Length > 100 && points[text.point].Owns(e.HitArea), text.id + " has the imported title, date, body and hit area.");
            var body = e.transform.Find("Print/Body").GetComponent<TMP_Text>();
            Require(body.text == text.body && body.font.HasCharacters(text.body.Replace("\n", "")),
                text.id + " displays all its text with Cyrillic glyph coverage.");
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
        var all = roots.SelectMany(o => o.GetComponentsInChildren<Transform>(true)).ToArray();
        Require(all.Count(o => o.name == "К-17" || o.name == "K-17") == 1, "There is exactly one K-17 crate.");
        var crate = all.Single(o => o.name is "К-17" or "K-17");
        var shelf = all.Single(o => o.name == "MetalShelf");
        var shelfBounds = FinalLabPropsSurvey.Bounds(shelf.GetComponentsInChildren<MeshRenderer>().Where(r => r.enabled));
        var crateBounds = FinalLabPropsSurvey.Bounds(crate.GetComponentsInChildren<MeshRenderer>().Where(r => r.enabled));
        Require(shelfBounds.min.z - crateBounds.max.z is > 0.022f and < 0.3f && crateBounds.min.x > -4.96f,
            "The crate is beside the metal shelf with clearance from the shelf and west wall.");
        var leads = all.Single(o => o.name == "K-17 wall-mounted cable paths");
        var architecture = roots.Single(o => o.name == "FinalLab_Environment").transform;
        Physics.SyncTransforms();
        for (int i = 0; i < 2; i++)
        {
            var run = leads.GetChild(i);
            var bounds = run.GetComponent<MeshRenderer>().bounds;
            Require(run.GetComponent<Collider>() == null && bounds.max.x < -4.70f && bounds.min.x > -5.002f &&
                run.Find("Port") != null && run.Find("Distribution terminal") != null,
                run.name + " is a static, anchored lead against the wall.");
            var vertices = run.GetComponent<MeshFilter>().sharedMesh.vertices;
            for (int ring = 0; ring < vertices.Length / 10; ring++)
            {
                Vector3 center = Vector3.zero;
                for (int side = 0; side < 10; side++) center += vertices[ring * 10 + side];
                Vector3 p = run.TransformPoint(center / 10);
                if (Physics.OverlapSphere(p, 0.008f, ~0, QueryTriggerInteraction.Ignore)
                    .Any(c => c.transform.IsChildOf(architecture)))
                    throw new InvalidOperationException($"{run.name} intersects a concrete surface at {p:F3}.");
            }
        }
        VerifyWalkway();
        Require(all.Count(t => t.GetComponent<Camera>() != null) == 1 && all.Count(t => t.GetComponent<AudioListener>() != null) == 1,
            "The inspection uses the existing camera and AudioListener.");
        Debug.Log("FINAL_LAB_INVESTIGATION_REOPEN passed.");
    }

    private static void VerifyWalkway()
    {
        var approach = FinalLabInvestigationBuilder.Approaches;
        var nodes = new[] { approach[0], new Vector3(0, 0, 6.58f), new(-1.90f, 0, 7.02f), approach[3],
            new(-3.54f, 0, 7.55f), new(-2.61f, 0, 9.65f), approach[1],
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

    public static void Play()
    {
        Reopen();
        SessionState.SetBool(BatchKey, true);
        EditorApplication.EnterPlaymode();
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(BatchKey, false)) return;
        deadline = EditorApplication.timeSinceStartup + 110;
        checks = RunPlayChecks();
    }

    private static void Tick()
    {
        if (checks == null || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Final Lab tests exceeded 110 seconds.");
            if (!checks.MoveNext()) Finish(null);
        }
        catch (Exception error)
        {
            Finish(error);
        }
    }

    private static IEnumerator RunPlayChecks()
    {
        keyboard = InputSystem.AddDevice<Keyboard>("FinalLabTestKeyboard");
        mouse = InputSystem.AddDevice<Mouse>("FinalLabTestMouse");
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        var player = Object.FindFirstObjectByType<FirstPersonPlayer>();
        var camera = player.GetComponentInChildren<Camera>();
        var controller = player.GetComponent<CharacterController>();
        var manager = Object.FindFirstObjectByType<FinalLabInvestigationManager>();
        var ui = Object.FindFirstObjectByType<FinalLabInvestigationUI>();
        yield return null;
        yield return null;
        Require(manager.State == FinalLabInvestigationManager.ViewState.Walking && manager.Completed == 0 &&
            ui.StatusText == "Осмотрено: 0/4" && ui.ObjectiveText == "Цель: Осмотрите архивную стену",
            "The real Play Mode starts in walking and shows the correct 0/4 objective.");
        var earlyProgress = new InvestigationProgress(4);
        Require(earlyProgress.Complete(2) && earlyProgress.Count == 0 && earlyProgress.IsStudied(2) &&
            !earlyProgress.Complete(2) && earlyProgress.Complete(0) && earlyProgress.Count == 1 &&
            earlyProgress.Complete(1) && earlyProgress.Count == 3, "Later evidence is retained and counted once after the preceding points.");
        foreach (var index in new[] { 2, 0, 0, 1, 2, 3 })
        {
            var point = manager.Points[index];
            int initial = manager.Completed;
            bool preview = index == 2 && initial == 0;
            bool repeat = index == 0 && initial == 1;
            Aim(player, camera, controller, FinalLabInvestigationBuilder.Approaches[index], point.Focus.position);
            for (int i = 0; i < 3; i++) yield return null;
            Require(manager.NearbyPoint() == point, point.Id + " is reachable from its approach.");
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
            if (!preview && !repeat) Capture(camera, point.Id + "-overview");
            LightAndOcclusion(point, manager, camera, !preview && !repeat);
            var selectedItems = preview ? point.Evidence.Where(e => e.Id == "И1") :
                repeat ? point.Evidence.Where(e => e.Id == point.KeyId) : point.Evidence;
            foreach (var evidence in selectedItems)
            {
                Vector3 home = evidence.transform.position;
                Quaternion homeRotation = evidence.transform.rotation;
                var pixel = camera.WorldToScreenPoint(home);
                Require(pixel.z > 0 && manager.DocumentAt(camera.ScreenPointToRay(pixel)) == evidence,
                    point.Id + ": the center of " + evidence.Id + " can be selected through the scene.");
                Pointer(pixel);
                for (int f = 0; f < 3; f++) yield return null;
                Require(evidence.IsHighlighted && point.Evidence.Count(e => e.IsHighlighted) == 1,
                    evidence.Id + " alone gets hover highlighting.");
                Pointer(pixel, true);
                for (int f = 0; f < 3; f++) yield return null;
                Pointer(pixel);
                Require(manager.Selected == evidence, evidence.Id + " selects with left click.");
                while (manager.State == FinalLabInvestigationManager.ViewState.Moving) yield return null;
                Require(manager.State == FinalLabInvestigationManager.ViewState.Detail &&
                    (Vector3.Distance(home, evidence.transform.position) > 0.10f) == evidence.Lifts,
                    evidence.Id + " uses the expected mounted or lifted inspection.");
                Require(manager.Completed == initial, evidence.Id + " cannot advance progress in detail view.");
                if (!preview && !repeat && (evidence.Id == point.KeyId || evidence.Id == "K17_Resonator" ||
                    evidence.Kind == EvidenceKind.Photograph)) Capture(camera, evidence.Id + "-detail");
                float fov = camera.fieldOfView;
                Pointer(pixel, false, 120);
                double end = EditorApplication.timeSinceStartup + 0.20;
                while (EditorApplication.timeSinceStartup < end) yield return null;
                Pointer(pixel);
                Require(camera.fieldOfView < fov - 1, evidence.Id + " zooms with the mouse wheel.");
                if (evidence.Lifts) Keys(Key.Escape);
                else Pointer(pixel, true);
                for (int f = 0; f < 3; f++) yield return null;
                Keys();
                Pointer(pixel);
                while (manager.State == FinalLabInvestigationManager.ViewState.Moving) yield return null;
                Require(manager.State == FinalLabInvestigationManager.ViewState.Overview &&
                    Vector3.Distance(home, evidence.transform.position) < 0.0002f &&
                    Quaternion.Angle(homeRotation, evidence.transform.rotation) < 0.05f && manager.Completed == initial,
                    evidence.Id + " returns to the overview and preserves its exact resting pose.");
            }
            Keys(Key.Escape);
            for (int f = 0; f < 3; f++) yield return null;
            Keys();
            while (manager.State == FinalLabInvestigationManager.ViewState.Moving) yield return null;
            int expected = (preview || repeat) ? initial : index + 1;
            Require(player.enabled && manager.State == FinalLabInvestigationManager.ViewState.Walking &&
                Cursor.lockState == cursor && Vector3.Distance(startingCamera, camera.transform.position) < 0.025f,
                point.Id + " restores movement, camera and cursor state on exit.");
            Require(manager.Completed == expected, point.Id + " exits with " + expected + "/4.");
            double fade = EditorApplication.timeSinceStartup + 0.51;
            while (EditorApplication.timeSinceStartup < fade) yield return null;
            Require(ui.StatusText == "Осмотрено: " + expected + "/4" &&
                manager.Points.Count(p => p.IsHighlighted) == (expected < 4 ? 1 : 0) &&
                (expected < 4 ? manager.Points[expected].IsHighlighted : ui.ObjectiveText == "Исследование завершено"),
                "HUD status and objective outline agree at " + expected + "/4.");
        }
        Require(manager.Completed == 4, "Play Mode completed the full 0/4 to 4/4 sequence.");
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
            File.WriteAllBytes(Path.Combine(Output, title + ".png"), image.EncodeToPNG());
            Object.Destroy(image);
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
        }
    }

    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
        Debug.Log("FINAL_LAB_CHECK " + message);
    }

    private static void Finish(Exception error)
    {
        checks = null;
        SessionState.EraseBool(BatchKey);
        if (error != null) Debug.LogException(error);
        Debug.Log("FINAL_LAB_INVESTIGATION_PLAY_" + (error == null ? "PASSED" : "FAILED"));
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
