using System.Collections.Generic;
using UnityEngine;

namespace TaxiSim.World
{
    /// <summary>
    /// Keeps track of occupied ground (roads, rails, metro, landmarks, buildings) so procedural
    /// placement never puts a tower on a road or two buildings inside each other.
    /// Circles are stored in a uniform grid for fast lookups.
    /// </summary>
    public sealed class SpaceRegistry
    {
        private struct Circle { public Vector2 C; public float R; }
        private struct Segment { public Vector2 A, B; public float HalfWidth; }

        private const float CellSize = 16f;
        private const float MaxCircleRadius = 20f;
        private readonly Dictionary<long, List<Circle>> _grid = new Dictionary<long, List<Circle>>();
        private readonly List<Segment> _segments = new List<Segment>();

        private static long Key(int x, int y) => ((long)x << 32) ^ (uint)y;

        public void AddCircle(Vector2 center, float radius)
        {
            int cx = Mathf.FloorToInt(center.x / CellSize), cy = Mathf.FloorToInt(center.y / CellSize);
            long k = Key(cx, cy);
            if (!_grid.TryGetValue(k, out List<Circle> list)) _grid[k] = list = new List<Circle>();
            list.Add(new Circle { C = center, R = Mathf.Min(radius, MaxCircleRadius) });
        }

        public void AddSegment(Vector2 a, Vector2 b, float halfWidth)
        {
            _segments.Add(new Segment { A = a, B = b, HalfWidth = halfWidth });
        }

        public void AddPolyline(IList<Vector2> points, float halfWidth)
        {
            for (int i = 0; i + 1 < points.Count; i++) AddSegment(points[i], points[i + 1], halfWidth);
        }

        /// <summary>Free space around p: distance to the nearest occupied area (negative = overlapping).</summary>
        public float Clearance(Vector2 p)
        {
            float best = float.MaxValue;
            foreach (Segment s in _segments)
            {
                float d = DistanceToSegment(p, s.A, s.B) - s.HalfWidth;
                if (d < best) best = d;
            }
            int reach = Mathf.CeilToInt(MaxCircleRadius / CellSize) + 1;
            int cx = Mathf.FloorToInt(p.x / CellSize), cy = Mathf.FloorToInt(p.y / CellSize);
            for (int x = cx - reach; x <= cx + reach; x++)
            {
                for (int y = cy - reach; y <= cy + reach; y++)
                {
                    if (!_grid.TryGetValue(Key(x, y), out List<Circle> list)) continue;
                    foreach (Circle c in list)
                    {
                        float d = Vector2.Distance(p, c.C) - c.R;
                        if (d < best) best = d;
                    }
                }
            }
            return best;
        }

        public bool IsFree(Vector2 p, float radius, float margin = 0.3f) => Clearance(p) >= radius + margin;

        /// <summary>Distance to the nearest registered segment and its direction (for aligning buildings to streets).</summary>
        public float NearestSegment(Vector2 p, out Vector2 direction)
        {
            float best = float.MaxValue;
            direction = Vector2.right;
            foreach (Segment s in _segments)
            {
                float d = DistanceToSegment(p, s.A, s.B) - s.HalfWidth;
                if (d < best)
                {
                    best = d;
                    direction = (s.B - s.A).normalized;
                }
            }
            return best;
        }

        public static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            float t = len2 > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2) : 0f;
            return Vector2.Distance(p, a + ab * t);
        }
    }
}
