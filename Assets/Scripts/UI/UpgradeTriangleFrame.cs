using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("UI/Upgrade Triangle Frame")]
[RequireComponent(typeof(CanvasRenderer))]
public sealed class UpgradeTriangleFrame : MaskableGraphic
{
    [SerializeField] private Color lineColor = new Color(0.85f, 0.65f, 0.36f);
    [SerializeField, Min(0.1f)] private float lineWidth = 1f;
    [SerializeField] private Color fillColor = new Color(0.95f, 0.65f, 0.22f, 0.22f);
    [SerializeField] private Color plotColor = new Color(1f, 0.83f, 0.42f);
    [SerializeField, Min(0.1f)] private float plotLineWidth = 0.9f;
    [SerializeField, Min(0f)] private float pointRadius = 0.8f;
    [SerializeField, Min(0f)] private float cornerRadius = 3f;
    [SerializeField] private Vector3 progress;
    private Vector3 targetProgress;
    private Tween fillTween;
    private int selectedAxis;
    private float EdgeFeather => 1f / Mathf.Max(1f, canvas != null ? canvas.rootCanvas.scaleFactor : 1f);

    public void SetSelectedAxis(int index)
    {
        index = Mathf.Clamp(index, 0, 2);
        if (selectedAxis == index) return;
        selectedAxis = index;
        SetVerticesDirty();
    }

    public void SetProgress(Vector3 value, bool animate = false)
    {
        value = new Vector3(Mathf.Clamp01(value.x), Mathf.Clamp01(value.y), Mathf.Clamp01(value.z));
        if (value == targetProgress && fillTween != null && fillTween.IsActive()) return;
        targetProgress = value;
        fillTween?.Kill();
        if (animate && Application.isPlaying && isActiveAndEnabled)
            fillTween = DOTween.To(() => progress, next => { progress = next; SetVerticesDirty(); },
                targetProgress, 0.3f).SetEase(Ease.OutCubic).SetUpdate(true);
        else
        {
            progress = targetProgress;
            SetVerticesDirty();
        }
    }

    protected override void OnDisable()
    {
        fillTween?.Kill();
        fillTween = null;
        progress = targetProgress;
        base.OnDisable();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect rect = GetPixelAdjustedRect();
        if (rect.width <= 6f || rect.height <= 6f) return;
        rect = new Rect(rect.x + 3f, rect.y + 3f, rect.width - 6f, rect.height - 6f);
        // Three equally spaced axes with a uniform scale, independent of the UI rectangle's aspect ratio.
        float radius = Mathf.Min(rect.width / Mathf.Sqrt(3f), rect.height / 1.5f);
        Vector2 center = rect.center + Vector2.down * (radius * 0.25f);
        Vector2 attack = center + new Vector2(-Mathf.Sqrt(3f) * 0.5f, -0.5f) * radius;
        Vector2 health = center + new Vector2(Mathf.Sqrt(3f) * 0.5f, -0.5f) * radius;
        Vector2 invincibility = center + Vector2.up * radius;
        Vector2[] outline = CreateOutline(new[] { attack, health, invincibility });
        AddPolygon(mesh, center, outline, color);
        Color guide = new Color(lineColor.r, lineColor.g, lineColor.b, 0.18f);
        for (int i = 1; i <= 3; i++)
        {
            float amount = i * 0.25f;
            Vector2[] ring = {
                Vector2.Lerp(center, attack, amount), Vector2.Lerp(center, health, amount),
                Vector2.Lerp(center, invincibility, amount)
            };
            AddOutline(mesh, ring, guide, 0.5f);
        }
        Vector2[] axes = { attack, health, invincibility };
        for (int i = 0; i < axes.Length; i++)
        {
            Vector2 start = center;
            Vector2 end = axes[i];
            ClipLine(ref start, ref end, outline);
            axes[i] = end;
            AddLine(mesh, center, end, new Color(lineColor.r, lineColor.g, lineColor.b, 0.4f), 0.65f);
        }
        Vector2[] values = {
            Vector2.Lerp(center, axes[0], progress.x), Vector2.Lerp(center, axes[1], progress.y),
            Vector2.Lerp(center, axes[2], progress.z)
        };
        if (Mathf.Abs(Cross(values[1] - values[0], values[2] - values[0])) > 0.001f)
            AddTriangle(mesh, values[0], values[1], values[2], fillColor);
        for (int i = 0; i < values.Length; i++)
        {
            Vector2 next = values[(i + 1) % values.Length];
            if ((next - values[i]).sqrMagnitude > 0.001f) AddLine(mesh, values[i], next, plotColor, plotLineWidth);
        }
        for (int i = 0; i < values.Length; i++)
            if (i != selectedAxis) AddPoint(mesh, values[i], plotColor, pointRadius);
        AddPoint(mesh, values[selectedAxis], Color.Lerp(plotColor, Color.white, 0.3f), pointRadius * 1.25f);
        AddOutline(mesh, outline, lineColor, lineWidth);
    }

    private Vector2[] CreateOutline(Vector2[] corners)
    {
        if (cornerRadius <= 0f) return corners;
        var outline = new Vector2[15];
        for (int i = 0; i < 3; i++)
        {
            Vector2 corner = corners[i];
            float radius = Mathf.Min(cornerRadius, 0.45f * Mathf.Min(
                Vector2.Distance(corner, corners[(i + 2) % 3]), Vector2.Distance(corner, corners[(i + 1) % 3])));
            Vector2 start = Vector2.MoveTowards(corner, corners[(i + 2) % 3], radius);
            Vector2 end = Vector2.MoveTowards(corner, corners[(i + 1) % 3], radius);
            for (int j = 0; j <= 4; j++)
            {
                float t = j / 4f;
                outline[i * 5 + j] = (1f - t) * (1f - t) * start
                    + 2f * (1f - t) * t * corner + t * t * end;
            }
        }
        return outline;
    }

