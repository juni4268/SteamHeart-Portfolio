using System;
using UnityEngine;

public enum Boss1HandAnimationRole
{
    None,
    PropellerPrepare,
    PropellerAttack,
    PropellerReturn,
    CrushPrepare1,
    CrushPrepare2,
    CrushAttack,
    CrushReturn,
    SlamPrepare,
    SlamAttack,
    SlamReturn,
    // Keep the serialized values of the former Overdrive Slam roles.
    OverdrivePrepare = 13,
    OverdriveAttack,
    OverdriveReturn
}

// Roles identify gameplay actions; their clips belong only to the Animator.
[Serializable]
public sealed class Boss1HandAnimationTrack
{
    [SerializeField] private Boss1HandAnimationRole prepareRole;
    [SerializeField] private Boss1HandAnimationRole secondPrepareRole;
    [SerializeField] private Boss1HandAnimationRole attackRole;
    [SerializeField] private Boss1HandAnimationRole returnRole;
    [SerializeField] private bool flipX;

    public Boss1HandAnimationRole PrepareRole => prepareRole;
    public Boss1HandAnimationRole SecondPrepareRole => secondPrepareRole;
    public Boss1HandAnimationRole AttackRole => attackRole;
    public Boss1HandAnimationRole ReturnRole => returnRole;
    public bool FlipX => flipX;
}

[Serializable]
public sealed class Boss1HandAnimationProfile
{
    [SerializeField] private Boss1HandAnimationTrack left = new Boss1HandAnimationTrack();
    [SerializeField] private Boss1HandAnimationTrack right = new Boss1HandAnimationTrack();

    public Boss1HandAnimationTrack Left => left;
    public Boss1HandAnimationTrack Right => right;

    public Boss1HandAnimationTrack GetTrack(Boss1HandSide side) =>
        side == Boss1HandSide.Left ? left : right;
}
