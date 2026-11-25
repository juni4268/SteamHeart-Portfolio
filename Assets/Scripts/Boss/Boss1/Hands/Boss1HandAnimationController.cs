using UnityEngine;

public enum Boss1HandAnimationPhase { Idle, Prepare, Attack, Return, SecondPrepare }

[DisallowMultipleComponent]
public sealed class Boss1HandAnimationController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Runtime State")]
    [SerializeField] private Boss1HandAnimationPhase currentPhase = Boss1HandAnimationPhase.Idle;

    private Boss1HandAnimationTrack currentTrack;
    private Boss1HandAnimationBinding currentBinding;
    private int currentTriggerHash;
    private bool stateEntered;
    private bool isHolding;
    private Sprite idleSprite;
    private bool idleFlipX;
    private bool poseFrozen;
    private float speedBeforeFreeze;

    public Boss1HandAnimationPhase CurrentPhase => currentPhase;
    public bool IsHolding => isHolding;
    public bool IsPlaying => currentPhase != Boss1HandAnimationPhase.Idle && !isHolding;
    public float NormalizedProgress { get; private set; }

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>(true);
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        if (spriteRenderer == null) return;
        idleSprite = spriteRenderer.sprite;
        idleFlipX = spriteRenderer.flipX;
    }

    private void Update()
    {
        if (currentPhase != Boss1HandAnimationPhase.Idle &&
            (animator == null || !animator.isActiveAndEnabled))
            CancelPlayback();
    }

    private void OnDisable() => ForceIdle();

    public bool PlayPrepare(Boss1HandAnimationTrack track)
    {
        ForceIdle();
        currentTrack = track;
        if (track == null || track.PrepareRole == Boss1HandAnimationRole.None) return false;
        if (spriteRenderer != null) spriteRenderer.flipX = track.FlipX;
        return PlayPhase(track.PrepareRole, Boss1HandAnimationPhase.Prepare);
    }

    public bool PlayAttack()
    {
        if (currentTrack == null || currentTrack.AttackRole == Boss1HandAnimationRole.None)
        {
            currentPhase = Boss1HandAnimationPhase.Attack;
            isHolding = true;
            return false;
        }
        return PlayPhase(currentTrack.AttackRole, Boss1HandAnimationPhase.Attack);
    }

    public void FreezeCurrentPose()
    {
        if (poseFrozen) return;
        ResetCurrentTrigger();
        CompleteReturn(false);
        if (animator == null) return;
        speedBeforeFreeze = animator.speed;
        animator.speed = 0f;
        poseFrozen = true;
    }

    public bool PlaySecondPrepare(Boss1HandAnimationTrack track)
    {
        if (track != null) currentTrack = track;
        if (currentTrack == null || currentTrack.SecondPrepareRole == Boss1HandAnimationRole.None) return false;
        return PlayPhase(currentTrack.SecondPrepareRole, Boss1HandAnimationPhase.SecondPrepare);
    }

    public bool PlayReturn()
    {
        if (currentTrack == null || currentTrack.ReturnRole == Boss1HandAnimationRole.None)
        {
            ForceIdle();
            return false;
        }
        return PlayPhase(currentTrack.ReturnRole, Boss1HandAnimationPhase.Return);
    }

    public void ForceIdle()
    {
        if (poseFrozen)
        {
            if (animator != null) animator.speed = speedBeforeFreeze;
            poseFrozen = false;
        }
        ResetCurrentTrigger();
        CompleteReturn();
        if (animator != null && animator.runtimeAnimatorController != null && animator.isActiveAndEnabled)
        {
            animator.Rebind();
            animator.Update(0f);
        }
        RestoreIdleSprite();
    }

    private bool PlayPhase(Boss1HandAnimationRole role, Boss1HandAnimationPhase phase)
    {
        if (!isActiveAndEnabled || animator == null || !animator.isActiveAndEnabled ||
            animator.runtimeAnimatorController == null) return false;
        Boss1HandAnimationBinding binding = FindBinding(role);
        if (binding == null || !binding.IsAssigned || !HasTrigger(binding.TriggerHash))
        {
            Debug.LogWarning($"{nameof(Boss1HandAnimationController)} needs a unique role binding with a valid Trigger for {role}.", this);
            return false;
        }
        ResetCurrentTrigger();
        currentPhase = phase;
        currentBinding = binding;
        currentTriggerHash = binding.TriggerHash;
        stateEntered = false;
        isHolding = false;
        NormalizedProgress = 0f;
        animator.SetTrigger(currentTriggerHash);
        return true;
    }

    internal void NotifyStateEntered(Boss1HandAnimationBinding binding)
    {
        if (binding == currentBinding) stateEntered = true;
    }

    internal void NotifyStateCompleted(Boss1HandAnimationBinding binding)
    {
        if (stateEntered && binding == currentBinding) isHolding = true;
    }

    internal void NotifyStateProgress(Boss1HandAnimationBinding binding, float normalizedTime)
    {
        if (stateEntered && binding == currentBinding)
            NormalizedProgress = Mathf.Clamp01(normalizedTime);
    }

    internal void NotifyStateExited(Boss1HandAnimationBinding binding, bool completed)
    {
        if (!stateEntered || binding != currentBinding) return;
        if (currentPhase == Boss1HandAnimationPhase.Return && completed) CompleteReturn();
        else CancelPlayback();
    }

    private void CancelPlayback()
    {
        ResetCurrentTrigger();
        CompleteReturn();
        GetComponentInParent<Boss1Controller>()?.CancelCurrentPattern();
        ForceIdle();
    }

    private Boss1HandAnimationBinding FindBinding(Boss1HandAnimationRole role)
    {
        Boss1HandAnimationBinding match = null;
        foreach (Boss1HandAnimationBinding binding in animator.GetBehaviours<Boss1HandAnimationBinding>())
        {
            if (binding.Role != role) continue;
            if (match != null) return null;
            match = binding;
        }
        return match;
    }

    private bool HasTrigger(int parameterHash)
    {
        foreach (AnimatorControllerParameter parameter in animator.parameters)
            if (parameter.nameHash == parameterHash && parameter.type == AnimatorControllerParameterType.Trigger)
                return true;
        return false;
    }

    private void ResetCurrentTrigger()
    {
        if (animator != null && currentTriggerHash != 0) animator.ResetTrigger(currentTriggerHash);
        currentTriggerHash = 0;
    }

    private void CompleteReturn(bool restoreSprite = true)
    {
        currentTrack = null;
        currentBinding = null;
        currentTriggerHash = 0;
        currentPhase = Boss1HandAnimationPhase.Idle;
        stateEntered = false;
        isHolding = false;
        NormalizedProgress = 0f;
        if (restoreSprite) RestoreIdleSprite();
    }

    private void RestoreIdleSprite()
    {
        if (spriteRenderer == null) return;
        spriteRenderer.sprite = idleSprite;
        spriteRenderer.flipX = idleFlipX;
    }
}
