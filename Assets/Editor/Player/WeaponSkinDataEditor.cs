using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WeaponSkinData))]
public sealed class WeaponSkinDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var skin = (WeaponSkinData)target;
        EditorGUILayout.HelpBox("외형 전용 스킨입니다. 기존 무기의 Animator 그래프를 공유하고 클립 길이·반복·Animation Event를 유지하세요. 공격력과 공격 판정은 WeaponData를 사용합니다.", MessageType.Info);
        if (skin.Icon == null) EditorGUILayout.HelpBox("상점에 표시할 무기 아이콘을 지정하세요.", MessageType.Warning);
        if (skin.Weapon == null) return;
        foreach (WeaponStageData stage in skin.Weapon.WeaponStages)
            if (!skin.IsCompatibleWith(skin.Weapon, stage, out string reason))
                EditorGUILayout.HelpBox(reason, MessageType.Error);
    }
}

[CustomEditor(typeof(WeaponSkinCatalog))]
public sealed class WeaponSkinCatalogEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var catalog = (WeaponSkinCatalog)target;
        if (catalog.DefaultSkin == null || catalog.GetIdle(catalog.DefaultSkin) == null)
            EditorGUILayout.HelpBox("기본 스킨과 Animator의 원본 Idle Motion을 지정하세요. Idle 미리보기는 스킨의 Override에서 자동으로 가져옵니다.", MessageType.Error);
        var ids = new System.Collections.Generic.HashSet<string>();
        foreach (WeaponSkinData skin in catalog.Skins)
        {
            if (skin == null) { EditorGUILayout.HelpBox("빈 스킨 항목을 지정하거나 제거하세요.", MessageType.Warning); continue; }
            if (string.IsNullOrWhiteSpace(skin.SkinId) || !ids.Add(skin.SkinId))
                EditorGUILayout.HelpBox("각 스킨은 고유한 Skin Id가 필요합니다. 복제한 스킨의 Id를 변경하세요.", MessageType.Error);
        }
    }
}
