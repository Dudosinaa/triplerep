using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

internal static partial class FinalLabFatherSetup
{
    private const string Generated = Scripts + "Father Sequence/";
    private const float ActorScale = .318f;
    private const float FrameRate = 30f;
    private const string Pelvis = "ValveBiped.Bip01_Pelvis";

    private sealed class PoseRig
    {
        public sealed class Bone
        {
            public Transform node;
            public Quaternion bindRotation;
            public Vector3 bindPosition;
        }

        public readonly GameObject Model;
        public readonly Dictionary<string, Bone> Bones;
        public readonly Transform[] Tracks;
        public readonly Transform Hips, Head, Chest, LeftFoot, RightFoot;
        public readonly Quaternion Frame;
        public readonly float Leg;
        public readonly SkinnedMeshRenderer[] Skins;

        public PoseRig(GameObject model, bool useSceneRest = false)
        {
            Model = model;
            Skins = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Bones = new Dictionary<string, Bone>(StringComparer.Ordinal);
            foreach (var skin in Skins.OrderByDescending(s => s.bones.Length))
            {
                var bind = skin.sharedMesh.bindposes;
                var nodes = skin.bones;
                for (int i = 0; i < nodes.Length; i++)
                {
                    if (nodes[i] == null) continue;
                    string name = JointName(nodes[i]);
                    if (!name.StartsWith("ValveBiped.Bip01_", StringComparison.Ordinal) || Bones.ContainsKey(name)) continue;
                    var rest = skin.localToWorldMatrix * bind[i].inverse;
                    Bones.Add(name, new Bone
                    {
                        node = nodes[i],
                        bindPosition = useSceneRest ? nodes[i].position : (Vector3)rest.GetColumn(3),
                        bindRotation = useSceneRest ? nodes[i].rotation : rest.rotation
                    });
                }
            }
            var registered = Bones.Values.Select(bone => bone.node).ToHashSet();
            Tracks = model.GetComponentsInChildren<Transform>(true).Where(registered.Contains).ToArray();
            Hips = Bones[Pelvis].node;
            Head = BoneOf("Head1");
            Chest = BoneOf("Spine2");
            LeftFoot = BoneOf("L_Foot");
            RightFoot = BoneOf("R_Foot");
            Vector3 right = (BindPosition("R_Thigh") - BindPosition("L_Thigh")).normalized;
            Vector3 up = (BindPosition("Spine2") - Bones[Pelvis].bindPosition).normalized;
            Frame = Quaternion.LookRotation(Vector3.Cross(right, up), up);
            Leg = Vector3.Distance(BindPosition("L_Thigh"), BindPosition("L_Calf")) + Vector3.Distance(BindPosition("L_Calf"), BindPosition("L_Foot"));
        }

        public Transform BoneOf(string name) => Bones["ValveBiped.Bip01_" + name].node;
        private Vector3 BindPosition(string name) => Bones["ValveBiped.Bip01_" + name].bindPosition;
        public Vector3 Feet => (LeftFoot.position + RightFoot.position) * .5f;
    }

    private sealed class FatherPose
    {
        public Vector3 hips;
        public Quaternion[] rotations;

        public static FatherPose Read(PoseRig rig) => new FatherPose
        {
            hips = rig.Hips.localPosition,
            rotations = rig.Tracks.Select(node => node.localRotation).ToArray()
        };

        public void Apply(PoseRig rig)
        {
            rig.Hips.localPosition = hips;
            for (int i = 0; i < rotations.Length; i++) rig.Tracks[i].localRotation = rotations[i];
        }
    }

