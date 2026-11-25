using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class IndustrialUITests
{
    [Test]
    public void SettingsBoardShowsAllColumnsAndKeepsTemplatesOutOfNavigation()
    {
        WithPrefab("GameSettingsPanel", root =>
        {
            var panel = root.GetComponent<GameSettingsPanel>();
            var data = new SerializedObject(panel);
            var columns = data.FindProperty("controlColumns");
            var resets = data.FindProperty("resetButtons");
            Assert.That(columns.arraySize, Is.EqualTo(3));
            Assert.That(resets.arraySize, Is.EqualTo(3));
            Invoke(panel, "ConfigureNavigation");
            for (int i = 0; i < 3; i++)
            {
                var column = (RectTransform)columns.GetArrayElementAtIndex(i).objectReferenceValue;
                Assert.That(column.gameObject.activeSelf, Is.True);
                var controls = column.GetComponentsInChildren<Selectable>();
                var reset = (Button)resets.GetArrayElementAtIndex(i).objectReferenceValue;
                Assert.That(reset.transform.IsChildOf(column), Is.True);
                Assert.That(controls.Last(), Is.EqualTo(reset));
                Assert.That(controls.Length, Is.EqualTo(i == 0 ? 5 : i == 1 ? 4 : 6));
                foreach (var control in controls)
                {
                    Assert.That(control.navigation.mode, Is.EqualTo(Navigation.Mode.Explicit));
                    Assert.That(control.navigation.selectOnDown, Is.Not.Null);
                }
            }
            Assert.That(panel.FirstSelection, Is.TypeOf<Slider>());
            Assert.That(root.GetComponentsInChildren<Slider>().Length, Is.EqualTo(4));
            Assert.That(root.GetComponentsInChildren<Toggle>().Length, Is.EqualTo(6));
        });
    }

    [Test]
    public void VolumeSlidersUseHorizontalInputToChangeValuesWithoutMovingFocus()
    {
        WithPrefab("GameSettingsPanel", root =>
        {
            var panel = root.GetComponent<GameSettingsPanel>();
            Invoke(panel, "ConfigureNavigation");
            EventSystem previous = EventSystem.current;
            var events = new GameObject("VolumeSliderNavigationTest", typeof(EventSystem));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(events, root.scene);
            var eventSystem = events.GetComponent<EventSystem>();
            try
            {
                Invoke(eventSystem, "OnEnable");
                EventSystem.current = eventSystem;
                foreach (Slider slider in root.GetComponentsInChildren<Slider>())
                {
                    slider.SetValueWithoutNotify(0.5f);
                    slider.Select();
                    slider.OnMove(new AxisEventData(eventSystem) { moveDir = MoveDirection.Right });
                    Assert.That(slider.value, Is.GreaterThan(0.5f), slider.name);
                    Assert.That(eventSystem.currentSelectedGameObject, Is.EqualTo(slider.gameObject));
                    slider.OnMove(new AxisEventData(eventSystem) { moveDir = MoveDirection.Left });
                    Assert.That(slider.value, Is.EqualTo(0.5f).Within(0.0001f), slider.name);
                    Assert.That(eventSystem.currentSelectedGameObject, Is.EqualTo(slider.gameObject));
                }
            }
            finally
            {
                Invoke(eventSystem, "OnDisable");
                Object.DestroyImmediate(events);
                if (previous != null) EventSystem.current = previous;
            }
        });
    }

    [Test]
    public void TabCyclesAcrossVisibleSettingsColumns()
    {
        var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/UI/GameSettingsPanel.prefab"));
        EventSystem previous = EventSystem.current;
        var events = new GameObject("SettingsNavigationTest", typeof(EventSystem));
        try
        {
            var panel = root.GetComponent<GameSettingsPanel>();
            // EventSystem is not ExecuteAlways; register it explicitly in this EditMode test.
            Invoke(events.GetComponent<EventSystem>(), "OnEnable");
            EventSystem.current = events.GetComponent<EventSystem>();
            panel.FirstSelection.Select();
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.Not.Null);
            foreach (string expected in new[] { "DisplayMode", "InteractionKeys", "Volume0" })
            {
                Invoke(panel, "SwitchFocusPane");
                Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo(expected));
            }
        }
        finally
        {
            Object.DestroyImmediate(root);
            Invoke(events.GetComponent<EventSystem>(), "OnDisable");
            Object.DestroyImmediate(events);
            if (previous != null) EventSystem.current = previous;
        }
    }

    [TestCase("PauseMenu", typeof(PauseMenu))]
    [TestCase("TitleScreen", typeof(TitleMenu))]
    public void MenusKeepAllRequiredReferences(string name, Type type)
    {
        WithPrefab(name, root =>
        {
            var component = root.GetComponent(type);
            var valid = type.GetProperty("HasRequiredReferences", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(valid.GetValue(component), Is.True);
            foreach (var image in root.GetComponentsInChildren<Image>(true)
                         .Where(image => image.name == "IndustrialFrame" || image.name == "PanelBackground"))
                Assert.That(image.sprite, Is.Not.Null, image.name);
        });
    }

    [TestCase("ControlsGuide", "LampLeft,LampRight")]
    [TestCase("GameSettingsPanel", "Heading,CategoryTitle,HeaderDivider,CategoryDivider,FooterDivider,Category0,Category1,Category2,ResetCategory,BottomSeam,LampLeft,LampRight")]
    [TestCase("PauseMenu", "Overlay/MetalPanel/Eyebrow,Overlay/MetalPanel/Divider,Overlay/MetalPanel/MainPage/LampLeft,Overlay/MetalPanel/MainPage/LampRight")]
    [TestCase("TitleScreen", "MainMenu/PanelBackground,MainMenu/IndustrialFrame,MainMenu/Eyebrow,MainMenu/Divider,Modal/MenuPanel/Eyebrow,Modal/MenuPanel/Divider")]
    public void MenusDoNotKeepHiddenLegacyObjects(string name, string paths)
    {
        WithPrefab(name, root =>
        {
            foreach (string path in paths.Split(','))
                Assert.That(root.transform.Find(path), Is.Null, path);
        });
    }

    [Test]
    public void WorkshopKeepsBoundPricesAndPurchaseButtons()
    {
        WithPrefab("Canvas", root =>
        {
            var view = root.GetComponentInChildren<UpgradeShopView>(true);
            Assert.That(view.HasRequiredReferences, Is.True);
            foreach (string path in new[] { "Card/UpgradeLayout/PurchaseArea/BuyAttack",
                         "Card/UpgradeLayout/PurchaseArea/BuyHealthPiece", "Card/Close" })
            {
                var button = view.transform.Find(path).GetComponent<Button>();
                Assert.That(button.targetGraphic, Is.Not.Null);
                Assert.That(button.GetComponent<Image>().sprite, Is.Not.Null);
            }
            foreach (string name in new[] { "AttackCard", "HealthCard", "BuyAttack", "BuyHealthPiece" })
                Assert.That(view.transform.Find("Card/" + name), Is.Null, name);
        });
    }

    [Test]
    public void WorkshopLayoutCanBeReappliedWithoutLegacyObjects()
    {
        WithPrefab("Canvas", root =>
        {
            var panel = root.transform.Find("UpgradeShopPanel");
            IndustrialUISetup.ApplyWorkshop(panel);
            Assert.That(panel.GetComponent<UpgradeShopView>().HasRequiredReferences, Is.True);
            Assert.That(panel.Find("Card/BuyAttack"), Is.Null);
            Assert.That(panel.Find("Card/UpgradeLayout/PurchaseArea/MaterialHeading"), Is.Null);
        });
    }

    [Test]
    public void NewWorkshopDoesNotKeepLegacyObjects()
    {
        var root = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
        var panel = new GameObject("UpgradeShopPanel", typeof(RectTransform));
        panel.transform.SetParent(root.transform, false);
        try
        {
            UpgradeShopSetup.ApplyPanelStyle((RectTransform)panel.transform);
            Assert.That(panel.GetComponent<UpgradeShopView>().HasRequiredReferences, Is.True);
            Assert.That(panel.transform.Find("Card/BuyAttack"), Is.Null);
            Assert.That(panel.transform.Find("Card/UpgradeLayout/PurchaseArea/MaterialHeading"), Is.Null);
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void WorkshopNavigationUsesVerticalButtonPositions()
    {
        var root = new GameObject("ShopNavigationTest", typeof(RectTransform), typeof(Canvas));
        EventSystem previous = EventSystem.current;
        var events = new GameObject("ShopNavigationEvents", typeof(EventSystem));
        var eventSystem = events.GetComponent<EventSystem>();
        try
        {
            Invoke(eventSystem, "OnEnable");
            EventSystem.current = eventSystem;
            Button CreateButton(string name, Vector2 position)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(root.transform, false);
                ((RectTransform)go.transform).anchoredPosition = position;
                return go.GetComponent<Button>();
            }

            var attack = CreateButton("Attack", new Vector2(-60, 40));
            var health = CreateButton("Health", new Vector2(0, 40));
            var invincibility = CreateButton("Invincibility", new Vector2(60, 40));
            var material = CreateButton("Material", new Vector2(0, -40));
            var confirm = CreateButton("Confirm", new Vector2(0, -60));
            var close = CreateButton("Close", new Vector2(90, -90));
            confirm.interactable = false;

            var shop = root.AddComponent<UpgradeShop>();
            var data = new SerializedObject(shop);
            data.FindProperty("panel").objectReferenceValue = root;
            data.FindProperty("attackButton").objectReferenceValue = attack;
            data.FindProperty("healthButton").objectReferenceValue = health;
            data.FindProperty("hitInvincibilityButton").objectReferenceValue = invincibility;
            data.FindProperty("materialButton").objectReferenceValue = material;
            data.FindProperty("confirmButton").objectReferenceValue = confirm;
            data.FindProperty("closeButton").objectReferenceValue = close;
            data.ApplyModifiedPropertiesWithoutUndo();

            shop.RefreshNavigation();
            Assert.That(health.navigation.selectOnDown, Is.EqualTo(material));
            Assert.That(material.navigation.selectOnUp, Is.EqualTo(health));
            Assert.That(health.navigation.selectOnLeft, Is.EqualTo(attack));
            Assert.That(health.navigation.selectOnRight, Is.EqualTo(invincibility));

            material.Select();
            confirm.interactable = true;
            material.interactable = false;
            Assert.That(eventSystem.currentSelectedGameObject, Is.Null,
                "Unity clears selection when the last material purchase disables its button.");
            shop.RefreshNavigation();
            Assert.That(health.navigation.selectOnDown, Is.EqualTo(confirm));
            Assert.That(eventSystem.currentSelectedGameObject, Is.EqualTo(confirm.gameObject));
            confirm.OnMove(new AxisEventData(eventSystem) { moveDir = MoveDirection.Up });
            Assert.That(eventSystem.currentSelectedGameObject, Is.EqualTo(health.gameObject));

            confirm.Select();
            material.interactable = true;
            confirm.interactable = false;
            Assert.That(eventSystem.currentSelectedGameObject, Is.Null,
                "Consuming materials disables the selected upgrade confirmation button.");
            shop.RefreshNavigation();
            Assert.That(eventSystem.currentSelectedGameObject, Is.EqualTo(material.gameObject));
            material.OnMove(new AxisEventData(eventSystem) { moveDir = MoveDirection.Up });
            Assert.That(eventSystem.currentSelectedGameObject, Is.EqualTo(health.gameObject));

            material.Select();
            material.interactable = false;
            shop.RefreshNavigation();
            Assert.That(eventSystem.currentSelectedGameObject, Is.EqualTo(close.gameObject));
        }
        finally
        {
            Object.DestroyImmediate(root);
            Invoke(eventSystem, "OnDisable");
            Object.DestroyImmediate(events);
            if (previous != null) EventSystem.current = previous;
        }
    }

    [Test]
    public void ShopConfirmSubmitsOnlyTheSelectedUsableShopButton()
    {
        EventSystem previous = EventSystem.current;
        var events = new GameObject("ShopConfirmEvents", typeof(EventSystem));
        var eventSystem = events.GetComponent<EventSystem>();
        var root = new GameObject("ShopConfirmRoot", typeof(RectTransform));
        var outside = new GameObject("Outside", typeof(RectTransform), typeof(Image), typeof(Button));
        try
        {
            Invoke(eventSystem, "OnEnable");
            EventSystem.current = eventSystem;
            var selected = new GameObject("Selected", typeof(RectTransform), typeof(Image), typeof(Button));
            selected.transform.SetParent(root.transform, false);
            var button = selected.GetComponent<Button>();
            int calls = 0;
            button.onClick.AddListener(() => calls++);
            int outsideCalls = 0;
            outside.GetComponent<Button>().onClick.AddListener(() => outsideCalls++);
            var shop = root.AddComponent<UpgradeShop>();
            var data = new SerializedObject(shop);
            data.FindProperty("panel").objectReferenceValue = root;
            data.ApplyModifiedPropertiesWithoutUndo();
            typeof(UpgradeShop).GetField("presentation", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(shop, root.AddComponent<MenuPanelTransition>());
            typeof(UpgradeShop).GetField("<IsOpen>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(shop, true);
            var confirm = typeof(UpgradeShop).GetMethod("OnConfirm", BindingFlags.Instance | BindingFlags.NonPublic);
            void Submit() => confirm.Invoke(shop, new object[] { default(UnityEngine.InputSystem.InputAction.CallbackContext) });

            eventSystem.SetSelectedGameObject(selected);
            Submit();
            Assert.That(calls, Is.EqualTo(1));
            button.interactable = false;
            eventSystem.SetSelectedGameObject(selected);
            Submit();
            Assert.That(calls, Is.EqualTo(1), "Disabled buttons cannot be submitted.");
            eventSystem.SetSelectedGameObject(outside);
            Submit();
            Assert.That(outsideCalls, Is.Zero, "A selection outside the shop cannot be submitted.");
        }
        finally
        {
            var shop = root.GetComponent<UpgradeShop>();
            if (shop != null)
                typeof(UpgradeShop).GetField("<IsOpen>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(shop, false);
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(outside);
            Invoke(eventSystem, "OnDisable");
            Object.DestroyImmediate(events);
            if (previous != null) EventSystem.current = previous;
        }
    }

    [Test]
    public void SelectionHighlightMovesToTheNewButtonOnly()
    {
        WithPrefab("GameSettingsPanel", root =>
        {
            EventSystem previous = EventSystem.current;
            var events = new GameObject("SelectionHighlightTest", typeof(EventSystem));
            try
            {
                var eventSystem = events.GetComponent<EventSystem>();
                Invoke(eventSystem, "OnEnable");
                EventSystem.current = eventSystem;
                var first = root.transform.Find("Sound/ResetDefaults").GetComponent<Button>();
                var second = root.transform.Find("Display/ResetDefaults").GetComponent<Button>();
                Invoke(first.GetComponent<UISelectionIndicator>(), "OnEnable");
                Invoke(second.GetComponent<UISelectionIndicator>(), "OnEnable");
                first.Select();
                Assert.That(first.transform.Find("FocusMarker").GetComponent<Image>().enabled, Is.True);
                second.Select();
                Assert.That(first.transform.Find("FocusMarker").GetComponent<Image>().enabled, Is.False);
                Assert.That(first.image.overrideSprite.name, Is.EqualTo("05_secondary_button"));
                Assert.That(second.transform.Find("FocusMarker").GetComponent<Image>().enabled, Is.True);
                Assert.That(second.image.overrideSprite.name, Is.EqualTo("06_primary_button"));
                Assert.That(first.targetGraphic.GetComponent<Outline>().enabled, Is.False);
            }
            finally
            {
                Invoke(events.GetComponent<EventSystem>(), "OnDisable");
                Object.DestroyImmediate(events);
                if (previous != null) EventSystem.current = previous;
            }
        });
    }

    [Test]
    public void ControlsMenusShareLayoutAndKeepReadableRowsInsideTheirFrame()
    {
        var shared = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/ControlsGuide.prefab").transform;
        foreach (string name in new[] { "ControlsGuide", "PauseMenu", "TitleScreen", "Canvas" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/" + name + ".prefab");
            Transform guide = prefab.GetComponentInChildren<ControlsGuideFeedback>(true).transform;
            foreach (string path in new[] { "IndustrialFrame", "TitlePlate", "BoardHeading", "DeviceDiagram", "Back", "ResetBindings", "BindingHint" })
            {
                var expected = (RectTransform)shared.Find(path);
                var actual = (RectTransform)guide.Find(path);
                Assert.That(actual.anchoredPosition, Is.EqualTo(expected.anchoredPosition), name + ": " + path);
                Assert.That(actual.sizeDelta, Is.EqualTo(expected.sizeDelta), name + ": " + path);
            }
            Assert.That(((RectTransform)guide.Find("IndustrialFrame")).sizeDelta, Is.EqualTo(new Vector2(432, 252)));
            Assert.That(guide.Find("DeviceHint").gameObject.activeSelf, Is.False);
            Assert.That(guide.Find("MovementHeading").GetComponent<Text>().text, Is.EqualTo("방향 조작"));
            Assert.That(guide.Find("ActionHeading").GetComponent<Text>().text, Is.EqualTo("액션"));
            Assert.That(guide.Find("SkillHeading").GetComponent<Text>().text, Is.EqualTo("스킬"));
            Assert.That(guide.Find("ActionHeading").gameObject.activeSelf, Is.True);
            Assert.That(guide.Find("Action0").GetComponent<Text>().text, Is.EqualTo("왼쪽"));
            Assert.That(guide.Find("ActionRight").GetComponent<Text>().text, Is.EqualTo("오른쪽"));
            Assert.That(guide.Find("Action5").GetComponent<Text>().text, Is.EqualTo("강타"));
            Assert.That(guide.Find("Action6").GetComponent<Text>().text, Is.EqualTo("카운터"));
            foreach (string heading in new[] { "MovementHeading", "ActionHeading", "SkillHeading", "SystemHeading" })
            {
                Assert.That(guide.Find(heading).GetComponent<Text>().fontSize, Is.EqualTo(7));
                var plate = guide.Find(heading + "Plate").GetComponent<Image>();
                Assert.That(plate.gameObject.activeSelf, Is.True);
                Assert.That(plate.raycastTarget, Is.False);
                Assert.That(plate.GetComponent<Selectable>(), Is.Null);
                Assert.That(plate.transform.GetSiblingIndex(), Is.LessThan(guide.Find(heading).GetSiblingIndex()));
            }
            for (int i = 0; i < 10; i++)
            {
                Text text = guide.Find("Action" + i).GetComponent<Text>();
                Assert.That(text.fontSize, Is.EqualTo(7));
                Assert.That(text.preferredWidth, Is.LessThanOrEqualTo(text.rectTransform.rect.width), text.text);
                var key = (RectTransform)guide.Find("Key" + i);
                float labelRight = text.rectTransform.anchoredPosition.x + text.rectTransform.rect.xMax;
                Assert.That(key.anchoredPosition.x + key.rect.xMin - labelRight, Is.EqualTo(8).Within(.001f));
            }
            var rightLabel = (RectTransform)guide.Find("ActionRight");
            var rightKey = (RectTransform)guide.Find("KeyRight");
            Assert.That(rightKey.anchoredPosition.x + rightKey.rect.xMin - rightLabel.anchoredPosition.x - rightLabel.rect.xMax,
                Is.EqualTo(8).Within(.001f));
            var attack = (RectTransform)guide.Find("Key2");
            var dash = (RectTransform)guide.Find("Key3");
            var jump = (RectTransform)guide.Find("Key1");
            Assert.That(attack.anchoredPosition.x, Is.EqualTo(dash.anchoredPosition.x));
            Assert.That(dash.anchoredPosition.x, Is.EqualTo(jump.anchoredPosition.x));
            Assert.That(attack.anchoredPosition.y, Is.GreaterThan(dash.anchoredPosition.y));
            Assert.That(dash.anchoredPosition.y, Is.GreaterThan(jump.anchoredPosition.y));
            var down = (RectTransform)guide.Find("Key4");
            var left = (RectTransform)guide.Find("Key0");
            Assert.That(down.anchoredPosition.x, Is.EqualTo(left.anchoredPosition.x));
            Assert.That(rightKey.anchoredPosition.x, Is.EqualTo(left.anchoredPosition.x));
            Assert.That(left.anchoredPosition.y, Is.GreaterThan(rightKey.anchoredPosition.y));
            Assert.That(rightKey.anchoredPosition.y, Is.GreaterThan(down.anchoredPosition.y));
            var slam = (RectTransform)guide.Find("Key5");
            var counter = (RectTransform)guide.Find("Key6");
            Assert.That(slam.anchoredPosition.x, Is.EqualTo(counter.anchoredPosition.x));
            Assert.That(slam.anchoredPosition.y, Is.GreaterThan(counter.anchoredPosition.y));
            foreach (string[] order in new[] { new[] { "Key0", "KeyRight", "Key4" }, new[] { "Key2", "Key3", "Key1" },
                new[] { "Key5", "Key6" }, new[] { "Key7", "Key8", "Key9" } })
                for (int i = 0; i < order.Length - 1; i++)
                {
                    var upper = (RectTransform)guide.Find(order[i]);
                    var lower = (RectTransform)guide.Find(order[i + 1]);
                    float clearance = upper.anchoredPosition.y + upper.rect.yMin - 5f / 4f - 1.2f
                        - lower.anchoredPosition.y - lower.rect.yMax;
                    Assert.That(clearance, Is.GreaterThanOrEqualTo(2), "Pressed rows need room for travel and outline.");
                }
            foreach (var pair in new[] { ("Key0", "MovementHeading"), ("Key2", "ActionHeading"),
                ("Key5", "SkillHeading"), ("Key7", "SystemHeading") })
            {
                var key = (RectTransform)guide.Find(pair.Item1);
                var heading = (RectTransform)guide.Find(pair.Item2);
                float clearance = heading.anchoredPosition.y + heading.rect.yMin
                    - key.anchoredPosition.y - key.rect.yMax;
                Assert.That(clearance, Is.GreaterThanOrEqualTo(2), "Each category needs space above its first keycap.");
            }
            var lastRow = (RectTransform)guide.Find("Key1");
            var divider = (RectTransform)guide.Find("BottomSeam");
            float downBottom = lastRow.anchoredPosition.y + lastRow.rect.yMin - 5f / 4f - 1.2f;
            float dividerTop = divider.anchoredPosition.y + divider.rect.yMax;
            Assert.That(downBottom - dividerTop, Is.GreaterThanOrEqualTo(2), "The pressed keycap and its outline need space above the footer divider.");
            foreach (string buttonName in new[] { "Back", "ResetBindings" })
            {
                var button = (RectTransform)guide.Find(buttonName);
                Assert.That(button.sizeDelta, Is.EqualTo(new Vector2(84, 18)));
                Assert.That(button.anchoredPosition.y + button.rect.yMin, Is.GreaterThan(-106));
                Assert.That(divider.anchoredPosition.y + divider.rect.yMin - button.anchoredPosition.y - button.rect.yMax,
                    Is.GreaterThanOrEqualTo(2));
                Assert.That(button.Find("Label").GetComponent<Text>().fontSize, Is.EqualTo(8));
            }
        }
    }

    [Test]
    public void SceneInputModulesKeepSelectionOnBackgroundClicks()
    {
        foreach (var entry in EditorBuildSettings.scenes.Where(scene => scene.enabled))
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene(entry.path);
            try
            {
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var module in root.GetComponentsInChildren<UnityEngine.InputSystem.UI.InputSystemUIInputModule>(true))
                        Assert.That(module.deselectOnBackgroundClick, Is.False, entry.path);
            }
            finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
        }
    }

    [Test]
    public void PauseAndQuitUseTheSameFrameAndWideMenusShareTheirHeadingHeight()
    {
        foreach (string name in new[] { "PauseMenu", "Canvas" })
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/" + name + ".prefab");
            Transform pause = root.GetComponentInChildren<PauseMenu>(true).transform.Find("Overlay/MetalPanel");
            foreach (string part in new[] { "IndustrialFrame", "PanelBackground" })
                Assert.That(((RectTransform)pause.Find("QuitPage/" + part)).sizeDelta,
                    Is.EqualTo(((RectTransform)pause.Find("MainPage/" + part)).sizeDelta));
        }
        foreach (string name in new[] { "GameSettingsPanel", "ControlsGuide" })
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/" + name + ".prefab");
            var plate = (RectTransform)root.transform.Find("TitlePlate");
            Assert.That(plate.anchoredPosition.y, Is.EqualTo(93));
            Assert.That(plate.sizeDelta.y, Is.EqualTo(20));
            Assert.That(root.transform.Find("BoardHeading").GetComponent<Text>().fontSize, Is.EqualTo(11));
        }
    }

    [Test]
    public void ControlsKeycapSelectionShowsMarkerWithoutChangingItsSprite()
    {
        WithPrefab("ControlsGuide", root =>
        {
            EventSystem previous = EventSystem.current;
            var events = new GameObject("ControlsSelectionTest", typeof(EventSystem));
            try
            {
                var eventSystem = events.GetComponent<EventSystem>();
                Invoke(eventSystem, "OnEnable");
                EventSystem.current = eventSystem;
                Button first = root.transform.Find("Key1/Cap0").GetComponent<Button>();
                Button second = root.transform.Find("Key2/Cap0").GetComponent<Button>();
                Invoke(first.GetComponent<UISelectionIndicator>(), "OnEnable");
                Invoke(second.GetComponent<UISelectionIndicator>(), "OnEnable");
                var group = root.GetComponent<CanvasGroup>() ?? root.AddComponent<CanvasGroup>();
                group.interactable = false;
                first.Select();
                Assert.That(first.transform.Find("FocusMarker").GetComponent<Image>().enabled, Is.True);
                group.interactable = true;
                Assert.That(first.image.overrideSprite.name, Is.EqualTo("18_keycap_ivory"));
                second.Select();
                Assert.That(first.transform.Find("FocusMarker").GetComponent<Image>().enabled, Is.False);
                Assert.That(second.transform.Find("FocusMarker").GetComponent<Image>().enabled, Is.True);
                Assert.That(second.targetGraphic.GetComponent<Outline>().enabled, Is.True);
            }
            finally
            {
                Invoke(events.GetComponent<EventSystem>(), "OnDisable");
                Object.DestroyImmediate(events);
                if (previous != null) EventSystem.current = previous;
            }
        });
    }

    [Test]
    public void WorkshopUsesOneSharedBalanceAndTheCommonFooterButtonSize()
    {
        WithPrefab("Canvas", root =>
        {
            Transform shop = root.transform.Find("UpgradeShopPanel");
            Assert.That(shop.GetComponent<WeaponSkinShop>().HasRequiredReferences, Is.True);
            Assert.That(shop.Find("Card/SkinPage/SkinList/Scrap").gameObject.activeSelf, Is.False);
            Assert.That(shop.Find("Card/WorkshopCurrency/WorkshopScrap").gameObject.activeSelf, Is.True);
            var close = (RectTransform)shop.Find("Card/Close");
            Assert.That(close.sizeDelta, Is.EqualTo(new Vector2(84, 18)));
            Assert.That(close.Find("Label").GetComponent<Text>().fontSize, Is.EqualTo(8));
        });
    }

    private static void Invoke(object target, string name) => target.GetType()
        .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);

    private static void WithPrefab(string name, Action<GameObject> check)
    {
        var root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/UI/" + name + ".prefab");
        try { check(root); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
