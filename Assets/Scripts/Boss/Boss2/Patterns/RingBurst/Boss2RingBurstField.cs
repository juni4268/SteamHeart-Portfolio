using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>One immutable path graph shared by the warning and the faster travelling attack pulse.</summary>
[DisallowMultipleComponent]
public sealed class Boss2RingBurstField : MonoBehaviour
{
    [Serializable]
    private struct RayDefinition
    {
        [Range(-180f, 180f)] public float angle;
        [Range(1, 2)] public int reflections;

        public RayDefinition(float angle, int reflections)
        {
            this.angle = angle;
            this.reflections = reflections;
        }
    }

    [Serializable]
    private sealed class LayoutDefinition
    {
        public string label;
        public Vector2 ringOffset;
        public RayDefinition[] rays;

        public LayoutDefinition(string label, Vector2 ringOffset, params RayDefinition[] rays)
        {
            this.label = label;
            this.ringOffset = ringOffset;
            this.rays = rays;
        }
    }

    [SerializeField] private Boss2CannonBeamVisual beamPrefab;
    [Header("Ring Laser Attack")]
    [SerializeField, Min(0.01f)] private float beamWidth = 0.24f;
    [Tooltip("레이저 선두가 가장 긴 반사 경로 끝까지 도달하는 시간(초). 작을수록 빠르게 날아갑니다.")]
    [SerializeField, Min(0.01f)] private float attackTravelDuration = 0.35f;
    [UnityEngine.Serialization.FormerlySerializedAs("beamDuration")]
    [Tooltip("공격이 각 지점을 통과하며 피해를 주는 유지 시간(초). 클수록 날아가는 빔의 몸통이 길어집니다.")]
    [SerializeField, Min(0.01f)] private float attackPulseDuration = 0.15f;
    [Header("Layouts")]
    [SerializeField] private LayoutDefinition[] layouts =
    {
        new LayoutDefinition("A", Vector2.zero, new RayDefinition(-35f, 2),
            new RayDefinition(-80f, 2), new RayDefinition(-160f, 2), new RayDefinition(35f, 2)),
        new LayoutDefinition("B", Vector2.zero, new RayDefinition(-20f, 2),
            new RayDefinition(-110f, 2), new RayDefinition(-140f, 2), new RayDefinition(80f, 2)),
        new LayoutDefinition("C", Vector2.zero, new RayDefinition(-23f, 2),
            new RayDefinition(43f, 2), new RayDefinition(135f, 2), new RayDefinition(-164f, 2)),
        new LayoutDefinition("D", new Vector2(0f, -0.61f), new RayDefinition(-7.5f, 1),
            new RayDefinition(47f, 2), new RayDefinition(133f, 2), new RayDefinition(-172.5f, 1))
    };
    [Tooltip("Minimum distance between any two beam arrival points on the arena walls.")]
    [SerializeField, Min(0f)] private float minimumEndpointSpacing = 1.25f;
    [SerializeField] private Color warningColor = new Color(1f, 0.75f, 0.8f, 0.4f);
    [SerializeField] private Color burstColor = new Color(1f, 0.65f, 0.8f, 1f);
    [SerializeField] private LayerMask playerLayers;
    [SerializeField, Min(1)] private int damage = 1;
    [SerializeField] private Boss2RingBurstLaserEffect laserEffect;

    public readonly struct Segment
    {
        public readonly Vector2 Start;
        public readonly Vector2 End;
        public readonly float StartDistance;
        public float Length => Vector2.Distance(Start, End);
        public float EndDistance => StartDistance + Length;

        public Segment(Vector2 start, Vector2 end, float startDistance)
        {
            Start = start;
            End = end;
            StartDistance = startDistance;
        }
    }

    private const float SurfaceOffset = 0.002f;
    private readonly List<Segment> segments = new List<Segment>();
    private readonly List<BeamPresentation> beams = new List<BeamPresentation>();
    private readonly HashSet<Collider2D> walls = new HashSet<Collider2D>();
    private readonly HashSet<PlayerHealth> contacted = new HashSet<PlayerHealth>();
    private float rayDistance;
    private int wallLayers;
    private float depth;
    private bool burst;
    private float attackElapsed;
    private Boss2Audio audioOwner;
    private Boss2Audio.Playback audioPlayback;

    public void BindAudio(Boss2Audio audio) => audioOwner = audio;

    private sealed class BeamPresentation
    {
        public Boss2CannonBeamVisual Beam;
        public Animator Animator;
        public SpriteRenderer Renderer;
        public bool Fired;
        public bool DamageStarted;
        public bool Fading;
        public bool ImpactShown;
    }

    public IReadOnlyList<Segment> Segments => segments;
    public float MaximumDistance { get; private set; }
    public bool AttackCompleted { get; private set; }
    public bool Prepared
    {
        get
        {
            if (beams.Count == 0) return false;
            foreach (BeamPresentation beam in beams) if (!beam.Beam.AimCompleted) return false;
            return true;
        }
    }

