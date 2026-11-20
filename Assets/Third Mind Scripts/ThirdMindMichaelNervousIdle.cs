using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ThirdMindMichaelNervousIdle : MonoBehaviour
{
    private struct BonePose { public Transform bone; public Quaternion rest; public float phase; public bool finger; }
    private readonly List<BonePose> bones = new List<BonePose>();
    private readonly List<ArmPose> arms = new List<ArmPose>();
    private Transform spineBone, leftShoulder, rightShoulder;
    private Quaternion spineRest, leftShoulderRest, rightShoulderRest;
    private Vector3 spineRestPosition;
    private float nextTwitch, twitchStart, twitchDuration, twitchAmount;
    private bool twitchLeft;

    private struct ArmPose { public Transform upper, lower, hand, hip, thigh, knee; public Quaternion upperRest, lowerRest; public float phase; }

    private void Awake()
    {
        CaptureBones();
    }

    private void CaptureBones()
    {
        bones.Clear(); arms.Clear();
        var all = GetComponentsInChildren<Transform>(true);
        foreach (var bone in all)
        {
            string n = Normalize(bone.name);
            bool finger = n.Contains("finger") || n.Contains("thumb") || n.Contains("index") || n.Contains("middle") || n.Contains("ring") || n.Contains("pinky");
            bool hand = !finger && (n.Contains("hand") || n.Contains("wrist"));
            if (!finger && !hand) continue;
            bones.Add(new BonePose { bone = bone, rest = bone.localRotation, phase = Random.value * 6.28f, finger = finger });
        }
        AddArm(all, "left"); AddArm(all, "right");
        spineBone = all.FirstOrDefault(t => Normalize(t.name).Contains("upperchest") || Normalize(t.name).Contains("spine2") || Normalize(t.name).EndsWith("chest"));
        leftShoulder = all.FirstOrDefault(t => Normalize(t.name).Contains("leftshoulder"));
        rightShoulder = all.FirstOrDefault(t => Normalize(t.name).Contains("rightshoulder"));
        if (spineBone != null) { spineRest = spineBone.localRotation; spineRestPosition = spineBone.localPosition; }
        if (leftShoulder != null) leftShoulderRest = leftShoulder.localRotation;
        if (rightShoulder != null) rightShoulderRest = rightShoulder.localRotation;
        nextTwitch = Time.time + Random.Range(5.5f, 10.5f);
    }

    public void PoseHandsBesideThighsInEditor()
    {
        if (arms.Count == 0) CaptureBones();
        foreach (var arm in arms)
        {
            if (arm.upper == null || arm.lower == null || arm.hand == null || arm.thigh == null || arm.knee == null) continue;
            arm.upper.localRotation = arm.upperRest;
            arm.lower.localRotation = arm.lowerRest;
            SolveArm(arm.upper, arm.lower, arm.hand, GetTarget(arm));
        }
    }

    private void LateUpdate()
    {
        float t = Time.time;
        if (t >= nextTwitch)
        {
            twitchStart = t; twitchDuration = Random.Range(0.16f, 0.32f); twitchAmount = Random.Range(1.1f, 2.0f);
            twitchLeft = Random.value < 0.5f; nextTwitch = t + Random.Range(8f, 16f);
        }
        float twitch = t < twitchStart + twitchDuration ? Mathf.Sin(Mathf.PI * Mathf.Clamp01((t - twitchStart) / twitchDuration)) * twitchAmount : 0f;
        float breath = Mathf.Sin(t * 0.78f + 0.4f);
        if (spineBone != null)
        {
            spineBone.localRotation = spineRest * Quaternion.Euler(breath * 0.95f + twitch * 0.25f, 0f, 0f);
            spineBone.localPosition = spineRestPosition + Vector3.up * (0.35f + (breath + 1f) * 0.28f);
        }
        if (leftShoulder != null) leftShoulder.localRotation = leftShoulderRest * Quaternion.Euler(0f, 0f, Mathf.Sin(t * 0.55f + 0.7f) * 0.35f + (twitchLeft ? twitch : 0f));
        if (rightShoulder != null) rightShoulder.localRotation = rightShoulderRest * Quaternion.Euler(0f, 0f, Mathf.Sin(t * 0.51f + 2.1f) * 0.35f + (!twitchLeft ? twitch : 0f));
        foreach (var arm in arms)
        {
            if (arm.upper == null || arm.lower == null || arm.hand == null || arm.thigh == null || arm.knee == null) continue;
            arm.upper.localRotation = arm.upperRest;
            arm.lower.localRotation = arm.lowerRest;
            SolveArm(arm.upper, arm.lower, arm.hand, GetTarget(arm));
        }
        foreach (var pose in bones)
        {
            if (pose.bone == null) continue;
            float amplitude = pose.finger ? 1.2f : 0.45f;
            float motion = Mathf.Sin(t * (pose.finger ? 0.38f : 0.21f) + pose.phase) * amplitude;
            if (pose.finger && Mathf.Sin(t * 0.19f + pose.phase) > 0.94f) motion += 2.2f;
            pose.bone.localRotation = pose.rest * Quaternion.Euler(motion, motion * 0.45f, motion * 0.6f);
        }
    }

    private Vector3 GetTarget(ArmPose arm)
    {
        Vector3 lateral = arm.thigh.position - arm.hip.position;
        lateral.y = 0f;
        if (lateral.sqrMagnitude < 0.001f) lateral = arm.upper.right;
        lateral.Normalize();
        float drift = Mathf.Sin(Time.time * 0.115f + arm.phase) * 1.2f;
        return Vector3.Lerp(arm.thigh.position, arm.knee.position, 0.42f) + lateral * (9f + drift);
    }

    private void AddArm(Transform[] all, string side)
    {
        string NormalizeName(Transform t) => Normalize(t.name);
        var upper = all.FirstOrDefault(t => NormalizeName(t).Contains(side) && (NormalizeName(t).EndsWith("arm") || NormalizeName(t).Contains("upperarm")) && !NormalizeName(t).Contains("forearm") && !NormalizeName(t).Contains("shoulder"));
        var lower = all.FirstOrDefault(t => NormalizeName(t).Contains(side) && (NormalizeName(t).Contains("forearm") || NormalizeName(t).Contains("lowerarm")));
        var hand = all.FirstOrDefault(t => NormalizeName(t).Contains(side) && NormalizeName(t).EndsWith("hand"));
        var hip = all.FirstOrDefault(t => NormalizeName(t).EndsWith("hips"));
        var thigh = all.FirstOrDefault(t => NormalizeName(t).Contains(side) && (NormalizeName(t).EndsWith("upleg") || NormalizeName(t).Contains("upperleg") || NormalizeName(t).Contains("thigh")));
        var knee = all.FirstOrDefault(t => NormalizeName(t).Contains(side) && (NormalizeName(t).EndsWith("leg") || NormalizeName(t).Contains("lowerleg") || NormalizeName(t).Contains("calf")));
        if (upper == null || lower == null || hand == null || hip == null || thigh == null || knee == null) return;
        arms.Add(new ArmPose { upper = upper, lower = lower, hand = hand, hip = hip, thigh = thigh, knee = knee, upperRest = upper.localRotation, lowerRest = lower.localRotation, phase = Random.value * 6.28f });
    }

    private static void SolveArm(Transform upper, Transform lower, Transform hand, Vector3 target)
    {
        for (int i = 0; i < 10; i++)
        {
            RotateJointToward(lower, hand.position, target);
            RotateJointToward(upper, hand.position, target);
        }
    }

    private static void RotateJointToward(Transform joint, Vector3 end, Vector3 target)
    {
        Vector3 current = end - joint.position;
        Vector3 desired = target - joint.position;
        if (current.sqrMagnitude < 0.0001f || desired.sqrMagnitude < 0.0001f) return;
        Quaternion delta = Quaternion.FromToRotation(current, desired);
        joint.rotation = delta * joint.rotation;
    }

    private static string Normalize(string value) => new string(value.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
}
