using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator), typeof(CharacterController))]
[RequireComponent(typeof(CapsuleCollider))]
public sealed class HostileTrainNpc : MonoBehaviour, IShootingTarget
{
    private enum State { Seated, Rising, Ready, Walking, Firing, Defeated }
    private enum Target { Hidden, Visible, BehindSeat }

    private static readonly int[] Animations =
    {
        Animator.StringToHash("Base Layer.Seated"), Animator.StringToHash("Base Layer.Rising"),
        Animator.StringToHash("Base Layer.Ready"), Animator.StringToHash("Base Layer.Walking"),
        Animator.StringToHash("Base Layer.Firing"), Animator.StringToHash("Base Layer.Defeated")
    };
    private const int DamagePerHit = 25;
    private const float StressPerHit = 10f;
    private const float ShotMoment = 0.06f;
    private const float MinShotInterval = 0.2f;
    private const float MaxShotInterval = 1f;
    private const float ApproachRestartBuffer = 0.2f;
    private const float WalkStartFraction = 0.30f;
    private const float WalkStopFraction = 0.10f;
    private const float MovementSmoothingTime = 0.14f;
    private const float LocomotionFadeTime = 0.13f;
    private static readonly int HitReaction = Animator.StringToHash("Hit reaction.Recoil");

    [Header("Carriage")]
    [SerializeField] private TrainCarriageZone carriage;
    [SerializeField] private Transform aisleEntry;
    [Tooltip("Vertical correction that fits the first frame of Stand Up to the seat cushion.")]
    [SerializeField] private float seatedHipOffset = -0.18f;
    [Tooltip("Extra forward travel near the end of Stand Up to clear the front of the seat.")]
    [SerializeField, Min(0f)] private float standExitOffset = 0.18f;
    [SerializeField, Min(0f)] private float approachDistance = 4f;
    [SerializeField, Min(0.1f)] private float walkSpeed = 1.3f;
    [SerializeField, Min(1f)] private float turnSpeed = 240f;

    [Header("Combat")]
    [SerializeField, Min(1)] private int maxHealth = 100;
    [SerializeField] private HostileNpcRagdoll ragdoll;
    [SerializeField] private Transform eyes;
    [SerializeField] private Transform muzzle;
    [SerializeField] private NpcMuzzleFlash muzzleFlash;
    [SerializeField, Min(0.1f)] private float sightRange = 19f;
    [SerializeField, Range(1f, 360f)] private float fieldOfView = 150f;
    [SerializeField, Min(0.1f)] private float shotRange = 16f;
    [SerializeField] private LayerMask solidLayers = ~0;
    [SerializeField] private AudioSource shotSource;
    [SerializeField] private AudioClip[] shotSounds = System.Array.Empty<AudioClip>();

    [Header("Voice")]
    [SerializeField] private AudioSource voiceSource;
    [SerializeField] private AudioClip[] voiceClips = System.Array.Empty<AudioClip>();
    [SerializeField, Min(0.1f)] private float voiceInterval = 7f;

    private readonly RaycastHit[] hits = new RaycastHit[16];
    private Animator animator;
    private CharacterController body;
    private CapsuleCollider seatedHitbox;
    private FirstPersonPlayer player;
    private PlayerStress stress;
    private State state;
    private bool playerWasInside;
    private bool onAisle;
    private bool approaching;
    private bool tracking;
    private bool hasFired;
    private bool shotPending;
    private bool reactingToHit;
    private Vector3 lookTarget;
    private int hitReactionLayer = -1;
    private float movementHeight;
    private float walkFraction;
    private float shotDelay;
    private float voiceDelay;
    private float appliedStandOffset;
    private AudioClip previousLine;
    private AudioClip previousShotSound;

    public int Health { get; private set; }

    private void Awake()
    {
        animator = GetComponent<Animator>();
        animator.keepAnimatorStateOnDisable = true;
        body = GetComponent<CharacterController>();
        seatedHitbox = GetComponent<CapsuleCollider>();
        movementHeight = transform.position.y;
        body.stepOffset = 0f;
        Health = maxHealth;
        voiceDelay = voiceInterval;
        body.enabled = false;
    }

    private void OnEnable()
    {
        playerWasInside = carriage != null && carriage.PlayerInside;
        if (carriage == null || carriage.Player == null) return;
        player = carriage.Player.GetComponent<FirstPersonPlayer>();
        stress = carriage.Player.GetComponent<PlayerStress>();
    }