    public int SelectLayout()
    {
        if (layouts == null || layouts.Length != 4)
            throw new InvalidOperationException("Assign all four ring burst layouts.");
        return UnityEngine.Random.Range(0, layouts.Length);
    }

    public Vector3 GetRingDestination(int layoutIndex, Vector3 baseDestination) =>
        baseDestination + (Vector3)GetLayout(layoutIndex).ringOffset;

    private LayoutDefinition GetLayout(int layoutIndex)
    {
        if (layouts == null || layouts.Length != 4 || layoutIndex < 0 || layoutIndex >= layouts.Length || layouts[layoutIndex] == null)
            throw new InvalidOperationException("Select one of the four assigned ring burst layouts.");
        LayoutDefinition layout = layouts[layoutIndex];
        if (layout.rays == null || layout.rays.Length != 4)
            throw new InvalidOperationException("Each ring burst layout requires exactly four authored rays.");
        foreach (RayDefinition ray in layout.rays)
            if (float.IsNaN(ray.angle) || float.IsInfinity(ray.angle) || ray.reflections < 1 || ray.reflections > 2)
                throw new InvalidOperationException("Each ring burst ray needs a finite angle and one or two reflections.");
        return layout;
    }

    public void Build(Vector3 origin, Collider2D[] boundaries, int sortingLayer, int sortingOrder, int layoutIndex)
    {
        if (segments.Count != 0 || beamPrefab == null || boundaries == null || boundaries.Length == 0)
            throw new InvalidOperationException("Assign the ring burst cannon beam prefab and closed arena boundary colliders.");
        RayDefinition[] rays = GetLayout(layoutIndex).rays;
        Bounds bounds = default;
        foreach (Collider2D boundary in boundaries)
        {
            if (boundary == null || !boundary.enabled || boundary.isTrigger || !boundary.gameObject.activeInHierarchy)
                throw new InvalidOperationException("Ring burst boundaries must be active, solid colliders.");
            if (walls.Count == 0) bounds = boundary.bounds;
            else bounds.Encapsulate(boundary.bounds);
            walls.Add(boundary);
            wallLayers |= 1 << boundary.gameObject.layer;
        }
        rayDistance = bounds.size.magnitude * 2f;
        depth = origin.z;
        foreach (RayDefinition ray in rays)
        {
            Vector2 start = origin;
            Vector2 direction = Rotate(Vector2.right, ray.angle);
            Vector2 normal = Vector2.zero;
            float distance = 0f;
            for (int reflection = 0; reflection <= ray.reflections; reflection++)
            {
                Vector2 castOrigin = start;
                if (reflection > 0) castOrigin += (direction + normal) * SurfaceOffset;
                RaycastHit2D hit = Trace(castOrigin, direction);
                if (!CanPlaceEndpoint(hit.point))
                    throw new InvalidOperationException("The authored ring burst arrivals are too close. Adjust the ray angles or ring destination.");
                Add(start, hit.point, distance);
                distance += Vector2.Distance(start, hit.point);
                start = hit.point;
                normal = hit.normal;
                direction = Vector2.Reflect(direction, normal).normalized;
            }
        }
        foreach (Segment segment in segments)
        {
            Boss2CannonBeamVisual beam = Instantiate(beamPrefab, transform);
            beam.BeginAim();
            beam.SetGeometry(AtDepth(segment.Start), (segment.End - segment.Start).normalized, 0f, beamWidth);
            SpriteRenderer renderer = beam.GetComponent<SpriteRenderer>();
            renderer.sortingLayerID = sortingLayer;
            renderer.sortingOrder = sortingOrder;
            renderer.color = warningColor;
            beams.Add(new BeamPresentation { Beam = beam, Animator = beam.GetComponent<Animator>(), Renderer = renderer });
        }
        if (laserEffect != null) laserEffect.Build(segments, depth, beamWidth, sortingLayer, sortingOrder + 1);
    }

    private bool CanPlaceEndpoint(Vector2 point)
    {
        float minimumSquared = minimumEndpointSpacing * minimumEndpointSpacing;
        foreach (Segment segment in segments)
            if ((segment.End - point).sqrMagnitude < minimumSquared) return false;
        return true;
    }

    private RaycastHit2D Trace(Vector2 origin, Vector2 direction)
    {
        foreach (RaycastHit2D hit in Physics2D.RaycastAll(origin, direction, rayDistance, wallLayers))
            if (walls.Contains(hit.collider) && hit.distance > SurfaceOffset * 0.1f) return hit;
        throw new InvalidOperationException("A ring burst ray escaped the assigned arena boundaries.");
    }

    private void Add(Vector2 start, Vector2 end, float distance)
    {
        var segment = new Segment(start, end, distance);
        segments.Add(segment);
        MaximumDistance = Mathf.Max(MaximumDistance, segment.EndDistance);
    }

