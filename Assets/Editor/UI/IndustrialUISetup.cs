using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Authoring only: the result is ordinary editable UI in the existing Prefabs.
public static class IndustrialUISetup
{
    private const string ExportRoot = "UI_Exports/SettingsBoard_v1";
    private const string ArtRoot = "Assets/Graphics/UI/Industrial";
    private const string MouseArtworkPath = ArtRoot + "/Controls/19_mouse_face.png";
    private static readonly Color Ink = new Color(0.98f, 0.9f, 0.7f);
    private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
    private static Font font;

    [Serializable] private class Manifest { public Entry[] assets; }
    [Serializable] private class Entry
    {
        public string name;
        public string file;
        public SpriteBounds suggestedSpriteRect;
        public float[] suggestedBorderLeftBottomRightTop;
    }
    [Serializable] private class SpriteBounds { public float x, y, width, height; }

    [MenuItem("Tools/Project K/Apply Industrial Menu Design")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Apply the menu design outside Play Mode.");
        ImportSprites();
        font = AssetDatabase.LoadAssetAtPath<Font>("Assets/UI/Fonts/NeoDunggeunmo.ttf");
        if (font == null) throw new InvalidOperationException("NeoDunggeunmo font is required.");
        EditPrefab("GameSettingsPanel", ApplySettings);
        EditPrefab("ControlsGuide", ApplyControls);
        EditPrefab("PauseMenu", ApplyPause);
        EditPrefab("TitleScreen", ApplyTitle);
        EditPrefab("Canvas", root => ApplyWorkshop(root.transform.Find("UpgradeShopPanel")));
        AssetDatabase.SaveAssets();
        FixSelection();
        MenuFeedbackSetup.Apply();
        Debug.Log("Industrial menu design saved: settings, controls, pause, title and workshop Prefabs.");
    }