    private static AnimationClip SourceClip(string file) => AssetDatabase.LoadAllAssetsAtPath(Sources + file + ".fbx")
        .OfType<AnimationClip>().Single(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal));

    private static FatherPose[] Transfer(PoseRig target, string file, Vector3 standingFeet, float floor, bool anchorEnd, bool ground = true)
    {
        var sourceModel = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Sources + file + ".fbx"));
        var mesh = new Mesh();
        try
        {
            var source = new PoseRig(sourceModel);
            var motion = SourceClip(file);
            var alignment = target.Frame * Quaternion.Inverse(source.Frame);
            var inverse = Quaternion.Inverse(alignment);
            var mapping = target.Tracks.Select(node => (target.Bones[JointName(node)], source.Bones.GetValueOrDefault(JointName(node))))
                .Where(pair => pair.Item2 != null).ToArray();
            if (mapping.Length < 48 || motion.humanMotion) throw new InvalidOperationException($"Incompatible father animation: {file}, {mapping.Length} matching joints.");

            void Sample(float time)
            {
                motion.SampleAnimation(sourceModel, time);
                foreach (var (destination, start) in mapping)
                    destination.node.rotation = alignment * start.node.rotation * Quaternion.Inverse(start.bindRotation) * inverse * destination.bindRotation;
                target.Hips.position = target.Bones[Pelvis].bindPosition + alignment * (source.Hips.position - source.Bones[Pelvis].bindPosition) * (target.Leg / source.Leg);
            }

            Sample(anchorEnd ? motion.length : 0f);
            Vector3 offset = Vector3.ProjectOnPlane(standingFeet - target.Feet, Vector3.up);
            Debug.Log($"Father transfer {file}: leg ratio {target.Leg / source.Leg:F3}, alignment={alignment.eulerAngles:F1}, sourceBind={source.Bones[Pelvis].bindPosition:F3}, targetBind={target.Bones[Pelvis].bindPosition:F3}, offset={offset:F3}, anchorFeet={target.Feet:F3}.");
            int count = Mathf.RoundToInt(motion.length * FrameRate) + 1;
            var frames = new FatherPose[count];
            for (int i = 0; i < count; i++)
            {
                float time = motion.length * i / (count - 1);
                Sample(time);
                target.Hips.position += offset;
                if (ground) Ground(target, mesh, floor + .007f);
                if (i == 0 || i == count - 1)
                    Debug.Log($"Father pose {file} {i}/{count - 1}: pelvis={target.Hips.position:F3}, feet={target.Feet:F3}, head={target.Head.position:F3}.");
                frames[i] = FatherPose.Read(target);
            }
            Debug.Log($"Father {file}: {mapping.Length} matched joints, {motion.length:F2} seconds.");
            return frames;
        }
        finally
        {
            Object.DestroyImmediate(mesh);
            Object.DestroyImmediate(sourceModel);
        }
    }

    private static FatherPose[] StageDeath(PoseRig rig, Vector3 feet, float floor, BoxCollider bed, Transform[] scene, bool relaxLegs = true)
    {
        var source = Transfer(rig, SourceNames[4], feet, floor, false);
        Physics.SyncTransforms();
        var obstacles = scene.Select(node => node.GetComponent<BoxCollider>()).Where(box => box != null && box.enabled &&
            !box.isTrigger && box.bounds.max.y > floor + .08f && box.bounds.min.y < floor + 1.85f &&
            (box == bed || box.bounds.SqrDistance(feet + Vector3.up * .6f) < 6.25f)).Select(box =>
            (box.bounds, inverse: box.transform.worldToLocalMatrix, box.center, half: box.size * .5f - Vector3.one * .015f, name: box.name, isBed: box == bed)).ToArray();
        var mesh = new Mesh();
        int deepest = -1;
        string blockage = "";
        try
        {
            foreach (float side in new[] { 0f, -.23f, .23f, -.39f, .39f, -.54f, .54f })
                foreach (float shift in new[] { .37f, .50f, .62f, .24f, .72f })
                foreach (float yaw in new[] { -88f, -94f, -100f, -82f, -106f, 94f, 106f, -116f, 116f, -138f, 138f, -158f, 158f })
                {
                    var result = new FatherPose[source.Length];
                    bool hit = false;
                    for (int frame = 0; frame < result.Length; frame++)
                    {
                        source[frame].Apply(rig);
                        float time = frame / FrameRate;
                        float blend = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.12f, 1.21f, time));
                        float stumble = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.03f, .74f, time));
                        var rotation = Quaternion.AngleAxis(yaw * blend, Vector3.up);
                        Vector3 stumbleOffset = rig.Model.transform.forward * shift + rig.Model.transform.right * side;
                        rig.Hips.position = feet + rotation * (rig.Hips.position - feet) + stumbleOffset * stumble;
                        rig.Hips.rotation = rotation * rig.Hips.rotation;
                        float settle = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1.57f, 2.40f, time));
                        if (settle > 0)
                        {
                            rig.Hips.position += Vector3.down * (.11f * settle);
                            foreach (string sideName in new[] { "L", "R" })
                            {
                                LiftEndpoint(rig.BoneOf(sideName + "_UpperArm"), rig.BoneOf(sideName + "_Forearm"), floor + .078f * settle);
                                LiftEndpoint(rig.BoneOf(sideName + "_Forearm"), rig.BoneOf(sideName + "_Hand"), floor + .077f * settle);
                                LiftEndpoint(rig.BoneOf(sideName + "_Calf"), rig.BoneOf(sideName + "_Foot"), floor + .105f * settle);
                            }
                            Ground(rig, mesh, floor + .007f);
                        }
                        float relax = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1.78f, 2.40f, time));
                        if (relaxLegs && relax > 0)
                        {
                            RelaxLegsAfterFall(rig, floor, relax);
                            Ground(rig, mesh, floor + .007f);
                        }
                        string obstacle = Obstruction();
                        if (obstacle != null)
                        {
                            if (frame > deepest) { deepest = frame; blockage = $"{yaw:F0}°/{shift:F2} m/{side:F2} m hit {obstacle} at sample {frame}"; }
                            hit = true;
                            break;
                        }
                        result[frame] = FatherPose.Read(rig);
                    }
                    if (hit) continue;
                    Debug.Log($"Father collapse: {yaw:F0}° rotation with {shift:F2} m forward and {side:F2} m lateral drift, skin clear of bed and {obstacles.Length} nearby solids in all {result.Length} samples.");
                    Debug.Log($"Father's held pose after floor support: head={rig.Head.position.y - floor:F3} m, pelvis={rig.Hips.position.y - floor:F3} m.");
                    Debug.Log($"Father final legs: knees {rig.BoneOf("L_Calf").position.y - floor:F3}/{rig.BoneOf("R_Calf").position.y - floor:F3} m; foot separation {Vector3.Distance(rig.LeftFoot.position, rig.RightFoot.position):F3} m.");
                    return result;
                }
            throw new InvalidOperationException("The father collapse could not be safely placed beside the bed. " + blockage);
        }
        finally { Object.DestroyImmediate(mesh); }

        string Obstruction()
        {
            foreach (var skin in rig.Skins)
            {
                skin.BakeMesh(mesh);
                foreach (Vector3 vertex in mesh.vertices)
                {
                    Vector3 world = skin.transform.TransformPoint(vertex);
                    foreach (var obstacle in obstacles)
                    {
                        if (!obstacle.bounds.Contains(world)) continue;
                        Vector3 local = obstacle.inverse.MultiplyPoint3x4(world) - obstacle.center;
                        // Between the end frames, the visible bed has open space under its raised mattress.
                        if (obstacle.isBed && Mathf.Abs(local.x) < obstacle.half.x - .18f &&
                            (world.y < floor + .38f || world.y > floor + .72f)) continue;
                        if (Mathf.Abs(local.x) < obstacle.half.x && Mathf.Abs(local.y) < obstacle.half.y && Mathf.Abs(local.z) < obstacle.half.z)
                            return $"{obstacle.name} at {world:F2}";
                    }
                }
            }
            return null;
        }
    }

    private static void LiftEndpoint(Transform joint, Transform child, float lowerLimit)
    {
        Vector3 segment = child.position - joint.position;
        float vertical = lowerLimit - joint.position.y;
        if (segment.y >= vertical || segment.sqrMagnitude < .00001f) return;
        float limit = segment.magnitude * .94f;
        vertical = Mathf.Clamp(vertical, -limit, limit);
        Vector3 level = Vector3.ProjectOnPlane(segment, Vector3.up);
        if (level.sqrMagnitude < .00001f) return;
        Vector3 desired = level.normalized * Mathf.Sqrt(segment.sqrMagnitude - vertical * vertical) + Vector3.up * vertical;
        joint.rotation = Quaternion.FromToRotation(segment, desired) * joint.rotation;
    }

    private static void RelaxLegsAfterFall(PoseRig rig, float floor, float amount)
    {
        Vector3 towardsFeet = Vector3.ProjectOnPlane(rig.Hips.position - rig.Chest.position, Vector3.up).normalized;
        towardsFeet = Quaternion.RotateTowards(Quaternion.LookRotation(towardsFeet, Vector3.up),
            Quaternion.LookRotation(rig.Model.transform.forward, Vector3.up), 19f) * Vector3.forward;
        Vector3 sideways = Vector3.ProjectOnPlane(rig.BoneOf("L_Thigh").position - rig.BoneOf("R_Thigh").position, Vector3.up).normalized;
        foreach (string side in new[] { "L", "R" })
        {
            float sideSign = side == "L" ? 1 : -1;
            Vector3 legDirection = (towardsFeet + sideways * (.16f * sideSign)).normalized;
            var thigh = rig.BoneOf(side + "_Thigh");
            var knee = rig.BoneOf(side + "_Calf");
            var ankle = rig.BoneOf(side + "_Foot");
            float upper = Vector3.Distance(thigh.position, knee.position);
            float lower = Vector3.Distance(knee.position, ankle.position);
            Vector3 goal = thigh.position + legDirection * ((upper + lower) * .87f);
            goal.y = floor + .12f;
            var reach = Vector3.Lerp(ankle.position, goal, amount) - thigh.position;
            Vector3 axis = reach.normalized;
            float distance = Mathf.Clamp(reach.magnitude, Mathf.Abs(upper - lower) + .001f, upper + lower - .001f);
            float kneeProjection = (upper * upper + distance * distance - lower * lower) / (2f * distance);
            float kneeOffset = Mathf.Sqrt(Mathf.Max(0f, upper * upper - kneeProjection * kneeProjection));
            Vector3 oldBend = Vector3.ProjectOnPlane(knee.position - thigh.position, axis).normalized;
            Vector3 finalBend = Vector3.ProjectOnPlane(sideways * sideSign + Vector3.up * .12f, axis).normalized;
            Vector3 bend = Vector3.Slerp(oldBend, finalBend, amount).normalized;
            Aim(thigh, knee, axis * kneeProjection + bend * kneeOffset);
            Aim(knee, ankle, thigh.position + axis * distance - knee.position);
            var toe = rig.BoneOf(side + "_Toe0");
            var toeVector = toe.position - ankle.position;
            var relaxedToe = legDirection + sideways * (.22f * sideSign) + Vector3.up * .32f;
            var aligned = Quaternion.FromToRotation(toeVector, relaxedToe) * ankle.rotation;
            ankle.rotation = Quaternion.Slerp(ankle.rotation, aligned, amount);
        }

        static void Aim(Transform joint, Transform child, Vector3 direction) =>
            joint.rotation = Quaternion.FromToRotation(child.position - joint.position, direction) * joint.rotation;
    }

    private static void BakeDeathBlendSupport(PoseRig rig, Animator animator, AnimationClip death, FatherPose[] frames, float floor)
    {
        const float delta = 1f / 120f;
        var offset = new float[frames.Length];
        var sample = Object.Instantiate(animator.gameObject);
        var skins = sample.GetComponentsInChildren<SkinnedMeshRenderer>();
        var mesh = new Mesh();
        float baseline = float.PositiveInfinity;
        try
        {
            foreach (float phase in new[] { 0f, 1.25f, 2.5f, 3.75f, 5f, 6.25f, 7.5f, 8.8f, 9.25f })
            {
                var graph = PlayableGraph.Create("Father fall floor sampling in Edit Mode");
                try
                {
                    var playable = AnimatorControllerPlayable.Create(graph, animator.runtimeAnimatorController);
                    AnimationPlayableOutput.Create(graph, "Father", sample.GetComponent<Animator>()).SetSourcePlayable(playable);
                    graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                    graph.Play();
                    graph.Evaluate(0);
                    playable.PlayInFixedTime("Base Layer.StandingIdle", 0, phase);
                    graph.Evaluate(0);
                    playable.CrossFadeInFixedTime("Base Layer.Death", FatherCutsceneController.DeathBlend, 0);
                    for (int i = 0; i < Mathf.CeilToInt((death.length + .15f) / delta); i++)
                    {
                        graph.Evaluate(delta);
                        bool blending = playable.IsInTransition(0);
                        var state = blending ? playable.GetNextAnimatorStateInfo(0) : playable.GetCurrentAnimatorStateInfo(0);
                        if (!state.IsName("Base Layer.Death")) continue;
                        float lowest = float.PositiveInfinity;
                        foreach (var skin in skins)
                        {
                            skin.BakeMesh(mesh);
                            var matrix = skin.transform.localToWorldMatrix;
                            foreach (var vertex in mesh.vertices) lowest = Mathf.Min(lowest, matrix.MultiplyPoint3x4(vertex).y);
                        }
                        baseline = Mathf.Min(baseline, lowest - floor);
                        float weight = blending ? Mathf.Clamp01(playable.GetAnimatorTransitionInfo(0).normalizedTime) : 1f;
                        float raise = Mathf.Clamp((floor + .008f - lowest) / Mathf.Max(.16f, weight) * 1.18f, 0f, .10f);
                        int left = Mathf.FloorToInt(Mathf.Clamp01(state.normalizedTime) * (offset.Length - 1));
                        offset[left] = Mathf.Max(offset[left], raise);
                        int right = Mathf.Min(left + 1, offset.Length - 1);
                        offset[right] = Mathf.Max(offset[right], raise);
                    }
                }
                finally { graph.Destroy(); }
            }
        }
        finally
        {
            Object.DestroyImmediate(mesh);
            Object.DestroyImmediate(sample);
        }
        if (!float.IsFinite(baseline)) throw new InvalidOperationException("The floor probe did not evaluate the Death state.");
        for (int i = offset.Length - 2; i >= 0; i--) offset[i] = Mathf.Max(offset[i], offset[i + 1] - .012f);
        for (int i = 1; i < offset.Length; i++) offset[i] = Mathf.Max(offset[i], offset[i - 1] - .012f);
        var localUp = rig.Hips.parent.InverseTransformVector(Vector3.up);
        for (int i = 0; i < frames.Length; i++) frames[i].hips += localUp * offset[i];
        SaveMotion(rig, FatherCutsceneController.Death, frames);
        Debug.Log($"Death blend floor support sampled at 120 Hz across nine phases: baseline {baseline:F3} m, peak correction {offset.Max():F3} m, held pose correction {offset[^1]:F3} m.");
    }

    private static void Ground(PoseRig rig, Mesh buffer, float height)
    {
        float lowest = float.PositiveInfinity;
        foreach (var skin in rig.Skins)
        {
            skin.BakeMesh(buffer);
            foreach (Vector3 vertex in buffer.vertices) lowest = Mathf.Min(lowest, skin.transform.TransformPoint(vertex).y);
        }
        rig.Hips.position += Vector3.up * (height - lowest);
    }

    private static FatherPose[] BuildWakeUp(PoseRig rig, FatherPose seated, Vector3 feet, float floor, BoxCollider bedBox,
        Transform bed, Vector3 pillow, Vector3 aisle, Vector3 along, float mattress)
    {
        seated.Apply(rig);
        Vector3 seat = rig.Hips.position;
        var frames = Transfer(rig, WakeSource, feet, floor, false, ground: false);
        Vector3 lyingForward = Vector3.Dot(pillow - bedBox.bounds.center, along) > 0 ? -along : along;
        float heading = Vector3.SignedAngle(aisle, lyingForward, Vector3.up);
        frames[0].Apply(rig);
        Vector3 rawStart = rig.Hips.position;
        Vector3 start = pillow + lyingForward * .09f + Vector3.ProjectOnPlane(
            Quaternion.AngleAxis(heading, Vector3.up) * (rig.Hips.position - rig.Head.position), Vector3.up);
        start.y = mattress + .145f;
        frames[^1].Apply(rig);
        Vector3 rawEnd = rig.Hips.position;
        var bedHeight = MattressHeight(bedBox, bed, floor);
        var mesh = new Mesh();
        var lift = new float[frames.Length];
        try
        {
            for (int i = 0; i < frames.Length; i++)
            {
                float part = (float)i / (frames.Length - 1);
                float turn = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.54f, .91f, part));
                float travel = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.57f, .93f, part));
                float settle = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.82f, 1f, part));
                var rotation = Quaternion.AngleAxis(heading * (1f - turn), Vector3.up);
                frames[i].Apply(rig);
                Vector3 authored = rig.Hips.position - Vector3.Lerp(rawStart, rawEnd, travel);
                rig.Hips.position = Vector3.Lerp(start, seat, travel) + rotation * (authored * .35f);
                rig.Hips.rotation = rotation * rig.Hips.rotation;
                float headSupport = 10f * (1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.09f, .36f, part)));
                rig.BoneOf("Spine").rotation = Quaternion.AngleAxis(headSupport, Vector3.Cross(Vector3.up, lyingForward)) * rig.BoneOf("Spine").rotation;
                if (settle > 0)
                {
                    for (int b = 0; b < rig.Tracks.Length; b++)
                        rig.Tracks[b].localRotation = Quaternion.Slerp(rig.Tracks[b].localRotation, seated.rotations[b], settle);
                    rig.Hips.localPosition = Vector3.Lerp(rig.Hips.localPosition, seated.hips, settle);
                }
                float needed = 0;
                foreach (var skin in rig.Skins)
                {
                    skin.BakeMesh(mesh);
                    foreach (Vector3 vertex in mesh.vertices)
                    {
                        var world = skin.transform.TransformPoint(vertex);
                        float surface = bedHeight(world);
                        float allowedCompression = Mathf.Lerp(.001f, .022f, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.53f, .87f, part)));
                        float fromBed = surface > floor + .2f ? (surface + .009f - allowedCompression - world.y) * (1 - settle) : 0f;
                        needed = Mathf.Max(needed, Mathf.Max(fromBed, floor + .006f - world.y));
                    }
                }
                lift[i] = needed;
                frames[i] = FatherPose.Read(rig);
            }
        }
        finally { Object.DestroyImmediate(mesh); }

        for (int i = lift.Length - 2; i >= 0; i--) lift[i] = Mathf.Max(lift[i], lift[i + 1] - .035f);
        for (int i = 1; i < lift.Length - 1; i++) lift[i] = Mathf.Max(lift[i], lift[i - 1] - .035f);
        for (int i = 0; i < frames.Length - 1; i++)
        {
            float smooth = (lift[Mathf.Max(0, i - 1)] + 2f * lift[i] + lift[i + 1]) * .25f;
            frames[i].hips += rig.Hips.parent.InverseTransformVector(Vector3.up * Mathf.Max(smooth, lift[i]));
        }
        frames[^1] = seated;
        frames[0].Apply(rig);
        float pillowGap = Vector3.ProjectOnPlane(rig.Head.position - pillow, Vector3.up).magnitude;
        if (pillowGap > .24f || Vector3.Dot(rig.Head.position - rig.Hips.position, -lyingForward) < .48f)
            throw new InvalidOperationException($"Wake Up head is not on the pillow side: {rig.Head.position:F3}, pillow {pillow:F3}.");
        Debug.Log($"Wake Up from {WakeSource}.fbx: {frames.Length} poses, head {rig.Head.position:F3}, pillow {pillow:F3}, horizontal gap {pillowGap:F3} m, peak mattress support {lift.Max():F3} m. The last pose exactly matches StandUp.");
        return frames;
    }

    private static Func<Vector3, float> MattressHeight(BoxCollider box, Transform bed, float floor)
    {
        const int resolution = 50;
        var top = new float[resolution + 1, resolution + 1];
        var probes = bed.GetComponentsInChildren<MeshFilter>().Where(filter => filter.name is "SM_Bed_01_S" or "SM_BedBedding_01").Select(filter =>
        {
            var temporary = new GameObject("Mattress surface sampling");
            temporary.transform.SetPositionAndRotation(filter.transform.position, filter.transform.rotation);
            temporary.transform.localScale = filter.transform.lossyScale;
            var collider = temporary.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh;
            return collider;
        }).ToArray();
        try
        {
            Physics.SyncTransforms();
            for (int x = 0; x <= resolution; x++)
                for (int z = 0; z <= resolution; z++)
                {
                    Vector3 at = box.transform.TransformPoint(box.center + new Vector3(
                        ((float)x / resolution - .5f) * box.size.x, 0, ((float)z / resolution - .5f) * box.size.z));
                    at.y = box.bounds.max.y + .6f;
                    float highest = floor;
                    foreach (var probe in probes)
                        if (probe.Raycast(new Ray(at, Vector3.down), out var hit, 3f)) highest = Mathf.Max(highest, hit.point.y);
                    top[x, z] = highest;
                }
        }
        finally
        {
            foreach (var probe in probes) Object.DestroyImmediate(probe.gameObject);
        }
        return world =>
        {
            Vector3 local = box.transform.InverseTransformPoint(world) - box.center;
            float x = (local.x / box.size.x + .5f) * resolution;
            float z = (local.z / box.size.z + .5f) * resolution;
            if (x < 0 || x > resolution || z < 0 || z > resolution) return floor;
            int x0 = Mathf.FloorToInt(x), z0 = Mathf.FloorToInt(z);
            int x1 = Mathf.Min(x0 + 1, resolution), z1 = Mathf.Min(z0 + 1, resolution);
            return Mathf.Lerp(Mathf.Lerp(top[x0, z0], top[x1, z0], x - x0),
                Mathf.Lerp(top[x0, z1], top[x1, z1], x - x0), z - z0);
        };
    }

    private static AnimationClip SaveMotion(PoseRig rig, string state, FatherPose[] poses, bool loop = false)
    {
        var clip = new AnimationClip { name = "Father_" + state, frameRate = FrameRate };
        for (int bone = 0; bone < rig.Tracks.Length; bone++)
        {
            int index = bone;
            string path = AnimationUtility.CalculateTransformPath(rig.Tracks[bone], rig.Model.transform);
            var rotations = poses.Select(frame => frame.rotations[index]).ToArray();
            for (int i = 1; i < rotations.Length; i++)
                if (Quaternion.Dot(rotations[i - 1], rotations[i]) < 0)
                {
                    var r = rotations[i];
                    rotations[i] = new Quaternion(-r.x, -r.y, -r.z, -r.w);
                }
            for (int axis = 0; axis < 4; axis++)
            {
                int component = axis;
                clip.SetCurve(path, typeof(Transform), "m_LocalRotation." + "xyzw"[component], SampleCurve(rotations.Select(r => r[component]).ToArray()));
            }
        }
        string pelvis = AnimationUtility.CalculateTransformPath(rig.Hips, rig.Model.transform);
        for (int axis = 0; axis < 3; axis++)
        {
            int component = axis;
            clip.SetCurve(pelvis, typeof(Transform), "m_LocalPosition." + "xyz"[component], SampleCurve(poses.Select(p => p.hips[component]).ToArray()));
        }
        clip.EnsureQuaternionContinuity();
        if (state == FatherCutsceneController.Death)
        {
            var cloth = rig.Skins.SingleOrDefault(s => s.sharedMesh.GetBlendShapeIndex(FinalHemShape) >= 0);
            if (cloth != null)
            {
                string clothPath = AnimationUtility.CalculateTransformPath(cloth.transform, rig.Model.transform);
                clip.SetCurve(clothPath, typeof(SkinnedMeshRenderer), "blendShape." + FinalHemShape,
                    AnimationCurve.EaseInOut((poses.Length - 1) / FrameRate - .53f, 0, (poses.Length - 1) / FrameRate, 100));
            }
        }
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        return Save(clip, Generated + clip.name + ".anim");
    }

    private static AnimationCurve SampleCurve(float[] values)
    {
        int last = values.Length - 1;
        if (values.All(value => Mathf.Abs(value - values[0]) < .000001f)) return AnimationCurve.Constant(0, last / FrameRate, values[0]);
        var keys = new Keyframe[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            float incoming = i > 0 ? (values[i] - values[i - 1]) * FrameRate : 0;
            float outgoing = i < last ? (values[i + 1] - values[i]) * FrameRate : 0;
            keys[i] = new Keyframe(i / FrameRate, values[i], incoming, outgoing);
        }
        return new AnimationCurve(keys);
    }

    private static T Save<T>(T asset, string path) where T : Object
    {
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
        EditorUtility.CopySerialized(asset, existing);
        EditorUtility.SetDirty(existing);
        Object.DestroyImmediate(asset);
        return existing;
    }
}
