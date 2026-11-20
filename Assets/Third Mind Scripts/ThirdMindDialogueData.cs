using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class ThirdMindDialogueLine
{
    public string characterName;
    [TextArea(2, 5)] public string text;
    public AudioClip voiceClip;
}

[CreateAssetMenu(fileName = "ThirdMindDialogue", menuName = "Third Mind/Dialogue")]
public sealed class ThirdMindDialogueData : ScriptableObject
{
    public List<ThirdMindDialogueLine> opening = new List<ThirdMindDialogueLine>();
    public List<ThirdMindDialogueLine> confession = new List<ThirdMindDialogueLine>();
}
