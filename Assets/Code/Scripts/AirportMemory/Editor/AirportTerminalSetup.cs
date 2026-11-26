using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

public static partial class AirportTerminalSetup
{
    private const string ScenePath = "Assets/Level/Scenes/Locations/MindsScenes/AirportCargoMemory.unity";
    private const string InterfaceName = "Cargo Terminal UI";
    private const string ScreenTexture = "Assets/Art/Materials/AirportMemory/Cargo Terminal Display Texture.png";
    private const string FontPath = "Assets/Art/Materials/AirportMemory/Cargo Terminal Cyrillic SDF.asset";
    private const string FontSource = "Assets/TextMesh Pro/Fonts/LiberationSans.ttf";
    private static readonly string[] Fields = { "Номер контейнера", "Дата отправки", "Отправитель", "Адрес получателя" };
    private static readonly string[] InitialValues = { "F-04", "12.06.1992", "Сергей Карамазов", "Семейный багаж, Нью-Йорк" };
    private static readonly Color32 Ink = new(24, 26, 25, 255);
    private static readonly Color32 Card = new(41, 43, 39, 255);
    private static readonly Color32 Frame = new(83, 76, 59, 255);
    private static readonly Color32 Amber = new(233, 194, 128, 255);
    private static readonly Color32 Text = new(245, 237, 220, 255);
    private static readonly Color32 Muted = new(187, 178, 157, 255);
    private static readonly (string Name, float Height)[] TallBoxes =
        { ("Cargo box 04 — SM_Boxes_05", .72f), ("Cargo box 14 — SM_Boxes_05", .76f) };

    public static void Survey()
    {
        var scene = OpenBatch();
        foreach (var document in Find<ReadableDocument>(scene))
            Debug.Log($"AIRPORT_DOCUMENT {document.name}: {document.Title} / {document.Text}");
        var terminal = Find<CargoPuzzleTerminal>(scene).Single();
        var player = Find<FirstPersonPlayer>(scene).Single();
        var view = player.GetComponentInChildren<Camera>();
        Debug.Log($"AIRPORT_FPS eye {view.transform.localPosition.y:F2} m; EventSystems: {Find<EventSystem>(scene).Count()}");
        foreach (var item in Find<Transform>(scene).Where(t => t == terminal.transform ||
                 t.name == "Scattered asset boxes" || t.name.StartsWith("Baggage trolley ", StringComparison.Ordinal) ||
                 t.name.StartsWith("Warehouse hangar", StringComparison.Ordinal)))
        {
            Bounds bounds = BoundsFor(item);
            Debug.Log($"AIRPORT_BOUNDS {item.name}: {bounds.size:F2} m, center {bounds.center:F2}");
            if (item == terminal.transform || item.name == "Scattered asset boxes" || item.name.StartsWith("Warehouse hangar", StringComparison.Ordinal))
                foreach (Transform child in item)
                    if (child.gameObject.activeInHierarchy && child.GetComponentInChildren<Renderer>() != null)
                        Debug.Log($"AIRPORT_PROP {child.name}: {BoundsFor(child).size:F2} m, center {BoundsFor(child).center:F2}");
        }
    }

    public static void Apply()
    {
        var scene = OpenBatch();
        var documents = Find<ReadableDocument>(scene).ToDictionary(doc => doc.Title);
        ReplaceDocumentText(documents["Семейный багаж"], "12.06.2004", "12.06.1992");
        ReplaceDocumentText(documents["Технический контейнер"], "14.06.2004", "14.06.1992", true);
        ReplaceDocumentText(documents["Личные вещи"], "16.06.2004", "16.06.1992");
        ReplaceScreenYear();
        BuildInterface(Find<CargoPuzzleTerminal>(scene).Single());
        VerifyScene(scene);
        Require(EditorSceneManager.SaveScene(scene), "Could not save AirportCargoMemory.");
        Debug.Log("AIRPORT_APPLIED " + scene.path);
    }

