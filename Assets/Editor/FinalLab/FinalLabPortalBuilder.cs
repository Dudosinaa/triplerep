using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class FinalLabPortalBuilder
{
    internal const string Logs = "Logs/FinalLab/Portals";
    internal const string ControlDoorPath = "Assets/Art/Models/Environment/FinalLab/door/door.glb";
    internal const string FatherDoorPath = "Assets/Art/Models/Environment/FinalLab/door/door___old___metal_-_14mb.glb";
    internal const string RootName = "FinalLab_FittedPortals";

    public static void Survey()
    {
        FinalLabInvestigationSurvey.RequireBatch();
        var scene = EditorSceneManager.OpenScene(FinalLabBuilder.ScenePath);
        var environment = scene.GetRootGameObjects().Single(o => o.name == "FinalLab_Environment").transform;
        var notes = new List<string>();
        foreach (string room in new[] { "04_Ivan_ControlRoom", "05_Father_Containment" })
        {
            var partition = environment.Find(room + "/HallPartition");
            foreach (var renderer in partition.GetComponentsInChildren<MeshRenderer>())
                notes.Add($"{room}/{renderer.name} {BoundsText(renderer.bounds)}");
        }
        foreach (string path in new[] { ControlDoorPath, FatherDoorPath })
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            notes.Add(path);
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var material = renderer.sharedMaterial;
                notes.Add($"  {renderer.name} ({renderer.transform.parent.name}) {BoundsText(renderer.bounds)}, " +
                    $"{material.name} | {material.shader.name} | {string.Join(", ", material.GetTexturePropertyNames().Where(p => material.GetTexture(p) != null))}");
            }
            Object.DestroyImmediate(root);
        }
        Directory.CreateDirectory(Logs);
        File.WriteAllLines(Path.Combine(Logs, "survey.txt"), notes);
        Debug.Log("FINAL_LAB_PORTALS_SURVEY");
    }

    private static string BoundsText(Bounds bounds) =>
        $"{bounds.min:F3} .. {bounds.max:F3} ({bounds.size:F3})";
}
