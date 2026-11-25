using System.Collections.Generic;
using UnityEngine;

// The body Animator owns the groggy lifetime; these points only present the electrical fault.
public sealed class Boss1GroggyEffect : MonoBehaviour
{
    [System.Serializable]
    private sealed class LeakPoint
    {
        public Transform start;
        public Transform end;
        public SpriteRenderer filament;

        [System.NonSerialized] public Animator animator;
        [System.NonSerialized] public MaterialPropertyBlock properties;
        [System.NonSerialized] public float remaining;
        [System.NonSerialized] public int cycle;
        [System.NonSerialized] public bool visible;
    }

    private static readonly int InkGridId = Shader.PropertyToID("_InkGrid");
    private static readonly int PixelGridId = Shader.PropertyToID("_ArcPixelGrid");
    private static readonly int WorldUVXId = Shader.PropertyToID("_ArcWorldUVX");
    private static readonly int WorldUVYId = Shader.PropertyToID("_ArcWorldUVY");
    private static readonly int ContactsId = Shader.PropertyToID("_ArcContacts");
    private static readonly int ContactCountId = Shader.PropertyToID("_ContactCount");
    private static readonly int SpriteRectId = Shader.PropertyToID("_SpriteUVRect");
    private static readonly int ChargeId = Shader.PropertyToID("_Charge");
    private static readonly int AuraTimeId = Shader.PropertyToID("_AuraTime");

    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private SpriteRenderer reflection;
    [SerializeField] private LeakPoint[] leaks;
    [SerializeField, ColorUsage(false)] private Color tint = new Color(0.85f, 0.85f, 0.85f, 1f);
    [SerializeField, Range(0.1f, 1f)] private float contactSpan = 0.76f;
    [SerializeField] private Vector2 onDuration = new Vector2(0.16f, 0.38f);
    [SerializeField] private Vector2 offDuration = new Vector2(0.12f, 0.55f);
    [SerializeField] private string rateParameter = "ChargeRate";
    [SerializeField] private string phaseParameter = "Phase";

    [Header("Smoke")]
    [SerializeField] private CombatAnimationEffect smokePrefab;
    [SerializeField] private Transform[] smokeOrigins;
    [SerializeField, Min(0.01f)] private float smokeScale = 0.65f;
    [SerializeField] private Vector2 smokeInterval = new Vector2(0.6f, 0.85f);

    private readonly Vector4[] contacts = new Vector4[8];
    private MaterialPropertyBlock reflectionProperties;
    private Sprite sourceSprite;
    private Sprite reflectionSprite;
    private bool playing;
    private float elapsed;
    private readonly List<CombatAnimationEffect> smokePuffs = new List<CombatAnimationEffect>();
    private float smokeRemaining;
    private int nextSmokeOrigin;
    private int smokeCycle;

    public bool IsPlaying => playing;

    private void Awake()
    {
        reflectionProperties = new MaterialPropertyBlock();
        int phaseHash = Animator.StringToHash(phaseParameter);
        for (int i = 0; i < leaks.Length; i++)
        {
            LeakPoint leak = leaks[i];
            leak.animator = leak.filament.GetComponent<Animator>();
            leak.properties = new MaterialPropertyBlock();
            leak.filament.enabled = false;
            leak.animator.SetFloat(phaseHash, Noise(i * 13f));
            leak.animator.enabled = false;
        }
        reflection.enabled = false;
    }

    public void Play()
    {
        if (playing) return;
        playing = true;
        elapsed = 0f;
        smokeRemaining = 0f;
        nextSmokeOrigin = 0;
        smokeCycle = 0;
        for (int i = 0; i < leaks.Length; i++)
        {
            LeakPoint leak = leaks[i];
            leak.cycle = 0;
            leak.visible = true;
            leak.remaining = 0.14f + Noise(i * 7f) * 0.1f;
            leak.animator.enabled = true;
            leak.animator.SetFloat(Animator.StringToHash(rateParameter), Mathf.Lerp(0.8f, 1.3f, Noise(i * 17f)));
        }
        UpdateVisuals();
        UpdateSmoke();
    }

    public void Stop()
    {
        playing = false;
        reflection.enabled = false;
        foreach (LeakPoint leak in leaks)
        {
            leak.visible = false;
            leak.filament.enabled = false;
            if (leak.animator != null) leak.animator.enabled = false;
        }
        ClearSmoke();
    }

    private void LateUpdate()
    {
        if (!playing) return;
        elapsed += Time.deltaTime;
        for (int i = 0; i < leaks.Length; i++)
        {
            LeakPoint leak = leaks[i];
            leak.remaining -= Time.deltaTime;
            if (leak.remaining > 0f) continue;
            leak.visible = !leak.visible;
            leak.cycle++;
            Vector2 duration = leak.visible ? onDuration : offDuration;
            leak.remaining = Mathf.Lerp(duration.x, duration.y, Noise(i * 31f + leak.cycle * 17f));
        }
        UpdateVisuals();
        UpdateSmoke();
    }

    private void UpdateSmoke()
    {
        smokeRemaining -= Time.deltaTime;
        if (smokeRemaining > 0f) return;
        smokePuffs.RemoveAll(puff => puff == null);
        if (smokePuffs.Count >= 3)
        {
            smokeRemaining = 0.1f;
            return;
        }

        Transform origin = smokeOrigins[nextSmokeOrigin];
        // Steam drifts in world space until it finishes or the groggy effect stops.
        CombatAnimationEffect puff = Instantiate(smokePrefab, origin.position, origin.rotation);
        float direction = origin.position.x < bodyRenderer.transform.position.x ? -1f : 1f;
        Vector3 scale = bodyRenderer.transform.lossyScale * smokeScale;
        scale.x *= direction;
        puff.transform.localScale = scale;
        SpriteRenderer renderer = puff.GetComponentInChildren<SpriteRenderer>();
        renderer.sortingLayerName = "BossEffect";
        renderer.sortingOrder = bodyRenderer.sortingOrder + 3;
        smokePuffs.Add(puff);
        nextSmokeOrigin = (nextSmokeOrigin + 1) % smokeOrigins.Length;
        smokeCycle++;
        smokeRemaining = Mathf.Lerp(smokeInterval.x, smokeInterval.y, Noise(smokeCycle * 19f));
    }

