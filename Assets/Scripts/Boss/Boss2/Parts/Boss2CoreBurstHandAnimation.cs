using System;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public sealed class Boss2CoreBurstHandAnimation : MonoBehaviour
{
    [SerializeField] private string actionParameter = "CoreBurstAction";
    private Animator animator;
    private bool returning;
    private bool reverseFinished;
    private bool forced;
    public bool Active { get; private set; }
    public bool Prepared { get; private set; }
    public bool Completed { get; private set; }
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
        bool found = false;
        foreach (var parameter in animator.parameters)
            if (parameter.name == actionParameter && parameter.type == AnimatorControllerParameterType.Int) found = true;
        if (!isActiveAndEnabled || !animator.isActiveAndEnabled || !found)
            throw new InvalidOperationException("Assign an active hand Animator with the Core Burst action parameter.");
        Active = true;
        Prepared = Completed = Interrupted = returning = reverseFinished = forced = false;
        animator.SetInteger(actionParameter, 1);
    }

    public void BeginReturn(bool force = false)
    {
        if (!Active || Interrupted || (!Prepared && !force)) throw new InvalidOperationException("Core Burst hand is not ready to return.");
        if (returning) return;
        forced = force;
        returning = true;
        animator.SetInteger(actionParameter, 2);
    }

    public void Cancel()
    {
        Interrupted = true;
        Active = false;
        if (animator != null && animator.isActiveAndEnabled) animator.SetInteger(actionParameter, 0);
    }

    private void OnDisable() { if (Active) Interrupted = true; }
    internal void ReachedEnd(bool reverse)
    {
        if (!Active || Interrupted) return;
        if (!reverse) Prepared = true;
        else if (returning) { reverseFinished = true; animator.SetInteger(actionParameter, 0); }
    }
    internal void Exited(bool reverse)
    {
        if (!Active || Interrupted) return;
        if (!reverse && forced && returning) return;
        if (!reverse && (!Prepared || !returning)) Interrupted = true;
        else if (reverse && !reverseFinished) Interrupted = true;
        else if (reverse) { Active = false; Completed = true; }
    }
}
