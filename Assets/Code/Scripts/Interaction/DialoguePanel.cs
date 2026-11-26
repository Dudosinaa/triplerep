using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

internal sealed class DialoguePanel : MonoBehaviour
{
    internal const string ContinueHint = "ЛКМ — следующая реплика";
    internal const string FinishHint = "ЛКМ — завершить разговор";

    [SerializeField] private Image frame;
    [SerializeField] private Image backing;
    [SerializeField] private TMP_Text speaker;
    [SerializeField] private TMP_Text body;
    [SerializeField] private TMP_Text hint;

    private RectTransform choiceList;
    private readonly List<(Button Button, TMP_Text Label, DialogueExitIcon Exit)> buttons = new();
    private Color buttonColor;
    private Color highlightColor;
    private Color pressedColor;

    internal static DialoguePanel Create(Transform owner)
    {
        var prefab = Resources.Load<GameObject>("DialoguePanel");
        var panel = Instantiate(prefab, owner, false).GetComponent<DialoguePanel>();
        panel.name = "Dialogue Panel";
        panel.ApplyPalette(owner.gameObject.scene.path == TrainMindSession.TrainPath);
        return panel;
    }

    private void ApplyPalette(bool train)
    {
        const float alpha = DialoguePanelFrame.BackgroundAlpha;
        if (train)
        {
            buttonColor = new Color(0.20f, 0.17f, 0.12f, alpha);
            highlightColor = new Color(0.36f, 0.30f, 0.21f, alpha);
            pressedColor = new Color(0.44f, 0.37f, 0.26f, alpha);
            return;
        }

        frame.color = new Color(0.16f, 0.25f, 0.32f, alpha);
        backing.color = new Color(0.06f, 0.09f, 0.13f, alpha);
        speaker.color = new Color(0.93f, 0.72f, 0.39f);
        body.color = Color.white;
        hint.color = new Color(0.82f, 0.85f, 0.90f);
        buttonColor = frame.color;
        highlightColor = new Color(0.28f, 0.45f, 0.56f, alpha);
        pressedColor = new Color(0.34f, 0.57f, 0.67f, alpha);
    }

    internal void Show(string name, string line, string prompt = "")
    {
        speaker.text = name;
        body.text = line;
        hint.text = prompt;
        gameObject.SetActive(true);
    }

    internal void Hide() => gameObject.SetActive(false);

    internal void SetChoices(IReadOnlyList<NpcDialogue.Choice> choices, Action<int> select)
    {
        int count = choices.Count > 1 ? choices.Count : 0;
        while (buttons.Count < count) AddChoiceButton(select);
        for (int i = 0; i < buttons.Count; i++)
        {
            var (button, label, exit) = buttons[i];
            bool visible = i < count;
            button.gameObject.SetActive(visible);
            if (!visible) continue;
            var choice = choices[i];
            label.text = i < 3 ? $"{i + 1}. {choice.text}" : choice.text;
            exit.gameObject.SetActive(choice.EndsDialogue);
            label.rectTransform.offsetMin = new Vector2(choice.EndsDialogue ? 35f : 11.25f, 3.75f);
        }
    }

    private void AddChoiceButton(Action<int> select)
    {
        if (choiceList == null)
        {
            var group = new GameObject("Dialogue Choices", typeof(RectTransform),
                typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            choiceList = group.GetComponent<RectTransform>();
            choiceList.SetParent(frame.rectTransform, false);
            choiceList.anchorMin = new Vector2(0f, 1f);
            choiceList.anchorMax = Vector2.one;
            choiceList.pivot = new Vector2(0.5f, 0f);
            choiceList.sizeDelta = Vector2.zero;
            choiceList.anchoredPosition = new Vector2(0f, 6.25f);
            var layout = group.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 3.75f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            group.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        int index = buttons.Count;
        var row = new GameObject($"Choice {index + 1}", typeof(RectTransform), typeof(Image),
            typeof(Button), typeof(LayoutElement));
        row.transform.SetParent(choiceList, false);
        row.GetComponent<LayoutElement>().preferredHeight = 37.5f;
        var button = row.GetComponent<Button>();
        button.targetGraphic = row.GetComponent<Image>();
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        var colors = button.colors;
        colors.normalColor = buttonColor;
        colors.highlightedColor = highlightColor;
        colors.pressedColor = pressedColor;
        colors.selectedColor = highlightColor;
        colors.disabledColor = buttonColor;
        button.colors = colors;
        button.onClick.AddListener(() => select(index));

        var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(row.transform, false);
        var label = labelObject.GetComponent<TMP_Text>();
        label.font = body.font;
        label.fontSharedMaterial = body.fontSharedMaterial;
        label.color = body.color;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.richText = false;
        label.raycastTarget = false;
        label.enableAutoSizing = true;
        label.fontSizeMin = 15f;
        label.fontSizeMax = 20f;
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMax = new Vector2(-11.25f, -3.75f);

        var iconObject = new GameObject("Dialogue Exit Icon", typeof(RectTransform), typeof(DialogueExitIcon));
        iconObject.transform.SetParent(row.transform, false);
        var icon = iconObject.GetComponent<DialogueExitIcon>();
        icon.color = speaker.color;
        icon.raycastTarget = false;
        var rect = icon.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
        rect.sizeDelta = new Vector2(17.5f, 17.5f);
        rect.anchoredPosition = new Vector2(17.5f, 0f);
        buttons.Add((button, label, icon));
    }
}
