using System;
using System.Collections;
using System.Collections.Generic;
using FinalLab.Investigation;
using TMPro;
using UnityEngine;

public sealed class FatherCutsceneController : MonoBehaviour
{
    public const string InitialBedPose = "InitialBedPose", SitUp = "SitUp", StandUp = "StandUp",
        Surprised = "Surprised", StandingIdle = "StandingIdle", Death = "Death";
    public const float DeathBlend = .57f;
    public const float WakeSpeed = 1.24f, RiseSpeed = 1.19f;

    [Serializable]
    public sealed class Line
    {
        public string speaker;
        [TextArea] public string text;
        public Line(string speaker, string text)
        {
            this.speaker = speaker;
            this.text = text;
        }
    }

    [SerializeField] private FirstPersonPlayer player;
    [SerializeField] private Camera view;
    [SerializeField] private FinalLabInvestigationManager interactions;
    [SerializeField] private FinalLabInvestigationUI hud;
    [SerializeField] private FinalLabIvanDialogue subtitles;
    [SerializeField] private TMP_Text clickHint;
    [SerializeField] private Animator father;
    [SerializeField] private Transform head, chest, hips;
    [SerializeField] private Transform hallEye, roomEye, conversationEye;
    [SerializeField] private CanvasGroup blackout;
    [SerializeField] private Line[] lines;

    private readonly List<Behaviour> suspended = new();
    private readonly RaycastHit[] cameraHits = new RaycastHit[24];
    private FinalLabIvanCameraController storedCamera;
    private CursorLockMode storedCursor;
    private bool storedCursorVisibility, finished;
    private string storedClickHint;
    private float headDip, imbalance;
    public Animator Actor => father;
    public FinalLabIvanDialogue Subtitles => subtitles;
    public IReadOnlyList<Line> Lines => lines;
    public float Blackout => blackout.alpha;
    private Vector3 BodyEye => conversationEye.position - conversationEye.forward * .36f + Vector3.down * .45f;

    public void Configure(FirstPersonPlayer alexei, Camera camera, FinalLabInvestigationManager manager, FinalLabInvestigationUI ui,
        FinalLabIvanDialogue panel, TMP_Text hint, Animator model, Transform skull, Transform spine, Transform pelvis,
        Transform entrance, Transform inside, Transform nearFather, CanvasGroup fade, Line[] script)
    {
        player = alexei;
        view = camera;
        interactions = manager;
        hud = ui;
        subtitles = panel;
        clickHint = hint;
        father = model;
        head = skull;
        chest = spine;
        hips = pelvis;
        hallEye = entrance;
        roomEye = inside;
        conversationEye = nearFather;
        blackout = fade;
        lines = script;
    }

    public void Begin(FinalLabFlowController flow)
    {
        if (storedCamera != null || finished) return;
        LockControls();
        StartCoroutine(Sequence(flow));
    }

