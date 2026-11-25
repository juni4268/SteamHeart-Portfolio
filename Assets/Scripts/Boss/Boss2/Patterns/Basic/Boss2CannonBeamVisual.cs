using System;
using UnityEngine;

/// <summary>Displays the authored cannon beam frames at the gameplay beam's origin and length.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator), typeof(SpriteRenderer))]
public sealed class Boss2CannonBeamVisual : MonoBehaviour
{
    [SerializeField] private Transform startMarker;
    [SerializeField] private Transform endMarker;
    [Tooltip("Thickness in local units represented by the authored active beam.")]
    [SerializeField, Min(0.001f)] private float referenceThickness = 0.1551724f;
    [SerializeField] private string phaseParameter = "CannonBeamPhase";
    [SerializeField] private string speedParameter = "CannonBeamSpeed";

    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private float requestedFireDuration;
    private bool aimEntered;
    private bool fireRequested;
    private bool fadeRequested;
    private bool fadeFinished;
    private Boss2Audio audioOwner;
    private Boss2Audio.Playback audioPlayback;

    public void BindAudio(Boss2Audio audio) => audioOwner = audio;

    public bool Active { get; private set; }
    public bool AimCompleted { get; private set; }
    public bool FireEntered { get; private set; }
    public bool FireCompleted { get; private set; }
    public bool Completed { get; private set; }
    public bool Interrupted { get; private set; }

    private void Awake() => ResolveComponents();
    private void OnDisable() { audioPlayback?.FadeOut(0.1f); if (Active) Interrupted = true; }

    public void BeginAim()
    {
        ResolveComponents();
        if (!isActiveAndEnabled || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null ||
            startMarker == null || endMarker == null || startMarker.parent != transform || endMarker.parent != transform ||
            endMarker.localPosition.x <= startMarker.localPosition.x || referenceThickness <= 0f ||
            string.IsNullOrWhiteSpace(phaseParameter) || string.IsNullOrWhiteSpace(speedParameter))
            throw new InvalidOperationException("Assign the beam Animator, geometry markers and parameters.");

        bool phaseFound = false;
        bool speedFound = false;
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name == phaseParameter && parameter.type == AnimatorControllerParameterType.Int) phaseFound = true;
            if (parameter.name == speedParameter && parameter.type == AnimatorControllerParameterType.Float) speedFound = true;
        }
        if (!phaseFound || !speedFound) throw new InvalidOperationException("The beam Animator is missing its parameters.");

        Active = true;
        AimCompleted = false;
        FireEntered = false;
        FireCompleted = false;
        Completed = false;
        Interrupted = false;
        aimEntered = false;
        fireRequested = false;
        fadeRequested = false;
        fadeFinished = false;
        animator.SetFloat(speedParameter, 1f);
        animator.SetInteger(phaseParameter, 1);
    }

    public void SetGeometry(Vector3 origin, Vector2 direction, float distance, float thickness)
    {
        if (startMarker == null || endMarker == null || referenceThickness <= 0f || direction.sqrMagnitude < 0.0001f)
            throw new InvalidOperationException("The beam geometry or direction is invalid.");
        float nativeLength = endMarker.localPosition.x - startMarker.localPosition.x;
        if (nativeLength <= 0f) throw new InvalidOperationException("The beam end must be to the right of its start.");
        Vector3 scale = new Vector3(Mathf.Max(0f, distance) / nativeLength, Mathf.Max(0f, thickness) / referenceThickness, 1f);
        Quaternion rotation = Quaternion.FromToRotation(Vector3.right, direction.normalized);
        transform.SetPositionAndRotation(origin - rotation * Vector3.Scale(startMarker.localPosition, scale), rotation);
        transform.localScale = scale;
        spriteRenderer.enabled = Active && !fadeFinished && distance > 0f && thickness > 0f;
    }

    public void BeginFire(float duration)
    {
        if (!Active || Interrupted || !AimCompleted || duration <= 0f || !isActiveAndEnabled || !animator.isActiveAndEnabled)
            throw new InvalidOperationException("The cannon beam is not ready to fire.");
        requestedFireDuration = duration;
        fireRequested = true;
        animator.SetInteger(phaseParameter, 2);
    }

    public void BeginFade()
    {
        if (!Active || Interrupted || !FireCompleted) throw new InvalidOperationException("The cannon beam has not finished firing.");
        fadeRequested = true;
        animator.SetInteger(phaseParameter, 3);
    }

    public void Cancel()
    {
        audioPlayback?.FadeOut(0.1f);
        if (!Active) return;
        Interrupted = true;
        Active = false;
        if (animator != null && animator.isActiveAndEnabled) animator.SetInteger(phaseParameter, 0);
        if (spriteRenderer != null) spriteRenderer.enabled = false;
    }

    internal void Entered(Boss2CannonBeamStage stage, AnimatorStateInfo stateInfo)
    {
        if (!Active || Interrupted) return;
        if (stage == Boss2CannonBeamStage.Aim) aimEntered = true;
        else if (stage == Boss2CannonBeamStage.Fire && fireRequested)
        {
            if (stateInfo.length <= 0f) { Interrupted = true; return; }
            animator.SetFloat(speedParameter, stateInfo.length / requestedFireDuration);
            FireEntered = true;
            audioPlayback?.FadeOut(0.1f);
            audioPlayback = audioOwner != null ? audioOwner.Play(Boss2Audio.Sound.CannonBeam, loop: true) : null;
        }
        else if (stage == Boss2CannonBeamStage.Fade && !fadeRequested) Interrupted = true;
    }

    internal void ReachedEnd(Boss2CannonBeamStage stage)
    {
        if (!Active || Interrupted) return;
        if (stage == Boss2CannonBeamStage.Aim && aimEntered) AimCompleted = true;
        else if (stage == Boss2CannonBeamStage.Fire && FireEntered)
        {
            FireCompleted = true;
            audioPlayback?.FadeOut(0.1f);
        }
        else if (stage == Boss2CannonBeamStage.Fade && fadeRequested && !fadeFinished)
        {
            fadeFinished = true;
            spriteRenderer.enabled = false;
            animator.SetInteger(phaseParameter, 0);
        }
    }

    internal void Exited(Boss2CannonBeamStage stage)
    {
        if (stage == Boss2CannonBeamStage.Fire) audioPlayback?.FadeOut(0.1f);
        if (!Active || Interrupted) return;
        if (stage == Boss2CannonBeamStage.Aim && (!AimCompleted || !fireRequested)) Interrupted = true;
        else if (stage == Boss2CannonBeamStage.Fire && (!FireCompleted || !fadeRequested)) Interrupted = true;
        else if (stage == Boss2CannonBeamStage.Fade)
        {
            if (!fadeFinished) Interrupted = true;
            else { Completed = true; Active = false; }
        }
    }

    private void ResolveComponents()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
    }
}
