using System;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class NpcDialogue : MonoBehaviour
{
    public enum ChoiceAction
    {
        [InspectorName("Перейти к узлу")] NextNode = 0,
        [InspectorName("Завершить диалог")] EndDialogue = 1
    }

    [Serializable]
    public sealed class Choice
    {
        [TextArea(1, 2)] public string text;
        [HideInInspector] public int nextNode = -1; // Legacy index-based dialogue data.
        [SerializeField, HideInInspector] private int targetNodeId;
        [Tooltip("Завершает диалог сразу при выборе ответа.")]
        public ChoiceAction action;

        public int TargetNodeId => targetNodeId;
        public bool EndsDialogue => action == ChoiceAction.EndDialogue;
    }

    [Serializable]
    public sealed class Node
    {
        [SerializeField, HideInInspector] private int id;
        [TextArea(2, 5)] public string npcText;
        public Choice[] choices;

        public int Id => id;
    }

    [SerializeField] private string characterName = "Смотритель";
    [SerializeField, HideInInspector] private int startNodeId;
    [SerializeField] private Node[] nodes;

    public string CharacterName => characterName;
    public Node[] Nodes => nodes;
    public int StartNodeId => startNodeId;

    public int GetStartNodeIndex()
    {
        if (nodes == null || nodes.Length == 0) return -1;
        return startNodeId == 0 ? 0 : FindNodeIndex(startNodeId);
    }

    public int GetNextNodeIndex(Choice choice) =>
        startNodeId == 0 ? choice.nextNode : FindNodeIndex(choice.TargetNodeId);

    private int FindNodeIndex(int id)
    {
        // Zero is the authored ending after Continue.
        if (id <= 0) return -1;
        return Array.FindIndex(nodes, node => node != null && node.Id == id);
    }
}