    private void OnDisable()
    {
        muzzleFlash?.Stop();
        if (voiceSource != null) voiceSource.Stop();
        if (shotSource != null) shotSource.Stop();
    }

    private void Update()
    {
        if (state == State.Defeated || carriage == null || player == null) return;

        bool inside = carriage.PlayerInside;
        bool entered = inside && !playerWasInside;
        playerWasInside = inside;
        if (state == State.Seated)
        {
            if (entered && player.isActiveAndEnabled && !LocationTransition.IsTransitioning)
            {
                lookTarget = PlayerAimPoint();
                SetState(State.Rising, 0f);
            }
            return;
        }

        if (state == State.Rising)
        {
            if (!CurrentAnimation(out float progress) || progress < 1f) return;
            seatedHitbox.enabled = false;
            body.enabled = true;
            SetState(State.Ready);
        }

        if (!player.isActiveAndEnabled || LocationTransition.IsTransitioning)
        {
            approaching = false;
            tracking = false;
            shotPending = false;
            walkFraction = 0f;
            SetState(State.Ready);
            return;
        }

        UpdateVoice();
        shotDelay = Mathf.Max(0f, shotDelay - Time.deltaTime);
        Target target = AcquireTarget(inside, out Vector3 aimPoint);
        if (target != Target.Hidden) lookTarget = aimPoint;

        if (state == State.Firing)
        {
            TurnTowards(lookTarget);
            if (!CurrentAnimation(out float progress)) return;
            if (shotPending && progress >= ShotMoment)
            {
                shotPending = false;
                if (target != Target.Hidden && CanFireAt(aimPoint, target)) Fire(aimPoint, target);
                if (!isActiveAndEnabled) return;
            }
            if (progress < 1f && (shotPending || shotDelay > 0f || target == Target.Hidden)) return;
            SetState(State.Ready, 0.06f);
        }

        float approachThreshold = approachDistance + (approaching ? 0f : ApproachRestartBuffer);
        float travelFraction = 0f;
        approaching = target == Target.Visible && FlatDirection(aimPoint).sqrMagnitude > approachThreshold * approachThreshold &&
            TryApproach(aimPoint, out travelFraction);
        UpdateLocomotion(travelFraction);
        if (approaching) return;

        TurnTowards(lookTarget);
        if (state == State.Ready && target != Target.Hidden && shotDelay <= 0f && CanFireAt(aimPoint, target))
        {
            walkFraction = 0f;
            if (hasFired)
            {
                // A new shot can interrupt the previous clip's recovery.
                state = State.Firing;
                animator.Play(Animations[(int)State.Firing], 0, ShotMoment);
                Fire(aimPoint, target);
            }
            else
            {
                shotPending = true;
                SetState(State.Firing, 0.04f);
            }
        }
    }

    private Target AcquireTarget(bool inside, out Vector3 aimPoint)
    {
        aimPoint = Vector3.zero;
        if (inside && eyes != null)
        {
            Vector3 delta = PlayerAimPoint() - eyes.position;
            Vector3 flat = Vector3.ProjectOnPlane(delta, Vector3.up);
            // The turn towards the aisle can put an already tracked player to the side.
            bool followingToAisle = tracking && approaching && !onAisle;
            if (delta.sqrMagnitude <= sightRange * sightRange &&
                (followingToAisle || Vector3.Angle(transform.forward, flat) <= fieldOfView * 0.5f) &&
                ShotRaycast.TryGetFirstHit(new Ray(eyes.position, delta), delta.magnitude + 0.05f,
                    solidLayers, transform, hits, out RaycastHit hit))
            {
                if (IsPlayer(hit.collider))
                {
                    tracking = true;
                    aimPoint = PlayerAimPoint();
                    return Target.Visible;
                }
                if (tracking && hasFired && carriage.IsSeatCover(hit.collider))
                {
                    aimPoint = hit.point + delta.normalized * 0.025f;
                    return Target.BehindSeat;
                }
            }
        }
        tracking = false;
        return Target.Hidden;
    }

    private bool CanFireAt(Vector3 point, Target target)
    {
        if (muzzle == null) return false;
        Vector3 direction = point - muzzle.position;
        return direction.sqrMagnitude <= shotRange * shotRange &&
            Vector3.Angle(transform.forward, FlatDirection(point)) <= 12f &&
            (target != Target.BehindSeat || hasFired);
    }

