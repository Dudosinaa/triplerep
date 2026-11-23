using System;
using System.Collections.Generic;
using System.Linq;
using FinalLab.Investigation;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

internal static class FinalLabEndingSetup
{
    private const string LabPath = "Assets/Level/Scenes/Locations/laba/FinalScene.unity";
    private const string AlexPath = "Assets/Art/Animations/Laba/Alex/";
    private const string ScriptPath = "Assets/Level/Scenes/Locations/laba/Final Lab Scripts/Final Lab/";
    private const string AssetsPath = ScriptPath + "Ending Assets/";
    private const float Scale = .82f;
    private const float ContactCell = .06f;

    public static void Apply()
    {
        if (!AssetDatabase.IsValidFolder(AssetsPath.TrimEnd('/'))) AssetDatabase.CreateFolder(ScriptPath.TrimEnd('/'), "Ending Assets");
        string[] names = { "Алексей встает в вагоне", "Алексей зол на Ивана(диалог в вагоне)",
            "Алексей тычет пльцом на Ивана", "Алексей отмахивается от Ивана",
            "Алексей отворачивается от Ивана", "Алексей потерял сознание" };
        var clips = names.Select((name, i) => Import(name, i)).ToArray();
        var modelPath = AlexPath + "Alexey.fbx";
        var modelImporter = (ModelImporter)AssetImporter.GetAtPath(modelPath);
        Require(modelImporter != null, "Missing Alexey.fbx.");
        if (modelImporter.animationType != ModelImporterAnimationType.Human || modelImporter.importAnimation)
        {
            modelImporter.animationType = ModelImporterAnimationType.Human;
            modelImporter.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            modelImporter.importAnimation = false;
            modelImporter.SaveAndReimport();
        }
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        var avatar = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().Single(a => a.isHuman && a.isValid);
        var trainController = Controller("Alexei_Train", new[] { TrainFinale.Rise, TrainFinale.Angry, TrainFinale.Point,
            TrainFinale.Dismiss, TrainFinale.Turn }, clips);
        var rescueController = Controller("Alexei_Rescue", new[] { "Unconscious" }, new[] { clips[5] });
        AssetDatabase.SaveAssets();

        var lab = EditorSceneManager.OpenScene(LabPath);
        var flow = InScene<FinalLabFlowController>(lab).Single();
        var font = flow.Father.Subtitles.Body.font;
        var train = EditorSceneManager.OpenScene(TrainFinale.ScenePath, OpenSceneMode.Additive);
        SceneManager.SetActiveScene(train);
        InstallTrain(train, flow.Father.Subtitles, font, model, avatar, trainController, clips);
        SceneManager.SetActiveScene(lab);
        InstallRescue(lab, flow, font, model, avatar, rescueController, clips[5]);

        var scenes = EditorBuildSettings.scenes;
        Require(scenes.Any(s => s.path == TrainFinale.ScenePath && s.enabled), "Train must be in the build list.");
        if (!scenes.Any(s => s.path == LabPath))
            EditorBuildSettings.scenes = scenes.Append(new EditorBuildSettingsScene(LabPath, true)).ToArray();

        Require(EditorSceneManager.SaveScene(train) && EditorSceneManager.SaveScene(lab), "Save both existing scenes.");
        AssetDatabase.SaveAssets();
        VerifySaved();
    }

    private static AnimationClip Import(string name, int index)
    {
        string path = AlexPath + name + ".fbx";
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        Require(importer != null, "Missing animation " + name);
        var clip = importer.defaultClipAnimations.Single();
        clip.loopTime = clip.loopPose = index == 1;
        clip.lockRootPositionXZ = clip.lockRootHeightY = clip.lockRootRotation = true;
        clip.keepOriginalOrientation = true;
        var imported = importer.clipAnimations.FirstOrDefault();
        if (importer.animationType != ModelImporterAnimationType.Human || imported == null ||
            imported.firstFrame != clip.firstFrame || imported.lastFrame != clip.lastFrame ||
            imported.loopTime != clip.loopTime || imported.loopPose != clip.loopPose ||
            !imported.lockRootPositionXZ || !imported.lockRootHeightY || !imported.lockRootRotation)
        {
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.clipAnimations = new[] { clip };
            importer.SaveAndReimport();
        }

        var assets = AssetDatabase.LoadAllAssetsAtPath(path);
        Require(assets.OfType<Avatar>().Any(a => a.isValid && a.isHuman), name + " needs a Humanoid Avatar.");
        var motion = assets.OfType<AnimationClip>().Single(c => !c.name.StartsWith("__", StringComparison.Ordinal));
        Require(motion.length > .5f && motion.humanMotion && motion.isLooping == (index == 1), "Invalid clip: " + name);
        Debug.Log($"FINALE clip {name}: {motion.length:F2} s, loop {motion.isLooping}.");
        return motion;
    }

