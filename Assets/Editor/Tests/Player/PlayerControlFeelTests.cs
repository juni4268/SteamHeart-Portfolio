using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class PlayerControlFeelTests
{
    private Player player;
    private PlayerMovement movement;

    private static void Set(object target, string field, object value) =>
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    private static T Get<T>(object target, string field) =>
        (T)target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

    private static void Invoke(object target, string method) =>
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);

    [UnitySetUp]
    public IEnumerator CreatePlayer()
    {
        yield return new EnterPlayMode();
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects()) root.SetActive(false);
        Time.timeScale = 1f;
        new GameObject("Control feel test camera", typeof(Camera)).tag = "MainCamera";
        var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab"));
        instance.GetComponent<WeaponUnlockOnSceneStart>().enabled = false;
        instance.transform.position = new Vector3(1000f, 1000f);
        instance.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        instance.GetComponent<Rigidbody2D>().interpolation = RigidbodyInterpolation2D.None;
        player = instance.GetComponent<Player>();
        movement = instance.GetComponent<PlayerMovement>();
        Set(player.health, "currentHP", player.health.MaxHP);
        Set(player, "data", Object.Instantiate(player.Data));
        player.motor.Stop();
        Set(movement, "isGrounded", true);
        player.GetComponent<PlayerWeaponController>().EquipWeapon();
        player.anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        player.anim.SetBool("isGrounded", true);
        player.anim.Update(0f);
    }

    [UnityTest]
    public IEnumerator DeathStopsHorizontalMotionAndFallsDuringTheDeathAnimation()
    {
        var body = player.GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Dynamic;
        Physics2D.SyncTransforms();
        body.linearVelocity = new Vector2(5f, 3f);
        Assert.That(player.health.TryTakeDamage(new DamageInfo(player.health.MaxHP)), Is.True);
        Assert.That(player.health.IsDead, Is.True);
        Assert.That(body.simulated, Is.True);
        Assert.That(body.constraints & RigidbodyConstraints2D.FreezePositionX, Is.EqualTo(RigidbodyConstraints2D.FreezePositionX));
        Vector2 deathPosition = body.position;
        yield return new WaitForSeconds(0.15f);
        Assert.That(body.position.x, Is.EqualTo(deathPosition.x).Within(0.001f));
        Assert.That(body.position.y, Is.LessThan(deathPosition.y));
    }

    [UnityTest]
    public IEnumerator DashLandAllowsBufferedMovementAndConsumesJumpOnlyAfterCompletion()
    {
        CreatePlatform();
        Set(movement, "moveInput", Vector2.right);
        Assert.That(player.dash.TryDash(), Is.True);
        player.anim.Update(0f);
        player.anim.Update(0.01f);
        player.anim.Update(0.31f);
        player.anim.Update(0f);
        Assert.That(Get<bool>(player.dash, "landingEntered"), Is.True);

        player.RequestAction(PlayerAction.Jump);
        Invoke(player, "LateUpdate");
        Assert.That(player.CanMove, Is.True);
        Assert.That(player.CanStartAction, Is.False);
        Invoke(movement, "FixedUpdate");
        Assert.That(player.motor.Velocity.x, Is.GreaterThan(0f));
        Assert.That(player.dash.IsDashing, Is.True);
        Assert.That(player.motor.Velocity.y, Is.Zero);

        player.anim.Update(0.25f);
        player.anim.Update(0f);
        Invoke(player, "LateUpdate");
        Assert.That(player.dash.IsDashing, Is.False);
        Assert.That(player.motor.Velocity.x, Is.GreaterThan(0f));
        Assert.That(player.motor.Velocity.y, Is.EqualTo(player.Data.JumpForce));
        player.motor.Stop();
        Invoke(player, "LateUpdate");
        Assert.That(player.motor.Velocity.y, Is.Zero, "A consumed input must not repeat.");
        yield break;
    }

    [UnityTest]
    public IEnumerator DashPassesOneWayCornersButKeepsTopAndWallCollisions()
    {
        SimulationMode2D previousMode = Physics2D.simulationMode;
        Physics2D.simulationMode = SimulationMode2D.Script;
        player.anim.speed = 0f;
        Set(player.Data, "dashDistance", 2f);
        Set(player.Data, "dashSpeedCurve", AnimationCurve.Constant(0f, 1f, 1f));
        Set(movement, "platformLayer", (LayerMask)LayerMask.GetMask("Ground"));
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Dynamic;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        Collider2D playerCollider = player.GetComponent<Collider2D>();
        try
        {
            foreach (var surface in new[] { (name: "One-way corner", oneWay: true, corner: true),
                         (name: "Solid wall", oneWay: false, corner: false),
                         (name: "One-way top", oneWay: true, corner: false) })
            {
                player.dash.CancelDash();
                Set(player.dash, "nextDashTime", 0f);
                player.motor.Stop();
                player.motor.SetPosition(new Vector2(1000f, 1000f));
                Physics2D.SyncTransforms();
                Bounds bounds = playerCollider.bounds;
                var obstacle = new GameObject(surface.name);
                obstacle.layer = LayerMask.NameToLayer("Ground");
                var collider = obstacle.AddComponent<BoxCollider2D>();
                if (surface.oneWay && !surface.corner)
                {
                    collider.size = new Vector2(6f, 0.2f);
                    obstacle.transform.position = new Vector2(bounds.center.x + 1f,
                        bounds.min.y - 0.1f + Physics2D.defaultContactOffset * 0.5f);
                }
                else
                {
                    collider.size = new Vector2(0.4f, surface.oneWay ? 0.2f : 2f);
                    obstacle.transform.position = new Vector2(bounds.max.x + 0.55f,
                        surface.oneWay ? bounds.min.y + bounds.extents.x * 0.8f - 0.1f : bounds.center.y);
                }
                if (surface.oneWay)
                {
                    var effector = obstacle.AddComponent<PlatformEffector2D>();
                    effector.useOneWay = true;
                    effector.surfaceArc = 175f;
                    collider.usedByEffector = true;
                }
                Physics2D.SyncTransforms();
                Assert.That(player.dash.TryDash(), Is.True, surface.name);
                player.dash.BeginDashMovement(1f);
                float startX = player.transform.position.x;
                bool ignoredDuringDash = false;
                for (int step = 0; step < 32 && Get<bool>(player.dash, "isMoving"); step++)
                {
                    Invoke(movement, "FixedUpdate");
                    Invoke(player.dash, "FixedUpdate");
                    Assert.That(Physics2D.Simulate(Time.fixedDeltaTime), Is.True);
                    ignoredDuringDash |= Physics2D.GetIgnoreCollision(playerCollider, collider);
                }
                Assert.That(Get<bool>(player.dash, "isMoving"), Is.EqualTo(surface.oneWay), surface.name);
                Assert.That(ignoredDuringDash, Is.EqualTo(surface.corner), surface.name);
                if (surface.oneWay)
                    Assert.That(player.transform.position.x - startX, Is.GreaterThan(1f), surface.name);
                Assert.That(Physics2D.GetIgnoreCollision(playerCollider, collider), Is.False,
                    "The platform must collide again once the player has cleared its actual shape.");
                obstacle.SetActive(false);
                Object.Destroy(obstacle);
            }
        }
        finally { Physics2D.simulationMode = previousMode; }
        yield break;
    }

    [UnityTest]
    public IEnumerator HitStopPreservesOnlyLatestInputAndNormalPauseClearsIt()
    {
        HitStopManager.Request(player, 1f);
        player.RequestAction(PlayerAction.Jump);
        player.RequestAction(PlayerAction.Dash);
        float frozenUntil = Time.realtimeSinceStartup + 0.15f;
        while (Time.realtimeSinceStartup < frozenUntil) yield return null;
        Assert.That(HitStopManager.IsActive, Is.True);
        Assert.That(player.dash.IsDashing, Is.False);
        Assert.That(player.motor.Velocity, Is.EqualTo(Vector2.zero));

        HitStopManager.Cancel(player);
        Invoke(player, "LateUpdate");
        Assert.That(player.dash.IsDashing, Is.True, "Hit stop must not expire the latest buffered input.");
        Assert.That(player.motor.Velocity.y, Is.Zero, "The replaced jump must not execute.");
        player.dash.CancelDash();
        Set(movement, "isGrounded", true);

        HitStopManager.Request(player, 1f);
        player.RequestAction(PlayerAction.Jump);
        HitStopManager.PrepareForPause();
        Assert.That(GameplayPause.TryAcquire(player, out GameplayPause.Handle pause), Is.True);
        pause.PausePlayer(player);
        Invoke(player, "LateUpdate");
        pause.Dispose();
        Invoke(player, "LateUpdate");
        Assert.That(player.motor.Velocity.y, Is.Zero);
    }

    [UnityTest]
    public IEnumerator ComboAcceptsFrozenInputAndCompletionImmediatelyAllowsNextAction()
    {
        CreatePlatform();
        PlayerAttack attack = player.GetComponent<PlayerAttack>();
        Assert.That(attack.TryAttack(), Is.True);
        attack.StartGroundComboInput();
        HitStopManager.Request(player, 1f);
        player.RequestAction(PlayerAction.Attack);
        PlayerGroundAttack combo = Get<PlayerGroundAttack>(attack, "groundAttack");
        Assert.That(Get<int>(combo, "attackNumber"), Is.EqualTo(1));
        Assert.That(Get<bool>(combo, "isSecondAttackQueued"), Is.True);
        float frozenUntil = Time.realtimeSinceStartup + 0.15f;
        while (Time.realtimeSinceStartup < frozenUntil) yield return null;
        HitStopManager.Cancel(player);
        attack.EndGroundComboInput();
        Assert.That(Get<int>(combo, "attackNumber"), Is.EqualTo(2));

        attack.FinishGroundAttack(2);
        Assert.That(player.CanMove, Is.True);
        Assert.That(player.CanStartAction, Is.True, "Attack completion must immediately allow the next action.");
        Set(movement, "moveInput", Vector2.right);
        Invoke(movement, "FixedUpdate");
        Assert.That(player.motor.Velocity.x, Is.GreaterThan(0f));
        Assert.That(attack.TryAttack(), Is.True, "A completed attack must not add a separate cooldown.");
    }

    [UnityTest]
    public IEnumerator EarlyComboInputWaitsForItsWindowAndOldInputExpires()
    {
        CreatePlatform();
        PlayerAttack attack = player.GetComponent<PlayerAttack>();
        Assert.That(attack.TryAttack(), Is.True);
        player.RequestAction(PlayerAction.Attack);
        PlayerGroundAttack combo = Get<PlayerGroundAttack>(attack, "groundAttack");
        Assert.That(Get<bool>(combo, "isSecondAttackQueued"), Is.False);
        attack.StartGroundComboInput();
        Invoke(player, "LateUpdate");
        attack.EndGroundComboInput();
        Assert.That(Get<int>(combo, "attackNumber"), Is.EqualTo(2));

        player.CancelCombatActions();
        player.anim.speed = 0f;
        Assert.That(attack.TryAttack(), Is.True);
        player.RequestAction(PlayerAction.Dash);
        float unlockAt = Time.time + player.Data.ActionBufferDuration + 0.05f;
        while (Time.time < unlockAt) yield return null;
        attack.FinishGroundAttack(1);
        Invoke(player, "LateUpdate");
        Assert.That(player.dash.IsDashing, Is.False, "Expired inputs must not execute after a long lock.");
    }

    [UnityTest]
    public IEnumerator AllActionInputsBufferUntilAttackCompletion()
    {
        CreatePlatform();
        player.anim.speed = 0f;
        PlayerAttack attack = player.GetComponent<PlayerAttack>();
        PlayerActionController actions = Get<PlayerActionController>(player, "actions");
        Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
        try
        {
            foreach (PlayerAction action in new[] { PlayerAction.Jump, PlayerAction.Dash,
                         PlayerAction.Attack, PlayerAction.GroundSlam, PlayerAction.Counter })
            {
                player.CancelCombatActions();
                player.dash.CancelDash();
                player.motor.Stop();
                Invoke(movement, "Update");
                Assert.That(attack.TryAttack(), Is.True, action.ToString());
                attack.StartGroundComboInput();
                Assert.That(attack.TryAttack(), Is.True);
                attack.EndGroundComboInput();
                Assert.That(attack.IsExecuting, Is.True);
                Assert.That(player.CanStartAction, Is.False);

                InputAction input = action switch
                {
                    PlayerAction.Jump => player.input.Player.Jump,
                    PlayerAction.Dash => player.input.Player.Dash,
                    PlayerAction.Attack => player.input.Player.Attack,
                    PlayerAction.GroundSlam => player.input.Player.GroundSlam,
                    PlayerAction.Counter => player.input.Player.Counter,
                    _ => throw new System.ArgumentOutOfRangeException()
                };
                PressKey(keyboard, input);
                Assert.That(Get<PlayerAction>(actions, "bufferedAction"), Is.EqualTo(action),
                    $"{action} input must wait while the current attack is executing (enabled={input.enabled}, attack={attack.IsExecuting}, grounded={movement.IsGrounded}).");
                Invoke(player, "LateUpdate");
                Assert.That(Get<PlayerAction>(actions, "bufferedAction"), Is.EqualTo(action),
                    $"{action} must remain buffered until the current attack finishes.");
                attack.FinishGroundAttack(2);
                Assert.That(player.CanStartAction, Is.True);
                Invoke(player, "LateUpdate");

                switch (action)
                {
                    case PlayerAction.Jump:
                        Assert.That(player.motor.Velocity.y, Is.EqualTo(player.Data.JumpForce));
                        break;
                    case PlayerAction.Dash:
                        Assert.That(player.dash.IsDashing, Is.True);
                        break;
                    case PlayerAction.Attack:
                        Assert.That(attack.IsExecuting, Is.True);
                        break;
                    case PlayerAction.GroundSlam:
                        Assert.That(player.GetComponent<PlayerGroundSlam>().IsExecuting, Is.True);
                        break;
                    case PlayerAction.Counter:
                        Assert.That(player.GetComponent<PlayerCounter>().IsExecuting, Is.True);
                        break;
                }
                Assert.That(Get<PlayerAction>(actions, "bufferedAction"), Is.EqualTo(PlayerAction.None),
                    $"{action} input must be consumed once the attack finishes.");
            }
        }
        finally
        {
            player.CancelCombatActions();
            player.dash.CancelDash();
            InputSystem.RemoveDevice(keyboard);
        }
        yield break;
    }

    [UnityTest]
    public IEnumerator PendingDashTakesPriorityOverAttackJumpAndSkillInputs()
    {
        CreatePlatform();
        player.anim.speed = 0f;
        PlayerAttack attack = player.GetComponent<PlayerAttack>();
        PlayerActionController actions = Get<PlayerActionController>(player, "actions");
        Assert.That(attack.TryAttack(), Is.True);
        attack.StartGroundComboInput();
        Assert.That(attack.TryAttack(), Is.True);
        PlayerGroundAttack combo = Get<PlayerGroundAttack>(attack, "groundAttack");
        Assert.That(Get<bool>(combo, "isSecondAttackQueued"), Is.True);

        Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
        try
        {
            PressKey(keyboard, player.input.Player.Counter);
            Assert.That(Get<PlayerAction>(actions, "bufferedAction"), Is.EqualTo(PlayerAction.Counter));
            PressKey(keyboard, player.input.Player.Dash);
            float expiresAt = Get<float>(actions, "bufferedUntil");
            foreach (InputAction input in new[] { player.input.Player.Attack, player.input.Player.Jump,
                         player.input.Player.GroundSlam, player.input.Player.Counter })
            {
                PressKey(keyboard, input);
                Assert.That(Get<PlayerAction>(actions, "bufferedAction"), Is.EqualTo(PlayerAction.Dash), input.name);
                Assert.That(Get<float>(actions, "bufferedUntil"), Is.EqualTo(expiresAt),
                    "Other inputs must not extend the pending dash's lifetime.");
            }

            Assert.That(player.dash.IsDashing, Is.False);
            attack.OpenDashCancelWindow();
            Invoke(player, "LateUpdate");
            Assert.That(player.dash.IsDashing, Is.True);
            Assert.That(attack.IsExecuting, Is.False);
            Assert.That(attack.CanDashCancel, Is.False);
            Assert.That(Get<bool>(combo, "isSecondAttackQueued"), Is.False,
                "Dash cancellation must discard an already queued combo attack.");
            Assert.That(player.GetComponent<PlayerGroundSlam>().IsExecuting, Is.False);
            Assert.That(player.GetComponent<PlayerCounter>().IsExecuting, Is.False);
            Assert.That(player.motor.Velocity.y, Is.Zero);
        }
        finally { InputSystem.RemoveDevice(keyboard); }
        yield break;
    }

    [UnityTest]
    public IEnumerator PendingCounterTakesPriorityOverAttackJumpAndGroundSlamInputs()
    {
        CreatePlatform();
        player.anim.speed = 0f;
        PlayerAttack attack = player.GetComponent<PlayerAttack>();
        PlayerCounter counter = player.GetComponent<PlayerCounter>();
        PlayerActionController actions = Get<PlayerActionController>(player, "actions");
        Assert.That(attack.TryAttack(), Is.True);
        attack.StartGroundComboInput();
        Assert.That(attack.TryAttack(), Is.True);
        PlayerGroundAttack combo = Get<PlayerGroundAttack>(attack, "groundAttack");
        Assert.That(Get<bool>(combo, "isSecondAttackQueued"), Is.True);

        Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
        try
        {
            PressKey(keyboard, player.input.Player.Counter);
            float expiresAt = Get<float>(actions, "bufferedUntil");
            foreach (InputAction input in new[] { player.input.Player.Attack, player.input.Player.Jump,
                         player.input.Player.GroundSlam })
            {
                PressKey(keyboard, input);
                Assert.That(Get<PlayerAction>(actions, "bufferedAction"), Is.EqualTo(PlayerAction.Counter), input.name);
                Assert.That(Get<float>(actions, "bufferedUntil"), Is.EqualTo(expiresAt),
                    "Other inputs must not extend the pending counter's lifetime.");
            }

            Assert.That(counter.IsExecuting, Is.False, "Attack startup must not be cancellable.");
            attack.OpenDashCancelWindow();
            Invoke(player, "LateUpdate");
            Assert.That(counter.IsExecuting, Is.True);
            Assert.That(counter.TryUse(), Is.False, "An executing counter must not restart itself.");
            Assert.That(attack.IsExecuting, Is.False);
            Assert.That(Get<bool>(combo, "isSecondAttackQueued"), Is.False);
            Assert.That(Get<PlayerAction>(actions, "bufferedAction"), Is.EqualTo(PlayerAction.None));
            Assert.That(player.dash.IsDashing, Is.False);
            Assert.That(player.GetComponent<PlayerGroundSlam>().IsExecuting, Is.False);

            player.anim.speed = 1f;
            player.anim.Update(0f);
            player.anim.Update(0.01f);
            AnimationClip clip = player.anim.GetCurrentAnimatorClipInfo(0)[0].clip;
            AnimationEvent window = System.Array.Find(clip.events,
                e => e.functionName == nameof(PlayerCounterAnimationEvents.BeginCounterWindow));
            Assert.That(window, Is.Not.Null, "The Animator must actually enter counter preparation.");
            float elapsed = player.anim.GetCurrentAnimatorStateInfo(0).normalizedTime * clip.length;
            player.anim.Update(Mathf.Max(0f, window.time - elapsed) + 0.001f);
            Assert.That(counter.IsInvincible, Is.True, "The authored counter window must still open.");
        }
        finally { InputSystem.RemoveDevice(keyboard); }
        yield break;
    }

    [UnityTest]
    public IEnumerator ExpiredCounterDoesNotKeepPriorityOverNewAttackInput()
    {
        CreatePlatform();
        player.anim.speed = 0f;
        PlayerAttack attack = player.GetComponent<PlayerAttack>();
        PlayerActionController actions = Get<PlayerActionController>(player, "actions");
        Assert.That(attack.TryAttack(), Is.True);
        player.RequestAction(PlayerAction.Counter);
        float expiresAt = Time.time + player.Data.ActionBufferDuration + 0.02f;
        while (Time.time < expiresAt) yield return null;
        attack.StartGroundComboInput();
        player.RequestAction(PlayerAction.Attack);
        Assert.That(Get<bool>(Get<PlayerGroundAttack>(attack, "groundAttack"), "isSecondAttackQueued"), Is.True);
        Assert.That(Get<PlayerAction>(actions, "bufferedAction"), Is.EqualTo(PlayerAction.None));
        Assert.That(player.GetComponent<PlayerCounter>().IsExecuting, Is.False);
    }

    [UnityTest]
    public IEnumerator GroundSlamCanStartAndCompleteWhileAirborne()
    {
        var slam = player.GetComponent<PlayerGroundSlam>();
        Set(slam, "cooldown", 0f);
        Set(movement, "isGrounded", false);
        player.anim.SetBool("isGrounded", false);

        foreach (float verticalSpeed in new[] { 3f, -3f })
        {
            player.motor.SetVelocityY(verticalSpeed);
            player.RequestAction(PlayerAction.GroundSlam);
            Assert.That(slam.IsExecuting, Is.True);
            Assert.That(player.motor.Velocity.y, Is.EqualTo(verticalSpeed).Within(0.001f));

            bool damageFrameReached = false;
            for (int step = 0; step < 100 && slam.IsExecuting; step++)
            {
                player.anim.Update(0.05f);
                damageFrameReached |= Get<bool>(slam, "damageApplied");
            }
            Assert.That(damageFrameReached, Is.True, "The airborne animation must reach its damage event.");
            Assert.That(slam.IsExecuting, Is.False, "The airborne animation must release the action.");
            Assert.That(player.CanStartAction, Is.True);
        }
        yield break;
    }

    [UnityTest]
    public IEnumerator CounterRecoveryCancelRespectsGroundingCooldownAndActiveSkill()
    {
        CreatePlatform();
        player.anim.speed = 0f;
        PlayerAttack attack = player.GetComponent<PlayerAttack>();
        PlayerCounter counter = player.GetComponent<PlayerCounter>();
        PlayerGroundSlam groundSlam = player.GetComponent<PlayerGroundSlam>();
        Set(movement, "isGrounded", false);
        Assert.That(attack.TryAttack(), Is.True);
        attack.OpenDashCancelWindow();
        Assert.That(counter.TryUse(), Is.False);
        Assert.That(attack.IsExecuting, Is.True, "An airborne counter request must not cancel the attack.");

        player.CancelCombatActions();
        Set(movement, "isGrounded", true);
        Assert.That(groundSlam.TryUse(), Is.True);
        Assert.That(counter.TryUse(), Is.False);
        Assert.That(groundSlam.IsExecuting, Is.True);
        groundSlam.Cancel();

        Assert.That(counter.TryUse(), Is.True);
        counter.Cancel();
        Assert.That(counter.CooldownRemaining, Is.GreaterThan(0f));
        Assert.That(attack.TryAttack(), Is.True);
        attack.OpenDashCancelWindow();
        Assert.That(counter.TryUse(), Is.False);
        Assert.That(attack.IsExecuting, Is.True, "A counter on cooldown must not cancel the attack.");
        Assert.That(player.dash.TryDash(), Is.True, "Counter cooldown must not prevent a valid dash cancel.");
        yield break;
    }

    [UnityTest]
    public IEnumerator ExpiredDashDoesNotKeepPriorityOverNewAttackInput()
    {
        CreatePlatform();
        player.anim.speed = 0f;
        PlayerAttack attack = player.GetComponent<PlayerAttack>();
        PlayerActionController actions = Get<PlayerActionController>(player, "actions");
        Assert.That(attack.TryAttack(), Is.True);
        player.RequestAction(PlayerAction.Dash);
        float expiresAt = Time.time + player.Data.ActionBufferDuration + 0.02f;
        while (Time.time < expiresAt) yield return null;
        Invoke(player, "LateUpdate");
        Assert.That(player.dash.IsDashing, Is.False);
        attack.StartGroundComboInput();
        player.RequestAction(PlayerAction.Attack);
        Assert.That(Get<bool>(Get<PlayerGroundAttack>(attack, "groundAttack"), "isSecondAttackQueued"), Is.True);
        Assert.That(Get<PlayerAction>(actions, "bufferedAction"), Is.EqualTo(PlayerAction.None));
    }

    [UnityTest]
    public IEnumerator GroundAndAirRecoveryDashCancelUsesClipEventsAtEveryWeaponStage()
    {
        CreatePlatform();
        PlayerAttack attack = player.GetComponent<PlayerAttack>();
        PlayerWeaponController weapon = player.GetComponent<PlayerWeaponController>();
        player.anim.speed = 1f;
        for (int stage = 0; stage < weapon.CurrentWeapon.WeaponStages.Length; stage++)
        {
            for (int kind = 0; kind < 3; kind++)
            {
                player.CancelCombatActions();
                player.dash.CancelDash();
                Set(player.dash, "nextDashTime", 0f);
                player.motor.Stop();
                weapon.SetWeaponStage(stage);
                player.anim.Rebind();
                bool grounded = kind != 2;
                Set(movement, "isGrounded", grounded);
                attack.ResetAirAttackUsage();
                player.anim.SetBool("isGrounded", grounded);
                player.anim.SetFloat("yVelocity", grounded ? 0f : 1f);
                player.anim.Update(0f);
                Assert.That(attack.TryAttack(), Is.True, $"stage={stage}, attackKind={kind}");
                player.anim.Update(0f);
                player.anim.Update(0.01f);
                if (kind == 1)
                {
                    attack.StartGroundComboInput();
                    Assert.That(attack.TryAttack(), Is.True);
                    attack.EndGroundComboInput();
                    float length = player.anim.GetCurrentAnimatorClipInfo(0)[0].clip.length;
                    float elapsed = player.anim.GetCurrentAnimatorStateInfo(0).normalizedTime * length;
                    player.anim.Update(length - elapsed + 0.001f);
                    player.anim.Update(0f);
                    player.anim.Update(0.001f);
                }

                AnimationClip clip = player.anim.GetCurrentAnimatorClipInfo(0)[0].clip;
                AnimationEvent cancel = System.Array.Find(clip.events,
                    e => e.functionName == nameof(PlayerAttackAnimationEvents.OpenDashCancelWindow));
                Assert.That(cancel, Is.Not.Null, clip.name);
                float currentTime = player.anim.GetCurrentAnimatorStateInfo(0).normalizedTime * clip.length;
                Assert.That(cancel.time - currentTime, Is.GreaterThan(0.01f), clip.name);
                player.anim.Update(cancel.time - currentTime - 0.01f);
                Assert.That(attack.CanDashCancel, Is.False, clip.name);
                Assert.That(player.dash.TryDash(), Is.False, "Startup and contact frames must not be cancellable.");
                player.RequestAction(PlayerAction.Dash);
                player.RequestAction(PlayerAction.Attack);
                player.anim.Update(0.02f);
                Assert.That(attack.CanDashCancel, Is.True, clip.name);
                Invoke(player, "LateUpdate");
                Assert.That(player.dash.IsDashing, Is.True, clip.name);
                Assert.That(attack.IsExecuting, Is.False, clip.name);
                player.anim.Update(0f);
                player.anim.Update(0.01f);
                Assert.That(Get<bool>(player.dash, "isMoving"), Is.True,
                    "The Animator must actually enter dash after cancelling the attack.");
                Assert.That(attack.CanDashCancel, Is.False);
            }
        }
        yield break;
    }

    private static void PressKey(Keyboard keyboard, InputAction action)
    {
        KeyControl key = null;
        foreach (var control in action.controls)
        {
            if (control.device != keyboard || control is not KeyControl candidate) continue;
            key = candidate;
            break;
        }
        Assert.That(key, Is.Not.Null, $"{action.name} must have a keyboard binding.");
        InputSettings settings = InputSystem.settings;
        var background = settings.backgroundBehavior;
        var editorInput = settings.editorInputBehaviorInPlayMode;
        settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        try
        {
            InputSystem.Update();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key.keyCode));
            InputSystem.Update();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
        }
        finally
        {
            settings.backgroundBehavior = background;
            settings.editorInputBehaviorInPlayMode = editorInput;
        }
    }

    private BoxCollider2D CreatePlatform()
    {
        Bounds bounds = player.GetComponent<Collider2D>().bounds;
        var platform = new GameObject("Control feel platform");
        platform.layer = LayerMask.NameToLayer("Ground");
        platform.transform.position = new Vector3(bounds.center.x, bounds.min.y - 0.05f);
        var collider = platform.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(3f, 0.1f);
        platform.AddComponent<PlatformEffector2D>().useOneWay = true;
        collider.usedByEffector = true;
        Set(movement, "groundLayer", (LayerMask)LayerMask.GetMask("Ground"));
        Set(movement, "platformLayer", (LayerMask)LayerMask.GetMask("Ground"));
        Physics2D.SyncTransforms();
        Invoke(movement, "Update");
        Assert.That(movement.IsGrounded, Is.True);
        return collider;
    }

    [UnityTest]
    public IEnumerator CoyoteJumpWorksOnceAndExpiresAfterLeavingSupport()
    {
        CreatePlatform();
        PlayerAttack attack = player.GetComponent<PlayerAttack>();
        Assert.That(attack.TryAttack(), Is.True);
        Invoke(movement, "Update");
        Vector3 groundedPosition = player.transform.position;
        player.motor.SetPosition(groundedPosition + Vector3.right * 10f);
        Physics2D.SyncTransforms();
        Invoke(movement, "Update");
        Assert.That(movement.IsGrounded, Is.False);
        attack.FinishGroundAttack(1);
        Assert.That(movement.TryJump(), Is.True);
        Assert.That(movement.TryJump(), Is.False);

        player.motor.Stop();
        player.motor.SetPosition(groundedPosition);
        Physics2D.SyncTransforms();
        Invoke(movement, "Update");
        player.motor.SetPosition(groundedPosition + Vector3.right * 10f);
        Physics2D.SyncTransforms();
        Invoke(movement, "Update");
        float expiredAt = Time.time + player.Data.CoyoteTime + 0.05f;
        while (Time.time < expiredAt) yield return null;
        Assert.That(movement.IsGrounded, Is.False);
        Assert.That(movement.TryJump(), Is.False);

        player.RequestAction(PlayerAction.Jump);
        Assert.That(player.motor.Velocity.y, Is.Zero);
        player.motor.SetPosition(groundedPosition);
        Physics2D.SyncTransforms();
        Invoke(movement, "Update");
        Invoke(player, "LateUpdate");
        Assert.That(player.motor.Velocity.y, Is.EqualTo(player.Data.JumpForce), "Landing consumes a buffered jump.");
    }

    [UnityTest]
    public IEnumerator PlatformDropAcrossAdjacentBlocksClearsGroundAndJumpGrace()
    {
        Bounds initialBounds = player.GetComponent<Collider2D>().bounds;
        BoxCollider2D left = CreatePlatform();
        BoxCollider2D right = CreatePlatform();
        left.name = "Left one-way block";
        right.name = "Right one-way block";
        left.size = right.size = new Vector2(1f, 1f);
        left.transform.position = new Vector2(initialBounds.center.x - 0.5f, initialBounds.min.y - 0.5f);
        right.transform.position = new Vector2(initialBounds.center.x + 0.5f, initialBounds.min.y - 0.5f);
        Physics2D.SyncTransforms();
        Invoke(movement, "Update");
        Assert.That(movement.IsGrounded, Is.True);
        Invoke(movement, "CheckForPlatformDrop");
        Assert.That(movement.IsGrounded, Is.False);
        Invoke(movement, "Update");
        Assert.That(movement.IsGrounded, Is.False, "Neither adjacent block may restore ground after dropping through their shared edge.");
        Assert.That(Physics2D.GetIgnoreCollision(player.GetComponent<Collider2D>(), left), Is.True);
        Assert.That(Physics2D.GetIgnoreCollision(player.GetComponent<Collider2D>(), right), Is.True);
        Assert.That(movement.TryJump(), Is.False, "A second ground query must not restore coyote time during the drop.");
        player.RequestAction(PlayerAction.Jump);
        Invoke(player, "LateUpdate");
        Assert.That(player.motor.Velocity.y, Is.LessThanOrEqualTo(0f));
        player.motor.SetPosition((Vector2)player.transform.position + Vector2.down * 0.15f);
        Physics2D.SyncTransforms();
        Invoke(movement, "FixedUpdate");
        Invoke(movement, "Update");
        Assert.That(movement.IsGrounded, Is.False);
        Assert.That(Physics2D.GetIgnoreCollision(player.GetComponent<Collider2D>(), left), Is.True);
        Assert.That(Physics2D.GetIgnoreCollision(player.GetComponent<Collider2D>(), right), Is.True);
        player.motor.SetPosition((Vector2)player.transform.position + Vector2.down * 2f);
        Physics2D.SyncTransforms();
        Invoke(movement, "FixedUpdate");
        Assert.That(Physics2D.GetIgnoreCollision(player.GetComponent<Collider2D>(), left), Is.False);
        Assert.That(Physics2D.GetIgnoreCollision(player.GetComponent<Collider2D>(), right), Is.False);
        yield break;
    }

    [UnityTest]
    public IEnumerator PlatformDropDoesNotRestoreJumpInsideThePlatform()
    {
        BoxCollider2D source = CreatePlatform();
        Bounds initialBounds = player.GetComponent<Collider2D>().bounds;
        source.gameObject.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
        CompositeCollider2D platform = source.gameObject.AddComponent<CompositeCollider2D>();
        platform.geometryType = CompositeCollider2D.GeometryType.Outlines;
        platform.usedByEffector = true;
        source.compositeOperation = Collider2D.CompositeOperation.Merge;
        Physics2D.SyncTransforms();
        Invoke(movement, "Update");
        Assert.That(movement.IsGrounded, Is.True);
        Vector2 startingPosition = player.transform.position;
        Invoke(movement, "CheckForPlatformDrop");
        Assert.That(movement.IsGrounded, Is.False);
        Assert.That(movement.TryJump(), Is.False, "Dropping must clear jump grace immediately.");

        // The player's center has passed the top edge, but the outline still intersects their body.
        float dropDistance = initialBounds.extents.y + 0.035f;
        player.motor.SetPosition(startingPosition + Vector2.down * dropDistance);
        Physics2D.SyncTransforms();
        Collider2D playerCollider = player.GetComponent<Collider2D>();
        Assert.That(playerCollider.Distance(platform).isOverlapped, Is.True);
        Invoke(movement, "FixedUpdate");
        Assert.That(Physics2D.GetIgnoreCollision(playerCollider, platform), Is.True,
            "A composite platform must stay ignored while the player still intersects its outline.");
        Invoke(movement, "Update");
        Assert.That(movement.IsGrounded, Is.False, "A cast starting inside the passed platform must not count its underside as ground.");
        player.RequestAction(PlayerAction.Jump);
        Invoke(player, "LateUpdate");
        Assert.That(player.motor.Velocity.y, Is.LessThanOrEqualTo(0f), "Jump input must not launch the player back into the platform.");

        // A real lower floor must still restore jumping after the drop.
        player.ClearBufferedAction(PlayerAction.Jump);
        player.motor.SetPosition(startingPosition + Vector2.down * 2f);
        Physics2D.SyncTransforms();
        Invoke(movement, "FixedUpdate");
        CreatePlatform();
        Assert.That(movement.IsGrounded, Is.True);
        Assert.That(movement.TryJump(), Is.True, "Landing on the next platform must allow a normal jump.");
        yield break;
    }

    [UnityTest]
    public IEnumerator PlatformDropGravityFieldAndKnockbackInvalidateJumpGraceAndBufferedInput()
    {
        CreatePlatform();
        Invoke(movement, "CheckForPlatformDrop");
        Assert.That(movement.IsGrounded, Is.False);
        Assert.That(movement.TryJump(), Is.False);

        Set(movement, "coyoteUntil", Time.time + 1f);
        movement.SetGravityField(player, 0.5f, true);
        player.RequestAction(PlayerAction.Jump);
        movement.SetGravityField(player, 0.5f, false);
        Assert.That(movement.TryJump(), Is.False);

        HitStopManager.Request(player, 1f);
        player.RequestAction(PlayerAction.Jump);
        movement.ApplyKnockback(Vector2.zero, 0.1f);
        HitStopManager.Cancel(player);
        movement.ClearKnockback(movement.KnockbackVersion);
        Set(movement, "isGrounded", true);
        Invoke(player, "LateUpdate");
        Assert.That(player.motor.Velocity.y, Is.Zero);
        yield break;
    }

    [UnityTearDown]
    public IEnumerator LeavePlayMode()
    {
        if (!Application.isPlaying) yield break;
        HitStopManager.PrepareForPause();
        Time.timeScale = 1f;
        if (player != null) Object.Destroy(player.Data);
        yield return new ExitPlayMode();
    }
}
