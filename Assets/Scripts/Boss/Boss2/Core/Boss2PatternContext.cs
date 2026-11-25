using System;
using System.Collections;

public sealed class Boss2PatternContext
{
    private readonly Boss2PartUsage usage;
    private readonly Boss2PartUsage.Reservation reservation;

    internal Boss2PatternContext(Boss2Controller controller, Boss2PartUsage usage,
        Boss2PartUsage.Reservation reservation)
    {
        Controller = controller;
        Phase = controller.CurrentPhase;
        this.usage = usage;
        this.reservation = reservation;
    }

    public Boss2Controller Controller { get; }
    // Capture at actual start. A later phase change does not change this invocation's settings.
    public Boss2Phase Phase { get; }
    public Boss2Parts HeldParts => reservation.HeldParts;
    public bool FinishRequested { get; internal set; }

    public Boss2PartController GetPart(Boss2PartId part)
    {
        if (!usage.Owns(reservation, Boss2PartMask.FromId(part)))
            throw new InvalidOperationException($"This execution does not own {part}.");
        return Controller.GetPart(part);
    }

    public IEnumerator WaitForParts(Boss2Parts parts)
    {
        if (!usage.Owns(reservation, parts))
            throw new InvalidOperationException("Cannot wait for parts owned by another execution.");
        while (!Controller.ArePartsReady(parts))
        {
            if (!Controller.HasActiveParts(parts))
                throw new InvalidOperationException("A required part became unavailable.");
            yield return null;
        }
    }

    public void ReleaseParts(Boss2Parts parts)
    {
        if (!Controller.ArePartsReady(parts) || !usage.ReleaseParts(reservation, parts))
            throw new InvalidOperationException("Release only owned parts that are ready for their next action.");
    }

    internal void ResetOwnedParts()
    {
        foreach (Boss2PartId id in Enum.GetValues(typeof(Boss2PartId)))
        {
            if ((HeldParts & Boss2PartMask.FromId(id)) == 0) continue;
            Boss2PartController part = Controller.GetPart(id);
            if (part == null) continue;
            part.StopMovement();
            if (!Controller.IsDead) part.FollowIdle();
        }
    }
}
