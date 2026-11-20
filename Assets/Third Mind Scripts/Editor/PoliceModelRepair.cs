#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PoliceModelRepair
{
    private const string ModelPath = "Assets/Art/Models/Characters/MindHeroes/ThirdMind/Police-Man/Police-Man.glb";
    private const string ScenePath = "Assets/Level/Scenes/Locations/MindsScenes/ThirdMind-interogation_room.unity";
    private const string MaterialFolder = "Assets/Art/Models/Characters/MindHeroes/ThirdMind/Police-Man/PoliceMaterials";
    private const string MeshFolder = "Assets/Art/Models/Characters/MindHeroes/ThirdMind/Police-Man/PosedMeshes";

    [MenuItem("Tools/Third Mind/Repair Police Materials and Lower Arms")]
    public static void Repair()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (!shader || !shader.isSupported) throw new System.InvalidOperationException("URP/Lit shader is unavailable.");

        EnsureFolder("Assets/Art/Models/Characters/MindHeroes/ThirdMind/Police-Man", "PoliceMaterials");
        EnsureFolder("Assets/Art/Models/Characters/MindHeroes/ThirdMind/Police-Man", "PosedMeshes");
        var textures = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Texture2D>().ToDictionary(t => t.name);
        var materialSpecs = new[]
        {
            new MaterialSpec("Body", 0, 2), new MaterialSpec("Bottom", 4, 6),
            new MaterialSpec("Eyewear", 8, 10), new MaterialSpec("material", 12, 14),
            new MaterialSpec("Shoes", 16, 18), new MaterialSpec("material_5", 20, 22)
        };
        var materials = new System.Collections.Generic.Dictionary<string, Material>();
        foreach (var spec in materialSpecs)
        {
            if (!textures.TryGetValue("image_" + spec.BaseIndex, out var baseMap) ||
                !textures.TryGetValue("image_" + spec.NormalIndex, out var normalMap))
                throw new System.InvalidOperationException($"Embedded base/normal texture missing for {spec.Name}.");

            var path = MaterialFolder + "/Police " + spec.Name + " URP.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else material.shader = shader;
            material.name = "Police " + spec.Name + " URP";
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", baseMap);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", spec.Name == "Shoes" || spec.Name == "Eyewear" ? 0.24f : 0.16f);
            material.SetTexture("_BumpMap", normalMap);
            material.SetFloat("_BumpScale", 0.7f);
            material.EnableKeyword("_NORMALMAP");
            material.SetTexture("_EmissionMap", baseMap);
            material.SetColor("_EmissionColor", new Color(0.35f, 0.35f, 0.35f, 1f));
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(material);
            materials.Add(spec.Name, material);
        }

        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var police = GameObject.Find("Police-Man");
        if (!police) throw new System.InvalidOperationException("Police-Man scene instance not found; scene left unchanged.");
        var originalLocalPosition = police.transform.localPosition;
        var originalLocalRotation = police.transform.localRotation;
        var originalLocalScale = police.transform.localScale;

        foreach (var filter in police.GetComponentsInChildren<MeshFilter>(true))
        {
            var source = filter.sharedMesh;
            if (!source || (source.name != "Object_2" && source.name != "Object_5")) continue;
            var posePath = MeshFolder + "/" + source.name + " Police Arms Down.asset";
            var posed = AssetDatabase.LoadAssetAtPath<Mesh>(posePath);
            if (!posed)
            {
                posed = Object.Instantiate(source);
                posed.name = source.name + " Police Arms Down";
                PoseArmsDown(posed);
                AssetDatabase.CreateAsset(posed, posePath);
            }
            filter.sharedMesh = posed;
            PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
        }

        foreach (var renderer in police.GetComponentsInChildren<Renderer>(true))
        {
            var sourceMaterials = renderer.sharedMaterials;
            var changed = false;
            for (var i = 0; i < sourceMaterials.Length; i++)
            {
                if (sourceMaterials[i] && materials.TryGetValue(sourceMaterials[i].name, out var fixedMaterial))
                {
                    sourceMaterials[i] = fixedMaterial;
                    changed = true;
                }
            }
            if (!changed) continue;
            renderer.sharedMaterials = sourceMaterials;
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        }

        if (police.transform.localPosition != originalLocalPosition ||
            police.transform.localRotation != originalLocalRotation || police.transform.localScale != originalLocalScale)
            throw new System.InvalidOperationException("Police transform changed unexpectedly; refusing to save.");

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"POLICE_REPAIR_DONE scene={scene.path} positionPreserved={police.transform.localPosition} rotationPreserved={police.transform.localRotation.eulerAngles} materials={materials.Count} posedMeshes=Object_2,Object_5 rig=none");
    }

    private static void EnsureFolder(string parent, string name)
    {
        var path = parent + "/" + name;
        if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, name);
    }

    private static void PoseArmsDown(Mesh mesh)
    {
        var vertices = mesh.vertices;
        var normals = mesh.normals;
        var tangents = mesh.tangents;
        for (var i = 0; i < vertices.Length; i++)
        {
            var p = vertices[i];
            var side = Mathf.Sign(p.x);
            var distance = Mathf.Abs(p.x);
            if (side == 0f || distance < 18f || p.z < 92f || p.z > 161f) continue;
            var t = Mathf.Clamp01((distance - 18f) / 40f);
            t = t * t * (3f - 2f * t);
            var angle = side * Mathf.Lerp(8f, 82f, t) * Mathf.Deg2Rad;
            var c = Mathf.Cos(angle);
            var s = Mathf.Sin(angle);
            const float shoulderZ = 137f;
            var shoulderX = side * 24f;
            var dx = p.x - shoulderX;
            var dz = p.z - shoulderZ;
            vertices[i] = new Vector3(shoulderX + c * dx + s * dz, p.y, shoulderZ - s * dx + c * dz);
            if (normals.Length == vertices.Length)
                normals[i] = new Vector3(c * normals[i].x + s * normals[i].z, normals[i].y, -s * normals[i].x + c * normals[i].z).normalized;
            if (tangents.Length == vertices.Length)
            {
                var tangent = tangents[i];
                var rotated = new Vector3(c * tangent.x + s * tangent.z, tangent.y, -s * tangent.x + c * tangent.z).normalized;
                tangents[i] = new Vector4(rotated.x, rotated.y, rotated.z, tangent.w);
            }
        }
        mesh.vertices = vertices;
        if (normals.Length == vertices.Length) mesh.normals = normals; else mesh.RecalculateNormals();
        if (tangents.Length == vertices.Length) mesh.tangents = tangents; else mesh.RecalculateTangents();
        mesh.RecalculateBounds();
    }

    private sealed class MaterialSpec
    {
        public string Name { get; }
        public int BaseIndex { get; }
        public int NormalIndex { get; }
        public MaterialSpec(string name, int baseIndex, int normalIndex) { Name = name; BaseIndex = baseIndex; NormalIndex = normalIndex; }
    }

    [MenuItem("Tools/Third Mind/Inspect Police Import")]
    public static void Inspect()
    {
        Debug.Log("POLICE_INSPECT_BEGIN");
        var assets = AssetDatabase.LoadAllAssetsAtPath(ModelPath);
        foreach (var asset in assets)
        {
            if (asset is Material material)
            {
                var baseTex = material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : null;
                var mainTex = material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
                var normal = material.HasProperty("_BumpMap") ? material.GetTexture("_BumpMap") : null;
                var gltfBase = material.HasProperty("baseColorTexture") ? material.GetTexture("baseColorTexture") : null;
                var gltfNormal = material.HasProperty("normalTexture") ? material.GetTexture("normalTexture") : null;
                Debug.Log($"POLICE_ASSET_MATERIAL name={material.name} shader={(material.shader ? material.shader.name : "null")} baseTex={(baseTex ? baseTex.name : "null")} mainTex={(mainTex ? mainTex.name : "null")} gltfBase={(gltfBase ? gltfBase.name : "null")} gltfNormal={(gltfNormal ? gltfNormal.name : "null")} standardNormal={(normal ? normal.name : "null")}");
            }
            else if (asset is Texture2D texture)
                Debug.Log($"POLICE_ASSET_TEXTURE name={texture.name} size={texture.width}x{texture.height} format={texture.format}");
            else if (asset is Mesh mesh)
                Debug.Log($"POLICE_ASSET_MESH name={mesh.name} vertices={mesh.vertexCount} bounds={mesh.bounds} submeshes={mesh.subMeshCount}");
            else if (asset is GameObject go)
                Debug.Log($"POLICE_ASSET_ROOT name={go.name}");
        }

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (model)
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                var mf = r.GetComponent<MeshFilter>();
                var sm = r as SkinnedMeshRenderer;
                var mesh = mf ? mf.sharedMesh : (sm ? sm.sharedMesh : null);
                Debug.Log($"POLICE_MODEL_RENDERER path={PathOf(r.transform, model.transform)} type={r.GetType().Name} mesh={(mesh ? mesh.name : "null")} localBounds={(mesh ? mesh.bounds : default)} bones={(sm ? sm.bones.Length : 0)} materials={string.Join("|", r.sharedMaterials.Select(m => m ? m.name + ":" + (m.shader ? m.shader.name : "null") : "null"))}");
                if (mesh)
                {
                    var v = mesh.vertices;
                    var min = v.Length > 0 ? v[0] : Vector3.zero;
                    var max = min;
                    foreach (var p in v) { min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
                    Debug.Log($"POLICE_MODEL_VERTICES mesh={mesh.name} localMin={min} localMax={max}");
                }
            }

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var police = GameObject.Find("Police-Man");
        if (police)
        {
            var t = police.transform;
            Debug.Log($"POLICE_SCENE_OBJECT path={PathOf(t, null)} localPos={t.localPosition} localRot={t.localRotation.eulerAngles} localScale={t.localScale} worldPos={t.position} active={police.activeInHierarchy}");
            foreach (var r in police.GetComponentsInChildren<Renderer>(true))
            {
                var mf = r.GetComponent<MeshFilter>(); var sm = r as SkinnedMeshRenderer;
                Debug.Log($"POLICE_SCENE_RENDERER path={PathOf(r.transform, police.transform)} type={r.GetType().Name} mesh={(mf && mf.sharedMesh ? mf.sharedMesh.name : sm && sm.sharedMesh ? sm.sharedMesh.name : "null")} materials={string.Join("|", r.sharedMaterials.Select(m => m ? m.name + ":" + (m.shader ? m.shader.name : "null") : "null"))}");
            }
        }
        else Debug.LogError("POLICE_SCENE_OBJECT_NOT_FOUND");
        Debug.Log("POLICE_INSPECT_END");
    }

    private static string PathOf(Transform t, Transform root)
    {
        var parts = new System.Collections.Generic.List<string>();
        while (t && t != root) { parts.Add(t.name); t = t.parent; }
        parts.Reverse(); return string.Join("/", parts);
    }
}

[InitializeOnLoad]
public static class PoliceModelRepairBatchBootstrap
{
    static PoliceModelRepairBatchBootstrap()
    {
        if (!Application.isBatchMode) return;
        EditorApplication.delayCall += Run;
    }

    private static void Run()
    {
        if (System.Environment.GetCommandLineArgs().Any(a => a == "-policeInspect"))
        {
            PoliceModelRepair.Inspect();
            EditorApplication.Exit(0);
        }
    }
}
#endif
