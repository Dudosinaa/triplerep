using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class IvanFinaleSetup
{
    private const string Scene = "Assets/Level/Scenes/Locations/laba/FinalScene.unity";
    private static Transform root;
    private static Material metal, ivory;

    [MenuItem("Tools/Final Lab/Install Ivan finale")]
    public static void Install()
    {
        var scene = EditorSceneManager.OpenScene(Scene);
        var previous = GameObject.Find("Ivan finale gameplay");
        if (previous != null)
        {
            if (previous.GetComponent<IvanFinaleController>() == null) throw new InvalidOperationException("Unexpected object with the finale root name.");
            var previousState = new SerializedObject(previous.GetComponent<IvanFinaleController>());
            var oldArm = previousState.FindProperty("carriedArm").objectReferenceValue as GameObject;
            if (oldArm != null) Object.DestroyImmediate(oldArm);
            foreach (var orphan in Object.FindObjectsByType<IvanFinaleActor>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(a => a.name == "Alexei carried on Ivan shoulder").ToArray()) Object.DestroyImmediate(orphan.gameObject);
            Object.DestroyImmediate(previous);
            foreach (var control in Object.FindObjectsByType<IvanFinaleControl>(FindObjectsSortMode.None))
            {
                var collider = control.GetComponent<BoxCollider>();
                if (collider != null) Object.DestroyImmediate(collider);
                Object.DestroyImmediate(control);
            }
        }
        root = new GameObject("Ivan finale gameplay").transform;
        metal = Mat("Finale steel", new Color(.19f, .22f, .19f));
        ivory = Mat("Finale ceramic", new Color(.76f, .72f, .57f));
        var player = Object.FindFirstObjectByType<FirstPersonPlayer>();
        var start = Mark("Ivan control spawn", new Vector3(-1.5f, .03f, 15.6f));
        start.rotation = Quaternion.Euler(0, -47, 0);
        var rescue = Mark("Alexei rescue", new Vector3(2.1f, .35f, 15.9f));
        var exit = Mark("K17 exit", new Vector3(0, 1.7f, -3.9f));
        var arrival = Mark("Platform arrival", new Vector3(0, .05f, -25));
        var actor = Object.FindFirstObjectByType<FinalLabIvanCharacterController>(FindObjectsInactive.Include);
        var alexei = Character("Unconscious Alexei", rescue.position);
        alexei.transform.rotation = Quaternion.Euler(0, 20, 85);
        alexei.SetActive(false);
        var arm = Object.Instantiate(alexei, player.GetComponentInChildren<Camera>(true).transform);
        arm.name = "Alexei carried on Ivan shoulder";
        arm.transform.localPosition = new Vector3(.5f, -1.3f, .65f);
        arm.transform.localRotation = Quaternion.Euler(0, -70, 8);
        MakeCarriedArm(arm);
        arm.transform.localRotation = Quaternion.Euler(0, -70, -25);
        var bones = arm.GetComponentsInChildren<Transform>(true);
        var shoulder = bones.First(b => b.name.EndsWith("LeftArm", StringComparison.OrdinalIgnoreCase));
        var forearm = bones.First(b => b.name.EndsWith("LeftForeArm", StringComparison.OrdinalIgnoreCase));
        var hand = bones.First(b => b.name.EndsWith("LeftHand", StringComparison.OrdinalIgnoreCase));
        var eyeTransform = player.GetComponentInChildren<Camera>(true).transform;
        arm.transform.position += eyeTransform.TransformPoint(new Vector3(.65f, -.2f, .65f)) - shoulder.position;
        var elbowTarget = eyeTransform.TransformPoint(new Vector3(.57f, -.37f, .75f));
        shoulder.rotation = Quaternion.FromToRotation(forearm.position - shoulder.position, elbowTarget - shoulder.position) * shoulder.rotation;
        var palmTarget = eyeTransform.TransformPoint(new Vector3(.22f, -.53f, .75f));
        forearm.rotation = Quaternion.FromToRotation(hand.position - forearm.position, palmTarget - forearm.position) * forearm.rotation;
        arm.SetActive(false);
        Target(alexei.transform, IvanFinaleControl.Action.Rescue, "Поднять Алексея", new Vector3(.55f, 1.7f, .5f));
        alexei.GetComponent<BoxCollider>().center = Vector3.up * .9f;
        var body = Character("Father body", new Vector3(2.55f, .3f, 16.1f), true);
        body.transform.rotation = Quaternion.Euler(0, 180, 0);
        body.SetActive(false);

        var k17 = GameObject.Find("K17_Resonator_Root").transform;
        var roomWall = GameObject.Find("FinalLab_Environment").GetComponentsInChildren<BoxCollider>().Single(c => c.name == "ControlWest");
        float cabinetX = roomWall.bounds.max.x + .11f;
        Cube("Metro mains breaker", new Vector3(cabinetX, 1.05f, 15.4f), new Vector3(.22f, 1.2f, .65f), metal);
        var lever = Object.Instantiate(k17.Find("K17_ControlPanel/Breaker bay/K17_KnifeSwitch").gameObject, root).transform;
        lever.name = "Metro industrial knife switch";
        lever.SetPositionAndRotation(new Vector3(cabinetX + .21f, .98f, 15.4f), Quaternion.Euler(0, 90, 0));
        lever.localScale = Vector3.one * 2.2f;
        Target(lever, IvanFinaleControl.Action.Breaker, "Опустить рубильник", new Vector3(.13f, .15f, .12f));
        lever.GetComponent<BoxCollider>().center = Vector3.up * .06f;
        var latch = Cube("Safety latch", new Vector3(cabinetX + .27f, 1.35f, 15.4f), new Vector3(.17f, .07f, .30f), ivory);
        Target(latch, IvanFinaleControl.Action.Latch, "Снять фиксатор", null);
        Label("ТЯГОВАЯ СЕТЬ\n1 — СНЯТЬ ФИКСАТОР\n2 — ОПУСТИТЬ РЫЧАГ", new Vector3(cabinetX + .16f, 1.72f, 15.4f), Quaternion.Euler(0, -90, 0), .035f);

        var mode = k17.Find("K17_ControlPanel/K17_ModeSelector");
        for (int i = 0; i < 3; i++)
        {
            var toggle = Cube("Reverse resonance toggle " + (i + 1), mode.TransformPoint(new Vector3(-.08f + i * .08f, .18f, .02f)), new Vector3(.025f, .06f, .03f), ivory);
            toggle.rotation = k17.rotation;
            Target(toggle, IvanFinaleControl.Action.Switch, new[] { "Переключить полярность", "Переключить внешний контур", "Переключить поглощение" }[i], null).Index = i;
        }
        Label("СХЕМА ОТЦА — РЕЖИМ Б\n1 ↑ ОБРАТНАЯ ПОЛЯРНОСТЬ\n2 ↓ ВНЕШНЯЯ СВЯЗЬ\n3 ↑ ПОГЛОЩЕНИЕ", k17.TransformPoint(new Vector3(-.5f, 1.85f, .4f)), k17.rotation * Quaternion.Euler(0, 180, 0), .026f);
        var hold = k17.Find("K17_ControlPanel/Breaker bay/K17_KnifeSwitch");
        Target(hold, IvanFinaleControl.Action.Hold, "Удерживать E — обратный резонанс", new Vector3(.16f, .22f, .15f));
        Target(k17.Find("K17_ControlPanel/K17_OverloadCover"), IvanFinaleControl.Action.Cover, "Открыть защитную крышку", new Vector3(.13f, .10f, .08f));
        Target(k17.Find("K17_ControlPanel/K17_OverloadButton"), IvanFinaleControl.Action.Destroy, "Подтвердить необратимую перегрузку", new Vector3(.09f, .08f, .07f));
        Target(exit, IvanFinaleControl.Action.Exit, "Вынести Алексея в последний вагон", new Vector3(1, 2, .4f));
        var exitLabel = Label("К ПОСЛЕДНЕМУ ВАГОНУ", exit.position + Vector3.up * 1.1f,
            Quaternion.identity, .04f);
        exitLabel.SetParent(exit, true);
        exit.gameObject.SetActive(false);

        var screen = Cube("Containment surveillance monitor", new Vector3(-2.9f, 1.4f, 16.9f), new Vector3(.9f, .52f, .10f), metal);
        var display = GameObject.CreatePrimitive(PrimitiveType.Quad);
        display.name = "Live containment video";
        display.transform.SetParent(root);
        display.transform.position = screen.position + Vector3.back * .056f;
        display.transform.localScale = new Vector3(.82f, .45f, 1);
        Object.DestroyImmediate(display.GetComponent<Collider>());
        var eye = Mark("CCTV viewpoint", new Vector3(1.6f, 2.2f, 14.6f));
        eye.LookAt(rescue.position + new Vector3(-.8f, .3f, .3f));
        var beam = new GameObject("Entity extraction");
        beam.transform.SetParent(root);
        var line = beam.AddComponent<LineRenderer>();
        line.sharedMaterial = Mat("Anomaly", new Color(.09f, .025f, .16f));
        var energy = beam.AddComponent<IvanFinaleEnergy>();
        energy.Alexei = Mark("Alexei fragment emitter", rescue.position + new Vector3(-1f, .3f, .35f));
        energy.Core = Mark("Core extraction target", k17.TransformPoint(new Vector3(-.35f, 1.3f, .05f)));
        line.enabled = false;
        var station = Station(scene, arrival, out var carriageSpawn);
        var gameplay = root.gameObject.AddComponent<IvanFinaleController>();
        var finale = new SerializedObject(gameplay);
        finale.FindProperty("labExit").objectReferenceValue = exit.gameObject;
        var controlChair = GameObject.Find("ElectricalPanel").transform.parent.Find("Chair");
        if (controlChair != null)
        {
            var chairBounds = controlChair.GetComponentsInChildren<Collider>().First().bounds;
            var seat = Mark("Ivan seated eye", new Vector3(chairBounds.center.x, 1.16f, chairBounds.center.z + .17f));
            seat.LookAt(screen.position);
            finale.FindProperty("seatedEyePose").objectReferenceValue = seat;
        }
        finale.ApplyModifiedPropertiesWithoutUndo();
        gameplay.ModeSelector = mode;
        gameplay.GaugeNeedle = k17.Find("K17_ControlPanel/K17_AnalogGauge/Needle pivot");
        gameplay.IsolationSegments = k17.Find("K17_ControlPanel/K17_IsolationGauge/Calibrated indicator rail").GetComponentsInChildren<Renderer>();
        gameplay.Configure(player, start, arrival, actor.gameObject, alexei, arm, station,
            display.GetComponent<Renderer>(), eye, carriageSpawn, body);
        gameplay.SetVoiceClip(2, AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/FinalLab/IvanFinale/Entity_Father_Voice.wav"));
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Debug.Log("IVAN_FINALE_INSTALLED");
    }

    [MenuItem("Tools/Final Lab/Start Ivan phase in Play Mode")]
    public static void StartIvan()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
        Object.FindFirstObjectByType<IvanFinaleController>().BeginIvanPhase();
    }

    private static GameObject Character(string title, Vector3 at, bool father = false)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(father
            ? "Assets/Art/Models/Characters/MainHeroes/OldManErnest/Sitting Idle.fbx"
            : "Assets/Art/Models/Characters/MainHeroes/gavin-reed-detroit-become-human/source/Gavin.fbx");
        if (model == null) throw new InvalidOperationException("Character asset missing: " + title);
        var obj = new GameObject(title);
        obj.transform.SetParent(root);
        obj.transform.position = at;
        var visual = Object.Instantiate(model, obj.transform);
        foreach (var collider in obj.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
        foreach (var animator in obj.GetComponentsInChildren<Animator>()) animator.enabled = false;
        string posePath = father ? "Assets/Art/Models/Characters/MainHeroes/OldManErnest/Sitting Idle.fbx"
            : "Assets/Art/Models/Characters/MainHeroes/gavin-reed-detroit-become-human/source/Gavin@Standing Up.fbx";
        var pose = AssetDatabase.LoadAllAssetsAtPath(posePath).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        if (pose != null) pose.SampleAnimation(visual, Mathf.Max(0, pose.length - .05f));
        var renderers = visual.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) throw new InvalidOperationException("Character model has no geometry: " + title);
        Bounds bounds = renderers[0].bounds;
        foreach(var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        visual.transform.localScale *= (father ? 1.25f : 1.7f) / bounds.size.y;
        bounds = renderers[0].bounds;
        foreach(var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        visual.transform.position += at - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        foreach(var skin in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true)) skin.updateWhenOffscreen = true;
        var actor = obj.AddComponent<IvanFinaleActor>();
        actor.Deceased = father;
        return obj;
    }
    private static void MakeCarriedArm(GameObject model)
    {
        int triangles = 0, meshIndex = 0;
        foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is not SkinnedMeshRenderer skin) { renderer.enabled = false; continue; }
            var original = skin.sharedMesh;
            var weights = original.boneWeights;
            var armBones = skin.bones.Select(b => b.name.IndexOf("left", StringComparison.OrdinalIgnoreCase) >= 0 &&
                (b.name.IndexOf("arm", StringComparison.OrdinalIgnoreCase) >= 0 || b.name.IndexOf("hand", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 b.name.IndexOf("shoulder", StringComparison.OrdinalIgnoreCase) >= 0)).ToArray();
            bool ArmVertex(int v)
            {
                var w = weights[v];
                return (armBones[w.boneIndex0] ? w.weight0 : 0) + (armBones[w.boneIndex1] ? w.weight1 : 0) +
                    (armBones[w.boneIndex2] ? w.weight2 : 0) + (armBones[w.boneIndex3] ? w.weight3 : 0) > .4f;
            }
            if (weights.Length != original.vertexCount) { renderer.enabled = false; continue; }
            var mesh = Object.Instantiate(original);
            int before = triangles;
            for (int sub = 0; sub < original.subMeshCount; sub++)
            {
                var indices = original.GetTriangles(sub);
                var kept = new System.Collections.Generic.List<int>();
                for (int n = 0; n < indices.Length; n += 3)
                    if (ArmVertex(indices[n]) && ArmVertex(indices[n+1]) && ArmVertex(indices[n+2]))
                    { kept.Add(indices[n]); kept.Add(indices[n+1]); kept.Add(indices[n+2]); }
                triangles += kept.Count / 3;
                mesh.SetTriangles(kept, sub);
            }
            if (triangles == before) { renderer.enabled = false; Object.DestroyImmediate(mesh); continue; }
            string path = "Assets/Code/Scripts/Scenes/IvanFinale/CarriedArm_" + meshIndex++ + ".asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved == null) AssetDatabase.CreateAsset(mesh, path);
            else { EditorUtility.CopySerialized(mesh, saved); Object.DestroyImmediate(mesh); mesh = saved; }
            skin.sharedMesh = mesh;
        }
        if (triangles == 0) throw new InvalidOperationException("Could not extract Alexei's arm. Bones: " + string.Join(",", model.GetComponentsInChildren<Transform>(true).Select(t => t.name)));
    }

    private static GameObject Station(UnityEngine.SceneManagement.Scene destination, Transform arrival, out Transform carriageSpawn)
    {
        var source = EditorSceneManager.OpenScene("Assets/Level/Scenes/Locations/Metro.unity", OpenSceneMode.Additive);
        var station = new GameObject("Morning New York platform and stopped train");
        SceneManager.MoveGameObjectToScene(station, destination);
        station.transform.SetParent(root);
        var sourceRoots = source.GetRootGameObjects();
        var sourcePlayer = sourceRoots.SelectMany(r => r.GetComponentsInChildren<FirstPersonPlayer>(true)).Single();
        Vector3 platformPosition = sourcePlayer.transform.position;
        var train = sourceRoots.SelectMany(r => r.GetComponentsInChildren<MetroTrainArrival>(true)).Single();
        var setup = new SerializedObject(train);
        int axis = setup.FindProperty("axis").enumValueIndex;
        train.transform.position += (axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward) * setup.FindProperty("distance").floatValue;
        OpenTrainDoors(setup, train.transform, "forwardDoors", 1);
        OpenTrainDoors(setup, train.transform, "rearwardDoors", -1);
        var floor = train.GetComponentsInChildren<MeshRenderer>(true)
            .Where(r => r.name.StartsWith("SueloInterior_LP", StringComparison.Ordinal))
            .OrderBy(r => Mathf.Abs(r.bounds.center.z - platformPosition.z)).First();
        Vector3 floorPoint = floor.bounds.center;
        floorPoint.y = floor.bounds.max.y + .03f;
        foreach (var obj in sourceRoots)
        {
            if (obj.GetComponentInChildren<FirstPersonPlayer>(true) != null) continue;
            if (obj.GetComponent<Camera>() != null) { Object.DestroyImmediate(obj); continue; }
            foreach (var camera in obj.GetComponentsInChildren<Camera>(true)) Object.DestroyImmediate(camera.gameObject);
            foreach (var behaviour in obj.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
            foreach (var renderer in obj.GetComponentsInChildren<Renderer>(true))
            { renderer.lightmapIndex = -1; renderer.realtimeLightmapIndex = -1; }
            SceneManager.MoveGameObjectToScene(obj, destination);
            obj.transform.SetParent(station.transform, true);
        }
        EditorSceneManager.CloseScene(source, true);
        var offset = new Vector3(-250, 0, 0);
        station.transform.position += offset;
        var dawn = new GameObject("Early morning light").AddComponent<Light>();
        dawn.transform.SetParent(station.transform);
        dawn.transform.rotation = Quaternion.Euler(40, -40, 0);
        dawn.type = LightType.Directional;
        dawn.color = new Color(.70f, .80f, 1);
        dawn.intensity = .85f;
        // The original Metro spawn is upstairs. End the rescue at the open
        // carriage door instead, on the actual platform at track height.
        var toPlatform = platformPosition - floorPoint;
        float sideSign = Mathf.Sign(Vector3.Dot(toPlatform, train.transform.right));
        var side = train.transform.right * sideSign;
        arrival.position = floorPoint + offset + side * 4.3f;
        foreach (var barrier in station.GetComponentsInChildren<Collider>(true))
            if (barrier.name == "Block" || barrier.name == "Location Transition Zone") barrier.enabled = false;
        carriageSpawn = Mark("Last carriage arrival", floorPoint + offset);
        carriageSpawn.LookAt(new Vector3(arrival.position.x, carriageSpawn.position.y, arrival.position.z));
        var exit = Cube("Platform escape marker", arrival.position + Vector3.up, new Vector3(.25f, 1.4f, .2f), ivory);
        exit.SetParent(station.transform, true);
        exit.GetComponent<Renderer>().enabled = false;
        Target(exit, IvanFinaleControl.Action.Exit, "Остановиться на платформе", null);
        Label("ПЛАТФОРМА", arrival.position + Vector3.up * 2, Quaternion.LookRotation(side), .04f);
        station.SetActive(false);
        return station;
    }

    private static void OpenTrainDoors(SerializedObject setup, Transform train, string property, float direction)
    {
        var leaves = setup.FindProperty(property);
        for (int i = 0; i < leaves.arraySize; i++)
        {
            var part = leaves.GetArrayElementAtIndex(i);
            var filter = (MeshFilter)part.FindPropertyRelative("original").objectReferenceValue;
            var opposite = Object.Instantiate(filter.gameObject, filter.transform.parent);
            var otherMesh = (Mesh)part.FindPropertyRelative("oppositeMesh").objectReferenceValue;
            opposite.GetComponent<MeshFilter>().sharedMesh = otherMesh;
            if (opposite.TryGetComponent(out MeshCollider oppositeCollider)) oppositeCollider.sharedMesh = otherMesh;
            var platformMesh = (Mesh)part.FindPropertyRelative("platformMesh").objectReferenceValue;
            filter.sharedMesh = platformMesh;
            if (filter.TryGetComponent(out MeshCollider collider)) collider.sharedMesh = platformMesh;
            filter.transform.position += train.forward * (.85f * direction);
        }
    }
    private static Transform Mark(string title, Vector3 at)
    { var obj = new GameObject(title).transform; obj.SetParent(root); obj.position = at; return obj; }
    private static Transform Cube(string title, Vector3 at, Vector3 size, Material mat)
    {
        var obj = GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name = title;
        obj.transform.SetParent(root); obj.transform.position = at; obj.transform.localScale = size;
        obj.GetComponent<Renderer>().sharedMaterial = mat; return obj.transform;
    }
    private static Material Mat(string name, Color color)
    {
        const string folder = "Assets/Code/Scripts/Scenes/IvanFinale";
        string path = folder + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;
        mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color };
        AssetDatabase.CreateAsset(mat, path); return mat;
    }
    private static IvanFinaleControl Target(Transform target, IvanFinaleControl.Action action, string prompt, Vector3? size)
    {
        var control = target.gameObject.AddComponent<IvanFinaleControl>();
        control.Kind = action; control.Prompt = prompt; control.MovingPart = target;
        if (size.HasValue) target.gameObject.AddComponent<BoxCollider>().size = size.Value;
        return control;
    }
    private static Transform Label(string words, Vector3 at, Quaternion rotation, float size)
    {
        var text = new GameObject("Instruction").AddComponent<TextMesh>();
        text.transform.SetParent(root); text.transform.SetPositionAndRotation(at, rotation);
        text.text = words; text.fontSize = 48; text.characterSize = size * 10f / text.fontSize;
        text.anchor = TextAnchor.MiddleCenter; text.color = new Color(.85f, .8f, .64f);
        return text.transform;
    }
}
