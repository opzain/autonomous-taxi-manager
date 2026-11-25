using TaxiSim.Core;
using UnityEngine;

namespace TaxiSim.World
{
    /// <summary>
    /// Live traffic conditions on the road graph: morning and evening rush hours, monsoon slow-downs
    /// and road closures. Writes each road's TrafficMultiplier (used by Dijkstra routing and vehicle
    /// speed) and drives the green/amber/red traffic heat overlay on the 3D roads.
    /// The full dispatch simulation will replace this with its TrafficManager.
    /// </summary>
    public sealed class CityPulse : MonoBehaviour
    {
        [System.NonSerialized] public CityGraph Graph;
        [System.NonSerialized] public RoadNetwork Roads;
        public TimeOfDay Clock;
        public WeatherSystem Weather;
        public bool HeatOverlay;
        /// <summary>While a dispatch shift runs, its simulation owns traffic: copy its multipliers and closures.</summary>
        [System.NonSerialized] public CityGraph Source;

        private float _timer;

        private static readonly Color Free = new Color(0.1f, 1f, 0.45f);
        private static readonly Color Busy = new Color(1f, 0.65f, 0.08f);
        private static readonly Color Jam = new Color(1f, 0.08f, 0.05f);

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = 0.5f;
            Recompute();
        }

        public void Recompute()
        {
            if (Graph == null || Roads == null) return;
            float h = Clock != null ? Clock.Hour : 12f;
            float rush = Mathf.Exp(-Mathf.Pow((h - 9.5f) / 1.4f, 2f)) + Mathf.Exp(-Mathf.Pow((h - 18.7f) / 1.6f, 2f));
            bool monsoon = Weather != null && Weather.Mode == WeatherMode.Monsoon;

            foreach (RoadEdge e in Graph.Edges)
            {
                float classFactor = e.Class == RoadClass.Local ? 0.5f : e.Class == RoadClass.Main ? 0.8f : 1f;
                float noise = Hash(e.Id);
                float mult = 1f + rush * (0.6f + 1.2f * noise) * classFactor + (monsoon ? 0.5f + 0.4f * noise : 0f);
                e.TrafficMultiplier = Mathf.Clamp(mult, 1f, 3f);
                if (Source != null && e.Id < Source.Edges.Count)
                {
                    e.TrafficMultiplier = Source.Edges[e.Id].TrafficMultiplier;
                    e.Blocked = Source.Edges[e.Id].Blocked;
                }

                if (!Roads.ByEdge.TryGetValue(e.Id, out RoadPath path)) continue;
                float t = Mathf.InverseLerp(1f, 3f, (float)e.TrafficMultiplier);
                Color c = t < 0.5f ? Color.Lerp(Free, Busy, t * 2f) : Color.Lerp(Busy, Jam, (t - 0.5f) * 2f);
                Roads.ApplyOverlay(path, c, HeatOverlay ? 1f : 0f, e.Blocked);
            }
        }

        private static float Hash(int id)
        {
            uint x = (uint)id * 2654435761u;
            x ^= x >> 13;
            x *= 0x5bd1e995u;
            x ^= x >> 15;
            return (x & 0xFFFF) / 65535f;
        }
    }
}
