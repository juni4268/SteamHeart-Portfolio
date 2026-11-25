using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum Boss1HandSide
{
    Left,
    Right
}

public enum Boss1HandMotion
{
    Idle,
    Moving,
    Tracking,
    Stopped
}

public enum Boss1HandTrackingAxis
{
    Horizontal,
    Vertical,
    Both
}

[DisallowMultipleComponent]
[RequireComponent(typeof(Boss1HandAnimationController))]
public class Boss1HandController : MonoBehaviour
{
    [Header("Hand")]
    [SerializeField] private Boss1HandSide handSide;

    [Header("Animation")]
    [SerializeField] private Boss1HandAnimationController animationController;

    [Header("Runtime State")]
    [SerializeField] private Boss1HandMotion currentMotion = Boss1HandMotion.Idle;

    private Vector3 idleLocalPosition;
    private Quaternion idleLocalRotation;
    private Coroutine movementCoroutine;
    private Transform trackingTarget;
    private Boss1HandTrackingAxis trackingAxis;
    private Vector2 trackingOffset;
    private float trackingSpeed;
    private int motionVersion;
    private Collider2D trackingCollider;
    private Boss1HandDamageHitbox trackingHitbox;
    private bool deathMotionActive;
    private Vector3 deathStartPosition;
    private float deathTopOffset;
    private float deathShakeStartTime;
    private ContactFilter2D trackingWallFilter;
    private readonly List<RaycastHit2D> trackingWallHits = new List<RaycastHit2D>();

    public event Action<Boss1HandController> MotionCompleted;
    public event Action<Vector2> BeforeMove;

    public Boss1HandSide HandSide => handSide;
    public Boss1HandAnimationController AnimationController => animationController;
    public Boss1HandMotion CurrentMotion => currentMotion;
    public bool IsBusy => currentMotion == Boss1HandMotion.Moving
        || currentMotion == Boss1HandMotion.Tracking;

    private void Awake()
    {
        if (animationController == null) animationController = GetComponent<Boss1HandAnimationController>();
        // RequireComponent does not retrofit dependencies onto hands already saved in a Scene.
        if (animationController == null) animationController = gameObject.AddComponent<Boss1HandAnimationController>();
        idleLocalPosition = transform.localPosition;
        idleLocalRotation = transform.localRotation;
        currentMotion = Boss1HandMotion.Idle;
    }

    private void Update()
    {
        if (currentMotion == Boss1HandMotion.Tracking)
        {
            UpdateTracking();
        }
    }

    private void OnDisable()
    {
        ResetState();
    }

    public void MoveTo(Vector2 worldPosition, float duration, AnimationCurve movementCurve = null)
    {
        CancelActiveMotion(Boss1HandMotion.Moving);

        if (duration <= 0f)
        {
            if (SetWorldPosition(worldPosition)) CompleteMotion(Boss1HandMotion.Stopped);
            return;
        }

        int version = motionVersion;
        movementCoroutine = StartCoroutine(MoveWorldRoutine(worldPosition, duration, version, movementCurve));
        if (version != motionVersion) movementCoroutine = null;
    }

    public void StartTracking(
        Transform target,
        float speed,
        Boss1HandTrackingAxis axis = Boss1HandTrackingAxis.Both,
        Vector2 offset = default)
    {
        if (target == null)
        {
            Debug.LogWarning($"{nameof(Boss1HandController)} needs a target to start tracking.", this);
            return;
        }

        CancelActiveMotion(Boss1HandMotion.Tracking);

        trackingTarget = target;
        trackingSpeed = Mathf.Max(0f, speed);
        trackingAxis = axis;
        trackingOffset = offset;
        trackingHitbox = GetComponent<Boss1HandDamageHitbox>();
        trackingCollider = trackingHitbox != null ? trackingHitbox.AttackCollider : GetComponent<Collider2D>();
        trackingWallFilter = new ContactFilter2D();
        trackingWallFilter.SetLayerMask(LayerMask.GetMask("Ground"));
        trackingWallFilter.useTriggers = false;
    }