    private void Fire(Vector3 point, Target target)
    {
        shotDelay = Random.Range(MinShotInterval, MaxShotInterval);
        hasFired = true;
        muzzleFlash?.Play();
        if (shotSource != null && shotSounds.Length > 0)
        {
            AudioClip clip = ChooseClip(shotSounds, previousShotSound);
            if (clip != null)
            {
                previousShotSound = clip;
                shotSource.PlayOneShot(clip);
            }
        }
        var ray = new Ray(muzzle.position, point - muzzle.position);
        if (ShotRaycast.TryGetFirstHit(ray, shotRange, solidLayers, transform, hits, out RaycastHit hit) &&
            target == Target.Visible && IsPlayer(hit.collider) && stress != null)
            stress.AddStress(StressPerHit);
    }

    private bool TryApproach(Vector3 point, out float travelFraction)
    {
        travelFraction = 0f;
        if (aisleEntry == null) return false;
        if (!onAisle && FlatDirection(aisleEntry.position).sqrMagnitude <= 0.025f)
            onAisle = true;

        Vector3 destination = aisleEntry.position;
        if (onAisle)
        {
            Vector3 axis = Vector3.ProjectOnPlane(aisleEntry.forward, Vector3.up).normalized;
            destination += axis * Vector3.Dot(point - aisleEntry.position, axis);
        }

        Vector3 direction = FlatDirection(destination);
        if (direction.sqrMagnitude < 0.001f) return false;
        TurnTowards(destination);
        if (Vector3.Angle(transform.forward, direction) > 50f) return true;
        Vector3 step = Vector3.ClampMagnitude(direction, walkSpeed * Time.deltaTime);
        if (step.sqrMagnitude == 0f || !carriage.Contains(transform.position + step)) return true;
        Vector3 before = transform.position;
        body.Move(step);
        Vector3 position = transform.position;
        if (position.y != movementHeight)
        {
            position.y = movementHeight;
            transform.position = position;
        }
        travelFraction = Mathf.Clamp01(Vector3.Dot(position - before, step) / step.sqrMagnitude);
        return true;
    }

    private void UpdateLocomotion(float travelFraction)
    {
        // Capsule contacts can consume individual steps at the seams between floor sections.
        float blend = 1f - Mathf.Exp(-Time.deltaTime / MovementSmoothingTime);
        walkFraction = Mathf.Lerp(walkFraction, travelFraction, blend);
        if (state == State.Ready && walkFraction >= WalkStartFraction)
            SetState(State.Walking);
        else if (state == State.Walking && walkFraction <= WalkStopFraction)
            SetState(State.Ready);
    }

