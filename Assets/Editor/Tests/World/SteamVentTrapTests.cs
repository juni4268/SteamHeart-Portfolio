using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

[Category("SteamVentTrap")]
public sealed class SteamVentTrapTests
{
    private static readonly Vector3 Origin = new Vector3(1000f, 1000f);
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    private static void Set(object target, string name, object value) =>
        target.GetType().GetField(name, PrivateInstance).SetValue(target, value);

    private static void Step(SteamVentTrap trap, int count = 1)
    {
        MethodInfo tick = typeof(SteamVentTrap).GetMethod("FixedUpdate", PrivateInstance);
        for (int i = 0; i < count; i++) tick.Invoke(trap, null);
    }

    [UnitySetUp]
    public IEnumerator SetUpScene()
    {
        yield return new EnterPlayMode();
        // Work on play-mode copies so the user's Editor scene is preserved.
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects()) root.SetActive(false);
        Time.timeScale = 1f;
    }

    [UnityTearDown]
    public IEnumerator RestoreEditor()
    {
        Time.timeScale = 1f;
        yield return new ExitPlayMode();
    }

    private static SteamVentTrap CreateTrap(float attackDuration = 10f)
    {
        var instance = new GameObject("Steam vent trap test");
        instance.SetActive(false);
        instance.transform.position = Origin;
        BoxCollider2D area = instance.AddComponent<BoxCollider2D>();
        area.size = Vector2.one * 4f;
        SteamVentTrap trap = instance.AddComponent<SteamVentTrap>();
        Set(trap, "attackCollider", area);
        Set(trap, "targetLayers", (LayerMask)LayerMask.GetMask("Player"));
        Set(trap, "attackDuration", attackDuration);
        Set(trap, "waitDuration", Time.fixedDeltaTime * 2f);
        instance.SetActive(true);
        return trap;
    }

    private static Player CreatePlayer(out BoxCollider2D first, out BoxCollider2D second)
    {
        GameObject instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab"));
        instance.transform.position = Origin + Vector3.right * 0.5f;
        instance.GetComponent<Rigidbody2D>().gravityScale = 0f;
        instance.GetComponent<PlayerMovement>().enabled = false;
        Player player = instance.GetComponent<Player>();
        player.input.Player.Disable();
        // Keep this contact test alive through several hits without changing PlayerData.
        Set(player.health, "currentHP", 10);
        foreach (Collider2D collider in instance.GetComponentsInChildren<Collider2D>()) collider.enabled = false;
        first = CreateCollider(instance.transform, -0.25f);
        second = CreateCollider(instance.transform, 0.25f);
        return player;
    }

    private static BoxCollider2D CreateCollider(Transform parent, float xOffset)
    {
        var child = new GameObject("Trap test player collider");
        child.layer = LayerMask.NameToLayer("Player");
        child.transform.SetParent(parent, false);
        child.transform.localPosition = Vector3.right * xOffset;
        BoxCollider2D collider = child.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one * 0.5f;
        return collider;
    }

    [Test]
    public void WarningMatchesAttackAreaWithoutDamageAndHidesOnAttackOrDisable()
    {
        Player player = CreatePlayer(out _, out _);
        var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/World/SteamVentTrap.prefab"));
        instance.transform.position = Origin;
        SteamVentTrap trap = instance.GetComponent<SteamVentTrap>();
        var data = new SerializedObject(trap);
        var warning = (Boss1CrushWarningArea)data.FindProperty("warningArea").objectReferenceValue;
        var renderer = warning.GetComponent<SpriteRenderer>();
        var area = (BoxCollider2D)data.FindProperty("attackCollider").objectReferenceValue;
        // This test isolates warning geometry; animated phase timing has its own coverage.
        trap.enabled = false;
        Set(trap, "attackAnimator", null);
        trap.enabled = true;
        area.size = new Vector2(2f, 1.6f);
        area.offset = new Vector2(0.2f, 0.1f);
        instance.transform.localScale = new Vector3(1.25f, 0.8f, 1f);
        instance.transform.rotation = Quaternion.Euler(0f, 0f, 23f);
        int initialHP = player.health.CurrentHP;

        Set(trap, "remainingTime", Time.fixedDeltaTime * 1.5f);
        Step(trap);
        Assert.That(trap.IsWarning, Is.True);
        Assert.That(renderer.enabled, Is.True);
        Assert.That(area.enabled, Is.False);
        Assert.That(player.health.CurrentHP, Is.EqualTo(initialHP));
        foreach (Vector2 corner in new[] { new Vector2(-1f, -1f), new Vector2(-1f, 1f),
                     new Vector2(1f, -1f), new Vector2(1f, 1f) })
        {
            Vector3 expected = area.transform.TransformPoint(area.offset + Vector2.Scale(area.size * 0.5f, corner));
            Bounds sprite = renderer.sprite.bounds;
            Vector3 actual = renderer.transform.TransformPoint(sprite.center +
                Vector3.Scale(sprite.extents, new Vector3(corner.x, corner.y, 0f)));
            Assert.That(Vector2.Distance(expected, actual), Is.LessThan(0.001f),
                "Warning corners must match the disabled attack collider, including offset, scale and rotation.");
        }

        Time.timeScale = 0f;
        Step(trap, 5);
        Assert.That(trap.IsWarning, Is.True, "Pausing must not advance the warning into an attack.");
        Time.timeScale = 1f;
        Step(trap);
        Assert.That(trap.IsAttacking, Is.True);
        Assert.That(trap.IsWarning, Is.False);
        Assert.That(renderer.enabled, Is.False);
        Assert.That(player.health.CurrentHP, Is.EqualTo(initialHP - 1));

        trap.enabled = false;
        trap.enabled = true;
        Set(trap, "remainingTime", Time.fixedDeltaTime * 1.5f);
        Step(trap);
        Assert.That(trap.IsWarning, Is.True);
        trap.enabled = false;
        Assert.That(trap.IsWarning, Is.False);
        Assert.That(renderer.enabled, Is.False);
        Assert.That(area.enabled, Is.False);
    }

    [Test]
    public void ContactDealsOneDamageAndOutwardKnockbackUntilEveryColliderLeaves()
    {
        Player player = CreatePlayer(out BoxCollider2D first, out _);
        SteamVentTrap trap = CreateTrap();
        int initialHP = player.health.CurrentHP;
        Step(trap, 2);
        Assert.That(player.health.CurrentHP, Is.EqualTo(initialHP - 1));
        Assert.That(player.motor.Velocity, Is.EqualTo(new Vector2(8f, 5f)));
        Assert.That(player.GetComponent<PlayerMovement>().IsKnockbackLocked, Is.True);

        Step(trap, 3);
        Assert.That(player.health.CurrentHP, Is.EqualTo(initialHP - 1), "Staying inside must not cause repeat damage.");

        first.transform.position = Origin + Vector3.right * 10f;
        Step(trap);
        first.transform.position = Origin + Vector3.right * 0.5f;
        Step(trap);
        Assert.That(player.health.CurrentHP, Is.EqualTo(initialHP - 1), "One collider leaving and returning is not a full exit.");

        player.transform.position = Origin + Vector3.right * 10f;
        Step(trap);
        player.transform.position = Origin + Vector3.left * 0.5f;
        Step(trap);
        Assert.That(player.health.CurrentHP, Is.EqualTo(initialHP - 2));
        Assert.That(player.motor.Velocity, Is.EqualTo(new Vector2(-8f, 5f)));
    }

    [Test]
    public void CycleStartsWaitingHitsExistingOverlapsAndResetsOnDisable()
    {
        Player player = CreatePlayer(out _, out _);
        SteamVentTrap trap = CreateTrap(Time.fixedDeltaTime * 2f);
        Collider2D area = trap.GetComponent<Collider2D>();
        int initialHP = player.health.CurrentHP;
        Assert.That(trap.IsAttacking, Is.False);
        Assert.That(area.enabled, Is.False);
        Assert.That(area.isTrigger, Is.True);
        Step(trap);
        Assert.That(player.health.CurrentHP, Is.EqualTo(initialHP));

        Step(trap);
        Assert.That(trap.IsAttacking, Is.True);
        Assert.That(area.enabled, Is.True);
        Assert.That(player.health.CurrentHP, Is.EqualTo(initialHP - 1));
        Step(trap, 2);
        Assert.That(trap.IsAttacking, Is.False);
        Assert.That(area.enabled, Is.False);
        Assert.That(player.health.CurrentHP, Is.EqualTo(initialHP - 1));
        Step(trap, 2);
        Assert.That(player.health.CurrentHP, Is.EqualTo(initialHP - 2), "The next attack must hit an existing overlap again.");

        Time.timeScale = 0f;
        Step(trap, 10);
        Assert.That(trap.IsAttacking, Is.True);
        Assert.That(player.health.CurrentHP, Is.EqualTo(initialHP - 2));
        Time.timeScale = 1f;

        trap.enabled = false;
        Assert.That(trap.IsAttacking, Is.False);
        Assert.That(area.enabled, Is.False);
        Step(trap, 10);
        Assert.That(player.health.CurrentHP, Is.EqualTo(initialHP - 2));
        trap.enabled = true;
        Step(trap);
        Assert.That(trap.IsAttacking, Is.False);
        Step(trap);
        Assert.That(player.health.CurrentHP, Is.EqualTo(initialHP - 3));
    }

    [Test]
    public void LayerFilteringAndDashInvincibilityDoNotConsumeTheNextAcceptedHit()
    {
        Player player = CreatePlayer(out BoxCollider2D first, out BoxCollider2D second);
        SteamVentTrap trap = CreateTrap();
        int initialHP = player.health.CurrentHP;
        first.gameObject.layer = second.gameObject.layer = 0;
        Step(trap, 2);
        Assert.That(player.health.CurrentHP, Is.EqualTo(initialHP));

        Set(player.dash, "isDashing", true);
        player.dash.BeginDashInvincibility();
        Assert.That(player.health.IsInvincible, Is.True);
        first.gameObject.layer = second.gameObject.layer = LayerMask.NameToLayer("Player");
        Step(trap, 2);
        Assert.That(player.health.CurrentHP, Is.EqualTo(initialHP));
        Assert.That(player.motor.Velocity, Is.EqualTo(Vector2.zero));

        player.dash.EndDashInvincibility();
        Step(trap);
        Assert.That(player.health.CurrentHP, Is.EqualTo(initialHP - 1));
        Assert.That(player.motor.Velocity, Is.EqualTo(new Vector2(8f, 5f)));
        Step(trap, 2);
        Assert.That(player.health.CurrentHP, Is.EqualTo(initialHP - 1));
    }
}
