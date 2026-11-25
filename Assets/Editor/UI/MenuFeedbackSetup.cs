using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class MenuFeedbackSetup
{
    [MenuItem("Tools/Project K/Apply Menu Feedback")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Apply menu feedback outside Play Mode.");

        foreach (string name in new[] { "GameSettingsPanel", "ControlsGuide", "PauseMenu", "TitleScreen", "Canvas", "InteractionKeyGuide" })
        {
            string path = "Assets/Prefabs/UI/" + name + ".prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (Button button in root.GetComponentsInChildren<Button>(true))
                {
                    if (name == "ControlsGuide" && button.transform.parent != root.transform) continue;
                    if (PrefabUtility.GetNearestPrefabInstanceRoot(button.gameObject) != null) continue;
                    if (name == "Canvas" && !button.transform.IsChildOf(root.transform.Find("UpgradeShopPanel"))) continue;
                    GetOrAdd<UIButtonPressFeedback>(button.gameObject);
                }
                if (name == "PauseMenu")
                {
                    Transform overlay = root.transform.Find("Overlay");
                    ConfigureTransition(overlay, (RectTransform)overlay.Find("MetalPanel"));
                }
                else if (name == "TitleScreen")
                {
                    Transform main = root.transform.Find("MainMenu");
                    Transform modal = root.transform.Find("Modal");
                    ConfigureTransition(main, (RectTransform)main);
                    ConfigureTransition(modal, (RectTransform)modal.Find("MenuPanel"));
                }
                else if (name == "Canvas")
                {
                    Transform shop = root.transform.Find("UpgradeShopPanel");
                    ConfigureWorkshop(shop);
                }
                else if (name == "InteractionKeyGuide")
                {
                    Transform canvas = root.transform.Find("PromptCanvas");
                    CanvasGroup group = GetOrAdd<CanvasGroup>(canvas.gameObject);
                    group.blocksRaycasts = false;
                    group.interactable = false;
                    var guide = new SerializedObject(root.GetComponent<InteractionKeyGuide>());
                    guide.FindProperty("presentation").objectReferenceValue = group;
                    guide.FindProperty("visualRoot").objectReferenceValue = canvas;
                    guide.ApplyModifiedPropertiesWithoutUndo();
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
    }

    public static void ConfigureWorkshop(Transform shop)
    {
        ConfigureTransition(shop, (RectTransform)shop.Find("Card"));
        foreach (Button button in shop.GetComponentsInChildren<Button>(true))
            GetOrAdd<UIButtonPressFeedback>(button.gameObject);
    }

    private static void ConfigureTransition(Transform root, RectTransform content)
    {
        var data = new SerializedObject(GetOrAdd<MenuPanelTransition>(root.gameObject));
        data.FindProperty("group").objectReferenceValue = GetOrAdd<CanvasGroup>(root.gameObject);
        data.FindProperty("content").objectReferenceValue = content;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component =>
        target.TryGetComponent(out T component) ? component : target.AddComponent<T>();
}
