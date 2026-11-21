using System;
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

    [Header("Periodic Highlight")]
    [SerializeField, Min(0.1f), Tooltip("Seconds between the starts of consecutive highlights.")]
    private float pulseInterval = 5f;
    [SerializeField, Min(0f), Tooltip("Seconds a highlight lasts. Set to zero to disable it.")]
    private float pulseDuration = 0.8f;
    [SerializeField] private Color pulseColor = new Color(1f, 0.72f, 0.28f, 0.48f);

    private FirstPersonPlayer player;
    private DocumentReader reader;
    private ItemInspector inspector;
    private DialogueController dialogue;
    private Collider[] nearbyColliders = new Collider[64];
    private readonly List<Renderer> renderers = new List<Renderer>();
    private readonly HashSet<Renderer> includedRenderers = new HashSet<Renderer>();
    private readonly HashSet<Transform> includedTargets = new HashSet<Transform>();
    private readonly Plane[] frustum = new Plane[6];

    private Behaviour offeredTarget;
    private int offeredFrame = -1;
    private float pulseEpoch;

    internal readonly List<Draw> Draws = new List<Draw>();
    internal Camera View => player.PlayerCamera;
    internal Color OutlineColor => outlineColor;
    internal int OutlineWidth => outlineWidth;
    internal Color PulseColor { get; private set; }

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

    private void OnEnable() => pulseEpoch = Time.time;

    private void OnDisable()
    {
        offeredTarget = null;
        offeredFrame = -1;
        Draws.Clear();
    }

    internal void OfferFocus(Behaviour target)
    {
        // Use the first eligible handler this frame, in the same order they get a chance to use E.
        if (isActiveAndEnabled && offeredFrame != Time.frameCount)
        {
            offeredTarget = target;
            offeredFrame = Time.frameCount;
        }
    }

    private void LateUpdate()
    {
        Draws.Clear();
        if (!CanShow) return;

        Camera view = player.PlayerCamera;
        GeometryUtility.CalculateFrustumPlanes(view, frustum);
        includedRenderers.Clear();
        includedTargets.Clear();

        if (offeredFrame == Time.frameCount && offeredTarget != null && IsFocusAvailable(offeredTarget))
            AddTarget(offeredTarget.transform, view, true);

        float interval = Mathf.Max(0.1f, pulseInterval);
        float elapsed = Time.time - pulseEpoch;
        float duration = Mathf.Clamp(pulseDuration, 0f, interval);
        float phase = elapsed % interval;
        if (elapsed < interval || phase >= duration || pulseColor.a <= 0f) return;

        PulseColor = new Color(pulseColor.r, pulseColor.g, pulseColor.b,
            pulseColor.a * Mathf.Sin(Mathf.PI * phase / duration));
        AddAvailableTargets(view);
    }

    private bool IsFocusAvailable(Behaviour target) => target switch
    {
        DoorKey key => key.isActiveAndEnabled,
        KeyDoor door => door.isActiveAndEnabled && door.CanOpen(player),
        PhoneCall phone => phone.CanAnswer,
        _ => true
    };

    private void AddAvailableTargets(Camera view)
    {
        float readingReach = reader != null && reader.isActiveAndEnabled ? reader.HintDistance : 0f;
        float inspectionReach = inspector != null && inspector.isActiveAndEnabled ? inspector.HintDistance : 0f;
        float phoneReach = dialogue != null && dialogue.isActiveAndEnabled ? dialogue.HintDistance : 0f;
        float keyReach = FirstPersonPlayer.KeyInteractionDistance;
        float maxReach = Mathf.Max(Mathf.Max(readingReach, inspectionReach), Mathf.Max(phoneReach, keyReach));
        Vector3 origin = view.transform.position;
        int count = Physics.OverlapSphereNonAlloc(origin, maxReach, nearbyColliders, ~0, QueryTriggerInteraction.Ignore);
        while (count == nearbyColliders.Length)
        {
            Array.Resize(ref nearbyColliders, nearbyColliders.Length * 2);
            count = Physics.OverlapSphereNonAlloc(origin, maxReach, nearbyColliders, ~0, QueryTriggerInteraction.Ignore);
        }

        for (int i = 0; i < count; i++)
        {
            Collider collider = nearbyColliders[i];
            nearbyColliders[i] = null;
            if (collider == null || collider.transform.IsChildOf(transform)) continue;

            // ClosestPoint is only supported for primitive and convex mesh colliders.
            float distanceSquared = collider is BoxCollider || collider is SphereCollider ||
                                    collider is CapsuleCollider || (collider is MeshCollider mesh && mesh.convex)
                ? (collider.ClosestPoint(origin) - origin).sqrMagnitude
                : collider.bounds.SqrDistance(origin);
            Transform target = FindAvailableTarget(collider, distanceSquared, readingReach, inspectionReach, phoneReach);
            if (target != null) AddTarget(target, view, false);
        }
    }

    private Transform FindAvailableTarget(Collider collider, float distanceSquared, float readingReach,
        float inspectionReach, float phoneReach)
    {
        if (readingReach > 0f && distanceSquared <= readingReach * readingReach &&
            collider.GetComponentInParent<ReadableDocument>() is { } document)
            return document.transform;

        if (inspectionReach > 0f && distanceSquared <= inspectionReach * inspectionReach &&
            collider.GetComponentInParent<InspectableItem>() is { } item)
            return item.transform;

        if (phoneReach > 0f && distanceSquared <= phoneReach * phoneReach &&
            collider.GetComponentInParent<PhoneCall>() is { CanAnswer: true } phone)
            return phone.transform;

        float keyReach = FirstPersonPlayer.KeyInteractionDistance;
        if (distanceSquared > keyReach * keyReach) return null;
        if (collider.GetComponentInParent<DoorKey>() is { isActiveAndEnabled: true } key)
            return key.transform;
        if (collider.GetComponentInParent<KeyDoor>() is { isActiveAndEnabled: true } door && door.CanOpen(player))
            return door.transform;
        return null;
    }

    private void AddTarget(Transform target, Camera view, bool outline)
    {
        if (!target.gameObject.activeInHierarchy || target.gameObject.scene.path == InterrogationScene ||
            !includedTargets.Add(target)) return;

        target.GetComponentsInChildren(false, renderers);
        foreach (Renderer renderer in renderers)
        {
            if (!renderer.enabled || renderer.forceRenderingOff || renderer.shadowCastingMode == ShadowCastingMode.ShadowsOnly ||
                (view.cullingMask & (1 << renderer.gameObject.layer)) == 0 ||
                !GeometryUtility.TestPlanesAABB(frustum, renderer.bounds))
                continue;

            Mesh mesh = renderer is SkinnedMeshRenderer skinned ? skinned.sharedMesh :
                renderer is MeshRenderer && renderer.TryGetComponent(out MeshFilter filter) ? filter.sharedMesh : null;
            if (mesh != null && includedRenderers.Add(renderer))
                Draws.Add(new Draw(renderer, mesh.subMeshCount, outline));
        }
    }

    internal readonly struct Draw
    {
        internal readonly Renderer Renderer;
        internal readonly int SubmeshCount;
        internal readonly bool Outline;

        internal Draw(Renderer renderer, int submeshCount, bool outline)
        {
            Renderer = renderer;
            SubmeshCount = submeshCount;
            Outline = outline;
        }
    }
}
