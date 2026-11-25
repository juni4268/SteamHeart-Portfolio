using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerWeaponController))]
public class PlayerWeaponControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        PlayerWeaponController weaponController = (PlayerWeaponController)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Runtime Weapon Stage Test", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "Current Weapon",
            weaponController.CurrentWeapon != null
                ? weaponController.CurrentWeapon.WeaponName
                : "None");
        EditorGUILayout.LabelField(
            "Current Stage Index",
            weaponController.CurrentWeapon != null
                ? weaponController.CurrentWeaponStageIndex.ToString()
                : "-");

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button("Equip Weapon"))
            {
                weaponController.EquipWeapon();
            }

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Set Stage 0"))
            {
                weaponController.SetWeaponStage(0);
            }

            if (GUILayout.Button("Advance Stage"))
            {
                weaponController.AdvanceWeaponStage();
            }

            EditorGUILayout.EndHorizontal();
        }

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Enter Play Mode to use the runtime weapon test buttons. " +
                "WeaponUnlockOnSceneStart controls automatic weapon equip.",
                MessageType.Info);
        }
    }
}
