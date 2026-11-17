using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Explicit batch test only. Temporary authored scenes are deleted after leaving Play Mode.
[InitializeOnLoad]
public static class LocationTransitionValidation
{
    private const string Running = "LocationTransitionValidation.Running";
    private const string Folder = "Assets/__LocationTransitionValidation";
    private const string A = Folder + "/AuthoredA.unity";
    private const string B = Folder + "/AuthoredB.unity";
    private const string Empty = Folder + "/Empty.unity";
    private const string Disabled = Folder + "/Disabled.unity";
    private const string Output = "Logs/LocationTransition";
    private static readonly List<string> results = new List<string>();
    private static readonly List<string> errors = new List<string>();
    private static IEnumerator routine;
    private static Func<bool> pending;
    private static int lastFrame = -1;
    private static double deadline;
    private static int sceneLoads;
    private static int expectedErrors;
    private static bool failed;

    [Serializable] private sealed class BuildScenes { public string[] paths; public bool[] enabled; }

    static LocationTransitionValidation()
    {
        EditorApplication.playModeStateChanged += Changed;
        EditorApplication.update += Tick;
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run in a separate batch Unity process.");
        if (AssetDatabase.IsValidFolder(Folder)) throw new InvalidOperationException("Test folder already exists; inspect it first.");
        Directory.CreateDirectory(Output);
        var original = EditorBuildSettings.scenes;
        SessionState.SetString(Running + ".BuildScenes", JsonUtility.ToJson(new BuildScenes
        { paths = original.Select(s => s.path).ToArray(), enabled = original.Select(s => s.enabled).ToArray() }));
        File.Copy("ProjectSettings/EditorBuildSettings.asset", Output + "/EditorBuildSettings.before.asset", true);
        AssetDatabase.CreateFolder("Assets", "__LocationTransitionValidation");
        try
        {
            ValidateAssets();
            File.WriteAllLines(Output + "/asset-validation.txt", results);
            MakeScene(A, true, Vector3.up * 0.05f, 0f, B);
            MakeScene(B, true, new Vector3(5f, 0.05f, 0f), 90f, null);
            MakeScene(Empty, false, Vector3.zero, 0f, null);
            MakeScene(Disabled, false, Vector3.zero, 0f, null);
            EditorBuildSettings.scenes = original.Concat(new[] { new EditorBuildSettingsScene(A, true),
                new EditorBuildSettingsScene(B, true), new EditorBuildSettingsScene(Empty, true),
                new EditorBuildSettingsScene(Disabled, false) }).ToArray();
            EditorSceneManager.OpenScene(A);
            SessionState.SetBool(Running, true);
            EditorApplication.EnterPlaymode();
        }
        catch { Cleanup(); throw; }
    }

