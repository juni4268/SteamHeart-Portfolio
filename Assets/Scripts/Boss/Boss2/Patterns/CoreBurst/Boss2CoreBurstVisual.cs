using System;
using UnityEngine;

[RequireComponent(typeof(Animator), typeof(SpriteRenderer))]
public sealed class Boss2CoreBurstVisual : MonoBehaviour
{
    [SerializeField] private string activeParameter = "Charging";
    private Animator animator;
    private Boss2Audio audioOwner;
    private Boss2Audio.Playback audioPlayback;

    public void BindAudio(Boss2Audio audio) => audioOwner = audio;
    internal void Entered()
    {
        if (!Active || Interrupted) return;
        audioPlayback?.FadeOut();
        audioPlayback = audioOwner != null ? audioOwner.Play(Boss2Audio.Sound.CoreCharge, loop: true, fadeIn: 0.1f) : null;
    }
    public bool Active { get; private set; }
    public bool Completed { get; private set; }
    public bool Interrupted { get; private set; }

    public void Begin()
    {
        animator = GetComponent<Animator>();
        bool found = false;
        foreach (var parameter in animator.parameters)
            if (parameter.name == activeParameter && parameter.type == AnimatorControllerParameterType.Bool) found = true;
        if (!isActiveAndEnabled || !animator.isActiveAndEnabled || !found)
            throw new InvalidOperationException("Assign an active Core Burst Animator and its charging parameter.");
        Active = true;
        Completed = Interrupted = false;
        animator.SetBool(activeParameter, true);
    }
    private void OnDisable()
    {
        audioPlayback?.FadeOut();
        if (Active) Interrupted = true;
    }
    public void Cancel()
    {
        audioPlayback?.FadeOut();
        if (!Active) return;
        Interrupted = true;
        Active = false;
        if (animator != null && animator.isActiveAndEnabled) animator.SetBool(activeParameter, false);
    }
    internal void ReachedEnd()
    {
        if (Active && !Interrupted) { Completed = true; Active = false; audioPlayback?.FadeOut(); }
    }
    internal void Exited() { audioPlayback?.FadeOut(); if (Active && !Completed) Interrupted = true; }
}
