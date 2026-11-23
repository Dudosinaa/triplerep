using System.Collections;
using System.Linq;
using FinalLab.Investigation;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using Stage = FinalLabFlowController.Stage;

public static partial class FinalLabInvestigationValidation
{
    private static IEnumerable CheckFatherDoorBeforeIvan(FinalLabFlowController flow, FirstPersonPlayer player, Camera camera,
        CharacterController controller, TMP_Text prompt)
    {
        Require(flow != null && flow.SkipGameplayForTesting && flow.State == Stage.IvanDialogue && flow.FatherDoor.IsLocked &&
            !flow.DoorMarkerVisible && !flow.Father.Actor.gameObject.activeSelf, "The saved encounter is hidden and locked while the investigation and Ivan dialogue are pending.");
        flow.OnGameplayCompleted();
        Require(flow.State == Stage.IvanDialogue && flow.FatherDoor.IsLocked, "An early gameplay callback cannot unlock the father door.");
        if (Application.isBatchMode) flow.SkipGameplayForTesting = false;
        flow.SkipIvanGameplayForTesting = false;
        var center = flow.FatherDoor.transform.position;
        Aim(player, camera, controller, center + Vector3.back * 1.31f, center + Vector3.up * 1.15f);
        for (int f = 0; f < 3; f++) yield return null;
        Require(prompt.text == "Дверь заперта", "The heavy door shows the locked prompt before Ivan finishes.");
        foreach (var frame in DoorKeyPress()) yield return frame;
        Require(!flow.FatherDoor.IsMoving && !flow.Father.Actor.gameObject.activeSelf, "E cannot reveal the father early.");
    }

