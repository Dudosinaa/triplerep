using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// This service alone survives a location load. Player objects remain owned by their scenes.
public sealed class LocationTransition : MonoBehaviour
{
    private static LocationTransition instance;
    private readonly List<PlayerSuspension> suspensions = new List<PlayerSuspension>();
    private CanvasGroup curtain;
    private bool busy;
    private string destination;
    public static bool IsTransitioning => instance != null && instance.busy;

    public static bool TryBegin(FirstPersonPlayer player, string scenePath, float fadeOut,
        float fadeIn, UnityEngine.Object context = null)
    {
        if (IsTransitioning || player == null || !player.isActiveAndEnabled) return false;
        if (!TryGetBuildIndex(scenePath, out _))
        {
            Debug.LogError($"Location transition: scene '{scenePath}' is unavailable. " +
                "Add and enable it in the active Build Profiles scene list.", context);
            return false;
        }

        var service = new GameObject("Location Transition").AddComponent<LocationTransition>();
        instance = service;
        service.busy = true; // Claim the lock before a second collider/zone can enter.
        service.destination = scenePath;
        DontDestroyOnLoad(service.gameObject);
        service.CreateCurtain();
        SceneManager.sceneLoaded += service.OnSceneLoaded;
        service.StartCoroutine(service.Run(player, SafeDuration(fadeOut), SafeDuration(fadeIn)));
        return true;
    }

    public static bool TryGetBuildIndex(string scenePath, out int index)
    {
        // Runtime indices reflect the effective build profile; disabled scenes are not loadable.
        // Do not use a Resources lookup or the existence of a .unity file as availability.
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            if (string.Equals(SceneUtility.GetScenePathByBuildIndex(i), scenePath,
                    StringComparison.Ordinal) && Application.CanStreamedLevelBeLoaded(i))
            {
                index = i;
                return true;
            }
        }
        index = -1;
        return false;
    }

    private static float SafeDuration(float duration) =>
        float.IsNaN(duration) || float.IsInfinity(duration) ? 0f : Mathf.Max(0f, duration);

    private void CreateCurtain()
    {
        var overlay = new GameObject("Black Curtain", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasGroup), typeof(GraphicRaycaster));
        overlay.transform.SetParent(transform, false);
        var canvas = overlay.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;
        // Also cover UI using a sorting layer above Default.
        var layers = SortingLayer.layers;
        if (layers.Length > 0) canvas.sortingLayerID = layers[layers.Length - 1].id;
        curtain = overlay.GetComponent<CanvasGroup>();
        curtain.alpha = 0f;
        curtain.blocksRaycasts = true;
        curtain.interactable = true;
        var black = new GameObject("Black", typeof(RectTransform), typeof(Image));
        black.transform.SetParent(overlay.transform, false);
        var image = black.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = true;
        image.rectTransform.anchorMin = Vector2.zero;
        image.rectTransform.anchorMax = Vector2.one;
        image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
    }

    private IEnumerator Run(FirstPersonPlayer player, float fadeOut, float fadeIn)
    {
        // Drive the iterator here so exceptions during loading also restore the screen and input.
        var transition = Travel(player, fadeOut, fadeIn);
        Exception failure = null;
        try
        {
            while (true)
            {
                bool more;
                try { more = transition.MoveNext(); }
                catch (Exception exception) { failure = exception; break; }
                if (!more) break;
                yield return transition.Current;
            }
            if (failure != null)
            {
                Debug.LogError($"Location transition to '{destination}' failed: {failure.Message}. " +
                    "Restoring the image and player control.", this);
                var recovery = Fade(0f, fadeIn);
                while (recovery.MoveNext()) yield return recovery.Current;
            }
        }
        finally
        {
            (transition as IDisposable)?.Dispose();
            Release();
            Destroy(gameObject);
        }
    }

    private IEnumerator Travel(FirstPersonPlayer player, float fadeOut, float fadeIn)
    {
        Suspend(player);
        var fade = Fade(1f, fadeOut);
        while (fade.MoveNext()) yield return fade.Current;
        // Render at least one fully black frame even with a zero duration.
        yield return null;
        if (!TryGetBuildIndex(destination, out int index))
            throw new InvalidOperationException("The destination is no longer in the active build scene list");
        var load = SceneManager.LoadSceneAsync(index, LoadSceneMode.Single);
        if (load == null) throw new InvalidOperationException("Unity did not create a scene loading operation");
        while (!load.isDone) yield return null;
        if (SceneManager.GetActiveScene().path != destination)
            throw new InvalidOperationException("The loaded scene does not match the destination");
        // sceneLoaded suspends authored players before Start; keep black through initialization.
        yield return null;
        fade = Fade(0f, fadeIn);
        while (fade.MoveNext()) yield return fade.Current;
    }

    private IEnumerator Fade(float target, float duration)
    {
        float start = curtain.alpha;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            curtain.alpha = Mathf.Lerp(start, target, elapsed / duration);
            yield return null;
        }
        curtain.alpha = target;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!busy || scene.path != destination) return;
        foreach (var root in scene.GetRootGameObjects())
            foreach (var player in root.GetComponentsInChildren<FirstPersonPlayer>(true))
                if (player.gameObject.activeInHierarchy) Suspend(player);
    }

    private void Suspend(FirstPersonPlayer player) => suspensions.Add(new PlayerSuspension(player));

    private void Release()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        foreach (var suspension in suspensions) suspension.Restore();
        suspensions.Clear();
        if (curtain != null) curtain.alpha = 0f;
        busy = false;
        if (instance == this) instance = null;
    }

    private void OnDestroy() => Release();

    private sealed class PlayerSuspension
    {
        private readonly List<Behaviour> enabledControls = new List<Behaviour>();
        private readonly FirstPersonPlayer player;
        private readonly CursorLockMode cursorLock;
        private readonly bool cursorVisible;

        public PlayerSuspension(FirstPersonPlayer target)
        {
            player = target;
            // Closing a modal may re-enable movement/other interactions in OnDisable.
            // Close those first, then suspend the resulting normal controls.
            Disable(target.GetComponent<SmokingController>());
            Disable(target.GetComponent<DialogueController>());
            Disable(target.GetComponent<ItemInspector>());
            Disable(target.GetComponent<DocumentReader>());
            cursorLock = Cursor.lockState;
            cursorVisible = Cursor.visible;
            Disable(target.GetComponent<PistolController>());
            Disable(target.GetComponent<SceneSwitch>());
            Disable(target.GetComponent<PlayerStress>());
            Disable(target.GetComponent<PlayerMovement>());
            Disable(target);
        }

        private void Disable(Behaviour control)
        {
            if (control == null || !control.enabled) return;
            enabledControls.Add(control);
            control.enabled = false;
        }

        public void Restore()
        {
            // The old scene's player has been destroyed by LoadSceneMode.Single.
            if (player == null) return;
            foreach (var control in enabledControls)
                if (control != null) control.enabled = true;
            Cursor.lockState = cursorLock;
            Cursor.visible = cursorVisible;
            enabledControls.Clear();
        }
    }
}
