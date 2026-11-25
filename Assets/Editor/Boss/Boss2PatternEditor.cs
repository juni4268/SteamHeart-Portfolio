using UnityEditor;

[CustomEditor(typeof(Boss2PatternBase), true)]
[CanEditMultipleObjects]
public sealed class Boss2PatternEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var type = target.GetType();
        bool hideKind = type.GetProperty(nameof(Boss2PatternBase.Kind)).DeclaringType != typeof(Boss2PatternBase);
        bool hideRequiredParts = type.GetProperty(nameof(Boss2PatternBase.RequiredParts)).DeclaringType != typeof(Boss2PatternBase);
        var property = serializedObject.GetIterator();
        bool enterChildren = true;
        while (property.NextVisible(enterChildren))
        {
            enterChildren = false;
            if ((hideKind && property.propertyPath == "kind") ||
                (hideRequiredParts && property.propertyPath == "requiredParts")) continue;
            using (new EditorGUI.DisabledScope(property.propertyPath == "m_Script"))
                EditorGUILayout.PropertyField(property, true);
        }
        serializedObject.ApplyModifiedProperties();
    }
}
