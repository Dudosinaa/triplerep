using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(-500)]
public sealed class TrainFinale : MonoBehaviour
{
    public const string ScenePath = "Assets/Level/Scenes/Locations/Train.unity";
    public const string Rise = "Rise", Angry = "Angry", Point = "Point", Dismiss = "Dismiss", Turn = "Turn";
    private const float VignetteSeconds = 2.65f;
    private static bool arrivingForFinale;

    [SerializeField] private GameObject[] ordinaryGameplay;
    [SerializeField] private Behaviour[] ordinaryControls;
    [SerializeField] private GameObject presentation;
    [SerializeField] private Camera view;
    [SerializeField] private Animator alexei;
    [SerializeField] private Transform seatedEye, standingEye;
    [SerializeField] private Vector3 standingOffset;
    [SerializeField] private FinalLabIvanDialogue subtitles;
    [SerializeField] private CanvasGroup curtain;
    [SerializeField] private Image vignette;
    [SerializeField] private TMP_Text title;

    internal static void LoadFromRescue()
    {
        if (!LocationTransition.TryGetBuildIndex(ScenePath, out int index))
        {
            Debug.LogError("The finale requires Train in the active build profile.");
            return;
        }
        arrivingForFinale = true;
        SceneManager.LoadSceneAsync(index);
    }

    private bool active;
    private bool alignAfterRise;
    private Vector3 riseOrigin;
    private Material closingMask;

    private void Awake()
    {
        if (!arrivingForFinale) return;
        arrivingForFinale = false;
        active = true;
        // Only the camera and its listener are needed. The player root would build the FPS weapon in Awake.
        view.transform.SetParent(transform, true);
        foreach (GameObject actor in ordinaryGameplay) actor.SetActive(false);
        foreach (Behaviour control in ordinaryControls) control.enabled = false;
        foreach (var weapon in view.GetComponentsInChildren<FirstPersonWeaponView>(true))
            weapon.gameObject.SetActive(false);
        presentation.SetActive(true);
        curtain.alpha = 1f;
        vignette.enabled = false;
        title.alpha = 0f;
        view.transform.SetPositionAndRotation(seatedEye.position, seatedEye.rotation);
    }

    private IEnumerator Start()
    {
        if (!active) yield break;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Transform head = alexei.GetBoneTransform(HumanBodyBones.Head);
        view.transform.SetPositionAndRotation(seatedEye.position, seatedEye.rotation);
        alexei.Play("Base Layer." + Rise, 0, 0f);
        alexei.Update(0f);
        alexei.speed = 0f;
        yield return FinalLabAlexeiRescue.Fade(curtain, 0f, 1.2f);
        alexei.speed = 1f;
        while (!Reached(Rise, 1f))
        {
            LookAt(seatedEye.position, head);
            yield return null;
        }
        riseOrigin = alexei.transform.position;
        alignAfterRise = true;
        Blend(Angry, .46f);
        const float standDuration = .45f;
        for (float t = 0; t < standDuration; t += Time.unscaledDeltaTime)
        {
            float lift = Mathf.SmoothStep(0f, 1f, t / standDuration);
            LookAt(Vector3.Lerp(seatedEye.position, standingEye.position, lift), head);
            yield return null;
        }
        LookAt(standingEye.position, head);
        while (alignAfterRise) yield return null;

        var lines = subtitles.ReadLines();
        for (int i = 0; i < lines.Length; i++)
        {
            subtitles.Show(i, lines[i]);
            if (i == 5)
            {
                Blend(Point, .33f);
                while (!Reached(Point, .93f))
                {
                    subtitles.ReadContinue();
                    LookAt(standingEye.position, head);
                    yield return null;
                }
                yield return BlendAndTrack(Angry, .38f, head, true);
            }
            while (!subtitles.ReadContinue())
            {
                LookAt(standingEye.position, head);
                yield return null;
            }
        }
        subtitles.Hide();
        Blend(Dismiss, .39f);
        while (!Reached(Dismiss, .89f)) yield return null;
        Blend(Turn, .24f);
        while (!Reached(Turn, 1f)) yield return null;
        alexei.speed = 0f;

        closingMask = new Material(vignette.material);
        vignette.material = closingMask;
        closingMask.SetFloat("_Closure", 0f);
        vignette.enabled = true;
        for (float t = 0; t < VignetteSeconds; t += Time.unscaledDeltaTime)
        {
            closingMask.SetFloat("_Closure", Mathf.SmoothStep(0f, 1f, t / VignetteSeconds));
            yield return null;
        }
        closingMask.SetFloat("_Closure", 1f);
        for (float t = 0; t < .8f; t += Time.unscaledDeltaTime)
        {
            title.alpha = Mathf.SmoothStep(0f, 1f, t / .8f);
            yield return null;
        }
        title.alpha = 1f;
    }

    private IEnumerator BlendAndTrack(string state, float length, Transform head, bool readContinue = false)
    {
        Blend(state, length);
        do
        {
            if (readContinue) subtitles.ReadContinue();
            LookAt(standingEye.position, head);
            yield return null;
        } while (!Reached(state, 0f));
    }

    private bool Reached(string state, float progress)
    {
        var current = alexei.GetCurrentAnimatorStateInfo(0);
        return !alexei.IsInTransition(0) && current.IsName("Base Layer." + state) && current.normalizedTime >= progress;
    }

    private void Blend(string state, float seconds) => alexei.CrossFadeInFixedTime("Base Layer." + state, seconds, 0, 0f);

    private void LateUpdate()
    {
        if (!alignAfterRise) return;
        if (alexei.IsInTransition(0) && alexei.GetNextAnimatorStateInfo(0).IsName("Base Layer." + Angry))
        {
            float weight = Mathf.Clamp01(alexei.GetAnimatorTransitionInfo(0).normalizedTime);
            alexei.transform.position = riseOrigin + standingOffset * weight;
        }
        else if (Reached(Angry, 0f))
        {
            alexei.transform.position = riseOrigin + standingOffset;
            alignAfterRise = false;
        }
    }

    private void LookAt(Vector3 eye, Transform head)
    {
        Quaternion aim = Quaternion.LookRotation(head.position + Vector3.up * .06f - eye);
        view.transform.SetPositionAndRotation(eye, Quaternion.Slerp(view.transform.rotation, aim,
            1f - Mathf.Exp(-8f * Time.unscaledDeltaTime)));
    }

    private void OnDestroy()
    {
        if (closingMask != null) Destroy(closingMask);
    }
}
