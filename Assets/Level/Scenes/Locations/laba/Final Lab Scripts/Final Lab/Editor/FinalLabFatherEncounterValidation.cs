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
        Require(flow != null && flow.State == Stage.IvanDialogue && flow.FatherDoor.IsLocked &&
            !flow.DoorMarkerVisible && !flow.Father.Actor.gameObject.activeSelf, "The saved encounter is hidden and locked while the investigation and Ivan dialogue are pending.");
        flow.OnGameplayCompleted();
        Require(flow.State == Stage.IvanDialogue && flow.FatherDoor.IsLocked, "An early gameplay callback cannot unlock the father door.");
        Require(!flow.SkipGameplayForTesting && !flow.SkipIvanGameplayForTesting, "Both gameplay phases are enabled.");
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
        Require(ivan.State == FinalLabIvanCutsceneController.Stage.Finished && !ivan.Door.IsOpen && player.enabled,
            "Ivan stays in his control room and FPS returns before the father encounter.");
        Require(flow.State == Stage.GameplayPhase && door.IsLocked && !flow.DoorMarkerVisible &&
            !animator.gameObject.activeSelf && !investigation.enabled,
            "The finished Ivan dialogue starts reserve power with the father room still locked.");
        var mechanisms = Object.FindFirstObjectByType<FinalLabMechanisms>();
        var controls = mechanisms.GetComponentsInChildren<FinalLabSwitch>();
        Keys(Key.LeftCtrl);
        while (controller.height > 1.15f) yield return null;
        Keys();
        while (controller.height < 1.65f) yield return null;
        Require(controller.height >= 1.65f, "Ctrl lowers the player capsule and releasing it restores standing height.");
        foreach (var control in controls.Where(c => c.Kind is FinalLabSwitch.Function.Reserve or FinalLabSwitch.Function.Isolation))
        {
            foreach (var tick in ActivateControl(control, player, camera, controller, prompt)) yield return tick;
            if (flow.State == Stage.GameplayPhase)
                Require(!animator.gameObject.activeSelf && door.IsLocked, "Father stays hidden before the last lever.");
        }
        Require(flow.State == Stage.FatherDoorReady && !door.IsLocked && flow.DoorMarkerVisible && animator.gameObject.activeSelf &&
            animator.speed == 0 && animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer." + FatherCutsceneController.InitialBedPose),
            "The fifth switch unlocks and highlights the door, with the father already lying still on his bed.");

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
        Require(player.transform.position.z > plane.z - 1.42f && prompt.text == "Открыть дверь — E", "The FPS player walks to the marker and sees the exact E prompt.");
        int completed = 0;
        flow.FatherSequenceFinished += () => completed++;
        foreach (var frame in DoorKeyPress()) yield return frame;
        Require(flow.State == Stage.FatherDoorOpening && !flow.DoorMarkerVisible && prompt.text.Length == 0 && !player.enabled && !investigation.enabled,
            "E hides the marker, starts the original door and locks conflicting controls.");
        while (door.Openness <= 0 && flow.State == Stage.FatherDoorOpening) yield return null;
        Require(animator.gameObject.activeSelf && animator.speed == 0 && !door.IsOpen,
            "The same father stays in his bed pose while the door begins to move.");
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
                if (animator.speed == 0 && Is(FatherCutsceneController.Death))
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
        foreach (var tick in CheckK17AndRescue(flow, mechanisms, controls, player, camera, controller, prompt)) yield return tick;
    }

    private static IEnumerable ActivateControl(FinalLabSwitch control, FirstPersonPlayer player, Camera camera,
        CharacterController controller, TMP_Text prompt, bool hold = false)
    {
        Vector3 target = control.Point;
        Vector3 preferred = control.Kind is FinalLabSwitch.Function.Reserve or FinalLabSwitch.Function.Isolation or FinalLabSwitch.Function.Traction
            ? control.transform.parent.forward : Vector3.back;
        bool aimed = false;
        int attempts = 0;
        string actualApproach = "";
        foreach (float radius in new[] { 1.12f, .88f, 1.42f, 1.76f })
        {
            for (int n = 0; n < 32 && !aimed; n++)
            {
                float angle = (n % 2 == 0 ? (n + 1) / 2 : -(n + 1) / 2) * 11.25f;
                Vector3 eye = new Vector3(target.x, 1.64f, target.z) +
                    Quaternion.Euler(0, angle, 0) * preferred * radius;
                if (!Physics.Raycast(eye, Vector3.down, out var floor, 1.75f, ~0, QueryTriggerInteraction.Ignore) ||
                    floor.point.y is > .14f or < -.1f) continue;
                if (Physics.OverlapCapsule(floor.point + Vector3.up * .29f, floor.point + Vector3.up * 1.47f,
                        .23f, ~0, QueryTriggerInteraction.Ignore).Any(c => c != controller)) continue;
                var toward = target - eye;
                if (Physics.Raycast(eye, toward.normalized, out var first, toward.magnitude - .025f, ~0,
                        QueryTriggerInteraction.Ignore) && !control.Owns(first.collider)) continue;
                Aim(player, camera, controller, floor.point, target);
                attempts++;
                for (int f = 0; f < 3; f++) yield return null;
                aimed = prompt.text == control.Label + " — E";
                Vector3 actual = target - camera.transform.position;
                float dot = Vector3.Dot(actual.normalized, camera.transform.forward);
                var blocked = Physics.Raycast(camera.transform.position, actual.normalized, out var obstruction,
                    actual.magnitude - .025f, ~0, QueryTriggerInteraction.Ignore) ? obstruction.collider.name : "none";
                actualApproach = $"eye {camera.transform.position:F2}, alignment {dot:F3}, distance {actual.magnitude:F2}, hit {blocked}";
            }
            if (aimed) break;
        }
        Require(aimed, $"The live prompt selects {control.name} at {target:F2} ({attempts} approaches; last prompt '{prompt.text}'; {actualApproach}).");
        Capture(camera, "FatherEncounter-control-" + control.transform.parent.name + "-" + control.name);
        yield return null;
        Keys(Key.E);
        for (int f = 0; f < 3; f++) yield return null;
        Require(control.Engaged, "E operates " + control.name + ".");
        if (hold)
            for (float elapsed = 0; elapsed < 5.45f; elapsed += Time.unscaledDeltaTime) yield return null;
        Keys();
        for (int f = 0; f < 3; f++) yield return null;
    }

    private static IEnumerable CheckK17AndRescue(FinalLabFlowController flow, FinalLabMechanisms mechanisms,
        FinalLabSwitch[] controls, FirstPersonPlayer player, Camera camera, CharacterController controller, TMP_Text prompt)
    {
        while (!player.enabled) yield return null;
        Require(!flow.FatherDoor.IsLocked && flow.Father.Actor.gameObject.activeSelf && flow.Father.Actor.speed == 0,
            "Ivan takes control after the fade while the father's body remains in place.");
        var arcs = mechanisms.GetComponentsInChildren<LineRenderer>();
        foreach (var control in controls.Where(c => c.Kind >= FinalLabSwitch.Function.Traction))
        {
            foreach (var tick in ActivateControl(control, player, camera, controller, prompt,
                         control.Kind == FinalLabSwitch.Function.Contour)) yield return tick;
            if (control.Kind == FinalLabSwitch.Function.Traction)
                Require(!controls.Single(c => c.Kind == FinalLabSwitch.Function.Cover).Engaged, "Traction alone cannot start overload.");
        }
        bool peakVisible = false;
        while (flow.State == Stage.IvanGameplayPhase)
        {
            if (!peakVisible && arcs.Any(a => a.enabled && a.widthMultiplier > .026f))
            {
                peakVisible = true;
                Capture(camera, "FatherEncounter-K17-overload");
            }
            yield return null;
        }
        Require(peakVisible && arcs.All(a => !a.enabled) && flow.State == Stage.AlexeiRescue,
            "Overload draws the arc and shuts it off completely before rescue.");
        var rescue = Object.FindFirstObjectByType<FinalLabAlexeiRescue>();
        var data = new UnityEditor.SerializedObject(rescue);
        var actor = (Animator)data.FindProperty("alexei").objectReferenceValue;
        var threshold = (Transform)data.FindProperty("roomThreshold").objectReferenceValue;
        var head = actor.GetBoneTransform(HumanBodyBones.Head);
        var dialogue = flow.Father.Subtitles;
        for (float t = 0; t < 1.45f; t += Time.unscaledDeltaTime) yield return null;
        bool reached = false;
        for (var z = threshold.position.z + .48f; z < threshold.position.z + 2.55f && !reached; z += .23f)
        for (var x = .62f; x < 3.22f && !reached; x += .23f)
        {
            var candidate = new Vector3(x, .025f, z);
            var eye = candidate + Vector3.up * 1.65f;
            if (Vector3.Distance(eye, head.position) > 2.25f ||
                Physics.Linecast(eye, head.position, ~0, QueryTriggerInteraction.Ignore) ||
                Physics.OverlapCapsule(candidate + Vector3.up * .29f, candidate + Vector3.up * 1.47f, .23f,
                    ~0, QueryTriggerInteraction.Ignore).Any(c => c != controller)) continue;
            Aim(player, camera, controller, candidate, head.position);
            for (int f = 0; f < 4; f++) yield return null;
            reached = !player.enabled;
        }
        Require(reached && head.position.y < .6f, "Ivan can get a clear line of sight to Alexei on the floor.");
        for (int i = 0; i < 2; i++)
        {
            while (!dialogue.Visible || dialogue.Index != i) yield return null;
            Capture(camera, "FatherEncounter-rescue-" + i);
            foreach (var tick in ContinueCaption()) yield return tick;
        }
        var rescuePrompt = (TMP_Text)data.FindProperty("prompt").objectReferenceValue;
        while (!rescuePrompt.isActiveAndEnabled) yield return null;
        foreach (var tick in DoorKeyPress()) yield return tick;
        while (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != TrainFinale.ScenePath) yield return null;
        foreach (var tick in CheckTrainFinale()) yield return tick;
    }

    private static IEnumerable ContinueCaption()
    {
        var center = new Vector2(Screen.width * .5f, Screen.height * .5f);
        Pointer(center);
        for (int i = 0; i < 3; i++) yield return null;
        Pointer(center, true);
        for (int i = 0; i < 3; i++) yield return null;
        Pointer(center);
        yield return null;
    }

    private static IEnumerable CheckTrainFinale()
    {
        var end = Object.FindFirstObjectByType<TrainFinale>();
        var data = new UnityEditor.SerializedObject(end);
        var dialogue = (FinalLabIvanDialogue)data.FindProperty("subtitles").objectReferenceValue;
        var camera = (Camera)data.FindProperty("view").objectReferenceValue;
        var seated = (Transform)data.FindProperty("seatedEye").objectReferenceValue;
        var standing = (Transform)data.FindProperty("standingEye").objectReferenceValue;
        float previous = camera.transform.position.y;
        bool photographed = false;
        float started = Time.unscaledTime;
        Require(Mathf.Abs(previous - seated.position.y) < .05f, "Train fades in from Ivan's seated eye height.");
        while (!dialogue.Visible)
        {
            float y = camera.transform.position.y;
            if (y < previous - .004f || y - previous >= .15f)
                throw new System.InvalidOperationException("Train camera height jumped during the rise.");
            previous = y;
            if (!photographed && y >= seated.position.y + .22f)
            {
                Capture(camera, "FatherEncounter-Train-rise");
                photographed = true;
            }
            yield return null;
        }
        Require(photographed && Mathf.Abs(previous - standing.position.y) < .025f && Time.unscaledTime - started >= 3f,
            "Train dialogue begins after the full smooth camera rise.");
        var trainActor = (Animator)data.FindProperty("alexei").objectReferenceValue;
        for (int i = 0; i < dialogue.ReadLines().Length; i++)
        {
            while (!dialogue.Visible || dialogue.Index != i) yield return null;
            if (i == 5)
            {
                while (!trainActor.GetCurrentAnimatorStateInfo(0).IsName("Base Layer." + TrainFinale.Point) ||
                    trainActor.GetCurrentAnimatorStateInfo(0).normalizedTime < .54f) yield return null;
                Capture(camera, "FatherEncounter-Train-gesture");
            }
            dialogue.Body.ForceMeshUpdate();
            Require(!dialogue.Body.isTextOverflowing, "Train subtitle " + (i + 1) + " fits.");
            foreach (var tick in ContinueCaption()) yield return tick;
        }
        var title = (TMP_Text)data.FindProperty("title").objectReferenceValue;
        while (title.alpha < .999f) yield return null;
        Require(title.text == "СВЯЗЬ РАЗОРВАНА", "The existing Train finale text is displayed.");
        Capture(camera, "FatherEncounter-Train-ending");
    }
}
