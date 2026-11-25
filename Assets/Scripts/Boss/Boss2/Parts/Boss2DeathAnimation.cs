using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public sealed class Boss2DeathAnimation : MonoBehaviour
{
    [SerializeField] private string activeParameter = "DeathActive";
    private Animator animator;
    private bool entered;
    private bool active;
    public event Action MotionEntered;
    public bool Completed { get; private set; }
    public bool Interrupted { get; private set; }

    public void Begin()
    {
        animator = GetComponent<Animator>();
        bool found = false;
        foreach (var parameter in animator.parameters)
            if (parameter.name == activeParameter && parameter.type == AnimatorControllerParameterType.Bool) found = true;
        if (!isActiveAndEnabled || !animator.isActiveAndEnabled || !found)
            throw new InvalidOperationException("Assign an active death Animator and its Bool parameter.");
        entered = Completed = Interrupted = false;
        active = true;
        animator.SetBool(activeParameter, true);
    }

    internal void Entered()
    {
        if (!active || entered) return;
        entered = true;
        MotionEntered?.Invoke();
    }
    internal void ReachedEnd() { if (active && entered && !Interrupted) Completed = true; }
    internal void Exited() { if (active && !Completed) Interrupted = true; }
    private void OnDisable() { if (active && !Completed) Interrupted = true; }
}
