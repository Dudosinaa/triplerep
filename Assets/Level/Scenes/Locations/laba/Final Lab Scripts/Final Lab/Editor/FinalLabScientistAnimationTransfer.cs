using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

internal static partial class FinalLabScientistAnimationTransfer
{
    private const string SourcePath = "Assets/Art/Animations/Laba/Иван грустно стоит(new model).fbx";
    private const string TargetPath = "Assets/Art/Animations/Laba/scientist.glb";
    private const string LogDirectory = "Logs/FinalLab/ScientistAnimation";

    public static void Survey()
    {
        FinalLabInvestigationSurvey.RequireBatch();
        var scene = EditorSceneManager.OpenScene(FinalLabBuilder.ScenePath);
        var roots = scene.GetRootGameObjects();
        var info = new StringBuilder();
        foreach (string path in new[] { SourcePath, TargetPath })
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var instance = roots.Single(root => PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root) == path);
            info.AppendLine($"\n{path}\nscene object {instance.name} T{instance.transform.position:F5} R{instance.transform.eulerAngles:F5} S{instance.transform.lossyScale:F5}");
            var rig = instance.GetComponentInChildren<Animator>(true);
            var avatar = rig != null ? rig.avatar : null;
            info.AppendLine(rig == null ? "No Animator" : $"Animator: {rig.name}; avatar {avatar}; controller {rig.runtimeAnimatorController}");
            foreach (var skin in asset.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                info.AppendLine($"Skin: {skin.name}, {skin.bones.Length} bones, root {skin.rootBone?.name}, {skin.sharedMesh.vertexCount} vertices, " +
                    string.Join(", ", skin.sharedMaterials.Select(m => m != null ? m.name : "missing")));
            var bones = asset.GetComponentsInChildren<SkinnedMeshRenderer>(true).SelectMany(skin => skin.bones).Distinct().Where(bone => bone != null);
            foreach (var bone in bones)
                info.AppendLine($"{bone.name} <- {bone.parent?.name}: in scene T{instance.transform.TransformPoint(asset.transform.InverseTransformPoint(bone.position)):F5}; " +
                    $"local T{bone.localPosition:F5} R{bone.localEulerAngles:F5} S{bone.localScale:F5}; {AnimationUtility.CalculateTransformPath(bone, asset.transform)}");
            foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")))
            {
                var curves = AnimationUtility.GetCurveBindings(clip);
                info.AppendLine($"Clip {clip.name} {clip.length:F3}s; human={clip.humanMotion} legacy={clip.legacy} curves={curves.Length}");
                foreach (var group in curves.GroupBy(c => c.path))
                    info.AppendLine($"clip path '{group.Key}': {string.Join(", ", group.Select(c => c.propertyName))}");
            }
        }
        Directory.CreateDirectory(LogDirectory);
        File.WriteAllText(LogDirectory + "/survey.txt", info.ToString());
        Debug.Log("FINAL_LAB_SCIENTIST_ANIMATION_SURVEYED");
    }
}
