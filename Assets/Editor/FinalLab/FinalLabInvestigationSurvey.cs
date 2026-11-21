using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FinalLabInvestigationSurvey
{
    internal const string Logs = "Logs/FinalLab/Investigation";

    public static void Run()
    {
        RequireBatch();
        var scene = EditorSceneManager.OpenScene(FinalLabBuilder.ScenePath);
        var report = new List<string>();
        foreach (var root in scene.GetRootGameObjects())
        {
            report.Add("ROOT " + root.name);
            foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
            {
                if (item.childCount == 0 && item.GetComponent<Renderer>() == null) continue;
                string source = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(item.gameObject);
                if (item.GetComponent<Renderer>() != null && !PrefabUtility.IsAnyPrefabInstanceRoot(item.gameObject) &&
                    !new[] { "floor", "wall", "shelf", "desk", "container", "crate", "case", "box", "recorder", "k17" }
                        .Any(x => item.name.Contains(x, StringComparison.OrdinalIgnoreCase))) continue;
                if (item.childCount > 0 || item.name.IndexOf("box", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var rs = item.GetComponentsInChildren<MeshRenderer>().Where(r => r.enabled).ToArray();
                    if (rs.Length == 0) continue;
                    var bounds = FinalLabPropsSurvey.Bounds(rs);
                    report.Add($"{Hierarchy(item)} | {bounds.min:F3} .. {bounds.max:F3} | {source}");
                }
            }
        }
        Directory.CreateDirectory(Logs);
        File.WriteAllLines(Path.Combine(Logs, "survey.txt"), report);
        Debug.Log($"FINAL_LAB_INVESTIGATION_SURVEY {report.Count} observations.");
    }

    internal static void RequireBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run FinalLab investigation tools in Unity Batch Mode.");
    }

    private static string Hierarchy(Transform item)
    {
        string path = item.name;
        for (var parent = item.parent; parent != null; parent = parent.parent) path = parent.name + "/" + path;
        return path;
    }
}
