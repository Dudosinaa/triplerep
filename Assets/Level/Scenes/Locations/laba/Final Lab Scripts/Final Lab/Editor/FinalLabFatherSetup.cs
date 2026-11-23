using System;
using System.Linq;
using System.Text.RegularExpressions;
using FinalLab.Investigation;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

internal static partial class FinalLabFatherSetup
{
    private const string Sources = "Assets/Art/Animations/Laba/Father/";
    private const string Scripts = "Assets/Level/Scenes/Locations/laba/Final Lab Scripts/Final Lab/";
    private static readonly string[] SourceNames = { "ОТец садиться на кровать", "отец встает с кровати", "отец стоит", "отец удивляеться", "Отец умирает" };
    private const string WakeSource = "Вэйкап отца";
    private const string ModelPath = Sources + "отец.glb";
    private static string JointName(Transform bone) => Regex.Replace(bone.name, @"_\d+(?:_\d+)?$", "");
    private static Transform Joint(Transform[] bones, string suffix) =>
        bones.FirstOrDefault(bone => JointName(bone).EndsWith(suffix, StringComparison.OrdinalIgnoreCase));

    public static void SurveyPolish()
    {
        FinalLabInvestigationSurvey.RequireBatch();
        var scene = EditorSceneManager.OpenScene(FinalLabBuilder.ScenePath);
        var roots = scene.GetRootGameObjects();
        var ivan = roots.Select(root => root.GetComponent<FinalLabIvanCutsceneController>()).Single(value => value != null);
        var flow = roots.Select(root => root.GetComponent<FinalLabFlowController>()).Single(value => value != null);
        var player = Object.FindFirstObjectByType<FirstPersonPlayer>();
        var eye = ivan.transform.Find("Staging marks/Alexei_ConversationEye");
        var sample = Object.Instantiate(ivan.Ivan.gameObject);
        var father = Object.Instantiate(flow.Father.Actor.gameObject);
        try
        {
            sample.transform.position = ivan.transform.Find("Staging marks/Ivan_DialoguePoint").position;
            var ivanActor = sample.GetComponent<FinalLabIvanCharacterController>();
            sample.transform.rotation = ivanActor.Direction(eye.position);
            var idle = ivanActor.Animator.runtimeAnimatorController.animationClips.Single(clip => clip.name == "Scientist_Calm_Stand");
            idle.SampleAnimation(ivanActor.Animator.gameObject, idle.length * .33f);
            Debug.Log($"POLISH IVAN: FPS local {player.GetComponentInChildren<Camera>().transform.localPosition:F3}, dialogue eye {eye.position:F3}, head {ivanActor.HeadPosition:F3}, turn={ivanActor.Animator.runtimeAnimatorController.animationClips.Single(clip => clip.name == "FinalLab_Ivan_ReturnTurn").length:F3}s");
            father.SetActive(true);
            var rig = father.GetComponentsInChildren<Transform>();
            var animator = father.GetComponent<Animator>();
            foreach (string label in new[] { "Father_InitialBedPose", "Father_StandingIdle", "Father_Death" })
            {
                var clip = animator.runtimeAnimatorController.animationClips.Single(candidate => candidate.name == label);
                clip.SampleAnimation(father, clip.length * .999f);
                Debug.Log($"POLISH {label}: {string.Join(", ", new[] { "_Head1", "_Spine2", "_Pelvis", "_L_Thigh", "_R_Thigh", "_L_Calf", "_R_Calf", "_L_Foot", "_R_Foot" }.Select(key => key + "=" + Joint(rig, key).position.ToString("F3")))}");
            }
            foreach (var skin in father.GetComponentsInChildren<SkinnedMeshRenderer>())
                Debug.Log($"POLISH SKIN: {skin.name}, {skin.sharedMesh.vertexCount} verts, {skin.sharedMesh.blendShapeCount} shapes, {skin.bones.Length} bones, {skin.bounds:F3}, {string.Join(", ", skin.sharedMaterials.Select(mat => mat.name))}");
        }
        finally { Object.DestroyImmediate(sample); Object.DestroyImmediate(father); }
    }

