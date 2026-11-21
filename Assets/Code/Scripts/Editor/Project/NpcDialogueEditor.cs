using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(NpcDialogue))]
internal sealed class NpcDialogueEditor : Editor
{
    private const int VisibleChoiceCount = 3;
    private readonly HashSet<int> expandedNodes = new HashSet<int>();
    private SerializedProperty nodes;
    private SerializedProperty start;

    private void OnEnable()
    {
        nodes = serializedObject.FindProperty("nodes");
        start = serializedObject.FindProperty("startNodeId");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("characterName"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("playerName"), new GUIContent("Имя игрока"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("openingLine"), new GUIContent("Вступительная реплика игрока"));

        if (nodes.arraySize > 0 && start.intValue == 0)
        {
            EditorGUILayout.HelpBox("Диалог использует старые индексы. Обновите формат для редактирования списка.",
                MessageType.Info);
            if (GUILayout.Button("Обновить формат диалога")) MigrateLegacyNodes(nodes, start);
            serializedObject.ApplyModifiedProperties();
            return;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Nodes", EditorStyles.boldLabel);
        if (nodes.arraySize > 0) DrawTargetPopup(start, "Стартовый узел", false);
        for (int i = 0; i < nodes.arraySize; i++) DrawNode(i);

        if (GUILayout.Button("Добавить узел"))
        {
            expandedNodes.Add(AddNode(nodes, start));
            FinishStructureEdit();
        }

        serializedObject.ApplyModifiedProperties();
        foreach (string warning in GetWarnings((NpcDialogue)target))
            EditorGUILayout.HelpBox(warning, MessageType.Warning);
    }

    private void DrawNode(int index)
    {
        var node = nodes.GetArrayElementAtIndex(index);
        int id = node.FindPropertyRelative("id").intValue;
        var text = node.FindPropertyRelative("npcText");
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                string heading = DescribeNode(id, text.stringValue);
                if (id == start.intValue) heading = "Старт · " + heading;
                bool expanded = EditorGUILayout.Foldout(expandedNodes.Contains(id), heading, true);
                if (expanded) expandedNodes.Add(id);
                else expandedNodes.Remove(id);

                using (new EditorGUI.DisabledScope(index == 0))
                    if (GUILayout.Button(new GUIContent("↑", "Поднять узел"), GUILayout.Width(26)))
                        MoveNode(index, index - 1);
                using (new EditorGUI.DisabledScope(index == nodes.arraySize - 1))
                    if (GUILayout.Button(new GUIContent("↓", "Опустить узел"), GUILayout.Width(26)))
                        MoveNode(index, index + 1);
                if (GUILayout.Button(new GUIContent("×", "Удалить узел"), GUILayout.Width(26)))
                {
                    nodes.DeleteArrayElementAtIndex(index);
                    expandedNodes.Remove(id);
                    FinishStructureEdit();
                }
            }

            if (!expandedNodes.Contains(id)) return;
            EditorGUILayout.PropertyField(text, new GUIContent("Реплика NPC"));
            var choices = node.FindPropertyRelative("choices");
            for (int i = 0; i < choices.arraySize; i++) DrawChoice(choices, i);
            using (new EditorGUI.DisabledScope(choices.arraySize >= VisibleChoiceCount))
            {
                if (GUILayout.Button(new GUIContent("Добавить ответ", "Игровой интерфейс показывает до трёх ответов.")))
                {
                    AddChoice(choices);
                    FinishStructureEdit();
                }
            }
        }
    }

