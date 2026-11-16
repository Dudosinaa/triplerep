using TMPro;
using UnityEngine;

/// <summary>Controls only the appearance and normalized screen position of the smoking hint.</summary>
public sealed class SmokingPromptUI : MonoBehaviour
{
    [Header("Position (normalized screen coordinates)")]
    [SerializeField, Range(0f, 1f)] private float horizontalPosition = 0.5f;
    [SerializeField, Range(0f, 1f)] private float verticalPosition = 0.12f;

    [Header("Text appearance")]
    [SerializeField, Range(0f, 1f)] private float alpha = 0.5f;
    [SerializeField, Min(1f)] private float fontSize = 30f;
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private bool enableShadow = true;

    private TMP_Text targetText;
    private const float ShadowWidth = 0.2f;

    public void Bind(TMP_Text text)
    {
        targetText = text;
        Apply();
    }

    private void OnValidate()
    {
        horizontalPosition = Mathf.Clamp01(horizontalPosition);
        verticalPosition = Mathf.Clamp01(verticalPosition);
        alpha = Mathf.Clamp01(alpha);
        fontSize = Mathf.Max(1f, fontSize);
        Apply();
    }

    private void Apply()
    {
        if (targetText == null)
            return;

        var rect = targetText.rectTransform;
        var normalizedPosition = new Vector2(horizontalPosition, verticalPosition);
        rect.anchorMin = normalizedPosition;
        rect.anchorMax = normalizedPosition;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(1000f, 80f);

        targetText.alignment = TextAlignmentOptions.Center;
        targetText.fontSize = fontSize;
        var color = textColor;
        color.a = alpha;
        targetText.color = color;
        targetText.outlineWidth = enableShadow ? ShadowWidth : 0f;
        targetText.outlineColor = Color.black;
    }
}
