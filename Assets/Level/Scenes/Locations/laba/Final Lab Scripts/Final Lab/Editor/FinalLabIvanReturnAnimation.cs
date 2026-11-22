using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

internal static partial class FinalLabIvanSceneSetup
{
    private const string ReturnTurnPath = Scripts + "FinalLab_Ivan_ReturnTurn.anim";

    public static void ApplyReturnMotion()
    {
        FinalLabInvestigationSurvey.RequireBatch();
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Return motion verification runs in Edit Mode.");
        var scene = EditorSceneManager.OpenScene(FinalLabBuilder.ScenePath);
        var root = scene.GetRootGameObjects().Single(node => node.name == RootName);
        var cutscene = root.GetComponent<FinalLabIvanCutsceneController>();
        var ivan = cutscene.Ivan;
        var marks = root.transform.Find("Staging marks");
        Vector3 inside = marks.Find("Ivan_InsideStart_and_End").position;
        Vector3 hall = marks.Find("Ivan_Hall").position;
        Vector3 talk = marks.Find("Ivan_DialoguePoint").position;
        Vector3 eye = marks.Find("Alexei_ConversationEye").position;
        VerifyRoutePair(cutscene.Door, inside, hall, talk, ivan.Motor);

        var turn = BakeReturnTurn(ivan.Animator.gameObject, Load<AnimationClip>(Motions[4].path));
        ivan.SetReturnTurnYaw(turn.heading);
        var controller = (AnimatorController)ivan.Animator.runtimeAnimatorController;
        var state = controller.layers.Single().stateMachine.states.Single(node => node.state.name == FinalLabIvanCharacterController.TurnAround).state;
        state.motion = turn.clip;
        EditorUtility.SetDirty(state);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(controller), ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        VerifyReturnBlend(ivan, inside, hall, talk, eye);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save Ivan's return motion.");
        var saved = EditorSceneManager.OpenScene(FinalLabBuilder.ScenePath).GetRootGameObjects().Single(node => node.name == RootName)
            .GetComponent<FinalLabIvanCutsceneController>().Ivan;
        if (!saved.Animator.runtimeAnimatorController.animationClips.Contains(turn.clip) || Mathf.Abs(Mathf.DeltaAngle(saved.ReturnTurnAngle(175, 1), 175)) > .01f)
            throw new InvalidOperationException("The return turn clip or heading was not saved.");
        Debug.Log("FINAL_LAB_IVAN_RETURN_VERIFIED_IN_EDIT_MODE");
    }

