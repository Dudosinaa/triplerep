#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class ThirdMindSceneBuilder
{
    private const string ScenePath = "Assets/Level/Scenes/Locations/MindsScenes/ThirdMind-interogation_room.unity";
    private const string MichaelModelPath = "Assets/Art/Models/Characters/MindHeroes/ThirdMind/Michael/source/MikeAlger.fbx";
    private const string PoliceModelPath = "Assets/Art/Models/Characters/MindHeroes/ThirdMind/Police-Man/source/Police man 1.fbx";
    private const string MichaelTexturePath = "Assets/Art/Models/Characters/MindHeroes/ThirdMind/Michael/textures/MikeAlger_Texture.jpeg";
    private const string PoliceTexture0Path = "Assets/Art/Models/Characters/MindHeroes/ThirdMind/Police-Man/textures/Police_man_1_packed0_diffuse.png";
    private const string PoliceTexture1Path = "Assets/Art/Models/Characters/MindHeroes/ThirdMind/Police-Man/textures/Police_man_1_packed1_diffuse.png";
    private const string DataPath = "Assets/Third Mind Data";
    private const string PrefabPath = "Assets/Third Mind Prefabs";
    private const string MaterialPath = "Assets/Third Mind Materials";
    private const string DialogueAssetPath = DataPath + "/ThirdMindDialogue.asset";
    private const string MichaelPrefabPath = PrefabPath + "/Michael Reid.prefab";
    private const string PolicePrefabPath = PrefabPath + "/Police-Man.prefab";
    private const string WindowMaterialPath = MaterialPath + "/Third Mind Window Glass.mat";
    private const string CctvEntryPath = DataPath + "/C12_2353_Entry.png";
    private const string CctvExitPath = DataPath + "/C12_0017_Exit.png";
    private const string IvanModelPath = "Assets/Art/Models/Characters/MindHeroes/ThirdMind/IVAN.glb";
    private const string OldLampPath = "Assets/Art/Models/Environment/Mind/old_lamp.glb";
    private const string WallMaterialPath = MaterialPath + "/Interrogation Walls Neutral Gray.mat";

    [MenuItem("Tools/Third Mind/Build Interrogation Memory")]
    public static void BuildFromMenu() => BuildInterrogationMemory();

    [MenuItem("Tools/Third Mind/Apply Narrative Improvements")]
    public static void ApplyNarrativeImprovements()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before updating the Third Mind scene.");
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var sequence = UnityEngine.Object.FindFirstObjectByType<ThirdMindSequence>();
        if (sequence == null || sequence.documents == null || sequence.documents.Any(d => d == null) ||
            !sequence.documents.Select(d => d.documentId).OrderBy(id => id).SequenceEqual(new[] { 0, 1, 2, 3 }))
            throw new InvalidOperationException("Expected the existing sequence and four documents in the interrogation scene.");
        var existingDialogue = sequence.dialogueData;
        if (existingDialogue == null) throw new InvalidOperationException("The existing dialogue asset is missing.");
        string[] openingBefore = OpeningSnapshot(existingDialogue);
        string spotlight = SpotlightSnapshot();
        var table = GetTable();
        var lamp = ConfigureTableLamp(scene, table);
        FocusInterrogationLighting(table.bounds);
        var contents = sequence.documents.Select(DocumentSnapshot).ToArray();
        float tabletopHeight = WorldMeshBounds(table).max.y;
        var report = sequence.documents.Single(document => document.documentId == 3);
        foreach (var document in sequence.documents)
        {
            document.bodyPrint = document.paperVisual.GetComponentsInChildren<TMP_Text>(true)
                .FirstOrDefault(text => text.transform.parent == document.paperVisual && text.rectTransform.anchorMax.y >= 0.6f && text.rectTransform.anchorMax.y < 0.7f);
            if (document.bodyPrint == null) throw new InvalidOperationException("The existing printed document body could not be found: " + document.documentId);
            var rule = document.paperVisual.Find("Printed rule");
            if (rule != null) UnityEngine.Object.DestroyImmediate(rule.gameObject);
            float lift = tabletopHeight + 0.18f - document.paperVisual.position.y;
            if (lift > 0f) document.transform.position += Vector3.up * lift;
            EditorUtility.SetDirty(document);
        }

        if (GenerateCctvStills())
        {
            sequence.cctvEntryPhoto = AssetDatabase.LoadAssetAtPath<Sprite>(CctvEntryPath);
            sequence.cctvExitPhoto = AssetDatabase.LoadAssetAtPath<Sprite>(CctvExitPath);
        }
        else
        {
            sequence.cctvEntryPhoto = null;
            sequence.cctvExitPhoto = null;
        }
        AssignCctvStills(report, sequence.cctvEntryPhoto, sequence.cctvExitPhoto);
        existingDialogue.confession = FinalConfession();
        EditorUtility.SetDirty(existingDialogue);
        ConfigureWallMaterial();
        EditorUtility.SetDirty(sequence);
        EditorSceneManager.MarkSceneDirty(scene);
        if (SpotlightSnapshot() != spotlight) throw new InvalidOperationException("Interrogation spotlight changed during the update.");
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save the interrogation scene.");
        AssetDatabase.SaveAssetIfDirty(existingDialogue);
        Debug.Log("THIRD_MIND_UPDATE_OK | Saved the existing interrogation scene.");
        ValidateNarrativeImprovements();
        var saved = UnityEngine.Object.FindFirstObjectByType<ThirdMindSequence>();
        if (!contents.SequenceEqual(saved.documents.Select(DocumentSnapshot)) || !openingBefore.SequenceEqual(OpeningSnapshot(saved.dialogueData)))
            throw new InvalidOperationException("Original document text, chronology or opening dialogue changed.");
        Debug.Log("THIRD_MIND_CONTENT_UNCHANGED | Original document titles, text, event order and opening dialogue verified.");
        Debug.Log("THIRD_MIND_SPOTLIGHT_UNCHANGED | Serialized light and transform match the original.");
    }

    private static string[] OpeningSnapshot(ThirdMindDialogueData data) => data.opening
        .Select(line => string.Join("\n", line.characterName, line.text,
            line.voiceClip != null ? AssetDatabase.GetAssetPath(line.voiceClip) : string.Empty)).ToArray();

    private static string DocumentSnapshot(ThirdMindDocumentSurface document) => string.Join("\n",
        document.documentId.ToString(), document.documentTitle, document.fullText, document.firstEventTime,
        string.Join("\n", document.paperVisual.GetComponentsInChildren<TMP_Text>(true).Select(text => text.text)));

    private static void SetRect(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static Renderer GetTable()
    {
        var room = GameObject.Find("interogation_room (1)");
        return (room != null ? FindRenderer(room.transform, "Table_Table_0") : null)
            ?? throw new InvalidOperationException("The existing interrogation table could not be found.");
    }

    private static GameObject ConfigureTableLamp(Scene scene, Renderer table)
    {
        var lamp = GameObject.Find("old_lamp");
        if (lamp == null)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(OldLampPath);
            if (model == null) throw new IOException("Could not import " + OldLampPath);
            lamp = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
            lamp.name = "old_lamp";
            PrefabUtility.RecordPrefabInstancePropertyModifications(lamp);
        }
        if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(lamp) != OldLampPath)
            throw new InvalidOperationException("The old_lamp instance does not reference the supplied GLB.");

        var tabletop = WorldMeshBounds(table);
        var transform = lamp.transform;
        var baseMesh = LampPart(lamp, "smbase");
        var foot = WorldMeshBounds(baseMesh);
        if (!LampSitsOnTable(foot, tabletop) || Vector3.Dot(transform.up, Vector3.up) < 0.95f)
        {
            transform.SetPositionAndRotation(tabletop.center, Quaternion.Euler(0f, -9f, 0f));
            transform.localScale = Vector3.one;
            var lampParts = lamp.GetComponentsInChildren<Renderer>().Select(WorldMeshBounds).ToArray();
            float naturalHeight = lampParts.Max(bounds => bounds.max.y) - lampParts.Min(bounds => bounds.min.y);
            if (naturalHeight < 0.001f) throw new InvalidOperationException("The supplied old_lamp has no rendered geometry.");
            transform.localScale = Vector3.one * (34f / naturalHeight);
            foot = WorldMeshBounds(baseMesh);
            transform.position += new Vector3(tabletop.max.x - foot.max.x - 8.5f,
                tabletop.max.y - foot.min.y + 0.025f, tabletop.min.z - foot.min.z + 1.4f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(transform);
        }

        var light = lamp.GetComponentInChildren<Light>(true);
        if (light == null)
        {
            light = new GameObject("Warm Table Light", typeof(Light)).GetComponent<Light>();
            light.transform.SetParent(transform, false);
        }
        var bulb = WorldMeshBounds(LampPart(lamp, "circle001")).center;
        var target = tabletop.center + new Vector3(17f, tabletop.extents.y, -3f);
        light.transform.SetPositionAndRotation(bulb + new Vector3(-1.8f, -1.5f, 0.5f),
            Quaternion.LookRotation(target - bulb));
        light.type = LightType.Spot;
        light.color = new Color(1f, 0.79f, 0.58f);
        light.intensity = 620f;
        light.range = 122f;
        light.spotAngle = 80f;
        light.innerSpotAngle = 52f;
        light.shadows = LightShadows.None;
        light.renderMode = LightRenderMode.ForcePixel;
        light.lightmapBakeType = LightmapBakeType.Realtime;
        if (PrefabUtility.IsPartOfPrefabInstance(light))
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(light);
            PrefabUtility.RecordPrefabInstancePropertyModifications(light.transform);
        }
        return lamp;
    }

    private static Renderer LampPart(GameObject lamp, string nodeName)
    {
        var node = lamp.GetComponentsInChildren<Transform>().FirstOrDefault(t => Normalize(t.name) == nodeName);
        return (node != null ? node.GetComponentInChildren<Renderer>() : null)
            ?? throw new InvalidOperationException("The supplied old_lamp is missing mesh part " + nodeName);
    }

    private static Bounds WorldMeshBounds(Renderer renderer)
    {
        var mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
        if (mesh == null) return renderer.bounds;
        var local = mesh.bounds;
        var matrix = renderer.transform.localToWorldMatrix;
        var bounds = new Bounds(matrix.MultiplyPoint3x4(local.center), Vector3.zero);
        for (int i = 0; i < 8; i++)
        {
            var corner = new Vector3((i & 1) == 0 ? local.min.x : local.max.x,
                (i & 2) == 0 ? local.min.y : local.max.y,
                (i & 4) == 0 ? local.min.z : local.max.z);
            bounds.Encapsulate(matrix.MultiplyPoint3x4(corner));
        }
        return bounds;
    }

    private static bool LampSitsOnTable(Bounds foot, Bounds top) =>
        Mathf.Abs(foot.min.y - top.max.y) <= 0.10f && foot.min.x >= top.center.x && foot.max.x <= top.max.x &&
        foot.min.z >= top.min.z && foot.max.z <= top.center.z;

    private static void FocusInterrogationLighting(Bounds tabletop)
    {
        RenderSettings.ambientLight = new Color(0.070f, 0.079f, 0.092f);
        RenderSettings.ambientIntensity = 0.25f;
        var directional = GameObject.Find("Directional Light")?.GetComponent<Light>();
        if (directional != null && directional.type == LightType.Directional) directional.intensity = 0.18f;
        foreach (string side in new[] { "Left", "Right" })
        {
            var fill = GameObject.Find("Third Mind Wall Fill " + side)?.GetComponent<Light>();
            if (fill == null) continue;
            var target = new Vector3(tabletop.center.x, tabletop.max.y + 32f, tabletop.center.z + 23f);
            fill.transform.rotation = Quaternion.LookRotation(target - fill.transform.position);
            fill.intensity = 365f;
            fill.range = 240f;
            fill.spotAngle = 72f;
            fill.innerSpotAngle = 53f;
        }
    }

    private static void ValidateLamp(Renderer table, GameObject lamp, ThirdMindDocumentSurface[] documents)
    {
        var top = WorldMeshBounds(table);
        var foot = WorldMeshBounds(LampPart(lamp, "smbase"));
        var cuffs = FindRenderer(table.transform.root, "Handcuffs_Handcuffs_0");
        if (!LampSitsOnTable(foot, top))
            throw new InvalidOperationException($"old_lamp is not seated on the near-right tabletop. Foot {foot.min:F3} – {foot.max:F3}; table {top.min:F3} – {top.max:F3}; root {lamp.transform.position:F3}; scale {lamp.transform.localScale:F3}.");
        var lampBounds = lamp.GetComponentsInChildren<Renderer>().Select(WorldMeshBounds).ToArray();
        if (lampBounds.Any(b => b.min.y < top.max.y - 0.08f || (cuffs != null && b.Intersects(cuffs.bounds)) ||
            documents.Any(d => b.Intersects(d.GetComponent<BoxCollider>().bounds))))
            throw new InvalidOperationException("old_lamp intersects the table, handcuffs or a document.");
        if (documents.Any(d => d.paperVisual.Find("Printed rule") != null))
            throw new InvalidOperationException("A decorative printed rule is still present.");
        Debug.Log($"THIRD_MIND_TABLE_LAMP_OK | asset={OldLampPath}; foot={foot.center}; height={CombinedBounds(lamp).size.y:0.0}; front-right clearance verified.");
    }

    private static string SpotlightSnapshot()
    {
        var spot = GameObject.Find("Interrogation_SpotLight")?.GetComponent<Light>();
        if (spot == null) throw new InvalidOperationException("Original interrogation spotlight is missing.");
        return EditorJsonUtility.ToJson(spot) + EditorJsonUtility.ToJson(spot.transform);
    }

    public static void ValidateNarrativeImprovements()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var sequence = UnityEngine.Object.FindFirstObjectByType<ThirdMindSequence>();
        if (sequence == null) throw new InvalidOperationException("Validation failed: ThirdMindSequence is missing.");
        if (sequence.documents == null || sequence.documents.Length != 4 || sequence.documents.Any(d => d == null))
            throw new InvalidOperationException("Validation failed: four document references are not intact.");
        if (!sequence.documents.Select(d => d.documentId).OrderBy(id => id).SequenceEqual(new[] { 0, 1, 2, 3 }))
            throw new InvalidOperationException("Validation failed: document IDs do not encode the required chronological order.");
        string[] requiredEvidence = { "22:30", "23:44", "23:51", "00:16", "23:53", "00:17" };
        string allDocumentText = string.Join("\n", sequence.documents.Select(d => d.fullText));
        if (requiredEvidence.Any(time => !allDocumentText.Contains(time))) throw new InvalidOperationException("Validation failed: a required timestamp is missing from the documents.");
        var dialogue = AssetDatabase.LoadAssetAtPath<ThirdMindDialogueData>(DialogueAssetPath);
        if (dialogue == null || dialogue.opening.Count == 0 || !dialogue.confession.Select(line => line.characterName + line.text)
            .SequenceEqual(FinalConfession().Select(line => line.characterName + line.text)))
            throw new InvalidOperationException("Validation failed: the final dialogue does not match the seven specified lines.");
        var missingScripts = scene.GetRootGameObjects().Sum(CountMissingScripts);
        if (missingScripts != 0) throw new InvalidOperationException("Validation failed: missing scripts found: " + missingScripts);
        if (sequence.cctvEntryPhoto == null || sequence.cctvExitPhoto == null)
            Debug.LogWarning("THIRD_MIND_C12_MISSING | No new C-12 images were saved.");
        var lamp = GameObject.Find("old_lamp");
        if (lamp == null) throw new InvalidOperationException("The saved scene has no old_lamp instance.");
        Physics.SyncTransforms();
        ValidateLamp(GetTable(), lamp, sequence.documents);
        ValidateDocumentLayout(sequence, lamp);
        if (!SceneSwitch.IsSceneAvailable(sequence.returnSceneIndex) ||
            Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(sequence.returnSceneIndex)) != "Train")
            throw new InvalidOperationException("Validation failed: return scene index does not resolve to Train.");
        var setup = GameObject.Find("Third Mind Sequence Setup");
        var michael = setup != null ? setup.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Michael Reid") : null;
        if (michael == null || michael.GetComponent<ThirdMindMichaelNervousIdle>() == null)
            throw new InvalidOperationException("Validation failed: Michael's nervous idle animation is not attached.");
        var spot = GameObject.Find("Interrogation_SpotLight")?.GetComponent<Light>();
        if (spot == null) throw new InvalidOperationException("Validation failed: original table spotlight is missing.");
        if (spot.type != LightType.Spot || Mathf.Abs(spot.intensity - 3200f) > 0.01f || Mathf.Abs(spot.range - 285f) > 0.01f || Mathf.Abs(spot.spotAngle - 72f) > 0.01f || Mathf.Abs(spot.innerSpotAngle - 54f) > 0.01f || spot.shadows != LightShadows.Soft || Mathf.Abs(spot.shadowStrength - 0.82f) > 0.01f || Mathf.Abs(spot.color.r - 0.78f) > 0.01f || Mathf.Abs(spot.color.g - 0.87f) > 0.01f || Mathf.Abs(spot.color.b - 1f) > 0.01f || Vector3.Distance(spot.transform.position, new Vector3(-427.47568f, 751.9458f, 769.32495f)) > 0.02f || Quaternion.Angle(spot.transform.rotation, Quaternion.Euler(90f, 0f, 0f)) > 0.02f)
            throw new InvalidOperationException("Validation failed: the interrogation spotlight no longer matches its preserved settings.");
        Debug.Log($"THIRD_MIND_VALIDATION_OK | scene={ScenePath} | docs={sequence.documents.Length} | opening={dialogue.opening.Count} | confession={dialogue.confession.Count} | missingScripts={missingScripts} | michaelIdleBones={michael.GetComponent<ThirdMindMichaelNervousIdle>()} | spotlightIntensity={spot.intensity} range={spot.range} angle={spot.spotAngle} position={spot.transform.position} rotation={spot.transform.rotation.eulerAngles}");
        var room = GameObject.Find("interogation_room (1)");
        var wallRenderer = room != null ? FindRenderer(room.transform, "Walls_Walls_0") : null;
        if (wallRenderer == null || wallRenderer.sharedMaterial == null || wallRenderer.sharedMaterial.name != "Interrogation Walls Neutral Gray")
            throw new InvalidOperationException("Validation failed: the interrogation wall material is not the neutral-gray material.");
        var bones = michael.GetComponentsInChildren<Transform>(true);
        foreach (var bone in bones.Where(t => Normalize(t.name).EndsWith("lefthand") || Normalize(t.name).EndsWith("righthand") || Normalize(t.name).EndsWith("leftupleg") || Normalize(t.name).EndsWith("rightupleg")))
            Debug.Log($"THIRD_MIND_REST_POSE_BONE | {bone.name} position={bone.position}");
        foreach (var side in new[] { "left", "right" })
        {
            var hand = bones.FirstOrDefault(t => Normalize(t.name).EndsWith(side + "hand"));
            var thigh = bones.FirstOrDefault(t => Normalize(t.name).Contains(side) && Normalize(t.name).EndsWith("upleg"));
            if (hand == null || thigh == null || Vector3.Distance(hand.position, thigh.position) > 18f)
                throw new InvalidOperationException("Validation failed: Michael's " + side + " hand is not resting alongside the thigh.");
            Debug.Log($"THIRD_MIND_HAND_BY_THIGH_OK | {side} hand={hand.position} thigh={thigh.position} distance={Vector3.Distance(hand.position, thigh.position):0.0}");
        }
        var wall = wallRenderer.sharedMaterial;
        var wallColor = wall.HasProperty("baseColorFactor") ? wall.GetColor("baseColorFactor") : wall.HasProperty("_BaseColor") ? wall.GetColor("_BaseColor") : wall.GetColor("_Color");
        if (wallColor.r < 0.6f || wallColor.g < 0.6f || wallColor.b < 0.6f)
            throw new InvalidOperationException("Validation failed: wall neutral-gray tint is too dark.");
        Debug.Log($"THIRD_MIND_VISUALS_OK | entryStill={AssetDatabase.GetAssetPath(sequence.cctvEntryPhoto)} | exitStill={AssetDatabase.GetAssetPath(sequence.cctvExitPhoto)} | wallMaterial={wall.name} baseColor={wallColor} texture={wall.GetTexture("baseColorTexture")}");
        foreach (var doc in sequence.documents.OrderBy(d => d.documentId))
            Debug.Log($"THIRD_MIND_DOC_OK | id={doc.documentId} | title={doc.documentTitle} | hasText={!string.IsNullOrWhiteSpace(doc.fullText)} | eventTime={doc.firstEventTime}");
    }

    private static int CountMissingScripts(GameObject root)
    {
        int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root);
        foreach (Transform child in root.transform) count += CountMissingScripts(child.gameObject);
        return count;
    }

    private static void ValidateDocumentLayout(ThirdMindSequence sequence, GameObject lamp)
    {
        var camera = sequence.memoryCamera;
        var anchor = sequence.tableViewAnchor;
        var ordered = sequence.documents.OrderBy(document => Vector3.Dot(document.transform.position, anchor.right)).ToArray();
        if (ThirdMindSequence.IsChronological(ordered) ||
            !ThirdMindSequence.IsChronological(ordered.OrderBy(document => document.documentId).ToArray()) ||
            ordered.Any(document => document.inspected))
            throw new InvalidOperationException("Expected shuffled documents, a valid chronological solution, and zero documents studied.");

        var startingPose = new Pose(camera.transform.position, camera.transform.rotation);
        float aspect = camera.aspect;
        camera.aspect = 16f / 9f;
        string previewDirectory = ReadArgument("-thirdMindPreviewDirectory");
        try
        {
            if (!string.IsNullOrEmpty(previewDirectory)) SaveScenePreview(camera, previewDirectory, "interrogation-room");
            camera.transform.SetPositionAndRotation(anchor.position, anchor.rotation);
            Physics.SyncTransforms();
            foreach (var document in ordered)
            {
                var viewport = camera.WorldToViewportPoint(document.paperVisual.position);
                var ray = camera.ViewportPointToRay(viewport);
                if (!Physics.Raycast(ray, out var hit, camera.farClipPlane) || hit.collider.gameObject != document.gameObject)
                    throw new InvalidOperationException($"Document {document.documentId} is obstructed in the table view.");
            }
            ValidateSwapPaths(ordered, anchor.up, lamp.GetComponentsInChildren<Renderer>().Select(WorldMeshBounds).ToArray());
            if (!string.IsNullOrEmpty(previewDirectory)) SaveScenePreview(camera, previewDirectory, "interrogation-table");
            foreach (var document in ordered)
            {
                var paper = document.transform;
                var slot = new Pose(paper.position, paper.rotation);
                ValidateDocumentHighlight(document);
                document.SetReading(true);
                document.AdvanceHighlight(0.18f);
                var target = ThirdMindSequence.GetReadingPose(document, camera);
                paper.SetPositionAndRotation(target.position, target.rotation);
                var corners = new Vector3[4];
                document.paperVisual.GetWorldCorners(corners);
                if (corners.Select(camera.WorldToViewportPoint).Any(point => point.x < 0f || point.x > 1f || point.y < 0.09f || point.y > 1f || point.z <= camera.nearClipPlane))
                    throw new InvalidOperationException($"Document {document.documentId} does not fit above the reading hint.");
                foreach (var text in document.paperVisual.GetComponentsInChildren<TMP_Text>().Where(t => t == document.bodyPrint || t.text == document.documentTitle))
                {
                    text.ForceMeshUpdate(true, true);
                    if (text.renderedHeight > text.rectTransform.rect.height + 1f)
                        throw new InvalidOperationException($"Document {document.documentId} text exceeds its layout bounds: {text.renderedHeight} > {text.rectTransform.rect.height}.");
                }
                if (!string.IsNullOrEmpty(previewDirectory)) SaveScenePreview(camera, previewDirectory, $"interrogation-document-{document.documentId + 1}");
                paper.SetPositionAndRotation(slot.position, slot.rotation);
                document.SetReading(false);
                document.AdvanceHighlight(0.18f);
            }
            Debug.Log("THIRD_MIND_DOCUMENT_LAYOUT_OK | Four world colliders reachable; highlight states tested; full reading text fits at 16:9; swap paths clear; initial and solved orders verified.");
        }
        finally
        {
            camera.ResetAspect();
            if (!Mathf.Approximately(camera.aspect, aspect)) camera.aspect = aspect;
            camera.transform.SetPositionAndRotation(startingPose.position, startingPose.rotation);
        }
    }

    private static void ValidateDocumentHighlight(ThirdMindDocumentSurface document)
    {
        var bare = document.paperImage.color;
        document.SetHovered(true);
        document.AdvanceHighlight(0.09f);
        var halfway = document.paperImage.color;
        document.AdvanceHighlight(0.09f);
        var yellow = document.paperImage.color;
        if (halfway.r <= bare.r || yellow.r <= halfway.r || yellow.g <= bare.g)
            throw new InvalidOperationException("Warm hover tint did not fade in: " + document.documentId);
        document.SetSelected(true);
        document.AdvanceHighlight(0.18f);
        var blue = document.paperImage.color;
        document.SetHovered(false);
        document.AdvanceHighlight(0.18f);
        if (blue.b <= yellow.b || document.paperImage.color != blue)
            throw new InvalidOperationException("Blue selection did not take priority and persist: " + document.documentId);
        document.SetSelected(false);
        document.AdvanceHighlight(0.18f);
        if (document.paperImage.color != bare)
            throw new InvalidOperationException("The original paper tint did not return: " + document.documentId);
    }

    private static void ValidateSwapPaths(ThirdMindDocumentSurface[] ordered, Vector3 cameraUp, Bounds[] lampBounds)
    {
        var bounds = ordered.Select(document => document.GetComponent<BoxCollider>().bounds).ToArray();
        var laneAxis = Vector3.ProjectOnPlane(cameraUp, Vector3.up).normalized;
        for (int pair = 0; pair < bounds.Length - 1; pair++)
        {
            float clearance = (bounds[pair].size.z + bounds[pair + 1].size.z) * 0.5f + 2f;
            ThirdMindSequence.SwapLanes(laneAxis, clearance, out var rear, out var front);
            for (int direction = 0; direction <= 1; direction++)
            {
                var first = bounds[pair + direction];
                var second = bounds[pair + 1 - direction];
                var start = first.center;
                var target = second.center;
                for (int step = 0; step <= 100; step++)
                {
                    float t = step / 100f;
                    first.center = ThirdMindSequence.SwapPosition(start, target, rear, t);
                    second.center = ThirdMindSequence.SwapPosition(target, start, front, t);
                    if (first.Intersects(second) || bounds.Where((item, index) => index != pair && index != pair + 1)
                        .Concat(lampBounds).Any(item => first.Intersects(item) || second.Intersects(item)))
                        throw new InvalidOperationException($"Documents overlap another document or the lamp when exchanging slots {pair + 1} and {pair + 2}.");
                }
            }
        }
    }

    private static string ReadArgument(string name)
    {
        var arguments = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(arguments, name);
        return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null;
    }

    private static void SaveScenePreview(Camera camera, string directory, string name)
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
        Directory.CreateDirectory(directory);
        Canvas.ForceUpdateCanvases();
        var target = RenderTexture.GetTemporary(1280, 720, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var previous = RenderTexture.active;
        var previousTarget = camera.targetTexture;
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try
        {
            if (GraphicsSettings.currentRenderPipeline != null)
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
            else
            {
                camera.targetTexture = target;
                camera.Render();
            }
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            File.WriteAllBytes(Path.Combine(directory, name + ".png"), image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            UnityEngine.Object.DestroyImmediate(image);
        }
    }

    private static void ConfigureWallMaterial()
    {
        var room = GameObject.Find("interogation_room (1)");
        var renderer = room != null ? FindRenderer(room.transform, "Walls_Walls_0") : null;
        if (renderer == null || renderer.sharedMaterials.Length == 0) throw new InvalidOperationException("Interrogation room wall renderer/material was not found.");
        var wall = AssetDatabase.LoadAssetAtPath<Material>(WallMaterialPath);
        if (wall == null || !renderer.sharedMaterials.Contains(wall))
            throw new InvalidOperationException("The scene-specific wall material is not assigned.");
        SetMaterialColor(wall, new Color(0.78f, 0.79f, 0.81f, 1f));
        if (wall.HasProperty("emissiveFactor")) wall.SetColor("emissiveFactor", new Color(0.029f, 0.031f, 0.036f, 1f));
        EditorUtility.SetDirty(wall);
        AssetDatabase.SaveAssetIfDirty(wall);
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("baseColorFactor")) material.SetColor("baseColorFactor", color);
    }

    private static void AssignCctvStills(ThirdMindDocumentSurface report, Sprite entry, Sprite exit)
    {
        if (report == null) return;
        foreach (var image in report.GetComponentsInChildren<Image>(true).Where(i => i.gameObject.name.StartsWith("Surveillance photo", StringComparison.Ordinal)))
        {
            bool isEntry = image.gameObject.name.Contains("23:53");
            image.sprite = isEntry ? entry : exit;
            image.gameObject.SetActive(image.sprite != null);
            image.preserveAspect = true;
            image.color = Color.white;
            float x = isEntry ? 0.065f : 0.515f;
            SetRect(image.rectTransform, new Vector2(x, 0.12f), new Vector2(x + 0.42f, 0.332f));
            foreach (Transform child in image.transform)
                if (child.name == "Person silhouette" || child.name == "Head silhouette") child.gameObject.SetActive(false);
            var caption = image.GetComponentInChildren<TMP_Text>(true);
            if (caption != null)
            {
                SetRect(caption.rectTransform, new Vector2(0f, -0.18f), new Vector2(1f, 0.075f));
                caption.enableAutoSizing = true;
                caption.fontSizeMin = 7f;
                caption.fontSizeMax = 9f;
                caption.color = new Color(0.24f, 0.21f, 0.17f);
            }
        }
    }

    private static bool GenerateCctvStills()
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
        {
            Debug.LogWarning("C12_3D_STILLS_UNAVAILABLE | A graphics device is required to render the surveillance photos.");
            return false;
        }
        var preview = EditorSceneManager.NewPreviewScene();
        var materials = new List<Material>();
        Texture2D entry = null;
        Texture2D exit = null;
        try
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(IvanModelPath);
            if (source == null) throw new FileNotFoundException("Original rigged C-12 actor could not be loaded.", IvanModelPath);
            var stage = new GameObject("Temporary C-12 Corridor");
            SceneManager.MoveGameObjectToScene(stage, preview);
            var wall = StageMaterial("Corridor walls", new Color(0.43f, 0.45f, 0.40f));
            var floor = StageMaterial("Corridor floor", new Color(0.32f, 0.33f, 0.34f));
            var door = StageMaterial("Painted S-04 door", new Color(0.26f, 0.32f, 0.29f));
            var trim = StageMaterial("Door trim", new Color(0.21f, 0.22f, 0.20f));
            materials.AddRange(new[] { wall, floor, door, trim });
            StageCube(stage.transform, "Floor", new Vector3(0f, -0.08f, 1f), new Vector3(3.3f, 0.16f, 9.4f), floor);
            StageCube(stage.transform, "Left wall", new Vector3(-1.66f, 1.5f, 0f), new Vector3(0.16f, 3f, 7.4f), wall);
            StageCube(stage.transform, "Right wall", new Vector3(1.66f, 1.5f, 0f), new Vector3(0.16f, 3f, 7.4f), wall);
            StageCube(stage.transform, "Ceiling", new Vector3(0f, 3f, 0f), new Vector3(3.4f, 0.12f, 7.4f), wall);
            StageCube(stage.transform, "Wall beside door (left)", new Vector3(-1.04f, 1.5f, 3.58f), new Vector3(1.08f, 3f, 0.18f), wall);
            StageCube(stage.transform, "Wall beside door (right)", new Vector3(1.31f, 1.5f, 3.58f), new Vector3(0.58f, 3f, 0.18f), wall);
            StageCube(stage.transform, "Lintel", new Vector3(0.26f, 2.58f, 3.58f), new Vector3(1.52f, 0.84f, 0.18f), wall);
            StageCube(stage.transform, "Room beyond S-04", new Vector3(0.23f, 1.4f, 5.1f), new Vector3(2.6f, 2.8f, 0.15f), trim);
            StageCube(stage.transform, "S-04 left jamb", new Vector3(-0.50f, 1.08f, 3.46f), new Vector3(0.09f, 2.16f, 0.11f), trim);
            StageCube(stage.transform, "S-04 right jamb", new Vector3(1.02f, 1.08f, 3.46f), new Vector3(0.09f, 2.16f, 0.11f), trim);
            StageCube(stage.transform, "S-04 head jamb", new Vector3(0.26f, 2.14f, 3.46f), new Vector3(1.61f, 0.09f, 0.11f), trim);
            var doorLeaf = StageCube(stage.transform, "Open door S-04", new Vector3(0.81f, 1.02f, 4.04f), new Vector3(1.23f, 2.02f, 0.07f), door);
            doorLeaf.transform.localRotation = Quaternion.Euler(0f, 67f, 0f);
            StageCube(stage.transform, "Door plaque", new Vector3(0.25f, 2.36f, 3.455f), new Vector3(0.63f, 0.23f, 0.035f), trim);
            var plaque = new GameObject("S-04", typeof(TextMesh));
            plaque.transform.SetParent(stage.transform, false);
            plaque.transform.localPosition = new Vector3(0.25f, 2.36f, 3.43f);
            var text = plaque.GetComponent<TextMesh>();
            text.text = "S-04";
            text.fontSize = 80;
            text.characterSize = 0.022f;
            text.anchor = TextAnchor.MiddleCenter;
            text.color = new Color(0.9f, 0.9f, 0.80f);

            var cameraObject = new GameObject("Fixed C-12 Camera", typeof(Camera));
            cameraObject.transform.SetParent(stage.transform, false);
            var camera = cameraObject.GetComponent<Camera>();
            camera.enabled = false;
            camera.scene = preview;
            camera.transform.position = new Vector3(-1.35f, 2.52f, -1.3f);
            camera.transform.LookAt(new Vector3(0.08f, 1.19f, 2.8f));
            camera.fieldOfView = 42f;
            camera.aspect = 16f / 9f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.13f, 0.12f);
            AddCctvLight(stage.transform, new Vector3(-0.62f, 2.78f, 1.68f), new Color(0.88f, 0.96f, 0.87f), 7f, 6.5f);
            AddCctvLight(stage.transform, new Vector3(0.83f, 2.55f, 4.65f), new Color(0.96f, 0.85f, 0.69f), 2.8f, 3.3f);
            AddCctvLight(stage.transform, new Vector3(-0.85f, 1.42f, -0.25f), new Color(0.86f, 0.91f, 1f), 1.6f, 7f);

            var actor = new GameObject("Ivan").transform;
            actor.SetParent(stage.transform, false);
            var character = (GameObject)PrefabUtility.InstantiatePrefab(source, preview);
            character.transform.SetParent(actor, false);
            var renderers = character.GetComponentsInChildren<SkinnedMeshRenderer>();
            if (renderers.Length == 0) throw new InvalidOperationException("The C-12 model has no skinned meshes.");
            foreach (var renderer in renderers) renderer.updateWhenOffscreen = true;
            float height = PosedBounds(renderers).size.y;
            if (height < 0.01f) throw new InvalidOperationException("The C-12 model has no usable bounds.");
            actor.localScale = Vector3.one * (1.78f / height);
            var bones = character.GetComponentsInChildren<Transform>(true);
            var restRotations = bones.Select(bone => bone.localRotation).ToArray();

            PoseIvan(actor, bones, true);
            actor.position += Vector3.up * -PosedBounds(renderers).min.y;
            entry = CaptureCctv(camera, true);
            for (int i = 0; i < bones.Length; i++) bones[i].localRotation = restRotations[i];
            PoseIvan(actor, bones, false);
            actor.position += Vector3.up * -PosedBounds(renderers).min.y;
            exit = CaptureCctv(camera, false);
            SaveAndImportCctv(entry, CctvEntryPath);
            SaveAndImportCctv(exit, CctvExitPath);
            ConfigureCctvImport(CctvEntryPath);
            ConfigureCctvImport(CctvExitPath);
            Debug.Log("C12_3D_STILLS_GENERATED | IVAN.glb, fixed camera and corridor, two skeletal walking poses and dated timestamps.");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning("C12_3D_STILLS_UNAVAILABLE | " + e);
            return false;
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
            foreach (var material in materials) UnityEngine.Object.DestroyImmediate(material);
            UnityEngine.Object.DestroyImmediate(entry);
            UnityEngine.Object.DestroyImmediate(exit);
        }
    }

    private static void AddCctvLight(Transform parent, Vector3 position, Color color, float intensity, float range)
    {
        var light = new GameObject("Corridor light", typeof(Light)).GetComponent<Light>();
        light.transform.SetParent(parent, false);
        light.transform.localPosition = position;
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.Soft;
    }

    private static void PoseIvan(Transform actor, Transform[] bones, bool entering)
    {
        actor.SetPositionAndRotation(entering ? new Vector3(-0.18f, 0f, 2.78f) : new Vector3(0.24f, 0f, 2.59f),
            Quaternion.Euler(0f, entering ? 28f : 213f, 0f));
        Transform Bone(string name) => bones.FirstOrDefault(bone => Normalize(bone.name.Split('_')[0]) == name)
            ?? throw new InvalidOperationException("Missing C-12 rig bone: " + name);
        void Aim(string from, string joint, Vector3 direction)
        {
            var bone = Bone(from);
            bone.rotation = Quaternion.FromToRotation(Bone(joint).position - bone.position, actor.TransformDirection(direction)) * bone.rotation;
        }

        float step = entering ? 1f : -1f;
        var spine = Bone("spine");
        spine.rotation = Quaternion.AngleAxis(6f * step, actor.up) * Quaternion.AngleAxis(4f, actor.right) * spine.rotation;
        foreach (var side in new[] { "left", "right" })
        {
            float outward = side == "left" ? -1f : 1f;
            float stride = -outward * step;
            Aim(side + "arm", side + "forearm", new Vector3(0.20f * outward, -1f, -0.23f * stride));
            Aim(side + "forearm", side + "hand", new Vector3(0.04f * outward, -1f, 0.28f - 0.24f * stride));
            Aim(side + "upleg", side + "leg", new Vector3(0.06f * outward, -1f, (stride > 0f ? 0.44f : -0.24f)));
            Aim(side + "leg", side + "foot", new Vector3(0f, -1f, (stride > 0f ? 0.08f : -0.49f)));
            Aim(side + "foot", side + "toebase", new Vector3(0.065f * outward, stride > 0f ? -0.12f : -0.52f, 1f));
        }
    }

    private static Bounds PosedBounds(SkinnedMeshRenderer[] renderers)
    {
        var mesh = new Mesh();
        var bounds = new Bounds();
        bool first = true;
        try
        {
            foreach (var renderer in renderers)
            {
                renderer.BakeMesh(mesh);
                foreach (var vertex in mesh.vertices)
                {
                    var world = renderer.transform.TransformPoint(vertex);
                    if (first) { bounds = new Bounds(world, Vector3.zero); first = false; }
                    else bounds.Encapsulate(world);
                }
            }
            return bounds;
        }
        finally { UnityEngine.Object.DestroyImmediate(mesh); }
    }

    private static Material StageMaterial(string name, Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var material = new Material(shader) { name = name };
        SetMaterialColor(material, color); return material;
    }

    private static GameObject StageCube(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent, false);
        cube.transform.localPosition = position;
        cube.transform.localScale = scale;
        cube.GetComponent<Renderer>().sharedMaterial = material;
        UnityEngine.Object.DestroyImmediate(cube.GetComponent<Collider>());
        return cube;
    }

    private static Texture2D CaptureCctv(Camera camera, bool entering)
    {
        const int width = 352, height = 198;
        var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var previous = RenderTexture.active;
        try
        {
            if (GraphicsSettings.currentRenderPipeline != null)
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
            else
            {
                camera.targetTexture = target;
                camera.Render();
                camera.targetTexture = null;
            }
            RenderTexture.active = target;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            var source = image.GetPixels();
            if (source.Max(pixel => pixel.grayscale) - source.Min(pixel => pixel.grayscale) < 0.12f)
            {
                UnityEngine.Object.DestroyImmediate(image);
                throw new InvalidOperationException("The C-12 render does not contain a visible scene.");
            }
            var pixels = new Color[width * height];
            var noise = new System.Random(entering ? 2353 : 17);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int i = y * width + x;
                    var sampled = source[i] * 0.76f + 0.08f * (source[y * width + Mathf.Max(0, x - 1)] +
                        source[y * width + Mathf.Min(width - 1, x + 1)] + source[Mathf.Max(0, y - 1) * width + x]);
                    var color = Color.Lerp(new Color(sampled.grayscale, sampled.grayscale, sampled.grayscale), sampled, 0.65f);
                    float nx = 2f * x / width - 1f;
                    float ny = 2f * y / height - 1f;
                    float grain = ((float)noise.NextDouble() - 0.5f) * 0.06f;
                    color = (color * 0.83f + new Color(0.065f, 0.071f, 0.061f)) * (1f - 0.12f * (nx * nx + ny * ny));
                    if (y % 3 == 0) color *= 0.955f;
                    pixels[i] = color + new Color(grain, grain, grain, 1f);
                }
            }
            StampCctv(pixels, width, entering ? "C-12 14.10.1999 23:53" : "C-12 15.10.1999 00:17");
            image.SetPixels(pixels);
            image.Apply(false, false);
            return image;
        }
        finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); }
    }

    private static void StampCctv(Color[] pixels, int width, string stamp)
    {
        const string alphabet = "0123456789C-.: ";
        int[][] glyphs =
        {
            new[] { 14, 17, 19, 21, 25, 17, 14 }, new[] { 4, 12, 4, 4, 4, 4, 14 },
            new[] { 14, 17, 1, 2, 4, 8, 31 }, new[] { 30, 1, 1, 14, 1, 1, 30 },
            new[] { 2, 6, 10, 18, 31, 2, 2 }, new[] { 31, 16, 16, 30, 1, 1, 30 },
            new[] { 14, 16, 16, 30, 17, 17, 14 }, new[] { 31, 1, 2, 4, 8, 8, 8 },
            new[] { 14, 17, 17, 14, 17, 17, 14 }, new[] { 14, 17, 17, 15, 1, 1, 14 },
            new[] { 14, 17, 16, 16, 16, 17, 14 }, new[] { 0, 0, 0, 31, 0, 0, 0 },
            new[] { 0, 0, 0, 0, 0, 12, 12 }, new[] { 0, 12, 12, 0, 12, 12, 0 },
            new[] { 0, 0, 0, 0, 0, 0, 0 }
        };
        const int x0 = 9, scale = 2;
        int y0 = pixels.Length / width - 22;
        for (int y = y0 - 3; y < y0 + 7 * scale + 3; y++)
            for (int x = x0 - 3; x < x0 + stamp.Length * 6 * scale + 1; x++) pixels[y * width + x] *= 0.28f;
        for (int i = 0; i < stamp.Length; i++)
        {
            var glyph = glyphs[alphabet.IndexOf(stamp[i])];
            for (int y = 0; y < 7 * scale; y++)
                for (int x = 0; x < 5 * scale; x++)
                    if ((glyph[6 - y / scale] & (16 >> (x / scale))) != 0)
                        pixels[(y0 + y) * width + x0 + i * 6 * scale + x] = new Color(0.91f, 0.94f, 0.86f);
        }
    }

    private static void SaveAndImportCctv(Texture2D image, string path)
    {
        EnsureFolder(DataPath);
        File.WriteAllBytes(Path.GetFullPath(path), image.EncodeToPNG());
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
    }

    private static void ConfigureCctvImport(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
        importer.maxTextureSize = 512; importer.npotScale = TextureImporterNPOTScale.None; importer.textureCompression = TextureImporterCompression.Uncompressed; importer.filterMode = FilterMode.Bilinear;
        importer.SaveAndReimport();
    }

    public static void BuildInterrogationMemory()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before building the Third Mind scene.");
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EnsureFolder(DataPath); EnsureFolder(PrefabPath); EnsureFolder(MaterialPath);
        var dialogue = CreateDialogueData();
        var room = GameObject.Find("interogation_room (1)");
        if (room == null) throw new InvalidOperationException("Could not find the existing interrogation room instance.");
        RemoveOnlyExtraRoomInstance();

        var mainCamera = GameObject.FindWithTag("MainCamera")?.GetComponent<Camera>();
        if (mainCamera == null) throw new InvalidOperationException("The target scene has no existing Main Camera.");
        EnsureOneAudioListener(mainCamera);
        var tableRenderer = FindRenderer(room.transform, "Table_Table_0");
        var chairRenderer = FindRenderer(room.transform, "Chair_Chair_0");
        var doorRenderer = FindRenderer(room.transform, "Door_Door_0");
        var roomRenderer = FindRenderer(room.transform, "Walls_Walls_0");
        if (tableRenderer == null || chairRenderer == null || doorRenderer == null || roomRenderer == null)
            throw new InvalidOperationException("Required room meshes (table, chair, door, walls) could not be located.");
        var floorY = roomRenderer.bounds.min.y;
        var tableBounds = tableRenderer.bounds;

        var generated = GameObject.Find("Third Mind Sequence Setup");
        if (generated != null) Undo.DestroyObjectImmediate(generated);
        generated = new GameObject("Third Mind Sequence Setup");

        var michaelPrefab = CreateCharacterPrefab(MichaelModelPath, MichaelPrefabPath, "Michael", 168f, new[] { MichaelTexturePath });
        var policePrefab = CreateCharacterPrefab(PoliceModelPath, PolicePrefabPath, "Police-Man", 178f, new[] { PoliceTexture0Path, PoliceTexture1Path });
        var michael = (GameObject)PrefabUtility.InstantiatePrefab(michaelPrefab, scene);
        michael.name = "Michael Reid";
        michael.transform.SetParent(generated.transform, true);
        michael.transform.position = new Vector3(chairRenderer.bounds.center.x, floorY, chairRenderer.bounds.center.z);
        michael.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        var michaelBones = michael.GetComponentsInChildren<Transform>(true);
        var head = FindTransform(michaelBones, "head", "neck");
        var spine = FindTransform(michaelBones, "spine", "chest");

        var police = (GameObject)PrefabUtility.InstantiatePrefab(policePrefab, scene);
        police.name = "Police-Man Guard";
        police.transform.SetParent(generated.transform, true);
        police.transform.position = new Vector3(doorRenderer.bounds.center.x - 8f, floorY, doorRenderer.bounds.center.z - 55f);
        police.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

        var playerRoot = GameObject.Find("Third Mind Seated Player");
        if (playerRoot == null)
        {
            playerRoot = new GameObject("Third Mind Seated Player");
            var playerTransform = playerRoot.transform;
            playerTransform.position = new Vector3(mainCamera.transform.position.x, floorY, mainCamera.transform.position.z);
            playerTransform.rotation = Quaternion.Euler(0f, mainCamera.transform.eulerAngles.y, 0f);
            mainCamera.transform.SetParent(playerTransform, true);
            var cc = playerRoot.AddComponent<CharacterController>();
            cc.height = 178f; cc.radius = 26f; cc.center = Vector3.up * 89f; cc.enabled = false;
            var movement = playerRoot.AddComponent<FirstPersonPlayer>();
            movement.enabled = false;
        }
        else if (playerRoot.GetComponent<FirstPersonPlayer>() != null) playerRoot.GetComponent<FirstPersonPlayer>().enabled = false;
        mainCamera.transform.localPosition = new Vector3(mainCamera.transform.localPosition.x, mainCamera.transform.localPosition.y, mainCamera.transform.localPosition.z);

        ConfigureLighting(tableBounds, floorY);
        ConfigureWindow(room.transform);
        CreateDocumentsAndSequence(generated, mainCamera, tableBounds, michael, head, spine, playerRoot, dialogue);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"THIRD_MIND_BUILD_OK | Scene={ScenePath} | Roots={string.Join(", ", scene.GetRootGameObjects().Select(x => x.name))} | AudioListeners={UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(x => x.enabled && x.gameObject.activeInHierarchy)} | TableCenter={tableBounds.center} | FloorY={floorY} | MichaelHeight={CombinedBounds(michael).size.y:0.0} | MichaelMaterials={MaterialSummary(michael)} | PoliceMaterials={MaterialSummary(police)}");
    }

    private static ThirdMindDialogueData CreateDialogueData()
    {
        var data = AssetDatabase.LoadAssetAtPath<ThirdMindDialogueData>(DialogueAssetPath);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<ThirdMindDialogueData>();
            AssetDatabase.CreateAsset(data, DialogueAssetPath);
        }
        data.opening = new List<ThirdMindDialogueLine>
        {
            Line("Майкл", "Я же сказал, в половине одиннадцатого я ушёл. Больше ничего не знаю"),
            Line("Алексей", "Тогда объясни, почему после твоего ухода отключилась камера C-14"),
            Line("Майкл", "Камеры постоянно барахлили. Может, кто-то перевёл её в сервисный режим"),
            Line("Алексей", "А кто подписал заявку на отключение?"),
            Line("Майкл", "Откуда мне знать? Я к этому отношения не имею"),
            Line("Алексей", "Хорошо. Тогда посмотрим документы")
        };
        data.confession = FinalConfession();
        EditorUtility.SetDirty(data);
        return data;
    }

    private static List<ThirdMindDialogueLine> FinalConfession() => new List<ThirdMindDialogueLine>
    {
        Line("Алексей", "Ты утверждал, что ушёл в 22:30. Но в 23:44 сам отключил камеру C-14. Зачем?"),
        Line("Майкл", "Потому что Иван меня подкупил. Заплатил, чтобы я отключил камеру. Мне тогда очень нужны были деньги… А он уверял, что это обычные технические работы"),
        Line("Алексей", "Ты знал, зачем он пошёл в S-04? Моего отца видел?"),
        Line("Майкл", "Нет! Я туда даже не заходил. Не видел твоего отца и понятия не имел, что Иван делал внутри"),
        Line("Алексей", "Тогда почему ты соврал, что ушёл раньше?"),
        Line("Майкл", "Испугался. Когда меня вызвали в отделение, я решил, что во всём обвинят меня"),
        Line("Алексей", "Значит, Иван заранее позаботился, чтобы камера ничего не записала…")
    };

    private static ThirdMindDialogueLine Line(string name, string text) => new ThirdMindDialogueLine { characterName = name, text = text };

    private static GameObject CreateCharacterPrefab(string modelPath, string prefabPath, string name, float desiredHeight, string[] texturePaths)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (model == null) throw new InvalidOperationException("Character model not found: " + modelPath);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
        if (instance == null) instance = UnityEngine.Object.Instantiate(model);
        var wrapper = new GameObject(name);
        instance.transform.SetParent(wrapper.transform, false);
        instance.name = "Model";
        var renderers = wrapper.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) throw new InvalidOperationException("Character model has no renderers: " + modelPath);
        var bounds = CombinedBounds(wrapper);
        if (bounds.size.y <= 0.001f) throw new InvalidOperationException("Character model has invalid bounds: " + modelPath);
        float scale = desiredHeight / bounds.size.y;
        instance.transform.localScale = Vector3.one * scale;
        bounds = CombinedBounds(wrapper);
        instance.transform.localPosition = new Vector3(0f, -bounds.min.y, 0f);
        FixCharacterMaterials(wrapper, texturePaths);
        if (name == "Michael") PoseMichaelForSitting(instance);
        ConfigurePoliceIdle(instance, modelPath);
        EnsureFolder(PrefabPath);
        var prefab = PrefabUtility.SaveAsPrefabAsset(wrapper, prefabPath);
        UnityEngine.Object.DestroyImmediate(wrapper);
        if (prefab == null) throw new InvalidOperationException("Could not save Third Mind character prefab at " + prefabPath);
        return prefab;
    }

    private static void FixCharacterMaterials(GameObject actor, string[] texturePaths)
    {
        var fallbacks = texturePaths.Select(p => AssetDatabase.LoadAssetAtPath<Texture2D>(p)).Where(t => t != null).ToArray();
        int fallbackIndex = 0;
        var replacements = new Dictionary<string, Material>();
        foreach (var renderer in actor.GetComponentsInChildren<Renderer>(true))
        {
            var mats = renderer.sharedMaterials;
            if (mats.Length == 0) mats = new Material[1];
            for (int i = 0; i < mats.Length; i++)
            {
                var source = mats[i];
                Texture texture = source != null && source.HasProperty("_BaseMap") ? source.GetTexture("_BaseMap") : null;
                if (texture == null && source != null && source.HasProperty("_MainTex")) texture = source.GetTexture("_MainTex");
                bool shaderSupported = source != null && source.shader != null && (source.shader.name.StartsWith("Universal Render Pipeline/", StringComparison.Ordinal) || source.shader.name.Contains("glTF-pbrMetallicRoughness"));
                if (shaderSupported && texture != null) continue;
                string sourceName = source != null ? source.name : renderer.name;
                string key = sourceName + "_" + i;
                if (!replacements.TryGetValue(key, out var copy))
                {
                    var urp = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                    copy = new Material(urp);
                    Color tint = source != null && source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : source != null && source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white;
                    copy.SetColor("_BaseColor", tint);
                    copy.SetFloat("_Smoothness", source != null && source.HasProperty("_Glossiness") ? source.GetFloat("_Glossiness") : 0.25f);
                    if (texture == null && fallbacks.Length > 0) { texture = fallbacks[fallbackIndex % fallbacks.Length]; fallbackIndex++; }
                    if (texture != null) copy.SetTexture("_BaseMap", texture);
                    string assetPath = MaterialPath + "/" + SafeName(actor.name) + "_" + SafeName(sourceName) + "_" + i + ".mat";
                    EnsureFolder(MaterialPath);
                    var prior = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
                    if (prior != null) UnityEngine.Object.DestroyImmediate(prior, true);
                    AssetDatabase.CreateAsset(copy, assetPath);
                    replacements[key] = copy;
                }
                mats[i] = copy;
            }
            renderer.sharedMaterials = mats;
        }
    }

    private static void PoseMichaelForSitting(GameObject actor)
    {
        var bones = actor.GetComponentsInChildren<Transform>(true);
        foreach (var bone in bones)
        {
            string n = Normalize(bone.name);
            if ((n.Contains("forearm") || n.Contains("lowerarm") || n.Contains("elbow")) && IsLeft(n, "forearm", "lowerarm", "elbow")) bone.localRotation = bone.localRotation * Quaternion.Euler(0f, 0f, 58f);
            else if ((n.Contains("forearm") || n.Contains("lowerarm") || n.Contains("elbow")) && IsRight(n, "forearm", "lowerarm", "elbow")) bone.localRotation = bone.localRotation * Quaternion.Euler(0f, 0f, -58f);
            else if ((n.Contains("upperarm") || n.EndsWith("leftarm") || n.EndsWith("rightarm")) && IsLeft(n, "upperarm", "arm")) bone.localRotation = bone.localRotation * Quaternion.Euler(0f, 0f, 65f);
            else if ((n.Contains("upperarm") || n.EndsWith("leftarm") || n.EndsWith("rightarm")) && IsRight(n, "upperarm", "arm")) bone.localRotation = bone.localRotation * Quaternion.Euler(0f, 0f, -65f);
            else if ((n.Contains("lowerleg") || n.Contains("calf") || (n.EndsWith("leftleg") && !n.Contains("upleg"))) && IsLeft(n, "lowerleg", "calf", "leg")) bone.localRotation = bone.localRotation * Quaternion.Euler(-75f, 0f, 0f);
            else if ((n.Contains("lowerleg") || n.Contains("calf") || (n.EndsWith("rightleg") && !n.Contains("upleg"))) && IsRight(n, "lowerleg", "calf", "leg")) bone.localRotation = bone.localRotation * Quaternion.Euler(-75f, 0f, 0f);
            else if ((n.Contains("upleg") || n.Contains("upperleg") || n.Contains("thigh")) && IsLeft(n, "upleg", "upperleg", "thigh")) bone.localRotation = bone.localRotation * Quaternion.Euler(78f, 0f, 0f);
            else if ((n.Contains("upleg") || n.Contains("upperleg") || n.Contains("thigh")) && IsRight(n, "upleg", "upperleg", "thigh")) bone.localRotation = bone.localRotation * Quaternion.Euler(78f, 0f, 0f);
        }
        Debug.Log("Michael rig bones: " + string.Join(", ", bones.Select(b => b.name).Where(n => Normalize(n).Contains("head") || Normalize(n).Contains("spine") || Normalize(n).Contains("arm") || Normalize(n).Contains("leg"))));
    }

    private static void ConfigurePoliceIdle(GameObject actor, string modelPath)
    {
        var avatar = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().FirstOrDefault(a => a.isValid);
        var clip = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal));
        if (avatar == null || clip == null)
        {
            Debug.Log($"Police-Man idle left static; compatible rig/clip available={(avatar != null && clip != null)} (avatar={(avatar ? avatar.name : "none")}, clip={(clip ? clip.name : "none")}).");
            return;
        }
        var controllerPath = DataPath + "/Police-Man Idle.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var state = controller.layers[0].stateMachine.AddState("Idle"); state.motion = clip; state.speed = 1f;
        }
        var animator = actor.GetComponent<Animator>() ?? actor.AddComponent<Animator>();
        animator.avatar = avatar; animator.runtimeAnimatorController = controller; animator.applyRootMotion = false;
    }

    private static void CreateDocumentsAndSequence(GameObject setup, Camera camera, Bounds tableBounds, GameObject michael, Transform head, Transform spine, GameObject playerRoot, ThirdMindDialogueData dialogue)
    {
        var tableCenter = tableBounds.center;
        float paperY = tableBounds.max.y + 3.2f;
        var anchor = new GameObject("Table Top Camera Anchor").transform; anchor.SetParent(setup.transform, false);
        anchor.position = new Vector3(tableCenter.x, tableBounds.max.y + 170f, tableCenter.z);
        anchor.rotation = Quaternion.Euler(90f, 0f, 0f);
        var docs = new ThirdMindDocumentSurface[4];
        int[] startingIds = { 2, 0, 3, 1 };
        string[] titles = { "STATEMENT OF MICHAEL REED", "EQUIPMENT SERVICE REQUEST C-14", "SECURITY ACCESS LOG • S-04", "SURVEILLANCE REPORT • C-12" };
        string[] times = { "22:30", "23:44", "23:51", "23:53" };
        string[] texts =
        {
            "NEW YORK POLICE DEPARTMENT\nINTERVIEW STATEMENT • 14 OCT 1999\n\nI left my workstation at 10:30 PM and went directly home. I did not return to the service sector.\n\nMichael Reed\nSignature: M. Reed",
            "NYPD • FACILITIES & ELECTRONICS\nWORK ORDER 99-1048 • 23:44\n\nCamera C-14 placed in SERVICE MODE for maintenance.\nAssigned technician: Michael Reed\nAuthorization: M. Reed\n\nReason: signal inspection / routine service",
            "SECURITY ACCESS LEDGER • SECTOR S-04\nCARD HOLDER: IVAN\n\n23:51  ENTRY — SERVICE CORRIDOR\n00:16  EXIT — SERVICE CORRIDOR\n\nCredential: assigned access card\nLog terminal: S-04 north door",
            "NYPD VIDEO REVIEW • CAMERA C-12\nC-14 was offline; C-12 remained operational.\n\n23:53 — Ivan enters the service sector alone.\n00:17 — The same person exits the service sector alone.\n\nTwo still photographs from the continuous C-12 recording show one person at entry and exit."
        };
        for (int slot = 0; slot < 4; slot++)
        {
            float x = tableCenter.x - 57f + slot * 38f;
            int id = startingIds[slot];
            docs[id] = CreateDocument(setup.transform, id, titles[id], texts[id], times[id], new Vector3(x, paperY, tableCenter.z), id == 3);
        }
        var sequenceObject = new GameObject("Third Mind Sequence Runtime"); sequenceObject.transform.SetParent(setup.transform, false);
        var seq = sequenceObject.AddComponent<ThirdMindSequence>();
        seq.dialogueData = dialogue; seq.memoryCamera = camera; seq.tableViewAnchor = anchor;
        seq.michaelHead = FindBone(head, "head"); seq.michaelSpine = FindBone(spine, "spine"); seq.playerController = playerRoot.GetComponent<FirstPersonPlayer>();
        seq.documents = docs;
        seq.returnSceneIndex = SceneUtility.GetBuildIndexByScenePath(TrainMindSession.TrainPath);
        seq.cameraMoveDuration = 1.35f;
        camera.fieldOfView = 55f;
        var audioListeners = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
        foreach (var listener in audioListeners) listener.enabled = listener.gameObject == camera.gameObject;
    }

    private static ThirdMindDocumentSurface CreateDocument(Transform parent, int id, string title, string content, string time, Vector3 position, bool addPhotos)
    {
        var root = new GameObject("Document " + (id + 1) + " — " + title, typeof(BoxCollider), typeof(ThirdMindDocumentSurface));
        root.transform.SetParent(parent, true); root.transform.position = position;
        var collider = root.GetComponent<BoxCollider>(); collider.size = new Vector3(33f, 2f, 44f);
        var surface = root.GetComponent<ThirdMindDocumentSurface>(); surface.documentId = id; surface.documentTitle = title; surface.fullText = content; surface.firstEventTime = time;
        var canvasGo = new GameObject("Printed Paper", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster)); canvasGo.transform.SetParent(root.transform, false);
        var canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = Camera.main; canvas.sortingOrder = 4;
        var canvasRect = canvasGo.GetComponent<RectTransform>(); canvasRect.sizeDelta = new Vector2(330f, 440f); canvasRect.localScale = Vector3.one * 0.1f; canvasRect.localRotation = Quaternion.Euler(90f, 0f, 0f); canvasRect.localPosition = new Vector3(0f, 1.15f, 0f);
        surface.paperVisual = canvasRect;
        var paper = new GameObject("Aged paper", typeof(RectTransform), typeof(Image)); paper.transform.SetParent(canvasGo.transform, false);
        var paperRect = paper.GetComponent<RectTransform>(); paperRect.anchorMin = Vector2.zero; paperRect.anchorMax = Vector2.one; paperRect.offsetMin = Vector2.zero; paperRect.offsetMax = Vector2.zero;
        surface.paperImage = paper.GetComponent<Image>(); surface.paperImage.color = new Color(0.93f, 0.87f, 0.72f, 1f); surface.paperImage.raycastTarget = false;
        AddPaperText(canvasGo.transform, "NYC • 1999", 22f, new Color(0.25f, 0.22f, 0.18f), new Vector2(0.05f, 0.88f), new Vector2(0.95f, 0.98f), TextAlignmentOptions.Center);
        AddPaperText(canvasGo.transform, title, 21f, new Color(0.12f, 0.16f, 0.18f), new Vector2(0.06f, 0.70f), new Vector2(0.94f, 0.88f), TextAlignmentOptions.Center);
        surface.bodyPrint = AddPaperText(canvasGo.transform, content, addPhotos ? 13f : 16f, new Color(0.14f, 0.16f, 0.16f), new Vector2(0.08f, addPhotos ? 0.34f : 0.12f), new Vector2(0.92f, 0.68f), TextAlignmentOptions.TopLeft);
        var slot = AddPaperText(canvasGo.transform, "00", 20f, new Color(0.58f, 0.18f, 0.13f), new Vector2(0.77f, 0.02f), new Vector2(0.96f, 0.1f), TextAlignmentOptions.Center);
        surface.slotLabel = slot;
        if (id == 0) AddPaperText(canvasGo.transform, "SIGNED", 13f, new Color(0.55f, 0.15f, 0.14f), new Vector2(0.62f, 0.11f), new Vector2(0.94f, 0.2f), TextAlignmentOptions.Center);
        if (id == 1) AddPaperText(canvasGo.transform, "SERVICE / C-14", 13f, new Color(0.55f, 0.15f, 0.14f), new Vector2(0.08f, 0.02f), new Vector2(0.66f, 0.1f), TextAlignmentOptions.Center);
        if (addPhotos)
        {
            CreatePhoto(canvasGo.transform, "23:53 • ENTRY", new Vector2(0.08f, 0.12f));
            CreatePhoto(canvasGo.transform, "00:17 • EXIT", new Vector2(0.54f, 0.12f));
        }
        surface.SetSelected(false);
        return surface;
    }

    private static TMP_Text AddPaperText(Transform parent, string value, float size, Color color, Vector2 min, Vector2 max, TextAlignmentOptions align)
    {
        var go = new GameObject("Print", typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>(); rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        var text = go.GetComponent<TextMeshProUGUI>(); text.text = value; text.font = TMP_Settings.defaultFontAsset; text.fontSize = size; text.color = color; text.alignment = align; text.textWrappingMode = TextWrappingModes.Normal; text.raycastTarget = false; text.overflowMode = TextOverflowModes.Truncate;
        return text;
    }

    private static void CreatePhoto(Transform parent, string caption, Vector2 anchorMin)
    {
        var go = new GameObject("Surveillance photo — " + caption, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>(); rect.anchorMin = anchorMin; rect.anchorMax = anchorMin + new Vector2(0.38f, 0.22f); rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        var image = go.GetComponent<Image>(); image.color = new Color(0.15f, 0.19f, 0.18f, 1f); image.raycastTarget = false;
        var label = AddPaperText(go.transform, caption, 10f, new Color(0.83f, 0.82f, 0.73f), new Vector2(0f, 0f), new Vector2(1f, 0.25f), TextAlignmentOptions.Center);
        var silhouette = new GameObject("Person silhouette", typeof(RectTransform), typeof(Image)); silhouette.transform.SetParent(go.transform, false);
        var sr = silhouette.GetComponent<RectTransform>(); sr.anchorMin = new Vector2(0.43f, 0.28f); sr.anchorMax = new Vector2(0.61f, 0.83f); sr.offsetMin = Vector2.zero; sr.offsetMax = Vector2.zero;
        silhouette.GetComponent<Image>().color = new Color(0.045f, 0.055f, 0.05f, 1f);
        var head = new GameObject("Head silhouette", typeof(RectTransform), typeof(Image)); head.transform.SetParent(go.transform, false);
        var hr = head.GetComponent<RectTransform>(); hr.anchorMin = new Vector2(0.47f, 0.78f); hr.anchorMax = new Vector2(0.57f, 0.92f); hr.offsetMin = Vector2.zero; hr.offsetMax = Vector2.zero;
        head.GetComponent<Image>().color = new Color(0.045f, 0.055f, 0.05f, 1f);
        label.raycastTarget = false;
    }

    private static void ConfigureLighting(Bounds table, float floorY)
    {
        var directional = GameObject.Find("Directional Light")?.GetComponent<Light>();
        if (directional != null) { directional.intensity = 0.08f; directional.color = new Color(0.67f, 0.73f, 0.82f); directional.shadows = LightShadows.Soft; }
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.055f, 0.066f, 0.085f, 1f);
        RenderSettings.ambientIntensity = 0.2f;
        var spotObject = GameObject.Find("Interrogation_SpotLight");
        if (spotObject == null) spotObject = new GameObject("Interrogation_SpotLight");
        var light = spotObject.GetComponent<Light>() ?? spotObject.AddComponent<Light>();
        light.type = LightType.Spot; light.color = new Color(0.78f, 0.87f, 1f); light.intensity = 3200f; light.range = 285f; light.spotAngle = 72f; light.innerSpotAngle = 54f;
        light.shadows = LightShadows.Soft; light.shadowStrength = 0.82f; light.bounceIntensity = 0.12f; light.useColorTemperature = false; light.renderMode = LightRenderMode.ForcePixel;
        light.lightmapBakeType = LightmapBakeType.Realtime;
        spotObject.transform.position = new Vector3(table.center.x, table.max.y + 105f, table.center.z);
        spotObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }

    private static void ConfigureWindow(Transform room)
    {
        var renderer = FindRenderer(room, "Window_Window_0");
        if (renderer == null) { Debug.LogWarning("Third Mind: room window renderer was not found."); return; }
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var material = AssetDatabase.LoadAssetAtPath<Material>(WindowMaterialPath);
        if (material == null)
        {
            material = new Material(shader); material.name = "Third Mind dark observation glass";
            material.SetColor("_BaseColor", new Color(0.045f, 0.052f, 0.063f, 1f));
            material.SetFloat("_Metallic", 0.08f); material.SetFloat("_Smoothness", 0.62f);
            AssetDatabase.CreateAsset(material, WindowMaterialPath);
        }
        var slots = renderer.sharedMaterials;
        for (int i = 0; i < slots.Length; i++) slots[i] = material;
        renderer.sharedMaterials = slots;
    }

    private static void RemoveOnlyExtraRoomInstance()
    {
        var extra = GameObject.Find("interogation_room (2)");
        if (extra != null) Undo.DestroyObjectImmediate(extra);
    }

    private static void EnsureOneAudioListener(Camera mainCamera)
    {
        var listeners = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
        if (listeners.All(x => x.gameObject != mainCamera.gameObject)) mainCamera.gameObject.AddComponent<AudioListener>();
        foreach (var listener in listeners) listener.enabled = listener.gameObject == mainCamera.gameObject;
    }

    private static Renderer FindRenderer(Transform root, string name)
        => root.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.name == name);
    private static Transform FindTransform(Transform[] bones, params string[] terms)
        => bones.FirstOrDefault(b => terms.Any(t => Normalize(b.name).Contains(t)));
    private static Transform FindBone(Transform from, string term)
        => from != null ? from.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => Normalize(t.name).Contains(term)) : null;
    private static string Normalize(string s) => new string(s.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
    private static bool IsLeft(string name, params string[] joints) => (name.Contains("left") || name.StartsWith("l")) && joints.Any(name.Contains);
    private static bool IsRight(string name, params string[] joints) => (name.Contains("right") || name.StartsWith("r")) && joints.Any(name.Contains);
    private static Bounds CombinedBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }
    private static string MaterialSummary(GameObject go)
        => string.Join(",", go.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null).Select(m => m.name + "[" + (m.shader ? m.shader.name : "missing") + "]").Distinct());
    private static string SafeName(string s) => string.Concat(s.Select(c => char.IsLetterOrDigit(c) || c == '-' ? c : '_'));
    private static void EnsureFolder(string path)
    {
        var parts = path.Split('/'); string current = parts[0];
        for (int i = 1; i < parts.Length; i++) { string next = current + "/" + parts[i]; if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]); current = next; }
    }
}
#endif

