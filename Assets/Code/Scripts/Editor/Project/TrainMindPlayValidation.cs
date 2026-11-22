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
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class TrainMindPlayValidation
{
    private const string RunFlag = "TrainMindPlayValidation.Running";
    private const string LiveFlag = "TrainMindPlayValidation.Live";
    private const string Train = "Assets/Level/Scenes/Locations/Train.unity";
    private const string FirstMind = "Assets/Level/Scenes/Locations/MindsScenes/FirstMind.unity";
    private const string ThirdMind = "Assets/Level/Scenes/Locations/MindsScenes/ThirdMind-interogation_room.unity";
    private const string Lab = "Assets/Level/Scenes/Locations/Lab.unity";
    private static readonly List<string> results = new List<string>();
    private static readonly List<string> errors = new List<string>();
    private static IEnumerator checks;
    private static double deadline;
    private static int lastFrame = -1;
    private static Mouse testMouse;
    private static Keyboard testKeyboard;
    private static KeyboardState keyboardState;
    private static bool ranInBackground;
    private static UnityEngine.Events.UnityAction<Scene, LoadSceneMode> onSceneLoaded;
    private static UnityEngine.Events.UnityAction<Scene> onSceneUnloaded;
    private static UnityEngine.Events.UnityAction<Scene, Scene> onActiveSceneChanged;

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

    public static void RunInCurrentEditor()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != Train || scene.isDirty)
            throw new InvalidOperationException("Open the saved Train scene in Edit Mode before running the checks.");
        SessionState.SetBool(LiveFlag, true);
        SessionState.SetBool(RunFlag, true);
        EditorApplication.EnterPlaymode();
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode && checks != null)
            Finish("Play Mode ended before validation completed.");
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(RunFlag, false)) return;
        results.Clear();
        errors.Clear();
        lastFrame = -1;
        ranInBackground = Application.runInBackground;
        Application.runInBackground = true;
        testKeyboard = InputSystem.AddDevice<Keyboard>("Train mind test");
        testMouse = InputSystem.AddDevice<Mouse>("Train mind test mouse");
        QueueKeys();
        InputSystem.onAfterUpdate += ApplyKeyboardState;
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
        var first = GameObject.Find("NPC_1").GetComponent<NpcDialogue>();
        var third = GameObject.Find("NPC_2").GetComponent<NpcDialogue>();
        var focus = first.GetComponentInChildren<FocusPoint>();
        var camera = player.GetComponentInChildren<Camera>();
        var stress = player.GetComponent<PlayerStress>();
        var pistol = player.GetComponent<PistolController>();
        var magazine = (WeaponMagazine)typeof(PistolController)
            .GetField("magazine", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pistol);
        var entry = player.GetComponent<SceneSwitch>();
        var entryInspector = new SerializedObject(entry);
        var vignetteColor = entryInspector.FindProperty("mindVignetteColor");
        var inspector = player.GetComponent<ItemInspector>();
        inspector.enabled = false;
        Check(train.path == Train && player.gameObject.scene == train && focus != null, "Train starts with its authored Player and NPCs");
        bool verifyCursor = !Application.isBatchMode && !SessionState.GetBool(LiveFlag, false);
        if (verifyCursor)
            Check(EditorWindow.focusedWindow?.GetType().FullName == "UnityEditor.GameView", "Game View has focus");

        var targetField = typeof(SceneSwitch).GetField("currentPoint", BindingFlags.Instance | BindingFlags.NonPublic);
        var dialogue = player.GetComponent<DialogueController>();
        var talkTarget = typeof(DialogueController).GetField("nearbyNpc", BindingFlags.Instance | BindingFlags.NonPublic);
        var firstApproach = new Vector3(35.77f, 0.12f, 8.42f);
        var thirdApproach = new Vector3(35.45f, 0.21f, 9.55f);
        var airport = GameObject.Find("NPC_3").GetComponent<NpcDialogue>();
        foreach (var frame in CheckDialogues(player, camera, dialogue, entry,
                     (first, "Петрович", firstApproach),
                     (third, "Майкл", thirdApproach),
                     (airport, "Иннокентий", new Vector3(35.45f, 0.21f, 10.32f))))
            yield return frame;

        QueueKeys(Key.T);
        yield return null;
        Check(!player.enabled && GameObject.Find("Train Mind Session") != null,
            "NPC_3: T starts the mind approach after the conversation");
        var airportFocus = airport.GetComponentInChildren<FocusPoint>();
        airportFocus.enabled = false;
        QueueKeys();
        while (!player.enabled) yield return null;
        airportFocus.enabled = true;

        Object.FindFirstObjectByType<TrainHostileEncounter>().EnableHostileNpcs();
        var positionBeforeUnlock = player.transform.position;
        var carriage = GameObject.Find("Ivan Pursuit Carriage").transform.position;
        player.Teleport(new Vector3(carriage.x, positionBeforeUnlock.y, carriage.z));
        yield return null;
        player.Teleport(positionBeforeUnlock);

        var changes = new GameObject("Added during Train play");
        var inactive = new GameObject("Initially inactive Train object");
        inactive.SetActive(false);
        var destroyed = new GameObject("Removed during Train play");
        float stressWhenMindLoaded = -1f;
        Vector3 cameraWhenMindLoaded = Vector3.zero;
        Quaternion rotationWhenMindLoaded = Quaternion.identity;
        Color coverWhenMindLoaded = Color.clear;
        int mindLoads = 0;
        int mindActivations = 0;
        int trainUnloads = 0;
        var labMode = LoadSceneMode.Additive;
        onSceneLoaded = (scene, mode) =>
        {
            if ((scene.path == FirstMind || scene.path == ThirdMind) && mode == LoadSceneMode.Additive)
            {
                mindLoads++;
                stressWhenMindLoaded = stress.stress;
                cameraWhenMindLoaded = camera.transform.position;
                rotationWhenMindLoaded = camera.transform.rotation;
                coverWhenMindLoaded = GameObject.Find("Mind Vignette Cover").GetComponent<Image>().color;
            }
            if (scene.path == Lab) labMode = mode;
        };
        onSceneUnloaded = scene => { if (scene == train) trainUnloads++; };
        onActiveSceneChanged = (_, scene) =>
        {
            if (scene.path == FirstMind || scene.path == ThirdMind) mindActivations++;
        };
        SceneManager.sceneLoaded += onSceneLoaded;
        SceneManager.sceneUnloaded += onSceneUnloaded;
        SceneManager.activeSceneChanged += onActiveSceneChanged;

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
            var npc = visit == 1 ? first : third;
            var target = npc.GetComponentInChildren<FocusPoint>();
            string mindPath = visit == 1 ? FirstMind : ThirdMind;
            player.Teleport(visit == 1 ? firstApproach : thirdApproach);
            if (visit == 1) Object.Destroy(destroyed);
            yield return null;
            foreach (var renderer in npc.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                AimAt(player, camera, renderer.bounds.center);
                yield return null;
                Check(entry.hintText.gameObject.activeInHierarchy && ReferenceEquals(targetField.GetValue(entry), target) &&
                    ReferenceEquals(talkTarget.GetValue(dialogue), npc),
                    $"{npc.name}: aiming at {renderer.name} keeps the same NPC and visible T prompt");
            }

            var head = npc.transform.Find("Interaction - Head").position;
            var torso = npc.transform.Find("Interaction - Torso").position;
            var legs = npc.transform.Find("Interaction - Legs").position;
            for (int step = 0; step <= 12; step++)
            {
                Vector3 aim = step <= 6 ? Vector3.Lerp(head, torso, step / 6f) : Vector3.Lerp(torso, legs, (step - 6) / 6f);
                AimAt(player, camera, aim);
                yield return null;
                Check(entry.hintText.gameObject.activeInHierarchy && ReferenceEquals(targetField.GetValue(entry), target),
                    $"{npc.name}: the T prompt remains visible and targets the same NPC at sweep step {step}");
            }

            if (visit == 1)
            {
                AimAt(player, camera, camera.transform.position + Vector3.up);
                QueueKeys(Key.T);
                yield return null;
                Check(!entry.hintText.gameObject.activeSelf && targetField.GetValue(entry) == null && player.enabled,
                    "Aiming away hides the hint and rejects T in the same frame");
                QueueKeys();
                AimAt(player, camera, target.transform.position);
                float reach = entry.interactionDistance;
                entry.interactionDistance = 0.5f;
                yield return null;
                QueueKeys(Key.T);
                yield return null;
                Check(!entry.hintText.gameObject.activeSelf && player.enabled, "An out-of-range NPC hides the hint and rejects T");
                QueueKeys();
                entry.interactionDistance = reach;
                int index = target.sceneIndex;
                target.sceneIndex = int.MaxValue;
                yield return null;
                QueueKeys(Key.T);
                yield return null;
                Check(!entry.hintText.gameObject.activeSelf && player.enabled, "An unavailable mind hides the hint and rejects T");
                QueueKeys();
                target.sceneIndex = index;
                target.enabled = false;
                yield return null;
                QueueKeys(Key.T);
                yield return null;
                Check(!entry.hintText.gameObject.activeSelf && player.enabled, "A disabled FocusPoint hides the hint and rejects T");
                QueueKeys();
                target.enabled = true;
            }

            AimAt(player, camera, torso);
            yield return null;
            Check(entry.hintText.gameObject.activeInHierarchy && ReferenceEquals(targetField.GetValue(entry), target),
                $"{npc.name}: the T prompt targets its authored mind before pressing the key");
            var pitch = camera.transform.localRotation;
            var npcPosition = npc.transform.position;
            var playerPosition = player.transform.position;
            var playerRotation = player.transform.rotation;
            var localCameraPosition = camera.transform.localPosition;
            var startCameraPosition = camera.transform.position;
            var startCameraRotation = camera.transform.rotation;
            Color testColor = visit == 1 ? new Color(0.22f, 0.07f, 0.13f) : new Color(0.06f, 0.17f, 0.24f);
            entryInspector.Update();
            vignetteColor.colorValue = testColor;
            entryInspector.ApplyModifiedPropertiesWithoutUndo();
            Check(magazine.TryFire() && magazine.TryBeginReload(), $"Visit {visit}: ammunition spent and reload started");
            if (visit == 1) Check(magazine.ApplyInsertionEvent(magazine.ReloadOperationId), "Committed reload changes the reserve");
            pistol.SetEquipped(visit != 1);
            int rounds = magazine.Rounds;
            int reserve = magazine.Reserve;
            stress.stress = visit == 1 ? 85f : 62f;

            QueueKeys(Key.T, Key.Space);
            while (player.enabled) yield return null;
            QueueKeys(Key.Space);
            var overlay = GameObject.Find("Mind Transition Canvas").GetComponent<Canvas>();
            var edge = GameObject.Find("Mind Vignette").GetComponent<RawImage>();
            var cover = GameObject.Find("Mind Vignette Cover").GetComponent<Image>();
            Check(!overlay.enabled && !SceneManager.GetSceneByPath(mindPath).IsValid() &&
                Vector3.Distance(camera.transform.position, startCameraPosition) < 0.003f &&
                Quaternion.Angle(camera.transform.rotation, startCameraRotation) < 0.05f,
                $"Visit {visit}: T starts at the current camera pose with no vignette or scene load");
            float initialStress = stress.stress;
            while (stress.stress >= initialStress) yield return null;
            while (Quaternion.Angle(camera.transform.rotation, startCameraRotation) < 0.25f) yield return null;
            Check(stress.stress > 0f && SceneManager.GetActiveScene() == train && !player.enabled && !overlay.enabled &&
                Vector3.Distance(camera.transform.position, startCameraPosition) < 0.003f,
                $"Visit {visit}: the camera turns in place before sliding and stress decreases");
            while (edge.uvRect.width < 2f) yield return null;
            Check(overlay.enabled && camera.isActiveAndEnabled && !SceneManager.GetSceneByPath(mindPath).IsValid() &&
                edge.color.a > 0f && cover.color.a == 0f && HasColor(edge.color, testColor),
                $"Visit {visit}: the Inspector color appears at the edges during the approach before loading");
            while (SceneManager.GetActiveScene().path != mindPath) yield return null;
            float approachDistance = Vector3.Distance(cameraWhenMindLoaded, startCameraPosition);
            Check(approachDistance > 0.05f && approachDistance <= 0.351f &&
                Vector3.Angle(rotationWhenMindLoaded * Vector3.forward, target.transform.position - cameraWhenMindLoaded) < 0.1f,
                $"Visit {visit}: the camera moved {approachDistance:F3} m toward the chosen NPC");
            Check(coverWhenMindLoaded.a == 1f && HasColor(coverWhenMindLoaded, testColor),
                $"Visit {visit}: the vignette is closed in the selected color when the scene loads");
            QueueKeys();
            yield return null;
            var mind = SceneManager.GetActiveScene();
            Check(mindLoads == visit && stressWhenMindLoaded == 0f && stress.stress == 0f,
                $"{npc.name}: T opens {mind.name} additively at zero stress");
            Check(train.isLoaded && trainUnloads == 0 && !player.gameObject.activeInHierarchy && !npc.gameObject.activeInHierarchy,
                $"Visit {visit}: original Train and Player stay loaded but suspended");
            Check(Camera.main != null && Camera.main.gameObject.scene == mind &&
                Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(item => item.enabled) == 1,
                $"Visit {visit}: the active camera and audio listener belong to the mind scene");
            if (verifyCursor && visit == 1) CheckCursorLocked("FirstMind starts with the cursor hidden and locked");
            while (overlay.enabled) yield return null;
            Check(cover.color.a == 0f, $"Visit {visit}: the new scene is revealed after the switch");

            QueueKeys(Key.Escape);
            yield return null;
            yield return null;
            Check(SceneManager.GetActiveScene() == mind && !player.enabled,
                $"Visit {visit}: Escape does not exit the mind");
            QueueKeys();
            yield return null;
            if (visit == 1)
            {
                QueueKeys(Key.U);
            }
            else
            {
                var sequence = Object.FindFirstObjectByType<ThirdMindSequence>();
                Check(GameObject.Find("Third Mind UI").scene == mind && EventSystem.current.gameObject.scene == mind,
                    "ThirdMind owns its runtime UI and EventSystem");
                typeof(ThirdMindSequence).GetMethod("FinishConfession", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(sequence, null);
            }
            while (!player.enabled) yield return null;
            QueueKeys();
            Check(SceneManager.GetActiveScene() == train && !mind.isLoaded && mindLoads == visit && trainUnloads == 0,
                $"Visit {visit}: the mind exit resumes Train without reloading it");
            Check(Object.FindObjectsByType<FirstPersonPlayer>(FindObjectsSortMode.None).Single() == player &&
                Camera.main == camera && EventSystem.current != null && !inspector.enabled && (overlay == null || !overlay.enabled),
                $"Visit {visit}: original controls and UI are restored with the mind vignette gone");
            Check(Vector3.Distance(player.transform.position, playerPosition) < 0.03f &&
                Quaternion.Angle(player.transform.rotation, playerRotation) < 0.01f &&
                Quaternion.Angle(camera.transform.localRotation, pitch) < 0.01f &&
                Vector3.Distance(camera.transform.localPosition, localCameraPosition) < 0.03f,
                $"Visit {visit}: player position, yaw and camera pose are restored");
            Check(pistol.Rounds == rounds && pistol.ReserveAmmo == reserve && pistol.IsEquipped == (visit != 1) &&
                !pistol.IsReloading && stress.stress == 0f,
                $"Visit {visit}: current ammunition and weapon state survive; stress stays at zero");
            Check(npc.transform.position == npcPosition && npc.gameObject.activeSelf && changes != null && !inactive.activeSelf && destroyed == null,
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

        player.Teleport(firstApproach);
        yield return null;
        AimAt(player, camera, first.transform.Find("Interaction - Head").position);
        yield return null;
        if (verifyCursor)
        {
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
        Vector3 beforeCancel = camera.transform.position;
        QueueKeys(Key.T);
        while (player.enabled) yield return null;
        QueueKeys();
        Vector3 cameraDirection = (focus.transform.position - beforeCancel).normalized;
        var wall = new GameObject("Camera clearance test wall");
        wall.transform.SetPositionAndRotation(beforeCancel + cameraDirection * 0.24f, Quaternion.LookRotation(cameraDirection));
        wall.AddComponent<BoxCollider>().size = new Vector3(0.45f, 0.45f, 0.05f);
        Physics.SyncTransforms();
        var abortOverlay = GameObject.Find("Mind Transition Canvas").GetComponent<Canvas>();
        var abortEdge = GameObject.Find("Mind Vignette").GetComponent<RawImage>();
        while (abortEdge.uvRect.width < 2.5f) yield return null;
        float blockedTravel = Vector3.Distance(camera.transform.position, beforeCancel);
        Check(blockedTravel > 0.02f && blockedTravel < 0.1f, $"Camera stops before an obstruction after {blockedTravel:F3} m");
        focus.enabled = false;
        while (!player.enabled) yield return null;
        Object.Destroy(wall);
        Check(SceneManager.GetActiveScene() == train && mindLoads == 2 && mindActivations == 2 &&
            !SceneManager.GetSceneByPath(FirstMind).IsValid() && Camera.main == camera &&
            (abortOverlay == null || !abortOverlay.enabled) && Vector3.Distance(camera.transform.position, beforeCancel) < 0.003f,
            "Cancelled approach restores the original camera and vignette without loading the mind");
        Check(stress.stress > 0f && stress.IsRecoveringAfterSmoking,
            "Cancelled entry restores control, nonzero stress and the smoking recovery");
        while (stress.IsRecoveringAfterSmoking) yield return null;
        Check(stress.stress == 0f, "Restored recovery finishes normally");

        var zone = Object.FindObjectsByType<LocationTransitionZone>(FindObjectsSortMode.None).Single(z => z.TargetScenePath == Lab);
        player.Teleport(zone.transform.TransformPoint(zone.GetComponent<BoxCollider>().center));
        Physics.SyncTransforms();
        while (SceneManager.GetActiveScene().path != Lab || LocationTransition.IsTransitioning) yield return null;
        Check(labMode == LoadSceneMode.Single && player == null && trainUnloads == 1 && mindActivations == 2,
            "Real Train-to-Lab trigger loads in Single mode and unloads Train and its player");
        Check(errors.Count == 0, "No runtime errors");
    }

    private static IEnumerable CheckDialogues(FirstPersonPlayer player, Camera camera,
        DialogueController dialogue, SceneSwitch entry, params (NpcDialogue Npc, string Name, Vector3 Approach)[] targets)
    {
        var dialogueView = new SerializedObject(dialogue);
        var speaker = (Text)dialogueView.FindProperty("speakerText").objectReferenceValue;
        var line = (Text)dialogueView.FindProperty("dialogueText").objectReferenceValue;
        var hint = entry.hintText.gameObject;
        foreach (var (npc, name, approach) in targets)
        {
            var torso = npc.transform.Find("Interaction - Torso").position;
            var opening = new SerializedObject(npc).FindProperty("openingLine").stringValue;
            Check(npc.CharacterName == name && !string.IsNullOrEmpty(opening) &&
                npc.Nodes.Length == 4 && NpcDialogueEditor.GetWarnings(npc).Count == 0,
                $"{npc.name}: authored graph belongs to {name}");

            player.Teleport(approach);
            yield return null;
            AimAt(player, camera, torso);
            yield return null;
            QueueKeys(Key.T);
            yield return null;
            Check(!hint.activeSelf && player.enabled && GameObject.Find("Train Mind Session") == null,
                $"{npc.name}: T is rejected before this NPC's dialogue, regardless of earlier NPCs");

            QueueKeys(Key.E);
            yield return null;
            Check(dialogue.IsTalking && speaker.text == "Алексей" && line.text == opening && !hint.activeSelf,
                $"{npc.name}: E shows Alexey's opening line");
            QueueKeys(Key.Space);
            yield return null;
            Check(dialogue.IsTalking && speaker.text == name && line.text == npc.Nodes[0].npcText,
                $"{npc.name}: Continue shows the first NPC line");

            QueueKeys(Key.Escape, Key.T);
            yield return null;
            Check(!dialogue.IsTalking && !hint.activeSelf && player.enabled && GameObject.Find("Train Mind Session") == null,
                $"{npc.name}: Escape closes the partial conversation and T stays locked");

            QueueKeys();
            AimAt(player, camera, torso);
            yield return null;
            QueueKeys(Key.E);
            yield return null;
            Check(dialogue.IsTalking && speaker.text == "Алексей" && line.text == opening,
                $"{npc.name}: interrupted dialogue can be replayed from the beginning");
            foreach (var node in npc.Nodes)
            {
                QueueKeys(Key.Space);
                yield return null;
                Check(dialogue.IsTalking && speaker.text == name && line.text == node.npcText && !hint.activeSelf,
                    $"{npc.name}: NPC line {node.Id} is shown with the mind hint hidden");

                var reply = node.choices.Single();
                QueueKeys(Key.Digit1);
                yield return null;
                if (!reply.EndsDialogue)
                    Check(dialogue.IsTalking && speaker.text == "Алексей" && line.text == reply.text,
                        $"{npc.name}: player reply after line {node.Id} keeps Alexey's name");
            }

            QueueKeys();
            AimAt(player, camera, torso);
            yield return null;
            Check(!dialogue.IsTalking && player.enabled && hint.activeInHierarchy,
                $"{npc.name}: finishing the final line unlocks T for this NPC");
        }
    }

    private static bool HasColor(Color actual, Color expected) =>
        Mathf.Abs(actual.r - expected.r) < 0.001f && Mathf.Abs(actual.g - expected.g) < 0.001f &&
        Mathf.Abs(actual.b - expected.b) < 0.001f;

    private static void AimAt(FirstPersonPlayer player, Camera camera, Vector3 point)
    {
        Vector3 direction = (point - camera.transform.position).normalized;
        float pitch = -Mathf.Asin(direction.y) * Mathf.Rad2Deg;
        player.transform.rotation = Quaternion.Euler(0f, Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg, 0f);
        typeof(FirstPersonPlayer).GetField("cameraPitch", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(player, pitch);
        camera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        Physics.SyncTransforms();
    }

    private static void QueueKeys(params Key[] keys) => keyboardState = new KeyboardState(keys);

    private static void ApplyKeyboardState()
    {
        if (InputState.currentUpdateType != InputUpdateType.Dynamic) return;
        testKeyboard.MakeCurrent();
        InputState.Change(testKeyboard, keyboardState, InputUpdateType.Dynamic);
    }

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
        SceneManager.sceneLoaded -= onSceneLoaded;
        SceneManager.sceneUnloaded -= onSceneUnloaded;
        SceneManager.activeSceneChanged -= onActiveSceneChanged;
        onSceneLoaded = null;
        onSceneUnloaded = null;
        onActiveSceneChanged = null;
        InputSystem.onAfterUpdate -= ApplyKeyboardState;
        InputSystem.RemoveDevice(testKeyboard);
        InputSystem.RemoveDevice(testMouse);
        Application.runInBackground = ranInBackground;
        bool live = SessionState.GetBool(LiveFlag, false);
        SessionState.SetBool(LiveFlag, false);
        SessionState.SetBool(RunFlag, false);
        var lines = new List<string> { failure == null ? "TRAIN_MIND_PLAY_PASS" : "TRAIN_MIND_PLAY_FAIL" };
        lines.AddRange(results);
        if (failure != null) lines.Add(failure);
        lines.AddRange(errors);
        Directory.CreateDirectory("Logs/TrainMind");
        File.WriteAllLines("Logs/TrainMind/play-validation.txt", lines);
        Debug.Log(lines[0]);
        if (live) EditorApplication.isPlaying = false;
        else EditorApplication.Exit(failure == null ? 0 : 1);
    }
}