    public static void Verify() => VerifyScene(OpenBatch());

    public static void ApplyPropScale()
    {
        var scene = OpenBatch();
        FitCardboardBoxes(scene);
        VerifyScene(scene);
        Require(EditorSceneManager.SaveScene(scene), "Could not save the corrected cardboard box scales.");
    }

    internal static void FitCardboardBoxes(Scene scene)
    {
        Require(scene.path == ScenePath || string.IsNullOrEmpty(scene.path), "Cardboard adjustment is airport-only.");
        var boxes = Find<Transform>(scene).Single(item => item.name == "Scattered asset boxes");
        foreach (var (name, height) in TallBoxes)
        {
            var box = boxes.Find(name);
            Require(box != null, "Missing cardboard box: " + name);
            var before = BoundsFor(box);
            Require(before.size.y > 0, "Box has no visible height: " + name);
            if (Mathf.Abs(before.size.y - height) > .001f)
            {
                box.localScale *= height / before.size.y;
                var scaled = BoundsFor(box);
                box.localPosition += boxes.InverseTransformVector(Vector3.up * (before.min.y - scaled.min.y));
                PrefabUtility.RecordPrefabInstancePropertyModifications(box);
                EditorSceneManager.MarkSceneDirty(scene);
            }
            Physics.SyncTransforms();
            var after = BoundsFor(box);
            var colliders = box.GetComponentsInChildren<Collider>().Where(item => item.enabled && !item.isTrigger).ToArray();
            Require(Mathf.Abs(after.size.y - height) < .002f && Mathf.Abs(after.min.y - before.min.y) < .002f &&
                colliders.Any(item => item.bounds.Intersects(after)), "Box lost its ground contact or collider: " + name);
            Debug.Log($"AIRPORT_BOX_SCALE {name}: {before.size.y:F2} m -> {after.size.y:F2} m; floor {after.min.y:F3} m");
        }
    }

