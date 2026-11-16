using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class HandsReloadIntegration
{
    [MenuItem("Tools/Weapon/Connect baked Akito reloads")]
    public static void Run()
    {
        var definition=AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Settings/Resources/HandsWeapon.asset");
        var fast=AssetDatabase.LoadAssetAtPath<AnimationClip>(HandsReloadBaker.Folder+"/MK_ReloadFast_Akito.anim");
        var full=AssetDatabase.LoadAssetAtPath<AnimationClip>(HandsReloadBaker.Folder+"/MK_ReloadFull_Akito.anim");
        var model=AssetDatabase.LoadAssetAtPath<GameObject>(HandsReloadBaker.Model);
        if(definition==null||fast==null||full==null||model==null) throw new System.InvalidOperationException("Bake the reload assets first");
        definition.modelPrefab=model;
        definition.reloadClip=fast;definition.emptyReloadClip=full;
        definition.reloadInsertNormalizedTime=Insertion(fast)/fast.length;
        definition.emptyReloadInsertNormalizedTime=Insertion(full)/full.length;
        var controller=(AnimatorController)definition.controller;
        foreach(var child in controller.layers[0].stateMachine.states)
        {
            if(child.state.name=="Reload") child.state.motion=fast;
            else if(child.state.name=="ReloadEmpty") child.state.motion=full;
            else continue;
            foreach(var transition in child.state.transitions) { transition.duration=.08f; transition.hasFixedDuration=true; transition.hasExitTime=false; }
            EditorUtility.SetDirty(child.state);
        }
        EditorUtility.SetDirty(definition);EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        WeaponAssetValidation.Validate();
    }
    private static float Insertion(AnimationClip clip)=>AnimationUtility.GetAnimationEvents(clip).Single(e=>e.functionName=="OnMagazineInserted").time;
}

