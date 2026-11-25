using System;
using UnityEngine;

/// <summary>Requests locomotion poses without taking animation control from an attacking part.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public sealed class Boss2MovementAnimation : MonoBehaviour
{
    [SerializeField] private Boss2Controller controller;
    [SerializeField] private Boss2BodyMovement bodyMovement;
    [Tooltip("Leave empty for the body Animator. Assign the hand or ring for a part Animator.")]
    [SerializeField] private Boss2PartController part;
    [SerializeField] private string movingParameter = "IsMoving";
    [Tooltip("Bind to the Animator Bool that controls left-facing movement.")]
    [SerializeField] private string facingLeftParameter;

    private Animator animator;
    private bool initialized;

    private void OnEnable()
    {
        initialized = false;
        animator = GetComponent<Animator>();
        if (controller == null || bodyMovement == null || animator.runtimeAnimatorController == null ||
            string.IsNullOrWhiteSpace(movingParameter))
            throw new InvalidOperationException("Assign the boss, body movement, Animator controller and movement Bool parameter.");

        if (!HasBoolParameter(movingParameter) ||
            (!string.IsNullOrWhiteSpace(facingLeftParameter) && !HasBoolParameter(facingLeftParameter)))
            throw new InvalidOperationException("The Animator is missing a bound movement or facing Bool parameter.");
        initialized = true;
        animator.SetBool(movingParameter, false);
        if (!string.IsNullOrWhiteSpace(facingLeftParameter)) animator.SetBool(facingLeftParameter, false);
    }

    private void Update()
    {
        bool moving = bodyMovement.IsMoving && controller.CurrentState == Boss2CombatState.Fighting && !controller.IsDead;
        if (part != null)
            moving &= part.isActiveAndEnabled && part.CurrentMotion == Boss2PartMotion.FollowingAnchor &&
                controller.PartUsage.GetOwner(part.PartId) == null;
        animator.SetBool(movingParameter, moving);
        if (!string.IsNullOrWhiteSpace(facingLeftParameter))
            animator.SetBool(facingLeftParameter, moving && bodyMovement.IsFacingLeft);
    }

    private void OnDisable()
    {
        if (initialized && animator != null && animator.isActiveAndEnabled)
        {
            animator.SetBool(movingParameter, false);
            if (!string.IsNullOrWhiteSpace(facingLeftParameter)) animator.SetBool(facingLeftParameter, false);
        }
        initialized = false;
    }

    private bool HasBoolParameter(string name)
    {
        foreach (AnimatorControllerParameter parameter in animator.parameters)
            if (parameter.name == name && parameter.type == AnimatorControllerParameterType.Bool) return true;
        return false;
    }
}
