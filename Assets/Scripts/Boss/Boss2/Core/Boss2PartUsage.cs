using System;
using System.Collections.Generic;

/// <summary>Atomic reservations. A main request waits for existing basic actions without interrupting them.</summary>
public sealed class Boss2PartUsage
{
    public sealed class Reservation
    {
        internal Reservation(Boss2Parts parts, Boss2PatternKind kind, Boss2Parts pauseBasicParts)
        {
            Parts = parts;
            HeldParts = parts;
            Kind = kind;
            PauseBasicParts = pauseBasicParts;
        }

        public Boss2Parts Parts { get; }
        public Boss2Parts HeldParts { get; internal set; }
        public Boss2PatternKind Kind { get; }
        public Boss2Parts PauseBasicParts { get; }
        public Boss2ExecutionState State { get; internal set; } = Boss2ExecutionState.WaitingForParts;
    }

    private readonly Reservation[] owners = new Reservation[5];
    private readonly Reservation[] nextOwners = new Reservation[5];
    private readonly HashSet<Reservation> reservations = new HashSet<Reservation>();

    public Reservation GetOwner(Boss2PartId part) => owners[(int)part];
    public Reservation GetReservation(Boss2PartId part) => nextOwners[(int)part];

    public bool IsBasicPaused(Boss2Parts parts)
    {
        foreach (Reservation reservation in reservations)
            if ((reservation.PauseBasicParts & parts) != 0) return true;
        return false;
    }

    public bool TryReserve(Boss2Parts parts, Boss2PatternKind kind, Boss2Parts pauseBasicParts,
        out Reservation reservation)
    {
        reservation = null;
        if (parts == Boss2Parts.None || !Boss2PartMask.IsValid(parts) ||
            !Boss2PartMask.IsValid(pauseBasicParts) ||
            (kind != Boss2PatternKind.Basic && kind != Boss2PatternKind.Main) ||
            (kind == Boss2PatternKind.Basic && (pauseBasicParts != Boss2Parts.None || IsBasicPaused(parts))))
            return false;

        for (int i = 0; i < owners.Length; i++)
        {
            if ((parts & (Boss2Parts)(1 << i)) == 0) continue;
            if (nextOwners[i] != null) return false;
            if (owners[i] != null && (kind == Boss2PatternKind.Basic || owners[i].Kind == Boss2PatternKind.Main))
                return false;
        }

        reservation = new Reservation(parts, kind, pauseBasicParts);
        reservations.Add(reservation);
        for (int i = 0; i < nextOwners.Length; i++)
            if ((parts & (Boss2Parts)(1 << i)) != 0) nextOwners[i] = reservation;
        return true;
    }

    public bool TryActivate(Reservation reservation, Func<Boss2PartId, bool> isReady = null)
    {
        if (reservation == null || !reservations.Contains(reservation) ||
            reservation.State != Boss2ExecutionState.WaitingForParts) return false;

        for (int i = 0; i < owners.Length; i++)
        {
            Boss2Parts part = (Boss2Parts)(1 << i);
            if ((reservation.Parts & part) != 0 &&
                (owners[i] != null || (isReady != null && !isReady((Boss2PartId)i)))) return false;
            // A guard can pause lower-hand basics without taking those hands from another main pattern.
            if ((reservation.PauseBasicParts & part) != 0 && owners[i]?.Kind == Boss2PatternKind.Basic)
                return false;
        }

        for (int i = 0; i < owners.Length; i++)
        {
            if ((reservation.Parts & (Boss2Parts)(1 << i)) == 0) continue;
            nextOwners[i] = null;
            owners[i] = reservation;
        }
        reservation.State = Boss2ExecutionState.Running;
        return true;
    }

    public bool Owns(Reservation reservation, Boss2Parts parts) => reservation != null &&
        reservations.Contains(reservation) && reservation.State == Boss2ExecutionState.Running &&
        parts != Boss2Parts.None && (reservation.HeldParts & parts) == parts;

    public bool ReleaseParts(Reservation reservation, Boss2Parts parts)
    {
        if (!Owns(reservation, parts)) return false;
        for (int i = 0; i < owners.Length; i++)
            if ((parts & (Boss2Parts)(1 << i)) != 0) owners[i] = null;
        reservation.HeldParts &= ~parts;
        return true;
    }

    public void Release(Reservation reservation)
    {
        if (reservation == null || !reservations.Remove(reservation)) return;
        for (int i = 0; i < owners.Length; i++)
        {
            if (owners[i] == reservation) owners[i] = null;
            if (nextOwners[i] == reservation) nextOwners[i] = null;
        }
        reservation.HeldParts = Boss2Parts.None;
        reservation.State = Boss2ExecutionState.Finished;
    }
}
