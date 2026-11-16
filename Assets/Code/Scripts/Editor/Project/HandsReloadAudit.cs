using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Read-only FBX inspection. Reports are outside Assets; source importers are never changed.
public static class HandsReloadAudit
{
    public const string Source = "Assets/Art/Models/Weapons/Hands + pistol animation/source/Arms_Mak.fbx";
    public const string Target = "Assets/Art/Models/Weapons/Hands/source/AnimatedPistol.fbx";
    public static string Output => Path.GetFullPath("Logs/HandsReload");
    [Serializable] public class Pose { public string path; public Vector3 position, scale, worldPosition; public Quaternion rotation, worldRotation; }
    [Serializable] public class Frame { public float time; public Pose[] bones; }
    [Serializable] public class Curve { public string path, property; public int keys; public float min, max; }
    [Serializable] public class Clip { public string name; public float length, fps; public Curve[] curves; public Frame[] frames; }
    [Serializable] public class Skin { public string name; public string[] bones; public int vertices; public Bounds bounds; public Vector3[] positions; public BoneWeight[] weights; public Matrix4x4[] bindposes; public int[] triangles; }
    [Serializable] public class Audit { public string model; public Pose[] rest; public Skin[] skins; public Clip[] clips; }
    public static Pose[] Poses(GameObject model) => model.GetComponentsInChildren<Transform>(true).Select(t => new Pose {
        path = AnimationUtility.CalculateTransformPath(t, model.transform), position = t.localPosition,
        rotation = t.localRotation, scale = t.localScale, worldPosition = t.position, worldRotation = t.rotation }).ToArray();
    [MenuItem("Tools/Weapon/Audit reload source rigs")]
    public static void Run()
    {
        Directory.CreateDirectory(Output);
        Inspect(Source, "source"); Inspect(Target, "target");
        Debug.Log("HANDS_RELOAD_AUDIT_PASS");
    }
    private static void Inspect(string path, string label)
    {
        var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
        try
        {
            var audit = new Audit { model = path, rest = Poses(model) };
            audit.skins = model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(s => new Skin {
                name = s.name, bones = s.bones.Select(b => AnimationUtility.CalculateTransformPath(b, model.transform)).ToArray(),
                vertices = s.sharedMesh.vertexCount, bounds = s.sharedMesh.bounds, positions = s.sharedMesh.vertices,
                weights = s.sharedMesh.boneWeights, bindposes = s.sharedMesh.bindposes, triangles = s.sharedMesh.triangles }).ToArray();
            audit.clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).Select(c => {
                var clip = new Clip { name = c.name, length = c.length, fps = c.frameRate };
                clip.curves = AnimationUtility.GetCurveBindings(c).Select(b => {
                    var curve = AnimationUtility.GetEditorCurve(c, b);
                    return new Curve { path = b.path, property = b.propertyName, keys = curve.length,
                        min = curve.keys.Min(k => k.value), max = curve.keys.Max(k => k.value) };
                }).ToArray();
                int count = Mathf.RoundToInt(c.length * 30f);
                clip.frames = Enumerable.Range(0, count + 1).Select(i => {
                    float time = Mathf.Min(i / 30f, c.length); c.SampleAnimation(model, time);
                    return new Frame { time = time, bones = Poses(model) };
                }).ToArray();
                return clip;
            }).ToArray();
            File.WriteAllText(Path.Combine(Output, label + "-rig.json"), JsonUtility.ToJson(audit));
        }
        finally { UnityEngine.Object.DestroyImmediate(model); }
    }
}
