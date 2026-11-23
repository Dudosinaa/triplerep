using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

internal static partial class FinalLabScientistAnimationTransfer
{
    public static void FixPreviewScaleAndWalkGround()
    {
        FinalLabInvestigationSurvey.RequireBatch();
        var scene = EditorSceneManager.OpenScene(FinalLabBuilder.ScenePath);
        var roots = scene.GetRootGameObjects();
        var group = roots.Single(root => root.name == PreviewRoot);
        var floor = roots.Single(root => root.name == "FinalLab_Environment").transform
            .Find("03_Main_Laboratory/MainFloor").GetComponent<BoxCollider>();
        Physics.SyncTransforms();
        float ground = floor.bounds.max.y;
        var walk = AssetDatabase.LoadAssetAtPath<AnimationClip>(PreviewFolder + "/Scientist_Walk.anim");
        if (walk == null) throw new InvalidOperationException("The walk animation is missing.");
        foreach (var actor in group.transform.Cast<Transform>())
        {
            var animation = actor.GetComponent<Animator>().runtimeAnimatorController.animationClips.Single();
            actor.localScale = Vector3.one * PreviewScale;
            var sample = Object.Instantiate(actor.gameObject);
            SampledPlacement(actor, sample, animation, walk, ground);
            if (animation == walk) CheckWalkingFloor(actor.gameObject, ground, walk.length);
            PrefabUtility.RecordPrefabInstancePropertyModifications(actor);
        }
        foreach (var actor in roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true)).Where(t =>
                     t.parent != group.transform && PrefabUtility.GetNearestPrefabInstanceRoot(t.gameObject) == t.gameObject &&
                     PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject) is TargetPath or "Assets/Art/Animations/Laba/Иван грустно стоит.glb"))
        {
            actor.localScale = Vector3.one * PreviewScale;
            PrefabUtility.RecordPrefabInstancePropertyModifications(actor);
        }
        AssetDatabase.SaveAssetIfDirty(walk);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save FinalScene.");
        var saved = EditorSceneManager.OpenScene(FinalLabBuilder.ScenePath).GetRootGameObjects().Single(root => root.name == PreviewRoot);
        if (saved.transform.childCount != 4 || saved.transform.Cast<Transform>().Any(t => t.localScale != Vector3.one * PreviewScale))
            throw new InvalidOperationException("The uniform model scale did not survive saving the scene.");
        Debug.Log("FINAL_LAB_SCIENTIST_WALK_GROUNDED_AND_SCALED");
    }

    private static void SampledPlacement(Transform actor, GameObject sample, AnimationClip animation, AnimationClip walk, float ground)
    {
        try
        {
            if (animation == walk)
            {
                RebuildWalkingCycle(sample, walk);
            }
            var bounds = SampleMotionBounds(sample, animation, out float firstFoot);
            var shift = Vector3.up * (ground + FootClearance - firstFoot);
            actor.position += shift;
            bounds.center += shift;
            CheckPreviewClearance(actor.gameObject, bounds);
            Debug.Log($"{actor.name}: saved scale {actor.localScale:F3}.");
        }
        finally { Object.DestroyImmediate(sample); }
    }

    private static void RebuildWalkingCycle(GameObject sample, AnimationClip walk)
    {
        const string sourcePath = "Assets/Art/Animations/Laba/Иван идет.fbx";
        var clip = AssetDatabase.LoadAllAssetsAtPath(sourcePath).OfType<AnimationClip>()
            .Single(asset => !asset.name.StartsWith("__preview__", StringComparison.Ordinal));
        var source = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath));
        AnimationClip rebuilt = null;
        try
        {
            rebuilt = Bake(clip, source, sample, walk.name, true, true);
            SmoothWalkRotations(rebuilt);
            GroundWalk(sample, rebuilt);
            EditorUtility.CopySerialized(rebuilt, walk);
            EditorUtility.SetDirty(walk);
        }
        finally
        {
            if (rebuilt != null) Object.DestroyImmediate(rebuilt);
            Object.DestroyImmediate(source);
        }
    }

    private static void GroundWalk(GameObject actor, AnimationClip walk, float? soleHeight = null)
    {
        var hips = Bones(actor)[Hips];
        string hipsPath = AnimationUtility.CalculateTransformPath(hips, actor.transform);
        int steps = Mathf.CeilToInt(walk.length * 120f);
        var times = new float[steps + 1];
        var corrected = new Vector3[steps + 1];
        var mesh = new Mesh();
        var source = Object.Instantiate(walk);
        var settings = AnimationUtility.GetAnimationClipSettings(source);
        settings.loopTime = false;
        AnimationUtility.SetAnimationClipSettings(source, settings);
        var skins = actor.GetComponentsInChildren<SkinnedMeshRenderer>();
        float reference = 0, min = float.PositiveInfinity, max = float.NegativeInfinity;
        try
        {
            // Use the animated soles so the pelvis correction stays valid at the scene model's scale.
            for (int i = 0; i <= steps; i++)
            {
                times[i] = i == steps ? walk.length : i * walk.length / steps;
                source.SampleAnimation(actor, times[i]);
                float feet = LowestVertex(skins, mesh);
                if (i == 0) reference = feet;
                min = Mathf.Min(min, feet - reference);
                max = Mathf.Max(max, feet - reference);
                corrected[i] = hips.localPosition + hips.parent.InverseTransformVector(Vector3.up * ((soleHeight ?? reference) - feet));
            }
        }
        finally
        {
            Object.DestroyImmediate(mesh);
            Object.DestroyImmediate(source);
        }
        var smooth = new Vector3[corrected.Length];
        for (int i = 0; i < steps; i++)
            smooth[i] = (corrected[(i + steps - 2) % steps] + 4 * corrected[(i + steps - 1) % steps] +
                6 * corrected[i] + 4 * corrected[(i + 1) % steps] + corrected[(i + 2) % steps]) / 16f;
        smooth[steps] = smooth[0];
        float lostClearance = 0f;
        for (int i = 0; i <= steps; i++)
            lostClearance = Mathf.Max(lostClearance, hips.parent.TransformVector(corrected[i] - smooth[i]).y);
        var compensate = hips.parent.InverseTransformVector(Vector3.up * lostClearance);
        for (int axis = 0; axis < 3; axis++)
        {
            int component = axis;
            WritePeriodicCurve(walk, hipsPath, "m_LocalPosition." + "xyz"[axis], times,
                smooth.Select(point => (point + compensate)[component]).ToArray());
        }
        EditorUtility.SetDirty(walk);
        Debug.Log($"Old walking sole height relative to first pose: {min:F4} to {max:F4} m. Grounded all {steps + 1} samples.");
    }

    private static void CheckWalkingFloor(GameObject actor, float floor, float duration)
    {
        var preview = Object.Instantiate(actor);
        var graph = PlayableGraph.Create("Walk floor validation in Edit Mode");
        var mesh = new Mesh();
        try
        {
            var animator = preview.GetComponent<Animator>();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var output = AnimationPlayableOutput.Create(graph, "Walk", animator);
            var controller = AnimatorControllerPlayable.Create(graph, animator.runtimeAnimatorController);
            output.SetSourcePlayable(controller);
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            graph.Play();
            var skins = preview.GetComponentsInChildren<SkinnedMeshRenderer>();
            float min = float.PositiveInfinity, max = float.NegativeInfinity;
            float peakAt = 0f;
            int frames = Mathf.CeilToInt(duration * 2 * 120f);
            for (int i = 0; i < frames; i++)
            {
                graph.Evaluate(1f / 120f);
                float height = LowestVertex(skins, mesh) - floor;
                min = Mathf.Min(min, height);
                if (height > max)
                {
                    max = height;
                    peakAt = controller.GetCurrentAnimatorStateInfo(0).normalizedTime;
                }
            }
            if (min < -.001f || max > .016f)
                throw new InvalidOperationException($"The actual walking controller leaves the floor range: {min:F5} to {max:F5} m; peak at normalized time {peakAt:F4}.");
            Debug.Log($"Two Edit Mode Animator walk cycles keep the soles {min:F4} to {max:F4} m above the actual floor.");
        }
        finally
        {
            graph.Destroy();
            Object.DestroyImmediate(mesh);
            Object.DestroyImmediate(preview);
        }
    }

    private static float LowestVertex(SkinnedMeshRenderer[] skins, Mesh mesh)
    {
        float minimum = float.PositiveInfinity;
        foreach (var skin in skins)
        {
            skin.BakeMesh(mesh);
            foreach (var vertex in mesh.vertices) minimum = Mathf.Min(minimum, skin.transform.TransformPoint(vertex).y);
        }
        return minimum;
    }
}
