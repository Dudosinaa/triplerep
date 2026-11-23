using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// A visit keeps the original Train objects alive and unloads only the mind scene on return.
internal sealed class TrainMindSession : MonoBehaviour
{
    internal const string TrainPath = "Assets/Level/Scenes/Locations/Train.unity";
    private const float TurnFraction = 0.25f;
    private const float MaxApproachDistance = 0.35f;
    private const float MinFocusDistance = 0.5f;
    private const float CameraRadius = 0.12f;
    private const float CameraPadding = 0.02f;
    private static TrainMindSession current;
    private readonly List<GameObject> suspendedRoots = new List<GameObject>();
    private readonly List<MonoBehaviour> suspendedControls = new List<MonoBehaviour>();
    private readonly RaycastHit[] cameraHits = new RaycastHit[16];
    private readonly Collider[] cameraOverlaps = new Collider[16];
    private Scene train;
    private Scene mind;
    private string mindPath;
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
    private bool returnOnU;
    private TransitionVignette vignette;

    internal static bool CanBegin(Scene train, CharacterController player, Camera camera, FocusPoint focus) =>
        current == null && !LocationTransition.IsTransitioning &&
        player != null && player.gameObject.scene == train &&
        camera != null && camera.isActiveAndEnabled &&
        focus != null && focus.isActiveAndEnabled && focus.gameObject.scene == train &&
        focus.GetComponentInParent<NpcDialogue>() is { isActiveAndEnabled: true, HasCompletedConversation: true } &&
        GetAvailableMindPath(focus.sceneIndex, train) != null;

    internal static bool TryBegin(SceneSwitch entry, Scene train, CharacterController player,
        Camera camera, FocusPoint focus)
    {
        if (!CanBegin(train, player, camera, focus)) return false;

        // Train is the active scene. This root stays active while its other roots are asleep.
        var session = new GameObject("Train Mind Session").AddComponent<TrainMindSession>();
        current = session;
        session.train = train;
        session.mindPath = SceneUtility.GetScenePathByBuildIndex(focus.sceneIndex);
        session.entry = entry;
        session.player = player;
        session.playerCamera = camera;
        session.returnOnU = focus.returnOnU;
        session.StartCoroutine(session.Enter(focus, focus.sceneIndex));
        return true;
    }

