using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class SkillRejectionFeedbackTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    private static object Get(object target, string field) => target.GetType().GetField(field, PrivateInstance).GetValue(target);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, PrivateInstance).SetValue(target, value);
    private static void Draw(PlayerAbilityHUD hud) => typeof(PlayerAbilityHUD).GetMethod("LateUpdate", PrivateInstance).Invoke(hud, null);

    [UnityTest]
    public IEnumerator RejectionsRespectTheInputBufferAndRestoreTheirHUD()
    {
        yield return new EnterPlayMode();
        Task task = VerifyRuntime();
        while (!task.IsCompleted) yield return null;
        task.GetAwaiter().GetResult();
        yield return new ExitPlayMode();
    }

    [UnityTearDown]
    public IEnumerator ExitRuntime()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }

    public static async Task VerifyRuntime()
    {
        Assert.That(Application.isPlaying, Is.True);
        var playerObject = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab"));
        GameObject hudObject = null;
        PlayerData data = null;
        try
        {
            playerObject.transform.position = new Vector3(1000f, 1000f);
            playerObject.GetComponent<Rigidbody2D>().simulated = false;
            playerObject.GetComponent<WeaponUnlockOnSceneStart>().enabled = false;
            var player = playerObject.GetComponent<Player>();
            var movement = playerObject.GetComponent<PlayerMovement>();
            movement.enabled = false;
            Set(movement, "isGrounded", true);
            Set(player.health, "currentHP", player.health.MaxHP);
            data = Object.Instantiate(player.Data);
            Set(player, "data", data);
            Set(data, "actionBufferDuration", 0.15f);
            playerObject.GetComponent<PlayerWeaponController>().EquipWeapon();
            player.anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            player.anim.speed = 0f;
            player.anim.SetBool("isGrounded", true);
            player.anim.Update(0f);

            var canvas = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Canvas.prefab");
            hudObject = Object.Instantiate(canvas.GetComponentInChildren<PlayerAbilityHUD>(true).gameObject);
            var hud = hudObject.GetComponent<PlayerAbilityHUD>();
            hud.enabled = false;
            Set(hud, "player", player);
            hud.enabled = true;
            await Task.Delay(100);
            var rejections = new List<PlayerAction>();
            player.ActionRejected += rejections.Add;
            var slam = playerObject.GetComponent<PlayerGroundSlam>();
            var counter = playerObject.GetComponent<PlayerCounter>();
            var actions = (PlayerActionController)Get(player, "actions");
            var slot = Get(hud, "groundSlamView");
            var frame = (Image)slot.GetType().GetField("frame").GetValue(slot);
            var icon = (Image)slot.GetType().GetField("icon").GetValue(slot);
            var key = (Text)slot.GetType().GetField("keyLabel").GetValue(slot);
            var plate = (RectTransform)slot.GetType().GetField("keyPlate").GetValue(slot);
            var remaining = (Text)slot.GetType().GetField("remainingLabel").GetValue(slot);
            Vector3 iconScale = icon.transform.localScale;
            Vector3 keyScale = plate.localScale;
            Vector3 remainingScale = remaining.transform.localScale;
            Color keyColor = key.color;
            Color keyBackground = plate.GetComponent<Image>().color;

            Set(Get(slam, "execution"), "nextUseTime", Time.time + 5f);
            player.RequestAction(PlayerAction.GroundSlam);
            Assert.That(rejections, Is.EqualTo(new[] { PlayerAction.GroundSlam }));
            Draw(hud);
            Assert.That(frame.color.r, Is.GreaterThan(frame.color.g * 2f));
            Assert.That(key.color, Is.Not.EqualTo(keyColor));
            Assert.That(plate.GetComponent<Image>().color, Is.Not.EqualTo(keyBackground));
            Assert.That(icon.transform.localScale.x, Is.LessThan(iconScale.x));
            Assert.That(remaining.transform.localScale.x, Is.GreaterThan(remainingScale.x));
            Assert.That(remaining.text, Is.Not.Empty);
            float rejectedUntil = (float)Get(slot, "rejectedUntil");
            float nextSound = (float)Get(hud, "nextRejectedSoundTime");
            player.RequestAction(PlayerAction.GroundSlam);
            Assert.That((float)Get(slot, "rejectedUntil"), Is.EqualTo(rejectedUntil), "Repeated presses do not stack the pulse.");
            Assert.That((float)Get(hud, "nextRejectedSoundTime"), Is.EqualTo(nextSound), "Repeated presses do not restart the sound.");
            await Task.Delay(350);
            Assert.That(rejections.Count, Is.EqualTo(2), "Expiry must not repeat a rejection already shown immediately.");
            Draw(hud);
            Assert.That(icon.transform.localScale, Is.EqualTo(iconScale));
            Assert.That(plate.localScale, Is.EqualTo(keyScale));
            Assert.That(remaining.transform.localScale, Is.EqualTo(remainingScale));
            Assert.That(key.color, Is.EqualTo(keyColor));
            Assert.That(plate.GetComponent<Image>().color, Is.EqualTo(keyBackground));

            rejections.Clear();
            Set(Get(slam, "execution"), "nextUseTime", 0f);
            Set(movement, "isGrounded", false);
            Draw(hud);
            Assert.That(frame.color, Is.EqualTo((Color)Get(hud, "readyRingColor")), "Ground slam stays ready in the air.");
            Assert.That(icon.color, Is.EqualTo(Color.white));
            player.RequestAction(PlayerAction.Counter);
            Assert.That(rejections, Is.Empty, "An airborne counter input can still be buffered for landing.");
            await Task.Delay(250);
            Assert.That(rejections, Is.EqualTo(new[] { PlayerAction.Counter }));
            actions.ConsumeBufferedAction();
            Assert.That(rejections.Count, Is.EqualTo(1));

            rejections.Clear();
            player.RequestAction(PlayerAction.GroundSlam);
            Assert.That(slam.IsExecuting, Is.True, "Ground slam starts immediately in the air.");
            Draw(hud);
            Assert.That(frame.color, Is.EqualTo((Color)Get(hud, "activeRingColor")));
            await Task.Delay(200);
            Assert.That(rejections, Is.Empty, "A successful airborne ground slam must not show rejection feedback.");
            slam.Cancel();
            Set(movement, "isGrounded", true);
            player.anim.Rebind();
            player.anim.SetBool("isGrounded", true);
            player.anim.Update(0f);

            Set(Get(slam, "execution"), "nextUseTime", Time.time + 0.06f);
            player.RequestAction(PlayerAction.GroundSlam);
            Assert.That(rejections, Is.Empty, "A cooldown ending inside the buffer must not reject immediately.");
            await Task.Delay(120);
            Assert.That(slam.IsExecuting, Is.True);
            Assert.That(rejections, Is.Empty);
            slam.Cancel();
            player.anim.Rebind();
            player.anim.SetBool("isGrounded", true);
            player.anim.Update(0f);

            var attack = playerObject.GetComponent<PlayerAttack>();
            Assert.That(attack.TryAttack(), Is.True);
            attack.OpenDashCancelWindow();
            Assert.That(player.CanStartAction, Is.False);
            Assert.That(player.CanStartCounter, Is.True);
            Draw(hud);
            var counterSlot = Get(hud, "counterView");
            var counterFrame = (Image)counterSlot.GetType().GetField("frame").GetValue(counterSlot);
            Assert.That(counterFrame.color, Is.EqualTo((Color)Get(hud, "readyRingColor")));
            player.RequestAction(PlayerAction.Counter);
            Assert.That(counter.IsExecuting, Is.True);
            Assert.That(rejections, Is.Empty);

            hud.enabled = false;
            Assert.That(plate.localScale, Is.EqualTo(keyScale));
            Assert.That(key.color, Is.EqualTo(keyColor));
            Assert.That(plate.GetComponent<Image>().color, Is.EqualTo(keyBackground));
            Assert.That(GameAudioSettings.Shared.GetUIClip(UIAudio.Cue.Back), Is.Not.Null);
        }
        finally
        {
            if (hudObject != null) Object.Destroy(hudObject);
            Object.Destroy(playerObject);
            if (data != null) Object.Destroy(data);
        }
    }
}
