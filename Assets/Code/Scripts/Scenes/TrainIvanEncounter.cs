using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController), typeof(NpcDialogue))]
public sealed class TrainIvanEncounter : MonoBehaviour
{
    private static readonly int IsRunning = Animator.StringToHash("IsRunning");
    private const float RunningClipSpeed = 5.9f;
    private const float ArrivalDistance = 0.05f;

    [SerializeField] private Animator animator;
    [SerializeField] private Transform[] route = System.Array.Empty<Transform>();
    [SerializeField] private Transform destination;
    [SerializeField] private TrainCarriageZone continuationCarriage;
    [SerializeField] private Transform continuationDestination;
    [SerializeField] private GameObject[] activateOnContinuation = System.Array.Empty<GameObject>();
    [SerializeField] private TrainInterCarDoor[] routeDoors = System.Array.Empty<TrainInterCarDoor>();
    [SerializeField] private TrainHostileEncounter encounter;
    [SerializeField] private GameObject[] hideAfterDialogue = System.Array.Empty<GameObject>();
    [SerializeField, Min(0.1f)] private float runSpeed = 4.6f;
    [SerializeField] private Text chaseObjective;
    [SerializeField] private Text escapeHint;
    [SerializeField] private AudioClip escapeVoice;
    [SerializeField] private AudioClip nextCarriageVoice;

    private NpcDialogue dialogue;
    private CharacterController body;
    private bool departed;
    private bool escaping;
    private bool escapeStarted;
    private bool waitingForPlayer;
    private bool runningContinuation;
    private bool continuationStarted;
    private FirstPersonPlayer chasePlayer;
    private CharacterController chasePlayerBody;
    private int nextWaypoint;
    private float floorHeight;
    private float animatedSpeed;
    private Vector3 dialoguePosition;
    private Quaternion dialogueRotation;
    private FirstPersonPlayer dialoguePlayer;
    private float lookAtTime;
    private Vector3 retreatPoint;
    private bool retreating;
    private DialoguePanel escapeSubtitle;
    private AudioSource alexeyVoice;
    private double objectiveUntil;
    private double subtitleUntil;
    private double hintUntil;

    internal bool CanOpenDoors => escaping && isActiveAndEnabled;
    internal bool HasReachedExit { get; private set; }

    private void Awake()
    {
        dialogue = GetComponent<NpcDialogue>();
        body = GetComponent<CharacterController>();
        floorHeight = transform.position.y;
        dialoguePosition = transform.position;
        dialogueRotation = transform.rotation;
        foreach (var door in routeDoors)
            if (door != null) door.SetNpcApproach(this);
    }

    private void OnEnable()
    {
        dialogue.ConversationCompleted += BeginEscape;
        dialogue.ConversationCancelled += ResetDialogueStaging;
        dialogue.ScriptedCue += StageDialogue;
        if (encounter != null) encounter.PistolUnlocked += OnPistolUnlocked;
    }

    private void OnDisable()
    {
        SetPlayerCollisionIgnored(false);
        if (escapeSubtitle != null) escapeSubtitle.Hide();
        if (alexeyVoice != null) alexeyVoice.Stop();
        dialogue.ConversationCompleted -= BeginEscape;
        dialogue.ConversationCancelled -= ResetDialogueStaging;
        dialogue.ScriptedCue -= StageDialogue;
        if (encounter != null) encounter.PistolUnlocked -= OnPistolUnlocked;
    }

    private void StageDialogue(FirstPersonPlayer player, string cue)
    {
        dialoguePlayer = player;
        if (cue == "LookAtPlayer")
            lookAtTime = Time.time + 0.25f;
        else if (cue == "BackAway" && route.Length > 0)
        {
            retreatPoint = Vector3.MoveTowards(transform.position, route[0].position, 0.38f);
            lookAtTime = Time.time;
            retreating = true;
        }
    }

    private void ResetDialogueStaging()
    {
        if (dialoguePlayer == null) return;
        dialoguePlayer = null;
        retreating = false;
        body.enabled = false;
        transform.SetPositionAndRotation(dialoguePosition, dialogueRotation);
        body.enabled = true;
    }

    private void BeginEscape(FirstPersonPlayer player)
    {
        if (departed || destination == null) return;
        departed = true;
        chasePlayer = player;
        chasePlayerBody = player != null ? player.GetComponent<CharacterController>() : null;
        dialoguePlayer = null;
        retreating = false;
        dialogue.enabled = false;
        escaping = true;
        foreach (var npc in hideAfterDialogue)
            if (npc != null) npc.SetActive(false);
    }

