using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Boss2BodyMovement))]
public sealed class Boss2BodyMovementEditor : Editor
{
    private void OnSceneGUI()
    {
        var movement = (Boss2BodyMovement)target;
        serializedObject.Update();
        SerializedProperty property = serializedObject.FindProperty("mapBounds");
        Vector2 bounds = property.vector2Value;
        float leftX = Mathf.Min(bounds.x, bounds.y);
        float rightX = Mathf.Max(bounds.x, bounds.y);
        Vector3 center = movement.transform.position;
        float halfHeight = HandleUtility.GetHandleSize(center) * 2f;

        using (new Handles.DrawingScope(Color.cyan, Matrix4x4.identity))
        {
            Handles.DrawDottedLine(new Vector3(leftX, center.y, center.z),
                new Vector3(rightX, center.y, center.z), 4f);

            EditorGUI.BeginChangeCheck();
            float left = DrawBoundary(new Vector3(leftX, center.y, center.z), halfHeight, "Left");
            float right = DrawBoundary(new Vector3(rightX, center.y, center.z), halfHeight, "Right");
            if (!EditorGUI.EndChangeCheck()) return;

            property.vector2Value = new Vector2(Mathf.Min(left, rightX), Mathf.Max(right, leftX));
            serializedObject.ApplyModifiedProperties();
            Repaint();
            SceneView.RepaintAll();
        }
    }

    private static float DrawBoundary(Vector3 position, float halfHeight, string label)
    {
        Handles.DrawLine(position + Vector3.down * halfHeight, position + Vector3.up * halfHeight);
        float size = HandleUtility.GetHandleSize(position);
        Handles.Label(position + Vector3.up * (size * 0.2f), $"{label} X: {position.x:0.##}");
        return Handles.Slider(position, Vector3.right, size * 0.1f,
            Handles.CubeHandleCap, EditorSnapSettings.move.x).x;
    }
}
