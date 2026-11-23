using System;
using UnityEngine;

/// <summary>Small pose overlay for the finale's supported body and departure.</summary>
public sealed class IvanFinaleActor : MonoBehaviour
{
    public bool Walking;
    public bool Deceased;
    private Transform spine;
    private Quaternion spineRest;
    private float recovery;
    private bool recovering;
    private Transform leftLeg, rightLeg, leftKnee, rightKnee, leftArm, rightArm;
    private Quaternion leftLegRest, rightLegRest, leftKneeRest, rightKneeRest, leftArmRest, rightArmRest;
    private void Awake()
    {
        spine = Bone("Spine", "Spine0"); spineRest = Rest(spine);
        leftLeg = Bone("LeftUpLeg", "LThigh"); rightLeg = Bone("RightUpLeg", "RThigh");
        leftKnee = Bone("LeftLeg", "LCalf"); rightKnee = Bone("RightLeg", "RCalf");
        leftArm = Bone("LeftArm", "LUpperArm"); rightArm = Bone("RightArm", "RUpperArm");
        leftLegRest = Rest(leftLeg); rightLegRest = Rest(rightLeg);
        leftKneeRest = Rest(leftKnee); rightKneeRest = Rest(rightKnee);
        leftArmRest = Rest(leftArm); rightArmRest = Rest(rightArm);
    }
    private Transform Bone(string first, string second)
    {
        foreach (var bone in GetComponentsInChildren<Transform>(true))
        {
            string name = bone.name.Replace("mixamorig:", "").Replace("Bip01", "").Replace(" ", "").Replace("_", "");
            if (name.Equals(first, StringComparison.OrdinalIgnoreCase) || name.Equals(second, StringComparison.OrdinalIgnoreCase)) return bone;
        }
        return null;
    }
    private static Quaternion Rest(Transform bone) => bone == null ? Quaternion.identity : bone.localRotation;
    public void BeginRecovery() { recovering = true; recovery = 0; }
    private void LateUpdate()
    {
        if (Deceased) Rotate(spine, spineRest, 35);
        if (recovering)
        {
            recovery = Mathf.Min(1, recovery + Time.deltaTime / 2);
            Rotate(spine, spineRest, 25 * (1 - Mathf.SmoothStep(0, 1, recovery)));
            recovering = recovery < 1;
        }
        if (!Walking) return;
        transform.position += transform.forward * (.8f * Time.deltaTime);
        float phase = Time.time * 7;
        float stride = Mathf.Sin(phase);
        Rotate(leftLeg, leftLegRest, stride * 22); Rotate(rightLeg, rightLegRest, -stride * 22);
        Rotate(leftKnee, leftKneeRest, Mathf.Max(0, -stride) * 27);
        Rotate(rightKnee, rightKneeRest, Mathf.Max(0, stride) * 27);
        Rotate(leftArm, leftArmRest, -stride * 9); Rotate(rightArm, rightArmRest, stride * 9);
    }
    private static void Rotate(Transform bone, Quaternion rest, float degrees)
    { if (bone != null) bone.localRotation = rest * Quaternion.Euler(degrees, 0, 0); }
}
