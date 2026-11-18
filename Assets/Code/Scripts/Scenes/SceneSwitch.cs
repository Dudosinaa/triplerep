using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public sealed class SceneSwitch : MonoBehaviour
{
    public float interactionDistance = 1.5f;
    public float moveDuration = 2f;
    public float hideDelay = 0.1f;
    public TMP_Text hintText;
    public bool returnOnly;
    [Min(0)] public int returnSceneIndex;
    private static int entrySceneIndex = -1;
    private bool isMoving;
    private FocusPoint currentPoint;
    private float hideTimer;

    private void Start() { if (hintText != null) hintText.gameObject.SetActive(false); }
    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (returnOnly)
        {
            if (keyboard.uKey.wasPressedThisFrame && !TrainMindSession.TryReturn(gameObject.scene))
                LoadSceneIndex(entrySceneIndex >= 0 ? entrySceneIndex : returnSceneIndex);
            return;
        }
        if (isMoving) return;
        var camera = Camera.main;
        var movement = camera != null ? camera.GetComponentInParent<FirstPersonPlayer>() : null;
        var legacyMovement = camera != null ? camera.GetComponentInParent<PlayerMovement>() : null;
        if ((movement != null && !movement.enabled) || (legacyMovement != null && !legacyMovement.enabled))
        {
            currentPoint = null;
            if (hintText != null) hintText.gameObject.SetActive(false);
            return;
        }
        FindNPC(camera);
        if (keyboard.tKey.wasPressedThisFrame && currentPoint != null && IsSceneAvailable(currentPoint.sceneIndex))
            StartCoroutine(MoveToNPC(camera, currentPoint));
    }
    private void FindNPC(Camera camera)
    {
        currentPoint = null;
        if (camera != null && Physics.Raycast(camera.transform.position, camera.transform.forward,
                out var hit, interactionDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            currentPoint = hit.collider.GetComponentInParent<FocusPoint>();
            var npc = hit.collider.GetComponentInParent<NpcDialogue>();
            if (currentPoint == null && npc != null) currentPoint = npc.GetComponentInChildren<FocusPoint>();
            if (currentPoint == null && hit.collider.transform.root.GetComponentInChildren<FirstPersonPlayer>() == null)
                currentPoint = hit.collider.transform.root.GetComponentInChildren<FocusPoint>();
        }
        if (hintText == null) return;
        if (currentPoint != null)
        {
            hideTimer = 0f;
            hintText.gameObject.SetActive(true);
        }
        else if (hintText.gameObject.activeSelf && (hideTimer += Time.deltaTime) >= hideDelay)
            hintText.gameObject.SetActive(false);
    }
    private IEnumerator MoveToNPC(Camera camera, FocusPoint point)
    {
        if (camera == null || point == null) yield break;
        isMoving = true;
        if (hintText != null) hintText.gameObject.SetActive(false);
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

    internal static IEnumerator AnimateToNPC(Camera camera, FocusPoint point, float moveDuration,
        PlayerStress calmingStress = null)
    {
        var startPosition = camera.transform.position;
        var startRotation = camera.transform.rotation;
        var targetRotation = Quaternion.LookRotation(-point.transform.forward, Vector3.up);
        float duration = Mathf.Max(0.01f, moveDuration);
        for (float time = 0f; time < duration && camera != null && point != null; time += Time.deltaTime)
        {
            float t = Mathf.Clamp01(time / duration);
            camera.transform.SetPositionAndRotation(Vector3.Lerp(startPosition, point.transform.position, t),
                Quaternion.Slerp(startRotation, targetRotation, t));
            if (calmingStress != null) calmingStress.SetMindCalmProgress(t);
            yield return null;
        }
        if (calmingStress != null) calmingStress.SetMindCalmProgress(1f);
    }

    internal void FinishTrainMindVisit() => isMoving = false;
    public static bool IsSceneAvailable(int index)
    {
        if (index >= 0 && index < SceneManager.sceneCountInBuildSettings) return true;
        Debug.LogError($"Scene index {index} is absent from the build scene list.");
        return false;
    }
    public static void LoadSceneIndex(int index)
    {
        if (IsSceneAvailable(index)) SceneManager.LoadScene(index);
    }
}
