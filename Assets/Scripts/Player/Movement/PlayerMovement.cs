using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

[RequireComponent(typeof(Player))]
[RequireComponent(typeof(PlayerMotor))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Ground Check")]
    [SerializeField] private float groundCheckDistance = 0.05f;
    [SerializeField, Range(0.1f, 1f)] private float groundCheckWidthMultiplier = 0.9f;
    [SerializeField, Range(0f, 1f)] private float minGroundNormalY = 0.5f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Platform Drop")]
    [SerializeField] private LayerMask platformLayer;

    [Header("Animation")]
    [Tooltip("기절 중 반복 모션을 제어하는 Animator Bool Parameter입니다.")]
    [SerializeField] private string groggyParameter = "IsGroggy";
    [Tooltip("Overdrive에 밀려가는 동안 자세를 유지하는 Animator Bool Parameter입니다.")]
    [SerializeField] private string forcedCarryParameter = "IsBeingCarried";

    [Header("Jump Effect")]
    [SerializeField] private GameObject jumpEffectPrefab;

    private Player player;
    private PlayerAttack attack;
    private PlayerAudio playerAudio;
    private PlayerMotor motor;
    private Collider2D playerCollider;
    private PlayerGroundSensor groundSensor;
    private PlayerPlatformDrop platformDrop;
    private Vector2 moveInput;
    private bool isGrounded;
    private bool wasGrounded;
    private bool groundAudioInitialized;
    private bool isFacingRight = true;
    private bool hasAppliedJumpForce;
    private float defaultGravityScale;
    private float knockbackControlLockedUntil;
    private int? groggyKnockbackVersion;
    private bool isBeingCarried;
    private bool isSubscribed;
    private float coyoteUntil = float.NegativeInfinity;
    private readonly Dictionary<Object, float> gravityFields = new Dictionary<Object, float>();

    public bool IsInGravityField => gravityFields.Count > 0;

    public void SetGravityField(Object source, float speedMultiplier, bool inside)
    {
        if (source == null) return;
        if (inside)
        {
            gravityFields[source] = Mathf.Clamp01(speedMultiplier);
            ClearJumpRequest();
            player.ClearBufferedAction(PlayerAction.Dash);
            coyoteUntil = float.NegativeInfinity;
        }
        else gravityFields.Remove(source);
    }

    private float GravityFieldSpeedMultiplier
    {
        get
        {
            float multiplier = 1f;
            foreach (float value in gravityFields.Values) multiplier = Mathf.Min(multiplier, value);
            return multiplier;
        }
    }

    public bool IsKnockbackLocked => Time.time < knockbackControlLockedUntil;
    public int KnockbackVersion { get; private set; }
    public bool IsGroggy => isActiveAndEnabled && player != null && player.health != null
        && !player.health.IsDead && IsKnockbackLocked && groggyKnockbackVersion == KnockbackVersion;
    public bool IsBeingCarried => isBeingCarried;
    public bool IsGrounded => isGrounded && !hasAppliedJumpForce;

    private bool IsMovingAwayFromGround =>
        Vector2.Dot(motor.Velocity, groundSensor.SupportingNormal) > 0.1f;

    public void ClearJumpRequest() => player.ClearBufferedAction(PlayerAction.Jump);

    internal bool TryPassThroughDashPlatform(Collider2D platform) =>
        platformDrop.IsIgnored(platform) || platformDrop.TryBegin(platform, platformLayer);

    public void FaceMovementInput()
    {
        if (moveInput.x > 0f && !isFacingRight) Flip();
        else if (moveInput.x < 0f && isFacingRight) Flip();
    }

    private PlayerData Data => player.Data;
    private float WalkSpeed => Data.WalkSpeed;
    private float JumpForce => Data.JumpForce;
    private float FallGravityMultiplier => Data.FallGravityMultiplier;
    private float GroundLerpFactor => Data.GroundLerpFactor;
    private float AirLerpFactor => Data.AirLerpFactor;

    private void Awake()
    {
        player = GetComponent<Player>();
        attack = GetComponent<PlayerAttack>();
        playerAudio = GetComponent<PlayerAudio>();
        motor = GetComponent<PlayerMotor>();
        playerCollider = GetComponent<Collider2D>();
        groundSensor = new PlayerGroundSensor(playerCollider);
        platformDrop = new PlayerPlatformDrop(playerCollider);
        defaultGravityScale = motor.GravityScale;

        if (player.Data == null)
        {
            Debug.LogError($"{nameof(PlayerMovement)} requires {nameof(PlayerData)} on {nameof(Player)}.", this);
            enabled = false;
        }
    }

    private void OnEnable() => SubscribeToInput();

    private void Start() => SubscribeToInput();

    private void SubscribeToInput()
    {
        if (isSubscribed || player == null || player.input == null) return;
        player.input.Player.Movement.performed += OnMovementPerformed;
        player.input.Player.Movement.canceled += OnMovementCanceled;
        player.input.Player.Jump.started += OnJumpStarted;
        player.input.Player.Down.started += OnDownStarted;
        isSubscribed = true;
    }

    private void UnsubscribeFromInput()
    {
        if (!isSubscribed || player == null || player.input == null)
        {
            isSubscribed = false;
            return;
        }

        player.input.Player.Movement.performed -= OnMovementPerformed;
        player.input.Player.Movement.canceled -= OnMovementCanceled;
        player.input.Player.Jump.started -= OnJumpStarted;
        player.input.Player.Down.started -= OnDownStarted;
        isSubscribed = false;
    }

    private void OnDisable()
    {
        EndForcedCarry();
        ClearKnockback(KnockbackVersion);
        groggyKnockbackVersion = null;
        UpdateControlAnimations();
        UnsubscribeFromInput();
        moveInput = Vector2.zero;
        ClearJumpRequest();
        coyoteUntil = float.NegativeInfinity;
        platformDrop?.RestoreAllCollisions();
        groundSensor?.Clear();
        gravityFields.Clear();
        groundAudioInitialized = false;
        playerAudio?.StopLocomotion();
    }

    private void OnMovementPerformed(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    private void OnMovementCanceled(InputAction.CallbackContext context)
    {
        moveInput = Vector2.zero;
    }

    private void OnJumpStarted(InputAction.CallbackContext context) => player.RequestAction(PlayerAction.Jump);

    private void OnDownStarted(InputAction.CallbackContext context)
    {
        HandleDownInput();
    }

    private void Update()
    {
        isGrounded = CheckGround();

        // Recover even when a ceiling hit or dash prevented an airborne ground-check frame.
        if (isGrounded && !IsMovingAwayFromGround)
        {
            hasAppliedJumpForce = false;
            coyoteUntil = !IsInGravityField && !IsKnockbackLocked && !isBeingCarried
                ? Time.time + Data.CoyoteTime : float.NegativeInfinity;
            // Reset before consuming a buffered jump, even while the attack is finishing.
            attack?.ResetAirAttackUsage();
        }

        if (!groundAudioInitialized)
        {
            groundAudioInitialized = isGrounded;
        }
        else if (!wasGrounded && isGrounded && !IsMovingAwayFromGround && !isBeingCarried)
        {
            GetComponent<PlayerAudio>()?.PlayLanding();
        }

        if (player.CanTurn) FaceMovementInput();

        UpdateLocomotionAudio();
        UpdateAnimator();
        wasGrounded = isGrounded;
    }

    private void UpdateLocomotionAudio()
    {
        bool isMovingOnGround = IsGrounded && player.CanMove && !isBeingCarried && !IsKnockbackLocked &&
            (player.dash == null || !player.dash.IsDashing) && Mathf.Abs(motor.Velocity.x) > 0.05f;

        if (!isMovingOnGround) playerAudio?.StopLocomotion();
    }

    private void FixedUpdate()
    {
        platformDrop.RestoreClearedCollisions(groundCheckDistance);

        if (isBeingCarried)
        {
            motor.Stop();
            motor.SetGravityScale(0f);
            return;
        }

        RefreshGravityScale();

        if (player.dash != null && player.dash.IsDashing && !player.dash.IsLanding) return;
        if (IsKnockbackLocked) return;
        if (!player.CanMove)
        {
            motor.SetVelocityX(0f);
            return;
        }

        float targetVelocityX = moveInput.x * WalkSpeed * GravityFieldSpeedMultiplier;
        float currentLerp = isGrounded ? GroundLerpFactor : AirLerpFactor;

        float newVelocityX = Mathf.Lerp(motor.Velocity.x, targetVelocityX, currentLerp);
        motor.SetVelocityX(newVelocityX);
    }

    internal void RefreshGravityScale()
    {
        float gravity = motor.Velocity.y < -0.1f
            ? defaultGravityScale * FallGravityMultiplier
            : defaultGravityScale;
        if (player.dash != null && player.dash.IsMoving) gravity = 0f;
        motor.SetGravityScale(gravity);
    }

    private bool CheckGround()
    {
        return groundSensor.Probe(groundCheckDistance, groundCheckWidthMultiplier, minGroundNormalY,
            groundLayer, platformDrop);
    }

    private void HandleDownInput()
    {
        if (player.CanStartAction && isGrounded)
        {
            CheckForPlatformDrop();
        }
    }

    private void CheckForPlatformDrop()
    {
        if (!platformDrop.TryBegin(groundSensor.SupportingCollider, platformLayer)) return;
        // At a seam, another one-way collider can still support the same ground probe.
        while (CheckGround())
        {
            if (!platformDrop.TryBegin(groundSensor.SupportingCollider, platformLayer)) break;
        }
        ClearJumpRequest();
        coyoteUntil = float.NegativeInfinity;
        isGrounded = false;
        groundSensor.Clear();
    }

    public bool TryJump()
    {
        if (!isActiveAndEnabled || !player.CanStartAction || IsInGravityField || hasAppliedJumpForce ||
            (!isGrounded && Time.time > coyoteUntil) || (isGrounded && IsMovingAwayFromGround)) return false;

        coyoteUntil = float.NegativeInfinity;
        FaceMovementInput();
        ApplyJumpForce();
        GetComponent<PlayerAudio>()?.PlayJump();
        if (jumpEffectPrefab != null)
        {
            Bounds bounds = playerCollider.bounds;
            Transform effect = Instantiate(jumpEffectPrefab, transform, false).transform;
            effect.position = new Vector3(bounds.center.x, bounds.min.y, transform.position.z);
            effect.SetParent(null, true);
        }
        isGrounded = false;
        player.anim?.SetTrigger("Jump");
        return true;
    }

    public void ApplyKnockback(Vector2 velocity, float controlLockDuration)
    {
        KnockbackVersion++;
        groggyKnockbackVersion = null;
        player.CancelCombatActions();
        player.dash?.CancelDash();
        ClearJumpRequest();
        coyoteUntil = float.NegativeInfinity;
        motor.SetVelocity(velocity);
        knockbackControlLockedUntil = Time.time + Mathf.Max(0f, controlLockDuration);
        isGrounded = false;
        hasAppliedJumpForce = false;
        UpdateControlAnimations();
    }

    public void BeginGroggy(int expectedKnockbackVersion)
    {
        if (expectedKnockbackVersion != KnockbackVersion || !IsKnockbackLocked) return;
        groggyKnockbackVersion = expectedKnockbackVersion;
        UpdateControlAnimations();
    }

    public void ClearKnockback(int expectedVersion)
    {
        // Cancelling an attack must not clear a newer hit's control lock.
        if (expectedVersion != KnockbackVersion || !IsKnockbackLocked) return;
        knockbackControlLockedUntil = Time.time;
        groggyKnockbackVersion = null;
        motor.Stop();
        UpdateControlAnimations();
    }

    public bool TryBeginForcedCarry()
    {
        if (!isActiveAndEnabled || isBeingCarried || player.health == null || player.health.IsDead)
            return false;
        player.CancelCombatActions();
        player.dash?.CancelDash();
        ClearJumpRequest();
        coyoteUntil = float.NegativeInfinity;
        isBeingCarried = true;
        isGrounded = false;
        hasAppliedJumpForce = false;
        motor.Stop();
        motor.SetGravityScale(0f);
        UpdateControlAnimations();
        return true;
    }

    public void MoveForcedCarry(Vector2 position)
    {
        if (!isBeingCarried) return;
        motor.Stop();
        motor.SetPosition(position);
    }

    public void EndForcedCarry()
    {
        if (!isBeingCarried) return;
        isBeingCarried = false;
        motor.Stop();
        motor.SetGravityScale(defaultGravityScale);
        UpdateControlAnimations();
    }

    private void ApplyJumpForce()
    {
        if (hasAppliedJumpForce) return;

        motor.SetVelocityY(JumpForce);
        hasAppliedJumpForce = true;
    }

    private void UpdateAnimator()
    {
        if (player.anim == null) return;

        if (!wasGrounded && isGrounded)
        {
            player.anim.ResetTrigger("Jump");
        }

        player.anim.SetBool("isGrounded", isGrounded);
        UpdateControlAnimations();

        player.anim.SetBool("hasMoveInput", player.CanMove && Mathf.Abs(moveInput.x) > 0.01f);
        player.anim.SetFloat("xVelocity", Mathf.Abs(motor.Velocity.x));
        player.anim.SetFloat("yVelocity", motor.Velocity.y);
    }

    private void UpdateControlAnimations()
    {
        if (player == null || player.anim == null) return;
        player.anim.SetBool(groggyParameter, IsGroggy);
        player.anim.SetBool(forcedCarryParameter, isBeingCarried);
    }

    private void Flip()
    {
        isFacingRight = !isFacingRight;
        Vector3 localScale = transform.localScale;
        localScale.x *= -1;
        transform.localScale = localScale;
    }

    private void OnDrawGizmosSelected()
    {
        Collider2D gizmoCollider = playerCollider != null ? playerCollider : GetComponent<Collider2D>();
        if (gizmoCollider != null)
        {
            Bounds bounds = gizmoCollider.bounds;
            const float probeHeight = PlayerGroundSensor.ProbeHeight;

            Gizmos.color = isGrounded ? Color.green : Color.red;
            Vector3 groundSize = new Vector3(
                bounds.size.x * groundCheckWidthMultiplier,
                probeHeight + groundCheckDistance,
                0f);
            Vector3 groundCenter = new Vector3(
                bounds.center.x,
                bounds.min.y + (probeHeight - groundCheckDistance) * 0.5f,
                bounds.center.z);
            Gizmos.DrawWireCube(groundCenter, groundSize);
        }
    }
}
