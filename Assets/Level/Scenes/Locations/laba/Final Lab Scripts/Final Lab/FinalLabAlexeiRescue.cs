using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class FinalLabAlexeiRescue : MonoBehaviour
{
    [SerializeField] private FirstPersonPlayer player;
    [SerializeField] private Camera view;
    [SerializeField] private Animator alexei;
    [SerializeField] private Transform spawn;
    [SerializeField] private Transform resonator;
    [SerializeField] private Transform roomThreshold;
    [SerializeField] private Renderer[] outline;
    [SerializeField] private TMP_Text objective;
    [SerializeField] private TMP_Text prompt;

    internal void Begin(FatherCutsceneController father) => StartCoroutine(Rescue(father));

    private IEnumerator Rescue(FatherCutsceneController father)
    {
        var curtain = father.ReleaseViewForRescue();
        var subtitles = father.Subtitles;
        alexei.gameObject.SetActive(true);
        alexei.Play("Base Layer.Unconscious", 0, 1f);
        alexei.Update(0);
        alexei.speed = 0;
        Transform head = alexei.GetBoneTransform(HumanBodyBones.Head);

        player.Teleport(spawn.position);
        view.transform.localPosition = new Vector3(0f, 1.65f, 0f);
        player.SetViewDirection(resonator.position - view.transform.position);
        player.enabled = true;
        objective.gameObject.SetActive(true);
        yield return Fade(curtain, 0f, 1.25f);

        Vector3 target = head.position;
        while ((view.transform.position - target).sqrMagnitude > 2.3f * 2.3f ||
               Vector3.Dot(view.transform.position - roomThreshold.position, roomThreshold.forward) < .15f ||
               Physics.Linecast(view.transform.position, target, ~0, QueryTriggerInteraction.Ignore))
            yield return null;

        player.enabled = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Vector3 eye = view.transform.position;
        yield return MoveCamera(eye, target, .42f);

        subtitles.Show(0, "ИВАН", "Нет, Алёша... Только не ты. Я уже потерял отца. Тебя я им не отдам");
        while (!subtitles.ReadContinue()) yield return null;
        subtitles.Show(1, "ИВАН", "Прости меня, брат. За всё, что я натворил... Но сейчас я тебя вытащу. Обещаю");
        while (!subtitles.ReadContinue()) yield return null;
        subtitles.Hide();
        foreach (var edge in outline) edge.enabled = false;

        float lowerEye = Mathf.Min(eye.y, Mathf.Max(spawn.position.y + .68f, target.y + .18f));
        Vector3 low = new Vector3(eye.x, lowerEye, eye.z);
        if (Physics.SphereCast(eye, .14f, Vector3.down, out RaycastHit obstacle,
                eye.y - low.y, ~0, QueryTriggerInteraction.Ignore))
            low.y = eye.y - Mathf.Max(0f, obstacle.distance - .04f);
        yield return MoveCamera(low, target, .85f);

        prompt.gameObject.SetActive(true);
        yield return null;
        while (Keyboard.current == null || !Keyboard.current.eKey.wasPressedThisFrame) yield return null;
        prompt.gameObject.SetActive(false);
        objective.gameObject.SetActive(false);
        Vector3 forward = (target - low).normalized;
        float reach = Physics.SphereCast(low, .14f, forward, out obstacle, .32f, ~0, QueryTriggerInteraction.Ignore)
            ? Mathf.Max(0f, obstacle.distance - .04f) : .32f;
        yield return MoveCamera(low + forward * reach, target, .45f);
        yield return Fade(curtain, 1f, 1.1f);
        yield return null;
        TrainFinale.LoadFromRescue();
    }

    private IEnumerator MoveCamera(Vector3 targetEye, Vector3 focus, float duration)
    {
        var cameraTransform = view.transform;
        Vector3 start = cameraTransform.position;
        Quaternion rotation = cameraTransform.rotation;
        for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
        {
            float progress = Mathf.SmoothStep(0f, 1f, t / duration);
            Vector3 eye = Vector3.Lerp(start, targetEye, progress);
            cameraTransform.SetPositionAndRotation(eye, Quaternion.Slerp(rotation,
                Quaternion.LookRotation(focus - eye), progress));
            yield return null;
        }
        cameraTransform.SetPositionAndRotation(targetEye, Quaternion.LookRotation(focus - targetEye));
    }

    internal static IEnumerator Fade(CanvasGroup curtain, float end, float duration)
    {
        float start = curtain.alpha;
        for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
        {
            curtain.alpha = Mathf.SmoothStep(start, end, t / duration);
            yield return null;
        }
        curtain.alpha = end;
    }
}
