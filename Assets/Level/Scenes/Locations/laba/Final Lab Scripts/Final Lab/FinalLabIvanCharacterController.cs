using System;
using System.Collections;
using UnityEngine;

public sealed class FinalLabIvanCharacterController : MonoBehaviour
{
    public const string Walking = "Walking", CalmIdle = "CalmIdle", AngryArgument = "AngryArgument", SadIdle = "SadIdle", TurnAround = "TurnAround";
    public const string WalkRate = "WalkRate";
    private static readonly int WalkRateHash = Animator.StringToHash(WalkRate);
    private const float MinimumWalkRate = .30f;
    public const float ReturnStartRate = .41f;
    [SerializeField] private Animator animator;
    [SerializeField] private CharacterController motor;
    [SerializeField] private Transform head;
    [SerializeField] private float speed = 1.15f;
    [SerializeField] private AnimationCurve returnTurnYaw;
    private Renderer[] skin;
    private Vector3 glanceTarget;
    private float glanceWeight, glanceGoal;
    public const float ReturnWalkBlend = .36f;
    public Animator Animator => animator;
    public CharacterController Motor => motor;
    public Vector3 HeadPosition => head.position;
    public string RequestedAnimation { get; private set; }
    public bool Visible => skin != null && skin.Length > 0 && skin[0].enabled;

    public void Configure(Animator animation, CharacterController controller, Transform skull, AnimationCurve turnYaw)
    {
        animator = animation;
        motor = controller;
        head = skull;
        SetReturnTurnYaw(turnYaw);
    }

    public void SetReturnTurnYaw(AnimationCurve turnYaw) => returnTurnYaw = turnYaw;

