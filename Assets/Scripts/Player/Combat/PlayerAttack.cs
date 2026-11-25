using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Player))]
[RequireComponent(typeof(PlayerWeaponController))]
public class PlayerAttack : MonoBehaviour
{
    private const string AttackPointName = "AttackPoint";

    [SerializeField] private Transform attackPoint;
    [Tooltip("일반 공격, 공중 공격, 강타와 카운터가 실제로 명중했을 때 재생하는 공통 이펙트입니다.")]
    [SerializeField] private CombatAnimationEffect hitEffectPrefab;
    [SerializeField] private PlayerAttackFeedback groundAttackFeedback = new PlayerAttackFeedback();
    [SerializeField] private PlayerAttackFeedback airAttackFeedback = new PlayerAttackFeedback();

    private Player player;
    private PlayerMovement movement;
    private PlayerWeaponController weaponController;
    private PlayerAirAttack airAttack;
    private PlayerGroundAttack groundAttack;
    private bool isSubscribed;
    private bool dashCancelAllowed;

    public Transform AttackPoint => attackPoint;
    public CombatAnimationEffect HitEffectPrefab => hitEffectPrefab;
    public bool IsExecuting =>
        (airAttack != null && airAttack.IsExecuting) ||
        (groundAttack != null && groundAttack.IsExecuting);
    public bool IsAirAttacking => airAttack != null && airAttack.IsExecuting;
    public bool CanDashCancel => IsExecuting && dashCancelAllowed;
    public bool BlocksMovement =>
        (groundAttack != null && groundAttack.BlocksMovement) ||
        HitStopManager.IsActive;

    private void Awake()
    {
        player = GetComponent<Player>();
        movement = GetComponent<PlayerMovement>();
        weaponController = GetComponent<PlayerWeaponController>();

        if (attackPoint == null)
            Debug.LogError($"{nameof(PlayerAttack)} requires an attack point.", this);

        PlayerAttackDamage damage = new PlayerAttackDamage(
            attackPoint, transform, LayerMask.GetMask("Enemy"), hitEffectPrefab);
        airAttack = new PlayerAirAttack(player, movement, damage, HandleAirHit);
        groundAttack = new PlayerGroundAttack(player, weaponController, damage, HandleGroundHit);
    }

    private void HandleAirHit(Component target)
    {
        airAttackFeedback.Play(target);
        GetComponent<PlayerAudio>()?.PlayAttackHit(1);
    }

    private void HandleGroundHit(Component target)
    {
        groundAttackFeedback.Play(target);
        GetComponent<PlayerAudio>()?.PlayAttackHit(groundAttack.CurrentAttackNumber);
    }

    private void Reset()
    {
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child.name != AttackPointName) continue;
            attackPoint = child;
            break;
        }
    }

    private void OnEnable()
    {
        groundAttackFeedback.Enable(this);
        airAttackFeedback.Enable(this);
        SubscribeToInput();
    }

    private void Start() => SubscribeToInput();

    private void SubscribeToInput()
    {
        if (isSubscribed || player == null || player.input == null) return;
        player.input.Player.Attack.started += OnAttackStarted;
        isSubscribed = true;
    }

    private void OnDisable()
    {
        if (isSubscribed && player != null && player.input != null)
            player.input.Player.Attack.started -= OnAttackStarted;

        isSubscribed = false;
        player.ClearBufferedAction(PlayerAction.Attack);
        Cancel();
        groundAttackFeedback.Disable();
        airAttackFeedback.Disable();
    }

    private void OnAttackStarted(InputAction.CallbackContext context) => player.RequestAction(PlayerAction.Attack);

    private void LateUpdate()
    {
        if (!IsAirAttacking && (groundAttack == null || !groundAttack.IsExecuting)) return;

        // An inactive Animator cannot send its state-exit notification.
        if (!player.CanAnimate)
            Cancel();
    }

    public bool TryAttack()
    {
        if (!isActiveAndEnabled) return false;

        // An active ground combo consumes attack input before CanStartAction rejects it.
        if (groundAttack.TryHandleComboInput()) return true;
        if (HitStopManager.IsActive || !player.CanStartAction || attackPoint == null) return false;

        if (movement != null && !movement.IsGrounded && player.anim != null)
        {
            WeaponAttackData attackData = weaponController.CurrentWeaponStage?.AirAttack;
            bool started = airAttack.TryStart(attackData);
            if (started) CloseDashCancelWindow();
            return started;
        }

        bool groundStarted = groundAttack.TryStart();
        if (groundStarted) CloseDashCancelWindow();
        return groundStarted;
    }

    public void OpenDashCancelWindow()
    {
        if (IsExecuting) dashCancelAllowed = true;
    }

    public void CloseDashCancelWindow() => dashCancelAllowed = false;

    public void Cancel()
    {
        GetComponent<PlayerAudio>()?.StopAttackSwing();
        CloseDashCancelWindow();
        airAttack?.Cancel();
        groundAttack?.Cancel();
    }

    public void FinishAirAttack() => airAttack?.Finish();
    public void CancelAirAttack() => airAttack?.Cancel();
    public void CancelGroundAttack(int attackNumber) => groundAttack?.Cancel(attackNumber);
    public void ResetAirAttackUsage() => airAttack?.ResetUsage();
    public void ApplyAirAttackDamage() => airAttack?.ApplyDamage();
    public void ApplyGroundAttackDamage() => groundAttack?.ApplyDamage();
    public void StartGroundComboInput() => groundAttack?.StartComboInput();
    public void EndGroundComboInput() => groundAttack?.EndComboInput();
    public void FinishGroundAttack(int attackNumber) => groundAttack?.Finish(attackNumber);
}
