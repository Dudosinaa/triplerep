using System;
using System.Linq;
using FinalLab.Investigation;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.UI;
using Object = UnityEngine.Object;

internal static partial class FinalLabIvanSceneSetup
{
    internal const string RootName = "Ivan and Alexei — final laboratory conversation";
    private const string Scripts = "Assets/Level/Scenes/Locations/laba/Final Lab Scripts/Final Lab/";
    private const string Laba = "Assets/Art/Animations/Laba/";
    private const string Prepared = Laba + "Scientist Animation Previews/";
    private static readonly (string state, string path)[] Motions =
    {
        ("Walking", Prepared + "Scientist_Walk.anim"), ("CalmIdle", Prepared + "Scientist_Calm_Stand.anim"),
        ("AngryArgument", Prepared + "Scientist_Angry_Argument.anim"), ("SadIdle", Laba + "Scientist Sad Stand/Scientist_Sad_Stand.anim"),
        ("TurnAround", Prepared + "Scientist_Turn.anim")
    };

    public static void ApplyMotion()
    {
        FinalLabInvestigationSurvey.RequireBatch();
        var scene = EditorSceneManager.OpenScene(FinalLabBuilder.ScenePath);
        var root = scene.GetRootGameObjects().Single(node => node.name == RootName);
        var cutscene = root.GetComponent<FinalLabIvanCutsceneController>();
        var marks = root.transform.Find("Staging marks");
        VerifyRoutePair(cutscene.Door, marks.Find("Ivan_InsideStart_and_End").position,
            marks.Find("Ivan_Hall").position, marks.Find("Ivan_DialoguePoint").position, cutscene.Ivan.Motor);

        var animator = cutscene.Ivan.Animator;
        var controller = (AnimatorController)animator.runtimeAnimatorController;
        var sad = (AnimationClip)controller.layers.Single().stateMachine.states
            .Single(node => node.state.name == FinalLabIvanCharacterController.SadIdle).state.motion;
        if (sad != Load<AnimationClip>(Motions[3].path)) throw new InvalidOperationException("SadIdle is linked to an unexpected clip.");
        EnsureSadLoop(sad);
        AssetDatabase.SaveAssetIfDirty(sad);
        AssetDatabase.ImportAsset(Motions[3].path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(controller), ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        VerifySadLoop(animator, Load<AnimationClip>(Motions[3].path));
        Debug.Log("FINAL_LAB_IVAN_MOTION_APPLIED_AND_VERIFIED_WITHOUT_PLAY_MODE");
    }

    public static void ApplyWalkCadence()
    {
        FinalLabInvestigationSurvey.RequireBatch();
        var scene = EditorSceneManager.OpenScene(FinalLabBuilder.ScenePath);
        var cutscene = scene.GetRootGameObjects().Single(node => node.name == RootName).GetComponent<FinalLabIvanCutsceneController>();
        var controller = (AnimatorController)cutscene.Ivan.Animator.runtimeAnimatorController;
        ConfigureWalkCadence(controller);
        VerifyWalkCadenceAcrossFrameRates();
        AssetDatabase.SaveAssets();
        Debug.Log("FINAL_LAB_IVAN_WALK_CADENCE_SAVED");
    }

    public static void Apply()
    {
        FinalLabInvestigationSurvey.RequireBatch();
        var scene = EditorSceneManager.OpenScene(FinalLabBuilder.ScenePath);
        var roots = scene.GetRootGameObjects();
        var previewGroup = roots.Single(root => root.name.StartsWith("Scientist — проверка", StringComparison.Ordinal));
        var template = previewGroup.GetComponentsInChildren<Animator>(true).Single(a =>
            a.runtimeAnimatorController != null && a.runtimeAnimatorController.animationClips.Any(clip => clip.name == "Scientist_Walk"));
        var avatar = template.avatar;
        if (avatar == null || !avatar.isValid || avatar.isHuman) throw new InvalidOperationException("The prepared Scientist Generic Avatar is invalid.");
        var clips = Motions.Select(m => Load<AnimationClip>(m.path)).ToArray();
        EnsureSadLoop(clips[3]);
        if (!clips[0].isLooping || !clips[1].isLooping || !clips[2].isLooping || clips[4].isLooping || clips.Any(clip => clip.humanMotion || clip.legacy))
            throw new InvalidOperationException("The prepared clips do not match the scene's Generic Animator.");
        var player = roots.SelectMany(root => root.GetComponentsInChildren<FirstPersonPlayer>(true)).Single();
        var camera = player.GetComponentInChildren<Camera>();
        var investigation = roots.SelectMany(root => root.GetComponentsInChildren<FinalLabInvestigationManager>(true)).Single();
        var investigationUI = investigation.GetComponentInChildren<FinalLabInvestigationUI>();
        var doors = roots.Single(root => root.name == FinalLabPortalBuilder.RootName).transform;
        var controlDoor = doors.Find("ControlRoom_MetalDoor").GetComponent<FinalLabMetalDoor>();
        var fatherDoor = doors.Find("FatherRoom_HeavyMetalDoor");
        var resonator = roots.Single(root => root.name == FinalLabResonatorBuilder.ModuleName).transform;
        var architecture = roots.Single(root => root.name == "FinalLab_Environment").transform;
        float floor = architecture.Find("03_Main_Laboratory/MainFloor").GetComponent<BoxCollider>().bounds.max.y;
        var aperture = FinalLabPortalBuilder.ReadOpening(architecture, FinalLabPortalBuilder.ControlRoom, "DoorOpening");
        var current = roots.SingleOrDefault(root => root.name == RootName);
        if (current != null) Object.DestroyImmediate(current);
        previewGroup.SetActive(false);
        foreach (var related in roots.Where(root => root != null && root != previewGroup &&
                     PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root) is Laba + "scientist.glb" or Laba + "Иван грустно стоит.glb"))
            related.SetActive(false);

        var rootObject = new GameObject(RootName);
        var rootTransform = rootObject.transform;
        var markers = Marker(rootTransform, "Staging marks", Vector3.zero);
        float lane = aperture.X - .035f;
        var inside = Marker(markers, "Ivan_InsideStart_and_End", new Vector3(lane, floor, aperture.Room + .97f));
        var doorway = Marker(markers, "Ivan_Doorway", new Vector3(lane, floor, (aperture.Room + aperture.Hall) * .5f));
        var hall = Marker(markers, "Ivan_Hall", new Vector3(lane, floor, aperture.Hall - .47f));
        var talk = Marker(markers, "Ivan_DialoguePoint", new Vector3(-2.18f, floor, 12.59f));
        var eye = Marker(markers, "Alexei_ConversationEye", new Vector3(-3.60f, floor + FinalLabIvanCutsceneController.DialogueEyeHeight, 11.10f));
        VerifyRoutePair(controlDoor, inside.position, hall.position, talk.position);

        var actor = Marker(rootTransform, "Ivan_Karamazov", inside.position);
        actor.rotation = Quaternion.LookRotation(Vector3.back);
        var model = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(Laba + "scientist.glb"), actor);
        model.name = "Scientist";
        model.transform.localScale = template.transform.localScale;
        model.transform.localRotation = template.transform.localRotation;
        model.transform.localPosition = new Vector3(0, template.transform.localPosition.y, 0);
        var originalSkins = template.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        var modelSkins = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if (modelSkins.Length != originalSkins.Length || modelSkins.Zip(originalSkins, (a, b) => a.sharedMesh == b.sharedMesh &&
                a.sharedMaterials.SequenceEqual(b.sharedMaterials)).Any(equal => !equal))
            throw new InvalidOperationException("The original Scientist meshes and materials did not survive cloning.");
        var animator = model.AddComponent<Animator>();
        animator.avatar = avatar;
        var returnTurn = BakeReturnTurn(model, clips[4]);
        clips[4] = returnTurn.clip;
        animator.runtimeAnimatorController = BuildAnimator(clips);
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var bones = model.GetComponentsInChildren<Transform>(true);
        var head = Bone(bones, "Head1");
        foreach (var clip in clips)
            if (AnimationUtility.GetCurveBindings(clip).Any(curve => model.transform.Find(curve.path) == null))
                throw new InvalidOperationException("A prepared clip cannot find a Scientist bone: " + clip.name);

