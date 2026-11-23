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
    [SerializeField] private Transform ivanSpawn, ivanView;
    [SerializeField] private FinalLabSwitch[] switches;
    [SerializeField] private Shader outlineShader;
    [SerializeField] private TMP_Text objective, telemetry, feedback, crouchHint, continueHint;
    [SerializeField] private Transform needle, contactA, contactB, sparkContact;
    [SerializeField] private Renderer powerLens, contourLens;
    [SerializeField] private Renderer[] segments;
    [SerializeField] private Light[] reserveLights, pulseLights;
    [SerializeField] private LineRenderer[] arcs;

    private Step step;
    private FinalLabIvanDialogue subtitles;
    private Material outlineMaterial, focusMaterial;
    private CanvasGroup blackout;
    private readonly Queue<string> messages = new();
    private float nextLine, feedbackUntil, isolation, current = 9.4f;
    private int reserves, isolated;
    private bool blocked;
    private bool tractionNoted;
    private float[] pulseStrength;
    private float displayedCurrent, voltage, instability, modeFlash, nextArc, arcEnds, nextTelemetry;
    private int displayedSegments = -1;

    private void Awake()
    {
        subtitles = flow.Father.Subtitles;
        outlineMaterial = MakeOutline(.006f, new Color32(255, 213, 0, 255));
        focusMaterial = MakeOutline(.012f, new Color32(255, 233, 61, 255));
        foreach (var item in switches) item.Prepare(outlineMaterial, focusMaterial);
        pulseStrength = new float[pulseLights.Length];
        for (int i = 0; i < pulseLights.Length; i++) pulseStrength[i] = pulseLights[i].intensity;
    }

    private Material MakeOutline(float width, Color color)
    {
        var material = new Material(outlineShader);
        material.SetColor("_Color", color);
        material.SetFloat("_Width", width);
        return material;
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
        foreach (var item in switches)
            if (item != null) item.Highlight(0);
    }

    private void OnDestroy()
    {
        if (outlineMaterial != null) Destroy(outlineMaterial);
        if (focusMaterial != null) Destroy(focusMaterial);
    }

    private void StartAlexei()
    {
        UseGameplayHud();
        step = Step.Reserve;
        Goal("Резервное питание: 0/3");
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
        ivanActor.SetActive(false);
        player.Teleport(ivanSpawn.position);
        view.transform.localPosition = new Vector3(0, 1.65f, 0);
        player.SetViewDirection(ivanView.position - view.transform.position);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        yield return FinalLabAlexeiRescue.Fade(blackout, 0, 1.15f);
        player.enabled = true;
        Set(crouchHint, "Присесть — Ctrl");
        blocked = false;
        Goal("Исследовать питание установки");
        displayedCurrent = current;
        Say("Питание установки нестабильно. Похоже, сущность использует общую энергетическую сеть.");
    }

    private void UseGameplayHud()
    {
        investigation.enabled = false;
        investigationHud.SetLaboratoryGameplay(true);
        Set(crouchHint, player.enabled ? "Присесть — Ctrl" : "");
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
        if (step >= Step.Traction) UpdateResonator();
        if (!player.enabled) return;

        var target = FindControl();
        Focus(target);
        if (target != null && target.Kind == FinalLabSwitch.Function.Traction && !tractionNoted)
        {
            tractionNoted = true;
            Say("Тяговая линия продолжает подавать питание…");
        }

        bool lookingAtDoor = target == null && step >= Step.Mode &&
            investigation.NearestHit(new Ray(view.transform.position, view.transform.forward), FinalLabMetalDoor.UseDistance,
                out var hit) && hit.collider.GetComponentInParent<FinalLabMetalDoor>() == controlDoor;
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
            float minimum = distance < 1.05f && step <= Step.Isolation ? .68f : step <= Step.Isolation ? .91f : .96f;
            if (alignment < minimum) continue;
            if (!VisibleFrom(eye.position, item, delta, distance))
                continue;
            float rating = alignment - distance * .023f;
            if (rating <= score) continue;
            score = rating;
            best = item;
        }
        return best;
    }

    private bool VisibleFrom(Vector3 eye, FinalLabSwitch item, Vector3 delta, float distance)
    {
        return distance > .025f && (!investigation.NearestHit(new Ray(eye, delta / distance), distance - .025f,
            out var hit, QueryTriggerInteraction.Ignore) || item.Owns(hit.collider));
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
        Vector3 eye = view.transform.position;
        foreach (var item in switches)
        {
            Vector3 delta = item.Point - eye;
            float distance = delta.magnitude;
            bool visible = !blocked && !item.Engaged && Available(item.Kind) &&
                distance > .025f && distance < 5.5f && VisibleFrom(eye, item, delta, distance);
            item.Highlight(item == target ? 2 : visible ? 1 : 0);
        }
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
                Goal($"Резервное питание: {reserves}/3");
                if (reserves == 1) Say("Есть! Но одной линии недостаточно. Где-то должны быть остальные.");
                if (reserves != 3) return;
                step = Step.Isolation;
                Goal("Изоляция: 0/2");
                Report("Резервное питание: 3/3");
                ReplaceSpeech("Питание вернулось. Но дверь всё ещё заблокирована… Значит, дело в изоляции камеры.",
                    "Система изоляции не даёт открыть дверь. Кажется, у неё два независимых контура. Проверь их, Алёша.");
                break;
            case FinalLabSwitch.Function.Isolation:
                isolated++;
                Goal($"Изоляция: {isolated}/2");
                if (isolated != 2) return;
                step = Step.FatherDoor;
                Focus(null);
                Goal("Подойдите к комнате отца");
                Report("Изоляция: 2/2");
                investigation.enabled = true;
                investigationHud.SetLaboratoryGameplay(true);
                ReplaceSpeech("Получилось! Дверь разблокирована. Иди к отцу… Только будь осторожен.");
                flow.OnGameplayCompleted();
                break;
            case FinalLabSwitch.Function.Traction:
                FinalLabSwitch.SetColor(powerLens, new Color(.13f, .17f, .15f));
                current = 7.6f;
                step = Step.Mode;
                controlDoor.SetLocked(false);
                Goal("Проверить режим резонатора К-17");
                Report("Тяговая линия отключена. К-17 на резерве");
                ReplaceSpeech("Основной режим не позволяет разорвать связь. Возможно, существует другой способ работы установки.");
                break;
            case FinalLabSwitch.Function.Mode:
                step = Step.Contour;
                modeFlash = Time.unscaledTime + .85f;
                Goal("Стабилизировать контур");
                Report("Режим Б активирован. Параметры изменились");
                ReplaceSpeech("Режим Б активирован. Параметры изменились.",
                    "Контур неустойчив. Нужно удержать систему в рабочем состоянии.");
                break;
            case FinalLabSwitch.Function.Contour:
                step = Step.Stabilizing;
                Goal("Удерживай E у включённого рубильника");
                ReplaceSpeech("Ток падает… Изоляция растёт.");
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
            VisibleFrom(view.transform.position, handle, delta, delta.magnitude);
        if (holding) isolation = Mathf.MoveTowards(isolation, 100f, Time.unscaledDeltaTime * 19f);
        current = Mathf.Lerp(7.6f, 1.2f, isolation / 100);
        FinalLabSwitch.SetColor(contourLens, Color.Lerp(new Color(.93f, .44f, .12f), new Color(.28f, .8f, .48f), isolation / 100));
        if (isolation < 100) return;
        step = Step.Overload;
        Goal("Запустить аварийную перегрузку К-17");
        Report("Контур стабилен");
        ReplaceSpeech("Изоляция завершена. Но сущность ещё здесь…");
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
        Set(crouchHint, "");
        const float duration = 4.2f;
        for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
        {
            float peak = Mathf.SmoothStep(0f, 1f, t / 2.35f) *
                         (1f - Mathf.SmoothStep(0f, 1f, (t - 2.55f) / 1.65f));
            current = (1.2f + 18.6f * peak) * (1f - Mathf.SmoothStep(0f, 1f, (t - 3.4f) / .8f));
            UpdateResonator(peak);
            yield return null;
        }
        foreach (var arc in arcs) arc.enabled = false;
        foreach (var light in pulseLights) light.intensity = 0f;
        current = displayedCurrent = 0;
        isolation = 0;
        needle.localRotation = Quaternion.Euler(0, 0, 52f);
        SetTelemetry();
        FinalLabSwitch.SetColor(powerLens, new Color(.10f, .12f, .11f));
        FinalLabSwitch.SetColor(contourLens, new Color(.11f, .13f, .12f));
        Goal("К-17 отключён");
        Report("Связь разорвана. Сущность уничтожена");
        yield return new WaitForSecondsRealtime(1.25f);
        yield return FinalLabAlexeiRescue.Fade(blackout, 1, .55f);
        StopGameplayHud();
        investigationHud.SetCinematic(true);
        flow.OnIvanGameplayCompleted();
    }

    private void UpdateResonator(float overload = 0f)
    {
        float now = Time.unscaledTime;
        float dt = Time.unscaledDeltaTime;
        float progress = isolation / 100f;
        float burst = Mathf.Clamp01((modeFlash - now) / .85f);
        float targetVoltage = step switch
        {
            Step.Traction => .43f,
            Step.Mode => .25f,
            Step.Discharge => overload,
            _ => Mathf.Lerp(.56f, .09f, progress) + burst * .36f
        };
        float targetInstability = step == Step.Discharge ? overload :
            step >= Step.Contour ? Mathf.Lerp(.57f, .1f, progress) + burst * .34f : .22f;
        voltage = Mathf.MoveTowards(voltage, targetVoltage, dt * 2.1f);
        instability = Mathf.MoveTowards(instability, targetInstability, dt * 2.8f);
        float wobble = Mathf.Sin(now * 8.7f) * instability * .38f;
        displayedCurrent = Mathf.Lerp(displayedCurrent, Mathf.Max(0, current + wobble), 1f - Mathf.Exp(-8f * dt));
        needle.localRotation = Quaternion.Euler(0, 0,
            Mathf.Lerp(52, -64, Mathf.Clamp01(displayedCurrent / 20f)));
        if (now >= nextTelemetry)
        {
            SetTelemetry();
            nextTelemetry = now + .085f;
        }

        if (now >= nextArc && voltage > .07f)
        {
            nextArc = now + Mathf.Lerp(.53f, .064f, voltage) * Random.Range(.73f, 1.28f);
            arcEnds = now + Mathf.Min(nextArc - now, Mathf.Lerp(.05f, .095f, voltage));
            DrawArcs();
        }
        if (now >= arcEnds)
            foreach (var arc in arcs) arc.enabled = false;

        float flash = Mathf.Clamp01((arcEnds - now) / .06f) * voltage;
        for (int i = 0; i < pulseLights.Length; i++)
        {
            float standby = step == Step.Traction ? 1f : .42f;
            if (step == Step.Discharge) standby = overload;
            pulseLights[i].intensity = pulseStrength[i] * (standby + flash * 1.3f);
        }
    }

    private void DrawArcs()
    {
        Vector3 from = contactA.position, to = contactB.position;
        DrawArc(arcs[0], from, to, .021f + instability * .067f,
            .0045f + Mathf.Clamp01(displayedCurrent / 20f) * .011f, new Color(.77f, .94f, 1f));
        var halo = arcs[1];
        for (int i = 0; i < halo.positionCount; i++) halo.SetPosition(i, arcs[0].GetPosition(i));
        StyleArc(halo, arcs[0].widthMultiplier * 2.7f, new Color(.17f, .52f, 1f, .42f));
        for (int i = 2; i < arcs.Length; i++)
        {
            bool show = (i != 4 || step == Step.Discharge) &&
                Random.value < instability * (i == 2 ? 1.45f : .77f);
            if (!show) { arcs[i].enabled = false; continue; }
            var origin = i == 2 ? arcs[0].GetPosition(Random.Range(3, 13)) : sparkContact.position;
            Vector3 end = origin + new Vector3(Random.Range(-.24f, .24f), Random.Range(.043f, .21f), .083f);
            DrawArc(arcs[i], origin, end, .04f, .004f + voltage * .004f,
                i == 4 ? new Color(.63f, .45f, 1f) : new Color(.47f, .82f, 1f));
        }
    }

    private void DrawArc(LineRenderer arc, Vector3 from, Vector3 to, float spread, float width, Color tint)
    {
        for (int i = 0; i < arc.positionCount; i++)
        {
            float t = i / (float)(arc.positionCount - 1);
            arc.SetPosition(i, Vector3.Lerp(from, to, t) +
                Random.insideUnitSphere * (Mathf.Sin(t * Mathf.PI) * spread));
        }
        StyleArc(arc, width, tint);
    }

    private void StyleArc(LineRenderer arc, float width, Color tint)
    {
        tint.a *= Mathf.Lerp(.62f, 1f, voltage);
        arc.widthMultiplier = width * Random.Range(.83f, 1.16f);
        arc.startColor = tint;
        arc.endColor = tint;
        arc.enabled = true;
    }

    private void SetTelemetry()
    {
        Set(telemetry, $"Ток: {displayedCurrent:0.0} А    ·    Изоляция: {Mathf.FloorToInt(isolation)}%");
        int activeSegments = Mathf.FloorToInt(isolation / 10f);
        if (activeSegments == displayedSegments) return;
        displayedSegments = activeSegments;
        for (int i = 0; i < segments.Length; i++)
            FinalLabSwitch.SetColor(segments[i], i < activeSegments
                ? new Color(.26f, .77f, .45f) : new Color(.13f, .17f, .16f));
    }

    private void Say(params string[] lines)
    {
        foreach (string line in lines) messages.Enqueue(line);
    }

    private void ReplaceSpeech(params string[] lines)
    {
        messages.Clear();
        nextLine = 0;
        Say(lines);
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
        Set(crouchHint, "");
    }

    private static void Set(TMP_Text label, string value)
    {
        if (label.text != value) label.text = value;
        var backing = label.transform.parent.gameObject;
        bool visible = value.Length > 0;
        if (backing.activeSelf != visible) backing.SetActive(visible);
    }
}
