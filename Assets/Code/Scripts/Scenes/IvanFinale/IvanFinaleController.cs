using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>Call BeginIvanPhase after Alexei's blackout. Never starts during the investigation.</summary>
public sealed class IvanFinaleController : MonoBehaviour
{
    public enum Phase { Waiting, Breaker, Resonance, Isolation, Destruction, Escape, Platform, Complete, Failed, LastCarriage }
    [SerializeField] private FirstPersonPlayer player;
    [SerializeField] private Transform controlSpawn, platformSpawn;
    [SerializeField] private GameObject ivanActor, unconsciousAlexei, carriedArm, platform;
    [SerializeField] private GameObject fatherBody;
    [SerializeField] private Renderer surveillanceScreen;
    [SerializeField] private Transform surveillanceEye;
    [SerializeField] private Transform lastCarriageSpawn;
    [SerializeField] private GameObject labExit;
    [SerializeField] private Transform seatedEyePose;
    [SerializeField] private UnityEvent onCompleted;
    [SerializeField] private AudioClip[] spokenLines;
    public Phase State { get; private set; }
    public float Isolation { get; private set; }
    public float SecondsLeft { get; private set; }
    public Transform ModeSelector, GaugeNeedle;
    public Renderer[] IsolationSegments = Array.Empty<Renderer>();
    private MaterialPropertyBlock isolationInk;
    public event Action Completed;
    public void SetVoiceClip(int index, AudioClip clip)
    {
        if (index < 0 || index >= 13) throw new ArgumentOutOfRangeException(nameof(index));
        if (spokenLines == null || spokenLines.Length < 13) Array.Resize(ref spokenLines, 13);
        spokenLines[index] = clip;
    }
    public void UseAlexeiBody(GameObject body)
    {
        if (State != Phase.Waiting || body == null || body.GetComponentInParent<FirstPersonPlayer>() != null)
            throw new ArgumentException("Assign the fallen character's visual body before starting Ivan's phase.");
        unconsciousAlexei.SetActive(false);
        unconsciousAlexei = body;
        var control = body.GetComponent<IvanFinaleControl>() ?? body.AddComponent<IvanFinaleControl>();
        control.Kind = IvanFinaleControl.Action.Rescue;
        control.Prompt = "Поднять Алексея";
        if (body.GetComponent<Collider>() == null) body.AddComponent<BoxCollider>().size = new Vector3(.5f, .7f, 1.6f);
    }
    public void UseFatherBody(GameObject body)
    {
        if (State != Phase.Waiting || body == null) throw new ArgumentException("Assign the father's visual body before the handoff.");
        fatherBody.SetActive(false);
        fatherBody = body;
    }
    private bool latchReleased, coverOpen, carrying, transitioning;
    private readonly bool[] switches = new bool[3];
    private readonly bool[] required = { true, false, true };
    private Camera view, monitorCamera;
    private RenderTexture monitorTexture;
    private Material monitorMaterial;
    private AudioSource voice;
    private AudioLowPassFilter voiceFilter;
    private AudioEchoFilter voiceEcho;
    private AudioSource machinery;
    private AudioClip generatedHum;
    private AudioSource mechanisms;
    private AudioClip contact, metalStrain;
    private float nextImpact;
    private float overloadDeadline;
    private Vector3 armRest, pickupEyeRest;
    private float fade;
    private float subtitleUntil, savedSpeed;
    private string subtitle, feedback;
    private IvanFinaleControl focused;
    private IvanFinaleControl heldLever;
    private Light[] lights;
    private float[] lightIntensity;
    private GUIStyle text, caption;
    private readonly string[] objectives = {
        "", "Разорвать связь с тяговой сетью", "Настроить обратный резонанс",
        "Изолировать объект", "Запустить необратимую перегрузку",
        "Спасти Алексея и добраться до выхода К-17", "Нью-Йорк · раннее утро", "Последний рейс завершён", "Перегрузка. Вы не успели выйти", "Вынести Алексея из последнего вагона на платформу"
    };

