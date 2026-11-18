using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// A visit keeps the original Train objects alive and unloads only the mind scene on return.
internal sealed class TrainMindSession : MonoBehaviour
{
    internal const string TrainPath = "Assets/Level/Scenes/Locations/Train.unity";
    private const string MindPath = "Assets/Level/Scenes/Gameplay/ConsciousnessScene.unity";
    private static TrainMindSession current;
    private readonly List<GameObject> suspendedRoots = new List<GameObject>();
    private readonly List<MonoBehaviour> suspendedControls = new List<MonoBehaviour>();
    private Scene train;
    private Scene mind;
    private SceneSwitch entry;
    private CharacterController player;
    private Camera playerCamera;
    private PlayerStress stress;
    private Vector3 playerPosition;
    private Quaternion playerRotation;
    private Vector3 cameraPosition;
    private Quaternion cameraRotation;
    private CursorLockMode cursorLock;
    private bool cursorVisible;
    private bool canReturn;

    internal static bool TryBegin(SceneSwitch entry, Scene train, CharacterController player,
        Camera camera, FocusPoint focus)
    {
        if (current != null || LocationTransition.IsTransitioning || player == null ||
            player.gameObject.scene != train || focus.gameObject.scene != train) return false;

        int index = focus.sceneIndex;
        if (!CanLoadMind(index))
        {
            Debug.LogError("Train mind visit requires the enabled ConsciousnessScene and a single instance of it.", focus);
            return false;
        }

        // Train is the active scene. This root stays active while its other roots are asleep.
        var session = new GameObject("Train Mind Session").AddComponent<TrainMindSession>();
        current = session;
        session.train = train;
        session.entry = entry;
        session.player = player;
        session.playerCamera = camera;
        session.StartCoroutine(session.Enter(focus, index));
        return true;
    }

    internal static bool TryReturn(Scene source)
    {
        if (current == null || current.mind != source) return false;
        if (current.canReturn)
        {
            current.canReturn = false;
            current.StartCoroutine(current.Return());
        }
        return true;
    }

    internal static bool OwnsMindScene(Scene scene) => current != null && current.mind == scene;

    private static bool CanLoadMind(int index) =>
        index >= 0 && index < SceneManager.sceneCountInBuildSettings &&
        SceneUtility.GetScenePathByBuildIndex(index) == MindPath &&
        Application.CanStreamedLevelBeLoaded(index) && !SceneManager.GetSceneByPath(MindPath).isLoaded;

    private IEnumerator Enter(FocusPoint focus, int index)
    {
        CapturePlayer();
        yield return SceneSwitch.AnimateToNPC(playerCamera, focus, entry.moveDuration, stress);
        if (focus == null || !focus.isActiveAndEnabled || playerCamera == null || player == null || !CanLoadMind(index))
        {
            Debug.LogWarning("Train mind visit was cancelled before loading ConsciousnessScene.", this);
            RestoreTrain(false);
            yield break;
        }

        foreach (var root in train.GetRootGameObjects())
        {
            if (root == gameObject || !root.activeSelf) continue;
            suspendedRoots.Add(root);
            root.SetActive(false);
        }

        SceneManager.sceneLoaded += OnMindLoaded;
        var loading = BeginMindLoad(index);
        if (loading != null) yield return loading;
        SceneManager.sceneLoaded -= OnMindLoaded;

        if (!mind.isLoaded || SceneManager.GetActiveScene() != mind)
        {
            Debug.LogError("Could not activate ConsciousnessScene. Restoring Train.", this);
            var cleanup = BeginMindUnload();
            if (cleanup != null) yield return cleanup;
            RestoreTrain(false);
            yield break;
        }

        canReturn = true;
    }

    private AsyncOperation BeginMindLoad(int index)
    {
        try { return SceneManager.LoadSceneAsync(index, LoadSceneMode.Additive); }
        catch (Exception error)
        {
            Debug.LogException(error, this);
            return null;
        }
    }

    private void OnMindLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Additive || scene.path != MindPath) return;
        mind = scene;
        // ConsciousnessPlaceholder.Start creates its camera and canvas in the active scene.
        if (SceneManager.SetActiveScene(scene))
            LockCursor();
        else
            foreach (var root in scene.GetRootGameObjects()) root.SetActive(false);
    }

    private IEnumerator Return()
    {
        if (!SceneManager.SetActiveScene(train))
        {
            Debug.LogError("Could not reactivate Train after the mind visit.", this);
            canReturn = true;
            yield break;
        }

        var unloading = BeginMindUnload();
        if (unloading != null) yield return unloading;
        if (mind.isLoaded)
        {
            SceneManager.SetActiveScene(mind);
            Debug.LogError("Could not unload ConsciousnessScene. Press U to retry.", this);
            canReturn = true;
            yield break;
        }
        RestoreTrain(true);
    }

    private AsyncOperation BeginMindUnload()
    {
        if (!mind.isLoaded) return null;
        try { return SceneManager.UnloadSceneAsync(mind); }
        catch (Exception error)
        {
            Debug.LogException(error, this);
            return null;
        }
    }

    private void CapturePlayer()
    {
        playerPosition = player.transform.position;
        playerRotation = player.transform.rotation;
        cameraPosition = playerCamera.transform.localPosition;
        cameraRotation = playerCamera.transform.localRotation;
        cursorLock = Cursor.lockState;
        cursorVisible = Cursor.visible;
        stress = player.GetComponent<PlayerStress>();
        if (stress != null) stress.BeginMindCalm();

        foreach (var control in player.GetComponents<MonoBehaviour>())
        {
            if (control == null || control == entry || control == stress || !control.enabled) continue;
            suspendedControls.Add(control);
            control.enabled = false;
        }
        // Disabling FirstPersonPlayer releases the cursor. Keep it captured for the approach.
        LockCursor();
    }

    private void RestoreTrain(bool completed)
    {
        SceneManager.SetActiveScene(train);
        if (player != null && (player.transform.position != playerPosition || player.transform.rotation != playerRotation))
        {
            bool controllerWasEnabled = player.enabled;
            player.enabled = false;
            player.transform.SetPositionAndRotation(playerPosition, playerRotation);
            player.enabled = controllerWasEnabled;
        }
        if (playerCamera != null) playerCamera.transform.SetLocalPositionAndRotation(cameraPosition, cameraRotation);

        foreach (var root in suspendedRoots)
            if (root != null) root.SetActive(true);
        if (stress != null && stress.gameObject.activeInHierarchy) stress.EndMindCalm(completed);
        foreach (var control in suspendedControls)
            if (control != null) control.enabled = true;
        if (entry != null) entry.FinishTrainMindVisit();
        if (completed)
        {
            LockCursor();
        }
        else
        {
            Cursor.lockState = cursorLock;
            Cursor.visible = cursorVisible;
        }
        current = null;
        Destroy(gameObject);
    }

    private static void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnMindLoaded;
        if (current == this) current = null;
    }
}
