using System.Collections;
using System.Collections.Generic;
using FinalLab.Investigation;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(25)]
public sealed class FinalLabMechanisms : MonoBehaviour
{
    internal enum Step { Inactive, Reserve, Isolation, FatherDoor, Traction, Mode, Contour, Stabilizing, Overload, Discharge }
    [SerializeField] private FinalLabFlowController flow;
    [SerializeField] private FirstPersonPlayer player;
    [SerializeField] private Camera view;
    [SerializeField] private FinalLabInvestigationManager investigation;
    [SerializeField] private FinalLabInvestigationUI investigationHud;
    [SerializeField] private FinalLabMetalDoor controlDoor;
    [SerializeField] private GameObject ivanActor;
    [SerializeField] private FinalLabAlexeiRescue rescue;
    [SerializeField] private Transform ivanSpawn, ivanView;
    [SerializeField] private FinalLabSwitch[] switches;
    [SerializeField] private Shader outlineShader;
    [SerializeField] private TMP_Text objective, telemetry, feedback, continueHint;
    [SerializeField] private Transform needle, resonatorCore;
    [SerializeField] private Renderer powerLens, contourLens;
    [SerializeField] private Renderer[] segments;
    [SerializeField] private Light[] reserveLights, pulseLights;
    [SerializeField] private LineRenderer[] arcs;

    private Step step;
    private FinalLabSwitch focused;
    private FinalLabIvanDialogue subtitles;
    private Material outlineMaterial;
    private CanvasGroup blackout;
    private readonly Queue<string> messages = new();
    private float nextLine, feedbackUntil, isolation, current = 9.4f;
    private int reserves, isolated;
    private bool blocked;
    private bool tractionNoted;
    private float[] pulseStrength;
    private Transform alexeiHead;

    private void Awake()
    {
        subtitles = flow.Father.Subtitles;
        outlineMaterial = new Material(outlineShader);
        outlineMaterial.SetColor("_Color", new Color(.98f, .76f, .31f));
        outlineMaterial.SetFloat("_Width", .005f);
        foreach (var item in switches) item.Prepare(outlineMaterial);
        pulseStrength = new float[pulseLights.Length];
        for (int i = 0; i < pulseLights.Length; i++) pulseStrength[i] = pulseLights[i].intensity;
    }

    private void OnEnable()
    {
        flow.GameplayPhaseStarted += StartAlexei;
        flow.FatherSequenceStarted += StopGameplayHud;
        flow.IvanGameplayPhaseStarted += StartIvan;
    }

    private void OnDisable()
    {
        flow.GameplayPhaseStarted -= StartAlexei;
        flow.FatherSequenceStarted -= StopGameplayHud;
        flow.IvanGameplayPhaseStarted -= StartIvan;
        Focus(null);
    }

    private void OnDestroy()
    {
        if (outlineMaterial != null) Destroy(outlineMaterial);
    }

    private void StartAlexei()
    {
        UseGameplayHud();
        step = Step.Reserve;
        Goal("Восстановить резервное питание   0/3");
        foreach (var lamp in reserveLights) lamp.enabled = false;
        Say("Алёша, дверь камеры запитана отдельно. Основная сеть мертва… Попробуй восстановить резервное питание.");
    }

    private void StartIvan() => StartCoroutine(ChangePerspective());

    private IEnumerator ChangePerspective()
    {
        blackout = flow.Father.ReleaseViewForRescue();
        blackout.alpha = 1;
        UseGameplayHud();
        blocked = true;
        step = Step.Traction;
        alexeiHead = rescue.PrepareAlexei();
        ivanActor.SetActive(false);
        player.Teleport(ivanSpawn.position);
        view.transform.localPosition = new Vector3(0, 1.65f, 0);
        player.SetViewDirection(ivanView.position - view.transform.position);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        yield return FinalLabAlexeiRescue.Fade(blackout, 0, 1.15f);
        player.enabled = true;
        blocked = false;
        Goal("Исследовать питание установки");
        SetTelemetry();
        Say("Питание установки нестабильно. Похоже, сущность использует общую энергетическую сеть.");
    }

