using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public sealed class Boss2CoreBurstBarrier : MonoBehaviour
{
    [Tooltip("플레이어 Collider의 중심이 이 범위 안에 있으면 Core Burst 피해를 막습니다.")]
    [SerializeField] private PolygonCollider2D protectedArea;
    [SerializeField] private string actionParameter = "BarrierAction";

    [Header("Flight")]
    [SerializeField, Min(0.01f)] private float flightDuration = 0.8f;
    [Tooltip("포물선이 출발점과 도착점을 잇는 직선보다 위로 솟는 높이입니다.")]
    [SerializeField, Min(0f)] private float arcHeight = 1.5f;

    private Animator animator;
    private bool activationRequested;
    private bool onEntered;
    private bool breakRequested;
    private bool breakCompleted;
    private bool takeRequested;
    private bool takeCompleted;
    private Vector3 flightStart;
    private Vector3 flightDestination;
    private float flightArcHeight;
    private float flightElapsed;
    private bool activateOnArrival;
    private Boss2Audio audioOwner;
    private Boss2Audio.Playback audioPlayback;

    public void BindAudio(Boss2Audio audio) => audioOwner = audio;
    public bool IsFlying { get; private set; }
    public bool Completed { get; private set; }
    public bool Interrupted { get; private set; }
    public bool IsStandby => isActiveAndEnabled && !activationRequested && !breakRequested &&
        !takeRequested && !Completed && !Interrupted;

    private bool CanProtect => isActiveAndEnabled && protectedArea != null && protectedArea.enabled &&
        onEntered && !IsFlying && !breakRequested && !takeRequested && !Interrupted;

    private void OnEnable()
    {
        animator = GetComponent<Animator>();
        bool found = false;
        foreach (var parameter in animator.parameters)
            if (parameter.name == actionParameter && parameter.type == AnimatorControllerParameterType.Int) found = true;
        if (protectedArea == null || !animator.isActiveAndEnabled || !found)
            throw new InvalidOperationException("Assign the barrier area, Animator and action Int parameter.");
        activationRequested = onEntered = breakRequested = breakCompleted = takeRequested = takeCompleted = Completed = Interrupted = false;
        IsFlying = false;
        animator.SetInteger(actionParameter, 0);
    }

    public void Launch(Vector3 origin, Vector2 horizontalBounds, BoxCollider2D ground, BoxCollider2D ceiling,
        bool activateWhenLanded = true)
    {
        if (activationRequested || IsFlying || takeRequested || Completed || Interrupted)
            throw new InvalidOperationException("Launch the barrier only from its standby pose.");
        if (ground == null || !ground.enabled || !ground.gameObject.activeInHierarchy ||
            ceiling == null || !ceiling.enabled || !ceiling.gameObject.activeInHierarchy || protectedArea == null)
            throw new InvalidOperationException("Assign the barrier polygon, horizontal landing ground and ceiling.");

        Bounds polygon = GetPolygonOffsets();
        Bounds floor = ground.bounds;
        float minX = Mathf.Max(horizontalBounds.x, floor.min.x) - polygon.min.x;
        float maxX = Mathf.Min(horizontalBounds.y, floor.max.x) - polygon.max.x;
        float minY = floor.max.y - polygon.min.y;
        float maxY = ceiling.bounds.min.y - polygon.max.y;
        if (minX > maxX || minY > maxY)
            throw new InvalidOperationException("The barrier polygon cannot fit inside the map and landing ground.");

        flightStart = origin;
        // Keep the full polygon inside the side boundaries throughout the flight as well.
        flightStart.x = Mathf.Clamp(flightStart.x, minX, maxX);
        flightStart.y = Mathf.Clamp(flightStart.y, minY, maxY);
        flightDestination = new Vector3(UnityEngine.Random.Range(minX, maxX),
            minY, origin.z);
        flightArcHeight = Mathf.Min(arcHeight, maxY - flightStart.y);
        flightElapsed = 0f;
        activateOnArrival = activateWhenLanded;
        transform.position = flightStart;
        IsFlying = true;
    }

    private Bounds GetPolygonOffsets()
    {
        var bounds = new Bounds();
        bool hasPoint = false;
        for (int path = 0; path < protectedArea.pathCount; path++)
        {
            foreach (Vector2 point in protectedArea.GetPath(path))
            {
                Vector3 offset = protectedArea.transform.TransformPoint(point + protectedArea.offset) - transform.position;
                if (!hasPoint) { bounds = new Bounds(offset, Vector3.zero); hasPoint = true; }
                else bounds.Encapsulate(offset);
            }
        }
        if (!hasPoint) throw new InvalidOperationException("The barrier protection polygon has no points.");
        return bounds;
    }

    private void Update()
    {
        if (HitStopManager.IsActive || Time.timeScale <= 0f) return;
        AdvanceFlight(Time.deltaTime);
    }

    private void AdvanceFlight(float deltaTime)
    {
        if (!IsFlying) return;
        flightElapsed += deltaTime;
        float progress = Mathf.Clamp01(flightElapsed / flightDuration);
        transform.position = Vector3.Lerp(flightStart, flightDestination, progress) +
            Vector3.up * (4f * flightArcHeight * progress * (1f - progress));
        if (progress < 1f) return;
        IsFlying = false;
        if (activateOnArrival) Activate();
    }

    public void Activate()
    {
        if (!isActiveAndEnabled || activationRequested || takeRequested || Completed || Interrupted) return;
        if (IsFlying) { activateOnArrival = true; return; }
        activationRequested = true;
        animator.SetInteger(actionParameter, 3);
    }

    public bool Protects(PlayerHealth player)
    {
        if (!CanProtect || player == null) return false;
        var collider = player.GetComponent<Collider2D>();
        return collider != null && collider.enabled && protectedArea.OverlapPoint(collider.bounds.center);
    }

    public void ReceiveCoreBurst()
    {
        if (!CanProtect) return;
        breakRequested = true;
        animator.SetInteger(actionParameter, 1);
    }

    public void Take()
    {
        if (!isActiveAndEnabled || Completed || Interrupted || takeRequested) return;
        IsFlying = false;
        takeRequested = true;
        if (!breakRequested || breakCompleted) animator.SetInteger(actionParameter, 2);
    }

    internal void Entered(Boss2CoreBurstBarrierState.Role role)
    {
        if (role == Boss2CoreBurstBarrierState.Role.On)
        {
            onEntered = true;
            audioPlayback?.FadeOut();
            audioPlayback = audioOwner != null ? audioOwner.Play(Boss2Audio.Sound.BarrierOn, fadeIn: 0.08f) : null;
        }
        else if (role == Boss2CoreBurstBarrierState.Role.Break || role == Boss2CoreBurstBarrierState.Role.Take)
            audioPlayback?.FadeOut();
    }

    internal void ReachedEnd(Boss2CoreBurstBarrierState.Role role)
    {
        if (Interrupted) return;
        if (role == Boss2CoreBurstBarrierState.Role.Break)
        {
            breakCompleted = true;
            if (takeRequested) animator.SetInteger(actionParameter, 2);
        }
        else if (role == Boss2CoreBurstBarrierState.Role.Take)
        {
            takeCompleted = true;
            animator.SetInteger(actionParameter, 4);
        }
        else if (role == Boss2CoreBurstBarrierState.Role.End) Completed = true;
    }

    internal void Exited(Boss2CoreBurstBarrierState.Role role)
    {
        if ((role == Boss2CoreBurstBarrierState.Role.Standby && !activationRequested && !takeRequested) ||
            (role == Boss2CoreBurstBarrierState.Role.On && !breakRequested && !takeRequested) ||
            (role == Boss2CoreBurstBarrierState.Role.Break && (!breakCompleted || !takeRequested)) ||
            (role == Boss2CoreBurstBarrierState.Role.Take && !takeCompleted) ||
            (role == Boss2CoreBurstBarrierState.Role.End && !Completed)) Interrupted = true;
    }

    private void LateUpdate()
    {
        if (Completed || Interrupted) Destroy(gameObject);
    }

    private void OnDisable()
    {
        audioPlayback?.FadeOut();
        audioPlayback = null;
        IsFlying = false;
        if (!Completed) Interrupted = true;
    }
}
