using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class FinalLabIvanDialogue : MonoBehaviour
{
    [Serializable]
    public sealed class Line
    {
        public string speaker, text, animation, look;
        public float pause;
    }

    [Serializable]
    private sealed class Transcript { public Line[] lines; }

    [SerializeField] private TextAsset transcript;
    [SerializeField] private AudioClip[] transcriptVoices = Array.Empty<AudioClip>();
    [SerializeField] private Canvas canvas;
    [SerializeField] private TMP_Text speaker, body;
    private int shownFrame;
    private bool released, continueRequested;
    private AudioSource voiceSource;
    public Line[] ReadLines() => JsonUtility.FromJson<Transcript>(transcript.text).lines;
    public int Index { get; private set; } = -1;
    public bool Visible => canvas.enabled;
    public TMP_Text Speaker => speaker;
    public TMP_Text Body => body;

    public void Configure(TextAsset source, Canvas panel, TMP_Text actor, TMP_Text line)
    {
        transcript = source;
        canvas = panel;
        speaker = actor;
        body = line;
    }

    public void Show(int index, Line line) => Show(index, line.speaker, line.text,
        index >= 0 && index < transcriptVoices.Length ? transcriptVoices[index] : null);

    public void Show(int index, string name, string text, AudioClip voiceClip = null)
    {
        Index = index;
        speaker.text = name;
        body.text = text;
        canvas.enabled = true;
        shownFrame = Time.frameCount;
        released = Mouse.current == null || !Mouse.current.leftButton.isPressed;
        continueRequested = false;
        StopVoice();
        if (voiceClip == null) return;
        if (voiceSource == null) voiceSource = gameObject.AddComponent<AudioSource>();
        voiceSource.clip = voiceClip;
        voiceSource.Play();
    }

    public bool ReadContinue()
    {
        if (continueRequested) return true;
        var mouse = Mouse.current;
        if (!Visible || mouse == null || Time.frameCount == shownFrame) return false;
        if (!released) released = !mouse.leftButton.isPressed;
        else continueRequested = mouse.leftButton.wasPressedThisFrame;
        if (continueRequested) StopVoice();
        return continueRequested;
    }

    public void Hide()
    {
        StopVoice();
        if (canvas != null) canvas.enabled = false;
    }

    private void OnDisable() => StopVoice();

    private void StopVoice()
    {
        if (voiceSource != null) voiceSource.Stop();
    }
}
