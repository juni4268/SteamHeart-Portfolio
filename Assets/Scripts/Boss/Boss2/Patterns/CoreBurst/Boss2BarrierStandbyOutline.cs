using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class Boss2BarrierStandbyOutline : MonoBehaviour
{
    [SerializeField] private Boss2CoreBurstBarrier barrier;
    [SerializeField] private SpriteRenderer source;
    [SerializeField, Range(1, 4)] private int width = 1;
    [SerializeField] private Color color = new Color(0.1f, 0.65f, 1f, 1f);

    private MeshRenderer outline;
    private Mesh mesh;
    private MaterialPropertyBlock properties;
    private Sprite currentSprite;
    private bool currentFlipX;
    private bool currentFlipY;
    private int currentWidth;

    private void OnEnable() => Refresh();
    private void LateUpdate() => Refresh();

    private void Refresh()
    {
        if (outline == null) outline = GetComponent<MeshRenderer>();
        outline.enabled = barrier != null && source != null && source.enabled && source.sprite != null && barrier.IsStandby;
        if (!outline.enabled) return;
        if (mesh == null || currentSprite != source.sprite || currentFlipX != source.flipX ||
            currentFlipY != source.flipY || currentWidth != width) BuildMesh();
        outline.sortingLayerID = source.sortingLayerID;
        outline.sortingOrder = source.sortingOrder + 1;
        properties ??= new MaterialPropertyBlock();
        Texture texture = source.sprite.texture;
        properties.SetTexture("_MainTex", texture);
        properties.SetVector("_TextureTexelSize", new Vector4(1f / texture.width, 1f / texture.height, 0f, 0f));
        Rect rect = source.sprite.rect;
        properties.SetVector("_SpriteUVRect", new Vector4(rect.xMin / texture.width, rect.yMin / texture.height,
            rect.xMax / texture.width, rect.yMax / texture.height));
        properties.SetFloat("_OutlineWidth", width);
        properties.SetColor("_OutlineColor", color);
        outline.SetPropertyBlock(properties);
    }

    private void BuildMesh()
    {
        currentSprite = source.sprite;
        currentFlipX = source.flipX;
        currentFlipY = source.flipY;
        currentWidth = width;
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        foreach (Vector2 vertex in currentSprite.vertices)
        {
            min = Vector2.Min(min, vertex);
            max = Vector2.Max(max, vertex);
        }
        float padding = width / currentSprite.pixelsPerUnit;
        min -= Vector2.one * padding;
        max += Vector2.one * padding;
        var corners = new[] { min, new Vector2(max.x, min.y), max, new Vector2(min.x, max.y) };
        var vertices = new Vector3[4];
        var uv = new Vector2[4];
        Rect rect = currentSprite.rect;
        Vector2 textureSize = new Vector2(currentSprite.texture.width, currentSprite.texture.height);
        for (int i = 0; i < 4; i++)
        {
            Vector2 pixel = corners[i] * currentSprite.pixelsPerUnit + currentSprite.pivot;
            uv[i] = (pixel + rect.position) / textureSize;
            vertices[i] = new Vector3(corners[i].x * (currentFlipX ? -1f : 1f),
                corners[i].y * (currentFlipY ? -1f : 1f), 0f);
        }
        if (mesh == null) mesh = new Mesh { name = "Barrier Standby Outline", hideFlags = HideFlags.HideAndDontSave };
        mesh.Clear();
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateBounds();
        GetComponent<MeshFilter>().sharedMesh = mesh;
    }

    private void OnDisable()
    {
        if (outline != null) outline.enabled = false;
    }

    private void OnDestroy()
    {
        if (mesh == null) return;
        if (Application.isPlaying) Destroy(mesh);
        else DestroyImmediate(mesh);
    }
}
