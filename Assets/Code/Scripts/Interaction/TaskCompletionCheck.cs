using UnityEngine;

public class TaskCompletionCheck : MonoBehaviour
{
    [SerializeField] private bool completed;

    public virtual bool IsCompleted => completed;

    public void MarkCompleted()
    {
        completed = true;
    }
}
