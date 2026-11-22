using System;
using TMPro;
using UnityEngine;

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
    [SerializeField] private Canvas canvas;
    [SerializeField] private TMP_Text speaker, body;
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

    public void Show(int index, Line line)
    {
        Index = index;
        speaker.text = line.speaker;
        body.text = line.text;
        canvas.enabled = true;
    }

    public void Hide()
    {
        if (canvas != null) canvas.enabled = false;
    }
}