    private static (AnimationClip clip, AnimationCurve heading) BakeReturnTurn(GameObject model, AnimationClip source)
    {
        const int probeSteps = 120;
        const int frames = 60;
        var sample = Object.Instantiate(model);
        try
        {
            var bones = sample.GetComponentsInChildren<Transform>();
            var hips = Bone(bones, "Pelvis");
            var thighs = (left: Bone(bones, "L_Thigh"), right: Bone(bones, "R_Thigh"));
            source.SampleAnimation(sample, 0);
            Vector3 initialFacing = PelvisFacing(thighs);
            Vector3 pivot = hips.position;
            var yaws = new float[probeSteps + 1];
            float rawPeak = 0f;
            for (int i = 1; i <= probeSteps; i++)
            {
                source.SampleAnimation(sample, source.length * i / probeSteps);
                float yaw = Vector3.SignedAngle(initialFacing, PelvisFacing(thighs), Vector3.up);
                yaws[i] = yaws[i - 1] + Mathf.DeltaAngle(yaws[i - 1], yaw);
                rawPeak = Mathf.Max(rawPeak, Mathf.Abs(yaws[i] - yaws[i - 1]));
            }
            float totalYaw = yaws[^1];
            if (Mathf.Abs(totalYaw) < 135f || Mathf.Abs(totalYaw) > 220f)
                throw new InvalidOperationException("The source turn does not contain the expected half turn.");

            // Give the rapid part of the authored turn more time without changing its poses.
            var remap = new float[probeSteps + 1];
            float furthest = 0;
            for (int i = 1; i <= probeSteps; i++)
            {
                furthest = Mathf.Max(furthest, Mathf.Clamp01(yaws[i] / totalYaw));
                remap[i] = .36f * i / probeSteps + .64f * furthest;
            }

            var sourceTimes = new float[frames + 1];
            var positions = new Vector3[frames + 1];
            var rotations = new Quaternion[frames + 1];
            var heading = new float[frames + 1];
            int span = 1;
            for (int i = 0; i <= frames; i++)
            {
                float time = (float)i / frames;
                while (span < probeSteps && time > remap[span]) span++;
                float sourceFrame = span - 1 + Mathf.InverseLerp(remap[span - 1], remap[span], time);
                sourceTimes[i] = source.length * sourceFrame / probeSteps;
                source.SampleAnimation(sample, sourceTimes[i]);
                float measured = Vector3.SignedAngle(initialFacing, PelvisFacing(thighs), Vector3.up);
                float reference = Mathf.Lerp(yaws[span - 1], yaws[span], sourceFrame - (span - 1));
                heading[i] = reference + Mathf.DeltaAngle(reference, measured);
                // The motor supplies heading once. The clip keeps the local shift, pitch and limb gestures.
                var unturn = Quaternion.AngleAxis(-heading[i], Vector3.up);
                positions[i] = hips.parent.InverseTransformPoint(pivot + unturn * (hips.position - pivot));
                var local = Quaternion.Inverse(hips.parent.rotation) * unturn * hips.rotation;
                rotations[i] = i > 0 && Quaternion.Dot(rotations[i - 1], local) < 0
                    ? new Quaternion(-local.x, -local.y, -local.z, -local.w) : local;
            }

            string pelvisPath = AnimationUtility.CalculateTransformPath(hips, sample.transform);
            var result = Object.Instantiate(source);
            result.name = "FinalLab_Ivan_ReturnTurn";
            result.frameRate = frames / source.length;
            var bindings = AnimationUtility.GetCurveBindings(source);
            var curves = bindings.Select(binding =>
            {
                int component = "xyzw".IndexOf(binding.propertyName[^1]);
                if (binding.path == pelvisPath && binding.propertyName.StartsWith("m_LocalRotation.", StringComparison.Ordinal))
                    return ReturnCurve(rotations.Select(rotation => rotation[component]).ToArray(), source.length);
                if (binding.path == pelvisPath && binding.propertyName.StartsWith("m_LocalPosition.", StringComparison.Ordinal))
                    return ReturnCurve(positions.Select(position => position[component]).ToArray(), source.length);
                var original = AnimationUtility.GetEditorCurve(source, binding);
                return ReturnCurve(sourceTimes.Select(original.Evaluate).ToArray(), source.length);
            }).ToArray();
            AnimationUtility.SetEditorCurves(result, bindings, curves);
            result.EnsureQuaternionContinuity();
            var saved = AssetDatabase.LoadAssetAtPath<AnimationClip>(ReturnTurnPath);
            if (saved == null)
            {
                AssetDatabase.CreateAsset(result, ReturnTurnPath);
                saved = result;
            }
            else
            {
                EditorUtility.CopySerialized(result, saved);
                EditorUtility.SetDirty(saved);
                Object.DestroyImmediate(result);
            }
            Debug.Log($"Source turn heading could change by {rawPeak:F1}° within 1/120 second.");
            return (saved, ReturnCurve(heading, 1f));
        }
        finally { Object.DestroyImmediate(sample); }
    }

    private static AnimationCurve ReturnCurve(float[] samples, float duration)
    {
        if (samples.All(value => Mathf.Abs(value - samples[0]) < .000001f))
            return AnimationCurve.Constant(0, duration, samples[0]);
        float interval = duration / (samples.Length - 1);
        var keys = new Keyframe[samples.Length];
        for (int i = 0; i < samples.Length; i++)
        {
            int before = Mathf.Max(0, i - 1);
            int after = Mathf.Min(samples.Length - 1, i + 1);
            float slope = (samples[after] - samples[before]) / ((after - before) * interval);
            keys[i] = new Keyframe(i * interval, samples[i], slope, slope);
        }
        return new AnimationCurve(keys);
    }

    private static Vector3 PelvisFacing((Transform left, Transform right) thighs) =>
        Vector3.Cross(thighs.right.position - thighs.left.position, Vector3.up);

