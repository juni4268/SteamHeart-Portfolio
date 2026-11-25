using UnityEngine;

// Shared start conditions and one-time cooldown completion; skill phases stay in each skill.
public sealed class PlayerSkillExecution
{
    private readonly Player player;
    private readonly Behaviour owner;
    private float nextUseTime;

    public PlayerSkillExecution(Player player, Behaviour owner)
    {
        this.player = player;
        this.owner = owner;
    }

    public bool IsExecuting { get; private set; }
    public float CooldownRemaining => Mathf.Max(0f, nextUseTime - Time.time);
    public bool CanExecute => player != null && player.CanExecuteCombatAction(owner);
    public bool CanStart => CanStartWithControl(player != null && player.CanStartAction);

    public bool CanStartWithControl(bool canStartAction) => CanExecute && canStartAction && player.CanAnimate &&
        Time.time >= nextUseTime;

    public void Begin() => IsExecuting = true;

    public bool Finish(float cooldown)
    {
        if (!IsExecuting) return false;
        IsExecuting = false;
        nextUseTime = Time.time + Mathf.Max(0f, cooldown);
        return true;
    }
}
