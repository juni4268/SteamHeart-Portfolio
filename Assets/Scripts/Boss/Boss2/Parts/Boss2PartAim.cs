using System;
using UnityEngine;

/// <summary>Visual-only aiming. Mirroring and the animated muzzle share one transform group.</summary>
[DisallowMultipleComponent]
[ExecuteAlways]
public sealed class Boss2PartAim : MonoBehaviour
{
    [SerializeField] private Transform aimPivot;
    [SerializeField] private Transform visualGroup;
    [Tooltip("The muzzle's local +X axis is its firing direction. Place it under the visual Animator.")]
    [SerializeField] private Transform firePoint;
    [SerializeField] private bool mirrored;
    [SerializeField] private float idleAngle;

    private Quaternion returnStart;

    public bool IsConfigured => aimPivot != null && visualGroup != null && firePoint != null &&
        aimPivot != transform && aimPivot.IsChildOf(transform) &&
        visualGroup.IsChildOf(aimPivot) && firePoint.IsChildOf(visualGroup);
    public Vector3 PivotPosition => aimPivot.position;
    public Vector3 FirePosition => firePoint.position;
    // TransformVector includes the negative scale used for left-hand mirroring.
    public Vector2 FireDirection => ((Vector2)firePoint.TransformVector(Vector3.right)).normalized;

    private void OnEnable()
    {
        if (IsConfigured) ResetAim();
    }

    private void LateUpdate()
    {
        if (!Application.IsPlaying(gameObject) && IsConfigured) ResetAim();
    }

    public bool TryAimAt(Vector2 target) => TryAimAt(target, float.PositiveInfinity, out _);

    public bool TryAimAt(Vector2 target, float maximumDegreesDelta, out bool aligned)
        => TryAimAt(target, firePoint, maximumDegreesDelta, out aligned);

    public bool TryAimAt(Vector2 target, Transform directionOrigin, float maximumDegreesDelta, out bool aligned)
    {
        RequireBindings();
        if (directionOrigin == null || !directionOrigin.IsChildOf(visualGroup))
            throw new ArgumentException("Assign a direction origin under this part's visual group.", nameof(directionOrigin));
        ApplyMirror();
        aligned = false;
        Vector2 direction = ((Vector2)directionOrigin.TransformVector(Vector3.right)).normalized;
        Vector2 toTarget = target - (Vector2)aimPivot.position;
        Vector2 muzzleOffset = directionOrigin.position - aimPivot.position;
        float targetDistance = toTarget.magnitude;
        if (direction.sqrMagnitude < 0.0001f || targetDistance < 0.0001f) return false;

        // Account for a muzzle offset from the pivot, including an animated/rotated muzzle.
        // Merely rotating the pivot toward the target would miss when this offset is sideways.
        float lateralOffset = direction.x * muzzleOffset.y - direction.y * muzzleOffset.x;
        if (Mathf.Abs(lateralOffset) > targetDistance) return false;
        float forwardDistance = Mathf.Sqrt(Mathf.Max(0f,
            targetDistance * targetDistance - lateralOffset * lateralOffset));
        if (forwardDistance - Vector2.Dot(muzzleOffset, direction) <= 0.0001f) return false;

        float desiredAngle = Mathf.Atan2(toTarget.y, toTarget.x) -
            Mathf.Asin(Mathf.Clamp(lateralOffset / targetDistance, -1f, 1f));
        float currentAngle = Mathf.Atan2(direction.y, direction.x);
        Quaternion targetRotation = Quaternion.AngleAxis((desiredAngle - currentAngle) * Mathf.Rad2Deg,
            Vector3.forward) * aimPivot.rotation;
        aimPivot.rotation = Quaternion.RotateTowards(aimPivot.rotation, targetRotation,
            Mathf.Max(0f, maximumDegreesDelta));
        aligned = Quaternion.Angle(aimPivot.rotation, targetRotation) < 0.01f;
        return true;
    }

    public void ResetAim()
    {
        RequireBindings();
        ApplyMirror();
        aimPivot.localRotation = Quaternion.Euler(0f, 0f, idleAngle);
    }

    public void BeginReturnToIdle()
    {
        RequireBindings();
        returnStart = aimPivot.localRotation;
    }

    public void UpdateReturnToIdle(float progress)
    {
        RequireBindings();
        aimPivot.localRotation = Quaternion.Slerp(returnStart,
            Quaternion.Euler(0f, 0f, idleAngle), Mathf.Clamp01(progress));
    }

    private void ApplyMirror()
    {
        Vector3 scale = visualGroup.localScale;
        scale.x = Mathf.Abs(scale.x) * (mirrored ? -1f : 1f);
        visualGroup.localScale = scale;
    }

    private void RequireBindings()
    {
        if (!IsConfigured)
            throw new InvalidOperationException("Assign the part's aim pivot, visual group and fire point.");
    }

    private void OnDrawGizmosSelected()
    {
        if (!IsConfigured) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(PivotPosition, 0.06f);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(FirePosition, 0.04f);
        Gizmos.DrawLine(FirePosition, FirePosition + (Vector3)FireDirection * 0.4f);
    }
}
