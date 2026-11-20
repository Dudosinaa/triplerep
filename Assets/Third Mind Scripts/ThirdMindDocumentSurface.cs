using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ThirdMindDocumentSurface : MonoBehaviour
{
    public int documentId;
    public string documentTitle;
    [TextArea(4, 16)] public string fullText;
    public string firstEventTime;
    public bool inspected;
    [HideInInspector] public RectTransform paperVisual;
    [HideInInspector] public TMPro.TMP_Text slotLabel;
    [HideInInspector] public UnityEngine.UI.Image paperImage;
    [HideInInspector] public TMPro.TMP_Text bodyPrint;

    private const float HighlightDuration = 0.18f;
    private bool hovered;
    private bool selected;
    private bool reading;
    private Outline selectionOutline;
    private Canvas paperCanvas;
    private int originalSortingOrder;
    private Color? originalTint;
    private Color startTint;
    private Color targetTint;
    private float tintElapsed = HighlightDuration;
    private string printedBody;
    private bool bodyAutoSizing;
    private float bodyFontSize;
    private float bodyFontSizeMin;
    private float bodyFontSizeMax;
    private TMPro.TextOverflowModes bodyOverflow;

    public void SetHovered(bool value)
    {
        hovered = value;
        RefreshPaper();
    }

    public void SetSelected(bool selected)
    {
        this.selected = selected;
        RefreshPaper();
    }

    public void SetReading(bool value)
    {
        if (reading == value) return;
        if (paperCanvas == null)
        {
            paperCanvas = paperVisual.GetComponent<Canvas>();
            originalSortingOrder = paperCanvas.sortingOrder;
        }
        reading = value;
        paperCanvas.sortingOrder = reading ? originalSortingOrder + 1 : originalSortingOrder;
        if (bodyPrint != null)
        {
            if (reading)
            {
                printedBody = bodyPrint.text;
                bodyAutoSizing = bodyPrint.enableAutoSizing;
                bodyFontSize = bodyPrint.fontSize;
                bodyFontSizeMin = bodyPrint.fontSizeMin;
                bodyFontSizeMax = bodyPrint.fontSizeMax;
                bodyOverflow = bodyPrint.overflowMode;
                bodyPrint.text = fullText;
                bodyPrint.enableAutoSizing = true;
                bodyPrint.fontSizeMin = 7.5f;
                bodyPrint.fontSizeMax = Mathf.Max(14f, bodyFontSize);
                bodyPrint.overflowMode = TMPro.TextOverflowModes.Overflow;
            }
            else
            {
                bodyPrint.text = printedBody;
                bodyPrint.enableAutoSizing = bodyAutoSizing;
                bodyPrint.fontSize = bodyFontSize;
                bodyPrint.fontSizeMin = bodyFontSizeMin;
                bodyPrint.fontSizeMax = bodyFontSizeMax;
                bodyPrint.overflowMode = bodyOverflow;
            }
        }
        RefreshPaper();
    }

    private void RefreshPaper()
    {
        if (paperImage == null) return;
        originalTint ??= paperImage.color;
        var paperTint = originalTint.Value;
        var next = paperTint;
        if (reading) next = Color.Lerp(paperTint, new Color(0.99f, 0.91f, 0.70f, paperTint.a), 0.15f);
        else if (selected) next = Color.Lerp(paperTint, new Color(0.71f, 0.88f, 0.97f, paperTint.a), 0.66f);
        else if (hovered) next = Color.Lerp(paperTint, new Color(1f, 0.92f, 0.37f, paperTint.a), 0.85f);
        if (next != targetTint)
        {
            targetTint = next;
            startTint = paperImage.color;
            tintElapsed = 0f;
        }
        if (selected && !reading && selectionOutline == null)
        {
            selectionOutline = paperImage.gameObject.AddComponent<Outline>();
            selectionOutline.effectColor = new Color(0.52f, 0.76f, 0.91f, 0.90f);
            selectionOutline.effectDistance = new Vector2(2f, -2f);
        }
        if (selectionOutline != null) selectionOutline.enabled = selected && !reading;
    }

    private void LateUpdate() => AdvanceHighlight(Time.unscaledDeltaTime);

    internal void AdvanceHighlight(float deltaTime)
    {
        if (tintElapsed >= HighlightDuration) return;
        tintElapsed = Mathf.Min(tintElapsed + deltaTime, HighlightDuration);
        float t = Mathf.SmoothStep(0f, 1f, tintElapsed / HighlightDuration);
        paperImage.color = Color.Lerp(startTint, targetTint, t);
    }

    public void SetSlot(int slot)
    {
        if (slotLabel != null) slotLabel.text = (slot + 1).ToString("00");
    }
}
