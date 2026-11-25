using UnityEngine;

[DisallowMultipleComponent]
public sealed class Boss1CrushWarningArea : Boss1WarningAreaBase
{
    public void ShowAcross(Bounds attackBounds, float duration)
    {
        ShowArea(
            new Vector3(attackBounds.center.x, attackBounds.center.y, transform.position.z),
            attackBounds.size,
            duration
        );
    }
}
