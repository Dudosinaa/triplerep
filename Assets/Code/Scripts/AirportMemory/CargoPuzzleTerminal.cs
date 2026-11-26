using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Reads the four facts in the ordinary ReadableDocument props from main.
public sealed class CargoPuzzleTerminal : MonoBehaviour
{
    internal const string WaybillKey = "AirportMemory.WaybillK17";

    private static readonly string[][] Options =
    {
        new[] { "F-04", "К-17", "L-09" },
        new[] { "12.06.1992", "14.06.1992", "16.06.1992" },
        new[] { "Сергей Карамазов", "Иван Карамазов", "Михаил Орлов" },
        new[] { "Семейный багаж, Нью-Йорк", "Технический склад, Нью-Йорк", "Личные вещи, Москва" }
    };
    private static readonly Color Pending = new Color32(181, 155, 110, 255);
    private static readonly Color Mismatch = new Color32(236, 153, 113, 255);
    private static readonly Color Match = new Color32(172, 211, 153, 255);

    [SerializeField] private FirstPersonPlayer player;
    [SerializeField] private Camera playerCamera;
    [SerializeField, Min(1f)] private float interactionDistance = 3.5f;
    [SerializeField] private GameObject panel, prompt, clue;
    [SerializeField] private Button[] facts;
    [SerializeField] private TMP_Text[] values;
    [SerializeField] private Button submit, dismiss;
    [SerializeField] private TMP_Text feedbackTitle, feedbackRecord, feedbackDetails;
    [SerializeField] private Image feedbackAccent;

    private readonly int[] selected = new int[4];
    private DocumentReader reader;
    private ItemInspector inspector;
    private DialogueController dialogue;
    private bool open;
    private bool solved;
    private bool movementWasEnabled, cursorWasVisible, waitForRelease;
    private CursorLockMode previousCursorLock;
    private GameObject previousSelection;
    private int ignoreClicksThrough;

    public bool IsSolved => solved;

    private void Awake()
    {
        if (player == null) player = FindFirstObjectByType<FirstPersonPlayer>();
        if (playerCamera == null && player != null) playerCamera = player.GetComponentInChildren<Camera>();
        if (player != null)
        {
            reader = player.GetComponent<DocumentReader>();
            inspector = player.GetComponent<ItemInspector>();
            dialogue = player.GetComponent<DialogueController>();
        }
        for (int i = 0; i < facts.Length; i++)
        {
            int field = i;
            facts[field].onClick.AddListener(() => Cycle(field));
            values[field].text = Options[field][selected[field]];
        }
        submit.onClick.AddListener(CheckAnswer);
        dismiss.onClick.AddListener(() => { if (CanClick()) Close(); });
        ShowPending();
    }

    private void Update()
    {
        bool paused = GameplayPause.BlocksGameplayInput;
        var keyboard = Keyboard.current;
        bool aimed = !paused && !open && CanApproach() && IsAimedAtTerminal();
        prompt.SetActive(aimed);
        clue.SetActive(!paused && !open && solved);
        if (paused) return;
        if (open)
        {
            if (waitForRelease && Mouse.current?.leftButton.isPressed != true)
            {
                waitForRelease = false;
                ignoreClicksThrough = Time.frameCount;
            }
            if (InteractionInput.TryConsumeCancel() || (keyboard?.eKey.wasPressedThisFrame == true && InteractionInput.TryConsume()))
                Close();
            return;
        }
        if (aimed && keyboard?.eKey.wasPressedThisFrame == true && InteractionInput.TryConsume()) Open();
    }

    private bool CanApproach() => player != null && player.enabled && playerCamera != null &&
        (reader == null || !reader.IsReading) && (inspector == null || !inspector.IsInspecting) &&
        (dialogue == null || !dialogue.IsTalking);

    private void Open()
    {
        open = true;
        movementWasEnabled = player.enabled;
        previousCursorLock = Cursor.lockState;
        cursorWasVisible = Cursor.visible;
        ignoreClicksThrough = Time.frameCount;
        waitForRelease = Mouse.current?.leftButton.isPressed == true;
        if (EventSystem.current != null)
        {
            previousSelection = EventSystem.current.currentSelectedGameObject;
            EventSystem.current.SetSelectedGameObject(null);
        }
        player.enabled = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        prompt.SetActive(false);
        clue.SetActive(false);
        panel.SetActive(true);
    }

    private bool IsAimedAtTerminal()
    {
        var ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        return Physics.Raycast(ray, out var hit, interactionDistance, ~0, QueryTriggerInteraction.Ignore) &&
               hit.collider.GetComponentInParent<CargoPuzzleTerminal>() == this;
    }

    private void Close()
    {
        open = false;
        panel.SetActive(false);
        if (player != null && movementWasEnabled) player.enabled = true;
        Cursor.lockState = previousCursorLock;
        Cursor.visible = cursorWasVisible;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(previousSelection);
        previousSelection = null;
    }

    private void OnDisable() { if (open) Close(); }

    private bool CanClick() => open && Time.frameCount > ignoreClicksThrough && !waitForRelease &&
        !GameplayPause.BlocksGameplayInput;

    private void Cycle(int field)
    {
        if (!CanClick()) return;
        selected[field] = NextIndex(field, selected[field]);
        values[field].text = Options[field][selected[field]];
        ShowPending();
    }

    private void CheckAnswer()
    {
        if (!CanClick()) return;
        if (!MatchesWaybill(selected))
        {
            SetFeedback(Mismatch, "ДАННЫЕ НЕ СОВПАДАЮТ", "",
                "Записи противоречат друг другу. Проверь накладные и попробуй ещё раз.");
            return;
        }
        solved = true;
        PlayerPrefs.SetInt(WaybillKey, 1);
        PlayerPrefs.Save();
        SetFeedback(Match, "НАКЛАДНАЯ НАЙДЕНА", "К-17 · 14.06.1992",
            "<b>Отправитель:</b> Иван Карамазов\n<b>Получатель:</b> технический склад, Нью-Йорк\n" +
            "Копия с подписью Ивана получена.\n<b>Теперь поговори с Тихоном Иннокентьевичем.</b>");
    }

    private static int NextIndex(int field, int index) => (index + 1) % Options[field].Length;

    private static bool MatchesWaybill(int[] indices)
    {
        foreach (int index in indices)
            if (index != 1) return false;
        return true;
    }

    private void ShowPending() => SetFeedback(Pending, "ОЖИДАНИЕ ПРОВЕРКИ", "", "");

    private void SetFeedback(Color accent, string title, string record, string details)
    {
        feedbackTitle.text = title;
        feedbackTitle.color = accent;
        feedbackAccent.color = accent;
        feedbackRecord.text = record;
        feedbackDetails.text = details;
    }
}
