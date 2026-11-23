using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class FinalLabPropsSurvey
{
    internal const string Lab = "Assets/Art/Models/Environment/FinalLab";
    internal const string Hospital = "Assets/Dnk_Dev/HospitalHorrorPack";
    internal const string Logs = "Logs/FinalLab/Decoration";

    public static void Run()
    {
        EditorSceneManager.OpenScene(FinalLabBuilder.ScenePath);
        Physics.SyncTransforms();
        var root = GameObject.Find(FinalLabBuilder.RootName);
        var lines = new List<string>();
        foreach (var b in root.GetComponentsInChildren<BoxCollider>())
            lines.Add($"ARCH {b.name} {Format(b.bounds)}");
        foreach (var light in root.GetComponentsInChildren<Light>())
            lines.Add($"LIGHT {light.name} {light.transform.position:F3}");

        var paths = AssetDatabase.GetAllAssetPaths().Where(p =>
            (p.StartsWith(Lab + "/tables + etc/") || p.StartsWith(Lab + "/Machinery/") || p.StartsWith(Lab + "/Cables/")) &&
            p.EndsWith(".glb") || p.StartsWith("Assets/FreeLabAssets/Prefabs/") && p.EndsWith(".prefab") ||
            p == "Assets/FreeLabAssets/Models/Funnel.fbx" ||
            p.StartsWith(Hospital + "/Prefab/") && new[] { "/P_Bed_01.prefab", "/P_BedBedding.prefab",
                "/P_Med_box_01.prefab", "/P_Lamp.prefab" }.Any(p.EndsWith));
        foreach (var path in paths.OrderBy(p => p))
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null)
            {
                lines.Add("MISSING " + path);
                continue;
            }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            var renderers = instance.GetComponentsInChildren<MeshRenderer>();
            lines.Add($"MODEL {Path.GetFileNameWithoutExtension(path)} | {PrefabUtility.GetPrefabAssetType(asset)} | meshes={renderers.Length} | " +
                (renderers.Length > 0 ? Format(Bounds(renderers)) : "NO RENDERERS"));
            foreach (var r in renderers.Take(30))
            {
                string materials = string.Join(", ", r.sharedMaterials.Select(m => m == null ? "MISSING" :
                    m.name + "@" + m.shader.name + "#" + m.GetTexturePropertyNames().Count(t => m.GetTexture(t) != null)));
                lines.Add($"    {r.name} {Format(r.bounds)} | {materials}");
            }
            if (renderers.Length > 30) lines.Add("    ...");
            Object.DestroyImmediate(instance);
        }
        Directory.CreateDirectory(Logs);
        File.WriteAllLines(Path.Combine(Logs, "survey.txt"), lines);
        Debug.Log("FINAL_LAB_PROPS_SURVEY " + lines.Count + " observations");
    }

    internal static Bounds Bounds(IEnumerable<Renderer> renderers)
    {
        using var iterator = renderers.GetEnumerator();
        if (!iterator.MoveNext()) throw new InvalidOperationException("The asset has no visible geometry.");
        var bounds = iterator.Current.bounds;
        while (iterator.MoveNext()) bounds.Encapsulate(iterator.Current.bounds);
        return bounds;
    }

    private static string Format(Bounds b) => $"{b.min:F3}..{b.max:F3} size={b.size:F3}";
}
