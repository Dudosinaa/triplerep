using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

internal static partial class FinalLabFatherSetup
{
    private const string FinalHemShape = "FinalDeathHem";

    private static void CorrectFinalHem(PoseRig rig, FatherPose final, float floor)
    {
        var cloth = rig.Skins.Single(skin => skin.sharedMaterials.Any(material => material.name == "Sci_Bdy_Dirty_D"));
        var original = cloth.sharedMesh;
        var authored = original.vertices;
        var triangles = original.triangles;
        var welded = new Dictionary<Vector3Int, int>();
        var groups = new int[authored.Length];
        var representative = new List<int>();
        for (int i = 0; i < groups.Length; i++)
        {
            var key = Vector3Int.RoundToInt(authored[i] * 10000f);
            if (!welded.TryGetValue(key, out int group))
            {
                group = representative.Count;
                welded.Add(key, group);
                representative.Add(i);
            }
            groups[i] = group;
        }

        final.Apply(rig);
        var sampled = new Mesh();
        try
        {
            cloth.BakeMesh(sampled);
            var baked = sampled.vertices;
            var bind = representative.Select(i => cloth.transform.TransformPoint(authored[i])).ToArray();
            var initial = representative.Select(i => cloth.transform.TransformPoint(baked[i])).ToArray();
            var result = (Vector3[])initial.Clone();
            var adjustable = bind.Select(world =>
            {
                Vector3 local = rig.Model.transform.InverseTransformPoint(world);
                return local.y > 1.60f && local.y < 3.46f && Mathf.Abs(local.x) < 1.5f;
            }).ToArray();
            var edges = new HashSet<(int a, int b)>();
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Edge(triangles[i], triangles[i + 1]);
                Edge(triangles[i + 1], triangles[i + 2]);
                Edge(triangles[i + 2], triangles[i]);
            }
            var hem = edges.Select(edge => (edge.a, edge.b, length: Vector3.Distance(bind[edge.a], bind[edge.b])))
                .Where(edge => edge.length > .011f && (adjustable[edge.a] || adjustable[edge.b])).ToArray();
            float Distortion(Vector3[] positions) => hem.Sum(edge => Mathf.Max(0,
                Vector3.Distance(positions[edge.a], positions[edge.b]) - edge.length * 1.27f));
            float before = Distortion(initial);
            string meshPath = Generated + "Father_Clothes_Settled.asset";
            if (before <= .005f)
            {
                if (AssetDatabase.LoadAssetAtPath<Mesh>(meshPath) != null) AssetDatabase.DeleteAsset(meshPath);
                Debug.Log($"Father hem: terminal leg pose left {before:F3} m of excess edge stretch. The scene uses the original clothes mesh.");
                return;
            }

            // Keep the original surface and correct the stretched lower folds only in the terminal pose.
            for (int pass = 0; pass < 8; pass++)
                foreach (var edge in hem)
                {
                    Vector3 segment = result[edge.b] - result[edge.a];
                    float length = segment.magnitude;
                    float stretch = length - edge.length * 1.27f;
                    if (stretch <= 0) continue;
                    float fraction = adjustable[edge.a] && adjustable[edge.b] ? .25f : .51f;
                    Vector3 correction = segment * (stretch / length * fraction);
                    if (adjustable[edge.a]) Adjust(edge.a, correction);
                    if (adjustable[edge.b]) Adjust(edge.b, -correction);
                }

            float after = Distortion(result);
            var delta = new Vector3[authored.Length];
            var weights = original.boneWeights;
            var bindPoses = original.bindposes;
            var matrices = cloth.bones.Select((bone, index) => bone == null ? Matrix4x4.identity : bone.localToWorldMatrix * bindPoses[index]).ToArray();
            for (int i = 0; i < delta.Length; i++)
            {
                Vector3 change = result[groups[i]] - initial[groups[i]];
                if (change.sqrMagnitude < .0000004f) continue;
                var w = weights[i];
                var weighted = Matrix4x4.zero;
                for (int col = 0; col < 4; col++)
                    weighted.SetColumn(col, matrices[w.boneIndex0].GetColumn(col) * w.weight0 + matrices[w.boneIndex1].GetColumn(col) * w.weight1 +
                        matrices[w.boneIndex2].GetColumn(col) * w.weight2 + matrices[w.boneIndex3].GetColumn(col) * w.weight3);
                delta[i] = weighted.inverse.MultiplyVector(change);
            }
            if (before > .005f && after >= before * .98f)
                throw new InvalidOperationException("The final coat correction did not reduce edge stretching.");
            var corrected = Object.Instantiate(original);
            corrected.name = "Father_Clothes_Settled";
            corrected.AddBlendShapeFrame(FinalHemShape, 100, delta, new Vector3[delta.Length], new Vector3[delta.Length]);
            cloth.sharedMesh = Save(corrected, meshPath);
            int shape = cloth.sharedMesh.GetBlendShapeIndex(FinalHemShape);
            cloth.SetBlendShapeWeight(shape, 100);
            cloth.BakeMesh(sampled);
            var correctedVertices = sampled.vertices;
            float actual = Distortion(representative.Select(i => cloth.transform.TransformPoint(correctedVertices[i])).ToArray());
            cloth.SetBlendShapeWeight(shape, 0);
            if (before > .005f && actual >= before * .985f)
                throw new InvalidOperationException($"The skinned coat did not retain the correction ({before:F3} to {actual:F3}).");
            PrefabUtility.RecordPrefabInstancePropertyModifications(cloth);
            Debug.Log($"Father hem: {delta.Count(change => change.sqrMagnitude > .000001f)} local vertex corrections, measured excess edge stretch {before:F3} -> {actual:F3} m. Original material and skin weights retained.");

            void Edge(int originalA, int originalB)
            {
                int a = groups[originalA], b = groups[originalB];
                if (a == b) return;
                edges.Add(a < b ? (a, b) : (b, a));
            }

            void Adjust(int index, Vector3 change)
            {
                Vector3 next = initial[index] + Vector3.ClampMagnitude(result[index] + change - initial[index], .060f);
                next.y = Mathf.Max(next.y, floor + .007f);
                result[index] = next;
            }
        }
        finally { Object.DestroyImmediate(sampled); }
    }
}
