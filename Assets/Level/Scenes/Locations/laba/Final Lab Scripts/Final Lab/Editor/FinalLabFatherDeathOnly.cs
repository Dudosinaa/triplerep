using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

internal static partial class FinalLabFatherSetup
{
    private const string DeathHemMeshPath = Generated + "Father_DeathHem.asset";

    public static void ApplyDeathOnly()
    {
        FinalLabInvestigationSurvey.RequireBatch();
        var scene = EditorSceneManager.OpenScene(FinalLabBuilder.ScenePath);
        var sceneNodes = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
        var flow = sceneNodes.Select(node => node.GetComponent<FinalLabFlowController>()).Single(component => component != null);
        var actor = flow.Father.Actor;
        var controller = (AnimatorController)actor.runtimeAnimatorController;
        var states = controller.layers.Single().stateMachine.states.Select(node => node.state).ToArray();
        var deathState = states.Single(state => state.name == FatherCutsceneController.Death);
        var originalClothes = DeathClothes(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
        var actorClothes = DeathClothes(actor.gameObject);
        var floor = sceneNodes.Single(node => node.name == "ContainmentFloor").GetComponent<Collider>().bounds.max.y;
        var bed = sceneNodes.Single(node => node.name == "BedWithBedding").GetComponentInChildren<BoxCollider>();
        var support = actor.transform.position;
        support.y = floor;
        var reference = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
        reference.transform.SetPositionAndRotation(actor.transform.position, actor.transform.rotation);
        reference.transform.localScale = actor.transform.lossyScale;
        var originalDoorAngle = flow.FatherDoor.Hinge.localRotation;
        try
        {
            var rig = new PoseRig(reference, useSceneRest: true);
            flow.FatherDoor.Hinge.localRotation = Quaternion.Euler(0, flow.FatherDoor.SwingAngle, 0);
            var poses = StageDeath(rig, support, floor, bed, sceneNodes, relaxLegs: false);
            flow.FatherDoor.Hinge.localRotation = originalDoorAngle;
            Physics.SyncTransforms();

            var death = SaveMotion(rig, FatherCutsceneController.Death, poses);
            deathState.motion = death;
            EditorUtility.SetDirty(deathState);
            Debug.Log("The raw FBX Death state was relinked to its animation retargeted for the scene's existing ValveBiped avatar.");

            var hem = ShortenedDeathHem(rig, originalClothes.sharedMesh, poses[^1], floor);
            actorClothes.sharedMesh = hem;
            actorClothes.SetBlendShapeWeight(hem.GetBlendShapeIndex(FinalHemShape), 0f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(actorClothes);
            var sampler = reference.GetComponent<Animator>();
            if (sampler == null) sampler = reference.AddComponent<Animator>();
            sampler.avatar = actor.avatar;
            sampler.applyRootMotion = false;
            sampler.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            sampler.runtimeAnimatorController = controller;
            death = SaveMotion(rig, FatherCutsceneController.Death, poses);
            BakeDeathBlendSupport(rig, sampler, death, poses, floor);
            VerifyDeathOnly(rig, states, death, floor);
            Object.DestroyImmediate(reference);
            reference = null;
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the Death-only mesh override.");
            Debug.Log("FINAL_LAB_RESTORED_DEATH_AND_DEATH_ONLY_HEM_SAVED");
        }
        finally
        {
            flow.FatherDoor.Hinge.localRotation = originalDoorAngle;
            Physics.SyncTransforms();
            if (reference != null) Object.DestroyImmediate(reference);
        }
    }

    private static SkinnedMeshRenderer DeathClothes(GameObject actor) => actor.GetComponentsInChildren<SkinnedMeshRenderer>(true)
        .Single(skin => skin.sharedMaterials.Any(material => material != null && material.name.StartsWith("Sci_Bdy_Dirty_D", StringComparison.Ordinal)));

    private static Mesh ShortenedDeathHem(PoseRig rig, Mesh original, FatherPose final, float floor)
    {
        var clothing = DeathClothes(rig.Model);
        var vertices = original.vertices;
        var uv = original.uv;
        bool flipped = Enumerable.Range(0, vertices.Length).Count(i => IsHem(i) && uv[i].y > .92f) >
            Enumerable.Range(0, vertices.Length).Count(i => IsHem(i) && uv[i].y < .08f);
        var seamWeights = new Dictionary<Vector3Int, float>();
        for (int i = 0; i < vertices.Length; i++)
        {
            float v = flipped ? 1f - uv[i].y : uv[i].y;
            float y = vertices[i].y;
            if (uv[i].x > .668f || v > .262f || y < 1.97f || y > 3.06f) continue;
            float weight = 1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(2.079f, 3.06f, y));
            var key = Vector3Int.RoundToInt(vertices[i] * 10000f);
            seamWeights[key] = Mathf.Max(weight, seamWeights.GetValueOrDefault(key));
        }

        var buffer = new Mesh();
        try
        {
            final.Apply(rig);
            clothing.BakeMesh(buffer);
            var before = buffer.vertices;
            Vector3 along = (rig.Chest.position - rig.Hips.position).normalized;
            var weights = original.boneWeights;
            var binds = original.bindposes;
            var bones = clothing.bones.Select((bone, index) => bone != null ? bone.localToWorldMatrix * binds[index] : Matrix4x4.identity).ToArray();
            var delta = new Vector3[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                float weight = seamWeights.GetValueOrDefault(Vector3Int.RoundToInt(vertices[i] * 10000f));
                if (weight <= 0f) continue;
                Vector3 worldChange = along * (.105f * weight);
                float height = clothing.transform.TransformPoint(before[i]).y;
                worldChange.y = Mathf.Max(worldChange.y, floor + .007f - height);
                var w = weights[i];
                var skin = Matrix4x4.zero;
                for (int col = 0; col < 4; col++)
                    skin.SetColumn(col, bones[w.boneIndex0].GetColumn(col) * w.weight0 + bones[w.boneIndex1].GetColumn(col) * w.weight1 +
                        bones[w.boneIndex2].GetColumn(col) * w.weight2 + bones[w.boneIndex3].GetColumn(col) * w.weight3);
                delta[i] = skin.inverse.MultiplyVector(worldChange);
            }
            int changed = delta.Count(value => value.sqrMagnitude > .000001f);
            if (changed < 13 || changed > 85) throw new InvalidOperationException($"Expected only the lower coat atlas, but {changed} vertices were selected.");
            var mesh = Object.Instantiate(original);
            mesh.name = "Father_DeathHem";
            mesh.AddBlendShapeFrame(FinalHemShape, 100f, delta, new Vector3[delta.Length], new Vector3[delta.Length]);
            clothing.sharedMesh = Save(mesh, DeathHemMeshPath);
            int shape = clothing.sharedMesh.GetBlendShapeIndex(FinalHemShape);
            clothing.SetBlendShapeWeight(shape, 100f);
            clothing.BakeMesh(buffer);
            var after = buffer.vertices;
            float shortest = Enumerable.Range(0, vertices.Length).Where(IsHem).Where(i => delta[i].sqrMagnitude > .001f)
                .Select(i => Vector3.Dot(clothing.transform.TransformVector(after[i] - before[i]), along)).DefaultIfEmpty(0).Average();
            if (shortest < .075f) throw new InvalidOperationException($"The skinned coat hem did not shorten toward the torso ({shortest:F3} m).");
            Debug.Log($"Death-only coat: {changed} hem vertices, {shortest:F3} m mean shortening toward the torso in the final skinned pose.");
            clothing.SetBlendShapeWeight(shape, 0f);
            return clothing.sharedMesh;
        }
        finally { Object.DestroyImmediate(buffer); }

        bool IsHem(int index) => uv[index].x < .668f && vertices[index].y >= 2.00f && vertices[index].y <= 2.14f;
    }

    private static void VerifyDeathOnly(PoseRig rig, AnimatorState[] states, AnimationClip death, float floor)
    {
        var cloth = DeathClothes(rig.Model);
        int blend = cloth.sharedMesh.GetBlendShapeIndex(FinalHemShape);
        if (death.isLooping || blend < 0 || AnimationUtility.GetCurveBindings(death).Any(binding => rig.Model.transform.Find(binding.path) == null))
            throw new InvalidOperationException("The restored Death clip is looping or has an invalid rig binding.");
        foreach (var state in states.Where(state => state.name != FatherCutsceneController.Death))
        {
            var clip = (AnimationClip)state.motion;
            if (AnimationUtility.GetCurveBindings(clip).Any(binding => binding.propertyName == "blendShape." + FinalHemShape))
                throw new InvalidOperationException(state.name + " also affects the death-only coat.");
            clip.SampleAnimation(rig.Model, clip.length * .5f);
            if (cloth.GetBlendShapeWeight(blend) != 0f) throw new InvalidOperationException(state.name + " altered the unshortened coat.");
        }
        death.SampleAnimation(rig.Model, death.length);
        if (cloth.GetBlendShapeWeight(blend) < 99f || rig.Head.position.y - floor > .49f || rig.Hips.position.y - floor > .49f)
            throw new InvalidOperationException("The restored final death pose did not activate the localized coat hem.");
        Debug.Log("All five preceding clips keep the original hem. The final non-looping Death pose alone reaches full shortening.");
    }
}
