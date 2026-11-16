using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class AssetLayoutValidation
{
    [MenuItem("Tools/Project/Validate Assets layout")]
    public static void Validate()
    {
        var checks=new List<string>();
        void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);checks.Add(message);}
        MergeValidation.Validate();WeaponAssetValidation.Validate();
        var definition=Resources.Load<WeaponDefinition>("HandsWeapon");
        Require(definition!=null&&definition.IsComplete,"Resources HandsWeapon is complete");
        Require(Resources.LoadAll<WeaponDefinition>("").Count(d=>d.name=="HandsWeapon")==1,"One runtime HandsWeapon resource");
        Require(Resources.Load<WeaponDefinition>("MakarovWeapon")!=null,"Previous weapon resource retained");
        Require(Resources.Load<TextAsset>("BillingMode")!=null,"BillingMode resource retained");
        Require(Resources.Load<GameObject>("MergedGameplay")!=null&&Resources.Load<GameObject>("MergedNpc")!=null,"Runtime player and NPC Resources keys retained");
        Require(Resources.Load<AudioClip>("MergeStressSound")!=null,"Stress audio Resources key retained");
        Require(File.Exists(Path.Combine(Application.streamingAssetsPath,"Smoking/model.gltf")),"Smoking StreamingAssets path retained");
        Require(GraphicsSettings.defaultRenderPipeline!=null,"Default render pipeline reference resolves");
        foreach(var scene in EditorBuildSettings.scenes)
        {
            Require(AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path)!=null,"Build scene resolves: "+scene.path);
            var preview=EditorSceneManager.OpenPreviewScene(scene.path);
            try
            {
                foreach(var root in preview.GetRootGameObjects())
                foreach(var t in root.GetComponentsInChildren<Transform>(true))
                    if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)!=0)throw new InvalidOperationException("Missing script in "+scene.path+"/"+t.name);
            }
            finally {EditorSceneManager.ClosePreviewScene(preview);}
            checks.Add("No missing scripts in "+scene.path);
            foreach(string path in AssetDatabase.GetDependencies(scene.path,true).Where(p=>p.StartsWith("Assets/",StringComparison.Ordinal)))
                if(!File.Exists(path)||string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path)))throw new InvalidOperationException("Unresolved dependency "+path);
        }
        var folder=Path.GetFullPath("Logs/AssetLayout");Directory.CreateDirectory(folder);
        File.WriteAllLines(Path.Combine(folder,"unity-validation.txt"),new[]{"ASSETS_LAYOUT_VALIDATION_PASS","Unity "+Application.unityVersion}.Concat(checks));
        Debug.Log("ASSETS_LAYOUT_VALIDATION_PASS");
    }
    public static void ValidateAndPlay(){Validate();HandsReloadPlayValidation.RunBatch();}
}