    public void ShowWarning(float progress)
    {
        if (burst) return;
        float distance = Mathf.Clamp01(progress) * MaximumDistance;
        for (int i = 0; i < segments.Count; i++)
        {
            Segment segment = segments[i];
            float length = Mathf.Clamp(distance - segment.StartDistance, 0f, segment.Length);
            BeamPresentation presentation = beams[i];
            presentation.Beam.SetGeometry(AtDepth(segment.Start), (segment.End - segment.Start).normalized, length, beamWidth);
            presentation.Renderer.color = warningColor;
        }
    }

    public void Burst()
    {
        if (burst) return;
        if (!Prepared) throw new InvalidOperationException("The ring burst warning beams have not finished their preparation.");
        if (attackTravelDuration <= 0f || attackPulseDuration <= 0f)
            throw new InvalidOperationException("Ring burst attack travel and pulse durations must be positive.");
        burst = true;
        // Reflected segments form one attack, so share one voice instead of stacking each segment.
        audioPlayback = audioOwner != null
            ? audioOwner.Play(Boss2Audio.Sound.CannonBeam, loop: true, pitch: 0.8f)
            : null;
        if (laserEffect != null) laserEffect.Release();
        foreach (BeamPresentation presentation in beams) presentation.Renderer.enabled = false;
        TickAttack(0f);
    }

    public void TickAttack(float deltaTime)
    {
        if (!burst || AttackCompleted || !isActiveAndEnabled) return;
        float speed = MaximumDistance / attackTravelDuration;
        float previousTail = Mathf.Max(0f, attackElapsed - attackPulseDuration) * speed;
        attackElapsed += deltaTime;
        if (laserEffect != null) laserEffect.SetTime(attackElapsed);
        float head = attackElapsed * speed;
        float tail = Mathf.Max(0f, attackElapsed - attackPulseDuration) * speed;
        bool completed = true;
        for (int i = 0; i < segments.Count; i++)
        {
            if (!isActiveAndEnabled) return;
            Segment segment = segments[i];
            BeamPresentation presentation = beams[i];
            if (presentation.Beam.Interrupted || !presentation.Animator.isActiveAndEnabled)
                throw new InvalidOperationException("A ring burst attack beam was interrupted.");
            if (!presentation.Fired)
            {
                if (head < segment.StartDistance) { completed = false; continue; }
                presentation.Beam.BeginFire(segment.Length / speed + attackPulseDuration);
                presentation.Fired = true;
            }
            if (!presentation.Beam.FireEntered) { completed = false; continue; }
            Vector2 direction = (segment.End - segment.Start).normalized;
            float localTail = Mathf.Clamp(tail - segment.StartDistance, 0f, segment.Length);
            float localHead = Mathf.Clamp(head - segment.StartDistance, 0f, segment.Length);
            if (laserEffect != null)
            {
                laserEffect.DrawSegment(i, tail - segment.StartDistance, head - segment.StartDistance, speed);
                if (!presentation.ImpactShown && head >= segment.EndDistance)
                {
                    presentation.ImpactShown = true;
                    laserEffect.Impact(AtDepth(segment.End), direction);
                }
            }
            presentation.Beam.SetGeometry(AtDepth(segment.Start + direction * localTail), direction,
                localHead - localTail, beamWidth);
            presentation.Renderer.color = Color.Lerp(Color.white, burstColor,
                Mathf.Clamp01((head - segment.StartDistance) / (segment.Length + attackPulseDuration * speed)));

            // Sweep since the previous tick so a fast front cannot skip a player between frames.
            float damageStart = presentation.DamageStarted
                ? Mathf.Clamp(previousTail - segment.StartDistance, 0f, segment.Length) : 0f;
            presentation.DamageStarted = true;
            if (localHead > damageStart)
                DamageSegment(segment.Start + direction * damageStart, direction, localHead - damageStart);
            if (!isActiveAndEnabled) return;
            if (presentation.Beam.FireCompleted && !presentation.Fading)
            {
                presentation.Fading = true;
                presentation.Beam.BeginFade();
            }
            completed &= presentation.Beam.Completed;
        }
        AttackCompleted = completed;
        if (completed) audioPlayback?.FadeOut(0.1f);
        if (completed && laserEffect != null) laserEffect.Hide();
    }

    private void DamageSegment(Vector2 start, Vector2 direction, float length)
    {
        foreach (RaycastHit2D hit in Physics2D.CircleCastAll(start, beamWidth * 0.5f, direction, length, playerLayers))
        {
            PlayerHealth player = hit.collider.GetComponentInParent<PlayerHealth>();
            if (player != null && contacted.Add(player)) player.TryTakeDamage(new DamageInfo(damage, start));
            if (!isActiveAndEnabled) return;
        }
    }

    private void OnDisable()
    {
        audioPlayback?.FadeOut(0.1f);
        foreach (BeamPresentation presentation in beams) presentation.Beam.Cancel();
        if (laserEffect != null) laserEffect.Hide();
    }

    private Vector3 AtDepth(Vector2 point) => new Vector3(point.x, point.y, depth);
    private static Vector2 Rotate(Vector2 direction, float degrees) => Quaternion.Euler(0f, 0f, degrees) * direction;
}
