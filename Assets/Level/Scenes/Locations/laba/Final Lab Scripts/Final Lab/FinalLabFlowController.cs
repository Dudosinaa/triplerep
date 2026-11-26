using System;
using FinalLab.Investigation;
using UnityEngine;

public sealed class FinalLabFlowController : MonoBehaviour
{
    public enum Stage { IvanDialogue, GameplayPhase, FatherDoorReady, FatherDoorOpening, FatherIntroduction, FatherDialogue, FatherDeath, FatherSceneFinished, IvanGameplayPhase, AlexeiRescue }

    [SerializeField] private bool skipGameplayForTesting;
    [SerializeField] private FinalLabIvanCutsceneController ivanDialogue;
    [SerializeField] private FinalLabMetalDoor fatherDoor;
    [SerializeField] private FatherCutsceneController father;
    [SerializeField] private FinalLabInvestigationUI hud;
    [SerializeField] private GameObject doorMarker;
    [SerializeField] private bool skipIvanGameplayForTesting;
    [SerializeField] private FinalLabAlexeiRescue rescue;

    public Stage State { get; private set; }
    public bool SkipGameplayForTesting { get => skipGameplayForTesting; set => skipGameplayForTesting = value; }
    public FinalLabMetalDoor FatherDoor => fatherDoor;
    public FatherCutsceneController Father => father;
    internal FinalLabAudio Audio { get; private set; }
    public bool DoorMarkerVisible => doorMarker.activeSelf;
    public event Action GameplayPhaseStarted;
    public event Action FatherSequenceStarted;
    public event Action FatherSequenceFinished;
    public event Action IvanGameplayPhaseStarted;
    public bool SkipIvanGameplayForTesting { get => skipIvanGameplayForTesting; set => skipIvanGameplayForTesting = value; }

    public void Configure(FinalLabIvanCutsceneController ivan, FinalLabMetalDoor door, FatherCutsceneController sequence,
        FinalLabInvestigationUI ui, GameObject marker)
    {
        ivanDialogue = ivan;
        fatherDoor = door;
        father = sequence;
        hud = ui;
        doorMarker = marker;
    }

    private void Awake()
    {
        Audio = GetComponent<FinalLabAudio>();
        fatherDoor.SetLocked(true);
        fatherDoor.OpenPrompt = "Открыть дверь — E";
        doorMarker.SetActive(false);
    }

    private void OnEnable()
    {
        ivanDialogue.Finished += OnIvanDialogueFinished;
        fatherDoor.OpeningRequestedByPlayer += StartFatherSequence;
    }

    private void OnDisable()
    {
        if (ivanDialogue != null) ivanDialogue.Finished -= OnIvanDialogueFinished;
        if (fatherDoor != null) fatherDoor.OpeningRequestedByPlayer -= StartFatherSequence;
    }

    public void OnIvanDialogueFinished() => StartGameplayPhase();

    public void StartGameplayPhase()
    {
        if (State != Stage.IvanDialogue) return;
        State = Stage.GameplayPhase;
        if (skipGameplayForTesting) OnGameplayCompleted();
        else GameplayPhaseStarted?.Invoke();
    }

    public void OnGameplayCompleted() => EnableFatherDoor();

    public void EnableFatherDoor()
    {
        if (State != Stage.GameplayPhase) return;
        SetDoorReady();
    }

    public void StartFatherSequence()
    {
        if (State != Stage.FatherDoorReady) return;
        State = Stage.FatherDoorOpening;
        doorMarker.SetActive(false);
        hud.ClearHint();
        hud.SetPrompt("");
        FatherSequenceStarted?.Invoke();
        father.Begin(this);
    }

    public void OnFatherDoorOpened()
    {
        if (State == Stage.FatherDoorOpening) State = Stage.FatherIntroduction;
    }

    internal void OnFatherDialogueStarted() => State = Stage.FatherDialogue;
    internal void OnFatherDeathStarted() => State = Stage.FatherDeath;

    public void OnFatherSequenceFinished()
    {
        if (State != Stage.FatherDeath) return;
        State = Stage.FatherSceneFinished;
        FatherSequenceFinished?.Invoke();
        if (rescue == null) return;
        State = Stage.IvanGameplayPhase;
        if (skipIvanGameplayForTesting) OnIvanGameplayCompleted();
        else IvanGameplayPhaseStarted?.Invoke();
    }

    public void OnIvanGameplayCompleted()
    {
        if (State != Stage.IvanGameplayPhase) return;
        State = Stage.AlexeiRescue;
        rescue.Begin(father);
    }

    internal void RetryFatherDoor()
    {
        if (State == Stage.FatherDoorOpening) SetDoorReady();
    }

    private void SetDoorReady()
    {
        father.PrepareOnBed();
        State = Stage.FatherDoorReady;
        fatherDoor.SetLocked(false);
        doorMarker.SetActive(true);
    }
}