    private void AddOutline(VertexHelper mesh, Vector2[] outline, Color tint, float width)
    {
        int start = mesh.currentVertCount;
        Color fade = new Color(tint.r, tint.g, tint.b, 0f);
        for (int i = 0; i < outline.Length; i++)
        {
            Vector2 incoming = (outline[i] - outline[(i + outline.Length - 1) % outline.Length]).normalized;
            Vector2 outgoing = (outline[(i + 1) % outline.Length] - outline[i]).normalized;
            Vector2 normal = new Vector2(-outgoing.y, outgoing.x);
            Vector2 join = new Vector2(-incoming.y, incoming.x) + normal;
            join.Normalize();
            float projection = Mathf.Max(0.1f, Vector2.Dot(join, normal));
            Vector2 offset = join * (width * 0.5f / projection);
            Vector2 feather = join * ((width * 0.5f + EdgeFeather) / projection);
            mesh.AddVert(outline[i] - feather, fade, Vector2.zero);
            mesh.AddVert(outline[i] - offset, tint, Vector2.zero);
            mesh.AddVert(outline[i] + offset, tint, Vector2.zero);
            mesh.AddVert(outline[i] + feather, fade, Vector2.zero);
        }
        // A shared strip keeps joins continuous; transparent edge strips provide screen-pixel antialiasing.
        for (int i = 0; i < outline.Length; i++)
        {
            int current = start + i * 4;
            int next = start + ((i + 1) % outline.Length) * 4;
            for (int strip = 0; strip < 3; strip++)
            {
                mesh.AddTriangle(current + strip, current + strip + 1, next + strip + 1);
                mesh.AddTriangle(current + strip, next + strip + 1, next + strip);
            }
        }
    }

    private void AddPoint(VertexHelper mesh, Vector2 point, Color tint, float radius)
    {
        if (radius <= 0f) return;
        const int segments = 16;
        int start = mesh.currentVertCount;
        mesh.AddVert(point, tint, Vector2.zero);
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            mesh.AddVert(point + direction * radius, tint, Vector2.zero);
            mesh.AddVert(point + direction * (radius + EdgeFeather), new Color(tint.r, tint.g, tint.b, 0f), Vector2.zero);
        }
        for (int i = 0; i < segments; i++)
        {
            int current = start + 1 + i * 2;
            int next = start + 1 + ((i + 1) % segments) * 2;
            mesh.AddTriangle(start, current, next);
            mesh.AddTriangle(current, current + 1, next + 1);
            mesh.AddTriangle(current, next + 1, next);
        }
    }

    private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

    private static bool ClipLine(ref Vector2 from, ref Vector2 to, Vector2[] outline)
    {
        for (int i = 0; i < outline.Length; i++)
        {
            Vector2 edgeStart = outline[i];
            Vector2 edge = outline[(i + 1) % outline.Length] - edgeStart;
            float a = Cross(edge, from - edgeStart);
            float b = Cross(edge, to - edgeStart);
            if (a < 0f && b < 0f) return false;
            if ((a < 0f) == (b < 0f)) continue;
            Vector2 intersection = Vector2.Lerp(from, to, a / (a - b));
            if (a < 0f) from = intersection;
            else to = intersection;
        }
        return true;
    }

    private static void AddPolygon(VertexHelper mesh, Vector2 center, Vector2[] points, Color tint)
    {
        for (int i = 0; i < points.Length; i++) AddTriangle(mesh, center, points[i], points[(i + 1) % points.Length], tint);
    }

    private static void AddTriangle(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Color tint)
    {
        int start = mesh.currentVertCount;
        mesh.AddVert(a, tint, Vector2.zero);
        mesh.AddVert(b, tint, Vector2.zero);
        mesh.AddVert(c, tint, Vector2.zero);
        mesh.AddTriangle(start, start + 1, start + 2);
    }

    private void AddLine(VertexHelper mesh, Vector2 from, Vector2 to, Color tint, float width)
    {
        float length = Vector2.Distance(from, to);
        if (length <= Mathf.Epsilon) return;
        Vector2 direction = (to - from) / length;
        Vector2 normal = new Vector2(-direction.y, direction.x);
        float feather = EdgeFeather;
        float[] along = { -feather, 0f, length, length + feather };
        float[] across = { -width * 0.5f - feather, -width * 0.5f, width * 0.5f, width * 0.5f + feather };
        int start = mesh.currentVertCount;
        for (int row = 0; row < 4; row++)
        for (int column = 0; column < 4; column++)
        {
            Color vertexColor = tint;
            if (row == 0 || row == 3 || column == 0 || column == 3) vertexColor.a = 0f;
            mesh.AddVert(from + direction * along[row] + normal * across[column], vertexColor, Vector2.zero);
        }
        for (int row = 0; row < 3; row++)
        for (int column = 0; column < 3; column++)
        {
            int vertex = start + row * 4 + column;
            mesh.AddTriangle(vertex, vertex + 1, vertex + 5);
            mesh.AddTriangle(vertex, vertex + 5, vertex + 4);
        }
    }
}