    public static void Apply()
    {
        FinalLabInvestigationSurvey.RequireBatch();
        var scene = EditorSceneManager.OpenScene(FinalLabBuilder.ScenePath);
        const string sequenceName = "Father encounter — final laboratory";
        var roots = scene.GetRootGameObjects();
        var previous = roots.SingleOrDefault(root => root.name == sequenceName);
        if (previous != null) Object.DestroyImmediate(previous);
        var all = scene.GetRootGameObjects().SelectMany(node => node.GetComponentsInChildren<Transform>(true)).ToArray();
        var player = all.Select(node => node.GetComponent<FirstPersonPlayer>()).Single(component => component != null);
        var manager = all.Select(node => node.GetComponent<FinalLabInvestigationManager>()).Single(component => component != null);
        var ivan = all.Select(node => node.GetComponent<FinalLabIvanCutsceneController>()).Single(component => component != null);
        var ivanEye = ivan.transform.Find("Staging marks/Alexei_ConversationEye");
        var ivanTalk = ivan.transform.Find("Staging marks/Ivan_DialoguePoint");
        var ivanEyePosition = ivanEye.position;
        ivanEyePosition.y = ivanTalk.position.y + FinalLabIvanCutsceneController.DialogueEyeHeight;
        ivanEye.position = ivanEyePosition;
        Vector3 towardsAlexei = Vector3.ProjectOnPlane(ivanEye.position - ivanTalk.position, Vector3.up);
        Vector3 towardsHall = Vector3.ProjectOnPlane(ivan.transform.Find("Staging marks/Ivan_Hall").position - ivanTalk.position, Vector3.up);
        float turnYaw = Vector3.SignedAngle(towardsAlexei, towardsHall, Vector3.up);
        float handoff = Enumerable.Range(820, 181).First(frame => ivan.Ivan.ReadyToWalk(turnYaw, frame / 1000f)) / 1000f;
        if (handoff > .92f) throw new InvalidOperationException("Ivan's turn still has a stationary tail before walking.");
        Debug.Log($"Ivan begins walking at {handoff:P1} of the authored turn with a .36-second blend and a synchronized start rate.");
        var door = all.Single(node => node.name == "FatherRoom_HeavyMetalDoor").GetComponent<FinalLabMetalDoor>();
        var camera = player.GetComponentInChildren<Camera>();
        var hud = manager.GetComponentInChildren<FinalLabInvestigationUI>();
        var clickHint = ivan.Dialogue.GetComponentsInChildren<TMP_Text>(true).Single(label => label.name == "Continue prompt");
        var bed = all.Single(node => node.name == "BedWithBedding");
        var bedBox = bed.GetComponentInChildren<BoxCollider>();
        var mattress = bed.GetComponentsInChildren<MeshRenderer>().Single(r => r.transform.name == "SM_Bed_01_S");
        var pillow = PillowFromBedding(bed);
        var floor = all.Single(node => node.name == "ContainmentFloor").GetComponent<Collider>().bounds.max.y;
        var center = bedBox.transform.TransformPoint(bedBox.center);
        bool xIsLong = bedBox.size.x > bedBox.size.z;
        Vector3 along = xIsLong ? bedBox.transform.right : bedBox.transform.forward;
        Vector3 aisle = xIsLong ? bedBox.transform.forward : bedBox.transform.right;
        if (Vector3.Dot(aisle, door.transform.position - center) < 0) aisle = -aisle;
        if (Vector3.Dot(along, door.transform.position - center) > 0) along = -along;
        float width = bedBox.transform.TransformVector(xIsLong ? Vector3.forward * bedBox.size.z : Vector3.right * bedBox.size.x).magnitude;
        Vector3 feet = new Vector3(center.x, floor, center.z) + aisle * (width * .5f + .36f);
        Vector3 entranceEye = door.transform.position + Vector3.back * .36f + Vector3.up * 1.44f;
        Vector3 roomEye = door.transform.position + Vector3.forward * 1.48f + Vector3.up * 1.80f;
        Vector3 talkEye = feet + aisle * 1.46f + Vector3.up * 1.68f - along * .14f;
        Debug.Log($"Father geometry: bed={center:F3}, width={width:F3}, aisle={aisle:F3}, along={along:F3}, feet={feet:F3}, eye={talkEye:F3}");

        var root = new GameObject(sequenceName);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        var actor = all.FirstOrDefault(t => t.gameObject == PrefabUtility.GetNearestPrefabInstanceRoot(t.gameObject) &&
            PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject) == ModelPath)?.gameObject;
        if (actor == null) actor = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        actor.name = "Father — original ValveBiped model";
        actor.transform.SetParent(root.transform, false);
        actor.transform.SetPositionAndRotation(feet, Quaternion.LookRotation(aisle, Vector3.up));
        actor.transform.localScale = Vector3.one * ActorScale;
        actor.SetActive(true);
        foreach (var animation in actor.GetComponentsInChildren<Animation>(true)) animation.enabled = false;
        var rig = new PoseRig(actor, useSceneRest: true);