    private static void ReplaceDocumentText(ReadableDocument document, string oldDate, string newDate, bool fullName = false)
    {
        Require(document.Text.Contains(oldDate) || document.Text.Contains(newDate), "Unexpected document text: " + document.Title);
        var data = new SerializedObject(document);
        string text = document.Text.Replace(oldDate, newDate);
        if (fullName) text = text.Replace("И. Карамазов", "Иван Карамазов");
        data.FindProperty("documentText").stringValue = text;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    internal static void BuildInterface(CargoPuzzleTerminal terminal)
    {
        var font = InterfaceFont();
        var scene = terminal.gameObject.scene;
        var previous = scene.GetRootGameObjects().SingleOrDefault(root => root.name == InterfaceName);
        if (previous != null) UnityEngine.Object.DestroyImmediate(previous);

        var root = new GameObject(InterfaceName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        SceneManager.MoveGameObjectToScene(root, scene);
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        var modal = Rect("Terminal overlay", root.transform);
        modal.anchorMin = Vector2.zero;
        modal.anchorMax = Vector2.one;
        modal.offsetMin = Vector2.zero;
        modal.offsetMax = Vector2.zero;
        ImageOn(modal, new Color32(10, 12, 12, 143));

        var window = Rect("Terminal window", modal);
        Dock(window, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, 1320, 780);
        ImageOn(window, Frame);
        Plate(window, "Window background", 2, 2, 1316, 776, Ink);
        Plate(window, "Header rule", 2, 2, 1316, 5, Amber);
        Label(window, font, "Title", "ТЕРМИНАЛ ГРУЗОВОГО УЧЁТА", 44, 31, 1232, 51, 42, Text, bold: true);
        var archive = Label(window, font, "Archive", "АРХИВ ОТПРАВОК • 1992 • ТЕРМИНАЛ К-17", 46, 89, 1230, 30, 20, Amber);
        archive.characterSpacing = 5;
        Plate(window, "Header divider", 44, 130, 1232, 2, Frame);
        Label(window, font, "Instructions", "Найди запись технического контейнера. Сопоставь данные трёх накладных",
            44, 149, 1232, 34, 27, Text);

        var facts = new Button[Fields.Length];
        var values = new TMP_Text[Fields.Length];
        for (int i = 0; i < Fields.Length; i++)
        {
            var card = Plate(window, Fields[i], 44 + i % 2 * 624, 204 + i / 2 * 136, 608, 122, Frame);
            var face = Plate(card, "Button surface", 1, 1, 606, 120, Card);
            facts[i] = PointerButton(face, new Color32(52, 55, 48, 255), new Color32(62, 60, 49, 255));
            Plate(face, "Amber marker", 0, 0, 4, 120, new Color32(142, 111, 65, 255));
            Label(face, font, "Field label", Fields[i], 22, 13, 494, 28, 23, Amber);
            values[i] = Label(face, font, "Selected value", InitialValues[i], 22, 44, 499, 39, 31, Text, bold: true);
            Label(face, font, "Mouse hint", "ЛКМ — сменить вариант", 22, 87, 390, 23, 18, Muted);
            var arrow = Plate(face, "Cycle indicator", 541, 37, 44, 44, Ink);
            Label(arrow, font, "Arrow", ">", 0, 0, 44, 44, 30, Amber, TextAlignmentOptions.Center);
        }

        var result = Plate(window, "Match result", 44, 487, 1232, 158, new Color32(31, 34, 30, 255));
        var accent = Plate(result, "Result accent", 0, 0, 4, 158, Amber).GetComponent<Image>();
        var resultTitle = Label(result, font, "Result title", "ОЖИДАНИЕ ПРОВЕРКИ", 23, 22, 385, 38, 27, Amber, bold: true);
        var resultRecord = Label(result, font, "Waybill number and date", "", 23, 64, 385, 38, 30, Text, bold: true);
        Plate(result, "Result divider", 418, 19, 1, 120, Frame);
        var resultDetails = Label(result, font, "Result details", "", 442, 13, 763, 132, 25, Text, TextAlignmentOptions.TopLeft, wrap: true);
        resultDetails.lineSpacing = 7f;

        var submit = Plate(window, "Compare", 44, 676, 608, 62, new Color32(218, 180, 110, 255));
        var submitButton = PointerButton(submit, new Color32(244, 207, 143, 255), new Color32(196, 156, 92, 255));
        Label(submit, font, "Compare label", "СОПОСТАВИТЬ", 0, 0, 608, 62, 27, Ink, TextAlignmentOptions.Center, true);
        var close = Plate(window, "Close border", 668, 676, 608, 62, Frame);
        var closeFace = Plate(close, "Close", 1, 1, 606, 60, Card);
        var closeButton = PointerButton(closeFace, new Color32(53, 55, 50, 255), new Color32(64, 62, 52, 255));
        Label(closeFace, font, "Close label", "ЗАКРЫТЬ [E / Esc]", 0, 0, 606, 60, 27, Text, TextAlignmentOptions.Center, true);

        var prompt = Rect("Terminal interaction hint", root.transform);
        Dock(prompt, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 74), 484, 60);
        ImageOn(prompt, new Color32(21, 23, 22, 232));
        var key = Plate(prompt, "E key", 10, 10, 60, 40, new Color32(218, 180, 110, 255));
        Label(key, font, "Key label", "[E]", 0, 0, 60, 40, 24, Ink, TextAlignmentOptions.Center, true);
        Label(prompt, font, "Interaction label", "Проверить грузовую накладную", 84, 0, 384, 60, 24, Text);

        var clue = Rect("Obtained waybill", root.transform);
        Dock(clue, Vector2.one, Vector2.one, new Vector2(-34, -30), 445, 48);
        ImageOn(clue, new Color32(24, 32, 26, 236));
        Plate(clue, "Confirmation marker", 0, 0, 4, 48, new Color32(172, 211, 153, 255));
        Label(clue, font, "Clue label", "УЛИКА: накладная К-17 / Иван Карамазов", 15, 0, 415, 48, 20, Text);

        var data = new SerializedObject(terminal);
        Reference(data, "panel", modal.gameObject);
        Reference(data, "prompt", prompt.gameObject);
        Reference(data, "clue", clue.gameObject);
        Reference(data, "submit", submitButton);
        Reference(data, "dismiss", closeButton);
        Reference(data, "feedbackTitle", resultTitle);
        Reference(data, "feedbackRecord", resultRecord);
        Reference(data, "feedbackDetails", resultDetails);
        Reference(data, "feedbackAccent", accent);
        References(data.FindProperty("facts"), facts);
        References(data.FindProperty("values"), values);
        data.ApplyModifiedPropertiesWithoutUndo();
        modal.gameObject.SetActive(false);
        prompt.gameObject.SetActive(false);
        clue.gameObject.SetActive(false);
    }

    private static RectTransform Rect(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static TMP_FontAsset InterfaceFont()
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font != null) return font;
        var source = AssetDatabase.LoadAssetAtPath<Font>(FontSource);
        Require(source != null, "The project font is missing.");
        font = TMP_FontAsset.CreateFontAsset(source, 56, 6, GlyphRenderMode.SDFAA, 1024, 1024,
            AtlasPopulationMode.Dynamic);
        string chars = new string(Enumerable.Range(32, 95).Concat(Enumerable.Range(0x410, 64)).Select(c => (char)c).ToArray());
        Require(font.TryAddCharacters(chars + "Ёё—•·«»"), "Could not build the airport terminal glyph atlas.");
        font.name = "Cargo Terminal Cyrillic SDF";
        font.atlasPopulationMode = AtlasPopulationMode.Static;
        AssetDatabase.CreateAsset(font, FontPath);
        AssetDatabase.AddObjectToAsset(font.atlasTexture, font);
        AssetDatabase.AddObjectToAsset(font.material, font);
        AssetDatabase.SaveAssetIfDirty(font);
        return font;
    }

