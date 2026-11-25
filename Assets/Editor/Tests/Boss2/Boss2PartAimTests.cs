using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class Boss2PartAimTests
{
    private GameObject root;
    private Boss2PartController part;
    private Boss2PartAim aim;
    private Transform pivot;
    private Transform mirror;
    private Transform firePoint;

    private static void Set(object target, System.Type type, string field, object value) =>
        type.GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

    private static Transform Child(string name, Transform parent, Vector3 position)
    {
        var child = new GameObject(name).transform;
        child.SetParent(parent, false);
        child.localPosition = position;
        return child;
    }

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Boss2 aiming test");
        root.transform.SetPositionAndRotation(new Vector3(3f, -2f, 0f), Quaternion.Euler(0f, 0f, 17f));
        part = root.AddComponent<Boss2PartController>();
        aim = root.AddComponent<Boss2PartAim>();
        pivot = Child("Pivot", root.transform, new Vector3(0.5f, 0.25f, 0f));
        mirror = Child("Mirror", pivot, Vector3.zero);
        var visual = Child("Visual", mirror, new Vector3(-0.6f, -0.4f, 0f));
        firePoint = Child("Muzzle", visual, new Vector3(0.9f, 0.75f, 0f));
        firePoint.localRotation = Quaternion.Euler(0f, 0f, 25f);
        Set(aim, typeof(Boss2PartAim), "aimPivot", pivot);
        Set(aim, typeof(Boss2PartAim), "visualGroup", mirror);
        Set(aim, typeof(Boss2PartAim), "firePoint", firePoint);
        Set(part, typeof(Boss2PartController), "aim", aim);
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(root);

    [TestCase(false, 4f, 3f)]
    [TestCase(false, -4f, 3f)]
    [TestCase(false, -4f, -3f)]
    [TestCase(false, 4f, -3f)]
    [TestCase(true, 4f, 3f)]
    [TestCase(true, -4f, 3f)]
    [TestCase(true, -4f, -3f)]
    [TestCase(true, 4f, -3f)]
    public void OffsetMuzzleAimsAtTheTargetWithoutMovingThePartOrChangingItsPose(bool mirrored, float x, float y)
    {
        Set(aim, typeof(Boss2PartAim), "mirrored", mirrored);
        aim.ResetAim();
        Vector3 rootPosition = root.transform.position;
        Quaternion rootRotation = root.transform.rotation;
        Vector3 muzzleLocalPosition = firePoint.localPosition;
        Quaternion muzzleLocalRotation = firePoint.localRotation;
        Vector2 target = (Vector2)pivot.position + new Vector2(x, y);

        Assert.That(aim.TryAimAt(target), Is.True);
        Vector2 actualDirection = (target - (Vector2)aim.FirePosition).normalized;
        Assert.That(Vector2.Dot(actualDirection, aim.FireDirection), Is.GreaterThan(0.99999f));
        Assert.That(root.transform.position, Is.EqualTo(rootPosition));
        Assert.That(root.transform.rotation, Is.EqualTo(rootRotation));
        Assert.That(firePoint.localPosition, Is.EqualTo(muzzleLocalPosition));
        Assert.That(firePoint.localRotation, Is.EqualTo(muzzleLocalRotation));
        Assert.That(mirror.localScale.x, Is.EqualTo(mirrored ? -1f : 1f));
    }

    [Test]
    public void ReturnAndCancellationRestoreTheAuthoredIdleAngle()
    {
        Set(aim, typeof(Boss2PartAim), "idleAngle", 35f);
        aim.ResetAim();
        Assert.That(aim.TryAimAt((Vector2)pivot.position + Vector2.left * 5f), Is.True);
        Quaternion idle = Quaternion.Euler(0f, 0f, 35f);
        float initialDifference = Quaternion.Angle(pivot.localRotation, idle);
        aim.BeginReturnToIdle();
        aim.UpdateReturnToIdle(0.5f);
        Assert.That(Quaternion.Angle(pivot.localRotation, idle), Is.LessThan(initialDifference));
        aim.UpdateReturnToIdle(1f);
        Assert.That(Quaternion.Angle(pivot.localRotation, idle), Is.LessThan(0.001f));

        aim.TryAimAt((Vector2)pivot.position + Vector2.down * 5f);
        part.FollowIdle();
        Assert.That(Quaternion.Angle(pivot.localRotation, idle), Is.LessThan(0.001f));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void GradualAimingLimitsEachStepAndEventuallyAlignsTheOffsetMuzzle(bool mirrored)
    {
        Set(aim, typeof(Boss2PartAim), "mirrored", mirrored);
        aim.ResetAim();
        Vector2 targetDirection = Quaternion.Euler(0f, 0f, 100f) * aim.FireDirection;
        Vector2 target = (Vector2)aim.PivotPosition + targetDirection * 5f;
        Quaternion original = pivot.rotation;

        Assert.That(aim.TryAimAt(target, 10f, out bool aligned), Is.True);
        Assert.That(aligned, Is.False);
        Assert.That(Quaternion.Angle(original, pivot.rotation), Is.EqualTo(10f).Within(0.01f));
        for (int i = 0; i < 30 && !aligned; i++)
        {
            Quaternion previous = pivot.rotation;
            Assert.That(aim.TryAimAt(target, 10f, out aligned), Is.True);
            Assert.That(Quaternion.Angle(previous, pivot.rotation), Is.LessThanOrEqualTo(10.01f));
        }
        Assert.That(aligned, Is.True);
        Assert.That(Vector2.Dot((target - (Vector2)aim.FirePosition).normalized,
            aim.FireDirection), Is.GreaterThan(0.99999f));
    }

    [Test]
    public void AnUnreachableTargetDoesNotRotateThePart()
    {
        firePoint.parent.localPosition = Vector3.zero;
        firePoint.localPosition = Vector3.up * 2f;
        firePoint.localRotation = Quaternion.identity;
        aim.ResetAim();
        Quaternion original = pivot.rotation;
        Assert.That(aim.TryAimAt((Vector2)pivot.position + Vector2.right), Is.False);
        Assert.That(pivot.rotation, Is.EqualTo(original));
    }
}
