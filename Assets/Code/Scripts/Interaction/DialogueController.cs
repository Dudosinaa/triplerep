using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class DialogueController : MonoBehaviour
{
    private const float CompletionHintDuration = 4f;

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
    [SerializeField, Min(0.1f)] private float talkDistance = 2.7f;


    private InteractionHints interactionHints;
    private NpcDialogue activeNpc;
    private NpcDialogue nearbyNpc;
    private Collider aimedCollider;
    private PhoneCall activePhone;
    private PhoneCall.Line[] phoneLines;
    private AudioSource dialogueVoice;
    private string completionHint;
    private float completionHintUntil;
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
    private readonly List<NpcDialogue.Choice> availableChoices = new();
    private DialoguePanel panel;
    private DialogueClick click;

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
        panel = DialoguePanel.Create(transform);
    }

    private void Update()
    {
        if (GameplayPause.BlocksGameplayInput) return;
        var keyboard = Keyboard.current;
        if (!IsTalking)
        {
            nearbyNpc = FindNearbyNpc();
            var phone = aimedCollider != null ? aimedCollider.GetComponentInParent<PhoneCall>() : null;
            bool targetReady = phone != null ? phone.CanAnswer : nearbyNpc != null;
            bool canTalk = targetReady && (movement == null || movement.enabled) &&
                (documentReader == null || !documentReader.IsReading) &&
                (itemInspector == null || !itemInspector.IsInspecting);
            if (canTalk) interactionHints?.OfferFocus(phone);
            UpdateInteractionPrompt(canTalk, phone);
            if (canTalk && keyboard?.eKey.wasPressedThisFrame == true && InteractionInput.TryConsume())
            {
                if (phone != null) AnswerPhone(phone);
                else BeginDialogue(nearbyNpc);
            }
            return;
        }

        if (InteractionInput.TryConsumeCancel())
        {
            if (Mouse.current?.leftButton.wasPressedThisFrame == true) InteractionInput.ConsumePrimary();
            EndDialogue();
            return;
        }

        if (waitingForScriptedLine)
        {
            if (Time.time >= scriptedLineReadyAt) ShowScriptedLine();
            return;
        }

        bool clicked = click.ReadPress();
        if (click.IsNewLine) return;
        if (availableChoices.Count < 2 && clicked)
        {
            InteractionInput.ConsumePrimary();
            ContinueDialogue();
            return;
        }

        if (keyboard == null) return;
        if (availableChoices.Count > 0)
        {
            if (keyboard.digit1Key.wasPressedThisFrame) SelectChoice(0);
            else if (keyboard.digit2Key.wasPressedThisFrame) SelectChoice(1);
            else if (keyboard.digit3Key.wasPressedThisFrame) SelectChoice(2);
        }
        if (!IsTalking || click.IsNewLine || availableChoices.Count >= 2) return;
        if (keyboard.eKey.wasPressedThisFrame ? InteractionInput.TryConsume() : keyboard.spaceKey.wasPressedThisFrame)
            ContinueDialogue();
    }

    private void UpdateInteractionPrompt(bool canTalk, PhoneCall phone)
    {
        if (interactionPrompt == null) return;

        bool showCompletionHint = !string.IsNullOrEmpty(completionHint) &&
            Time.time < completionHintUntil;
        interactionPrompt.SetActive(showCompletionHint || canTalk);
        var label = interactionPrompt.GetComponent<Text>();
        if (label == null) return;

        if (showCompletionHint) label.text = completionHint;
        else if (canTalk)
            label.text = phone != null ? "[E] Ответить на звонок" : "[E] Поговорить — " + nearbyNpc.CharacterName;
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
        activePhone = phone;
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
        else ShowPlayerReply(npc.OpeningLine, startIndex, npc.OpeningVoiceClip);
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
        playerCamera.enabled = true;
        if (dialogueCamera != null) dialogueCamera.enabled = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void ShowPhoneLine()
    {
        var line = phoneLines[nodeIndex];
        ShowLine(line.speaker, line.text, DialoguePanel.ContinueHint, line.voiceClip);
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
        ShowLine(activeNpc.CharacterName, string.Empty, string.Empty);
    }

    private void ShowScriptedLine()
    {
        waitingForScriptedLine = false;
        var line = activeNpc.ScriptedLines[nodeIndex];
        ShowLine(line.playerSpeaks ? activeNpc.PlayerName : activeNpc.CharacterName,
            line.text, DialoguePanel.ContinueHint, line.voiceClip);
    }

    private void ShowNpcNode()
    {
        if (nodeIndex < 0 || nodeIndex >= activeNpc.Nodes.Length || activeNpc.Nodes[nodeIndex] == null)
        {
            EndDialogue();
            return;
        }
        showingPlayerReply = false;
        NpcDialogue.Node node = activeNpc.Nodes[nodeIndex];
        ShowChoices(node.choices);
        string prompt = availableChoices.Count switch
        {
            0 => DialoguePanel.FinishHint,
            1 => availableChoices[0].EndsDialogue ? DialoguePanel.FinishHint : DialoguePanel.ContinueHint,
            _ => "Выберите ответ"
        };
        ShowLine(activeNpc.CharacterName, node.npcText, prompt, node.voiceClip);
    }

    private void ShowLine(string speaker, string line, string prompt, AudioClip voiceClip = null)
    {
        click.Reset();
        panel.Show(speaker, line, prompt);
        PlayVoice(voiceClip);
    }

    private void PlayVoice(AudioClip voiceClip)
    {
        if (dialogueVoice != null) dialogueVoice.Stop();
        if (voiceClip == null) return;

        if (dialogueVoice == null) dialogueVoice = gameObject.AddComponent<AudioSource>();
        dialogueVoice.clip = voiceClip;
        dialogueVoice.Play();
    }

    private void ShowChoices(NpcDialogue.Choice[] choices)
    {
        availableChoices.Clear();
        if (choices != null)
            foreach (var choice in choices)
                if (choice != null) availableChoices.Add(choice);
        panel.SetChoices(availableChoices, OnChoiceClicked);
    }

    private void OnChoiceClicked(int index)
    {
        if (availableChoices.Count < 2 || !click.CanSelect || GameplayPause.BlocksGameplayInput) return;
        SelectChoice(index);
    }

    private void SelectChoice(int index)
    {
        if (!IsTalking || index < 0 || index >= availableChoices.Count) return;
        NpcDialogue.Choice choice = availableChoices[index];
        if (choice.EndsDialogue)
        {
            EndDialogue(completed: true);
            return;
        }
        ShowPlayerReply(choice.text, activeNpc.GetNextNodeIndex(choice), choice.voiceClip);
    }

    private void ShowPlayerReply(string line, int nextNode, AudioClip voiceClip)
    {
        pendingNextNode = nextNode;
        showingPlayerReply = true;
        ShowChoices(null);
        string prompt = nextNode < 0 ? DialoguePanel.FinishHint : DialoguePanel.ContinueHint;
        ShowLine(activeNpc.PlayerName, line, prompt, voiceClip);
    }

    private void ContinueDialogue()
    {
        if (phoneLines != null)
        {
            nodeIndex++;
            if (nodeIndex < phoneLines.Length) ShowPhoneLine();
            else EndDialogue(completed: true);
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
        if (!showingPlayerReply)
        {
            if (availableChoices.Count == 1) SelectChoice(0);
            else if (availableChoices.Count == 0) EndDialogue(completed: true);
            return;
        }
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
        if (dialogueVoice != null) dialogueVoice.Stop();
        var npc = activeNpc;
        var phone = activePhone;
        activeNpc = null;
        activePhone = null;
        phoneLines = null;
        showingPlayerReply = false;
        waitingForScriptedLine = false;
        availableChoices.Clear();
        panel.Hide();
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
        if (phone != null)
        {
            phone.EndCall(completed);
            if (completed && !string.IsNullOrEmpty(phone.CompletionHint))
            {
                completionHint = phone.CompletionHint;
                completionHintUntil = Time.time + CompletionHintDuration;
                UpdateInteractionPrompt(false, null);
                PlayVoice(phone.CompletionVoiceClip);
            }
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
        else if (dialogueVoice != null) dialogueVoice.Stop();
        if (interactionPrompt != null) interactionPrompt.SetActive(false);
    }
}
