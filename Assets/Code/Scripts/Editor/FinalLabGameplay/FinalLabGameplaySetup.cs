using System;
using System.Collections.Generic;
using System.Linq;
using FinalLab.Investigation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Function = FinalLabSwitch.Function;

public static class FinalLabGameplaySetup
{
    private const string LabPath = "Assets/Level/Scenes/Locations/laba/FinalScene.unity";
    private const string AssetsPath = "Assets/Code/Scripts/Scenes/FinalLabGameplay/";
    private static readonly Vector3 Back = Vector3.back;
    private const float BaseTrainOffsetZ = .87725437f;

    public static void Install()
    {
        var scene = EditorSceneManager.OpenScene(LabPath);
        var flow = Find<FinalLabFlowController>();
        var father = flow.Father;
        var ivan = Find<FinalLabIvanCutsceneController>();
        var motor = Find<FirstPersonPlayer>();
        var root = Node(null, "Lab gameplay · Alexei and Ivan", Vector3.zero);
        var previous = Find<FinalLabMechanisms>(optional: true);
        if (previous != null) Object.DestroyImmediate(previous.gameObject);

        var rescue = Find<FinalLabAlexeiRescue>();
        var playerCamera = motor.GetComponentInChildren<Camera>();
        var k17 = Named("K17_Resonator_Root");
        var panel = k17.Find("K17_ControlPanel");
        var knife = panel.Find("Breaker bay/K17_KnifeSwitch");
        var mode = panel.Find("K17_ModeSelector");
        var cover = panel.Find("K17_OverloadCover");
        var overload = panel.Find("K17_OverloadButton");
        var needle = panel.Find("K17_AnalogGauge/Needle pivot");
        var core = Named("K17_Core");
        var segments = panel.Find("K17_IsolationGauge/Calibrated indicator rail").GetComponentsInChildren<MeshRenderer>();
        var powerLens = panel.Find("K17_PowerIndicator/Ceramic lens").GetComponent<Renderer>();
        var contourLens = panel.Find("K17_ContourIndicator/Ceramic lens").GetComponent<Renderer>();

        var cabinet = Asset("Switch housing", new Color(.14f, .19f, .17f), unlit: false);
        var lens = Asset("Local switch lamp", new Color(.20f, .13f, .07f), unlit: true);
        var blueArc = Asset("K17 discharge", new Color(.34f, .66f, 1f), unlit: true);
        var fragmentArc = Asset("Contained fragment", new Color(.40f, .17f, .66f), unlit: true);
        var font = father.Subtitles.Body.font;
        var west = Named("West_Distribution");
        var generator = Named("Generator_Input");
        var generatorBounds = BoundsOf(Named("Generator"));
        var eastWall = Named("HallEntryEast").GetComponent<BoxCollider>().bounds;
        var westWall = Named("HallWest").GetComponent<BoxCollider>().bounds;
        var wall = Named("ContainmentPartition_ObservationWindowOpening_Sill").GetComponent<BoxCollider>().bounds;
        var electrical = BoundsOf(Named("ElectricalPanel"));

        var controls = new[]
        {
            Mount(root, knife, new Vector3(westWall.max.x, 1.36f, west.position.z - .7f), Vector3.right,
                "РЕЗЕРВ 1 · ЗАПАД", Function.Reserve, cabinet, lens, font),
            Mount(root, knife, new Vector3(generatorBounds.min.x - .08f, generator.position.y + .13f, generator.position.z), Vector3.left,
                "РЕЗЕРВ 2 · ГЕНЕРАТОР", Function.Reserve, cabinet, lens, font),
            Mount(root, knife, new Vector3(2.28f, 1.40f, eastWall.max.z), Vector3.forward,
                "РЕЗЕРВ 3 · ВОСТОК", Function.Reserve, cabinet, lens, font),
            Mount(root, knife, new Vector3(1.91f, 1.37f, wall.min.z), Back,
                "ИЗОЛЯЦИЯ · КОНТУР 1", Function.Isolation, cabinet, lens, font),
            Mount(root, knife, new Vector3(4.24f, 1.37f, wall.min.z), Back,
                "ИЗОЛЯЦИЯ · КОНТУР 2", Function.Isolation, cabinet, lens, font),
            Mount(root, knife, new Vector3(electrical.min.x, electrical.center.y + .02f, electrical.center.z), Vector3.left,
                "ТЯГОВАЯ ЛИНИЯ", Function.Traction, cabinet, lens, font),
            Mark(root, mode, Function.Mode, "Выбрать режим Б", new Vector3(0, 0, 80), mode.TransformPoint(new Vector3(0, .012f, .059f))),
            Mark(root, knife, Function.Contour, "Включить рубильник контура", new Vector3(-47, 0, 0),
                knife.TransformPoint(new Vector3(0, .09f, .052f))),
            Mark(root, cover, Function.Cover, "Открыть защитную крышку", new Vector3(-88, 0, 0),
                cover.TransformPoint(new Vector3(0, .02f, .056f))),
            Mark(root, overload, Function.Overload, "Подтвердить аварийную перегрузку", new Vector3(18, 0, 0),
                overload.TransformPoint(new Vector3(0, 0, .066f)))
        };

        var marker = (GameObject)new SerializedObject(flow).FindProperty("doorMarker").objectReferenceValue;
        DoorFrame(flow.FatherDoor, marker, Asset("Door amber", new Color(.94f, .68f, .21f), unlit: true));
        var hud = Hud(root, font);
        var start = Node(root, "Ivan POV · control room", new Vector3(-2.48f, .035f, 15.52f));
        var pointsAt = Node(root, "Ivan initial view · traction control", controls[5].Point);
        var corePoint = Node(root, "K17 discharge focus", core.position + k17.forward * .20f);
        var discharges = new[] { Arc(root, fragmentArc), Arc(root, blueArc), Arc(root, blueArc) };
        var controller = root.gameObject.AddComponent<FinalLabMechanisms>();
        var so = new SerializedObject(controller);
        Set(so, "flow", flow);
        Set(so, "player", motor);
        Set(so, "view", playerCamera);
        Set(so, "investigation", Find<FinalLabInvestigationManager>());
        Set(so, "investigationHud", Find<FinalLabInvestigationUI>());
        Set(so, "controlDoor", ivan.Door);
        Set(so, "ivanActor", ivan.Ivan.gameObject);
        Set(so, "rescue", rescue);
        Set(so, "ivanSpawn", start);
        Set(so, "ivanView", pointsAt);
        Set(so, "outlineShader", Shader.Find("FinalLab/Rescue Outline"));
        Set(so, "objective", hud.goal);
        Set(so, "telemetry", hud.telemetry);
        Set(so, "feedback", hud.feedback);
        Set(so, "continueHint", father.Subtitles.GetComponentsInChildren<TMP_Text>(true).Single(t => t.name == "Continue prompt"));
        Set(so, "needle", needle);
        Set(so, "resonatorCore", corePoint);
        Set(so, "powerLens", powerLens);
        Set(so, "contourLens", contourLens);
        Set(so, "segments", segments);
        Set(so, "switches", controls);
        Set(so, "reserveLights", new[] { Named("Coil_WorkLight").GetComponent<Light>(),
            Named("Containment_EntryAccent").GetComponent<Light>(), Named("Containment_White").GetComponent<Light>() });
        Set(so, "pulseLights", new[] { Named("Resonator warm worklight").GetComponent<Light>(),
            Named("Copper glint").GetComponent<Light>(), Named("Coil_WorkLight").GetComponent<Light>() });
        Set(so, "arcs", discharges);
        so.ApplyModifiedPropertiesWithoutUndo();

        var flowData = new SerializedObject(flow);
        flowData.FindProperty("skipGameplayForTesting").boolValue = false;
        flowData.FindProperty("skipIvanGameplayForTesting").boolValue = false;
        flowData.ApplyModifiedPropertiesWithoutUndo();
        GroundAlexei(rescue, father, flow.FatherDoor);
        Physics.SyncTransforms();
        ValidateLab(scene, controls, flow, rescue, motor);
        Require(!Physics.CheckCapsule(start.position + Vector3.up * .27f, start.position + Vector3.up * 1.47f,
                    .22f, ~0, QueryTriggerInteraction.Ignore), "Ivan starts inside an obstacle.");
        var spawnEye = start.position + Vector3.up * 1.65f;
        var towardCabinet = pointsAt.position - spawnEye;
        Require(!Physics.Raycast(spawnEye, towardCabinet.normalized, towardCabinet.magnitude - .025f,
                    ~0, QueryTriggerInteraction.Ignore), "Ivan's starting view is obstructed.");
        EditorSceneManager.MarkSceneDirty(scene);
        Require(EditorSceneManager.SaveScene(scene), "FinalScene could not be saved.");
        ComposeTrain();
        AssetDatabase.SaveAssets();
        Debug.Log("LABA_GAMEPLAY_BATCH_VERIFIED");
    }

