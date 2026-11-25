using UnityEngine;

[RequireComponent(typeof(PlayerHealth))]
[RequireComponent(typeof(PlayerMotor))]
[DefaultExecutionOrder(-100)]
public class Player : MonoBehaviour
{
    [SerializeField] private PlayerData data;

    public PlayerData Data => data;
    public PlayerMotor motor { get; private set; }
    public Animator anim { get; private set; }
    public PlayerInputSet input { get; private set; }
    public PlayerHealth health { get; private set; }
    public PlayerDash dash { get; private set; }
    public event System.Action<PlayerAction> ActionRejected;

    private PlayerMovement movement;
    private PlayerActionController actions;

    public bool IsCombatExecuting => actions != null && actions.IsCombatExecuting;
    public bool CanStartAction => actions != null && actions.CanStartAction;
    public bool CanStartDash => actions != null && actions.CanStartDash;
    public bool CanStartCounter => actions != null && actions.CanStartCounter;
    public bool CanMove => actions != null && actions.CanMove;
    public bool CanTurn => actions != null && actions.CanTurn;
    public bool CanAnimate => actions != null && actions.CanAnimate;
    public bool CanExecuteCombatAction(Behaviour action) => actions != null && actions.CanExecuteCombatAction(action);

    private void Awake()
    {
        motor = GetComponent<PlayerMotor>();
        anim = GetComponentInChildren<Animator>();
        health = GetComponent<PlayerHealth>();
        dash = GetComponent<PlayerDash>();
        movement = GetComponent<PlayerMovement>();
        actions = new PlayerActionController(this, movement, GetComponent<PlayerAttack>(),
            GetComponent<PlayerGroundSlam>(), GetComponent<PlayerCounter>());
    }

    private void OnEnable()
    {
        // Script reloads call OnEnable again without recreating this component in Awake.
        input ??= new PlayerInputSet();
        PlayerKeyBindings.ApplyTo(input);
        PlayerKeyBindings.Changed += RefreshKeyBindings;
        input.Player.Enable();
    }

    private void Update() => PlayerProgressPersistence.RetryPendingSave();

    private void LateUpdate() => actions?.ConsumeBufferedAction();

    public void RequestAction(PlayerAction action) => actions?.RequestAction(action);
    public void ClearBufferedAction(PlayerAction action) => actions?.ClearBufferedAction(action);
    internal void ReportRejectedAction(PlayerAction action) => ActionRejected?.Invoke(action);

    private void OnApplicationPause(bool isPaused)
    {
        if (isPaused) PlayerProgressPersistence.RetryPendingSave(immediately: true);
    }

    private void OnApplicationQuit() => PlayerProgressPersistence.RetryPendingSave(immediately: true);

    private void OnDisable()
    {
        PlayerKeyBindings.Changed -= RefreshKeyBindings;
        movement?.EndForcedCarry();
        input?.Player.Disable();
        CancelCombatActions();
        dash?.CancelDash();
    }

    private void RefreshKeyBindings() => PlayerKeyBindings.ApplyTo(input);

    public void StopMovementForCombat()
    {
        motor.SetVelocityX(0f);
        movement?.ClearJumpRequest();
    }

    // Forced interruption only: knockback, death, or disabling the player.
    public void CancelCombatActions() => actions?.CancelCombatActions();

    private void OnDestroy()
    {
        input?.Dispose();
        input = null;
    }
}
