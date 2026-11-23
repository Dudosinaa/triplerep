using System;
using System.Collections;
using System.Collections.Generic;
using FinalLab.Investigation;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(20)]
public sealed class AlexeiLabGameplay : MonoBehaviour
{
    [Serializable]
    public sealed class Panel
    {
        public Transform overview;
        public Collider face;
        public GameObject outline;
        public Collider[] targets;
        public Transform[] handles;
        public GameObject[] hover;
        public Renderer[] lamps;
        public TMP_Text status;
    }

    [SerializeField] private FinalLabFlowController flow;
    [SerializeField] private FinalLabInvestigationUI investigationHud;
    [SerializeField] private FirstPersonPlayer player;
    [SerializeField] private Panel power, isolation;
    [SerializeField] private TMP_Text objective, prompt, feedback, subtitle;
    [SerializeField] private Light chamberLight;
    [SerializeField] private AudioSource sounds, equipment;
    [SerializeField] private AudioClip click, error, startup;
    private readonly Queue<string> lines = new();
    private readonly RaycastHit[] hits = new RaycastHit[16];
    private DocumentInspectionCamera cameraMove;
    private Panel activePanel;
    private enum Stage { Waiting, Power, Isolation, FatherDoor }
    private Stage phase;
    private int step, hovered = -1;
    private bool moving, hinted, powerIntro, isolationIntro;
    private float subtitleUntil, feedbackUntil, hintAt;
    private static readonly Color Green = new(.36f, .78f, .48f);
    private static readonly Color Red = new(.84f, .23f, .16f);
    private static readonly Color Off = new(.12f, .15f, .14f);

    private void OnEnable()
    {
        flow.GameplayPhaseStarted += Begin;
        flow.FatherSequenceStarted += Finish;
    }

    private void OnDisable()
    {
        flow.GameplayPhaseStarted -= Begin;
        flow.FatherSequenceStarted -= Finish;
        if (activePanel != null)
        {
            cameraMove.Restore();
            player.enabled = true;
        }
    }

    private void Begin()
    {
        cameraMove = new DocumentInspectionCamera(player.PlayerCamera, player.transform);
        investigationHud.SetCinematic(true);
        if (player.TryGetComponent(out PistolController weapon))
        {
            weapon.SetEquipped(false);
            weapon.enabled = false;
        }
        phase = Stage.Power;
        power.outline.SetActive(true);
        Show(objective, "НОВАЯ ЗАДАЧА\nВосстановить резервное питание");
        Lamp(power, 0, Red);
        Lamp(isolation, 0, Red);
        if (chamberLight != null) chamberLight.intensity *= .32f;
        subtitleUntil = Time.unscaledTime + .65f;
        Speak("Алёша, слышишь меня? Я добрался до управления. Отсек отца всё ещё обесточен.",
              "Найди распределительный щит. Без питания мы даже дверь не откроем.");
    }

    private void Update()
    {
        if (phase == Stage.Waiting) return;
        if (Time.unscaledTime >= subtitleUntil)
        {
            Show(subtitle, lines.Count == 0 ? "" : "ИВАН   " + lines.Dequeue());
            subtitleUntil = Time.unscaledTime + 4.5f;
        }
        if (Time.unscaledTime >= feedbackUntil) Show(feedback, "");
        if (phase == Stage.FatherDoor || moving) return;

        var keys = Keyboard.current;
        var mouse = Mouse.current;
        if (activePanel != null)
        {
            if (phase == Stage.Power && !hinted && Time.unscaledTime >= hintAt)
            {
                hinted = true;
                Speak("Посмотри, куда уходит повреждённая линия. Резервный источник не должен быть связан с ней.");
            }
            int next = -1;
            if (mouse != null && Hit(player.PlayerCamera.ScreenPointToRay(mouse.position.ReadValue()), 3, out var area))
                next = Array.IndexOf(activePanel.targets, area);
            Hover(next);
            if (keys != null && keys.escapeKey.wasPressedThisFrame && InteractionInput.TryConsume())
                StartCoroutine(Return());
            else if (hovered >= 0 && mouse.leftButton.wasPressedThisFrame) Operate(hovered);
            return;
        }

        var panel = phase == Stage.Power ? power : isolation;
        var eye = player.PlayerCamera.transform;
        bool aimed = player.enabled && Hit(new Ray(eye.position, eye.forward), 2.35f, out var target)
            && (target == panel.face || Array.IndexOf(panel.targets, target) >= 0);
        Show(prompt, aimed ? phase == Stage.Power ? "Осмотреть распределительный щит — E" : "Осмотреть блок изоляции — E" : "");
        if (aimed && keys != null && keys.eKey.wasPressedThisFrame && InteractionInput.TryConsume())
            Inspect(panel);
    }

    private bool Hit(Ray ray, float distance, out Collider target)
    {
        target = null;
        var candidates = hits;
        int count = Physics.RaycastNonAlloc(ray, candidates, distance, ~0, QueryTriggerInteraction.Ignore);
        if (count == candidates.Length)
        {
            candidates = Physics.RaycastAll(ray, distance, ~0, QueryTriggerInteraction.Ignore);
            count = candidates.Length;
        }
        for (int i = 0; i < count; i++)
        {
            var hit = candidates[i];
            if (hit.distance >= distance || hit.collider.transform.IsChildOf(player.transform)) continue;
            distance = hit.distance;
            target = hit.collider;
        }
        return target != null;
    }

