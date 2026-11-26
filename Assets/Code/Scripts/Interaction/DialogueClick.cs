using UnityEngine;
using UnityEngine.InputSystem;

internal struct DialogueClick
{
    private int shownFrame;
    private int releasedFrame;
    private bool awaitingRelease;

    internal bool IsNewLine => shownFrame == Time.frameCount;
    internal bool CanSelect => !IsNewLine && !awaitingRelease && releasedFrame != Time.frameCount;

    internal void Reset()
    {
        shownFrame = Time.frameCount;
        releasedFrame = -1;
        awaitingRelease = Mouse.current?.leftButton.isPressed == true;
    }

    internal bool ReadPress()
    {
        var button = Mouse.current?.leftButton;
        if (IsNewLine || button == null) return false;
        if (!awaitingRelease) return button.wasPressedThisFrame;
        if (!button.isPressed)
        {
            awaitingRelease = false;
            releasedFrame = Time.frameCount;
        }
        return false;
    }
}
