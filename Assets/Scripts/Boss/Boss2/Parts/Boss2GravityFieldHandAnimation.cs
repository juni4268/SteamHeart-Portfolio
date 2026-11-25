using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public sealed class Boss2GravityFieldHandAnimation : MonoBehaviour
{
    [SerializeField] private string gestureParameter = "GravityFieldGesture";
    [SerializeField] private Transform zoneSpawnPoint;

    private Animator animator;
    private bool reachedEnd;
    public Transform ZoneSpawnPoint => zoneSpawnPoint;
    public bool Completed { get; private set; }
    public bool Interrupted { get; private set; }

    public void Begin()
    {
        animator = GetComponent<Animator>();
        if (!isActiveAndEnabled || !animator.isActiveAndEnabled || zoneSpawnPoint == null)
            throw new InvalidOperationException("Assign an active Gravity Field hand Animator and its Zone spawn point.");
        bool found = false;
        foreach (AnimatorControllerParameter parameter in animator.parameters)
            if (parameter.name == gestureParameter && parameter.type == AnimatorControllerParameterType.Bool) found = true;
        if (!found) throw new InvalidOperationException("The hand Animator is missing its Gravity Field Bool parameter.");
        reachedEnd = false;
        Completed = false;
        Interrupted = false;
        animator.SetBool(gestureParameter, true);
    }

    public void Cancel()
    {
        Interrupted = true;
        if (animator != null && animator.isActiveAndEnabled) animator.SetBool(gestureParameter, false);
    }

    internal void ReachedEnd()
    {
        reachedEnd = true;
        animator.SetBool(gestureParameter, false);
    }

    internal void Exited()
    {
        if (reachedEnd && !Interrupted) Completed = true;
        else Interrupted = true;
    }

    private void OnDisable() => Interrupted = true;
}
