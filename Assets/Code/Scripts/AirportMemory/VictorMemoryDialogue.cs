using UnityEngine;

// After the waybill is identified, Victor reveals why the shipment matters.
public sealed class VictorMemoryDialogue : MonoBehaviour
{
    [SerializeField] private CargoPuzzleTerminal terminal;
    [SerializeField] private NpcDialogue dialogue;
    private bool revealed;

    public void Configure(CargoPuzzleTerminal cargoTerminal, NpcDialogue npcDialogue)
    {
        terminal = cargoTerminal;
        dialogue = npcDialogue;
    }

    private void Update()
    {
        if (revealed || terminal == null || !terminal.IsSolved || dialogue == null ||
            dialogue.Nodes == null || dialogue.Nodes.Length == 0) return;
        revealed = true;
        dialogue.Nodes[0].npcText = "Твой отец хотел оставить всё это там. Иван был категорически против. " +
            "Они тогда сильно поссорились. Я помню, как отец сказал: «Ты не имеешь права это отправлять». " +
            "Похоже, он пытался остановить работу, а Иван продолжил её сам.";
        dialogue.Nodes[0].choices = new[]
        {
            new NpcDialogue.Choice { text = "Понятно. У меня есть копия накладной.", nextNode = -1 }
        };
    }
}