    public void StopMovement()
    {
        CancelActiveMotion(Boss1HandMotion.Stopped);
    }

    public void BeginDeath()
    {
        CancelActiveMotion(Boss1HandMotion.Moving);
        GetComponent<Boss1HandDamageHitbox>()?.EndAttack();
        animationController?.FreezeCurrentPose();
        deathMotionActive = true;
        deathStartPosition = transform.position;
        deathShakeStartTime = Time.time;
        deathTopOffset = 0f;
        foreach (SpriteRenderer renderer in GetComponentsInChildren<SpriteRenderer>())
        {
            if (renderer.enabled)
                deathTopOffset = Mathf.Max(deathTopOffset, renderer.bounds.max.y - deathStartPosition.y);
        }
    }

    public void ApplyDeathPose(float progress, float despawnY)
    {
        if (!deathMotionActive || !isActiveAndEnabled) return;
        progress = Mathf.Clamp01(progress);
        Vector2 position = deathStartPosition;
        // Separate rhythms keep the hands from moving in lockstep with each other or the body.
        bool isLeft = handSide == Boss1HandSide.Left;
        float elapsed = Time.time - deathShakeStartTime;
        float frequency = isLeft ? 8.1f : 9.7f;
        float phase = elapsed * frequency * Mathf.PI * 2f + (isLeft ? 0f : 2.1f);
        float amplitude = isLeft ? 0.0275f : 0.0225f;
        float shake = Mathf.Sin(phase) * 0.75f + Mathf.Sin(phase * 1.73f) * 0.25f;
        position.x += shake * amplitude * Mathf.Clamp01(elapsed * 12f) * (1f - progress);
        float targetY = Mathf.Min(deathStartPosition.y, despawnY - deathTopOffset);
        position.y = Mathf.Lerp(deathStartPosition.y, targetY, progress * progress);
        if (!SetWorldPosition(position)) return;
        if (progress >= 1f) gameObject.SetActive(false);
    }

    public void ResetState()
    {
        deathMotionActive = false;
        CancelActiveMotion(Boss1HandMotion.Idle);
        transform.localPosition = idleLocalPosition;
        transform.localRotation = idleLocalRotation;
        animationController?.ForceIdle();
    }

    public void SetRotationOffset(float degrees)
    {
        transform.localRotation = idleLocalRotation * Quaternion.Euler(0f, 0f, degrees);
    }

    public void ReturnToIdle(float duration, bool playReturnAnimation = true)
    {
        if (playReturnAnimation) animationController?.PlayReturn();
        CancelActiveMotion(Boss1HandMotion.Moving);

        if (duration <= 0f)
        {
            transform.localPosition = idleLocalPosition;
            transform.localRotation = idleLocalRotation;
            CompleteMotion(Boss1HandMotion.Idle);
            return;
        }

        movementCoroutine = StartCoroutine(ReturnToIdleRoutine(duration));
    }

    public IEnumerator WaitForMotion()
    {
        while (IsBusy)
        {
            yield return null;
        }
    }

    public bool PlayPrepareAnimation(Boss1HandAnimationTrack track)
    {
        return animationController != null && animationController.PlayPrepare(track);
    }

    public bool PlayAttackAnimation()
    {
        return animationController != null && animationController.PlayAttack();
    }

    public bool PlaySecondPrepareAnimation(Boss1HandAnimationTrack track)
    {
        return animationController != null && animationController.PlaySecondPrepare(track);
    }

    public bool PlayReturnAnimation()
    {
        return animationController != null && animationController.PlayReturn();
    }

    public void ForceIdleAnimation()
    {
        animationController?.ForceIdle();
    }

    public IEnumerator WaitForAnimation()
    {
        while (animationController != null && animationController.IsPlaying)
        {
            yield return null;
        }
    }

