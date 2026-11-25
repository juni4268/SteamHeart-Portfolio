using System;
using UnityEngine;

/// <summary>Requests the body/ring pose for the duration controlled by Core Burst.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public sealed class Boss2CoreBurstPose : MonoBehaviour
{
    [SerializeField] private string activeParameter = "CoreBurstActive";
    private Animator animator;
    private bool endRequested;
    public bool Active { get; private set; }
    public bool Entered { get; private set; }
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
            if (parameter.name == activeParameter && parameter.type == AnimatorControllerParameterType.Bool) found = true;
        if (!isActiveAndEnabled || !animator.isActiveAndEnabled || !found)
            throw new InvalidOperationException("Assign an active Core Burst pose Animator and its Bool parameter.");
        Active = true;
        Entered = Completed = Interrupted = endRequested = false;
        animator.SetBool(activeParameter, true);
    }

    public void End()
    {
        if (!Active || Interrupted) return;
        if (!Entered) { Cancel(); return; }
        endRequested = true;
        animator.SetBool(activeParameter, false);
    }

    public void Cancel()
    {
        Interrupted = true;
        Active = false;
        if (animator != null && animator.isActiveAndEnabled) animator.SetBool(activeParameter, false);
    }

    private void OnDisable() { if (Active) Interrupted = true; }
    internal void NotifyEntered()
    {
        if (!Active || endRequested) { Interrupted = true; return; }
        Entered = true;
    }
    internal void NotifyExited()
    {
        if (!Active || Interrupted) return;
        if (!endRequested) { Interrupted = true; return; }
        Active = false;
        Completed = true;
    }
}
