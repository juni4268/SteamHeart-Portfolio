using NUnit.Framework;
using UnityEngine;

public sealed class GameplayRegressionTests
{
    [TestCase(1f)]
    [TestCase(-1f)]
    public void VerticalHandTrackingStopsAtWallsAndCanMoveAway(float direction)
    {
        GameObject handObject = new GameObject("Tracking hand test");
        GameObject wallObject = new GameObject("Tracking wall test");
        try
        {
            Vector2 origin = new Vector2(1000f, 1000f);
            handObject.transform.position = origin;
            handObject.AddComponent<BoxCollider2D>().isTrigger = true;
            Boss1HandController hand = handObject.AddComponent<Boss1HandController>();
            wallObject.layer = LayerMask.NameToLayer("Ground");
            wallObject.transform.position = origin + Vector2.up * (3f * direction);
            BoxCollider2D wall = wallObject.AddComponent<BoxCollider2D>();
            wall.size = new Vector2(4f, 1f);
            hand.StartTracking(wallObject.transform, 100f, Boss1HandTrackingAxis.Vertical);
            var clamp = typeof(Boss1HandController).GetMethod("ClampTrackingPosition",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Vector2 target = origin + Vector2.up * (10f * direction);
            Vector2 stopped = (Vector2)clamp.Invoke(hand, new object[] { target });
            // Physics queries include a small contact margin in addition to the hand clearance.
            Assert.That((stopped.y - origin.y) * direction, Is.InRange(1.97f, 1.999f));
            handObject.transform.position = stopped;
            Assert.That((Vector2)clamp.Invoke(hand, new object[] { origin }), Is.EqualTo(origin));

            handObject.transform.position = origin;
            wallObject.layer = LayerMask.NameToLayer("Wall");
            Assert.That((Vector2)clamp.Invoke(hand, new object[] { target }), Is.EqualTo(target));
            wallObject.layer = LayerMask.NameToLayer("Ground");
            wall.isTrigger = true;
            Assert.That((Vector2)clamp.Invoke(hand, new object[] { target }), Is.EqualTo(target));
        }
        finally
        {
            Object.DestroyImmediate(handObject);
            Object.DestroyImmediate(wallObject);
        }
    }

    [Test]
    [Category("PlayerRefactor")]
    public void ScrapProgressRoundTripsAndOlderSavesStartAtZero()
    {
        PlayerProgressData legacyCoins = PlayerProgressStore.Deserialize(
            "{\"version\":1,\"coins\":37}");
        Assert.That(legacyCoins.scrap, Is.EqualTo(37));
        Assert.That(PlayerProgressStore.IsValid(legacyCoins), Is.True);

        PlayerProgressData loaded = JsonUtility.FromJson<PlayerProgressData>(
            JsonUtility.ToJson(new PlayerProgressData { scrap = 37 }));
        Assert.That(loaded.scrap, Is.EqualTo(37));
        Assert.That(PlayerProgressStore.IsValid(loaded), Is.True);

        PlayerProgressData older = JsonUtility.FromJson<PlayerProgressData>("{\"version\":1}");
        Assert.That(older.scrap, Is.Zero);
        Assert.That(older.attackPowerBonus, Is.Zero);
        Assert.That(older.healthPieces, Is.Zero);
        Assert.That(PlayerProgressStore.IsValid(older), Is.True);

        PlayerProgressData legacyHealth = PlayerProgressStore.Deserialize(
            "{\"version\":1,\"healthShards\":6}");
        Assert.That(legacyHealth.healthPieces, Is.EqualTo(6));
        PlayerProgressData mixed = PlayerProgressStore.Deserialize(
            "{\"version\":1,\"scrap\":0,\"coins\":37,\"healthPieces\":0,\"healthShards\":6}");
        Assert.That(mixed.scrap, Is.Zero);
        Assert.That(mixed.healthPieces, Is.Zero);
        Assert.That(PlayerProgressStore.IsValid(PlayerProgressStore.Deserialize(
            "{\"version\":1,\"coins\":-1}")), Is.False);
        Assert.That(PlayerProgressStore.IsValid(new PlayerProgressData { scrap = -1 }), Is.False);
        Assert.That(PlayerProgressStore.IsValid(new PlayerProgressData { attackPowerBonus = -1 }), Is.False);
        Assert.That(PlayerProgressStore.IsValid(new PlayerProgressData { healthPieces = -1 }), Is.False);
    }

    [TestCase("{}")]
    [TestCase("{\"scrap\":37,\"attackPowerBonus\":2,\"healthPieces\":6}")]
    [TestCase("{\"metadata\":{\"version\":1}}")]
    public void ProgressWithoutAnExplicitVersionIsRejected(string json)
    {
        Assert.That(PlayerProgressStore.IsValid(PlayerProgressStore.Deserialize(json)), Is.False);
    }

    [Test]
    public void VersionedLegacyProgressStillLoads()
    {
        PlayerProgressData progress = PlayerProgressStore.Deserialize(
            "{\"version\":1,\"coins\":37,\"attackPowerBonus\":2,\"healthShards\":6}");
        Assert.That(PlayerProgressStore.IsValid(progress), Is.True);
        Assert.That(progress.scrap, Is.EqualTo(37));
        Assert.That(progress.attackPowerBonus, Is.EqualTo(2));
        Assert.That(progress.healthPieces, Is.EqualTo(6));
        Assert.That(PlayerProgressStore.IsValid(PlayerProgressStore.Deserialize("{\"version\":1}")), Is.True);
    }

    [Test]
    public void CancellingDuringAnInstantHandSweepPreservesIdleState()
    {
        GameObject handObject = new GameObject("Cancelled hand test");
        try
        {
            Boss1HandController hand = handObject.AddComponent<Boss1HandController>();
            hand.BeforeMove += position => hand.ResetState();
            hand.MoveTo(new Vector2(5f, 0f), 0f);
            Assert.That(hand.CurrentMotion, Is.EqualTo(Boss1HandMotion.Idle));
            Assert.That(hand.transform.localPosition, Is.EqualTo(Vector3.zero));
        }
        finally { Object.DestroyImmediate(handObject); }
    }

    [Test]
    public void BossRunnerCleansUpOnCompletionCancellationAndFailure()
    {
        Assert.That(Boss1ArchitectureChecks.Run(), Has.Length.EqualTo(6));
    }

    [Test]
    public void AttackBoxUsesMirroredOffsetAndScaledSize()
    {
        GameObject point = new GameObject("Attack geometry test");
        try
        {
            point.transform.position = new Vector3(3f, 4f);
            point.transform.localScale = new Vector3(-2f, 3f, 1f);
            AttackBoxGeometry box = new AttackBoxGeometry(point.transform,
                new Vector2(0.5f, 0.25f), new Vector2(1f, 2f));
            Assert.That(box.Center.x, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(box.Center.y, Is.EqualTo(4.75f).Within(0.0001f));
            Assert.That(box.Size, Is.EqualTo(new Vector2(2f, 6f)));
            Assert.That(AttackBoxGeometry.ToLocalSize(point.transform, box.Size),
                Is.EqualTo(new Vector2(1f, 2f)));

            point.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            box = new AttackBoxGeometry(point.transform, Vector2.right, Vector2.one);
            Assert.That(box.Center.x, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(box.Center.y, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(box.Angle, Is.EqualTo(90f).Within(0.0001f));
        }
        finally { Object.DestroyImmediate(point); }
    }

    [Test]
    [Category("PlayerRefactor")]
    public void ProgressSerializationStoresOnlyPermanentProgress()
    {
        PlayerProgressData original = new PlayerProgressData
        {
            scrap = 12,
            attackPowerBonus = 2,
            healthPieces = 5
        };
        string json = JsonUtility.ToJson(original);
        PlayerProgressData loaded = JsonUtility.FromJson<PlayerProgressData>(json);
        Assert.That(PlayerProgressStore.IsValid(loaded), Is.True);
        Assert.That(loaded.scrap, Is.EqualTo(12));
        Assert.That(loaded.attackPowerBonus, Is.EqualTo(2));
        Assert.That(loaded.healthPieces, Is.EqualTo(5));
        Assert.That(json, Does.Not.Contain("currentHP"));
        Assert.That(json, Does.Not.Contain("hasWeapon"));
        Assert.That(json, Does.Not.Contain("checkpoint"));
    }

    [Test]
    [Category("PlayerRefactor")]
    public void PermanentUpgradeProgressRoundTrips()
    {
        PlayerProgressData original = new PlayerProgressData
        {
            scrap = 25,
            attackPowerBonus = 4,
            healthPieces = 8
        };

        PlayerProgressData loaded = JsonUtility.FromJson<PlayerProgressData>(
            JsonUtility.ToJson(original));

        Assert.That(PlayerProgressStore.IsValid(loaded), Is.True);
        Assert.That(loaded.attackPowerBonus, Is.EqualTo(4));
        Assert.That(loaded.healthPieces, Is.EqualTo(8));
        Assert.That(loaded.healthPieces / UpgradeSettings.Shared.MaterialsPerUpgrade, Is.EqualTo(2));
    }

    [Test]
    [Category("PlayerRefactor")]
    public void ProgressRejectsUnsupportedVersionsAndNegativeValues()
    {
        Assert.That(PlayerProgressStore.IsValid(new PlayerProgressData { version = 99 }), Is.False);
        Assert.That(PlayerProgressStore.IsValid(new PlayerProgressData { scrap = -1 }), Is.False);
    }
}
