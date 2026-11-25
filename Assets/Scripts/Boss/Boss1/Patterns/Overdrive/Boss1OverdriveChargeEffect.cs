using System.Collections.Generic;
using UnityEngine;

// Owns charge presentation; the hand and pattern own attack motion and timing.
public sealed class Boss1OverdriveChargeEffect : MonoBehaviour, System.IDisposable
{
    private static readonly int RimColorId = Shader.PropertyToID("_RimColor");
    private static readonly int RimWidthId = Shader.PropertyToID("_RimWidth");
    private static readonly int ChargeId = Shader.PropertyToID("_Charge");
    private static readonly int AuraTimeId = Shader.PropertyToID("_AuraTime");
    private static readonly int SpriteRectId = Shader.PropertyToID("_SpriteUVRect");
    private static readonly int ContactsId = Shader.PropertyToID("_ArcContacts");
    private static readonly int ContactCountId = Shader.PropertyToID("_ContactCount");
    private static readonly int InkGridId = Shader.PropertyToID("_InkGrid");
    private static readonly int ArcPixelGridId = Shader.PropertyToID("_ArcPixelGrid");
    private static readonly int ArcWorldUVXId = Shader.PropertyToID("_ArcWorldUVX");
    private static readonly int ArcWorldUVYId = Shader.PropertyToID("_ArcWorldUVY");

    [Header("Electric Aura")]
    [SerializeField] private Material rimMaterial;
    [SerializeField] private SpriteRenderer arcPrefab;
    [SerializeField, ColorUsage(false)] private Color rimColor = new Color(1f, 0.72f, 0.1f, 1f);
    [SerializeField, Range(1f, 3f)] private float rimWidthPixels = 1f;
    [SerializeField, Range(1, 8)] private int arcCount = 5;
    [SerializeField, Min(1f)] private float arcSpanPixels = 23f;
    [Tooltip("Distance between the painted contact points, as a fraction of the sprite width.")]
    [SerializeField, Range(0.1f, 1f)] private float arcContactSpan = 0.76f;
    [SerializeField] private string arcRateParameter = "ChargeRate";
    [SerializeField] private string arcPhaseParameter = "Phase";

    [Header("Discharge")]
    [SerializeField] private Animator dischargeAnimator;
    [SerializeField] private string releaseTrigger = "Release";
    [SerializeField, Range(0f, 1f)] private float dischargeProgress;

    [Header("Afterimage")]
    [SerializeField, Range(0f, 1f)] private float afterimageOpacity = 0.24f;
    [SerializeField, Min(0f)] private float afterimageFollowSpeed = 14f;

    private sealed class ElectricArc
    {
        public SpriteRenderer Renderer;
        public Animator Animator;
        public readonly MaterialPropertyBlock Properties = new MaterialPropertyBlock();

        public void SetVisible(bool visible) => Renderer.enabled = visible;
    }

    private Boss1OverdriveConfig config;
    private SpriteRenderer source;
    private Collider2D handCollider;
    private SpriteRenderer visual;
    private bool sourceWasEnabled;
    private SpriteRenderer rim;
    private SpriteRenderer afterimage;
    private Material runtimeRimMaterial;
    private readonly Dictionary<Sprite, Sprite> rimSprites = new Dictionary<Sprite, Sprite>();
    private readonly List<Vector2> outline = new List<Vector2>();
    private Sprite outlineSprite;
    private float outlineLength;
    private readonly Vector4[] contacts = new Vector4[8];
    private int arcRateHash;
    private ElectricArc[] arcs;
    private Vector2 localDirection;
    private float pressure;
    private float elapsed;
    private float releaseElapsed;
    private bool locked;
    private bool released;
    private bool dischargeCompleted;
    private bool attackCompleted;
    private bool disposed;

    public static Boss1OverdriveChargeEffect Create(Boss1OverdriveChargeEffect prefab,
        SpriteRenderer source, Collider2D handCollider, Boss1OverdriveConfig config, Vector2 direction)
    {
        var effect = Instantiate(prefab, source.transform.parent, false);
        effect.gameObject.layer = source.gameObject.layer;
        effect.Initialize(source, handCollider, config, direction);
        return effect;
    }

