using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Run a separate editor with -playerPrefabValidation -executeMethod PlayerPrefabPlayValidation.RunBatch.
// Omit -batchmode and -quit: native cursor lock requires a regular editor with a Game View.
// All interaction fixtures are created only in memory by this test, never by the player.
[InitializeOnLoad]
public static class PlayerPrefabPlayValidation
{
    private const string Running = "PlayerPrefabPlayValidation.Running";
    private static readonly List<string> Results = new List<string>();
    private static readonly List<string> Errors = new List<string>();
    private static readonly List<string> EditorDiagnostics = new List<string>();
    private static Keyboard testKeyboard;
    private static Mouse testMouse;
    private static IEnumerator routine;
    private static Func<bool> pending;
    private static double deadline;
    private static int lastFrame = -1;
    private static readonly MethodInfo AllowCursor = typeof(EditorApplication).Assembly.GetTypes()
        .SelectMany(t => t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
        .FirstOrDefault(m => m.Name == "SetAllowCursorLock");

    static PlayerPrefabPlayValidation()
    {
        EditorApplication.playModeStateChanged += Changed;
        EditorApplication.update += Tick;
        Application.logMessageReceived += (message, stack, type) =>
        {
            if (!SessionState.GetBool(Running, false) || (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)) return;
            // A batch project has no Search UI; its editor-only startup indexer can report an empty database.
            if (stack.Contains("UnityEditor.Search.")) EditorDiagnostics.Add(message + "\n" + stack);
            else Errors.Add(message);
        };
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode && !Environment.GetCommandLineArgs().Contains("-playerPrefabValidation"))
            throw new InvalidOperationException("Use a separate validation project with -playerPrefabValidation.");
        AssetLayoutValidation.Validate();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Validation floor";
        floor.transform.position = new Vector3(0f, -0.5f, 0f);
        floor.transform.localScale = new Vector3(30f, 1f, 30f);
        var player = (GameObject)PrefabUtility.InstantiatePrefab(Resources.Load<GameObject>("Player"));
        player.transform.position = Vector3.up * 0.05f;
        // Cursor lock requires a Game View, even when virtual device input is explicitly routed there.
        var gameView = EditorWindow.GetWindow(typeof(EditorApplication).Assembly.GetType("UnityEditor.GameView"));
        gameView.Show();
        gameView.Focus();
        SessionState.SetBool(Running, true);
        EditorApplication.EnterPlaymode();
    }

    private static void Changed(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        var loop = UnityEngine.LowLevel.PlayerLoop.GetCurrentPlayerLoop();
        for (int i = 0; i < loop.subSystemList.Length; i++)
        {
            if (loop.subSystemList[i].type != typeof(UnityEngine.PlayerLoop.Update)) continue;
            var steps = loop.subSystemList[i].subSystemList.ToList();
            steps.Insert(0, new UnityEngine.LowLevel.PlayerLoopSystem { type = typeof(PlayerPrefabPlayValidation), updateDelegate = SupplyFocus });
            loop.subSystemList[i].subSystemList = steps.ToArray();
        }
        UnityEngine.LowLevel.PlayerLoop.SetPlayerLoop(loop);
        deadline = EditorApplication.timeSinceStartup + 240;
        routine = Checks();
    }

    private static void SupplyFocus()
    {
        testKeyboard?.MakeCurrent();
        testMouse?.MakeCurrent();
        if (AllowCursor != null)
        {
            // Unity 6.3 adds a flags enum identifying the editor's reasons for suppressing lock.
            var args = AllowCursor.GetParameters().Select(p => p.ParameterType == typeof(bool) ? (object)true :
                Enum.ToObject(p.ParameterType, Enum.GetValues(p.ParameterType).Cast<object>().Aggregate(0L, (mask, value) => mask | Convert.ToInt64(value)))).ToArray();
            AllowCursor.Invoke(null, args);
        }
        var movement = Object.FindFirstObjectByType<FirstPersonPlayer>();
        if (movement != null && movement.enabled) Cursor.lockState = CursorLockMode.Locked;
    }