    private void DrawChoice(SerializedProperty choices, int index)
    {
        var choice = choices.GetArrayElementAtIndex(index);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Ответ " + (index + 1), EditorStyles.miniBoldLabel);
                if (GUILayout.Button(new GUIContent("×", "Удалить ответ"), GUILayout.Width(26)))
                {
                    choices.DeleteArrayElementAtIndex(index);
                    FinishStructureEdit();
                }
            }
            EditorGUILayout.PropertyField(choice.FindPropertyRelative("text"), new GUIContent("Текст"));
            var action = choice.FindPropertyRelative("action");
            EditorGUILayout.PropertyField(action, new GUIContent("Action", action.tooltip));
            if (action.intValue != (int)NpcDialogue.ChoiceAction.EndDialogue)
                DrawTargetPopup(choice.FindPropertyRelative("targetNodeId"), "Следующий узел", true);
        }
    }

    private void DrawTargetPopup(SerializedProperty property, string label, bool allowContinueEnd)
    {
        var labels = new List<string>();
        var ids = new List<int>();
        if (allowContinueEnd)
        {
            labels.Add("Завершить после «Продолжить»");
            ids.Add(0);
        }
        for (int i = 0; i < nodes.arraySize; i++)
        {
            var node = nodes.GetArrayElementAtIndex(i);
            int id = node.FindPropertyRelative("id").intValue;
            // Unity's popup treats slashes in labels as submenu separators.
            labels.Add(DescribeNode(id, node.FindPropertyRelative("npcText").stringValue).Replace('/', '／'));
            ids.Add(id);
        }

        int selected = ids.IndexOf(property.intValue);
        if (selected < 0)
        {
            labels.Insert(0, "Отсутствует узел #" + property.intValue);
            ids.Insert(0, property.intValue);
            selected = 0;
        }

        Rect rect = EditorGUILayout.GetControlRect();
        using (var scope = new EditorGUI.PropertyScope(rect, new GUIContent(label), property))
        {
            int picked = EditorGUI.Popup(rect, scope.content, selected, ToOptions(labels));
            if (picked != selected) property.intValue = ids[picked];
        }
    }

    private static GUIContent[] ToOptions(List<string> labels) =>
        labels.ConvertAll(label => new GUIContent(label)).ToArray();

    private void MoveNode(int from, int to)
    {
        nodes.MoveArrayElement(from, to);
        FinishStructureEdit();
    }

    private void FinishStructureEdit()
    {
        serializedObject.ApplyModifiedProperties();
        GUIUtility.ExitGUI();
    }

    internal static int AddNode(SerializedProperty nodes, SerializedProperty start)
    {
        // Retain IDs referenced by deleted targets and the deleted start so new nodes cannot inherit those links.
        var reserved = new HashSet<int> { start.intValue };
        for (int i = 0; i < nodes.arraySize; i++)
        {
            var node = nodes.GetArrayElementAtIndex(i);
            reserved.Add(node.FindPropertyRelative("id").intValue);
            var choices = node.FindPropertyRelative("choices");
            for (int j = 0; j < choices.arraySize; j++)
                reserved.Add(choices.GetArrayElementAtIndex(j).FindPropertyRelative("targetNodeId").intValue);
        }

        int id = 1;
        while (reserved.Contains(id)) id++;
        int index = nodes.arraySize++;
        var added = nodes.GetArrayElementAtIndex(index);
        added.FindPropertyRelative("id").intValue = id;
        added.FindPropertyRelative("npcText").stringValue = "";
        added.FindPropertyRelative("choices").arraySize = 0;
        if (start.intValue == 0) start.intValue = id;
        return id;
    }

    internal static void AddChoice(SerializedProperty choices)
    {
        int index = choices.arraySize++;
        var choice = choices.GetArrayElementAtIndex(index);
        choice.FindPropertyRelative("text").stringValue = "";
        choice.FindPropertyRelative("action").intValue = (int)NpcDialogue.ChoiceAction.NextNode;
        choice.FindPropertyRelative("targetNodeId").intValue = 0;
        choice.FindPropertyRelative("nextNode").intValue = -1;
    }

    internal static void MigrateLegacyNodes(SerializedProperty nodes, SerializedProperty start)
    {
        if (nodes.arraySize == 0 || start.intValue != 0) return;
        start.intValue = 1;
        for (int i = 0; i < nodes.arraySize; i++)
        {
            var node = nodes.GetArrayElementAtIndex(i);
            node.FindPropertyRelative("id").intValue = i + 1;
            var choices = node.FindPropertyRelative("choices");
            for (int j = 0; j < choices.arraySize; j++)
            {
                var choice = choices.GetArrayElementAtIndex(j);
                int index = choice.FindPropertyRelative("nextNode").intValue;
                choice.FindPropertyRelative("targetNodeId").intValue =
                    index < 0 ? 0 : index == int.MaxValue ? -1 : index + 1;
            }
        }
    }

    internal static List<string> GetWarnings(NpcDialogue npc)
    {
        var warnings = new List<string>();
        var graph = npc.Nodes ?? System.Array.Empty<NpcDialogue.Node>();
        int startIndex = npc.GetStartNodeIndex();
        if (startIndex < 0 && npc.StartNodeId != 0)
            warnings.Add($"Стартовый узел #{npc.StartNodeId} отсутствует. Выберите новый старт.");

        var outgoing = new List<int>[graph.Length];
        var incoming = new List<int>[graph.Length];
        var canExit = new bool[graph.Length];
        var exitQueue = new Queue<int>();
        for (int i = 0; i < graph.Length; i++)
        {
            outgoing[i] = new List<int>();
            incoming[i] = new List<int>();
        }

        for (int i = 0; i < graph.Length; i++)
        {
            var node = graph[i];
            var choices = node?.choices;
            if (choices == null) continue;
            if (choices.Length > VisibleChoiceCount)
                warnings.Add($"Узел #{node.Id}: в игре доступны только первые {VisibleChoiceCount} ответа.");
            for (int j = 0; j < choices.Length; j++)
            {
                var choice = choices[j];
                if (choice == null) continue;
                bool ends = choice.EndsDialogue || choice.TargetNodeId == 0;
                int next = ends ? -1 : npc.GetNextNodeIndex(choice);
                if (!ends && next < 0)
                {
                    warnings.Add($"Узел #{node.Id}, ответ {j + 1} «{Preview(choice.text)}»: отсутствует цель #{choice.TargetNodeId}.");
                    continue;
                }
                if (j >= VisibleChoiceCount) continue;
                if (ends)
                {
                    if (!canExit[i]) exitQueue.Enqueue(i);
                    canExit[i] = true;
                }
                else
                {
                    outgoing[i].Add(next);
                    incoming[next].Add(i);
                }
            }
        }

        // Work backwards from every authored ending to include branches that can reach one through a cycle.
        while (exitQueue.Count > 0)
        {
            foreach (int previous in incoming[exitQueue.Dequeue()])
            {
                if (canExit[previous]) continue;
                canExit[previous] = true;
                exitQueue.Enqueue(previous);
            }
        }
        if (startIndex < 0) return warnings;

        var reachable = new HashSet<int> { startIndex };
        var todo = new Queue<int>(reachable);
        while (todo.Count > 0)
        {
            int current = todo.Dequeue();
            if (!canExit[current])
                warnings.Add($"Из узла {DescribeNode(graph[current].Id, graph[current].npcText)} нет пути к завершению.");
            foreach (int next in outgoing[current])
                if (reachable.Add(next)) todo.Enqueue(next);
        }
        return warnings;
    }

    private static string DescribeNode(int id, string line) => $"#{id} · {Preview(line)}";

    private static string Preview(string line)
    {
        const int limit = 48;
        string singleLine = Regex.Replace(line ?? "", @"\s+", " ").Trim();
        if (singleLine.Length == 0) return "Без текста";
        return singleLine.Length <= limit ? singleLine : singleLine.Substring(0, limit - 1) + "…";
    }
}
