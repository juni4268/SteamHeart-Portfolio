using System.Collections.Generic;
using UnityEngine;

public enum Boss1DebrisState { Falling, Landed, Flying }

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer), typeof(Rigidbody2D))]
public sealed class Boss1FallingDebris : MonoBehaviour
{
    private const int MaximumStackCount = 2;

    [Header("Appearance")]
    [Tooltip("생성할 때 한 번 선택할 잔해 그림입니다. 크기와 Collider 설정은 변경하지 않습니다.")]
    [SerializeField] private Sprite[] appearanceSprites = System.Array.Empty<Sprite>();

    // Appearance choices must not change gameplay's drop-position random sequence.
    private static readonly System.Random AppearanceRandom = new System.Random();

    private BoxCollider2D body;
    private Rigidbody2D physicsBody;
    private bool touchingPlayer;
    private bool separatingProtectedPlayer;
    private Collider2D separationCollider;
    private bool restorePlayerCollision;
    private float separationDirection;
    private readonly List<RaycastHit2D> contactHits = new List<RaycastHit2D>();
    private readonly List<RaycastHit2D> groundHits = new List<RaycastHit2D>();
    private readonly List<RaycastHit2D> separationHits = new List<RaycastHit2D>();

    public Boss1DebrisState State { get; private set; }
    public BoxCollider2D Body => body;
    public bool CanDealDamage => State == Boss1DebrisState.Falling;
    public Vector2 WorldSize => Vector2.Scale(GetComponent<BoxCollider2D>().size,
        new Vector2(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y)));

    private void Awake()
    {
        body = GetComponent<BoxCollider2D>();
        physicsBody = GetComponent<Rigidbody2D>();
        if (appearanceSprites.Length > 0)
            GetComponent<SpriteRenderer>().sprite = appearanceSprites[AppearanceRandom.Next(appearanceSprites.Length)];
    }

    public void BeginFalling(Collider2D playerCollider)
    {
        EndProtectedSeparation();
        GetComponent<SpriteRenderer>().enabled = true;
        body.enabled = true;
        body.isTrigger = false;
        body.usedByEffector = false;
        PlatformEffector2D effector = GetComponent<PlatformEffector2D>();
        if (effector != null) effector.enabled = false;
        // Landing is swept explicitly; physical contacts are reserved for the player.
        int playerMask = 1 << playerCollider.gameObject.layer;
        body.includeLayers = playerMask;
        body.excludeLayers = ~playerMask;
        physicsBody.bodyType = RigidbodyType2D.Kinematic;
        physicsBody.gravityScale = 0f;
        physicsBody.freezeRotation = true;
        physicsBody.linearVelocity = Vector2.zero;
        touchingPlayer = false;
        State = Boss1DebrisState.Falling;
    }

    public void Advance(float deltaTime, float speed, Collider2D floor, out bool breakOnLanding, out Vector2 landingPoint)
    {
        breakOnLanding = false;
        landingPoint = default;
        if (State != Boss1DebrisState.Falling) return;

        float distance = Mathf.Max(0f, speed) * deltaTime;
        bool landed = false;
        Physics2D.SyncTransforms();
        ContactFilter2D filter = new ContactFilter2D();
        int landingMask = 1 << gameObject.layer;
        if (floor != null && floor.enabled) landingMask |= 1 << floor.gameObject.layer;
        filter.SetLayerMask(landingMask);
        filter.useTriggers = false;
        Bounds landingBounds = body.bounds;
        Physics2D.BoxCast(landingBounds.center, landingBounds.size, 0f, Vector2.down,
            filter, groundHits, distance + Physics2D.defaultContactOffset);
        foreach (RaycastHit2D hit in groundHits)
        {
            if (hit.collider == body || hit.normal.y < 0.5f) continue;
            Boss1FallingDebris support = hit.collider.GetComponent<Boss1FallingDebris>();
            if (hit.collider != floor && support == null) continue;
            distance = Mathf.Clamp(hit.distance - Physics2D.defaultContactOffset, 0f, distance);
            landed = true;
            break;
        }
        Vector2 position = physicsBody.position + Vector2.down * distance;
        landingBounds.center += Vector3.down * distance;
        if (landed) landingPoint = new Vector2(landingBounds.center.x, landingBounds.min.y);
        if (landed && ExceedsStackLimit(landingBounds, floor))
        {
            // Place the break effect at the collision, before the field removes this piece.
            transform.position = new Vector3(position.x, position.y, transform.position.z);
            physicsBody.position = position;
            breakOnLanding = true;
            return;
        }
        physicsBody.MovePosition(position);
        if (landed) Land();
    }

    private bool ExceedsStackLimit(Bounds landingBounds, Collider2D floor)
    {
        float tolerance = Mathf.Max(Physics2D.defaultContactOffset * 3f, 0.05f);
        return floor != null && landingBounds.min.y > floor.bounds.max.y
            + landingBounds.size.y * (MaximumStackCount - 1) + tolerance;
    }

    public bool BeginLaunch()
    {
        if (State != Boss1DebrisState.Landed) return false;
        State = Boss1DebrisState.Flying;
        touchingPlayer = false;
        physicsBody.linearVelocity = Vector2.zero;
        // Flight contacts are swept explicitly, so other bodies cannot stop the debris.
        body.isTrigger = true;
        EndProtectedSeparation();
        return true;
    }

    public void MoveLaunched(Vector2 movement)
    {
        if (State != Boss1DebrisState.Flying) return;
        Vector2 position = (Vector2)transform.position + movement;
        // Keep the final position when wall arrival deactivates the body in the same frame.
        transform.position = new Vector3(position.x, position.y, transform.position.z);
        physicsBody.position = position;
    }

    // False pauses the fall when neither side has enough space to separate a protected player.
    public bool CheckPlayerContact(PlayerHealth player, Collider2D playerCollider,
        float movementDistance, int damage, float knockbackSpeed,
        float knockbackLift, float knockbackDuration)
    {
        if (player == null || player.IsDead || playerCollider == null || !playerCollider.enabled)
        {
            EndProtectedSeparation();
            return true;
        }
        if (!CanDealDamage && !separatingProtectedPlayer) return true;
        Physics2D.SyncTransforms();
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(1 << playerCollider.gameObject.layer);
        filter.useTriggers = false;
        Physics2D.BoxCast(body.bounds.center, body.bounds.size, 0f, Vector2.down,
            filter, contactHits, movementDistance + Physics2D.defaultContactOffset);
        ColliderDistance2D playerDistance = body.Distance(playerCollider);
        bool contact = playerDistance.isOverlapped;
        foreach (RaycastHit2D hit in contactHits)
            if (hit.collider == playerCollider) contact = true;

        // Preserve the existing safe top surface while the debris is falling.
        bool standingOnTop = IsStandingOnTop(playerCollider);
        // A conservative BoxCast can still report contact after the horizontal separation is complete.
        // Stop here rather than turning back toward an already cleared edge.
        Bounds playerBounds = playerCollider.bounds;
        Bounds debrisBounds = body.bounds;
        bool clearToSide = playerDistance.isValid && !playerDistance.isOverlapped
            && playerDistance.distance >= Physics2D.defaultContactOffset * 0.5f
            && (playerBounds.min.x >= debrisBounds.max.x + Physics2D.defaultContactOffset
                || playerBounds.max.x <= debrisBounds.min.x - Physics2D.defaultContactOffset);
        if (!contact || standingOnTop || clearToSide)
        {
            touchingPlayer = false;
            EndProtectedSeparation();
            return true;
        }

        // Keep this contact protected after Counter consumes its counter window.
        separatingProtectedPlayer |= player.IsInvincible;
        bool canAdvance = !separatingProtectedPlayer || TrySeparatePlayer(player, playerCollider);
        if (!canAdvance) physicsBody.linearVelocity = Vector2.zero;
        if (CanDealDamage && !touchingPlayer)
        {
            float offsetX = playerCollider.bounds.center.x - body.bounds.center.x;
            float direction = Mathf.Abs(offsetX) > 0.01f ? Mathf.Sign(offsetX)
                : (player.transform.localScale.x >= 0f ? -1f : 1f);
            Vector2 velocity = new Vector2(direction * Mathf.Max(0f, knockbackSpeed), Mathf.Max(0f, knockbackLift));
            player.TryTakeDamage(new DamageInfo(damage, transform.position, velocity, knockbackDuration));
        }
        touchingPlayer = true;
        return canAdvance;
    }

    private bool TrySeparatePlayer(PlayerHealth player, Collider2D playerCollider)
    {
        Player controller = player.GetComponent<Player>();
        Rigidbody2D playerBody = playerCollider.attachedRigidbody;
        if (controller == null || controller.Data == null || playerBody == null) return false;

        Bounds debrisBounds = body.bounds;
        Bounds playerBounds = playerCollider.bounds;
        // Leave space for both collider skins and a positive gap before restoring collision.
        float clearance = Physics2D.defaultContactOffset * 3f;
        float leftOffset = debrisBounds.min.x - playerBounds.max.x - clearance;
        float rightOffset = debrisBounds.max.x - playerBounds.min.x + clearance;
        PlayerDash dash = player.GetComponent<PlayerDash>();
        bool preferRight = separationDirection != 0f ? separationDirection > 0f
            : (dash != null && dash.IsDashing ? dash.Direction > 0f : rightOffset < -leftOffset);
        float step = Mathf.Max(0f, controller.Data.WalkSpeed) * Time.fixedDeltaTime;
        if (step <= 0f) return false;
        return TryMovePlayerSideways(playerBody, playerCollider, preferRight ? rightOffset : leftOffset, step)
            || TryMovePlayerSideways(playerBody, playerCollider, preferRight ? leftOffset : rightOffset, step);
    }

    private bool TryMovePlayerSideways(Rigidbody2D playerBody, Collider2D playerCollider, float offset, float step)
    {
        float direction = Mathf.Sign(offset);
        float distance = Mathf.Abs(offset);
        ContactFilter2D filter = new ContactFilter2D { useTriggers = false };
        int collisionMask = Physics2D.GetLayerCollisionMask(playerCollider.gameObject.layer);
        filter.SetLayerMask((collisionMask | playerCollider.includeLayers.value) & ~playerCollider.excludeLayers.value);
        playerCollider.Cast(Vector2.right * direction, filter, separationHits,
            distance + Physics2D.defaultContactOffset);
        foreach (RaycastHit2D hit in separationHits)
        {
            Collider2D obstacle = hit.collider;
            if (obstacle == body || obstacle.attachedRigidbody == playerCollider.attachedRigidbody
                || Physics2D.GetIgnoreCollision(playerCollider, obstacle)) continue;
            // Supporting ground must not block a horizontal separation at floor level.
            if (obstacle.bounds.max.y <= playerCollider.bounds.min.y + Physics2D.defaultContactOffset) continue;
            if (hit.normal.x * direction < -0.01f) return false;
        }

        if (separationCollider == null)
        {
            separationCollider = playerCollider;
            restorePlayerCollision = !Physics2D.GetIgnoreCollision(body, playerCollider);
            if (restorePlayerCollision) Physics2D.IgnoreCollision(body, playerCollider, true);
        }
        separationDirection = direction;
        // Use physical movement at WalkSpeed, not an instant position correction or damage knockback.
        playerBody.MovePosition(playerBody.position + Vector2.right * direction * Mathf.Min(distance, step));
        return true;
    }

    private void EndProtectedSeparation()
    {
        if (restorePlayerCollision && separationCollider != null && body != null)
            Physics2D.IgnoreCollision(body, separationCollider, false);
        separationCollider = null;
        restorePlayerCollision = false;
        separationDirection = 0f;
        separatingProtectedPlayer = false;
    }

    private void OnDisable() => EndProtectedSeparation();

    private bool IsStandingOnTop(Collider2D playerCollider)
    {
        Bounds debrisBounds = body.bounds;
        Bounds playerBounds = playerCollider.bounds;
        bool horizontalOverlap = playerBounds.max.x > debrisBounds.min.x
            && playerBounds.min.x < debrisBounds.max.x;
        float tolerance = Mathf.Max(Physics2D.defaultContactOffset * 3f, 0.05f);
        return horizontalOverlap && playerBounds.min.y >= debrisBounds.max.y - tolerance;
    }

    private void Land()
    {
        State = Boss1DebrisState.Landed;
        // Separation may outlast the fall; keep its collision exemption until the player is clear.
        if (!separatingProtectedPlayer) touchingPlayer = false;
        physicsBody.linearVelocity = Vector2.zero;
        body.usedByEffector = false;
        PlatformEffector2D effector = GetComponent<PlatformEffector2D>();
        if (effector != null) effector.enabled = false;
    }
}
