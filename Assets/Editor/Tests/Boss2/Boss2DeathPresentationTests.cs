using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class Boss2DeathPresentationTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [Test]
    public void BodyAndRingFinishTheirAuthoredDeathMotionsAndHoldTheLastSprites()
    {
        foreach (string role in new[] { "Body", "Ring" })
        {
            var root = new GameObject("Death motion test", typeof(SpriteRenderer), typeof(Animator), typeof(Boss2DeathAnimation));
            try
            {
                var animator = root.GetComponent<Animator>();
                animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                    "Assets/Animations/Boss/Boss2/Boss2" + role + "Idle.controller");
                animator.Rebind();
                animator.Update(0f);
                var death = root.GetComponent<Boss2DeathAnimation>();
                death.Begin();
                Assert.That(death.Completed, Is.False);
                for (int i = 0; i < 60; i++) animator.Update(1f / 60f);
                Assert.That(death.Completed, Is.False);
                for (int i = 0; i < 240 && !death.Completed; i++) animator.Update(1f / 60f);
                Assert.That(death.Completed, Is.True);
                Assert.That(death.Interrupted, Is.False);
                var renderer = root.GetComponent<SpriteRenderer>();
                Assert.That(renderer.sprite.name, Is.EqualTo("Boss2Dead" + role + "_37"));
                var last = renderer.sprite;
                for (int i = 0; i < 120; i++) animator.Update(1f / 60f);
                Assert.That(renderer.sprite, Is.SameAs(last));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }

    [Test]
    public void DeadCoreFollowsAMovingTargetAndDisappearsOnlyOnContact()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Boss/Boss2/Boss2DeadCore.prefab");
        var root = Object.Instantiate(prefab);
        var target = new GameObject("DeadCore moving target", typeof(BoxCollider2D));
        try
        {
            var core = root.GetComponent<Boss2DeadCore>();
            var settings = new SerializedObject(core);
            settings.FindProperty("flightDuration").floatValue = 1f;
            settings.FindProperty("arcHeight").floatValue = 1f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            root.transform.position = new Vector3(10000f, 10005f, 0f);
            target.transform.position = new Vector3(10004f, 10000f, 0f);
            Physics2D.SyncTransforms();
            core.Begin(target.GetComponent<Collider2D>());
            var tick = typeof(Boss2DeadCore).GetMethod("Tick", PrivateInstance);
            tick.Invoke(core, new object[] { 0.5f });
            Assert.That(core.Collected, Is.False);
            Assert.That(root.activeSelf, Is.True);
            Assert.That(target.GetComponentsInChildren<ParticleSystem>(), Is.Empty);
            target.transform.position += Vector3.right * 3f;
            Physics2D.SyncTransforms();
            tick.Invoke(core, new object[] { 0.5f });
            Assert.That(core.Collected, Is.True);
            Assert.That(root.activeSelf, Is.False);
            Assert.That(root.transform.position.x, Is.EqualTo(target.transform.position.x).Within(0.001f));
            var effects = target.GetComponentsInChildren<ParticleSystem>();
            Assert.That(effects, Is.Not.Empty);
            Assert.That(effects.Count(effect => effect.transform.parent == target.transform), Is.EqualTo(1));
            tick.Invoke(core, new object[] { 0.5f });
            Assert.That(target.GetComponentsInChildren<ParticleSystem>().Length, Is.EqualTo(effects.Length));
        }
        finally { Object.DestroyImmediate(root); Object.DestroyImmediate(target); }
    }

    [Test]
    public void ExplosionCreatesFourBodyFragmentsAndCopiesTheFourCurrentHandVisuals()
    {
        var random = Random.state;
        var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Boss/Boss2/Boss2DeathDebris.prefab"));
        var handRoot = new GameObject("Death hand sources");
        try
        {
            var sprite = AssetDatabase.LoadAllAssetsAtPath("Assets/Graphics/Characters/Boss2/Boss2CoreburstUpperHand.png")
                .OfType<Sprite>().First();
            var hands = new SpriteRenderer[4];
            for (int i = 0; i < 4; i++)
            {
                var hand = new GameObject("Hand " + i, typeof(SpriteRenderer));
                hand.transform.SetParent(handRoot.transform, false);
                hand.transform.position = new Vector3(i, i * 0.5f, 0f);
                hand.transform.rotation = Quaternion.Euler(0f, 0f, i * 20f);
                hand.transform.localScale = new Vector3(i % 2 == 0 ? -1f : 1f, 1f, 1f);
                hands[i] = hand.GetComponent<SpriteRenderer>();
                hands[i].sprite = sprite;
                hands[i].flipY = i % 2 == 0;
            }
            root.GetComponent<Boss2DeathDebris>().Launch(hands);
            Assert.That(root.GetComponentsInChildren<SpriteRenderer>().Length, Is.EqualTo(8));
            Assert.That(root.GetComponentsInChildren<Rigidbody2D>().Length, Is.EqualTo(8));
            Assert.That(root.GetComponentsInChildren<BoxCollider2D>().Length, Is.EqualTo(8));
            for (int i = 0; i < 4; i++)
            {
                var copy = root.transform.Find("Hand Debris " + (i + 1)).GetComponent<SpriteRenderer>();
                Assert.That(copy.sprite, Is.SameAs(hands[i].sprite));
                Assert.That(copy.flipY, Is.EqualTo(hands[i].flipY));
                Assert.That(copy.transform.position, Is.EqualTo(hands[i].transform.position));
                Assert.That(Vector3.Distance(copy.transform.lossyScale, hands[i].transform.lossyScale), Is.LessThan(0.0001f));
                Assert.That(copy.GetComponent<Rigidbody2D>().linearVelocity.y, Is.GreaterThan(0f));
            }
        }
        finally { Object.DestroyImmediate(root); Object.DestroyImmediate(handRoot); Random.state = random; }
    }
}
