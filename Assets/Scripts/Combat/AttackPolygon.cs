using System;
using UnityEngine;

[Serializable]
public sealed class AttackPolygon
{
    [Serializable]
    public sealed class Path
    {
        [SerializeField] private Vector2[] points = Array.Empty<Vector2>();

        public Vector2[] Points => points;

        public Path(Vector2[] points) => this.points = points;
    }

    [SerializeField] private Path[] paths = Array.Empty<Path>();

    public bool IsValid
    {
        get
        {
            if (paths == null || paths.Length == 0) return false;
            foreach (Path path in paths)
                if (path?.Points == null || path.Points.Length < 3) return false;
            return true;
        }
    }

    public AttackPolygon(params Vector2[][] outlines)
    {
        paths = new Path[outlines.Length];
        for (int i = 0; i < outlines.Length; i++) paths[i] = new Path(outlines[i]);
    }

    internal void SetWorldPaths(PolygonCollider2D collider, Transform point)
    {
        collider.pathCount = paths.Length;
        for (int i = 0; i < paths.Length; i++)
        {
            Vector2[] localPoints = paths[i].Points;
            var worldPoints = new Vector2[localPoints.Length];
            for (int j = 0; j < localPoints.Length; j++)
                worldPoints[j] = point.TransformPoint(localPoints[j]);
            collider.SetPath(i, worldPoints);
        }
    }
}
