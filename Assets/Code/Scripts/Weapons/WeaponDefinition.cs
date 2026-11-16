using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Karamazov/First Person Weapon")]
public sealed class WeaponDefinition : ScriptableObject
{
    [Serializable] public sealed class MaterialBinding
    {
        public string sourceName;
        public Material material;
    }
    public string displayName = "First Person Weapon";
    public string rigRoot = "";
    public GameObject modelPrefab;
    public RuntimeAnimatorController controller;
    public AnimationClip idleClip;
    public AnimationClip shotClip;
    public AnimationClip reloadClip;
    public AnimationClip emptyReloadClip;
    public AnimationClip equipClip;
    public MaterialBinding[] materialBindings;
    public string[] visibleRendererNames;
    // Retained solely to preserve serialization of the inactive previous resource.
    [HideInInspector] public Material handsMaterial;
    [HideInInspector] public Material pistolMaterial;
    [HideInInspector] public Material attachmentsMaterial;
    public Vector3 hipOffset = Vector3.zero;
    public Vector3 modelScale = Vector3.one;
    public string sightBone;
    public Vector3 sightOffsetMeters;
    public float aimDepthOffset;
    [Range(0f, 1f)] public float reloadInsertNormalizedTime = 1f;
    [Range(0f, 1f)] public float emptyReloadInsertNormalizedTime = 1f;
    public bool IsComplete
    {
        get
        {
            if (modelPrefab == null || controller == null || idleClip == null || shotClip == null ||
                reloadClip == null || emptyReloadClip == null || materialBindings == null ||
                materialBindings.Length == 0 || string.IsNullOrEmpty(rigRoot) || string.IsNullOrEmpty(sightBone)) return false;
            foreach (var binding in materialBindings)
                if (binding == null || string.IsNullOrEmpty(binding.sourceName) || binding.material == null) return false;
            return true;
        }
    }
}