    private static void GroundAlexei(FinalLabAlexeiRescue rescue, FatherCutsceneController father, FinalLabMetalDoor door)
    {
        var profile = new SerializedObject(rescue);
        var actor = (Animator)profile.FindProperty("alexei").objectReferenceValue;
        var room = Named("ContainmentFloor").GetComponent<BoxCollider>();
        var clip = actor.runtimeAnimatorController.animationClips.Single();
        var actorProbe = Object.Instantiate(actor);
        var fatherProbe = Object.Instantiate(father.Actor);
        try
        {
            actorProbe.gameObject.SetActive(true);
            actorProbe.applyRootMotion = false;
            foreach (var edge in actorProbe.GetComponentsInChildren<SkinnedMeshRenderer>())
                if (edge.name == "Yellow rescue outline") edge.enabled = false;
            fatherProbe.gameObject.SetActive(true);
            var death = fatherProbe.runtimeAnimatorController.animationClips.Single(c => c.name == "Father_Death");
            var dead = FinalLabEndingSetup.Sample(fatherProbe, death, death.length).bounds;
            Bounds safe = room.bounds;
            safe.Expand(new Vector3(-.24f, 0, -.24f));
            var openLeaf = new GameObject("Open door clearance", typeof(BoxCollider));
            try
            {
                var hinge = door.Hinge;
                openLeaf.transform.SetPositionAndRotation(hinge.position, hinge.parent.rotation * Quaternion.Euler(0, door.SwingAngle, 0));
                openLeaf.transform.localScale = door.Leaf.transform.lossyScale;
                var leaf = openLeaf.GetComponent<BoxCollider>();
                leaf.center = door.Leaf.center;
                leaf.size = door.Leaf.size;
                Physics.SyncTransforms();

                float best = float.PositiveInfinity;
                Vector3 place = Vector3.zero;
                Quaternion orientation = Quaternion.identity;
                var threshold = ((Transform)profile.FindProperty("roomThreshold").objectReferenceValue).position;
                var approaches = (from z in Enumerable.Range(0, 10)
                                  from x in Enumerable.Range(0, 12)
                                  select new Vector3(.62f + x * .23f, safe.max.y, threshold.z + .48f + z * .23f))
                    .Where(p => !Physics.CheckCapsule(p + Vector3.up * .28f, p + Vector3.up * 1.5f, .22f,
                        ~0, QueryTriggerInteraction.Ignore)).Select(p => p + Vector3.up * 1.65f).ToArray();
                int checkedPositions = 0, clearPositions = 0;
                foreach (float yaw in new[] { actor.transform.eulerAngles.y, 0, 45, 90, 135, 180, 225, 270, 315 })
                foreach (float tilt in new[] { 0, -8, 8, 16 })
                {
                    var rotation = Quaternion.Euler(0, yaw, 0) * Quaternion.AngleAxis(tilt, Vector3.forward);
                    actorProbe.transform.SetPositionAndRotation(Vector3.zero, rotation);
                    var slices = new Dictionary<int, Bounds>();
                    var pose = FinalLabEndingSetup.Sample(actorProbe, clip, clip.length, slices);
                    if (pose.bounds.size.y > .96f) continue;
                    for (float x = safe.min.x + .2f; x < safe.max.x - .2f; x += .14f)
                    for (float z = safe.min.z + .35f; z < safe.max.z - .2f; z += .14f)
                    {
                        var center = new Vector3(x, safe.max.y - pose.bounds.min.y + .015f, z);
                        center -= new Vector3(pose.bounds.center.x, 0, pose.bounds.center.z);
                        var posed = pose.bounds;
                        posed.center += center;
                        if (posed.min.x < safe.min.x || posed.max.x > safe.max.x ||
                            posed.min.z < safe.min.z || posed.max.z > safe.max.z) continue;
                        float separation = DistanceXZ(posed, dead);
                        if (separation < .045f) continue;
                        checkedPositions++;
                        var head = center + pose.head;
                        float cost = separation * 2.5f + Vector3.Distance(head, threshold) * .29f + Mathf.Abs(tilt) * .013f;
                        if (cost >= best) continue;
                        if (slices.Values.Any(b => Physics.OverlapBox(center + rotation * b.center,
                                b.extents + new Vector3(.018f, 0, .026f), rotation, ~0, QueryTriggerInteraction.Ignore)
                            .Any(c => c != room && c != door.Leaf))) continue;
                        clearPositions++;
                        if (!approaches.Any(p => (p - head).sqrMagnitude < 2.27f * 2.27f &&
                            !Physics.Linecast(p, head, ~0, QueryTriggerInteraction.Ignore))) continue;
                        best = cost;
                        place = center;
                        orientation = rotation;
                    }
                }
                Require(float.IsFinite(best), $"No clear floor pose near the father was found ({checkedPositions} placements, {clearPositions} clear, {approaches.Length} approaches; father {dead:F2}).");
                actor.transform.SetPositionAndRotation(place, orientation);
                actor.applyRootMotion = false;
                EditorUtility.SetDirty(actor);
                Debug.Log($"LABA_FLOOR_SAMPLE_OK Alexei root {place:F3}, minimum skin height 0.015 m above floor.");
            }
            finally { Object.DestroyImmediate(openLeaf); }
        }
        finally
        {
            Object.DestroyImmediate(actorProbe.gameObject);
            Object.DestroyImmediate(fatherProbe.gameObject);
        }
    }

