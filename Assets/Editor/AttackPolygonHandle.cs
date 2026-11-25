using UnityEditor;
using UnityEngine;

internal static class AttackPolygonHandle
{
    internal static bool Draw(Transform point, SerializedProperty polygon, Color color, string label)
    {
        polygon.serializedObject.Update();
        SerializedProperty paths = polygon.FindPropertyRelative("paths");
        Vector3 center = Vector3.zero;
        int count = 0;
        bool changed = false;
        using (new Handles.DrawingScope(color))
        {
            for (int i = 0; i < paths.arraySize; i++)
            {
                SerializedProperty points = paths.GetArrayElementAtIndex(i).FindPropertyRelative("points");
                if (points.arraySize < 3) continue;
                var outline = new Vector3[points.arraySize + 1];
                for (int j = 0; j < points.arraySize; j++)
                {
                    SerializedProperty vertex = points.GetArrayElementAtIndex(j);
                    Vector3 world = point.TransformPoint(vertex.vector2Value);
                    EditorGUI.BeginChangeCheck();
                    Vector3 moved = Handles.FreeMoveHandle(world,
                        HandleUtility.GetHandleSize(world) * 0.04f, Vector3.zero, Handles.DotHandleCap);
                    if (EditorGUI.EndChangeCheck())
                    {
                        vertex.vector2Value = point.InverseTransformPoint(
                            new Vector3(moved.x, moved.y, world.z));
                        changed = true;
                    }
                    outline[j] = point.TransformPoint(vertex.vector2Value);
                    center += outline[j];
                    count++;
                }
                outline[points.arraySize] = outline[0];
                Handles.DrawAAPolyLine(3f, outline);
            }

            if (count > 0)
            {
                center /= count;
                Handles.Label(center, label);
                EditorGUI.BeginChangeCheck();
                Vector3 moved = Handles.PositionHandle(center, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    Vector2 delta = point.InverseTransformVector(new Vector3(
                        moved.x - center.x, moved.y - center.y, 0f));
                    for (int i = 0; i < paths.arraySize; i++)
                    {
                        SerializedProperty points = paths.GetArrayElementAtIndex(i).FindPropertyRelative("points");
                        for (int j = 0; j < points.arraySize; j++)
                            points.GetArrayElementAtIndex(j).vector2Value += delta;
                    }
                    changed = true;
                }
            }
        }
        // Serialized properties preserve Undo and Prefab overrides.
        return changed && polygon.serializedObject.ApplyModifiedProperties();
    }
}
