using System;
using System.Linq;
using FinalLab.Investigation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class AlexeiLabSetup
{
    private const string Scene = "Assets/Level/Scenes/Locations/laba/FinalScene.unity";
    private const string Folder = "Assets/Code/Scripts/Scenes/AlexeiFinale/";
    private static TMP_FontAsset font;
    private static Material shell, metal, handle, indicator, ink, warmOutline, greenOutline;

    public static void Install()
    {
        var scene = EditorSceneManager.OpenScene(Scene);
        if (Object.FindFirstObjectByType<IvanFinaleController>() == null)
        {
            IvanFinaleSetup.Install();
            scene = EditorSceneManager.GetActiveScene();
        }

        var flow = Object.FindFirstObjectByType<FinalLabFlowController>();
        var hud = Object.FindFirstObjectByType<FinalLabInvestigationUI>();
        var player = Object.FindFirstObjectByType<FirstPersonPlayer>();
        var finale = Object.FindFirstObjectByType<IvanFinaleController>();
        var flowProperties = new SerializedObject(flow);
        var marker = (GameObject)flowProperties.FindProperty("doorMarker").objectReferenceValue;
        var old = GameObject.Find("Alexei lab gameplay");
        if (old != null) Object.DestroyImmediate(old);
        font = flow.Father.Subtitles.Body.font;
        shell = Material("Panel shell", "Universal Render Pipeline/Lit", new Color(.16f, .20f, .19f));
        metal = Material("Dark enamel", "Universal Render Pipeline/Lit", new Color(.07f, .085f, .08f));
        handle = Material("Lever handle", "Universal Render Pipeline/Lit", new Color(.48f, .39f, .23f));
        indicator = Material("Indicator glass", "Universal Render Pipeline/Unlit", new Color(.12f, .15f, .14f));
        ink = Material("Wiring diagram", "Universal Render Pipeline/Unlit", new Color(.67f, .64f, .50f));
        warmOutline = Material("Goal amber", "FinalLab/Rescue Outline", new Color(.93f, .72f, .33f), .004f);
        greenOutline = Material("Door trim", "Universal Render Pipeline/Unlit", new Color(.38f, .80f, .51f));

        var root = new GameObject("Alexei lab gameplay").transform;
        var powerWall = GameObject.Find("HallEntryEast").GetComponent<Collider>().bounds;
        var doorWall = GameObject.Find("ContainmentPartition_ObservationWindowOpening_Sill").GetComponent<Collider>().bounds;
        var power = BuildPanel(root, new Vector3(2.28f, 1.36f, powerWall.max.z + .035f), 0, true);
        var isolation = BuildPanel(root, new Vector3(1.89f, 1.35f, doorWall.min.z - .035f), 180, false);

        var markerRenderers = marker.GetComponentsInChildren<Renderer>(true);
        foreach (var r in markerRenderers) r.enabled = false;
        for (int i = marker.transform.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(marker.transform.GetChild(i).gameObject);
        var leaf = flow.FatherDoor.Leaf.bounds;
        marker.transform.SetPositionAndRotation(new Vector3(leaf.center.x, leaf.center.y, leaf.min.z - .028f), Quaternion.identity);
        marker.transform.localScale = Vector3.one;
        float frameWidth = leaf.size.x * .88f;
        float frameHeight = leaf.size.y * .91f;
        const float stroke = .009f;
        foreach (int side in new[] { -1, 1 })
        {
            Part(marker.transform, "Door edge", Vector3.right * side * frameWidth / 2,
                new Vector3(stroke, frameHeight, stroke), greenOutline, false);
            Part(marker.transform, "Door edge", Vector3.up * side * frameHeight / 2,
                new Vector3(frameWidth + stroke, stroke, stroke), greenOutline, false);
        }
        marker.SetActive(false);

        var canvas = new GameObject("Alexei gameplay HUD", typeof(Canvas), typeof(CanvasScaler));
        canvas.transform.SetParent(root, false);
        var overlay = canvas.GetComponent<Canvas>();
        overlay.renderMode = RenderMode.ScreenSpaceOverlay;
        overlay.sortingOrder = 23;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .5f;

        var gameplay = root.gameObject.AddComponent<AlexeiLabGameplay>();
        var fields = new SerializedObject(gameplay);
        Assign(fields, "flow", flow);
        Assign(fields, "player", player);
        Assign(fields, "investigationHud", hud);
        SetPanel(fields.FindProperty("power"), power);
        SetPanel(fields.FindProperty("isolation"), isolation);
        Assign(fields, "objective", HUD(canvas.transform, "Objective", 1, -67, 730, 91, new Color(.96f, .83f, .58f)));
        Assign(fields, "prompt", HUD(canvas.transform, "Prompt", 0, 190, 750, 48, Color.white));
        Assign(fields, "feedback", HUD(canvas.transform, "Feedback", 0, 260, 960, 52, new Color(1, .61f, .43f)));
        Assign(fields, "subtitle", HUD(canvas.transform, "Subtitle", 0, 91, 1120, 100, Color.white));
        Assign(fields, "chamberLight", GameObject.Find("Containment_White").GetComponent<Light>());
        Assign(fields, "sounds", root.gameObject.AddComponent<AudioSource>());
        var equipment = power.face.gameObject.AddComponent<AudioSource>();
        equipment.clip = Audio("Equipment hum");
        equipment.loop = true;
        equipment.playOnAwake = false;
        equipment.spatialBlend = 1;
        equipment.volume = .38f;
        Assign(fields, "equipment", equipment);
        Assign(fields, "click", Audio("Switch click"));
        Assign(fields, "error", Audio("Cutout"));
        Assign(fields, "startup", Audio("Relay startup"));
        fields.ApplyModifiedPropertiesWithoutUndo();
        flow.SkipGameplayForTesting = false;
        flow.SkipIvanGameplayForTesting = false;
        Assign(flowProperties, "ivanFinale", finale);
        flowProperties.FindProperty("skipGameplayForTesting").boolValue = false;
        flowProperties.FindProperty("skipIvanGameplayForTesting").boolValue = false;
        flowProperties.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(flow);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save FinalScene.");
        Debug.Log("ALEXEI_FINAL_SCENE_INSTALLED");
    }

    private static AlexeiLabGameplay.Panel BuildPanel(Transform parent, Vector3 at, float yaw, bool power)
    {
        var title = power ? "РАСПРЕДЕЛИТЕЛЬНЫЙ ЩИТ" : "БЛОК ИЗОЛЯЦИИ";
        var root = Group(parent, title, at);
        root.localRotation = Quaternion.Euler(0, yaw, 0);
        float width = power ? .90f : .61f;
        var face = Part(root, "Cabinet", Vector3.zero, new Vector3(width, .57f, .10f), shell);
        Part(root, "Faceplate", new Vector3(0, 0, .052f), new Vector3(width - .034f, .532f, .014f), metal, false);
        for (int x = -1; x <= 1; x += 2)
        for (int y = -1; y <= 1; y += 2)
            Part(root, "Screw", new Vector3(x * (width / 2 - .028f), y * .264f, .064f), new Vector3(.013f, .013f, .009f), handle, false);
        Label(root, title, 0, .222f, width - .055f, .062f, 4.7f);
        var diagram = power
            ? "ОСНОВНАЯ ЛИНИЯ [АВАРИЯ] ─┐\nРЕЗЕРВНЫЙ ГЕНЕРАТОР ────┴─ ШИНА ── ОТСЕК\nАВАРИЙНАЯ ЛИНИЯ БЛОКИРУЕТ РЕЗЕРВ"
            : "ВНИМАНИЕ\nРАЗРЫВ КОНТУРА МОЖЕТ ПРИВЕСТИ\nК ПОТЕРЕ СТАБИЛЬНОСТИ ОБЪЕКТА";
        Label(root, diagram, 0, .133f, width - .045f, .12f, power ? 2.8f : 2.45f);
        Part(root, "Enamel separator", new Vector3(0, .061f, .064f), new Vector3(width - .06f, .003f, .003f), ink, false);

        string[] names = power ? new[] { "ОСНОВНОЙ\nКОНТУР", "РЕЗЕРВНЫЙ\nГЕНЕРАТОР", "ПИТАНИЕ\nОТСЕКА" } :
            new[] { "А · ВНЕШНЯЯ\nСВЯЗЬ", "Б · ИЗОЛЯЦИЯ\nКАМЕРЫ" };
        int count = names.Length;
        var result = new AlexeiLabGameplay.Panel
        {
            face = face.GetComponent<Collider>(),
            outline = Outline(face, warmOutline),
            targets = new Collider[count],
            handles = new Transform[count],
            hover = new GameObject[count],
            lamps = new Renderer[count],
            status = Label(root, power ? "ПИТАНИЕ ОТСУТСТВУЕТ" : "ВНЕШНИЙ КОНТУР АКТИВЕН", 0, -.228f, width - .04f, .06f, 3.8f)
        };
        for (int i = 0; i < count; i++)
        {
            float x = ((count - 1) / 2f - i) * .258f;
            Label(root, names[i], x, .010f, .245f, .078f, power ? 3.4f : 3f);
            var pivot = Group(root, "Handle pivot", new Vector3(x, -.109f, .086f));
            pivot.localRotation = Quaternion.Euler(i == 0 ? -28 : 28, 0, 0);
            Part(root, "Lever recess", new Vector3(x, -.115f, .064f), new Vector3(.117f, .111f, .013f), shell, false);
            var grip = Part(pivot, "Lever grip", new Vector3(0, .025f, .025f), new Vector3(.036f, .089f, .037f), handle, false);
            Part(pivot, "Lever shaft", new Vector3(0, 0, .012f), new Vector3(.017f, .076f, .023f), ink, false);
            var target = Group(root, "Click target", new Vector3(x, -.109f, .112f)).gameObject.AddComponent<BoxCollider>();
            target.size = new Vector3(.14f, .132f, .105f);
            result.targets[i] = target;
            result.handles[i] = pivot;
            result.hover[i] = Outline(grip, warmOutline);
            result.lamps[i] = Part(root, "Status lens", new Vector3(x + .086f, -.104f, .071f),
                new Vector3(.027f, .022f, .011f), indicator, false).GetComponent<Renderer>();
        }
        result.overview = Group(root, "Inspection camera pose", new Vector3(0, 0, .82f));
        result.overview.localRotation = Quaternion.Euler(0, 180, 0);
        return result;
    }

    private static TMP_Text Label(Transform parent, string words, float x, float y, float width, float height, float size)
    {
        var label = new GameObject(words.Split('\n')[0], typeof(RectTransform), typeof(TextMeshPro)).GetComponent<TextMeshPro>();
        label.transform.SetParent(parent, false);
        label.transform.localPosition = new Vector3(x, y, .066f);
        label.transform.localRotation = Quaternion.Euler(0, 180, 0);
        label.transform.localScale = Vector3.one * .1f;
        label.rectTransform.sizeDelta = new Vector2(width, height) * 10;
        label.alignment = TextAlignmentOptions.Center;
        label.font = font;
        label.color = new Color(.91f, .85f, .71f);
        label.fontSize = size;
        label.text = words;
        return label;
    }

    private static TMP_Text HUD(Transform parent, string name, float anchorY, float offsetY, float width, float height, Color tint)
    {
        var background = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        background.transform.SetParent(parent, false);
        var rect = background.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, anchorY);
        rect.anchoredPosition = new Vector2(0, offsetY);
        rect.sizeDelta = new Vector2(width, height);
        background.color = new Color(.02f, .025f, .024f, .81f);
        background.raycastTarget = false;
        var label = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        label.transform.SetParent(rect, false);
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(15, 4);
        label.rectTransform.offsetMax = new Vector2(-15, -4);
        label.font = font;
        label.color = tint;
        label.fontSize = name == "Objective" ? 29 : 26;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        background.gameObject.SetActive(false);
        return label;
    }

    private static Transform Group(Transform parent, string name, Vector3 at)
    {
        var group = new GameObject(name).transform;
        group.SetParent(parent, false);
        group.localPosition = at;
        return group;
    }

    private static Transform Part(Transform parent, string name, Vector3 at, Vector3 size, Material material, bool solid = true)
    {
        var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = at;
        part.transform.localScale = size;
        part.GetComponent<Renderer>().sharedMaterial = material;
        if (!solid) Object.DestroyImmediate(part.GetComponent<Collider>());
        return part.transform;
    }

    private static GameObject Outline(Transform original, Material material)
    {
        var outline = Part(original, "Outline", Vector3.zero, Vector3.one, material, false).gameObject;
        outline.SetActive(false);
        return outline;
    }

    private static Material Material(string name, string shader, Color color, float width = 0)
    {
        string path = Folder + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find(shader)) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        material.color = color;
        if (width > 0) material.SetFloat("_Width", width);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static AudioClip Audio(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>(Folder + name + ".wav");

    private static void Assign(SerializedObject fields, string name, Object obj) =>
        fields.FindProperty(name).objectReferenceValue = obj;

    private static void SetPanel(SerializedProperty property, AlexeiLabGameplay.Panel panel)
    {
        property.FindPropertyRelative("overview").objectReferenceValue = panel.overview;
        property.FindPropertyRelative("face").objectReferenceValue = panel.face;
        property.FindPropertyRelative("outline").objectReferenceValue = panel.outline;
        property.FindPropertyRelative("status").objectReferenceValue = panel.status;
        SetArray(property.FindPropertyRelative("targets"), panel.targets);
        SetArray(property.FindPropertyRelative("handles"), panel.handles);
        SetArray(property.FindPropertyRelative("hover"), panel.hover);
        SetArray(property.FindPropertyRelative("lamps"), panel.lamps);
    }

    private static void SetArray(SerializedProperty property, Object[] objects)
    {
        property.arraySize = objects.Length;
        for (int i = 0; i < objects.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = objects[i];
    }
}