    private static float DistanceXZ(Bounds a, Bounds b)
    {
        float x = Mathf.Max(0, a.min.x - b.max.x, b.min.x - a.max.x);
        float z = Mathf.Max(0, a.min.z - b.max.z, b.min.z - a.max.z);
        return new Vector2(x, z).magnitude;
    }

    private static void ComposeTrain()
    {
        var scene = EditorSceneManager.OpenScene(TrainFinale.ScenePath);
        var end = Find<TrainFinale>();
        var properties = new SerializedObject(end);
        var actor = (Animator)properties.FindProperty("alexei").objectReferenceValue;
        var eye = (Transform)properties.FindProperty("standingEye").objectReferenceValue;
        var offsetField = properties.FindProperty("standingOffset");
        var original = offsetField.vector3Value;
        float direction = Mathf.Sign(actor.transform.position.z + BaseTrainOffsetZ - eye.position.z);
        var move = Vector3.forward * direction * .58f;
        // Use the longitudinal aisle and leave the initial floor pose and camera marks intact.
        offsetField.vector3Value = new Vector3(original.x, original.y, .58f * direction + BaseTrainOffsetZ);
        properties.ApplyModifiedPropertiesWithoutUndo();
        Require(actor.avatar != null && actor.avatar.isValid && actor.runtimeAnimatorController != null &&
            actor.runtimeAnimatorController.animationClips.Length >= 5 && eye != null, "Train ending lost its rig or camera markers.");
        VerifyTrainFraming(actor, eye, offsetField.vector3Value);
        EditorSceneManager.MarkSceneDirty(scene);
        Require(EditorSceneManager.SaveScene(scene), "Train could not be saved.");
        Debug.Log($"LABA_TRAIN_COMPOSITION_OK shifted along the aisle by {move.magnitude:0.00} m.");
    }

