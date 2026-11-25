using System;

public enum Boss2PartId { UpperLeftHand, UpperRightHand, LowerLeftHand, LowerRightHand, Ring }

[Flags]
public enum Boss2Parts
{
    None = 0,
    UpperLeftHand = 1,
    UpperRightHand = 2,
    LowerLeftHand = 4,
    LowerRightHand = 8,
    Ring = 16,
    UpperHands = UpperLeftHand | UpperRightHand,
    LowerHands = LowerLeftHand | LowerRightHand,
    AllHands = UpperHands | LowerHands,
    All = AllHands | Ring
}

public enum Boss2PatternKind { Basic, Main }
public enum Boss2Phase { One, Two }
public enum Boss2CombatState { Inactive, Fighting, Dead, Disabled, ReturningForGroggy, Groggy }
public enum Boss2PatternResult { Completed, Cancelled, Failed }
public enum Boss2ExecutionState { WaitingForParts, Running, Finished }

public static class Boss2PartMask
{
    public static Boss2Parts FromId(Boss2PartId id) => (Boss2Parts)(1 << (int)id);
    public static bool IsValid(Boss2Parts parts) => (parts & ~Boss2Parts.All) == 0;
}
