using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public sealed partial class FirstMindInteriorBuilder
{
    private void PlaceTelevision()
    {
        const string meshPath = "Television scan cleaned.asset";
        GameObject source = Object.Instantiate(Load<GameObject>(Models + "1980s_tv_3d_scan.glb"));
        MeshRenderer[] parts = source.GetComponentsInChildren<MeshRenderer>();
        Material material = parts[0].sharedMaterial;
        Mesh mesh = TrimTelevisionScan(parts);
        Object.DestroyImmediate(source);

        Transform television = Group("1980s television", living);
        Transform housing = Group("Original CRT scan", television);
        housing.SetPositionAndRotation(At(-1.285f, 1.475f, 0.565f), Quaternion.Euler(0f, 201f, 0f));
        housing.localScale = Vector3.one * 0.081f;
        housing.gameObject.AddComponent<MeshFilter>().sharedMesh = Store(mesh, meshPath);
        housing.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
        MoveToSupport(television, housing.position, housing);
        FitCollider(television);
        supportedItems.Add((television.name, CombinedVisible(television)));
    }

    private static Mesh TrimTelevisionScan(MeshRenderer[] parts)
    {
        const float cutoff = -1.68f;
        const float positionResolution = 15f;
        const float textureResolution = 480f;
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();
        var welded = new Dictionary<(int, int, int, int, int), int>();
        foreach (MeshRenderer part in parts)
        {
            Mesh input = part.GetComponent<MeshFilter>().sharedMesh;
            Matrix4x4 matrix = part.localToWorldMatrix;
            Matrix4x4 normalsMatrix = matrix.inverse.transpose;
            Vector3[] positions = input.vertices.Select(matrix.MultiplyPoint3x4).ToArray();
            Vector3[] oldNormals = input.normals;
            Vector2[] uv = input.uv;
            var map = new int[positions.Length];
            System.Array.Fill(map, -1);
            int[] indices = input.GetTriangles(0);
            for (int i = 0; i < indices.Length; i += 3)
            {
                int a = indices[i], b = indices[i + 1], c = indices[i + 2];
                if (Mathf.Min(positions[a].y, positions[b].y, positions[c].y) < cutoff) continue;
                a = MapVertex(a);
                b = MapVertex(b);
                c = MapVertex(c);
                if (a == b || b == c || c == a) continue;
                triangles.Add(a);
                triangles.Add(b);
                triangles.Add(c);
            }

            int MapVertex(int index)
            {
                if (map[index] >= 0) return map[index];
                Vector3 p = positions[index];
                Vector2 t = uv[index];
                var key = (Mathf.RoundToInt(p.x * positionResolution), Mathf.RoundToInt(p.y * positionResolution),
                    Mathf.RoundToInt(p.z * positionResolution), Mathf.RoundToInt(t.x * textureResolution),
                    Mathf.RoundToInt(t.y * textureResolution));
                if (!welded.TryGetValue(key, out int result))
                {
                    result = vertices.Count;
                    vertices.Add(p);
                    normals.Add(normalsMatrix.MultiplyVector(oldNormals[index]).normalized);
                    uvs.Add(t);
                    welded.Add(key, result);
                }
                map[index] = result;
                return result;
            }
        }

        Check(triangles.Count > 30000, "The CRT scan was unexpectedly empty after removing the scan support");
        var mesh = new Mesh { name = "CRT without the scan support", indexFormat = IndexFormat.UInt32 };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        MeshUtilityForTelevision(mesh);
        Debug.Log($"INTERIOR_SCAN: CRT retained {mesh.vertexCount} vertices, {triangles.Count / 3} triangles");
        return mesh;
    }

    private static void MeshUtilityForTelevision(Mesh mesh)
    {
        UnityEditor.MeshUtility.Optimize(mesh);
        UnityEditor.MeshUtility.SetMeshCompression(mesh, UnityEditor.ModelImporterMeshCompression.Low);
    }
}
