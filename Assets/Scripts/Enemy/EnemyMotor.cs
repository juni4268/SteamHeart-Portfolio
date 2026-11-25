using UnityEngine;

public sealed class EnemyMotor
{
    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");

    private readonly Transform transform;
    private readonly Rigidbody2D body;
    private readonly EnemyFrameMovement frameMovement;
    private readonly Animator animator;
    private int moveDirection;

    public int FacingDirection => transform.localScale.x < 0f ? -1 : 1;

    public EnemyMotor(Transform transform, Rigidbody2D body, EnemyFrameMovement frameMovement)
    {
        this.transform = transform;
        this.body = body;
        this.frameMovement = frameMovement;
        animator = transform.GetComponent<Animator>();
        if (animator == null) animator = transform.GetComponentInChildren<Animator>();
    }

    public void Face(int direction)
    {
        if (direction == 0) return;
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * (direction > 0 ? 1 : -1);
        transform.localScale = scale;
    }

    public void SetMoveRequest(int direction)
    {
        moveDirection = direction;
        if (animator != null) animator.SetBool(IsMovingHash, moveDirection != 0);
    }

    public void ApplyMovement()
    {
        if (moveDirection == 0 || frameMovement == null || !frameMovement.isActiveAndEnabled)
        {
            Stop();
            return;
        }
        frameMovement.Move(moveDirection);
    }

    public void Stop()
    {
        SetMoveRequest(0);
        frameMovement?.Stop();
        if (body != null) body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
    }

    public void ApplyKnockback(Vector2 velocity)
    {
        if (body != null) body.linearVelocity = velocity;
    }
}
