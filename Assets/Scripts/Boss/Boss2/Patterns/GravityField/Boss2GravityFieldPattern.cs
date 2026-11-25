using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class Boss2GravityFieldPattern : Boss2PatternBase
{
    [SerializeField] private PlayerMovement target;
    [SerializeField] private Boss2GravityFieldZone zonePrefab;
    [SerializeField] private LayerMask groundLayers;

    public override Boss2PatternKind Kind => Boss2PatternKind.Main;
    public override Boss2Parts RequiredParts
    {
        get
        {
            Boss2PartId first = UnityEngine.Random.value < 0.5f ? Boss2PartId.LowerLeftHand : Boss2PartId.LowerRightHand;
            Boss2PartId second = first == Boss2PartId.LowerLeftHand ? Boss2PartId.LowerRightHand : Boss2PartId.LowerLeftHand;
            if (CanReserve(first)) return Boss2PartMask.FromId(first);
            return CanReserve(second) ? Boss2PartMask.FromId(second) : Boss2Parts.None;
        }
    }

    private bool CanReserve(Boss2PartId id)
    {
        if (!Owner.HasActiveParts(Boss2PartMask.FromId(id))) return false;
        var current = Owner.PartUsage.GetOwner(id);
        return Owner.PartUsage.GetReservation(id) == null && (current == null || current.Kind == Boss2PatternKind.Basic);
    }

    public override Boss2PatternExecution CreateExecution(Boss2PatternContext context)
    {
        Boss2PartId id = (context.HeldParts & Boss2Parts.LowerLeftHand) != 0
            ? Boss2PartId.LowerLeftHand : Boss2PartId.LowerRightHand;
        var hand = context.GetPart(id).GetComponentInChildren<Boss2GravityFieldHandAnimation>(true);
        if (hand == null || hand.ZoneSpawnPoint == null || target == null || zonePrefab == null || groundLayers.value == 0)
            throw new InvalidOperationException("Assign both Gravity Field hand animations, Zone prefab, player and ground layers.");
        return new Execution(this, hand);
    }

    private sealed class Execution : Boss2PatternExecution
    {
        private readonly Boss2GravityFieldPattern pattern;
        private readonly Boss2GravityFieldHandAnimation hand;
        private Boss2GravityFieldZone zone;

        public Execution(Boss2GravityFieldPattern pattern, Boss2GravityFieldHandAnimation hand)
        {
            this.pattern = pattern;
            this.hand = hand;
        }

        public override IEnumerator Execute(Boss2PatternContext context)
        {
            zone = UnityEngine.Object.Instantiate(pattern.zonePrefab);
            zone.BindAudio(context.Controller.GetComponent<Boss2Audio>());
            zone.Begin(hand.ZoneSpawnPoint, pattern.target, pattern.groundLayers);
            hand.Begin();
            while (!hand.Completed || !zone.Detached)
            {
                if (hand.Interrupted || !hand.isActiveAndEnabled || zone == null || zone.Interrupted)
                    throw new InvalidOperationException("The Gravity Field animation was interrupted.");
                yield return null;
            }
        }

        public override void StopForDeath(Boss2PatternContext context)
        {
            hand.Cancel();
            if (zone != null) UnityEngine.Object.Destroy(zone.gameObject);
            zone = null;
        }

        public override IEnumerator ReturnAfterDeath(Boss2PatternContext context) { yield break; }

        public override void Cleanup(Boss2PatternResult result)
        {
            if (result == Boss2PatternResult.Completed) return;
            hand.Cancel();
            if (zone != null) UnityEngine.Object.Destroy(zone.gameObject);
        }
    }
}