    private void UseGameplayHud()
    {
        investigation.enabled = false;
        investigationHud.SetLaboratoryGameplay(true);
        continueHint.enabled = false;
        if (player.TryGetComponent(out PistolController pistol))
        {
            pistol.SetEquipped(false);
            pistol.enabled = false;
        }
    }

    private void Update()
    {
        if (step == Step.Inactive || blocked) return;
        PresentSpeech();
        if (Time.unscaledTime > feedbackUntil) Set(feedback, "");
        if (step == Step.FatherDoor) return;
        if (step == Step.Stabilizing) UpdateIsolation();
        if (!player.enabled) return;

        var target = FindControl();
        Focus(target);
        if (target != null && target.Kind == FinalLabSwitch.Function.Traction && !tractionNoted)
        {
            tractionNoted = true;
            Say("Тяговая линия продолжает подавать питание…");
        }

        bool lookingAtDoor = target == null && step >= Step.Mode &&
            Physics.Raycast(view.transform.position, view.transform.forward, out var hit, FinalLabMetalDoor.UseDistance, ~0,
                QueryTriggerInteraction.Collide) && hit.collider.GetComponentInParent<FinalLabMetalDoor>() == controlDoor;
        string prompt = target != null ? target.Label + " — E" : lookingAtDoor ? controlDoor.Prompt : "";
        investigationHud.SetPrompt(prompt);
        if (Keyboard.current == null || !Keyboard.current.eKey.wasPressedThisFrame) return;
        if (target != null && InteractionInput.TryConsume()) Operate(target);
        else if (lookingAtDoor && InteractionInput.TryConsume()) controlDoor.Toggle();
    }

    private FinalLabSwitch FindControl()
    {
        var eye = view.transform;
        var best = default(FinalLabSwitch);
        float score = float.NegativeInfinity;
        foreach (var item in switches)
        {
            if (item.Engaged || !Available(item.Kind)) continue;
            Vector3 delta = item.Point - eye.position;
            float distance = delta.magnitude;
            if (distance > 2.55f || distance < .01f) continue;
            float alignment = Vector3.Dot(eye.forward, delta / distance);
            float minimum = distance < 1.05f && step <= Step.Isolation ? .68f : step <= Step.Isolation ? .91f : .978f;
            if (alignment < minimum) continue;
            if (Physics.Raycast(eye.position, delta / distance, Mathf.Max(0, distance - .025f), ~0, QueryTriggerInteraction.Ignore))
                continue;
            float rating = alignment - distance * .023f;
            if (rating <= score) continue;
            score = rating;
            best = item;
        }
        return best;
    }

    private bool Available(FinalLabSwitch.Function kind) => step switch
    {
        Step.Reserve => kind == FinalLabSwitch.Function.Reserve,
        Step.Isolation => kind == FinalLabSwitch.Function.Isolation,
        Step.Traction => kind == FinalLabSwitch.Function.Traction,
        Step.Mode => kind == FinalLabSwitch.Function.Mode,
        Step.Contour => kind == FinalLabSwitch.Function.Contour,
        Step.Overload => kind == FinalLabSwitch.Function.Cover ||
            kind == FinalLabSwitch.Function.Overload && HasOpenedCover(),
        _ => false
    };

    private bool HasOpenedCover()
    {
        foreach (var item in switches)
            if (item.Kind == FinalLabSwitch.Function.Cover) return item.Engaged;
        return false;
    }

    private void Focus(FinalLabSwitch target)
    {
        if (focused == target) return;
        if (focused != null) focused.Highlight(false);
        focused = target;
        if (focused != null) focused.Highlight(true);
    }

