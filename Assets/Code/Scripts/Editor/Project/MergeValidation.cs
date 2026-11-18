using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.InputSystem.UI;

public static class MergeValidation
{
    private static readonly string[] Scenes = {
        "Assets/Level/Scenes/Locations/Metro.unity",
        "Assets/Level/Scenes/Locations/Home.unity",
        "Assets/Level/Scenes/Gameplay/ConsciousnessScene.unity",
        "Assets/Level/Scenes/Gameplay/CarxDemo.unity",
        "Assets/Level/Scenes/Gameplay/StressDemo.unity"
    };
    [Serializable] private sealed class ValidationReport
    {
        public string unityVersion;
        public string[] checks;
        public string[] manualChecks;
    }
    [MenuItem("Tools/Merge/Validate project")]
    public static void Validate()
    {
        var checks = new List<string>();
        var buildScenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        Require(buildScenes.SequenceEqual(Scenes), "Build scene indices have changed; update FocusPoint and location exits.");
        checks.Add("Build scene order: Metro=0, Home=1, Consciousness=2, CarxDemo=3, StressDemo=4");
        var guids = new HashSet<string>();
        foreach (var scene in Scenes)
        {
            string guid = AssetDatabase.AssetPathToGUID(scene);
            Require(!string.IsNullOrEmpty(guid) && guids.Add(guid), "Missing or duplicate scene GUID: " + scene);
        }
        checks.Add("Every build scene has a distinct GUID");
        PlayerPrefabBuilder.Validate();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabBuilder.PrefabPath);
        Require(prefab != null, "Player prefab could not be imported.");
        foreach (var transform in prefab.GetComponentsInChildren<Transform>(true))
            Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) == 0,
                "Missing script in gameplay prefab: " + transform.name);
        Require(prefab.GetComponentInChildren<FirstPersonPlayer>(true) != null, "FirstPersonPlayer missing");
        Require(prefab.GetComponentInChildren<DocumentReader>(true) != null, "DocumentReader missing");
        Require(prefab.GetComponentInChildren<ItemInspector>(true) != null, "ItemInspector missing");
        Require(prefab.GetComponentInChildren<DialogueController>(true) != null, "DialogueController missing");
        Require(prefab.GetComponentInChildren<PistolController>(true) != null, "PistolController missing");
        Require(prefab.GetComponentInChildren<InputSystemUIInputModule>(true) != null, "New UI input module missing");
        Require(Resources.Load<AudioClip>("MergeStressSound") != null, "Stress audio missing");
        checks.Add("Standalone player prefab, scripts, UI input and stress audio imported; no test content");
        var projectRoot = Directory.GetParent(Application.dataPath).FullName;
        Require(File.ReadAllText(Path.Combine(projectRoot, "ProjectSettings/ProjectSettings.asset")).Contains("activeInputHandler: 1"),
            "Only Input System must be enabled");
        Require(File.Exists(Path.Combine(Application.streamingAssetsPath, "Smoking/model.gltf")), "Smoking glTF missing");
        foreach (var file in Directory.GetFiles(Application.dataPath, "*", SearchOption.AllDirectories))
        {
            if (new FileInfo(file).Length > 2048) continue;
            Require(!File.ReadAllText(file).StartsWith("version https://git-lfs.github.com/spec/v1"),
                "Unresolved LFS pointer: " + file);
        }
        checks.Add("Input System only; build-safe smoking source; no LFS pointers in Assets");
        var report = new ValidationReport {
            unityVersion = Application.unityVersion,
            checks = checks.ToArray(),
            manualChecks = new[] {
                "Play Metro and Home: floor collisions, spawn position, lighting and camera",
                "In CarxDemo or with scene-authored interaction objects: read, inspect, talk and resume movement",
                "Fire, aim, reload, holster; in CarxDemo hit targets; weapon must hide during other actions",
                "Space/minus change stress; Y at stress >= 40 plays smoking and recovers stress",
                "T at a scene-authored NPC with FocusPoint opens consciousness; U returns to the originating scene",
                "Enter a LocationTransitionZone with a manually placed player: fade, async load, fade in; repeat in the Windows build"
            }
        };
        var log = Path.Combine(projectRoot, "Logs/Merge");
        Directory.CreateDirectory(log);
        File.WriteAllText(Path.Combine(log, "unity-validation.json"), JsonUtility.ToJson(report, true));
        Debug.Log("MERGE_VALIDATION_PASS: " + checks.Count + " checks. Play Mode and build checks remain manual.");
    }
    [MenuItem("Tools/Merge/Build Windows x64")]
    public static void BuildWindows()
    {
        Validate();
        var projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string output = Path.Combine(projectRoot, "Builds/Windows/KaramazovTrain.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = Scenes,
            locationPathName = output,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        });
        if (result.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException("Windows build failed: " + result.summary.result);
        Debug.Log("MERGE_BUILD_PASS: " + output);
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
