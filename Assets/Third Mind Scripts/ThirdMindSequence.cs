using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ThirdMindSequence : MonoBehaviour
{
    private enum Phase { Opening, CameraToTable, Studying, CameraToMichael, Confession, Fading, Done }
    [Header("Story")]
    public ThirdMindDialogueData dialogueData;
    [Header("Scene references")]
    public Camera memoryCamera;
    public Transform tableViewAnchor;
    public Transform michaelHead;
    public Transform michaelSpine;
    public FirstPersonPlayer playerController;
    public ThirdMindDocumentSurface[] documents;
    public Sprite cctvEntryPhoto;
    public Sprite cctvExitPhoto;
    public int returnSceneIndex = 3;
    [Header("Document sounds")]
    public AudioClip documentPickupSound;
    public AudioClip documentSelectSound;
    [Header("Camera")]
    [Min(0.1f)] public float cameraMoveDuration = 1.3f;
    [SerializeField, Range(0f, 180f)] private float openingLookLeft = 70f;
    [SerializeField, Range(0f, 180f)] private float openingLookRight = 70f;
    [SerializeField, Range(0f, 90f)] private float openingLookUp = 35f;
    [SerializeField, Range(0f, 90f)] private float openingLookDown = 45f;

    private const int DocumentCount = 4;
    private const float PaperMoveDuration = 0.32f;
    private const float ReadingPanelHeight = 54f;
    private const string IncompleteOrderFeedback = "Сначала изучите все документы";
    private const string StudyHint = "ЛКМ — поднять документ | ПКМ — выбрать документ";
    private const string HoverHint = "ЛКМ — поднять документ";
    private const string MoveHint = "← / → — переместить документ";
    private const string ReadHint = "ЛКМ — положить документ";

    private Canvas canvas;
    private TMP_Text speakerText;
    private TMP_Text dialogueText;
    private TMP_Text dialogueHint;
    private GameObject dialoguePanel;
    private RectTransform studyPanel;
    private TMP_Text studyHint;
    private TMP_Text studyProgress;
    private TMP_Text orderFeedback;
    private Button moveLeftButton;
    private Button moveRightButton;
    private Button confirmOrderButton;
    private GameObject fadePanel;
    private Image fadeImage;
    private ThirdMindDocumentSurface[] orderedDocuments;
    private ThirdMindDocumentSurface selectedDocument;
    private ThirdMindDocumentSurface hoveredDocument;
    private ThirdMindDocumentSurface readingDocument;
    private Pose readingSlot;
    private bool documentMoving;
    private ThirdMindDialogueLine[] activeLines;
    private int lineIndex;
    private Phase phase;
    private Vector3 openingCameraPosition;
    private Quaternion openingCameraRotation;
    private Quaternion openingCameraLocalRotation;
    private Quaternion headBaseRotation;
    private float openingYaw;
    private float openingPitch;
    private AudioSource voiceSource;

    private void Awake()
    {
        if (memoryCamera == null) memoryCamera = Camera.main;
        if (memoryCamera == null)
        {
            Debug.LogError("Third Mind: target scene has no Main Camera.", this);
            enabled = false;
            return;
        }
        openingCameraPosition = memoryCamera.transform.position;
        openingCameraRotation = memoryCamera.transform.rotation;
        openingCameraLocalRotation = memoryCamera.transform.localRotation;
        if (playerController != null) playerController.enabled = false;
        memoryCamera.GetComponent<AudioListener>().enabled = true;
        voiceSource = GetComponent<AudioSource>();
        if (voiceSource == null) voiceSource = gameObject.AddComponent<AudioSource>();
        BuildInterface();
        if (michaelHead != null) headBaseRotation = michaelHead.localRotation;
        phase = Phase.Opening;
        StartDialogue(dialogueData != null ? dialogueData.opening.ToArray() : new ThirdMindDialogueLine[0]);
    }

    private void OnEnable()
    {
        Cursor.lockState = CursorLockMode.Confined;
        Cursor.visible = true;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && isActiveAndEnabled && phase != Phase.Done)
            Cursor.lockState = CursorLockMode.Confined;
    }

    private void OnDisable()
    {
        if (Cursor.lockState == CursorLockMode.Confined)
            Cursor.lockState = CursorLockMode.None;
    }

    private void Update()
    {
        if (GameplayPause.BlocksGameplayInput) return;
        if (michaelHead != null && phase != Phase.Done)
        {
            float t = Time.time;
            float yaw = (Mathf.PerlinNoise(t * 0.12f, 3.4f) - 0.5f) * 2.8f;
            float pitch = (Mathf.PerlinNoise(t * 0.17f, 7.2f) - 0.5f) * 1.7f;
            michaelHead.localRotation = headBaseRotation * Quaternion.Euler(pitch, yaw, (Mathf.PerlinNoise(t * 0.19f, 1.6f) - 0.5f) * 1.1f);
        }

        var mouse = Mouse.current;
        bool leftPressed = mouse != null && mouse.leftButton.wasPressedThisFrame;
        if (phase == Phase.Opening) UpdateOpeningLook();
        if (phase == Phase.Opening || phase == Phase.Confession)
        {
            if (Keyboard.current?.spaceKey.wasPressedThisFrame == true) AdvanceDialogue();
            return;
        }
        if (phase != Phase.Studying || documentMoving) return;

        var keyboard = Keyboard.current;
        if (readingDocument != null)
        {
            if (InteractionInput.TryConsumeCancel() || leftPressed) CloseDocument();
            else
            {
                var pose = GetActiveReadingPose();
                if (readingDocument.transform.position != pose.position)
                    readingDocument.transform.SetPositionAndRotation(pose.position, pose.rotation);
            }
            return;
        }

        var underPointer = DocumentUnderPointer(mouse);
        SetHoveredDocument(underPointer);
        if (underPointer != null && leftPressed)
        {
            if (documentPickupSound != null) voiceSource.PlayOneShot(documentPickupSound);
            OpenDocument(underPointer);
            return;
        }
        if (underPointer != null && mouse.rightButton.wasPressedThisFrame)
        {
            if (documentSelectSound != null) voiceSource.PlayOneShot(documentSelectSound);
            SelectDocument(underPointer);
        }

        if (keyboard == null) return;
        if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame) MoveSelected(-1);
        else if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame) MoveSelected(1);
    }

    private ThirdMindDocumentSurface DocumentUnderPointer(Mouse mouse)
    {
        if (mouse == null) return null;
        Vector2 position = mouse.position.ReadValue();
        if (RectTransformUtility.RectangleContainsScreenPoint(studyPanel, position)) return null;
        var ray = memoryCamera.ScreenPointToRay(position);
        if (!Physics.Raycast(ray, out var hit, memoryCamera.farClipPlane, ~0, QueryTriggerInteraction.Ignore)) return null;
        return hit.collider.GetComponentInParent<ThirdMindDocumentSurface>();
    }

    private void SetHoveredDocument(ThirdMindDocumentSurface document)
    {
        if (document == hoveredDocument) return;
        if (hoveredDocument != null) hoveredDocument.SetHovered(false);
        hoveredDocument = document;
        if (hoveredDocument != null) hoveredDocument.SetHovered(true);
        RefreshStudyControls();
    }

    private void SelectDocument(ThirdMindDocumentSurface document)
    {
        if (document == selectedDocument) return;
        if (selectedDocument != null) selectedDocument.SetSelected(false);
        selectedDocument = document;
        if (selectedDocument != null) selectedDocument.SetSelected(true);
        RefreshStudyControls();
    }

    private void UpdateOpeningLook()
    {
        if (Mouse.current == null || Cursor.lockState == CursorLockMode.Locked) return;
        Vector2 delta = Mouse.current.delta.ReadValue();
        openingYaw = Mathf.Clamp(openingYaw + delta.x * 0.035f, -openingLookLeft, openingLookRight);
        openingPitch = Mathf.Clamp(openingPitch - delta.y * 0.035f, -openingLookUp, openingLookDown);
        memoryCamera.transform.localRotation = openingCameraLocalRotation * Quaternion.Euler(openingPitch, openingYaw, 0f);
    }

    private void StartDialogue(ThirdMindDialogueLine[] lines)
    {
        activeLines = lines;
        lineIndex = 0;
        if (activeLines == null || activeLines.Length == 0)
        {
            if (phase == Phase.Opening) BeginTableView();
            else FinishConfession();
            return;
        }
        dialoguePanel.SetActive(true);
        RenderDialogueLine();
    }

    private void RenderDialogueLine()
    {
        if (lineIndex < 0 || lineIndex >= activeLines.Length) return;
        var line = activeLines[lineIndex];
        speakerText.text = line.characterName;
        dialogueText.text = line.text;
        dialogueHint.text = "Пробел — продолжить";
        if (voiceSource != null && line.voiceClip != null) voiceSource.PlayOneShot(line.voiceClip);
    }

    private void AdvanceDialogue()
    {
        lineIndex++;
        if (lineIndex < activeLines.Length) { RenderDialogueLine(); return; }
        dialoguePanel.SetActive(false);
        if (phase == Phase.Opening) BeginTableView();
        else if (phase == Phase.Confession) StartCoroutine(WaitAndFade());
    }

    private void BeginTableView()
    {
        phase = Phase.CameraToTable;
        if (tableViewAnchor == null) { Debug.LogError("Third Mind: table camera anchor is missing.", this); return; }
        StartCoroutine(MoveCamera(tableViewAnchor.position, tableViewAnchor.rotation, Phase.Studying));
    }

    private IEnumerator MoveCamera(Vector3 targetPosition, Quaternion targetRotation, Phase nextPhase)
    {
        Vector3 startPosition = memoryCamera.transform.position;
        Quaternion startRotation = memoryCamera.transform.rotation;
        float duration = Mathf.Max(0.1f, cameraMoveDuration);
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            memoryCamera.transform.SetPositionAndRotation(Vector3.Lerp(startPosition, targetPosition, t), Quaternion.Slerp(startRotation, targetRotation, t));
            yield return null;
        }
        memoryCamera.transform.SetPositionAndRotation(targetPosition, targetRotation);
        phase = nextPhase;
        if (nextPhase == Phase.Studying)
        {
            orderedDocuments = documents.Where(document => document != null)
                .OrderBy(document => Vector3.Dot(document.transform.position, memoryCamera.transform.right)).ToArray();
            UpdateDocumentLabels();
            studyPanel.gameObject.SetActive(true);
            RefreshStudyControls();
        }
        else if (nextPhase == Phase.Confession)
        {
            StartDialogue(dialogueData != null ? dialogueData.confession.ToArray() : new ThirdMindDialogueLine[0]);
        }
    }

    private void OpenDocument(ThirdMindDocumentSurface document)
    {
        readingDocument = document;
        readingSlot = new Pose(document.transform.position, document.transform.rotation);
        document.inspected = true;
        document.SetReading(true);
        SetHoveredDocument(null);
        StartCoroutine(AnimateReadingPose(GetActiveReadingPose(), false));
    }

    private Pose GetActiveReadingPose()
    {
        var hintTop = studyPanel.TransformPoint(0f, ReadingPanelHeight, 0f);
        float screenY = RectTransformUtility.WorldToScreenPoint(null, hintTop).y;
        float hintTopInViewport = (screenY - memoryCamera.pixelRect.yMin) / Mathf.Max(1f, memoryCamera.pixelHeight);
        return GetReadingPose(readingDocument, memoryCamera, hintTopInViewport);
    }

    internal static Pose GetReadingPose(ThirdMindDocumentSurface document, Camera camera, float hintTopInViewport = 0.07f)
    {
        const float margin = 0.025f;
        var paper = document.paperVisual;
        float bottom = Mathf.Clamp(hintTopInViewport, 0f, 1f - 3f * margin) + margin;
        float top = 1f - margin;
        float halfHeight = paper.rect.height * paper.lossyScale.y * 0.5f;
        float halfWidth = paper.rect.width * paper.lossyScale.x * 0.5f;
        float viewScale = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float depth = Mathf.Max(halfHeight / (top - bottom), halfWidth / ((1f - 2f * margin) * camera.aspect)) / viewScale;
        depth = Mathf.Max(depth, camera.nearClipPlane + 0.1f);
        var paperCenter = camera.ViewportToWorldPoint(new Vector3(0.5f, (bottom + top) * 0.5f, depth));
        var paperRotation = camera.transform.rotation;
        var rotation = paperRotation * Quaternion.Inverse(paper.localRotation);
        var rectCenterOffset = paperRotation * Vector3.Scale(paper.rect.center, paper.lossyScale);
        var position = paperCenter - rectCenterOffset - rotation * Vector3.Scale(paper.localPosition, document.transform.lossyScale);
        return new Pose(position, rotation);
    }

    private void CloseDocument() => StartCoroutine(AnimateReadingPose(readingSlot, true));

    private IEnumerator AnimateReadingPose(Pose target, bool returning)
    {
        documentMoving = true;
        RefreshStudyControls();
        var document = readingDocument;
        var paperRoot = document.transform;
        var start = new Pose(paperRoot.position, paperRoot.rotation);
        for (float elapsed = 0f; elapsed < PaperMoveDuration; elapsed += Time.deltaTime)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / PaperMoveDuration);
            paperRoot.SetPositionAndRotation(Vector3.Lerp(start.position, target.position, t),
                Quaternion.Slerp(start.rotation, target.rotation, t));
            yield return null;
        }
        paperRoot.SetPositionAndRotation(target.position, target.rotation);
        if (returning)
        {
            document.SetReading(false);
            readingDocument = null;
            Physics.SyncTransforms();
        }
        documentMoving = false;
        RefreshStudyControls();
    }

    private bool CanArrange => phase == Phase.Studying && readingDocument == null && !documentMoving;

    private int SelectedIndex => System.Array.IndexOf(orderedDocuments, selectedDocument);

    private void MoveSelected(int delta)
    {
        if (!CanArrange || selectedDocument == null) return;
        int index = SelectedIndex;
        int neighbor = index + delta;
        if (index < 0 || neighbor < 0 || neighbor >= orderedDocuments.Length) return;
        SetHoveredDocument(null);
        StartCoroutine(SwapDocuments(index, neighbor));
    }

    private IEnumerator SwapDocuments(int index, int neighbor)
    {
        documentMoving = true;
        orderFeedback.text = string.Empty;
        RefreshStudyControls();
        var first = orderedDocuments[index];
        var second = orderedDocuments[neighbor];
        var firstSlot = first.transform.position;
        var secondSlot = second.transform.position;
        var laneAxis = Vector3.ProjectOnPlane(memoryCamera.transform.up, Vector3.up).normalized;
        float clearance = (first.GetComponent<BoxCollider>().bounds.size.z + second.GetComponent<BoxCollider>().bounds.size.z) * 0.5f + 2f;
        SwapLanes(laneAxis, clearance, out var rearLane, out var frontLane);
        const float duration = 0.54f;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            first.transform.position = SwapPosition(firstSlot, secondSlot, rearLane, elapsed / duration);
            second.transform.position = SwapPosition(secondSlot, firstSlot, frontLane, elapsed / duration);
            yield return null;
        }
        first.transform.position = secondSlot;
        second.transform.position = firstSlot;
        orderedDocuments[index] = second;
        orderedDocuments[neighbor] = first;
        Physics.SyncTransforms();
        UpdateDocumentLabels();
        documentMoving = false;
        RefreshStudyControls();
    }

    internal static void SwapLanes(Vector3 towardBack, float clearance, out Vector3 rear, out Vector3 front)
    {
        // Leave the near edge clear for the lamp.
        rear = towardBack * (clearance * 0.80f);
        front = -towardBack * (clearance * 0.20f);
    }

    internal static Vector3 SwapPosition(Vector3 start, Vector3 target, Vector3 lane, float t)
    {
        const float lanePortion = 0.28f;
        float across = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(lanePortion, 1f - lanePortion, t));
        float outside = t < 0.5f ? t : 1f - t;
        float separation = Mathf.SmoothStep(0f, 1f, outside / lanePortion);
        return Vector3.Lerp(start, target, across) + (lane + Vector3.up * 2f) * separation;
    }

    internal static bool IsChronological(ThirdMindDocumentSurface[] order)
    {
        return order.Length == DocumentCount && order.Select((document, index) => document.documentId == index).All(match => match);
    }

    private void CheckOrder()
    {
        if (!CanArrange) return;
        if (InspectedCount() < DocumentCount)
        {
            orderFeedback.text = IncompleteOrderFeedback;
            return;
        }
        if (!IsChronological(orderedDocuments))
        {
            orderFeedback.text = "Порядок не сходится. Проверьте даты и время в документах";
            return;
        }
        phase = Phase.CameraToMichael;
        SetHoveredDocument(null);
        SelectDocument(null);
        studyPanel.gameObject.SetActive(false);
        StartCoroutine(MoveCamera(openingCameraPosition, openingCameraRotation, Phase.Confession));
    }

    private IEnumerator WaitAndFade()
    {
        phase = Phase.Fading;
        yield return new WaitForSeconds(2f);
        fadePanel.SetActive(true);
        fadeImage.raycastTarget = true;
        for (float t = 0f; t < 1.2f; t += Time.deltaTime)
        {
            Color c = fadeImage.color; c.a = Mathf.Clamp01(t / 1.2f); fadeImage.color = c;
            yield return null;
        }
        Color final = fadeImage.color; final.a = 1f; fadeImage.color = final;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        phase = Phase.Done;
        SceneEntryCheckpoint.CompleteTrainMemory(SceneEntryCheckpoint.TrainMemory.Michael, gameObject.scene);
        if (TrainMindSession.TryReturn(gameObject.scene)) yield break;
        int destination = SceneEntryCheckpoint.GetReturnIndex(returnSceneIndex);
        if (SceneSwitch.CanLoadSceneIndex(destination)) SceneSwitch.LoadSceneIndex(destination);
        else SceneManager.LoadScene("Train");
    }

    private void FinishConfession() => StartCoroutine(WaitAndFade());

    private void UpdateDocumentLabels()
    {
        for (int i = 0; i < orderedDocuments.Length; i++) orderedDocuments[i].SetSlot(i);
    }

    private int InspectedCount() => orderedDocuments.Count(document => document.inspected);

    private void RefreshStudyControls()
    {
        bool reading = readingDocument != null;
        int count = InspectedCount();
        studyPanel.sizeDelta = reading ? new Vector2(690f, ReadingPanelHeight) : new Vector2(1060f, 110f);
        studyProgress.rectTransform.anchoredPosition = new Vector2(22f, reading ? -8f : -10f);
        studyHint.rectTransform.sizeDelta = new Vector2(reading ? 390f : 620f, 40f);
        studyHint.rectTransform.anchoredPosition = new Vector2(reading ? 120f : 0f, 27f);
        orderFeedback.gameObject.SetActive(!reading);
        if (count == DocumentCount && orderFeedback.text == IncompleteOrderFeedback)
            orderFeedback.text = string.Empty;
        confirmOrderButton.gameObject.SetActive(!reading);
        moveLeftButton.gameObject.SetActive(!reading);
        moveRightButton.gameObject.SetActive(!reading);
        studyProgress.text = $"Изучено документов: {count}/{DocumentCount}";
        studyHint.text = reading ? ReadHint : selectedDocument != null ? MoveHint : hoveredDocument != null ? HoverHint : StudyHint;
        int index = SelectedIndex;
        moveLeftButton.interactable = CanArrange && index > 0;
        moveRightButton.interactable = CanArrange && index >= 0 && index < orderedDocuments.Length - 1;
        confirmOrderButton.interactable = CanArrange;
    }

    private void BuildInterface()
    {
        var canvasObject = new GameObject("Third Mind UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        SceneManager.MoveGameObjectToScene(canvasObject, gameObject.scene);
        canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 50;
        var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
        if (EventSystem.current == null)
        {
            var eventObject = new GameObject("Third Mind EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            SceneManager.MoveGameObjectToScene(eventObject, gameObject.scene);
            eventObject.GetComponent<EventSystem>().sendNavigationEvents = false;
        }
        dialoguePanel = Panel("Dialogue Panel", canvas.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(1440, 228), new Color(0.035f, 0.052f, 0.067f, 0.96f));
        dialoguePanel.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 24f);
        speakerText = Text("Speaker", dialoguePanel.transform, "", 32, new Color(0.86f, 0.72f, 0.45f), TextAlignmentOptions.TopLeft, new Vector2(0f, 0.76f), new Vector2(1f, 0.96f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        speakerText.rectTransform.offsetMin = new Vector2(46f, 0f); speakerText.rectTransform.offsetMax = new Vector2(-46f, 0f);
        dialogueText = Text("Line", dialoguePanel.transform, "", 28, Color.white, TextAlignmentOptions.TopLeft, new Vector2(0f, 0.2f), new Vector2(1f, 0.73f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        dialogueText.rectTransform.offsetMin = new Vector2(46f, 0f); dialogueText.rectTransform.offsetMax = new Vector2(-46f, 0f);
        dialogueHint = Text("Dialogue Hint", dialoguePanel.transform, "ЛКМ — продолжить", 20, new Color(0.72f, 0.76f, 0.78f), TextAlignmentOptions.BottomRight, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(460, 32), new Vector2(-40, 17));

        var bottomCenter = new Vector2(0.5f, 0f);
        studyPanel = Panel("Study Controls", canvas.transform, bottomCenter, bottomCenter, bottomCenter,
            new Vector2(1060, 110), new Color(0.035f, 0.052f, 0.067f, 0.87f)).GetComponent<RectTransform>();
        studyPanel.anchoredPosition = new Vector2(0f, 14f);
        var border = studyPanel.gameObject.AddComponent<Outline>();
        border.effectColor = new Color(0.55f, 0.46f, 0.34f, 0.5f);
        border.effectDistance = new Vector2(1f, -1f);
        studyHint = Text("Study Hint", studyPanel, StudyHint, 21, Color.white, TextAlignmentOptions.Center,
            bottomCenter, bottomCenter, new Vector2(0.5f, 0.5f), new Vector2(620, 40), new Vector2(0, 27));
        studyProgress = Text("Study Progress", studyPanel, "", 18, new Color(0.9f, 0.76f, 0.5f), TextAlignmentOptions.Left,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(244, 38), new Vector2(22, -10));
        orderFeedback = Text("Order Feedback", studyPanel, "", 17, new Color(0.98f, 0.78f, 0.55f), TextAlignmentOptions.Center,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(536, 44), new Vector2(0, -7));
        moveLeftButton = Button("▲", studyPanel, bottomCenter, bottomCenter, new Vector2(0.5f, 0.5f), new Vector2(52, 42), new Vector2(-325, 27), () => MoveSelected(-1)).GetComponent<Button>();
        moveRightButton = Button("▲", studyPanel, bottomCenter, bottomCenter, new Vector2(0.5f, 0.5f), new Vector2(52, 42), new Vector2(325, 27), () => MoveSelected(1)).GetComponent<Button>();
        moveLeftButton.GetComponentInChildren<TMP_Text>().rectTransform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        moveRightButton.GetComponentInChildren<TMP_Text>().rectTransform.localRotation = Quaternion.Euler(0f, 0f, -90f);
        confirmOrderButton = Button("Проверить порядок", studyPanel, Vector2.one, Vector2.one, Vector2.one,
            new Vector2(214, 38), new Vector2(-18, -10), CheckOrder).GetComponent<Button>();
        studyPanel.gameObject.SetActive(false);

        fadePanel = Panel("Fade to Black", canvas.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Color.black);
        fadePanel.GetComponent<RectTransform>().offsetMin = Vector2.zero; fadePanel.GetComponent<RectTransform>().offsetMax = Vector2.zero;
        fadeImage = fadePanel.GetComponent<Image>(); fadeImage.color = new Color(0f, 0f, 0f, 0f); fadeImage.raycastTarget = false; fadePanel.SetActive(false);
    }

    private GameObject Panel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>(); rect.anchorMin = anchorMin; rect.anchorMax = anchorMax; rect.pivot = pivot; rect.sizeDelta = size; rect.anchoredPosition = Vector2.zero;
        var image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = true;
        return go;
    }

    private TMP_Text Text(string name, Transform parent, string value, float size, Color color, TextAlignmentOptions alignment, Vector2 amin, Vector2 amax, Vector2 pivot, Vector2 rectSize, Vector2 position)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>(); rect.anchorMin = amin; rect.anchorMax = amax; rect.pivot = pivot; rect.sizeDelta = rectSize; rect.anchoredPosition = position;
        var text = go.GetComponent<TextMeshProUGUI>(); text.text = value; text.font = TMP_Settings.defaultFontAsset; text.fontSize = size; text.color = color; text.alignment = alignment; text.textWrappingMode = TextWrappingModes.Normal; text.raycastTarget = false;
        return text;
    }

    private GameObject Button(string label, Transform parent, Vector2 amin, Vector2 amax, Vector2 pivot, Vector2 size, Vector2 position, UnityEngine.Events.UnityAction action)
    {
        var go = new GameObject("Button - " + label, typeof(RectTransform), typeof(Image), typeof(Button)); go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>(); rect.anchorMin = amin; rect.anchorMax = amax; rect.pivot = pivot; rect.sizeDelta = size; rect.anchoredPosition = position;
        var image = go.GetComponent<Image>(); image.color = new Color(0.18f, 0.24f, 0.28f, 1f);
        var button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        var colors = button.colors;
        colors.disabledColor = new Color(0.48f, 0.48f, 0.48f, 0.6f);
        button.colors = colors;
        button.onClick.AddListener(action);
        var text = Text("Label", go.transform, label, label.Length > 4 ? 19 : 30, Color.white, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        text.rectTransform.offsetMin = Vector2.zero; text.rectTransform.offsetMax = Vector2.zero;
        return go;
    }
}