        if (!AssetDatabase.IsValidFolder(Generated.TrimEnd('/'))) AssetDatabase.CreateFolder(Scripts.TrimEnd('/'), "Father Sequence");
        AssignWakeMaterials();
        var stand = Transfer(rig, SourceNames[1], feet, floor, true);
        var idle = Transfer(rig, SourceNames[2], feet, floor, false);
        var surprise = Transfer(rig, SourceNames[3], feet, floor, false);
        var sit = BuildWakeUp(rig, stand[0], feet, floor, bedBox, bed, pillow, aisle, along, mattress.bounds.max.y);
        stand[0].Apply(rig);
        Debug.Log($"Father seated: pelvis={rig.Hips.position:F3}, feet={rig.Feet:F3}, mattress={mattress.bounds.max.y:F3}.");
        var closed = door.Hinge.localRotation;
        door.Hinge.localRotation = Quaternion.Euler(0, door.SwingAngle, 0);
        FatherPose[] death;
        try { death = StageDeath(rig, feet, floor, bedBox, all); }
        finally
        {
            door.Hinge.localRotation = closed;
            Physics.SyncTransforms();
        }
        CorrectFinalHem(rig, death[^1], floor);
        var clips = new[]
        {
            (FatherCutsceneController.InitialBedPose, SaveMotion(rig, FatherCutsceneController.InitialBedPose, new[] { sit[0], sit[0] })),
            (FatherCutsceneController.SitUp, SaveMotion(rig, FatherCutsceneController.SitUp, sit)),
            (FatherCutsceneController.StandUp, SaveMotion(rig, FatherCutsceneController.StandUp, stand)),
            (FatherCutsceneController.Surprised, SaveMotion(rig, FatherCutsceneController.Surprised, surprise)),
            (FatherCutsceneController.StandingIdle, SaveMotion(rig, FatherCutsceneController.StandingIdle, idle, true)),
            (FatherCutsceneController.Death, SaveMotion(rig, FatherCutsceneController.Death, death))
        };
        sit[0].Apply(rig);
        // Save the authored starting pose in the scene as well as in the controller.
        foreach (var bone in rig.Tracks) PrefabUtility.RecordPrefabInstancePropertyModifications(bone);
        var animator = actor.GetComponent<Animator>();
        if (animator == null) animator = actor.AddComponent<Animator>();
        var avatar = AvatarBuilder.BuildGenericAvatar(actor, rig.Hips.name);
        if (avatar == null || !avatar.isValid || avatar.isHuman) throw new InvalidOperationException("Could not create the father Generic Avatar.");
        avatar.name = "Father_ValveBiped";
        animator.avatar = Save(avatar, Generated + "Father_ValveBiped.asset");
        animator.runtimeAnimatorController = BuildFatherAnimator(clips);
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.applyRootMotion = false;
        BakeDeathBlendSupport(rig, animator, clips[^1].Item2, death, floor);
        VerifyPolishedStaging(rig, door, entranceEye, roomEye, talkEye, feet, floor, clips[0].Item2, clips[^1].Item2);
        var flow = root.AddComponent<FinalLabFlowController>();
        var sequence = root.AddComponent<FatherCutsceneController>();
        sequence.Configure(player, camera, manager, hud, ivan.Dialogue, clickHint, animator, rig.Head, rig.Chest, rig.Hips,
            Point(root.transform, "Hall approach eye", entranceEye, center), Point(root.transform, "Doorway eye", roomEye, center),
            Point(root.transform, "Conversation eye", talkEye, feet), Curtain(root.transform), Transcript());
        flow.Configure(ivan, door, sequence, hud, Marker(root.transform, door));
        actor.SetActive(false);
        door.SetLocked(true);
        EditorUtility.SetDirty(door);
        ValidateSetup(flow, sequence, clips, camera);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the father encounter in FinalScene.");
        Debug.Log("FINAL_LAB_FATHER_SCENE_SAVED");
    }

    private static void VerifyPolishedStaging(PoseRig rig, FinalLabMetalDoor door, Vector3 hall, Vector3 bedEye, Vector3 talkEye,
        Vector3 feet, float floor, AnimationClip rest, AnimationClip death)
    {
        var lensObject = new GameObject("Cutscene framing verification");
        var lens = lensObject.AddComponent<Camera>();
        lens.enabled = false;
        lens.aspect = 16f / 9f;
        var previous = door.Hinge.localRotation;
        try
        {
            door.Hinge.localRotation = Quaternion.Euler(0, door.SwingAngle, 0);
            Physics.SyncTransforms();
            Vector3 flight = bedEye - hall;
            if (Physics.SphereCast(hall, .09f, flight.normalized, out var obstruction, flight.magnitude, ~0, QueryTriggerInteraction.Ignore))
                throw new InvalidOperationException("The bed camera flight crosses " + obstruction.collider.name);
            rest.SampleAnimation(rig.Model, 0);
            Frame(bedEye, Vector3.Lerp(rig.Hips.position, rig.Head.position, .18f), 62f, "bed", rig.Head, rig.LeftFoot, rig.RightFoot,
                rig.BoneOf("L_Hand"), rig.BoneOf("R_Hand"));
            death.SampleAnimation(rig.Model, death.length);
            Vector3 direction = Vector3.ProjectOnPlane(feet - talkEye, Vector3.up).normalized;
            Vector3 bodyEye = talkEye - direction * .36f + Vector3.down * .45f;
            Frame(bodyEye, Vector3.Lerp(rig.Hips.position, rig.Head.position, .48f), 64f, "floor", rig.Head, rig.LeftFoot, rig.RightFoot);
            float knee = Mathf.Max(rig.BoneOf("L_Calf").position.y, rig.BoneOf("R_Calf").position.y) - floor;
            float separation = Vector3.Distance(rig.LeftFoot.position, rig.RightFoot.position);
            if (knee > .35f || separation < .21f) throw new InvalidOperationException($"Unnatural floor pose: knee height {knee:F3}, ankle distance {separation:F3}.");
            Debug.Log($"Polished framing: bed eye {bedEye.y - floor:F2} m, standing eye {talkEye.y - floor:F2} m, floor eye {bodyEye.y - floor:F2} m; settled knees {knee:F2} m, ankles apart {separation:F2} m.");
        }
        finally
        {
            door.Hinge.localRotation = previous;
            Physics.SyncTransforms();
            Object.DestroyImmediate(lensObject);
            rest.SampleAnimation(rig.Model, 0);
            var coat = rig.Skins.SingleOrDefault(skin => skin.sharedMesh.GetBlendShapeIndex(FinalHemShape) >= 0);
            if (coat != null) coat.SetBlendShapeWeight(coat.sharedMesh.GetBlendShapeIndex(FinalHemShape), 0);
        }

        void Frame(Vector3 eye, Vector3 target, float fov, string shot, params Transform[] landmarks)
        {
            lens.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(target - eye, Vector3.up));
            lens.fieldOfView = fov;
            foreach (var landmark in landmarks)
            {
                Vector3 vp = lens.WorldToViewportPoint(landmark.position);
                if (vp.z <= 0f || vp.x < .025f || vp.x > .975f || vp.y < .055f || vp.y > .96f)
                    throw new InvalidOperationException($"The {shot} camera crops {landmark.name}: {vp:F3}.");
            }
            Debug.Log($"Polished {shot} shot covers {landmarks.Length} body landmarks at 16:9.");
        }
    }

    private static AnimatorController BuildFatherAnimator((string state, AnimationClip clip)[] clips)
    {
        string path = Generated + "FatherFinalScene.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path) ?? AnimatorController.CreateAnimatorControllerAtPath(path);
        if (controller.layers.Length == 0) controller.AddLayer("Base Layer");
        var machine = controller.layers.Single().stateMachine;
        for (int i = 0; i < clips.Length; i++)
        {
            var (name, clip) = clips[i];
            var state = machine.states.Select(item => item.state).SingleOrDefault(item => item.name == name) ??
                machine.AddState(name, new Vector3(230 + i % 2 * 210, 70 + i / 2 * 75));
            state.motion = clip;
            state.speed = name switch
            {
                FatherCutsceneController.SitUp => FatherCutsceneController.WakeSpeed,
                FatherCutsceneController.StandUp => FatherCutsceneController.RiseSpeed,
                _ => 1f
            };
            if (i == 0) machine.defaultState = state;
            EditorUtility.SetDirty(state);
        }
        EditorUtility.SetDirty(machine);
        return controller;
    }

    private static Transform Point(Transform parent, string name, Vector3 position, Vector3 focus)
    {
        var point = new GameObject(name).transform;
        point.SetParent(parent, false);
        point.SetPositionAndRotation(position, Quaternion.LookRotation(Vector3.ProjectOnPlane(focus - position, Vector3.up), Vector3.up));
        return point;
    }

    private static GameObject Marker(Transform parent, FinalLabMetalDoor door)
    {
        var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        marker.name = "Father door marker";
        marker.transform.SetParent(parent, false);
        marker.transform.SetPositionAndRotation(door.transform.position + new Vector3(-.20f, 1.73f, -.28f), Quaternion.Euler(0, 0, 45));
        marker.transform.localScale = new Vector3(.057f, .057f, .016f);
        Object.DestroyImmediate(marker.GetComponent<Collider>());
        var material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "Father_DoorMarker" };
        material.SetColor("_BaseColor", new Color(.94f, .72f, .31f));
        marker.GetComponent<MeshRenderer>().sharedMaterial = Save(material, Generated + "Father_DoorMarker.mat");
        marker.SetActive(false);
        return marker;
    }

    private static CanvasGroup Curtain(Transform parent)
    {
        var screen = new GameObject("Father final fade", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        screen.transform.SetParent(parent, false);
        var canvas = screen.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;
        var image = MakeImage(screen.transform, "Black", Color.black);
        image.rectTransform.anchorMin = Vector2.zero;
        image.rectTransform.anchorMax = Vector2.one;
        image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
        var group = screen.GetComponent<CanvasGroup>();
        group.alpha = 0;
        group.blocksRaycasts = false;
        group.interactable = false;
        return group;
    }

    private static Image MakeImage(Transform parent, string name, Color color)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false);
        var image = obj.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static FatherCutsceneController.Line[] Transcript() => new FatherCutsceneController.Line[]
    {
        new("ОТЕЦ", "Что?.. Где я?.. Сколько времени прошло?.."),
        new("ОТЕЦ", "Алёша?.. Это ты?.. Алёша, неужели это правда ты?.."),
        new("АЛЕКСЕЙ", "Папа?.. Это я. Я здесь. Мы столько лет тебя искали... Я думал, что больше никогда тебя не увижу"),
        new("ОТЕЦ", "Я слышал, как ты звал меня... даже там. Но каждый раз боялся, что это опять не ты"),
        new("АЛЕКСЕЙ", "Всё закончилось. Мы выберемся отсюда, слышишь? Я помогу тебе. Теперь всё будет хорошо"),
        new("ОТЕЦ", "Нет, сынок... Они вернули только моё тело. А что-то внутри меня так и осталось по ту сторону"),
        new("АЛЕКСЕЙ", "Не говори так. Пожалуйста... Просто дай мне руку. Мы ведь только нашли друг друга"),
        new("ОТЕЦ", "Прости меня, Алёша... Я должен был вернуться к тебе намного раньше. Я так тебя люблю, сынок..."),
        new("АЛЕКСЕЙ", "Папа?.. Папа, нет! Посмотри на меня!.."),
        new("ИВАН", "Нет, папа... Алексей, скажи, что он дышит!")
    };

    private static void ValidateSetup(FinalLabFlowController flow, FatherCutsceneController father,
        (string state, AnimationClip clip)[] clips, Camera camera)
    {
        if (!flow.SkipGameplayForTesting || flow.DoorMarkerVisible || !flow.FatherDoor.IsLocked || father.Actor.gameObject.activeSelf ||
            !father.Actor.avatar.isValid || father.Actor.applyRootMotion || father.Lines.Count != 10 ||
            camera.GetComponent<AudioListener>() == null || Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length != 1)
            throw new InvalidOperationException("The father sequence has invalid scene references.");
        if (father.Lines.Any(line => !father.Subtitles.Body.font.HasCharacters(line.speaker + line.text)))
            throw new InvalidOperationException("Father subtitles contain glyphs unavailable in the shared font.");
        foreach (var (state, clip) in clips)
            if (clip.length <= 0 || clip.isLooping != (state == FatherCutsceneController.StandingIdle) ||
                state == FatherCutsceneController.SitUp && Mathf.Abs(clip.length - SourceClip(WakeSource).length) > 1f / FrameRate ||
                AnimationUtility.GetCurveBindings(clip).Any(binding => father.Actor.transform.Find(binding.path) == null))
                throw new InvalidOperationException("Invalid father animation bindings: " + state);
        if (father.Actor.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any(renderer => renderer.sharedMesh == null ||
            renderer.sharedMaterials.Any(material => material == null || material.shader == null || material.shader.name == "Hidden/InternalErrorShader")))
            throw new InvalidOperationException("The original father model has an unresolved mesh or material.");
        Debug.Log("Father references, ValveBiped Avatar, six poses, Cyrillic, original materials and single AudioListener validated.");
    }

    private static Vector3 PillowFromBedding(Transform bed)
    {
        var bedding = bed.GetComponentsInChildren<MeshFilter>().Single(mesh => mesh.name == "SM_BedBedding_01");
        var highest = bedding.sharedMesh.vertices.Select(vertex => bedding.transform.TransformPoint(vertex)).OrderByDescending(vertex => vertex.y).Take(30).ToArray();
        return highest.Aggregate(Vector3.zero, (sum, value) => sum + value) / highest.Length;
    }

    private static void AssignWakeMaterials()
    {
        string path = Sources + WakeSource + ".fbx";
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var original = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath).GetComponentsInChildren<SkinnedMeshRenderer>(true)
            .SelectMany(renderer => renderer.sharedMaterials).Distinct().ToDictionary(material => material.name, StringComparer.Ordinal);
        bool changed = false;
        foreach (var slot in model.GetComponentsInChildren<SkinnedMeshRenderer>(true).SelectMany(renderer => renderer.sharedMaterials).Distinct())
        {
            if (!original.TryGetValue(slot.name, out var textured)) throw new InvalidOperationException("No original father texture for " + slot.name);
            if (slot == textured) continue;
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), slot.name), textured);
            changed = true;
        }
        if (changed) importer.SaveAndReimport();
        var reloaded = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<SkinnedMeshRenderer>(true)
            .SelectMany(renderer => renderer.sharedMaterials).Distinct().ToArray();
        if (reloaded.Length != original.Count || reloaded.Any(material => !original.ContainsValue(material) ||
            !material.GetTexturePropertyNames().Any(property => material.GetTexture(property) != null)))
            throw new InvalidOperationException("Wake Up did not retain the original textured material slots after import.");
        Debug.Log($"Wake Up: all {reloaded.Length} source preview materials remapped to the original textured father model.");
    }

    public static void Survey()
    {
        FinalLabInvestigationSurvey.RequireBatch();
        var scene = EditorSceneManager.OpenScene(FinalLabBuilder.ScenePath);
        var all = scene.GetRootGameObjects().SelectMany(node => node.GetComponentsInChildren<Transform>(true)).ToArray();
        var bed = all.Single(t => t.name == "BedWithBedding");
        Debug.Log("FATHER EXISTING MODEL | " + string.Join(", ", all.Where(t => PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject) == ModelPath).Select(t => t.name).Take(5)));
        foreach (var t in all.Where(t => t.name == "FatherRoom_HeavyMetalDoor" || t.name == "FatherRoom_Observation" ||
                     t.IsChildOf(bed) || t.name.Contains("MainFloor") || (t.position.x > .3f && t.position.z > 14 && t.position.z < 19 &&
                     t.TryGetComponent<BoxCollider>(out var box) && box.bounds.size.magnitude >= .30f)))
        {
            var col = t.GetComponent<Collider>();
            var render = t.GetComponent<Renderer>();
            Debug.Log($"FATHER SCENE | {t.name} p={t.position:F3} rot={t.eulerAngles:F1} coll={(col != null ? col.bounds.ToString("F3") : "")} render={(render != null ? render.bounds.ToString("F3") : "")}");
        }
        Debug.Log($"FATHER PILLOW | center={PillowFromBedding(bed):F3}");
        foreach (string path in SourceNames.Append(WakeSource).Select(name => Sources + name + ".fbx").Prepend(ModelPath))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
            if (prefab == null) { Debug.Log("FATHER SOURCE missing prefab " + path); continue; }
            var instance = Object.Instantiate(prefab);
            try
            {
                var bones = instance.GetComponentsInChildren<Transform>(true);
                var skins = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var animator = instance.GetComponentInChildren<Animator>();
                var avatar = animator != null ? animator.avatar : null;
                Debug.Log($"FATHER SOURCE | {path} clips={clips.Length} nodes={bones.Length} skins={skins.Length} avatar={(avatar != null ? avatar.isValid + "/" + avatar.isHuman : "none")} " +
                    string.Join(" ", skins.Select(s => s.name + "/" + s.sharedMesh.vertexCount + "/" + string.Join(",", s.sharedMaterials.Select(m => m != null ? m.shader.name : "missing")))));
                foreach (var material in skins.SelectMany(s => s.sharedMaterials).Where(m => m != null).Distinct())
                    Debug.Log($"FATHER MATERIAL | {material.name} -> {string.Join(", ", material.GetTexturePropertyNames().Where(property => material.GetTexture(property) != null).Select(property => property + ":" + material.GetTexture(property).name))}");
                Debug.Log("FATHER JOINTS | " + string.Join(" ", bones.Select(t => t.name).Take(30)));
                var joints = new[] { "_Pelvis", "_Head1", "_L_Thigh", "_R_Thigh", "_L_Foot", "_R_Foot", "_L_Hand", "_R_Hand" }.Select(s => Joint(bones, s)).ToArray();
                if (joints.Any(t => t == null)) continue;
                void Pose(string phase) => Debug.Log($"FATHER POSE {phase} | " + string.Join(" ", joints.Select(t => $"{JointName(t)}={t.position:F3}")) +
                    $" face={Vector3.Cross(joints[3].position - joints[2].position, joints[1].position - joints[0].position).normalized:F2}");
                Pose("Bind");
                foreach (var clip in clips)
                {
                    var settings = AnimationUtility.GetAnimationClipSettings(clip);
                    var bindings = AnimationUtility.GetCurveBindings(clip);
                    Debug.Log($"FATHER CLIP | {clip.name} length={clip.length:F3} rate={clip.frameRate} human={clip.humanMotion} loop={settings.loopTime} curves={bindings.Length} first={string.Join(" | ", bindings.Take(3).Select(b => b.path + "/" + b.propertyName))}");
                    for (int i = 0; i <= 8; i++)
                    {
                        clip.SampleAnimation(instance, clip.length * i / 8);
                        Pose(i + "/8");
                    }
                }
            }
            finally { Object.DestroyImmediate(instance); }
        }
    }
}
