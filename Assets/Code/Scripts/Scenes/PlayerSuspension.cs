using System.Collections.Generic;
using UnityEngine;

internal sealed class PlayerSuspension
{
    private readonly List<Behaviour> enabledControls = new List<Behaviour>();
    private readonly FirstPersonPlayer player;
    private readonly CursorLockMode cursorLock;
    private readonly bool cursorVisible;

    internal PlayerSuspension(FirstPersonPlayer target)
    {
        player = target;
        // Close modals first because they can re-enable movement from OnDisable.
        Disable(target.GetComponent<SmokingController>());
        Disable(target.GetComponent<DialogueController>());
        Disable(target.GetComponent<ItemInspector>());
        Disable(target.GetComponent<DocumentReader>());
        cursorLock = Cursor.lockState;
        cursorVisible = Cursor.visible;
        Disable(target.GetComponent<PistolController>());
        Disable(target.GetComponent<SceneSwitch>());
        Disable(target.GetComponent<PlayerStress>());
        Disable(target.GetComponent<PlayerMovement>());
        Disable(target);
    }

    private void Disable(Behaviour control)
    {
        if (control == null || !control.enabled) return;
        enabledControls.Add(control);
        control.enabled = false;
    }

    internal void Restore()
    {
        if (player == null) return;
        foreach (var control in enabledControls)
            if (control != null) control.enabled = true;
        Cursor.lockState = cursorLock;
        Cursor.visible = cursorVisible;
        enabledControls.Clear();
    }
}
