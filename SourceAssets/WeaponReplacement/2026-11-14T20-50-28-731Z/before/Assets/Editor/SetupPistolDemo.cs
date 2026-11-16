using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public static class SetupPistolDemo
{
    private const string ModelPath = "Assets/ThirdParty/Sketchfab/Makarov/source/Arms_Mak.fbx";
    private const string AnimationPath = "Assets/ThirdParty/Sketchfab/Makarov/Makarov.controller";
    private const string MaterialFolder = "Assets/ThirdParty/Sketchfab/Makarov/Materials";
    private const string TextureFolder = "Assets/ThirdParty/Sketchfab/Makarov/textures/";

    [MenuItem("Tools/Setup Pistol Demo")]
    public static void Setup()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/CarxDemo.unity");
        GameObject player = GameObject.Find("Player");
        if (player == null) throw new InvalidOperationException("Player not found");
        Camera camera = player.GetComponentInChildren<Camera>();
        if (camera == null) throw new InvalidOperationException("Player camera not found");
        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (modelAsset == null)
        {
            var fallback = player.GetComponent<PistolController>();
            if (fallback == null) fallback = player.AddComponent<PistolController>();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Procedural weapon is enabled; original LFS FBX is not present.");
            return;
        }

        var clips = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>().ToArray();
        AnimationClip idle = FindClip(clips, "MK_Idle");
        AnimationClip shot = FindClip(clips, "MK_Shot");
        AnimationClip reload = FindClip(clips, "MK_ReloadFast");
        AnimationClip walk = FindClip(clips, "MK_walk");
        if (idle == null || shot == null || reload == null)
            throw new InvalidOperationException("Required animation clips missing from Makarov FBX");

        if (!AssetDatabase.IsValidFolder(MaterialFolder))
            AssetDatabase.CreateFolder("Assets/ThirdParty/Sketchfab/Makarov", "Materials");
        Material gunMaterial = Material("Gun", "Makarov_main_Base_Color.png", 0.75f);
        Material handsMaterial = Material("Hands", "armCcolor.png", 0f);
        Material attachmentMaterial = Material("Attachments", "makarov_attachments_Base_Color-makarov_att.png", 0.65f);

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimationPath);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(AnimationPath);
        var machine = controller.layers[0].stateMachine;
        foreach (var state in machine.states) machine.RemoveState(state.state);
        AddState(machine, "Idle", idle, true);
        AddState(machine, "Shot", shot, false);
        AddState(machine, "Reload", reload, false);
        if (walk != null) AddState(machine, "Walk", walk, true);
        EditorUtility.SetDirty(controller);

        GameObject root = GameObject.Find("Pistol View Model");
        if (root == null) root = new GameObject("Pistol View Model");
        root.transform.SetParent(camera.transform, false);
        root.transform.localPosition = new Vector3(0.24f, -0.23f, 0.4f);
        root.transform.localRotation = Quaternion.identity;
        Transform previous = root.transform.Find("Arms_Mak");
        if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
        GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, root.transform);
        model.name = "Arms_Mak";
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one;
        Animator animator = model.GetComponent<Animator>();
        if (animator == null) animator = model.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var old = renderer.sharedMaterials;
            var mapped = new Material[old.Length];
            for (int i = 0; i < old.Length; i++)
            {
                string name = old[i] != null ? old[i].name.ToLowerInvariant() : "";
                if (name.Contains("arm") || name.Contains("hand") || renderer.name == "mesh") mapped[i] = handsMaterial;
                else if (name.Contains("attach")) mapped[i] = attachmentMaterial;
                else mapped[i] = gunMaterial;
            }
            renderer.sharedMaterials = mapped;
        }
        camera.nearClipPlane = 0.01f;

        var canvas = GameObject.Find("Pistol HUD");
        if (canvas == null) canvas = new GameObject("Pistol HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        Text ammo = MakeText("Ammo", canvas.transform, 36, TextAnchor.LowerRight,
            new Vector2(1f, 0f), new Vector2(-36f, 25f), new Vector2(360f, 65f));
        Text hint = MakeText("Weapon Hint", canvas.transform, 24, TextAnchor.LowerLeft,
            new Vector2(0f, 0f), new Vector2(30f, 25f), new Vector2(960f, 55f));

        var pistol = player.GetComponent<PistolController>();
        if (pistol == null) pistol = player.AddComponent<PistolController>();
        var data = new SerializedObject(pistol);
        data.FindProperty("playerCamera").objectReferenceValue = camera;
        data.FindProperty("movement").objectReferenceValue = player.GetComponent<FirstPersonPlayer>();
        data.FindProperty("viewModel").objectReferenceValue = root.transform;
        data.FindProperty("animator").objectReferenceValue = animator;
        data.FindProperty("shotClip").objectReferenceValue = shot;
        data.FindProperty("reloadClip").objectReferenceValue = reload;
        data.FindProperty("ammoText").objectReferenceValue = ammo;
        data.FindProperty("statusText").objectReferenceValue = hint;
        data.FindProperty("aimPosition").vector3Value = new Vector3(0.58f, -0.13f, 0.28f);
        data.FindProperty("aimScale").floatValue = 1.2f;
        data.FindProperty("aimCameraForward").floatValue = 0.08f;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(pistol);
        var movementData = new SerializedObject(player.GetComponent<FirstPersonPlayer>());
        movementData.FindProperty("pistol").objectReferenceValue = pistol;
        movementData.ApplyModifiedPropertiesWithoutUndo();

        CreateTarget("Target Left", new Vector3(-4f, 1.25f, 7.5f), new Color(0.8f, 0.2f, 0.16f));
        CreateTarget("Target Center", new Vector3(0f, 1.25f, 8.5f), new Color(0.92f, 0.73f, 0.18f));
        CreateTarget("Target Right", new Vector3(4f, 1.25f, 7.5f), new Color(0.18f, 0.68f, 0.74f));
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("PISTOL_DEMO_READY clips: " + idle.name + ", " + shot.name + ", " + reload.name);
    }

    private static AnimationClip FindClip(AnimationClip[] clips, string suffix) =>
        clips.FirstOrDefault(c => !c.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase) &&
            c.name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));

    private static void AddState(AnimatorStateMachine machine, string name, AnimationClip clip, bool isDefault)
    {
        AnimatorState state = machine.AddState(name);
        state.motion = clip;
        if (isDefault && name == "Idle") machine.defaultState = state;
    }

    private static Material Material(string name, string textureName, float metallic)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + textureName));
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", name == "Hands" ? 0.3f : 0.65f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Text MakeText(string name, Transform parent, int size, TextAnchor anchor,
        Vector2 edge, Vector2 position, Vector2 dimensions)
    {
        Transform previous = parent.Find(name);
        GameObject obj = previous != null ? previous.gameObject : new GameObject(name, typeof(RectTransform), typeof(Text));
        obj.transform.SetParent(parent, false);
        Text text = obj.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = size;
        text.color = Color.white;
        text.alignment = anchor;
        text.raycastTarget = false;
        var rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = edge;
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;
        return text;
    }

    private static void CreateTarget(string name, Vector3 position, Color color)
    {
        GameObject target = GameObject.Find(name);
        if (target == null) target = GameObject.CreatePrimitive(PrimitiveType.Cube);
        target.name = name;
        target.transform.position = position;
        target.transform.localScale = new Vector3(0.8f, 1.5f, 0.15f);
        string path = MaterialFolder + "/" + name.Replace(' ', '_') + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.color = color;
        target.GetComponent<Renderer>().sharedMaterial = material;
        if (target.GetComponent<ShootingTarget>() == null) target.AddComponent<ShootingTarget>();
    }
}
