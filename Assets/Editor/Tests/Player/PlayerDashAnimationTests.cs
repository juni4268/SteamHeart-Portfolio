using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

[Category("PlayerRefactor")]
public sealed class PlayerDashAnimationTests
{
    private static void Invoke(PlayerDash dash, string method) =>
        typeof(PlayerDash).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(dash, null);

    private static bool LandingEntered(PlayerDash dash) => (bool)typeof(PlayerDash)
        .GetField("landingEntered", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(dash);

    [UnityTest]
    public IEnumerator CompletionAndBlockedMovementEnterLandBeforeUnlocking()
    {
        yield return new EnterPlayMode();
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects()) root.SetActive(false);
        var cameraObject = new GameObject("Dash test camera", typeof(Camera));
        cameraObject.tag = "MainCamera";
        var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab"));
        instance.transform.position = new Vector3(1000f, 1000f);
        instance.GetComponent<PlayerMovement>().enabled = false;
        instance.GetComponent<Rigidbody2D>().simulated = false;
        PlayerDash dash = instance.GetComponent<PlayerDash>();
        Animator animator = instance.GetComponentInChildren<Animator>();
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.SetBool("isGrounded", true);
        animator.Rebind();
        animator.Update(0f);

        Invoke(dash, "StartDash");
        animator.Update(0f);
        animator.Update(0.01f);
        Assert.That(dash.IsDashing, Is.True);
        Assert.That(LandingEntered(dash), Is.False);
        Assert.That(instance.GetComponent<PlayerMotor>().Velocity.x, Is.GreaterThan(0f));
        animator.Update(0.31f);
        animator.Update(0f);
        Assert.That(LandingEntered(dash), Is.True);
        Assert.That(dash.IsDashing, Is.True, "Keep controls locked through recovery.");
        Assert.That(instance.GetComponent<PlayerMotor>().Velocity.x, Is.Zero);
        animator.Update(0.25f);
        animator.Update(0f);
        Assert.That(dash.IsDashing, Is.False);

        // Animator.Update advances animation time, but not the gameplay cooldown clock.
        Invoke(dash, "StartDash");
        Assert.That(dash.IsDashing, Is.False, "A second dash must respect its gameplay cooldown.");
        float nextDashTime = Time.time + instance.GetComponent<Player>().Data.DashCooldown;
        while (Time.time <= nextDashTime) yield return null;

        Invoke(dash, "StartDash");
        animator.Update(0f);
        animator.Update(0.01f);
        // A body that cannot move must stop before the Dash clip finishes.
        Invoke(dash, "FixedUpdate");
        Invoke(dash, "FixedUpdate");
        Invoke(dash, "FixedUpdate");
        animator.Update(0f);
        animator.Update(0.01f);
        Assert.That(LandingEntered(dash), Is.True);
        Assert.That(dash.IsDashing, Is.True);
        Assert.That(instance.GetComponent<PlayerMotor>().Velocity.x, Is.Zero);
        animator.Update(0.25f);
        animator.Update(0f);
        Assert.That(dash.IsDashing, Is.False);
        yield return new ExitPlayMode();
    }

    [UnityTearDown]
    public IEnumerator LeavePlayModeAfterFailure()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
}
