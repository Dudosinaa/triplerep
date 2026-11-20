using UnityEngine;

public sealed class VictorMemoryDialogue : MonoBehaviour
{
    [SerializeField] private CargoPuzzleTerminal terminal;
    [SerializeField] private NpcDialogue dialogue;

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
        start.npcText = "Твой отец хотел оставить всё это там. Иван был категорически против. " +
            "Они тогда сильно поссорились. Я помню, как отец сказал: «Ты не имеешь права это отправлять». " +
            "Похоже, он пытался остановить работу, а Иван продолжил её сам.";
        start.choices = new[]
        {
            new NpcDialogue.Choice { text = "Понятно. У меня есть копия накладной.", nextNode = -1 }
        };
    }

    private void OnConversationCompleted(FirstPersonPlayer player)
    {
        introductionCompleted = true;
        if (!discussingWaybill || terminal == null || !terminal.IsSolved) return;

        if (!TrainMindSession.TryReturn(gameObject.scene))
            LocationTransition.TryBegin(player, TrainMindSession.TrainPath, 0.55f, 0.65f, this);
    }
}
