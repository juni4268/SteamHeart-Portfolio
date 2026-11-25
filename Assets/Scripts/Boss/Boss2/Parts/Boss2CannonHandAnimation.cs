using System;
using UnityEngine;

/// <summary>Requests cannon hand poses and receives their actual Animator state callbacks.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public sealed class Boss2CannonHandAnimation : MonoBehaviour
{
    [SerializeField] private string actionParameter = "CannonAction";
    [SerializeField] private bool flipVerticallyDuringCannon;

    private Animator animator;
    private bool prepareEntered;
    private bool fireRequested;
    private bool returnRequested;
    private bool returnFinished;
    private bool forcedReturn;
    private bool repeatRequested;
    private bool repeatExitExpected;
    private bool flipped;
    private Vector3 originalScale;

    public bool Active { get; private set; }
    public bool Prepared { get; private set; }
    public bool FireEntered { get; private set; }
    public bool FireCompleted { get; private set; }
    public bool FireRequested => fireRequested;
    public bool Completed { get; private set; }
    private bool interrupted;
    public bool Interrupted
    {
        get => interrupted || (Active && (animator == null || !animator.isActiveAndEnabled ||
            animator.runtimeAnimatorController == null));
        private set => interrupted = value;
    }

    private void Awake() => animator = GetComponent<Animator>();

    private void OnDisable()
    {
        if (Active) Interrupted = true;
        RestoreScale();
    }

    public void Begin()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (!isActiveAndEnabled || animator == null || !animator.isActiveAndEnabled ||
            animator.runtimeAnimatorController == null || string.IsNullOrWhiteSpace(actionParameter))
            throw new InvalidOperationException("Assign an active cannon hand Animator and action parameter.");

        bool found = false;
        foreach (AnimatorControllerParameter parameter in animator.parameters)
            if (parameter.name == actionParameter && parameter.type == AnimatorControllerParameterType.Int)
                found = true;
        if (!found) throw new InvalidOperationException("The cannon hand Animator is missing its action Int parameter.");

        Active = true;
        Prepared = false;
        FireEntered = false;
        FireCompleted = false;
        Completed = false;
        Interrupted = false;
        prepareEntered = false;
        fireRequested = false;
        returnRequested = false;
        returnFinished = false;
        forcedReturn = false;
        repeatRequested = repeatExitExpected = false;
        animator.SetInteger(actionParameter, 1);
    }

    public void BeginFire()
    {
        if (!Active || Interrupted || !Prepared || !isActiveAndEnabled || !animator.isActiveAndEnabled)
            throw new InvalidOperationException("The cannon hand is not prepared to fire.");
        fireRequested = true;
        animator.SetInteger(actionParameter, 2);
    }

    public void BeginReturn()
    {
        if (!Active || Interrupted || !Prepared || (fireRequested && !FireCompleted) ||
            !isActiveAndEnabled || !animator.isActiveAndEnabled)
            throw new InvalidOperationException("The cannon hand is not ready to return.");
        returnRequested = true;
        animator.SetInteger(actionParameter, 3);
    }

    public void RepeatFire()
    {
        if (!Active || Interrupted || !FireCompleted || returnRequested || !isActiveAndEnabled || !animator.isActiveAndEnabled)
            throw new InvalidOperationException("The cannon hand has not finished its previous shot.");
        repeatRequested = repeatExitExpected = true;
        FireEntered = false;
        animator.SetInteger(actionParameter, 4);
    }

    public void BeginDeathReturn()
    {
        if (!Active || returnRequested) return;
        if (Interrupted || !isActiveAndEnabled || !animator.isActiveAndEnabled)
            throw new InvalidOperationException("The cannon hand cannot return after being interrupted.");
        forcedReturn = true;
        returnRequested = true;
        animator.SetInteger(actionParameter, 3);
    }

    public void Cancel()
    {
        if (!Active) return;
        Interrupted = true;
        Active = false;
        RestoreScale();
        if (animator != null && animator.isActiveAndEnabled) animator.SetInteger(actionParameter, 0);
    }

    internal void Entered(Boss2CannonHandStage stage)
    {
        if (!Active || Interrupted) return;
        if (stage == Boss2CannonHandStage.Prepare)
        {
            prepareEntered = true;
            if (flipVerticallyDuringCannon && !flipped)
            {
                originalScale = transform.localScale;
                Vector3 scale = originalScale;
                scale.y = -scale.y;
                transform.localScale = scale;
                flipped = true;
            }
        }
        else if (stage == Boss2CannonHandStage.Fire && fireRequested)
        {
            if (repeatRequested)
            {
                repeatRequested = false;
                FireCompleted = false;
                animator.SetInteger(actionParameter, 2);
            }
            FireEntered = true;
        }
        else if (stage == Boss2CannonHandStage.Return && !returnRequested) Interrupted = true;
    }

    internal void ReachedEnd(Boss2CannonHandStage stage)
    {
        if (!Active || Interrupted) return;
        if (stage == Boss2CannonHandStage.Prepare && prepareEntered) Prepared = true;
        else if (stage == Boss2CannonHandStage.Fire && FireEntered) FireCompleted = true;
        else if (stage == Boss2CannonHandStage.Return && returnRequested && !returnFinished)
        {
            returnFinished = true;
            animator.SetInteger(actionParameter, 0);
        }
    }

    internal void Exited(Boss2CannonHandStage stage)
    {
        if (!Active || Interrupted) return;
        if (forcedReturn && stage != Boss2CannonHandStage.Return) return;
        if (stage == Boss2CannonHandStage.Fire && repeatExitExpected)
        {
            repeatExitExpected = false;
            return;
        }
        if (stage == Boss2CannonHandStage.Prepare && (!Prepared || (!fireRequested && !returnRequested))) Interrupted = true;
        else if (stage == Boss2CannonHandStage.Fire && (!FireCompleted || !returnRequested)) Interrupted = true;
        else if (stage == Boss2CannonHandStage.Return)
        {
            if (!returnFinished) Interrupted = true;
            else { Completed = true; Active = false; RestoreScale(); }
        }
    }

    private void RestoreScale()
    {
        if (!flipped) return;
        transform.localScale = originalScale;
        flipped = false;
    }
}
