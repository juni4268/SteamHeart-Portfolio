using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public sealed class Boss2GroggyAnimation : MonoBehaviour
{
    [SerializeField] private string activeParameter = "GroggyActive";
    private Animator animator;
    private bool endRequested;
    public bool Active { get; private set; }
    public bool Entered { get; private set; }
    public bool Completed { get; private set; }
    public bool Interrupted { get; private set; }
    internal bool EndRequested => endRequested;

    public void Begin()
    {
        animator = GetComponent<Animator>();
        bool found = false;
        foreach (var parameter in animator.parameters)
            if (parameter.name == activeParameter && parameter.type == AnimatorControllerParameterType.Bool) found = true;
        if (!isActiveAndEnabled || !animator.isActiveAndEnabled || !found)
            throw new InvalidOperationException("Assign an active groggy Animator and its Bool parameter.");
        Entered = Completed = Interrupted = endRequested = false;
        Active = true;
        animator.SetBool(activeParameter, true);
    }

    public void End()
    {
        if (!Active || Interrupted) return;
        endRequested = true;
        animator.SetBool(activeParameter, false);
    }

    public void Cancel()
    {
        Interrupted = true;
        Active = false;
        if (animator != null && animator.isActiveAndEnabled) animator.SetBool(activeParameter, false);
    }

    internal void NotifyEntered() => Entered = true;

    internal void NotifyExited()
    {
        if (!Active || Interrupted) return;
        if (!endRequested || !Entered) { Interrupted = true; return; }
        Active = false;
        Completed = true;
    }

    private void OnDisable() { if (Active) Interrupted = true; }
}