    public void Configure(FirstPersonPlayer motor, Transform start, Transform arrival, GameObject ivan, GameObject alexei, GameObject arm, GameObject station,
        Renderer monitor, Transform eye, Transform carriageSpawn, GameObject father)
    {
        player = motor; controlSpawn = start;
        platformSpawn = arrival; ivanActor = ivan; unconsciousAlexei = alexei;
        carriedArm = arm; platform = station; surveillanceScreen = monitor; surveillanceEye = eye;
        lastCarriageSpawn = carriageSpawn;
        fatherBody = father;
    }

    public void BeginIvanPhase(CanvasGroup previousBlackout = null)
    {
        if (State != Phase.Waiting) return;
        if (player == null || controlSpawn == null || unconsciousAlexei == null || fatherBody == null ||
            lastCarriageSpawn == null || platformSpawn == null || surveillanceScreen == null || surveillanceEye == null)
            throw new InvalidOperationException("Install and configure the Ivan finale scene before starting the phase.");
        Time.timeScale = 1;
        player.gameObject.SetActive(true);
        StartCoroutine(Begin(previousBlackout));
    }

    private IEnumerator Begin(CanvasGroup previousBlackout)
    {
        State = Phase.Breaker;
        transitioning = true;
        fade = 1;
        player.enabled = false;
        yield return new WaitForSeconds(.7f);
        view = player.PlayerCamera;
        view.gameObject.SetActive(true);
        view.enabled = true;
        savedSpeed = player.WalkSpeed;
        player.Teleport(controlSpawn.position);
        player.ResetView(controlSpawn.eulerAngles.y);
        Vector3 standingEye = view.transform.localPosition;
        Quaternion standingLook = view.transform.localRotation;
        Vector3 seatedEye = new Vector3(standingEye.x, 1.16f, standingEye.z);
        Quaternion seatedLook = standingLook;
        if (previousBlackout != null)
        {
            if (seatedEyePose != null)
            {
                var cameraParent = view.transform.parent;
                seatedEye = cameraParent.InverseTransformPoint(seatedEyePose.position);
                seatedLook = Quaternion.Inverse(cameraParent.rotation) * seatedEyePose.rotation;
            }
            view.transform.SetLocalPositionAndRotation(seatedEye, seatedLook);
            previousBlackout.alpha = 0;
        }
        if (ivanActor != null) ivanActor.SetActive(false);
        foreach (var weapon in player.GetComponentsInChildren<PistolController>(true))
        { weapon.SetEquipped(false); weapon.enabled = false; }
        foreach (var investigation in FindObjectsByType<FinalLab.Investigation.FinalLabInvestigationManager>(FindObjectsSortMode.None))
            investigation.enabled = false;
        foreach (var hud in FindObjectsByType<FinalLab.Investigation.FinalLabInvestigationUI>(FindObjectsSortMode.None))
            hud.enabled = false;
        lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
        lightIntensity = new float[lights.Length];
        for (int i = 0; i < lights.Length; i++) lightIntensity[i] = lights[i].intensity;
        unconsciousAlexei.SetActive(true);
        fatherBody.SetActive(true);
        carriedArm.SetActive(false);
        armRest = carriedArm.transform.localPosition;
        platform.SetActive(false);
        SetupMonitor();
        voice = new GameObject("Finale dialogue voices").AddComponent<AudioSource>();
        voice.transform.SetParent(transform, false);
        voice.playOnAwake = false;
        voiceFilter = voice.gameObject.AddComponent<AudioLowPassFilter>();
        voiceFilter.cutoffFrequency = 1600;
        voiceEcho = voice.gameObject.AddComponent<AudioEchoFilter>();
        voiceEcho.delay = 180; voiceEcho.decayRatio = .4f; voiceEcho.wetMix = .25f;
        voiceFilter.enabled = voiceEcho.enabled = false;
        generatedHum = AudioClip.Create("K17 electrical strain", 44100, 1, 22050, false);
        var samples = new float[44100];
        for (int i = 0; i < samples.Length; i++)
        {
            float t = i / 22050f;
            samples[i] = .12f * Mathf.Sin(t * 2 * Mathf.PI * 50) + .035f * Mathf.Sin(t * 2 * Mathf.PI * 450);
        }
        generatedHum.SetData(samples, 0);
        machinery = gameObject.AddComponent<AudioSource>();
        machinery.clip = generatedHum; machinery.loop = true; machinery.volume = .15f; machinery.Play();
        mechanisms = gameObject.AddComponent<AudioSource>();
        contact = MechanismClip("Switch contact", .12f, 1700);
        metalStrain = MechanismClip("Metal under stress", 1.2f, 410);
        foreach (var door in FindObjectsByType<FinalLab.Investigation.FinalLabMetalDoor>(FindObjectsSortMode.None))
        { door.SetLocked(false); door.OpenDoor(); }
        Say(0, "Система: НОСИТЕЛЬ: АЛЕКСЕЙ / ПЕРЕНОС ФРАГМЕНТА", 6);
        if (previousBlackout != null)
        {
            yield return new WaitForSeconds(1.45f);
            const float riseDuration = .9f;
            for (float t = 0; t < riseDuration; t += Time.deltaTime)
            {
                float blend = Mathf.SmoothStep(0, 1, t / riseDuration);
                view.transform.SetLocalPositionAndRotation(Vector3.Lerp(seatedEye, standingEye, blend),
                    Quaternion.Slerp(seatedLook, standingLook, blend));
                yield return null;
            }
            view.transform.SetLocalPositionAndRotation(standingEye, standingLook);
        }
        player.enabled = true;
        transitioning = false;
    }

