using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

internal sealed class ErnestSittingIdleMaterials : AssetPostprocessor
{
    private const string CharacterDirectory = "Assets/Art/Models/Characters/MainHeroes/OldManErnest/";
    private const string ModelPath = CharacterDirectory + "Sitting Idle.fbx";
    private const string MaterialDirectory = CharacterDirectory + "textures/Materials/";

    // This FBX has no named material slots. Match the mesh groups in the source model.
    private static readonly Dictionary<string, string> MaterialByRenderer = new()
    {
        { "Ernest", "male_generic" },
        { "ErnestEyes", "high-poly.001" },
        { "Pants", "ErnestClothes" },
        { "Pullover", "ErnestClothes" },
        { "ErnestShirt", "ErnestClothes" },
        { "Glasses", "OldManHatEtc" },
        { "Hat", "OldManHatEtc" },
        { "ShoeL", "OldManHatEtc" },
        { "ShoeR", "OldManHatEtc" }
    };

    private static readonly string[] MaterialNames = MaterialByRenderer.Values.Distinct().ToArray();

    public override uint GetVersion() => 1;

    private void OnPreprocessModel()
    {
        if (assetPath != ModelPath)
            return;

        foreach (var materialName in MaterialNames)
            context.DependsOnArtifact(MaterialDirectory + materialName + ".mat");
    }

    private void OnPostprocessModel(GameObject model)
    {
        if (assetPath != ModelPath)
            return;

        var materials = new Dictionary<string, Material>();
        foreach (var materialName in MaterialNames)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialDirectory + materialName + ".mat");
            if (material == null)
            {
                context.LogImportError($"Missing material for Sitting Idle: {materialName}.mat");
                return;
            }

            materials.Add(materialName, material);
        }

        foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (!MaterialByRenderer.TryGetValue(renderer.name, out var materialName))
                continue;

            var slots = new Material[renderer.sharedMesh.subMeshCount];
            for (var index = 0; index < slots.Length; index++)
                slots[index] = materials[materialName];

            renderer.sharedMaterials = slots;
        }
    }
}
