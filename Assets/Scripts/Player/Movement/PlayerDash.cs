using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Player))]
[RequireComponent(typeof(PlayerMotor))]
public class PlayerDash : MonoBehaviour
{
    private static readonly int IsDashingParameter = Animator.StringToHash("isDashing");
    private static readonly int DashLandingParameter = Animator.StringToHash("dashLanding");

    private Player player;
    private PlayerMotor motor;
    private PlayerMovement movement;
    private PlayerAttack attack;
    private float dashDirection;
    private float dashSpeed;
    private float dashMovementStartedAt;
    private float dashMovementDuration;
    private float dashTargetX;
    private float nextDashTime;
    private bool isDashing;
    private bool isInvincible;
    private bool isMoving;
    private bool isSubscribed;
    private float lastMovementX;
    private float lastRequestedSpeed;
    private int blockedSteps;
    private bool landingEntered;
    private bool awaitingLanding;

    public bool IsDashing => isDashing;
    internal bool IsMoving => isDashing && isMoving;
    internal bool IsLanding => isDashing && landingEntered;
    public bool IsInvincible => isDashing && isInvincible;
    public float Direction => dashDirection;
    public float CooldownRemaining => Mathf.Max(0f, nextDashTime - Time.time);
    public float CooldownDuration => player != null && player.Data != null ? Mathf.Max(0f, Data.DashCooldown) : 0f;

    private PlayerData Data => player.Data;
    private float DashDistance => Data.DashDistance;

