using System;
using System.Reflection;
using UnityEditor;

internal static class FinalLabGameViewSize
{
    private const string PreviousIndex = "FinalLab.PreviousGameViewIndex";
    private const string TemporaryIndex = "FinalLab.TemporaryGameViewIndex";
    private const BindingFlags Members = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    internal static void SetResolution(int width, int height)
    {
        var (window, selection, group) = Current();
        int previous = (int)selection.GetValue(window);
        if (SessionState.GetInt(PreviousIndex, -1) < 0)
            SessionState.SetInt(PreviousIndex, previous);
        var groupType = group.GetType();
        int count = (int)groupType.GetMethod("GetTotalCount", Members).Invoke(group, null);
        var getSize = groupType.GetMethod("GetGameViewSize", Members);
        for (int i = 0; i < count; i++)
        {
            var value = getSize.Invoke(group, new object[] { i });
            var definition = value.GetType();
            if ((int)definition.GetProperty("width", Members).GetValue(value) != width ||
                (int)definition.GetProperty("height", Members).GetValue(value) != height) continue;
            selection.SetValue(window, i);
            return;
        }
        var editor = window.GetType().Assembly;
        var sizeType = editor.GetType("UnityEditor.GameViewSize");
        var kind = editor.GetType("UnityEditor.GameViewSizeType");
        var constructor = sizeType.GetConstructor(Members, null,
            new[] { kind, typeof(int), typeof(int), typeof(string) }, null);
        var custom = constructor.Invoke(new[] { Enum.Parse(kind, "FixedResolution"), (object)width, height, "Final Lab Play Check" });
        groupType.GetMethod("AddCustomSize", Members).Invoke(group, new[] { custom });
        SessionState.SetInt(TemporaryIndex + width, count);
        selection.SetValue(window, count);
    }

    internal static void Restore()
    {
        int previous = SessionState.GetInt(PreviousIndex, -1);
        if (previous < 0) return;
        var (window, selection, group) = Current();
        selection.SetValue(window, previous);
        var groupType = group.GetType();
        int builtin = (int)groupType.GetMethod("GetBuiltinCount", Members).Invoke(group, null);
        foreach (int width in new[] { 1366, 1920 })
        {
            string key = TemporaryIndex + width;
            int index = SessionState.GetInt(key, -1);
            if (index >= builtin)
                groupType.GetMethod("RemoveCustomSize", Members).Invoke(group, new object[] { index - builtin });
            SessionState.EraseInt(key);
        }
        SessionState.EraseInt(PreviousIndex);
    }

    private static (EditorWindow, PropertyInfo, object) Current()
    {
        var gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
        var editor = gameViewType.Assembly;
        var window = EditorWindow.GetWindow(gameViewType);
        var sizes = editor.GetType("UnityEditor.GameViewSizes");
        var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizes);
        var current = singleton.GetProperty("instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
        var groupType = editor.GetType("UnityEditor.GameViewSizeGroupType");
        var getGroup = sizes.GetMethod("GetGroup", Members, null, new[] { groupType }, null);
        var group = getGroup.Invoke(current, new[] { Enum.Parse(groupType, "Standalone") });
        return (window, gameViewType.GetProperty("selectedSizeIndex", Members), group);
    }
}