    private void Initialize(SpriteRenderer renderer, Collider2D collider,
        Boss1OverdriveConfig settings, Vector2 direction)
    {
        source = renderer;
        handCollider = collider;
        config = settings;
        localDirection = source.transform.parent.InverseTransformDirection(direction).normalized;
        sourceWasEnabled = source.enabled;
        visual = CreateRenderer("Charged Hand", transform, source.sharedMaterial, 0);
        runtimeRimMaterial = new Material(rimMaterial);
        runtimeRimMaterial.SetColor(RimColorId, Tint(rimColor));
        runtimeRimMaterial.SetFloat(RimWidthId, rimWidthPixels);
        rim = CreateRenderer("Charged Hand Edge", visual.transform, runtimeRimMaterial, 1);
        source.enabled = false;
        afterimage = CreateRenderer("Overdrive Hand Afterimage", source.transform.parent,
            source.sharedMaterial, -1);
        afterimage.enabled = false;
        SynchronizeHand();
        arcRateHash = Animator.StringToHash(arcRateParameter);
        int phaseHash = Animator.StringToHash(arcPhaseParameter);
        arcs = new ElectricArc[arcCount];
        for (int i = 0; i < arcs.Length; i++)
        {
            SpriteRenderer rendererInstance = Instantiate(arcPrefab, visual.transform, false);
            rendererInstance.name = "Electric Filament " + (i + 1);
            rendererInstance.gameObject.layer = source.gameObject.layer;
            rendererInstance.sortingLayerName = "BossEffect";
            rendererInstance.sortingOrder = source.sortingOrder + 2;
            rendererInstance.color = Tint(Color.white);
            Animator animator = rendererInstance.GetComponent<Animator>();
            animator.SetFloat(phaseHash, i * 0.173f);
            arcs[i] = new ElectricArc { Renderer = rendererInstance, Animator = animator };
            arcs[i].SetVisible(false);
        }
        UpdateRim();
    }

    private Color Tint(Color color)
    {
        color.r *= config.ChargeColor.r * config.ChargeBrightness;
        color.g *= config.ChargeColor.g * config.ChargeBrightness;
        color.b *= config.ChargeColor.b * config.ChargeBrightness;
        color.a = 1f;
        return color;
    }

    private SpriteRenderer CreateRenderer(string name, Transform parent, Material material, int order)
    {
        var root = new GameObject(name);
        root.layer = source.gameObject.layer;
        root.transform.SetParent(parent, false);
        var renderer = root.AddComponent<SpriteRenderer>();
        renderer.sharedMaterial = material;
        renderer.sortingLayerName = "BossEffect";
        renderer.sortingOrder = source.sortingOrder + order;
        return renderer;
    }

    public void SetProgress(float progress)
    {
        if (!disposed && !released)
            pressure = Mathf.Clamp01(config.ChargePressure.Evaluate(Mathf.Clamp01(progress)));
    }

    public void Lock() => locked = true;

    public void Release()
    {
        if (disposed || released) return;
        released = true;
        source.enabled = sourceWasEnabled;
        visual.enabled = false;
        transform.localPosition = Vector3.zero;
        transform.localScale = Vector3.one;
        dischargeAnimator.SetTrigger(Animator.StringToHash(releaseTrigger));
        afterimage.sprite = source.sprite;
        afterimage.flipX = source.flipX;
        afterimage.flipY = source.flipY;
        afterimage.transform.SetParent(null, true);
        afterimage.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
        afterimage.transform.localScale = source.transform.lossyScale;
        afterimage.color = new Color(1f, 1f, 1f, afterimageOpacity);
        afterimage.enabled = true;
    }

    // Authored event at the end of the discharge clip, independent of Animator state names.
    public void CompleteDischarge()
    {
        dischargeCompleted = true;
    }

    public void CompleteAttack()
    {
        if (disposed) return;
        attackCompleted = true;
        rim.enabled = false;
        foreach (ElectricArc arc in arcs)
            arc.SetVisible(false);
    }

    private void LateUpdate()
    {
        if (disposed) return;
        if (source == null || !source.gameObject.activeInHierarchy)
        {
            Dispose();
            return;
        }
        elapsed += Time.deltaTime;
        SynchronizeHand();
        if (released)
        {
            releaseElapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(releaseElapsed / config.ChargeReleaseDuration);
            Color color = afterimage.color;
            color.a = afterimageOpacity * (1f - Mathf.Clamp01(progress / 0.45f));
            afterimage.color = color;
            afterimage.transform.position = Vector3.Lerp(afterimage.transform.position, source.transform.position,
                1f - Mathf.Exp(-afterimageFollowSpeed * Time.deltaTime));
            if (progress >= 1f && dischargeCompleted && attackCompleted) Dispose();
        }
        else
        {
            float compression = config.ChargeCompression * pressure;
            Vector3 scale = new Vector3(
                1f - compression * Mathf.Abs(localDirection.x) + compression * 0.3f * Mathf.Abs(localDirection.y),
                1f - compression * Mathf.Abs(localDirection.y) + compression * 0.3f * Mathf.Abs(localDirection.x), 1f);
            Vector3 center = visual.transform.localPosition + visual.transform.localRotation
                * Vector3.Scale(visual.transform.localScale, source.sprite.bounds.center);
            float vibration = locked ? 0f : config.ChargeShakeStrength * pressure * pressure;
            float phase = elapsed * Mathf.Lerp(12f, 28f, pressure) * Mathf.PI * 2f;
            Vector3 shake = new Vector3(Mathf.Sin(phase), Mathf.Sin(phase * 1.37f) * 0.45f, 0f) * vibration;
            transform.localScale = scale;
            transform.localPosition = center - Vector3.Scale(center, scale) + shake;
        }
        if (!disposed)
        {
            UpdateArcs();
            UpdateRim();
        }
    }