    private static void Tick()
    {
        if (routine == null) return;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Player play checks timed out");
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            SupplyFocus();
            if (pending != null && !pending()) return;
            pending = null;
            if (!routine.MoveNext()) { Finish(null); return; }
            pending = routine.Current as Func<bool>;
        }
        catch (Exception error) { Finish(error.ToString()); }
    }

    private static void Finish(string failure)
    {
        routine = null;
        SessionState.SetBool(Running, false);
        var lines = new List<string> { failure == null ? "PLAYER_PLAY_VALIDATION_PASS" : "PLAYER_PLAY_VALIDATION_FAIL" };
        lines.AddRange(Results);
        if (failure != null) lines.Add(failure);
        lines.Add("Runtime errors: " + Errors.Count);
        lines.AddRange(Errors.Distinct());
        Directory.CreateDirectory("Logs/Player");
        File.WriteAllLines("Logs/Player/play-validation.txt", lines);
        File.WriteAllLines("Logs/Player/editor-diagnostics.txt", EditorDiagnostics);
        Debug.Log(lines[0]);
        EditorApplication.Exit(failure == null ? 0 : 1);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        Results.Add("PASS: " + message);
        Directory.CreateDirectory("Logs/Player");
        File.WriteAllLines("Logs/Player/play-progress.txt", Results);
    }
    private static Func<bool> Delay(float seconds) { float until = Time.time + seconds; return () => Time.time >= until; }
    private static object Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(target);
    private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, args);
    private static void Key(Key key, bool down) => InputSystem.QueueStateEvent(Keyboard.current, down ? new KeyboardState(key) : new KeyboardState());
    private static void NoTestContent()
    {
        Check(Object.FindObjectsByType<NpcDialogue>(FindObjectsSortMode.None).Length == 0 &&
            Object.FindObjectsByType<ReadableDocument>(FindObjectsSortMode.None).Length == 0 &&
            Object.FindObjectsByType<InspectableItem>(FindObjectsSortMode.None).Length == 0 &&
            Object.FindObjectsByType<ShootingTarget>(FindObjectsSortMode.None).Length == 0, "No NPC, documents, inspectable items or targets spawned");
    }
    private static GameObject Fixture(Camera camera, string name)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = camera.transform.position + camera.transform.forward * 1.5f;
        go.transform.localScale = Vector3.one * 0.3f;
        return go;
    }

    private static IEnumerator Checks()
    {
        testKeyboard = InputSystem.AddDevice<Keyboard>("PlayerValidationKeyboard");
        testMouse = InputSystem.AddDevice<Mouse>("PlayerValidationMouse");
        yield return Delay(2f);
        var player = Object.FindFirstObjectByType<FirstPersonPlayer>();
        var camera = player.GetComponentInChildren<Camera>();
        var pistol = player.GetComponent<PistolController>();
        var stress = player.GetComponent<PlayerStress>();
        var smoking = player.GetComponent<SmokingController>();
        yield return (Func<bool>)(() => (bool)Field(smoking, "modelReady"));
        Check(Object.FindObjectsByType<FirstPersonPlayer>(FindObjectsSortMode.None).Length == 1, "Standalone scene has one player");
        NoTestContent();
        Check(Object.FindFirstObjectByType<MergedGameBootstrap>() == null, "Player runs without the location bootstrap");
        Check(Camera.main == camera && Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(a => a.enabled) == 1, "Correct main camera and one active listener");
        Check(pistol.WeaponView.Model != null && pistol.WeaponView.Animator != null, "Weapon model and Animator initialize");
        Check(((GameObject)Field(smoking, "modelRoot")).transform.IsChildOf(camera.transform), "Smoking model belongs to the player camera");
        Check(((Camera)Field(player.GetComponent<ItemInspector>(), "previewCamera")).transform.IsChildOf(player.transform), "Inspection camera belongs to player");
        Check(((TMPro.TMP_Text)Field(smoking, "hintText")).transform.IsChildOf(player.transform), "Smoking uses the prefab's hint");
        Canvas.ForceUpdateCanvases();
        Check(player.GetComponentsInChildren<Canvas>().All(c => c.pixelRect.width > 0f && c.transform.lossyScale.sqrMagnitude > 0f), "Owned UI canvases have visible dimensions");

        Vector3 before = player.transform.position;
        Key(UnityEngine.InputSystem.Key.W, true);
        yield return Delay(0.35f);
        Key(UnityEngine.InputSystem.Key.W, false);
        yield return Delay(0.1f);
        Check(Vector3.Distance(before, player.transform.position) > 0.5f, "WASD moves the standalone player (from " + before + " to " + player.transform.position + ", movement=" + player.enabled + ")");
        Key(UnityEngine.InputSystem.Key.LeftCtrl, true);
        yield return Delay(0.3f);
        Check(player.GetComponent<CharacterController>().height < 1.2f, "Ctrl crouches");
        Key(UnityEngine.InputSystem.Key.LeftCtrl, false);
        yield return Delay(0.3f);
        Check(player.GetComponent<CharacterController>().height > 1.7f, "Releasing Ctrl restores height");
        yield return Delay(1.5f);
        Check(pistol.CanUseWeapon && !pistol.IsEquipping, "Weapon is ready (cursor=" + Cursor.lockState + ", ready=" + Field(pistol, "ready") + ", camera=" + camera.enabled + ", equip=" + pistol.IsEquipping + ", cursorAPI=" + AllowCursor?.DeclaringType.FullName + ")");
        Check(pistol.TryFire() && pistol.Rounds == 7, "Weapon fires and consumes ammunition");
        yield return Delay(0.5f);
        Check(pistol.TryReload(), "Reload starts");
        yield return (Func<bool>)(() => !pistol.IsReloading);
        Check(pistol.Rounds == 8 && pistol.ReserveAmmo == 31, "Animation event completes reload and transfers only the deficit");
        InputSystem.QueueStateEvent(Mouse.current, new MouseState { buttons = 2 });
        yield return Delay(0.5f);
        Check(Vector3.Distance(((Transform)Field(pistol, "viewModel")).localPosition, pistol.WeaponView.AimOffset) < 0.005f, "Right mouse aims the weapon");
        InputSystem.QueueStateEvent(Mouse.current, new MouseState());
        yield return Delay(0.2f);

        var documentGo = Fixture(camera, "Temporary document fixture");
        var document = documentGo.AddComponent<ReadableDocument>();
        Physics.SyncTransforms();
        var reader = player.GetComponent<DocumentReader>();
        Check(Call(reader, "FindDocument") == (object)document, "Reader finds a scene-authored document");
        Call(reader, "OpenDocument", document);
        yield return Delay(0.1f);
        Check(reader.IsReading && !player.enabled && !pistol.CanUseWeapon && !pistol.WeaponView.gameObject.activeSelf, "Reading blocks movement and hides weapon");
        Call(reader, "CloseDocument");
        Object.Destroy(documentGo);
        yield return Delay(0.2f);

        var itemGo = Fixture(camera, "Temporary inspection fixture");
        var item = itemGo.AddComponent<InspectableItem>();
        var inspector = player.GetComponent<ItemInspector>();
        Physics.SyncTransforms();
        Check(Call(inspector, "FindItem") == (object)item, "Inspector finds a scene-authored item");
        Call(inspector, "OpenInspection", item);
        yield return Delay(0.1f);
        Check(inspector.IsInspecting && !player.enabled && ((GameObject)Field(inspector, "previewRoot")).transform.IsChildOf(player.transform), "Inspection blocks movement and owns its preview");
        Call(inspector, "CloseInspection");
        Object.Destroy(itemGo);
        yield return Delay(0.2f);

        var dialogue = player.GetComponent<DialogueController>();
        var demo = Resources.Load<GameObject>("MergedGameplay");
        var npcGo = Object.Instantiate(demo.GetComponentInChildren<NpcDialogue>().gameObject);
        npcGo.name = "Temporary NPC fixture";
        // Use a small box centered on the ray so the test does not depend on a demo character's height.
        foreach (var collider in npcGo.GetComponentsInChildren<Collider>()) collider.enabled = false;
        npcGo.transform.position = camera.transform.position + camera.transform.forward * 1.5f;
        npcGo.AddComponent<BoxCollider>().size = Vector3.one * 0.3f;
        yield return Delay(0.1f);
        Physics.SyncTransforms();
        var npc = npcGo.GetComponent<NpcDialogue>();
        Check(Call(dialogue, "FindNearbyNpc") == (object)npc, "Dialogue discovers an unassigned NPC under the crosshair");
        var focus = new GameObject("Temporary consciousness focus").AddComponent<FocusPoint>();
        focus.transform.SetParent(npcGo.transform, false);
        var sceneSwitch = player.GetComponent<SceneSwitch>();
        Call(sceneSwitch, "FindNPC", camera);
        Check(Field(sceneSwitch, "currentPoint") == (object)focus, "Consciousness discovers a scene-authored FocusPoint");
        Call(dialogue, "BeginDialogue", npc);
        yield return Delay(0.1f);
        Check(dialogue.IsTalking && !player.enabled && !reader.enabled && !inspector.enabled && !pistol.CanUseWeapon, "Dialogue blocks other interactions");
        Call(dialogue, "SelectChoice", 0);
        Call(dialogue, "ContinueDialogue");
        Check(dialogue.IsTalking, "Dialogue choice advances to the next node");
        Call(dialogue, "EndDialogue");
        Object.Destroy(npcGo);
        yield return Delay(0.3f);
        Check(player.enabled && reader.enabled && inspector.enabled, "Closing interactions restores player control");

        stress.stress = 80f;
        yield return Delay(0.7f);
        Check(stress.volume.weight > 0.5f, "Stress drives the player's own volume");
        Key(UnityEngine.InputSystem.Key.Y, true);
        yield return Delay(0.1f);
        Key(UnityEngine.InputSystem.Key.Y, false);
        Check(smoking.IsSmoking && !player.enabled && !pistol.CanUseWeapon, "Y starts smoking and blocks weapon/movement");
        yield return (Func<bool>)(() => !smoking.IsSmoking && !stress.IsRecoveringAfterSmoking && stress.stress == 0f);
        yield return Delay(0.8f);
        Check(player.enabled && stress.volume.weight < 0.01f, "Smoking finishes, restores control and removes stress effect");

        // A location bootstrap must reuse a player placed by a level designer.
        var bootstrapGo = new GameObject("Temporary location services");
        var bootstrap = bootstrapGo.AddComponent<MergedGameBootstrap>();
        bootstrap.locationLabel = "Validation";
        yield return Delay(0.3f);
        Check(Object.FindObjectsByType<FirstPersonPlayer>(FindObjectsSortMode.None).Length == 1 && Camera.main == camera && camera.enabled, "Bootstrap reuses a placed player and preserves its camera");
        Check(!player.GetComponentsInChildren<MeshCollider>(true).Any(), "Scene collision setup does not add colliders to player visuals");
        NoTestContent();

        foreach (int index in new[] { 0, 1 })
        {
            SceneSwitch.LoadSceneIndex(index);
            yield return Delay(2f);
            Check(Object.FindObjectsByType<FirstPersonPlayer>(FindObjectsSortMode.None).Length == 0,
                "Empty location does not create a player: " + SceneManager.GetActiveScene().name);
            Check(GameObject.Find("Location Exit") == null, "Old runtime travel marker is removed");
            Check(Object.FindObjectsByType<LocationTransitionZone>(FindObjectsSortMode.None).Length == 1,
                "Location has one authored trigger zone");
            NoTestContent();
        }
        // Authored-player location loading and the mind round trip are covered by
        // LocationTransitionValidation, using temporary scenes saved only for that test.
        SceneSwitch.LoadSceneIndex(4);
        yield return Delay(2f);
        var legacySmoking = Object.FindFirstObjectByType<SmokingController>();
        Check(legacySmoking != null && Field(legacySmoking, "hintText") != null, "Preserved StressDemo still receives its smoking hint");
        Check(Errors.Count == 0, "No runtime errors during player and location checks");
    }
}
