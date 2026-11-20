using UnityEngine;
using UnityEngine.InputSystem;

public sealed partial class RadioMemoryPuzzle : MonoBehaviour
{
    private enum Target { None, Radio, Fuse }
    private enum DragPart { None, Fuse, SpeakerPlug }
    private static readonly string[] Sockets = { "АНТЕННА", "ДИНАМИК", "ВНЕШНИЙ\nВХОД" };
    private static readonly string[] Subtitles =
    {
        "Отец: Я говорил тебе, что K-17 нельзя перевозить отдельно.",
        "Иван: Я уже всё оформил. Контейнер отправят раньше нас.",
        "Отец: Ты хоть понимаешь, что без второго блока он бесполезен?",
        "[Запись прерывается.]"
    };
    private static readonly float[] LineSeconds = { 4.5f, 4.5f, 4.5f, 2f };
    public const string ClueKey = "FirstMind.Clue.K17";

    [SerializeField] private FirstPersonPlayer player;
    [SerializeField] private Camera view;
    [SerializeField] private AudioSource recordingSource;
    [Tooltip("По порядку: первая реплика отца, реплика Ивана, последняя реплика отца, обрыв записи.")]
    [SerializeField] private AudioClip[] voiceLines;
    private BoxCollider radioTarget;
    private bool panelOpen;
    private bool hasFuse, fuseInstalled, speakerConnected, externalInput, solved;
    private int line = -1;
    private float lineEndsAt;
    private string feedback = "";
    private Target aimedTarget;
    private DragPart dragging;
    private GUIStyle titleStyle, bodyStyle, centeredStyle, subtitleStyle;

