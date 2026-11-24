using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class VictorMemoryDialogue : MonoBehaviour
{
    [SerializeField] private CargoPuzzleTerminal terminal;
    [SerializeField] private NpcDialogue dialogue;
    [SerializeField] private AudioClip waybillVoiceClip;
    [SerializeField] private AudioClip waybillReplyVoiceClip;

    private bool introductionCompleted;
    private bool discussingWaybill;

    public void Configure(CargoPuzzleTerminal cargoTerminal, NpcDialogue npcDialogue)
    {
        terminal = cargoTerminal;
        dialogue = npcDialogue;
    }

    private void OnEnable()
    {
        if (dialogue == null) return;
        dialogue.ConversationStarted += OnConversationStarted;
        dialogue.ConversationCompleted += OnConversationCompleted;
    }

    private void OnDisable()
    {
        if (dialogue == null) return;
        dialogue.ConversationStarted -= OnConversationStarted;
        dialogue.ConversationCompleted -= OnConversationCompleted;
    }

    private void OnConversationStarted()
    {
        if (discussingWaybill || !introductionCompleted || terminal == null || !terminal.IsSolved) return;
        int startIndex = dialogue.GetStartNodeIndex();
        if (startIndex < 0) return;

        discussingWaybill = true;
        var start = dialogue.Nodes[startIndex];
        start.voiceClip = waybillVoiceClip;
        start.npcText = "Твой отец хотел оставить всё это там. Иван был категорически против. " +
            "Они тогда сильно поссорились. Я помню, как отец сказал: «Ты не имеешь права это отправлять». " +
            "Похоже, он пытался остановить работу, а Иван продолжил её сам.";
        start.choices = new[]
        {
            new NpcDialogue.Choice
            {
                text = "Понятно. У меня есть копия накладной.",
                nextNode = -1,
                voiceClip = waybillReplyVoiceClip
            }
        };
    }

    private void OnConversationCompleted(FirstPersonPlayer player)
    {
        introductionCompleted = true;
        if (!discussingWaybill || terminal == null || !terminal.IsSolved) return;

        SceneEntryCheckpoint.CompleteTrainMemory(SceneEntryCheckpoint.TrainMemory.Innokenty, gameObject.scene);
        if (TrainMindSession.TryReturn(gameObject.scene)) return;
        int destination = SceneEntryCheckpoint.GetReturnIndex(SceneUtility.GetBuildIndexByScenePath(TrainMindSession.TrainPath));
        LocationTransition.TryBegin(player, SceneUtility.GetScenePathByBuildIndex(destination), 0.55f, 0.65f, this);
    }
}