    private static void Dock(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(width, height);
    }

    private static RectTransform Place(Transform parent, string name, float x, float y, float width, float height)
    {
        var rect = Rect(name, parent);
        var topLeft = new Vector2(0, 1);
        Dock(rect, topLeft, topLeft, new Vector2(x, -y), width, height);
        return rect;
    }

    private static Image ImageOn(RectTransform rect, Color color)
    {
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static RectTransform Plate(Transform parent, string name, float x, float y, float width, float height, Color color)
    {
        var rect = Place(parent, name, x, y, width, height);
        ImageOn(rect, color);
        return rect;
    }

    private static TMP_Text Label(Transform parent, TMP_FontAsset font, string name, string content, float x, float y,
        float width, float height, int size, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft,
        bool bold = false, bool wrap = false)
    {
        var label = Place(parent, name, x, y, width, height).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.text = content;
        label.fontSize = size;
        label.color = color;
        label.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        label.alignment = alignment;
        label.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        return label;
    }

    private static Button PointerButton(RectTransform rect, Color hover, Color pressed)
    {
        var surface = rect.GetComponent<Image>();
        var normal = surface.color;
        surface.color = Color.white;
        var button = rect.gameObject.AddComponent<Button>();
        surface.raycastTarget = true;
        button.targetGraphic = surface;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.colors = new ColorBlock
        {
            normalColor = normal,
            highlightedColor = hover,
            pressedColor = pressed,
            selectedColor = normal,
            disabledColor = normal,
            colorMultiplier = 1f,
            fadeDuration = .08f
        };
        return button;
    }

    private static void Reference(SerializedObject data, string name, UnityEngine.Object value) =>
        data.FindProperty(name).objectReferenceValue = value;

    private static void References(SerializedProperty property, UnityEngine.Object[] values)
    {
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    private static void ReplaceScreenYear()
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        var font = TMP_FontAsset.CreateFontAsset(AssetDatabase.LoadAssetAtPath<Font>(FontSource), 18, 0,
            GlyphRenderMode.SMOOTH, 128, 128, AtlasPopulationMode.Dynamic);
        try
        {
            Require(texture.LoadImage(File.ReadAllBytes(ScreenTexture)) && texture.width == 512 && texture.height == 320,
                "Unexpected dimensions for the existing terminal screen.");
            Require(font.TryAddCharacters("192"), "Could not rasterize the new year.");
            var pixels = texture.GetPixels32();
            var before = (Color32[])pixels.Clone();
            const int left = 36, right = 82, bottom = 48, top = 66;
            var background = pixels[58 * texture.width + 34];
            var foreground = new Color32(133, 113, 76, 255);
            for (int y = bottom; y < top; y++)
                for (int x = left; x < right; x++) pixels[y * texture.width + x] = background;

            var atlas = font.atlasTexture;
            var alpha = atlas.GetRawTextureData<byte>();
            Require(alpha.Length == atlas.width * atlas.height, "The year atlas should have a single coverage channel.");
            float pen = 38;
            foreach (char digit in "1992")
            {
                var glyph = font.characterLookupTable[digit].glyph;
                var rect = glyph.glyphRect;
                int x0 = Mathf.RoundToInt(pen + glyph.metrics.horizontalBearingX);
                int y0 = Mathf.RoundToInt(50 + glyph.metrics.horizontalBearingY) - rect.height;
                Require(x0 >= left && x0 + rect.width <= right && y0 >= bottom && y0 + rect.height <= top,
                    "Replacement digit exceeds the original year area.");
                for (int y = 0; y < rect.height; y++)
                    for (int x = 0; x < rect.width; x++)
                    {
                        float coverage = alpha[(rect.y + y) * atlas.width + rect.x + x] / 255f;
                        pixels[(y0 + y) * texture.width + x0 + x] = Color32.Lerp(background, foreground, coverage);
                    }
                pen += glyph.metrics.horizontalAdvance;
            }
            if (!pixels.SequenceEqual(before))
            {
                texture.SetPixels32(pixels);
                texture.Apply();
                File.WriteAllBytes(ScreenTexture, texture.EncodeToPNG());
                AssetDatabase.ImportAsset(ScreenTexture, ImportAssetOptions.ForceSynchronousImport);
            }
        }
        finally
        {
            foreach (var atlas in font.atlasTextures) UnityEngine.Object.DestroyImmediate(atlas);
            UnityEngine.Object.DestroyImmediate(font.material);
            UnityEngine.Object.DestroyImmediate(font);
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    private static void VerifyScene(Scene scene)
    {
        var terminal = Find<CargoPuzzleTerminal>(scene).Single();
        var data = new SerializedObject(terminal);
        foreach (string name in new[] { "panel", "prompt", "clue", "submit", "dismiss", "feedbackTitle", "feedbackRecord", "feedbackDetails", "feedbackAccent" })
            Require(data.FindProperty(name).objectReferenceValue != null, "Missing terminal reference: " + name);
        foreach (string name in new[] { "facts", "values" })
        {
            var list = data.FindProperty(name);
            Require(list.arraySize == 4, "Expected four terminal fields.");
            for (int i = 0; i < list.arraySize; i++)
                Require(list.GetArrayElementAtIndex(i).objectReferenceValue != null, "Missing terminal field reference.");
        }
        var eventSystem = Find<EventSystem>(scene).Single();
        Require(eventSystem.GetComponent<InputSystemUIInputModule>() != null, "The scene Input System UI module is missing.");
        var panel = ((GameObject)data.FindProperty("panel").objectReferenceValue).transform;
        Require(panel.GetComponentsInChildren<TMP_Dropdown>(true).Length == 0 && panel.GetComponentsInChildren<Dropdown>(true).Length == 0,
            "Unexpected dropdown in the terminal.");

        var documents = Find<ReadableDocument>(scene).ToDictionary(doc => doc.Title, doc => doc.Text);
        Require(documents.Count == 3 && documents["Семейный багаж"].Contains("12.06.1992. Отправитель: Сергей Карамазов.") &&
            documents["Технический контейнер"].Contains("14.06.1992. Подпись отправителя: Иван Карамазов.") &&
            documents["Личные вещи"].Contains("16.06.1992. Отправитель: Михаил Орлов.") && documents.Values.All(text => !text.Contains("2004")),
            "The documents do not match the requested dates and senders.");

        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
        Type type = typeof(CargoPuzzleTerminal);
        var options = (string[][])type.GetField("Options", flags).GetValue(null);
        var expected = new[]
        {
            new[] { "F-04", "К-17", "L-09" },
            new[] { "12.06.1992", "14.06.1992", "16.06.1992" },
            new[] { "Сергей Карамазов", "Иван Карамазов", "Михаил Орлов" },
            new[] { "Семейный багаж, Нью-Йорк", "Технический склад, Нью-Йорк", "Личные вещи, Москва" }
        };
        var next = (Func<int, int, int>)type.GetMethod("NextIndex", flags).CreateDelegate(typeof(Func<int, int, int>));
        var correct = (Func<int[], bool>)type.GetMethod("MatchesWaybill", flags).CreateDelegate(typeof(Func<int[], bool>));
        for (int field = 0; field < expected.Length; field++)
        {
            Require(options[field].SequenceEqual(expected[field]), "Unexpected terminal values.");
            Require(next(field, 0) == 1 && next(field, 1) == 2 && next(field, 2) == 0, "A field does not wrap to its first value.");
        }
        for (int a = 0; a < 3; a++)
            for (int b = 0; b < 3; b++)
                for (int c = 0; c < 3; c++)
                    for (int d = 0; d < 3; d++)
                        Require(correct(new[] { a, b, c, d }) == (a == 1 && b == 1 && c == 1 && d == 1),
                            "Incorrect puzzle result in the 81-combination truth table.");

        var uiRoot = scene.GetRootGameObjects().Single(root => root.name == InterfaceName);
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        string uiText = string.Concat(uiRoot.GetComponentsInChildren<TMP_Text>(true).Select(label => label.text));
        Require(font.HasCharacters(uiText + string.Concat(expected.SelectMany(field => field)) + "НАКЛАДНАЯ НАЙДЕНА ДАННЫЕ НЕ СОВПАДАЮТ ·"),
            "Cyrillic or a required punctuation character is missing in the static TMP font.");
        var npc = new SerializedObject(Find<VictorMemoryDialogue>(scene).Single());
        Require(npc.FindProperty("terminal").objectReferenceValue == terminal && npc.FindProperty("dialogue").objectReferenceValue != null,
            "Tikhon's existing waybill dialogue hook is missing.");
        Debug.Log("AIRPORT_BATCH_OK documents=3 factFields=4 truthTable=81 uiEventSystems=1 waybillDialogue=linked");
    }

    private static Bounds BoundsFor(Transform group)
    {
        var renderers = group.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
        if (renderers.Length == 0) return new Bounds(group.position, Vector3.zero);
        var result = renderers[0].bounds;
        foreach (var renderer in renderers.Skip(1)) result.Encapsulate(renderer.bounds);
        return result;
    }

    private static Scene OpenBatch()
    {
        Require(Application.isBatchMode, "Only Unity Batch Mode may change AirportCargoMemory.");
        return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    private static System.Collections.Generic.IEnumerable<T> Find<T>(Scene scene) where T : Component =>
        scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true));

    private static void Require(bool valid, string error)
    {
        if (!valid) throw new InvalidOperationException(error);
    }
}