    private void OnAnimatorMove()
    {
        if (state != State.Rising || !CurrentAnimation(out float progress)) return;
        // The sitting mesh starts over the cushion. A movement capsule would step up onto it.
        float offset = standExitOffset * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.92f, progress));
        transform.position += Vector3.ProjectOnPlane(animator.deltaPosition, Vector3.up) +
            transform.forward * (offset - appliedStandOffset);
        appliedStandOffset = offset;
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (layerIndex != 0 || (state != State.Seated && state != State.Rising)) return;
        float weight = 1f;
        if (state == State.Rising && CurrentAnimation(out float progress))
            weight -= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.08f, 0.43f, progress));
        if (weight <= 0f) return;

        Vector3 leftFoot = animator.GetIKPosition(AvatarIKGoal.LeftFoot);
        Vector3 rightFoot = animator.GetIKPosition(AvatarIKGoal.RightFoot);
        animator.bodyPosition += Vector3.up * (seatedHipOffset * weight);
        animator.SetIKPosition(AvatarIKGoal.LeftFoot, leftFoot);
        animator.SetIKPosition(AvatarIKGoal.RightFoot, rightFoot);
        animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, weight);
        animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, weight);
    }

    private Vector3 PlayerAimPoint() => carriage.Player.transform.TransformPoint(carriage.Player.center + Vector3.up * 0.16f);
    private bool IsPlayer(Collider collider) => collider.transform.IsChildOf(player.transform);
    private Vector3 FlatDirection(Vector3 point) => Vector3.ProjectOnPlane(point - transform.position, Vector3.up);

    private void TurnTowards(Vector3 point)
    {
        Vector3 direction = FlatDirection(point);
        if (direction.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation,
                Quaternion.LookRotation(direction), turnSpeed * Time.deltaTime);
    }

    private void SetState(State next, float fadeSeconds = LocomotionFadeTime)
    {
        if (next == state) return;
        state = next;
        if (fadeSeconds == 0f) animator.Play(Animations[(int)next], 0, 0f);
        else animator.CrossFadeInFixedTime(Animations[(int)next], fadeSeconds, 0, 0f);
    }

    private bool CurrentAnimation(out float progress)
    {
        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
        progress = info.normalizedTime;
        return info.fullPathHash == Animations[(int)state];
    }

    public void Hit()
    {
        if (state == State.Defeated) return;
        Health = Mathf.Max(0, Health - DamagePerHit);
        if (Health > 0)
        {
            if (hitReactionLayer < 0 && animator.isInitialized)
                hitReactionLayer = animator.GetLayerIndex("Hit reaction");
            if (hitReactionLayer >= 0)
            {
                reactingToHit = true;
                animator.SetLayerWeight(hitReactionLayer, 1f);
                animator.Play(HitReaction, hitReactionLayer, 0f);
            }
            return;
        }
        shotPending = false;
        if (hitReactionLayer >= 0) animator.SetLayerWeight(hitReactionLayer, 0f);
        reactingToHit = false;
        if (voiceSource != null) voiceSource.Stop();
        body.enabled = false;
        seatedHitbox.enabled = false;
        if (ragdoll == null)
        {
            SetState(State.Defeated);
            return;
        }

        state = State.Defeated;
        animator.enabled = false;
        ragdoll.BeginFall();
    }

    // The encounter checkpoint is taken immediately after activation, before the NPC starts rising.
    internal void RestoreEncounterStart(Vector3 position, Quaternion rotation, int health)
    {
        body.enabled = false;
        ragdoll?.RestoreAnimatedPose();
        transform.SetPositionAndRotation(position, rotation);
        movementHeight = position.y;
        Health = health;
        state = State.Seated;
        playerWasInside = carriage != null && carriage.PlayerInside;
        onAisle = approaching = tracking = hasFired = shotPending = reactingToHit = false;
        lookTarget = Vector3.zero;
        walkFraction = shotDelay = appliedStandOffset = 0f;
        voiceDelay = voiceInterval;
        previousLine = null;
        seatedHitbox.enabled = true;
        animator.enabled = true;
        animator.Rebind();
        animator.Play(Animations[(int)State.Seated], 0, 0f);
        if (animator.layerCount > 1) animator.SetLayerWeight(1, 0f);
        if (hitReactionLayer >= 0) animator.SetLayerWeight(hitReactionLayer, 0f);
        animator.Update(0f);
    }

    private void LateUpdate()
    {
        if (!animator.enabled) return;
        if (animator.isInitialized && animator.layerCount > 1)
        {
            float targetWeight = state == State.Walking ? 1f : 0f;
            float weight = Mathf.MoveTowards(animator.GetLayerWeight(1), targetWeight, Time.deltaTime / LocomotionFadeTime);
            animator.SetLayerWeight(1, weight);
        }
        if (!reactingToHit) return;
        float progress = animator.GetCurrentAnimatorStateInfo(hitReactionLayer).normalizedTime;
        animator.SetLayerWeight(hitReactionLayer,
            1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.70f, 1f, progress)));
        reactingToHit = progress < 1f;
    }

    private void UpdateVoice()
    {
        if (voiceSource == null || voiceClips.Length == 0 || voiceSource.isPlaying) return;
        voiceDelay -= Time.deltaTime;
        if (voiceDelay > 0f) return;
        voiceDelay = voiceInterval;

        AudioClip selected = ChooseClip(voiceClips, previousLine);
        if (selected == null) return;
        previousLine = selected;
        voiceSource.clip = selected;
        voiceSource.Play();
    }

    private static AudioClip ChooseClip(AudioClip[] clips, AudioClip previous)
    {
        AudioClip selected = null;
        AudioClip repeat = null;
        int candidates = 0;
        foreach (AudioClip clip in clips)
        {
            if (clip == null) continue;
            if (clip == previous)
            {
                repeat = clip;
                continue;
            }
            if (Random.Range(0, ++candidates) == 0) selected = clip;
        }
        return selected != null ? selected : repeat;
    }
}
