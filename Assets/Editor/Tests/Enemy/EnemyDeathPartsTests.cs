using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class EnemyDeathPartsTests
{
    [TestCase("EnemyB", 6)]
    [TestCase("EnemyL", 4)]
    [TestCase("EnemyS", 4)]
    public void EnemyPrefabsBindTheirOwnPartsAndKeepTheSmokeEffect(string name, int count)
    {
        var enemy = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy/" + name + ".prefab");
        var health = new SerializedObject(enemy.GetComponent<EnemyHealth>());
        Assert.That(AssetDatabase.GetAssetPath(health.FindProperty("deathEffectPrefab").objectReferenceValue),
            Is.EqualTo("Assets/Prefabs/Enemy/EnemySmokeDeath.prefab"));
        var parts = (EnemyDeathParts)health.FindProperty("deathPartsPrefab").objectReferenceValue;
        Assert.That(parts, Is.Not.Null);
        var renderers = parts.GetComponentsInChildren<SpriteRenderer>(true);
        Assert.That(renderers.Length, Is.EqualTo(count));
        foreach (SpriteRenderer renderer in renderers)
        {
            Assert.That(renderer.sprite, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(renderer.sprite),
                Does.StartWith("Assets/Graphics/Characters/Enemies/" + name + "/"));
            Assert.That(renderer.GetComponent<Rigidbody2D>(), Is.Not.Null);
            Assert.That(renderer.GetComponent<Collider2D>(), Is.Not.Null);
        }
        if (name == "EnemyL")
        {
            Assert.That(renderers.Any(r => r.sprite.name == "EnemyLCore_0"), Is.True);
            Assert.That(renderers.Any(r => r.sprite.name == "EnemyLLeg_0"), Is.True);
        }
    }

    [UnityTest]
    public IEnumerator PartsKeepTheirAuthoredMaterialsAfterSourceFlashIsDestroyed()
    {
        yield return new EnterPlayMode();
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects()) root.SetActive(false);
        Time.timeScale = 1f;
        Random.State randomState = Random.state;
        GameObject enemy = null;
        EnemyDeathParts parts = null;
        try
        {
            foreach (string name in new[] { "EnemyS", "EnemyL", "EnemyB" })
            {
                enemy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy/" + name + ".prefab"));
                enemy.transform.position = new Vector3(1000f, 1000f);
                enemy.GetComponent<Rigidbody2D>().simulated = false;
                var health = new SerializedObject(enemy.GetComponent<EnemyHealth>());
                var source = ((Transform)health.FindProperty("deathEffectOrigin").objectReferenceValue).GetComponent<SpriteRenderer>();
                var prefab = (EnemyDeathParts)health.FindProperty("deathPartsPrefab").objectReferenceValue;
                parts = Object.Instantiate(prefab, source.bounds.center, Quaternion.identity);
                // Keep the parts alive while the source and its owned flash material are destroyed.
                parts.enabled = false;
                var renderers = parts.GetComponentsInChildren<SpriteRenderer>(true);
                Material[] authoredMaterials = renderers.Select(renderer => renderer.sharedMaterial).ToArray();

                enemy.GetComponent<HitImpactFlash>().Play(1f, 10f, 0f);
                Material temporaryMaterial = source.sharedMaterial;
                Assert.That(enemy.GetComponent<HitImpactFlash>().IsPlaying, Is.True);
                Assert.That(authoredMaterials.All(material => material != null && material != temporaryMaterial), Is.True);
                parts.Launch(source, 1);
                Object.Destroy(enemy);
                yield return null;
                yield return null;

                Assert.That(enemy == null, Is.True, name + " source must be removed.");
                Assert.That(temporaryMaterial == null, Is.True, name + " must dispose its temporary flash material.");
                for (int i = 0; i < renderers.Length; i++)
                {
                    Assert.That(renderers[i].sharedMaterial, Is.Not.Null, name + " part material must survive the source.");
                    Assert.That(renderers[i].sharedMaterial, Is.SameAs(authoredMaterials[i]), name + " must keep each part's authored material.");
                }
                Object.DestroyImmediate(parts.gameObject);
                parts = null;
            }
        }
        finally
        {
            if (enemy != null) Object.DestroyImmediate(enemy);
            if (parts != null) Object.DestroyImmediate(parts.gameObject);
            Random.state = randomState;
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator DeathLaunchesPartsWithSmokeAndRemovesBothEffects()
    {
        yield return new EnterPlayMode();
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects()) root.SetActive(false);
        Time.timeScale = 1f;
        GameObject enemy = null;
        EnemyDeathParts parts = null;
        CombatAnimationEffect smoke = null;
        try
        {
            enemy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy/EnemyL.prefab"));
            enemy.transform.position = new Vector3(1000f, 1000f, 0f);
            var health = enemy.GetComponent<EnemyHealth>();
            var data = new SerializedObject(health);
            data.FindProperty("scrapReward").intValue = 0;
            data.ApplyModifiedPropertiesWithoutUndo();
            var source = ((Transform)data.FindProperty("deathEffectOrigin").objectReferenceValue).GetComponent<SpriteRenderer>();
            Vector3 origin = source.bounds.center;
            float pixelSize = Mathf.Abs(source.transform.lossyScale.x) / source.sprite.pixelsPerUnit;
            health.TakeDamage(health.MaxHp);
            yield return new WaitForSecondsRealtime(.08f);

            Assert.That(enemy == null, Is.True, "The enemy body should leave no corpse.");
            parts = Object.FindObjectsByType<EnemyDeathParts>(FindObjectsSortMode.None).Single();
            smoke = Object.FindObjectsByType<CombatAnimationEffect>(FindObjectsSortMode.None)
                .Single(effect => Vector3.Distance(effect.transform.position, origin) < 1f);
            Assert.That(parts.GetComponentsInChildren<SpriteRenderer>().Length, Is.EqualTo(4));
            int actors = LayerMask.GetMask("Player", "Enemy", "EnemyDeath");
            foreach (SpriteRenderer part in parts.GetComponentsInChildren<SpriteRenderer>())
            {
                Assert.That(part.GetComponent<Rigidbody2D>().linearVelocity.sqrMagnitude, Is.GreaterThan(0f));
                Assert.That(Mathf.Abs(part.transform.lossyScale.x) / part.sprite.pixelsPerUnit,
                    Is.EqualTo(pixelSize).Within(.0001f), "Parts must keep the source's pixel density.");
                Assert.That(part.GetComponent<Collider2D>().excludeLayers.value & actors, Is.EqualTo(actors),
                    "Cosmetic parts must not collide with actors or each other.");
            }

            float deadline = Time.time + 1.2f;
            while ((parts != null || smoke != null) && Time.time < deadline) yield return null;
            Assert.That(parts == null, Is.True, "No parts should remain after their lifetime.");
            Assert.That(smoke == null, Is.True, "The existing smoke completion event must still run.");
        }
        finally
        {
            if (enemy != null) Object.DestroyImmediate(enemy);
            if (parts != null) Object.DestroyImmediate(parts.gameObject);
            if (smoke != null) Object.DestroyImmediate(smoke.gameObject);
        }
        yield return new ExitPlayMode();
    }

    [UnityTearDown]
    public IEnumerator RestoreEditor()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
}
