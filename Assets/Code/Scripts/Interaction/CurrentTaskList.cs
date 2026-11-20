using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CurrentTaskList : MonoBehaviour
{
    [SerializeField] private List<TaskCompletionCheck> currentTasks = new List<TaskCompletionCheck>();

    public event Action AllTasksCompleted;

    internal bool CompletionReported { get; private set; }

    public bool AreAllTasksCompleted()
    {
        foreach (var task in currentTasks)
            if (task == null || !task.IsCompleted) return false;

        return true;
    }

    private void Update()
    {
        if (CompletionReported || !AreAllTasksCompleted()) return;

        CompletionReported = true;
        AllTasksCompleted?.Invoke();
    }
}
