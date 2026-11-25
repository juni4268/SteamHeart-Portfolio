using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public sealed class EnemyFrameMovement : MonoBehaviour
{
    private Rigidbody2D body;
    private EnemyMovementStateBehaviour movementState;
    private int direction;
    private float previousDistance;
    private int previousLoop;
    private int currentLoop;
    private bool hasSample;
    private bool synchronizeTransform;

    internal System.Func<float, float> ConstrainHorizontalStep { get; set; }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
    }

    public void Move(int moveDirection)
    {
        direction = moveDirection == 0 ? 0 : moveDirection > 0 ? 1 : -1;
        StopXVelocity();
    }

    public void Stop()
    {
        direction = 0;
        StopXVelocity();
    }

    public void Suspend()
    {
        direction = 0;
    }

    internal void NotifyMovementEntered(EnemyMovementStateBehaviour state, float normalizedTime)
    {
        movementState = state;
        previousDistance = 0f;
        previousLoop = currentLoop = Mathf.FloorToInt(normalizedTime);
        hasSample = true;
    }

    internal void NotifyMovementUpdated(EnemyMovementStateBehaviour state, float normalizedTime)
    {
        if (movementState != state)
        {
            // Resume from the current curve value when this Component is re-enabled.
            movementState = state;
            hasSample = false;
        }

        currentLoop = Mathf.FloorToInt(normalizedTime);
    }

    internal void ApplyMovement(float distance, float loopDistance)
    {
        if (movementState == null) return;
        if (!hasSample)
        {
            previousDistance = distance;
            previousLoop = currentLoop;
            hasSample = true;
            return;
        }

        float delta = distance - previousDistance + (currentLoop - previousLoop) * loopDistance;
        previousDistance = distance;
        previousLoop = currentLoop;

        if (!isActiveAndEnabled || body == null || direction == 0 || delta == 0f) return;

        float step = delta * direction;
        if (ConstrainHorizontalStep != null) step = ConstrainHorizontalStep(step);
        if (step == 0f) return;
        body.position += Vector2.right * step;
        synchronizeTransform = true;
    }

    private void LateUpdate()
    {
        if (!synchronizeTransform || body == null) return;
        synchronizeTransform = false;

        // Animator evaluation can leave the parent Transform at its previous physics pose.
        // Match the horizontal pose after animation writes and before rendering the new Sprite.
        Vector3 position = transform.position;
        position.x = body.position.x;
        transform.position = position;
    }

    internal void NotifyMovementExited(EnemyMovementStateBehaviour state)
    {
        if (movementState == state) movementState = null;
    }

    private void OnDisable()
    {
        Stop();
    }

    private void StopXVelocity()
    {
        if (body != null) body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
    }
}
