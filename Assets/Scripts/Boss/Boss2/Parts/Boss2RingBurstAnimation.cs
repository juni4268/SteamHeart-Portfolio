using System;
using UnityEngine;

/// <summary>Semantic ring controls; state names and graph locations are owned by the Animator.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public sealed class Boss2RingBurstAnimation : MonoBehaviour
{
    [SerializeField] private string actionParameter = "RingBurstAction";
    private Animator animator;
    private bool prepareFinished;
    private bool chargeRequested;
    private bool loopRequested;
    private bool attackRequested;
    private bool returnRequested;
    private bool returnChargeFinished;
    private bool returnPrepareFinished;
    private bool earlyReturn;
    private Boss2Audio.Playback chargeAudio;
    private Boss2Audio.Playback attackAudio;

    public event Action AttackMotionCompleted;

    public bool Active { get; private set; }
    public bool Prepared { get; private set; }
    public bool ChargeEntered { get; private set; }
    public bool LoopEntered { get; private set; }
    public bool AttackEntered { get; private set; }
    public bool Released { get; private set; }
    public bool AttackCompleted { get; private set; }
    public bool ReturnChargeEntered { get; private set; }
    public bool ReturnPrepareEntered { get; private set; }
    public bool Completed { get; private set; }
    public bool CanReturnEarly => prepareFinished && (!chargeRequested || Prepared);
    private bool interrupted;
    public bool Interrupted
    {
        get => interrupted || (Active && (animator == null || !animator.isActiveAndEnabled ||
            animator.runtimeAnimatorController == null));
        private set => interrupted = value;
    }

    public void Begin()
    {
        animator = GetComponent<Animator>();
        bool bound = false;
        if (animator.runtimeAnimatorController != null)
            foreach (AnimatorControllerParameter parameter in animator.parameters)
                if (parameter.name == actionParameter && parameter.type == AnimatorControllerParameterType.Int) bound = true;
        if (!isActiveAndEnabled || !animator.isActiveAndEnabled || !bound)
            throw new InvalidOperationException("Assign an active ring Animator and its action Int parameter.");
        Active = true;
        Prepared = ChargeEntered = LoopEntered = AttackEntered = Released = AttackCompleted = Completed = Interrupted = false;
        ReturnChargeEntered = ReturnPrepareEntered = false;
        prepareFinished = chargeRequested = loopRequested = attackRequested = returnRequested = false;
        returnChargeFinished = returnPrepareFinished = false;
        earlyReturn = false;
        animator.SetInteger(actionParameter, 1);
    }

    public void BeginCharge()
    {
        RequireActive();
        chargeRequested = true;
        if (prepareFinished) animator.SetInteger(actionParameter, 4);
    }

    public void BeginLoop()
    {
        RequireActive();
        if (!Prepared) throw new InvalidOperationException("The ring has not completed its preparation.");
        loopRequested = true;
        animator.SetInteger(actionParameter, 2);
    }

    public void BeginAttack()
    {
        RequireActive();
        if (!LoopEntered) throw new InvalidOperationException("The ring has not entered its charging loop.");
        attackRequested = true;
        animator.SetInteger(actionParameter, 3);
    }

    public void BeginReturn()
    {
        RequireActive();
        if (!AttackCompleted) throw new InvalidOperationException("The ring has not completed its attack motion.");
        if (returnRequested) return;
        returnRequested = true;
        animator.SetInteger(actionParameter, 5);
    }

    public void BeginEarlyReturn()
    {
        RequireActive();
        if (!CanReturnEarly || AttackEntered)
            throw new InvalidOperationException("Finish the current ring preparation before returning.");
        if (returnRequested) return;
        earlyReturn = returnRequested = true;
        chargeAudio?.FadeOut();
        if (ChargeEntered) animator.SetInteger(actionParameter, 5);
        else
        {
            returnChargeFinished = true;
            animator.SetInteger(actionParameter, 6);
        }
    }

    // Authored on the release flash in the explicitly assigned Attack clip.
    public void ReleaseBurst()
    {
        if (!Active || Interrupted || !AttackEntered || Released) return;
        Released = true;
        chargeAudio?.FadeOut(0.1f);
        attackAudio = GetComponentInParent<Boss2Audio>()?.Play(Boss2Audio.Sound.RingAttack);
    }

    public void Cancel()
    {
        chargeAudio?.FadeOut();
        attackAudio?.FadeOut();
        if (!Active) return;
        Active = false;
        Interrupted = true;
        if (animator != null && animator.isActiveAndEnabled) animator.SetInteger(actionParameter, 0);
    }

    internal void Entered(Boss2RingBurstStage stage)
    {
        if (!Active || Interrupted) return;
        if (stage == Boss2RingBurstStage.Charge)
        {
            if (!chargeRequested || !prepareFinished) { Interrupted = true; return; }
            ChargeEntered = true;
            chargeAudio?.FadeOut();
            chargeAudio = GetComponentInParent<Boss2Audio>()?.Play(Boss2Audio.Sound.RingCharge,
                loop: true, fadeIn: 0.1f);
        }
        else if (stage == Boss2RingBurstStage.Loop)
        {
            if (!loopRequested || !Prepared) { Interrupted = true; return; }
            LoopEntered = true;
        }
        else if (stage == Boss2RingBurstStage.Attack)
        {
            if (!attackRequested || !LoopEntered) { Interrupted = true; return; }
            AttackEntered = true;
        }
        else if (stage == Boss2RingBurstStage.ReturnCharge)
        {
            if (!returnRequested || (!AttackCompleted && !earlyReturn)) { Interrupted = true; return; }
            ReturnChargeEntered = true;
        }
        else if (stage == Boss2RingBurstStage.ReturnPrepare)
        {
            if (!returnRequested || !returnChargeFinished) { Interrupted = true; return; }
            ReturnPrepareEntered = true;
        }
    }

    internal void ReachedEnd(Boss2RingBurstStage stage)
    {
        if (!Active || Interrupted) return;
        if (stage == Boss2RingBurstStage.Prepare)
        {
            prepareFinished = true;
            if (chargeRequested) animator.SetInteger(actionParameter, 4);
        }
        else if (stage == Boss2RingBurstStage.Charge && ChargeEntered) Prepared = true;
        else if (stage == Boss2RingBurstStage.Attack)
        {
            if (!Released) { Interrupted = true; return; }
            AttackCompleted = true;
            AttackMotionCompleted?.Invoke();
        }
        else if (stage == Boss2RingBurstStage.ReturnCharge && ReturnChargeEntered)
        {
            returnChargeFinished = true;
            animator.SetInteger(actionParameter, 6);
        }
        else if (stage == Boss2RingBurstStage.ReturnPrepare && ReturnPrepareEntered)
        {
            returnPrepareFinished = true;
            animator.SetInteger(actionParameter, 0);
        }
    }

    internal void Exited(Boss2RingBurstStage stage)
    {
        if (!Active || Interrupted) return;
        if (stage == Boss2RingBurstStage.Prepare && (!prepareFinished || (!chargeRequested && !returnRequested))) Interrupted = true;
        else if (stage == Boss2RingBurstStage.Charge && (!Prepared || (!loopRequested && !returnRequested))) Interrupted = true;
        else if (stage == Boss2RingBurstStage.Loop && !attackRequested && !returnRequested) Interrupted = true;
        else if (stage == Boss2RingBurstStage.Attack)
        {
            if (!AttackCompleted || !returnRequested) Interrupted = true;
        }
        else if (stage == Boss2RingBurstStage.ReturnCharge && !returnChargeFinished) Interrupted = true;
        else if (stage == Boss2RingBurstStage.ReturnPrepare)
        {
            if (!returnPrepareFinished) Interrupted = true;
            else { Completed = true; Active = false; }
        }
    }

    private void RequireActive()
    {
        if (!Active || Interrupted || !isActiveAndEnabled || animator == null || !animator.isActiveAndEnabled)
            throw new InvalidOperationException("The ring burst animation was interrupted.");
    }

    private void OnDisable()
    {
        chargeAudio?.FadeOut();
        attackAudio?.FadeOut();
        if (Active) Interrupted = true;
    }
}
