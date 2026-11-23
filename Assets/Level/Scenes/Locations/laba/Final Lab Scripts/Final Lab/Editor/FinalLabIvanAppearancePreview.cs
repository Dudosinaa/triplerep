using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

[InitializeOnLoad]
internal static class FinalLabIvanAppearancePreview
{
    private const string PreviewKey = "FinalLab.IvanAppearancePreview";
    private static AnimationClip[] clips;
    private static Animator actor;
    private static Camera camera;
    private static PlayableGraph graph;
    private static int action;
    private static double started;
    private static bool captured;
    private static readonly Vector3 Position = new(1.85f, 0, 8.02f);

    static FinalLabIvanAppearancePreview()
    {
        EditorApplication.playModeStateChanged += UpdatePlayMode;
        EditorApplication.update += Tick;
    }

    public static void Open()
    {
        if (Application.isBatchMode) throw new InvalidOperationException("Ivan appearance must be verified in graphical Play Mode.");
        EditorSceneManager.OpenScene(FinalLabBuilder.ScenePath);
        EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
        FinalLabGameViewSize.SetResolution(1920, 1080);
        SessionState.SetBool(PreviewKey, true);
        EditorApplication.EnterPlaymode();
    }

    private static void UpdatePlayMode(PlayModeStateChange phase)
    {
        if (!SessionState.GetBool(PreviewKey, false)) return;
        if (phase == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.EraseBool(PreviewKey);
            FinalLabGameViewSize.Restore();
        }
        if (phase == PlayModeStateChange.ExitingPlayMode)
        {
            actor = null;
            if (graph.IsValid()) graph.Destroy();
        }
        if (phase != PlayModeStateChange.EnteredPlayMode) return;
        try
        {
            var player = Object.FindFirstObjectByType<FirstPersonPlayer>();
            player.enabled = false;
            camera = player.GetComponentInChildren<Camera>();
            camera.fieldOfView = 46;
            var model = Object.Instantiate(FinalLabIvanAppearance.Load<GameObject>(FinalLabIvanAppearance.PrefabPath), Position, Quaternion.identity);
            actor = model.GetComponent<Animator>();
            actor.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (!actor.isHuman || actor.GetComponentsInChildren<SkinnedMeshRenderer>().SelectMany(mesh => mesh.sharedMaterials)
                .Any(mat => !mat.name.StartsWith("Ivan_", StringComparison.Ordinal)))
                throw new InvalidOperationException("Incorrect appearance prefab in Play Mode.");
            clips = FinalLabIvanAppearance.ReadClips();
            action = 0;
            SetClip();
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            EditorApplication.ExitPlaymode();
        }
    }

    private static void SetClip()
    {
        if (graph.IsValid()) graph.Destroy();
        graph = PlayableGraph.Create("Ivan material and animation preview");
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        var output = AnimationPlayableOutput.Create(graph, "Ivan", actor);
        output.SetSourcePlayable(AnimationClipPlayable.Create(graph, clips[action]));
        graph.Play();
        var offset = action % 2 == 0 ? new Vector3(.25f, 1.33f, 2.85f) : new Vector3(-.62f, 1.38f, 2.70f);
        camera.transform.position = Position + offset;
        camera.transform.rotation = Quaternion.LookRotation(Position + Vector3.up * .94f - camera.transform.position);
        started = EditorApplication.timeSinceStartup;
        captured = false;
    }

    private static void Tick()
    {
        if (actor == null || !EditorApplication.isPlaying) return;
        var elapsed = EditorApplication.timeSinceStartup - started;
        if (!captured && elapsed > Math.Min(.78, clips[action].length * .55))
        {
            captured = true;
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(FinalLabIvanAppearance.LogDirectory + $"/Ivan-appearance-{action:D2}.png"));
            Debug.Log($"Ivan visual preview {action}: {FinalLabIvanAppearance.Takes[action]}, {Screen.width}x{Screen.height}.");
        }
        if (elapsed < Math.Max(1.35, Math.Min(clips[action].length + .1, 2.1))) return;
        if (++action < clips.Length)
        {
            SetClip();
            return;
        }
        Debug.Log("FINAL_LAB_IVAN_APPEARANCE_GUI_CAPTURED");
        EditorApplication.ExitPlaymode();
        actor = null;
    }
}
