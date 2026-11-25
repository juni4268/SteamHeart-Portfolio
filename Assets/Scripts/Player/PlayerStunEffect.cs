using UnityEngine;

// The marker follows one groggy control lock; it never owns player movement or timing.
public sealed class PlayerStunEffect : MonoBehaviour, System.IDisposable
{
    [SerializeField] private SpriteRenderer visual;
    [SerializeField, Min(0f)] private float headOffset = 0.13f;
    [SerializeField, Min(0.1f)] private float widthMultiplier = 2f;

    private Collider2D target;
    private PlayerMovement movement;
    private int knockbackVersion;
    private bool disposed;

    public void Bind(Collider2D collider, PlayerMovement owner, int version)
    {
        target = collider;
        movement = owner;
        knockbackVersion = version;
        SpriteRenderer character = owner.GetComponent<Player>().anim.GetComponent<SpriteRenderer>();
        visual.sortingLayerID = character.sortingLayerID;
        visual.sortingOrder = character.sortingOrder + 3;
        UpdatePosition();
    }

    private void LateUpdate()
    {
        if (movement == null || !movement.IsGroggy || movement.KnockbackVersion != knockbackVersion
            || target == null || !target.isActiveAndEnabled)
        {
            Dispose();
            return;
        }
        UpdatePosition();
    }

    private void UpdatePosition()
    {
        Bounds bounds = target.bounds;
        transform.position = new Vector3(bounds.center.x, bounds.max.y + headOffset, target.transform.position.z);
        float size = bounds.size.x * widthMultiplier / visual.sprite.bounds.size.x;
        Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        transform.localScale = new Vector3(size / Mathf.Abs(parentScale.x), size / Mathf.Abs(parentScale.y), 1f);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        gameObject.SetActive(false);
        Destroy(gameObject);
    }
}
