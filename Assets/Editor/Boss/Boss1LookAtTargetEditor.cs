using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Boss1LookAtTarget))]
public sealed class Boss1LookAtTargetEditor : Editor
{
    private bool previewing;
    private Vector2 previewOffset;

    private void OnEnable()
    {
        EditorApplication.playModeStateChanged += HandlePlayModeChanged;
        UnityEditor.SceneManagement.EditorSceneManager.sceneSaving += HandleSceneSaving;
    }

    private void OnDisable()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
        UnityEditor.SceneManagement.EditorSceneManager.sceneSaving -= HandleSceneSaving;
        StopPreview();
    }

    private void HandlePlayModeChanged(PlayModeStateChange state) => StopPreview();
    private void HandleSceneSaving(UnityEngine.SceneManagement.Scene scene, string path) => StopPreview();

    public override void OnInspectorGUI()
    {
        Boss1LookAtTarget look = (Boss1LookAtTarget)target;
        EditorGUI.BeginChangeCheck();
        DrawDefaultInspector();
        if (EditorGUI.EndChangeCheck())
        {
            StopPreview();
            ApplyBasePose(look);
            SceneView.RepaintAll();
        }

        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.Vector2Field("Current Offset", look.CurrentOffset);

        if (Application.isPlaying) return;
        EditorGUILayout.Space();
        bool enabled = EditorGUILayout.Toggle("Preview Offset", previewing);
        if (!enabled) StopPreview();
        else if (!previewing && !AnimationMode.InAnimationMode())
        {
            AnimationMode.StartAnimationMode();
            previewing = true;
            Transform pose = look.transform;
            Vector3 original = pose.localPosition;
            for (int axis = 0; axis < 3; axis++)
            {
                string property = "m_LocalPosition." + "xyz"[axis];
                AnimationMode.AddPropertyModification(
                    EditorCurveBinding.FloatCurve("", typeof(Transform), property),
                    new PropertyModification { target = pose, propertyPath = property,
                        value = original[axis].ToString(System.Globalization.CultureInfo.InvariantCulture) }, true);
            }
        }

        if (!previewing) return;
        previewOffset.x = EditorGUILayout.Slider("Preview X", previewOffset.x, -look.MaxOffset.x, look.MaxOffset.x);
        previewOffset.y = EditorGUILayout.Slider("Preview Y", previewOffset.y, -look.MaxOffset.y, look.MaxOffset.y);
        previewOffset = look.ClampOffset(previewOffset);
        look.transform.localPosition = look.BaseLocalPosition + (Vector3)previewOffset;
        SceneView.RepaintAll();
    }

    private void OnSceneGUI()
    {
        Boss1LookAtTarget look = (Boss1LookAtTarget)target;
        Transform pose = look.transform;
        Matrix4x4 basis = pose.parent != null ? pose.parent.localToWorldMatrix : Matrix4x4.identity;
        using (new Handles.DrawingScope(Color.cyan, basis))
        {
            Vector3 center = look.BaseLocalPosition;
            Vector2 radius = look.MaxOffset;
            var points = new Vector3[65];
            for (int i = 0; i < points.Length; i++)
            {
                float angle = i * Mathf.PI * 2f / (points.Length - 1);
                points[i] = center + new Vector3(Mathf.Cos(angle) * radius.x, Mathf.Sin(angle) * radius.y, 0f);
            }
            Handles.DrawAAPolyLine(2f, points);
            Handles.DrawLine(center, pose.localPosition);
            Handles.Label(pose.localPosition, $"Offset ({look.CurrentOffset.x:F3}, {look.CurrentOffset.y:F3})");
            if (Application.isPlaying) return;

            float size = HandleUtility.GetHandleSize(basis.MultiplyPoint3x4(center)) * 0.08f;
            EditorGUI.BeginChangeCheck();
            Vector3 nextCenter = Handles.PositionHandle(center, Quaternion.identity);
            Vector3 x = Handles.Slider(center + Vector3.right * radius.x, Vector3.right,
                size, Handles.DotHandleCap, 0f);
            Vector3 y = Handles.Slider(center + Vector3.up * radius.y, Vector3.up,
                size, Handles.DotHandleCap, 0f);
            if (!EditorGUI.EndChangeCheck()) return;
            StopPreview();
            serializedObject.Update();
            nextCenter.z = center.z;
            serializedObject.FindProperty("baseLocalPosition").vector3Value = nextCenter;
            serializedObject.FindProperty("maxOffsetX").floatValue = Mathf.Max(0f, x.x - center.x);
            serializedObject.FindProperty("maxOffsetY").floatValue = Mathf.Max(0f, y.y - center.y);
            serializedObject.ApplyModifiedProperties();
            ApplyBasePose(look);
        }
    }

    private static void ApplyBasePose(Boss1LookAtTarget look)
    {
        if (Application.isPlaying) return;
        Undo.RecordObject(look.transform, "Adjust gaze base position");
        look.transform.localPosition = look.BaseLocalPosition;
    }

    private void StopPreview()
    {
        if (!previewing) return;
        AnimationMode.StopAnimationMode();
        previewing = false;
    }
}
