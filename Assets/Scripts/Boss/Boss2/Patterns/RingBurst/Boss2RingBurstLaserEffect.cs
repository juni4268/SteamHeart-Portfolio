using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class Boss2RingBurstLaserEffect : MonoBehaviour
{
    [SerializeField] private Material laserMaterial;
    [SerializeField] private ParticleSystem impacts;
    [SerializeField, Min(0.01f)] private float trailDuration = 0.12f;
    [SerializeField, Min(0.01f)] private float dimDuration = 0.16f;
    [SerializeField, Range(0f, 1f)] private float dimStrength = 0.65f;
    [SerializeField, Min(0f)] private float shakeStrength = 0.1f;

    private sealed class BeamGlow
    {
        public MeshRenderer Renderer;
        public readonly MaterialPropertyBlock Properties = new MaterialPropertyBlock();
        public float Length;
    }

    private readonly List<BeamGlow> glows = new List<BeamGlow>();
    private MaterialPropertyBlock dimProperties;
    private Mesh mesh;
    private MeshRenderer backgroundDim;
    private float elapsed;
    private bool released;

    public void Build(IReadOnlyList<Boss2RingBurstField.Segment> segments, float depth,
        float width, int sortingLayer, int sortingOrder)
    {
        if (laserMaterial == null || impacts == null)
            throw new InvalidOperationException("Assign the Ring Burst laser material and impact particles.");

        dimProperties = new MaterialPropertyBlock();
        mesh = new Mesh { name = "Ring Burst Effect Quad" };
        mesh.vertices = new[] { new Vector3(-0.5f, -0.5f), new Vector3(-0.5f, 0.5f),
            new Vector3(0.5f, 0.5f), new Vector3(0.5f, -0.5f) };
        mesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
        mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateBounds();
        float margin = width * 3f;
        foreach (var segment in segments)
        {
            var renderer = MakeQuad("Laser Glow", laserMaterial, sortingLayer, sortingOrder);
            Vector2 direction = (segment.End - segment.Start).normalized;
            Vector2 center = (segment.Start + segment.End) * 0.5f;
            renderer.transform.SetPositionAndRotation(new Vector3(center.x, center.y, depth),
                Quaternion.FromToRotation(Vector3.right, direction));
            renderer.transform.localScale = new Vector3(segment.Length + margin * 2f, width * 6f, 1f);
            var glow = new BeamGlow { Renderer = renderer, Length = segment.Length };
            glow.Properties.SetFloat("_Length", segment.Length);
            glow.Properties.SetFloat("_Width", width);
            glow.Properties.SetFloat("_Margin", margin);
            glows.Add(glow);
        }

        var particleRenderer = impacts.GetComponent<ParticleSystemRenderer>();
        particleRenderer.sortingLayerID = sortingLayer;
        particleRenderer.sortingOrder = sortingOrder + 1;
        backgroundDim = MakeQuad("Release Background Dim", particleRenderer.sharedMaterial,
            SortingLayer.NameToID("Background"), short.MaxValue);
        dimProperties.SetTexture("_BaseMap", Texture2D.whiteTexture);
    }

    private MeshRenderer MakeQuad(string name, Material material, int layer, int order)
    {
        var instance = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        instance.transform.SetParent(transform, false);
        instance.GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = instance.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.sortingLayerID = layer;
        renderer.sortingOrder = order;
        renderer.enabled = false;
        return renderer;
    }

    public void Release()
    {
        released = true;
        elapsed = 0f;
        if (Application.isPlaying)
            HitCameraShake.Play(this, 0.16f, shakeStrength, new Vector2(1f, 0.65f), 28f, 0.02f);
        UpdateDim();
    }

    public void SetTime(float attackElapsed) => elapsed = attackElapsed;

    public void DrawSegment(int index, float tail, float head, float speed)
    {
        var glow = glows[index];
        float trailLength = speed * trailDuration;
        glow.Renderer.enabled = released && head > 0f && tail < glow.Length + trailLength;
        glow.Properties.SetFloat("_Head", Mathf.Clamp(head, 0f, glow.Length));
        glow.Properties.SetFloat("_Tail", tail);
        glow.Properties.SetFloat("_HeadOn", head > 0f && head < glow.Length ? 1f : 0f);
        glow.Properties.SetFloat("_TrailLength", trailLength);
        glow.Properties.SetFloat("_EffectTime", elapsed);
        glow.Renderer.SetPropertyBlock(glow.Properties);
    }

    public void Impact(Vector3 position, Vector2 incoming)
    {
        impacts.Play();
        float rotation = Mathf.Atan2(incoming.y, incoming.x) * Mathf.Rad2Deg;
        for (int i = 0; i < 3; i++)
        {
            var flash = new ParticleSystem.EmitParams
            {
                position = position, velocity = Vector3.zero, rotation = rotation + i * 60f,
                startSize3D = new Vector3(i == 0 ? 1.1f : 0.75f, 0.075f, 1f),
                startColor = Color.white, startLifetime = 0.2f
            };
            impacts.Emit(flash, 1);
        }
        for (int i = 0; i < 12; i++)
        {
            float angle = rotation * Mathf.Deg2Rad + i * Mathf.PI * 2f / 12f;
            var fragment = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * (i % 2 == 0 ? 2.8f : 1.6f),
                startSize3D = new Vector3(0.16f, 0.045f, 1f), rotation = angle * Mathf.Rad2Deg,
                startColor = new Color(1f, 0.65f, 0.85f), startLifetime = 0.24f
            };
            impacts.Emit(fragment, 1);
        }
    }

    private void LateUpdate() => UpdateDim();

    private void UpdateDim()
    {
        if (backgroundDim == null) return;
        Camera camera = Camera.main;
        backgroundDim.enabled = released && elapsed < dimDuration && camera != null;
        if (!backgroundDim.enabled) return;
        float depth = Vector3.Dot(transform.position - camera.transform.position, camera.transform.forward);
        Vector3 bottomLeft = camera.ViewportToWorldPoint(new Vector3(0f, 0f, depth));
        Vector3 topRight = camera.ViewportToWorldPoint(new Vector3(1f, 1f, depth));
        backgroundDim.transform.SetPositionAndRotation((bottomLeft + topRight) * 0.5f, camera.transform.rotation);
        backgroundDim.transform.localScale = new Vector3(Mathf.Abs(topRight.x - bottomLeft.x),
            Mathf.Abs(topRight.y - bottomLeft.y), 1f);
        dimProperties.SetColor("_BaseColor", new Color(0f, 0f, 0f,
            dimStrength * (1f - Mathf.SmoothStep(0f, 1f, elapsed / dimDuration))));
        backgroundDim.SetPropertyBlock(dimProperties);
    }

    public void Hide()
    {
        released = false;
        foreach (var glow in glows) if (glow.Renderer != null) glow.Renderer.enabled = false;
        if (backgroundDim != null) backgroundDim.enabled = false;
        if (impacts != null) impacts.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        HitCameraShake.Cancel(this);
    }

    private void OnDisable() => Hide();
    private void OnDestroy()
    {
        if (mesh == null) return;
        if (Application.isPlaying) Destroy(mesh);
        else DestroyImmediate(mesh);
    }
}
