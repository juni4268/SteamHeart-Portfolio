using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerAttack))]
public class PlayerAttackEditor : Editor
{
    private Transform attackPoint;
    [SerializeField] private WeaponData previewWeapon;
    [SerializeField] private int previewStageIndex;
    [SerializeField] private int previewAttackIndex;
    [SerializeField] private bool previewAirAttack;

    private void OnEnable()
    {
        attackPoint = ((PlayerAttack)target).AttackPoint;
        if (previewWeapon == null)
        {
            PlayerWeaponController controller = ((PlayerAttack)target).GetComponent<PlayerWeaponController>();
            if (controller != null)
            {
                SerializedObject controllerObject = new SerializedObject(controller);
                previewWeapon = controllerObject.FindProperty("weaponData").objectReferenceValue as WeaponData;
            }
        }
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        attackPoint = ((PlayerAttack)target).AttackPoint;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Attack Polygon Preview", EditorStyles.boldLabel);

        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView != null && !sceneView.drawGizmos)
        {
            EditorGUILayout.HelpBox("Enable Scene Gizmos to see and edit the attack polygon handles.", MessageType.Warning);
            if (GUILayout.Button("Enable Scene Gizmos"))
            {
                sceneView.drawGizmos = true;
                sceneView.Repaint();
            }
        }

        WeaponData selectedWeapon = (WeaponData)EditorGUILayout.ObjectField(
            "Preview Weapon",
            previewWeapon,
            typeof(WeaponData),
            false);

        if (selectedWeapon != previewWeapon)
        {
            previewWeapon = selectedWeapon;
            previewStageIndex = 0;
            previewAttackIndex = 0;
            SceneView.RepaintAll();
        }

        if (attackPoint == null)
        {
            EditorGUILayout.HelpBox(
                "Assign the Attack Point Transform used by the runtime attack.",
                MessageType.Warning);
            return;
        }

        if (previewWeapon == null)
        {
            EditorGUILayout.HelpBox(
                "Assign a WeaponData asset to preview and edit its attack polygon.",
                MessageType.Info);
            return;
        }

        SerializedObject weaponSerializedObject = new SerializedObject(previewWeapon);
        weaponSerializedObject.Update();

        SerializedProperty stagesProperty = weaponSerializedObject.FindProperty("weaponStages");
        if (stagesProperty == null || stagesProperty.arraySize == 0)
        {
            EditorGUILayout.HelpBox(
                "The preview weapon has no weapon stages.",
                MessageType.Warning);
            return;
        }

        previewStageIndex = Mathf.Clamp(previewStageIndex, 0, stagesProperty.arraySize - 1);
        int selectedStageIndex = EditorGUILayout.Popup(
            "Preview Stage",
            previewStageIndex,
            BuildElementLabels(stagesProperty, "stageName", "Stage"));

        if (selectedStageIndex != previewStageIndex)
        {
            previewStageIndex = selectedStageIndex;
            previewAttackIndex = 0;
            SceneView.RepaintAll();
        }

        SerializedProperty stageProperty = stagesProperty.GetArrayElementAtIndex(previewStageIndex);
        EditorGUI.BeginChangeCheck();
        previewAirAttack = EditorGUILayout.Toggle("Preview Air Attack", previewAirAttack);
        if (EditorGUI.EndChangeCheck())
            SceneView.RepaintAll();

        if (!previewAirAttack)
        {
            SerializedProperty attacksProperty = stageProperty.FindPropertyRelative("comboAttacks");
            if (attacksProperty == null || attacksProperty.arraySize == 0)
            {
                EditorGUILayout.HelpBox(
                    "The selected stage has no combo attacks.",
                    MessageType.Warning);
                return;
            }

            previewAttackIndex = Mathf.Clamp(previewAttackIndex, 0, attacksProperty.arraySize - 1);
            int selectedAttackIndex = EditorGUILayout.Popup(
                "Preview Attack",
                previewAttackIndex,
                BuildElementLabels(attacksProperty, "attackName", "Attack"));

            if (selectedAttackIndex != previewAttackIndex)
            {
                previewAttackIndex = selectedAttackIndex;
                SceneView.RepaintAll();
            }
        }

        SerializedProperty attackProperty = GetPreviewAttackProperty(stageProperty);
        SerializedProperty polygonProperty = attackProperty?.FindPropertyRelative("attackPolygon");

        if (polygonProperty == null)
        {
            EditorGUILayout.HelpBox(
                "The selected attack does not contain attack polygon data.",
                MessageType.Error);
            return;
        }

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(polygonProperty, true);

        if (EditorGUI.EndChangeCheck())
        {
            weaponSerializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(previewWeapon);
            SceneView.RepaintAll();
        }

        EditorGUILayout.HelpBox(
            "Drag the red Scene vertices to edit the outline, or use its center handle to move it. " +
            "Each path needs at least three points. Changes apply to the preview WeaponData asset.",
            MessageType.Info);
    }

    private void OnSceneGUI()
    {
        attackPoint = ((PlayerAttack)target).AttackPoint;

        if (attackPoint == null ||
            !TryGetPreviewAttackProperty(out SerializedProperty polygonProperty))
        {
            return;
        }

        if (AttackPolygonHandle.Draw(attackPoint, polygonProperty, Color.red, "Attack")) Repaint();
    }

    private bool TryGetPreviewAttackProperty(out SerializedProperty polygonProperty)
    {
        polygonProperty = null;

        if (previewWeapon == null) return false;

        var weaponSerializedObject = new SerializedObject(previewWeapon);
        weaponSerializedObject.Update();

        SerializedProperty stagesProperty = weaponSerializedObject.FindProperty("weaponStages");
        if (stagesProperty == null || stagesProperty.arraySize == 0) return false;

        previewStageIndex = Mathf.Clamp(previewStageIndex, 0, stagesProperty.arraySize - 1);
        SerializedProperty stageProperty = stagesProperty.GetArrayElementAtIndex(previewStageIndex);
        SerializedProperty attackProperty = GetPreviewAttackProperty(stageProperty);
        polygonProperty = attackProperty?.FindPropertyRelative("attackPolygon");

        return polygonProperty != null;
    }

    private SerializedProperty GetPreviewAttackProperty(SerializedProperty stageProperty)
    {
        if (previewAirAttack)
            return stageProperty.FindPropertyRelative("airAttack");

        SerializedProperty attacksProperty = stageProperty.FindPropertyRelative("comboAttacks");
        if (attacksProperty == null || attacksProperty.arraySize == 0) return null;

        previewAttackIndex = Mathf.Clamp(previewAttackIndex, 0, attacksProperty.arraySize - 1);
        return attacksProperty.GetArrayElementAtIndex(previewAttackIndex);
    }

    private static string[] BuildElementLabels(
        SerializedProperty arrayProperty,
        string nameProperty,
        string fallbackLabel)
    {
        string[] labels = new string[arrayProperty.arraySize];

        for (int i = 0; i < arrayProperty.arraySize; i++)
        {
            SerializedProperty element = arrayProperty.GetArrayElementAtIndex(i);
            SerializedProperty displayName = element.FindPropertyRelative(nameProperty);
            string label = displayName?.stringValue;
            labels[i] = string.IsNullOrWhiteSpace(label)
                ? $"{fallbackLabel} {i}"
                : $"{i}: {label}";
        }

        return labels;
    }

}