    private static void VerifyTrainFraming(Animator actor, Transform eye, Vector3 offset)
    {
        var copy = Object.Instantiate(actor);
        try
        {
            copy.gameObject.SetActive(true);
            copy.transform.SetPositionAndRotation(actor.transform.position + offset, actor.transform.rotation);
            var graph = (UnityEditor.Animations.AnimatorController)actor.runtimeAnimatorController;
            var seats = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(r => r.name.StartsWith("CojinBajoAsiento", StringComparison.Ordinal) &&
                    Mathf.Abs(r.bounds.center.z - copy.transform.position.z) < 2.5f).Select(r => r.bounds).ToArray();
            foreach (string name in new[] { TrainFinale.Angry, TrainFinale.Point, TrainFinale.Dismiss })
            {
                var state = graph.layers[0].stateMachine.states.Single(s => s.state.name == name).state;
                var clip = (AnimationClip)state.motion;
                foreach (float fraction in new[] { .15f, .50f, .85f })
                {
                    var frame = FinalLabEndingSetup.Sample(copy, clip, clip.length * fraction);
                    Require(Vector3.Distance(frame.head, eye.position) > 1.1f, "Train dialogue is still framed too close.");
                    Require(!seats.Any(s => s.Contains(frame.hips) || s.Contains(frame.head)), "Train dialogue intersects a seat.");
                }
            }
        }
        finally { Object.DestroyImmediate(copy.gameObject); }
    }