    private void Awake()
    {
        if (player == null || view == null) { enabled = false; return; }
        var renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) { enabled = false; return; }
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        var target = new GameObject("Radio interaction target");
        target.transform.SetPositionAndRotation(bounds.center, Quaternion.identity);
        target.transform.SetParent(transform, true);
        radioTarget = target.AddComponent<BoxCollider>();
        radioTarget.isTrigger = true;
        radioTarget.size = bounds.size + Vector3.one * .04f;
        if (recordingSource == null)
        {
            recordingSource = gameObject.AddComponent<AudioSource>();
            recordingSource.playOnAwake = false;
        }
        CreateFusePickup();
        CreateRoomAudio();
    }

    private void Update()
    {
        if (line >= 0 && Time.unscaledTime >= lineEndsAt)
        {
            if (line + 1 < Subtitles.Length) ShowLine(line + 1);
            else
            {
                line = -1;
                solved = true;
                PlayerPrefs.SetInt(ClueKey, 1);
                PlayerPrefs.Save();
            }
        }

        var keyboard = Keyboard.current;
        if (solved && !panelOpen && line < 0 && keyboard != null &&
            keyboard.uKey.wasPressedThisFrame && TrainMindSession.TryReturn(gameObject.scene)) return;
        if (panelOpen)
        {
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) Close();
            return;
        }

        aimedTarget = player.isActiveAndEnabled && line < 0 ? FindTarget() : Target.None;
        if (aimedTarget == Target.None || keyboard == null ||
            !keyboard.eKey.wasPressedThisFrame || !InteractionInput.TryConsume()) return;
        if (aimedTarget == Target.Fuse)
        {
            hasFuse = true;
            fusePickup.gameObject.SetActive(false);
            PlayEffect(contactClick);
            notice = "Предохранитель 0,5 А найден. Вернись к радио.";
            noticeUntil = Time.unscaledTime + 3f;
            aimedTarget = Target.None;
            return;
        }
        panelOpen = true;
        feedback = "";
        player.enabled = false;
    }

    private Target FindTarget()
    {
        var ray = new Ray(view.transform.position, view.transform.forward);
        bool hasSolid = Physics.Raycast(ray, out var hit, 2.2f, ~0, QueryTriggerInteraction.Ignore);
        float reach = hasSolid ? hit.distance + .02f : 2.2f;
        bool radioAimed = radioTarget.Raycast(ray, out var radioHit, 2.2f) &&
            (radioHit.distance <= reach || (hasSolid && hit.transform.IsChildOf(transform)));
        if (!hasFuse && fusePickup != null && fusePickup.Raycast(ray, out var fuseHit, reach) &&
            (!radioAimed || fuseHit.distance < radioHit.distance)) return Target.Fuse;
        return radioAimed ? Target.Radio : Target.None;
    }

    private void Close()
    {
        if (!panelOpen) return;
        panelOpen = false;
        dragging = DragPart.None;
        if (player != null && player.gameObject.activeInHierarchy) player.enabled = true;
    }

    private void OnDisable()
    {
        Close();
        if (recordingSource != null) recordingSource.Stop();
        line = -1;
    }

    private void PlayRecording()
    {
        if (!fuseInstalled || !speakerConnected || !externalInput)
        {
            feedback = "Для записи нужны питание, динамик и внешний вход.";
            return;
        }
        PlayEffect(tapeNoise);
        Close();
        ShowLine(0);
    }

    private void InstallFuse()
    {
        if (!hasFuse) return;
        fuseInstalled = true;
        timingStartedAt = Time.unscaledTime;
        PlayEffect(contactClick);
        feedback = "Удерживай штекер над разъёмом 2. Отпусти, когда метка попадёт в зелёную зону.";
    }

    private void ConnectSpeaker(int socket)
    {
        if (!fuseInstalled)
        {
            feedback = "Сначала найди и установи предохранитель.";
            return;
        }
        if (socket == 1 && IsSafePhase(TimingPhase))
        {
            speakerConnected = true;
            PlayEffect(contactClick);
            feedback = "Динамик подключён. Выбери внешний вход и нажми Play.";
            return;
        }
        ResetAttempt(socket == 1
            ? "Разряд! Ты промахнулся по зелёной зоне."
            : "Разряд! Штекер попал в другой разъём.");
    }

    private void ShowLine(int index)
    {
        line = index;
        var clip = voiceLines != null && index < voiceLines.Length ? voiceLines[index] : null;
        if (recordingSource != null)
        {
            recordingSource.Stop();
            if (clip != null) recordingSource.PlayOneShot(clip);
        }
        lineEndsAt = Time.unscaledTime + Mathf.Max(LineSeconds[index], clip != null ? clip.length : 0f);
    }

    private void OnGUI()
    {
        if (dragControlId != 0 && dragging == DragPart.None)
            ReleaseDragControl();

        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 23, fontStyle = FontStyle.Bold };
            bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, wordWrap = true };
            centeredStyle = new GUIStyle(bodyStyle) { alignment = TextAnchor.MiddleCenter };
            subtitleStyle = new GUIStyle(centeredStyle) { fontSize = 21 };
        }

        float scale = Mathf.Min(Screen.width / 960f, Screen.height / 600f);
        var matrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
        float width = Screen.width / scale, height = Screen.height / scale;
        if (panelOpen)
            DrawPanel(new Rect((width - 800) / 2, (height - 570) / 2, 800, 570));
        else if (line >= 0)
        {
            var rect = new Rect((width - 720) / 2, height - 106, 720, 76);
            Backdrop(rect);
            GUI.Label(new Rect(rect.x + 18, rect.y + 4, rect.width - 36, 68), Subtitles[line], subtitleStyle);
        }
        else if (aimedTarget != Target.None || Time.unscaledTime < noticeUntil)
        {
            string hint = aimedTarget == Target.Radio ? "[E] Осмотреть радиокомплекс" :
                aimedTarget == Target.Fuse ? "[E] Взять предохранитель" : notice;
            var rect = new Rect((width - 460) / 2, height * .76f, 460, 42);
            Backdrop(rect);
            GUI.Label(rect, hint, centeredStyle);
        }
        if (solved && !panelOpen && line < 0)
        {
            var rect = new Rect(width - 302, 16, 286, 38);
            Backdrop(rect);
            string clue = TrainMindSession.OwnsMindScene(gameObject.scene)
                ? "Улика №1 · K-17   [U] Вернуться"
                : "Улика №1 · Номер K-17";
            GUI.Label(rect, clue, centeredStyle);
        }
        GUI.matrix = matrix;
    }

    private static void Backdrop(Rect rect)
    {
        var color = GUI.color;
        GUI.color = new Color(.075f, .064f, .048f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = color;
    }
}
