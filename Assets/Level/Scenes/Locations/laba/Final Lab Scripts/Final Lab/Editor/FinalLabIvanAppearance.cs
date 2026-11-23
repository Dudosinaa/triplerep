using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class FinalLabIvanAppearance
{
    internal const string Source = "Assets/Art/Animations/Laba";
    internal const string Generated = Source + "/Ivan/FinalScene Ivan";
    internal const string PrefabPath = Generated + "/FinalLab_Ivan.prefab";
    internal const string LogDirectory = "Logs/FinalLab/IvanCutscene";
    internal static readonly string[] Takes = { "Ivan_Walking", "Иван спокойно стоит", "Иван яростно спорит", "Иван грустно стоит", "Иван резко разворачивается" };

    public static void Build()
    {
        FinalLabInvestigationSurvey.RequireBatch();
        Directory.CreateDirectory(LogDirectory);
        if (!AssetDatabase.IsValidFolder(Generated)) AssetDatabase.CreateFolder(Source + "/Ivan", "FinalScene Ivan");
        var origin = Load<GameObject>(Source + "/Ivan/source/black_mesa_scientist_extended.glb");
        var secondary = Load<GameObject>(Source + "/scientist.glb");
        var actor = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(Source + "/Ivan_Walking.fbx"));
        var materials = origin.GetComponentsInChildren<SkinnedMeshRenderer>(true).SelectMany(mesh => mesh.sharedMaterials)
            .Distinct().ToDictionary(m => m.name);
        Debug.Log($"Ivan source skinned meshes: {origin.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length}. " +
            $"Secondary scientist: {secondary.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length}. " +
            $"Walking model: {actor.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length}.");
        var recipes = new (string slot, int albedo, int roughness, float fallback)[] {
            ("Base_M_D", 2, 3, .28f), ("body", 4, 5, .24f), ("ear_pencil", 6, 3, .25f),
            ("eyeball_l", 0, 1, .19f), ("eyeball_r", 0, -1, .18f), ("base_mouth", -1, -1, .21f)
        };
        var saved = new Dictionary<string, Material>();
        foreach (var recipe in recipes)
        {
            var original = materials[recipe.slot];
            if (recipe.albedo >= 0 && original.GetTexture("baseColorTexture") == null)
                throw new InvalidOperationException($"Original GLB slot {recipe.slot} has no base color map.");
            var path = Generated + "/Ivan_" + recipe.slot + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Ivan_" + recipe.slot };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", recipe.albedo < 0 ? original.GetColor("baseColorFactor") : Color.white);
            material.SetTexture("_BaseMap", recipe.albedo < 0 ? null : Load<Texture2D>(TexturePath(recipe.albedo)));
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", recipe.fallback);
            if (recipe.roughness >= 0)
            {
                material.SetTexture("_MetallicGlossMap", SmoothnessMap(recipe.roughness));
                material.SetFloat("_Smoothness", 1f);
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            EditorUtility.SetDirty(material);
            saved[recipe.slot] = material;
            Debug.Log($"{recipe.slot}: albedo={(recipe.albedo < 0 ? "original color factor" : TexturePath(recipe.albedo))}, roughness G={recipe.roughness}");
        }
        var mapped = new HashSet<string>();
        foreach (var mesh in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (mesh.sharedMesh == null || mesh.sharedMesh.vertexCount == 0 || !mesh.sharedMesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord0))
                throw new InvalidOperationException("The animated FBX has a skinned mesh without UV0: " + mesh.name);
            mesh.sharedMaterials = mesh.sharedMaterials.Select(material =>
            {
                string slot = saved.Keys.Single(key => material.name == key || material.name.StartsWith(key + " ", StringComparison.Ordinal) ||
                    material.name.StartsWith(key + ".", StringComparison.Ordinal));
                mapped.Add(slot);
                return saved[slot];
            }).ToArray();
            Debug.Log($"Animated mesh {mesh.name}: {mesh.sharedMesh.vertexCount} vertices, {mesh.bones.Length} bones, " +
                string.Join(", ", mesh.sharedMaterials.Select(material => material.name)));
        }
        if (mapped.Count != recipes.Length) throw new InvalidOperationException("Incomplete material mapping on the Walking FBX.");
        var animator = actor.GetComponent<Animator>();
        if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
            throw new InvalidOperationException("The walking FBX has no valid humanoid avatar.");
        animator.applyRootMotion = false;
        actor.name = "Ivan Karamazov";
        foreach (var clip in ReadClips())
        {
            if (!clip.humanMotion) throw new InvalidOperationException("Clip is not humanoid: " + AssetDatabase.GetAssetPath(clip));
            Debug.Log($"Ivan humanoid clip {AssetDatabase.GetAssetPath(clip)}: {clip.length:F2}s, loop {clip.isLooping}");
        }
        if (PrefabUtility.SaveAsPrefabAsset(actor, PrefabPath) == null) throw new InvalidOperationException("Ivan prefab could not be saved.");
        Object.DestroyImmediate(actor);
        AssetDatabase.SaveAssets();
        Debug.Log("FINAL_LAB_IVAN_MATERIALS_SAVED");
    }

    internal static AnimationClip[] ReadClips() => Takes.Select(name => AssetDatabase.LoadAllAssetsAtPath(Source + "/" + name + ".fbx")
        .OfType<AnimationClip>().Single(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal))).ToArray();

    private static string TexturePath(int index) => Source + "/Ivan/textures/gltf_embedded_" + index + ".png";

    private static Texture2D SmoothnessMap(int index)
    {
        string path = Generated + "/Gloss_from_Roughness_" + index + ".asset";
        var output = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (output != null) return output;
        var image = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
        if (!ImageConversion.LoadImage(image, File.ReadAllBytes(Source + "/Ivan/textures/gltf_embedded_" + index + "@channels=G.png")))
            throw new InvalidOperationException("Invalid Ivan roughness image " + index);
        var pixels = image.GetPixels32();
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(0, 0, 0, (byte)(255 - pixels[i].g));
        output = new Texture2D(image.width, image.height, TextureFormat.RGBA32, true, true) { name = "Ivan smoothness " + index };
        output.SetPixels32(pixels);
        output.Apply();
        EditorUtility.CompressTexture(output, TextureFormat.DXT5, TextureCompressionQuality.Best);
        AssetDatabase.CreateAsset(output, path);
        Object.DestroyImmediate(image);
        return output;
    }

    internal static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path) ??
        throw new InvalidOperationException("Missing Ivan resource: " + path);
}
