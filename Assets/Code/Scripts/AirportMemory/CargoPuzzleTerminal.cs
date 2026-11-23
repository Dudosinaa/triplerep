using UnityEngine;
using UnityEngine.InputSystem;

// Reads the four facts in the ordinary ReadableDocument props from main.
public sealed class CargoPuzzleTerminal : MonoBehaviour
{
    private static readonly string[][] Options =
    {
        new[] { "F-04", "K-17", "L-09" },
        new[] { "12.06.2004", "14.06.2004", "16.06.2004" },
        new[] { "Сергей Карамазов", "И. Карамазов", "Михаил Орлов" },
        new[] { "Семейный багаж, Нью-Йорк", "Технический склад, Нью-Йорк", "Личные вещи, Москва" }
    };
    private static readonly string[] Labels =
        { "Номер контейнера", "Дата отправки", "Отправитель", "Адрес получателя" };

    [SerializeField] private FirstPersonPlayer player;
    [SerializeField] private Camera playerCamera;
    [SerializeField, Min(1f)] private float interactionDistance = 3.5f;
    private readonly int[] selected = new int[4];
    private DocumentReader reader;
    private ItemInspector inspector;
    private DialogueController dialogue;
    private bool open;
    private bool solved;
    private bool movementWasEnabled;
    private string feedback = "Сверь данные трёх накладных и маркировку К-17.";

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
    }

    private void Update()
    {
        if (GameplayPause.BlocksGameplayInput) return;
        if (Keyboard.current == null) return;
        if (open)
        {
            if (InteractionInput.TryConsumeCancel() || (Keyboard.current.eKey.wasPressedThisFrame && InteractionInput.TryConsume()))
                Close();
            return;
        }
        if (player == null || !player.enabled || playerCamera == null ||
            (reader != null && reader.IsReading) ||
            (inspector != null && inspector.IsInspecting) ||
            (dialogue != null && dialogue.IsTalking)) return;
        if (!Keyboard.current.eKey.wasPressedThisFrame || !IsAimedAtTerminal() ||
            !InteractionInput.TryConsume()) return;
        open = true;
        movementWasEnabled = player.enabled;
        player.enabled = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
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
        if (player != null && movementWasEnabled) player.enabled = true;
    }

    private void OnDisable() { if (open) Close(); }

    private void OnGUI()
    {
        if (GameplayPause.BlocksGameplayInput) return;
        if (!open)
        {
            if (solved)
                GUI.Box(new Rect(Screen.width - 340f, 12f, 328f, 42f),
                    "УЛИКА: накладная К-17 / Иван Карамазов");
            if (player != null && player.enabled && playerCamera != null && IsAimedAtTerminal())
                GUI.Box(new Rect(Screen.width * .35f, Screen.height * .78f,
                    Screen.width * .3f, 38f), "[E] Проверить грузовую накладную");
            return;
        }
        float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
        var area = new Rect((Screen.width - 700f * scale) * .5f,
            (Screen.height - 570f * scale) * .5f, 700f * scale, 570f * scale);
        Color previousColor = GUI.color;
        GUI.color = new Color(.065f, .075f, .07f, 1f);
        GUI.DrawTexture(area, Texture2D.whiteTexture);
        GUI.color = previousColor;
        GUI.Box(area, "ТЕРМИНАЛ ГРУЗОВОГО УЧЁТА");
        GUILayout.BeginArea(new Rect(area.x + 24f * scale, area.y + 35f * scale,
            area.width - 48f * scale, area.height - 55f * scale));
        GUILayout.Label("Найди запись технического контейнера. Выбери данные из трёх документов.");
        GUILayout.Space(10f * scale);
        for (int i = 0; i < Labels.Length; i++)
        {
            GUILayout.Label(Labels[i]);
            if (GUILayout.Button(Options[i][selected[i]], GUILayout.Height(34f * scale)))
                selected[i] = (selected[i] + 1) % Options[i].Length;
            GUILayout.Space(4f * scale);
        }
        GUILayout.Label(feedback, GUILayout.Height(96f * scale));
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Сопоставить", GUILayout.Height(38f * scale))) CheckAnswer();
        if (GUILayout.Button("Закрыть [E / Esc]", GUILayout.Height(38f * scale))) Close();
        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }

    private void CheckAnswer()
    {
        for (int i = 0; i < selected.Length; i++)
            if (selected[i] != 1)
            {
                feedback = "Данные не совпали. Проверь номер, дату, подпись и адрес.";
                return;
            }
        solved = true;
        PlayerPrefs.SetInt("AirportMemory.WaybillK17", 1);
        PlayerPrefs.Save();
        feedback = "НАКЛАДНАЯ НАЙДЕНА: K-17, 14.06.2004. Отправитель — Иван Карамазов. " +
            "Получатель — технический склад, Нью-Йорк. Копия с подписью Ивана получена.\n" +
            "Теперь поговори с Тихоном Инокентьевичем.";
    }
}
