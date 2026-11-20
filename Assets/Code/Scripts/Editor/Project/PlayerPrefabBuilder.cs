using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// Explicit authoring command; never runs automatically on import or when entering Play Mode.
public static class PlayerPrefabBuilder
{
    public const string PrefabPath = "Assets/Level/Prefabs/Resources/Player.prefab";
    private const string ProfilePath = "Assets/Settings/PlayerStressProfile.asset";
    private const string SourcePath = "Assets/Level/Prefabs/Resources/MergedGameplay.prefab";

    [MenuItem("Tools/Player/Rebuild standalone player prefab")]
    public static void Build()
    {
        var source = PrefabUtility.LoadPrefabContents(SourcePath);
        GameObject player = null;
        try
        {
            player = source.GetComponentInChildren<FirstPersonPlayer>(true).gameObject;
            player.transform.SetParent(null, false);
            player.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            player.transform.localScale = Vector3.one;
            player.name = "Player";

            var ui = new GameObject("Player UI");
            ui.transform.SetParent(player.transform, false);
            foreach (var name in new[] { "Document UI", "Item Inspection UI", "Dialogue UI", "Pistol HUD", "Dialogue EventSystem" })
            {
                var child = source.transform.Find(name);
                if (child == null) throw new InvalidOperationException("Missing source UI: " + name);
                child.SetParent(ui.transform, false);
            }
            ui.transform.Find("Dialogue EventSystem").name = "Player EventSystem";

            // The current dialogue is first-person; these inactive demo visuals have no purpose here.
            var body = player.transform.Find("Dialogue Player Body");
            if (body != null) UnityEngine.Object.DestroyImmediate(body.gameObject);
            var dialogue = new SerializedObject(player.GetComponent<DialogueController>());
            dialogue.FindProperty("dialogueCamera").objectReferenceValue = null;
            dialogue.FindProperty("playerBody").objectReferenceValue = null;
            dialogue.ApplyModifiedPropertiesWithoutUndo();

            var hints = new GameObject("Player Hints", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            hints.transform.SetParent(ui.transform, false);
            hints.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = hints.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            var consciousnessHint = MakeHint(hints.transform, "Consciousness Hint", "T — войти в сознание", 0.33f);
            var smokingHint = MakeHint(hints.transform, "Smoking Hint", "Покурить — Y", 0.12f);

            var sceneSwitch = player.AddComponent<SceneSwitch>();
            sceneSwitch.interactionDistance = 2.7f;
            sceneSwitch.hintText = consciousnessHint;

            var effects = new GameObject("Stress Effects", typeof(Volume));
            effects.transform.SetParent(player.transform, false);
            var volume = effects.GetComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 100f;
            volume.weight = 0f;
            volume.sharedProfile = GetStressProfile();
            var stress = player.AddComponent<PlayerStress>();
            stress.volume = volume;
            stress.increaseSpeed = stress.decreaseSpeed = 20f;
            stress.maxVignetteIntensity = 1f;
            stress.soundThreshold = 70f;
            stress.stressSound = Resources.Load<AudioClip>("MergeStressSound");
            var stressData = new SerializedObject(stress);
            stressData.FindProperty("blendWithSceneProfile").boolValue = true;
            stressData.ApplyModifiedPropertiesWithoutUndo();

            player.AddComponent<SmokingPromptUI>();
            var smoking = player.AddComponent<SmokingController>();
            var smokingData = new SerializedObject(smoking);
            smokingData.FindProperty("hintText").objectReferenceValue = smokingHint;
            smokingData.ApplyModifiedPropertiesWithoutUndo();
            player.GetComponentInChildren<Camera>().GetUniversalAdditionalCameraData().renderPostProcessing = true;

            PrefabUtility.SaveAsPrefabAsset(player, PrefabPath);
            AssetDatabase.SaveAssets();
        }
        finally
        {
            if (player != null) UnityEngine.Object.DestroyImmediate(player);
            PrefabUtility.UnloadPrefabContents(source);
        }
        Validate();
    }

    private static TMP_Text MakeHint(Transform parent, string name, string text, float height)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var hint = go.GetComponent<TMP_Text>();
        hint.font = TMP_Settings.defaultFontAsset;
        hint.text = text;
        hint.fontSize = 26f;
        hint.alignment = TextAlignmentOptions.Center;
        hint.raycastTarget = false;
        hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(0.5f, height);
        hint.rectTransform.sizeDelta = new Vector2(1000f, 80f);
        hint.rectTransform.anchoredPosition = Vector2.zero;
        go.SetActive(false);
        return hint;
    }

