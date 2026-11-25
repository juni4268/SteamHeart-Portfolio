using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BossHealthBarSweep : MaskableGraphic
{
    [SerializeField, Range(0.02f, 0.25f)] private float widthRatio = 0.08f;

    private float progress;

    public float Progress
    {
        get => progress;
        set
        {
            progress = Mathf.Clamp01(value);
            SetVerticesDirty();
        }
    }

    protected override void OnPopulateMesh(VertexHelper vertices)
    {
        vertices.Clear();
        Rect area = GetPixelAdjustedRect();
        if (area.width <= 0f || area.height <= 0f) return;

        float halfWidth = Mathf.Max(1f, area.width * widthRatio * 0.5f);
        float slant = area.height * 0.55f;
        float margin = halfWidth + slant * 0.5f;
        float center = Mathf.Lerp(area.xMin - margin, area.xMax + margin, progress);
        Color transparent = color;
        transparent.a = 0f;

        AddColumn(vertices, area, center - halfWidth, slant, transparent);
        AddColumn(vertices, area, center, slant, color);
        AddColumn(vertices, area, center + halfWidth, slant, transparent);
        for (int i = 0; i < 2; i++)
        {
            int start = i * 2;
            vertices.AddTriangle(start, start + 1, start + 3);
            vertices.AddTriangle(start + 3, start + 2, start);
        }
    }

    private static void AddColumn(VertexHelper vertices, Rect area, float x, float slant, Color color)
    {
        vertices.AddVert(new Vector3(x - slant * 0.5f, area.yMin), color, Vector2.zero);
        vertices.AddVert(new Vector3(x + slant * 0.5f, area.yMax), color, Vector2.zero);
    }
}
