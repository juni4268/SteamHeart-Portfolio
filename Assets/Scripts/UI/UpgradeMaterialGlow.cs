using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("UI/Upgrade Material Glow")]
public sealed class UpgradeMaterialGlow : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        const int segments = 32;
        mesh.Clear();
        Rect rect = GetPixelAdjustedRect();
        mesh.AddVert(rect.center, color, Vector2.zero);
        for (int ring = 0; ring < 2; ring++)
        {
            float radius = ring == 0 ? 0.5f : 1f;
            Color tint = color;
            tint.a *= ring == 0 ? 0.3f : 0f;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                mesh.AddVert(rect.center + new Vector2(Mathf.Cos(angle) * rect.width,
                    Mathf.Sin(angle) * rect.height) * (radius * 0.5f), tint, Vector2.zero);
            }
        }
        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            mesh.AddTriangle(0, 1 + i, 1 + next);
            mesh.AddTriangle(1 + i, 1 + segments + i, 1 + segments + next);
            mesh.AddTriangle(1 + i, 1 + segments + next, 1 + next);
        }
    }
}
