using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SetupPistolDemo
{
    [MenuItem("Tools/Setup Pistol Demo")]
    public static void Setup()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save open scene changes before configuring the pistol demo.");
        var scene = EditorSceneManager.OpenScene("Assets/Level/Scenes/Gameplay/CarxDemo.unity");
        var definition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Settings/Resources/HandsWeapon.asset");
        if (definition == null || !definition.IsComplete) throw new InvalidOperationException("Hands weapon assets are incomplete.");
        foreach (var root in scene.GetRootGameObjects())
        foreach (var pistol in root.GetComponentsInChildren<PistolController>(true))
        {
            var data = new SerializedObject(pistol);
            data.FindProperty("definition").objectReferenceValue = definition;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pistol);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("PISTOL_DEMO_READY: Hands AnimatedPistol model and matching Akito Rig clips.");
    }
}
