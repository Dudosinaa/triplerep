using System;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class SceneEntryCheckpoint
{
    internal const string MainMenuPath = "Assets/Level/Scenes/MainMenu.unity";
    private const string HomePath = "Assets/Level/Scenes/Locations/Home.unity";
    private const string Locations = "Assets/Level/Scenes/Locations/";
    private const string SaveKey = "Story.SceneEntry";

    [Flags]
    internal enum TrainMemory
    {
        Petrovich = 1 << 0,
        Michael = 1 << 1,
        Innokenty = 1 << 2
    }

    private const TrainMemory RequiredTrainMemories = TrainMemory.Petrovich | TrainMemory.Michael | TrainMemory.Innokenty;
    private static TrainMemory completedTrainMemories;
    private static bool startingNewGame;

    [Serializable]
    private sealed class Entry
    {
        public string scenePath;
        public string returnPath;
        public TrainMemory completedTrainMemories;
    }

    internal static string ReturnPath { get; set; }

    internal static bool AllTrainMemoriesCompleted =>
        (completedTrainMemories & RequiredTrainMemories) == RequiredTrainMemories;

    internal static void CompleteTrainMemory(TrainMemory memory, Scene scene)
    {
        if ((completedTrainMemories & memory) == memory) return;
        completedTrainMemories |= memory;
        Record(scene);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        ReturnPath = null;
        completedTrainMemories = 0;
        startingNewGame = false;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    internal static bool HasCheckpoint => TryReadCheckpoint(out _, out _);

    private static bool TryReadCheckpoint(out Entry entry, out int index)
    {
        entry = null;
        index = -1;
        string json = PlayerPrefs.GetString(SaveKey);
        if (string.IsNullOrEmpty(json)) return false;

        try { entry = JsonUtility.FromJson<Entry>(json); }
        catch (ArgumentException) { return false; }

        return entry != null && IsGameplayPath(entry.scenePath) &&
            LocationTransition.TryGetBuildIndex(entry.scenePath, out index);
    }

    internal static bool TryContinue()
    {
        if (!TryReadCheckpoint(out Entry entry, out int index)) return false;

        ReturnPath = entry.returnPath;
        completedTrainMemories = entry.completedTrainMemories;
        return SceneManager.LoadSceneAsync(index) != null;
    }

    internal static bool TryStartNewGame()
    {
        if (!LocationTransition.TryGetBuildIndex(HomePath, out int index)) return false;

        startingNewGame = true;
        AsyncOperation load = null;
        try
        {
            load = SceneManager.LoadSceneAsync(index);
            return load != null;
        }
        finally
        {
            if (load == null) startingNewGame = false;
        }
    }

    internal static int GetReturnIndex(int fallback) =>
        IsGameplayPath(ReturnPath) && LocationTransition.TryGetBuildIndex(ReturnPath, out int index)
            ? index : fallback;

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        if (startingNewGame && scene.path == HomePath)
        {
            completedTrainMemories = 0;
            PlayerPrefs.DeleteKey(RadioMemoryPuzzle.ClueKey);
            PlayerPrefs.DeleteKey(CargoPuzzleTerminal.WaybillKey);
        }
        startingNewGame = false;
        if (!IsMind(scene.path)) ReturnPath = null;
        Enter(scene);
    }

    // A fresh load recreates the authored spawn, intro and scene-local state.
    internal static bool Record(Scene scene)
    {
        if (!IsGameplayScene(scene)) return false;
        var entry = new Entry
        {
            scenePath = scene.path,
            returnPath = IsMind(scene.path) ? ReturnPath : null,
            completedTrainMemories = completedTrainMemories
        };
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(entry));
        PlayerPrefs.Save();
        return true;
    }

    internal static void Enter(Scene scene)
    {
        if (!Record(scene)) return;
        var root = new GameObject("Gameplay Pause");
        SceneManager.MoveGameObjectToScene(root, scene);
        root.AddComponent<GameplayPause>();
    }

    internal static bool IsGameplayScene(Scene scene) =>
        IsGameplayPath(scene.path) && LocationTransition.TryGetBuildIndex(scene.path, out _);

    private static bool IsGameplayPath(string path) => path?.StartsWith(Locations, StringComparison.Ordinal) == true;
    private static bool IsMind(string path) => path?.StartsWith(Locations + "MindsScenes/", StringComparison.Ordinal) == true;
}