    private void Update()
    {
        if (escapeStarted)
        {
            double now = Time.timeAsDouble;
            HideExpired(chaseObjective, objectiveUntil, now);
            if (escapeSubtitle != null && now >= subtitleUntil) escapeSubtitle.Hide();
            HideExpired(escapeHint, hintUntil, now);
        }
        if (dialoguePlayer != null && Time.time >= lookAtTime)
        {
            Face(PlanarOffset(dialoguePlayer.transform.position), 260f);
            if (retreating)
            {
                Vector3 remaining = PlanarOffset(retreatPoint);
                if (remaining.sqrMagnitude > 0.001f)
                    MoveOnDeck(Vector3.ClampMagnitude(remaining, Time.deltaTime));
                else
                    retreating = false;
            }
        }
        if (Time.deltaTime <= 0f || (!escaping && !waitingForPlayer)) return;

        // In the narrow aisle a player who arrives first can stand directly on Ivan's route.
        SetPlayerCollisionIgnored(true);
        if (waitingForPlayer)
        {
            if (chasePlayer != null)
                Face(PlanarOffset(chasePlayer.transform.position), 600f);
            if (!continuationCarriage.PlayerInside || chasePlayer == null || !chasePlayer.isActiveAndEnabled)
                return;

            waitingForPlayer = false;
            runningContinuation = true;
            escaping = true;
        }
        bool finalSegment = nextWaypoint == route.Length;
        Transform target = runningContinuation ? continuationDestination :
            finalSegment ? destination : route[nextWaypoint];
        Vector3 offset = PlanarOffset(target.position);
        if (offset.sqrMagnitude <= ArrivalDistance * ArrivalDistance)
        {
            if (!finalSegment)
            {
                nextWaypoint++;
                return;
            }

            escaping = false;
            animatedSpeed = 0f;
            animator.SetBool(IsRunning, false);
            animator.speed = 1f;
            if (!runningContinuation && continuationDestination != null && continuationCarriage != null)
            {
                waitingForPlayer = true;
                if (chasePlayer != null)
                {
                    Vector3 towardPlayer = PlanarOffset(chasePlayer.transform.position);
                    if (towardPlayer.sqrMagnitude > 0.001f)
                        transform.rotation = Quaternion.LookRotation(towardPlayer);
                }
            }
            else
            {
                HasReachedExit = true;
                SetPlayerCollisionIgnored(false);
            }
            return;
        }

        Face(offset, 600f);
        Vector3 before = transform.position;
        MoveOnDeck(Vector3.ClampMagnitude(offset, runSpeed * Time.deltaTime));
        float speed = PlanarOffset(before).magnitude / Time.deltaTime;
        animatedSpeed = Mathf.Lerp(animatedSpeed, speed, 1f - Mathf.Exp(-Time.deltaTime / 0.08f));
        bool moving = animatedSpeed > 0.45f;
        animator.SetBool(IsRunning, moving);
        animator.speed = moving ? Mathf.Clamp(animatedSpeed / RunningClipSpeed, 0.45f, 1.3f) : 1f;
        if (runningContinuation && moving && !continuationStarted)
        {
            continuationStarted = true;
            foreach (var targetObject in activateOnContinuation)
                if (targetObject != null) targetObject.SetActive(true);
        }
        if (moving && !escapeStarted) ConfirmEscapeStarted();
    }

    private void ConfirmEscapeStarted()
    {
        escapeStarted = true;
        encounter?.EnableHostileNpcs();
        objectiveUntil = ShowFeedback(chaseObjective, "догоните Ивана", 3d);
        hintUntil = ShowFeedback(escapeHint, "Shift — бежать", 6d);
        ShowAlexeyLine("Ваня! СТОЙ, СУКА!", escapeVoice);
    }

    private void OnPistolUnlocked()
    {
        if (!escapeStarted) return;
        hintUntil = ShowFeedback(escapeHint, "Q — достать пистолет", 6d);
        ShowAlexeyLine("Твою ж мать!", nextCarriageVoice);
    }

    private void ShowAlexeyLine(string text, AudioClip voice)
    {
        if (escapeSubtitle == null) escapeSubtitle = DialoguePanel.Create(transform);
        escapeSubtitle.Show("Алексей", text);
        subtitleUntil = Time.timeAsDouble + 3d;
        if (alexeyVoice != null) alexeyVoice.Stop();
        if (voice == null) return;
        if (alexeyVoice == null) alexeyVoice = gameObject.AddComponent<AudioSource>();
        alexeyVoice.clip = voice;
        alexeyVoice.Play();
    }

    private static double ShowFeedback(Text label, string text, double duration)
    {
        if (label != null)
        {
            label.text = text;
            label.gameObject.SetActive(true);
        }
        return Time.timeAsDouble + duration;
    }

    private static void HideExpired(Text label, double until, double now)
    {
        if (now >= until && label != null && label.gameObject.activeSelf)
            label.gameObject.SetActive(false);
    }

    private Vector3 PlanarOffset(Vector3 target) => Vector3.ProjectOnPlane(target - transform.position, Vector3.up);

    private void Face(Vector3 offset, float turnSpeed)
    {
        if (offset.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(offset),
                turnSpeed * Time.deltaTime);
    }

    private void SetPlayerCollisionIgnored(bool ignored)
    {
        if (chasePlayerBody == null || !chasePlayerBody.enabled || !chasePlayerBody.gameObject.activeInHierarchy ||
            body == null || !body.enabled || !gameObject.activeInHierarchy)
            return;

        if (Physics.GetIgnoreCollision(body, chasePlayerBody) != ignored)
            Physics.IgnoreCollision(body, chasePlayerBody, ignored);
    }

    private void MoveOnDeck(Vector3 step)
    {
        body.Move(step);
        // The authored route follows a level deck, including very short seams between floor colliders.
        Vector3 position = transform.position;
        if (position.y != floorHeight)
        {
            position.y = floorHeight;
            transform.position = position;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 from = transform.position;
        foreach (Transform waypoint in route)
        {
            if (waypoint == null) continue;
            Gizmos.DrawLine(from, waypoint.position);
            from = waypoint.position;
        }
        if (destination != null) Gizmos.DrawLine(from, destination.position);
        if (destination != null && continuationDestination != null)
            Gizmos.DrawLine(destination.position, continuationDestination.position);
    }
}
