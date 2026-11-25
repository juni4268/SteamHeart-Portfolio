using System;
using UnityEngine;

/// <summary>Controls the upward gesture using an explicit Animator parameter.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public sealed class Boss2TrackingLaserHandAnimation : MonoBehaviour
{
    [SerializeField] private string gestureParameter = "TrackingLaserGesture";

    private Animator animator;
    private bool forwardEntered;
    private bool reverseEntered;
    private bool reverseRequested;
    private bool reverseFinished;
    private bool waitingForIdle;
    private bool active;
    private bool forcedReturn;

    public bool ForwardCompleted { get; private set; }
    public bool Active => active;
    public bool Completed { get; private set; }
    private bool interrupted;
    public bool Interrupted
    {
        get => interrupted || (Active && (animator == null || !animator.isActiveAndEnabled ||
            animator.runtimeAnimatorController == null));
        private set => interrupted = value;
    }

    private void Awake() => animator = GetComponent<Animator>();

    private void OnDisable() => Interrupted = true;

    public void Begin()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (!isActiveAndEnabled || animator == null || !animator.isActiveAndEnabled ||
            animator.runtimeAnimatorController == null || string.IsNullOrWhiteSpace(gestureParameter))
            throw new InvalidOperationException("Assign an active hand Animator and its gesture parameter.");

        bool found = false;
        foreach (AnimatorControllerParameter parameter in animator.parameters)
            if (parameter.name == gestureParameter && parameter.type == AnimatorControllerParameterType.Int)
                found = true;
        if (!found) throw new InvalidOperationException("The hand Animator is missing its gesture Int parameter.");

        ForwardCompleted = false;
        Completed = false;
        Interrupted = false;
        forwardEntered = false;
        reverseEntered = false;
        reverseRequested = false;
        reverseFinished = false;
        waitingForIdle = false;
        active = true;
        forcedReturn = false;
        animator.SetInteger(gestureParameter, 1);
    }

    public void BeginReturn()
    {
        if (!ForwardCompleted || Interrupted) throw new InvalidOperationException("The hand gesture has not completed.");
        reverseRequested = true;
        animator.SetInteger(gestureParameter, 2);
    }

    public void BeginDeathReturn()
    {
        if (!active || reverseRequested) return;
        if (Interrupted || !isActiveAndEnabled || !animator.isActiveAndEnabled)
            throw new InvalidOperationException("The hand gesture cannot return after being interrupted.");
        forcedReturn = true;
        reverseRequested = true;
        animator.SetInteger(gestureParameter, 2);
    }

    public void Cancel()
    {
        Interrupted = true;
        active = false;
        waitingForIdle = false;
        if (animator != null && animator.isActiveAndEnabled) animator.SetInteger(gestureParameter, 0);
    }

    internal void Entered(bool reverse)
    {
        if (reverse) reverseEntered = true;
        else forwardEntered = true;
    }

    internal void ReachedEnd(bool reverse)
    {
        if (Interrupted) return;
        if (!reverse && forwardEntered) ForwardCompleted = true;
        else if (reverse && reverseEntered && !reverseFinished)
        {
            reverseFinished = true;
            waitingForIdle = true;
            animator.SetInteger(gestureParameter, 0);
        }
    }

    internal void Exited(bool reverse)
    {
        if (Interrupted) return;
        if (forcedReturn && !reverse) return;
        if (reverse && reverseFinished && waitingForIdle) { Completed = true; active = false; }
        else if (reverse || !ForwardCompleted || !reverseRequested) Interrupted = true;
    }
}
