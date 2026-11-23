using System.Collections;
using System.Linq;
using FinalLab.Investigation;
using UnityEngine;
using UnityEngine.InputSystem;
using Stage = FinalLabIvanCutsceneController.Stage;

public static partial class FinalLabInvestigationValidation
{
    private static IEnumerable ExerciseIvanCutscene(FinalLabIvanCutsceneController scene, FirstPersonPlayer player, Camera camera,
        FinalLabInvestigationManager investigation, Vector3 originalEye)
    {
        var door = scene.Door;
        var ivan = scene.Ivan;
        var dialogue = scene.Dialogue;
        var transcript = dialogue.ReadLines();
        Vector3 scale = ivan.Animator.transform.localScale;
        Require(scene.State == Stage.FramingDoor && !dialogue.Visible && !ivan.Visible,
            "Final ESC starts by smoothly framing the existing control-room door with Ivan still hidden.");
        while (scene.State == Stage.FramingDoor) yield return null;
        Require(scene.State == Stage.Opening && Vector3.Angle(camera.transform.forward,
            door.transform.position + Vector3.up * 1.13f - camera.transform.position) < 8f,
            "The FPS camera turns to the control-room doorway before Ivan walks.");
        Capture(camera, "Cutscene-01-door");
        while (scene.State == Stage.Opening) yield return null;
        Require(scene.State == Stage.Exiting && door.IsOpen && ivan.Visible, "Ivan waits inside until the existing door is fully open.");
        foreach (var frame in CheckIvanWalk(scene, camera, leaving: true)) yield return frame;
        Require(scene.State == Stage.Talking && !player.enabled && !investigation.enabled, "Gameplay controls stay disabled during the conversation.");
        Require(camera.transform.position.y > 1.56f && Mathf.Abs(camera.transform.position.y - ivan.HeadPosition.y) < .19f,
            "Ivan and Alexei hold the conversation near the same eye level.");
        Vector3 feet = player.transform.position;
        var middle = new Vector2(Screen.width * .5f, Screen.height * .5f);
        string expected = "CalmIdle";
        for (int index = 0; index < transcript.Length; index++)
        {
            var line = transcript[index];
            bool checkBlend = index is 24 or 32;
            float blendBegan = -1;
            int blendSamples = 0;
            while (scene.State == Stage.Talking && (!dialogue.Visible || dialogue.Index != index))
            {
                if (checkBlend && ivan.Animator.IsInTransition(0) && ivan.Animator.GetNextAnimatorStateInfo(0).IsName("Base Layer." + line.animation))
                {
                    if (blendBegan < 0) blendBegan = Time.time;
                    float fraction = ivan.Animator.GetAnimatorTransitionInfo(0).normalizedTime;
                    if (blendSamples < 2 && fraction >= (blendSamples + 1) / 3f)
                    {
                        blendSamples++;
                        Capture(camera, $"Cutscene-blend-{line.animation}-{blendSamples}");
                    }
                }
                yield return null;
            }
            Require(scene.State == Stage.Talking && dialogue.Index == index && dialogue.Visible, "Dialogue line " + (index + 1) + " waits for a click.");
            if (checkBlend)
                Require(blendSamples == 2 && Time.time - blendBegan > .59f, $"{line.animation} passes through two intermediate poses over {Time.time - blendBegan:F2} seconds.");
            for (int f = 0; f < 3; f++) yield return null;
            dialogue.Body.ForceMeshUpdate();
            if (!string.IsNullOrEmpty(line.animation)) expected = line.animation;
            Require(dialogue.Speaker.text == line.speaker && dialogue.Body.text == line.text && !dialogue.Body.isTextOverflowing &&
                ivan.Animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer." + expected) && ivan.Animator.transform.localScale == scale,
                $"Exact Russian line {index + 1}/35 with no overflow, animator {expected}.");
            if (index == 0)
            {
                Keys(Key.E, Key.W, Key.Escape);
                for (int f = 0; f < 4; f++) yield return null;
                Keys();
                Require(Vector3.Distance(feet, player.transform.position) < .001f && door.IsOpen && dialogue.Index == 0,
                    "E, W and Escape leave the dialogue, player position and door unchanged.");
            }
            if (index is 0 or 1 or 8 or 10 or 24 or 32 or 34)
            {
                Capture(camera, $"Cutscene-dialogue-{index + 1:D2}-{expected}");
                yield return null;
            }
            if (index == 10)
            {
                var hand = ivan.Animator.GetComponentsInChildren<Transform>()
                    .Single(bone => bone.name == "ValveBiped.Bip01_R_Hand_041_67");
                Vector3 start = hand.position;
                float distance = 0f;
                for (float elapsed = 0; elapsed < 2.1f; elapsed += Time.deltaTime)
                {
                    distance = Mathf.Max(distance, Vector3.Distance(start, hand.position));
                    yield return null;
                }
                Require(dialogue.Index == index && dialogue.Visible && distance > .06f,
                    "Ivan gestures while the first angry line remains on screen until the player's click.");
                Capture(camera, "Cutscene-anger-gestures");
                yield return null;
            }
            Pointer(middle, true);
            for (int f = 0; f < 2; f++) yield return null;
            Pointer(middle);
            yield return null;
        }
        Require(!dialogue.Visible && scene.State == Stage.Turning, "Only the click after the last line closes the subtitles.");
        while (scene.State == Stage.Turning && !ivan.Animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.TurnAround")) yield return null;
        Require(scene.State == Stage.Turning, "The actual TurnAround clip plays after the exit pause.");
        while (scene.State == Stage.Turning && ivan.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime < .49f) yield return null;
        Capture(camera, "Cutscene-04-turn");
        while (scene.State == Stage.Turning) yield return null;
        Require(scene.State == Stage.Returning && ivan.RequestedAnimation == "Walking", "Ivan returns with the same walking clip.");
        foreach (var frame in CheckIvanWalk(scene, camera, leaving: false)) yield return frame;
        while (scene.State != Stage.Finished) yield return null;
        Require(ivan.transform.position.z > door.transform.position.z + .90f && !door.IsOpen && !door.IsMoving &&
            player.enabled && investigation.enabled && investigation.Completed == 3 && !dialogue.Visible &&
            Vector3.Distance(originalEye, camera.transform.position) < .035f,
            "Ivan is safely inside, the door is closed, FPS controls and camera are restored, and progress remains 3/3.");
        Capture(camera, "Cutscene-06-restored");
        Keys(Key.Escape);
        for (int f = 0; f < 3; f++) yield return null;
        Keys();
        Require(scene.State == Stage.Finished && !dialogue.Visible, "Escape cannot replay the completed cutscene.");
        if (activeScope == Scope.FatherEncounter)
        {
            Debug.Log("FINAL_LAB_IVAN_CONVERSATION_PLAYED_35_LINES");
            yield break;
        }
        Vector3 doorway = door.transform.position;
        Vector3 ivanInside = ivan.transform.position;
        Aim(player, camera, player.GetComponent<CharacterController>(), doorway + Vector3.back * 1.17f, doorway + Vector3.up * 1.13f);
        for (int f = 0; f < 3; f++) yield return null;
        Keys(Key.E);
        for (int f = 0; f < 3; f++) yield return null;
        Keys();
        Require(door.IsMoving, "The regular E interaction opens the control-room door after the scene.");
        while (door.IsMoving) yield return null;
        Require(door.IsOpen && scene.State == Stage.Finished && !dialogue.Visible && ivan.transform.position == ivanInside,
            "The door opens manually without restarting the scene, and Ivan stays inside the control room.");
        Debug.Log("FINAL_LAB_IVAN_CONVERSATION_PLAYED_35_LINES");
    }

    private static IEnumerable CheckIvanWalk(FinalLabIvanCutsceneController scene, Camera camera, bool leaving)
    {
        var ivan = scene.Ivan;
        var stage = leaving ? Stage.Exiting : Stage.Returning;
        string direction = leaving ? "exit" : "return";
        float threshold = scene.Door.transform.position.z + (leaving ? -.28f : .18f);
        Vector3 position = ivan.transform.position;
        float time = Time.time;
        float first = 0, last = 0, peak = 0, longestFrame = 0;
        bool crossed = false, braking = false;
        while (scene.State == stage)
        {
            Vector3 next = ivan.transform.position;
            float dt = Time.time - time;
            if (dt > 0)
            {
                longestFrame = Mathf.Max(longestFrame, dt);
                float current = Vector3.ProjectOnPlane(next - position, Vector3.up).magnitude / dt;
                if (current > .025f)
                {
                    if (first == 0) first = current;
                    last = current;
                    peak = Mathf.Max(peak, current);
                }
            }
            position = next;
            time = Time.time;
            if (!crossed && (leaving ? next.z <= threshold : next.z >= threshold))
            {
                crossed = true;
                var gait = ivan.Animator.GetCurrentAnimatorStateInfo(0);
                Require(scene.Door.IsOpen && gait.IsName("Base Layer.Walking") && gait.speedMultiplier > .85f,
                    $"Ivan clears the open threshold at the regular walking cadence on {direction}.");
                Capture(camera, leaving ? "Cutscene-02-walking-out" : "Cutscene-05-returning");
            }
            braking |= crossed && ivan.RequestedAnimation == FinalLabIvanCharacterController.CalmIdle;
            yield return null;
        }
        float finalSpeedLimit = longestFrame >= .10f ? .82f : .62f;
        Require(crossed && braking && first > .16f && first < .68f && peak > 1f && peak < 1.30f && last < finalSpeedLimit,
            $"Ivan {direction} eases in and out of the gait (speed {first:F2} → {peak:F2} → {last:F2} m/s).");
    }
}
