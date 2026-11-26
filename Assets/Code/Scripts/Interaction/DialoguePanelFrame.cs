using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class DialoguePanelFrame : Image
{
    public const float BackgroundAlpha = 0.50f;
    public const float MinimumHeight = 110f;

    [SerializeField] private TMP_Text body;
    private string lastLine;
    private float lastWidth;

    public void Configure(TMP_Text text) => body = text;

    private void LateUpdate()
    {
        if (body == null) return;

        float width = body.rectTransform.rect.width;
        if (width <= 0f || (Mathf.Approximately(width, lastWidth) && body.text == lastLine)) return;
        lastWidth = width;
        lastLine = body.text;

        rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, MinimumHeight);
        body.ForceMeshUpdate();
        if (!body.isTextOverflowing) return;

        float textHeight = body.GetPreferredValues(lastLine, width, Mathf.Infinity).y;
        float extraHeight = Mathf.Ceil(textHeight - body.rectTransform.rect.height);
        if (extraHeight > 0f)
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, MinimumHeight + extraHeight);
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect rect = GetPixelAdjustedRect();
        const float thickness = 1.25f;
        if (rect.width <= 2f * thickness || rect.height <= 2f * thickness) return;

        // Draw the border alone so it does not increase the opacity of the backing.
        AddStrip(rect.xMin, rect.yMin, rect.xMax, rect.yMin + thickness);
        AddStrip(rect.xMin, rect.yMax - thickness, rect.xMax, rect.yMax);
        AddStrip(rect.xMin, rect.yMin + thickness, rect.xMin + thickness, rect.yMax - thickness);
        AddStrip(rect.xMax - thickness, rect.yMin + thickness, rect.xMax, rect.yMax - thickness);

        void AddStrip(float left, float bottom, float right, float top)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(new Vector3(left, bottom), color, Vector2.zero);
            mesh.AddVert(new Vector3(left, top), color, Vector2.zero);
            mesh.AddVert(new Vector3(right, top), color, Vector2.zero);
            mesh.AddVert(new Vector3(right, bottom), color, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
