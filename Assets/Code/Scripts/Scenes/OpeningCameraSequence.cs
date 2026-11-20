using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class OpeningCameraSequence : MonoBehaviour
{
    [Serializable]
    private sealed class Shot
    {
        public Camera camera;
        [Min(0f)] public float holdSeconds = 2f;
    }

    [SerializeField] private List<Shot> shots = new List<Shot>();
    [SerializeField, Min(0f)] private float moveSeconds = 3f;
    [SerializeField] private Transform target;
    [SerializeField] private GameObject deactivateOnArrival;
    [SerializeField] private GameObject player;
    [SerializeField, Tooltip("Optional location bootstrap to start after the player becomes active.")]
    private MergedGameBootstrap bootstrap;

    private bool resumeBootstrap;

    private void Awake()
    {
        if (bootstrap != null && bootstrap.enabled)
        {
            resumeBootstrap = true;
            bootstrap.enabled = false;
        }

        if (player != null)
            player.SetActive(false);

        foreach (var shot in shots)
        {
            if (shot?.camera != null)
                SetCameraEnabled(shot.camera, false);
        }
    }

    private void Start()
    {
        StartCoroutine(PlaySequence());
    }

    private IEnumerator PlaySequence()
    {
        Camera lastCamera = null;
        foreach (var shot in shots)
        {
            if (shot?.camera == null)
                continue;

            if (lastCamera != null)
                SetCameraEnabled(lastCamera, false);

            lastCamera = shot.camera;
            SetCameraEnabled(lastCamera, true);

            if (shot.holdSeconds > 0f)
                yield return new WaitForSecondsRealtime(shot.holdSeconds);
        }

        if (lastCamera != null)
        {
            if (target != null)
            {
                yield return MoveToTarget(lastCamera.transform);

                if (deactivateOnArrival != null)
                    deactivateOnArrival.SetActive(false);
            }

            SetCameraEnabled(lastCamera, false);
        }

        if (player != null)
            player.SetActive(true);

        if (resumeBootstrap && bootstrap != null)
            bootstrap.enabled = true;
    }

    private IEnumerator MoveToTarget(Transform cameraTransform)
    {
        Vector3 startPosition = cameraTransform.position;
        Quaternion startRotation = cameraTransform.rotation;
        Vector3 endPosition = target.position;
        Quaternion endRotation = target.rotation;

        for (float elapsed = 0f; elapsed < moveSeconds;)
        {
            yield return null;

            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, elapsed / moveSeconds);
            cameraTransform.SetPositionAndRotation(
                Vector3.Lerp(startPosition, endPosition, progress),
                Quaternion.Slerp(startRotation, endRotation, progress));
        }

        cameraTransform.SetPositionAndRotation(endPosition, endRotation);
    }

    private static void SetCameraEnabled(Camera camera, bool enabled)
    {
        if (enabled && !camera.gameObject.activeSelf)
            camera.gameObject.SetActive(true);

        camera.enabled = enabled;
        if (camera.TryGetComponent<AudioListener>(out var listener))
            listener.enabled = enabled;
    }
}
