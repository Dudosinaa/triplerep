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
    public static void SmoothCurrentWalk()
    {
        FinalLabInvestigationSurvey.RequireBatch();
        var scene = EditorSceneManager.OpenScene(FinalLabBuilder.ScenePath);
        var group = scene.GetRootGameObjects().Single(root => root.name == PreviewRoot);
        var actor = group.transform.Find("Иван идёт — scientist").gameObject;
        var walk = AssetDatabase.LoadAssetAtPath<AnimationClip>(PreviewFolder + "/Scientist_Walk.anim");
        if (walk == null || !walk.isLooping || actor.GetComponent<Animator>().runtimeAnimatorController.animationClips.Single() != walk)
            throw new InvalidOperationException("The walking scientist does not reference the expected looping clip.");
        var architecture = scene.GetRootGameObjects().Single(root => root.name == "FinalLab_Environment");
        Physics.SyncTransforms();
        float floor = architecture.transform.Find("03_Main_Laboratory/MainFloor").GetComponent<BoxCollider>().bounds.max.y;
        float before = PeakWalkVelocityChange(actor, walk.length);
        var sample = Object.Instantiate(actor);
        try
        {
            SmoothWalkRotations(walk);
            GroundWalk(sample, walk, floor + FootClearance);
        }
        finally { Object.DestroyImmediate(sample); }
        float after = PeakWalkVelocityChange(actor, walk.length);
        if (after >= before * .90f) throw new InvalidOperationException($"Walking velocity changes did not improve: {before:F3} to {after:F3} m/s.");
        Debug.Log($"Peak one-step walking velocity change reduced from {before:F4} to {after:F4} m/s at 120 Hz.");
        CheckWalkingFloor(actor, floor, walk.length);
        AssetDatabase.SaveAssetIfDirty(walk);
        Debug.Log("FINAL_LAB_SCIENTIST_WALK_SMOOTHED");
    }

    private static void SmoothWalkRotations(AnimationClip walk)
    {
        int samples = Mathf.RoundToInt(walk.length * walk.frameRate);
        var times = Enumerable.Range(0, samples + 1).Select(index => index * walk.length / samples).ToArray();
        int[] weights = { 1, 4, 6, 4, 1 };
        var paths = AnimationUtility.GetCurveBindings(walk).Where(binding => binding.type == typeof(Transform) &&
            binding.propertyName.StartsWith("m_LocalRotation.", StringComparison.Ordinal)).Select(binding => binding.path).Distinct().ToArray();
        foreach (var path in paths)
        {
            var curves = "xyzw".Select(axis => AnimationUtility.GetEditorCurve(walk,
                EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation." + axis))).ToArray();
            var rotation = new Quaternion[samples];
            for (int i = 0; i < samples; i++)
                rotation[i] = new Quaternion(curves[0].Evaluate(times[i]), curves[1].Evaluate(times[i]),
                    curves[2].Evaluate(times[i]), curves[3].Evaluate(times[i])).normalized;
            var filtered = new Quaternion[samples + 1];
            for (int i = 0; i < samples; i++)
            {
                var center = rotation[i];
                var sum = Vector4.zero;
                for (int offset = -2; offset <= 2; offset++)
                {
                    var nearby = rotation[(i + offset + samples) % samples];
                    int weight = weights[offset + 2];
                    float signedWeight = Quaternion.Dot(center, nearby) < 0 ? -weight : weight;
                    sum += new Vector4(nearby.x, nearby.y, nearby.z, nearby.w) * signedWeight;
                }
                var q = new Quaternion(sum.x, sum.y, sum.z, sum.w).normalized;
                filtered[i] = i > 0 && Quaternion.Dot(filtered[i - 1], q) < 0 ? new Quaternion(-q.x, -q.y, -q.z, -q.w) : q;
            }
            filtered[samples] = filtered[0];
            for (int axis = 0; axis < 4; axis++)
            {
                int component = axis;
                WritePeriodicCurve(walk, path, "m_LocalRotation." + "xyzw"[axis], times, filtered.Select(q => q[component]).ToArray());
            }
        }
        walk.EnsureQuaternionContinuity();
        EditorUtility.SetDirty(walk);
    }

    private static void WritePeriodicCurve(AnimationClip clip, string path, string property, float[] times, float[] values)
    {
        int samples = times.Length - 1;
        float halfRate = samples / (2f * times[samples]);
        var keys = new Keyframe[times.Length];
        for (int i = 0; i <= samples; i++)
        {
            float slope = (values[(i + 1) % samples] - values[(i + samples - 1) % samples]) * halfRate;
            keys[i] = new Keyframe(times[i], values[i], slope, slope);
        }
        clip.SetCurve(path, typeof(Transform), property, new AnimationCurve(keys));
    }

    private static float PeakWalkVelocityChange(GameObject actor, float duration)
    {
        var sample = Object.Instantiate(actor);
        var graph = PlayableGraph.Create("Walking continuity check in Edit Mode");
        try
        {
            var animator = sample.GetComponent<Animator>();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var output = AnimationPlayableOutput.Create(graph, "Walk", animator);
            output.SetSourcePlayable(AnimatorControllerPlayable.Create(graph, animator.runtimeAnimatorController));
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            graph.Play();
            var bones = Bones(sample);
            var landmarks = new[] { bones[Hips], bones["ValveBiped.Bip01_L_Toe0"], bones["ValveBiped.Bip01_R_Toe0"],
                bones["ValveBiped.Bip01_Head1"], bones["ValveBiped.Bip01_L_Hand"], bones["ValveBiped.Bip01_R_Hand"] };
            var positions = new Vector3[landmarks.Length];
            var velocities = new Vector3[landmarks.Length];
            int steps = Mathf.CeilToInt(duration * 3 * 120f);
            float largest = 0f;
            for (int step = 0; step < steps; step++)
            {
                graph.Evaluate(1f / 120f);
                for (int i = 0; i < landmarks.Length; i++)
                {
                    var current = landmarks[i].position;
                    var velocity = (current - positions[i]) * 120f;
                    if (step >= steps / 3) largest = Mathf.Max(largest, Vector3.Distance(velocity, velocities[i]));
                    positions[i] = current;
                    velocities[i] = velocity;
                }
            }
            return largest;
        }
        finally
        {
            graph.Destroy();
            Object.DestroyImmediate(sample);
        }
    }
}
