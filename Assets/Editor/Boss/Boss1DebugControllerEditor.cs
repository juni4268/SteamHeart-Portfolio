using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Boss1DebugController))]
public sealed class Boss1DebugControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        Boss1DebugController debugController = (Boss1DebugController)target;

        EditorGUILayout.Space();
        DrawRuntimeState(debugController);
        EditorGUILayout.Space();
        DrawTestControls(debugController);

        if (Application.isPlaying)
        {
            Repaint();
        }
    }

    private static void DrawRuntimeState(Boss1DebugController debugController)
    {
        EditorGUILayout.LabelField("Runtime State", EditorStyles.boldLabel);

        Boss1Controller controller = debugController.Controller;
        if (controller == null)
        {
            EditorGUILayout.HelpBox("Boss1Controller reference is missing.", MessageType.Warning);
            return;
        }

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.EnumPopup("Boss State", controller.CurrentState);
            EditorGUILayout.TextField("Current Pattern", GetPatternName(controller.CurrentPattern));
            EditorGUILayout.TextField("Last Result", controller.LastPatternResult?.ToString() ?? "None");
            if (!string.IsNullOrEmpty(controller.LastPatternError))
                EditorGUILayout.HelpBox(controller.LastPatternError, MessageType.Warning);
            EditorGUILayout.Toggle("Scheduler Running", debugController.Scheduler != null
                && debugController.Scheduler.IsRunning);
            DrawHandState("Left Hand", controller.LeftHand);
            DrawHandState("Right Hand", controller.RightHand);
        }
    }

    private static void DrawTestControls(Boss1DebugController debugController)
    {
        EditorGUILayout.LabelField("Play Mode Test Controls", EditorStyles.boldLabel);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to use the test controls.", MessageType.Info);
        }

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button("Execute Selected Pattern"))
            {
                debugController.ExecuteSelectedPattern();
            }

            if (GUILayout.Button("Cancel Current Pattern"))
            {
                debugController.CancelCurrentPattern();
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Start Scheduler"))
            {
                debugController.StartScheduler();
            }

            if (GUILayout.Button("Stop Scheduler"))
            {
                debugController.StopScheduler();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Move Left Hand"))
            {
                debugController.MoveLeftHand();
            }

            if (GUILayout.Button("Move Right Hand"))
            {
                debugController.MoveRightHand();
            }

            if (GUILayout.Button("Move Both Hands"))
            {
                debugController.MoveBothHands();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Return Left Hand"))
            {
                debugController.ReturnLeftHand();
            }

            if (GUILayout.Button("Return Right Hand"))
            {
                debugController.ReturnRightHand();
            }

            if (GUILayout.Button("Return Both Hands"))
            {
                debugController.ReturnBothHands();
            }
            EditorGUILayout.EndHorizontal();
        }
    }

    private static void DrawHandState(string label, Boss1HandController hand)
    {
        string value = hand == null
            ? "Missing"
            : $"{hand.CurrentMotion} (Busy: {hand.IsBusy})";

        EditorGUILayout.TextField(label, value);
    }

    private static string GetPatternName(Boss1PatternBase pattern)
    {
        return pattern == null ? "None" : pattern.GetType().Name;
    }
}