    private void Inspect(Panel panel)
    {
        if (!cameraMove.CanReach(cameraMove.Current.position, panel.overview.position))
        {
            Report("Подойди к панели спереди");
            return;
        }
        cameraMove.Remember();
        activePanel = panel;
        player.enabled = false;
        Show(prompt, "ЛКМ — переключить    Esc — выйти");
        if (phase == Stage.Power && !powerIntro)
        {
            powerIntro = true;
            hintAt = Time.unscaledTime + 28f;
            Speak("Основной контур повреждён. Похоже, система пытается запитаться через неисправную линию.");
        }
        if (phase == Stage.Isolation && !isolationIntro)
        {
            isolationIntro = true;
            Speak("Здесь два независимых механизма. Мне нужно, чтобы камера перешла в автономный режим.");
        }
        StartCoroutine(Move(new Pose(panel.overview.position, panel.overview.rotation), 51));
    }

    private IEnumerator Move(Pose pose, float fov)
    {
        moving = true;
        cameraMove.Move(pose, fov, .8f);
        while (!cameraMove.Advance(Time.unscaledDeltaTime, out _)) yield return null;
        moving = false;
    }

    private IEnumerator Return()
    {
        Hover(-1);
        Show(prompt, "");
        yield return Move(cameraMove.GameplayPose, cameraMove.GameplayFov);
        cameraMove.Restore();
        activePanel = null;
        player.enabled = true;
    }

    private void Hover(int index)
    {
        if (hovered == index) return;
        if (hovered >= 0) activePanel.hover[hovered].SetActive(false);
        hovered = index;
        if (hovered >= 0) activePanel.hover[hovered].SetActive(true);
    }

    private void Operate(int index)
    {
        if (index < step) return;
        if (index != step)
        {
            sounds.PlayOneShot(error);
            Report(phase == Stage.Isolation ? "ИЗОЛЯЦИЯ НЕВОЗМОЖНА: ВНЕШНИЙ КОНТУР АКТИВЕН" :
                index == 1 ? "ОШИБКА: ОСНОВНОЙ КОНТУР НЕ ИЗОЛИРОВАН" : "НЕДОСТАТОЧНО ПИТАНИЯ");
            StartCoroutine(Reject(activePanel.handles[index]));
            return;
        }
        var handle = activePanel.handles[index];
        handle.localRotation = Quaternion.Euler(index == 0 ? 28 : -28, 0, 0);
        Lamp(activePanel, index, index == 0 ? Off : Green);
        sounds.PlayOneShot(click);
        step++;
        if (phase == Stage.Isolation && step == 1)
        {
            if (equipment != null) equipment.volume *= .37f;
            Speak("Связь разорвана… Странно. Показания начинают падать.");
        }
        if (step == activePanel.targets.Length) StartCoroutine(CompletePanel());
    }

    private IEnumerator Reject(Transform handle)
    {
        moving = true;
        var rest = handle.localRotation;
        handle.localRotation = rest * Quaternion.Euler(13, 0, 0);
        yield return new WaitForSecondsRealtime(.16f);
        handle.localRotation = rest;
        moving = false;
    }

    private IEnumerator CompletePanel()
    {
        moving = true;
        bool first = phase == Stage.Power;
        activePanel.status.text = first ? "РЕЗЕРВНОЕ ПИТАНИЕ ВОССТАНОВЛЕНО" : "КАМЕРА ИЗОЛИРОВАНА";
        activePanel.status.color = Green;
        sounds.PlayOneShot(startup);
        if (first)
        {
            if (equipment != null) equipment.Play();
            if (chamberLight != null) chamberLight.intensity /= .32f;
            Speak("Есть! Питание пошло… Но дверь всё ещё не открывается.",
                  "Подожди. Камеру удерживает ещё одна система.",
                  "Алёша, найди блок изоляции. По моим показаниям, камера всё ещё связана с установкой.");
        }
        else Speak("Получилось. Камера отсоединена от установки.",
                   "Теперь попробуй открыть дверь. Я не знаю, что мы там увидим…");

        Show(objective, first ? "ЗАДАЧА ВЫПОЛНЕНА\nРезервное питание восстановлено" : "ЗАДАЧА ВЫПОЛНЕНА\nОтсек отца изолирован");
        activePanel.outline.SetActive(false);
        yield return new WaitForSecondsRealtime(1.15f);
        yield return Return();
        step = 0;
        phase = first ? Stage.Isolation : Stage.FatherDoor;
        if (first)
        {
            isolation.outline.SetActive(true);
            Show(objective, "НОВАЯ ЗАДАЧА\nИзолировать отсек отца");
        }
        else
        {
            Show(objective, "НОВАЯ ЗАДАЧА\nПодойдите к комнате отца");
            investigationHud.SetCinematic(false);
            investigationHud.SetResearchProgressVisible(false);
            flow.OnGameplayCompleted();
        }
    }

    private void Finish()
    {
        phase = Stage.Waiting;
        lines.Clear();
        Show(objective, "");
        Show(prompt, "");
        Show(feedback, "");
        Show(subtitle, "");
        if (equipment != null) equipment.Stop();
    }

    private static void Lamp(Panel panel, int index, Color color)
    {
        var values = new MaterialPropertyBlock();
        values.SetColor("_BaseColor", color);
        panel.lamps[index].SetPropertyBlock(values);
    }

    private void Report(string message)
    {
        Show(feedback, message);
        feedbackUntil = Time.unscaledTime + 3;
    }

    private void Speak(params string[] messages)
    {
        foreach (string message in messages) lines.Enqueue(message);
    }

    private static void Show(TMP_Text label, string message)
    {
        if (label.text == message) return;
        label.text = message;
        label.transform.parent.gameObject.SetActive(message.Length > 0);
    }
}
