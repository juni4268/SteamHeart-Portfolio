using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class Boss2BlackHoleDamageTests
{
    private GameObject root;
    private GameObject playerObject;
    private PlayerData data;
    private Boss2Controller boss;
    private Boss2BlackHoleField field;
    private PlayerHealth health;
    private int originalRuntimeHealth;

    private static void Set(object target, System.Type type, string name, object value) =>
        type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

    private void Tick(float duration) => typeof(Boss2BlackHoleField)
        .GetMethod("TickDamage", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(field, new object[] { duration });

    [SetUp]
    public void SetUp()
    {
        originalRuntimeHealth = PlayerRuntimeState.CurrentHP;
        root = new GameObject("Black Hole damage test"); root.transform.position = new Vector3(1000f, 1000f);
        boss = root.AddComponent<Boss2Controller>();
        var bossHealth = root.GetComponent<BossHealth>();
        Set(bossHealth, typeof(BossHealth), "currentHp", bossHealth.MaxHp);
        Set(boss, typeof(Boss2Controller), "health", bossHealth); boss.BeginCombat();
        playerObject = new GameObject("Damage target"); playerObject.SetActive(false);
        playerObject.transform.SetParent(root.transform, false);
        var player = playerObject.AddComponent<Player>();
        data = ScriptableObject.CreateInstance<PlayerData>();
        Set(data, typeof(PlayerData), "maxHP", 20);
        Set(player, typeof(Player), "data", data);
        health = playerObject.GetComponent<PlayerHealth>();
        Set(health, typeof(PlayerHealth), "player", player);
        Set(health, typeof(PlayerHealth), "currentHP", 20);
        playerObject.AddComponent<BoxCollider2D>().size = Vector2.one * 0.5f;
        playerObject.SetActive(true);
        field = root.AddComponent<Boss2BlackHoleField>();
        Set(field, typeof(Boss2BlackHoleField), "origin", root.transform);
        Physics2D.SyncTransforms(); field.Begin(boss, health);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(root); Object.DestroyImmediate(data);
        PlayerRuntimeState.SetHealth(originalRuntimeHealth);
    }

    [Test]
    public void ContactDamagesImmediatelyThenRespectsTheInterval()
    {
        Tick(0f); Assert.That(health.CurrentHP, Is.EqualTo(19));
        Tick(0.25f); Assert.That(health.CurrentHP, Is.EqualTo(19));
        Tick(0.25f); Assert.That(health.CurrentHP, Is.EqualTo(18));
    }

    [Test]
    public void DamageUsesBodyOverlapAndDoesNotFillTheEntireSuctionRange()
    {
        playerObject.transform.localPosition = Vector3.right * 1.4f; Physics2D.SyncTransforms();
        Tick(1f); Assert.That(health.CurrentHP, Is.EqualTo(20));
        playerObject.transform.localPosition = Vector3.right * 1.2f; Physics2D.SyncTransforms();
        Tick(0f); Assert.That(health.CurrentHP, Is.EqualTo(19));
    }

    [Test]
    public void LeavingAndReenteringDoesNotBypassTheHitInterval()
    {
        Tick(0f);
        playerObject.transform.localPosition = Vector3.right * 4f; Physics2D.SyncTransforms(); Tick(0.25f);
        playerObject.transform.localPosition = Vector3.zero; Physics2D.SyncTransforms(); Tick(0.1f);
        Assert.That(health.CurrentHP, Is.EqualTo(19));
        Tick(0.2f); Assert.That(health.CurrentHP, Is.EqualTo(18));
    }

    [Test]
    public void StopPreventsFurtherDamageAndBeginStartsAFreshInterval()
    {
        Tick(0f); field.Stop(); Tick(2f);
        Assert.That(health.CurrentHP, Is.EqualTo(19));
        field.Begin(boss, health); Tick(0f);
        Assert.That(health.CurrentHP, Is.EqualTo(18));
    }

    [Test]
    public void DamageCallbackCanStopTheFieldWithoutRepeatedHitsOrNullAccess()
    {
        health.Damaged += _ => field.Stop();
        Assert.DoesNotThrow(() => Tick(2f));
        Assert.That(health.CurrentHP, Is.EqualTo(19));
        Assert.That(field.Active, Is.False);
        Assert.DoesNotThrow(() => Tick(2f));
        Assert.That(health.CurrentHP, Is.EqualTo(19));
    }
}
