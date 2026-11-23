using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[RequireComponent(typeof(FirstPersonPlayer))]
[DefaultExecutionOrder(100)]
public sealed class InteractionHints : MonoBehaviour
{
    private const string InterrogationScene =
        "Assets/Level/Scenes/Locations/MindsScenes/ThirdMind-interogation_room.unity";

    [Header("Focus Outline")]
    [SerializeField] private Color outlineColor = new Color(1f, 0.86f, 0.45f, 0.95f);
    [SerializeField, Range(1, 4)] private int outlineWidth = 2;

    private FirstPersonPlayer player;
    private DocumentReader reader;
    private ItemInspector inspector;
    private DialogueController dialogue;
    private readonly List<Renderer> renderers = new List<Renderer>();
    private readonly Plane[] frustum = new Plane[6];

    private Behaviour offeredTarget;
    private int offeredFrame = -1;

    internal readonly List<Draw> Draws = new List<Draw>();
    internal Camera View => player.PlayerCamera;
    internal Color OutlineColor => outlineColor;
    internal int OutlineWidth => outlineWidth;

    internal bool CanShow => isActiveAndEnabled && player.isActiveAndEnabled &&
        player.PlayerCamera != null && player.PlayerCamera.isActiveAndEnabled &&
        Keyboard.current != null && gameObject.scene.path != InterrogationScene &&
        SceneManager.GetActiveScene().path != InterrogationScene &&
        (dialogue == null || !dialogue.IsTalking) &&
        (reader == null || !reader.IsReading) && (inspector == null || !inspector.IsInspecting);

    private void Awake()
    {
        player = GetComponent<FirstPersonPlayer>();
        reader = GetComponent<DocumentReader>();
        inspector = GetComponent<ItemInspector>();
        dialogue = GetComponent<DialogueController>();
    }

    private void OnDisable()
    {
        offeredTarget = null;
        offeredFrame = -1;
        Draws.Clear();
    }

    internal void OfferFocus(Behaviour target)
    {
        // Use the first eligible handler this frame, in the same order they get a chance to use E.
        if (target != null && isActiveAndEnabled && offeredFrame != Time.frameCount)
        {
            offeredTarget = target;
            offeredFrame = Time.frameCount;
        }
    }

    private void LateUpdate()
    {
        Draws.Clear();
        if (offeredFrame != Time.frameCount || offeredTarget == null ||
            !CanShow || !IsFocusAvailable(offeredTarget)) return;

        Camera view = player.PlayerCamera;
        AddTarget(offeredTarget.transform, view);
    }

    private bool IsFocusAvailable(Behaviour target) => target switch
    {
        DoorKey key => key.CanPickUp,
        KeyDoor door => door.isActiveAndEnabled && door.CanInteract(player),
        PhoneCall phone => phone.CanAnswer,
        _ => true
    };

    private void AddTarget(Transform target, Camera view)
    {
        if (!target.gameObject.activeInHierarchy || target.gameObject.scene.path == InterrogationScene) return;

        GeometryUtility.CalculateFrustumPlanes(view, frustum);
        target.GetComponentsInChildren(false, renderers);
        foreach (Renderer renderer in renderers)
        {
            if (!renderer.enabled || renderer.forceRenderingOff || renderer.shadowCastingMode == ShadowCastingMode.ShadowsOnly ||
                (view.cullingMask & (1 << renderer.gameObject.layer)) == 0 ||
                !GeometryUtility.TestPlanesAABB(frustum, renderer.bounds))
                continue;

            Mesh mesh = renderer is SkinnedMeshRenderer skinned ? skinned.sharedMesh :
                renderer is MeshRenderer && renderer.TryGetComponent(out MeshFilter filter) ? filter.sharedMesh : null;
            if (mesh != null)
                Draws.Add(new Draw(renderer, mesh.subMeshCount));
        }
    }

    internal readonly struct Draw
    {
        internal readonly Renderer Renderer;
        internal readonly int SubmeshCount;
        internal Draw(Renderer renderer, int submeshCount)
        {
            Renderer = renderer;
            SubmeshCount = submeshCount;
        }
    }
}
