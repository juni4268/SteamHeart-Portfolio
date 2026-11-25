using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public enum SteamVentPhase { Waiting, Starting, Attacking, Ending }

[DisallowMultipleComponent]
public sealed class SteamVentTrap : MonoBehaviour
{
    [Header("Cycle")]
    [SerializeField, Min(0.01f)] private float attackDuration = 1f;
    [SerializeField, Min(0f)] private float waitDuration = 2f;

    [Header("Attack Area")]
    [Tooltip("Dedicated attack collider. Enabled only while attacking; do not use the trap's solid body collider.")]
    [SerializeField] private BoxCollider2D attackCollider;
    [FormerlySerializedAs("playerLayer")]
    [SerializeField] private LayerMask targetLayers;

    [Header("Steam Animation")]
    [SerializeField] private Animator attackAnimator;
    [Tooltip("Bool parameter requesting the start/attack sequence, followed by its ending motion when cleared.")]
    [SerializeField] private string attackParameter = "IsAttacking";
    [Tooltip("Renderer animated throughout waiting, starting, attacking and ending.")]
    [SerializeField] private SpriteRenderer attackRenderer;

    [Header("Warning")]
    [Tooltip("Warning within the existing wait time; this does not extend the attack cycle.")]
    [SerializeField, Min(0f)] private float warningDuration = 0.6f;
    [SerializeField] private Boss1CrushWarningArea warningArea;

    [Header("Knockback")]
    [Tooltip("X is the speed away from the attack area's center; Y is the upward speed.")]
    [SerializeField] private Vector2 knockbackVelocity = new Vector2(8f, 5f);
    [SerializeField, Min(0f)] private float controlLockDuration = 0.2f;

    private readonly List<Collider2D> overlaps = new List<Collider2D>();
    private readonly HashSet<IDamageable> touchingTargets = new HashSet<IDamageable>();
    private readonly HashSet<IDamageable> damagedTargets = new HashSet<IDamageable>();
    private float remainingTime;
    private bool isAttacking;
    private bool isWarning;
    private bool animationControlled;
    private bool attackRequested;

    public bool IsAttacking => isAttacking;
    public bool IsWarning => isWarning;
    public SteamVentPhase Phase { get; private set; }

    private void Reset()
    {
        attackCollider = GetComponent<BoxCollider2D>();
        targetLayers = LayerMask.GetMask("Player", "Enemy");
    }

    private void Awake()
    {
        if (attackCollider == null)
        {
            Debug.LogError($"{nameof(SteamVentTrap)} requires an attack collider.", this);
            enabled = false;
            return;
        }

        attackCollider.isTrigger = true;
    }

    private void OnEnable()
    {
        animationControlled = attackAnimator != null && attackAnimator.runtimeAnimatorController != null;
        Phase = animationControlled ? SteamVentPhase.Ending : SteamVentPhase.Waiting;
        attackRequested = false;
        SetAttackArea(false);
        RequestAttackAnimation(false);
        if (attackRenderer != null) attackRenderer.enabled = true;
        remainingTime = waitDuration;
    }

    public void NotifyAnimationPhaseEntered(Animator source, SteamVentPhase enteredPhase)
    {
        if (!isActiveAndEnabled || !animationControlled || source != attackAnimator) return;
        if ((enteredPhase == SteamVentPhase.Starting || enteredPhase == SteamVentPhase.Attacking)
            && !attackRequested) return;

        Phase = enteredPhase;
        SetAttackArea(enteredPhase == SteamVentPhase.Attacking);
        if (enteredPhase == SteamVentPhase.Waiting)
        {
            attackRequested = false;
            RequestAttackAnimation(false);
            remainingTime = waitDuration;
        }
        else if (enteredPhase == SteamVentPhase.Attacking)
            remainingTime = attackDuration;
        UpdateWarning();
    }

    public void SynchronizeWaitingAnimation(Animator source)
    {
        // Re-enabling the component need not make an already waiting Animator enter its State again.
        if (Phase == SteamVentPhase.Ending && !attackRequested)
            NotifyAnimationPhaseEntered(source, SteamVentPhase.Waiting);
    }

    public void NotifyAttackAnimationExited(Animator source)
    {
        if (!isActiveAndEnabled || source != attackAnimator || !isAttacking) return;
        // An early exit cancels damage; it is not treated as a completed attack duration.
        EndAttack();
    }