    private static IEnumerable CheckFatherEncounter(FinalLabFlowController flow, FinalLabIvanCutsceneController ivan,
        FirstPersonPlayer player, Camera camera, CharacterController controller, FinalLabInvestigationManager investigation, TMP_Text prompt)
    {
        var door = flow.FatherDoor;
        var father = flow.Father;
        var animator = father.Actor;
        var rig = animator.GetComponentsInChildren<Transform>(true);
        var head = rig.Single(t => t.name.StartsWith("ValveBiped.Bip01_Head1_"));
        var hips = rig.Single(t => t.name.StartsWith("ValveBiped.Bip01_Pelvis_"));
        var leftFoot = rig.Single(t => t.name.StartsWith("ValveBiped.Bip01_L_Foot_"));
        var rightFoot = rig.Single(t => t.name.StartsWith("ValveBiped.Bip01_R_Foot_"));
        Require(ivan.State == FinalLabIvanCutsceneController.Stage.Finished && !ivan.Door.IsOpen && player.enabled && investigation.enabled,
            "Ivan stays in his control room and FPS returns before the father encounter.");
        if (!flow.SkipGameplayForTesting)
        {
            Require(flow.State == Stage.GameplayPhase && door.IsLocked && !flow.DoorMarkerVisible,
                "With skip off, the finished Ivan dialogue waits in GameplayPhase.");
            flow.OnGameplayCompleted();
        }
        Require(flow.State == Stage.FatherDoorReady && !door.IsLocked && flow.DoorMarkerVisible && !animator.gameObject.activeSelf,
            "The next stage unlocks the existing heavy door and shows the marker while the father remains hidden.");

        Vector3 plane = door.transform.position;
        Vector3 target = plane + Vector3.up * 1.15f;
        Aim(player, camera, controller, plane + Vector3.back * 2.47f, target);
        for (int f = 0; f < 3; f++) yield return null;
        Capture(camera, "FatherEncounter-01-marker");
        yield return null;
        Keys(Key.W);
        for (float t = 0; t < 2f && player.transform.position.z < plane.z - 1.15f; t += Time.deltaTime) yield return null;
        Keys();
        for (int f = 0; f < 4; f++) yield return null;
        Require(player.transform.position.z > plane.z - 1.42f && prompt.text == "Открыть дверь в комнату отца — E", "The FPS player walks to the marker and sees the exact E prompt.");
        int completed = 0;
        flow.FatherSequenceFinished += () => completed++;
        foreach (var frame in DoorKeyPress()) yield return frame;
        Require(flow.State == Stage.FatherDoorOpening && !flow.DoorMarkerVisible && prompt.text.Length == 0 && !player.enabled && !investigation.enabled,
            "E hides the marker, starts the original door and locks conflicting controls.");
        while (!animator.gameObject.activeSelf && flow.State == Stage.FatherDoorOpening) yield return null;
        Require(animator.gameObject.activeSelf && door.Openness > 0 && !door.IsOpen, "The father first becomes visible while the door begins to move.");
        float openingCameraZ = camera.transform.position.z;
        while (!door.IsOpen && flow.State == Stage.FatherDoorOpening) yield return null;
        Require(camera.transform.position.z > openingCameraZ + .38f &&
            Vector3.Angle(camera.transform.forward, Vector3.Lerp(hips.position, head.position, .76f) - camera.transform.position) < 24f,
            "From the opening, the FPS camera advances and aims toward the bed rather than the door face.");

        bool bed = false, seated = false, rising = false, standing = false, surprise = false, falling = false, frozen = false, fading = false;
        var captions = new bool[father.Lines.Count];
        var frozenHead = new Pose();
        float lookDown = 0f;
        float lowestDeathVertex = float.PositiveInfinity, nextFloorSample = 0;
        float clickAt = float.PositiveInfinity, releaseAt = 0f;
        int pendingLine = -1, shownLines = 0;
        int releasedOn = -10;
        bool pressed = true, clickedDuringDeath = false, checkedSurpriseGate = false;
        var mousePoint = new Vector2(Screen.width * .5f, Screen.height * .5f);
        Pointer(mousePoint, true);
        releaseAt = float.PositiveInfinity;
        Require(father.Lines[0].text == "Что?.. Где я?.. Сколько времени прошло?.." &&
            father.Lines[1].text == "Алёша?.. Это ты?.. Алёша, неужели это правда ты?..", "Saved first and second captions match the new wording exactly.");
        var floorBuffer = new Mesh();
        var skin = animator.GetComponentsInChildren<SkinnedMeshRenderer>();
        try
        {
            while (flow.State != Stage.IvanGameplayPhase)
            {
                var state = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
                bool Is(string name) => state.IsName("Base Layer." + name);
                if (pressed && Time.time >= releaseAt)
                {
                    Pointer(mousePoint);
                    pressed = false;
                    releasedOn = Time.frameCount;
                }
                if (!bed && door.IsOpen && Is(FatherCutsceneController.InitialBedPose) && camera.transform.position.z > plane.z + .85f)
                {
                    bed = true;
                    Require(head.position.y > .58f && head.position.y < 1.32f, "Initial pose remains near mattress height.");
                    Capture(camera, "FatherEncounter-02-on-bed");
                }
                if (!seated && Is(FatherCutsceneController.SitUp) && state.normalizedTime > .89f)
                {
                    seated = true;
                    Capture(camera, "FatherEncounter-03-seated");
                }
                if (!rising && Is(FatherCutsceneController.StandUp) && state.normalizedTime > .42f)
                {
                    rising = true;
                    Capture(camera, "FatherEncounter-04-rising");
                }
                if (!surprise && Is(FatherCutsceneController.Surprised) && state.normalizedTime > .42f)
                {
                    surprise = true;
                    Capture(camera, "FatherEncounter-05-recognition");
                }
                if (!standing && Is(FatherCutsceneController.StandingIdle) && state.normalizedTime > .06f)
                {
                    standing = true;
                    Require(head.position.y > 1.34f && Mathf.Min(leftFoot.position.y, rightFoot.position.y) < .28f &&
                        Mathf.Abs(leftFoot.position.y - rightFoot.position.y) < .13f && camera.transform.position.z > plane.z + .55f &&
                        camera.transform.position.y > 1.57f && Mathf.Abs(camera.transform.position.y - head.position.y) < .20f,
                        "The father stands with both feet near the floor and the FPS camera at face level.");
                }
                if (!falling && Is(FatherCutsceneController.Death) && state.normalizedTime > .5f)
                {
                    falling = true;
                    Capture(camera, "FatherEncounter-07-collapse");
                }
                if (Is(FatherCutsceneController.Death) && Time.time >= nextFloorSample)
                {
                    nextFloorSample = Time.time + .036f;
                    foreach (var renderer in skin)
                    {
                        renderer.BakeMesh(floorBuffer);
                        foreach (var v in floorBuffer.vertices)
                            lowestDeathVertex = Mathf.Min(lowestDeathVertex, renderer.transform.TransformPoint(v).y - plane.y);
                    }
                }
                if (!clickedDuringDeath && Is(FatherCutsceneController.Death) && state.normalizedTime > .20f && state.normalizedTime < .7f && !pressed && Time.frameCount > releasedOn + 1)
                {
                    Require(!father.Subtitles.Visible && animator.speed > 0, "No caption advances before the fall completes.");
                    Pointer(mousePoint, true);
                    pressed = true;
                    releaseAt = Time.time + .17f;
                    clickedDuringDeath = true;
                }
                if (animator.speed == 0)
                {
                    if (!frozen)
                    {
                        frozen = true;
                        frozenHead = new Pose(head.position, head.rotation);
                        Require(Is(FatherCutsceneController.Death) && head.position.y < .49f && hips.position.y < .49f,
                            "The non-looping Death state holds the body beside the bed at floor level.");
                    }
                    lookDown = Mathf.Min(lookDown, camera.transform.forward.y);
                    if (Vector3.Distance(frozenHead.position, head.position) > .002f || Quaternion.Angle(frozenHead.rotation, head.rotation) > .15f)
                        throw new System.InvalidOperationException("The final body pose moved after death.");
                }
                if (father.Subtitles.Visible)
                {
                    int index = father.Subtitles.Index;
                    if (index >= 0 && index < captions.Length && !captions[index] && father.Subtitles.Body.text == father.Lines[index].text)
                    {
                        Require(index == shownLines++, "Each father caption appears once and in order despite held clicks.");
                        captions[index] = true;
                        father.Subtitles.Body.ForceMeshUpdate();
                        Require(father.Subtitles.Speaker.text == father.Lines[index].speaker && !father.Subtitles.Body.isTextOverflowing,
                            $"Father encounter caption {index + 1} displays the agreed Russian text without overflow.");
                        if (index is 0 or 4 or 8 or 9) Capture(camera, $"FatherEncounter-line-{index + 1:D2}");
                        Require(father.Subtitles.GetComponentsInChildren<TMP_Text>(true).Any(label => label.text == "ЛКМ — продолжить" && label.enabled),
                            "The father uses the shared subtitle panel with the manual continue hint.");
                        pendingLine = index;
                        clickAt = Time.time + (index == 1 ? .26f : index == 0 ? 1.20f : Application.isBatchMode ? .23f : 1.15f);
                        if (index == 0) releaseAt = Time.time + .24f;
                    }
                }
                if (!pressed && pendingLine >= 0 && Time.time >= clickAt && Time.frameCount > releasedOn + 1)
                {
                    Require(father.Subtitles.Visible && father.Subtitles.Index == pendingLine, "A father line waits for a fresh mouse press.");
                    if (pendingLine == 1)
                    {
                        checkedSurpriseGate = flow.State == Stage.FatherIntroduction && Is(FatherCutsceneController.Surprised) && state.normalizedTime < .85f;
                        Require(checkedSurpriseGate, "A click is accepted while the surprise animation is still playing.");
                    }
                    Pointer(mousePoint, true);
                    pressed = true;
                    releaseAt = Time.time + .27f;
                    pendingLine = -1;
                }
                if (checkedSurpriseGate && Is(FatherCutsceneController.Surprised) && state.normalizedTime < .93f && father.Subtitles.Index != 1)
                    throw new System.InvalidOperationException("An early click cut off the surprise animation.");
                if (!fading && father.Blackout > .35f)
                {
                    fading = true;
                    Capture(camera, "FatherEncounter-08-fade");
                }
                yield return null;
            }
        }
        finally { Object.Destroy(floorBuffer); }
        Require(float.IsFinite(lowestDeathVertex) && lowestDeathVertex > -.020f,
            $"During the death and hold, the lowest visible mesh sample stays at floor level ({lowestDeathVertex:F3} m relative to floor).");
        Require(bed && seated && rising && standing && surprise && falling && frozen && fading && clickedDuringDeath && checkedSurpriseGate && captions.All(seen => seen) &&
            lookDown < -.26f && completed == 1 && father.Blackout == 1 && !player.enabled && !investigation.enabled &&
            !father.Subtitles.Visible && animator.speed == 0 && !ivan.Door.IsOpen,
            "The full encounter reaches the final black screen once, with all lines shown, the body held and control still suspended.");
        Capture(camera, "FatherEncounter-09-black");
        yield return null;
        Debug.Log("FINAL_LAB_FATHER_ENCOUNTER_PLAYED_TO_BLACK");
    }
}
