using System;
using System.Collections;
using FinalLab.Investigation;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class FinalLabIvanCutsceneController : MonoBehaviour
{
    public const float DialogueEyeHeight = 1.65f;
    public enum Stage { Waiting, FramingDoor, Opening, Exiting, Talking, Turning, Returning, Closing, Finished }
    [SerializeField] private FinalLabInvestigationManager investigation;
    [SerializeField] private FinalLabInvestigationUI investigationUI;
    [SerializeField] private FirstPersonPlayer player;
    [SerializeField] private Camera gameplayCamera;
    [SerializeField] private FinalLabMetalDoor door;
    [SerializeField] private FinalLabIvanCharacterController ivan;
    [SerializeField] private FinalLabIvanDialogue dialogue;
    [SerializeField] private Transform inside, doorway, hall, talk, eye, fatherDoor, resonator;
    private FinalLabIvanCameraController cameraRig;
    private PistolController pistol;
    private bool pistolWasEnabled, playerWasEnabled, investigationWasEnabled;
    private CursorLockMode cursor;
    private bool cursorVisible;
    public Stage State { get; private set; }
    public FinalLabIvanCharacterController Ivan => ivan;
    public FinalLabIvanDialogue Dialogue => dialogue;
    public FinalLabMetalDoor Door => door;
    public event Action Finished;

    public void Configure(FinalLabInvestigationManager research, FinalLabInvestigationUI hud, FirstPersonPlayer motor, Camera camera,
        FinalLabMetalDoor controlDoor, FinalLabIvanCharacterController character, FinalLabIvanDialogue subtitles,
        Transform insidePoint, Transform threshold, Transform hallPoint, Transform talkPoint, Transform cameraPoint,
        Transform fatherRoomDoor, Transform k17)
    {
        investigation = research;
        investigationUI = hud;
        player = motor;
        gameplayCamera = camera;
        door = controlDoor;
        ivan = character;
        dialogue = subtitles;
        inside = insidePoint;
        doorway = threshold;
        hall = hallPoint;
        talk = talkPoint;
        eye = cameraPoint;
        fatherDoor = fatherRoomDoor;
        resonator = k17;
    }

    private void OnEnable() => investigation.IvanDeskClosedAfterInvestigation += Begin;

    private void OnDisable()
    {
        if (investigation != null) investigation.IvanDeskClosedAfterInvestigation -= Begin;
        StopAllCoroutines();
        RestoreControl();
    }

    private void Begin()
    {
        if (State != Stage.Waiting || investigation.Completed != 3 || investigation.ActivePoint != null) return;
        State = Stage.FramingDoor;
        StartCoroutine(Sequence());
    }

    private IEnumerator Sequence()
    {
        TakeControl();
        try
        {
            var doorFocus = doorway.position + Vector3.up * 1.13f;
            var arrivalEye = gameplayCamera.transform.position;
            yield return cameraRig.LookAtDoor(doorFocus);
            State = Stage.Opening;
            ivan.Reveal(doorway.position);
            if (!door.IsOpen && !door.OpenDoor()) throw new InvalidOperationException("The control-room door would not open for Ivan.");
            yield return WaitForDoor(open: true);
            State = Stage.Exiting;
            yield return ivan.WalkAround(hall.position, talk.position, () =>
            {
                float throughDoor = Mathf.InverseLerp(doorway.position.z + .25f, doorway.position.z - .55f, ivan.transform.position.z);
                Follow(Vector3.Lerp(arrivalEye, eye.position, Mathf.SmoothStep(0, 1, throughDoor)));
            });
            yield return FaceConversation();
            yield return cameraRig.FrameConversation(eye.position, ivan.HeadPosition - Vector3.up * .065f);
            State = Stage.Talking;
            yield return PresentDialogue();

            State = Stage.Turning;
            dialogue.Hide();
            ivan.ClearGlance();
            yield return new WaitForSeconds(.43f);
            yield return ivan.TurnTo(hall.position, () => Follow(eye.position));
            State = Stage.Returning;
            yield return ivan.WalkAround(hall.position, inside.position, () => Follow(eye.position), returning: true);
            State = Stage.Closing;
            door.CloseDoor();
            for (float elapsed = 0; door.IsMoving && elapsed < door.TravelTime + 1.6f; elapsed += Time.deltaTime)
            {
                cameraRig.Follow(eye.position, doorFocus, 58f, Time.deltaTime);
                yield return null;
            }
            if (door.IsMoving || door.IsOpen) throw new InvalidOperationException("The control-room door did not close after Ivan cleared it.");
            yield return cameraRig.Return();
        }
        finally
        {
            RestoreControl();
            State = Stage.Finished;
        }
        Finished?.Invoke();
    }

    private IEnumerator FaceConversation()
    {
        Quaternion start = ivan.transform.rotation;
        Quaternion facing = ivan.Direction(eye.position);
        for (float elapsed = 0; elapsed < .28f; elapsed += Time.deltaTime)
        {
            ivan.transform.rotation = Quaternion.Slerp(start, facing, Mathf.SmoothStep(0, 1, elapsed / .28f));
            yield return null;
        }
        ivan.transform.rotation = facing;
    }

    private IEnumerator PresentDialogue()
    {
        var lines = dialogue.ReadLines();
        for (int index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            ivan.ClearGlance();
            if (!string.IsNullOrEmpty(line.animation) && ivan.RequestedAnimation != line.animation)
            {
                dialogue.Hide();
                yield return ivan.BlendEmotion(line.animation);
            }
            if (line.pause > 0)
            {
                dialogue.Hide();
                float elapsed = 0;
                if (line.look == "ResonatorThenFather")
                {
                    ivan.GlanceAt(resonator.position + Vector3.up * 1.15f);
                    elapsed = .46f;
                    yield return new WaitForSeconds(elapsed);
                }
                if (line.look is "Father" or "ResonatorThenFather") ivan.GlanceAt(fatherDoor.position + Vector3.up * 1.18f);
                yield return new WaitForSeconds(line.pause - elapsed);
            }
            dialogue.Show(index, line);
            // A click during a pose change or held across the reveal cannot advance the new line.
            yield return null;
            while (Mouse.current != null && Mouse.current.leftButton.isPressed) yield return null;
            while (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) yield return null;
        }
    }

    private IEnumerator WaitForDoor(bool open)
    {
        for (float elapsed = 0; elapsed < door.TravelTime + 1.6f; elapsed += Time.deltaTime)
        {
            if (!door.IsMoving && door.IsOpen == open) yield break;
            yield return null;
        }
        throw new InvalidOperationException("The control-room door did not reach its scripted position.");
    }

    private void Follow(Vector3 from) => cameraRig.Follow(from, ivan.transform.position + Vector3.up * 1.46f, 57f, Time.deltaTime);

    private void TakeControl()
    {
        cameraRig = new FinalLabIvanCameraController(gameplayCamera);
        cursor = Cursor.lockState;
        cursorVisible = Cursor.visible;
        playerWasEnabled = player.enabled;
        investigationWasEnabled = investigation.enabled;
        if (player.TryGetComponent(out pistol))
        {
            pistolWasEnabled = pistol.enabled;
            pistol.enabled = false;
        }
        player.enabled = false;
        investigation.enabled = false;
        investigationUI.SetCinematic(true);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void RestoreControl()
    {
        if (cameraRig == null) return;
        if (dialogue != null) dialogue.Hide();
        cameraRig.Restore();
        cameraRig = null;
        if (investigationUI != null) investigationUI.SetCinematic(false);
        if (investigation != null) investigation.enabled = investigationWasEnabled;
        if (player != null) player.enabled = playerWasEnabled;
        if (pistol != null) pistol.enabled = pistolWasEnabled;
        Cursor.lockState = cursor;
        Cursor.visible = cursorVisible;
    }
}
