using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class KeyRebindingTests
{
    private const string Prefix = "ProjectK.Key.";
    private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;

    private sealed class Preferences : IDisposable
    {
        private readonly Dictionary<string, int?> saved = new Dictionary<string, int?>();
        public Preferences()
        {
            var input = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Settings/Input/PlayerInputSet.inputactions");
            foreach (InputAction action in input)
                foreach (InputBinding binding in action.bindings)
                {
                    string name = Prefix + binding.id;
                    saved[name] = PlayerPrefs.HasKey(name) ? PlayerPrefs.GetInt(name) : null;
                    PlayerPrefs.DeleteKey(name);
                }
            Reload();
        }
        public void Dispose()
        {
            foreach (var item in saved)
                if (item.Value.HasValue) PlayerPrefs.SetInt(item.Key, item.Value.Value);
                else PlayerPrefs.DeleteKey(item.Key);
            PlayerPrefs.Save();
            Reload();
        }
    }

    private static void Reload()
    {
        var field = typeof(PlayerKeyBindings).GetField("controls", PrivateStatic);
        if (field.GetValue(null) is PlayerInputSet input) UnityEngine.Object.DestroyImmediate(input.asset);
        field.SetValue(null, null);
    }

    [Test]
    public void DuplicateKeyboardAndMouseBindingsPersistAndPreserveDisabledInput()
    {
        using var preferences = new Preferences();
        var input = new PlayerInputSet();
        try
        {
            string jump = PlayerKeyBindings.ActionBindingId("Jump");
            string down = PlayerKeyBindings.ActionBindingId("Down");
            string attack = PlayerKeyBindings.ActionBindingId("Attack");
            Assert.That(PlayerKeyBindings.TryRebind(jump, Key.Z, out string conflict), Is.True);
            Assert.That(conflict, Is.EqualTo(attack));
            Assert.That(PlayerKeyBindings.HasDuplicateBinding(jump), Is.True);
            Assert.That(PlayerKeyBindings.HasDuplicateBinding(attack), Is.True);
            Assert.That(PlayerKeyBindings.TryRebind(jump, Key.Escape, out _), Is.False);
            Assert.That(PlayerKeyBindings.TryRebind(jump, Key.F1, out _), Is.True);
            Assert.That(PlayerKeyBindings.TryRebind(jump, (Key)999, out _), Is.False);
            Assert.That(PlayerKeyBindings.TryRebind(jump, Key.V, out _), Is.True);
            Assert.That(PlayerKeyBindings.HasDuplicateBinding(attack), Is.False);
            Assert.That(PlayerPrefs.GetInt(Prefix + jump), Is.EqualTo((int)Key.V));
            Assert.That(PlayerKeyBindings.TryRebind(down, Key.Digit1, out _), Is.True);
            string[] mouseActions = { "Attack", "Dash", "GroundSlam", "Counter", "Interact" };
            MouseButton[] mouseButtons = { MouseButton.Left, MouseButton.Right, MouseButton.Middle, MouseButton.Back, MouseButton.Forward };
            string[] mousePaths = { "leftButton", "rightButton", "middleButton", "backButton", "forwardButton" };
            for (int i = 0; i < mouseActions.Length; i++)
                Assert.That(PlayerKeyBindings.TryRebind(PlayerKeyBindings.ActionBindingId(mouseActions[i]), mouseButtons[i], out _), Is.True);
            Assert.That(PlayerKeyBindings.TryRebind(jump, MouseButton.Left, out conflict), Is.True);
            Assert.That(conflict, Is.EqualTo(attack));
            Assert.That(PlayerKeyBindings.TryRebind(jump, (MouseButton)999, out _), Is.False);
            Reload();
            PlayerKeyBindings.ApplyTo(input);
            for (int i = 0; i < mouseActions.Length; i++)
            {
                Assert.That(input.asset.FindAction(mouseActions[i]).bindings[0].effectivePath, Is.EqualTo("<Mouse>/" + mousePaths[i]));
                Assert.That(PlayerKeyBindings.DisplayName(PlayerKeyBindings.ActionBindingId(mouseActions[i])), Is.EqualTo("M" + (i + 1)));
            }
            Assert.That(input.Player.enabled, Is.False);
            Assert.That(input.Player.Jump.bindings[0].effectivePath, Is.EqualTo("<Mouse>/leftButton"));
            Assert.That(PlayerKeyBindings.HasDuplicateBinding(jump), Is.True);
            Assert.That(PlayerKeyBindings.HasDuplicateBinding(attack), Is.True);
            Assert.That(input.Player.Down.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/1"));
            Assert.That(input.Player.Movement.bindings.Single(binding => binding.name == "down").effectivePath,
                Is.EqualTo(input.Player.Down.bindings[0].effectivePath));
            input.Player.Enable();
            PlayerKeyBindings.ApplyTo(input);
            Assert.That(input.Player.enabled, Is.True);
            input.Disable();
            PlayerKeyBindings.ResetToDefaults();
            PlayerKeyBindings.ApplyTo(input);
            Assert.That(PlayerPrefs.HasKey(Prefix + jump), Is.False);
            Assert.That(input.Player.Jump.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/c"));
            Assert.That(input.Player.Dash.bindings.Count(binding => binding.path.StartsWith("<Keyboard>") || binding.path.StartsWith("<Mouse>")), Is.EqualTo(1));
            Assert.That(input.Player.Movement.bindings.Single(binding => binding.name == "down").overridePath, Is.Null);
            Assert.That(PlayerKeyBindings.TryRebind(down, MouseButton.Middle, out _), Is.True);
            PlayerKeyBindings.ApplyTo(input);
            Assert.That(input.Player.Movement.bindings.Single(binding => binding.name == "down").effectivePath,
                Is.EqualTo("<Mouse>/middleButton"), "The platform-drop mouse binding also updates the movement composite.");
        }
        finally { input.Disable(); UnityEngine.Object.DestroyImmediate(input.asset); }
    }

    [Test]
    public void GamepadBindingsRemainAvailableAfterKeyboardRebinding()
    {
        using var preferences = new Preferences();
        var input = new PlayerInputSet();
        try
        {
            Assert.That(PlayerKeyBindings.TryRebind(PlayerKeyBindings.ActionBindingId("Jump"), Key.V, out _), Is.True);
            PlayerKeyBindings.ApplyTo(input);
            input.Player.Enable();
            var buttons = new[] {
                (input.Player.Jump, "buttonSouth"), (input.Player.Attack, "buttonWest"),
                (input.Player.Dash, "buttonEast"), (input.Player.Interact, "buttonNorth"),
                (input.Player.Counter, "leftShoulder"), (input.Player.GroundSlam, "rightShoulder"),
                (input.Player.PortalUse, "dpad/up"), (input.Player.Down, "dpad/down"),
                (input.Player.PortalUse, "leftStick/up"), (input.Player.Down, "leftStick/down"),
                (input.Player.Movement, "leftStick"), (input.Player.Movement, "dpad")
            };
            foreach (var (action, button) in buttons)
                Assert.That(action.bindings.Any(binding => binding.effectivePath == "<Gamepad>/" + button), Is.True, action.name);
            string padId = input.Player.Jump.bindings.Single(binding => binding.path == "<Gamepad>/buttonSouth").id.ToString();
            Assert.That(PlayerKeyBindings.TryRebind(padId, Key.K, out _), Is.False);
        }
        finally
        {
            input.Disable();
            UnityEngine.Object.DestroyImmediate(input.asset);
        }
    }

    [Test]
    public void GamepadRebindingAllowsDuplicatesAndResetsOnlyTheSelectedDevice()
    {
        using var preferences = new Preferences();
        var input = new PlayerInputSet();
        try
        {
            string jump = PlayerKeyBindings.GamepadBindingId("Jump");
            string keyboardJump = PlayerKeyBindings.ActionBindingId("Jump");
            string attack = PlayerKeyBindings.GamepadBindingId("Attack");
            Assert.That(PlayerKeyBindings.TryRebind(jump, GamepadButton.West, out string conflict), Is.True);
            Assert.That(conflict, Is.EqualTo(attack));
            Reload();
            Assert.That(PlayerKeyBindings.HasDuplicateBinding(jump), Is.True);
            Assert.That(PlayerKeyBindings.HasDuplicateBinding(attack), Is.True);
            foreach (GamepadButton reserved in new[] { GamepadButton.Start, GamepadButton.DpadLeft, GamepadButton.DpadRight, (GamepadButton)999 })
                Assert.That(PlayerKeyBindings.TryRebind(jump, reserved, out _), Is.False);
            Assert.That(PlayerKeyBindings.TryRebind(keyboardJump, GamepadButton.LeftTrigger, out _), Is.False);
            Assert.That(PlayerKeyBindings.TryRebind(keyboardJump, Key.V, out _), Is.True);
            Assert.That(PlayerKeyBindings.TryRebind(jump, GamepadButton.LeftTrigger, out _), Is.True);
            Assert.That(PlayerKeyBindings.HasDuplicateBinding(attack), Is.False);
            Assert.That(PlayerKeyBindings.TryRebind(PlayerKeyBindings.GamepadBindingId("Down"), GamepadButton.RightStick, out _), Is.True);
            Reload();
            Assert.That(PlayerKeyBindings.DisplayName(jump), Is.EqualTo("LT"));
            PlayerKeyBindings.ApplyTo(input);
            Assert.That(input.Player.enabled, Is.False);
            Assert.That(input.Player.Jump.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/v").IgnoreCase);
            Assert.That(input.Player.Jump.bindings.Any(binding => binding.effectivePath == "<Gamepad>/leftTrigger"), Is.True);
            Assert.That(input.Player.Down.bindings.Where(binding => binding.path.StartsWith("<Gamepad>/"))
                .All(binding => binding.effectivePath == "<Gamepad>/rightStickPress"), Is.True);
            PlayerKeyBindings.ResetToDefaults();
            Assert.That(PlayerKeyBindings.DisplayName(jump), Is.EqualTo("LT"));
            Assert.That(PlayerKeyBindings.DisplayName(keyboardJump), Is.EqualTo("C").IgnoreCase);
            Assert.That(PlayerKeyBindings.TryRebind(keyboardJump, Key.V, out _), Is.True);
            PlayerKeyBindings.ResetToDefaults(true);
            Assert.That(PlayerKeyBindings.DisplayName(jump), Is.EqualTo("A"));
            Assert.That(PlayerKeyBindings.DisplayName(keyboardJump), Is.EqualTo("V").IgnoreCase);
            Assert.That(PlayerPrefs.HasKey(Prefix + jump), Is.False);
            PlayerKeyBindings.ApplyTo(input);
            Assert.That(input.Player.Down.bindings.Any(binding => binding.effectivePath == "<Gamepad>/leftStick/down"), Is.True);
        }
        finally { input.Disable(); UnityEngine.Object.DestroyImmediate(input.asset); }
    }

    [TestCase("Gamepad", false, "A", "B", "RB")]
    [TestCase("DualSenseGamepadHID", false, "×", "○", "R1")]
    [TestCase("SwitchProControllerHID", true, "A", "B", "R")]
    public void GamepadMenuLabelsAndCancelMatchTheDeviceLayout(string layout, bool reversed,
        string submitLabel, string cancelLabel, string shoulderLabel)
    {
        Gamepad pad = (Gamepad)InputSystem.AddDevice(layout);
        try
        {
            using var submit = new InputAction(binding: "*/{Submit}");
            using var cancel = new InputAction(binding: PlayerKeyBindings.GamepadCancelPath);
            ButtonControl expectedSubmit = reversed ? pad.buttonEast : pad.buttonSouth;
            ButtonControl expectedCancel = reversed ? pad.buttonSouth : pad.buttonEast;
            Assert.That(submit.controls.Where(control => control.device == pad), Is.EqualTo(new[] { expectedSubmit }));
            Assert.That(cancel.controls.Where(control => control.device == pad), Is.EqualTo(new[] { expectedCancel }));
            Assert.That(PlayerKeyBindings.ControlForPath(PlayerKeyBindings.GamepadCancelPath), Is.SameAs(expectedCancel));
            Assert.That(PlayerKeyBindings.GamepadDisplayName(PlayerKeyBindings.GamepadSubmitPath), Is.EqualTo(submitLabel));
            Assert.That(PlayerKeyBindings.GamepadDisplayName(PlayerKeyBindings.GamepadCancelPath), Is.EqualTo(cancelLabel));
            Assert.That(PlayerKeyBindings.GamepadDisplayName("<Gamepad>/rightShoulder"), Is.EqualTo(shoulderLabel));
        }
        finally { InputSystem.RemoveDevice(pad); }
    }

    [Test]
    public void DeviceGraphicsProduceFiniteVisibleMeshes()
    {
        var root = new GameObject("DeviceGraphicTest", typeof(RectTransform), typeof(ControlButtonGraphic));
        try
        {
            var graphic = root.GetComponent<ControlButtonGraphic>();
            var populate = typeof(ControlButtonGraphic).GetMethod("OnPopulateMesh",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            foreach (ControlButtonGraphic.ButtonShape shape in Enum.GetValues(typeof(ControlButtonGraphic.ButtonShape)))
            {
                graphic.Shape = shape;
                graphic.rectTransform.sizeDelta = shape == ControlButtonGraphic.ButtonShape.Side
                    ? new Vector2(2.2f, 6f) : new Vector2(14.8f, 23.5f);
                using (var helper = new VertexHelper())
                {
                    populate.Invoke(graphic, new object[] { helper });
                    var vertices = new List<UIVertex>();
                    helper.GetUIVertexStream(vertices);
                    Assert.That(vertices, Is.Not.Empty, shape.ToString());
                    foreach (UIVertex vertex in vertices)
                    {
                        Assert.That(float.IsNaN(vertex.position.x) || float.IsNaN(vertex.position.y)
                            || float.IsInfinity(vertex.position.x) || float.IsInfinity(vertex.position.y), Is.False);
                        Rect bounds = graphic.rectTransform.rect;
                        Assert.That(vertex.position.x, Is.InRange(bounds.xMin - 0.001f, bounds.xMax + 0.001f));
                        Assert.That(vertex.position.y, Is.InRange(bounds.yMin - 0.001f, bounds.yMax + 0.001f));
                    }
                }
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [Test]
    public void MouseArtworkHasFiveClippedFeedbackRegions()
    {
        var guide = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/ControlsGuide.prefab");
        var diagram = guide.GetComponentInChildren<ControlDeviceDisplay>(true);
        var body = diagram.transform.Find("MouseBody").GetComponent<Image>();
        Assert.That(body.sprite, Is.Not.Null);
        var visuals = new SerializedObject(diagram).FindProperty("visuals");
        int count = 0;
        for (int i = 0; i < visuals.arraySize; i++)
        {
            var visual = visuals.GetArrayElementAtIndex(i);
            if (!visual.FindPropertyRelative("controlPath").stringValue.StartsWith("<Mouse>/")) continue;
            var image = (Image)visual.FindPropertyRelative("image").objectReferenceValue;
            Assert.That(image.GetType(), Is.EqualTo(typeof(Image)), image.name);
            Assert.That(image.useSpriteMesh, Is.True, image.name);
            Assert.That(image.material.shader.name, Is.EqualTo("ProjectK/UI/MouseButtonFeedback"), image.name);
            Assert.That(image.sprite, Is.Not.Null, image.name);
            Assert.That(image.sprite.texture, Is.EqualTo(body.sprite.texture), image.name);
            Assert.That(image.sprite.vertices.Length, Is.GreaterThan(4), image.name);
            Assert.That(image.sprite.triangles, Is.Not.Empty, image.name);
            Assert.That(image.sprite.bounds.center.sqrMagnitude, Is.LessThan(0.0001f), image.name);
            if (visual.FindPropertyRelative("controlPath").stringValue == "<Mouse>/middleButton")
            {
                Assert.That(visual.FindPropertyRelative("legend").objectReferenceValue, Is.Null);
                Assert.That(image.transform.Find("Legend"), Is.Null, "Only the M3 caption is removed; wheel feedback stays connected.");
            }
            else
            {
                var legend = (Text)visual.FindPropertyRelative("legend").objectReferenceValue;
                Assert.That(legend.font, Is.EqualTo(AssetDatabase.LoadAssetAtPath<Font>("Assets/UI/Fonts/RobotoMono-Medium.ttf")));
            }
            count++;
        }
        Assert.That(count, Is.EqualTo(5));
    }

    [Test]
    public void SharedViewsAndSceneInstancesHaveTheirKeyReferences()
    {
        var guide = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/ControlsGuide.prefab");
        var data = new SerializedObject(guide.GetComponent<ControlsGuideFeedback>());
        var caps = data.FindProperty("keycaps");
        Assert.That(caps.arraySize, Is.EqualTo(11));
        Assert.That(guide.GetComponentsInChildren<Button>(true).Length, Is.EqualTo(24));
        var diagram = guide.GetComponentInChildren<ControlDeviceDisplay>(true);
        Assert.That(diagram, Is.Not.Null);
        Assert.That(diagram.GetComponentsInChildren<Selectable>(true), Is.Empty, "The physical devices only display input.");
        var visualData = new SerializedObject(diagram).FindProperty("visuals");
        Assert.That(visualData.arraySize, Is.EqualTo(109));
        var input = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Settings/Input/PlayerInputSet.inputactions");
        for (int i = 0; i < caps.arraySize; i++)
        {
            var cap = caps.GetArrayElementAtIndex(i);
            Assert.That(cap.FindPropertyRelative("legend").objectReferenceValue, Is.Not.Null);
            if (i == caps.arraySize - 1)
            {
                Assert.That(cap.FindPropertyRelative("button").objectReferenceValue, Is.Null, "Escape stays fixed.");
                continue;
            }
            var button = cap.FindPropertyRelative("button").objectReferenceValue as Button;
            Assert.That(button, Is.Not.Null);
            Assert.That(button.navigation.mode, Is.EqualTo(Navigation.Mode.Explicit));
            string id = cap.FindPropertyRelative("bindingId").stringValue;
            Assert.That(input.Any(action => action.bindings.Any(binding => binding.id.ToString() == id)), Is.True);
        }
        foreach (string name in new[] { "Respawn_Map", "LowerTown_Map", "X_Map", "Y_Map", "UpperClass_Map", "Boss1_Map", "Boss2_Map" })
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/" + name + ".unity");
            try
            {
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (var prompt in root.GetComponentsInChildren<InteractionKeyGuide>(true))
                        Assert.That(new SerializedObject(prompt).FindProperty("keyLabel").objectReferenceValue, Is.Not.Null, name);
                    foreach (var hud in root.GetComponentsInChildren<PlayerAbilityHUD>(true))
                    {
                        var serialized = new SerializedObject(hud);
                        foreach (string slot in new[] { "groundSlamView", "counterView" })
                            Assert.That(serialized.FindProperty(slot).FindPropertyRelative("keyLabel").objectReferenceValue,
                                Is.Not.Null, name + ": " + slot);
                    }
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }

    [UnityTest]
    public IEnumerator LastDeviceSwitchesPromptsAndChoosesTheGuideTab()
    {
        yield return new EnterPlayMode();
        SceneManager.LoadScene("Respawn_Map");
        yield return null;
        var task = VerifyLastDeviceRuntime();
        while (!task.IsCompleted) yield return null;
        task.GetAwaiter().GetResult();
        yield return new ExitPlayMode();
    }

    public static async Task VerifyLastDeviceRuntime()
    {
        var preferences = new Preferences();
        var keyboard = InputSystem.AddDevice<Keyboard>();
        var mouse = InputSystem.AddDevice<Mouse>();
        var pad = InputSystem.AddDevice<Gamepad>();
        var canvas = new GameObject("DeviceSwitchTest", typeof(RectTransform), typeof(Canvas));
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var players = UnityEngine.Object.FindObjectsByType<Player>(FindObjectsSortMode.None)
            .Where(player => player.input.Player.enabled).ToArray();
        foreach (Player player in players) player.input.Player.Disable();
        try
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.V));
            await Task.Delay(100);
            Assert.That(LastInputDevice.IsGamepad, Is.False);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            await Task.Delay(100);
            Assert.That(LastInputDevice.IsGamepad, Is.False, "Connecting a pad alone must not switch prompts.");

            var hud = UnityEngine.Object.FindFirstObjectByType<PlayerAbilityHUD>();
            var hudData = new SerializedObject(hud);
            Text Skill(string name) => (Text)hudData.FindProperty(name).FindPropertyRelative("keyLabel").objectReferenceValue;
            var guideObject = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/ControlsGuide.prefab"), canvas.transform, false);
            guideObject.SetActive(false);
            var tabs = guideObject.GetComponent<ControlsGuideTabs>();
            var feedback = guideObject.GetComponent<ControlsGuideFeedback>();
            var promptObject = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/InteractionKeyGuide.prefab"), canvas.transform, false);
            promptObject.SetActive(true);
            var prompt = promptObject.GetComponent<InteractionKeyGuide>();
            prompt.SetInputType(InteractionInputType.Interact);
            var promptText = (Text)new SerializedObject(prompt).FindProperty("keyLabel").objectReferenceValue;
            var video = UnityEngine.Object.Instantiate(Resources.Load<VideoCutsceneOverlay>("UI/VideoCutsceneOverlay"));
            var videoButton = (Button)new SerializedObject(video).FindProperty("skipButton").objectReferenceValue;
            Text videoText = videoButton.GetComponentInChildren<Text>(true);

            InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = new Vector2(.12f, 0) });
            await Task.Delay(100);
            Assert.That(LastInputDevice.IsGamepad, Is.False, "Small stick drift must not switch prompts.");
            InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = Vector2.up });
            await Task.Delay(150);
            Assert.That(LastInputDevice.IsGamepad, Is.True);
            Assert.That(Skill("groundSlamView").text, Is.EqualTo("RB"));
            Assert.That(Skill("counterView").text, Is.EqualTo("LB"));
            Assert.That(promptText.text, Is.EqualTo("Y"));
            Assert.That(videoText.text, Is.EqualTo("건너뛰기 [B]"));
            prompt.SetInputType(InteractionInputType.PortalUse);
            Assert.That(promptText.text, Is.EqualTo("↑"));
            Assert.That(PlayerKeyBindings.IsCurrentActionPressed("PortalUse"), Is.True, "The stick also animates the portal prompt.");
            guideObject.SetActive(true);
            Assert.That(tabs.ShowingGamepad, Is.True);
            Assert.That(feedback.FirstSelection, Is.EqualTo(guideObject.transform.Find("GamepadPage/AttackKey/AttackCap").GetComponent<Button>()));
            tabs.ShowKeyboard();
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.West));
            await Task.Delay(100);
            Assert.That(tabs.ShowingGamepad, Is.False, "An open guide preserves the manually selected tab.");
            guideObject.SetActive(false);
            Assert.That(feedback.FirstSelection, Is.EqualTo(guideObject.transform.Find("GamepadPage/AttackKey/AttackCap").GetComponent<Button>()));
            guideObject.SetActive(true);
            Assert.That(tabs.ShowingGamepad, Is.True);

            string slam = PlayerKeyBindings.GamepadBindingId("GroundSlam");
            Assert.That(PlayerKeyBindings.TryRebind(slam, GamepadButton.LeftTrigger, out _), Is.True);
            Assert.That(Skill("groundSlamView").text, Is.EqualTo("LT"), "Prompts follow saved pad bindings.");
            InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(1, 1) });
            await Task.Delay(100);
            Assert.That(LastInputDevice.IsGamepad, Is.True, "Minor mouse motion must not switch prompts.");
            InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(8, 0) });
            await Task.Delay(100);
            Assert.That(LastInputDevice.IsGamepad, Is.False);
            Assert.That(Skill("groundSlamView").text, Is.EqualTo("A").IgnoreCase);
            Assert.That(videoText.text, Is.EqualTo("건너뛰기 [ESC]"));
            Assert.That(tabs.ShowingGamepad, Is.True, "Mouse input does not switch an open guide.");
            guideObject.SetActive(false);
            guideObject.SetActive(true);
            Assert.That(tabs.ShowingGamepad, Is.False);
            Assert.That(feedback.FirstSelection, Is.EqualTo(guideObject.transform.Find("Key0/Cap0").GetComponent<Button>()));
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.South));
            await Task.Delay(100);
            Assert.That(LastInputDevice.IsGamepad, Is.True);
            InputSystem.RemoveDevice(pad);
            pad = null;
            await Task.Delay(100);
            Assert.That(LastInputDevice.IsGamepad, Is.False, "Disconnecting the active pad restores keyboard prompts.");
            Assert.That(Skill("groundSlamView").text, Is.EqualTo("A").IgnoreCase);
            UnityEngine.Object.Destroy(video.gameObject);
        }
        finally
        {
            preferences.Dispose();
            if (pad != null) InputSystem.RemoveDevice(pad);
            InputSystem.RemoveDevice(keyboard);
            InputSystem.RemoveDevice(mouse);
            UnityEngine.Object.Destroy(canvas);
            foreach (Player player in players) if (player != null && !player.health.IsDead)
            { PlayerKeyBindings.ApplyTo(player.input); player.input.Player.Enable(); }
        }
    }

    [UnityTest]
    public IEnumerator GamepadGuideTabsAndPressedFeedbackPreserveKeyboardControls()
    {
        yield return new EnterPlayMode();
        var task = VerifyGamepadGuideRuntime();
        while (!task.IsCompleted) yield return null;
        task.GetAwaiter().GetResult();
        yield return new ExitPlayMode();
    }

    public static async Task VerifyGamepadGuideRuntime()
    {
        var players = UnityEngine.Object.FindObjectsByType<Player>(FindObjectsSortMode.None)
            .Where(player => player.input.Player.enabled).ToArray();
        foreach (Player player in players) player.input.Player.Disable();
        var eventSystems = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None)
            .Where(system => system.enabled).ToArray();
        foreach (EventSystem system in eventSystems) system.enabled = false;
        var canvas = new GameObject("GamepadGuideTest", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.GetComponent<Canvas>().sortingOrder = 1000;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(480, 288);
        var events = new GameObject("GamepadGuideTestEvents", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        events.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>().AssignDefaultActions();
        var pad = InputSystem.AddDevice<Gamepad>();
        var preferences = new Preferences();
        GameplayPause.Handle testPause = null;
        try
        {
            var guide = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/ControlsGuide.prefab"), canvas.transform, false);
            guide.SetActive(true);
            var tabs = guide.GetComponent<ControlsGuideTabs>();
            var feedback = guide.GetComponent<ControlsGuideFeedback>();
            GameplayPause.TryAcquire(feedback, out testPause);
            Transform keyboard = guide.transform.Find("DeviceDiagram");
            Transform page = guide.transform.Find("GamepadPage");
            var firstKey = guide.transform.Find("Key0/Cap0").GetComponent<Button>();
            Vector2 keyboardPosition = ((RectTransform)keyboard).anchoredPosition;
            Assert.That(feedback.FirstSelection, Is.EqualTo(firstKey));
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.RightShoulder));
            await Task.Delay(150);
            Assert.That(tabs.ShowingGamepad && page.gameObject.activeSelf && !keyboard.gameObject.activeSelf, Is.True);
            Assert.That(page.Find("Diagram").GetComponentsInChildren<Selectable>(), Is.Empty, "The illustration only displays input.");
            Assert.That(page.GetComponentsInChildren<Button>().Length, Is.EqualTo(8));
            var physical = (RectTransform)page.Find("Diagram/X");
            Vector2 restPosition = physical.anchoredPosition;
            var action = page.Find("AttackAction").GetComponent<Text>();
            Color restColor = action.color;
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.West));
            await Task.Delay(150);
            Assert.That(physical.anchoredPosition.y, Is.LessThan(restPosition.y));
            Assert.That(physical.localScale.x, Is.LessThan(1f));
            Assert.That(action.color, Is.EqualTo(restColor), "Live input does not compete with the selected binding.");
            Assert.That(feedback.IsRebinding, Is.False);
            InputSystem.QueueStateEvent(pad, new GamepadState());
            await Task.Delay(200);
            Assert.That(physical.anchoredPosition, Is.EqualTo(restPosition));
            Assert.That(action.color, Is.EqualTo(restColor));
            var stick = (RectTransform)page.Find("Diagram/LeftStick");
            Vector2 stickRest = stick.anchoredPosition;
            InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = new Vector2(.8f, .6f) });
            await Task.Delay(150);
            Assert.That(stick.anchoredPosition.x, Is.GreaterThan(stickRest.x));
            InputSystem.RemoveDevice(pad);
            pad = null;
            await Task.Delay(200);
            Assert.That(stick.anchoredPosition, Is.EqualTo(stickRest), "Disconnecting restores the stick.");
            pad = InputSystem.AddDevice<Gamepad>();
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.LeftShoulder));
            await Task.Delay(150);
            Assert.That(tabs.ShowingGamepad, Is.False);
            Assert.That(keyboard.gameObject.activeSelf, Is.True);
            Assert.That(((RectTransform)keyboard).anchoredPosition, Is.EqualTo(keyboardPosition));
            firstKey.onClick.Invoke();
            Assert.That(feedback.IsRebinding, Is.True);
            tabs.ShowGamepad();
            Assert.That(tabs.ShowingGamepad, Is.False, "Tab switching cannot interrupt a key capture.");
            feedback.TryCancelRebind();
            await Task.Delay(150);
            tabs.ShowGamepad();
            var attackCap = page.Find("AttackKey/AttackCap").GetComponent<Button>();
            attackCap.Select();
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.South));
            await Task.Delay(150);
            Assert.That(feedback.IsRebinding, Is.True, "A starts input capture.");
            InputSystem.QueueStateEvent(pad, new GamepadState());
            await Task.Delay(100);
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.East));
            await Task.Delay(150);
            var dashCap = page.Find("DashKey/DashCap").GetComponent<Button>();
            Color duplicateTint = attackCap.image.color;
            Text attackLegend = attackCap.transform.Find("Label").GetComponent<Text>();
            Text dashLegend = dashCap.transform.Find("Label").GetComponent<Text>();
            Assert.That(feedback.IsRebinding, Is.False, "A duplicate binding is accepted.");
            Assert.That(attackLegend.text, Is.EqualTo("B"));
            Assert.That(dashLegend.text, Is.EqualTo("B"));
            Assert.That(attackLegend.color, Is.EqualTo(dashLegend.color));
            Assert.That(duplicateTint.g, Is.LessThan(.7f));
            Assert.That(dashCap.image.color.g, Is.LessThan(.7f));
            InputSystem.QueueStateEvent(pad, new GamepadState());
            await Task.Delay(150);
            attackCap.onClick.Invoke();
            await Task.Delay(100);
            InputSystem.QueueStateEvent(pad, new GamepadState { leftTrigger = 1f });
            await Task.Delay(150);
            Assert.That(feedback.IsRebinding, Is.False);
            Assert.That(attackCap.transform.Find("Label").GetComponent<Text>().text, Is.EqualTo("LT"));
            Assert.That(dashCap.image.color.g, Is.GreaterThan(duplicateTint.g), "Resolving a duplicate restores its normal tint.");
            Assert.That(attackCap.interactable, Is.False, "Input capture waits for the assigned trigger's release.");
            InputSystem.QueueStateEvent(pad, new GamepadState());
            await Task.Delay(150);
            Assert.That(attackCap.interactable, Is.True);
            Assert.That(PlayerKeyBindings.TryRebind(PlayerKeyBindings.GamepadBindingId("Dash"), GamepadButton.RightTrigger, out _), Is.True);
            attackCap.Select();
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.South));
            await Task.Delay(100);
            InputSystem.QueueStateEvent(pad, new GamepadState());
            await Task.Delay(100);
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.East));
            await Task.Delay(150);
            Assert.That(feedback.IsRebinding, Is.False);
            Assert.That(attackCap.transform.Find("Label").GetComponent<Text>().text, Is.EqualTo("B"));
            InputSystem.QueueStateEvent(pad, new GamepadState());
            await Task.Delay(150);
            attackCap.onClick.Invoke();
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.Start));
            await Task.Delay(150);
            Assert.That(feedback.IsRebinding, Is.False, "Start cancels without changing the assigned button.");
            InputSystem.QueueStateEvent(pad, new GamepadState());
            await Task.Delay(150);
            guide.SetActive(false);
            guide.SetActive(true);
            Assert.That(tabs.ShowingGamepad && page.gameObject.activeSelf, Is.True);
        }
        finally
        {
            testPause?.Dispose();
            preferences.Dispose();
            if (pad != null) InputSystem.RemoveDevice(pad);
            UnityEngine.Object.Destroy(canvas);
            UnityEngine.Object.Destroy(events);
            foreach (Player player in players) if (player != null && !player.health.IsDead)
            { PlayerKeyBindings.ApplyTo(player.input); player.input.Player.Enable(); }
            foreach (EventSystem system in eventSystems) if (system != null) system.enabled = true;
        }
    }

    [UnityTest]
    public IEnumerator ArrowsEnterMouseFeedbackCancellationAndSceneChangesKeepWorking()
    {
        yield return new EnterPlayMode();
        SceneManager.LoadScene("Respawn_Map");
        yield return null;
        var task = VerifyRuntime();
        while (!task.IsCompleted) yield return null;
        task.GetAwaiter().GetResult();
        yield return new ExitPlayMode();
    }

    [UnityTearDown]
    public IEnumerator RestoreEditor()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }

    public static async Task VerifyRuntime()
    {
        await Until(() => !SceneTransitionOverlay.IsTransitioning && !GameplayPause.IsActive);
        using (new Preferences())
        {
            var player = UnityEngine.Object.FindFirstObjectByType<Player>();
            PlayerKeyBindings.ApplyTo(player.input);
            var pause = UnityEngine.Object.FindFirstObjectByType<PauseMenu>();
            pause.Open();
            await Task.Delay(400);
            pause.ShowControls();
            await Task.Delay(400);
            var feedback = UnityEngine.Object.FindFirstObjectByType<ControlsGuideFeedback>();
            var caps = new SerializedObject(feedback).FindProperty("keycaps");
            Button Cap(int index) => (Button)caps.GetArrayElementAtIndex(index).FindPropertyRelative("button").objectReferenceValue;
            Text Legend(int index) => (Text)caps.GetArrayElementAtIndex(index).FindPropertyRelative("legend").objectReferenceValue;
            Text ActionLabel(int index) => (Text)caps.GetArrayElementAtIndex(index).FindPropertyRelative("actionLabel").objectReferenceValue;
            var display = feedback.GetComponentInChildren<ControlDeviceDisplay>();
            var visuals = new SerializedObject(display).FindProperty("visuals");
            Image DeviceImage(string path)
            {
                for (int i = 0; i < visuals.arraySize; i++)
                {
                    var visual = visuals.GetArrayElementAtIndex(i);
                    if (string.Equals(visual.FindPropertyRelative("controlPath").stringValue, path, StringComparison.OrdinalIgnoreCase))
                        return (Image)visual.FindPropertyRelative("image").objectReferenceValue;
                }
                throw new InvalidOperationException("Missing device visual: " + path);
            }
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Cap(0).gameObject));
            Assert.That(Cap(0).transform.Find("FocusMarker").GetComponent<Image>().enabled, Is.True,
                "The initial selection remains visible after the panel transition.");
            Color normalLabelColor = ActionLabel(2).color;
            Assert.That(ActionLabel(0).color, Is.Not.EqualTo(normalLabelColor), "The selected key highlights its action name.");
            Assert.That(ActionLabel(0).text, Is.EqualTo("왼쪽"));
            Assert.That(ActionLabel(1).text, Is.EqualTo("오른쪽"));
            Assert.That(ActionLabel(1).color, Is.EqualTo(normalLabelColor));
            await Click(MouseButton.Left, new Vector2(12, 12));
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Cap(0).gameObject),
                "A background click must preserve the last selection for keyboard navigation.");
            Assert.That(Cap(0).transform.Find("FocusMarker").GetComponent<Image>().enabled, Is.True);
            await Press(Key.RightArrow);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Cap(3).gameObject));
            Assert.That(Cap(0).transform.Find("FocusMarker").GetComponent<Image>().enabled, Is.False);
            Assert.That(Cap(3).transform.Find("FocusMarker").GetComponent<Image>().enabled, Is.True);
            Assert.That(ActionLabel(0).color, Is.EqualTo(normalLabelColor));
            Assert.That(ActionLabel(3).color, Is.Not.EqualTo(normalLabelColor));
            await Press(Key.RightArrow);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Cap(6).gameObject));
            await Press(Key.DownArrow);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Cap(7).gameObject));
            await Press(Key.RightArrow);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Cap(9).gameObject));
            await Press(Key.DownArrow);
            Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("Back"));
            await Press(Key.LeftArrow);
            Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("ResetBindings"));
            await Press(Key.UpArrow);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Cap(5).gameObject));
            await Press(Key.UpArrow);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Cap(1).gameObject));
            await Press(Key.RightArrow);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Cap(4).gameObject));
            await Press(Key.DownArrow);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Cap(2).gameObject));
            await Press(Key.UpArrow);
            await Press(Key.UpArrow);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Cap(3).gameObject));

            var pauseReset = (Button)new SerializedObject(feedback).FindProperty("resetButton").objectReferenceValue;
            pauseReset.onClick.Invoke();
            await Press(Key.Escape);
            Assert.That(pause.IsOpen && feedback.isActiveAndEnabled, Is.True, "Escape cancels reset without leaving controls.");
            Cap(3).Select();

            var bindingRect = (RectTransform)Cap(2).transform;
            Vector2 bindingRestPosition = bindingRect.anchoredPosition;
            Color bindingRestColor = Cap(2).image.color;
            float pixelScale = feedback.GetComponentInParent<Canvas>().scaleFactor;
            Cap(2).onClick.Invoke();
            Assert.That(feedback.IsRebinding, Is.True);
            Assert.That(bindingRect.anchoredPosition.y, Is.EqualTo(bindingRestPosition.y - 5f / pixelScale).Within(.001f));
            Assert.That(bindingRect.localScale, Is.EqualTo(Vector3.one));
            Assert.That(Cap(2).image.color, Is.EqualTo(new SerializedObject(feedback).FindProperty("highlightTint").colorValue));
            feedback.TryCancelRebind();
            await Task.Delay(220);
            Assert.That(bindingRect.anchoredPosition, Is.EqualTo(bindingRestPosition));
            Assert.That(Cap(2).image.color, Is.EqualTo(bindingRestColor));

            Cap(2).Select();
            await Press(Key.Enter);
            Assert.That(feedback.IsRebinding, Is.True);
            Assert.That(Legend(2).text, Is.EqualTo("?"));
            Assert.That(EventSystem.current.sendNavigationEvents, Is.False);
            Assert.That(ActionLabel(2).color, Is.Not.EqualTo(normalLabelColor), "The label stays highlighted while waiting.");
            await Press(Key.V);
            Assert.That(feedback.IsRebinding, Is.False);
            Assert.That(Legend(2).text, Is.EqualTo("V").IgnoreCase);
            Assert.That(player.input.Player.Jump.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/V").IgnoreCase);
            Assert.That(player.input.Player.enabled, Is.False, "Rebinding must keep the paused Player disabled.");
            Assert.That(EventSystem.current.sendNavigationEvents, Is.True);

            var mouse = Mouse.current;
            Assert.That(mouse, Is.Not.Null);
            Vector2 point = RectTransformUtility.WorldToScreenPoint(null, Cap(3).transform.position);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point, buttons = 1 });
            await Awaitable.NextFrameAsync(); await Awaitable.NextFrameAsync();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            await Awaitable.NextFrameAsync(); await Awaitable.NextFrameAsync();
            Assert.That(feedback.IsRebinding, Is.True, "Clicking a keycap starts waiting.");
            await Press(Key.H);
            Assert.That(Legend(3).text, Is.EqualTo("H").IgnoreCase);
            Assert.That(player.input.Player.Attack.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/H").IgnoreCase);

            Cap(2).Select(); await Press(Key.Enter);
            await Press(Key.H);
            Assert.That(feedback.IsRebinding, Is.True, "A duplicate key keeps waiting.");
            Assert.That(new SerializedObject(feedback).FindProperty("bindingHint").objectReferenceValue,
                Is.Not.Null);
            await Press(Key.Escape);
            Assert.That(feedback.IsRebinding, Is.False);
            Assert.That(pause.IsOpen, Is.True, "Escape cancels input capture without leaving the page.");
            Assert.That(feedback.isActiveAndEnabled, Is.True);

            var rect = (RectTransform)Legend(2).transform.parent;
            var physicalKey = DeviceImage("<Keyboard>/v").rectTransform;
            Cap(3).Select();
            await Task.Delay(150);
            float restScale = rect.localScale.y;
            Vector2 bindingInputRestPosition = rect.anchoredPosition;
            Vector2 physicalRestPosition = physicalKey.anchoredPosition;
            Color deviceRestColor = DeviceImage("<Keyboard>/v").color;
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.V));
            await Task.Delay(100);
            Assert.That(DeviceImage("<Keyboard>/v").color,
                Is.EqualTo(new SerializedObject(display).FindProperty("pressedColor").colorValue));
            Assert.That(physicalKey.localScale, Is.EqualTo(Vector3.one), "Input feedback keeps the pixel font at its native scale.");
            Assert.That(physicalKey.anchoredPosition.y, Is.EqualTo(physicalRestPosition.y - 2f / pixelScale).Within(.001f));
            Assert.That(rect.anchoredPosition, Is.EqualTo(bindingInputRestPosition), "Real input does not depress the binding selector.");
            Assert.That(rect.localScale.y, Is.EqualTo(restScale), "The binding selector does not animate from real input.");
            Assert.That(ActionLabel(2).color, Is.EqualTo(normalLabelColor), "Real input does not compete with the selected action.");
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            await Task.Delay(150);
            Assert.That(DeviceImage("<Keyboard>/v").color, Is.EqualTo(deviceRestColor));
            Assert.That(physicalKey.anchoredPosition, Is.EqualTo(physicalRestPosition));
            Assert.That(rect.localScale.y, Is.EqualTo(restScale).Within(0.001f));
            Assert.That(ActionLabel(2).color, Is.EqualTo(normalLabelColor), "An unselected action returns to its normal color after release.");

            string slam = PlayerKeyBindings.ActionBindingId("GroundSlam");
            Assert.That(PlayerKeyBindings.TryRebind(slam, Key.Backspace, out _), Is.True);
            var hud = UnityEngine.Object.FindFirstObjectByType<PlayerAbilityHUD>();
            var hudData = new SerializedObject(hud).FindProperty("groundSlamView");
            Assert.That(((Text)hudData.FindPropertyRelative("keyLabel").objectReferenceValue).text, Is.EqualTo("Bksp"));

            Cap(6).Select(); await Press(Key.Enter); await Press(Key.Enter);
            Assert.That(feedback.IsRebinding, Is.False, "Binding Enter must not immediately submit again.");
            Assert.That(Legend(6).text, Is.EqualTo("Enter").IgnoreCase);

            Cap(0).Select(); await Press(Key.Enter); await Press(Key.RightShift);
            Assert.That(Legend(0).text, Is.EqualTo("R Shift"));
            var left = (RectTransform)Cap(0).transform;
            var right = (RectTransform)Cap(1).transform;
            Assert.That(left.parent, Is.Not.EqualTo(right.parent));
            Assert.That(caps.GetArrayElementAtIndex(0).FindPropertyRelative("separator").objectReferenceValue, Is.Null);
            Assert.That(Legend(1).text, Is.EqualTo("→"), "Changing left must preserve right.");
            Cap(1).Select(); await Press(Key.Enter);
            Assert.That(new SerializedObject(feedback).FindProperty("bindingHint").objectReferenceValue is Text hint
                && hint.text.Contains("오른쪽"), Is.True);
            await Press(Key.K);
            Assert.That(Legend(1).text, Is.EqualTo("K").IgnoreCase);
            Assert.That(Legend(0).text, Is.EqualTo("R Shift"), "Changing right must preserve left.");

            string attack = PlayerKeyBindings.ActionBindingId("Attack");
            Vector2 mouseArtworkPosition = DeviceImage("<Mouse>/leftButton").rectTransform.anchoredPosition;
            Cap(3).Select(); await Press(Key.Enter);
            point = RectTransformUtility.WorldToScreenPoint(null, Cap(3).transform.position);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point, buttons = 1 });
            await Task.Delay(100);
            Assert.That(feedback.IsRebinding, Is.False);
            Assert.That(Legend(3).text, Is.EqualTo("M1"));
            Assert.That(player.input.Player.Attack.bindings[0].effectivePath, Is.EqualTo("<Mouse>/leftButton"));
            Assert.That(Cap(3).interactable, Is.False, "The click remains consumed until release.");
            Assert.That(DeviceImage("<Mouse>/leftButton").color,
                Is.EqualTo(new SerializedObject(display).FindProperty("pressedColor").colorValue));
            Assert.That(DeviceImage("<Mouse>/leftButton").rectTransform.anchoredPosition, Is.EqualTo(mouseArtworkPosition));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            await Task.Delay(150);
            Assert.That(feedback.IsRebinding, Is.False, "Releasing the capture click must not start another rebind.");
            Assert.That(Cap(3).interactable, Is.True);
            Cap(2).Select(); await Press(Key.Enter);
            await Click(MouseButton.Left, point);
            Assert.That(feedback.IsRebinding, Is.True, "Duplicate mouse bindings keep waiting.");
            await Press(Key.Escape);
            Assert.That(PlayerKeyBindings.TryRebind(slam, MouseButton.Middle, out _), Is.True);
            Assert.That(((Text)hudData.FindPropertyRelative("keyLabel").objectReferenceValue).text, Is.EqualTo("M3"));
            Cap(7).Select(); await Press(Key.Enter); await Click(MouseButton.Back, point);
            Assert.That(Legend(7).text, Is.EqualTo("M4"));
            Assert.That(feedback.IsRebinding, Is.False);

            var promptRoot = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/InteractionKeyGuide.prefab"));
            try
            {
                promptRoot.SetActive(true);
                var prompt = promptRoot.GetComponent<InteractionKeyGuide>();
                prompt.SetInputType(InteractionInputType.Interact);
                Assert.That(PlayerKeyBindings.TryRebind(PlayerKeyBindings.ActionBindingId("Interact"), MouseButton.Right, out _), Is.True);
                var promptData = new SerializedObject(prompt);
                Assert.That(((Text)promptData.FindProperty("keyLabel").objectReferenceValue).text, Is.EqualTo("M2"));
                var promptCap = (RectTransform)promptData.FindProperty("keycap").objectReferenceValue;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = point, buttons = (ushort)(1 << (int)MouseButton.Right) });
                await Task.Delay(100);
                Assert.That(promptCap.localScale.y, Is.LessThan(1f));
                InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            }
            finally { UnityEngine.Object.Destroy(promptRoot); }

            Cap(2).Select(); await Press(Key.Enter);
            pause.Close();
            await Until(() => !pause.IsOpen);
            Assert.That(EventSystem.current.sendNavigationEvents, Is.True, "Closing a waiting page restores navigation.");
            Assert.That(player.input.Player.enabled, Is.True);
            int jumpInputs = 0;
            void CountJump(InputAction.CallbackContext _) => jumpInputs++;
            player.input.Player.Jump.started += CountJump;
            try { await Press(Key.V); await Press(Key.C); }
            finally { player.input.Player.Jump.started -= CountJump; }
            Assert.That(jumpInputs, Is.EqualTo(1), "The new key triggers gameplay and the old key no longer does.");
            Assert.That(SceneTransitionOverlay.TryLoadScene("Assets/Scenes/Title.unity", 0f, 0.03f, Color.black), Is.True);
            await Until(() => SceneManager.GetActiveScene().name == "Title" && !SceneTransitionOverlay.IsTransitioning);
            await Task.Delay(400);
            UnityEngine.Object.FindFirstObjectByType<TitleMenu>().ShowControls();
            await Task.Delay(700);
            feedback = UnityEngine.Object.FindFirstObjectByType<ControlsGuideFeedback>();
            caps = new SerializedObject(feedback).FindProperty("keycaps");
            await Click(MouseButton.Left, new Vector2(12, 12));
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Cap(0).gameObject));
            await Press(Key.RightArrow);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Cap(3).gameObject),
                "Title controls must remain keyboard-navigable after a background click.");
            Assert.That(Legend(2).text, Is.EqualTo("V").IgnoreCase);
            Assert.That(Legend(3).text, Is.EqualTo("M1"));
            Assert.That(Legend(6).text, Is.EqualTo("M3"));
            Assert.That(Legend(7).text, Is.EqualTo("M4"));
            Assert.That(Legend(8).text, Is.EqualTo("M2"));
            var reset = (Button)new SerializedObject(feedback).FindProperty("resetButton").objectReferenceValue;
            reset.onClick.Invoke();
            var confirmation = (MenuConfirmationDialog)new SerializedObject(feedback)
                .FindProperty("resetConfirmation").objectReferenceValue;
            Assert.That(confirmation.IsOpen, Is.True);
            Assert.That(Legend(2).text, Is.EqualTo("V").IgnoreCase, "Opening confirmation must not reset bindings.");
            Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("Cancel"));
            Assert.That(Cap(0).IsInteractable(), Is.False);
            Cap(0).Select();
            await Awaitable.NextFrameAsync(); await Awaitable.NextFrameAsync();
            Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("Cancel"), "Focus stays inside confirmation.");
            await Press(Key.Enter);
            Assert.That(confirmation.IsOpen, Is.False, "Enter on the initial selection cancels reset.");
            Assert.That(Legend(2).text, Is.EqualTo("V").IgnoreCase);
            reset.onClick.Invoke();
            await Press(Key.Escape);
            Assert.That(feedback.isActiveAndEnabled, Is.True);
            Assert.That(confirmation.IsOpen, Is.False);
            reset.onClick.Invoke();
            await Press(Key.LeftArrow);
            Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("Confirm"));
            await Press(Key.Enter);
            Assert.That(confirmation.IsOpen, Is.False);
            Assert.That(Legend(2).text, Is.EqualTo("C").IgnoreCase);
            Assert.That(Legend(3).text, Is.EqualTo("Z").IgnoreCase);
            Assert.That(Legend(0).text, Is.EqualTo("←"));
        }
    }

    private static async Task Press(Key key)
    {
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(key));
        await Awaitable.NextFrameAsync(); await Awaitable.NextFrameAsync();
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
        await Awaitable.NextFrameAsync(); await Awaitable.NextFrameAsync();
    }

    private static async Task Click(MouseButton button, Vector2 position)
    {
        InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = position, buttons = (ushort)(1 << (int)button) });
        await Awaitable.NextFrameAsync(); await Awaitable.NextFrameAsync();
        InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = position });
        await Awaitable.NextFrameAsync(); await Awaitable.NextFrameAsync();
    }

    private static async Task Until(Func<bool> condition)
    {
        float deadline = Time.realtimeSinceStartup + 20f;
        while (!condition())
        {
            Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline));
            await Awaitable.NextFrameAsync();
        }
    }
}
