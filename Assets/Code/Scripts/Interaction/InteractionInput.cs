using UnityEngine;
using UnityEngine.InputSystem;

// Each press belongs to one interaction, including the frame that closes a modal panel.
public static class InteractionInput
{
    private static int consumedFrame = -1;
    private static int cancelFrame = -1;
    private static int primaryFrame = -1;

    internal static bool PrimaryConsumedThisFrame => primaryFrame == Time.frameCount;

    internal static void ConsumePrimary() => primaryFrame = Time.frameCount;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState() => consumedFrame = cancelFrame = primaryFrame = -1;

    public static bool TryConsumeCancel()
    {
        if (GameplayPause.BlocksGameplayInput || cancelFrame == Time.frameCount ||
            Keyboard.current?.escapeKey.wasPressedThisFrame != true) return false;
        consumedFrame = cancelFrame = Time.frameCount;
        return true;
    }

    public static bool TryConsume()
    {
        if (GameplayPause.BlocksGameplayInput || consumedFrame == Time.frameCount) return false;
        consumedFrame = Time.frameCount;
        return true;
    }
}
