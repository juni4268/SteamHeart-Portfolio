using UnityEngine;
using UnityEngine.Sprites;

/// <summary>Toggles a contour on the original sprite without adding another renderer.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class SpriteInteractionOutline : MonoBehaviour
{
    private static readonly int SpriteUVRect = Shader.PropertyToID("_SpriteUVRect");
    private static readonly int OutlineAmount = Shader.PropertyToID("_OutlineAmount");
    private SpriteRenderer spriteRenderer;
    private MaterialPropertyBlock properties;

    private void OnEnable() => SetFocused(false);

    private void OnDisable() => SetFocused(false);

    public void SetFocused(bool focused)
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (properties == null) properties = new MaterialPropertyBlock();
        spriteRenderer.GetPropertyBlock(properties);
        if (spriteRenderer.sprite != null)
            properties.SetVector(SpriteUVRect, DataUtility.GetOuterUV(spriteRenderer.sprite));
        properties.SetFloat(OutlineAmount, focused && isActiveAndEnabled ? 1f : 0f);
        spriteRenderer.SetPropertyBlock(properties);
    }
}
