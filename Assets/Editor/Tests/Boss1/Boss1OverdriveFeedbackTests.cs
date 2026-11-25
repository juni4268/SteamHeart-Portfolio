using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

[Category("Boss1Feedback")]
public sealed class Boss1OverdriveFeedbackTests
{
    // Bind references after entering Play and loading the actual Boss1 scene.
    private static Boss1Controller controller;
    private static Boss1SlamPattern slam;
    private static Boss1OverdrivePattern overdrive;
    private static Boss1OverdriveField field;
    private static Boss1DebrisField debris;
    private static Player player;
    private static Rigidbody2D playerBody;
    private static float playerY;
    private static float centerX;

    private static T Get<T>(object target, string field) => (T)target.GetType()
        .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

    private static void Set(object target, string field, object value) => target.GetType()
        .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    private static IEnumerator Until(Func<bool> condition, string message, float timeout = 8f)
    {
        float deadline = Time.realtimeSinceStartup + timeout;
        while (!condition())
        {
            Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), message);
            yield return null;
        }
    }

    // Keep Edit Mode runner yields at the root; helpers contain only play-mode frames.
    private static IEnumerable<object> Frames(IEnumerator routine)
    {
        var stack = new Stack<IEnumerator>();
        stack.Push(routine);
        while (stack.Count > 0)
        {
            IEnumerator current = stack.Peek();
            if (!current.MoveNext())
            {
                stack.Pop();
                continue;
            }
            if (current.Current is IEnumerator nested) stack.Push(nested);
            else yield return current.Current;
        }
    }

    private static IEnumerator PrepareEncounter()
    {
        SceneManager.LoadScene("Boss1_Map");
        yield return null;
        yield return Until(() => UnityEngine.Object.FindFirstObjectByType<Player>()?.input != null,
            "Boss1 scene player initialization");
        yield return Until(() => !SceneTransitionOverlay.IsTransitioning, "Initial scene fade");
        controller = UnityEngine.Object.FindFirstObjectByType<Boss1Controller>();
        controller.GetComponent<Boss1PatternScheduler>().StopSchedulingAndCancelPattern();
        slam = controller.GetComponent<Boss1SlamPattern>();
        overdrive = controller.GetComponent<Boss1OverdrivePattern>();
        field = controller.GetComponent<Boss1OverdriveField>();
        debris = controller.GetComponent<Boss1DebrisField>();
        player = UnityEngine.Object.FindFirstObjectByType<Player>();
        playerBody = player.GetComponent<Rigidbody2D>();
        playerBody.constraints = RigidbodyConstraints2D.FreezeAll;
        player.input.Player.Disable();

        var slamConfig = UnityEngine.Object.Instantiate(Get<Boss1SlamConfig>(slam, "config"));
        Set(slamConfig, "preparationDuration", 0.05f);
        Set(slamConfig, "preparationWaitDuration", 0.05f);
        Set(slamConfig, "returnDuration", 0.05f);
        Set(slamConfig, "debrisWarningDuration", 0.05f);
        Set(slam, "config", slamConfig);
        var debrisConfig = UnityEngine.Object.Instantiate(Get<Boss1DebrisConfig>(debris, "config"));
        Set(debrisConfig, "fallSpeed", 80f);
        Set(debris, "config", debrisConfig);
        var config = UnityEngine.Object.Instantiate(Get<Boss1OverdriveConfig>(overdrive, "config"));
        Set(config, "placementDuration", 0.05f);
        Set(config, "attackWaitDuration", 0.05f);
        Set(config, "strikeSpeed", 40f);
        Set(config, "slamTrackingDuration", 0.6f);
        Set(config, "slamSpeed", 15f);
        // Keep the test's wall lock alive through the authored preparation animation.
        Set(config, "wallStunDuration", 3f);
        Set(config, "groggyDescentDuration", 0.1f);
        Set(config, "groggyDuration", 0.5f);
        Set(config, "recoveryDuration", 0.1f);
        Set(overdrive, "config", config);

        Assert.That(controller.TryStartPattern(slam), Is.True);
        yield return Until(() => !controller.IsExecutingPattern, "Actual Slam completion");
        Assert.That(debris.HasLandedDebris, Is.True);
        Physics2D.SyncTransforms();
        Collider2D playerCollider = player.GetComponent<Collider2D>();
        centerX = (field.LeftWall.bounds.max.x + field.RightWall.bounds.min.x) * 0.5f;
        playerY = debris.FloorCollider.bounds.max.y + player.transform.position.y - playerCollider.bounds.min.y + 0.01f;
        playerBody.position = new Vector2(centerX, playerY);
        Physics2D.SyncTransforms();
    }

    [UnityTest]
    public IEnumerator ActualOverdriveFlashesLockedStrikeTargetAndReleasesVisuals()
    {
        yield return new EnterPlayMode();
        foreach (object frame in Frames(CheckCompletedSequence())) yield return frame;
        yield return new ExitPlayMode();
    }

    private static IEnumerator CheckCompletedSequence()
    {
        yield return PrepareEncounter();
        Vector3 originalBodyPosition = field.BodyRoot.localPosition;
        int broken = 0;
        debris.DebrisBroken += _ => broken++;
        Assert.That(controller.TryStartPattern(overdrive), Is.True);
        Boss1OverdriveFeedback feedback = null;
        yield return Until(() => (feedback = UnityEngine.Object.FindFirstObjectByType<Boss1OverdriveFeedback>()) != null,
            "Execution-owned feedback");
        Boss1WarningVisual target = feedback.GetComponentInChildren<Boss1WarningVisual>();
        Boss1SlamPathWarning path = feedback.GetComponentInChildren<Boss1SlamPathWarning>();
        yield return Until(() => target.Renderer.enabled, "Slam target warning");
        Assert.That(path.Renderer.enabled, Is.True);
        Assert.That(path.Renderer.bounds.min.y, Is.EqualTo(debris.FloorCollider.bounds.max.y).Within(0.01f));
        Assert.That(broken, Is.GreaterThan(0), "Only actual flight impacts emit break events");
        Assert.That(Get<object>(feedback, "stun"), Is.Not.Null, "Wall hit displays its control lock");
        Boss1HandController slamHand = Get<Transform>(path, "hand").GetComponent<Boss1HandController>();
        Assert.That(target.Renderer.bounds.size.x,
            Is.EqualTo(slamHand.GetComponent<Boss1HandDamageHitbox>().AttackCollider.bounds.size.x).Within(0.01f));
        Assert.That(path.Renderer.bounds.center.x, Is.EqualTo(target.Renderer.bounds.center.x).Within(0.01f));
        Assert.That(path.Renderer.bounds.size.x, Is.EqualTo(target.Renderer.bounds.size.x).Within(0.01f));
        Assert.That(Get<bool>(path, "locked"), Is.True, "The strike warning is already locked when shown");
        Assert.That(Get<float>(target, "duration"), Is.Zero);
        float lockedX = target.Renderer.bounds.center.x;
        Bounds lockedPath = path.Renderer.bounds;
        playerBody.position += Vector2.right;
        yield return null;
        Assert.That(target.Renderer.bounds.center.x, Is.EqualTo(lockedX).Within(0.001f));
        Assert.That(path.Renderer.bounds, Is.EqualTo(lockedPath));
        yield return Until(() => field.BodyRoot.GetComponent<Boss1BodyAnimationController>().IsGroggy,
            "Body Animator entered groggy");
        Assert.That(field.BodyRoot.localPosition.y, Is.LessThan(originalBodyPosition.y));
        Assert.That(target.Renderer.enabled, Is.False);
        Assert.That(path.Renderer.enabled, Is.False);
        yield return Until(() => !controller.IsExecutingPattern, "Overdrive completion");
        yield return null;
        Assert.That(UnityEngine.Object.FindFirstObjectByType<Boss1OverdriveFeedback>(), Is.Null);
        Assert.That(controller.LastPatternError, Is.Null.Or.Empty);
        Assert.That(Vector3.Distance(field.BodyRoot.localPosition, originalBodyPosition), Is.LessThan(0.001f));
    }

    [UnityTest]
    public IEnumerator CancellingTargetClearsEffectsAndPreservesANewerControlLock()
    {
        yield return new EnterPlayMode();
        foreach (object frame in Frames(CheckCancellation())) yield return frame;
        yield return new ExitPlayMode();
    }

    private static IEnumerator CheckCancellation()
    {
        yield return PrepareEncounter();
        Assert.That(controller.TryStartPattern(overdrive), Is.True);
        Boss1OverdriveFeedback feedback = null;
        yield return Until(() => (feedback = UnityEngine.Object.FindFirstObjectByType<Boss1OverdriveFeedback>()) != null,
            "Execution-owned feedback");
        Boss1WarningVisual target = feedback.GetComponentInChildren<Boss1WarningVisual>();
        Boss1SlamPathWarning path = feedback.GetComponentInChildren<Boss1SlamPathWarning>();
        yield return Until(() => target.Renderer.enabled, "Target preparation");
        PlayerMovement movement = player.GetComponent<PlayerMovement>();
        Assert.That(Get<object>(feedback, "stun"), Is.Not.Null);
        movement.ApplyKnockback(Vector2.zero, 2f);
        int newerLock = movement.KnockbackVersion;
        yield return null;
        Assert.That(Get<PlayerStunEffect>(feedback, "stun") == null, Is.True,
            "The older stun marker does not label a newer hit");
        controller.CancelCurrentPattern();
        Assert.That(feedback.gameObject.activeSelf, Is.False);
        Assert.That(target.Renderer.enabled, Is.False);
        Assert.That(path.Renderer.enabled, Is.False);
        Assert.That(movement.KnockbackVersion, Is.EqualTo(newerLock));
        Assert.That(movement.IsKnockbackLocked, Is.True);
        yield return null;
        Assert.That(UnityEngine.Object.FindFirstObjectByType<Boss1OverdriveFeedback>(), Is.Null);
    }

    [UnityTest]
    public IEnumerator SlamWarnsBothHandPathsAndClearsThemAfterCompletionOrCancellation()
    {
        yield return new EnterPlayMode();
        foreach (object frame in Frames(CheckSlamPaths())) yield return frame;
        yield return new ExitPlayMode();
    }

    private static IEnumerator CheckSlamPaths()
    {
        yield return PrepareEncounter();
        var config = Get<Boss1SlamConfig>(slam, "config");
        Set(config, "preparationWaitDuration", 0.5f);
        Set(config, "slamDuration", 0.3f);
        var floorArea = Get<Boss1SlamWarningArea>(slam, "slamWarningArea");
        var sourceVisual = floorArea.GetComponent<Boss1WarningVisual>();
        Set(sourceVisual, "baseColor", new Color(0.1f, 0.4f, 0.9f, 0.08f));
        Set(sourceVisual, "flashColor", new Color(0.2f, 0.9f, 0.5f, 0.6f));
        Set(sourceVisual, "maximumFillAlpha", 0.07f);
        Set(sourceVisual, "finalFlashStart", 0.5f);
        Set(sourceVisual, "initialPulseFrequency", 2.5f);
        Set(sourceVisual, "finalPulseFrequency", 6f);
        Set(sourceVisual, "frameThickness", 0.02f);
        Assert.That(controller.TryStartPattern(slam), Is.True);
        Boss1SlamPathWarning[] paths = null;
        yield return Until(() => (paths = UnityEngine.Object.FindObjectsByType<Boss1SlamPathWarning>(
            FindObjectsSortMode.None)).Length == 2 && paths.All(p => p.Renderer.enabled), "Both Slam paths");
        var floorWarning = floorArea.GetComponent<SpriteRenderer>();
        Assert.That(floorWarning.enabled, Is.True, "The floor impact warning is preserved");
        foreach (var path in paths)
        {
            var visual = path.GetComponent<Boss1WarningVisual>();
            foreach (string setting in new[] { "baseColor", "flashColor", "maximumFillAlpha", "finalFlashStart",
                "initialPulseFrequency", "finalPulseFrequency", "frameThickness" })
                Assert.That(Get<object>(visual, setting), Is.EqualTo(Get<object>(sourceVisual, setting)), setting);
            Assert.That(visual.Style, Is.EqualTo(Boss1WarningVisual.WarningStyle.Falling));
            Assert.That(sourceVisual.Style, Is.EqualTo(Boss1WarningVisual.WarningStyle.Ground));
            Assert.That(path.Renderer.color, Is.EqualTo(floorWarning.color));
            Bounds hitbox = Get<Collider2D>(path, "hitbox").bounds;
            Assert.That(path.Renderer.bounds.size.x, Is.EqualTo(hitbox.size.x).Within(0.01f));
            Assert.That(path.Renderer.bounds.min.y, Is.EqualTo(debris.FloorCollider.bounds.max.y).Within(0.01f));
            Assert.That(path.Renderer.bounds.max.y, Is.EqualTo(hitbox.max.y).Within(0.01f));
        }
        Assert.That(Get<float>(sourceVisual, "duration"), Is.EqualTo(config.PreparationWaitDuration),
            "Generated paths must keep the configured preparation warning duration");
        // Slam paths follow their hands; only Overdrive's strike warning locks a target.
        Assert.That(paths.All(p => !Get<bool>(p, "locked")), Is.True);
        yield return Until(() => !controller.IsExecutingPattern, "Slam completion");
        yield return null;
        Assert.That(UnityEngine.Object.FindObjectsByType<Boss1SlamPathWarning>(FindObjectsSortMode.None), Is.Empty);
        Assert.That(floorWarning.enabled, Is.False);
        Assert.That(controller.TryStartPattern(slam), Is.True);
        yield return Until(() => (paths = UnityEngine.Object.FindObjectsByType<Boss1SlamPathWarning>(
            FindObjectsSortMode.None)).Length == 2 && paths.All(p => p.Renderer.enabled), "Repeated Slam paths");
        controller.CancelCurrentPattern();
        Assert.That(paths.All(p => !p.Renderer.enabled && !p.gameObject.activeSelf), Is.True);
        yield return null;
        Assert.That(UnityEngine.Object.FindObjectsByType<Boss1SlamPathWarning>(FindObjectsSortMode.None), Is.Empty);
        Assert.That(floorWarning.enabled, Is.False);
    }

    [UnityTest]
    public IEnumerator DisablingBossDuringGroggyRestoresBodyAndRemovesEffects()
    {
        yield return new EnterPlayMode();
        foreach (object frame in Frames(CheckDisabledBoss())) yield return frame;
        yield return new ExitPlayMode();
    }

    private static IEnumerator CheckDisabledBoss()
    {
        yield return PrepareEncounter();
        Vector3 originalPosition = field.BodyRoot.localPosition;
        Assert.That(controller.TryStartPattern(overdrive), Is.True);
        Boss1OverdriveFeedback feedback = null;
        yield return Until(() => (feedback = UnityEngine.Object.FindFirstObjectByType<Boss1OverdriveFeedback>()) != null,
            "Execution-owned feedback");
        yield return Until(() => field.BodyRoot.GetComponent<Boss1BodyAnimationController>().IsGroggy,
            "Body Animator entered groggy");
        Assert.That(field.BodyRoot.localPosition.y, Is.LessThan(originalPosition.y));
        controller.enabled = false;
        Assert.That(field.BodyRoot.localPosition, Is.EqualTo(originalPosition));
        Assert.That(feedback.gameObject.activeSelf, Is.False);
        yield return null;
        Assert.That(UnityEngine.Object.FindFirstObjectByType<Boss1OverdriveFeedback>(), Is.Null);
    }

    [UnityTest]
    public IEnumerator CrushPreparationMovesArtworkAndResetsOnCompletionAndCancellation()
    {
        yield return new EnterPlayMode();
        foreach (object frame in Frames(CheckCrushArtwork())) yield return frame;
        yield return new ExitPlayMode();
    }

    private static IEnumerator CheckCrushArtwork()
    {
        yield return PrepareEncounter();
        var crush = controller.GetComponent<Boss1CrushPattern>();
        var config = UnityEngine.Object.Instantiate(Get<Boss1CrushConfig>(crush, "config"));
        Set(config, "repeatCount", 1);
        Set(config, "placementDuration", 0.05f);
        Set(config, "trackingDuration", 0.05f);
        Set(config, "warningDuration", 0.05f);
        Set(config, "pullbackDistance", 0.2f);
        Set(config, "pullbackDuration", 0.15f);
        Set(config, "crushDuration", 0.05f);
        Set(config, "impactHoldDuration", 0.05f);
        Set(config, "returnDuration", 0.05f);
        Set(crush, "config", config);
        var hands = new[] { controller.LeftHand, controller.RightHand };
        var artwork = hands.Select(h => h.GetComponentInChildren<Animator>().transform).ToArray();
        Vector3[] idleRoots = hands.Select(h => h.transform.localPosition).ToArray();
        var idleCurves = artwork.Select(t => t.GetComponent<Animator>().runtimeAnimatorController.animationClips
            .SelectMany(c => UnityEditor.AnimationUtility.GetCurveBindings(c)
                .Where(b => b.type == typeof(Transform) && b.path == string.Empty && b.propertyName == "m_LocalPosition.y")
                .Select(b => new { clip = c, curve = UnityEditor.AnimationUtility.GetEditorCurve(c, b) }))
            .Where(c => c.clip.isLooping).ToArray()).ToArray();

        for (int attempt = 0; attempt < 2; attempt++)
        {
            Assert.That(controller.TryStartPattern(crush), Is.True);
            yield return Until(() => hands.All(h =>
                h.AnimationController.CurrentPhase == Boss1HandAnimationPhase.SecondPrepare), "Crush second preparation");
            Vector3[] placedRoots = hands.Select(h => h.transform.position).ToArray();
            yield return Until(() => artwork.All(t => t.localPosition.y < -0.1f), "Authored artwork descent");
            for (int i = 0; i < hands.Length; i++)
            {
                Assert.That(hands[i].transform.position, Is.EqualTo(placedRoots[i]),
                    "The gameplay root stays still while the child artwork animates");
                Assert.That(hands[i].IsBusy, Is.False);
                Assert.That(Get<bool>(hands[i].GetComponent<Boss1HandDamageHitbox>(), "attackActive"), Is.False);
            }

            if (attempt == 0)
            {
                var warning = Get<Boss1CrushWarningArea>(crush, "crushWarningArea").GetComponent<SpriteRenderer>();
                var hitboxes = crush.GetComponentsInChildren<Boss1HandDamageHitbox>(true);
                yield return Until(() => warning.enabled, "Crush warning before pullback");
                Vector3[] lockedRoots = hands.Select(h => h.transform.position).ToArray();
                yield return Until(() => !warning.enabled && hands.All(h => h.IsBusy), "Crush pullback");
                Assert.That(hands[0].transform.position.x, Is.LessThan(lockedRoots[0].x));
                Assert.That(hands[1].transform.position.x, Is.GreaterThan(lockedRoots[1].x));
                foreach (var hitbox in hitboxes)
                    Assert.That(Get<bool>(hitbox, "attackActive"), Is.False, "Pullback must not deal damage");

                var attackStarts = new Vector3[2];
                var attackStarted = new bool[2];
                Action<Vector2>[] observers = hands.Select((hand, i) => new Action<Vector2>(_ =>
                {
                    if (attackStarted[i] || !Get<bool>(hand.GetComponent<Boss1HandDamageHitbox>(), "attackActive")) return;
                    attackStarts[i] = hand.transform.position;
                    attackStarted[i] = true;
                })).ToArray();
                for (int i = 0; i < hands.Length; i++) hands[i].BeforeMove += observers[i];
                try { yield return Until(() => attackStarted.All(started => started), "Crush attack after pullback"); }
                finally
                {
                    for (int i = 0; i < hands.Length; i++) hands[i].BeforeMove -= observers[i];
                }
                Assert.That(attackStarts[0].x, Is.EqualTo(lockedRoots[0].x - config.PullbackDistance).Within(0.001f));
                Assert.That(attackStarts[1].x, Is.EqualTo(lockedRoots[1].x + config.PullbackDistance).Within(0.001f));
                for (int i = 0; i < hands.Length; i++)
                    Assert.That(attackStarts[i].y, Is.EqualTo(lockedRoots[i].y).Within(0.001f));

                yield return Until(() => !controller.IsExecutingPattern, "Crush completion");
                Assert.That(controller.LastPatternResult, Is.EqualTo(Boss1PatternResult.Completed));
            }
            else controller.CancelCurrentPattern();

            yield return null;
            for (int i = 0; i < hands.Length; i++)
            {
                Assert.That(Vector3.Distance(hands[i].transform.localPosition, idleRoots[i]), Is.LessThan(0.001f));
                var animator = artwork[i].GetComponent<Animator>();
                AnimationClip clip = animator.GetCurrentAnimatorClipInfo(0).Single().clip;
                var idle = idleCurves[i].Single(c => c.clip == clip);
                float time = Mathf.Repeat(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, 1f) * clip.length;
                Assert.That(artwork[i].localPosition.y, Is.EqualTo(idle.curve.Evaluate(time)).Within(0.001f),
                    "Artwork must resume its authored idle movement, not a previous frame's position");
                Assert.That(hands[i].AnimationController.CurrentPhase, Is.EqualTo(Boss1HandAnimationPhase.Idle));
            }
        }
    }

    [UnityTearDown]
    public IEnumerator RestoreEditor()
    {
        if (!Application.isPlaying) yield break;
        if (controller != null) controller.CancelCurrentPattern();
        HitStopManager.PrepareForPause();
        Time.timeScale = 1f;
        yield return new ExitPlayMode();
    }
}
