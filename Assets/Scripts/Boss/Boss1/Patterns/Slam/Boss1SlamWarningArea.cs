using UnityEngine;

[DisallowMultipleComponent]
public sealed class Boss1SlamWarningArea : Boss1WarningAreaBase
{
    public void ShowOver(Collider2D floor, float height, float duration)
    {
        if (floor == null)
        {
            Hide();
            return;
        }

        Physics2D.SyncTransforms();
        Bounds bounds = floor.bounds;
        float warningHeight = Mathf.Max(0.01f, height);
        ShowArea(
            new Vector3(bounds.center.x, bounds.max.y + warningHeight * 0.5f, transform.position.z),
            new Vector2(bounds.size.x, warningHeight),
            duration,
            Boss1WarningVisual.WarningStyle.Ground
        );
    }
}