    private void SynchronizeHand()
    {
        visual.sprite = source.sprite;
        visual.flipX = source.flipX;
        visual.flipY = source.flipY;
        visual.color = source.color;
        visual.transform.SetLocalPositionAndRotation(source.transform.localPosition, source.transform.localRotation);
        visual.transform.localScale = source.transform.localScale;
        visual.enabled = sourceWasEnabled && !released;
        if (!rimSprites.TryGetValue(source.sprite, out Sprite rimSprite))
        {
            Sprite sprite = source.sprite;
            // A full rectangle leaves room for the halo beyond the source's tight mesh.
            rimSprite = Sprite.Create(sprite.texture, sprite.rect,
                sprite.pivot / sprite.rect.size, sprite.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            rimSprite.name = sprite.name + " Electric Rim";
            rimSprites.Add(sprite, rimSprite);
        }
        rim.sprite = rimSprite;
        rim.flipX = source.flipX;
        rim.flipY = source.flipY;
    }

    private void UpdateRim()
    {
        Sprite sprite = source.sprite;
        Rect rect = sprite.rect;
        Texture2D texture = sprite.texture;
        runtimeRimMaterial.SetVector(SpriteRectId, new Vector4(rect.xMin / texture.width,
            rect.yMin / texture.height, rect.xMax / texture.width, rect.yMax / texture.height));
        runtimeRimMaterial.SetFloat(ChargeId, released ? 1f : pressure);
        runtimeRimMaterial.SetFloat(AuraTimeId, elapsed);
        rim.enabled = sourceWasEnabled && !attackCompleted && (released || pressure > 0.02f);
    }

    private void UpdateArcs()
    {
        RefreshOutline();
        int visibleCount = released ? arcs.Length : Mathf.CeilToInt(Mathf.Lerp(1f, arcs.Length, pressure));
        int contactCount = 0;
        for (int i = 0; i < arcs.Length; i++)
        {
            bool visible = released ? !attackCompleted
                : pressure > 0.04f && i < visibleCount
                    && (i == 0 || Mathf.Repeat(elapsed * 0.9f + i * 0.31f, 1f) < Mathf.Lerp(0.38f, 0.85f, pressure));
            ElectricArc arc = arcs[i];
            arc.SetVisible(visible);
            arc.Animator.SetFloat(arcRateHash, Mathf.Lerp(0.7f, 1.4f, pressure));
            if (!visible && !released) continue;

            float fraction = Mathf.Repeat((i + 0.25f) / arcs.Length + Noise(i * 31f) * 0.04f, 1f);
            float span = arcSpanPixels * Mathf.Lerp(0.85f, 1.15f, Noise(i * 31f + 1f));
            GetArcAnchors(fraction, span, out Vector3 a, out Vector3 b);
            a = visual.transform.InverseTransformPoint(a);
            b = visual.transform.InverseTransformPoint(b);
            Vector3 tangent = b - a;
            Vector3 outward = new Vector3(-tangent.y, tangent.x, 0f);
            bool flipY = Vector3.Dot(outward, (a + b) * 0.5f) < 0f;
            if (flipY) outward = -outward;
            float spriteWidth = arc.Renderer.sprite.rect.width / arc.Renderer.sprite.pixelsPerUnit;
            float scale = tangent.magnitude / (spriteWidth * arcContactSpan);
            float dischargeOffset = Mathf.Sin(dischargeProgress * Mathf.PI) * 3f / source.sprite.pixelsPerUnit;
            Vector3 offset = released ? outward.normalized * dischargeOffset : Vector3.zero;
            arc.Renderer.transform.SetLocalPositionAndRotation(a + offset,
                Quaternion.Euler(0f, 0f, Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg));
            arc.Renderer.transform.localScale = Vector3.one * scale;
            arc.Renderer.flipY = flipY;
            Sprite sprite = arc.Renderer.sprite;
            arc.Properties.SetFloat(InkGridId, Mathf.Max(1f, sprite.pixelsPerUnit / (source.sprite.pixelsPerUnit * scale)));
            // Keep the electricity on the hand's pixel grid even when the patch rotates.
            float pixelSize = Mathf.Max(Mathf.Abs(visual.transform.lossyScale.x), Mathf.Abs(visual.transform.lossyScale.y))
                / source.sprite.pixelsPerUnit;
            Vector3 gridOrigin = visual.transform.position;
            arc.Properties.SetVector(ArcPixelGridId, new Vector4(gridOrigin.x, gridOrigin.y, pixelSize, 0f));
            Matrix4x4 inverse = arc.Renderer.transform.worldToLocalMatrix;
            float uScale = sprite.pixelsPerUnit / sprite.texture.width;
            float vScale = sprite.pixelsPerUnit / sprite.texture.height * (flipY ? -1f : 1f);
            arc.Properties.SetVector(ArcWorldUVXId, new Vector4(inverse.m00 * uScale, inverse.m01 * uScale,
                inverse.m03 * uScale + (sprite.pivot.x + sprite.rect.x) / sprite.texture.width, 0f));
            arc.Properties.SetVector(ArcWorldUVYId, new Vector4(inverse.m10 * vScale, inverse.m11 * vScale,
                inverse.m13 * vScale + (sprite.pivot.y + sprite.rect.y) / sprite.texture.height, 0f));
            arc.Renderer.SetPropertyBlock(arc.Properties);
            Vector2 uvA = ContactUV(a);
            Vector2 uvB = ContactUV(b);
            contacts[contactCount++] = new Vector4(uvA.x, uvA.y, uvB.x, uvB.y);
        }
        runtimeRimMaterial.SetVectorArray(ContactsId, contacts);
        runtimeRimMaterial.SetInt(ContactCountId, contactCount);
    }

    private void RefreshOutline()
    {
        if (outlineSprite == source.sprite) return;
        outlineSprite = source.sprite;
        outline.Clear();
        outlineLength = 0f;
        if (outlineSprite.GetPhysicsShapeCount() == 0) return;
        outlineSprite.GetPhysicsShape(0, outline);
        for (int i = 0; i < outline.Count; i++)
            outlineLength += Vector2.Distance(outline[i], outline[(i + 1) % outline.Count]);
    }

    private Vector3 OutlinePoint(float distance)
    {
        distance = Mathf.Repeat(distance, outlineLength);
        for (int i = 0; i < outline.Count; i++)
        {
            Vector2 a = outline[i];
            Vector2 b = outline[(i + 1) % outline.Count];
            float length = Vector2.Distance(a, b);
            if (distance <= length)
            {
                Vector2 point = Vector2.Lerp(a, b, distance / length);
                if (source.flipX) point.x = -point.x;
                if (source.flipY) point.y = -point.y;
                return visual.transform.TransformPoint(point);
            }
            distance -= length;
        }
        return visual.bounds.center;
    }

    private void GetArcAnchors(float fraction, float spanPixels, out Vector3 a, out Vector3 b)
    {
        if (outlineLength > 0f)
        {
            float distance = fraction * outlineLength;
            a = OutlinePoint(distance);
            b = OutlinePoint(distance + spanPixels / source.sprite.pixelsPerUnit);
            return;
        }
        // Preserve collider anchoring for sprites without an authored physics outline.
        Bounds bounds = handCollider.bounds;
        float angle = fraction * Mathf.PI * 2f;
        Vector2 normal = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        Vector2 start = handCollider.ClosestPoint((Vector2)bounds.center + normal * bounds.size.magnitude);
        Vector2 tangent = new Vector2(-normal.y, normal.x);
        a = MapToVisual(start);
        b = MapToVisual(handCollider.ClosestPoint(start + (normal + tangent) * spanPixels
            / source.sprite.pixelsPerUnit * Mathf.Abs(source.transform.lossyScale.x)));
    }

    private Vector2 ContactUV(Vector3 point)
    {
        Sprite sprite = source.sprite;
        if (source.flipX) point.x = -point.x;
        if (source.flipY) point.y = -point.y;
        Vector2 pixel = (Vector2)point * sprite.pixelsPerUnit + sprite.pivot + sprite.rect.position;
        return pixel / new Vector2(sprite.texture.width, sprite.texture.height);
    }

    private static float Noise(float seed) => Mathf.Repeat(Mathf.Sin(seed * 12.9898f + 4.1414f) * 43758.5453f, 1f);

    private Vector3 MapToVisual(Vector2 point) =>
        visual.transform.TransformPoint(source.transform.InverseTransformPoint(
            new Vector3(point.x, point.y, source.transform.position.z)));

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (source != null) source.enabled = sourceWasEnabled;
        if (afterimage != null) Destroy(afterimage.gameObject);
        if (runtimeRimMaterial != null) Destroy(runtimeRimMaterial);
        foreach (Sprite sprite in rimSprites.Values) Destroy(sprite);
        Destroy(gameObject);
    }

    private void OnDisable()
    {
        if (Application.isPlaying) Dispose();
    }
}