    private void UpdateVisuals()
    {
        Sprite body = bodyRenderer.sprite;
        float pixelSize = Mathf.Max(Mathf.Abs(bodyRenderer.transform.lossyScale.x),
            Mathf.Abs(bodyRenderer.transform.lossyScale.y)) / body.pixelsPerUnit;
        int count = 0;
        for (int i = 0; i < leaks.Length; i++)
        {
            LeakPoint leak = leaks[i];
            SpriteRenderer renderer = leak.filament;
            renderer.enabled = leak.visible && bodyRenderer.enabled;
            if (!renderer.enabled) continue;

            Vector3 a = transform.InverseTransformPoint(leak.start.position);
            Vector3 b = transform.InverseTransformPoint(leak.end.position);
            Vector3 tangent = b - a;
            Sprite sprite = renderer.sprite;
            float scale = tangent.magnitude / (sprite.rect.width / sprite.pixelsPerUnit * contactSpan);
            renderer.transform.SetLocalPositionAndRotation(a,
                Quaternion.Euler(0f, 0f, Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg));
            renderer.transform.localScale = Vector3.one * scale;
            renderer.sortingLayerName = "BossEffect";
            renderer.sortingOrder = bodyRenderer.sortingOrder + 2;
            renderer.color = new Color(tint.r, tint.g, tint.b, 1f);

            Matrix4x4 inverse = renderer.transform.worldToLocalMatrix;
            float uScale = sprite.pixelsPerUnit / sprite.texture.width;
            float vScale = sprite.pixelsPerUnit / sprite.texture.height * (renderer.flipY ? -1f : 1f);
            Vector3 origin = bodyRenderer.transform.position;
            leak.properties.SetFloat(InkGridId, Mathf.Max(1f, sprite.pixelsPerUnit / (body.pixelsPerUnit * scale)));
            leak.properties.SetVector(PixelGridId, new Vector4(origin.x, origin.y, pixelSize, 0f));
            leak.properties.SetVector(WorldUVXId, new Vector4(inverse.m00 * uScale, inverse.m01 * uScale,
                inverse.m03 * uScale + (sprite.pivot.x + sprite.rect.x) / sprite.texture.width, 0f));
            leak.properties.SetVector(WorldUVYId, new Vector4(inverse.m10 * vScale, inverse.m11 * vScale,
                inverse.m13 * vScale + (sprite.pivot.y + sprite.rect.y) / sprite.texture.height, 0f));
            renderer.SetPropertyBlock(leak.properties);
            Vector2 uvA = BodyUV(leak.start.position);
            Vector2 uvB = BodyUV(leak.end.position);
            contacts[count++] = new Vector4(uvA.x, uvA.y, uvB.x, uvB.y);
        }

        if (sourceSprite != body)
        {
            if (reflectionSprite != null) Destroy(reflectionSprite);
            sourceSprite = body;
            reflectionSprite = Sprite.Create(body.texture, body.rect, body.pivot / body.rect.size,
                body.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            reflectionSprite.name = body.name + " Groggy Reflection";
            reflection.sprite = reflectionSprite;
        }
        reflection.flipX = bodyRenderer.flipX;
        reflection.flipY = bodyRenderer.flipY;
        reflection.sortingLayerName = "BossEffect";
        reflection.sortingOrder = bodyRenderer.sortingOrder + 1;
        Rect rect = body.rect;
        reflectionProperties.SetVector(SpriteRectId, new Vector4(rect.xMin / body.texture.width,
            rect.yMin / body.texture.height, rect.xMax / body.texture.width, rect.yMax / body.texture.height));
        reflectionProperties.SetVectorArray(ContactsId, contacts);
        reflectionProperties.SetInt(ContactCountId, count);
        reflectionProperties.SetFloat(ChargeId, 1f);
        reflectionProperties.SetFloat(AuraTimeId, elapsed);
        reflection.SetPropertyBlock(reflectionProperties);
        reflection.enabled = count > 0 && bodyRenderer.enabled;
    }

    private Vector2 BodyUV(Vector3 world)
    {
        Sprite sprite = bodyRenderer.sprite;
        Vector3 local = bodyRenderer.transform.InverseTransformPoint(world);
        if (bodyRenderer.flipX) local.x = -local.x;
        if (bodyRenderer.flipY) local.y = -local.y;
        Vector2 pixel = (Vector2)local * sprite.pixelsPerUnit + sprite.pivot + sprite.rect.position;
        return pixel / new Vector2(sprite.texture.width, sprite.texture.height);
    }

    private static float Noise(float seed) => Mathf.Repeat(Mathf.Sin(seed * 12.9898f + 4.1414f) * 43758.5453f, 1f);

    private void OnDisable()
    {
        Stop();
    }

    private void ClearSmoke()
    {
        foreach (CombatAnimationEffect puff in smokePuffs)
        {
            if (puff == null) continue;
            puff.gameObject.SetActive(false);
            Destroy(puff.gameObject);
        }
        smokePuffs.Clear();
    }

    private void OnDestroy()
    {
        ClearSmoke();
        if (reflectionSprite != null) Destroy(reflectionSprite);
    }
}
