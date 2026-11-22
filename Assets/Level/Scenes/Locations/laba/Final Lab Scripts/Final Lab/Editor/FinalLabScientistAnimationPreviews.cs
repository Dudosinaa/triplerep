using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

internal static partial class FinalLabScientistAnimationTransfer
{
    private const string PreviewFolder = "Assets/Art/Animations/Laba/Scientist Animation Previews";
    private const string PreviewRoot = "Scientist — проверка четырёх анимаций";
    private const float PreviewScale = 1.3f;
    private const float FootClearance = .004f;

    public static void ApplyFourPreviews()
    {
        FinalLabInvestigationSurvey.RequireBatch();
        var scene = EditorSceneManager.OpenScene(FinalLabBuilder.ScenePath);
        var target = AssetDatabase.LoadAssetAtPath<GameObject>(TargetPath);
        var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(AvatarPath);
        if (target == null || avatar == null || !avatar.isValid || avatar.isHuman)
            throw new InvalidOperationException("The original scientist model and its valid Generic Avatar are required.");
        var originalAppearance = Snapshot(target).appearance;
        if (!AssetDatabase.IsValidFolder(PreviewFolder)) AssetDatabase.CreateFolder("Assets/Art/Animations/Laba", "Scientist Animation Previews");
        var old = scene.GetRootGameObjects().SingleOrDefault(root => root.name == PreviewRoot);
        if (old != null) Object.DestroyImmediate(old);
        var group = new GameObject(PreviewRoot);
        var actions = new[]
        {
            (file: "Иван идет", label: "Иван идёт", asset: "Walk", state: "Walk", x: -3.36f, loop: true),
            (file: "Иван резко поворачивается", label: "Иван резко разворачивается", asset: "Turn", state: "Turn", x: -1.54f, loop: false),
            (file: "Иван спокойно стоит", label: "Иван спокойно стоит", asset: "Calm_Stand", state: "Calm Stand", x: 1.54f, loop: true),
            (file: "Иван яростно спорит", label: "Иван яростно спорит", asset: "Angry_Argument", state: "Angry Argument", x: 3.36f, loop: true)
        };
        foreach (var action in actions)
        {
            string sourcePath = "Assets/Art/Animations/Laba/" + action.file + ".fbx";
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            var clip = AssetDatabase.LoadAllAssetsAtPath(sourcePath).OfType<AnimationClip>()
                .Single(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal));
            if (source == null || clip.humanMotion || clip.legacy || clip.length <= 0 || clip.frameRate <= 0)
                throw new InvalidOperationException("Expected a valid Generic clip in " + sourcePath);
            var actor = (GameObject)PrefabUtility.InstantiatePrefab(target, group.transform);
            actor.name = action.label + " — scientist";
            actor.transform.SetPositionAndRotation(new Vector3(action.x, 0, 7.04f), Quaternion.Euler(-90, 0, 0));
            actor.transform.localScale = Vector3.one * PreviewScale;
            var sourcePose = Object.Instantiate(source);
            var targetPose = Object.Instantiate(actor);
            AnimationClip baked;
            try
            {
                Debug.Log($"{action.file}: original clip {clip.name}, {clip.length:F3}s, {clip.frameRate:F0} fps.");
                baked = Bake(clip, sourcePose, targetPose, "Scientist_" + action.asset, action.asset == "Walk", action.loop);
                if (action.asset == "Walk")
                {
                    SmoothWalkRotations(baked);
                    GroundWalk(targetPose, baked);
                }
                var motionBounds = SampleMotionBounds(targetPose, baked, out float firstFootY);
                if (motionBounds.size.y is < 1.30f or > 2.35f)
                    throw new InvalidOperationException(actor.name + " does not have an upright human-sized animation bound.");
                actor.transform.position += Vector3.up * (FootClearance - firstFootY);
                motionBounds.center += Vector3.up * (FootClearance - firstFootY);
                CheckPreviewClearance(actor, motionBounds);
            }
            finally
            {
                Object.DestroyImmediate(sourcePose);
                Object.DestroyImmediate(targetPose);
            }

            baked = Save(baked, PreviewFolder + "/Scientist_" + action.asset + ".anim");
            string controllerPath = PreviewFolder + "/Scientist_" + action.asset + ".controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                var machine = controller.layers.Single().stateMachine;
                machine.defaultState = machine.AddState(action.state);
            }
            controller.layers.Single().stateMachine.defaultState.motion = baked;
            EditorUtility.SetDirty(controller.layers.Single().stateMachine.defaultState);
            var animator = actor.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.avatar = avatar;
            animator.runtimeAnimatorController = controller;
            PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
            var snapshot = Snapshot(actor);
            if (snapshot.appearance != originalAppearance) throw new InvalidOperationException("The original scientist appearance was changed.");
            VerifyAssignment(actor, snapshot, clip.length);
            VerifyControllerPlayback(actor, action.state);
        }

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save FinalScene.");
        var saved = EditorSceneManager.OpenScene(FinalLabBuilder.ScenePath).GetRootGameObjects().Single(root => root.name == PreviewRoot);
        if (saved.transform.childCount != actions.Length) throw new InvalidOperationException("The preview group did not retain four characters.");
        foreach (var action in actions)
        {
            var actor = saved.transform.Find(action.label + " — scientist").gameObject;
            var animator = actor.GetComponent<Animator>();
            var clip = animator.runtimeAnimatorController.animationClips.Single();
            if (Snapshot(actor).appearance != originalAppearance || animator.avatar != avatar ||
                clip.name != "Scientist_" + action.asset || clip.isLooping != action.loop ||
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(actor) != TargetPath)
                throw new InvalidOperationException("Incorrect serialized model, Animator or animation: " + actor.name);
        }
        Debug.Log("FINAL_LAB_SCIENTIST_FOUR_PREVIEWS_SAVED");
    }

    private static Bounds SampleMotionBounds(GameObject actor, AnimationClip clip, out float firstFootY)
    {
        var mesh = new Mesh();
        var skins = actor.GetComponentsInChildren<SkinnedMeshRenderer>();
        var bounds = new Bounds(actor.transform.position, Vector3.zero);
        firstFootY = float.PositiveInfinity;
        try
        {
            for (int frame = 0; frame <= 12; frame++)
            {
                clip.SampleAnimation(actor, clip.length * (frame / 12f));
                foreach (var skin in skins)
                {
                    skin.BakeMesh(mesh);
                    foreach (var vertex in mesh.vertices)
                    {
                        Vector3 world = skin.transform.TransformPoint(vertex);
                        bounds.Encapsulate(world);
                        if (frame == 0) firstFootY = Mathf.Min(firstFootY, world.y);
                    }
                }
            }
        }
        finally { Object.DestroyImmediate(mesh); }
        return bounds;
    }

    private static void CheckPreviewClearance(GameObject actor, Bounds bounds)
    {
        Physics.SyncTransforms();
        var bottom = bounds.min;
        bottom.y = .08f;
        var top = bounds.max;
        var hits = Physics.OverlapBox((bottom + top) * .5f, (top - bottom) * .5f, Quaternion.identity,
            ~0, QueryTriggerInteraction.Ignore);
        if (hits.Any(hit => hit is not CharacterController))
            throw new InvalidOperationException(actor.name + " animation reaches scene geometry: " + string.Join(", ", hits.Select(hit => hit.name)));
        Debug.Log($"{actor.name}: validated placement at {actor.transform.position:F3}, motion extent {bounds.size:F3}.");
    }
}
