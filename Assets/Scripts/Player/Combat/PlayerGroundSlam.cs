using UnityEngine;

[RequireComponent(typeof(Player), typeof(PlayerAttack))]
public class PlayerGroundSlam : MonoBehaviour
{
    private static readonly int GroundSlamTrigger = Animator.StringToHash("GroundSlam");

    [SerializeField, Min(0f)] private float cooldown = 8f;
    [SerializeField] private AttackPolygon attackPolygon = new AttackPolygon();

    [Header("Feedback")]
    [SerializeField] private PlayerAttackFeedback hitFeedback = new PlayerAttackFeedback();

    private PlayerAttackDamage attackDamage;
    private PlayerSkillExecution execution;
    private Player player;
    private PlayerAttack playerAttack;
    private PlayerWeaponController weaponController;
    private bool damageApplied;

    public float CooldownRemaining => execution != null ? execution.CooldownRemaining : 0f;
    public float CooldownDuration => Mathf.Max(0f, cooldown);
    public bool IsExecuting => execution != null && execution.IsExecuting;

    private void Awake()
    {
        player = GetComponent<Player>();
        playerAttack = GetComponent<PlayerAttack>();
        weaponController = GetComponent<PlayerWeaponController>();
        attackDamage = new PlayerAttackDamage(playerAttack.AttackPoint, transform, LayerMask.GetMask("Enemy"),
            playerAttack.HitEffectPrefab);
        execution = new PlayerSkillExecution(player, this);
    }

    private void OnEnable() => hitFeedback.Enable(this);

    public bool TryUse()
    {
        if (!execution.CanStart) return false;
        if (playerAttack.AttackPoint == null || weaponController.CurrentWeaponStage == null) return false;
        execution.Begin();
        damageApplied = false;
        player.StopMovementForCombat();
        player.anim.ResetTrigger(GroundSlamTrigger);
        player.anim.SetTrigger(GroundSlamTrigger);
        return true;
    }

    private void LateUpdate()
    {
        if (!IsExecuting) return;
        // An inactive Animator cannot send its state-exit notification.
        if (!execution.CanExecute || !player.CanAnimate)
            Cancel();
    }

    public void Cancel()
    {
        hitFeedback.Disable();
        if (execution == null || !execution.Finish(cooldown)) return;
        GetComponent<PlayerAudio>()?.StopGroundSlam();
        damageApplied = false;
        attackDamage.EndAttack();
        player.anim?.ResetTrigger(GroundSlamTrigger);
    }
    private void OnDisable() => Cancel();

    // Called when the Animator leaves PlayerGroundSlam, including interruptions.
    public void FinishGroundSlam() => Cancel();

    // Animation Event: contact frame, adjustable on each weapon's clip.
    public void ApplyGroundSlamDamage()
    {
        if (!IsExecuting || damageApplied) return;
        if (!execution.CanExecute || playerAttack.AttackPoint == null) return;
        WeaponStageData weaponStage = weaponController.CurrentWeaponStage;
        if (weaponStage == null) return;
        damageApplied = true;

        int baseDamage = Random.Range(weaponStage.GroundSlamMinDamage, weaponStage.GroundSlamMaxDamage + 1);
        int damage = PlayerAttackDamage.AddAttackPowerBonus(baseDamage);
        Debug.Log($"Ground Slam damage: {damage}.", this);
        if (damage == 0) return;
        int upgradedMaxDamage = PlayerAttackDamage.AddAttackPowerBonus(weaponStage.GroundSlamMaxDamage);
        float cameraImpactMultiplier = (float)damage / upgradedMaxDamage;

        attackDamage.BeginAttack();
        attackDamage.Apply(playerAttack.AttackPoint, attackPolygon, baseDamage,
            target => hitFeedback.Play(target, cameraImpactMultiplier));
    }
}
