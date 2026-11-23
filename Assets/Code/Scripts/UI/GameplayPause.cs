using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Modal gameplay interactions get first use of Escape in Update.
[DefaultExecutionOrder(1000)]
internal sealed class GameplayPause : MonoBehaviour
{
    private static GameplayPause activePause;
    private static int resumeFrame = -1;
    private static readonly Color PanelColor = new Color32(19, 30, 39, 255);
    private static readonly Color SecondaryColor = new Color32(34, 48, 58, 255);
    private static readonly Color AccentColor = new Color32(217, 183, 116, 255);
    private static readonly Color TextColor = new Color32(239, 233, 219, 255);

    internal static bool BlocksGameplayInput => activePause != null || resumeFrame == Time.frameCount;
    internal static bool HandlesEscape => SceneEntryCheckpoint.IsGameplayScene(SceneManager.GetActiveScene());

    private GameObject overlay, menu, confirmation, previousSelection;
    private EventSystem eventSystem, fallbackEventSystem;
    private Button resume, exit, yes, no;
    private Font font;
    private float previousTimeScale;
    private bool previousAudioPause, previousCursorVisibility, leaving;
    private CursorLockMode previousCursorLock;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        activePause = null;
        resumeFrame = -1;
    }

    private void LateUpdate()
    {
        if (activePause != null || gameObject.scene != SceneManager.GetActiveScene() ||
            Time.timeScale <= 0f || LocationTransition.IsTransitioning || TrainMindSession.IsTransitioning) return;

        if (InteractionInput.TryConsumeCancel()) Pause();
    }

    private void Pause()
    {
        if (overlay == null) CreateOverlay();
        previousTimeScale = Time.timeScale;
        previousAudioPause = AudioListener.pause;
        previousCursorLock = Cursor.lockState;
        previousCursorVisibility = Cursor.visible;
        activePause = this;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        eventSystem = EventSystem.current;
        if (eventSystem == null && fallbackEventSystem == null)
        {
            var input = new GameObject("Pause Event System");
            input.transform.SetParent(overlay.transform, false);
            fallbackEventSystem = input.AddComponent<EventSystem>();
            input.AddComponent<InputSystemUIInputModule>();
        }
        if (eventSystem == null) eventSystem = fallbackEventSystem;
        if (fallbackEventSystem != null) fallbackEventSystem.gameObject.SetActive(eventSystem == fallbackEventSystem);
        previousSelection = eventSystem.currentSelectedGameObject;
        overlay.SetActive(true);
        ShowConfirmation(false);
        resume.Select();
    }

    private void ShowConfirmation(bool show)
    {
        menu.SetActive(!show);
        confirmation.SetActive(show);
        // Focus the safe choice for keyboard and gamepad users.
        (show ? no : exit).Select();
    }

    private void Resume()
    {
        if (activePause != this) return;
        activePause = null;
        resumeFrame = Time.frameCount;
        Time.timeScale = previousTimeScale;
        AudioListener.pause = previousAudioPause;
        if (!leaving)
        {
            Cursor.lockState = previousCursorLock;
            Cursor.visible = previousCursorVisibility;
            if (eventSystem != null) eventSystem.SetSelectedGameObject(previousSelection);
        }
        if (overlay != null) overlay.SetActive(false);
    }

    private void ExitToMainMenu()
    {
        if (leaving || !LocationTransition.TryGetBuildIndex(SceneEntryCheckpoint.MainMenuPath, out int index)) return;
        leaving = true;
        try
        {
            if (SceneManager.LoadSceneAsync(index, LoadSceneMode.Single) != null)
            {
                yes.interactable = no.interactable = false;
                return;
            }
        }
        catch (Exception error) { Debug.LogException(error, this); }
        leaving = false;
    }

    private void OnDisable() => Resume();

    private void CreateOverlay()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        overlay = new GameObject("Pause Overlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        overlay.transform.SetParent(transform, false);
        overlay.SetActive(false);
        var canvas = overlay.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;
        var layers = SortingLayer.layers;
        if (layers.Length > 0) canvas.sortingLayerID = layers[layers.Length - 1].id;
        var scaler = overlay.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        var shade = CreateImage(overlay.transform, "Backdrop", new Color32(5, 9, 13, 200));
        Stretch(shade.rectTransform);
        menu = CreatePanel(shade.transform, "Pause Menu", new Vector2(380, 196));
        confirmation = CreatePanel(shade.transform, "Exit Confirmation", new Vector2(490, 180));
        resume = CreateButton(menu.transform, "Продолжить", new Vector2(0, 34), 284, true, Resume);
        exit = CreateButton(menu.transform, "Выйти", new Vector2(0, -34), 284, false, () => ShowConfirmation(true));
        CreateLabel(confirmation.transform, "Вы уверены, что хотите выйти в главное меню?",
            new Vector2(0, 36), new Vector2(410, 66), TextColor, 23);
        yes = CreateButton(confirmation.transform, "Да", new Vector2(-100, -41), 176, false, ExitToMainMenu);
        no = CreateButton(confirmation.transform, "Нет", new Vector2(100, -41), 176, true, () => ShowConfirmation(false));
    }

    private static GameObject CreatePanel(Transform parent, string name, Vector2 size)
    {
        Image panel = CreateImage(parent, name, PanelColor);
        Place(panel.rectTransform, Vector2.zero, size);
        Image rule = CreateImage(panel.transform, "Top Rule", AccentColor);
        rule.raycastTarget = false;
        var rect = rule.rectTransform;
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(.5f, 1);
        rect.sizeDelta = new Vector2(0, 2);
        return panel.gameObject;
    }

    private Button CreateButton(Transform parent, string caption, Vector2 position, float width,
        bool primary, UnityAction onClick)
    {
        Image image = CreateImage(parent, caption, Color.white);
        var size = new Vector2(width, 48);
        Place(image.rectTransform, position, size);
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        Color background = primary ? AccentColor : SecondaryColor;
        var colors = button.colors;
        colors.normalColor = background;
        colors.highlightedColor = Color.Lerp(background, Color.white, .22f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = Color.Lerp(background, Color.black, .15f);
        button.colors = colors;
        button.onClick.AddListener(onClick);
        CreateLabel(image.transform, caption, Vector2.zero, size, primary ? PanelColor : TextColor, 23);
        return button;
    }

    private void CreateLabel(Transform parent, string caption, Vector2 position, Vector2 size, Color color, int fontSize)
    {
        var label = new GameObject("Label", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        label.transform.SetParent(parent, false);
        label.font = font;
        label.text = caption;
        label.color = color;
        label.fontSize = fontSize;
        label.alignment = TextAnchor.MiddleCenter;
        label.raycastTarget = false;
        Place(label.rectTransform, position, size);
    }

    private static Image CreateImage(Transform parent, string name, Color color)
    {
        var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        image.transform.SetParent(parent, false);
        image.color = color;
        return image;
    }

    private static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