    public float ReturnTurnAngle(float desiredYaw, float fraction)
    {
        float clipYaw = returnTurnYaw.Evaluate(1);
        // Let the motor finish aligning during the last part of the pose, ready for the first step.
        float tail = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.52f, .87f, fraction));
        float headingTime = Mathf.Clamp01(fraction + .13f * tail);
        return returnTurnYaw.Evaluate(headingTime) + Mathf.DeltaAngle(clipYaw, desiredYaw) * Mathf.SmoothStep(0, 1, headingTime);
    }

    public bool ReadyToWalk(float desiredYaw, float fraction) => fraction >= .82f &&
        Mathf.Abs(Mathf.DeltaAngle(ReturnTurnAngle(desiredYaw, fraction), desiredYaw)) < 3f;

    private void Awake()
    {
        skin = animator.GetComponentsInChildren<SkinnedMeshRenderer>();
        foreach (var renderer in skin) renderer.enabled = false;
        motor.enabled = false;
    }

    public void Reveal(Vector3 towards)
    {
        transform.rotation = Direction(towards);
        foreach (var renderer in skin) renderer.enabled = true;
        motor.enabled = true;
    }

    public void Play(string state, float blend = .22f)
    {
        RequestedAnimation = state;
        animator.CrossFadeInFixedTime("Base Layer." + state, blend, 0, 0f);
    }

    public IEnumerator BlendEmotion(string state)
    {
        float blend = state switch
        {
            SadIdle => .86f,
            CalmIdle => .78f,
            _ => .48f
        };
        Play(state, blend);
        for (float elapsed = 0; elapsed < blend + .65f; elapsed += Time.deltaTime)
        {
            if (!animator.IsInTransition(0) && animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer." + state)) yield break;
            yield return null;
        }
        throw new InvalidOperationException("Ivan's Animator did not reach " + state);
    }

    public Quaternion Direction(Vector3 towards)
    {
        Vector3 forward = Vector3.ProjectOnPlane(towards - transform.position, Vector3.up);
        return forward.sqrMagnitude > .0001f ? Quaternion.LookRotation(forward) : transform.rotation;
    }

    public static Vector3[] RoundedRoute(Vector3 start, Vector3 corner, Vector3 end)
    {
        const int curveSteps = 12;
        float trim = Mathf.Min(.36f, .45f * Mathf.Min(Vector3.Distance(start, corner), Vector3.Distance(end, corner)));
        var entry = Vector3.MoveTowards(corner, start, trim);
        var exit = Vector3.MoveTowards(corner, end, trim);
        var route = new Vector3[curveSteps + 3];
        route[0] = start;
        route[1] = entry;
        for (int i = 1; i <= curveSteps; i++)
        {
            float t = (float)i / curveSteps;
            route[i + 1] = Vector3.Lerp(Vector3.Lerp(entry, corner, t), Vector3.Lerp(corner, exit, t), t);
        }
        route[^1] = end;
        return route;
    }

    public static float AdvanceWalkingDistance(float walked, float routeLength, float speed, float deltaTime, float startingRate = MinimumWalkRate)
    {
        const float sampleInterval = 1f / 120f;
        while (deltaTime > 0 && walked < routeLength)
        {
            float sample = Mathf.Min(deltaTime, sampleInterval);
            float acceleration = Mathf.SmoothStep(startingRate, 1, walked / .29f);
            float braking = Mathf.SmoothStep(MinimumWalkRate, 1, (routeLength - walked) / .32f);
            float rate = Mathf.Min(acceleration, braking);
            walked = Mathf.Min(routeLength, walked + speed * rate * sample);
            deltaTime -= sample;
        }
        return walked;
    }

    public IEnumerator WalkAround(Vector3 corner, Vector3 destination, Action follow, bool returning = false)
    {
        Vector3 start = transform.position;
        start.y = destination.y;
        var route = RoundedRoute(start, corner, destination);
        float routeLength = 0;
        for (int i = 1; i < route.Length; i++) routeLength += Vector3.Distance(route[i - 1], route[i]);
        Vector3 cursor = start;
        int segment = 1;
        float walked = 0;
        float turnVelocity = 0;
        float blocked = 0;
        float startingRate = returning ? ReturnStartRate : MinimumWalkRate;
        animator.SetFloat(WalkRateHash, startingRate);
        if (RequestedAnimation != Walking) Play(Walking, .38f);
        while (true)
        {
            float dt = Time.deltaTime;
            if (dt <= 0)
            {
                yield return null;
                continue;
            }
            float nextDistance = AdvanceWalkingDistance(walked, routeLength, speed, dt, startingRate);
            float stride = nextDistance - walked;
            float rate = Mathf.Clamp(stride / (speed * dt), MinimumWalkRate, 1);
            animator.SetFloat(WalkRateHash, rate);
            if (routeLength - nextDistance < .18f && RequestedAnimation == Walking) Play(CalmIdle, .40f);
            Vector3 previousCursor = cursor;
            float remaining = stride;
            while (segment < route.Length)
            {
                float length = Vector3.Distance(cursor, route[segment]);
                cursor = Vector3.MoveTowards(cursor, route[segment], remaining);
                if (remaining < length) break;
                remaining -= length;
                segment++;
            }
            walked = nextDistance;

            if (walked >= routeLength)
            {
                cursor = destination;
                segment = route.Length;
            }

            Vector3 delta = Vector3.ProjectOnPlane(cursor - transform.position, Vector3.up);
            if (segment == route.Length && delta.sqrMagnitude < .000025f) yield break;
            Vector3 bearing = returning ? cursor - previousCursor : delta;
            if (bearing.sqrMagnitude > .000001f)
            {
                float heading = Mathf.Atan2(bearing.x, bearing.z) * Mathf.Rad2Deg;
                float yaw = Mathf.SmoothDampAngle(transform.eulerAngles.y, heading, ref turnVelocity, .12f, 135f, dt);
                transform.rotation = Quaternion.Euler(0, yaw, 0);
            }
            Vector3 previous = transform.position;
            Vector3 step = Vector3.ClampMagnitude(delta, speed * rate * dt);
            motor.Move(step + Vector3.down * 1.7f * dt);
            float moved = Vector3.ProjectOnPlane(transform.position - previous, Vector3.up).sqrMagnitude;
            blocked = moved < step.sqrMagnitude * .04f ? blocked + dt : 0;
            if (blocked > .75f || Vector3.ProjectOnPlane(cursor - transform.position, Vector3.up).sqrMagnitude > .12f * .12f)
                throw new InvalidOperationException("Ivan was blocked while walking towards " + destination);
            follow();
            yield return null;
        }
    }

    public IEnumerator TurnTo(Vector3 point, Action follow)
    {
        ClearGlance();
        Quaternion before = transform.rotation;
        Quaternion after = Direction(point);
        float desiredYaw = Vector3.SignedAngle(before * Vector3.forward, after * Vector3.forward, Vector3.up);
        Play(TurnAround, .22f);
        float normalized = 0;
        for (float elapsed = 0; elapsed < 2.5f; elapsed += Time.deltaTime)
        {
            var state = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
            if (state.IsName("Base Layer." + TurnAround)) normalized = Mathf.Clamp01(state.normalizedTime);
            transform.rotation = Quaternion.AngleAxis(ReturnTurnAngle(desiredYaw, normalized), Vector3.up) * before;
            if (ReadyToWalk(desiredYaw, normalized))
            {
                animator.SetFloat(WalkRateHash, ReturnStartRate);
                Play(Walking, ReturnWalkBlend);
                yield break;
            }
            follow();
            yield return null;
        }
        throw new InvalidOperationException("Ivan's turn clip did not reach the walking handoff.");
    }

    public void GlanceAt(Vector3 target)
    {
        glanceTarget = target;
        glanceGoal = 1;
    }

    public void ClearGlance() => glanceGoal = 0;

    private void LateUpdate()
    {
        if (!Visible) return;
        glanceWeight = Mathf.MoveTowards(glanceWeight, glanceGoal, 3.8f * Time.deltaTime);
        if (glanceWeight <= 0) return;
        var direction = Vector3.ProjectOnPlane(glanceTarget - head.position, Vector3.up);
        float yaw = Mathf.Clamp(Vector3.SignedAngle(transform.forward, direction, Vector3.up), -43, 43);
        head.rotation = Quaternion.AngleAxis(glanceWeight * yaw, Vector3.up) * head.rotation;
    }
}
