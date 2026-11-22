using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

internal static partial class FinalLabScientistAnimationTransfer
{
    private const string Folder = "Assets/Art/Animations/Laba/Scientist Sad Stand";
    private const string ClipPath = Folder + "/Scientist_Sad_Stand.anim";
    private const string AvatarPath = Folder + "/Scientist_Generic.asset";
    private const string ControllerPath = Folder + "/Scientist_Sad_Stand.controller";
    private const string Hips = "ValveBiped.Bip01_Pelvis";
    private static readonly Regex ExportSuffix = new(@"_\d+_\d+$");

    public static void RepairAssignment()
    {
        FinalLabInvestigationSurvey.RequireBatch();
        var scene = EditorSceneManager.OpenScene(FinalLabBuilder.ScenePath);
        var scientist = scene.GetRootGameObjects().Single(root => root.name == "scientist" &&
            PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root) == TargetPath);
        var original = Snapshot(scientist);
        var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(AvatarPath);
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        if (avatar == null || controller == null || clip == null)
            throw new InvalidOperationException("The existing scientist animation assets are missing.");

        if (!scientist.TryGetComponent<Animator>(out var animator))
        {
            Debug.Log("The on-disk FinalScene contained scientist but no Animator on it.");
            animator = scientist.AddComponent<Animator>();
        }
        animator.avatar = avatar;
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.enabled = true;
        PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
        VerifyAssignment(scientist, original, clip.length);
        VerifyControllerPlayback(scientist);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save FinalScene.");
        var reopened = EditorSceneManager.OpenScene(FinalLabBuilder.ScenePath).GetRootGameObjects().Single(root => root.name == "scientist");
        VerifyAssignment(reopened, original, clip.length);
        Debug.Log("FINAL_LAB_SCIENTIST_CONTROLLER_RESTORED_AND_EVALUATED");
    }

    private static void VerifyControllerPlayback(GameObject scientist, string stateName = "Sad Stand")
    {
        var preview = Object.Instantiate(scientist);
        var mesh = new Mesh();
        var graph = PlayableGraph.Create("Scientist Animator validation in Edit Mode");
        try
        {
            var animator = preview.GetComponent<Animator>();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var playable = AnimatorControllerPlayable.Create(graph, animator.runtimeAnimatorController);
            var output = AnimationPlayableOutput.Create(graph, "Scientist", animator);
            output.SetSourcePlayable(playable);
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            graph.Play();
            var skin = preview.GetComponentsInChildren<SkinnedMeshRenderer>().OrderByDescending(r => r.sharedMesh.vertexCount).First();
            var playback = animator.runtimeAnimatorController.animationClips.Single();
            graph.Evaluate(playback.length * .055f);
            skin.BakeMesh(mesh);
            var first = mesh.vertices;
            int steps = Mathf.Max(8, Mathf.CeilToInt(playback.length * 15));
            for (int i = 0; i < steps; i++) graph.Evaluate(playback.length * .51f / steps);
            skin.BakeMesh(mesh);
            var state = playable.GetCurrentAnimatorStateInfo(0);
            var displacement = first.Zip(mesh.vertices, Vector3.Distance).Max();
            if (!state.IsName("Base Layer." + stateName) || state.normalizedTime <= .1f || displacement < .004f)
                throw new InvalidOperationException($"Animator did not advance or deform the mesh: time {state.normalizedTime:F3}, delta {displacement:F4}.");
            Debug.Log($"Saved Animator controller evaluated in Edit Mode: {stateName} at {state.normalizedTime:P0}, mesh displacement {displacement:F4}.");
        }
        finally
        {
            graph.Destroy();
            Object.DestroyImmediate(mesh);
            Object.DestroyImmediate(preview);
        }
    }

    public static void Apply()
    {
        FinalLabInvestigationSurvey.RequireBatch();
        var scene = EditorSceneManager.OpenScene(FinalLabBuilder.ScenePath);
        var roots = scene.GetRootGameObjects();
        var scientist = roots.Single(root => PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root) == TargetPath);
        var source = roots.Single(root => PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root) == SourcePath);
        if (scientist.name != "scientist") throw new InvalidOperationException("The intended scientist model was not selected.");
        var clip = AssetDatabase.LoadAllAssetsAtPath(SourcePath).OfType<AnimationClip>()
            .Single(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal));
        if (clip.humanMotion || clip.legacy || clip.length <= 0) throw new InvalidOperationException("Expected the original Generic sad-stand clip.");
        var original = Snapshot(scientist);
        var sourcePose = Object.Instantiate(source);
        var targetPose = Object.Instantiate(scientist);
        AnimationClip baked;
        try
        {
            baked = Bake(clip, sourcePose, targetPose, loop: true);
        }
        finally
        {
            Object.DestroyImmediate(sourcePose);
            Object.DestroyImmediate(targetPose);
        }

        var avatar = AvatarBuilder.BuildGenericAvatar(scientist, Bones(scientist)[Hips].name);
        if (avatar == null || !avatar.isValid || avatar.isHuman) throw new InvalidOperationException("The actual scientist rig did not build a valid Generic Avatar.");
        avatar.name = "Scientist_Generic";
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Art/Animations/Laba", "Scientist Sad Stand");
        baked = Save(baked, ClipPath);
        avatar = Save(avatar, AvatarPath);
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var machine = controller.layers.Single().stateMachine;
            machine.defaultState = machine.AddState("Sad Stand");
        }
        controller.layers.Single().stateMachine.defaultState.motion = baked;
        EditorUtility.SetDirty(controller.layers.Single().stateMachine.defaultState);
        if (!scientist.TryGetComponent<Animator>(out var animator)) animator = scientist.AddComponent<Animator>();
        animator.avatar = avatar;
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
        AssetDatabase.SaveAssets();
        VerifyAssignment(scientist, original, clip.length);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save FinalScene.");
        var reopened = EditorSceneManager.OpenScene(FinalLabBuilder.ScenePath).GetRootGameObjects().Single(root => root.name == "scientist");
        VerifyAssignment(reopened, original, clip.length);
        Debug.Log("FINAL_LAB_SCIENTIST_SAD_STAND_SAVED: Generic Avatar, one default Animator state, original scene Transform and materials.");
    }

    private static AnimationClip Bake(AnimationClip sourceClip, GameObject source, GameObject target,
        string outputName = "Scientist_Sad_Stand", bool removeWalkingDrift = false, bool? loop = null)
    {
        var sourceBones = Bones(source);
        var targetBones = Bones(target);
        var mapped = target.GetComponentsInChildren<Transform>(true)
            .Where(bone => targetBones.TryGetValue(Canonical(bone.name), out var registered) && registered == bone &&
                sourceBones.ContainsKey(Canonical(bone.name))).ToArray();
        if (mapped.Length != sourceBones.Count) throw new InvalidOperationException("Not all source skin joints have counterparts on scientist.");
        var bindings = AnimationUtility.GetCurveBindings(sourceClip);
        if (bindings.Any(binding => binding.type != typeof(Transform) || source.transform.Find(binding.path) == null))
            throw new InvalidOperationException("The source clip binds to an absent node or a non-transform property.");
        var names = sourceBones.ToDictionary(pair => pair.Value, pair => pair.Key);
        var targetNames = mapped.ToDictionary(bone => bone, bone => Canonical(bone.name));
        foreach (var targetBone in mapped)
        {
            var sourceBone = sourceBones[Canonical(targetBone.name)];
            string sourceParent = MatchedParent(sourceBone, names);
            string targetParent = MatchedParent(targetBone, targetNames);
            if (sourceParent != targetParent) throw new InvalidOperationException($"The skeletal parent does not match for {targetBone.name}.");
        }
        var sourceRest = SkinBindPose(source, sourceBones);
        var targetRest = targetBones.ToDictionary(pair => pair.Key, pair => (pair.Value.position, pair.Value.rotation));
        Quaternion sourceFrame = AnatomicalFrame(sourceRest);
        Quaternion alignment = AnatomicalFrame(targetRest) * Quaternion.Inverse(sourceFrame);
        float scale = LegLength(targetRest) / LegLength(sourceRest);
        var targetHips = targetBones[Hips];
        var sourceHips = sourceBones[Hips];
        int frameCount = Mathf.RoundToInt(sourceClip.length * sourceClip.frameRate) + 1;
        var tracks = mapped.Select(bone => new Quaternion[frameCount]).ToArray();
        var hipTrack = new Vector3[frameCount];
        var sampleTimes = Enumerable.Range(0, frameCount).Select(i => Mathf.Min(i / sourceClip.frameRate, sourceClip.length)).ToArray();
        sampleTimes[^1] = sourceClip.length;
        var drift = Vector3.zero;
        var sourceOrigin = sourceRest[Hips].position;
        if (removeWalkingDrift)
        {
            sourceClip.SampleAnimation(source, 0);
            sourceOrigin = sourceHips.position;
            sourceClip.SampleAnimation(source, sourceClip.length);
            drift = sourceHips.position - sourceOrigin;
            Debug.Log($"{outputName}: removed accumulated travel {drift:F3} from the repeatable preview cycle.");
        }
        float minDirectionMatch = 1f;
        for (int frame = 0; frame < frameCount; frame++)
        {
            sourceClip.SampleAnimation(source, sampleTimes[frame]);
            var translation = sourceHips.position - sourceOrigin - drift * (sampleTimes[frame] / sourceClip.length);
            targetHips.position = targetRest[Hips].position + alignment * translation * scale;
            if (Vector3.Distance(targetHips.position, targetRest[Hips].position) > .6f)
                throw new InvalidOperationException($"{outputName}: the pelvis travels too far for an in-place scene preview, {translation.magnitude:F3} m.");
            hipTrack[frame] = targetHips.localPosition;
            for (int i = 0; i < mapped.Length; i++)
            {
                var bone = mapped[i];
                string name = Canonical(bone.name);
                Quaternion motion = sourceBones[name].rotation * Quaternion.Inverse(sourceRest[name].rotation);
                bone.rotation = alignment * motion * Quaternion.Inverse(alignment) * targetRest[name].rotation;
                Quaternion q = bone.localRotation;
                if (frame > 0 && Quaternion.Dot(tracks[i][frame - 1], q) < 0) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                tracks[i][frame] = q;
            }
            minDirectionMatch = Mathf.Min(minDirectionMatch, VerifyPoseDirections(sourceBones, targetBones, alignment));
        }
        if (minDirectionMatch < .86f) throw new InvalidOperationException("Retargeted limb directions disagree with the sad-stand source: " + minDirectionMatch);

        var result = new AnimationClip { name = outputName, frameRate = sourceClip.frameRate };
        for (int i = 0; i < mapped.Length; i++)
        {
            string path = AnimationUtility.CalculateTransformPath(mapped[i], target.transform);
            for (int component = 0; component < 4; component++)
            {
                int index = component;
                WriteCurve(result, path, "m_LocalRotation." + "xyzw"[index], sampleTimes, tracks[i].Select(q => q[index]).ToArray());
            }
        }
        string hipsPath = AnimationUtility.CalculateTransformPath(targetHips, target.transform);
        for (int component = 0; component < 3; component++)
        {
            int index = component;
            WriteCurve(result, hipsPath, "m_LocalPosition." + "xyz"[index], sampleTimes, hipTrack.Select(point => point[index]).ToArray());
        }
        result.EnsureQuaternionContinuity();
        var settings = AnimationUtility.GetAnimationClipSettings(result);
        settings.loopTime = loop ?? sourceClip.isLooping;
        AnimationUtility.SetAnimationClipSettings(result, settings);
        VerifySkinMoves(target, result);
        Debug.Log($"{outputName}: retargeted {mapped.Length} joints across {frameCount} source samples; minimum limb direction agreement {minDirectionMatch:F4}.");
        return result;
    }

    private static Dictionary<string, Transform> Bones(GameObject actor) => actor.GetComponentsInChildren<SkinnedMeshRenderer>(true)
        .SelectMany(skin => skin.bones).Distinct().Where(bone => bone != null && bone.name.StartsWith("ValveBiped.", StringComparison.Ordinal))
        .ToDictionary(bone => Canonical(bone.name));

    private static string Canonical(string name) => ExportSuffix.Replace(name, "");

    private static string MatchedParent(Transform bone, Dictionary<Transform, string> jointNames)
    {
        for (var parent = bone.parent; parent != null; parent = parent.parent)
            if (jointNames.TryGetValue(parent, out string name)) return name;
        return "";
    }

    private static Dictionary<string, (Vector3 position, Quaternion rotation)> SkinBindPose(GameObject root, Dictionary<string, Transform> bones)
    {
        var skin = root.GetComponentsInChildren<SkinnedMeshRenderer>().OrderByDescending(r => r.sharedMesh.vertexCount).First();
        var bindPoses = skin.sharedMesh.bindposes;
        return bones.ToDictionary(pair => pair.Key, pair =>
        {
            int index = Array.IndexOf(skin.bones, pair.Value);
            if (index < 0) throw new InvalidOperationException("Missing bind pose for " + pair.Key);
            Matrix4x4 rest = skin.localToWorldMatrix * bindPoses[index].inverse;
            return ((Vector3)rest.GetColumn(3), rest.rotation);
        });
    }

    private static Quaternion AnatomicalFrame(Dictionary<string, (Vector3 position, Quaternion rotation)> rest)
    {
        var right = (rest["ValveBiped.Bip01_R_Thigh"].position - rest["ValveBiped.Bip01_L_Thigh"].position).normalized;
        var up = (rest["ValveBiped.Bip01_Spine2"].position - rest[Hips].position).normalized;
        return Quaternion.LookRotation(Vector3.Cross(right, up), up);
    }

    private static float LegLength(Dictionary<string, (Vector3 position, Quaternion rotation)> bind)
    {
        var knee = bind["ValveBiped.Bip01_L_Calf"].position;
        return Vector3.Distance(bind["ValveBiped.Bip01_L_Thigh"].position, knee) + Vector3.Distance(knee, bind["ValveBiped.Bip01_L_Foot"].position);
    }

    private static float VerifyPoseDirections(Dictionary<string, Transform> source, Dictionary<string, Transform> target, Quaternion alignment)
    {
        string[] segments = { "L_UpperArm", "L_Forearm", "L_Hand", "R_UpperArm", "R_Forearm", "R_Hand", "L_Thigh", "L_Calf", "L_Foot", "R_Thigh", "R_Calf", "R_Foot" };
        float minimum = 1f;
        for (int i = 0; i < segments.Length; i += 3)
            for (int joint = i; joint < i + 2; joint++)
            {
                string from = "ValveBiped.Bip01_" + segments[joint], to = "ValveBiped.Bip01_" + segments[joint + 1];
                Vector3 sourceDirection = source[to].position - source[from].position;
                Vector3 targetDirection = target[to].position - target[from].position;
                minimum = Mathf.Min(minimum, Vector3.Dot(alignment * sourceDirection.normalized, targetDirection.normalized));
            }
        return minimum;
    }

    private static void WriteCurve(AnimationClip clip, string path, string property, float[] times, float[] values)
    {
        var keys = new Keyframe[times.Length];
        for (int i = 0; i < times.Length; i++)
        {
            float incoming = i == 0 ? 0 : (values[i] - values[i - 1]) / (times[i] - times[i - 1]);
            float outgoing = i == times.Length - 1 ? 0 : (values[i + 1] - values[i]) / (times[i + 1] - times[i]);
            keys[i] = new Keyframe(times[i], values[i], incoming, outgoing);
        }
        clip.SetCurve(path, typeof(Transform), property, new AnimationCurve(keys));
    }

    private static void VerifySkinMoves(GameObject actor, AnimationClip animation)
    {
        var skin = actor.GetComponentsInChildren<SkinnedMeshRenderer>().OrderByDescending(r => r.sharedMesh.vertexCount).First();
        var mesh = new Mesh();
        try
        {
            animation.SampleAnimation(actor, .18f);
            skin.BakeMesh(mesh);
            Vector3[] first = mesh.vertices;
            animation.SampleAnimation(actor, animation.length * .74f);
            skin.BakeMesh(mesh);
            Vector3[] second = mesh.vertices;
            var maxDelta = first.Zip(second, Vector3.Distance).Max();
            if (first.Length != skin.sharedMesh.vertexCount || maxDelta < .004f)
                throw new InvalidOperationException("The original skinned mesh did not visibly deform in Edit Mode sampling.");
            Debug.Log($"CPU skin sampling: {skin.name} retains {first.Length} vertices; maximum animated vertex displacement {maxDelta:F4} in mesh space.");
        }
        finally { Object.DestroyImmediate(mesh); }
    }

    private static (Vector3 position, Quaternion rotation, Vector3 scale, string appearance) Snapshot(GameObject actor)
    {
        string Key(Object asset)
        {
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long fileId);
            return guid + ":" + fileId;
        }
        string appearance = string.Join("|", actor.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(skin =>
            Key(skin.sharedMesh) + ";" + string.Join(",", skin.sharedMaterials.Select(Key))));
        return (actor.transform.localPosition, actor.transform.localRotation, actor.transform.localScale, appearance);
    }

    private static void VerifyAssignment(GameObject scientist, (Vector3, Quaternion, Vector3, string) original, float sourceDuration)
    {
        var animator = scientist.GetComponent<Animator>();
        var controller = animator.runtimeAnimatorController as AnimatorController;
        var clip = controller.layers.Single().stateMachine.defaultState.motion as AnimationClip;
        if (Snapshot(scientist) != original || !animator.avatar.isValid || animator.avatar.isHuman || animator.applyRootMotion ||
            scientist.GetComponentsInChildren<Animator>(true).Length != 1 || Mathf.Abs(clip.length - sourceDuration) > .001f ||
            AnimationUtility.GetCurveBindings(clip).Any(curve => string.IsNullOrEmpty(curve.path) || scientist.transform.Find(curve.path) == null ||
                curve.propertyName.StartsWith("m_LocalScale", StringComparison.Ordinal)))
            throw new InvalidOperationException("Saved scientist has invalid animation bindings or a changed appearance or scene Transform.");
    }

    private static T Save<T>(T asset, string path) where T : Object
    {
        var saved = AssetDatabase.LoadAssetAtPath<T>(path);
        if (saved == null)
        {
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
        EditorUtility.CopySerialized(asset, saved);
        Object.DestroyImmediate(asset);
        return saved;
    }
}
