using UnityEngine;

/// <summary>Offsets a face or eye toward a target within a local ellipse.</summary>
[DisallowMultipleComponent]
public sealed class Boss1LookAtTarget : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform target;
    [SerializeField] private BossHealth bossHealth;

    [Header("Local Offset")]
    [Tooltip("부모 Transform을 기준으로 한 기본 위치입니다. Scene View에서 조절할 수 있습니다.")]
    [SerializeField] private Vector3 baseLocalPosition;
    [Tooltip("기본 위치에서 좌우로 이동할 수 있는 최대 거리(로컬 단위)입니다.")]
    [SerializeField, Min(0f)] private float maxOffsetX = 0.15f;
    [Tooltip("기본 위치에서 상하로 이동할 수 있는 최대 거리(로컬 단위)입니다.")]
    [SerializeField, Min(0f)] private float maxOffsetY = 0.08f;
    [Tooltip("Offset이 목표 위치를 따라가는 속도(로컬 단위/초)입니다.")]
    [SerializeField, Min(0f)] private float followSpeed = 0.8f;
    [Tooltip("플레이어가 이 거리만큼 떨어지면 타원 경계까지 이동합니다(월드 단위).")]
    [SerializeField, Min(0.01f)] private float fullOffsetDistance = 5f;

    private SpriteRenderer spriteRenderer;
    private Boss1BodyAnimationController bodyAnimation;

    public Vector3 BaseLocalPosition => baseLocalPosition;
    public Vector2 MaxOffset => new Vector2(maxOffsetX, maxOffsetY);
    public Vector2 CurrentOffset => (Vector2)(transform.localPosition - baseLocalPosition);

    private void Reset()
    {
        baseLocalPosition = transform.localPosition;
        bossHealth = GetComponentInParent<BossHealth>();
    }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        bodyAnimation = GetComponentInParent<Boss1BodyAnimationController>();
        transform.localPosition = baseLocalPosition;
    }

    private void LateUpdate()
    {
        // Groggy and death animate the visible head; keep its artwork and hitbox at rest.
        if ((spriteRenderer != null && !spriteRenderer.enabled)
            || (bodyAnimation != null && bodyAnimation.IsGroggy)
            || (bossHealth != null && bossHealth.IsDead))
        {
            transform.localPosition = baseLocalPosition;
            return;
        }

        Vector2 desiredOffset = Vector2.zero;
        if (target != null && target.gameObject.activeInHierarchy)
        {
            Transform parent = transform.parent;
            Vector3 origin = parent != null ? parent.TransformPoint(baseLocalPosition) : baseLocalPosition;
            Vector3 direction = target.position - origin;
            direction.z = 0f;
            float strength = Mathf.Clamp01(direction.magnitude / fullOffsetDistance);
            Vector3 localDirection = parent != null ? parent.InverseTransformVector(direction) : direction;
            Vector2 unitDirection = ((Vector2)localDirection).normalized;
            desiredOffset = Vector2.Scale(unitDirection, MaxOffset) * strength;
        }

        Vector2 offset = Vector2.MoveTowards(ClampOffset(CurrentOffset), desiredOffset, followSpeed * Time.deltaTime);
        transform.localPosition = baseLocalPosition + new Vector3(offset.x, offset.y, 0f);
    }

    public Vector2 ClampOffset(Vector2 offset)
    {
        Vector2 normalized = new Vector2(maxOffsetX > 0f ? offset.x / maxOffsetX : 0f,
            maxOffsetY > 0f ? offset.y / maxOffsetY : 0f);
        return Vector2.Scale(Vector2.ClampMagnitude(normalized, 1f), MaxOffset);
    }

    private void OnDisable() => transform.localPosition = baseLocalPosition;

    private void OnValidate()
    {
        maxOffsetX = Mathf.Max(0f, maxOffsetX);
        maxOffsetY = Mathf.Max(0f, maxOffsetY);
        followSpeed = Mathf.Max(0f, followSpeed);
        fullOffsetDistance = Mathf.Max(0.01f, fullOffsetDistance);
    }
}
