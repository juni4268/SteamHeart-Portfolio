using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public sealed class Boss2TrackingLaserVisual : MonoBehaviour
{
    [SerializeField] private string phaseParameter = "LaserPhase";
    [SerializeField] private SpriteRenderer beamRenderer;
    [SerializeField] private SpriteRenderer impactRenderer;
    [SerializeField] private Transform beamTop;
    [SerializeField] private Transform beamBottom;
    [SerializeField] private Transform groundAnchor;
    [SerializeField] private Boss1DebrisWarningArea warningArea;
    [Tooltip("Damage width at a local X scale of 1. This does not resize the effect.")]
    [SerializeField, Min(0.001f)] private float referenceWidth = 1f;

    private Animator animator;
    private bool hasGeometry;
    private bool fireRequested;
    private bool fadeRequested;
    private bool fadeFinished;
    private float warningDuration;
    private Boss2Audio audioOwner;
    private Boss2Audio.Playback audioPlayback;

    public void BindAudio(Boss2Audio audio) => audioOwner = audio;

    public bool Active { get; private set; }
    public bool FireEntered { get; private set; }
    public bool Completed { get; private set; }
    public bool Interrupted { get; private set; }
    public Bounds DamageBounds
    {
        get
        {
            Vector3 top = beamTop.position;
            Vector3 bottom = beamBottom.position;
            return new Bounds((top + bottom) * 0.5f, new Vector3(
                referenceWidth * Mathf.Abs(beamRenderer.transform.lossyScale.x), Mathf.Abs(top.y - bottom.y), 0f));
        }
    }

    private void Awake() => animator = GetComponent<Animator>();
    private void OnDisable()
    {
        audioPlayback?.FadeOut();
        if (Active) Interrupted = true;
        if (warningArea != null) warningArea.Hide();
    }

    public void BeginWarning(float duration)
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (!isActiveAndEnabled || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null ||
            beamRenderer == null || impactRenderer == null || beamTop == null || beamBottom == null ||
            groundAnchor == null || warningArea == null || !warningArea.isActiveAndEnabled || referenceWidth <= 0f)
            throw new InvalidOperationException("Assign the Tracking Laser Animator, renderers, geometry markers and warning area.");
        bool found = false;
        foreach (AnimatorControllerParameter parameter in animator.parameters)
            if (parameter.name == phaseParameter && parameter.type == AnimatorControllerParameterType.Int) found = true;
        if (!found) throw new InvalidOperationException("The Tracking Laser Animator is missing its phase Int parameter.");

        Active = true;
        FireEntered = false;
        Completed = false;
        Interrupted = false;
        fireRequested = false;
        fadeRequested = false;
        fadeFinished = false;
        warningDuration = Mathf.Max(0f, duration);
        HideGeometry();
        animator.SetInteger(phaseParameter, 0);
    }

    public void BeginFire()
    {
        if (!Active || Interrupted || fireRequested)
            throw new InvalidOperationException("The Tracking Laser warning is not active.");
        fireRequested = true;
        warningArea.Hide();
        animator.SetInteger(phaseParameter, 1);
    }

    public void SetGroundPosition(Vector3 ground)
    {
        Vector3 offset = ground - groundAnchor.position;
        offset.x = ground.x - DamageBounds.center.x;
        transform.position += offset;
        hasGeometry = true;
        if (Active && !fireRequested)
        {
            Bounds bounds = DamageBounds;
            warningArea.ShowAt(bounds.center.x, bounds.max.y, bounds.min.y, bounds.center.z,
                warningDuration, bounds.size.x);
        }
        SetVisible(Active && FireEntered);
    }

    public void HideGeometry()
    {
        hasGeometry = false;
        SetVisible(false);
        if (warningArea != null) warningArea.Hide();
    }

    public void BeginFade()
    {
        if (!Active || Interrupted || !FireEntered)
            throw new InvalidOperationException("The Tracking Laser has not entered its firing animation.");
        fadeRequested = true;
        audioPlayback?.FadeOut();
        animator.SetInteger(phaseParameter, 2);
    }

    public void Cancel()
    {
        audioPlayback?.FadeOut();
        if (!Active) return;
        Interrupted = true;
        Active = false;
        HideGeometry();
        if (animator != null && animator.isActiveAndEnabled) animator.SetInteger(phaseParameter, 0);
    }

    internal void Entered(Boss2TrackingLaserStage stage)
    {
        if (!Active || Interrupted) return;
        if (stage == Boss2TrackingLaserStage.Fire)
        {
            if (!fireRequested) { Interrupted = true; return; }
            FireEntered = true;
            audioPlayback?.FadeOut();
            audioPlayback = audioOwner != null
                ? audioOwner.Play(Boss2Audio.Sound.TrackingLaser, loop: true, fadeIn: 0.05f)
                : null;
            SetVisible(hasGeometry);
        }
        else if (!fadeRequested) Interrupted = true;
    }

    internal void ReachedEnd(Boss2TrackingLaserStage stage)
    {
        if (!Active || Interrupted || stage != Boss2TrackingLaserStage.Fade || !fadeRequested) return;
        fadeFinished = true;
        SetVisible(false);
        animator.SetInteger(phaseParameter, 0);
    }

    internal void Exited(Boss2TrackingLaserStage stage)
    {
        if (stage == Boss2TrackingLaserStage.Fire) audioPlayback?.FadeOut();
        if (!Active || Interrupted) return;
        if (stage == Boss2TrackingLaserStage.Fire)
        {
            if (!fadeRequested) Interrupted = true;
        }
        else if (!fadeFinished) Interrupted = true;
        else { Completed = true; Active = false; }
    }

    private void SetVisible(bool visible)
    {
        if (beamRenderer != null) beamRenderer.enabled = visible;
        if (impactRenderer != null) impactRenderer.enabled = visible;
    }
}
