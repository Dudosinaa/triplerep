using UnityEngine;
using UnityEngine.SceneManagement;

public sealed partial class FirstPersonPlayer
{
    [Header("Fall Recovery")]
    [SerializeField, Tooltip("World Y below the lowest walkable surface in this scene.")]
    private float fallRecoveryY = -50f;

    private Scene fallRecoveryScene;
    private Vector3 fallRecoveryStart;

    private void CaptureFallRecoveryStart()
    {
        Scene scene = gameObject.scene;
        if (fallRecoveryScene == scene) return;

        // Intro and transition scripts enable movement after placing the player.
        fallRecoveryStart = transform.position;
        fallRecoveryScene = scene;
    }

    private void RecoverFromFall()
    {
        if (transform.position.y < fallRecoveryY && fallRecoveryStart.y >= fallRecoveryY)
            Teleport(fallRecoveryStart);
    }
}