    private void Update()
    {
        if (GameplayPause.BlocksGameplayInput) return;
        if (canReturn && returnOnU && Keyboard.current?.uKey.wasPressedThisFrame == true)
            TryReturn(mind);
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

    internal static bool IsTransitioning => current != null && !current.canReturn;

    internal static bool OwnsMindScene(Scene scene) => current != null && current.mind == scene;

    private static string GetAvailableMindPath(int index, Scene train)
    {
        if (!SceneSwitch.CanLoadSceneIndex(index)) return null;

        string path = SceneUtility.GetScenePathByBuildIndex(index);
        return string.IsNullOrEmpty(path) || path == train.path || SceneManager.GetSceneByPath(path).isLoaded
            ? null : path;
    }

    private IEnumerator Enter(FocusPoint focus, int index)
    {
        CapturePlayer();
        vignette = new TransitionVignette(transform);
        yield return Approach(focus, index);
        // Draw the closed vignette once before the scene switch, even on a fast load.
        yield return null;

        if (!CanEnterMind(focus, index) || GetAvailableMindPath(index, train) != mindPath)
        {
            Debug.LogWarning($"Train mind visit was cancelled before loading '{mindPath}'.", this);
            yield return RewindApproach();
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
            Debug.LogError($"Could not activate '{mindPath}'. Restoring Train.", this);
            var cleanup = BeginMindUnload();
            if (cleanup != null) yield return cleanup;
            RestoreTrain(false);
            yield break;
        }

        yield return null;
        for (float elapsed = 0f; elapsed < 0.4f; elapsed += Time.unscaledDeltaTime)
        {
            vignette.SetProgress(1f - elapsed / 0.4f, entry.MindVignetteColor);
            yield return null;
        }
        vignette.SetProgress(0f, entry.MindVignetteColor);
        canReturn = true;
    }

    private bool CanEnterMind(FocusPoint focus, int index) =>
        focus != null && focus.isActiveAndEnabled && focus.gameObject.scene == train && focus.sceneIndex == index &&
        entry != null && player != null && playerCamera != null && playerCamera.isActiveAndEnabled &&
        SceneManager.GetActiveScene() == train;

    private IEnumerator Approach(FocusPoint focus, int index)
    {
        if (!CanEnterMind(focus, index)) yield break;

        var view = playerCamera.transform;
        var startPosition = view.position;
        var startRotation = view.rotation;
        Vector3 toFocus = focus.transform.position - startPosition;
        Vector3 direction = toFocus.normalized;
        Quaternion lookAtFocus = direction == Vector3.zero ? startRotation : Quaternion.LookRotation(direction);
        float maxTravel = Mathf.Clamp(toFocus.magnitude - MinFocusDistance, 0f, MaxApproachDistance);
        float duration = Mathf.Max(0.01f, entry.moveDuration);
        float previousSlide = 0f;
        float travelled = 0f;

        for (float elapsed = 0f; CanEnterMind(focus, index); elapsed += Time.deltaTime)
        {
            float progress = Mathf.Clamp01(elapsed / duration);
            float turn = Mathf.SmoothStep(0f, 1f, progress / TurnFraction);
            float approach = Mathf.InverseLerp(TurnFraction, 1f, progress);
            float slide = Mathf.SmoothStep(0f, 1f, approach);
            float step = (slide - previousSlide) * maxTravel;
            if (step > 0f)
                travelled += SafeCameraStep(startPosition + direction * travelled, direction, step);
            previousSlide = slide;

            view.SetPositionAndRotation(startPosition + direction * travelled,
                Quaternion.Slerp(startRotation, lookAtFocus, turn));
            vignette.SetProgress(approach, entry.MindVignetteColor);
            if (stress != null) stress.SetMindCalmProgress(progress);
            if (progress >= 1f) yield break;
            yield return null;
        }
    }

    private float SafeCameraStep(Vector3 position, Vector3 direction, float distance)
    {
        const QueryTriggerInteraction triggers = QueryTriggerInteraction.Collide;
        int overlaps = Physics.OverlapSphereNonAlloc(position, CameraRadius, cameraOverlaps, ~0, triggers);
        // A full buffer could omit a nearby obstacle.
        if (overlaps == cameraOverlaps.Length) return 0f;
        for (int i = 0; i < overlaps; i++)
            if (BlocksCamera(cameraOverlaps[i])) return 0f;

        int hits = Physics.SphereCastNonAlloc(position, CameraRadius, direction, cameraHits, distance + CameraPadding, ~0, triggers);
        if (hits == cameraHits.Length) return 0f;
        for (int i = 0; i < hits; i++)
            if (BlocksCamera(cameraHits[i].collider))
                distance = Mathf.Min(distance, Mathf.Max(0f, cameraHits[i].distance - CameraPadding));
        return distance;
    }

    private bool BlocksCamera(Collider hit) =>
        !hit.transform.IsChildOf(player.transform) &&
        (!hit.isTrigger || hit.GetComponentInParent<NpcDialogue>() != null);

    private IEnumerator RewindApproach()
    {
        if (playerCamera == null || entry == null) yield break;
        var view = playerCamera.transform;
        Vector3 startPosition = view.localPosition;
        Quaternion startRotation = view.localRotation;
        float startVignette = vignette.Progress;
        for (float elapsed = 0f; elapsed < 0.25f && view != null; elapsed += Time.unscaledDeltaTime)
        {
            float progress = Mathf.SmoothStep(0f, 1f, elapsed / 0.25f);
            view.SetLocalPositionAndRotation(Vector3.Lerp(startPosition, cameraPosition, progress),
                Quaternion.Slerp(startRotation, cameraRotation, progress));
            vignette.SetProgress(startVignette * (1f - progress), entry.MindVignetteColor);
            yield return null;
        }
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
        if (mode != LoadSceneMode.Additive || scene.path != mindPath) return;
        mind = scene;
        if (SceneManager.SetActiveScene(scene))
        {
            SceneEntryCheckpoint.ReturnPath = train.path;
            SceneEntryCheckpoint.Enter(scene);
        }
        else
        {
            foreach (var root in scene.GetRootGameObjects()) root.SetActive(false);
        }
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
            Debug.LogError($"Could not unload '{mindPath}'.", this);
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

        vignette?.Hide();
        foreach (var root in suspendedRoots)
            if (root != null) root.SetActive(true);
        if (stress != null && stress.gameObject.activeInHierarchy) stress.EndMindCalm(completed);
        foreach (var control in suspendedControls)
            if (control != null) control.enabled = true;
        if (entry != null) entry.FinishTrainMindVisit();
        if (completed)
        {
            SceneEntryCheckpoint.ReturnPath = null;
            SceneEntryCheckpoint.Record(train);
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
        vignette?.Release();
        SceneManager.sceneLoaded -= OnMindLoaded;
        if (current == this) current = null;
    }

    private sealed class TransitionVignette
    {
        private readonly Canvas canvas;
        private readonly RawImage edge;
        private readonly Image cover;
        private readonly Texture2D radialMask;
        public float Progress { get; private set; }

        public TransitionVignette(Transform parent)
        {
            // An overlay survives the handoff between cameras, including cameras with post-processing off.
            var root = new GameObject("Mind Transition Canvas", typeof(RectTransform), typeof(Canvas));
            root.transform.SetParent(parent, false);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            var layers = SortingLayer.layers;
            if (layers.Length > 0) canvas.sortingLayerID = layers[layers.Length - 1].id;
            canvas.enabled = false;

            radialMask = CreateRadialMask();
            edge = CreateLayer<RawImage>("Mind Vignette", root.transform);
            edge.texture = radialMask;
            cover = CreateLayer<Image>("Mind Vignette Cover", root.transform);
        }

        public void SetProgress(float progress, Color color)
        {
            Progress = progress;
            canvas.enabled = progress > 0f;
            float size = Mathf.Lerp(1f, 3.5f, progress);
            float offset = (1f - size) * 0.5f;
            edge.uvRect = new Rect(offset, offset, size, size);
            color.a = Mathf.SmoothStep(0f, 1f, progress * 2f);
            edge.color = color;
            // Close the remaining center only near the end of the approach.
            color.a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.66f, 1f, progress));
            cover.color = color;
        }

        public void Hide() => canvas.enabled = false;
        public void Release() => Destroy(radialMask);

        private static T CreateLayer<T>(string name, Transform parent) where T : Graphic
        {
            var layer = new GameObject(name, typeof(RectTransform), typeof(T)).GetComponent<T>();
            layer.transform.SetParent(parent, false);
            layer.rectTransform.anchorMin = Vector2.zero;
            layer.rectTransform.anchorMax = Vector2.one;
            layer.rectTransform.offsetMin = Vector2.zero;
            layer.rectTransform.offsetMax = Vector2.zero;
            layer.raycastTarget = false;
            return layer;
        }

        private static Texture2D CreateRadialMask()
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float radius = new Vector2(2f * x / (size - 1) - 1f, 2f * y / (size - 1) - 1f).magnitude;
                float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.16f, 1f, radius));
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
