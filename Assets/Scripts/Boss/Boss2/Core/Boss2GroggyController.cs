using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Boss2Controller), typeof(Boss2BodyMovement))]
public sealed class Boss2GroggyController : MonoBehaviour
{
    [Serializable]
    private sealed class HandGroundBinding
    {
        public Boss2PartController part;
        public Transform groundPoint;
        public Transform leftEdge;
        public Transform rightEdge;
        public Boss2GroggyAnimation Animation { get; set; }
    }

    [SerializeField] private Boss2CoreBurstPattern coreBurst;
    [Tooltip("본체, 손 4개, 링의 그로기 애니메이션입니다.")]
    [SerializeField] private Boss2GroggyAnimation[] animations = Array.Empty<Boss2GroggyAnimation>();
    [SerializeField, Min(0f)] private float duration = 5f;
    [Header("Hand Landing")]
    [SerializeField] private LayerMask groundLayers;
    [SerializeField] private HandGroundBinding[] hands = Array.Empty<HandGroundBinding>();
    [Tooltip("착지한 손의 양쪽 끝 사이에 확보할 최소 간격입니다.")]
    [SerializeField, Min(0f)] private float handSpacing = 0.15f;
    [SerializeField, Min(0.1f)] private float fallSpeed = 4f;
    [SerializeField, Min(0.1f)] private float returnSpeed = 6f;

    private Boss2Controller owner;
    private bool pending;
    private bool triggered;
    private bool barrierDropped;
    private bool ending;
    private bool returning;
    private bool handsLowered;
    private float elapsed;
    private Boss2Audio.Playback audioPlayback;

    private void OnEnable()
    {
        owner = GetComponent<Boss2Controller>();
        owner.PhaseChanged += HandlePhaseChanged;
        owner.StateChanged += HandleStateChanged;
        HandlePhaseChanged(owner.CurrentPhase);
    }

    private void OnDisable()
    {
        if (owner == null) return;
        owner.PhaseChanged -= HandlePhaseChanged;
        owner.StateChanged -= HandleStateChanged;
        StopAnimations();
        owner.EndGroggy();
    }

    private void HandlePhaseChanged(Boss2Phase phase)
    {
        if (phase != Boss2Phase.Two || triggered) return;
        pending = true;
        TryStart();
    }

    private void TryStart()
    {
        if (!pending || triggered || owner.CurrentState != Boss2CombatState.Fighting) return;
        if (coreBurst == null || animations.Length != 6 || hands.Length != 4 || groundLayers.value == 0)
            throw new InvalidOperationException("Assign Core Burst, six groggy animations, four hand landing bindings and ground layers.");
        foreach (var hand in hands)
            if (hand.part == null || hand.groundPoint == null || hand.leftEdge == null || hand.rightEdge == null ||
                !hand.groundPoint.IsChildOf(hand.part.transform) || !hand.leftEdge.IsChildOf(hand.part.transform) ||
                !hand.rightEdge.IsChildOf(hand.part.transform))
                throw new InvalidOperationException("Assign each hand and its child groggy ground/edge points.");
        foreach (var animation in animations)
            if (animation == null || !animation.isActiveAndEnabled)
                throw new InvalidOperationException("Assign an active groggy animation to the body, four hands and ring.");
        foreach (var hand in hands)
        {
            hand.Animation = Array.Find(animations, animation => animation.transform.IsChildOf(hand.part.transform));
            if (hand.Animation == null)
                throw new InvalidOperationException("Each groggy hand must have its animation in the existing animation bindings.");
        }
        if (!owner.BeginGroggyReturn()) return;
        pending = false;
        triggered = true;
    }

    private void Update() => Tick(Time.deltaTime);

    private void Tick(float deltaTime)
    {
        if (HitStopManager.IsActive || Time.timeScale <= 0f) return;
        TryStart();
        if (owner.EnterGroggy())
        {
            elapsed = 0f;
            barrierDropped = ending = returning = false;
            foreach (var animation in animations) animation.Begin();
            LowerHands();
        }
        if (owner.CurrentState != Boss2CombatState.Groggy) return;
        foreach (var animation in animations)
        {
            if (!animation.isActiveAndEnabled || animation.Interrupted)
                throw new InvalidOperationException("A groggy animation was interrupted.");
            if (!animation.Entered) return;
        }
        if (!barrierDropped)
        {
            audioPlayback = GetComponent<Boss2Audio>()?.Play(Boss2Audio.Sound.Groggy,
                loop: true, fadeIn: 0.1f);
            coreBurst.DropBarrier();
            barrierDropped = true;
        }
        if (!returning)
        {
            elapsed += deltaTime;
            if (elapsed < duration || HandsMoving()) return;
            foreach (var hand in hands) hand.Animation.End();
            ReturnHands();
            returning = true;
        }
        if (HandsMoving()) return;
        if (!ending)
        {
            audioPlayback?.FadeOut(0.2f);
            foreach (var animation in animations) animation.End();
            ending = true;
        }
        foreach (var animation in animations) if (!animation.Completed) return;
        handsLowered = false;
        owner.EndGroggy();
        owner.TryRequestPattern(coreBurst, out _);
    }