    private void Operate(FinalLabSwitch control)
    {
        if (control.Engaged || !Available(control.Kind)) return;
        control.Engage();
        Focus(null);
        switch (control.Kind)
        {
            case FinalLabSwitch.Function.Reserve:
                reserves++;
                if (reserves <= reserveLights.Length) reserveLights[reserves - 1].enabled = true;
                Goal($"Восстановить резервное питание   {reserves}/3");
                if (reserves == 1) Say("Есть! Но одной линии недостаточно. Где-то должны быть остальные.");
                if (reserves != 3) return;
                step = Step.Isolation;
                Goal("Изолировать комнату отца   0/2");
                Report("Резервное питание восстановлено");
                Say("Питание вернулось. Но дверь всё ещё заблокирована… Значит, дело в изоляции камеры.",
                    "Система изоляции не даёт открыть дверь. Кажется, у неё два независимых контура. Проверь их, Алёша.");
                break;
            case FinalLabSwitch.Function.Isolation:
                isolated++;
                Goal($"Изолировать комнату отца   {isolated}/2");
                if (isolated != 2) return;
                step = Step.FatherDoor;
                Set(feedback, "");
                Goal("Подойдите к комнате отца");
                investigation.enabled = true;
                investigationHud.SetLaboratoryGameplay(true);
                Say("Получилось! Дверь разблокирована. Иди к отцу… Только будь осторожен.");
                flow.OnGameplayCompleted();
                break;
            case FinalLabSwitch.Function.Traction:
                FinalLabSwitch.SetColor(powerLens, new Color(.13f, .17f, .15f));
                current = 7.6f;
                SetTelemetry();
                step = Step.Mode;
                controlDoor.SetLocked(false);
                Goal("Проверить режим резонатора К-17");
                Report("Тяговая линия отключена. К-17 на резерве");
                Say("Основной режим не позволяет разорвать связь. Возможно, существует другой способ работы установки.");
                break;
            case FinalLabSwitch.Function.Mode:
                step = Step.Contour;
                Goal("Стабилизировать контур");
                Report("Режим Б активирован. Параметры изменились");
                Say("Режим Б активирован. Параметры изменились.",
                    "Контур неустойчив. Нужно удержать систему в рабочем состоянии.");
                break;
            case FinalLabSwitch.Function.Contour:
                step = Step.Stabilizing;
                Goal("Удерживай E у включённого рубильника");
                Say("Ток падает… Изоляция растёт.");
                SetArcState(true);
                break;
            case FinalLabSwitch.Function.Cover:
                Report("Аварийная кнопка доступна");
                break;
            case FinalLabSwitch.Function.Overload:
                StartCoroutine(Discharge());
                break;
        }
    }

    private void UpdateIsolation()
    {
        FinalLabSwitch handle = null;
        foreach (var item in switches)
            if (item.Kind == FinalLabSwitch.Function.Contour) handle = item;
        if (handle == null) return;
        Vector3 delta = handle.Point - view.transform.position;
        bool near = delta.sqrMagnitude < 2.15f * 2.15f && Vector3.Dot(delta.normalized, view.transform.forward) > .85f;
        bool holding = near && Keyboard.current != null && Keyboard.current.eKey.isPressed &&
            !Physics.Raycast(view.transform.position, delta.normalized, Mathf.Max(0, delta.magnitude - .025f),
                ~0, QueryTriggerInteraction.Ignore);
        if (holding) isolation = Mathf.MoveTowards(isolation, 100f, Time.unscaledDeltaTime * 19f);
        current = Mathf.Lerp(7.6f, 1.2f, isolation / 100);
        SetTelemetry();
        DrawArcs(.05f + isolation * .003f, holding ? 1 : .3f);
        FinalLabSwitch.SetColor(contourLens, Color.Lerp(new Color(.93f, .44f, .12f), new Color(.28f, .8f, .48f), isolation / 100));
        if (isolation < 100) return;
        step = Step.Overload;
        Goal("Запустить аварийную перегрузку К-17");
        Report("Контур стабилен");
        Say("Изоляция завершена. Но сущность ещё здесь…");
    }

