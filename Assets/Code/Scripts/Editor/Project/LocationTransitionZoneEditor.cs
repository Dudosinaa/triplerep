using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LocationTransitionZone))]
public sealed class LocationTransitionZoneEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var path = serializedObject.FindProperty("targetScenePath");
        var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path.stringValue);
        EditorGUI.BeginChangeCheck();
        scene = (SceneAsset)EditorGUILayout.ObjectField("Target Scene", scene, typeof(SceneAsset), false);
        if (EditorGUI.EndChangeCheck()) path.stringValue = scene != null ? AssetDatabase.GetAssetPath(scene) : "";
        EditorGUILayout.PropertyField(serializedObject.FindProperty("fadeOutDuration"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("fadeInDuration"));
        serializedObject.ApplyModifiedProperties();

        if (!LocationTransition.TryGetBuildIndex(path.stringValue, out _))
            EditorGUILayout.HelpBox("Enable the target scene in the active Build Profiles scene list.", MessageType.Warning);
        var zone = (LocationTransitionZone)target;
        if (!zone.GetComponent<BoxCollider>().isTrigger)
            EditorGUILayout.HelpBox("Enable Is Trigger on the Box Collider.", MessageType.Error);
        var body = zone.GetComponent<Rigidbody>();
        if (!body.isKinematic || body.useGravity)
            EditorGUILayout.HelpBox("Set Rigidbody: Is Kinematic on, Use Gravity off.", MessageType.Error);
    }
}
