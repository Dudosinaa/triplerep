using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class DialogueController : MonoBehaviour
{
    private const float ExitIconSize = 22f;
    private const float ExitIconGap = 8f;

    [SerializeField] private Camera playerCamera;
    // Legacy demo references. Conversations now use the first-person camera.
    [SerializeField, HideInInspector] private Camera dialogueCamera;
    [SerializeField] private FirstPersonPlayer movement;
    [SerializeField] private DocumentReader documentReader;
    [SerializeField] private ItemInspector itemInspector;
    [SerializeField, HideInInspector] private GameObject playerBody;
    [SerializeField] private GameObject crosshair;
    [SerializeField] private GameObject[] otherPrompts;
    [SerializeField] private GameObject interactionPrompt;
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private Text speakerText;
    [SerializeField] private Text dialogueText;
    [SerializeField] private Button[] choiceButtons;
    [SerializeField] private Button continueButton;
    [SerializeField, Min(0.1f)] private float talkDistance = 2.7f;

    internal float HintDistance => talkDistance;

    private InteractionHints interactionHints;
    private NpcDialogue activeNpc;
    private NpcDialogue nearbyNpc;
    private Collider aimedCollider;
    private PhoneCall.Line[] phoneLines;
    private readonly RaycastHit[] aimHits = new RaycastHit[16];
    private int nodeIndex;
    private int pendingNextNode;
    private bool showingPlayerReply;
    private bool waitingForScriptedLine;
    private float scriptedLineReadyAt;
    private bool movementWasEnabled;
    private bool readerWasEnabled;
    private bool inspectorWasEnabled;
    private bool crosshairWasActive;
    private bool[] otherPromptsWereActive;
    private (Text Label, DialogueExitIcon Icon, Vector2 OriginalOffsetMin)[] choiceViews;

    public bool IsTalking => activeNpc != null || phoneLines != null;

    private void Awake()
    {
        interactionHints = GetComponent<InteractionHints>();
        if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>();
        if (movement == null) movement = GetComponent<FirstPersonPlayer>();
        if (documentReader == null) documentReader = GetComponent<DocumentReader>();
        if (itemInspector == null) itemInspector = GetComponent<ItemInspector>();
        if (dialogueCamera != null) dialogueCamera.enabled = false;
        if (playerBody != null) playerBody.SetActive(false);
        if (interactionPrompt != null) interactionPrompt.SetActive(false);
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
        choiceViews = new (Text, DialogueExitIcon, Vector2)[choiceButtons?.Length ?? 0];
        for (int i = 0; i < choiceViews.Length; i++)
        {
            Button button = choiceButtons[i];
            if (button == null) continue;
            int index = i;
            button.onClick.AddListener(() => SelectChoice(index));
            Text label = button.GetComponentInChildren<Text>(true);
            if (label != null)
                choiceViews[i] = (label, CreateExitIcon(label), label.rectTransform.offsetMin);
        }
        if (continueButton != null) continueButton.onClick.AddListener(ContinueDialogue);
    }

    private DialogueExitIcon CreateExitIcon(Text label)
    {
        var iconObject = new GameObject("Dialogue Exit Icon", typeof(RectTransform), typeof(DialogueExitIcon));
        iconObject.SetActive(false);
        var icon = iconObject.GetComponent<DialogueExitIcon>();
        icon.color = speakerText != null ? speakerText.color : label.color;
        icon.raycastTarget = false;
        RectTransform rect = icon.rectTransform;
        rect.SetParent(label.rectTransform, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
        rect.sizeDelta = Vector2.one * ExitIconSize;
        rect.anchoredPosition = new Vector2(-ExitIconGap - ExitIconSize * 0.5f, 0f);
        return icon;
    }

    private void Update()
    {
        if (Keyboard.current == null) return;
        if (!IsTalking)
        {
            nearbyNpc = FindNearbyNpc();
            var phone = aimedCollider != null ? aimedCollider.GetComponentInParent<PhoneCall>() : null;
            bool targetReady = phone != null ? phone.CanAnswer : nearbyNpc != null;
            bool canTalk = targetReady && (movement == null || movement.enabled) &&
                (documentReader == null || !documentReader.IsReading) &&
                (itemInspector == null || !itemInspector.IsInspecting);
            if (canTalk) interactionHints?.OfferFocus(phone);
            if (interactionPrompt != null)
            {
                interactionPrompt.SetActive(canTalk);
                var label = interactionPrompt.GetComponent<Text>();
                if (canTalk && label != null)
                    label.text = phone != null ? "[E] Ответить на звонок" : "[E] Поговорить — " + nearbyNpc.CharacterName;
            }
            if (canTalk && Keyboard.current.eKey.wasPressedThisFrame && InteractionInput.TryConsume())
            {
                if (phone != null) AnswerPhone(phone);
                else BeginDialogue(nearbyNpc);
            }
            return;
        }

        if (activeNpc != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            EndDialogue();
            return;
        }

        if (waitingForScriptedLine)
        {
            if (Time.time >= scriptedLineReadyAt) ShowScriptedLine();
            return;
        }

        if (showingPlayerReply || phoneLines != null || (activeNpc != null && activeNpc.HasScriptedLines))
        {
            if (Keyboard.current.eKey.wasPressedThisFrame ? InteractionInput.TryConsume() : Keyboard.current.spaceKey.wasPressedThisFrame)
                ContinueDialogue();
        }
        else
        {
            if (Keyboard.current.digit1Key.wasPressedThisFrame) SelectChoice(0);
            else if (Keyboard.current.digit2Key.wasPressedThisFrame) SelectChoice(1);
            else if (Keyboard.current.digit3Key.wasPressedThisFrame) SelectChoice(2);
        }
    }

    private NpcDialogue FindNearbyNpc()
    {
        // Use the same unobstructed aim target for NPC dialogue and calls.
        aimedCollider = NpcDialogue.FindAimedCollider(playerCamera, transform, talkDistance, aimHits, out var target);
        return target != null && target.isActiveAndEnabled &&
            (target.HasScriptedLines || target.GetStartNodeIndex() >= 0) ? target : null;
    }

    private void AnswerPhone(PhoneCall phone)
    {
        if (!phone.TryAnswer(out phoneLines)) return;
        nodeIndex = 0;
        OpenDialogue(phone.transform);
        ShowChoices(null);
        ShowPhoneLine();
    }

    private void BeginDialogue(NpcDialogue npc)
    {
        int startIndex = npc.GetStartNodeIndex();
        if ((!npc.HasScriptedLines && startIndex < 0) || playerCamera == null) return;
        npc.NotifyConversationStarted();
        activeNpc = npc;
        nodeIndex = npc.HasScriptedLines ? 0 : startIndex;
        OpenDialogue(npc.transform);
        if (npc.HasScriptedLines) PrepareScriptedLine();
        else if (string.IsNullOrEmpty(npc.OpeningLine)) ShowNpcNode();
        else ShowPlayerReply(npc.OpeningLine, startIndex);
    }

    private void OpenDialogue(Transform target)
    {
        movementWasEnabled = movement != null && movement.enabled;
        readerWasEnabled = documentReader != null && documentReader.enabled;
        inspectorWasEnabled = itemInspector != null && itemInspector.enabled;
        if (movement != null) movement.enabled = false;
        if (documentReader != null) documentReader.enabled = false;
        if (itemInspector != null) itemInspector.enabled = false;
        if (interactionPrompt != null) interactionPrompt.SetActive(false);
        if (otherPrompts != null)
        {
            otherPromptsWereActive = new bool[otherPrompts.Length];
            for (int i = 0; i < otherPrompts.Length; i++)
            {
                if (otherPrompts[i] == null) continue;
                otherPromptsWereActive[i] = otherPrompts[i].activeSelf;
                otherPrompts[i].SetActive(false);
            }
        }
        Vector3 towardTarget = target.position - playerCamera.transform.position;
        towardTarget.y = 0f;
        if (towardTarget.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(towardTarget);
        crosshairWasActive = crosshair != null && crosshair.activeSelf;
        if (crosshair != null) crosshair.SetActive(false);
        if (dialoguePanel != null) dialoguePanel.SetActive(true);
        playerCamera.enabled = true;
        if (dialogueCamera != null) dialogueCamera.enabled = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void ShowPhoneLine()
    {
        var line = phoneLines[nodeIndex];
        ShowLine(line.speaker, line.text, true);
    }

    private void PrepareScriptedLine()
    {
        var line = activeNpc.ScriptedLines[nodeIndex];
        if (!string.IsNullOrEmpty(line.cue)) activeNpc.NotifyScriptedCue(movement, line.cue);
        ShowChoices(null);
        if (line.pauseBefore <= 0f)
        {
            ShowScriptedLine();
            return;
        }
        waitingForScriptedLine = true;
        scriptedLineReadyAt = Time.time + line.pauseBefore;
        ShowLine(activeNpc.CharacterName, string.Empty, false);
    }

    private void ShowScriptedLine()
    {
        waitingForScriptedLine = false;
        var line = activeNpc.ScriptedLines[nodeIndex];
        ShowLine(line.playerSpeaks ? activeNpc.PlayerName : activeNpc.CharacterName, line.text, true);
    }

    private void ShowNpcNode()
    {
        if (nodeIndex < 0 || nodeIndex >= activeNpc.Nodes.Length)
        {
            EndDialogue();
            return;
        }
        showingPlayerReply = false;
        NpcDialogue.Node node = activeNpc.Nodes[nodeIndex];
        ShowLine(activeNpc.CharacterName, node.npcText, false);
        ShowChoices(node.choices);
    }

    private void ShowLine(string speaker, string line, bool canContinue)
    {
        if (speakerText != null) speakerText.text = speaker;
        if (dialogueText != null) dialogueText.text = line;
        if (continueButton != null) continueButton.gameObject.SetActive(canContinue);
    }

    private void ShowChoices(NpcDialogue.Choice[] choices)
    {
        for (int i = 0; i < choiceViews.Length; i++)
        {
            Button button = choiceButtons[i];
            if (button == null) continue;
            NpcDialogue.Choice choice = choices != null && i < choices.Length ? choices[i] : null;
            var (label, icon, originalOffsetMin) = choiceViews[i];
            if (label != null)
            {
                bool showExit = choice != null && choice.EndsDialogue;
                icon.gameObject.SetActive(showExit);
                label.rectTransform.offsetMin = showExit
                    ? originalOffsetMin + Vector2.right * (ExitIconSize + ExitIconGap)
                    : originalOffsetMin;
                if (choice != null) label.text = (i + 1) + ". " + choice.text;
            }
            button.gameObject.SetActive(choice != null);
        }
    }

    private void SelectChoice(int index)
    {
        if (activeNpc == null || activeNpc.HasScriptedLines || showingPlayerReply) return;
        NpcDialogue.Choice[] choices = activeNpc.Nodes[nodeIndex].choices;
        if (choices == null || index < 0 || index >= choices.Length) return;
        NpcDialogue.Choice choice = choices[index];
        if (choice == null) return;
        if (choice.EndsDialogue)
        {
            EndDialogue(completed: true);
            return;
        }
        ShowPlayerReply(choice.text, activeNpc.GetNextNodeIndex(choice));
    }

    private void ShowPlayerReply(string line, int nextNode)
    {
        pendingNextNode = nextNode;
        showingPlayerReply = true;
        ShowLine(activeNpc.PlayerName, line, true);
        ShowChoices(null);
    }

    private void ContinueDialogue()
    {
        if (phoneLines != null)
        {
            nodeIndex++;
            if (nodeIndex < phoneLines.Length) ShowPhoneLine();
            else EndDialogue();
            return;
        }

        if (activeNpc == null || waitingForScriptedLine) return;
        if (activeNpc.HasScriptedLines)
        {
            nodeIndex++;
            if (nodeIndex < activeNpc.ScriptedLines.Length) PrepareScriptedLine();
            else EndDialogue(completed: true);
            return;
        }
        if (!showingPlayerReply) return;
        if (pendingNextNode < 0 || pendingNextNode >= activeNpc.Nodes.Length)
        {
            EndDialogue(completed: true);
            return;
        }
        nodeIndex = pendingNextNode;
        ShowNpcNode();
    }

    private void EndDialogue(bool completed = false)
    {
        var npc = activeNpc;
        activeNpc = null;
        phoneLines = null;
        showingPlayerReply = false;
        waitingForScriptedLine = false;
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
        if (dialogueCamera != null) dialogueCamera.enabled = false;
        if (playerCamera != null) playerCamera.enabled = true;
        if (playerBody != null) playerBody.SetActive(false);
        if (crosshair != null) crosshair.SetActive(crosshairWasActive);
        if (otherPrompts != null && otherPromptsWereActive != null)
        {
            for (int i = 0; i < otherPrompts.Length; i++)
                if (otherPrompts[i] != null) otherPrompts[i].SetActive(otherPromptsWereActive[i]);
        }
        if (movement != null) movement.enabled = movementWasEnabled;
        if (documentReader != null) documentReader.enabled = readerWasEnabled;
        if (itemInspector != null) itemInspector.enabled = inspectorWasEnabled;
        if (movementWasEnabled)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        if (npc != null)
        {
            if (completed) npc.NotifyConversationCompleted(movement);
            else npc.NotifyConversationCancelled();
        }
    }

    private void OnDisable()
    {
        if (IsTalking) EndDialogue();
        if (interactionPrompt != null) interactionPrompt.SetActive(false);
    }
}
