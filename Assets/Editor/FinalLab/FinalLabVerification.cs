using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static partial class FinalLabVerification
{
    private const string RunFlag = "FinalLab.RunTour";
    private static bool HasRefinement => GameObject.Find(FinalLabRefinement.LadderMount) != null;
    private static bool HasDressing => GameObject.Find(FinalLabProps.RootName) != null;
    private static string LogFolder => HasDressing ? FinalLabPropsSurvey.Logs :
        HasRefinement ? FinalLabRefinement.LogFolder : "Logs/FinalLab";
    private static readonly List<string> Results = new();
    private static readonly List<string> RuntimeErrors = new();
    private static IEnumerator tour;
    private static Keyboard keyboard;
    private static Mouse mouse;
    private static KeyboardState keys;
    private static Vector2 mouseDelta;
    private static double deadline;
    private static int previousFrame;
    private static bool previousRunInBackground;

    static FinalLabVerification()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.update += Tick;
    }

    public static void RebuildAndValidate()
    {
        FinalLabBuilder.Build();
        ValidateSaved();
    }

    [MenuItem("Tools/Final Lab/Validate Saved Architecture")]
    public static void ValidateSaved()
    {
        var scene = SceneManager.GetActiveScene();
        if (Application.isBatchMode)
            scene = EditorSceneManager.OpenScene(FinalLabBuilder.ScenePath);
        Results.Clear();
        Require(scene.path == FinalLabBuilder.ScenePath && !scene.isDirty, "FinalScene is open and saved.");
        Physics.SyncTransforms();
        var root = GameObject.Find(FinalLabBuilder.RootName);
        Require(root != null && root.transform.childCount == 7, "Five zones, lighting and spawn belong to the scene root.");
        var renderers = root.GetComponentsInChildren<MeshRenderer>().Where(r => r.enabled).ToArray();
        Require(renderers.All(r => r.GetComponent<MeshFilter>()?.sharedMesh != null &&
            r.sharedMaterials.All(m => m != null && m.shader != null && m.shader.name != "Hidden/InternalErrorShader")),
            "Every enabled renderer has a mesh and valid materials.");
        var concrete = renderers.SelectMany(r => r.sharedMaterials)
            .Where(m => m.name.StartsWith("FinalLab_Concrete_", StringComparison.Ordinal)).Distinct().ToArray();
        Require(concrete.Length == 4 && concrete.All(m => m.shader.name == "Universal Render Pipeline/Lit" &&
            m.GetTexture("_BaseMap") != null && m.GetTexture("_MetallicGlossMap") != null &&
            m.IsKeywordEnabled("_NORMALMAP") && m.IsKeywordEnabled("_METALLICSPECGLOSSMAP") &&
            AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(m.GetTexture("_BumpMap"))) is TextureImporter normal &&
            normal.textureType == TextureImporterType.NormalMap), "Four URP concrete materials have albedo, normal and roughness-derived smoothness.");
        Require(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length == 1 &&
            Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length == 1 &&
            Object.FindObjectsByType<FirstPersonPlayer>(FindObjectsSortMode.None).Length == 1,
            "One player controller, camera and audio listener.");

        var stairwell = root.transform.Find("01_Entrance_Stairwell");
        var stairModel = stairwell.Find("Stairs/StairModelFit").GetChild(0).gameObject;
        var hatch = stairwell.Find("Hatch").GetChild(0).gameObject;
        Require(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(stairModel).EndsWith("/old_wood_stairs.glb") &&
            PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(hatch).EndsWith("/old_floor_hatch.glb"),
            "Stair and hatch are linked to their original imported models.");
        var hatchParts = hatch.GetComponentsInChildren<MeshRenderer>();
        var hatchBounds = hatchParts[0].bounds;
        foreach (var part in hatchParts)
            hatchBounds.Encapsulate(part.bounds);
        Require(hatchBounds.size.y < 0.20f && hatchBounds.size.x >= 1.39f && hatchBounds.size.z >= 1.39f,
            "The closed hatch lies horizontally and covers the ceiling opening.");
        var topTread = stairModel.GetComponentsInChildren<MeshRenderer>().Single(r => r.name == "SketchUp.011_material_2_0");
        var threshold = stairwell.Find("Stairs/UpperLanding_WoodThreshold").GetComponent<BoxCollider>().bounds;
        if (Mathf.Abs(topTread.bounds.max.y - FinalLabBuilder.LandingHeight) >= 0.01f ||
            threshold.min.z >= topTread.bounds.min.z || threshold.max.z <= topTread.bounds.min.z)
            throw new InvalidOperationException($"Upper tread {topTread.bounds} misses landing threshold {threshold}.");
        Require(true, "The upper tread meets the landing through the wood threshold.");
        var bottomTread = stairModel.GetComponentsInChildren<MeshRenderer>().Single(r => r.name == "Bottom_Wood_Tread");
        Require(Mathf.Abs(bottomTread.bounds.min.y) < 0.008f && Mathf.Abs(bottomTread.bounds.max.z) < 0.008f,
            "The lower wood tread meets the concrete landing.");
        var bunkerModule = root.transform.Find("02_Archive_Corridor/EntranceOpening/Bunker_Doorway_Module/wall.046")
            .GetComponent<MeshFilter>().sharedMesh;
        string modulePath = AssetDatabase.GetAssetPath(bunkerModule);
        Require(modulePath.EndsWith("/realism_bunker_modular.glb") || modulePath == FinalLabRefinement.RepairedPortalMeshPath,
            "Archive doorway uses the imported solid bunker module geometry.");
        if (HasRefinement)
            FinalLabRefinement.Verify(root.transform, Results);

        VerifyOpenings();
        VerifyEnclosure();
        Directory.CreateDirectory(LogFolder);
        File.WriteAllLines(Path.Combine(LogFolder, "batch-validation.txt"), Results);
        Debug.Log("FINAL_LAB_BATCH_VALIDATION PASSED\n" + string.Join("\n", Results));
    }

    private static void VerifyOpenings()
    {
        RayHits(new Vector3(-0.44f, 1.65f, 4.95f), Vector3.forward, "RearPartitionPier", 17);
        RayHits(new Vector3(-1.52f, 1.65f, 13.1f), Vector3.forward, "ControlBack");
        RayHits(new Vector3(0.92f, 1.65f, 13.1f), Vector3.forward, "ContainmentBack");
        RayHits(new Vector3(-3.46f, 1.65f, 13.1f), Vector3.forward, "ControlBack");
        RayHits(new Vector3(3.13f, 1.65f, 13.1f), Vector3.forward, "ContainmentBack");
        RayHits(new Vector3(-3.46f, 0.65f, 13.1f), Vector3.forward, "Sill");
        RayHits(new Vector3(3.13f, 0.65f, 13.1f), Vector3.forward, "Sill");
        RayHits(new Vector3(0, 2.35f, 5.04f), Vector3.forward, "ArchiveLintel");
        RayHits(FinalLabBuilder.Spawn + Vector3.up * 1.65f, Vector3.up, "Hatch");
    }

    private static void VerifyEnclosure()
    {
        Vector3[] spaces =
        {
            new(0, 2.25f, -4.02f), new(0, 1.45f, 3.35f), new(0.2f, 1.45f, 9.10f),
            new(-3.3f, 1.45f, 15.8f), new(2.8f, 1.45f, 16.0f)
        };
        foreach (Vector3 center in spaces)
        {
            for (int n = 0; n < 96; n++)
            {
                float t = n * 2.39996323f;
                float height = 1 - 2 * (n + 0.5f) / 96;
                float radius = Mathf.Sqrt(1 - height * height);
                Vector3 direction = new(Mathf.Cos(t) * radius, height, Mathf.Sin(t) * radius);
                if (!Physics.Raycast(center, direction, 30, ~0, QueryTriggerInteraction.Ignore))
                    throw new InvalidOperationException($"Open exterior ray from {center} along {direction}.");
            }
        }
        Require(true, "480 rays from the five spaces hit a solid boundary. Door and window sightlines remain open.");
    }

    private static void RayHits(Vector3 origin, Vector3 direction, string expected, float distance = 8)
    {
        if (!Physics.Raycast(origin, direction, out var hit, distance, ~0, QueryTriggerInteraction.Ignore) ||
            !hit.collider.name.Contains(expected, StringComparison.Ordinal))
            throw new InvalidOperationException($"Expected {expected} from {origin}. Hit {hit.collider?.name ?? "nothing"}.");
    }

    public static void RunGraphical()
    {
        if (Application.isBatchMode)
            throw new InvalidOperationException("The graphical tour needs the Unity Editor with a graphics device.");
        EditorSceneManager.OpenScene(FinalLabBuilder.ScenePath, OpenSceneMode.Single);
        EditorApplication.update += StartFocusedTour;
    }

    public static void OpenRefinementSceneView()
    {
        EditorSceneManager.OpenScene(FinalLabBuilder.ScenePath, OpenSceneMode.Single);
        FrameLadder();
    }

    [MenuItem("Tools/Final Lab/Frame Hatch and Ladder")]
    public static void FrameLadder()
    {
        FrameScene(new Vector3(0.33f, 2.82f, -3.94f), new Vector3(-0.60f, 2.48f, -2.83f), 1.08f);
    }

    [MenuItem("Tools/Final Lab/Frame Archive Threshold")]
    public static void FrameArchiveThreshold()
    {
        FrameScene(new Vector3(0, 0.56f, 5.52f), new Vector3(0.48f, 0.85f, 4.20f), 1.05f);
    }

    private static void FrameScene(Vector3 pivot, Vector3 from, float size)
    {
        var view = SceneView.lastActiveSceneView ?? EditorWindow.GetWindow<SceneView>();
        view.Show();
        view.orthographic = false;
        view.LookAtDirect(pivot, Quaternion.LookRotation(pivot - from), size);
        view.sceneLighting = true;
        view.drawGizmos = false;
        view.Focus();
        view.Repaint();
    }

    private static void StartFocusedTour()
    {
        if (!UnityEditorInternal.InternalEditorUtility.isApplicationActive)
            return;
        EditorApplication.update -= StartFocusedTour;
        StartTour();
    }

    [MenuItem("Tools/Final Lab/Run Play Mode Tour and Capture")]
    public static void StartTour()
    {
        var scene = SceneManager.GetActiveScene();
        if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode ||
            scene.path != FinalLabBuilder.ScenePath || scene.isDirty)
            throw new InvalidOperationException("Open the saved FinalScene in the graphical Editor before running the tour.");
        var gameView = EditorWindow.GetWindow(typeof(EditorApplication).Assembly.GetType("UnityEditor.GameView"));
        gameView.Show();
        gameView.Focus();
        SessionState.SetBool(RunFlag, true);
        EditorApplication.EnterPlaymode();
    }

    private static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.ExitingPlayMode && tour != null)
            Finish("Play Mode ended before the route completed.");
        if (change != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(RunFlag, false))
            return;

        Results.Clear();
        RuntimeErrors.Clear();
        previousFrame = -1;
        deadline = EditorApplication.timeSinceStartup + 95;
        previousRunInBackground = Application.runInBackground;
        Application.runInBackground = true;
        keyboard = InputSystem.AddDevice<Keyboard>("Final Lab verification keyboard");
        mouse = InputSystem.AddDevice<Mouse>("Final Lab verification mouse");
        keys = new KeyboardState();
        mouseDelta = Vector2.zero;
        InputSystem.onAfterUpdate += ApplyInput;
        Application.logMessageReceived += RecordError;
        tour = Tour();
    }

    private static void ApplyInput()
    {
        InputState.Change(keyboard, keys);
        keyboard.MakeCurrent();
        InputState.Change(mouse, new MouseState { delta = mouseDelta });
        mouse.MakeCurrent();
        mouseDelta = Vector2.zero;
    }

    private static void RecordError(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Assert || type == LogType.Exception)
            RuntimeErrors.Add(message + "\n" + stack);
    }

    private static void Tick()
    {
        if (tour == null || !EditorApplication.isPlaying || previousFrame == Time.frameCount)
            return;
        previousFrame = Time.frameCount;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline)
                throw new TimeoutException("The FinalScene tour timed out.");
            if (!tour.MoveNext())
                Finish(null);
        }
        catch (Exception error)
        {
            Finish(error.ToString());
        }
    }

    private static IEnumerator Tour()
    {
        for (int frame = 0; frame < 8; frame++) yield return null;
        var player = Object.FindObjectsByType<FirstPersonPlayer>(FindObjectsSortMode.None).Single();
        var camera = player.GetComponentInChildren<Camera>();
        var motor = player.GetComponent<CharacterController>();
        Require(Vector3.Distance(player.transform.position, FinalLabBuilder.Spawn) < 0.09f && motor.isGrounded,
            "Play starts grounded on the upper landing.");
        Require(camera.transform.forward.y < -0.2f && camera.transform.forward.z > 0.9f &&
            Mathf.Abs(camera.transform.position.y - player.transform.position.y - 1.65f) < 0.03f,
            "Initial eye height and view aim down the staircase.");
        mouseDelta = new Vector2(0, 790);
        for (int frame = 0; frame < 4; frame++) yield return null;
        if (camera.transform.forward.y <= 0.96f ||
            !Physics.Raycast(camera.transform.position, camera.transform.forward, out var hatch, 1.5f) ||
            hatch.collider.name != "Hatch")
            throw new InvalidOperationException($"Mouse look did not face the hatch. Direction: {camera.transform.forward:F3}. Cursor: {Cursor.lockState}.");
        Require(true, "Mouse look finds the closed hatch overhead.");
        foreach (var frame in Capture(camera, "02-hatch", new Vector3(0, 2.90f, -2.55f), new Vector3(0, 3.94f, -3.96f))) yield return frame;
        if (HasRefinement)
        {
            foreach (var frame in Capture(camera, "09-ladder-from-below", new Vector3(-0.62f, 2.08f, -2.61f),
                new Vector3(0.47f, 2.74f, -3.97f), fieldOfView: 73)) yield return frame;
            foreach (var frame in Capture(camera, "10-ladder-side", new Vector3(-0.74f, 2.87f, -4.57f),
                new Vector3(0.54f, 2.77f, -3.96f), fieldOfView: 84)) yield return frame;
            foreach (var frame in CaptureThroughHatch(camera)) yield return frame;
        }
        mouseDelta = new Vector2(0, -730);
        yield return null;
        foreach (var frame in Capture(camera, "01-stairwell", camera.transform.position, new Vector3(0, 0.20f, -0.08f))) yield return frame;

        foreach (var frame in Walk(player, 0, 0.65f, "lower landing")) yield return frame;
        Require(motor.isGrounded && Mathf.Abs(player.transform.position.y) < 0.09f, "Walked down the stairs onto the floor.");
        foreach (var frame in Walk(player, 0, 3.30f, "archive corridor")) yield return frame;
        foreach (var frame in Capture(camera, "03-archive-corridor", new Vector3(0.84f, 1.64f, 1.41f), new Vector3(-0.6f, 1.37f, 4.62f))) yield return frame;
        if (HasRefinement)
        {
            float plateZ = GameObject.Find(FinalLabRefinement.ThresholdName).transform.position.z;
            foreach (var frame in Walk(player, 0, 4.97f, "archive side of the plate")) yield return frame;
            foreach (var frame in LookAtPlate(player, camera, Vector3.forward)) yield return frame;
            foreach (var frame in Walk(player, 0, 6.30f, "first crossing into the hall")) yield return frame;
            foreach (var frame in LookAtPlate(player, camera, Vector3.back)) yield return frame;
            foreach (var frame in Capture(camera, "12-threshold-from-hall", new Vector3(0.12f, 0.25f, plateZ + 0.72f),
                new Vector3(0, 0.035f, plateZ))) yield return frame;
            foreach (var frame in Walk(player, 0, 4.87f, "first crossing back to the archive")) yield return frame;
            foreach (var frame in LookPitch(camera, 8)) yield return frame;
            foreach (var frame in Capture(camera, "13-threshold-close", new Vector3(0.48f, 0.56f, plateZ - 0.75f),
                new Vector3(0, 0.036f, plateZ))) yield return frame;
            foreach (var frame in Capture(camera, "14-doorway-after", new Vector3(0.21f, 1.42f, 4.07f),
                new Vector3(0, 1.03f, 5.61f))) yield return frame;
            foreach (var frame in Capture(camera, "15-threshold-from-above", new Vector3(0, 1.68f, plateZ - 0.23f),
                new Vector3(0, 0.012f, plateZ))) yield return frame;
        }
        foreach (var frame in Walk(player, 0, 6.57f, "hall entry")) yield return frame;
        if (HasDressing)
        {
            foreach (var frame in DecoratedTour(player, camera, motor)) yield return frame;
            yield break;
        }
        foreach (var frame in Walk(player, 0, 10.62f, "laboratory hall")) yield return frame;
        foreach (var frame in Capture(camera, "04-main-hall", new Vector3(0.01f, 1.66f, 6.19f), new Vector3(-0.02f, 1.32f, 13.5f))) yield return frame;
        VerifyOpenings();

        foreach (var frame in Walk(player, -3.46f, 12.84f, "control window")) yield return frame;
        player.transform.rotation = Quaternion.identity;
        keys = new KeyboardState(Key.W);
        double barrierUntil = EditorApplication.timeSinceStartup + 0.6;
        while (EditorApplication.timeSinceStartup < barrierUntil) yield return null;
        keys = new KeyboardState();
        Require(player.transform.position.z < 13.78f, "The control window sill prevents the player walking through.");
        foreach (var frame in Walk(player, -1.52f, 12.7f, "control door approach")) yield return frame;
        foreach (var frame in Walk(player, -1.52f, 15.32f, "Ivan's control room")) yield return frame;
        foreach (var frame in Capture(camera, "05-control-room", new Vector3(-4.35f, 1.64f, 16.94f), new Vector3(-2.09f, 1.31f, 13.9f))) yield return frame;
        foreach (var frame in Walk(player, -1.52f, 12.69f, "return to the hall")) yield return frame;
        foreach (var frame in Walk(player, 0.92f, 12.71f, "containment door approach")) yield return frame;
        foreach (var frame in Walk(player, 0.92f, 16.0f, "father's containment room")) yield return frame;
        foreach (var frame in Capture(camera, "06-containment", new Vector3(3.83f, 1.64f, 17.56f), new Vector3(1.62f, 1.27f, 13.93f))) yield return frame;
        foreach (var frame in Walk(player, 0.92f, 12.63f, "containment exit")) yield return frame;
        foreach (var frame in Walk(player, 0, 8.22f, "return across the hall")) yield return frame;
        foreach (var frame in Walk(player, 0, 4.80f, "return to the corridor")) yield return frame;
        foreach (var frame in Walk(player, 0, 0.60f, "return to the stairs")) yield return frame;
        if (HasRefinement)
            foreach (var frame in Capture(camera, "16-ladder-from-lower-landing", new Vector3(-0.55f, 1.66f, 0.38f),
                new Vector3(0.34f, 2.72f, -3.93f))) yield return frame;
        foreach (var frame in Walk(player, 0, -4.03f, "return up the stairs")) yield return frame;
        Require(motor.isGrounded && Mathf.Abs(player.transform.position.y - FinalLabBuilder.LandingHeight) < 0.09f,
            "Walked back upstairs to the upper landing.");
        foreach (var frame in CapturePlan(camera)) yield return frame;
    }

    private static IEnumerable CaptureThroughHatch(Camera camera)
    {
        var hatch = GameObject.Find(FinalLabRefinement.LadderMount).transform.parent.Find("Hatch");
        var cover = hatch.GetComponentsInChildren<MeshRenderer>();
        foreach (var part in cover)
            part.forceRenderingOff = true;
        try
        {
            foreach (var frame in Capture(camera, "11-hatch-opening-inspection", new Vector3(-0.29f, 4.87f, -3.47f),
                new Vector3(0.54f, 2.78f, -4.01f))) yield return frame;
        }
        finally
        {
            foreach (var part in cover)
                part.forceRenderingOff = false;
        }
    }

    private static IEnumerable LookAtPlate(FirstPersonPlayer player, Camera camera, Vector3 facing)
    {
        var plate = GameObject.Find(FinalLabRefinement.ThresholdName).GetComponent<BoxCollider>();
        Vector3 target = plate.bounds.center;
        target.y = plate.bounds.max.y - 0.001f;
        Vector3 offset = target - camera.transform.position;
        var heading = Vector3.ProjectOnPlane(offset, Vector3.up);
        Require(Vector3.Dot(heading.normalized, facing) > 0.97f, "The player has reached the expected side of the threshold.");
        player.transform.rotation = Quaternion.LookRotation(heading);
        float horizontal = Vector3.ProjectOnPlane(offset, Vector3.up).magnitude;
        float pitch = Mathf.Atan2(-offset.y, horizontal) * Mathf.Rad2Deg;
        foreach (var frame in LookPitch(camera, pitch)) yield return frame;
        var hit = Physics.RaycastAll(camera.transform.position, camera.transform.forward, 3)
            .Where(item => !item.collider.transform.IsChildOf(player.transform))
            .OrderBy(item => item.distance).FirstOrDefault();
        if (hit.collider != plate)
            throw new InvalidOperationException($"Plate sightline at {camera.transform.position:F3}, " +
                $"direction {camera.transform.forward:F3}; hit {hit.collider?.name ?? "nothing"} (cursor {Cursor.lockState}).");
        Require(true, "The FPS camera can look directly at the plate before crossing.");
    }

    private static IEnumerable LookPitch(Camera camera, float pitch)
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            float current = Mathf.DeltaAngle(0, camera.transform.localEulerAngles.x);
            if (Mathf.Abs(current - pitch) < 0.40f)
                yield break;
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                yield return null;
            }
            mouseDelta = new Vector2(0, Mathf.Clamp((current - pitch) / 0.12f, -260, 260));
            for (int frame = 0; frame < 3; frame++) yield return null;
        }
    }

    private static IEnumerable Walk(FirstPersonPlayer player, float x, float z, string destination)
    {
        double until = EditorApplication.timeSinceStartup + 8;
        Vector2 target = new(x, z);
        while (true)
        {
            Vector3 current = player.transform.position;
            Vector2 delta = target - new Vector2(current.x, current.z);
            if (delta.magnitude < 0.115f)
                break;
            if (EditorApplication.timeSinceStartup > until || current.y < -0.35f)
                throw new InvalidOperationException($"Could not walk to {destination}. Player stopped at {current:F3}.");
            player.transform.rotation = Quaternion.LookRotation(new Vector3(delta.x, 0, delta.y));
            keys = new KeyboardState(Key.W);
            yield return null;
        }
        keys = new KeyboardState();
        for (int frame = 0; frame < 8; frame++) yield return null;
        Require(true, $"Reached {destination} at {player.transform.position:F3}.");
    }

    private static IEnumerable CapturePlan(Camera camera)
    {
        var root = GameObject.Find(FinalLabBuilder.RootName).transform;
        string[] roofs =
        {
            "01_Entrance_Stairwell/StairwellCeiling", "02_Archive_Corridor/ArchiveCeiling",
            "03_Main_Laboratory/Ceiling", "04_Ivan_ControlRoom/ControlCeiling",
            "05_Father_Containment/ContainmentCeiling"
        };
        var cover = roofs.SelectMany(path => root.Find(path).GetComponentsInChildren<MeshRenderer>())
            .Concat(root.Find("01_Entrance_Stairwell/Hatch").GetComponentsInChildren<MeshRenderer>())
            .Concat(root.Find("06_Lighting").GetComponentsInChildren<MeshRenderer>()).ToArray();
        foreach (var part in cover)
            part.forceRenderingOff = true;
        var fill = new GameObject("Temporary overhead inspection light");
        try
        {
            fill.transform.rotation = Quaternion.Euler(70, -28, 0);
            var light = fill.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.shadows = LightShadows.None;
            foreach (var frame in Capture(camera, "07-overhead", new Vector3(0, 25, 6.8f), new Vector3(0, 0, 6.8f), true))
                yield return frame;
            foreach (var part in cover)
                part.forceRenderingOff = false;
            foreach (var frame in Capture(camera, "08-exterior", new Vector3(10.8f, 13.5f, -7.6f), new Vector3(0, 1.2f, 7.2f)))
                yield return frame;
        }
        finally
        {
            Object.DestroyImmediate(fill);
            foreach (var part in cover)
                part.forceRenderingOff = false;
        }
    }

    private static IEnumerable Capture(Camera playerCamera, string name, Vector3 position, Vector3 focus, bool plan = false,
        float fieldOfView = 63)
    {
        int width = plan ? 1120 : 1440;
        int height = plan ? 1440 : 900;
        var buffer = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var view = new GameObject("Temporary inspection camera");
        var camera = view.AddComponent<Camera>();
        var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            camera.CopyFrom(playerCamera);
            camera.targetTexture = buffer;
            camera.fieldOfView = fieldOfView;
            camera.orthographic = plan;
            camera.orthographicSize = 12.65f;
            camera.aspect = (float)width / height;
            view.transform.SetPositionAndRotation(position, plan ? Quaternion.Euler(90, 0, 0) : Quaternion.LookRotation(focus - position));
            camera.GetUniversalAdditionalCameraData().antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            for (int frame = 0; frame < 4; frame++) yield return null;
            var previousBuffer = RenderTexture.active;
            try
            {
                RenderTexture.active = buffer;
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
            }
            finally
            {
                RenderTexture.active = previousBuffer;
            }
            Directory.CreateDirectory(LogFolder);
            File.WriteAllBytes(Path.Combine(LogFolder, name + ".png"), pixels.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = null;
            Object.DestroyImmediate(view);
            RenderTexture.ReleaseTemporary(buffer);
            Object.DestroyImmediate(pixels);
        }
    }

    private static void Require(bool condition, string result)
    {
        if (!condition)
            throw new InvalidOperationException(result);
        Results.Add(result);
    }

    private static void Finish(string failure)
    {
        (tour as IDisposable)?.Dispose();
        tour = null;
        SessionState.SetBool(RunFlag, false);
        InputSystem.onAfterUpdate -= ApplyInput;
        Application.logMessageReceived -= RecordError;
        InputSystem.RemoveDevice(keyboard);
        InputSystem.RemoveDevice(mouse);
        Application.runInBackground = previousRunInBackground;
        if (RuntimeErrors.Count > 0)
            failure = string.Join("\n", new[] { failure }.Concat(RuntimeErrors));
        string status = failure == null ? "FINAL_LAB_GRAPHICAL_PLAY PASSED" : "FINAL_LAB_GRAPHICAL_PLAY FAILED";
        Directory.CreateDirectory(LogFolder);
        File.WriteAllLines(Path.Combine(LogFolder, "graphical-play-validation.txt"),
            new[] { status }.Concat(Results).Concat(failure == null ? Array.Empty<string>() : new[] { failure }));
        if (failure == null)
            Debug.Log(status);
        else
            Debug.LogError(status + "\n" + failure);
        EditorApplication.ExitPlaymode();
    }
}