    private static void ValidateLab(Scene scene, FinalLabSwitch[] targets, FinalLabFlowController flow,
        FinalLabAlexeiRescue rescue, FirstPersonPlayer player)
    {
        Require(targets.Count(t => t.Kind == Function.Reserve) == 3 &&
            targets.Count(t => t.Kind == Function.Isolation) == 2, "Missing power or isolation switch.");
        foreach (var k in new[] { Function.Traction, Function.Mode, Function.Contour, Function.Cover, Function.Overload })
            Require(targets.Count(t => t.Kind == k) == 1, "Missing or duplicate K-17 action: " + k);
        Require(!flow.SkipGameplayForTesting && !flow.SkipIvanGameplayForTesting && flow.FatherDoor.IsLocked,
            "Gameplay bypass is enabled or father door starts unlocked.");
        flow.OnGameplayCompleted();
        flow.StartFatherSequence();
        flow.OnIvanGameplayCompleted();
        Require(flow.State == FinalLabFlowController.Stage.IvanDialogue && flow.FatherDoor.IsLocked, "An early completion bypassed gameplay.");
        Require(flow.Father.Actor.avatar.isValid && new SerializedObject(rescue).FindProperty("alexei").objectReferenceValue is Animator body &&
            body.avatar.isValid && body.isHuman, "A saved character rig is not valid.");
        Require(EditorBuildSettings.scenes.Any(s => s.enabled && s.path == TrainFinale.ScenePath), "Train is not enabled in the build.");
        Require(scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<AudioListener>()).Count(l => l.enabled) == 1,
            "FinalScene must use one AudioListener.");
        Require(scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<FirstPersonPlayer>()).Single() == player,
            "FinalScene must use one first-person controller.");

        foreach (var target in targets)
        {
            var origin = target.Point;
            bool reachable = false;
            for (int n = 0; n < 16; n++)
            {
                var offset = Quaternion.Euler(0, n * 22.5f, 0) * Vector3.forward * 1.12f;
                var eye = new Vector3(origin.x, 1.65f, origin.z) + offset;
                if (!Physics.Raycast(eye, Vector3.down, out var floor, 1.75f, ~0, QueryTriggerInteraction.Ignore) ||
                    floor.point.y > .14f || floor.point.y < -.1f) continue;
                if (Physics.CheckCapsule(floor.point + Vector3.up * .29f, floor.point + Vector3.up * 1.45f,
                        .22f, ~0, QueryTriggerInteraction.Ignore)) continue;
                var toward = origin - eye;
                if (!Physics.Raycast(eye, toward.normalized, toward.magnitude - .025f, ~0, QueryTriggerInteraction.Ignore))
                {
                    reachable = true;
                    break;
                }
            }
            Require(reachable, $"No standing approach for {target.Kind} at {target.Point:F3}.");
        }
        Debug.Log("LABA_CONTROLS_REACHABLE_AND_FLOW_GATED");
    }

    private static FinalLabSwitch Mount(Transform root, Transform model, Vector3 anchor, Vector3 outward, string text,
        Function role, Material casing, Material lampMaterial, TMP_FontAsset font)
    {
        var basePlate = Node(root, text, anchor + outward * .051f);
        basePlate.rotation = Quaternion.LookRotation(outward);
        Block(basePlate, "Enamel switch mounting", Vector3.zero, new Vector3(.24f, .28f, .064f), casing);
        var grip = Object.Instantiate(model.gameObject, basePlate).transform;
        grip.name = "Knife actuator";
        grip.localPosition = new Vector3(0, -.062f, .043f);
        grip.localRotation = Quaternion.identity;
        grip.localScale = Vector3.one * 1.15f;
        var lamp = Block(basePlate, "Reserve circuit indicator", new Vector3(-.083f, .020f, .040f),
            new Vector3(.025f, .018f, .01f), lampMaterial);
        var label = new GameObject("Enamel caption", typeof(RectTransform), typeof(TextMeshPro)).GetComponent<TextMeshPro>();
        label.transform.SetParent(basePlate, false);
        label.transform.SetLocalPositionAndRotation(new Vector3(0, .106f, .038f), Quaternion.Euler(0, 180, 0));
        label.transform.localScale = Vector3.one * .095f;
        label.rectTransform.sizeDelta = new Vector2(2.3f, .38f);
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 1.95f;
        label.font = font;
        label.color = new Color(.88f, .82f, .66f);
        label.text = text;
        return Mark(basePlate, grip, role, role is Function.Reserve or Function.Isolation ? "Взаимодействовать" : "Отключить тяговую линию",
            new Vector3(-53, 0, 0), basePlate.TransformPoint(new Vector3(0, .036f, .112f)), lamp);
    }

    private static FinalLabSwitch Mark(Transform parent, Transform visual, Function kind, string caption,
        Vector3 angle, Vector3 point, Renderer lamp = null)
    {
        var meshes = visual.GetComponentsInChildren<MeshRenderer>().Where(r =>
            r.TryGetComponent<MeshFilter>(out var f) && f.sharedMesh != null).ToArray();
        Require(meshes.Length > 0, "No visible part on " + visual.name);
        var item = Node(parent, kind + " target", point).gameObject.AddComponent<FinalLabSwitch>();
        item.Configure(kind, caption, visual, angle, lamp, meshes);
        return item;
    }

    private static void DoorFrame(FinalLabMetalDoor door, GameObject marker, Material material)
    {
        marker.GetComponent<Renderer>().enabled = false;
        for (int i = marker.transform.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(marker.transform.GetChild(i).gameObject);
        var face = door.Leaf.bounds;
        marker.transform.SetPositionAndRotation(new Vector3(face.center.x, face.center.y, face.min.z - .028f), Quaternion.identity);
        marker.transform.localScale = Vector3.one;
        float width = face.size.x * .9f, height = face.size.y * .91f;
        foreach (int sign in new[] { -1, 1 })
        {
            Block(marker.transform, "Door contour", Vector3.right * sign * width / 2, new Vector3(.011f, height, .011f), material);
            Block(marker.transform, "Door contour", Vector3.up * sign * height / 2, new Vector3(width + .011f, .011f, .011f), material);
        }
        marker.SetActive(false);
    }

    private static (TMP_Text goal, TMP_Text telemetry, TMP_Text feedback) Hud(Transform root, TMP_FontAsset font)
    {
        var canvas = new GameObject("Mechanisms HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvas.transform.SetParent(root, false);
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.GetComponent<Canvas>().sortingOrder = 27;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .5f;
        return (Label(canvas.transform, font, "Objective", -59f, 724f, 64f, 30f, new Color(.99f, .86f, .59f)),
            Label(canvas.transform, font, "Measurements", -120f, 486f, 45f, 24f, Color.white),
            Label(canvas.transform, font, "Feedback", -174f, 675f, 44f, 23f, new Color(.91f, .84f, .71f)));
    }

    private static TMP_Text Label(Transform parent, TMP_FontAsset font, string name, float y, float width,
        float height, float size, Color color)
    {
        var backdrop = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        backdrop.transform.SetParent(parent, false);
        backdrop.rectTransform.anchorMin = backdrop.rectTransform.anchorMax = new Vector2(.5f, 1f);
        backdrop.rectTransform.anchoredPosition = new Vector2(0, y);
        backdrop.rectTransform.sizeDelta = new Vector2(width, height);
        backdrop.color = new Color(.036f, .043f, .042f, .88f);
        backdrop.raycastTarget = false;
        var text = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        text.transform.SetParent(backdrop.transform, false);
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = new Vector2(15, 3);
        text.rectTransform.offsetMax = new Vector2(-15, -3);
        text.alignment = TextAlignmentOptions.Center;
        text.font = font;
        text.color = color;
        text.fontSize = size;
        text.text = "";
        text.raycastTarget = false;
        backdrop.gameObject.SetActive(false);
        return text;
    }

    private static LineRenderer Arc(Transform parent, Material material)
    {
        var line = Node(parent, material.name, parent.position).gameObject.AddComponent<LineRenderer>();
        line.sharedMaterial = material;
        line.positionCount = 15;
        line.alignment = LineAlignment.View;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.enabled = false;
        return line;
    }

    private static Transform Node(Transform parent, string name, Vector3 position)
    {
        var node = new GameObject(name).transform;
        if (parent != null) node.SetParent(parent, false);
        node.position = position;
        return node;
    }

    private static Renderer Block(Transform parent, string name, Vector3 center, Vector3 size, Material material)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(cube.GetComponent<Collider>());
        cube.name = name;
        cube.transform.SetParent(parent, false);
        cube.transform.localPosition = center;
        cube.transform.localScale = size;
        var renderer = cube.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        return renderer;
    }

    private static Material Asset(string name, Color color, bool unlit)
    {
        string path = AssetsPath + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        material = new Material(Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit"))
            { name = name, color = color };
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static Bounds BoundsOf(Transform root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>();
        var bounds = renderers.First().bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }

    private static Transform Named(string name) => GameObject.Find(name).transform;
    private static T Find<T>(bool optional = false) where T : Object
    {
        var obj = Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
        if (!optional) Require(obj != null, "Missing " + typeof(T).Name);
        return obj;
    }

    private static void Set(SerializedObject data, string field, Object value)
    {
        Require(value != null, "Missing reference " + field);
        data.FindProperty(field).objectReferenceValue = value;
    }

    private static void Set(SerializedObject data, string field, Object[] values)
    {
        var property = data.FindProperty(field);
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    private static void Require(bool valid, string reason)
    {
        if (!valid) throw new InvalidOperationException(reason);
    }
}
