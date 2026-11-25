using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("UI/Control Button Graphic")]
public sealed class ControlButtonGraphic : Image
{
    public enum ButtonShape { Keycap, MouseLeft, MouseRight, Wheel, Side, Chassis, Circle, Gamepad }
    [SerializeField] private ButtonShape shape;

    public ButtonShape Shape
    {
        get => shape;
        set { shape = value; SetVerticesDirty(); }
    }

    protected override void OnPopulateMesh(VertexHelper helper)
    {
        helper.Clear();
        Rect rect = rectTransform.rect;
        Color edge = Color.Lerp(color, new Color(0.48f, 0.39f, 0.27f, color.a), 0.6f);
        Draw(helper, rect, edge);
        float border = shape == ButtonShape.Gamepad ? 1.3f : shape == ButtonShape.Chassis ? 0.6f : 0.3f;
        rect = new Rect(rect.x + border, rect.y + border, rect.width - border * 2, rect.height - border * 2);
        Draw(helper, rect, Color.Lerp(color, edge, 0.32f));
        rect.yMin += shape == ButtonShape.Chassis ? 1f : 0.65f;
        Draw(helper, rect, color);
    }

    private void Draw(VertexHelper helper, Rect rect, Color fill)
    {
        if (shape == ButtonShape.Gamepad) { DrawGamepad(helper, rect, fill); return; }
        int center = helper.currentVertCount;
        helper.AddVert(rect.center, fill, Vector2.zero);
        const int steps = 6, count = 4 * (steps + 1);
        float radius = shape switch
        {
            ButtonShape.Chassis => 6f,
            ButtonShape.Circle => Mathf.Min(rect.width, rect.height) * .5f,
            ButtonShape.MouseLeft or ButtonShape.MouseRight => 3.5f,
            ButtonShape.Wheel or ButtonShape.Side => 1.2f,
            _ => 2.2f
        };
        radius = Mathf.Min(radius, Mathf.Min(rect.width, rect.height) * 0.5f);
        for (int corner = 0; corner < 4; corner++)
        {
            Vector2 origin = new Vector2(corner == 0 || corner == 3 ? rect.xMax - radius : rect.xMin + radius,
                corner < 2 ? rect.yMax - radius : rect.yMin + radius);
            for (int step = 0; step <= steps; step++)
            {
                float angle = (corner * 90f + step * 90f / steps) * Mathf.Deg2Rad;
                helper.AddVert(origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, fill, Vector2.zero);
            }
        }
        for (int i = 0; i < count; i++)
            helper.AddTriangle(center, center + 1 + i, center + 1 + (i + 1) % count);
    }

    private static void DrawGamepad(VertexHelper helper, Rect rect, Color fill)
    {
        int center = helper.currentVertCount;
        helper.AddVert(rect.center, fill, Vector2.zero);
        Vector2 Point(float x, float y) => rect.center + Vector2.Scale(new Vector2(x, y), rect.size * .5f);
        void Curve(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            for (int i = 0; i < 8; i++)
            {
                float t = i / 8f, s = 1f - t;
                helper.AddVert(s * s * s * a + 3f * s * s * t * b + 3f * s * t * t * c + t * t * t * d, fill, Vector2.zero);
            }
        }
        Curve(Point(-.58f, .94f), Point(-.3f, 1f), Point(.3f, 1f), Point(.58f, .94f));
        Curve(Point(.58f, .94f), Point(.85f, .91f), Point(.99f, .17f), Point(1f, -.5f));
        Curve(Point(1f, -.5f), Point(1f, -1.12f), Point(.75f, -1.14f), Point(.6f, -.65f));
        Curve(Point(.6f, -.65f), Point(.39f, -.58f), Point(.39f, -.65f), Point(0, -.65f));
        Curve(Point(0, -.65f), Point(-.39f, -.65f), Point(-.39f, -.58f), Point(-.6f, -.65f));
        Curve(Point(-.6f, -.65f), Point(-.75f, -1.14f), Point(-1f, -1.12f), Point(-1f, -.5f));
        Curve(Point(-1f, -.5f), Point(-.99f, .17f), Point(-.85f, .91f), Point(-.58f, .94f));
        int count = helper.currentVertCount - center - 1;
        for (int i = 0; i < count; i++)
            helper.AddTriangle(center, center + 1 + i, center + 1 + (i + 1) % count);
    }
}
