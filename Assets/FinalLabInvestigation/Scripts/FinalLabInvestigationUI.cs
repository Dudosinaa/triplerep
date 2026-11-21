using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FinalLab.Investigation
{
    public sealed class FinalLabInvestigationUI : MonoBehaviour
    {
        [SerializeField] private TMP_FontAsset font;
        private TMP_Text status, objective, controls, routeHint, prompt, subtitle;
        private CanvasGroup progressFade, subtitleFade, hintFade;
        private string pendingStatus, pendingObjective;
        private float transition = -1, subtitleUntil, hintUntil;

        public string StatusText => status != null ? status.text : "";
        public string ObjectiveText => objective != null ? objective.text : "";

        public void Configure(TMP_FontAsset asset) => font = asset;

        private void Awake()
        {
            var canvasNode = new GameObject("Investigation HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasNode.transform.SetParent(transform, false);
            var canvas = canvasNode.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 22;
            var scaler = canvasNode.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.45f;

            var hud = Panel(canvasNode.transform, "Progress", new Vector2(0, 1), new Vector2(0, 1), new Vector2(31, -30),
                new Vector2(546, 126), new Color(0.024f, 0.025f, 0.022f, 0.58f));
            Label(hud, "Research title", new Vector2(17, -12), new Vector2(500, 27), 20, new Color(0.92f, 0.82f, 0.55f),
                "ИССЛЕДОВАНИЕ ЛАБОРАТОРИИ").fontStyle = FontStyles.Bold;
            var values = new GameObject("Progress value", typeof(RectTransform), typeof(CanvasGroup));
            values.transform.SetParent(hud, false);
            Place(values.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, Vector2.zero);
            progressFade = values.GetComponent<CanvasGroup>();
            status = Label(values.transform, "Counter", new Vector2(17, -40), new Vector2(505, 27), 22, Color.white);
            objective = Label(values.transform, "Objective", new Vector2(17, -70), new Vector2(510, 48), 20, Color.white);
            controls = Label(canvasNode.transform, "Controls", new Vector2(49, -166), new Vector2(615, 83), 21, Color.white);
            routeHint = Label(canvasNode.transform, "Route hint", new Vector2(49, -168), new Vector2(660, 72), 22,
                new Color(0.96f, 0.86f, 0.62f));
            hintFade = routeHint.gameObject.AddComponent<CanvasGroup>();

            var bottom = Panel(canvasNode.transform, "Reaction", new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(0, 119), new Vector2(970, 86), new Color(0.024f, 0.025f, 0.022f, 0.76f));
            subtitleFade = bottom.gameObject.AddComponent<CanvasGroup>();
            subtitle = Label(bottom, "Alexei", new Vector2(22, -12), new Vector2(926, 63), 25, new Color(0.96f, 0.93f, 0.85f));
            subtitle.alignment = TextAlignmentOptions.Center;

            prompt = Label(canvasNode.transform, "Interaction", Vector2.zero, new Vector2(730, 54), 27, Color.white);
            Place(prompt.rectTransform, new Vector2(0.5f, 0.36f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(730, 54));
            prompt.alignment = TextAlignmentOptions.Center;
        }

        public void Progress(int complete, int total, string goal, bool immediate)
        {
            pendingStatus = $"Осмотрено: {complete}/{total}";
            pendingObjective = complete == total ? "Исследование завершено" : "Цель: " + goal;
            if (!immediate)
            {
                transition = 0;
                return;
            }
            status.text = pendingStatus;
            objective.text = pendingObjective;
            progressFade.alpha = 1;
        }

        public void SetPrompt(string value) => prompt.text = value;
        public void SetControls(string text) => controls.text = text;

        public void React(string quote)
        {
            subtitle.text = "АЛЕКСЕЙ   " + quote;
            subtitleUntil = Time.unscaledTime + 6.65f;
        }

        public void Hint(string text)
        {
            routeHint.text = text;
            hintUntil = Time.unscaledTime + 6.1f;
        }

        public void ClearHint() => hintUntil = 0;

        private void Update()
        {
            if (transition >= 0)
            {
                transition += Time.unscaledDeltaTime;
                if (transition >= 0.20f)
                {
                    if (status.text != pendingStatus) status.text = pendingStatus;
                    if (objective.text != pendingObjective) objective.text = pendingObjective;
                    progressFade.alpha = Mathf.Clamp01((transition - 0.20f) / 0.22f);
                }
                else progressFade.alpha = 1 - transition / 0.20f;
                if (transition >= 0.42f) transition = -1;
            }
            subtitleFade.alpha = Mathf.Clamp01((subtitleUntil - Time.unscaledTime) * 2.2f);
            hintFade.alpha = Mathf.Clamp01((hintUntil - Time.unscaledTime) * 2.2f);
        }

        private static RectTransform Panel(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 position,
            Vector2 size, Color background)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(Image));
            item.transform.SetParent(parent, false);
            var image = item.GetComponent<Image>();
            image.color = background;
            image.raycastTarget = false;
            var rect = item.GetComponent<RectTransform>();
            Place(rect, anchor, pivot, position, size);
            return rect;
        }

        private TMP_Text Label(Transform parent, string name, Vector2 position, Vector2 size, float fontSize, Color color,
            string value = "")
        {
            var item = new GameObject(name, typeof(RectTransform));
            item.transform.SetParent(parent, false);
            var label = item.AddComponent<TextMeshProUGUI>();
            Place(label.rectTransform, new Vector2(0, 1), new Vector2(0, 1), position, size);
            label.font = font;
            label.fontSize = fontSize;
            label.color = color;
            label.text = value;
            label.raycastTarget = false;
            label.richText = false;
            label.textWrappingMode = TextWrappingModes.Normal;
            return label;
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
