using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal static class EnemyMovementCurveSync
{
    internal const string DistanceProperty = "movementDistance";
    internal const string LoopDistanceProperty = "movementLoopDistance";

    private static readonly HashSet<string> PendingClips = new HashSet<string>();
    private static readonly HashSet<string> PendingSaves = new HashSet<string>();
    private static bool synchronizing;
    private static bool scheduled;

    static EnemyMovementCurveSync()
    {
        AnimationUtility.onCurveWasModified += OnCurveModified;
        EditorApplication.delayCall += SynchronizeExistingClips;
    }

    private static void SynchronizeExistingClips()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { "Assets/Animations/Enemies" }))
            Queue(AssetDatabase.GUIDToAssetPath(guid));
    }

    private static void OnCurveModified(AnimationClip clip, EditorCurveBinding binding, AnimationUtility.CurveModifiedType change)
    {
        if (synchronizing || clip == null) return;
        if (change == AnimationUtility.CurveModifiedType.ClipModified)
        {
            // Clip Awake sends this notification before its length is initialized.
            // Wait until loading finishes instead of generating a zero loop distance.
            Queue(AssetDatabase.GetAssetPath(clip));
            return;
        }
        if (Synchronize(clip)) Queue(AssetDatabase.GetAssetPath(clip), true);
    }

    internal static void Queue(string path, bool saveGeneratedChanges = false)
    {
        if (string.IsNullOrEmpty(path) || !path.EndsWith(".anim")) return;
        PendingClips.Add(path);
        if (saveGeneratedChanges) PendingSaves.Add(path);
        if (scheduled) return;
        scheduled = true;
        EditorApplication.delayCall += Flush;
    }

    private static void Flush()
    {
        scheduled = false;
        string[] paths = PendingClips.ToArray();
        var saves = new HashSet<string>(PendingSaves);
        PendingClips.Clear();
        PendingSaves.Clear();
        foreach (string path in paths)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null) continue;
            if (Synchronize(clip) || saves.Contains(path)) AssetDatabase.SaveAssetIfDirty(clip);
        }
    }

    internal static bool Synchronize(AnimationClip clip)
    {
        bool changed = false;
        synchronizing = true;
        try
        {
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
            foreach (EditorCurveBinding binding in bindings)
            {
                if (binding.type != typeof(EnemyAnimationEventReceiver) || binding.propertyName != DistanceProperty) continue;
                AnimationCurve distance = AnimationUtility.GetEditorCurve(clip, binding);
                float loopDistance = distance.Evaluate(clip.length) - distance.Evaluate(0f);
                var loopBinding = EditorCurveBinding.FloatCurve(binding.path, binding.type, LoopDistanceProperty);
                AnimationCurve existing = AnimationUtility.GetEditorCurve(clip, loopBinding);
                if (existing != null && existing.length == 1 && existing[0].time == 0f && existing[0].value == loopDistance) continue;
                AnimationUtility.SetEditorCurve(clip, loopBinding, new AnimationCurve(new Keyframe(0f, loopDistance)));
                changed = true;
            }

            foreach (EditorCurveBinding binding in bindings)
            {
                if (binding.type != typeof(EnemyAnimationEventReceiver) || binding.propertyName != LoopDistanceProperty) continue;
                if (bindings.Any(b => b.path == binding.path && b.type == binding.type && b.propertyName == DistanceProperty)) continue;
                AnimationUtility.SetEditorCurve(clip, binding, null);
                changed = true;
            }
        }
        finally
        {
            synchronizing = false;
        }
        return changed;
    }
}

internal sealed class EnemyMovementCurvePostprocessor : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        foreach (string path in imported) EnemyMovementCurveSync.Queue(path);
        foreach (string path in moved) EnemyMovementCurveSync.Queue(path);
    }
}
