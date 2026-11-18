using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class DialogueExitIcon : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect rect = GetPixelAdjustedRect();

        // Door frame, open door and handle on a 24-unit grid.
        AddQuad(mesh, rect, new Vector2(4, 4), new Vector2(6, 4), new Vector2(6, 21), new Vector2(4, 21));
        AddQuad(mesh, rect, new Vector2(4, 19), new Vector2(20, 19), new Vector2(20, 21), new Vector2(4, 21));
        AddQuad(mesh, rect, new Vector2(18, 4), new Vector2(20, 4), new Vector2(20, 19), new Vector2(18, 19));
        AddQuad(mesh, rect, new Vector2(6, 4), new Vector2(16, 1), new Vector2(16, 17), new Vector2(6, 19));
        AddQuad(mesh, rect, new Vector2(12, 8), new Vector2(14, 8), new Vector2(14, 10), new Vector2(12, 10),
            new Color(0.16f, 0.25f, 0.32f));
    }

    private void AddQuad(VertexHelper mesh, Rect rect, Vector2 a, Vector2 b, Vector2 c, Vector2 d,
        Color? tint = null)
    {
        int start = mesh.currentVertCount;
        var vertex = UIVertex.simpleVert;
        vertex.color = tint ?? color;
        AddVertex(a);
        AddVertex(b);
        AddVertex(c);
        AddVertex(d);
        mesh.AddTriangle(start, start + 2, start + 1);
        mesh.AddTriangle(start, start + 3, start + 2);

        void AddVertex(Vector2 point)
        {
            vertex.position = rect.min + Vector2.Scale(point / 24f, rect.size);
            mesh.AddVert(vertex);
        }
    }
}
