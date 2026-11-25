using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

internal static class Boss1HandAnimationBindingUtility
{
    internal sealed class StateChoice { public AnimatorState State; public string Path; }

    internal static List<StateChoice> GetStates(AnimatorController controller)
    {
        var states = new List<StateChoice>();
        if (controller != null)
            foreach (var layer in controller.layers) Collect(layer.stateMachine, layer.name, states);
        return states;
    }

    private static void Collect(AnimatorStateMachine machine, string prefix, List<StateChoice> states)
    {
        foreach (ChildAnimatorState child in machine.states)
            states.Add(new StateChoice { State = child.state, Path = prefix + "." + child.state.name });
        foreach (ChildAnimatorStateMachine child in machine.stateMachines)
            Collect(child.stateMachine, prefix + "." + child.stateMachine.name, states);
    }

    private static IEnumerable<AnimatorStateTransition> GetTransitions(AnimatorStateMachine machine)
    {
        foreach (AnimatorStateTransition transition in machine.anyStateTransitions) yield return transition;
        foreach (ChildAnimatorState child in machine.states)
            foreach (AnimatorStateTransition transition in child.state.transitions) yield return transition;
        foreach (ChildAnimatorStateMachine child in machine.stateMachines)
            foreach (AnimatorStateTransition transition in GetTransitions(child.stateMachine)) yield return transition;
    }

    internal static string[] GetTriggers(AnimatorController controller, AnimatorState state)
    {
        var triggers = new HashSet<string>(controller.parameters
            .Where(p => p.type == AnimatorControllerParameterType.Trigger).Select(p => p.name));
        return controller.layers.SelectMany(layer => GetTransitions(layer.stateMachine)).Where(t => t.destinationState == state)
            .SelectMany(t => t.conditions).Where(c => c.mode == AnimatorConditionMode.If && triggers.Contains(c.parameter))
            .Select(c => c.parameter).Distinct().ToArray();
    }

    internal static AnimatorController GetController(SerializedProperty track)
    {
        var component = track.serializedObject.targetObject as Component;
        Boss1Controller boss = component != null ? component.GetComponent<Boss1Controller>() : null;
        if (boss == null) return null;
        Boss1HandController hand = track.name == "right" ? boss.RightHand : boss.LeftHand;
        if (hand == null) return null;
        var playback = hand.GetComponent<Boss1HandAnimationController>();
        if (playback == null) return null;
        var animator = new SerializedObject(playback).FindProperty("animator").objectReferenceValue as Animator;
        return animator != null ? animator.runtimeAnimatorController as AnimatorController : null;
    }

    internal static StateChoice FindOwner(AnimatorController controller, Boss1HandAnimationBinding binding) =>
        GetStates(controller).FirstOrDefault(s => s.State.behaviours.Contains(binding));

    internal static string Validate(AnimatorController controller, StateChoice owner, Boss1HandAnimationBinding binding)
    {
        if (owner == null) return "Binding has no State in this Controller.";
        if (!(owner.State.motion is AnimationClip)) return "Assign an AnimationClip to the State Motion.";
        if (binding.Role == Boss1HandAnimationRole.None) return "Assign the gameplay role.";
        if (!GetTriggers(controller, owner.State).Contains(binding.TriggerName))
            return "Binding does not match an incoming Trigger transition. Synchronize the Trigger.";
        if (GetStates(controller).SelectMany(s => s.State.behaviours.OfType<Boss1HandAnimationBinding>())
            .Count(b => b.Role == binding.Role) != 1) return "Each gameplay role needs exactly one binding.";
        return null;
    }
}

[CustomEditor(typeof(Boss1HandAnimationBinding))]
public sealed class Boss1HandAnimationBindingEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var binding = (Boss1HandAnimationBinding)target;
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(AssetDatabase.GetAssetPath(binding));
        var owner = Boss1HandAnimationBindingUtility.FindOwner(controller, binding);
        if (owner == null)
        {
            EditorGUILayout.HelpBox("Add this behaviour to a hand animation State.", MessageType.Warning);
            return;
        }
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("role"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("triggerName"));
        string[] triggers = Boss1HandAnimationBindingUtility.GetTriggers(controller, owner.State);
        using (new EditorGUI.DisabledScope(Application.isPlaying || triggers.Length != 1))
            if (GUILayout.Button("Synchronize Trigger"))
                serializedObject.FindProperty("triggerName").stringValue = triggers[0];
        serializedObject.ApplyModifiedProperties();
        EditorGUILayout.HelpBox("Set Motion only on the Animator State. Renaming or moving a State and replacing its Clip preserve the role.", MessageType.Info);
        string error = Boss1HandAnimationBindingUtility.Validate(controller, owner, binding);
        if (error != null) EditorGUILayout.HelpBox(error, MessageType.Warning);
    }
}

[CustomPropertyDrawer(typeof(Boss1HandAnimationTrack))]
public sealed class Boss1HandAnimationTrackDrawer : PropertyDrawer
{
    private static readonly string[] RoleFields = { "prepareRole", "secondPrepareRole", "attackRole", "returnRole" };
    private const float Spacing = 2f;
    private static float Line => EditorGUIUtility.singleLineHeight;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = Line;
        if (!property.isExpanded) return height;
        var controller = Boss1HandAnimationBindingUtility.GetController(property);
        height += 5f * (Line + Spacing);
        foreach (string field in RoleFields)
            if (GetError(controller, (Boss1HandAnimationRole)property.FindPropertyRelative(field).intValue) != null)
                height += 2f * Line + Spacing;
        return height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        var row = new Rect(position.x, position.y, position.width, Line);
        property.isExpanded = EditorGUI.Foldout(row, property.isExpanded, label, true);
        if (!property.isExpanded) { EditorGUI.EndProperty(); return; }
        EditorGUI.indentLevel++;
        var controller = Boss1HandAnimationBindingUtility.GetController(property);
        foreach (string field in RoleFields)
        {
            row.y += Line + Spacing;
            var role = property.FindPropertyRelative(field);
            EditorGUI.PropertyField(row, role);
            string error = GetError(controller, (Boss1HandAnimationRole)role.intValue);
            if (error == null) continue;
            row.y += Line + Spacing;
            EditorGUI.HelpBox(new Rect(row.x, row.y, row.width, 2f * Line), error, MessageType.Warning);
            row.y += Line;
        }
        row.y += Line + Spacing;
        EditorGUI.PropertyField(row, property.FindPropertyRelative("flipX"));
        EditorGUI.indentLevel--;
        EditorGUI.EndProperty();
    }

    private static string GetError(AnimatorController controller, Boss1HandAnimationRole role)
    {
        if (role == Boss1HandAnimationRole.None) return null;
        if (controller == null) return "Assign the hand Animator and its Controller.";
        var owners = Boss1HandAnimationBindingUtility.GetStates(controller)
            .Where(s => s.State.behaviours.OfType<Boss1HandAnimationBinding>().Any(b => b.Role == role)).ToArray();
        if (owners.Length != 1) return "The role needs exactly one State binding.";
        var binding = owners[0].State.behaviours.OfType<Boss1HandAnimationBinding>().Single(b => b.Role == role);
        return Boss1HandAnimationBindingUtility.Validate(controller, owners[0], binding);
    }
}
