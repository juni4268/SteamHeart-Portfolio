using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("UI/Workshop Inset Frame")]
[RequireComponent(typeof(CanvasRenderer))]
public sealed class WorkshopInsetFrame : MaskableGraphic
{
    [SerializeField, Min(0.1f)] private float lineWidth = 0.6f;
    [SerializeField, Min(0f)] private float cornerCut = 3f;
    [SerializeField] private Color surfaceColor = new Color(0.12f, 0.055f, 0.025f, 0.12f);

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect rect = GetPixelAdjustedRect();
        float cut = Mathf.Min(cornerCut, Mathf.Min(rect.width, rect.height) * 0.5f);
        Vector2[] points = {
            new Vector2(rect.xMin + cut, rect.yMax), new Vector2(rect.xMax - cut, rect.yMax),
            new Vector2(rect.xMax, rect.yMax - cut), new Vector2(rect.xMax, rect.yMin + cut),
            new Vector2(rect.xMax - cut, rect.yMin), new Vector2(rect.xMin + cut, rect.yMin),
            new Vector2(rect.xMin, rect.yMin + cut), new Vector2(rect.xMin, rect.yMax - cut)
        };
        mesh.AddVert(rect.center, surfaceColor, Vector2.zero);
        foreach (Vector2 point in points) mesh.AddVert(point, surfaceColor, Vector2.zero);
        for (int i = 0; i < points.Length; i++)
        {
            int next = (i + 1) % points.Length;
            mesh.AddTriangle(0, i + 1, next + 1);
            AddLine(mesh, points[i] + Vector2.down * 0.5f, points[next] + Vector2.down * 0.5f,
                new Color(0.1f, 0.05f, 0.02f, 0.6f));
            AddLine(mesh, points[i], points[next], color);
        }
    }

    private void AddLine(VertexHelper mesh, Vector2 from, Vector2 to, Color tint)
    {
        Vector2 direction = (to - from).normalized;
        Vector2 offset = new Vector2(-direction.y, direction.x) * (lineWidth * 0.5f);
        int start = mesh.currentVertCount;
        mesh.AddVert(from - offset, tint, Vector2.zero);
        mesh.AddVert(from + offset, tint, Vector2.zero);
        mesh.AddVert(to + offset, tint, Vector2.zero);
        mesh.AddVert(to - offset, tint, Vector2.zero);
        mesh.AddTriangle(start, start + 1, start + 2);
        mesh.AddTriangle(start, start + 2, start + 3);
    }
}
