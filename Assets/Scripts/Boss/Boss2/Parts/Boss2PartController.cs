using System;
using System.Collections;
using UnityEngine;

public enum Boss2PartMotion { FollowingAnchor, FollowingTarget, Moving, HoldingWorldPosition, Stopped }

/// <summary>Shared hand/ring movement. Animation belongs on a separate Visual child when added.</summary>
[DisallowMultipleComponent]
[ExecuteAlways]
public sealed class Boss2PartController : MonoBehaviour
{
    [SerializeField] private Boss2PartId partId;
    [SerializeField] private Transform idleAnchor;
    [SerializeField] private Boss2PartAim aim;
    [SerializeField] private Boss2MovementPose movementPose;

    private Boss2PartMotion motion = Boss2PartMotion.FollowingAnchor;
    private Transform moveAnchor;
    private Vector3 moveOffset;
    private Vector3 moveStart;
    private Vector3 worldTarget;
    private Vector3 heldWorldPosition;
    private float moveDuration;
    private float moveElapsed;
    private bool followAfterMove;
    private bool movingToAnchor;
    private bool easeReturn;
    private AnimationCurve returnProgressCurve;

    public event Action<Boss2PartController> MotionCompleted;
    public Boss2PartId PartId => partId;
    public Transform IdleAnchor => idleAnchor;
    public Vector3 IdlePosition
    {
        get
        {
            if (idleAnchor == null) throw new InvalidOperationException("Assign the part's idle anchor.");
            return movementPose != null ? movementPose.GetPosition(idleAnchor.position) : idleAnchor.position;
        }
    }
    public Boss2PartAim Aim => aim;
    public Boss2PartMotion CurrentMotion => motion;
    public bool IsMoving => motion == Boss2PartMotion.Moving;
    public bool IsReady => isActiveAndEnabled && !IsMoving;

    private void OnEnable()
    {
        heldWorldPosition = transform.position;
    }

    private void OnDisable() => StopMovement();

    private void LateUpdate()
    {
        // Keep the temporary sprites visible at their edited anchors outside Play Mode too.
        if (!Application.IsPlaying(gameObject))
        {
            if (idleAnchor != null) transform.position = idleAnchor.position;
            return;
        }
        Tick(Time.deltaTime);
    }

    private void Tick(float deltaTime)
    {
        if (motion == Boss2PartMotion.Stopped) return;
        if (motion == Boss2PartMotion.FollowingAnchor)
        {
            if (idleAnchor != null) transform.position = IdlePosition;
            return;
        }
        if (motion == Boss2PartMotion.HoldingWorldPosition)
        {
            transform.position = heldWorldPosition;
            return;
        }
        if (motion == Boss2PartMotion.FollowingTarget)
        {
            if (moveAnchor == null) StopMovement();
            else transform.position = moveAnchor.TransformPoint(moveOffset);
            return;
        }
        if (moveAnchor == null && (followAfterMove || movingToAnchor))
        {
            StopMovement();
            return;
        }
        if (!HitStopManager.IsActive) moveElapsed += deltaTime;
        Vector3 target = followAfterMove ? IdlePosition :
            (moveAnchor != null ? moveAnchor.TransformPoint(moveOffset) : worldTarget);
        float progress = moveDuration <= 0f ? 1f : Mathf.Clamp01(moveElapsed / moveDuration);
        float movementProgress = easeReturn ? 1f - (1f - progress) * (1f - progress) : progress;
        if (returnProgressCurve != null)
            movementProgress = progress >= 1f ? 1f : Mathf.Clamp01(returnProgressCurve.Evaluate(progress));
        transform.position = Vector3.Lerp(moveStart, target, movementProgress);
        if (followAfterMove && aim != null) aim.UpdateReturnToIdle(movementProgress);
        if (progress < 1f) return;
        heldWorldPosition = target;
        motion = followAfterMove ? Boss2PartMotion.FollowingAnchor :
            (moveAnchor != null ? Boss2PartMotion.FollowingTarget : Boss2PartMotion.HoldingWorldPosition);
        if (motion != Boss2PartMotion.FollowingTarget) moveAnchor = null;
        MotionCompleted?.Invoke(this);
    }

    public void MoveToWorld(Vector3 destination, float duration)
    {
        BeginMove(null, destination, Vector3.zero, duration, false);
    }

    public void MoveToAnchor(Transform anchor, Vector3 localOffset, float duration)
    {
        if (anchor == null) throw new ArgumentNullException(nameof(anchor));
        BeginMove(anchor, Vector3.zero, localOffset, duration, false);
    }

    public void ReturnToIdle(float duration)
    {
        ReturnToIdle(duration, false);
    }

    public void ReturnToIdle(float duration, bool easeOut)
    {
        if (idleAnchor == null) throw new InvalidOperationException("Assign the part's idle anchor before returning.");
        if (aim != null) aim.BeginReturnToIdle();
        BeginMove(idleAnchor, Vector3.zero, Vector3.zero, duration, true);
        easeReturn = easeOut;
    }

    public void ReturnToIdle(float duration, AnimationCurve speedCurve)
    {
        AnimationCurve progressCurve = IntegrateReturnSpeed(speedCurve);
        ReturnToIdle(duration);
        returnProgressCurve = progressCurve;
    }

    private static AnimationCurve IntegrateReturnSpeed(AnimationCurve speedCurve)
    {
        if (speedCurve == null || speedCurve.length == 0)
            throw new ArgumentException("Assign a return speed curve.", nameof(speedCurve));

        const int samples = 64;
        var keys = new Keyframe[samples + 1];
        float area = 0f;
        float previousSpeed = Mathf.Max(0f, speedCurve.Evaluate(0f));
        keys[0] = new Keyframe(0f, 0f, previousSpeed, previousSpeed);
        for (int i = 1; i <= samples; i++)
        {
            float time = (float)i / samples;
            float speed = Mathf.Max(0f, speedCurve.Evaluate(time));
            area += (previousSpeed + speed) * 0.5f / samples;
            keys[i] = new Keyframe(time, area, speed, speed);
            previousSpeed = speed;
        }
        if (area <= 0f)
            throw new ArgumentException("The return speed curve must contain positive speed.", nameof(speedCurve));

        // Normalize accumulated speed so Return Speed still determines the full travel time.
        for (int i = 0; i <= samples; i++)
        {
            keys[i].value /= area;
            keys[i].inTangent /= area;
            keys[i].outTangent /= area;
        }
        return new AnimationCurve(keys);
    }

    public void FollowIdle()
    {
        moveAnchor = null;
        motion = Boss2PartMotion.FollowingAnchor;
        if (aim != null) aim.ResetAim();
    }

    public void StopMovement()
    {
        moveAnchor = null;
        heldWorldPosition = transform.position;
        motion = Boss2PartMotion.Stopped;
    }

    public IEnumerator WaitForMotion()
    {
        while (IsMoving && isActiveAndEnabled) yield return null;
        if (!isActiveAndEnabled || motion == Boss2PartMotion.Stopped)
            throw new InvalidOperationException("The part's movement was interrupted.");
    }

    private void BeginMove(Transform anchor, Vector3 destination, Vector3 offset, float duration, bool follow)
    {
        moveStart = transform.position;
        moveAnchor = anchor;
        movingToAnchor = anchor != null;
        worldTarget = destination;
        moveOffset = offset;
        moveDuration = Mathf.Max(0f, duration);
        moveElapsed = 0f;
        followAfterMove = follow;
        easeReturn = false;
        returnProgressCurve = null;
        motion = Boss2PartMotion.Moving;
    }
}
