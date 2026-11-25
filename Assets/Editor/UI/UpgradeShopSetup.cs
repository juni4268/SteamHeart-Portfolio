using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class UpgradeShopSetup
{
    [MenuItem("Tools/Project K/Create Shop for Selected NPC")]
    private static void CreateShop()
    {
        InteractableNPC merchant = Selection.activeGameObject != null
            ? Selection.activeGameObject.GetComponent<InteractableNPC>() : null;
        Player player = Object.FindFirstObjectByType<Player>();
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (EditorApplication.isPlayingOrWillChangePlaymode || merchant == null || player == null || canvas == null)
        {
            Debug.LogError("Select an InteractableNPC in a scene containing a Player and Canvas, outside Play Mode.");
            return;
        }
        UpgradeShop existingShop = merchant.GetComponent<UpgradeShop>();
        if (existingShop != null && existingShop.HasRequiredReferences)
        {
            Debug.LogWarning("This NPC already has a fully connected UpgradeShop.", merchant);
            return;
        }
        InteractionKeyGuide guide = merchant.GetComponentInChildren<InteractionKeyGuide>(true);
        if (guide == null)
        {
            Debug.LogError("The selected NPC needs an InteractionKeyGuide child.", merchant);
            return;
        }

        if (existingShop != null && new SerializedObject(existingShop).FindProperty("panel").objectReferenceValue != null)
        {
            ConnectExistingPanel(existingShop, merchant, guide, player);
            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Create Upgrade Shop");
        RectTransform overlay = CreateRect("UpgradeShopPanel", canvas.transform, Vector2.zero, Vector2.zero);
        ApplyPanelStyle(overlay);
        Transform card = overlay.Find("Card");
        Transform purchaseArea = card.Find("UpgradeLayout/PurchaseArea");
        Text scrap = card.Find("WorkshopCurrency/WorkshopScrap").GetComponent<Text>();
        Button attack = purchaseArea.Find("BuyAttack").GetComponent<Button>();
        Button health = purchaseArea.Find("BuyHealthPiece").GetComponent<Button>();
        Button close = card.Find("Close").GetComponent<Button>();
        Text attackText = card.Find("UpgradeLayout/StatsArea/attackSector/Value").GetComponent<Text>();
        Text healthText = card.Find("UpgradeLayout/StatsArea/healthSector/Value").GetComponent<Text>();

        UpgradeShop shop = existingShop != null ? existingShop : Undo.AddComponent<UpgradeShop>(merchant.gameObject);
        SerializedObject serialized = new SerializedObject(shop);
        SetReference(serialized, "merchant", merchant);
        SetReference(serialized, "interactionKeyGuide", guide);
        SetReference(serialized, "player", player);
        SetReference(serialized, "panel", overlay.gameObject);
        SetReference(serialized, "scrapLabel", scrap);
        SetReference(serialized, "attackLabel", attackText);
        SetReference(serialized, "healthLabel", healthText);
        SetReference(serialized, "attackButton", attack);
        SetReference(serialized, "healthButton", health);
        SetReference(serialized, "closeButton", close);
        SetReference(serialized, "materialButton", purchaseArea.Find("BuyMaterial").GetComponent<Button>());
        SetReference(serialized, "hitInvincibilityButton", purchaseArea.Find("BuyHitInvincibility").GetComponent<Button>());
        SetReference(serialized, "confirmButton", purchaseArea.Find("ConfirmUpgrade").GetComponent<Button>());
        serialized.ApplyModifiedProperties();
        PrefabUtility.RecordPrefabInstancePropertyModifications(shop);
        overlay.gameObject.SetActive(false);
        EditorSceneManager.MarkSceneDirty(merchant.gameObject.scene);
        Undo.CollapseUndoOperations(undoGroup);
        Selection.activeGameObject = merchant.gameObject;
        Debug.Log("Upgrade shop connected to " + merchant.name + ". Scene changes are not saved.", shop);
    }

    private static void ConnectExistingPanel(UpgradeShop shop, InteractableNPC merchant,
        InteractionKeyGuide guide, Player player)
    {
        var serialized = new SerializedObject(shop);
        var panel = (GameObject)serialized.FindProperty("panel").objectReferenceValue;
        IndustrialUISetup.ApplyWorkshop(panel.transform);
        string[] fields = { "scrapLabel", "attackLabel", "healthLabel", "attackButton", "healthButton", "closeButton" };
        string[] paths = { "Card/WorkshopCurrency/WorkshopScrap", "Card/UpgradeLayout/StatsArea/attackSector/Value", "Card/UpgradeLayout/StatsArea/healthSector/Value", "Card/UpgradeLayout/PurchaseArea/BuyAttack", "Card/UpgradeLayout/PurchaseArea/BuyHealthPiece", "Card/Close" };
        var values = new Object[fields.Length];
        for (int i = 0; i < fields.Length; i++)
        {
            values[i] = serialized.FindProperty(fields[i]).objectReferenceValue;
            if (values[i] != null) continue;
            Transform child = panel.transform.Find(paths[i]);
            if (child != null) values[i] = i < 3 ? (Object)child.GetComponent<Text>() : child.GetComponent<Button>();
            if (values[i] != null) continue;
            Debug.LogError("The existing shop panel needs " + fields[i] + ". Assign it in the Inspector; no replacement UI was created.", shop);
            return;
        }
        Undo.RecordObject(shop, "Connect Upgrade Shop");
        SetReference(serialized, "merchant", merchant);
        SetReference(serialized, "interactionKeyGuide", guide);
        SetReference(serialized, "player", player);
        SetReference(serialized, "materialButton", panel.transform.Find("Card/UpgradeLayout/PurchaseArea/BuyMaterial").GetComponent<Button>());
        SetReference(serialized, "hitInvincibilityButton", panel.transform.Find("Card/UpgradeLayout/PurchaseArea/BuyHitInvincibility").GetComponent<Button>());
        SetReference(serialized, "confirmButton", panel.transform.Find("Card/UpgradeLayout/PurchaseArea/ConfirmUpgrade").GetComponent<Button>());
        for (int i = 0; i < fields.Length; i++) SetReference(serialized, fields[i], values[i]);
        serialized.ApplyModifiedProperties();
        PrefabUtility.RecordPrefabInstancePropertyModifications(shop);
        EditorSceneManager.MarkSceneDirty(shop.gameObject.scene);
        Debug.Log("Existing upgrade shop connected. Scene changes are not saved.", shop);
    }

    private static void SetReference(SerializedObject target, string name, Object value)
    {
        target.FindProperty(name).objectReferenceValue = value;
    }

    public static void ApplyPanelStyle(RectTransform overlay)
    {
        Font font = AssetDatabase.LoadAssetAtPath<Font>("Assets/UI/Fonts/NeoDunggeunmo.ttf");
        Sprite frame = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Graphics/UI/Pause/PausePanel.png");
        if (font == null || frame == null)
            throw new System.InvalidOperationException("The shop requires NeoDunggeunmo and the pause panel sprite.");

        overlay.anchorMin = Vector2.zero;
        overlay.anchorMax = Vector2.one;
        overlay.offsetMin = overlay.offsetMax = Vector2.zero;
        Image dimmer = GetOrAdd<Image>(overlay.gameObject);
        dimmer.color = new Color(0.025f, 0.03f, 0.02f, 0.8f);
        dimmer.raycastTarget = true;
        Canvas shopCanvas = GetOrAdd<Canvas>(overlay.gameObject);
        shopCanvas.overrideSorting = true;
        shopCanvas.sortingLayerID = shopCanvas.rootCanvas.sortingLayerID;
        shopCanvas.sortingOrder = 100;
        GetOrAdd<GraphicRaycaster>(overlay.gameObject);

        // Use the common HUD's 480 x 270 reference resolution directly.
        RectTransform card = CreateRect("Card", overlay, Vector2.zero, new Vector2(220f, 244f));
        card.localScale = Vector3.one;
        Image panelImage = GetOrAdd<Image>(card.gameObject);
        panelImage.sprite = frame;
        panelImage.color = Color.white;
        panelImage.raycastTarget = false;
        Text eyebrow = CreateText("Eyebrow", card, font, "W O R K S H O P", new Vector2(0f, 78f), new Vector2(166f, 10f), 6);
        eyebrow.color = new Color(0.62f, 0.57f, 0.43f);
        CreateText("Title", card, font, "강화 상점", new Vector2(0f, 67f), new Vector2(166f, 22f), 15);

        Image tray = GetOrAdd<Image>(CreateRect("ScrapTray", card, new Vector2(0f, 48f), new Vector2(166f, 19f)).gameObject);
        tray.color = new Color(0.1f, 0.105f, 0.09f);
        tray.raycastTarget = false;
        tray.transform.SetAsFirstSibling();
        Image scrapIcon = GetOrAdd<Image>(CreateRect("ScrapIcon", card, new Vector2(-69f, 48f), new Vector2(12f, 12f)).gameObject);
        scrapIcon.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Graphics/UI/UI_Scrap.png");
        scrapIcon.preserveAspect = true;
        scrapIcon.raycastTarget = false;
        Text scrap = CreateText("Scrap", card, font, "보유 고철  0", new Vector2(9f, 48f), new Vector2(142f, 16f), 10);
        scrap.color = new Color(0.88f, 0.79f, 0.52f);
        scrap.resizeTextForBestFit = true;
        scrap.resizeTextMinSize = 7;
        scrap.resizeTextMaxSize = 10;
        Image divider = GetOrAdd<Image>(CreateRect("Divider", card, new Vector2(0f, 33f), new Vector2(166f, 1f)).gameObject);
        divider.color = new Color(0.47f, 0.38f, 0.22f);
        divider.raycastTarget = false;

        Button attack = CreateButton("BuyAttack", card, font,
            "영구 공격력 증가\n현재 추가 공격력 +0", new Vector2(0f, 8f), new Vector2(166f, 44f), out Text attackText);
        ConfigureUpgrade(attack, attackText, font, "공격력 +1", 12f, -7f);

        Button health = CreateButton("BuyHealthPiece", card, font,
            "3개 수집 시 체력 +1\n현재 체력 보너스 +0",
            new Vector2(0f, -42f), new Vector2(166f, 52f), out Text healthText);
        ConfigureUpgrade(health, healthText, font, "체력 조각 +1", 17f, 2f);

        UpgradeShopView view = GetOrAdd<UpgradeShopView>(overlay.gameObject);
        SerializedObject viewData = new SerializedObject(view);
        SetReference(viewData, "attackHeading", attack.transform.Find("Heading").GetComponent<Text>());
        SetReference(viewData, "healthHeading", health.transform.Find("Heading").GetComponent<Text>());
        ConfigurePrice(attack, font, viewData, "attack", UpgradeSettings.Shared.MaterialScrapCost);
        ConfigurePrice(health, font, viewData, "health", UpgradeSettings.Shared.MaterialScrapCost);
        Text counter = CreateText("PieceCounter", health.transform, font, "0 / 3", new Vector2(0f, -17f), new Vector2(48f, 14f), 11);
        SetReference(viewData, "materialPriceLabel", counter);
        SerializedProperty slots = viewData.FindProperty("materialSlots");
        // The shared workshop layout replaces these legacy health-piece visuals.
        slots.arraySize = 3;
        for (int i = 0; i < slots.arraySize; i++)
        {
            RectTransform slot = CreateRect($"Piece{i + 1}", health.transform,
                new Vector2(-68f + i * 16f, -17f), new Vector2(13f, 13f));
            Image piece = GetOrAdd<Image>(slot.gameObject);
            piece.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Graphics/UI/UI_Heart.png");
            piece.preserveAspect = true;
            piece.raycastTarget = false;
            piece.color = viewData.FindProperty("emptyPieceColor").colorValue;
            Outline edge = GetOrAdd<Outline>(slot.gameObject);
            edge.effectColor = new Color(0.55f, 0.46f, 0.28f);
            edge.effectDistance = new Vector2(0.5f, -0.5f);
            slots.GetArrayElementAtIndex(i).objectReferenceValue = piece;
        }
        viewData.ApplyModifiedPropertiesWithoutUndo();

        CreateButton("Close", card, font, "닫기  [Esc]", new Vector2(0f, -80f), new Vector2(166f, 18f), out Text closeText);
        closeText.fontSize = 10;
        IndustrialUISetup.ApplyWorkshop(overlay);
    }

    private static void ConfigureUpgrade(Button button, Text label, Font font, string title,
        float titleY, float labelY)
    {
        Text heading = CreateText("Heading", button.transform, font, title, new Vector2(-25f, titleY), new Vector2(100f, 14f), 10);
        heading.alignment = TextAnchor.MiddleLeft;
        RectTransform rect = label.rectTransform;
        rect.anchoredPosition = new Vector2(-25f, labelY);
        rect.sizeDelta = new Vector2(100f, 20f);
        label.alignment = TextAnchor.MiddleLeft;
        label.fontSize = 7;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = 6;
        label.resizeTextMaxSize = 7;
    }

    private static void ConfigurePrice(Button button, Font font, SerializedObject view, string prefix, int cost)
    {
        Text purchase = CreateText("PurchaseLabel", button.transform, font, $"{cost:N0} 고철 · 구매",
            Vector2.zero, new Vector2(112f, 16f), 8);
        Text status = CreateText("Status", button.transform, font, string.Empty,
            Vector2.zero, new Vector2(132f, 10f), 7);
        SetReference(view, prefix + "PurchaseLabel", purchase);
        SetReference(view, prefix + "StatusLabel", status);
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        return target.TryGetComponent<T>(out T component) ? component : target.AddComponent<T>();
    }

    private static RectTransform CreateRect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        Transform existing = parent.Find(name);
        GameObject created = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform));
        created.layer = parent.gameObject.layer;
        if (existing == null) Undo.RegisterCreatedObjectUndo(created, "Create Upgrade Shop UI");
        RectTransform rect = (RectTransform)created.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static Text CreateText(string name, Transform parent, Font font, string value,
        Vector2 position, Vector2 size, int fontSize)
    {
        RectTransform rect = CreateRect(name, parent, position, size);
        Text text = GetOrAdd<Text>(rect.gameObject);
        text.font = font;
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(0.9f, 0.85f, 0.68f);
        text.raycastTarget = false;
        text.supportRichText = true;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    private static Button CreateButton(string name, Transform parent, Font font, string value,
        Vector2 position, Vector2 size, out Text label)
    {
        RectTransform rect = CreateRect(name, parent, position, size);
        Image background = GetOrAdd<Image>(rect.gameObject);
        background.color = Color.white;
        Button button = GetOrAdd<Button>(rect.gameObject);
        button.targetGraphic = background;
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.14f, 0.145f, 0.12f);
        colors.highlightedColor = new Color(0.32f, 0.27f, 0.16f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(0.22f, 0.18f, 0.1f);
        colors.disabledColor = new Color(0.095f, 0.1f, 0.085f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        button.navigation = new Navigation { mode = Navigation.Mode.Vertical };
        GetOrAdd<PauseMenuPointerSelection>(rect.gameObject);
        Outline outline = GetOrAdd<Outline>(rect.gameObject);
        outline.effectColor = new Color(0.43f, 0.35f, 0.21f, 0.8f);
        outline.effectDistance = new Vector2(0.5f, -0.5f);
        label = CreateText("Label", rect, font, value, Vector2.zero, size - new Vector2(24f, 8f), 19);
        return button;
    }
}
