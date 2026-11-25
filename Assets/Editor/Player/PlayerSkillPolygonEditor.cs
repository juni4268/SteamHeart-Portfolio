using UnityEditor;
using UnityEngine;

public abstract class PlayerSkillPolygonEditor : Editor
{
    protected abstract string SkillLabel { get; }
    protected abstract Color PolygonColor { get; }
    protected abstract string VertexColorName { get; }

    private SerializedProperty polygonProperty;
    private bool showAttackPolygon;

    protected virtual void OnEnable()
    {
        polygonProperty = serializedObject.FindProperty("attackPolygon");
    }

    public override void OnInspectorGUI()
    {
        if (DrawDefaultInspector()) SceneView.RepaintAll();

        EditorGUI.BeginChangeCheck();
        showAttackPolygon = EditorGUILayout.Toggle($"Show {SkillLabel} Attack Polygon", showAttackPolygon);
        if (EditorGUI.EndChangeCheck()) SceneView.RepaintAll();

        if (GetAttackPoint() == null)
        {
            EditorGUILayout.HelpBox(
                "Assign the Attack Point on PlayerAttack to preview the skill range.",
                MessageType.Warning);
            return;
        }

        EditorGUILayout.HelpBox(
            $"Enable Show {SkillLabel} Attack Polygon, then drag the {VertexColorName} vertices to edit the outline, " +
            "or use its center handle to move it. Each path needs at least three points.",
            MessageType.Info);
    }

    protected virtual void OnSceneGUI()
    {
        if (!showAttackPolygon) return;
        Transform point = GetAttackPoint();
        if (point != null && AttackPolygonHandle.Draw(point,
            polygonProperty, PolygonColor, SkillLabel)) Repaint();
    }

    private Transform GetAttackPoint()
    {
        PlayerAttack attack = ((Component)target).GetComponent<PlayerAttack>();
        return attack != null ? attack.AttackPoint : null;
    }
}