    private static void ImportSprites()
    {
        var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(ExportRoot + "/sprite-manifest.json"));
        var factories = new SpriteDataProviderFactories();
        factories.Init();
        foreach (Entry entry in manifest.assets)
        {
            string destination = ArtRoot + "/" + entry.file;
            if (!File.Exists(destination))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(ExportRoot + "/" + entry.file, destination);
            }
            AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(destination);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 100;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = 4096;
            var textureSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(textureSettings);
            textureSettings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(textureSettings);
            var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            var previous = provider.GetSpriteRects().FirstOrDefault(r => r.name == entry.name);
            var id = previous != null ? previous.spriteID : GUID.Generate();
            SpriteBounds b = entry.suggestedSpriteRect;
            float[] border = entry.suggestedBorderLeftBottomRightTop;
            provider.SetSpriteRects(new[] { new SpriteRect {
                name = entry.name, spriteID = id, alignment = SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f), rect = new Rect(b.x, b.y, b.width, b.height),
                border = new Vector4(border[0], border[1], border[2], border[3])
            } });
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
                new[] { new SpriteNameFileIdPair(entry.name, id) });
            provider.Apply();
            importer.SaveAndReimport();
            Sprites[entry.name] = AssetDatabase.LoadAllAssetsAtPath(destination).OfType<Sprite>().Single();
        }
    }

    private static void EditPrefab(string name, Action<GameObject> edit)
    {
        string path = "Assets/Prefabs/UI/" + name + ".prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try { edit(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // Adjust existing objects only, so scene-specific edits and component references remain intact.
    [MenuItem("Tools/Project K/Fit Industrial Menu Layout")]
    public static void FitExistingLayout()
    {
        EditPrefab("GameSettingsPanel", root =>
        {
            Transform t = root.transform;
            Place(t, 0, 0, 432, 252);
            Place(t.Find("PanelBackground"), 0, 0, 422, 242);
            Place(t.Find("IndustrialFrame"), 0, 0, 432, 252);
            FitHeading(t, 93, 112);
            Inactive(t, "LampLeft", "LampRight");
            string[] names = { "Sound", "Display", "Gameplay" };
            float[] widths = { 118, 112, 136 };
            float[] positions = { -132, -10, 119 };
            for (int i = 0; i < names.Length; i++)
            {
                Transform column = t.Find(names[i]);
                Place(column, positions[i], -5, widths[i], 146);
                Place(column.Find("SectionBorder"), 0, 0, widths[i], 146);
                Place(column.Find("CategoryPlate"), 0, 64, widths[i] - 10, 20);
                Place(column.Find("CategoryHeading"), 0, 64, widths[i] - 24, 18);
                Place(column.Find("ResetDefaults"), 0, -58, widths[i] - 30, 16);
                Place(column.Find("ResetDefaults/Label"), 0, 0, widths[i] - 42, 12);
            }
            Transform sound = t.Find("Sound");
            for (int i = 0; i < 4; i++)
            {
                float y = 44 - i * 24;
                Place(sound.Find("Volume" + i + "Title"), -14, y, 66, 12);
                Place(sound.Find("Volume" + i + "Value"), 40, y, 24, 12);
                Transform slider = sound.Find("Volume" + i);
                Place(slider, 0, y - 12, 98, 14);
                Place(slider.Find("Track"), 0, 0, 98, 9);
            }
            Transform display = t.Find("Display");
            foreach (var item in new[] { ("DisplayMode", 44f), ("Resolution", 6f) })
            {
                Place(display.Find(item.Item1 + "Title"), 0, item.Item2, 98, 12);
                Place(display.Find(item.Item1), 0, item.Item2 - 18, 98, 19);
            }
            Place(display.Find("VSync"), 0, -37, 100, 18);
            Transform gameplay = t.Find("Gameplay");
            string[] toggles = { "InteractionKeys", "DamageNumbers", "EnemyHealthBars", "BossHealthBar", "AutoSkipWatchedCutscenes" };
            for (int i = 0; i < toggles.Length; i++)
            {
                Transform toggle = gameplay.Find(toggles[i]);
                Place(toggle, 0, 44 - i * 18 - (i > 0 ? 12 : 0), 124, 18);
                Place(toggle.Find("Box"), -54, 0, 15, 15);
                Place(toggle.Find("Label"), 12, 0, 98, 18);
            }
            Place(gameplay.Find("GuideDescription"), 5, 29, 120, 10);
            Place(t.Find("Back"), 142, -94.5f, 84, 18);
            Place(t.Find("Back/Label"), 0, 0, 72, 14);
            Inactive(t, "BottomSeam");
        });
        EditPrefab("ControlsGuide", root =>
        {
            Transform t = root.transform;
            Inactive(t, "LampLeft", "LampRight");
            ApplyControlKeycaps(t);
        });
        EditPrefab("PauseMenu", root =>
        {
            Transform panel = root.transform.Find("Overlay/MetalPanel");
            Inactive(panel.Find("MainPage"), "LampLeft", "LampRight");
            Place(panel.Find("SettingsPage/GameSettingsPanel"), 0, 0, 432, 252);
            Place(panel.Find("Title"), 0, 83, 150, 26);
            Place(panel.Find("MainPage/TitlePlate"), 0, 83, 168, 30);
            Place(panel.Find("QuitPage/TitlePlate"), 0, 83, 168, 30);
        });
        EditPrefab("TitleScreen", root =>
        {
            Transform panel = root.transform.Find("Modal/MenuPanel");
            Place(panel.Find("SettingsPage/GameSettingsPanel"), 0, 0, 432, 252);
            Place(panel.Find("Title"), 0, 83, 150, 26);
            Place(panel.Find("QuitPage/TitlePlate"), 0, 83, 168, 30);
        });
        EditPrefab("Canvas", root => ApplyWorkshop(root.transform.Find("UpgradeShopPanel")));
        AssetDatabase.SaveAssets();
        Debug.Log("Adjusted existing menu Prefab layout without recreating lamps.");
    }

    [MenuItem("Tools/Project K/Fix Menu Selection")]
    public static void FixSelection()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Edit Prefabs outside Play Mode.");
        string[] paths = AssetDatabase.FindAssets("05_secondary_button t:Sprite", new[] { ArtRoot })
            .Select(AssetDatabase.GUIDToAssetPath).ToArray();
        Sprite secondary = paths.SelectMany(AssetDatabase.LoadAllAssetsAtPath).OfType<Sprite>()
            .First(s => s.name == "05_secondary_button");
        Sprite selectedSprite = AssetDatabase.FindAssets("06_primary_button t:Sprite", new[] { ArtRoot })
            .Select(AssetDatabase.GUIDToAssetPath).SelectMany(AssetDatabase.LoadAllAssetsAtPath)
            .OfType<Sprite>().First(s => s.name == "06_primary_button");
        Sprite arrow = AssetDatabase.FindAssets("12_dropdown_arrow t:Sprite", new[] { ArtRoot })
            .Select(AssetDatabase.GUIDToAssetPath).SelectMany(AssetDatabase.LoadAllAssetsAtPath)
            .OfType<Sprite>().First(s => s.name == "12_dropdown_arrow");
        foreach (string name in new[] { "GameSettingsPanel", "ControlsGuide", "PauseMenu", "TitleScreen", "Canvas" })
            EditPrefab(name, root =>
            {
                Transform shop = name == "Canvas" ? root.transform.Find("UpgradeShopPanel") : null;
                foreach (Selectable selectable in root.GetComponentsInChildren<Selectable>(true))
                {
                    if (name == "ControlsGuide" && selectable.transform.parent != root.transform) continue;
                    if (!selectable.gameObject.activeSelf || selectable.targetGraphic == null || selectable is Scrollbar) continue;
                    if (shop != null && !selectable.transform.IsChildOf(shop)) continue;
                    // Nested UI has its own Prefab and must be edited there.
                    if (PrefabUtility.GetNearestPrefabInstanceRoot(selectable.gameObject) != null) continue;
                    var image = selectable.targetGraphic as Image;
                    if (selectable is Button && image != null && image.sprite != null &&
                        image.sprite.name == "06_primary_button")
                    {
                        image.sprite = secondary;
                        var text = selectable.transform.Find("Label")?.GetComponent<Text>();
                        if (text != null) text.color = Ink;
                    }
                    Outline outline = Component<Outline>(selectable.targetGraphic.transform);
                    outline.effectColor = new Color(1f, 0.82f, 0.28f, 1f);
                    outline.effectDistance = new Vector2(1.2f, -1.2f);
                    outline.useGraphicAlpha = true;
                    outline.enabled = false;
                    var indicator = Component<UISelectionIndicator>(selectable.transform);
                    var indicatorData = new SerializedObject(indicator);
                    indicatorData.FindProperty("selectedButtonSprite").objectReferenceValue =
                        selectable is Button && selectable.GetComponentsInChildren<Text>(true).Length == 1
                            ? selectedSprite : null;
                    indicatorData.ApplyModifiedPropertiesWithoutUndo();
                    RectTransform marker = Rect(selectable.transform, "FocusMarker", 0, 0, 4, 6);
                    marker.anchorMin = marker.anchorMax = new Vector2(0, 0.5f);
                    marker.anchoredPosition = new Vector2(-3, 0);
                    marker.localRotation = Quaternion.Euler(0, 0, 90);
                    Image markerImage = Component<Image>(marker);
                    markerImage.sprite = arrow;
                    markerImage.color = Color.white;
                    markerImage.raycastTarget = false;
                    markerImage.enabled = false;
                    if (selectable is Toggle && selectable.name == "Item")
                    {
                        Component<PauseMenuPointerSelection>(selectable.transform);
                        Component<DropdownOptionVisibility>(selectable.transform);
                    }
                }
                if (shop != null)
                {
                    Button attack = shop.Find("Card/UpgradeLayout/PurchaseArea/BuyAttack").GetComponent<Button>();
                    Button health = shop.Find("Card/UpgradeLayout/PurchaseArea/BuyHealthPiece").GetComponent<Button>();
                    Button close = shop.Find("Card/Close").GetComponent<Button>();
                    attack.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = health, selectOnDown = close };
                    health.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = attack, selectOnDown = close };
                    close.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = health };
                }
                if (name != "GameSettingsPanel") return;
                foreach (Dropdown dropdown in root.GetComponentsInChildren<Dropdown>(true))
                {
                    RectTransform content = (RectTransform)dropdown.template.Find("Viewport/Content");
                    content.sizeDelta = new Vector2(0, 24);
                    ScrollRect scroll = dropdown.template.GetComponent<ScrollRect>();
                    scroll.horizontal = false;
                    scroll.vertical = true;
                    scroll.viewport.offsetMin = new Vector2(3, 3);
                    scroll.viewport.offsetMax = new Vector2(-9, -3);
                    RectTransform rail = Rect(dropdown.template, "Scrollbar", 0, 0, 3, 0);
                    rail.anchorMin = new Vector2(1, 0);
                    rail.anchorMax = Vector2.one;
                    rail.offsetMin = new Vector2(-6, 3);
                    rail.offsetMax = new Vector2(-3, -3);
                    Image background = Component<Image>(rail);
                    background.color = new Color(0.12f, 0.07f, 0.04f);
                    RectTransform handle = Rect(rail, "Handle", 0, 0, 0, 0);
                    Stretch(handle);
                    Image grip = Component<Image>(handle);
                    grip.color = new Color(0.92f, 0.62f, 0.24f);
                    Scrollbar scrollbar = Component<Scrollbar>(rail);
                    scrollbar.direction = Scrollbar.Direction.BottomToTop;
                    scrollbar.handleRect = handle;
                    scrollbar.targetGraphic = grip;
                    scrollbar.navigation = new Navigation { mode = Navigation.Mode.None };
                    scroll.verticalScrollbar = scrollbar;
                    scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
                }
            });
        AssetDatabase.SaveAssets();
    }

    private static T Component<T>(Transform t) where T : Component =>
        t.TryGetComponent<T>(out T value) ? value : t.gameObject.AddComponent<T>();

    private static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
    {
        Transform found = parent.Find(name);
        var rect = found != null ? (RectTransform)found :
            (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        if (found == null) { rect.SetParent(parent, false); rect.gameObject.layer = parent.gameObject.layer; }
        Place(rect, x, y, w, h);
        return rect;
    }

    private static void Place(Transform t, float x, float y, float w, float h)
    {
        var r = (RectTransform)t;
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = new Vector2(x, y);
        r.sizeDelta = new Vector2(w, h);
        r.localScale = Vector3.one;
    }

    private static void Stretch(RectTransform r, float horizontal = 0, float vertical = 0)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.pivot = new Vector2(0.5f, 0.5f);
        r.offsetMin = new Vector2(horizontal, vertical);
        r.offsetMax = new Vector2(-horizontal, -vertical);
        r.localScale = Vector3.one;
    }

    private static Image Skin(Transform t, string spriteName, float height, bool center = true)
    {
        Image image = Component<Image>(t);
        image.sprite = Sprites[spriteName];
        image.overrideSprite = null;
        image.color = Color.white;
        image.type = image.sprite.border.sqrMagnitude > 0 ? Image.Type.Sliced : Image.Type.Simple;
        image.pixelsPerUnitMultiplier = image.sprite.rect.height / Mathf.Max(1, height);
        image.fillCenter = center;
        image.raycastTarget = t.GetComponent<Selectable>() != null;
        image.preserveAspect = false;
        foreach (var effect in t.GetComponents<BaseMeshEffect>()) effect.enabled = false;
        return image;
    }

    private static Image Picture(Transform parent, string name, string sprite, float x, float y,
        float w, float h, bool center = true)
    {
        return Skin(Rect(parent, name, x, y, w, h), sprite, h, center);
    }

    private static Text Label(Transform parent, string name, string value, float x, float y,
        float w, float h, int size, TextAnchor alignment = TextAnchor.MiddleCenter)
    {
        Text text = Component<Text>(Rect(parent, name, x, y, w, h));
        text.text = value;
        StyleText(text, size, alignment);
        return text;
    }

    private static void StyleText(Text text, int size, TextAnchor alignment)
    {
        text.font = font;
        text.fontSize = size;
        text.color = Ink;
        text.alignment = alignment;
        text.resizeTextForBestFit = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        foreach (var effect in text.GetComponents<BaseMeshEffect>()) effect.enabled = false;
    }

    private static void Inactive(Transform parent, params string[] names)
    {
        foreach (string name in names) if (parent.Find(name) != null) parent.Find(name).gameObject.SetActive(false);
    }

    private static void Shell(Transform root, float width, float height)
    {
        Image old = root.GetComponent<Image>();
        if (old != null) { old.enabled = false; old.raycastTarget = false; }
        foreach (var effect in root.GetComponents<BaseMeshEffect>()) effect.enabled = false;
        Image background = Picture(root, "PanelBackground", "00_panel_background", 0, 0, width - 10, height - 10);
        background.transform.SetAsFirstSibling();
        Image frame = Picture(root, "IndustrialFrame", "01_outer_frame", 0, 0, width, height, false);
        frame.transform.SetSiblingIndex(1);
    }

    private static void Heading(Transform root, string value, float y, float width = 112)
    {
        Picture(root, "TitlePlate", "03_title_plate", 0, y, width, 20);
        Label(root, "BoardHeading", value, 0, y, width - 42, 18, 11);
        Picture(root, "HeadingGear", "13_gear", width * 0.5f - 22, y, 9, 9);
    }

    private static void FitHeading(Transform root, float y, float width)
    {
        Transform plate = root.Find("TitlePlate");
        Place(plate, 0, y, width, 20);
        Image image = plate.GetComponent<Image>();
        image.pixelsPerUnitMultiplier = image.sprite.rect.height / 20;
        Place(root.Find("BoardHeading"), 0, y, width - 42, 18);
        root.Find("BoardHeading").GetComponent<Text>().fontSize = 11;
        root.Find("BoardHeading").GetComponent<Text>().alignment = TextAnchor.MiddleCenter;
        Place(root.Find("HeadingGear"), width * 0.5f - 22, y, 9, 9);
    }

    private static void Lamps(Transform root, float x, float y)
    {
        Inactive(root, "LampLeft", "LampRight");
    }

    private static Button Button(Transform t, float x, float y, float w, float h, bool primary = false)
    {
        Place(t, x, y, w, h);
        var button = Component<Button>(t);
        button.targetGraphic = Skin(t, primary ? "06_primary_button" : "05_secondary_button", h);
        var colors = ColorBlock.defaultColorBlock;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.15f, 1.12f, 1.04f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(0.72f, 0.67f, 0.57f);
        colors.disabledColor = new Color(0.48f, 0.45f, 0.4f, 0.85f);
        colors.fadeDuration = 0.08f;
        button.transition = Selectable.Transition.ColorTint;
        button.colors = colors;
        Component<PauseMenuPointerSelection>(t);
        Text label = t.Find("Label")?.GetComponent<Text>();
        if (label != null)
        {
            Place(label.transform, 0, 0, w - 12, h - 4);
            StyleText(label, h >= 24 ? 10 : 8, TextAnchor.MiddleCenter);
            if (primary) label.color = new Color(0.2f, 0.1f, 0.055f);
        }
        return button;
    }

    private static void ApplySettings(GameObject root)
    {
        Transform t = root.transform;
        Place(t, 0, 0, 432, 252);
        Shell(t, 432, 252);
        Heading(t, "설정", 93);
        Lamps(t, 140, 100);
        string[] names = { "Sound", "Display", "Gameplay" };
        string[] titles = { "소리", "화면", "게임플레이·UI" };
        float[] widths = { 118, 112, 136 };
        float[] positions = { -132, -10, 119 };
        var serialized = new SerializedObject(root.GetComponent<GameSettingsPanel>());
        var columns = serialized.FindProperty("controlColumns");
        var resets = serialized.FindProperty("resetButtons");
        columns.arraySize = resets.arraySize = 3;
        for (int i = 0; i < names.Length; i++)
        {
            Transform column = t.Find(names[i]);
            column.gameObject.SetActive(true);
            Place(column, positions[i], -5, widths[i], 146);
            Image border = Picture(column, "SectionBorder", "02_section_border", 0, 0, widths[i], 146, false);
            border.transform.SetAsFirstSibling();
            Picture(column, "CategoryPlate", "04_category_plate", 0, 64, widths[i] - 10, 20);
            Label(column, "CategoryHeading", titles[i], 0, 64, widths[i] - 24, 18, 9);
            var reset = Rect(column, "ResetDefaults", 0, -58, widths[i] - 30, 16);
            Label(reset, "Label", "기본값 복원", 0, 0, widths[i] - 42, 12, 8);
            resets.GetArrayElementAtIndex(i).objectReferenceValue = Button(reset, 0, -58, widths[i] - 30, 16);
            columns.GetArrayElementAtIndex(i).objectReferenceValue = column;
        }
        Transform sound = t.Find("Sound");
        for (int i = 0; i < 4; i++)
        {
            float y = 44 - i * 24;
            Transform name = sound.Find("Volume" + i + "Title");
            Place(name, -14, y, 66, 12);
            StyleText(name.GetComponent<Text>(), 8, TextAnchor.MiddleLeft);
            Transform value = sound.Find("Volume" + i + "Value");
            Place(value, 40, y, 24, 12);
            StyleText(value.GetComponent<Text>(), 8, TextAnchor.MiddleRight);
            Slider slider = sound.Find("Volume" + i).GetComponent<Slider>();
            Place(slider.transform, 0, y - 12, 98, 14);
            var hit = slider.GetComponent<Image>();
            hit.sprite = null; hit.color = Color.clear; hit.raycastTarget = true;
            Transform track = slider.transform.Find("Track");
            Place(track, 0, 0, 98, 9);
            Skin(track, "07_slider_track", 9);
            Stretch((RectTransform)slider.transform.Find("FillArea"), 4, 4.5f);
            Image fill = Skin(slider.fillRect, "08_slider_fill", 5);
            fill.type = Image.Type.Tiled;
            fill.pixelsPerUnitMultiplier = fill.sprite.rect.height / 5;
            Stretch((RectTransform)slider.transform.Find("HandleArea"), 5, 7);
            slider.handleRect.anchorMin = slider.handleRect.anchorMax = new Vector2(slider.normalizedValue, 0.5f);
            slider.handleRect.sizeDelta = new Vector2(11, 11);
            slider.handleRect.anchoredPosition = Vector2.zero;
            slider.targetGraphic = Skin(slider.handleRect, "09_slider_handle", 11);
        }
        Transform display = t.Find("Display");
        foreach (var item in new[] { ("DisplayMode", 44f), ("Resolution", 6f) })
        {
            Transform label = display.Find(item.Item1 + "Title");
            Place(label, 0, item.Item2, 98, 12);
            StyleText(label.GetComponent<Text>(), 8, TextAnchor.MiddleLeft);
            Dropdown dropdown = display.Find(item.Item1).GetComponent<Dropdown>();
            StyleDropdown(dropdown, item.Item2 - 18, 98);
        }
        StyleToggle(display.Find("VSync"), -37, 100, "수직 동기화");
        Transform gameplay = t.Find("Gameplay");
        string[] toggles = { "InteractionKeys", "DamageNumbers", "EnemyHealthBars", "BossHealthBar", "AutoSkipWatchedCutscenes" };
        for (int i = 0; i < toggles.Length; i++)
            StyleToggle(gameplay.Find(toggles[i]), 44 - i * 18 - (i > 0 ? 12 : 0), 124, null);
        Place(gameplay.Find("GuideDescription"), 5, 29, 120, 10);
        StyleText(gameplay.Find("GuideDescription").GetComponent<Text>(), 6, TextAnchor.MiddleLeft);
        Transform back = t.Find("Back");
        Button(back, 142, -94.5f, 84, 18, true);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        ApplySettingsSafety(t);
    }

    [MenuItem("Tools/Project K/Apply Menu Confirmations")]
    public static void ApplyMenuSafetyLayout()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Edit menu confirmations outside Play Mode.");
        font = AssetDatabase.LoadAssetAtPath<Font>("Assets/UI/Fonts/NeoDunggeunmo.ttf");
        foreach (var asset in new[] {
            ("00_panel_background", "Background"), ("01_outer_frame", "Background"),
            ("04_category_plate", "Plates"), ("05_secondary_button", "Controls"),
            ("06_primary_button", "Controls"), ("12_dropdown_arrow", "Icons") })
            Sprites[asset.Item1] = AssetDatabase.LoadAssetAtPath<Sprite>(ArtRoot + "/" + asset.Item2 + "/" + asset.Item1 + ".png");
        EditPrefab("ControlsGuide", root => ApplyControlsSafety(root.transform));
        EditPrefab("GameSettingsPanel", root => ApplySettingsSafety(root.transform));
        AssetDatabase.SaveAssets();
    }

    private static void ApplySettingsSafety(Transform root)
    {
        Label(root, "NavigationHint", "Tab 분류 이동",
            -45, -94.5f, 270, 12, 6, TextAnchor.MiddleLeft);
        var data = new SerializedObject(root.GetComponent<GameSettingsPanel>());
        data.FindProperty("displayConfirmation").objectReferenceValue = ConfirmationDialog(root);
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ApplyControlsSafety(Transform root)
    {
        root.Find("Key9/Cap0").GetComponent<Image>().color = new Color(.72f, .67f, .58f);
        root.Find("Action9").GetComponent<Text>().color = new Color(.82f, .75f, .63f);
        Inactive(root.Find("Key9"), "FixedHint");
        MenuConfirmationDialog confirmation = ConfirmationDialog(root);
        Transform card = confirmation.transform.Find("Card");
        Place(card, 0, 0, 200, 90);
        Shell(card, 200, 90);
        Inactive(card, "TitlePlate", "Heading", "Detail");
        Place(card.Find("Message"), 0, 15, 176, 18);
        foreach (var item in new[] { ("Confirm", -43f), ("Cancel", 43f) })
            Button(card.Find(item.Item1), item.Item2, -17, 74, 18);
        var data = new SerializedObject(root.GetComponent<ControlsGuideFeedback>());
        data.FindProperty("resetConfirmation").objectReferenceValue = confirmation;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static MenuConfirmationDialog ConfirmationDialog(Transform root)
    {
        RectTransform overlay = Rect(root, "Confirmation", 0, 0, 432, 252);
        Stretch(overlay);
        overlay.SetAsLastSibling();
        Image shade = Component<Image>(overlay);
        shade.color = new Color(0, 0, 0, .6f);
        shade.raycastTarget = true;
        CanvasGroup modal = Component<CanvasGroup>(overlay);
        modal.ignoreParentGroups = true;
        RectTransform card = Rect(overlay, "Card", 0, 0, 264, 140);
        Shell(card, 264, 140);
        Picture(card, "TitlePlate", "04_category_plate", 0, 44, 210, 20);
        Text heading = Label(card, "Heading", "확인", 0, 44, 184, 18, 11);
        Text message = Label(card, "Message", string.Empty, 0, 11, 222, 22, 9);
        Text detail = Label(card, "Detail", string.Empty, 0, -10, 222, 14, 7);
        Button Option(string name, string caption, float x)
        {
            var rect = Rect(card, name, x, -42, 94, 20);
            Label(rect, "Label", caption, 0, 0, 82, 16, 8);
            var button = Button(rect, x, -42, 94, 20);
            ConfigureWorkshopSelection(rect);
            Component<UIButtonPressFeedback>(rect);
            Component<UIAudioFeedback>(rect);
            return button;
        }
        Button confirm = Option("Confirm", "확인", -56);
        Button cancel = Option("Cancel", "취소", 56);
        confirm.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = cancel, selectOnRight = cancel };
        cancel.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = confirm, selectOnRight = confirm };
        var dialog = Component<MenuConfirmationDialog>(overlay);
        var data = new SerializedObject(dialog);
        data.FindProperty("backgroundControls").objectReferenceValue = Component<CanvasGroup>(root);
        data.FindProperty("heading").objectReferenceValue = heading;
        data.FindProperty("message").objectReferenceValue = message;
        data.FindProperty("detail").objectReferenceValue = detail;
        data.FindProperty("confirmButton").objectReferenceValue = confirm;
        data.FindProperty("cancelButton").objectReferenceValue = cancel;
        data.ApplyModifiedPropertiesWithoutUndo();
        overlay.gameObject.SetActive(false);
        return dialog;
    }

    private static void StyleToggle(Transform t, float y, float width, string caption)
    {
        Place(t, 0, y, width, 18);
        Image hit = t.GetComponent<Image>();
        hit.color = Color.clear; hit.sprite = null; hit.raycastTarget = true;
        Transform box = t.Find("Box");
        Place(box, -width * 0.5f + 8, 0, 15, 15);
        var toggle = t.GetComponent<Toggle>();
        toggle.targetGraphic = Skin(box, "10_checkbox_base", 15);
        Place(box.Find("Checkmark"), 0, 0, 10, 9);
        toggle.graphic = Skin(box.Find("Checkmark"), "11_checkmark", 9);
        Text label = t.Find("Label").GetComponent<Text>();
        if (caption != null) label.text = caption;
        Place(label.transform, 12, 0, width - 26, 18);
        StyleText(label, 8, TextAnchor.MiddleLeft);
    }

    private static void StyleDropdown(Dropdown dropdown, float y, float width)
    {
        Transform t = dropdown.transform;
        Place(t, 0, y, width, 19);
        dropdown.targetGraphic = Skin(t, "05_secondary_button", 19);
        var dropdownColors = ColorBlock.defaultColorBlock;
        dropdownColors.highlightedColor = new Color(1.15f, 1.12f, 1.04f);
        dropdownColors.selectedColor = dropdownColors.highlightedColor;
        dropdownColors.pressedColor = new Color(0.72f, 0.67f, 0.57f);
        dropdown.colors = dropdownColors;
        Place(dropdown.captionText.transform, -6, 0, width - 22, 16);
        StyleText(dropdown.captionText, 8, TextAnchor.MiddleLeft);
        Transform arrow = t.Find("Arrow");
        arrow.GetComponent<Text>().enabled = false;
        Place(arrow, width * 0.5f - 10, 0, 7, 5);
        Picture(arrow, "Sprite", "12_dropdown_arrow", 0, 0, 7, 5);
        RectTransform template = dropdown.template;
        template.anchorMin = template.anchorMax = new Vector2(0.5f, 0);
        template.pivot = new Vector2(0.5f, 1);
        template.anchoredPosition = new Vector2(0, -1);
        template.sizeDelta = new Vector2(width, 100);
        // Keep the rim as thin as a normal field even when the option list grows taller.
        Skin(template, "05_secondary_button", 19);
        var viewport = (RectTransform)template.Find("Viewport");
        Stretch(viewport, 3, 3);
        viewport.GetComponent<Mask>().showMaskGraphic = false;
        var content = (RectTransform)viewport.Find("Content");
        content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0.5f, 1); content.anchoredPosition = Vector2.zero;
        // Padding is included in the template's content height so Dropdown.Show
        // keeps every option inside the inset viewport when the list shrinks.
        content.sizeDelta = new Vector2(0, 24);
        var item = (RectTransform)content.Find("Item");
        item.anchorMin = new Vector2(0, 0.5f); item.anchorMax = new Vector2(1, 0.5f);
        item.sizeDelta = new Vector2(0, 18); item.anchoredPosition = Vector2.zero;
        Stretch((RectTransform)item.Find("Item Background"));
        item.Find("Item Background").GetComponent<Image>().color = new Color(0.63f, 0.37f, 0.14f, 0.7f);
        Transform check = item.Find("Item Checkmark");
        Place(check, -width * 0.5f + 8, 0, 6, 6); Skin(check, "11_checkmark", 6);
        Text text = dropdown.itemText;
        Stretch(text.rectTransform, 12, 0);
        StyleText(text, 8, TextAnchor.MiddleLeft);
        template.gameObject.SetActive(false);
    }

    private static void ApplyControls(GameObject root)
    {
        Transform t = root.transform;
        Place(t, 0, 0, 432, 252);
        Shell(t, 432, 252);
        Heading(t, "조작 안내", 93, 140);
        Lamps(t, 130, 97);
        ApplyControlKeycaps(t);
    }

    [MenuItem("Tools/Project K/Apply Controls Keycaps")]
    public static void ApplyControlsKeycapLayout()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Edit the controls guide outside Play Mode.");
        EditPrefab("ControlsGuide", root => ApplyControlKeycaps(root.transform));
        AssetDatabase.SaveAssets();
    }

    private static void ApplyControlKeycaps(Transform root)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtRoot + "/Controls/18_keycap_ivory.png");
        if (sprite == null) throw new InvalidOperationException("The controls keycap sprite is required.");
        Sprites["18_keycap_ivory"] = sprite;
        Sprites["02_section_border"] = AssetDatabase.LoadAssetAtPath<Sprite>(ArtRoot + "/Background/02_section_border.png");
        Sprites["04_category_plate"] = AssetDatabase.LoadAssetAtPath<Sprite>(ArtRoot + "/Plates/04_category_plate.png");
        foreach (var asset in new[] {
            ("00_panel_background", "Background"), ("01_outer_frame", "Background"),
            ("05_secondary_button", "Controls"), ("06_primary_button", "Controls"), ("12_dropdown_arrow", "Icons") })
            Sprites[asset.Item1] = AssetDatabase.LoadAssetAtPath<Sprite>(ArtRoot + "/" + asset.Item2 + "/" + asset.Item1 + ".png");
        if (font == null) font = AssetDatabase.LoadAssetAtPath<Font>("Assets/UI/Fonts/NeoDunggeunmo.ttf");
        Place(root, 0, 0, 432, 252);
        Shell(root, 432, 252);
        FitHeading(root, 93, 140);
        Inactive(root, "PrimarySection", "SystemSection", "PrimaryPlate", "SystemPlate", "DeviceHint");
        ApplyDeviceDiagram(root);
        void CategoryHeading(string name, string caption, float x)
        {
            Text heading = Label(root, name, caption, x, -5, 86, 10, 7);
            heading.gameObject.SetActive(true);
            RectTransform plate = Rect(root, name + "Plate", x, -5, 86, 10);
            plate.SetSiblingIndex(3);
            Image background = Component<Image>(plate);
            background.sprite = null;
            background.color = new Color(.18f, .105f, .05f, .86f);
            background.raycastTarget = false;
            RectTransform rule = Rect(plate, "Rule", 0, 0, 0, .5f);
            rule.anchorMin = Vector2.zero;
            rule.anchorMax = new Vector2(1, 0);
            rule.pivot = new Vector2(.5f, 0);
            Image line = Component<Image>(rule);
            line.color = new Color(.66f, .47f, .25f, .8f);
            line.raycastTarget = false;
        }
        CategoryHeading("MovementHeading", "방향 조작", -141);
        CategoryHeading("ActionHeading", "액션", -47);
        CategoryHeading("SkillHeading", "스킬", 47);
        CategoryHeading("SystemHeading", "상호작용·시스템", 141);
        Text rightAction = Label(root, "ActionRight", "오른쪽", -160.5f, -45, 47, 14, 7, TextAnchor.MiddleRight);
        RectTransform rightKey = Rect(root, "KeyRight", -113.5f, -46, 31, 14);
        Key[][] bindings = {
            new[] { Key.LeftArrow, Key.RightArrow }, new[] { Key.C }, new[] { Key.Z },
            new[] { Key.X }, new[] { Key.DownArrow }, new[] { Key.A },
            new[] { Key.S }, new[] { Key.F }, new[] { Key.UpArrow }, new[] { Key.Escape }
        };
        var feedback = new SerializedObject(Component<ControlsGuideFeedback>(root));
        SerializedProperty keycaps = feedback.FindProperty("keycaps");
        keycaps.arraySize = bindings.Sum(keys => keys.Length);
        var input = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Settings/Input/PlayerInputSet.inputactions");
        string[] actions = { "Movement", "Jump", "Attack", "Dash", "Down", "GroundSlam", "Counter", "Interact", "PortalUse" };
        var capButtons = new List<Button>();
        int[] columns = { 0, 1, 1, 1, 0, 2, 2, 3, 3, 3 };
        int[] rows = { 0, 2, 0, 1, 2, 0, 1, 0, 1, 2 };
        float[] labelCenters = { -160.5f, -66.5f, 27.5f, 121.5f };
        float[] keyCenters = { -113.5f, -19.5f, 74.5f, 168.5f };
        int bindingIndex = 0;
        for (int i = 0; i < 10; i++)
        {
            int column = columns[i];
            float xCenter = keyCenters[column];
            const float keyWidth = 31;
            float y = -24 - rows[i] * 22;
            const float capHeight = 14;
            Transform action = root.Find("Action" + i);
            Place(action, labelCenters[column], y + 1, 47, 14);
            if (i == 0) action.GetComponent<Text>().text = "왼쪽";
            else if (i == 5) action.GetComponent<Text>().text = "강타";
            else if (i == 6) action.GetComponent<Text>().text = "카운터";
            StyleText(action.GetComponent<Text>(), 7, TextAnchor.MiddleRight);

            Transform key = root.Find("Key" + i);
            Place(key, xCenter, y, keyWidth, capHeight);
            Image oldPlate = key.GetComponent<Image>();
            oldPlate.enabled = oldPlate.raycastTarget = false;
            if (i == 0 && key.Find("Cap1") is Transform previousRight)
                previousRight.SetParent(rightKey, false);
            // Keep the original key names as editable source text; each visible cap gets its own legend.
            Text source = key.Find("Label").GetComponent<Text>();
            if (i == 1) source.text = "C";
            else if (i == 3) source.text = "X";
            string[] names = source.text.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(value => value.Trim()).ToArray();
            source.gameObject.SetActive(false);
            foreach (Transform child in key)
                if (child.name.StartsWith("Cap", StringComparison.Ordinal) || child.name.StartsWith("Separator", StringComparison.Ordinal))
                    child.gameObject.SetActive(false);
            float[] widths = names.Select(value => value == "Esc" ? 23f : 17f).ToArray();
            float x = -keyWidth * 0.5f;
            for (int n = 0; n < names.Length; n++)
            {
                Transform parent = i == 0 && n == 1 ? rightKey : key;
                float capX = i == 0 ? -keyWidth * .5f : x;
                RectTransform cap = Rect(parent, "Cap" + n, capX + widths[n] * 0.5f, 0, widths[n], capHeight);
                cap.gameObject.SetActive(true);
                Image image = Skin(cap, "18_keycap_ivory", capHeight);
                Text legend = Label(cap, "Label", names[n], 0, 2, widths[n] - 8, 10, names[n].Length == 1 ? 8 : 6);
                legend.color = new Color(0.22f, 0.17f, 0.11f);
                legend.resizeTextForBestFit = true;
                legend.resizeTextMinSize = 4;
                legend.resizeTextMaxSize = legend.fontSize;
                SerializedProperty binding = keycaps.GetArrayElementAtIndex(bindingIndex++);
                binding.FindPropertyRelative("key").intValue = (int)bindings[i][n];
                binding.FindPropertyRelative("image").objectReferenceValue = image;
                binding.FindPropertyRelative("legend").objectReferenceValue = legend;
                binding.FindPropertyRelative("actionLabel").objectReferenceValue = i == 0 && n == 1 ? rightAction : action.GetComponent<Text>();
                binding.FindPropertyRelative("separator").objectReferenceValue = null;
                if (i < actions.Length)
                {
                    InputAction inputAction = input.FindAction("Player/" + actions[i], true);
                    InputBinding sourceBinding = i == 0
                        ? inputAction.bindings.First(item => item.isPartOfComposite && item.name == (n == 0 ? "left" : "right"))
                        : inputAction.bindings[0];
                    binding.FindPropertyRelative("bindingId").stringValue = sourceBinding.id.ToString();
                    Button button = Component<Button>(cap);
                    button.targetGraphic = image;
                    button.transition = Selectable.Transition.None;
                    button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
                    capButtons.Add(button);
                    image.raycastTarget = true;
                    binding.FindPropertyRelative("button").objectReferenceValue = button;
                    Outline outline = Component<Outline>(cap);
                    outline.effectColor = new Color(1f, 0.82f, 0.28f, 1f);
                    outline.effectDistance = new Vector2(1.2f, -1.2f);
                    outline.enabled = false;
                    var indicator = new SerializedObject(Component<UISelectionIndicator>(cap));
                    indicator.FindProperty("selectedButtonSprite").objectReferenceValue = null;
                    indicator.ApplyModifiedPropertiesWithoutUndo();
                    Image marker = Picture(cap, "FocusMarker", "12_dropdown_arrow", -3, 0, 3, 4);
                    marker.rectTransform.anchorMin = marker.rectTransform.anchorMax = new Vector2(0, .5f);
                    marker.rectTransform.localRotation = Quaternion.Euler(0, 0, 90);
                    marker.raycastTarget = false;
                    marker.enabled = false;
                    Component<PauseMenuPointerSelection>(cap);
                    Component<UIAudioFeedback>(cap);
                }
                x += widths[n];
                if (i == 0 || n == names.Length - 1) continue;
                Text separator = Label(key, "Separator" + n, "/", x + 4, 1, 4, 10, 6);
                binding.FindPropertyRelative("separator").objectReferenceValue = separator.rectTransform;
                separator.gameObject.SetActive(true);
                x += 8;
            }
        }
        Text hint = Label(root, "BindingHint", "키변경 : 키캡 선택 → Enter/클릭 → 새 입력 · Esc 뒤로", 0, -94.5f, 184, 12, 6);
        Place(root.Find("BottomSeam"), 0, -83, 368, 1);
        feedback.FindProperty("bindingHint").objectReferenceValue = hint;
        Transform reset = root.Find("ResetBindings");
        if (reset == null)
        {
            reset = Object.Instantiate(root.Find("Back").gameObject, root).transform;
            reset.name = "ResetBindings";
            reset.GetComponent<Button>().onClick = new Button.ButtonClickedEvent();
        }
        Button(reset, -142, -94.5f, 84, 18);
        ConfigureWorkshopSelection(reset);
        Text resetLabel = reset.Find("Label").GetComponent<Text>();
        resetLabel.text = "기본값 복원";
        Place(resetLabel.transform, 0, 0, 72, 14);
        StyleText(resetLabel, 8, TextAnchor.MiddleCenter);
        capButtons.Add(reset.GetComponent<Button>());
        capButtons.Add(root.Find("Back").GetComponent<Button>());
        (int up, int down, int left, int right)[] links = {
            (-1, 1, -1, 3), (0, 5, -1, 4), (4, 10, 5, 7), (-1, 4, 0, 6),
            (3, 2, 1, 7), (1, 10, -1, 2), (-1, 7, 3, 8), (6, 11, 4, 9),
            (-1, 9, 6, -1), (8, 11, 7, -1), (5, -1, -1, 11), (9, -1, 10, -1)
        };
        Selectable At(int index) => index < 0 ? null : capButtons[index];
        for (int i = 0; i < capButtons.Count; i++)
            capButtons[i].navigation = new Navigation { mode = Navigation.Mode.Explicit,
                selectOnUp = At(links[i].up), selectOnDown = At(links[i].down),
                selectOnLeft = At(links[i].left), selectOnRight = At(links[i].right) };
        Button(root.Find("Back"), 142, -94.5f, 84, 18);
        ConfigureWorkshopSelection(root.Find("Back"));
        feedback.FindProperty("resetButton").objectReferenceValue = reset.GetComponent<Button>();
        feedback.FindProperty("backButton").objectReferenceValue = root.Find("Back").GetComponent<Button>();
        feedback.ApplyModifiedPropertiesWithoutUndo();
        ApplyControlsSafety(root);
        ApplyGamepadControls(root);
    }

    [MenuItem("Tools/Project K/Apply Gamepad Controls Guide")]
    public static void ApplyGamepadControlsGuide()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Edit the controls guide outside Play Mode.");
        font = AssetDatabase.LoadAssetAtPath<Font>("Assets/UI/Fonts/NeoDunggeunmo.ttf");
        EditPrefab("ControlsGuide", root => ApplyGamepadControls(root.transform));
    }

    private static void ApplyGamepadControls(Transform root)
    {
        Color cream = new Color(.91f, .85f, .73f);
        Color brown = new Color(.39f, .30f, .20f);
        Color amber = new Color(.99f, .82f, .49f);
        var entries = new List<(string path, string alternate, Image image, Text label)>();
        ControlButtonGraphic Surface(Transform parent, string name, float x, float y, float w, float h,
            ControlButtonGraphic.ButtonShape shape, Color color)
        {
            var image = Component<ControlButtonGraphic>(Rect(parent, name, x, y, w, h));
            image.Shape = shape;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }
        Button Tab(string name, string caption, float x)
        {
            const float width = 102;
            var image = Surface(root, name, x, 93, width, 18, ControlButtonGraphic.ButtonShape.Keycap, brown);
            image.raycastTarget = true;
            Button button = Component<Button>(image.transform);
            button.targetGraphic = image;
            var colors = ColorBlock.defaultColorBlock;
            colors.selectedColor = colors.highlightedColor = new Color(1.25f, 1.2f, 1.1f);
            colors.pressedColor = new Color(.75f, .72f, .65f);
            button.colors = colors;
            Label(image.transform, "Label", caption, 0, 1, width - 12, 10, 7).color = cream;
            if (image.transform.Find("ShoulderHint") is Transform shoulderHint) Object.DestroyImmediate(shoulderHint.gameObject);
            var line = Component<Image>(Rect(image.transform, "ActiveUnderline", 0, -7, width - 12, 1));
            line.color = amber;
            line.raycastTarget = false;
            Component<PauseMenuPointerSelection>(image.transform);
            Component<UIAudioFeedback>(image.transform);
            return button;
        }
        Button keyboardTab = Tab("KeyboardTab", "키보드·마우스", -133);
        Button gamepadTab = Tab("GamepadTab", "패드", 133);
        foreach (string name in new[] { "TabLeftHint", "TabRightHint" })
            if (root.Find(name) is Transform hint) Object.DestroyImmediate(hint.gameObject);
        RectTransform page = Rect(root, "GamepadPage", 0, 0, 432, 252);
        page.SetSiblingIndex(root.Find("DeviceDiagram").GetSiblingIndex() + 1);
        RectTransform diagram = Rect(page, "Diagram", 0, 0, 372, 94);
        diagram.localScale = Vector3.one * .9f;
        diagram.anchoredPosition = new Vector2(0, 4.35f);
        Surface(diagram, "Body", 0, 41, 174, 78, ControlButtonGraphic.ButtonShape.Gamepad, new Color(.86f, .78f, .64f));
        Font deviceFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/UI/Fonts/RobotoMono-Medium.ttf");
        Text Caption(Transform parent, string name, string caption, float x, float y, float w, float h, int size)
        {
            Text text = Label(parent, name, caption, x, y, w, h, size);
            text.font = deviceFont;
            text.color = brown;
            return text;
        }
        Image PadButton(Transform parent, string name, string path, string caption,
            float x, float y, float w, float h, ControlButtonGraphic.ButtonShape shape, Color legend)
        {
            var image = Surface(parent, name, x, y, w, h, shape, cream);
            if (!string.IsNullOrEmpty(caption))
                Caption(image.transform, "Label", caption, 0, .5f, w, h < 7 ? h + 3 : h - 1, h < 10 || caption.Length > 2 ? 5 : 7).color = legend;
            entries.Add(("<Gamepad>/" + path, null, image, null));
            return image;
        }
        PadButton(diagram, "LT", "leftTrigger", "LT", -51, 82, 24, 6, ControlButtonGraphic.ButtonShape.Keycap, brown);
        PadButton(diagram, "RT", "rightTrigger", "RT", 51, 82, 24, 6, ControlButtonGraphic.ButtonShape.Keycap, brown);
        PadButton(diagram, "LB", "leftShoulder", "LB", -51, 74, 40, 8, ControlButtonGraphic.ButtonShape.Keycap, brown);
        PadButton(diagram, "RB", "rightShoulder", "RB", 51, 74, 40, 8, ControlButtonGraphic.ButtonShape.Keycap, brown);
        Surface(diagram, "Home", 0, 69, 7, 7, ControlButtonGraphic.ButtonShape.Circle, brown);
        Caption(diagram, "HomeMark", "+", 0, 69.5f, 7, 7, 5).color = cream;
        var sticks = new List<RectTransform>();
        foreach (var stick in new[] { ("LeftStick", -50f, 51f), ("RightStick", 28f, 31f) })
        {
            Surface(diagram, stick.Item1 + "Well", stick.Item2, stick.Item3, 29, 29,
                ControlButtonGraphic.ButtonShape.Circle, new Color(.57f, .47f, .34f));
            var knob = Surface(diagram, stick.Item1, stick.Item2, stick.Item3 + .5f, 22, 22,
                ControlButtonGraphic.ButtonShape.Circle, new Color(.74f, .65f, .51f));
            Surface(knob.transform, "Inset", 0, .3f, 17, 17, ControlButtonGraphic.ButtonShape.Circle, new Color(.66f, .56f, .42f));
            entries.Add(("<Gamepad>/" + (stick.Item1 == "LeftStick" ? "leftStickPress" : "rightStickPress"), null, knob, null));
            sticks.Add(knob.rectTransform);
        }
        Surface(diagram, "DpadWell", -28, 31, 28, 28, ControlButtonGraphic.ButtonShape.Circle, new Color(.67f, .58f, .45f));
        PadButton(diagram, "DpadUp", "dpad/up", "", -28, 39, 7, 9, ControlButtonGraphic.ButtonShape.Keycap, brown);
        PadButton(diagram, "DpadDown", "dpad/down", "", -28, 23, 7, 9, ControlButtonGraphic.ButtonShape.Keycap, brown);
        PadButton(diagram, "DpadLeft", "dpad/left", "", -36, 31, 9, 7, ControlButtonGraphic.ButtonShape.Keycap, brown);
        PadButton(diagram, "DpadRight", "dpad/right", "", -20, 31, 9, 7, ControlButtonGraphic.ButtonShape.Keycap, brown);
        Surface(diagram, "DpadCenter", -28, 31, 7, 7, ControlButtonGraphic.ButtonShape.Keycap, cream);
        PadButton(diagram, "A", "buttonSouth", "A", 52, 40, 12, 12, ControlButtonGraphic.ButtonShape.Circle, new Color(.26f, .44f, .29f));
        PadButton(diagram, "B", "buttonEast", "B", 65, 53, 12, 12, ControlButtonGraphic.ButtonShape.Circle, new Color(.64f, .30f, .25f));
        PadButton(diagram, "X", "buttonWest", "X", 39, 53, 12, 12, ControlButtonGraphic.ButtonShape.Circle, new Color(.25f, .39f, .55f));
        PadButton(diagram, "Y", "buttonNorth", "Y", 52, 66, 12, 12, ControlButtonGraphic.ButtonShape.Circle, new Color(.64f, .46f, .17f));
        PadButton(diagram, "Select", "select", "..", -12, 52, 13, 8, ControlButtonGraphic.ButtonShape.Keycap, brown);
        PadButton(diagram, "Start", "start", "", 12, 52, 13, 8, ControlButtonGraphic.ButtonShape.Keycap, brown);
        var start = diagram.Find("Start");
        for (int i = 0; i < 3; i++)
        {
            var line = Component<Image>(Rect(start, "Line" + i, 0, 2 - i * 1.5f, 5, .5f));
            line.color = brown;
            line.raycastTarget = false;
        }
        Caption(diagram, "Brand", "STEAM HEART", 0, 38, 42, 7, 4);
        foreach (string name in new[] { "DirectionHint", "MenuHint" })
            if (diagram.Find(name) is Transform hint) hint.SetParent(page, false);
        Label(page, "DirectionHint", "왼쪽 스틱\n/ 방향키", -139, 44, 80, 28, 7);
        Label(page, "MenuHint", "A 선택\nB 뒤로", 139, 44, 80, 28, 7);

        string[] headings = { "방향 조작", "액션", "스킬", "상호작용·시스템" };
        for (int i = 0; i < 4; i++)
        {
            float x = -141 + i * 94;
            var plate = Surface(page, "HeadingPlate" + i, x, -5, 86, 10, ControlButtonGraphic.ButtonShape.Keycap,
                new Color(.18f, .105f, .05f, .86f));
            Label(page, "Heading" + i, headings[i], x, -5, 86, 10, 7);
        }
        var rows = new[] {
            ("Left", "왼쪽", "←", "dpad/left", "leftStick/left", 0, 0),
            ("Right", "오른쪽", "→", "dpad/right", "leftStick/right", 0, 1),
            ("Down", "발판 내려가기", "↓", "dpad/down", "leftStick/down", 0, 2),
            ("Attack", "공격", "X", "buttonWest", "", 1, 0),
            ("Dash", "대시", "B", "buttonEast", "", 1, 1),
            ("Jump", "점프", "A", "buttonSouth", "", 1, 2),
            ("Slam", "강타", "RB", "rightShoulder", "", 2, 0),
            ("Counter", "카운터", "LB", "leftShoulder", "", 2, 1),
            ("Interact", "상호작용", "Y", "buttonNorth", "", 3, 0),
            ("Portal", "지역 이동", "↑", "dpad/up", "leftStick/up", 3, 1),
            ("Pause", "일시정지", "Start", "start", "", 3, 2)
        };
        var feedback = new SerializedObject(root.GetComponent<ControlsGuideFeedback>());
        SerializedProperty gamepadCaps = feedback.FindProperty("gamepadKeycaps");
        gamepadCaps.arraySize = 8;
        var padButtons = new List<Button>();
        var padBindings = new Dictionary<Image, string>();
        string[] rowActions = { "", "", "Down", "Attack", "Dash", "Jump", "GroundSlam", "Counter", "Interact", "PortalUse", "" };
        var input = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Settings/Input/PlayerInputSet.inputactions");
        int rowIndex = 0, capIndex = 0;
        foreach (var row in rows)
        {
            float x = -141 + row.Item6 * 94, y = -24 - row.Item7 * 22;
            Text label = Label(page, row.Item1 + "Action", row.Item2, x - 19.5f, y + 1, 47, 14, 7, TextAnchor.MiddleRight);
            if (page.Find(row.Item1 + "Key/" + row.Item1 + "Cap") is Transform previousCap) previousCap.SetParent(page, false);
            Image cap = PadButton(page, row.Item1 + "Cap", row.Item4, row.Item3, x + 27.5f, y, 25, 14,
                row.Item3.Length == 1 && char.IsLetter(row.Item3[0]) ? ControlButtonGraphic.ButtonShape.Circle : ControlButtonGraphic.ButtonShape.Keycap, brown);
            entries[entries.Count - 1] = ("<Gamepad>/" + row.Item4,
                string.IsNullOrEmpty(row.Item5) ? null : "<Gamepad>/" + row.Item5, cap, label);
            string actionName = rowActions[rowIndex++];
            if (string.IsNullOrEmpty(actionName))
            {
                cap.color = new Color(cream.r * .88f, cream.g * .88f, cream.b * .88f, cream.a);
                cap.rectTransform.anchoredPosition = new Vector2(x + (row.Item1 == "Pause" ? 24.5f : 19), y);
                if (row.Item1 != "Pause") cap.rectTransform.sizeDelta = new Vector2(14, 14);
                continue;
            }
            string id = input.FindAction("Player/" + actionName).bindings.First(binding => binding.path == "<Gamepad>/" + row.Item4).id.ToString();
            RectTransform slot = Rect(page, row.Item1 + "Key", x + 27.5f, y, 31, 14);
            cap.transform.SetParent(slot, false);
            cap.rectTransform.anchoredPosition = Vector2.zero;
            Button button = Component<Button>(cap.transform);
            button.targetGraphic = cap;
            button.transition = Selectable.Transition.None;
            cap.raycastTarget = true;
            Outline outline = Component<Outline>(cap.transform);
            outline.effectColor = new Color(1f, .82f, .28f);
            outline.effectDistance = new Vector2(1.2f, -1.2f);
            outline.enabled = false;
            Component<UISelectionIndicator>(cap.transform);
            Component<PauseMenuPointerSelection>(cap.transform);
            Component<UIAudioFeedback>(cap.transform);
            Text legend = cap.transform.Find("Label").GetComponent<Text>();
            legend.resizeTextForBestFit = true;
            legend.resizeTextMinSize = 4;
            legend.resizeTextMaxSize = 7;
            SerializedProperty binding = gamepadCaps.GetArrayElementAtIndex(capIndex++);
            binding.FindPropertyRelative("bindingId").stringValue = id;
            binding.FindPropertyRelative("image").objectReferenceValue = cap;
            binding.FindPropertyRelative("button").objectReferenceValue = button;
            binding.FindPropertyRelative("legend").objectReferenceValue = legend;
            binding.FindPropertyRelative("actionLabel").objectReferenceValue = label;
            padBindings.Add(cap, id);
            padButtons.Add(button);
        }
        Text padHint = Label(page, "InputHint", "키변경 : 버튼 선택 → A/클릭 → 새 입력\nLB/RB 탭 전환 · B 뒤로", 0, -94.5f, 184, 14, 6);
        feedback.FindProperty("gamepadBindingHint").objectReferenceValue = padHint;
        feedback.ApplyModifiedPropertiesWithoutUndo();
        if (page.Find("TabsHint") is Transform oldTabsHint) Object.DestroyImmediate(oldTabsHint.gameObject);
        var display = new SerializedObject(Component<ControlDeviceDisplay>(page));
        SerializedProperty visuals = display.FindProperty("visuals");
        visuals.arraySize = entries.Count;
        for (int i = 0; i < entries.Count; i++)
        {
            var visual = visuals.GetArrayElementAtIndex(i);
            visual.FindPropertyRelative("controlPath").stringValue = entries[i].path;
            visual.FindPropertyRelative("alternatePath").stringValue = entries[i].alternate;
            visual.FindPropertyRelative("image").objectReferenceValue = entries[i].image;
            visual.FindPropertyRelative("actionLabel").objectReferenceValue = entries[i].label;
            visual.FindPropertyRelative("bindingId").stringValue = padBindings.TryGetValue(entries[i].image, out string id) ? id : string.Empty;
            visual.FindPropertyRelative("legend").objectReferenceValue = !string.IsNullOrEmpty(id) ? entries[i].image.transform.Find("Label").GetComponent<Text>() : null;
            visual.FindPropertyRelative("fixedControl").boolValue = true;
        }
        display.FindProperty("assignedColor").colorValue = cream;
        display.FindProperty("pressedColor").colorValue = new Color(.99f, .82f, .39f);
        display.FindProperty("leftStick").objectReferenceValue = sticks[0];
        display.FindProperty("rightStick").objectReferenceValue = sticks[1];
        display.ApplyModifiedPropertiesWithoutUndo();

        var tabsComponent = Component<ControlsGuideTabs>(root);
        var tabs = new SerializedObject(tabsComponent);
        var keyboardObjects = root.Cast<Transform>().Where(t => t.name == "DeviceDiagram" || t.name.StartsWith("Key") && t.name != "KeyboardTab"
            || t.name.StartsWith("Action") || new[] { "MovementHeading", "MovementHeadingPlate", "SkillHeading", "SkillHeadingPlate",
                "SystemHeading", "SystemHeadingPlate", "BindingHint" }.Contains(t.name)).ToArray();
        var content = tabs.FindProperty("keyboardContent");
        content.arraySize = keyboardObjects.Length;
        for (int i = 0; i < keyboardObjects.Length; i++) content.GetArrayElementAtIndex(i).objectReferenceValue = keyboardObjects[i].gameObject;
        Selectable first = root.Find("Key0/Cap0").GetComponent<Selectable>();
        Button back = root.Find("Back").GetComponent<Button>();
        Button reset = root.Find("ResetBindings").GetComponent<Button>();
        padButtons.Add(reset);
        padButtons.Add(back);
        (int up, int down, int left, int right)[] padLinks = {
            (-1, 8, -1, 3), (-1, 2, 0, 4), (1, 3, 0, 5), (2, 8, 0, 5),
            (-1, 5, 1, 6), (4, 9, 2, 7), (-1, 7, 4, -1), (6, 9, 5, -1)
        };
        Selectable PadAt(int index) => index < 0 ? gamepadTab : padButtons[index];
        for (int i = 0; i < 8; i++) padButtons[i].navigation = new Navigation {
            mode = Navigation.Mode.Explicit, selectOnUp = PadAt(padLinks[i].up), selectOnDown = PadAt(padLinks[i].down),
            selectOnLeft = padLinks[i].left < 0 ? null : PadAt(padLinks[i].left),
            selectOnRight = padLinks[i].right < 0 ? null : PadAt(padLinks[i].right)
        };
        foreach (var field in new[] { ("keyboardTab", (Object)keyboardTab), ("gamepadTab", gamepadTab),
            ("keyboardFirstSelection", first), ("backButton", back), ("gamepadPage", page.gameObject),
            ("gamepadFirstSelection", padButtons[1]), ("gamepadLastSelection", padButtons[7]),
            ("gamepadDownSelection", padButtons[0]), ("resetButton", reset),
            ("keyboardUnderline", keyboardTab.transform.Find("ActiveUnderline").GetComponent<Image>()),
            ("gamepadUnderline", gamepadTab.transform.Find("ActiveUnderline").GetComponent<Image>()),
            ("feedback", root.GetComponent<ControlsGuideFeedback>()) })
            tabs.FindProperty(field.Item1).objectReferenceValue = field.Item2;
        tabs.ApplyModifiedPropertiesWithoutUndo();
        feedback.FindProperty("deviceTabs").objectReferenceValue = tabsComponent;
        feedback.ApplyModifiedPropertiesWithoutUndo();
        keyboardTab.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = gamepadTab, selectOnDown = first };
        gamepadTab.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = keyboardTab, selectOnDown = back };
        foreach (string key in new[] { "Key0/Cap0", "Key2/Cap0", "Key5/Cap0", "Key7/Cap0" })
        {
            Selectable cap = root.Find(key).GetComponent<Selectable>();
            Navigation navigation = cap.navigation;
            navigation.selectOnUp = keyboardTab;
            cap.navigation = navigation;
        }
        gamepadTab.transform.Find("ActiveUnderline").GetComponent<Image>().enabled = false;
        page.gameObject.SetActive(false);
    }

    private static Dictionary<string, Sprite> ImportMouseArtwork()
    {
        var source = new Texture2D(2, 2);
        source.LoadImage(File.ReadAllBytes(MouseArtworkPath));
        int width = source.width, height = source.height;
        Color32[] pixels = source.GetPixels32();
        Object.DestroyImmediate(source);
        int minX = width, minY = height, maxX = 0, maxY = 0;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                if (pixels[y * width + x].a < 64) continue;
                minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y);
                maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y);
            }
        if (minX >= maxX || minY >= maxY) throw new InvalidOperationException("The mouse artwork is empty.");
        Rect bounds = UnityEngine.Rect.MinMaxRect(minX - 2, minY - 2, maxX + 3, maxY + 3);
        Vector2 Point(float x, float y) => new Vector2(x * width, (1f - y) * height);
        var left = new List<Vector2> { Point(.5f,0), Point(0,0), Point(0,.403f), Point(.28f,.403f) };
        for (int step = 1; step <= 11; step++)
        {
            float x = .28f + step * .02f;
            float distance = (x - .5f) / .22f;
            left.Add(Point(x, .453f - .05f * distance * distance));
        }
        left.AddRange(new[] { Point(.5f,.337f), Point(.478f,.333f), Point(.469f,.316f),
            Point(.469f,.232f), Point(.478f,.216f), Point(.5f,.21f) });
        Vector2[] Pill(float x1, float y1, float x2, float y2)
        {
            float r = (x2 - x1) * .25f;
            return new[] { Point(x1+r,y1), Point(x2-r,y1), Point(x2,y1+r), Point(x2,y2-r),
                Point(x2-r,y2), Point(x1+r,y2), Point(x1,y2-r), Point(x1,y1+r) };
        }
        // These Sprite Editor outlines isolate the illustrated buttons without duplicating the bitmap.
        var regions = new (string name, Vector2[] outline)[] {
            ("MouseBody", new[] { bounds.min, new Vector2(bounds.xMin,bounds.yMax), bounds.max, new Vector2(bounds.xMax,bounds.yMin) }),
            ("MouseLeft", left.ToArray()),
            ("MouseRight", left.Select(p => new Vector2(width-p.x,p.y)).Reverse().ToArray()),
            ("MouseMiddle", Pill(.47f,.212f,.53f,.338f)),
            ("MouseBack", Pill(.251f,.482f,.279f,.56f)),
            ("MouseForward", Pill(.251f,.397f,.279f,.47f))
        };
        AssetDatabase.ImportAsset(MouseArtworkPath, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(MouseArtworkPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 100;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.alphaIsTransparency = true;
        importer.maxTextureSize = 2048;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.Tight;
        importer.SetTextureSettings(settings);
        var factories = new SpriteDataProviderFactories();
        factories.Init();
        var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();
        var previous = provider.GetSpriteRects();
        var rects = regions.Select(region => new SpriteRect {
            name = region.name,
            spriteID = previous.FirstOrDefault(r => r.name == region.name)?.spriteID ?? GUID.Generate(),
            alignment = SpriteAlignment.Center, pivot = new Vector2(.5f,.5f),
            rect = UnityEngine.Rect.MinMaxRect(region.outline.Min(p=>p.x),region.outline.Min(p=>p.y),
                region.outline.Max(p=>p.x),region.outline.Max(p=>p.y))
        }).ToArray();
        provider.SetSpriteRects(rects);
        var outlines = provider.GetDataProvider<ISpriteOutlineDataProvider>();
        for (int i = 0; i < rects.Length; i++)
        {
            outlines.SetOutlines(rects[i].spriteID, new List<Vector2[]> {
                regions[i].outline.Select(p => p - rects[i].rect.center).ToArray() });
            outlines.SetTessellationDetail(rects[i].spriteID, 1f);
        }
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
            rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
        provider.Apply();
        importer.SaveAndReimport();
        return AssetDatabase.LoadAllAssetsAtPath(MouseArtworkPath).OfType<Sprite>().ToDictionary(s=>s.name);
    }

    private static void ApplyDeviceDiagram(Transform root)
    {
        Font deviceFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/UI/Fonts/RobotoMono-Medium.ttf");
        if (deviceFont == null) throw new InvalidOperationException("The device label font is required.");
        RectTransform diagram = Rect(root, "DeviceDiagram", 0, 43.5f, 372, 94);
        diagram.SetSiblingIndex(2);
        ControlButtonGraphic Surface(RectTransform rect, ControlButtonGraphic.ButtonShape shape, Color color)
        {
            Image previous = rect.GetComponent<Image>();
            if (previous != null && !(previous is ControlButtonGraphic)) Object.DestroyImmediate(previous);
            var image = Component<ControlButtonGraphic>(rect);
            image.Shape = shape;
            image.sprite = null;
            image.raycastTarget = false;
            image.color = color;
            return image;
        }
        var frame = Surface(Rect(diagram, "KeyboardFrame", -35, 0, 302, 74),
            ControlButtonGraphic.ButtonShape.Chassis, new Color(0.62f, 0.52f, 0.38f));
        Surface(Rect(frame.transform, "Inset", 0, 0.8f, 299, 70),
            ControlButtonGraphic.ButtonShape.Chassis, new Color(0.7f, 0.61f, 0.47f));
        var entries = new List<(string path, Image image, Text label, bool fixedControl)>();
        void Control(string name, string path, string caption, float x, float y, float width, float height, int size,
            bool fixedControl = false, ControlButtonGraphic.ButtonShape shape = ControlButtonGraphic.ButtonShape.Keycap)
        {
            RectTransform rect = Rect(diagram, name, x, y, width, height);
            var image = Surface(rect, shape, new Color(0.91f, 0.85f, 0.73f));
            Text label = Label(rect, "Legend", caption, 0, height > 15 ? 2 : 1,
                Mathf.Max(3, width - 2), Mathf.Max(5, height - 2), size);
            label.color = new Color(0.25f, 0.21f, 0.15f);
            if (caption.All(character => character < 128)) label.font = deviceFont;
            label.resizeTextForBestFit = caption.Length > 1;
            label.resizeTextMinSize = 3;
            label.resizeTextMaxSize = size;
            entries.Add((path, image, label, fixedControl));
        }
        const float unit = 12f, origin = -175f;
        void KeyCell(Key key, string caption, float x, float y, float units = 1, float height = 8.5f)
        {
            Control("Keyboard_" + key, PlayerKeyBindings.PathFor(key), caption,
                x + (unit * units - 1) * 0.5f, y - 38, unit * units - 1, height,
                4, key == Key.Escape);
        }
        KeyCell(Key.Escape, "Esc", origin, 66);
        float[] functionOffsets = { 2, 3, 4, 5, 6.5f, 7.5f, 8.5f, 9.5f, 11, 12, 13, 14 };
        for (int i = 0; i < 12; i++) KeyCell((Key)((int)Key.F1 + i), "F" + (i + 1), origin + functionOffsets[i] * unit, 66);
        void Row(float y, (Key key, string caption, float width)[] keys)
        {
            float x = origin;
            foreach (var key in keys) { KeyCell(key.key, key.caption, x, y, key.width); x += key.width * unit; }
        }
        Row(55, new[] {
            (Key.Backquote,"~",1f),(Key.Digit1,"1",1f),(Key.Digit2,"2",1f),(Key.Digit3,"3",1f),
            (Key.Digit4,"4",1f),(Key.Digit5,"5",1f),(Key.Digit6,"6",1f),(Key.Digit7,"7",1f),
            (Key.Digit8,"8",1f),(Key.Digit9,"9",1f),(Key.Digit0,"0",1f),(Key.Minus,"-",1f),
            (Key.Equals,"=",1f),(Key.Backspace,"Backspace",2f)
        });
        Row(44, new[] {
            (Key.Tab,"Tab",1.5f),(Key.Q,"Q",1f),(Key.W,"W",1f),(Key.E,"E",1f),(Key.R,"R",1f),
            (Key.T,"T",1f),(Key.Y,"Y",1f),(Key.U,"U",1f),(Key.I,"I",1f),(Key.O,"O",1f),(Key.P,"P",1f),
            (Key.LeftBracket,"[",1f),(Key.RightBracket,"]",1f),(Key.Backslash,"\\",1.5f)
        });
        Row(33, new[] {
            (Key.CapsLock,"Caps",1.75f),(Key.A,"A",1f),(Key.S,"S",1f),(Key.D,"D",1f),(Key.F,"F",1f),
            (Key.G,"G",1f),(Key.H,"H",1f),(Key.J,"J",1f),(Key.K,"K",1f),(Key.L,"L",1f),
            (Key.Semicolon,";",1f),(Key.Quote,"'",1f),(Key.Enter,"Enter",2.25f)
        });
        Row(22, new[] {
            (Key.LeftShift,"Shift",2.25f),(Key.Z,"Z",1f),(Key.X,"X",1f),(Key.C,"C",1f),(Key.V,"V",1f),
            (Key.B,"B",1f),(Key.N,"N",1f),(Key.M,"M",1f),(Key.Comma,",",1f),(Key.Period,".",1f),
            (Key.Slash,"/",1f),(Key.RightShift,"Shift",2.75f)
        });
        Row(11, new[] {
            (Key.LeftCtrl,"Ctrl",1.25f),(Key.LeftMeta,"Win",1.25f),(Key.LeftAlt,"Alt",1.25f),
            (Key.Space,"Space",6.25f),(Key.RightAlt,"Alt",1.25f),(Key.RightMeta,"Win",1.25f),
            (Key.ContextMenu,"Menu",1.25f),(Key.RightCtrl,"Ctrl",1.25f)
        });
        KeyCell(Key.PrintScreen,"PrtSc",14,66); KeyCell(Key.ScrollLock,"ScrLk",26,66); KeyCell(Key.Pause,"Pause",38,66);
        KeyCell(Key.Insert,"Ins",14,55); KeyCell(Key.Home,"Home",26,55); KeyCell(Key.PageUp,"PgUp",38,55);
        KeyCell(Key.Delete,"Del",14,44); KeyCell(Key.End,"End",26,44); KeyCell(Key.PageDown,"PgDn",38,44);
        KeyCell(Key.UpArrow,"↑",26,22); KeyCell(Key.LeftArrow,"←",14,11); KeyCell(Key.DownArrow,"↓",26,11); KeyCell(Key.RightArrow,"→",38,11);
        KeyCell(Key.NumLock,"Num",59,55); KeyCell(Key.NumpadDivide,"/",71,55); KeyCell(Key.NumpadMultiply,"*",83,55); KeyCell(Key.NumpadMinus,"-",95,55);
        KeyCell(Key.Numpad7,"7",59,44); KeyCell(Key.Numpad8,"8",71,44); KeyCell(Key.Numpad9,"9",83,44); KeyCell(Key.NumpadPlus,"+",95,38.5f,1,19.5f);
        KeyCell(Key.Numpad4,"4",59,33); KeyCell(Key.Numpad5,"5",71,33); KeyCell(Key.Numpad6,"6",83,33);
        KeyCell(Key.Numpad1,"1",59,22); KeyCell(Key.Numpad2,"2",71,22); KeyCell(Key.Numpad3,"3",83,22); KeyCell(Key.NumpadEnter,"Enter",95,16.5f,1,19.5f);
        KeyCell(Key.Numpad0,"0",59,11,2); KeyCell(Key.NumpadPeriod,".",83,11);

        var mouseSprites = ImportMouseArtwork();
        string materialPath = ArtRoot + "/Controls/MouseButtonFeedback.mat";
        var feedbackMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (feedbackMaterial == null)
        {
            Shader shader = Shader.Find("ProjectK/UI/MouseButtonFeedback");
            if (shader == null) throw new InvalidOperationException("The mouse feedback shader is required.");
            feedbackMaterial = new Material(shader);
            AssetDatabase.CreateAsset(feedbackMaterial, materialPath);
        }
        Sprite artwork = mouseSprites["MouseBody"];
        float mouseScale = 65.6f / artwork.rect.height;
        Image MouseImage(string name, Sprite sprite)
        {
            Vector2 offset = (sprite.rect.center - artwork.rect.center) * mouseScale;
            var rect = Rect(diagram, name, 151 + offset.x, 5 + offset.y,
                sprite.rect.width * mouseScale, sprite.rect.height * mouseScale);
            Graphic previous = rect.GetComponent<Graphic>();
            if (previous != null && previous.GetType() != typeof(Image)) Object.DestroyImmediate(previous);
            Image image = Component<Image>(rect);
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.useSpriteMesh = name != "MouseBody";
            image.material = name == "MouseBody" ? null : feedbackMaterial;
            image.color = Color.white;
            image.raycastTarget = false;
            return image;
        }
        var body = MouseImage("MouseBody", artwork);
        body.raycastTarget = false;
        Inactive(diagram, "MouseCaption");
        (string name, string control)[] mouseButtons = {
            ("MouseLeft", "leftButton"), ("MouseRight", "rightButton"), ("MouseMiddle", "middleButton"),
            ("MouseBack", "backButton"), ("MouseForward", "forwardButton")
        };
        Vector2[] legendPoints = {
            new Vector2(.375f,.273f), new Vector2(.625f,.273f), new Vector2(.5f,.397f),
            new Vector2(.21f,.435f), new Vector2(.21f,.522f)
        };
        for (int i = 0; i < mouseButtons.Length; i++)
        {
            var item = mouseButtons[i];
            Image image = MouseImage(item.name, mouseSprites[item.name]);
            if (i == 2)
            {
                Transform previousLabel = image.transform.Find("Legend");
                if (previousLabel != null) Object.DestroyImmediate(previousLabel.gameObject);
                entries.Add(("<Mouse>/" + item.control, image, null, false));
                continue;
            }
            Vector2 sourcePoint = new Vector2(legendPoints[i].x * artwork.texture.width,
                (1f - legendPoints[i].y) * artwork.texture.height);
            Vector2 labelPosition = body.rectTransform.anchoredPosition + (sourcePoint - artwork.rect.center) * mouseScale;
            if (i >= 3)
                labelPosition = image.rectTransform.anchoredPosition +
                    new Vector2(-image.rectTransform.rect.width * .5f - 7f, 0);
            labelPosition = new Vector2(Mathf.Round(labelPosition.x * 4f), Mathf.Round(labelPosition.y * 4f)) / 4f;
            Vector2 offset = labelPosition - image.rectTransform.anchoredPosition;
            Text label = Label(image.transform, "Legend", "M" + (i+1), offset.x, offset.y, 10, 8, 6,
                i >= 3 ? TextAnchor.MiddleRight : TextAnchor.MiddleCenter);
            label.font = deviceFont;
            label.color = new Color(0.25f, 0.21f, 0.15f);
            entries.Add(("<Mouse>/" + item.control, image, label, false));
        }
        foreach (string name in new[] { "MouseBack", "MouseForward" })
            diagram.Find(name + "/Legend").GetComponent<Text>().color = Ink;
        float leftEdge = frame.rectTransform.anchoredPosition.x + frame.rectTransform.rect.xMin;
        float rightEdge = body.rectTransform.anchoredPosition.x + body.rectTransform.rect.xMax;
        float centerOffset = Mathf.Round((leftEdge + rightEdge) * 2f) / 4f;
        foreach (RectTransform child in diagram)
            if (child.gameObject.activeSelf) child.anchoredPosition -= new Vector2(centerOffset, 0);
        var display = new SerializedObject(Component<ControlDeviceDisplay>(diagram));
        display.FindProperty("unusedColor").colorValue = new Color(0.91f, 0.85f, 0.73f);
        display.FindProperty("assignedColor").colorValue = new Color(0.96f, 0.76f, 0.44f);
        display.FindProperty("pressedColor").colorValue = new Color(0.99f, 0.86f, 0.62f);
        SerializedProperty visuals = display.FindProperty("visuals");
        visuals.arraySize = entries.Count;
        for (int i = 0; i < entries.Count; i++)
        {
            var visual = visuals.GetArrayElementAtIndex(i);
            visual.FindPropertyRelative("controlPath").stringValue = entries[i].path;
            visual.FindPropertyRelative("image").objectReferenceValue = entries[i].image;
            visual.FindPropertyRelative("legend").objectReferenceValue = entries[i].label;
            visual.FindPropertyRelative("fixedControl").boolValue = entries[i].fixedControl;
        }
        display.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ApplyPause(GameObject root)
    {
        Transform panel = root.transform.Find("Overlay/MetalPanel");
        Place(panel, 0, 0, 448, 252);
        panel.GetComponent<Image>().enabled = false;
        Transform title = panel.Find("Title");
        Place(title, 0, 83, 150, 26);
        StyleText(title.GetComponent<Text>(), 14, TextAnchor.MiddleCenter);
        title.SetAsLastSibling();
        Transform main = panel.Find("MainPage");
        Shell(main, 264, 244);
        Picture(main, "TitlePlate", "03_title_plate", 0, 83, 168, 30);
        Lamps(main, 79, 65);
        string[] buttons = { "Resume", "Settings", "Controls", "Quit" };
        for (int i = 0; i < buttons.Length; i++)
            Button(main.Find(buttons[i]), 0, 38 - i * 32, 174, 25, i == 0);
        Place(main.Find("BackHint"), 0, -91, 170, 14);
        StyleText(main.Find("BackHint").GetComponent<Text>(), 8, TextAnchor.MiddleCenter);
        ConfigureQuit(panel.Find("QuitPage"));
        panel.Find("MainPage").gameObject.SetActive(true);
        panel.Find("SettingsPage").gameObject.SetActive(false);
        panel.Find("ControlsPage").gameObject.SetActive(false);
        panel.Find("QuitPage").gameObject.SetActive(false);
        root.transform.Find("Overlay").gameObject.SetActive(false);
    }

    private static void ConfigureQuit(Transform page)
    {
        Shell(page, 264, 244);
        Picture(page, "TitlePlate", "03_title_plate", 0, 83, 168, 30);
        Place(page.Find("Question"), 0, 39, 220, 24);
        StyleText(page.Find("Question").GetComponent<Text>(), 13, TextAnchor.MiddleCenter);
        Place(page.Find("Description"), 0, 3, 220, 34);
        StyleText(page.Find("Description").GetComponent<Text>(), 9, TextAnchor.MiddleCenter);
        Button(page.Find("ConfirmQuit"), 0, -43, 174, 25);
        Button(page.Find("CancelQuit"), 0, -77, 174, 25, true);
    }

    private static void ApplyTitle(GameObject root)
    {
        Transform main = root.transform.Find("MainMenu");
        Place(main, 102, 0, 244, 244);
        Image mainImage = main.GetComponent<Image>();
        if (mainImage != null) { mainImage.enabled = false; mainImage.raycastTarget = false; }
        foreach (var effect in main.GetComponents<BaseMeshEffect>()) effect.enabled = false;
        Place(main.Find("Title"), 0, 86, 190, 30);
        StyleText(main.Find("Title").GetComponent<Text>(), 18, TextAnchor.MiddleCenter);
        Transform page = main.Find("MainPage");
        string[] buttons = { "StartGame", "Settings", "Controls", "Quit" };
        for (int i = 0; i < buttons.Length; i++) Button(page.Find(buttons[i]), 0, 40 - i * 32, 174, 25, i == 0);
        Place(page.Find("BackHint"), 0, -91, 190, 14);
        StyleText(page.Find("BackHint").GetComponent<Text>(), 7, TextAnchor.MiddleCenter);
        Transform panel = root.transform.Find("Modal/MenuPanel");
        Place(panel, 0, 0, 448, 252);
        panel.GetComponent<Image>().enabled = false;
        Place(panel.Find("Title"), 0, 83, 150, 26);
        StyleText(panel.Find("Title").GetComponent<Text>(), 14, TextAnchor.MiddleCenter);
        panel.Find("Title").SetAsLastSibling();
        ConfigureQuit(panel.Find("QuitPage"));
    }

    [MenuItem("Tools/Project K/Apply Workshop Card Layout")]
    public static void ApplyWorkshopLayout()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Edit the workshop outside Play Mode.");
        EditPrefab("Canvas", root => ApplyWorkshop(root.transform.Find("UpgradeShopPanel")));
        AssetDatabase.SaveAssets();
    }

    public static void ApplyWorkshop(Transform overlay)
    {
        if (overlay == null) throw new InvalidOperationException("UpgradeShopPanel is required.");
        if (!Sprites.ContainsKey("00_panel_background"))
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ArtRoot }))
                foreach (Sprite sprite in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)).OfType<Sprite>())
                    Sprites[sprite.name] = sprite;
            font = AssetDatabase.LoadAssetAtPath<Font>("Assets/UI/Fonts/NeoDunggeunmo.ttf");
        }
        Transform card = overlay.Find("Card");
        if (card.Find("UpgradeLayout/PurchaseArea/BuyAttack") != null
            && card.Find("UpgradeLayout/PurchaseArea/BuyHealthPiece") != null
            && card.Find("UpgradeLayout/PurchaseArea/BuyHitInvincibility") != null
            && card.Find("UpgradeLayout/PurchaseArea/BuyMaterial") != null)
        {
            ApplyWorkshopFrame(overlay);
            RemoveLegacyWorkshopObjects(card);
            overlay.gameObject.SetActive(false);
            return;
        }
        Place(card, 0, 0, 384, 252);
        Shell(card, 384, 252);
        Inactive(card, "Eyebrow", "Divider", "ScrapTray", "RowDivider");
        Image titlePlate = Picture(card, "TitlePlate", "03_title_plate", 0, 72, 112, 24);
        titlePlate.enabled = true;
        titlePlate.gameObject.SetActive(true);
        Place(card.Find("Title"), 0, 72, 82, 20);
        card.Find("Title").GetComponent<Text>().text = "제작대";
        StyleText(card.Find("Title").GetComponent<Text>(), 14, TextAnchor.MiddleCenter);
        RectTransform scrapInfo = Rect(card, "ScrapInfo", -25, 72, 0, 18);
        scrapInfo.anchorMin = scrapInfo.anchorMax = new Vector2(1, 0.5f);
        scrapInfo.pivot = new Vector2(1, 0.5f);
        Transform scrapIcon = scrapInfo.Find("ScrapIcon") ?? card.Find("ScrapIcon");
        Transform scrapText = scrapInfo.Find("Scrap") ?? card.Find("Scrap");
        scrapIcon.SetParent(scrapInfo, false);
        scrapText.SetParent(scrapInfo, false);
        Place(scrapIcon, 0, 0, 12, 12);
        Place(scrapText, 0, 0, 110, 16);
        Text scrapLabel = scrapText.GetComponent<Text>();
        StyleText(scrapLabel, 9, TextAnchor.MiddleRight);
        scrapLabel.color = new Color(0.82f, 0.75f, 0.6f);
        scrapLabel.supportRichText = true;
        var iconSize = Component<LayoutElement>(scrapIcon);
        iconSize.minWidth = iconSize.preferredWidth = 12;
        iconSize.minHeight = iconSize.preferredHeight = 12;
        var scrapLayout = Component<HorizontalLayoutGroup>(scrapInfo);
        scrapLayout.childAlignment = TextAnchor.MiddleRight;
        scrapLayout.spacing = 4;
        scrapLayout.childControlWidth = scrapLayout.childControlHeight = true;
        scrapLayout.childForceExpandWidth = scrapLayout.childForceExpandHeight = false;
        var scrapFit = Component<ContentSizeFitter>(scrapInfo);
        scrapFit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrapFit.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
        Picture(card, "HeaderSeam", "16_horizontal_divider", 0, 52, 330, 1);
        var view = new SerializedObject(overlay.GetComponent<UpgradeShopView>());
        // Reuse the existing cards and their explicit bindings when upgrading the layout.
        Transform oldHealthButton = card.Find("BuyHealthPiece");
        Transform healthInfo = card.Find("HealthCard");
        if (healthInfo == null)
        {
            healthInfo = Rect(card, "HealthCard", 0, 0, 156, 54);
            oldHealthButton.Find("Label").SetParent(healthInfo, false);
            oldHealthButton.Find("Heading").SetParent(healthInfo, false);
        }
        Transform attackInfo = card.Find("AttackCard");
        if (attackInfo == null)
        {
            attackInfo = Rect(card, "AttackCard", 0, 0, 156, 54);
            card.Find("BuyAttack/Label").SetParent(attackInfo, false);
            card.Find("BuyAttack/Heading").SetParent(attackInfo, false);
        }
        RectTransform materialInfo = Rect(card, "MaterialCard", -83, 37, 156, 54);
        Inactive(healthInfo, "PieceCounter", "Piece1", "Piece2", "Piece3");
        Inactive(oldHealthButton, "PieceCounter", "Piece1", "Piece2", "Piece3");
        Text counter = Label(materialInfo, "Counter", "0 / 3", 0, 7, 140, 17, 13);
        view.FindProperty("materialPriceLabel").objectReferenceValue = counter;
        var slots = view.FindProperty("materialSlots");
        slots.arraySize = 3;
        for (int i = 0; i < slots.arraySize; i++)
        {
            RectTransform slot = Rect(materialInfo, "Material" + (i + 1), (i - 1) * 16, -8, 9, 9);
            Image piece = Component<Image>(slot);
            piece.sprite = null;
            piece.raycastTarget = false;
            slots.GetArrayElementAtIndex(i).objectReferenceValue = piece;
        }
        Inactive(attackInfo, "ComparisonCaption");
        Inactive(card, "CenterDivider");
        string[] prefixes = { "material", "attack", "health", "hitInvincibility" };
        string[] cards = { "MaterialCard", "AttackCard", "HealthCard", "HitInvincibilityCard" };
        string[] buttonNames = { "BuyMaterial", "BuyAttack", "BuyHealthPiece", "BuyHitInvincibility" };
        string[] titles = { "공통 강화 재화", "공격력 강화", "최대 체력 강화", "피격 무적시간 강화" };
        for (int i = 0; i < prefixes.Length; i++)
        {
            float x = i % 2 == 0 ? -83 : 83;
            float y = i < 2 ? 37 : -43;
            RectTransform info = Rect(card, cards[i], x, y, 156, 54);
            Image surface = Component<Image>(info);
            surface.sprite = null;
            surface.color = new Color(0.12f, 0.06f, 0.025f, 0.15f);
            surface.raycastTarget = false;
            Inactive(info, "CategoryPlate");
            Label(info, "CategoryHeading", titles[i], 0, 24, 148, 14, 10);
            Transform button = card.Find(buttonNames[i]);
            if (button == null)
            {
                button = Object.Instantiate(card.Find("BuyAttack").gameObject, card).transform;
                button.name = buttonNames[i];
                Undo.RegisterCreatedObjectUndo(button.gameObject, "Create Upgrade Purchase Button");
            }
            // Older panels kept the purchase label under this name.
            Transform oldLabel = button.Find("PurchaseLabel");
            if (oldLabel != null && button.Find("Label") == null) oldLabel.name = "Label";
            Text purchase = Label(button, "Label", i == 0 ? "5 고철 · 구매" : "재화 3개 · 강화", 0, 0, 126, 16, 8);
            Transform oldStatus = button.Find("Status");
            if (oldStatus != null) oldStatus.SetParent(info, false);
            Text status = Label(info, "Status", string.Empty, 0, -42, 148, 8, 7);
            Button(button, x, y - 28, 138, 18);
            ConfigureWorkshopSelection(button);
            view.FindProperty(prefixes[i] + "PurchaseLabel").objectReferenceValue = purchase;
            view.FindProperty(prefixes[i] + "StatusLabel").objectReferenceValue = status;
            if (i == 0) continue;
            Text value = Label(info, "Label", i == 3 ? "+0초" : "+0", 0, 7, 148, 17, 13);
            Text heading = Label(info, "Heading", string.Empty, 0, -8, 148, 11, 7);
            view.FindProperty(prefixes[i] + "Heading").objectReferenceValue = heading;
            view.FindProperty(i == 1 ? "attackValueLabel" : i == 2 ? "healthBonusLabel" : "hitInvincibilityValueLabel").objectReferenceValue = value;
        }
        Place(card, 0, 0, 384, 252);
        Place(card.Find("TitlePlate"), 0, 90, 112, 24);
        Place(card.Find("Title"), 0, 90, 82, 20);
        Place(scrapInfo, -25, 90, 0, 18);
        scrapInfo.anchorMin = scrapInfo.anchorMax = new Vector2(1, 0.5f);
        scrapInfo.pivot = new Vector2(1, 0.5f);
        Picture(card, "HeaderSeam", "16_horizontal_divider", 0, 70, 330, 1);
        Picture(card, "BottomSeam", "16_horizontal_divider", 0, -94, 330, 1);
        Button close = Button(card.Find("Close"), 119, -109, 84, 16);
        close.transform.Find("Label").GetComponent<Text>().text = "돌아가기";
        ConfigureWorkshopSelection(close.transform);
        view.ApplyModifiedPropertiesWithoutUndo();
        overlay.GetComponent<UpgradeShopView>().Refresh();
        foreach (UpgradeShop shop in Object.FindObjectsByType<UpgradeShop>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var shopData = new SerializedObject(shop);
            if (shopData.FindProperty("panel").objectReferenceValue != overlay.gameObject) continue;
            Undo.RecordObject(shop, "Connect Shared Material Upgrades");
            shopData.FindProperty("materialButton").objectReferenceValue = card.Find("BuyMaterial").GetComponent<UnityEngine.UI.Button>();
            shopData.FindProperty("hitInvincibilityButton").objectReferenceValue = card.Find("BuyHitInvincibility").GetComponent<UnityEngine.UI.Button>();
            shopData.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(shop);
        }
        card.Find("Title").SetAsLastSibling();
        MenuFeedbackSetup.ConfigureWorkshop(overlay);
        ApplyWorkshopFrame(overlay);
        RemoveLegacyWorkshopObjects(card);
        overlay.gameObject.SetActive(false);
    }

    private static void RemoveLegacyWorkshopObjects(Transform card)
    {
        Transform purchase = card.Find("UpgradeLayout/PurchaseArea");
        Button[] buttons = { card.Find("PageToggle").GetComponent<Button>(),
            purchase.Find("BuyAttack").GetComponent<Button>(), purchase.Find("BuyHealthPiece").GetComponent<Button>(),
            purchase.Find("BuyHitInvincibility").GetComponent<Button>(), purchase.Find("BuyMaterial").GetComponent<Button>(),
            purchase.Find("ConfirmUpgrade").GetComponent<Button>(), card.Find("Close").GetComponent<Button>() };
        for (int i = 0; i < buttons.Length; i++)
        {
            Button previous = buttons[(i + buttons.Length - 1) % buttons.Length];
            Button next = buttons[(i + 1) % buttons.Length];
            buttons[i].navigation = new Navigation { mode = Navigation.Mode.Explicit,
                selectOnLeft = previous, selectOnUp = previous, selectOnRight = next, selectOnDown = next };
        }
        string[] names = { "ScrapTray", "BuyAttack", "BuyHealthPiece", "Eyebrow", "Divider",
            "AttackCard", "HealthCard", "RowDivider", "CenterDivider", "ScrapInfo", "MaterialCard",
            "BuyMaterial", "HitInvincibilityCard", "BuyHitInvincibility", "SkinTab" };
        foreach (string name in names)
        {
            Transform old = card.Find(name);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        }
        foreach (string name in new[] { "MaterialCounter", "MaterialHeading" })
        {
            Transform old = purchase.Find(name);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        }
    }

    private static void ApplyWorkshopFrame(Transform overlay)
    {
        Transform card = overlay.Find("Card");
        Place(card, 0, 0, 448, 244);
        Shell(card, 448, 244);
        Inactive(card, "AttackCard", "HealthCard", "HitInvincibilityCard", "MaterialCard",
            "BuyAttack", "BuyHealthPiece", "BuyHitInvincibility", "BuyMaterial", "ScrapInfo");
        Place(card.Find("TitlePlate"), 0, 86, 112, 24);
        Place(card.Find("Title"), 0, 86, 82, 18);
        StyleText(card.Find("Title").GetComponent<Text>(), 13, TextAnchor.MiddleCenter);
        Picture(card, "HeaderSeam", "16_horizontal_divider", 0, 66, 400, 1);
        Picture(card, "BottomSeam", "16_horizontal_divider", 0, -78, 400, 1).gameObject.SetActive(true);
        Label(card, "ControlsHint", "방향키 이동 · Z / Enter 선택 · Esc 닫기", -55, -90, 280, 12, 7, TextAnchor.MiddleLeft);

        // The 410-unit content area is split into 246 (60%) and 164 (40%).
        RectTransform layout = Rect(card, "UpgradeLayout", 0, -6, 410, 136);
        RectTransform left = Rect(layout, "StatsArea", -82, 0, 246, 136);
        RectTransform right = Rect(layout, "PurchaseArea", 123, 0, 164, 136);
        Image divider = Component<Image>(Rect(layout, "Divider", 41, 0, 1, 136));
        divider.color = new Color(0.65f, 0.46f, 0.26f, 0.65f);
        divider.raycastTarget = false;
        RectTransform triangleRect = Rect(left, "Triangle", 0, 14, 150, 96);
        Component<CanvasRenderer>(triangleRect);
        var triangle = Component<UpgradeTriangleFrame>(triangleRect);
        triangle.color = new Color(0.12f, 0.065f, 0.03f, 0.45f);
        triangle.raycastTarget = false;

        var view = new SerializedObject(overlay.GetComponent<UpgradeShopView>());
        view.FindProperty("triangle").objectReferenceValue = triangle;
        var triangleData = new SerializedObject(triangle);
        triangleData.FindProperty("lineWidth").floatValue = 1.6f;
        triangleData.FindProperty("cornerRadius").floatValue = 3f;
        triangleData.ApplyModifiedPropertiesWithoutUndo();
        string[] prefixes = { "attack", "health", "hitInvincibility" };
        string[] captions = { "공격력", "체력", "피격 무적" };
        Vector2[] centers = { new Vector2(-76, 22), new Vector2(76, 22), new Vector2(0, -52) };
        string[] valueFields = { "attackValueLabel", "healthBonusLabel", "hitInvincibilityValueLabel" };
        string[] buttonNames = { "BuyAttack", "BuyHealthPiece", "BuyHitInvincibility" };
        for (int i = 0; i < prefixes.Length; i++)
        {
            RectTransform sector = Rect(left, prefixes[i] + "Sector", centers[i].x, centers[i].y, 68, 32);
            Label(sector, "Caption", captions[i], 0, 7, 68, 13, 9);
            Text value = Label(sector, "Value", "+0", 0, -8, 68, 14, 10);
            Text heading = Label(sector, "Heading", string.Empty, 0, 0, 68, 12, 7);
            heading.gameObject.SetActive(false);
            view.FindProperty(valueFields[i]).objectReferenceValue = value;
            view.FindProperty(prefixes[i] + "Heading").objectReferenceValue = heading;
            Transform buttonTransform = right.Find(buttonNames[i]);
            if (buttonTransform == null)
            {
                buttonTransform = Object.Instantiate(card.Find(buttonNames[i]).gameObject, right).transform;
                buttonTransform.name = buttonNames[i];
                Undo.RegisterCreatedObjectUndo(buttonTransform.gameObject, "Create Workshop Frame Button");
            }
            buttonTransform.gameObject.SetActive(true);
            Button(buttonTransform, 0, 2 - i * 26, 142, 22);
            ConfigureWorkshopSelection(buttonTransform);
            Text label = buttonTransform.Find("Label").GetComponent<Text>();
            view.FindProperty(prefixes[i] + "PurchaseLabel").objectReferenceValue = label;
            Text status = Label(right, prefixes[i] + "Status", string.Empty, 0, 0, 142, 10, 7);
            status.gameObject.SetActive(false);
            view.FindProperty(prefixes[i] + "StatusLabel").objectReferenceValue = status;
        }
        Transform oldScrap = right.Find("Scrap");
        if (oldScrap != null)
        {
            Transform oldHeader = card.Find("WorkshopScrap");
            if (oldHeader != null) Undo.DestroyObjectImmediate(oldHeader.gameObject);
            oldScrap.SetParent(card, false);
            oldScrap.name = "WorkshopScrap";
            oldScrap.gameObject.SetActive(true);
        }
        RectTransform currency = Rect(card, "WorkshopCurrency", 200, 86, 0, 16);
        currency.pivot = new Vector2(1, 0.5f);
        Transform existingScrap = card.Find("WorkshopScrap");
        if (existingScrap != null) existingScrap.SetParent(currency, false);
        Transform existingIcon = card.Find("WorkshopScrapIcon");
        if (existingIcon != null) existingIcon.SetParent(currency, false);
        Text scrap = Label(currency, "WorkshopScrap", $"보유 고철  {PlayerPermanentProgress.Scrap:N0}", 0, 0, 100, 16, 8, TextAnchor.MiddleRight);
        scrap.resizeTextForBestFit = true;
        scrap.resizeTextMinSize = 6;
        scrap.resizeTextMaxSize = 8;
        Sprite scrapIcon = AssetDatabase.LoadAllAssetsAtPath("Assets/Graphics/UI/UI_Scrap.png").OfType<Sprite>().Single();
        Image headerIcon = Component<Image>(Rect(currency, "WorkshopScrapIcon", 0, 0, 10, 10));
        headerIcon.sprite = scrapIcon;
        headerIcon.preserveAspect = true;
        headerIcon.raycastTarget = false;
        headerIcon.transform.SetAsFirstSibling();
        LayoutElement iconSize = Component<LayoutElement>(headerIcon.transform);
        iconSize.preferredWidth = iconSize.minWidth = 10;
        iconSize.preferredHeight = iconSize.minHeight = 10;
        HorizontalLayoutGroup currencyLayout = Component<HorizontalLayoutGroup>(currency);
        currencyLayout.childAlignment = TextAnchor.MiddleRight;
        currencyLayout.spacing = 4;
        currencyLayout.childControlWidth = currencyLayout.childControlHeight = true;
        currencyLayout.childForceExpandWidth = currencyLayout.childForceExpandHeight = false;
        Component<ContentSizeFitter>(currency).horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        var slots = view.FindProperty("materialSlots");
        var frames = view.FindProperty("materialFrames");
        slots.arraySize = frames.arraySize = UpgradeSettings.Shared.MaterialsPerUpgrade;
        Sprite materialIcon = AssetDatabase.LoadAllAssetsAtPath("Assets/Graphics/UI/UI_UpgradeMaterial.png").OfType<Sprite>().Single();
        for (int i = 0; i < slots.arraySize; i++)
        {
            float x = (i - (slots.arraySize - 1) * 0.5f) * 44f;
            RectTransform frameRect = Rect(right, "MaterialSlot" + (i + 1), x, 36, 40, 36);
            Image oldFrame = frameRect.GetComponent<Image>();
            if (oldFrame != null) Undo.DestroyObjectImmediate(oldFrame);
            UpgradeMaterialGlow frame = Component<UpgradeMaterialGlow>(frameRect);
            frame.raycastTarget = false;
            Transform oldIcon = right.Find("Material" + (i + 1));
            if (oldIcon != null && frame.transform.Find("Icon") == null)
            {
                oldIcon.SetParent(frame.transform, false);
                oldIcon.name = "Icon";
            }
            Image slot = Component<Image>(Rect(frame.transform, "Icon", 0, 0, 21, 18));
            slot.sprite = materialIcon;
            slot.preserveAspect = true;
            slot.raycastTarget = false;
            Outline outline = slot.GetComponent<Outline>();
            if (outline != null) outline.enabled = false;
            slots.GetArrayElementAtIndex(i).objectReferenceValue = slot;
            frames.GetArrayElementAtIndex(i).objectReferenceValue = frame;
        }
        view.FindProperty("collectedPieceColor").colorValue = Color.white;
        view.FindProperty("emptyPieceColor").colorValue = new Color(1f, 1f, 1f, 0.25f);
        Transform buyMaterial = right.Find("BuyMaterial");
        if (buyMaterial == null)
        {
            buyMaterial = Object.Instantiate(card.Find("BuyMaterial").gameObject, right).transform;
            buyMaterial.name = "BuyMaterial";
            Undo.RegisterCreatedObjectUndo(buyMaterial.gameObject, "Create Workshop Material Purchase Button");
        }
        buyMaterial.gameObject.SetActive(true);
        Text purchaseLabel = Label(buyMaterial, "Label", "재료 구매", -24, 0, 82, 14, 8);
        purchaseLabel.gameObject.SetActive(true);
        Button purchaseButton = Button(buyMaterial, 0, 2, 142, 22);
        Place(purchaseLabel.transform, -24, 0, 82, 14);
        ConfigureWorkshopSelection(buyMaterial);
        UIButtonPressFeedback press = buyMaterial.GetComponent<UIButtonPressFeedback>();
        if (press != null) press.enabled = true;
        view.FindProperty("materialPurchaseButton").objectReferenceValue = purchaseButton;
        Transform oldPrice = right.Find("MaterialPrice");
        if (oldPrice != null) oldPrice.SetParent(buyMaterial, false);
        RectTransform price = Rect(buyMaterial, "MaterialPrice", 44, 0, 38, 14);
        Inactive(price, "Caption");
        Image priceIcon = Component<Image>(Rect(price, "ScrapIcon", -11, 0, 11, 11));
        priceIcon.sprite = scrapIcon;
        priceIcon.preserveAspect = true;
        priceIcon.raycastTarget = false;
        Text valueLabel = Label(price, "Value", UpgradeSettings.Shared.MaterialScrapCost.ToString(), 9, 0, 18, 13, 9);
        view.FindProperty("materialPurchaseLabel").objectReferenceValue = purchaseLabel;
        view.FindProperty("materialPriceLabel").objectReferenceValue = valueLabel;
        Text materialStatus = Label(right, "MaterialStatus", string.Empty, 0, 0, 142, 10, 7);
        materialStatus.gameObject.SetActive(false);
        view.FindProperty("materialStatusLabel").objectReferenceValue = materialStatus;
        view.ApplyModifiedPropertiesWithoutUndo();
        overlay.GetComponent<UpgradeShopView>().Refresh();

        Button close = Button(card.Find("Close"), 150, -90.5f, 84, 18);
        close.transform.Find("Label").GetComponent<Text>().text = "돌아가기";
        foreach (UpgradeShop shop in Object.FindObjectsByType<UpgradeShop>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var data = new SerializedObject(shop);
            if (data.FindProperty("panel").objectReferenceValue != overlay.gameObject) continue;
            Undo.RecordObject(shop, "Connect Workshop Frame");
            data.FindProperty("scrapLabel").objectReferenceValue = scrap;
            data.FindProperty("attackLabel").objectReferenceValue = view.FindProperty("attackValueLabel").objectReferenceValue;
            data.FindProperty("healthLabel").objectReferenceValue = view.FindProperty("healthBonusLabel").objectReferenceValue;
            data.FindProperty("attackButton").objectReferenceValue = right.Find("BuyAttack").GetComponent<UnityEngine.UI.Button>();
            data.FindProperty("healthButton").objectReferenceValue = right.Find("BuyHealthPiece").GetComponent<UnityEngine.UI.Button>();
            data.FindProperty("hitInvincibilityButton").objectReferenceValue = right.Find("BuyHitInvincibility").GetComponent<UnityEngine.UI.Button>();
            data.FindProperty("materialButton").objectReferenceValue = buyMaterial.GetComponent<UnityEngine.UI.Button>();
            data.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(shop);
        }
        MenuFeedbackSetup.ConfigureWorkshop(overlay);
        ApplyWeaponSkinShop(overlay);
        RefineWorkshop(overlay);
    }

    private static void ApplyWeaponSkinShop(Transform overlay)
    {
        WeaponSkinCatalog catalog = AssetDatabase.LoadAssetAtPath<WeaponSkinCatalog>("Assets/Data/Weapon/WeaponSkinCatalog.asset");
        if (catalog == null) return;
        Transform card = overlay.Find("Card");
        Transform upgradePage = card.Find("UpgradeLayout");
        RectTransform page = Rect(card, "SkinPage", 0, -6, 410, 136);
        RectTransform left = Rect(page, "SkinList", -82, 0, 246, 136);
        Image divider = Component<Image>(Rect(page, "Divider", 41, 0, 1, 136));
        divider.color = new Color(0.65f, 0.46f, 0.26f, 0.65f);
        divider.raycastTarget = false;
        RectTransform right = Rect(page, "PreviewArea", 123, 0, 164, 136);
        Inactive(left, "Scrap");
        RectTransform viewport = Rect(left, "Viewport", 0, -12, 236, 104);
        Component<RectMask2D>(viewport);
        Image listSurface = Component<Image>(viewport);
        listSurface.color = new Color(0.1f, 0.05f, 0.025f, 0.15f);
        RectTransform content = Rect(viewport, "Content", 0, 0, 236, 140);
        content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0.5f, 1); content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0, 0);
        var rowsLayout = Component<VerticalLayoutGroup>(content);
        rowsLayout.spacing = 6; rowsLayout.padding = new RectOffset(3, 3, 3, 3);
        rowsLayout.childControlWidth = rowsLayout.childControlHeight = true;
        rowsLayout.childForceExpandWidth = true; rowsLayout.childForceExpandHeight = false;
        var fit = Component<ContentSizeFitter>(content);
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scroll = Component<ScrollRect>(left);
        scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false;
        scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;

        RectTransform row = Rect(content, "SkinRow", 0, 0, 230, 64);
        var height = Component<LayoutElement>(row); height.preferredHeight = height.minHeight = 64;
        Transform iconButton = Rect(row, "Select", -84, 0, 52, 52);
        Button select = Button(iconButton, -84, 0, 52, 52);
        Image icon = Component<Image>(Rect(iconButton, "Icon", 0, 0, 38, 38));
        icon.sprite = catalog.DefaultSkin.Icon; icon.preserveAspect = true; icon.raycastTarget = false;
        Text name = Label(row, "Name", catalog.DefaultSkin.DisplayName, 27, 18, 148, 18, 11, TextAnchor.MiddleLeft);
        Transform purchaseTransform = Rect(row, "Purchase", 25, -13, 140, 23);
        Label(purchaseTransform, "Label", "장착 중", 0, 0, 128, 18, 9);
        Button purchase = Button(purchaseTransform, 25, -13, 140, 23);
        ConfigureWorkshopSelection(purchaseTransform);
        WeaponSkinShopRow rowView = Component<WeaponSkinShopRow>(row);
        var rowData = new SerializedObject(rowView);
        rowData.FindProperty("selectButton").objectReferenceValue = select;
        rowData.FindProperty("icon").objectReferenceValue = icon;
        rowData.FindProperty("nameLabel").objectReferenceValue = name;
        rowData.FindProperty("purchaseButton").objectReferenceValue = purchase;
        rowData.FindProperty("purchaseLabel").objectReferenceValue = purchaseTransform.Find("Label").GetComponent<Text>();
        rowData.ApplyModifiedPropertiesWithoutUndo();
        row.gameObject.SetActive(false);

        Text selectedName = Label(right, "SelectedName", catalog.DefaultSkin.DisplayName, 0, 52, 148, 18, 11);
        RectTransform previewViewport = Rect(right, "PreviewViewport", 0, -12, 152, 104);
        Component<RectMask2D>(previewViewport);
        Image previewImage = Component<Image>(Rect(previewViewport, "Character", 0, 0, 448, 448));
        previewImage.preserveAspect = true; previewImage.raycastTarget = false;
        previewImage.sprite = null; previewImage.enabled = false;
        WeaponSkinPreview preview = Component<WeaponSkinPreview>(previewViewport);
        var previewData = new SerializedObject(preview);
        previewData.FindProperty("image").objectReferenceValue = previewImage;
        previewData.ApplyModifiedPropertiesWithoutUndo();

        Transform oldTab = card.Find("UpgradeTab");
        if (card.Find("PageToggle") == null && oldTab != null) oldTab.name = "PageToggle";
        Inactive(card, "SkinTab");
        Transform pageButtonTransform = Rect(card, "PageToggle", -164, 86, 72, 20);
        Text pageButtonLabel = Label(pageButtonTransform, "Label", "무기 스킨", 0, 0, 60, 14, 8);
        Button pageButton = Button(pageButtonTransform, -164, 86, 72, 20);
        ConfigureWorkshopSelection(pageButtonTransform);
        WeaponSkinShop skinShop = Component<WeaponSkinShop>(overlay);
        var data = new SerializedObject(skinShop);
        data.FindProperty("catalog").objectReferenceValue = catalog;
        data.FindProperty("upgradePage").objectReferenceValue = upgradePage.gameObject;
        data.FindProperty("skinPage").objectReferenceValue = page.gameObject;
        data.FindProperty("pageButton").objectReferenceValue = pageButton;
        data.FindProperty("pageButtonLabel").objectReferenceValue = pageButtonLabel;
        data.FindProperty("titleLabel").objectReferenceValue = card.Find("Title").GetComponent<Text>();
        data.FindProperty("selectedName").objectReferenceValue = selectedName;
        data.FindProperty("preview").objectReferenceValue = preview;
        data.FindProperty("rowTemplate").objectReferenceValue = rowView;
        data.ApplyModifiedPropertiesWithoutUndo();
        page.gameObject.SetActive(false);
        upgradePage.gameObject.SetActive(true);
        foreach (var button in new[] { select, purchase, pageButton }) Component<UIButtonPressFeedback>(button.transform);
    }

    public static void RefineWorkshop(Transform overlay)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Refine the workshop outside Play Mode.");
        Transform card = overlay.Find("Card");
        Transform stats = card.Find("UpgradeLayout/StatsArea");
        WorkshopInset(stats, 236, 128);
        Place(stats.Find("Triangle"), 0, -4, 144, 92);
        Place(stats.Find("attackSector"), -80, -34, 68, 32);
        Place(stats.Find("healthSector"), 80, -34, 68, 32);
        Place(stats.Find("hitInvincibilitySector"), 0, 53, 120, 16);
        Place(stats.Find("hitInvincibilitySector/Caption"), -20, 0, 72, 14);
        Place(stats.Find("hitInvincibilitySector/Value"), 36, 0, 36, 14);
        var axisData = new SerializedObject(overlay.GetComponent<UpgradeShopView>());
        SerializedProperty axisLabels = axisData.FindProperty("axisLabels");
        axisLabels.arraySize = 3;
        string[] sectors = { "attackSector", "healthSector", "hitInvincibilitySector" };
        for (int i = 0; i < sectors.Length; i++)
        {
            Transform sector = stats.Find(sectors[i]);
            sector.Find("Value").gameObject.SetActive(false);
            Place(sector.Find("Caption"), 0, 0, i == 2 ? 120 : 68, 16);
            axisLabels.GetArrayElementAtIndex(i).objectReferenceValue = sector.Find("Caption").GetComponent<Text>();
        }
        axisData.ApplyModifiedPropertiesWithoutUndo();
        var radarStyle = new SerializedObject(stats.Find("Triangle").GetComponent<UpgradeTriangleFrame>());
        radarStyle.FindProperty("lineWidth").floatValue = 0.8f;
        radarStyle.FindProperty("lineColor").colorValue = new Color(0.85f, 0.67f, 0.4f);
        radarStyle.FindProperty("fillColor").colorValue = new Color(1f, 0.72f, 0.28f, 0.22f);
        radarStyle.FindProperty("plotColor").colorValue = new Color(1f, 0.85f, 0.48f);
        radarStyle.FindProperty("plotLineWidth").floatValue = 0.9f;
        radarStyle.FindProperty("pointRadius").floatValue = 0.8f;
        radarStyle.FindProperty("cornerRadius").floatValue = 1.5f;
        radarStyle.ApplyModifiedPropertiesWithoutUndo();

        Transform purchase = card.Find("UpgradeLayout/PurchaseArea");
        var viewData = new SerializedObject(overlay.GetComponent<UpgradeShopView>());
        SerializedProperty frames = viewData.FindProperty("materialFrames");
        for (int i = 0; i < frames.arraySize; i++)
        {
            var glow = (Graphic)frames.GetArrayElementAtIndex(i).objectReferenceValue;
            RectTransform rect = glow.rectTransform;
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, 30);
            rect.sizeDelta = new Vector2(32, 28);
        }
        Place(purchase.Find("BuyMaterial"), 0, -2, 142, 22);

        Transform skinList = card.Find("SkinPage/SkinList");
        Transform row = skinList.Find("Viewport/Content/SkinRow");
        WorkshopInset(row, 0, 0, stretch: true);
        Place(row.Find("Select"), -84, 0, 46, 46);
        Place(row.Find("Select/Icon"), 0, 0, 34, 34);
        Place(row.Find("Name"), 27, 17, 148, 18);
        Place(row.Find("Purchase"), 25, -13, 140, 21);
        Text purchaseLabel = row.Find("Purchase/Label").GetComponent<Text>();
        purchaseLabel.rectTransform.sizeDelta = new Vector2(128, 17);
        purchaseLabel.fontSize = 8;
        Inactive(skinList, "Scrap");
        Transform preview = card.Find("SkinPage/PreviewArea/PreviewViewport");
        Place(preview.Find("Character"), 0, 0, 432, 432);
        ConfigureUpgradeDetails(overlay);
    }

    private static void ConfigureUpgradeDetails(Transform overlay)
    {
        if (font == null) font = AssetDatabase.LoadAssetAtPath<Font>("Assets/UI/Fonts/NeoDunggeunmo.ttf");
        if (!Sprites.ContainsKey("05_secondary_button"))
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ArtRoot }))
                foreach (Sprite sprite in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)).OfType<Sprite>())
                    Sprites[sprite.name] = sprite;

        Transform right = overlay.Find("Card/UpgradeLayout/PurchaseArea");
        var data = new SerializedObject(overlay.GetComponent<UpgradeShopView>());
        string[] names = { "BuyAttack", "BuyHealthPiece", "BuyHitInvincibility" };
        string[] captions = { "공격력", "체력", "무적" };
        WeaponSkinCatalog catalog = AssetDatabase.LoadAssetAtPath<WeaponSkinCatalog>("Assets/Data/Weapon/WeaponSkinCatalog.asset");
        Sprite[] icons = {
            catalog.DefaultSkin.Icon,
            AssetDatabase.LoadAllAssetsAtPath("Assets/Graphics/UI/UI_Heart.png").OfType<Sprite>().Single(),
            AbilityHUDArtwork.Counter()
        };
        SerializedProperty marks = data.FindProperty("selectionMarks");
        marks.arraySize = names.Length;
        for (int i = 0; i < names.Length; i++)
        {
            Transform target = right.Find(names[i]);
            target.gameObject.SetActive(true);
            Button button = Button(target, (i - 1) * 52, 54, 44, 26);
            button.interactable = true;
            ConfigureWorkshopSelection(target);
            // Use an inset tint for selection; keep the focus marker for keyboard navigation.
            var indicator = new SerializedObject(target.GetComponent<UISelectionIndicator>());
            indicator.FindProperty("selectedButtonSprite").objectReferenceValue = null;
            indicator.ApplyModifiedPropertiesWithoutUndo();
            target.GetComponent<Outline>().effectColor = Color.clear;
            Image icon = Component<Image>(Rect(target, "Icon", 0, 5, 12, 12));
            icon.sprite = icons[i]; icon.preserveAspect = true; icon.raycastTarget = false;
            Label(target, "Label", captions[i], 0, -6, 40, 8, 6);
            Image mark = Component<Image>(Rect(target, "Selected", 0, 0, 36, 18));
            mark.raycastTarget = false;
            mark.transform.SetAsFirstSibling();
            marks.GetArrayElementAtIndex(i).objectReferenceValue = mark;
        }
        Text heading = Label(right, "SelectedHeading", "공격력", 0, 32, 148, 12, 9);
        Text comparison = Label(right, "SelectedComparison", "+0 → +1", 0, 16, 148, 16, 12);
        comparison.supportRichText = true;
        Text limit = Label(right, "SelectedLimit", "최대 +10", 0, 3, 148, 10, 7);
        limit.color = new Color(0.82f, 0.73f, 0.58f);
        data.FindProperty("selectedHeading").objectReferenceValue = heading;
        data.FindProperty("selectedComparison").objectReferenceValue = comparison;
        data.FindProperty("selectedLimit").objectReferenceValue = limit;
        SerializedProperty frames = data.FindProperty("materialFrames");
        for (int i = 0; i < frames.arraySize; i++)
        {
            var glow = (Graphic)frames.GetArrayElementAtIndex(i).objectReferenceValue;
            Place(glow.transform, (i - (frames.arraySize - 1) * 0.5f) * 28, -15, 26, 22);
            Place(glow.transform.Find("Icon"), 0, 0, 18, 15);
        }
        Transform purchase = right.Find("BuyMaterial");
        Button(purchase, 0, -33.75f, 102, 15);
        Text purchaseLabel = purchase.Find("Label").GetComponent<Text>();
        Place(purchaseLabel.transform, -17, 0, 59, 11);
        purchaseLabel.fontSize = 7;
        Transform price = purchase.Find("MaterialPrice");
        Place(price, 32, 0, 30, 12);
        Place(price.Find("ScrapIcon"), -7, 0, 9, 9);
        Place(price.Find("Value"), 7, 0, 14, 11);
        price.Find("Value").GetComponent<Text>().fontSize = 7;
        Transform confirm = Rect(right, "ConfirmUpgrade", 0, -56, 142, 20);
        Text confirmLabel = Label(confirm, "Label", "공격력 강화", 0, 0, 130, 16, 9);
        Button confirmButton = Button(confirm, 0, -56, 142, 20);
        ConfigureWorkshopSelection(confirm);
        Component<UIButtonPressFeedback>(confirm);
        data.FindProperty("confirmButton").objectReferenceValue = confirmButton;
        data.FindProperty("confirmLabel").objectReferenceValue = confirmLabel;
        data.ApplyModifiedPropertiesWithoutUndo();
        overlay.GetComponent<UpgradeShopView>().Refresh();
        foreach (UpgradeShop shop in Object.FindObjectsByType<UpgradeShop>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var shopData = new SerializedObject(shop);
            if (shopData.FindProperty("panel").objectReferenceValue != overlay.gameObject) continue;
            Undo.RecordObject(shop, "Connect Selected Upgrade Action");
            shopData.FindProperty("confirmButton").objectReferenceValue = confirmButton;
            shopData.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(shop);
        }
    }

    private static void WorkshopInset(Transform parent, float width, float height, bool stretch = false)
    {
        RectTransform frameRect = Rect(parent, "InsetFrame", 0, 0, width, height);
        if (stretch) Stretch(frameRect, 1, 1);
        WorkshopInsetFrame frame = Component<WorkshopInsetFrame>(frameRect);
        frame.color = new Color(0.66f, 0.39f, 0.16f, 0.8f);
        frame.raycastTarget = false;
        frameRect.SetAsFirstSibling();
    }

    [MenuItem("Tools/Project K/Apply Ability HUD")]
    public static void ApplyAbilityHUDLayout()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Edit the ability HUD outside Play Mode.");
        font = AssetDatabase.LoadAssetAtPath<Font>("Assets/UI/Fonts/NeoDunggeunmo.ttf");
        Sprite bodySprite = AbilityHUDArtwork.DialBody();
        Sprite ringSprite = AbilityHUDArtwork.DialRing();
        Sprite[] icons = { AbilityHUDArtwork.GroundSlam(), AbilityHUDArtwork.Counter() };
        EditPrefab("Canvas", root =>
        {
            Transform hud = root.transform.Find("HUD");
            RectTransform healthBar = (RectTransform)hud.Find("HPBar");
            healthBar.anchoredPosition = new Vector2(74, healthBar.anchoredPosition.y);
            Transform existing = hud.Find("Abilities");
            if (existing != null) existing.SetParent(healthBar, false);
            RectTransform abilities = Rect(healthBar, "Abilities", -8, 0, 54, 34);
            abilities.anchorMin = abilities.anchorMax = new Vector2(0, 0.5f);
            abilities.pivot = new Vector2(1, 0.5f);
            Component<LayoutElement>(abilities).ignoreLayout = true;
            Image mountEdge = Component<Image>(Rect(abilities, "MountEdge", 0, 0, 52, 12));
            mountEdge.color = new Color(0.34f, 0.27f, 0.18f);
            mountEdge.raycastTarget = false;
            mountEdge.transform.SetAsFirstSibling();
            Image mount = Component<Image>(Rect(abilities, "Mount", 0, 0, 50, 10));
            mount.color = new Color(0.1f, 0.09f, 0.07f);
            mount.raycastTarget = false;
            mount.transform.SetSiblingIndex(1);
            var data = new SerializedObject(Component<PlayerAbilityHUD>(abilities));
            string[] names = { "GroundSlam", "Counter" };
            string[] fields = { "groundSlamView", "counterView" };
            string[] keys = { "A", "S" };
            float[] positions = { -13, 13 };
            for (int i = 0; i < names.Length; i++)
            {
                RectTransform slot = Rect(abilities, names[i], positions[i], 0, 22, 22);
                DialImage(slot, "Background", bodySprite, 22).transform.SetAsFirstSibling();
                Image frame = DialImage(slot, "Frame", ringSprite, 22);
                frame.color = new Color(0.78f, 0.58f, 0.32f);
                frame.transform.SetSiblingIndex(1);
                Image fill = DialImage(slot, "CooldownFill", ringSprite, 22);
                fill.color = frame.color;
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Radial360;
                fill.fillOrigin = (int)Image.Origin360.Top;
                fill.fillClockwise = true;
                fill.fillAmount = 0;
                fill.enabled = false;
                fill.transform.SetSiblingIndex(2);
                Image icon = DialImage(slot, "Icon", icons[i], i == 0 ? 17 : 15);
                icon.rectTransform.anchoredPosition = new Vector2(0f, i == 0 ? 0.5f : 0f);
                icon.material = null;
                icon.preserveAspect = true;
                icon.transform.SetSiblingIndex(3);
                Text remaining = Label(slot, "Remaining", string.Empty, 0, 0, 16, 12, 8);
                remaining.transform.SetSiblingIndex(4);
                Shadow numberShadow = Component<Shadow>(remaining.transform);
                numberShadow.effectColor = new Color(0.06f, 0.05f, 0.04f);
                numberShadow.effectDistance = new Vector2(0.5f, -0.5f);
                numberShadow.enabled = true;
                Image keyPlate = Component<Image>(Rect(slot, "KeyPlate", 0, 11, 7, 5));
                keyPlate.color = new Color(0.1f, 0.09f, 0.07f);
                keyPlate.raycastTarget = false;
                keyPlate.transform.SetSiblingIndex(5);
                Outline keyOutline = Component<Outline>(keyPlate.transform);
                keyOutline.effectColor = new Color(0.45f, 0.33f, 0.19f);
                keyOutline.effectDistance = new Vector2(0.5f, -0.5f);
                keyOutline.enabled = true;
                Text key = Label(slot, "KeyLabel", keys[i], 0, 11, 7, 6, 4);
                key.transform.SetAsLastSibling();
                Shadow keyShadow = Component<Shadow>(key.transform);
                keyShadow.effectColor = new Color(0, 0, 0, 0.85f);
                keyShadow.effectDistance = new Vector2(0.25f, -0.25f);
                keyShadow.enabled = true;
                SerializedProperty view = data.FindProperty(fields[i]);
                view.FindPropertyRelative("frame").objectReferenceValue = frame;
                view.FindPropertyRelative("icon").objectReferenceValue = icon;
                view.FindPropertyRelative("cooldownFill").objectReferenceValue = fill;
                view.FindPropertyRelative("remainingLabel").objectReferenceValue = remaining;
                view.FindPropertyRelative("keyLabel").objectReferenceValue = key;
                view.FindPropertyRelative("keyPlate").objectReferenceValue = keyPlate.rectTransform;
            }
            data.FindProperty("readyColor").colorValue = new Color(1f, 0.91f, 0.65f);
            data.ApplyModifiedPropertiesWithoutUndo();
        });
    }

    private static Image DialImage(Transform parent, string name, Sprite sprite, float size)
    {
        Image image = Component<Image>(Rect(parent, name, 0, 0, size, size));
        image.sprite = sprite;
        image.overrideSprite = null;
        image.color = Color.white;
        image.type = Image.Type.Simple;
        image.enabled = true;
        image.raycastTarget = false;
        foreach (var effect in image.GetComponents<BaseMeshEffect>()) effect.enabled = false;
        return image;
    }

    private static void ConfigureWorkshopSelection(Transform button)
    {
        Outline outline = Component<Outline>(button);
        outline.effectColor = new Color(1f, 0.82f, 0.28f, 1f);
        outline.effectDistance = new Vector2(1.2f, -1.2f);
        outline.useGraphicAlpha = true;
        outline.enabled = false;
        var indicator = new SerializedObject(Component<UISelectionIndicator>(button));
        indicator.FindProperty("selectedButtonSprite").objectReferenceValue = Sprites["06_primary_button"];
        indicator.ApplyModifiedPropertiesWithoutUndo();
        RectTransform marker = Rect(button, "FocusMarker", 0, 0, 4, 6);
        marker.anchorMin = marker.anchorMax = new Vector2(0, 0.5f);
        marker.anchoredPosition = new Vector2(-3, 0);
        marker.localRotation = Quaternion.Euler(0, 0, 90);
        Image image = Component<Image>(marker);
        image.sprite = Sprites["12_dropdown_arrow"];
        image.color = Color.white;
        image.raycastTarget = false;
        image.enabled = false;
    }
}
