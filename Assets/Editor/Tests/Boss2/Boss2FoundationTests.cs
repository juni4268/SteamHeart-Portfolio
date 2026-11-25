using NUnit.Framework;

public sealed class Boss2FoundationTests
{
    [Test]
    public void MainReservationWaitsForCurrentBasicAndBlocksOnlyItsNextUse()
    {
        var usage = new Boss2PartUsage();
        Assert.That(usage.TryReserve(Boss2Parts.LowerLeftHand, Boss2PatternKind.Basic, Boss2Parts.None, out var basic), Is.True);
        Assert.That(usage.TryActivate(basic), Is.True);
        Assert.That(usage.TryReserve(Boss2Parts.LowerHands, Boss2PatternKind.Main, Boss2Parts.None, out var gravity), Is.True);

        Assert.That(usage.GetOwner(Boss2PartId.LowerLeftHand), Is.SameAs(basic));
        Assert.That(usage.TryActivate(gravity), Is.False);
        Assert.That(usage.TryReserve(Boss2Parts.LowerRightHand, Boss2PatternKind.Basic, Boss2Parts.None, out _), Is.False);
        Assert.That(usage.TryReserve(Boss2Parts.Ring, Boss2PatternKind.Basic, Boss2Parts.None, out var ring), Is.True);
        Assert.That(usage.TryActivate(ring), Is.True);

        usage.Release(basic);
        Assert.That(usage.TryActivate(gravity), Is.True);
        Assert.That(usage.GetOwner(Boss2PartId.LowerLeftHand), Is.SameAs(gravity));
        Assert.That(usage.GetOwner(Boss2PartId.Ring), Is.SameAs(ring));
    }

    [Test]
    public void ConflictingMainRequestDoesNotReserveAnyOtherParts()
    {
        var usage = new Boss2PartUsage();
        usage.TryReserve(Boss2Parts.UpperLeftHand, Boss2PatternKind.Main, Boss2Parts.None, out var pointing);
        usage.TryActivate(pointing);

        Assert.That(usage.TryReserve(Boss2Parts.AllHands, Boss2PatternKind.Main, Boss2Parts.None, out _), Is.False);
        Assert.That(usage.GetReservation(Boss2PartId.LowerLeftHand), Is.Null);
        Assert.That(usage.GetReservation(Boss2PartId.UpperRightHand), Is.Null);
        Assert.That(usage.TryReserve(Boss2Parts.LowerHands, Boss2PatternKind.Main, Boss2Parts.None, out _), Is.True);
    }

    [Test]
    public void PointingHandCanBeReleasedWhileRingContinues()
    {
        var usage = new Boss2PartUsage();
        usage.TryReserve(Boss2Parts.UpperLeftHand | Boss2Parts.Ring, Boss2PatternKind.Main, Boss2Parts.None, out var beam);
        usage.TryActivate(beam);

        Assert.That(usage.ReleaseParts(beam, Boss2Parts.UpperLeftHand), Is.True);
        Assert.That(usage.Owns(beam, Boss2Parts.Ring), Is.True);
        Assert.That(usage.TryReserve(Boss2Parts.AllHands, Boss2PatternKind.Main, Boss2Parts.None, out var explosion), Is.True);
        Assert.That(usage.TryActivate(explosion), Is.True);
        Assert.That(usage.TryReserve(Boss2Parts.Ring, Boss2PatternKind.Main, Boss2Parts.None, out _), Is.False);

        usage.Release(beam);
        Assert.That(usage.GetOwner(Boss2PartId.UpperLeftHand), Is.SameAs(explosion));
        Assert.That(usage.GetOwner(Boss2PartId.Ring), Is.Null);
    }

    [Test]
    public void GuardWaitsForBasicAttackAndKeepsLowerHandsPausedWithoutOwningThem()
    {
        var usage = new Boss2PartUsage();
        usage.TryReserve(Boss2Parts.LowerLeftHand, Boss2PatternKind.Basic, Boss2Parts.None, out var cannon);
        usage.TryActivate(cannon);
        usage.TryReserve(Boss2Parts.UpperHands | Boss2Parts.Ring, Boss2PatternKind.Main, Boss2Parts.LowerHands, out var guard);

        Assert.That(usage.TryActivate(guard), Is.False);
        usage.Release(cannon);
        Assert.That(usage.TryActivate(guard), Is.True);
        Assert.That(usage.GetOwner(Boss2PartId.LowerLeftHand), Is.Null);
        Assert.That(usage.TryReserve(Boss2Parts.LowerHands, Boss2PatternKind.Basic, Boss2Parts.None, out _), Is.False);
        Assert.That(usage.TryReserve(Boss2Parts.LowerHands, Boss2PatternKind.Main, Boss2Parts.None, out _), Is.True);
        usage.Release(guard);
        Assert.That(usage.IsBasicPaused(Boss2Parts.LowerHands), Is.False);
    }

    [Test]
    public void CancellingPendingRequestClearsReservationAndBasicPause()
    {
        var usage = new Boss2PartUsage();
        usage.TryReserve(Boss2Parts.Ring, Boss2PatternKind.Main, Boss2Parts.LowerHands, out var request);
        usage.Release(request);
        Assert.That(request.State, Is.EqualTo(Boss2ExecutionState.Finished));
        Assert.That(usage.GetReservation(Boss2PartId.Ring), Is.Null);
        Assert.That(usage.TryReserve(Boss2Parts.LowerHands, Boss2PatternKind.Basic, Boss2Parts.None, out _), Is.True);
        Assert.That(usage.TryReserve(Boss2Parts.Ring, Boss2PatternKind.Main, Boss2Parts.None, out _), Is.True);
    }

    [Test]
    public void AllRequiredPartsMustBeReadyBeforeAnyOwnershipChanges()
    {
        var usage = new Boss2PartUsage();
        usage.TryReserve(Boss2Parts.AllHands, Boss2PatternKind.Main, Boss2Parts.None, out var request);
        Assert.That(usage.TryActivate(request, id => id != Boss2PartId.LowerRightHand), Is.False);
        Assert.That(usage.GetOwner(Boss2PartId.UpperLeftHand), Is.Null);
        Assert.That(request.State, Is.EqualTo(Boss2ExecutionState.WaitingForParts));
        Assert.That(usage.TryActivate(request, _ => true), Is.True);
    }

    [Test]
    public void BasicPauseRemainsUntilAllRequestingExecutionsHaveFinished()
    {
        var usage = new Boss2PartUsage();
        usage.TryReserve(Boss2Parts.UpperLeftHand, Boss2PatternKind.Main, Boss2Parts.LowerHands, out var first);
        usage.TryReserve(Boss2Parts.Ring, Boss2PatternKind.Main, Boss2Parts.LowerHands, out var second);
        usage.Release(first);
        Assert.That(usage.IsBasicPaused(Boss2Parts.LowerHands), Is.True);
        usage.Release(second);
        Assert.That(usage.IsBasicPaused(Boss2Parts.LowerHands), Is.False);
    }
}
