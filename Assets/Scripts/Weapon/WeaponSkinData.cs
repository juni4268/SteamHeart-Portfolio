using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "WeaponSkin", menuName = "Project K/Weapon Skin")]
public sealed class WeaponSkinData : ScriptableObject
{
    [SerializeField] private string skinId;
    [SerializeField] private string displayName;
    [SerializeField] private WeaponData weapon;
    [SerializeField] private Sprite icon;
    [SerializeField, Min(0)] private int scrapCost;
    [SerializeField] private bool unlockedByDefault;
    [SerializeField] private AnimatorOverrideController animationController;

    public string SkinId => skinId;
    public string DisplayName => displayName;
    public WeaponData Weapon => weapon;
    public Sprite Icon => icon;
    public int ScrapCost => Mathf.Max(0, scrapCost);
    public bool UnlockedByDefault => unlockedByDefault;
    public AnimatorOverrideController AnimationController => animationController;

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(skinId)) skinId = Guid.NewGuid().ToString("N");
    }

    public bool IsCompatibleWith(WeaponData targetWeapon, WeaponStageData stage, out string reason)
    {
        reason = string.Empty;
        if (weapon != targetWeapon || animationController == null || stage?.AnimatorOverrideController == null)
        {
            reason = "Assign the same weapon and a skin AnimatorOverrideController.";
            return false;
        }
        AnimatorOverrideController source = stage.AnimatorOverrideController;
        if (animationController.runtimeAnimatorController != source.runtimeAnimatorController)
        {
            reason = "The skin must share the weapon's Animator graph.";
            return false;
        }
        var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        source.GetOverrides(pairs);
        foreach (var pair in pairs)
        {
            AnimationClip original = pair.Value != null ? pair.Value : pair.Key;
            AnimationClip replacement = animationController[pair.Key];
            if (replacement == null) replacement = pair.Key;
            if (!HaveSameTiming(original, replacement))
            {
                reason = $"The replacement for {pair.Key.name} changes duration, looping, or Animation Events.";
                return false;
            }
        }
        return true;
    }

    private static bool HaveSameTiming(AnimationClip original, AnimationClip replacement)
    {
        if (original == replacement) return true;
        if (!Mathf.Approximately(original.length, replacement.length) || original.isLooping != replacement.isLooping)
            return false;
        AnimationEvent[] a = original.events;
        AnimationEvent[] b = replacement.events;
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (!Mathf.Approximately(a[i].time, b[i].time) || a[i].functionName != b[i].functionName ||
                a[i].intParameter != b[i].intParameter || !Mathf.Approximately(a[i].floatParameter, b[i].floatParameter) ||
                a[i].stringParameter != b[i].stringParameter || a[i].objectReferenceParameter != b[i].objectReferenceParameter)
                return false;
        }
        return true;
    }
}