    private void LowerHands()
    {
        Boss2PartId[] order = { Boss2PartId.UpperLeftHand, Boss2PartId.LowerLeftHand,
            Boss2PartId.LowerRightHand, Boss2PartId.UpperRightHand };
        var ordered = new HandGroundBinding[4];
        var widths = new float[4];
        var leftOffsets = new float[4];
        float totalWidth = handSpacing * 3f;
        for (int i = 0; i < order.Length; i++)
        {
            var hand = Array.Find(hands, h => h.part.PartId == order[i]);
            if (hand == null) throw new InvalidOperationException("Assign each of the four groggy hands exactly once.");
            ordered[i] = hand;
            float left = Mathf.Min(hand.leftEdge.position.x, hand.rightEdge.position.x);
            float right = Mathf.Max(hand.leftEdge.position.x, hand.rightEdge.position.x);
            leftOffsets[i] = left - hand.part.transform.position.x;
            widths[i] = right - left;
            totalWidth += widths[i];
        }
        Vector2 map = GetComponent<Boss2BodyMovement>().HorizontalMapBounds;
        float minX = Mathf.Min(map.x, map.y);
        float maxX = Mathf.Max(map.x, map.y);
        if (totalWidth > maxX - minX)
            throw new InvalidOperationException("The four groggy hands and their spacing cannot fit inside Map Bounds.");
        float cursor = Mathf.Clamp(owner.transform.position.x - totalWidth * 0.5f, minX, maxX - totalWidth);
        var destinations = new Vector3[4];
        float travelDuration = 0f;
        for (int i = 0; i < ordered.Length; i++)
        {
            var hand = ordered[i];
            Vector3 position = hand.part.transform.position;
            float destinationX = cursor - leftOffsets[i];
            Vector3 probe = hand.groundPoint.position + Vector3.right * (destinationX - position.x);
            RaycastHit2D ground = Physics2D.Raycast(probe, Vector2.down, Mathf.Infinity, groundLayers);
            if (ground.collider == null)
                throw new InvalidOperationException("No ground was found below a groggy hand.");
            destinations[i] = new Vector3(destinationX, position.y + ground.point.y - probe.y, position.z);
            travelDuration = Mathf.Max(travelDuration, Vector2.Distance(position, destinations[i]) / fallSpeed);
            cursor += widths[i] + handSpacing;
        }
        for (int i = 0; i < ordered.Length; i++) ordered[i].part.MoveToWorld(destinations[i], travelDuration);
        handsLowered = true;
    }

    private bool HandsMoving()
    {
        foreach (var hand in hands) if (hand.part.IsMoving) return true;
        return false;
    }

    private void ReturnHands()
    {
        foreach (var hand in hands)
            hand.part.ReturnToIdle(Vector2.Distance(hand.part.transform.position, hand.part.IdlePosition) / returnSpeed, true);
    }

    private void HandleStateChanged(Boss2CombatState previous, Boss2CombatState next)
    {
        if (next == Boss2CombatState.Dead || next == Boss2CombatState.Disabled || next == Boss2CombatState.Inactive)
            StopAnimations();
    }

    private void StopAnimations()
    {
        audioPlayback?.FadeOut(0.2f);
        audioPlayback = null;
        pending = false;
        foreach (var animation in animations) if (animation != null && animation.Active) animation.Cancel();
        if (handsLowered) { ReturnHands(); handsLowered = false; }
        if (coreBurst != null) coreBurst.RemovePreparedBarrier();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        foreach (var hand in hands)
        {
            if (hand.groundPoint == null) continue;
            Vector3 point = hand.groundPoint.position;
            Gizmos.DrawLine(point + Vector3.left * 0.12f, point + Vector3.right * 0.12f);
            Gizmos.DrawWireSphere(point, 0.025f);
            if (hand.leftEdge != null && hand.rightEdge != null)
                Gizmos.DrawLine(hand.leftEdge.position, hand.rightEdge.position);
        }
    }
}