    private void Awake()
    {
        player = GetComponent<Player>();
        motor = GetComponent<PlayerMotor>();
        movement = GetComponent<PlayerMovement>();
        attack = GetComponent<PlayerAttack>();

        if (player.Data == null)
        {
            Debug.LogError($"{nameof(PlayerDash)} requires {nameof(PlayerData)} on {nameof(Player)}.", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        SubscribeToInput();
    }

    private void Start()
    {
        SubscribeToInput();
    }

    private void SubscribeToInput()
    {
        if (isSubscribed || player == null || player.input == null) return;

        player.input.Player.Dash.started += OnDashStarted;
        isSubscribed = true;
    }

    private void OnDashStarted(InputAction.CallbackContext context) => player.RequestAction(PlayerAction.Dash);

    public bool TryDash()
    {
        if (!isActiveAndEnabled || !player.CanStartDash || movement == null || movement.IsInGravityField ||
            Time.time < nextDashTime) return false;
        if (attack != null && attack.IsExecuting) attack.Cancel();
        movement.FaceMovementInput();
        StartDash();
        return true;
    }

    private void StartDash()
    {
        if (Time.time < nextDashTime) return;
        EndDashInvincibility();
        isDashing = true;
        isMoving = false;
        landingEntered = false;
        awaitingLanding = false;
        dashDirection = Mathf.Sign(transform.localScale.x);
        player.StopMovementForCombat();
        player.anim?.SetBool(DashLandingParameter, false);
        player.anim?.SetBool(IsDashingParameter, true);
    }

    // Animation Events define the invincibility window independently of movement.
    public void BeginDashInvincibility()
    {
        if (isActiveAndEnabled && isDashing) isInvincible = true;
    }

    public void EndDashInvincibility() => isInvincible = false;

    // Called when the Animator enters Dash.
    public void BeginDashMovement(float duration)
    {
        if (!isDashing || isMoving) return;

        dashMovementDuration = Mathf.Max(duration, 0.01f);
        dashMovementStartedAt = Time.time;
        dashTargetX = transform.position.x + dashDirection * DashDistance;
        float curveArea = GetSpeedCurveArea();
        float distance = Mathf.Abs(dashTargetX - transform.position.x);
        dashSpeed = curveArea > 0f ? distance / (dashMovementDuration * curveArea) : 0f;
        isMoving = true;
        GetComponent<PlayerAudio>()?.PlayDash();
        motor.SetVelocityY(0f);
        movement.RefreshGravityScale();
        lastMovementX = transform.position.x;
        lastRequestedSpeed = 0f;
        blockedSteps = 0;
        motor.SetVelocityX(dashDirection * Mathf.Min(GetDashSpeed(), distance / Time.fixedDeltaTime));
    }

    private float GetSpeedCurveArea(float from = 0f)
    {
        const int sampleCount = 64;
        float area = 0f;
        float previous = Mathf.Max(0f, Data.DashSpeedCurve.Evaluate(from));
        for (int i = 1; i <= sampleCount; i++)
        {
            float progress = Mathf.Lerp(from, 1f, (float)i / sampleCount);
            float current = Mathf.Max(0f, Data.DashSpeedCurve.Evaluate(progress));
            area += (previous + current) * 0.5f * (1f - from) / sampleCount;
            previous = current;
        }
        return area;
    }

    internal void BeginDashLanding()
    {
        if (!isDashing) return;
        landingEntered = true;
        EndDashMovement();
    }

    internal void NotifyDashMovementExited()
    {
        if (isDashing) awaitingLanding = true;
    }

    private void LateUpdate()
    {
        if (isDashing && !player.CanAnimate) CancelDash();
        if (!awaitingLanding) return;
        awaitingLanding = false;
        if (!landingEntered) CancelDash();
    }

    private float GetDashSpeed()
    {
        float progress = Mathf.Clamp01((Time.time - dashMovementStartedAt) / dashMovementDuration);
        return dashSpeed * Mathf.Max(0f, Data.DashSpeedCurve.Evaluate(progress));
    }

    // Request DashLand immediately; keep the action locked through its recovery.
    public void EndDashMovement()
    {
        if (!isDashing || !isMoving) return;
        GetComponent<PlayerAudio>()?.StopDash();
        isMoving = false;
        movement.RefreshGravityScale();
        motor.SetVelocityX(0f);
        player.anim?.SetBool(DashLandingParameter, true);
    }

    private void FixedUpdate()
    {
        if (!isDashing) return;
        if (!isMoving)
        {
            if (!landingEntered) motor.SetVelocityX(0f);
            return;
        }

        motor.SetVelocityY(0f);
        float moved = (transform.position.x - lastMovementX) * dashDirection;
        blockedSteps = lastRequestedSpeed > 0.1f && moved < 0.001f ? blockedSteps + 1 : 0;
        if (blockedSteps >= 2 || Time.time - dashMovementStartedAt >= dashMovementDuration)
        {
            EndDashMovement();
            return;
        }

        lastMovementX = transform.position.x;
        float remaining = Mathf.Max(0f, (dashTargetX - transform.position.x) * dashDirection);
        lastRequestedSpeed = Mathf.Min(GetDashSpeed(), remaining / Time.fixedDeltaTime);
        motor.SetVelocityX(dashDirection * lastRequestedSpeed);
    }

    private void OnCollisionEnter2D(Collision2D collision) => CheckDashCollision(collision);

    private void OnCollisionStay2D(Collision2D collision) => CheckDashCollision(collision);

    private void CheckDashCollision(Collision2D collision)
    {
        if (!isDashing || !isMoving) return;
        for (int i = 0; i < collision.contactCount; i++)
        {
            Vector2 normal = collision.GetContact(i).normal;
            if (normal.x * dashDirection >= -0.5f || Mathf.Abs(normal.y) >= 0.5f) continue;
            if (movement.TryPassThroughDashPlatform(collision.collider)) return;
            EndDashMovement();
            return;
        }
    }

    // Called when the Animator leaves DashLand.
    public void FinishDash()
    {
        if (!isDashing) return;
        float velocityX = motor.Velocity.x;
        CancelDash();
        motor.SetVelocityX(velocityX);
    }

    public void CancelDash()
    {
        GetComponent<PlayerAudio>()?.StopDash();
        EndDashInvincibility();
        if (!isDashing) return;
        nextDashTime = Time.time + Mathf.Max(0f, Data.DashCooldown);
        isMoving = false;
        isDashing = false;
        movement.RefreshGravityScale();
        landingEntered = false;
        awaitingLanding = false;
        motor.SetVelocityX(0f);
        player.anim?.SetBool(IsDashingParameter, false);
        player.anim?.SetBool(DashLandingParameter, false);
    }

    private void OnDisable()
    {
        player.ClearBufferedAction(PlayerAction.Dash);
        if (isSubscribed && player != null && player.input != null)
        {
            player.input.Player.Dash.started -= OnDashStarted;
        }
        isSubscribed = false;

        CancelDash();
    }
}