    private void SetupMonitor()
    {
        monitorTexture = new RenderTexture(640, 360, 16);
        monitorCamera = new GameObject("Containment CCTV").AddComponent<Camera>();
        monitorCamera.transform.SetParent(transform);
        monitorCamera.transform.SetPositionAndRotation(surveillanceEye.position, surveillanceEye.rotation);
        monitorCamera.targetTexture = monitorTexture;
        monitorCamera.fieldOfView = 58;
        monitorMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        monitorMaterial.mainTexture = monitorTexture;
        surveillanceScreen.material = monitorMaterial;
    }

    private void Update()
    {
        if (GameplayPause.BlocksGameplayInput) return;
        if (State == Phase.Waiting || view == null) return;
        fade = Mathf.MoveTowards(fade, 0, Time.deltaTime);
        if (State == Phase.Escape)
        {
            SecondsLeft = Mathf.Max(0, overloadDeadline - Time.time);
            if (SecondsLeft <= 0) Fail();
        }
        focused = null;
        if (transitioning || State == Phase.Platform || State == Phase.Complete) return;
        if (machinery != null)
        {
            machinery.volume = State == Phase.Escape ? .4f : State == Phase.Isolation ? .28f : .13f;
            machinery.pitch = State == Phase.Escape ? 1 + (60 - SecondsLeft) / 100 : 1;
        }
        focused = null;
        if (Physics.Raycast(view.transform.position, view.transform.forward, out var hit, 2.4f, ~0, QueryTriggerInteraction.Ignore))
            focused = hit.collider.GetComponentInParent<IvanFinaleControl>();
        var keyboard = Keyboard.current;
        if (State == Phase.Failed && keyboard != null && keyboard.rKey.wasPressedThisFrame)
        {
            RetryEscape();
        }
        if (focused != null && keyboard != null && keyboard.eKey.wasPressedThisFrame && InteractionInput.TryConsume())
            Use(focused);
        if (State == Phase.Isolation)
        {
            bool holding = focused != null && focused.Kind == IvanFinaleControl.Action.Hold && keyboard != null && keyboard.eKey.isPressed;
            if (holding) heldLever = focused;
            if (heldLever != null) heldLever.Move(holding ? -35 : 0);
            if (holding) Isolation = Mathf.Min(100, Isolation + Time.deltaTime * 4);
            if (GaugeNeedle != null) GaugeNeedle.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(52, -64, Isolation / 100));
            isolationInk ??= new MaterialPropertyBlock();
            for (int i = 0; i < IsolationSegments.Length; i++)
            {
                var color = Isolation >= (i + 1) * 10 ? new Color(.2f, .8f, .3f) : new Color(.12f, .14f, .12f);
                isolationInk.SetColor("_BaseColor", color);
                isolationInk.SetColor("baseColorFactor", color);
                IsolationSegments[i].SetPropertyBlock(isolationInk);
            }
            if (Isolation >= 35 && subtitleUntil < Time.time && !tempted)
            {
                tempted = true;
                Say(2, "Сущность: Ваня, остановись. Мы же собирались изменить мир. Наше открытие…", 6);
            }
            if (tempted && !answered && subtitleUntil < Time.time)
            {
                answered = true;
                Say(3, "Иван: Я уже изменил его. Хватит.", 4);
            }
            foreach (var beam in GetComponentsInChildren<IvanFinaleEnergy>()) beam.SetProgress(Isolation / 100, holding);
            if (Isolation >= 100)
            {
                State = Phase.Destruction;
                RestoreLights();
                feedback = "ИЗОЛЯЦИЯ 100%. Объект заключён в сердечник.";
            }
        }
        if (State == Phase.Escape)
        {
            if (Time.time >= nextImpact)
            {
                mechanisms.PlayOneShot(metalStrain, .55f);
                nextImpact = Time.time + UnityEngine.Random.Range(2.5f, 5);
            }
            if (SecondsLeft <= 0) Fail();
        }
        if (State == Phase.Isolation || State == Phase.Escape)
            for (int i = 0; i < lights.Length; i++)
                lights[i].intensity = lightIntensity[i] * (.5f + .5f * Mathf.Sin(Time.time * 19 + i));
    }
    private bool tempted, answered;

    public void Use(IvanFinaleControl control)
    {
        if (transitioning) return;
        if (mechanisms != null) mechanisms.PlayOneShot(contact, .45f);
        switch (control.Kind)
        {
            case IvanFinaleControl.Action.Latch:
                if (State != Phase.Breaker || latchReleased) return;
                latchReleased = true; control.Move(-70);
                feedback = "Фиксатор снят. Опусти рубильник.";
                break;
            case IvanFinaleControl.Action.Breaker:
                if (State != Phase.Breaker) return;
                if (!latchReleased) { feedback = "Сначала сними защитный фиксатор."; return; }
                control.Move(65); State = Phase.Resonance;
                Say(1, "Система: ВНЕШНИЙ КАНАЛ ОТКЛЮЧЁН. Питание от аккумулятора К-17.", 5);
                break;
            case IvanFinaleControl.Action.Switch:
                if (State != Phase.Resonance) return;
                switches[control.Index] = !switches[control.Index];
                control.Move(switches[control.Index] ? -35 : 35);
                if (switches[0] == required[0] && switches[1] == required[1] && switches[2] == required[2])
                {
                    State = Phase.Isolation;
                    if (ModeSelector != null) ModeSelector.localRotation = Quaternion.Euler(0, 0, 80);
                    Say(4, "Иван: Всю жизнь я пытался это включить… А теперь придётся уничтожить.", 6);
                    feedback = "РЕЖИМ Б: ГОТОВ. Удерживай рычаг обратного резонанса.";
                }
                break;
            case IvanFinaleControl.Action.Cover:
                if (State != Phase.Destruction) { feedback = "Перегрузка доступна после полной изоляции."; return; }
                coverOpen = true; control.Move(-110);
                control.GetComponent<Collider>().enabled = false;
                feedback = "Подтверди необратимую перегрузку красной кнопкой.";
                break;
            case IvanFinaleControl.Action.Destroy:
                if (State != Phase.Destruction || !coverOpen) return;
                if (labExit != null) labExit.SetActive(true);
                SecondsLeft = 60; overloadDeadline = Time.time + 60; State = Phase.Escape;
                Say(5, "Система: ПЕРЕГРУЗКА — 60 СЕКУНД", 5);
                break;
            case IvanFinaleControl.Action.Rescue:
                if (State != Phase.Escape || carrying) return;
                carrying = true;
                Say(6, "Иван: Прости меня…", 4);
                StartCoroutine(PickUpBrother());
                break;
            case IvanFinaleControl.Action.Exit:
                if (State == Phase.LastCarriage)
                {
                    State = Phase.Platform;
                    StartCoroutine(Ending());
                    return;
                }
                if (State != Phase.Escape) return;
                if (!carrying) { feedback = "Алексей остался в защитном отсеке."; return; }
                StartCoroutine(LeaveLaboratory());
                break;
        }
    }

    private IEnumerator PickUpBrother()
    {
        transitioning = true;
        player.enabled = false;
        pickupEyeRest = view.transform.localPosition;
        for (float elapsed = 0; elapsed < 1.1f; elapsed += Time.deltaTime)
        {
            float t = elapsed / 1.1f;
            view.transform.localPosition = pickupEyeRest - Vector3.up * (Mathf.Sin(t * Mathf.PI) * .18f);
            if (t >= .4f)
            {
                unconsciousAlexei.SetActive(false);
                carriedArm.SetActive(true);
                carriedArm.transform.localPosition = armRest - Vector3.up * (.35f * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.4f, 1, t))));
            }
            yield return null;
        }
        view.transform.localPosition = pickupEyeRest;
        carriedArm.transform.localPosition = armRest;
        player.WalkSpeed = 1.8f;
        player.ResetView(player.transform.eulerAngles.y);
        player.enabled = true;
        transitioning = false;
    }

    private IEnumerator LeaveLaboratory()
    {
        State = Phase.LastCarriage;
        transitioning = true;
        RestoreLights();
        player.enabled = false;
        fade = 1;
        platform.SetActive(true);
        player.Teleport(lastCarriageSpawn.position);
        player.ResetView(lastCarriageSpawn.eulerAngles.y);
        yield return new WaitForSeconds(1.2f);
        if (machinery != null) machinery.Stop();
        mechanisms.PlayOneShot(metalStrain);
        Say(12, "Позади грохочет сердечник. Гул обрывается. Поезд остановился.", 5);
        player.enabled = true;
        transitioning = false;
    }

    private IEnumerator Ending()
    {
        transitioning = true;
        player.enabled = false;
        fade = 1;
        platform.SetActive(true);
        player.Teleport(platformSpawn.position);
        player.ResetView(platformSpawn.eulerAngles.y);
        foreach (var beam in GetComponentsInChildren<IvanFinaleEnergy>()) beam.gameObject.SetActive(false);
        carriedArm.SetActive(false);
        unconsciousAlexei.transform.SetPositionAndRotation(platformSpawn.position + platformSpawn.forward * 2, Quaternion.LookRotation(-platformSpawn.forward));
        unconsciousAlexei.SetActive(true);
        unconsciousAlexei.GetComponent<IvanFinaleActor>()?.BeginRecovery();
        yield return new WaitForSeconds(2);
        string[] lines = {
            "Алексей: Отец погиб. Ты знал, во что нас втягиваешь, и всё равно продолжал.",
            "Иван: Я хотел вернуть его. Я думал, что смогу всё исправить.",
            "Алексей: Ты хотел доказать, что был прав. Даже когда я два года искал его.",
            "Иван: Я уничтожил установку. Всё, над чем работал. Я вытащил тебя.",
            "Алексей: Я хотел, чтобы ты был моим братом. Хотя бы один раз." };
        for (int i = 0; i < lines.Length; i++)
        {
            Say(7 + i, lines[i], 6);
            yield return new WaitForSeconds(Mathf.Max(6, voice.clip != null ? voice.clip.length : 0));
        }
        unconsciousAlexei.transform.rotation = Quaternion.LookRotation(platformSpawn.forward);
        var departing = unconsciousAlexei.GetComponent<IvanFinaleActor>();
        if (departing != null) departing.Walking = true;
        yield return new WaitForSeconds(6);
        State = Phase.Complete; player.WalkSpeed = savedSpeed;
        transitioning = false;
        player.enabled = true;
        if (machinery != null) machinery.Stop();
        onCompleted?.Invoke(); Completed?.Invoke();
    }

    public void RetryEscape()
    {
        if (State != Phase.Failed) return;
        SecondsLeft = 60;
        overloadDeadline = Time.time + 60;
        carrying = false;
        feedback = null;
        unconsciousAlexei.SetActive(true);
        carriedArm.SetActive(false);
        player.WalkSpeed = savedSpeed;
        player.Teleport(controlSpawn.position);
        player.ResetView(controlSpawn.eulerAngles.y);
        player.enabled = true;
        State = Phase.Escape;
    }

    private void Fail()
    {
        if (transitioning)
        {
            StopAllCoroutines();
            view.transform.localPosition = pickupEyeRest;
            carriedArm.transform.localPosition = armRest;
        }
        transitioning = false;
        State = Phase.Failed; player.enabled = false;
        feedback = "Нажми R, чтобы повторить побег.";
    }

    private void Say(int index, string words, float seconds)
    {
        feedback = null;
        subtitle = words; subtitleUntil = Time.time + seconds;
        if (voice != null)
        {
            voice.Stop(); voice.clip = null; voice.pitch = 1;
            voiceFilter.enabled = voiceEcho.enabled = false;
        }
        if (voice != null && spokenLines != null && index < spokenLines.Length && spokenLines[index] != null)
        {
            voice.clip = spokenLines[index];
            if (index == 2) { voice.pitch = .72f; voiceFilter.enabled = voiceEcho.enabled = true; }
            voice.Play();
            subtitleUntil = Time.time + Mathf.Max(seconds, voice.clip.length / voice.pitch);
        }
    }

    private void OnGUI()
    {
        if (GameplayPause.BlocksGameplayInput) return;
        if (State == Phase.Waiting) return;
        text ??= new GUIStyle(GUI.skin.label) { fontSize = 20, wordWrap = true, alignment = TextAnchor.MiddleCenter };
        caption ??= new GUIStyle(text) { fontSize = 24 };
        var matrix = GUI.matrix;
        float scale = Mathf.Min(Screen.width / 960f, Screen.height / 600f);
        GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
        float w = Screen.width / scale, h = Screen.height / scale;
        Box(new Rect(20, 16, w - 40, 55), "Иван Карамазов · " + objectives[(int)State], caption);
        if (State == Phase.Isolation || State == Phase.Destruction)
            Box(new Rect(w - 240, 85, 220, 60), $"ИЗОЛЯЦИЯ {Isolation:0}%", text);
        if (State == Phase.Escape) Box(new Rect(w - 240, 85, 220, 60), $"ПЕРЕГРУЗКА {Mathf.CeilToInt(SecondsLeft)} с", text);
        if (focused != null) Box(new Rect((w - 650) / 2, h - 145, 650, 40), "[E] " + focused.Prompt, text);
        if (subtitleUntil > Time.time) Box(new Rect((w - 850) / 2, h - 95, 850, 75), subtitle, text);
        else if (!string.IsNullOrEmpty(feedback)) Box(new Rect((w - 850) / 2, h - 95, 850, 75), feedback, text);
        if (fade > 0)
        {
            var color = GUI.color; GUI.color = new Color(0, 0, 0, fade);
            GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture); GUI.color = color;
        }
        GUI.matrix = matrix;
    }
    private static void Box(Rect rect, string words, GUIStyle style)
    {
        var color = GUI.color; GUI.color = new Color(.035f, .04f, .035f, .96f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = color; GUI.Label(rect, words, style);
    }
    private void OnDestroy()
    {
        RestoreLights();
        if (monitorTexture != null) { monitorTexture.Release(); Destroy(monitorTexture); }
        if (generatedHum != null) Destroy(generatedHum);
        if (monitorMaterial != null) Destroy(monitorMaterial);
        if (contact != null) Destroy(contact);
        if (metalStrain != null) Destroy(metalStrain);
    }

    private void RestoreLights()
    {
        if (lights == null) return;
        for (int i = 0; i < lights.Length; i++)
            if (lights[i] != null) lights[i].intensity = lightIntensity[i];
    }

    private static AudioClip MechanismClip(string title, float length, float frequency)
    {
        const int sampleRate = 22050;
        var data = new float[Mathf.CeilToInt(length * sampleRate)];
        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)sampleRate;
            float phase = t * frequency * 2 * Mathf.PI;
            data[i] = Mathf.Exp(-t * 6 / length) * Mathf.Clamp01(t * 300) *
                (.22f * Mathf.Sin(phase) + .13f * Mathf.Sin(phase * 2.31f) + .08f * Mathf.Sin(phase * 3.57f));
        }
        var clip = AudioClip.Create(title, data.Length, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