    private IEnumerator MoveWorldRoutine(Vector2 targetPosition, float duration, int version,
        AnimationCurve movementCurve)
    {
        Vector3 startPosition = transform.position;
        Vector3 endPosition = new Vector3(targetPosition.x, targetPosition.y, startPosition.z);
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            if (version != motionVersion) yield break;
            elapsedTime += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsedTime / duration);
            if (movementCurve != null) progress = Mathf.Clamp01(movementCurve.Evaluate(progress));
            if (!SetWorldPosition(Vector3.Lerp(startPosition, endPosition, progress))) yield break;
            yield return null;
        }

        if (version != motionVersion || !SetWorldPosition(endPosition)) yield break;
        CompleteMotion(Boss1HandMotion.Stopped);
    }

    private IEnumerator ReturnToIdleRoutine(float duration)
    {
        Vector3 startPosition = transform.localPosition;
        Quaternion startRotation = transform.localRotation;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsedTime / duration);
            transform.localPosition = Vector3.Lerp(startPosition, idleLocalPosition, progress);
            transform.localRotation = Quaternion.Slerp(startRotation, idleLocalRotation, progress);
            yield return null;
        }

        transform.localPosition = idleLocalPosition;
        transform.localRotation = idleLocalRotation;
        CompleteMotion(Boss1HandMotion.Idle);
    }

    private void UpdateTracking()
    {
        if (trackingTarget == null)
        {
            StopMovement();
            return;
        }

        Vector2 currentPosition = transform.position;
        Vector2 desiredPosition = (Vector2)trackingTarget.position + trackingOffset;

        switch (trackingAxis)
        {
            case Boss1HandTrackingAxis.Horizontal:
                desiredPosition.y = currentPosition.y;
                break;
            case Boss1HandTrackingAxis.Vertical:
                desiredPosition.x = currentPosition.x;
                break;
        }

        Vector2 nextPosition = Vector2.MoveTowards(
            currentPosition,
            desiredPosition,
            trackingSpeed * Time.deltaTime
        );

        SetWorldPosition(ClampTrackingPosition(nextPosition));
    }

    private Vector2 ClampTrackingPosition(Vector2 nextPosition)
    {
        if (trackingAxis != Boss1HandTrackingAxis.Vertical) return nextPosition;
        if (trackingHitbox != null) trackingCollider = trackingHitbox.AttackCollider;
        if (trackingCollider == null || !trackingCollider.enabled) return nextPosition;

        Vector2 currentPosition = transform.position;
        Vector2 movement = nextPosition - currentPosition;
        float distance = movement.magnitude;
        if (distance <= Mathf.Epsilon) return nextPosition;

        Physics2D.SyncTransforms();
        Vector2 direction = movement / distance;
        const float clearance = 0.01f;
        // Hands have no Rigidbody2D, so include static Ground colliders in the cast.
        trackingCollider.Cast(direction, trackingWallFilter, trackingWallHits,
            distance + clearance, ignoreSiblingColliders: false);
        foreach (RaycastHit2D hit in trackingWallHits)
        {
            // Ignore surfaces parallel to or facing away from the movement.
            if (Vector2.Dot(hit.normal, direction) >= -0.001f) continue;
            distance = Mathf.Min(distance, Mathf.Max(0f, hit.distance - clearance));
        }
        return currentPosition + direction * distance;
    }

    private bool SetWorldPosition(Vector2 position)
    {
        int version = motionVersion;
        BeforeMove?.Invoke(position);
        if (version != motionVersion) return false;
        transform.position = new Vector3(position.x, position.y, transform.position.z);
        return true;
    }

    private void CancelActiveMotion(Boss1HandMotion nextMotion)
    {
        motionVersion++;
        if (movementCoroutine != null)
        {
            StopCoroutine(movementCoroutine);
            movementCoroutine = null;
        }

        trackingTarget = null;
        currentMotion = nextMotion;
    }

    private void CompleteMotion(Boss1HandMotion completedState)
    {
        movementCoroutine = null;
        trackingTarget = null;
        currentMotion = completedState;
        MotionCompleted?.Invoke(this);
    }
}
