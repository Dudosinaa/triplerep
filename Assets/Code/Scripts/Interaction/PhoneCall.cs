using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public sealed class PhoneCall : MonoBehaviour
{
    [Serializable]
    internal struct Line
    {
        public string speaker;
        [TextArea(2, 4)] public string text;
        public AudioClip voiceClip;
    }

    private enum CallState
    {
        WaitingForTasks,
        CountingDown,
        Ringing,
        InProgress,
        Completed
    }

    [SerializeField] private CurrentTaskList taskList;
    [SerializeField, Min(0f)] private float ringDelay = 15f;
    [SerializeField] private Line[] dialogue = Array.Empty<Line>();

    [SerializeField] private string completionHint;
    [SerializeField] private AudioClip completionVoiceClip;

    private AudioSource ringSource;
    private CallState state;
    private float ringAt;

    internal bool CanAnswer => isActiveAndEnabled && state == CallState.Ringing && dialogue.Length > 0;

    internal bool IsCompleted => state == CallState.Completed;
    internal string CompletionHint => completionHint;
    internal AudioClip CompletionVoiceClip => completionVoiceClip;

    private void Awake()
    {
        ringSource = GetComponent<AudioSource>();
        ringSource.loop = true;
    }

    private void OnEnable()
    {
        if (taskList != null)
        {
            taskList.AllTasksCompleted += StartCountdown;
            if (taskList.CompletionReported) StartCountdown();
        }

        if (state == CallState.Ringing) PlayRing();
    }

    private void OnDisable()
    {
        if (taskList != null) taskList.AllTasksCompleted -= StartCountdown;
        ringSource.Stop();
    }

    private void StartCountdown()
    {
        if (state != CallState.WaitingForTasks) return;

        ringAt = Time.time + Mathf.Max(0f, ringDelay);
        state = CallState.CountingDown;
    }

    private void Update()
    {
        if (state != CallState.CountingDown || Time.time < ringAt) return;

        state = CallState.Ringing;
        PlayRing();
    }

    private void PlayRing()
    {
        if (ringSource.clip != null) ringSource.Play();
    }

    internal bool TryAnswer(out Line[] lines)
    {
        lines = null;
        if (!CanAnswer) return false;

        state = CallState.InProgress;
        ringSource.Stop();
        lines = dialogue;
        return true;
    }

    internal void EndCall(bool completed)
    {
        if (state != CallState.InProgress) return;

        state = completed ? CallState.Completed : CallState.Ringing;
        if (!completed && isActiveAndEnabled) PlayRing();
    }
}
