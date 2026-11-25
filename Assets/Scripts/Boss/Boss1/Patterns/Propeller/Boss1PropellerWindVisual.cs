using System.Collections.Generic;
using UnityEngine;

// Owns rendering only; suction and damage remain in Boss1PropellerField.
public sealed class Boss1PropellerWindVisual
{
    private const float SequenceAngle = 137.50776f * Mathf.Deg2Rad;
    private readonly List<WindStream> streams = new List<WindStream>();
    private readonly Stack<WindStream> availableStreams = new Stack<WindStream>();
    // Visual variation must not consume the gameplay random sequence.
    private readonly System.Random random = new System.Random();
    private float nextSpawnInterval;
    private float spawnElapsed;
    private float nextAngle;

    private Transform parent;
    private Boss1HandController hand;
    private Transform suctionOrigin;
    private Boss1PropellerConfig config;
    private GameObject visualRoot;
    private Material lineMaterial;
    private float warningElapsed;
    private bool isPrepared;
    private bool isSuctionActive;
    private bool isFinishing;

    public bool HasActiveStreams => streams.Count > 0;

    public void Prepare(Transform visualParent, Boss1HandController selectedHand,
        Transform origin, Boss1PropellerConfig propellerConfig)
    {
        Stop();
        parent = visualParent;
        hand = selectedHand;
        suctionOrigin = origin;
        config = propellerConfig;
        isPrepared = true;
        EnsureVisuals();
        UpdateVisualPosition();
        spawnElapsed = 0f;
        nextAngle = 0f;
        nextSpawnInterval = Sample(config.SpawnIntervalRange);
    }

    public void StartSuction()
    {
        if (isPrepared) isSuctionActive = true;
    }

    public void Finish()
    {
        if (!isPrepared) return;
        isFinishing = true;
    }

    public void Stop()
    {
        isPrepared = false;
        isSuctionActive = false;
        isFinishing = false;
        warningElapsed = 0f;
        foreach (WindStream stream in streams) ReleaseStream(stream);
        streams.Clear();
        spawnElapsed = 0f;
        parent = null;
        hand = null;
        suctionOrigin = null;
        config = null;
    }

    public void DestroyVisuals()
    {
        Stop();
        availableStreams.Clear();
        if (visualRoot != null) Object.Destroy(visualRoot);
        if (lineMaterial != null) Object.Destroy(lineMaterial);
        visualRoot = null;
        lineMaterial = null;
    }

    public void Tick()
    {
        if (!isPrepared || hand == null || suctionOrigin == null || config == null) return;

        warningElapsed += Time.deltaTime;
        float warningProgress = config.WarningDuration <= 0f
            ? 1f
            : Mathf.Clamp01(warningElapsed / config.WarningDuration);

        UpdateVisualPosition();
        float intensity = isSuctionActive ? config.ActiveIntensity : warningProgress * config.WarningIntensity;
        for (int i = streams.Count - 1; i >= 0; i--)
        {
            if (streams[i].Tick(Time.deltaTime, intensity)) continue;
            ReleaseStream(streams[i]);
            streams.RemoveAt(i);
        }

        if (isFinishing) return;

        spawnElapsed += Time.deltaTime * (isSuctionActive ? 1f : warningProgress * 0.45f);
        while (spawnElapsed >= nextSpawnInterval)
        {
            spawnElapsed -= nextSpawnInterval;
            CreateWindStream(intensity);
            nextSpawnInterval = Sample(config.SpawnIntervalRange);
        }
    }

    private void EnsureVisuals()
    {
        if (visualRoot == null)
        {
            visualRoot = new GameObject("Propeller Visual");
            visualRoot.hideFlags = HideFlags.DontSave;
            visualRoot.transform.SetParent(parent, false);
        }

        if (lineMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
                lineMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
        }
    }

    private void CreateWindStream(float intensity)
    {
        WindStream stream;
        if (availableStreams.Count > 0)
        {
            stream = availableStreams.Pop();
        }
        else
        {
            // Grow only when all existing streams are still alive, including their fade.
            GameObject streamObject = new GameObject("Spiral Wind");
            streamObject.hideFlags = HideFlags.DontSave;
            streamObject.transform.SetParent(visualRoot.transform, false);
            LineRenderer renderer = streamObject.AddComponent<LineRenderer>();
            renderer.useWorldSpace = true;
            renderer.numCapVertices = 2;
            stream = new WindStream(renderer);
        }
        LineRenderer line = stream.Renderer;
        line.sharedMaterial = lineMaterial;
        line.widthMultiplier = Sample(config.WidthRange);
        CopySorting(line);

        float radius = Sample(config.SpawnRadiusRange);
        float rotationAmount = Sample(config.RotationAmountRange) * Mathf.Deg2Rad;
        if (hand.HandSide == Boss1HandSide.Left) rotationAmount = -rotationAmount;
        float startAngle = nextAngle + Sample(new Vector2(-config.AngleJitter, config.AngleJitter)) * Mathf.Deg2Rad;
        stream.Reset(suctionOrigin.position, radius, startAngle, rotationAmount,
            Sample(config.CurvatureRange), Sample(config.MaxLengthRange), Sample(config.TravelDurationRange), config);
        streams.Add(stream);
        stream.Tick(0f, intensity);
        line.gameObject.SetActive(true);
        nextAngle = (nextAngle + SequenceAngle) % (Mathf.PI * 2f);
    }

