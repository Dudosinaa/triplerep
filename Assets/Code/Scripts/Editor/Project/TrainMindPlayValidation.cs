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
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class TrainMindPlayValidation
{
    private const string RunFlag = "TrainMindPlayValidation.Running";
    private const string Train = "Assets/Level/Scenes/Locations/Train.unity";
    private const string Mind = "Assets/Level/Scenes/Gameplay/ConsciousnessScene.unity";
    private const string Lab = "Assets/Level/Scenes/Locations/Lab.unity";
    private static readonly List<string> results = new List<string>();
    private static readonly List<string> errors = new List<string>();
    private static IEnumerator checks;
    private static double deadline;
    private static int lastFrame = -1;
    private static Mouse testMouse;

    static TrainMindPlayValidation()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.update += Tick;
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode && !Environment.GetCommandLineArgs().Contains("-trainMindCursorValidation"))
            throw new InvalidOperationException("Run in a separate editor with -trainMindCursorValidation or -batchmode.");
        EditorSceneManager.OpenScene(Train);
        if (!Application.isBatchMode)
        {
            var gameView = EditorWindow.GetWindow(typeof(EditorApplication).Assembly.GetType("UnityEditor.GameView"));
            gameView.Show();
            gameView.Focus();
        }
        SessionState.SetBool(RunFlag, true);
        EditorApplication.EnterPlaymode();
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(RunFlag, false)) return;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.AddDevice<Keyboard>("Train mind test");
        testMouse = InputSystem.AddDevice<Mouse>("Train mind test mouse");
        Application.logMessageReceived += OnLog;
        deadline = EditorApplication.timeSinceStartup + 90;
        checks = RunChecks();
    }

    private static void Tick()
    {
        if (checks == null || !EditorApplication.isPlaying || lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline)
                throw new TimeoutException("Timed out after: " + (results.LastOrDefault() ?? "starting Play Mode"));
            if (!checks.MoveNext()) Finish(null);
        }
        catch (Exception error) { Finish(error.ToString()); }
    }

    private static void OnLog(string message, string stack, LogType type)
    {
        if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) &&
            !stack.Contains("UnityEditor.Search.")) errors.Add(message + "\n" + stack);
    }

    private static IEnumerator RunChecks()
    {
        yield return null;
        var train = SceneManager.GetActiveScene();
        var player = Object.FindObjectsByType<FirstPersonPlayer>(FindObjectsSortMode.None).Single();
        var npc = Object.FindObjectsByType<NpcDialogue>(FindObjectsSortMode.None).Single();
        var focus = npc.GetComponentInChildren<FocusPoint>();
        var camera = player.GetComponentInChildren<Camera>();
        var stress = player.GetComponent<PlayerStress>();
        var pistol = player.GetComponent<PistolController>();
        var magazine = (WeaponMagazine)typeof(PistolController)
            .GetField("magazine", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pistol);
        var entry = player.GetComponent<SceneSwitch>();
        var inspector = player.GetComponent<ItemInspector>();
        inspector.enabled = false;
        Check(train.path == Train && player.gameObject.scene == train && focus != null, "Train starts with its authored Player and NPC");
        bool verifyCursor = !Application.isBatchMode;
        if (verifyCursor)
            Check(EditorWindow.focusedWindow?.GetType().FullName == "UnityEditor.GameView", "Game View has focus");

        // Keep the reversible test changes clear of the carriage's static colliders.
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.transform.SetPositionAndRotation(new Vector3(0f, 99.5f, 0f), Quaternion.identity);
        floor.transform.localScale = new Vector3(30f, 1f, 30f);
        var inactive = new GameObject("Initially inactive Train object");
        inactive.SetActive(false);
        var destroyed = new GameObject("Removed during Train play");
        float stressWhenMindLoaded = -1f;
        int mindLoads = 0;
        int trainUnloads = 0;
        var labMode = LoadSceneMode.Additive;
        SceneManager.sceneLoaded += (scene, mode) =>
        {
            if (scene.path == Mind && mode == LoadSceneMode.Additive)
            {
                mindLoads++;
                stressWhenMindLoaded = stress.stress;
            }
            if (scene.path == Lab) labMode = mode;
        };
        SceneManager.sceneUnloaded += scene => { if (scene == train) trainUnloads++; };

        for (int visit = 1; visit <= 2; visit++)
        {
            if (verifyCursor)
            {
                QueueKeys(Key.Escape);
                yield return null;
                Check(Cursor.visible && Cursor.lockState == CursorLockMode.None,
                    $"Visit {visit}: Train Escape releases the cursor before entering the mind");
                QueueKeys();
            }
            player.Teleport(new Vector3(visit * 2f, 100.08f, 0f));
            player.transform.rotation = Quaternion.Euler(0f, visit * 23f, 0f);
            if (visit == 1) Object.Destroy(destroyed);
            yield return null;
            var pitch = Quaternion.Euler(visit * 11f, 0f, 0f);
            typeof(FirstPersonPlayer).GetField("cameraPitch", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(player, visit * 11f);
            camera.transform.localRotation = pitch;
            npc.transform.position = camera.transform.position + camera.transform.forward * 1.1f;
            Physics.SyncTransforms();
            var npcPosition = npc.transform.position;
            var playerPosition = player.transform.position;
            var playerRotation = player.transform.rotation;
            var localCameraPosition = camera.transform.localPosition;
            Check(magazine.TryFire() && magazine.TryBeginReload(), $"Visit {visit}: ammunition spent and reload started");
            if (visit == 1) Check(magazine.ApplyInsertionEvent(magazine.ReloadOperationId), "Committed reload changes the reserve");
            pistol.SetEquipped(visit != 1);
            int rounds = magazine.Rounds;
            int reserve = magazine.Reserve;
            stress.stress = visit == 1 ? 85f : 62f;

            QueueKeys(Key.T, Key.Space);
            while (player.enabled) yield return null;
            QueueKeys(Key.Space);
            float initialStress = stress.stress;
            while (stress.stress >= initialStress) yield return null;
            Check(stress.stress > 0f && SceneManager.GetActiveScene() == train && !player.enabled,
                $"Visit {visit}: stress decreases during the approach and gameplay stays locked");
            while (SceneManager.GetActiveScene().path != Mind) yield return null;
            QueueKeys();
            yield return null;
            var mind = SceneManager.GetActiveScene();
            Check(mindLoads == visit && stressWhenMindLoaded == 0f && stress.stress == 0f,
                $"Visit {visit}: ConsciousnessScene loads additively at zero stress");
            Check(train.isLoaded && trainUnloads == 0 && !player.gameObject.activeInHierarchy && !npc.gameObject.activeInHierarchy,
                $"Visit {visit}: original Train and Player stay loaded but suspended");
            Check(Camera.main != null && Camera.main.gameObject.scene == mind &&
                Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(item => item.enabled) == 1 &&
                GameObject.Find("Consciousness Placeholder").scene == mind && EventSystem.current == null,
                $"Visit {visit}: only the mind camera, audio listener and UI are active");
            Check(GameObject.Find("Return Hint").GetComponent<UnityEngine.UI.Text>().text == "Сознание\nU — вернуться",
                $"Visit {visit}: the return hint names U");
            if (verifyCursor) CheckCursorLocked($"Visit {visit}: the mind starts with the cursor hidden and locked");

            QueueKeys(Key.Escape);
            yield return null;
            yield return null;
            Check(SceneManager.GetActiveScene() == mind && !player.enabled,
                $"Visit {visit}: Escape does not exit the mind");
            if (verifyCursor) CheckCursorLocked($"Visit {visit}: the cursor remains locked in the mind after initialization");
            QueueKeys();
            QueueKeys(Key.U);
            QueueKeys();
            QueueKeys(Key.U);
            while (!player.enabled) yield return null;
            QueueKeys();
            Check(SceneManager.GetActiveScene() == train && !mind.isLoaded && mindLoads == visit && trainUnloads == 0,
                $"Visit {visit}: U resumes Train without reloading it");
            Check(Object.FindObjectsByType<FirstPersonPlayer>(FindObjectsSortMode.None).Single() == player &&
                Camera.main == camera && EventSystem.current != null && !inspector.enabled,
                $"Visit {visit}: original controls and UI are restored without duplication");
            Check(Vector3.Distance(player.transform.position, playerPosition) < 0.03f &&
                Quaternion.Angle(player.transform.rotation, playerRotation) < 0.01f &&
                Quaternion.Angle(camera.transform.localRotation, pitch) < 0.01f &&
                Vector3.Distance(camera.transform.localPosition, localCameraPosition) < 0.03f,
                $"Visit {visit}: player position, yaw and camera pose are restored");
            Check(pistol.Rounds == rounds && pistol.ReserveAmmo == reserve && pistol.IsEquipped == (visit != 1) &&
                !pistol.IsReloading && stress.stress == 0f,
                $"Visit {visit}: current ammunition and weapon state survive; stress stays at zero");
            Check(npc.transform.position == npcPosition && npc.gameObject.activeSelf && floor != null && !inactive.activeSelf && destroyed == null,
                $"Visit {visit}: modified NPC and created, inactive and removed Train objects retain their state");
            if (verifyCursor)
            {
                CheckCursorLocked($"Visit {visit}: U restores Train with the cursor already hidden and locked");
                Quaternion beforeMouse = camera.transform.rotation;
                testMouse.MakeCurrent();
                InputSystem.QueueStateEvent(testMouse, new MouseState { delta = new Vector2(42f, -24f) });
                yield return null;
                Check(Quaternion.Angle(beforeMouse, camera.transform.rotation) > 1f,
                    $"Visit {visit}: mouse motion turns the camera on the first frame after return, without a click");
                yield return null;
                CheckCursorLocked($"Visit {visit}: Train leaves the cursor hidden after subsequent updates");
            }
            else yield return null;
        }

        npc.transform.position = camera.transform.position + camera.transform.forward * 1.1f;
        Physics.SyncTransforms();
        if (verifyCursor)
        {
            var dialogue = player.GetComponent<DialogueController>();
            QueueKeys(Key.E);
            yield return null;
            Check(dialogue.IsTalking && Cursor.visible && Cursor.lockState == CursorLockMode.None,
                "A dialogue after returning still shows and releases the cursor");
            QueueKeys();
            dialogue.enabled = false;
            dialogue.enabled = true;
        }

        stress.stress = 70f;
        stress.BeginSmokingRecovery();
        QueueKeys(Key.T);
        while (player.enabled) yield return null;
        QueueKeys();
        focus.enabled = false;
        while (!player.enabled) yield return null;
        Check(SceneManager.GetActiveScene() == train && mindLoads == 2 && stress.stress > 0f && stress.IsRecoveringAfterSmoking,
            "Cancelled entry restores control, nonzero stress and the smoking recovery");
        while (stress.IsRecoveringAfterSmoking) yield return null;
        Check(stress.stress == 0f, "Restored recovery finishes normally");

        var zone = Object.FindObjectsByType<LocationTransitionZone>(FindObjectsSortMode.None).Single(z => z.TargetScenePath == Lab);
        player.Teleport(zone.transform.TransformPoint(zone.GetComponent<BoxCollider>().center));
        Physics.SyncTransforms();
        while (SceneManager.GetActiveScene().path != Lab || LocationTransition.IsTransitioning) yield return null;
        Check(labMode == LoadSceneMode.Single && player == null && trainUnloads == 1 && mindLoads == 2,
            "Real Train-to-Lab trigger loads in Single mode and unloads Train and its player");
        Check(errors.Count == 0, "No runtime errors");
    }

    private static void QueueKeys(params Key[] keys) => InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(keys));

    private static void CheckCursorLocked(string message) =>
        Check(!Cursor.visible && Cursor.lockState == CursorLockMode.Locked,
            $"{message} (visible={Cursor.visible}, lockState={Cursor.lockState})");

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        results.Add("PASS: " + message);
    }

    private static void Finish(string failure)
    {
        checks = null;
        Application.logMessageReceived -= OnLog;
        SessionState.SetBool(RunFlag, false);
        var lines = new List<string> { failure == null ? "TRAIN_MIND_PLAY_PASS" : "TRAIN_MIND_PLAY_FAIL" };
        lines.AddRange(results);
        if (failure != null) lines.Add(failure);
        lines.AddRange(errors);
        Directory.CreateDirectory("Logs/TrainMind");
        File.WriteAllLines("Logs/TrainMind/play-validation.txt", lines);
        Debug.Log(lines[0]);
        EditorApplication.Exit(failure == null ? 0 : 1);
    }
}
