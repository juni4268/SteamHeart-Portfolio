using System;
using UnityEngine;

/// <summary>Shared stage signals for the pointing hand and the ring's Black Hole animation.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public sealed class Boss2BlackHoleAnimation : MonoBehaviour
{
    [SerializeField] private string actionParameter = "BlackHoleAction";
    private Animator animator;
    private bool prepareEntered;
    private bool activeRequested;
    private bool returnRequested;
    private bool returnFinished;
    private bool forcedReturn;
    private bool returnTravelRequested;
    private SpriteRenderer travelRenderer;
    private bool travelFacingOverride;
    private bool travelFlipX;

    public bool Active { get; private set; }
    public bool Prepared { get; private set; }
    public bool ActiveEntered { get; private set; }
    public bool Completed { get; private set; }
    public bool RecoveryCompleted { get; private set; }
    private bool interrupted;
    public bool Interrupted
    {
        get => interrupted || (Active && (animator == null || !animator.isActiveAndEnabled ||
            animator.runtimeAnimatorController == null));
        private set => interrupted = value;
    }
    public float ReturnProgress { get; private set; }

    private void OnDisable()
    {
        ClearTravelFacing();
        if (Active) Interrupted = true;
    }

    public void SetTravelFacing(bool flipX)
    {
        if (travelRenderer == null) travelRenderer = GetComponent<SpriteRenderer>();
        if (travelRenderer == null)
            throw new InvalidOperationException("Assign the Black Hole travel animation to its SpriteRenderer.");
        travelFlipX = flipX;
        travelFacingOverride = true;
        travelRenderer.flipX = flipX;
    }

    public void ClearTravelFacing() => travelFacingOverride = false;

    private void LateUpdate()
    {
        // Idle and locomotion clips also animate Flip X, so apply travel facing after Animator evaluation.
        if (travelFacingOverride && Active && !Interrupted && travelRenderer != null)
            travelRenderer.flipX = travelFlipX;
    }

    public void Begin()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (!isActiveAndEnabled || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null)
            throw new InvalidOperationException("Assign an active Black Hole Animator.");
        bool found = false;
        foreach (var parameter in animator.parameters)
            if (parameter.name == actionParameter && parameter.type == AnimatorControllerParameterType.Int) found = true;
        if (!found) throw new InvalidOperationException("Bind the Black Hole action Int parameter.");
        Active = true;
        Prepared = ActiveEntered = Completed = Interrupted = false;
        prepareEntered = activeRequested = returnRequested = returnFinished = forcedReturn = false;
        returnTravelRequested = RecoveryCompleted = false;
        ReturnProgress = 0f;
        animator.SetInteger(actionParameter, 1);
    }

    public void BeginActive()
    {
        EnsureActive();
        if (!Prepared) throw new InvalidOperationException("The Black Hole preparation has not completed.");
        activeRequested = true;
        animator.SetInteger(actionParameter, 2);
    }

    public void BeginReturn(bool force = false)
    {
        if (!Active || returnRequested) return;
        EnsureActive();
        if (!force && !Prepared) throw new InvalidOperationException("The Black Hole animation is not ready to return.");
        forcedReturn = force;
        returnRequested = true;
        animator.SetInteger(actionParameter, 3);
    }

    public void Cancel()
    {
        ClearTravelFacing();
        if (!Active) return;
        Interrupted = true;
        Active = false;
        if (animator != null && animator.isActiveAndEnabled) animator.SetInteger(actionParameter, 0);
    }

    public void BeginReturnTravel()
    {
        if (returnTravelRequested) return;
        EnsureActive();
        if (!returnRequested || !RecoveryCompleted)
            throw new InvalidOperationException("Wait for the Black Hole recovery motion before returning the ring.");
        returnTravelRequested = true;
        animator.SetInteger(actionParameter, 4);
    }

    private void EnsureActive()
    {
        if (!Active || Interrupted || !isActiveAndEnabled || !animator.isActiveAndEnabled)
            throw new InvalidOperationException("The Black Hole animation was interrupted.");
    }

    internal void Entered(Boss2BlackHoleStage stage)
    {
        if (!Active || Interrupted) return;
        if (stage == Boss2BlackHoleStage.Prepare) prepareEntered = true;
        else if (stage == Boss2BlackHoleStage.Active && activeRequested) ActiveEntered = true;
        else if (stage == Boss2BlackHoleStage.Return && !returnRequested) Interrupted = true;
    }

    internal void Progress(Boss2BlackHoleStage stage, float progress)
    {
        if (Active && !Interrupted && stage == Boss2BlackHoleStage.Return) ReturnProgress = Mathf.Clamp01(progress);
    }

    internal void ReachedEnd(Boss2BlackHoleStage stage)
    {
        if (!Active || Interrupted) return;
        if (stage == Boss2BlackHoleStage.Prepare && prepareEntered) Prepared = true;
        else if (stage == Boss2BlackHoleStage.Recovery && returnRequested) RecoveryCompleted = true;
        else if (stage == Boss2BlackHoleStage.Return && returnRequested)
        {
            returnFinished = true;
            ReturnProgress = 1f;
            animator.SetInteger(actionParameter, 0);
        }
    }

    internal void Exited(Boss2BlackHoleStage stage)
    {
        if (!Active || Interrupted) return;
        if (forcedReturn && stage != Boss2BlackHoleStage.Return) return;
        if (stage == Boss2BlackHoleStage.Prepare && (!Prepared || (!activeRequested && !returnRequested))) Interrupted = true;
        else if (stage == Boss2BlackHoleStage.Active && !returnRequested) Interrupted = true;
        else if (stage == Boss2BlackHoleStage.Recovery && (!RecoveryCompleted || !returnTravelRequested)) Interrupted = true;
        else if (stage == Boss2BlackHoleStage.Return)
        {
            if (!returnFinished) Interrupted = true;
            else { Completed = true; Active = false; }
        }
    }
}