    private static void ValidateAssets()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Level/Prefabs" }))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            foreach (var t in prefab.GetComponentsInChildren<Transform>(true))
                Check(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0,
                    "Prefab scripts: " + prefab.name + "/" + t.name);
        }
        foreach (var name in new[] { "Metro", "Home" })
        {
            var scene = EditorSceneManager.OpenScene("Assets/Level/Scenes/Locations/" + name + ".unity");
            var objects = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            Check(objects.All(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0), name + " has no Missing Script");
            Check(!objects.Any(t => t.GetComponent<FirstPersonPlayer>() != null), name + " has no inserted player");
            var zone = objects.Select(t => t.GetComponent<LocationTransitionZone>()).Single(z => z != null);
            Check(zone.GetComponent<BoxCollider>().isTrigger && zone.GetComponent<Rigidbody>().isKinematic &&
                !zone.GetComponent<Rigidbody>().useGravity && zone.GetComponent<Renderer>() == null, name + " has an invisible static trigger");
            Check(LocationTransition.TryGetBuildIndex(zone.TargetScenePath, out _), name + " destination is in current build settings");
        }
    }

    private static void MakeScene(string path, bool withPlayer, Vector3 position, float yaw, string target)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.transform.position = Vector3.down * 0.5f;
        floor.transform.localScale = new Vector3(50f, 1f, 50f);
        new GameObject("Test location services").AddComponent<MergedGameBootstrap>().locationLabel = "Validation";
        if (withPlayer)
        {
            var player = (GameObject)PrefabUtility.InstantiatePrefab(Resources.Load<GameObject>("Player"));
            player.name = Path.GetFileNameWithoutExtension(path) + " Player";
            player.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            var extraCollider = new GameObject("Second player collider");
            extraCollider.transform.SetParent(player.transform, false);
            extraCollider.AddComponent<SphereCollider>().radius = 0.1f;
        }
        if (target != null)
        {
            var zone = new GameObject("Test zone").AddComponent<LocationTransitionZone>();
            zone.GetComponent<BoxCollider>().isTrigger = true;
            zone.GetComponent<BoxCollider>().size = new Vector3(1f, 3f, 2f);
            zone.GetComponent<Rigidbody>().isKinematic = true;
            zone.GetComponent<Rigidbody>().useGravity = false;
            zone.transform.position = new Vector3(0f, 1f, 4f);
            Set(zone, "targetScenePath", target);
            Set(zone, "fadeOutDuration", 0.15f);
            Set(zone, "fadeInDuration", 0.15f);
        }
        EditorSceneManager.SaveScene(scene, path);
    }

    private static void Changed(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Running, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.AddDevice<Keyboard>("LocationTestKeyboard");
            InputSystem.AddDevice<Mouse>("LocationTestMouse");
            Application.logMessageReceived += Log;
            SceneManager.sceneLoaded += (_, __) => sceneLoads++;
            deadline = EditorApplication.timeSinceStartup + 150;
            routine = Checks();
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Running, false);
            Cleanup();
            EditorApplication.Exit(SessionState.GetBool(Running + ".Failed", false) ? 1 : 0);
        }
    }

    private static void Tick()
    {
        if (routine == null || !EditorApplication.isPlaying || lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Location checks timed out");
            if (pending != null && !pending()) return;
            pending = null;
            if (!routine.MoveNext()) { Finish(null); return; }
            pending = routine.Current as Func<bool>;
        }
        catch (Exception e) { Finish(e.ToString()); }
    }

    private static void Log(string message, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        if (message.StartsWith("Location transition")) { expectedErrors++; return; }
        if (!stack.Contains("UnityEditor.Search.")) errors.Add(message + "\n" + stack);
    }

    private static IEnumerator Checks()
    {
        yield return Delay(0.5f);
        var player = Object.FindFirstObjectByType<FirstPersonPlayer>();
        Check(player != null && Players() == 1, "Play with authored prefab has exactly one player");
        Check(Mathf.Abs(player.transform.position.x) < 0.01f && Mathf.Abs(player.transform.position.z) < 0.01f,
            "Bootstrap preserves authored position");
        Check(player.GetComponentInChildren<Camera>().enabled && player.GetComponent<PistolController>().WeaponView.Model != null,
            "Authored camera and weapon initialize");
        Check(GameObject.Find("Location Exit") == null && GameObject.Find("Spawn Safety Collider") == null,
            "Old portal and spawn fallback are absent");
        Check(!LocationTransition.TryGetBuildIndex(Disabled, out _) &&
            !LocationTransition.TryBegin(player, Disabled, 0.1f, 0.1f) && player.enabled,
            "An existing scene disabled in the build list is rejected without locking input");

        var zone = Object.FindFirstObjectByType<LocationTransitionZone>();
        var npc = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Call(zone, "OnTriggerEnter", npc.GetComponent<Collider>());
        Check(!LocationTransition.IsTransitioning, "Non-player collider cannot start transition");
        Object.Destroy(npc);
        Object.Instantiate(zone.gameObject).name = "Second overlapping transition zone";
        int before = sceneLoads;
        player.Teleport(new Vector3(0f, 0.05f, 4f));
        Physics.SyncTransforms();
        yield return (Func<bool>)(() => LocationTransition.IsTransitioning);
        Check(!player.enabled && !player.GetComponent<SceneSwitch>().enabled && !player.GetComponent<PistolController>().CanUseWeapon,
            "Trigger suspends movement, mind input and weapon");
        Check(!player.GetComponent<DocumentReader>().enabled && !player.GetComponent<ItemInspector>().enabled &&
            !player.GetComponent<DialogueController>().enabled && !player.GetComponent<SmokingController>().enabled,
            "Interactions are suspended");
        Check(!LocationTransition.TryBegin(player, Empty, 0f, 0f), "Repeated request cannot start another load");
        var curtain = Object.FindFirstObjectByType<LocationTransition>().GetComponentInChildren<CanvasGroup>();
        var canvas = curtain.GetComponent<Canvas>();
        Check(canvas.renderMode == RenderMode.ScreenSpaceOverlay && canvas.sortingOrder == short.MaxValue && curtain.blocksRaycasts,
            "Curtain overlays game/UI and intercepts UI input");
        yield return (Func<bool>)(() => SceneManager.GetActiveScene().path == B);
        Check(curtain.alpha == 1f, "Curtain remains fully black at destination activation");
        var next = Object.FindFirstObjectByType<FirstPersonPlayer>();
        Check(player == null && next != null && Players() == 1 && next.gameObject.scene.path == B,
            "Old player is destroyed and only the destination-authored player is used");
        Check(Mathf.Abs(next.transform.position.x - 5f) < 0.01f && Mathf.Abs(next.transform.eulerAngles.y - 90f) < 0.01f,
            "Destination authored position and yaw are preserved");
        Check(!next.enabled && !next.GetComponent<DocumentReader>().enabled, "Destination controls stay blocked while fading in");
        yield return (Func<bool>)(() => !LocationTransition.IsTransitioning);
        Check(sceneLoads == before + 1 && next.enabled && next.GetComponent<SceneSwitch>().enabled,
            "Multiple player colliders produce one load and restore controls");

        before = expectedErrors;
        Check(!LocationTransition.TryBegin(next, "Assets/Missing.unity", 0.1f, 0.1f), "Missing scene is rejected");
        Check(expectedErrors == before + 1 && next.enabled && !LocationTransition.IsTransitioning,
            "Missing scene reports one clear error without black screen or input lock");
        Check(LocationTransition.TryBegin(next, A, 0.1f, 0.1f), "Recovery test starts");
        Set(Object.FindFirstObjectByType<LocationTransition>(), "destination", "Assets/Missing.unity");
        yield return (Func<bool>)(() => !LocationTransition.IsTransitioning);
        Check(expectedErrors == before + 2 && next.enabled && SceneManager.GetActiveScene().path == B,
            "Failure after fade restores image and control in the original scene");

        // Use zero timescale to verify the curtain does not depend on gameplay time.
        Time.timeScale = 0f;
        Check(LocationTransition.TryBegin(next, Empty, 0f, 0.05f), "Transition accepts zero fade-out and paused gameplay");
        yield return (Func<bool>)(() => !LocationTransition.IsTransitioning);
        Time.timeScale = 1f;
        Check(SceneManager.GetActiveScene().path == Empty && Players() == 0, "An empty destination does not create a player");
        yield return Delay(0.2f);
        Check(Players() == 0, "No delayed fallback creates a player");
        foreach (int index in new[] { 0, 1 })
        {
            SceneManager.LoadScene(index);
            yield return Delay(0.3f);
            Check(Players() == 0 && !LocationTransition.IsTransitioning, "Production location remains empty: " + SceneManager.GetActiveScene().name);
        }

        SceneManager.LoadScene(A);
        yield return Delay(0.3f);
        player = Object.FindFirstObjectByType<FirstPersonPlayer>();
        var camera = player.GetComponentInChildren<Camera>();
        var focus = GameObject.CreatePrimitive(PrimitiveType.Cube);
        focus.transform.position = camera.transform.position + camera.transform.forward;
        focus.transform.localScale = Vector3.one * 0.2f;
        focus.AddComponent<FocusPoint>().sceneIndex = 2;
        var mind = player.GetComponent<SceneSwitch>();
        Set(mind, "moveDuration", 0.4f);
        Physics.SyncTransforms();
        Call(mind, "FindNPC", camera);
        Check((FocusPoint)Get(mind, "currentPoint") == focus.GetComponent<FocusPoint>(), "Unchanged mind raycast finds FocusPoint");
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.T));
        yield return Delay(0.03f);
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
        Check(!player.enabled && !player.GetComponent<DocumentReader>().enabled, "Mind entry retains its original control locks");
        Call(Object.FindFirstObjectByType<LocationTransitionZone>(), "OnTriggerEnter", player.GetComponent<Collider>());
        Check(!LocationTransition.IsTransitioning, "Location trigger cannot interrupt mind entry");
        yield return (Func<bool>)(() => SceneManager.GetActiveScene().buildIndex == 2);
        Check(Object.FindFirstObjectByType<SceneSwitch>().returnOnly, "Original mind scene and return component load");
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.Escape));
        yield return Delay(0.03f);
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
        yield return (Func<bool>)(() => SceneManager.GetActiveScene().path == A);
        yield return Delay(0.2f);
        Check(Players() == 1 && Object.FindFirstObjectByType<FirstPersonPlayer>().enabled,
            "Escape returns to the original authored scene with one functional player");
        Check(errors.Count == 0, "No unexpected runtime errors");
    }

    private static int Players() => Object.FindObjectsByType<FirstPersonPlayer>(FindObjectsSortMode.None).Length;
    private static Func<bool> Delay(float seconds) { double until = EditorApplication.timeSinceStartup + seconds; return () => EditorApplication.timeSinceStartup >= until; }
    private static object Get(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(target, value);
    private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        results.Add("PASS: " + message);
    }
    private static void Finish(string failure)
    {
        routine = null;
        failed = failure != null;
        SessionState.SetBool(Running + ".Failed", failed);
        var report = new List<string> { failed ? "LOCATION_VALIDATION_FAIL" : "LOCATION_VALIDATION_PASS" };
        report.AddRange(results);
        if (failure != null) report.Add(failure);
        report.AddRange(errors);
        report.Add("Expected location errors: " + expectedErrors + "; unexpected runtime errors: " + errors.Count);
        File.WriteAllLines(Output + "/validation.txt", report);
        EditorApplication.isPlaying = false;
    }
    private static void Cleanup()
    {
        var original = JsonUtility.FromJson<BuildScenes>(SessionState.GetString(Running + ".BuildScenes", ""));
        if (original != null)
            EditorBuildSettings.scenes = original.paths.Select((p, i) => new EditorBuildSettingsScene(p, original.enabled[i])).ToArray();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        AssetDatabase.DeleteAsset(Folder);
        File.Copy(Output + "/EditorBuildSettings.before.asset", "ProjectSettings/EditorBuildSettings.asset", true);
    }
}
