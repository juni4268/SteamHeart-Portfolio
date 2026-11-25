using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class CameraTargetDirectionSmootherTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [Test]
    public void ShortReversalsAndReleasedInputKeepTheViewWhileContinuousInputTurnsIt()
    {
        var root = new GameObject("Camera turn delay test") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            var target = new GameObject("CameraTarget").transform;
            target.SetParent(root.transform);
            target.localPosition = new Vector3(2f, 1f, 0f);
            var smoother = root.AddComponent<CameraTargetDirectionSmoother>();
            typeof(CameraTargetDirectionSmoother).GetMethod("Awake", PrivateInstance).Invoke(smoother, null);
            var update = typeof(CameraTargetDirectionSmoother).GetMethod("UpdateCameraTarget", PrivateInstance);
            void Step(float input, float seconds) => update.Invoke(smoother, new object[] { input, seconds });

            root.transform.localScale = new Vector3(-1f, 1f, 1f);
            Step(-1f, 0.1f);
            Assert.That(target.position.x, Is.EqualTo(2f), "A short reversal keeps the previous view.");

            root.transform.position = new Vector3(3f, 0f, 0f);
            Step(0f, 1f);
            Assert.That(target.position.x, Is.EqualTo(5f), "Releasing cancels the turn but still follows the player.");
            Assert.That(target.position.y, Is.EqualTo(1f));

            Step(-1f, 0f);
            Step(-1f, 0.1f);
            Assert.That(target.position.x, Is.EqualTo(5f), "Paused time and earlier taps do not count toward the delay.");

            root.transform.localScale = Vector3.one;
            Step(1f, 0.1f);
            root.transform.localScale = new Vector3(-1f, 1f, 1f);
            Step(-1f, 0.1f);
            Assert.That(target.position.x, Is.EqualTo(5f), "Returning to the original direction resets the delay.");

            Step(-1f, 0.11f);
            Assert.That(target.position.x, Is.LessThan(5f), "Continuous reverse input starts a smooth turn after the delay.");
            for (int i = 0; i < 20; i++) Step(-1f, 0.1f);
            Assert.That(target.position.x, Is.EqualTo(1f).Within(0.01f));
        }
        finally { Object.DestroyImmediate(root); }
    }
}
