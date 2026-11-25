using System;
using UnityEngine;

/// <summary>Blends a part's idle position toward its authored locomotion pose.</summary>
[DisallowMultipleComponent]
public sealed class Boss2MovementPose : MonoBehaviour
{
    [SerializeField] private Boss2BodyMovement bodyMovement;
    [Tooltip("Part position at full speed when the boss moves left.")]
    [SerializeField] private Transform leftMovementAnchor;
    [Tooltip("Mirror the left anchor across the body movement's local X origin when moving right.")]
    [SerializeField] private bool mirrorLeftPoseForRight;
    [Tooltip("Part position at full speed when moving right. Unused when mirroring is enabled.")]
    [SerializeField] private Transform rightMovementAnchor;

    public Vector3 GetPosition(Vector3 idlePosition)
    {
        if (!isActiveAndEnabled) return idlePosition;
        if (bodyMovement == null)
            throw new InvalidOperationException("Assign the body movement for this movement pose.");
        if (!bodyMovement.IsMoving) return idlePosition;

        bool mirror = !bodyMovement.IsFacingLeft && mirrorLeftPoseForRight;
        Transform anchor = bodyMovement.IsFacingLeft || mirror ? leftMovementAnchor : rightMovementAnchor;
        if (anchor == null)
            throw new InvalidOperationException("Assign the movement pose anchor for this direction.");

        Vector3 target = anchor.position;
        if (mirror)
        {
            Transform origin = bodyMovement.transform;
            Vector3 localPosition = origin.InverseTransformPoint(target);
            localPosition.x = -localPosition.x;
            target = origin.TransformPoint(localPosition);
        }
        return Vector3.Lerp(idlePosition, target, bodyMovement.MovementBlend);
    }
}