    private static VolumeProfile GetStressProfile()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile != null) return profile;
        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.name = "PlayerStressProfile";
        AssetDatabase.CreateAsset(profile, ProfilePath);
        var vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(1f);
        vignette.smoothness.Override(1f);
        AssetDatabase.AddObjectToAsset(vignette, profile);
        EditorUtility.SetDirty(profile);
        return profile;
    }

    [MenuItem("Tools/Player/Validate standalone player prefab")]
    public static void Validate()
    {
        var checks = new List<string>();
        void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
            checks.Add(message);
        }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Require(prefab != null && Resources.Load<GameObject>("Player") == prefab, "Player Resources prefab resolves");
        Require(prefab.GetComponent<FirstPersonPlayer>() != null, "Movement is on the prefab root");
        foreach (var type in new[] { typeof(DocumentReader), typeof(ItemInspector), typeof(DialogueController),
            typeof(PistolController), typeof(PlayerStress), typeof(SmokingController), typeof(SmokingPromptUI), typeof(SceneSwitch) })
            Require(prefab.GetComponentsInChildren(type, true).Length == 1 && prefab.GetComponent(type) != null,
                "Exactly one root component: " + type.Name);
        Require(prefab.GetComponentsInChildren<CharacterController>(true).Length == 1 &&
            prefab.GetComponentsInChildren<PlayerMovement>(true).Length == 0 &&
            prefab.GetComponentsInChildren<Rigidbody>(true).Length == 0, "One CharacterController, no competing movement or Rigidbody");
        Require(prefab.transform.position == Vector3.zero, "Prefab origin is at the player's feet");
        foreach (var type in new[] { typeof(NpcDialogue), typeof(ReadableDocument), typeof(InspectableItem), typeof(ShootingTarget) })
            Require(prefab.GetComponentsInChildren(type, true).Length == 0, "No test content: " + type.Name);
        Require(prefab.GetComponentsInChildren<Camera>(true).Length == 1 &&
            prefab.GetComponentsInChildren<AudioListener>(true).Length == 1, "One authored player camera and listener");
        Require(prefab.GetComponentsInChildren<InputSystemUIInputModule>(true).Length == 1, "Player owns UI input");
        foreach (var name in new[] { "Document UI", "Item Inspection UI", "Dialogue UI", "Pistol HUD", "Player Hints" })
            Require(prefab.transform.Find("Player UI/" + name) != null, "Owned UI: " + name);
        var stress = prefab.GetComponent<PlayerStress>();
        Require(stress.volume != null && stress.volume.transform.IsChildOf(prefab.transform) &&
            stress.volume.sharedProfile != null && stress.volume.sharedProfile.TryGet<Vignette>(out _) && stress.stressSound != null,
            "Stress volume, vignette and audio are configured");
        Require(prefab.GetComponent<SceneSwitch>().hintText != null &&
            new SerializedObject(prefab.GetComponent<SmokingController>()).FindProperty("hintText").objectReferenceValue != null,
            "Consciousness and smoking hints are serialized");
        void RequireReferences(Component component, params string[] fields)
        {
            var data = new SerializedObject(component);
            foreach (var field in fields)
            {
                var value = data.FindProperty(field).objectReferenceValue;
                Require(value != null, component.GetType().Name + "." + field + " is assigned");
                var owner = value is GameObject go ? go.transform : (value as Component)?.transform;
                Require(owner != null && owner.IsChildOf(prefab.transform), component.GetType().Name + "." + field + " belongs to the player");
            }
        }
        RequireReferences(prefab.GetComponent<FirstPersonPlayer>(), "playerCamera", "pistol", "footstepSource");
        RequireReferences(prefab.GetComponent<DocumentReader>(), "playerCamera", "movement", "interactionPrompt", "readingPanel", "titleText", "bodyText", "bodyScrollRect");
        RequireReferences(prefab.GetComponent<ItemInspector>(), "playerCamera", "movement", "documentReader", "interactionPrompt", "promptText", "inspectionPanel", "itemTitle", "previewImage");
        RequireReferences(prefab.GetComponent<DialogueController>(), "playerCamera", "movement", "documentReader", "itemInspector", "crosshair", "interactionPrompt", "dialoguePanel", "speakerText", "dialogueText", "continueButton");
        RequireReferences(prefab.GetComponent<PistolController>(), "playerCamera", "movement", "viewModel", "statusText");
        var choices = new SerializedObject(prefab.GetComponent<DialogueController>()).FindProperty("choiceButtons");
        Require(choices.arraySize == 3 && Enumerable.Range(0, 3).All(i => choices.GetArrayElementAtIndex(i).objectReferenceValue != null), "All three dialogue choices are assigned");
        Require(new SerializedObject(stress).FindProperty("blendWithSceneProfile").boolValue && stress.volume.weight == 0f,
            "Stress starts with zero volume weight and blends with scene lighting");
        Require(prefab.GetComponentInChildren<Camera>().GetUniversalAdditionalCameraData().renderPostProcessing, "Player camera enables post processing");
        foreach (var transform in prefab.GetComponentsInChildren<Transform>(true))
        {
            Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) == 0, "No missing script: " + transform.name);
            foreach (var component in transform.GetComponents<Component>())
            {
                var data = new SerializedObject(component);
                var property = data.GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                    var reference = property.objectReferenceValue;
                    if (reference == null)
                    {
                        Require(property.objectReferenceInstanceIDValue == 0, "No broken reference: " + transform.name + "/" + property.propertyPath);
                        continue;
                    }
                    if (EditorUtility.IsPersistent(reference)) continue;
                    var target = reference is GameObject go ? go.transform : (reference as Component)?.transform;
                    Require(target != null && target.IsChildOf(prefab.transform), "Reference belongs to player: " + property.propertyPath);
                }
            }
        }
        var dependencies = AssetDatabase.GetDependencies(PrefabPath, true);
        Require(!dependencies.Contains(SourcePath) && !dependencies.Any(p => p.EndsWith("MergedNpc.prefab")), "Player has no dependency on the demo or NPC prefabs");
        Directory.CreateDirectory("Logs/Player");
        File.WriteAllLines("Logs/Player/prefab-validation.txt", new[] { "PLAYER_PREFAB_VALIDATION_PASS", "Unity " + Application.unityVersion }.Concat(checks));
        Debug.Log("PLAYER_PREFAB_VALIDATION_PASS: " + checks.Count + " checks");
    }
}