    private void FixedUpdate()
    {
        if (!isActiveAndEnabled || attackCollider == null || Time.timeScale <= 0f) return;
        if (animationControlled && (attackAnimator == null || !attackAnimator.isActiveAndEnabled
            || attackAnimator.runtimeAnimatorController == null))
        {
            EndAttack();
            UpdateWarning();
            return;
        }

        if (Phase == SteamVentPhase.Waiting)
        {
            remainingTime -= Time.fixedDeltaTime;
            if (remainingTime <= 0f)
            {
                attackRequested = true;
                Phase = animationControlled ? SteamVentPhase.Starting : SteamVentPhase.Attacking;
                RequestAttackAnimation(true);
                if (!animationControlled)
                {
                    SetAttackArea(true);
                    remainingTime = attackDuration;
                }
            }
        }
        else if (Phase == SteamVentPhase.Attacking)
        {
            remainingTime -= Time.fixedDeltaTime;
            if (remainingTime <= 0f) EndAttack();
        }

        UpdateWarning();

        if (isAttacking) CheckAttackArea();
    }

    private void UpdateWarning()
    {
        bool shouldWarn = Phase == SteamVentPhase.Waiting && warningDuration > 0f
            && remainingTime <= Mathf.Min(warningDuration, waitDuration);
        if (shouldWarn != isWarning)
        {
            isWarning = shouldWarn;
            if (warningArea != null)
            {
                if (isWarning)
                {
                    var geometry = new AttackBoxGeometry(attackCollider.transform, attackCollider.offset, attackCollider.size);
                    warningArea.ShowAcross(new Bounds(
                        new Vector3(geometry.Center.x, geometry.Center.y, attackCollider.transform.position.z),
                        geometry.Size), Mathf.Min(warningDuration, waitDuration));
                    warningArea.transform.rotation = attackCollider.transform.rotation;
                }
                else warningArea.Hide();
            }
        }
    }

    private void SetAttackArea(bool attacking)
    {
        isAttacking = attacking;
        if (attackCollider != null) attackCollider.enabled = attacking;
        overlaps.Clear();
        touchingTargets.Clear();
        damagedTargets.Clear();
    }

    private void RequestAttackAnimation(bool attacking)
    {
        if (attackAnimator != null && attackAnimator.isActiveAndEnabled
            && attackAnimator.runtimeAnimatorController != null && !string.IsNullOrEmpty(attackParameter))
            attackAnimator.SetBool(attackParameter, attacking);
    }

    private void EndAttack()
    {
        attackRequested = false;
        SetAttackArea(false);
        RequestAttackAnimation(false);
        Phase = animationControlled ? SteamVentPhase.Ending : SteamVentPhase.Waiting;
        if (!animationControlled) remainingTime = waitDuration;
    }

    private void CheckAttackArea()
    {
        touchingTargets.Clear();
        if (attackCollider.isActiveAndEnabled)
        {
            var filter = new ContactFilter2D { useTriggers = true };
            filter.SetLayerMask(targetLayers);
            Physics2D.SyncTransforms();
            attackCollider.Overlap(filter, overlaps);
            foreach (Collider2D overlap in overlaps)
            {
                IDamageable target = overlap.GetComponentInParent<IDamageable>();
                if (target != null && !target.IsDead) touchingTargets.Add(target);
            }
        }

        // A target can be hit again only after all of its colliders have left.
        damagedTargets.IntersectWith(touchingTargets);
        Vector2 sourcePosition = attackCollider.bounds.center;
        foreach (IDamageable target in touchingTargets)
        {
            Component component = target as Component;
            if (component == null || damagedTargets.Contains(target)) continue;

            float direction = component.transform.position.x >= sourcePosition.x ? 1f : -1f;
            Vector2 velocity = new Vector2(direction * knockbackVelocity.x, knockbackVelocity.y);
            bool accepted = target.TryTakeDamage(new DamageInfo(1, sourcePosition, velocity, controlLockDuration));
            if (!isActiveAndEnabled) return;
            if (accepted) damagedTargets.Add(target);
        }
    }

    private void OnDisable()
    {
        EndAttack();
        remainingTime = 0f;
        isWarning = false;
        if (warningArea != null) warningArea.Hide();
    }

    private void OnValidate()
    {
        attackDuration = Mathf.Max(0.01f, attackDuration);
        waitDuration = Mathf.Max(0f, waitDuration);
        warningDuration = Mathf.Max(0f, warningDuration);
        knockbackVelocity.x = Mathf.Max(0f, knockbackVelocity.x);
        knockbackVelocity.y = Mathf.Max(0f, knockbackVelocity.y);
        controlLockDuration = Mathf.Max(0f, controlLockDuration);
    }
}