    private IEnumerator Discharge()
    {
        step = Step.Discharge;
        blocked = true;
        player.enabled = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        messages.Clear();
        subtitles.Hide();
        investigationHud.SetPrompt("");
        Goal("Аварийная перегрузка");
        Report("СЕРДЕЧНИК НЕСТАБИЛЕН");
        SetArcState(true);
        const float duration = 3f;
        for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
        {
            float strength = t < 1.5f ? t / 1.5f : 1 - (t - 1.5f) / 1.5f;
            current = Mathf.Lerp(1.2f, 19.8f, strength);
            SetTelemetry();
            DrawArcs(.18f + strength * .64f, strength);
            for (int i = 0; i < pulseLights.Length; i++)
                pulseLights[i].intensity = pulseStrength[i] * (.7f + strength * Mathf.Abs(Mathf.Sin(t * 29f + i)) * 1.4f);
            yield return null;
        }
        SetArcState(false);
        for (int i = 0; i < pulseLights.Length; i++) pulseLights[i].intensity = pulseStrength[i];
        if (pulseLights.Length > 0) pulseLights[0].enabled = false;
        current = 0;
        isolation = 0;
        SetTelemetry();
        FinalLabSwitch.SetColor(contourLens, new Color(.11f, .13f, .12f));
        Goal("К-17 отключён");
        Report("Связь разорвана. Сущность уничтожена");
        yield return new WaitForSecondsRealtime(1.25f);
        yield return FinalLabAlexeiRescue.Fade(blackout, 1, .55f);
        StopGameplayHud();
        investigationHud.SetCinematic(true);
        flow.OnIvanGameplayCompleted();
    }

    private void SetArcState(bool visible)
    {
        foreach (var arc in arcs) arc.enabled = visible;
    }

    private void DrawArcs(float spread, float strength)
    {
        float now = Time.unscaledTime;
        for (int i = 0; i < arcs.Length; i++)
        {
            var arc = arcs[i];
            var from = alexeiHead.position + Vector3.up * .13f;
            if (i > 0) from = resonatorCore.position + new Vector3(i == 1 ? -.5f : .5f, -.46f, -.22f);
            for (int n = 0; n < arc.positionCount; n++)
            {
                float distance = n / (float)(arc.positionCount - 1);
                Vector3 p = Vector3.Lerp(from, resonatorCore.position, distance);
                float bend = Mathf.Sin(distance * Mathf.PI) * spread;
                p += new Vector3(Mathf.Sin(now * 22 + n * 2.2f + i) * bend, bend * 1.05f,
                    Mathf.Cos(now * 16 + n * 3.1f) * bend * .6f);
                arc.SetPosition(n, p);
            }
            arc.widthMultiplier = .012f + strength * .025f;
        }
    }

    private void SetTelemetry()
    {
        Set(telemetry, $"ТОК  {current:0.0} A    ·    ИЗОЛЯЦИЯ  {isolation:0}%");
        needle.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(52, -64, Mathf.Clamp01(current / 12)));
        for (int i = 0; i < segments.Length; i++)
            FinalLabSwitch.SetColor(segments[i], isolation >= (i + 1) * 10
                ? new Color(.26f, .77f, .45f) : new Color(.13f, .17f, .16f));
    }

    private void Say(params string[] lines)
    {
        foreach (string line in lines) messages.Enqueue(line);
    }

    private void PresentSpeech()
    {
        if (Time.unscaledTime < nextLine) return;
        if (subtitles.Visible) subtitles.Hide();
        if (messages.Count == 0) return;
        string line = messages.Dequeue();
        subtitles.Show(-1, "ИВАН", line);
        nextLine = Time.unscaledTime + Mathf.Clamp(1.7f + line.Length * .037f, 3.2f, 6.6f);
    }

    private void Goal(string line) => Set(objective, line);

    private void Report(string message)
    {
        Set(feedback, message);
        feedbackUntil = Time.unscaledTime + 3.6f;
    }

    private void StopGameplayHud()
    {
        blocked = false;
        step = Step.Inactive;
        Focus(null);
        messages.Clear();
        nextLine = 0;
        subtitles.Hide();
        continueHint.enabled = true;
        investigationHud.SetLaboratoryGameplay(false);
        investigationHud.SetPrompt("");
        Set(objective, "");
        Set(telemetry, "");
        Set(feedback, "");
    }

    private static void Set(TMP_Text label, string value)
    {
        if (label.text != value) label.text = value;
        var backing = label.transform.parent.gameObject;
        bool visible = value.Length > 0;
        if (backing.activeSelf != visible) backing.SetActive(visible);
    }
}
