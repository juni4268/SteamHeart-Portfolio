using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class MouseBodyGraphic : MaskableGraphic
{
    [SerializeField] private Color borderColor = new Color(0.56f, 0.47f, 0.34f);
    private static readonly Vector2[] Curve = {
        new Vector2(0f, 0.5f), new Vector2(-0.43f, 0.5f), new Vector2(-0.48f, 0.44f), new Vector2(-0.48f, 0.05f),
        new Vector2(-0.5f, -0.31f), new Vector2(-0.43f, -0.5f), new Vector2(0f, -0.5f),
        new Vector2(0.43f, -0.5f), new Vector2(0.5f, -0.31f), new Vector2(0.48f, 0.05f),
        new Vector2(0.48f, 0.44f), new Vector2(0.43f, 0.5f), new Vector2(0f, 0.5f)
    };

    protected override void OnPopulateMesh(VertexHelper helper)
    {
        helper.Clear();
        DrawBody(helper, 1f, Vector2.down * 0.8f, new Color(0.31f, 0.25f, 0.18f, color.a));
        DrawBody(helper, 1f, Vector2.zero, borderColor);
        DrawBody(helper, 0.96f, Vector2.zero, Color.Lerp(color, borderColor, 0.22f));
        DrawBody(helper, 0.94f, Vector2.up * 0.6f, color);
    }

    private void DrawBody(VertexHelper helper, float scale, Vector2 offset, Color fill)
    {
        Rect rect = rectTransform.rect;
        int center = helper.currentVertCount;
        helper.AddVert(rect.center + offset, fill, Vector2.zero);
        const int steps = 12;
        for (int curve = 0; curve < 4; curve++)
            for (int step = 0; step < steps; step++)
            {
                float t = (float)step / steps, u = 1f - t;
                int index = curve * 3;
                Vector2 point = u * u * u * Curve[index] + 3f * u * u * t * Curve[index + 1]
                    + 3f * u * t * t * Curve[index + 2] + t * t * t * Curve[index + 3];
                point = rect.center + offset + Vector2.Scale(point * scale, rect.size);
                helper.AddVert(point, fill, Vector2.zero);
            }
        for (int i = 0; i < 4 * steps; i++)
            helper.AddTriangle(center, center + 1 + i, center + 1 + (i + 1) % (4 * steps));
    }
}
