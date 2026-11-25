using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

public sealed class MenuSafetyTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [Test]
    public void SharedMenusProvideFixedKeyHintsAndIsolatedConfirmations()
    {
        foreach (string name in new[] { "GameSettingsPanel", "ControlsGuide" })
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/" + name + ".prefab");
            var dialog = root.GetComponentInChildren<MenuConfirmationDialog>(true);
            Assert.That(dialog, Is.Not.Null);
            Assert.That(dialog.gameObject.activeSelf, Is.False);
            Assert.That(dialog.GetComponent<Image>().raycastTarget, Is.True);
            Assert.That(dialog.GetComponent<CanvasGroup>().ignoreParentGroups, Is.True);
            var data = new SerializedObject(dialog);
            Assert.That(data.FindProperty("backgroundControls").objectReferenceValue, Is.EqualTo(root.GetComponent<CanvasGroup>()));
            var confirm = (Button)data.FindProperty("confirmButton").objectReferenceValue;
            var cancel = (Button)data.FindProperty("cancelButton").objectReferenceValue;
            Assert.That(confirm.navigation.selectOnRight, Is.EqualTo(cancel));
            Assert.That(cancel.navigation.selectOnLeft, Is.EqualTo(confirm));
            Assert.That(confirm.GetComponent<UIButtonPressFeedback>(), Is.Not.Null);
            Assert.That(cancel.GetComponent<UIButtonPressFeedback>(), Is.Not.Null);
            float innerBottom = -((RectTransform)dialog.transform.Find("Card")).rect.height * .5f + 10;
            Assert.That(((RectTransform)confirm.transform).rect.yMin + confirm.transform.localPosition.y - 1.5f - 1.2f,
                Is.GreaterThan(innerBottom), "The pressed buttons stay inside the modal frame.");
        }
        var settings = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/GameSettingsPanel.prefab");
        var hint = settings.transform.Find("NavigationHint").GetComponent<Text>();
        Assert.That(hint.text, Is.EqualTo("Tab 분류 이동"));
        Assert.That(hint.preferredWidth, Is.LessThanOrEqualTo(hint.rectTransform.rect.width));
        var controls = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/ControlsGuide.prefab");
        var fixedKey = controls.transform.Find("Key9/Cap0").GetComponent<Image>();
        Assert.That(fixedKey.GetComponent<Button>(), Is.Null);
        Assert.That(fixedKey.color, Is.Not.EqualTo(controls.transform.Find("Key0/Cap0").GetComponent<Image>().color));
        Assert.That(controls.transform.Find("Key9/FixedHint").gameObject.activeSelf, Is.False);
        var resetCard = (RectTransform)controls.transform.Find("Confirmation/Card");
        Assert.That(resetCard.sizeDelta, Is.EqualTo(new Vector2(200, 90)));
        foreach (string name in new[] { "TitlePlate", "Heading", "Detail" })
            Assert.That(resetCard.Find(name).gameObject.activeSelf, Is.False);
        Assert.That(((RectTransform)settings.transform.Find("Confirmation/Card")).sizeDelta,
            Is.EqualTo(new Vector2(264, 140)), "The timed display confirmation keeps its existing layout.");
    }

    public static async Task VerifyRuntime()
    {
        var pause = UnityEngine.Object.FindFirstObjectByType<PauseMenu>();
        var originalResolution = new Vector2Int(Screen.width, Screen.height);
        FullScreenMode originalMode = Screen.fullScreenMode;
        int width = GameSettings.ResolutionWidth, height = GameSettings.ResolutionHeight;
        bool fullscreen = GameSettings.Fullscreen, vSync = GameSettings.VSync;
        string[] keys = { "ProjectK.Fullscreen", "ProjectK.ResolutionWidth", "ProjectK.ResolutionHeight", "ProjectK.VSync" };
        int?[] saved = keys.Select(key => PlayerPrefs.HasKey(key) ? (int?)PlayerPrefs.GetInt(key) : null).ToArray();
        GameSettingsPanel panel = null;
        try
        {
            pause.Open();
            await Task.Delay(350);
            pause.ShowSettings();
            await Task.Delay(350);
            panel = UnityEngine.Object.FindFirstObjectByType<GameSettingsPanel>();
            var data = new SerializedObject(panel);
            var mode = (Dropdown)data.FindProperty("displayMode").objectReferenceValue;
            var resolution = (Dropdown)data.FindProperty("resolution").objectReferenceValue;
            var dialog = (MenuConfirmationDialog)data.FindProperty("displayConfirmation").objectReferenceValue;
            var dialogData = new SerializedObject(dialog);
            var confirm = (Button)dialogData.FindProperty("confirmButton").objectReferenceValue;
            var cancel = (Button)dialogData.FindProperty("cancelButton").objectReferenceValue;
            void PreviewMode() => mode.value = GameSettings.Fullscreen ? 1 : 0;
            void AssertStoredDisplayUnchanged()
            {
                Assert.That(GameSettings.Fullscreen, Is.EqualTo(fullscreen));
                Assert.That(GameSettings.ResolutionWidth, Is.EqualTo(width));
                Assert.That(GameSettings.ResolutionHeight, Is.EqualTo(height));
                for (int i = 0; i < 3; i++)
                {
                    Assert.That(PlayerPrefs.HasKey(keys[i]), Is.EqualTo(saved[i].HasValue));
                    if (saved[i].HasValue) Assert.That(PlayerPrefs.GetInt(keys[i]), Is.EqualTo(saved[i].Value));
                }
            }

            Assert.That(Time.timeScale, Is.Zero, "Timeout must also work while gameplay is paused.");
            mode.Select();
            await Press(Key.Enter);
            Assert.That(EventSystem.current.currentSelectedGameObject.GetComponent<Toggle>(), Is.Not.Null);
            await Press(mode.value == 0 ? Key.DownArrow : Key.UpArrow);
            await Press(Key.Enter);
            await Task.Delay(80);
            Assert.That(dialog.IsOpen, Is.True);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(cancel.gameObject));
            Assert.That(confirm.IsInteractable(), Is.True);
            Assert.That(panel.FirstSelection.IsInteractable(), Is.False);
            typeof(GameSettingsPanel).GetMethod("SwitchFocusPane", PrivateInstance).Invoke(panel, null);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(cancel.gameObject), "Tab stays inside the dialog.");
            GameSettings.Save();
            AssertStoredDisplayUnchanged();
            await Press(Key.Enter);
            Assert.That(dialog.IsOpen, Is.False);
            AssertStoredDisplayUnchanged();

            PreviewMode();
            await Press(Key.Escape);
            Assert.That(dialog.IsOpen, Is.False);
            Assert.That(panel.isActiveAndEnabled && pause.IsOpen, Is.True, "Escape rejects the preview without leaving settings.");
            AssertStoredDisplayUnchanged();

            if (resolution.options.Count > 1)
            {
                resolution.value = (resolution.value + 1) % resolution.options.Count;
                Assert.That(dialog.IsOpen, Is.True);
                AssertStoredDisplayUnchanged();
                cancel.onClick.Invoke();
                AssertStoredDisplayUnchanged();
            }

            PreviewMode();
            var deadline = typeof(GameSettingsPanel).GetField("displayConfirmationDeadline", PrivateInstance);
            Assert.That((float)deadline.GetValue(panel) - Time.unscaledTime, Is.InRange(14f, 15.01f));
            deadline.SetValue(panel, Time.unscaledTime - 1f);
            await Task.Delay(80);
            Assert.That(dialog.IsOpen, Is.False, "An expired preview returns automatically while timeScale is zero.");
            AssertStoredDisplayUnchanged();

            PreviewMode();
            confirm.onClick.Invoke();
            Assert.That(GameSettings.Fullscreen, Is.EqualTo(!fullscreen));
            Assert.That(PlayerPrefs.GetInt(keys[0]), Is.EqualTo(fullscreen ? 0 : 1));

            GameSettings.SetFullscreen(false);
            GameSettings.SetVSync(false);
            var reset = (Button)data.FindProperty("resetButtons").GetArrayElementAtIndex(1).objectReferenceValue;
            reset.onClick.Invoke();
            Assert.That(dialog.IsOpen, Is.True);
            Assert.That(GameSettings.VSync, Is.False, "Display defaults are deferred until confirmation.");
            cancel.onClick.Invoke();
            Assert.That(GameSettings.Fullscreen || GameSettings.VSync, Is.False);
            reset.onClick.Invoke();
            confirm.onClick.Invoke();
            Assert.That(GameSettings.Fullscreen && GameSettings.VSync, Is.True);

            PreviewMode();
            pause.ShowControls();
            await Task.Delay(250);
            Assert.That(dialog.IsOpen, Is.False, "Disabling settings cancels an unconfirmed preview.");
            Assert.That(GameSettings.Fullscreen, Is.True);
        }
        finally
        {
            if (panel != null) panel.TryCloseConfirmation();
            GameSettings.SetFullscreen(fullscreen);
            GameSettings.SetResolution(width, height);
            GameSettings.SetVSync(vSync);
            for (int i = 0; i < keys.Length; i++)
                if (saved[i].HasValue) PlayerPrefs.SetInt(keys[i], saved[i].Value);
                else PlayerPrefs.DeleteKey(keys[i]);
            GameSettings.Save();
            Screen.SetResolution(originalResolution.x, originalResolution.y, originalMode);
            pause.Close();
            await Task.Delay(350);
        }
    }

    private static async Task Press(Key key)
    {
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(key));
        await Awaitable.NextFrameAsync(); await Awaitable.NextFrameAsync();
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
        await Awaitable.NextFrameAsync(); await Awaitable.NextFrameAsync();
    }
}