    private void ReleaseStream(WindStream stream)
    {
        stream.Renderer.gameObject.SetActive(false);
        availableStreams.Push(stream);
    }

    private float Sample(Vector2 range) => Mathf.Lerp(range.x, range.y, (float)random.NextDouble());

    private void UpdateVisualPosition()
    {
        if (visualRoot == null || suctionOrigin == null || config == null) return;

        visualRoot.transform.position = suctionOrigin.position;
        visualRoot.transform.rotation = Quaternion.identity;

    }

    private void CopySorting(Renderer targetRenderer)
    {
        SpriteRenderer source = hand != null ? hand.GetComponentInChildren<SpriteRenderer>(true) : null;
        if (source == null || targetRenderer == null) return;
        targetRenderer.sortingLayerName = "BossEffect";
        targetRenderer.sortingOrder = source.sortingOrder + 2;
    }

    private sealed class WindStream
    {
        private int pathSegments;
        private int visibleSegments;
        private readonly LineRenderer line;
        private Vector3[] path;
        private float[] distances;
        private Vector3[] visiblePoints;
        private Color startColor;
        private Color endColor;
        private float totalLength;
        private float speed;
        private float maxLength;
        private float elapsed;

        public LineRenderer Renderer => line;

        public WindStream(LineRenderer renderer)
        {
            line = renderer;
        }

        public void Reset(Vector3 center, float radius, float startAngle,
            float rotationAmount, float curvature, float maximumLength, float travelDuration,
            Boss1PropellerConfig config)
        {
            elapsed = 0f;
            visibleSegments = config.SegmentCount;
            pathSegments = visibleSegments * 3;
            if (visiblePoints == null || visiblePoints.Length != visibleSegments + 1)
            {
                path = new Vector3[pathSegments + 1];
                distances = new float[pathSegments + 1];
                visiblePoints = new Vector3[visibleSegments + 1];
            }
            distances[0] = 0f;
            startColor = config.StartColor;
            endColor = config.EndColor;
            // World-space samples keep the original spawn point fixed while the line grows.
            for (int i = 0; i <= pathSegments; i++)
            {
                float progress = (float)i / pathSegments;
                float angle = startAngle + rotationAmount * progress;
                float remainingRadius = radius * Mathf.Pow(1f - progress, curvature);
                path[i] = center + new Vector3(
                    Mathf.Cos(angle) * remainingRadius * config.HorizontalScale,
                    Mathf.Sin(angle) * remainingRadius * config.VerticalScale, -0.01f);
                if (i > 0)
                    distances[i] = distances[i - 1] + Vector3.Distance(path[i - 1], path[i]);
            }
            totalLength = distances[pathSegments];
            speed = totalLength / travelDuration;
            maxLength = Mathf.Min(maximumLength, totalLength);
            line.positionCount = visiblePoints.Length;
        }

        public bool Tick(float deltaTime, float intensity)
        {
            elapsed += deltaTime;
            float travelled = elapsed * speed;
            float head = Mathf.Min(travelled, totalLength);
            float tail = Mathf.Max(0f, travelled - maxLength);
            if (travelled >= totalLength * 0.95f) return false;

            // Tail stays at zero during growth, then follows at a fixed world arc length.
            // The stream fades out before its head reaches the center.
            for (int i = 0; i <= visibleSegments; i++)
                visiblePoints[i] = SampleDistance(Mathf.Lerp(tail, head, (float)i / visibleSegments));
            line.SetPositions(visiblePoints);
            // Fade over 80-95% of the path, leaving the arrival point clear.
            float fadeProgress = Mathf.InverseLerp(totalLength * 0.8f,
                totalLength * 0.95f, travelled);
            float opacity = intensity * (1f - Mathf.SmoothStep(0f, 1f, fadeProgress));
            Color tailColor = startColor;
            Color headColor = endColor;
            tailColor.a *= opacity;
            headColor.a *= opacity;
            line.startColor = tailColor;
            line.endColor = headColor;
            return true;
        }

        private Vector3 SampleDistance(float distance)
        {
            int low = 0;
            int high = pathSegments;
            while (high - low > 1)
            {
                int middle = (low + high) / 2;
                if (distances[middle] < distance) low = middle;
                else high = middle;
            }
            float t = Mathf.InverseLerp(distances[low], distances[high], distance);
            return Vector3.Lerp(path[low], path[high], t);
        }

    }
}
