using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public sealed class Boss2GravityFieldZone : MonoBehaviour
{
    [SerializeField] private string phaseParameter = "GravityFieldPhase";
    [SerializeField] private BoxCollider2D area;
    [SerializeField, Min(0.01f)] private float fallSpeed = 7f;
    [Tooltip("바닥에 닿을 접지 기준점입니다. Zone의 자식 오브젝트를 Scene 뷰에서 이동해 맞춥니다.")]
    [SerializeField] private Transform groundPoint;
    [SerializeField, Min(0f)] private float duration = 5f;
    [SerializeField, Range(0f, 1f)] private float movementMultiplier = 0.5f;
    [SerializeField] private Boss2GravityFieldVisual fieldVisual;

    private Animator animator;
    private Transform spawnPoint;
    private PlayerMovement player;
    private Collider2D playerCollider;
    private LayerMask groundLayers;
    private bool following;
    private bool detachPending;
    private bool falling;
    private bool landingRequested;
    private bool active;
    private bool applied;
    private float remaining;
    private Boss2Audio audioOwner;
    private Boss2Audio.Playback audioPlayback;

    public void BindAudio(Boss2Audio audio) => audioOwner = audio;
    public bool Detached { get; private set; }
    public bool Interrupted { get; private set; }

    public void Begin(Transform point, PlayerMovement target, LayerMask ground)
    {
        animator = GetComponent<Animator>();
        spawnPoint = point;
        player = target;
        playerCollider = target != null ? target.GetComponent<Collider2D>() : null;
        groundLayers = ground;
        if (spawnPoint == null || playerCollider == null || area == null || ground.value == 0 ||
            groundPoint == null || !groundPoint.IsChildOf(transform))
            throw new System.InvalidOperationException("Assign the Gravity Field spawn point, player, area, ground layers and a child ground point.");
        transform.position = spawnPoint.position;
        following = true;
        animator.SetInteger(phaseParameter, 1);
    }

    private void LateUpdate()
    {
        if (HitStopManager.IsActive || Time.timeScale <= 0f) return;
        if (following && spawnPoint != null) transform.position = spawnPoint.position;
        if (detachPending)
        {
            detachPending = false;
            following = false;
            Detached = true;
            falling = true;
        }
        if (falling)
        {
            float step = fallSpeed * Time.deltaTime;
            Vector3 contactPoint = groundPoint.position;
            RaycastHit2D hit = Physics2D.Raycast(contactPoint, Vector2.down, step, groundLayers);
            if (hit.collider != null)
            {
                transform.position += Vector3.up * (hit.point.y - contactPoint.y);
                falling = false;
                landingRequested = true;
                animator.SetInteger(phaseParameter, 3);
            }
            else transform.position += Vector3.down * step;
        }
        if (!active) return;
        bool inside = player != null && player.isActiveAndEnabled && playerCollider != null &&
            playerCollider.enabled && area.Distance(playerCollider).isOverlapped;
        if (inside || applied)
        {
            if (player != null) player.SetGravityField(this, movementMultiplier, inside);
            applied = inside;
        }
        remaining -= Time.deltaTime;
        if (fieldVisual != null) fieldVisual.SetRemaining(remaining);
        if (remaining <= 0f) Destroy(gameObject);
    }

    internal void Entered(Boss2GravityFieldZoneState.Phase phase)
    {
        if (phase == Boss2GravityFieldZoneState.Phase.Falling)
        {
            // LateUpdate samples the marker after the hand Animator applies this frame's keys.
            detachPending = true;
        }
        else if (phase == Boss2GravityFieldZoneState.Phase.Active)
        {
            audioPlayback?.FadeOut();
            audioPlayback = audioOwner != null
                ? audioOwner.Play(Boss2Audio.Sound.GravityField, loop: true, fadeIn: 0.2f)
                : null;
            active = true;
            remaining = duration;
            if (fieldVisual != null) fieldVisual.Show(duration);
        }
    }

    internal void Exited(Boss2GravityFieldZoneState.Phase phase)
    {
        if ((phase == Boss2GravityFieldZoneState.Phase.Falling && !landingRequested) ||
            (phase == Boss2GravityFieldZoneState.Phase.Active && active)) Interrupted = true;
    }

    private void OnDisable()
    {
        audioPlayback?.FadeOut(0.25f);
        audioPlayback = null;
        if (player != null) player.SetGravityField(this, movementMultiplier, false);
        if (fieldVisual != null) fieldVisual.Hide();
        applied = false;
    }

    private void OnDrawGizmosSelected()
    {
        if (groundPoint == null) return;
        Gizmos.color = Color.cyan;
        Vector3 point = groundPoint.position;
        Gizmos.DrawLine(point + Vector3.left * 0.25f, point + Vector3.right * 0.25f);
        Gizmos.DrawLine(point + Vector3.down * 0.05f, point + Vector3.up * 0.05f);
        Gizmos.DrawLine(transform.position, point);
    }
}