    private static AnimatorController Controller(string name, string[] states, AnimationClip[] clips)
    {
        var path = AssetsPath + name + ".controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path) ?? AnimatorController.CreateAnimatorControllerAtPath(path);
        if (controller.layers.Length == 0) controller.AddLayer("Base Layer");
        var machine = controller.layers[0].stateMachine;
        for (int i = 0; i < states.Length; i++)
        {
            var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == states[i]) ??
                machine.AddState(states[i], new Vector3(200 + (i % 2) * 205, 75 + (i / 2) * 72, 0));
            state.motion = clips[i];
            if (i == 0) machine.defaultState = state;
            EditorUtility.SetDirty(state);
        }
        EditorUtility.SetDirty(machine);
        return controller;
    }

    private static Animator Actor(Transform parent, GameObject model, Avatar avatar, AnimatorController controller)
    {
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
        instance.name = "Alexei Karamazov";
        instance.transform.localScale = Vector3.one * Scale;
        foreach (var component in instance.GetComponentsInChildren<Camera>(true)) component.enabled = false;
        foreach (var component in instance.GetComponentsInChildren<Light>(true)) component.enabled = false;
        var actor = instance.GetComponent<Animator>();
        actor.avatar = avatar;
        actor.runtimeAnimatorController = controller;
        actor.applyRootMotion = false;
        actor.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var materials = actor.GetComponentsInChildren<SkinnedMeshRenderer>(true).SelectMany(r => r.sharedMaterials).Distinct().ToArray();
        Require(materials.Length > 0 && materials.All(m => m != null && m.shader != null && m.shader.isSupported &&
            m.shader.name != "Hidden/InternalErrorShader"), "Alexei has unsupported materials.");
        Debug.Log("FINALE material maps: " + string.Join(", ", materials.Select(m =>
            m.name + "/" + m.shader.name + "/" + (m.mainTexture != null ? m.mainTexture.name : "constant"))));
        return actor;
    }

    private static void InstallTrain(Scene train, FinalLabIvanDialogue original, TMP_FontAsset font, GameObject model,
        Avatar avatar, AnimatorController controller, AnimationClip[] clips)
    {
        var root = Root(train, "Train finale");
        var trainFinale = root.AddComponent<TrainFinale>();
        var presentation = Node(root.transform, "Finale presentation");
        var player = InScene<FirstPersonPlayer>(train).Single();
        var view = player.GetComponentInChildren<Camera>();
        var trainGeometry = train.GetRootGameObjects().Single(o => o.name == "Train_Unity");
        var geometry = trainGeometry.GetComponentsInChildren<Renderer>(true);
        float aisle = view.transform.position.x;
        float end = player.transform.position.z;
        var cushions = geometry.Where(r => r.name.StartsWith("CojinBajoAsiento", StringComparison.Ordinal))
            .Select(r => r.bounds).ToArray();
        var left = cushions.Where(b => b.center.x < aisle - .55f).ToArray();
        var right = cushions.Where(b => b.center.x > aisle + .55f).ToArray();
        Debug.Log($"FINALE Train cushion samples: {string.Join(" ", cushions.Take(10).Select(b => b.center.ToString("F2")))}; left {left.Length}, right {right.Length}");
        var pairs = from a in left from b in right
            orderby Mathf.Abs(a.center.z - end) + Mathf.Abs(a.center.z - b.center.z) * 2
            select (a, b);
        // The prefab can combine an entire bench into one renderer. Its seated NPC marks identify the individual positions.
        var seats = pairs.FirstOrDefault(p => Mathf.Abs(p.a.center.z - end) < 2.5f && Mathf.Abs(p.a.center.z - p.b.center.z) < .65f);
        if (seats.a.size == Vector3.zero)
        {
            var marks = train.GetRootGameObjects().Where(o => o.name.StartsWith("NPC_"))
                .Select(o => o.transform.position).ToArray();
            Vector3 a = marks.Where(p => p.x < aisle).OrderBy(p => Mathf.Abs(p.z - end)).First();
            Vector3 b = marks.Where(p => p.x > aisle).OrderBy(p => Mathf.Abs(p.z - a.z)).First();
            seats = (new Bounds(new Vector3(a.x, player.transform.position.y + .44f, a.z), new Vector3(.64f, .08f, .60f)),
                new Bounds(new Vector3(b.x, player.transform.position.y + .44f, b.z), new Vector3(.64f, .08f, .60f)));
        }
        float lane = seats.a.center.z;
        var tiles = geometry.Where(r => r.name.StartsWith("SueloInterior_", StringComparison.Ordinal))
            .Select(r => r.bounds).Where(b => b.min.z <= lane && b.max.z >= lane && aisle >= b.min.x && aisle <= b.max.x)
            .ToArray();
        Physics.SyncTransforms();
        float floor = tiles.Length > 0 ? tiles.Max(b => b.max.y) : player.transform.position.y;
        if (Physics.Raycast(new Vector3(aisle, floor + .27f, lane), Vector3.down, out RaycastHit tile, .65f,
                ~0, QueryTriggerInteraction.Ignore) && tile.collider.transform.IsChildOf(trainGeometry.transform))
            floor = tile.point.y;

        var actor = Actor(presentation, model, avatar, controller);
        var seatEye = new Vector3(seats.b.center.x - .26f, floor + 1.23f, seats.b.center.z + .11f);
        var upEye = new Vector3(seatEye.x, 0f, seatEye.z);
        var actorPosition = new Vector3(aisle + .18f, 0f, lane - 1.30f);
        actor.transform.rotation = Quaternion.LookRotation(upEye - actorPosition);
        var initial = Sample(actor, clips[0], 0f);
        var standing = Sample(actor, clips[0], clips[0].length);
        var dialogueStart = Sample(actor, clips[1], 0f);
        var standingOffset = Vector3.ProjectOnPlane(standing.hips - dialogueStart.hips, Vector3.up);
        actorPosition.y = floor - standing.bounds.min.y + .012f;
        actor.transform.position = actorPosition;
        var target = actorPosition + initial.head;
        Require(target.y < floor + .48f && actorPosition.y + initial.hips.y < floor + .40f,
            $"Alexei must open in a lying pose on the aisle floor. Initial head height above floor: {target.y - floor:F3}.");
        var seatedEye = Point(presentation, "Ivan seated eye", seatEye, target);
        upEye.y = actorPosition.y + standing.head.y + .13f;
        var standingEye = Point(presentation, "Ivan standing eye", upEye, actorPosition + standing.head);
        Require(Vector3.Distance(standingEye.position, actorPosition + standing.head) > 1f, "The Train face framing is too close.");
        var before = Sample(actor, clips[3], clips[3].length);
        var after = Sample(actor, clips[4], clips[4].length);
        Require(Vector3.Dot(before.across, after.across) < -.75f, "The authored final turn must end facing away.");
        foreach (var clip in clips.Take(5))
            foreach (float n in new[] { 0f, .25f, .5f, .75f, 1f })
            {
                actor.transform.position = clip == clips[0] ? actorPosition : actorPosition + standingOffset;
                var body = Sample(actor, clip, n * clip.length);
                Require(body.bounds.min.y > floor - .13f, $"Train floor support failed for {clip.name} at {n} ({body.bounds.min.y:F2}/{floor:F2}).");
                Require(body.bounds.min.x > seats.a.min.x - .16f && body.bounds.max.x < seats.b.max.x + .16f,
                    $"Alexei exceeds the carriage lateral clearance in {AssetDatabase.GetAssetPath(clip)} at {n}: {body.bounds}.");
            }
        actor.transform.position = actorPosition;
        Debug.Log($"FINALE Train staging: floor {floor:F3}; seats {seats.a.center:F3} / {seats.b.center:F3}; camera {seatedEye.position:F3} -> {standingEye.position:F3}; motion root {actorPosition:F3}.");

        var subtitles = Object.Instantiate(original.gameObject, presentation).GetComponent<FinalLabIvanDialogue>();
        subtitles.name = "Train finale subtitles";
        var transcript = AssetDatabase.LoadAssetAtPath<TextAsset>(ScriptPath + "TrainFinaleDialogue.json");
        subtitles.Configure(transcript, subtitles.GetComponent<Canvas>(), subtitles.Speaker, subtitles.Body);
        subtitles.GetComponentsInChildren<TMP_Text>(true).Single(t => t.name == "Continue prompt").text = "ЛКМ — продолжить";
        subtitles.Hide();

        var overlay = Canvas(presentation, "Ending overlay", 91);
        var curtain = Fullscreen(overlay, "Load fade", Color.black).gameObject.AddComponent<CanvasGroup>();
        curtain.alpha = 1f;
        var vignette = Fullscreen(overlay, "Closing vignette", Color.white);
        string materialPath = AssetsPath + "Ending_Vignette.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        var shader = Shader.Find("FinalLab/Ending Vignette");
        Require(shader != null && shader.isSupported && !ShaderUtil.ShaderHasError(shader), "Ending vignette shader failed.");
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, materialPath);
        }
        vignette.material = material;
        vignette.enabled = false;
        var title = Label(overlay, font, "СВЯЗЬ РАЗОРВАНА", 78, new Vector2(.5f, .5f), Vector2.zero);
        title.rectTransform.sizeDelta = new Vector2(1150, 130);
        title.fontStyle = FontStyles.Bold;
        title.alpha = 0f;

        var ordinary = train.GetRootGameObjects().Where(o => o == player.gameObject || o.name.StartsWith("NPC_") ||
            o.GetComponent<LocationTransitionZone>() != null).ToArray();
        var controls = player.GetComponentsInChildren<Behaviour>(true)
            .Where(b => b is FirstPersonPlayer or DocumentReader or ItemInspector or DialogueController or PistolController
                or SceneSwitch or PlayerStress or SmokingPromptUI or SmokingController or InteractionHints)
            .Concat(trainGeometry.GetComponentsInChildren<MetroTrainArrival>(true))
            .Concat(InScene<Camera>(train).Where(c => c != view))
            .Concat(InScene<AudioListener>(train).Where(l => l.gameObject != view.gameObject)).ToArray();

        var setup = new SerializedObject(trainFinale);
        Set(setup, "ordinaryGameplay", ordinary);
        Set(setup, "ordinaryControls", controls);
        setup.FindProperty("standingOffset").vector3Value = standingOffset;
        Set(setup, ("presentation", presentation.gameObject), ("view", view), ("alexei", actor), ("seatedEye", seatedEye),
            ("standingEye", standingEye), ("subtitles", subtitles), ("curtain", curtain), ("vignette", vignette), ("title", title));
        presentation.gameObject.SetActive(false);
    }

    private static void InstallRescue(Scene scene, FinalLabFlowController flow, TMP_FontAsset font,
        GameObject model, Avatar avatar, AnimatorController controller, AnimationClip clip)
    {
        var root = Root(scene, "Ivan rescues Alexei").transform;
        var rescue = root.gameObject.AddComponent<FinalLabAlexeiRescue>();
        var player = InScene<FirstPersonPlayer>(scene).Single();
        var resonator = InScene<Transform>(scene).Single(t => t.name == "K17_Resonator_Root");
        var roomFloor = InScene<BoxCollider>(scene).Single(c => c.name == "ContainmentFloor").bounds;
        var mainFloor = InScene<BoxCollider>(scene).Single(c => c.name == "MainFloor").bounds;
        var threshold = flow.FatherDoor.Hinge.position;
        var doorThreshold = Point(root, "Inside father room", threshold, threshold + Vector3.forward);
        var spawn = Node(root, "Ivan at K-17");
        spawn.position = new Vector3(resonator.position.x - 1.57f, mainFloor.max.y + .025f, resonator.position.z);
        var focus = Node(root, "K-17 focus");
        focus.position = resonator.position + new Vector3(-.12f, .9f, 0f);
        var actor = Actor(root, model, avatar, controller);

        var father = flow.Father.Actor;
        var death = father.runtimeAnimatorController.animationClips.Single(c => c.name == "Father_Death");
        var probe = Object.Instantiate(father, father.transform.parent);
        probe.gameObject.SetActive(true);
        var surface = new Mesh();
        Bounds deadBody;
        try { deadBody = Sample(probe, death, death.length, surface: surface).bounds; }
        finally { Object.DestroyImmediate(probe.gameObject); }
        var support = FatherSurface(surface, deadBody);
        Object.DestroyImmediate(surface);
        var room = new Bounds();
        room.SetMinMax(new Vector3(roomFloor.min.x + .35f, roomFloor.max.y, threshold.z + .17f),
            new Vector3(roomFloor.max.x - .32f, roomFloor.max.y + 1f, roomFloor.max.z - .32f));
        var door = flow.FatherDoor;
        var openLeaf = new GameObject("Open door clearance probe", typeof(BoxCollider));
        openLeaf.transform.SetPositionAndRotation(door.Hinge.position,
            door.Hinge.parent.rotation * Quaternion.Euler(0f, door.SwingAngle, 0f));
        openLeaf.transform.localScale = door.Leaf.transform.lossyScale;
        var leaf = openLeaf.GetComponent<BoxCollider>();
        leaf.center = door.Leaf.center;
        leaf.size = door.Leaf.size + new Vector3(.20f, 0f, .16f);
        Physics.SyncTransforms();
        var approaches = Enumerable.Range(0, 12).SelectMany(z => Enumerable.Range(0, 12).Select(x =>
            new Vector3(.68f + x * .24f, roomFloor.max.y, threshold.z + .42f + z * .24f)))
            .Where(p => !Physics.CheckCapsule(p + Vector3.up * .28f, p + Vector3.up * 1.52f, .23f,
                ~0, QueryTriggerInteraction.Ignore)).Select(p => p + Vector3.up * 1.65f).ToArray();
        float best = float.PositiveInfinity;
        int inRoom = 0, clear = 0, contacts = 0;
        Vector3 head = default, position = default;
        Quaternion rotation = default;
        float bestDoorClearance = 0f;
        foreach (var angles in from tilt in new[] { 8f, 16f, 24f, -10f, 0f }
                     from yaw in Enumerable.Range(0, 12) select (tilt, yaw: yaw * 30f))
        {
            actor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, angles.yaw, 0f) *
                Quaternion.AngleAxis(angles.tilt, Vector3.forward));
            var slices = new Dictionary<int, Bounds>();
            var skin = new Dictionary<Vector2Int, Vector3>();
            var pose = Sample(actor, clip, clip.length, slices, underside: skin);
            var bodyRotation = actor.transform.rotation;
            float floorY = roomFloor.max.y - pose.bounds.min.y + .012f;
            var underside = skin.Values.ToArray();
            for (float z = deadBody.min.z - .40f; z <= deadBody.max.z + .30f; z += ContactCell)
                for (float x = deadBody.min.x - .28f; x <= deadBody.max.x + .60f; x += ContactCell)
                {
                    var point = new Vector3(x, floorY, z) -
                        new Vector3(pose.bounds.center.x, 0f, pose.bounds.center.z);
                    var body = new Bounds(point + pose.bounds.center, pose.bounds.size + new Vector3(.10f, 0f, .10f));
                    if (body.min.x < room.min.x || body.max.x > room.max.x || body.min.z < room.min.z ||
                        body.max.z > room.max.z || body.max.z < deadBody.min.z || body.min.z > deadBody.max.z ||
                        body.max.x < deadBody.min.x || body.min.x > deadBody.max.x) continue;
                    inRoom++;
                    var (supportY, touching) = RestOnFather(point, underside, support);
                    if (touching < 4 || supportY - floorY > .26f) continue;
                    contacts++;
                    point.y = supportY;
                    if (slices.Values.Any(b => Physics.CheckBox(point + bodyRotation * b.center,
                            b.extents + new Vector3(.018f, 0f, .035f), bodyRotation, ~0, QueryTriggerInteraction.Ignore))) continue;
                    clear++;
                    Vector3 visibleHead = point + pose.head;
                    var doorDelta = visibleHead - threshold;
                    float cost = doorDelta.x * doorDelta.x + doorDelta.z * doorDelta.z +
                        .9f * (supportY - floorY) - .015f * Mathf.Min(touching, 24);
                    if (cost >= best || !approaches.Any(p => (p - visibleHead).sqrMagnitude < 2.3f * 2.3f &&
                        !Physics.Linecast(p, visibleHead, ~0, QueryTriggerInteraction.Ignore))) continue;
                    position = point;
                    rotation = actor.transform.rotation;
                    head = visibleHead;
                    best = cost;
                    bestDoorClearance = Mathf.Sqrt(leaf.bounds.SqrDistance(body.center));
                }
        }
        Object.DestroyImmediate(openLeaf);
        Require(!float.IsPositiveInfinity(best), $"No supported rescue pose (room {inRoom}, contact {contacts}, static clearance {clear}).");
        actor.transform.SetPositionAndRotation(position, rotation);
        Debug.Log($"FINALE rescue: supported on the father's sampled surface; head {head:F3}; actor {position:F3}; center distance from open door {bestDoorClearance:F2} m.");
        var outline = RescueOutline(actor);
        actor.gameObject.SetActive(false);
        var canvas = Canvas(root, "Rescue HUD", 45);
        var objective = Label(canvas, font, "СПАСТИ АЛЕКСЕЯ", 64, new Vector2(.5f, 1f), new Vector2(0, -63f));
        objective.fontStyle = FontStyles.Bold;
        objective.color = Color.white;
        objective.rectTransform.sizeDelta = new Vector2(1160f, 100f);
        var prompt = Label(canvas, font, "Поднять и спасти Алексея — E", 31, new Vector2(.5f, 0f), new Vector2(0, 100f));
        objective.gameObject.SetActive(false);
        prompt.gameObject.SetActive(false);
        var rescueData = new SerializedObject(rescue);
        Set(rescueData, "outline", outline);
        Set(rescueData, ("view", player.GetComponentInChildren<Camera>()), ("player", player),
            ("alexei", actor), ("spawn", spawn), ("resonator", focus),
            ("roomThreshold", doorThreshold), ("objective", objective), ("prompt", prompt));
        var flowData = new SerializedObject(flow);
        Set(flowData, ("rescue", rescue));
    }

    private static SkinnedMeshRenderer[] RescueOutline(Animator actor)
    {
        const string materialPath = AssetsPath + "Alexei_RescueOutline.mat";
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(ScriptPath + "FinalLabRescueOutline.shader");
        Require(shader != null, "The rescue outline shader was not imported.");
        Require(!ShaderUtil.ShaderHasError(shader), "Rescue outline shader failed: " +
            string.Join("; ", ShaderUtil.GetShaderMessages(shader).Select(m => m.message)));
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, materialPath);
        }
        return actor.GetComponentsInChildren<SkinnedMeshRenderer>()
            .Where(r => r.name is "boots" or "Hair" or "hands" or "Head" or "Jacket" or "Jeans")
            .Select(source =>
            {
                var node = Node(source.transform, "Yellow rescue outline");
                var edge = node.gameObject.AddComponent<SkinnedMeshRenderer>();
                edge.sharedMesh = source.sharedMesh;
                edge.sharedMaterials = Enumerable.Repeat(material, source.sharedMesh.subMeshCount).ToArray();
                edge.bones = source.bones;
                edge.rootBone = source.rootBone;
                edge.localBounds = source.localBounds;
                edge.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                edge.receiveShadows = false;
                return edge;
            }).ToArray();
    }

    private static Dictionary<Vector2Int, float> FatherSurface(Mesh mesh, Bounds bounds)
    {
        var temporary = new GameObject("Measure the father's final pose", typeof(MeshCollider));
        var collider = temporary.GetComponent<MeshCollider>();
        collider.sharedMesh = mesh;
        Physics.SyncTransforms();
        var heights = new Dictionary<Vector2Int, float>();
        for (int x = Mathf.FloorToInt(bounds.min.x / ContactCell); x <= Mathf.CeilToInt(bounds.max.x / ContactCell); x++)
            for (int z = Mathf.FloorToInt(bounds.min.z / ContactCell); z <= Mathf.CeilToInt(bounds.max.z / ContactCell); z++)
            {
                var ray = new Ray(new Vector3(x * ContactCell, bounds.max.y + .16f, z * ContactCell), Vector3.down);
                if (collider.Raycast(ray, out var hit, bounds.size.y + .24f) && hit.point.y > bounds.min.y + .06f)
                    heights.Add(new Vector2Int(x, z), hit.point.y);
            }
        Object.DestroyImmediate(temporary);
        Require(heights.Count > 30, "No usable contact surface on the father's final pose.");
        return heights;
    }

    private static (float height, int touching) RestOnFather(Vector3 point, Vector3[] underside,
        Dictionary<Vector2Int, float> support)
    {
        float height = point.y;
        foreach (Vector3 p in underside)
            if (support.TryGetValue(ContactKey(point + p), out float top))
                height = Mathf.Max(height, top - p.y + .027f);
        int touching = 0;
        foreach (Vector3 p in underside)
            if (support.TryGetValue(ContactKey(point + p), out float top) && height + p.y - top < .08f)
                touching++;
        return (height, touching);
    }

    private static Vector2Int ContactKey(Vector3 p) => new Vector2Int(Mathf.RoundToInt(p.x / ContactCell), Mathf.RoundToInt(p.z / ContactCell));

    internal static (Bounds bounds, Vector3 head, Vector3 hips, Vector3 across) Sample(Animator actor, AnimationClip clip,
        float time, Dictionary<int, Bounds> horizontalSlices = null, Mesh surface = null,
        Dictionary<Vector2Int, Vector3> underside = null)
    {
        AnimationMode.StartAnimationMode();
        var scratch = new Mesh();
        var graph = default(PlayableGraph);
        try
        {
            if (actor.isHuman)
            {
                graph = PlayableGraph.Create("Sample the saved Humanoid rig");
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var output = AnimationPlayableOutput.Create(graph, "Humanoid", actor);
                var playable = AnimationClipPlayable.Create(graph, clip);
                output.SetSourcePlayable(playable);
                playable.SetApplyFootIK(false);
            }
            AnimationMode.BeginSampling();
            try
            {
                if (graph.IsValid()) AnimationMode.SamplePlayableGraph(graph, 0, time);
                else AnimationMode.SampleAnimationClip(actor.gameObject, clip, time);
            }
            finally { AnimationMode.EndSampling(); }
            var meshes = actor.GetComponentsInChildren<SkinnedMeshRenderer>().Where(r => r.enabled);
            var bounds = new Bounds();
            bool first = true;
            Quaternion toActor = Quaternion.Inverse(actor.transform.rotation);
            Vector3 origin = actor.transform.position;
            var vertices = surface != null ? new List<Vector3>() : null;
            var triangles = surface != null ? new List<int>() : null;
            foreach (var mesh in meshes)
            {
                mesh.BakeMesh(scratch);
                // BakeMesh returns these FBX skin vertices in meters despite the inherited centimeter transform.
                var toWorld = actor.isHuman && mesh.transform.lossyScale.x > 10f
                    ? Matrix4x4.TRS(mesh.transform.position, mesh.transform.rotation, Vector3.one) : mesh.transform.localToWorldMatrix;
                if (surface != null)
                    foreach (int index in scratch.triangles) triangles.Add(vertices.Count + index);
                foreach (Vector3 local in scratch.vertices)
                {
                    var p = toWorld.MultiplyPoint3x4(local);
                    vertices?.Add(p);
                    if (underside != null)
                    {
                        var key = ContactKey(p);
                        if (!underside.TryGetValue(key, out Vector3 prior) || p.y < prior.y) underside[key] = p;
                    }
                    if (first) { bounds = new Bounds(p, Vector3.zero); first = false; }
                    else bounds.Encapsulate(p);
                    if (horizontalSlices != null)
                    {
                        Vector3 relative = toActor * (p - origin);
                        int band = Mathf.FloorToInt(relative.x / .18f);
                        if (horizontalSlices.TryGetValue(band, out Bounds slice)) slice.Encapsulate(relative);
                        else slice = new Bounds(relative, Vector3.zero);
                        horizontalSlices[band] = slice;
                    }
                }
            }
            Require(!first, "No sampled skin vertices in " + actor.name);
            if (surface != null)
            {
                surface.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                surface.SetVertices(vertices);
                surface.SetTriangles(triangles, 0);
            }
            if (!actor.isHuman) return (bounds, default, default, default);
            Transform Bone(HumanBodyBones bone) => actor.GetBoneTransform(bone);
            if (bounds.size.magnitude > 6f)
            {
                // Some imported skins bake in FBX units during editor-only sampling. Use the retargeted skeleton for clearance.
                bounds = new Bounds(Bone(HumanBodyBones.Hips).position, Vector3.zero);
                foreach (var bone in new[] { HumanBodyBones.Head, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot,
                             HumanBodyBones.LeftToes, HumanBodyBones.RightToes, HumanBodyBones.LeftHand, HumanBodyBones.RightHand,
                             HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.LeftShoulder, HumanBodyBones.RightShoulder })
                {
                    var joint = Bone(bone);
                    if (joint != null) bounds.Encapsulate(joint.position);
                }
                bounds.Expand(new Vector3(.21f, .16f, .21f));
            }
            return (bounds, Bone(HumanBodyBones.Head).position, Bone(HumanBodyBones.Hips).position,
                (Bone(HumanBodyBones.RightUpperLeg).position - Bone(HumanBodyBones.LeftUpperLeg).position).normalized);
        }
        finally
        {
            AnimationMode.StopAnimationMode();
            if (graph.IsValid()) graph.Destroy();
            Object.DestroyImmediate(scratch);
        }
    }

    public static void VerifySaved()
    {
        var train = EditorSceneManager.OpenScene(TrainFinale.ScenePath);
        var finale = new SerializedObject(InScene<TrainFinale>(train).Single());
        var animator = (Animator)finale.FindProperty("alexei").objectReferenceValue;
        var states = ((AnimatorController)animator.runtimeAnimatorController).layers.Single().stateMachine.states;
        Require(animator.avatar.isHuman && animator.avatar.isValid && !animator.applyRootMotion &&
            states.Length == 5 && states.All(s => s.state.motion is AnimationClip c && c.humanMotion &&
                c.isLooping == (s.state.name == TrainFinale.Angry)), "Saved Train Animator, Avatar, and loop configuration.");
        var rise = (AnimationClip)states.Single(s => s.state.name == TrainFinale.Rise).state.motion;
        Require(rise.length > 7f, "Train must use the full floor-to-standing clip.");
        VerifyHandoff(animator, finale.FindProperty("standingOffset").vector3Value);
        var player = InScene<FirstPersonPlayer>(train).Single();
        var ordinary = finale.FindProperty("ordinaryGameplay");
        Require(Enumerable.Range(0, ordinary.arraySize).Any(i => ordinary.GetArrayElementAtIndex(i).objectReferenceValue == player.gameObject),
            "The finale must suspend the FPS player root that initializes the weapon view.");
        var subtitles = (FinalLabIvanDialogue)finale.FindProperty("subtitles").objectReferenceValue;
        Require(subtitles.ReadLines().Length == 10 && subtitles.ReadLines().Where((l, i) =>
            l.speaker != (i % 2 == 0 ? "ИВАН" : "АЛЕКСЕЙ")).Count() == 0, "The saved Train dialogue must have ten ordered speakers.");
        Require(InScene<Camera>(train).Count(c => c.enabled && c.gameObject.activeInHierarchy) == 1 &&
            InScene<AudioListener>(train).Count(l => l.enabled && l.gameObject.activeInHierarchy) == 1,
            "Train requires one active camera and Audio Listener.");
        var lab = EditorSceneManager.OpenScene(LabPath);
        var flow = InScene<FinalLabFlowController>(lab).Single();
        var rescue = InScene<FinalLabAlexeiRescue>(lab).Single();
        var rescueData = new SerializedObject(rescue);
        Require(new SerializedObject(flow).FindProperty("rescue").objectReferenceValue == rescue &&
            rescueData.FindProperty("alexei").objectReferenceValue is Animator prone &&
            prone.avatar.isHuman && prone.avatar.isValid && !prone.gameObject.activeSelf,
            "The saved laboratory flow, rescue, and inactive Alexei must be wired.");
        var objective = (TMP_Text)rescueData.FindProperty("objective").objectReferenceValue;
        var outline = rescueData.FindProperty("outline");
        Require(objective.text == "СПАСТИ АЛЕКСЕЯ" && objective.fontSize >= 60f && objective.fontStyle == FontStyles.Bold &&
            objective.color == Color.white && outline.arraySize > 0 && Enumerable.Range(0, outline.arraySize).All(i =>
                outline.GetArrayElementAtIndex(i).objectReferenceValue is SkinnedMeshRenderer renderer &&
                renderer.sharedMaterial.shader.name == "FinalLab/Rescue Outline"), "The rescue HUD and yellow skinned outline must be assigned.");
        Debug.Log("FINAL_LAB_ENDING_SAVED_AND_VERIFIED");
    }

    private static void VerifyHandoff(Animator saved, Vector3 offset)
    {
        var actor = Object.Instantiate(saved);
        actor.transform.SetPositionAndRotation(saved.transform.position, saved.transform.rotation);
        var origin = actor.transform.position;
        var graph = PlayableGraph.Create("Check the Train animation handoff");
        try
        {
            var playable = AnimatorControllerPlayable.Create(graph, actor.runtimeAnimatorController);
            AnimationPlayableOutput.Create(graph, "Handoff", actor).SetSourcePlayable(playable);
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            graph.Play();
            playable.Play("Base Layer.Rise", 0, 1f);
            graph.Evaluate(.02f);
            var hip = actor.GetBoneTransform(HumanBodyBones.Hips);
            var planted = hip.position;
            playable.CrossFadeInFixedTime("Base Layer.Angry", .46f, 0, 0);
            float largest = 0f;
            for (int i = 0; i < 15; i++)
            {
                graph.Evaluate(.04f);
                float weight = playable.IsInTransition(0) ? Mathf.Clamp01(playable.GetAnimatorTransitionInfo(0).normalizedTime) :
                    playable.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Angry") ? 1f : 0f;
                actor.transform.position = origin + offset * weight;
                largest = Mathf.Max(largest, Vector3.ProjectOnPlane(hip.position - planted, Vector3.up).magnitude);
            }
            Require(largest < .11f, $"Train handoff moves {largest:F3} m even after alignment.");
            Debug.Log($"FINALE rise-to-dialogue: compensated {offset.magnitude:F3} m; remaining horizontal hip movement {largest:F3} m.");
        }
        finally { graph.Destroy(); Object.DestroyImmediate(actor.gameObject); }
    }

    private static T[] InScene<T>(Scene scene) where T : Component =>
        scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<T>(true)).ToArray();

    private static GameObject Root(Scene scene, string name)
    {
        var old = scene.GetRootGameObjects().FirstOrDefault(o => o.name == name);
        if (old != null) Object.DestroyImmediate(old);
        return new GameObject(name);
    }

    private static Transform Node(Transform parent, string name)
    {
        var node = new GameObject(name).transform;
        node.SetParent(parent, false);
        return node;
    }

    private static Transform Point(Transform parent, string name, Vector3 place, Vector3 focus)
    {
        var node = Node(parent, name);
        node.SetPositionAndRotation(place, Quaternion.LookRotation(focus - place));
        return node;
    }

    private static Transform Canvas(Transform parent, string name, int order)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        obj.transform.SetParent(parent, false);
        obj.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        obj.GetComponent<Canvas>().sortingOrder = order;
        var scaler = obj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .42f;
        return obj.transform;
    }

    private static Image Fullscreen(Transform parent, string name, Color color)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false);
        var image = obj.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        image.rectTransform.anchorMin = Vector2.zero;
        image.rectTransform.anchorMax = Vector2.one;
        image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
        return image;
    }

    private static TMP_Text Label(Transform parent, TMP_FontAsset font, string content, float size, Vector2 anchor, Vector2 offset)
    {
        var obj = new GameObject(content, typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);
        var label = obj.GetComponent<TMP_Text>();
        label.font = font;
        label.text = content;
        label.fontSize = size;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        label.rectTransform.anchorMin = label.rectTransform.anchorMax = anchor;
        label.rectTransform.anchoredPosition = offset;
        label.rectTransform.sizeDelta = new Vector2(900, 80);
        return label;
    }

    private static void Set(SerializedObject target, params (string field, Object value)[] references)
    {
        foreach (var (field, value) in references)
        {
            Require(value != null, "Missing component for " + field);
            target.FindProperty(field).objectReferenceValue = value;
        }
        target.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Set(SerializedObject target, string field, Object[] values)
    {
        var property = target.FindProperty(field);
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        target.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