    private IEnumerator Sequence(FinalLabFlowController flow)
    {
        var door = flow.FatherDoor;
        bool revealed = false;
        for (float time = 0; !door.IsOpen && time < door.TravelTime + 2f; time += Time.deltaTime)
        {
            if (!revealed && door.Openness > .005f)
            {
                father.gameObject.SetActive(true);
                father.Play("Base Layer." + InitialBedPose, 0, 0);
                father.Update(0f);
                revealed = true;
            }
            float through = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.30f, .88f, door.Openness));
            Vector3 approach = Vector3.Lerp(hallEye.position, roomEye.position, through);
            Track(approach, Focus(.76f), 60f);
            yield return null;
        }
        if (!door.IsOpen)
        {
            door.CloseDoor();
            father.gameObject.SetActive(false);
            RestoreControls();
            flow.RetryFatherDoor();
            yield break;
        }

        flow.OnFatherDoorOpened();
        yield return Hold(.29f, roomEye.position, bedFrame: true);
        yield return Animate(SitUp, .24f, roomEye.position);
        yield return Caption(0, roomEye.position);
        yield return Animate(StandUp, .43f, roomEye.position);

        Play(Surprised, .30f);
        Show(1);
        yield return WaitForMotion(Surprised, conversationEye.position, allowContinue: true);
        Play(StandingIdle, .46f);
        yield return WaitForContinue(conversationEye.position);
        subtitles.Hide();

        flow.OnFatherDialogueStarted();
        for (int line = 2; line < 8; line++)
        {
            if (line == 3) headDip = 7f;
            if (line == 6) yield return Caption(line, conversationEye.position + conversationEye.forward * .16f);
            else yield return Caption(line, conversationEye.position);
            headDip = 0f;
            if (line == 5)
            {
                imbalance = 4.5f;
                yield return Hold(.55f, conversationEye.position);
                imbalance = 0f;
            }
        }

        yield return Hold(1.35f, conversationEye.position);
        flow.OnFatherDeathStarted();
        yield return Animate(Death, DeathBlend, conversationEye.position);
        father.speed = 0;
        var lowEye = BodyEye;
        yield return Caption(8, lowEye, down: true);
        yield return Hold(.62f, lowEye, down: true);
        yield return Caption(9, lowEye, down: true);
        yield return Hold(2.85f, lowEye, down: true);
        for (float t = 0; t < 2f; t += Time.deltaTime)
        {
            blackout.alpha = Mathf.SmoothStep(0, 1, t / 2f);
            yield return null;
        }
        blackout.alpha = 1f;
        finished = true;
        flow.OnFatherSequenceFinished();
    }

    private void Show(int index)
    {
        var line = lines[index];
        subtitles.Show(index, line.speaker, line.text);
    }

    private IEnumerator Caption(int index, Vector3 eye, bool down = false)
    {
        Show(index);
        yield return WaitForContinue(eye, down);
        subtitles.Hide();
    }

    private IEnumerator WaitForContinue(Vector3 eye, bool down = false)
    {
        while (!subtitles.ReadContinue())
        {
            Track(eye, Focus(down ? .48f : .88f), down ? 64f : 54f);
            yield return null;
        }
    }

    private void Play(string state, float blend) => father.CrossFadeInFixedTime("Base Layer." + state, blend, 0, 0);

    private IEnumerator Animate(string state, float blend, Vector3 eye)
    {
        Play(state, blend);
        yield return WaitForMotion(state, eye);
    }

    private IEnumerator WaitForMotion(string state, Vector3 eye, bool allowContinue = false)
    {
        string path = "Base Layer." + state;
        float timeout = 2.5f;
        for (float time = 0; time < timeout; time += Time.deltaTime)
        {
            var info = father.IsInTransition(0) ? father.GetNextAnimatorStateInfo(0) : father.GetCurrentAnimatorStateInfo(0);
            if (allowContinue) subtitles.ReadContinue();
            if (info.IsName(path))
            {
                timeout = Mathf.Max(timeout, info.length + 2f);
                if (info.normalizedTime >= 1f && !father.IsInTransition(0)) yield break;
            }
            if (state == Death)
            {
                float fall = info.IsName(path) ? Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.16f, .94f, info.normalizedTime)) : 0f;
                Track(Vector3.Lerp(eye, BodyEye, fall), Focus(Mathf.Lerp(.87f, .48f, fall)), Mathf.Lerp(54f, 64f, fall));
            }
            else if (state == SitUp)
            {
                float rise = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.16f, .89f, info.normalizedTime));
                Track(eye, Focus(Mathf.Lerp(.18f, .88f, rise)), Mathf.Lerp(62f, 54f, rise));
            }
            else if (state == StandUp)
            {
                float rise = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.12f, .87f, info.normalizedTime));
                Track(Vector3.Lerp(eye, conversationEye.position, rise), Focus(.88f), 54f);
            }
            else Track(eye, Focus(.88f), 54f);
            yield return null;
        }
        throw new InvalidOperationException("Father animation did not finish: " + state);
    }

    private Vector3 Focus(float headWeight) => Vector3.Lerp(hips.position, head.position, headWeight);

    private IEnumerator Hold(float seconds, Vector3 eye, bool down = false, bool bedFrame = false)
    {
        for (float time = 0; time < seconds; time += Time.deltaTime)
        {
            Track(eye, Focus(bedFrame ? .18f : down ? .48f : .88f), bedFrame ? 62f : down ? 64f : 54f);
            yield return null;
        }
    }

    private void Track(Vector3 eye, Vector3 target, float fov)
    {
        var camera = view.transform;
        float t = 1 - Mathf.Exp(-3.45f * Time.deltaTime);
        Vector3 travel = (eye - camera.position) * t;
        float length = travel.magnitude;
        if (length > .0001f)
        {
            int count = Physics.SphereCastNonAlloc(camera.position, .095f, travel / length, cameraHits, length,
                ~0, QueryTriggerInteraction.Ignore);
            float permitted = length;
            for (int i = 0; i < count; i++)
                if (!cameraHits[i].collider.transform.IsChildOf(player.transform)) permitted = Mathf.Min(permitted, Mathf.Max(0, cameraHits[i].distance - .025f));
            camera.position += travel * (permitted / length);
        }
        Vector3 direction = target - camera.position;
        if (direction.sqrMagnitude > .0001f)
            camera.rotation = Quaternion.Slerp(camera.rotation, Quaternion.LookRotation(direction, Vector3.up), t);
        view.fieldOfView = Mathf.Lerp(view.fieldOfView, fov, t);
    }

    private float actualDip, actualSway;

    private void LateUpdate()
    {
        if (storedCamera == null || !father.gameObject.activeSelf || father.speed == 0) return;
        actualDip = Mathf.MoveTowards(actualDip, headDip, 13 * Time.deltaTime);
        actualSway = Mathf.MoveTowards(actualSway, imbalance, 10 * Time.deltaTime);
        if (actualDip > 0) head.rotation = Quaternion.AngleAxis(actualDip, father.transform.right) * head.rotation;
        if (actualSway > 0) chest.rotation = Quaternion.AngleAxis(actualSway, father.transform.forward) * chest.rotation;
    }

    private void LockControls()
    {
        storedCamera = new FinalLabIvanCameraController(view);
        storedCursor = Cursor.lockState;
        storedCursorVisibility = Cursor.visible;
        Suspend(player);
        Suspend(interactions);
        foreach (var behaviour in player.GetComponentsInChildren<Behaviour>())
            if (behaviour is PistolController or SmokingController or PlayerStress or DocumentReader or ItemInspector or DialogueController)
                Suspend(behaviour);
        hud.SetCinematic(true);
        subtitles.Hide();
        storedClickHint = clickHint.text;
        clickHint.text = "ЛКМ — продолжить";
        clickHint.enabled = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Suspend(Behaviour behaviour)
    {
        if (!behaviour.enabled) return;
        behaviour.enabled = false;
        suspended.Add(behaviour);
    }

    internal CanvasGroup ReleaseViewForRescue()
    {
        storedCamera?.Restore();
        storedCamera = null;
        suspended.Clear();
        subtitles.Hide();
        return blackout;
    }

    private void OnDisable() => RestoreControls();

    private void RestoreControls()
    {
        if (storedCamera == null) return;
        if (subtitles != null) subtitles.Hide();
        if (clickHint != null) clickHint.text = storedClickHint;
        if (hud != null) hud.SetCinematic(false);
        storedCamera.Restore();
        storedCamera = null;
        foreach (var behaviour in suspended)
            if (behaviour != null) behaviour.enabled = true;
        suspended.Clear();
        Cursor.lockState = storedCursor;
        Cursor.visible = storedCursorVisibility;
    }
}
