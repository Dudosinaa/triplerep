using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public sealed class SceneSwitch : MonoBehaviour
{
    public float interactionDistance = 1.5f;
    public float moveDuration = 2f;
    [SerializeField, ColorUsage(false), InspectorName("Mind Vignette Color")]
    private Color mindVignetteColor = Color.black;
    public TMP_Text hintText;
    public bool returnOnly;
    [Min(0)] public int returnSceneIndex = 1;
    private static int entrySceneIndex = -1;
    private bool isMoving;
    private FocusPoint currentPoint;
    private readonly RaycastHit[] aimHits = new RaycastHit[16];
    private Camera playerCamera;

    internal Color MindVignetteColor => mindVignetteColor;

    private void Awake() => playerCamera = GetComponentInChildren<Camera>();
    private void Start() => ClearTarget();
    private void OnDisable() => ClearTarget();

    private void LateUpdate()
    {
        var keyboard = Keyboard.current;
        if (returnOnly)
        {
            if (keyboard != null && keyboard.uKey.wasPressedThisFrame && !TrainMindSession.TryReturn(gameObject.scene))
                LoadSceneIndex(entrySceneIndex >= 0 ? entrySceneIndex : returnSceneIndex);
            return;
        }
        if (keyboard == null)
        {
            ClearTarget();
            return;
        }

        var camera = playerCamera != null ? playerCamera : Camera.main;
        FindNPC(camera);
        if (currentPoint != null && keyboard.tKey.wasPressedThisFrame)
            StartCoroutine(MoveToNPC(camera, currentPoint));
    }

    private void FindNPC(Camera camera)
    {
        currentPoint = FindAvailablePoint(camera);
        if (hintText != null) hintText.gameObject.SetActive(currentPoint != null);
    }

    private FocusPoint FindAvailablePoint(Camera camera)
    {
        if (isMoving || LocationTransition.IsTransitioning || camera == null || !camera.isActiveAndEnabled)
            return null;

        var movement = camera.GetComponentInParent<FirstPersonPlayer>();
        var legacyMovement = camera.GetComponentInParent<PlayerMovement>();
        if ((movement != null && !movement.isActiveAndEnabled) ||
            (legacyMovement != null && !legacyMovement.isActiveAndEnabled)) return null;

        var player = camera.GetComponentInParent<CharacterController>();
        var collider = NpcDialogue.FindAimedCollider(camera, player != null ? player.transform : transform,
            interactionDistance, aimHits, out var npc);
        if (collider == null) return null;

        FocusPoint point;
        if (npc != null)
        {
            if (!npc.isActiveAndEnabled) return null;
            point = npc.GetComponentInChildren<FocusPoint>();
            if (point == null || point.GetComponentInParent<NpcDialogue>() != npc) return null;
        }
        else
        {
            point = collider.GetComponentInParent<FocusPoint>();
        }
        if (point == null || !point.isActiveAndEnabled) return null;

        var origin = SceneManager.GetActiveScene();
        bool available = origin.path == TrainMindSession.TrainPath
            ? TrainMindSession.CanBegin(origin, player, camera, point)
            : CanLoadSceneIndex(point.sceneIndex);
        return available ? point : null;
    }

    private void ClearTarget()
    {
        currentPoint = null;
        if (hintText != null) hintText.gameObject.SetActive(false);
    }

    private IEnumerator MoveToNPC(Camera camera, FocusPoint point)
    {
        if (camera == null || point == null) yield break;
        isMoving = true;
        ClearTarget();
        var player = camera.GetComponentInParent<CharacterController>();
        var origin = SceneManager.GetActiveScene();
        if (origin.path == TrainMindSession.TrainPath)
        {
            if (!TrainMindSession.TryBegin(this, origin, player, camera, point)) FinishTrainMindVisit();
            yield break;
        }

        entrySceneIndex = origin.buildIndex;
        if (player != null)
        {
            foreach (var behaviour in player.GetComponents<MonoBehaviour>())
                if (behaviour != this && !(behaviour is PlayerStress)) behaviour.enabled = false;
        }
        yield return AnimateToNPC(camera, point, moveDuration);
        if (point != null) LoadSceneIndex(point.sceneIndex);
    }

    private static IEnumerator AnimateToNPC(Camera camera, FocusPoint point, float moveDuration)
    {
        if (camera == null || point == null) yield break;

        var startPosition = camera.transform.position;
        var startRotation = camera.transform.rotation;
        var targetRotation = Quaternion.LookRotation(-point.transform.forward, Vector3.up);
        float duration = Mathf.Max(0.01f, moveDuration);
        for (float time = 0f; time < duration && camera != null && point != null; time += Time.deltaTime)
        {
            float t = Mathf.Clamp01(time / duration);
            camera.transform.SetPositionAndRotation(Vector3.Lerp(startPosition, point.transform.position, t),
                Quaternion.Slerp(startRotation, targetRotation, t));
            yield return null;
        }
        if (camera != null && point != null)
            camera.transform.SetPositionAndRotation(point.transform.position, targetRotation);
    }

    internal void FinishTrainMindVisit() => isMoving = false;

    internal static bool CanLoadSceneIndex(int index) =>
        index >= 0 && index < SceneManager.sceneCountInBuildSettings && Application.CanStreamedLevelBeLoaded(index);

    public static bool IsSceneAvailable(int index)
    {
        if (CanLoadSceneIndex(index)) return true;
        Debug.LogError($"Scene index {index} is absent from the build scene list.");
        return false;
    }
    public static void LoadSceneIndex(int index)
    {
        if (IsSceneAvailable(index)) SceneManager.LoadScene(index);
    }
}