        var motor = actor.gameObject.AddComponent<CharacterController>();
        motor.radius = .24f;
        motor.height = 1.79f;
        motor.center = Vector3.up * (.5f * motor.height);
        motor.stepOffset = .125f;
        motor.skinWidth = .022f;
        motor.minMoveDistance = 0f;
        var character = actor.gameObject.AddComponent<FinalLabIvanCharacterController>();
        character.Configure(animator, motor, head, returnTurn.heading);
        var subtitles = BuildSubtitles(rootTransform);
        var cutscene = rootObject.AddComponent<FinalLabIvanCutsceneController>();
        cutscene.Configure(investigation, investigationUI, player, camera, controlDoor, character, subtitles,
            inside, doorway, hall, talk, eye, fatherDoor, resonator);
        Validate(rootObject, clips, subtitles);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the Ivan conversation in FinalScene.");
        var reopened = EditorSceneManager.OpenScene(FinalLabBuilder.ScenePath).GetRootGameObjects().Single(o => o.name == RootName);
        Validate(reopened, clips, reopened.GetComponentInChildren<FinalLabIvanDialogue>(true));
        Debug.Log("FINAL_LAB_IVAN_CONVERSATION_SAVED");
    }

    private static AnimatorController BuildAnimator(AnimationClip[] clips)
    {
        const string path = Scripts + "FinalLab_Ivan_Animator.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        if (controller.layers.Length == 0) controller.AddLayer("Base Layer");
        var machine = controller.layers.Single().stateMachine;
        for (int i = 0; i < Motions.Length; i++)
        {
            var state = machine.states.Select(node => node.state).SingleOrDefault(node => node.name == Motions[i].state) ??
                machine.AddState(Motions[i].state, new Vector3(265 + (i % 2) * 220, 75 + (i / 2) * 85));
            state.motion = clips[i];
            EditorUtility.SetDirty(state);
            if (state.name == "CalmIdle") machine.defaultState = state;
        }
        EditorUtility.SetDirty(machine);
        ConfigureWalkCadence(controller);
        return controller;
    }

    private static void ConfigureWalkCadence(AnimatorController controller)
    {
        string parameterName = FinalLabIvanCharacterController.WalkRate;
        if (!controller.parameters.Any(parameter => parameter.name == parameterName))
            controller.AddParameter(new AnimatorControllerParameter { name = parameterName, type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
        var walking = controller.layers.Single().stateMachine.states
            .Single(node => node.state.name == FinalLabIvanCharacterController.Walking).state;
        walking.speedParameter = parameterName;
        walking.speedParameterActive = true;
        EditorUtility.SetDirty(walking);
    }

    private static void VerifyWalkCadenceAcrossFrameRates()
    {
        const float length = 2.65f;
        const float speed = 1.15f;
        const float elapsed = .25f;
        foreach (float from in new[] { 0f, 1.2f, length - .24f })
        {
            float shortFrames = from;
            for (int frame = 0; frame < 30; frame++)
                shortFrames = FinalLabIvanCharacterController.AdvanceWalkingDistance(shortFrames, length, speed, elapsed / 30);
            float longFrame = FinalLabIvanCharacterController.AdvanceWalkingDistance(from, length, speed, elapsed);
            if (Mathf.Abs(shortFrames - longFrame) > .002f)
                throw new InvalidOperationException("A long frame changed Ivan's walking cadence.");
        }
        float brakeStart = length - .24f;
        float brakeEnd = FinalLabIvanCharacterController.AdvanceWalkingDistance(brakeStart, length, speed, elapsed);
        if ((brakeEnd - brakeStart) / elapsed > .78f)
            throw new InvalidOperationException("Ivan did not slow down during a long braking frame.");
        Debug.Log("Ivan's acceleration, steady pace and braking match 120 Hz even during a 250 ms frame.");
    }

    private static void EnsureSadLoop(AnimationClip sad)
    {
        if (sad.isLooping) return;
        var settings = AnimationUtility.GetAnimationClipSettings(sad);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(sad, settings);
        EditorUtility.SetDirty(sad);
    }

    private static void VerifySadLoop(Animator source, AnimationClip sad)
    {
        var preview = Object.Instantiate(source.gameObject);
        var graph = PlayableGraph.Create("Ivan sad stance Edit Mode verification");
        try
        {
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var controller = AnimatorControllerPlayable.Create(graph, source.runtimeAnimatorController);
            AnimationPlayableOutput.Create(graph, "Scientist", preview.GetComponent<Animator>()).SetSourcePlayable(controller);
            var head = Bone(preview.GetComponentsInChildren<Transform>(), "Head1");
            string sadState = "Base Layer." + FinalLabIvanCharacterController.SadIdle;
            string calmState = "Base Layer." + FinalLabIvanCharacterController.CalmIdle;
            graph.Play();
            graph.Evaluate(0);
            controller.Play(sadState, 0, 0);
            graph.Evaluate(0);
            graph.Evaluate(sad.length * .08f);
            Quaternion start = head.rotation;
            float minimumMovement = float.PositiveInfinity;
            for (int cycle = 1; cycle <= 3; cycle++)
            {
                float movement = 0;
                for (int frame = 0; frame < 60; frame++)
                {
                    graph.Evaluate(sad.length / 60);
                    movement = Mathf.Max(movement, Quaternion.Angle(start, head.rotation));
                }
                var state = controller.GetCurrentAnimatorStateInfo(0);
                float difference = Quaternion.Angle(start, head.rotation);
                if (!sad.isLooping || !state.IsName(sadState) || state.normalizedTime < cycle || difference > 1f || movement < 4f)
                    throw new InvalidOperationException($"SadIdle did not repeat cycle {cycle}: clip looping {sad.isLooping}, state looping {state.loop}, " +
                        $"active state {state.IsName(sadState)}, time {state.normalizedTime:F3}, pose difference {difference:F2}°, motion {movement:F2}°.");
                minimumMovement = Mathf.Min(minimumMovement, movement);
            }
            controller.CrossFadeInFixedTime(calmState, .22f, 0);
            for (int frame = 0; frame < 20; frame++) graph.Evaluate(1f / 60f);
            if (!controller.GetCurrentAnimatorStateInfo(0).IsName(calmState) || controller.IsInTransition(0))
                throw new InvalidOperationException("The sad pose did not blend back to CalmIdle.");
            Debug.Log($"SadIdle repeated three cycles in Edit Mode with at least {minimumMovement:F1}° of head motion per cycle and blended to CalmIdle.");
        }
        finally
        {
            graph.Destroy();
            Object.DestroyImmediate(preview);
        }
    }

    private static void VerifyRoutePair(FinalLabMetalDoor door, Vector3 inside, Vector3 hall, Vector3 talk, Collider mover = null)
    {
        var shut = door.Hinge.localRotation;
        door.Hinge.localRotation = Quaternion.Euler(0, door.SwingAngle, 0);
        Physics.SyncTransforms();
        try
        {
            VerifyRoute(FinalLabIvanCharacterController.RoundedRoute(inside, hall, talk));
            VerifyRoute(FinalLabIvanCharacterController.RoundedRoute(talk, hall, inside));
            Debug.Log("Ivan's rounded route clears the open door in both directions.");
        }
        finally
        {
            door.Hinge.localRotation = shut;
            Physics.SyncTransforms();
        }

        void VerifyRoute(Vector3[] route)
        {
            float largestStep = 0;
            Vector3 previous = Vector3.zero;
            for (int i = 1; i < route.Length; i++)
            {
                Vector3 from = route[i - 1];
                Vector3 move = route[i] - from;
                var hit = Physics.CapsuleCastAll(from + Vector3.up * .275f, from + Vector3.up * 1.55f, .243f,
                    move.normalized, move.magnitude, ~0, QueryTriggerInteraction.Ignore).FirstOrDefault(candidate => candidate.collider != mover);
                if (hit.collider != null) throw new InvalidOperationException($"Ivan's rounded route collides with {hit.collider.name} at {from:F3}.");
                largestStep = Mathf.Max(largestStep, Vector3.Angle(previous, move));
                previous = move;
            }
            if (largestStep > 7f) throw new InvalidOperationException($"Ivan's route still contains a sharp turn: {largestStep:F1}°.");
            Debug.Log($"Largest direction change between adjacent points on Ivan's route: {largestStep:F1}°.");
        }
    }

    private static FinalLabIvanDialogue BuildSubtitles(Transform parent)
    {
        var font = Load<TMP_FontAsset>(FinalLabEvidenceAssets.Root + "/Data/FinalLab_Cyrillic_SDF.asset");
        var material = Load<Material>(FinalLabEvidenceAssets.Root + "/Data/Dialogue Text.mat");
        var hosting = new GameObject("Ivan and Alexei subtitles", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(FinalLabIvanDialogue));
        hosting.transform.SetParent(parent, false);
        var canvas = hosting.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 23;
        canvas.enabled = false;
        var scaler = hosting.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .42f;
        const float alpha = DialoguePanelFrame.BackgroundAlpha;
        var frame = Image<DialoguePanelFrame>(hosting.transform, "Frame", new Color(.43f, .37f, .26f, alpha));
        frame.anchorMin = new Vector2(.2625f, 0);
        frame.anchorMax = new Vector2(.7375f, 0);
        frame.pivot = new Vector2(.5f, 0);
        frame.anchoredPosition = new Vector2(0, 35);
        frame.sizeDelta = new Vector2(0, DialoguePanelFrame.MinimumHeight);
        var background = Image<Image>(frame, "Dark backing", new Color(.025f, .027f, .023f, alpha));
        Stretch(background, new Vector2(1.25f, 1.25f), new Vector2(-1.25f, -1.25f));
        var speaker = Text(background, "Speaker", font, material, 17.5f, new Color(.92f, .80f, .58f));
        speaker.fontStyle = FontStyles.Bold;
        SetTextArea(speaker.rectTransform, 1, 1, -31.25f, -8.75f);
        var body = Text(background, "Dialogue text", font, material, 22.5f, new Color(.98f, .956f, .905f));
        body.enableAutoSizing = true;
        body.fontSizeMin = 17.5f;
        body.fontSizeMax = 22.5f;
        body.textWrappingMode = TextWrappingModes.Normal;
        SetTextArea(body.rectTransform, 0, 1, 23.75f, -31.25f);
        frame.GetComponent<DialoguePanelFrame>().Configure(body);
        var hint = Text(background, "Continue prompt", font, material, 15, new Color(.79f, .75f, .65f));
        hint.text = "ЛКМ — следующая реплика";
        hint.alignment = TextAlignmentOptions.TopRight;
        SetTextArea(hint.rectTransform, 0, 0, 6.25f, 23.75f);
        var dialogue = hosting.GetComponent<FinalLabIvanDialogue>();
        dialogue.Configure(Load<TextAsset>(Scripts + "FinalLabIvanDialogue.json"), canvas, speaker, body);
        return dialogue;
    }

    private static RectTransform Image<T>(Transform parent, string name, Color color) where T : Image
    {
        var node = new GameObject(name, typeof(RectTransform), typeof(T));
        node.transform.SetParent(parent, false);
        var image = node.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image.rectTransform;
    }

    private static TMP_Text Text(Transform parent, string name, TMP_FontAsset font, Material material, float size, Color color)
    {
        var node = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        node.transform.SetParent(parent, false);
        var label = node.GetComponent<TMP_Text>();
        label.font = font;
        label.fontSharedMaterial = material;
        label.fontSize = size;
        label.color = color;
        label.raycastTarget = false;
        label.richText = false;
        label.alignment = TextAlignmentOptions.TopLeft;
        return label;
    }

    private static void SetTextArea(RectTransform rect, float minY, float maxY, float bottom, float top)
    {
        rect.anchorMin = new Vector2(0f, minY);
        rect.anchorMax = new Vector2(1f, maxY);
        rect.offsetMin = new Vector2(20f, bottom);
        rect.offsetMax = new Vector2(-20f, top);
    }

    private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = min;
        rect.offsetMax = max;
    }

    private static Transform Marker(Transform parent, string name, Vector3 position)
    {
        var node = new GameObject(name).transform;
        node.SetParent(parent, false);
        node.position = position;
        return node;
    }

    private static Transform Bone(Transform[] bones, string name) => bones.Single(bone =>
        bone.name.StartsWith("ValveBiped.Bip01_" + name + "_0", StringComparison.Ordinal));

    private static void Validate(GameObject root, AnimationClip[] clips, FinalLabIvanDialogue subtitles)
    {
        var cutscene = root.GetComponent<FinalLabIvanCutsceneController>();
        var lines = subtitles.ReadLines();
        if (cutscene == null || cutscene.Door == null || cutscene.Ivan == null || cutscene.Dialogue != subtitles || lines.Length != 35 ||
            lines[0].animation != "CalmIdle" || lines[10].animation != "AngryArgument" || lines[24].animation != "SadIdle" || lines[32].animation != "CalmIdle" ||
            lines.Any(line => !subtitles.Body.font.HasCharacters(line.speaker + line.text)))
            throw new InvalidOperationException("The dialogue has invalid references, markers or fonts.");
        var animator = root.GetComponentsInChildren<Animator>(true).Single();
        if (!animator.runtimeAnimatorController.animationClips.ToHashSet().SetEquals(clips) || !animator.avatar.isValid || animator.applyRootMotion ||
            animator.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any(skin => skin.sharedMesh == null || skin.sharedMaterials.Any(material => material == null)) ||
            root.GetComponentsInChildren<Transform>(true).Any(transform => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) > 0))
            throw new InvalidOperationException("The saved cutscene is missing an animation, material or component.");
        Debug.Log("Ivan setup verified: one Animator and Avatar, five clips, 35 lines, cue indices 0/10/24/32, all subtitle glyphs.");
    }

    private static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path) ??
        throw new InvalidOperationException("Missing scene asset: " + path);
}
