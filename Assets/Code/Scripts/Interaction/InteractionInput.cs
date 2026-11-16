using UnityEngine;

// One E press belongs to one interaction, including the frame that closes a modal panel.
public static class InteractionInput
{
    private static int consumedFrame = -1;
    public static bool TryConsume()
    {
        if (consumedFrame == Time.frameCount) return false;
        consumedFrame = Time.frameCount;
        return true;
    }
}
