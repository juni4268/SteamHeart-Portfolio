using UnityEngine;

[DisallowMultipleComponent]
public sealed class Boss1DebrisWarningArea : Boss1WarningAreaBase
{
    public void ShowAt(float x, float topY, float bottomY, float z, float duration, float width)
    {
        float height = Mathf.Max(0.01f, topY - bottomY);
        Vector3 center = new Vector3(x, (topY + bottomY) * 0.5f, z);
        ShowArea(center, new Vector2(width, height), duration, Boss1WarningVisual.WarningStyle.Falling);
    }

    public void SetTargetX(float x)
    {
        Vector3 position = transform.position;
        position.x = x;
        transform.position = position;
    }
}
