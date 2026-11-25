using UnityEngine;

namespace TaxiSim.World
{
    /// <summary>Polyline with cumulative distances, used by trains and decorative traffic.</summary>
    public sealed class Polyline3
    {
        public readonly Vector3[] Points;
        public readonly float[] Cumulative;
        public float Length => Cumulative[Cumulative.Length - 1];

        public Polyline3(Vector3[] points)
        {
            Points = points;
            Cumulative = new float[points.Length];
            for (int i = 1; i < points.Length; i++) Cumulative[i] = Cumulative[i - 1] + Vector3.Distance(points[i - 1], points[i]);
        }

        public Vector3 Sample(float d, out Vector3 forward)
        {
            d = Mathf.Clamp(d, 0f, Length);
            int lo = 0, hi = Points.Length - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (Cumulative[mid] <= d) lo = mid; else hi = mid;
            }
            float seg = Mathf.Max(Cumulative[hi] - Cumulative[lo], 1e-5f);
            forward = (Points[hi] - Points[lo]) / seg;
            return Vector3.Lerp(Points[lo], Points[hi], (d - Cumulative[lo]) / seg);
        }
    }

    /// <summary>
    /// Moves a train (or a single car) along a polyline. Ping-pong mode shuttles between the two ends
    /// with a dwell time (Mumbai locals, Metro 2B); loop mode wraps around (Sea Link traffic).
    /// </summary>
    public sealed class TrainMover : MonoBehaviour
    {
        [System.NonSerialized] public Polyline3 Path;
        public Transform[] Cars;
        public float Speed = 6f;
        public float CarSpacing = 2.2f;
        public bool PingPong = true;
        public float Dwell = 6f;
        public float Distance;
        public float TimeScale = 1f;

        private int _direction = 1;
        private float _wait;

        private void Update()
        {
            if (Path == null || Cars == null || Cars.Length == 0) return;
            float dt = Time.deltaTime * TimeScale;
            float half = (Cars.Length - 1) * CarSpacing * 0.5f;

            if (_wait > 0f)
            {
                _wait -= dt;
            }
            else
            {
                Distance += _direction * Speed * dt;
                if (PingPong)
                {
                    if (Distance > Path.Length - half) { Distance = Path.Length - half; _direction = -1; _wait = Dwell; }
                    else if (Distance < half) { Distance = half; _direction = 1; _wait = Dwell; }
                }
                else if (Distance > Path.Length) Distance -= Path.Length;
            }

            for (int i = 0; i < Cars.Length; i++)
            {
                float d = Distance + (i * CarSpacing - half);
                if (!PingPong) d = Mathf.Repeat(d, Path.Length);
                Vector3 pos = Path.Sample(d, out Vector3 fwd);
                Cars[i].position = pos;
                if (fwd.sqrMagnitude > 1e-6f) Cars[i].rotation = Quaternion.LookRotation(fwd, Vector3.up);
            }
        }
    }
}
