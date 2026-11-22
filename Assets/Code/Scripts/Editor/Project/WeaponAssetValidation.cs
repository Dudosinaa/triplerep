using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class WeaponAssetValidation
{
    private const string ModelPath = "Assets/Art/Models/Weapons/Hands/source/AnimatedPistol.fbx";
    [Serializable] private sealed class Report
    {
        public string unityVersion;
        public string model;
        public string[] clips;
        public string[] checks;
        public string[] manualChecks;
    }
    [MenuItem("Tools/Weapon/Validate assets")]
    public static void Validate()
    {
        var definition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Settings/Resources/HandsWeapon.asset");
        Require(definition != null && definition.IsComplete, "HandsWeapon references are incomplete");
        Require(AssetDatabase.GetAssetPath(definition.modelPrefab) == HandsReloadBaker.Model, "Expected derived Hands prefab");
        var nativeClips = new[] { definition.idleClip, definition.shotClip, definition.equipClip };
        var suffixes = new[] { "Idle", "Shoot", "Take" };
        for (int i = 0; i < nativeClips.Length; i++)
        {
            Require(AssetDatabase.GetAssetPath(nativeClips[i]) == ModelPath, "Native Hands clip was replaced");
            Require(nativeClips[i].name.EndsWith(suffixes[i], StringComparison.Ordinal), "Unexpected native clip name");
        }
        var clips = nativeClips.Concat(new[]{definition.reloadClip,definition.emptyReloadClip}).ToArray();
        foreach(var clip in clips) Require(clip.length>0f&&!clip.legacy,"Expected a nonempty Mecanim clip");
        Require(definition.rigRoot == "Akito Rig", "Hands must use its own Akito Rig");
        Require(definition.reloadInsertNormalizedTime > 0f && definition.reloadInsertNormalizedTime < 1f,
            "Insertion event must be inside the Hands Reload clip");
        Require(definition.emptyReloadClip != definition.reloadClip, "Partial and empty reloads must be distinct");
        Require(definition.reloadClip.name == "MK_ReloadFast_Akito" && definition.emptyReloadClip.name == "MK_ReloadFull_Akito", "Incorrect adapted reloads");
        foreach(var clip in new[]{definition.reloadClip,definition.emptyReloadClip})
        {
            Require(AssetDatabase.GetAssetPath(clip).StartsWith(HandsReloadBaker.Folder+"/",StringComparison.Ordinal),"Reload must be a baked Akito asset");
            var marker=AnimationUtility.GetAnimationEvents(clip).Single(e=>e.functionName=="OnMagazineInserted");
            Require(marker.time>0&&marker.time<clip.length,"Insertion marker must be inside its clip");
        }
        var preview = EditorSceneManager.NewPreviewScene();
        var holder = new GameObject("Weapon Validation Holder");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(holder, preview);
        try
        {
            var view = holder.AddComponent<FirstPersonWeaponView>();
            Require(view.Initialize(definition), "Weapon initialization failed; inspect Console");
            Require(view.Model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Count(r => r.gameObject.activeInHierarchy) == 3,
                "Expected only the Hands, Sleeves and pistol skinned meshes");
            foreach (var clip in clips)
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                if (binding.type == typeof(Transform))
                    Require(string.IsNullOrEmpty(binding.path) || view.Model.transform.Find(binding.path) != null,
                        "Unresolved bone path in " + clip.name + ": " + binding.path);
            foreach (var skin in view.Model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                Require(skin.bones.All(b => b != null && b.IsChildOf(view.Model.transform)),
                    "Skin references bones outside the selected rig");
            var materials = definition.materialBindings.Select(b => b.material).ToArray();
            foreach (var renderer in view.Model.GetComponentsInChildren<Renderer>(true).Where(r => r.gameObject.activeInHierarchy))
                Require(renderer.sharedMaterials.All(m => materials.Contains(m)), "Unmapped material slot");
            view.ArmReloadEvent(7, false);
            var insertion = view.RuntimeReloadClip.events.Single(e => e.functionName == "OnMagazineInserted");
            Require(Mathf.Abs(insertion.time - definition.reloadInsertNormalizedTime * definition.reloadClip.length) < 0.001f,
                "Runtime insertion event does not match Hands phase");
            Require(insertion.intParameter == 7 && view.Model.GetComponent<WeaponAnimationEvents>() != null,
                "Missing operation token or Animator event receiver");
            view.InvalidateReloadEvent();
            view.ArmReloadEvent(8,true);
            var emptyInsertion=view.RuntimeEmptyReloadClip.events.Single(e=>e.functionName=="OnMagazineInserted");
            Require(Mathf.Abs(emptyInsertion.time-definition.emptyReloadInsertNormalizedTime*definition.emptyReloadClip.length)<.001f&&emptyInsertion.intParameter==8,"Empty insertion marker/token mismatch");
            view.InvalidateReloadEvent();
            foreach (var camera in view.Model.GetComponentsInChildren<Camera>(true))
                Require(!camera.enabled, "Imported camera must not render");
            foreach (var state in new[] { "Idle", "Shot", "Reload", "ReloadEmpty", "Equip" })
                Require(((UnityEditor.Animations.AnimatorController)definition.controller).layers[0].stateMachine.states.Any(s=>s.state.name==state), "Missing Animator state: " + state);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabBuilder.PrefabPath);
            var pistol = prefab != null ? prefab.GetComponentInChildren<PistolController>(true) : null;
            Require(pistol != null, "Common player has no PistolController");
            Require(new SerializedObject(pistol).FindProperty("definition").objectReferenceValue == definition,
                "Runtime-spawned common player is not configured");
            var report = new Report {
                unityVersion = Application.unityVersion, model = HandsReloadBaker.Model,
                clips = clips.Select(c => c.name + " | " + c.length.ToString("F3") + " seconds").ToArray(),
                checks = new[] { "Single Akito Rig and only three visible skinned meshes", "Hands insertion AnimationEvent is operation-scoped", "All clip transform paths resolve", "Skin bones belong to the model",
                    "Materials mapped", "Imported cameras disabled", "Animator states exist", "Common player configured" },
                manualChecks = new[] { "Right mouse zooms the camera with the weapon in its resting pose",
                    "Shoot and hit targets with the configured shot sound", "Full reload clip and sound with both partial and empty magazines",
                    "Cancel before and after magazine insertion; no duplicate ammunition",
                    "Dialogue, document, item inspection, smoking and scene transfer hide and block weapon" }
            };
            string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs/Weapon");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "unity-asset-validation.json"), JsonUtility.ToJson(report, true));
            Debug.Log("WEAPON_ASSET_VALIDATION_PASS. Play Mode and visual checks still required.");
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
