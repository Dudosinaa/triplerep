using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class FinalLabResonatorSurvey
{
    internal const string Models = "Assets/Art/Models/Environment/FinalLab/k-17+transport crate";
    internal const string Output = "Logs/FinalLab/Resonator";

    public static void Run()
    {
        FinalLabInvestigationSurvey.RequireBatch();
        var scene = EditorSceneManager.OpenScene(FinalLabBuilder.ScenePath);
        var report = new StringBuilder();
        foreach (var root in scene.GetRootGameObjects())
        {
            report.AppendLine($"ROOT {root.name}");
            foreach (var node in root.GetComponentsInChildren<Transform>(true).Where(t =>
                         t.name is "ArchiveCabinet" or "ArchiveBoxes" or "ArchiveFloor" or "ArchiveEast" or "ArchiveWest" or "MainFloor" or
                         "HallEast" or "HallEntryEast" or "East_Distribution" or "Beam_Junction" or "Tesla_Base_Connector" or "Generator_Input" or
                         "Cabinet" or "TeslaCoil" or "Generator" or "04_LabUtilityTable" or "04 K-17 investigation" or "K-17 wall-mounted cable paths" ||
                         t.name.Contains("Debris") || t.TryGetComponent<Light>(out _)))
            {
                var meshes = node.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
                report.AppendLine($"{PathOf(node)} @ {node.position:F3} " + (meshes.Length == 0 ? "" : BoundsText(meshes)));
                if (node.name is "04_LabUtilityTable" or "ArchiveBoxes" or "ArchiveCabinet")
                    foreach (var part in node.GetComponentsInChildren<Collider>().Where(c => c.enabled))
                        report.AppendLine($" collider {part.name} {part.bounds.min:F3}..{part.bounds.max:F3}");
            }
        }
        foreach (string guid in AssetDatabase.FindAssets("t:GameObject", new[] { Models }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.IndexOf("/TirgamesAssets/", System.StringComparison.Ordinal) >= 0 ||
                path.IndexOf("/FreeIndustrialModels/", System.StringComparison.Ordinal) >= 0)
            {
                if (path.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase)) report.AppendLine(path);
                continue;
            }
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) continue;
            var model = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            model.transform.position = Vector3.zero;
            var meshes = model.GetComponentsInChildren<MeshRenderer>(true);
            report.AppendLine($"\n{path} [{asset.name}] {BoundsText(meshes)}; {meshes.Length} renderers, {model.GetComponentsInChildren<Collider>(true).Length} colliders");
            foreach (var mesh in meshes.Take(24))
            {
                var geom = mesh.GetComponent<MeshFilter>().sharedMesh;
                report.AppendLine($"  {PathOf(mesh.transform)} {mesh.bounds.min:F3}..{mesh.bounds.max:F3}; {Enumerable.Range(0, geom.subMeshCount).Sum(i => (long)geom.GetIndexCount(i))/3} tris; " +
                    string.Join(", ", mesh.sharedMaterials.Select(m => m == null ? "missing" : m.name + " / " + m.shader.name)));
            }
            Object.DestroyImmediate(model);
        }
        Directory.CreateDirectory(Output);
        File.WriteAllText(Output + "/survey.txt", report.ToString());
        Debug.Log("FINAL_LAB_RESONATOR_SURVEYED");
    }

    private static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;

    private static string BoundsText(Renderer[] renderers)
    {
        if (renderers.Length == 0) return "no geometry";
        var b = renderers[0].bounds;
        foreach (var r in renderers.Skip(1)) b.Encapsulate(r.bounds);
        return $"{b.min:F3}..{b.max:F3} ({b.size:F3})";
    }
}
