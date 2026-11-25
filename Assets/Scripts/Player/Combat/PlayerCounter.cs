using UnityEngine;

[RequireComponent(typeof(Player), typeof(PlayerAttack))]
public class PlayerCounter : MonoBehaviour
{
    private static readonly int CounterPrepareTrigger = Animator.StringToHash("CounterPrepare");
    private static readonly int CounterAttackTrigger = Animator.StringToHash("CounterAttack");

    private enum Phase { Idle, Preparing, Invincible, Attacking, Recovering }

    [SerializeField, Min(0f)] private float cooldown = 5f;
    [SerializeField] private AttackPolygon attackPolygon = new AttackPolygon();

    [Header("Feedback")]
    [SerializeField] private PlayerAttackFeedback hitFeedback = new PlayerAttackFeedback();

    private PlayerAttackDamage attackDamage;
    private PlayerSkillExecution execution;
    private Player player;
    private PlayerMovement movement;
    private PlayerHealth health;
    private PlayerAttack playerAttack;
    private PlayerWeaponController weaponController;
    private Phase phase;
    private bool receivedAttack;
    private bool damageApplied;
    private bool counterStateEntered;
    private bool awaitingCounterEntry;

    public float CooldownRemaining => execution != null ? execution.CooldownRemaining : 0f;
    public float CooldownDuration => Mathf.Max(0f, cooldown);
    public bool IsExecuting => execution != null && execution.IsExecuting;
    public bool IsInvincible => phase == Phase.Invincible && CanExecute();
    public bool ReceivedAttack => receivedAttack;

    private void Awake()
    {
        player = GetComponent<Player>();
        movement = GetComponent<PlayerMovement>();
        health = GetComponent<PlayerHealth>();
        playerAttack = GetComponent<PlayerAttack>();
        weaponController = GetComponent<PlayerWeaponController>();
        attackDamage = new PlayerAttackDamage(playerAttack.AttackPoint, transform, LayerMask.GetMask("Enemy"),
            playerAttack.HitEffectPrefab);
        execution = new PlayerSkillExecution(player, this);
    }

    private void OnEnable()
    {
        health.Died += Cancel;
        hitFeedback.Enable(this);
    }

    private void OnDisable()
    {
        health.Died -= Cancel;
        Cancel();
    }

    private void Update()
    {
        // Cleanup only; animation events advance every phase of the skill.
        if (IsExecuting && (!CanExecute() || !player.CanAnimate))
            Cancel();
    }

    private void LateUpdate()
    {
        if (!awaitingCounterEntry) return;
        awaitingCounterEntry = false;
        if (!counterStateEntered) Cancel();
    }

    public bool TryUse()
    {
        if (!CanExecute() || !execution.CanStartWithControl(player.CanStartCounter)) return false;
        if (movement == null || !movement.IsGrounded) return false;
        if (playerAttack.AttackPoint == null || weaponController.CurrentWeaponStage == null) return false;

        // Do not lock control without both animations and their triggers.
        Animator animator = player.anim;
        if (animator == null || !animator.isActiveAndEnabled ||
            !HasAnimationRole(animator, PlayerCounterAnimationRole.Preparation) ||
            !HasAnimationRole(animator, PlayerCounterAnimationRole.Counter))
            return false;

        bool hasTrigger = false;
        bool hasAttackTrigger = false;
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.type != AnimatorControllerParameterType.Trigger) continue;
            if (parameter.nameHash == CounterPrepareTrigger) hasTrigger = true;
            if (parameter.nameHash == CounterAttackTrigger) hasAttackTrigger = true;
        }
        if (!hasTrigger || !hasAttackTrigger) return false;

        if (playerAttack.IsExecuting) playerAttack.Cancel();
        receivedAttack = false;
        damageApplied = false;
        counterStateEntered = false;
        awaitingCounterEntry = false;
        execution.Begin();
        phase = Phase.Preparing;
        player.StopMovementForCombat();
        animator.ResetTrigger(CounterPrepareTrigger);
        animator.ResetTrigger(CounterAttackTrigger);
        animator.SetTrigger(CounterPrepareTrigger);
        return true;
    }

    // Animation Event: beginning of the counter window.
    public void BeginCounterWindow()
    {
        if (!CanExecute() || phase != Phase.Preparing) return;
        phase = Phase.Invincible;
    }

    // Called by PlayerHealth only after validating incoming damage.
    public bool TryBlockDamage()
    {
        if (!IsInvincible) return false;
        GetComponent<PlayerAudio>()?.PlayParry();
        receivedAttack = true;
        // Leaving the counter window here prevents later hits from triggering another counter.
        phase = Phase.Attacking;
        player.anim.ResetTrigger(CounterAttackTrigger);
        player.anim.SetTrigger(CounterAttackTrigger);
        return true;
    }

    // Animation Event: end of the unused counter window; finish the remaining preparation motion.
    public void EndCounterWindow()
    {
        if (!CanExecute() || phase != Phase.Invincible) return;
        phase = Phase.Recovering;
    }

    // Animation Event on CounterAttack: contact frame, once per counter.
    public void ApplyCounterDamage()
    {
        if (!CanExecute() || phase != Phase.Attacking || damageApplied) return;
        WeaponStageData weaponStage = weaponController.CurrentWeaponStage;
        if (weaponStage == null) return;
        damageApplied = true;
        int damage = weaponStage.CounterDamage;
        if (PlayerAttackDamage.AddAttackPowerBonus(damage) <= 0 || playerAttack.AttackPoint == null) return;

        attackDamage.BeginAttack();
        attackDamage.Apply(playerAttack.AttackPoint, attackPolygon, damage, target =>
        {
            health.Heal(1);
            hitFeedback.Play(target);
            GetComponent<PlayerAudio>()?.PlayCounterHit();
        });
    }

    internal void NotifyAnimationEntered(PlayerCounterAnimationRole role)
    {
        if (IsExecuting && role == PlayerCounterAnimationRole.Counter)
        {
            counterStateEntered = true;
            GetComponent<PlayerAudio>()?.PlayCounterSwing();
        }
    }

    internal void NotifyAnimationExited(PlayerCounterAnimationRole role, bool completed)
    {
        if (!IsExecuting) return;
        if (role == PlayerCounterAnimationRole.Preparation && receivedAttack)
        {
            // State callback ordering must not finish the continuous counter execution.
            awaitingCounterEntry = true;
            return;
        }
        if (completed) FinishCounter();
        else Cancel();
    }

    private static bool HasAnimationRole(Animator animator, PlayerCounterAnimationRole role)
    {
        foreach (PlayerCounterStateBehaviour binding in animator.GetBehaviours<PlayerCounterStateBehaviour>())
            if (binding.Role == role) return true;
        return false;
    }

    // Only a completed final animation calls this; cancellation keeps its existing cooldown policy.
    public void FinishCounter() => Cancel();

    public void Cancel()
    {
        GetComponent<PlayerAudio>()?.StopCounterSwing();
        hitFeedback.Disable();
        player.anim?.ResetTrigger(CounterPrepareTrigger);
        player.anim?.ResetTrigger(CounterAttackTrigger);
        execution?.Finish(cooldown);
        phase = Phase.Idle;
        receivedAttack = false;
        damageApplied = false;
        counterStateEntered = false;
        awaitingCounterEntry = false;
        attackDamage?.EndAttack();
    }

    private bool CanExecute()
    {
        return execution != null && execution.CanExecute && health != null && health.isActiveAndEnabled;
    }
}