    private static void VerifyReturnBlend(FinalLabIvanCharacterController original, Vector3 inside, Vector3 hall, Vector3 talk, Vector3 eye)
    {
        const float dt = 1f / 120f;
        var subject = Object.Instantiate(original.gameObject).GetComponent<FinalLabIvanCharacterController>();
        var graph = PlayableGraph.Create("Ivan return turn verification in Edit Mode");
        try
        {
            subject.transform.position = talk;
            Quaternion before = subject.Direction(eye);
            Quaternion after = subject.Direction(hall);
            subject.transform.rotation = before;
            float desiredYaw = Vector3.SignedAngle(before * Vector3.forward, after * Vector3.forward, Vector3.up);
            var animator = subject.Animator;
            var bones = animator.GetComponentsInChildren<Transform>();
            var thighs = (left: Bone(bones, "L_Thigh"), right: Bone(bones, "R_Thigh"));
            var ankles = new[] { Bone(bones, "L_Foot"), Bone(bones, "R_Foot") };
            var playable = AnimatorControllerPlayable.Create(graph, animator.runtimeAnimatorController);
            AnimationPlayableOutput.Create(graph, "Return", animator).SetSourcePlayable(playable);
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            graph.Play();
            graph.Evaluate(.25f);
            playable.CrossFadeInFixedTime("Base Layer.TurnAround", .22f, 0);
            Vector3 previous = PelvisFacing(thighs);
            float turnPeak = 0;
            float fraction = 0;
            for (int i = 0; i < 180 && fraction < 1; i++)
            {
                graph.Evaluate(dt);
                var state = playable.IsInTransition(0) ? playable.GetNextAnimatorStateInfo(0) : playable.GetCurrentAnimatorStateInfo(0);
                if (state.IsName("Base Layer.TurnAround")) fraction = Mathf.Clamp01(state.normalizedTime);
                subject.transform.rotation = Quaternion.AngleAxis(subject.ReturnTurnAngle(desiredYaw, fraction), Vector3.up) * before;
                Vector3 facing = PelvisFacing(thighs);
                turnPeak = Mathf.Max(turnPeak, Vector3.Angle(previous, facing));
                previous = facing;
            }
            if (fraction < 1 || Quaternion.Angle(subject.transform.rotation, after) > .2f || turnPeak > 6f)
                throw new InvalidOperationException($"Ivan's retimed turn did not settle facing the hall. Peak {turnPeak:F2}° in one frame.");

            playable.SetFloat(FinalLabIvanCharacterController.WalkRate, .30f);
            playable.CrossFadeInFixedTime("Base Layer.Walking", FinalLabIvanCharacterController.ReturnWalkBlend, 0);
            var route = FinalLabIvanCharacterController.RoundedRoute(talk, hall, inside);
            float length = 0;
            for (int i = 1; i < route.Length; i++) length += Vector3.Distance(route[i - 1], route[i]);
            float seamPeak = 0, footPeak = 0, distance = 0, turnVelocity = 0;
            bool settledGait = false, braking = false;
            var feet = ankles.Select(bone => bone.position).ToArray();
            for (int i = 0; i < 600 && distance < length; i++)
            {
                float next = FinalLabIvanCharacterController.AdvanceWalkingDistance(distance, length, 1.15f, dt);
                playable.SetFloat(FinalLabIvanCharacterController.WalkRate, Mathf.Clamp((next - distance) / (1.15f * dt), .30f, 1f));
                if (!braking && length - next < .18f)
                {
                    braking = true;
                    playable.CrossFadeInFixedTime("Base Layer.CalmIdle", .40f, 0);
                }
                Vector3 position = AtDistance(next);
                Vector3 step = position - subject.transform.position;
                if (step.sqrMagnitude > .000001f)
                {
                    float heading = Mathf.Atan2(step.x, step.z) * Mathf.Rad2Deg;
                    float yaw = Mathf.SmoothDampAngle(subject.transform.eulerAngles.y, heading, ref turnVelocity, .12f, 135f, dt);
                    subject.transform.rotation = Quaternion.Euler(0, yaw, 0);
                }
                subject.transform.position = position;
                distance = next;
                graph.Evaluate(dt);
                settledGait |= playable.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Walking");
                Vector3 facing = PelvisFacing(thighs);
                seamPeak = Mathf.Max(seamPeak, Vector3.Angle(previous, facing));
                previous = facing;
                for (int foot = 0; foot < feet.Length; foot++)
                {
                    footPeak = Mathf.Max(footPeak, Vector3.Distance(ankles[foot].position, feet[foot]));
                    feet[foot] = ankles[foot].position;
                }
            }
            for (int i = 0; i < 54; i++) graph.Evaluate(dt);
            if (seamPeak > 3f || footPeak > .065f || !settledGait || !braking || Vector3.Distance(inside, subject.transform.position) > .005f ||
                !playable.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.CalmIdle"))
                throw new InvalidOperationException($"The turn-to-walk blend had an abrupt pose change: {seamPeak:F2}° and {footPeak:F3} m.");
            Debug.Log($"Edit Mode at 120 Hz: retimed turn peak {turnPeak:F1}°, return walk peak {seamPeak:F1}°, largest step of either ankle {footPeak:F3} m. Ivan reaches the room and settles in CalmIdle.");

            Vector3 AtDistance(float metres)
            {
                for (int i = 1; i < route.Length; i++)
                {
                    float segment = Vector3.Distance(route[i - 1], route[i]);
                    if (metres <= segment) return Vector3.MoveTowards(route[i - 1], route[i], metres);
                    metres -= segment;
                }
                return inside;
            }
        }
        finally
        {
            graph.Destroy();
            Object.DestroyImmediate(subject.gameObject);
        }
    }
}
