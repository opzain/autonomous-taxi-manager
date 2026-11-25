using System.Collections.Generic;
using TaxiSim.Core;
using UnityEngine;

namespace TaxiSim.World
{
    /// <summary>
    /// Monsoon waterlogging, Mumbai's yearly reality. Low-lying stretches of road (along the Mithi
    /// River and around Kurla station) slowly fill with muddy water during heavy rain and drain
    /// afterwards. Vehicles crawl through flooded stretches (down to 30% speed) and throw up spray.
    /// Gameplay hook: <see cref="FloodedEdges"/> / <see cref="Depth01"/> feed the dispatch simulation's
    /// "heavy rain" event later (slower edges, possible closures).
    /// </summary>
    public sealed class FloodSystem : MonoBehaviour
    {
        public sealed class Zone
        {
            public string Name;
            public RoadPath Path;
            public float FromA, ToA;
            public Transform Water;
            public MeshRenderer Renderer;
            public float Depth; // 0 .. MaxDepth
        }

        public bool Enabled = true;
        public float MaxDepth = 0.07f;
        public float FillSeconds = 45f;
        public float DrainSeconds = 90f;
        public WeatherSystem Weather;
        public TrafficAmbient Traffic;
        public Transform CameraTransform;

        public readonly List<Zone> Zones = new List<Zone>();
        private ParticleSystem _spray;
        private float _sprayTimer;

        public bool AnyFlooded { get; private set; }

        /// <summary>Overall flooding 0..1 (deepest zone).</summary>
        public float Depth01 { get; private set; }

        public IEnumerable<int> FloodedEdges
        {
            get { foreach (Zone z in Zones) if (z.Depth > MaxDepth * 0.3f) yield return z.Path.Edge.Id; }
        }

        public void Init(WorldContext ctx, int maxZones = 6)
        {
            CityGeography geo = ctx.Map.Geography;
            var candidates = new List<(RoadPath path, float s, float score)>();
            CityNode kurla = null;
            foreach (CityNode n in ctx.Map.Graph.Nodes) if (n.Key == "kurla_stn") kurla = n;

            foreach (RoadPath path in ctx.Roads.Paths)
            {
                if (path.Edge.Elevated || path.Length < 10f) continue;
                float bestScore = float.MaxValue, bestS = 0f;
                for (float s = 4f; s < path.Length - 4f; s += 1f)
                {
                    Vector3 p = path.Sample(s, out _);
                    if (p.y > 0.1f) continue; // bridges stay dry
                    double river = geo.DistanceToRiver(p.x, p.z, out _);
                    float score = (float)river;
                    if (kurla != null)
                        score = Mathf.Min(score, Vector2.Distance(new Vector2(p.x, p.z), kurla.Position.XZ.ToVector2()) * 0.8f);
                    score += p.y * 50f; // lower ground floods first
                    if (score < bestScore) { bestScore = score; bestS = s; }
                }
                if (bestScore < 40f) candidates.Add((path, bestS, bestScore));
            }
            candidates.Sort((a, b) => a.score.CompareTo(b.score));

            var root = new GameObject("Monsoon Waterlogging").transform;
            root.SetParent(transform, false);
            for (int i = 0; i < candidates.Count && Zones.Count < maxZones; i++)
            {
                (RoadPath path, float s, _) = candidates[i];
                float half = Mathf.Min(5f, path.Length * 0.25f);
                var z = new Zone
                {
                    Name = path.Edge.Name,
                    Path = path,
                    FromA = Mathf.Max(2f, s - half),
                    ToA = Mathf.Min(path.Length - 2f, s + half)
                };
                BuildWater(ctx, z, root);
                Zones.Add(z);
            }

            _spray = Effects.Create("Flood_Spray", root, Vector3.zero, Quaternion.identity, ctx.Mats.Spray);
            var main = _spray.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
            main.gravityModifier = 0.6f;
            main.maxParticles = 3000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = _spray.emission;
            emission.rateOverTime = 0f;
            _spray.Play();
        }

        private static void BuildWater(WorldContext ctx, Zone z, Transform root)
        {
            var pts = new List<Vector3>();
            for (float s = z.FromA; s <= z.ToA + 0.01f; s += 0.5f) pts.Add(z.Path.Sample(Mathf.Min(s, z.ToA), out _));
            var mb = new MeshBuilder();
            // Covers the road and both footpaths; the ends are tapered by the water shader's depth fade
            mb.AddRibbon(pts, z.Path.Width + 2.0f, Color.white, 0f);
            GameObject go = ctx.Spawn($"Flood_{z.Name}", mb.ToMesh("FloodWater"), ctx.Mats.Water, Vector3.zero, Quaternion.identity, root, false);
            z.Water = go.transform;
            z.Renderer = go.GetComponent<MeshRenderer>();
            z.Renderer.enabled = false;
        }

        /// <summary>Speed factor for vehicles at a position on a road (1 = dry).</summary>
        public float SpeedAt(int edgeId, float distanceFromA)
        {
            float f = 1f;
            foreach (Zone z in Zones)
            {
                if (z.Path.Edge.Id != edgeId || z.Depth <= 0.002f) continue;
                if (distanceFromA < z.FromA - 1f || distanceFromA > z.ToA + 1f) continue;
                f = Mathf.Min(f, 1f - 0.7f * (z.Depth / MaxDepth));
            }
            return f;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            bool raining = Enabled && Weather != null && Weather.Mode == WeatherMode.Monsoon && Weather.Rain > 0.6f;
            AnyFlooded = false;
            Depth01 = 0f;
            foreach (Zone z in Zones)
            {
                float target = raining ? MaxDepth : 0f;
                z.Depth = Mathf.MoveTowards(z.Depth, target, dt * MaxDepth / (raining ? FillSeconds : DrainSeconds));
                if (!Enabled) z.Depth = 0f;
                bool visible = z.Depth > 0.003f;
                z.Renderer.enabled = visible;
                z.Water.localPosition = new Vector3(0f, z.Depth - 0.015f, 0f);
                if (visible) AnyFlooded = true;
                Depth01 = Mathf.Max(Depth01, z.Depth / MaxDepth);
            }
            if (AnyFlooded) Splash(dt);
        }

        /// <summary>Spray thrown up by vehicles driving through water near the camera.</summary>
        private void Splash(float dt)
        {
            if (Traffic == null || _spray == null) return;
            _sprayTimer -= dt;
            if (_sprayTimer > 0f) return;
            _sprayTimer = 0.05f;
            Vector3 cam = CameraTransform != null ? CameraTransform.position : Vector3.zero;
            var ep = new ParticleSystem.EmitParams();
            foreach (TrafficAmbient.Agent a in Traffic.Agents)
            {
                if (a.Speed < 0.2f) continue;
                Vector3 p = a.Transform.position;
                if (CameraTransform != null && (p - cam).sqrMagnitude > 70f * 70f) continue;
                float depth = 0f;
                foreach (Zone z in Zones)
                {
                    if (z.Path != a.Path) continue;
                    float d = a.DistanceFromA;
                    if (d >= z.FromA && d <= z.ToA) depth = Mathf.Max(depth, z.Depth / MaxDepth);
                }
                if (depth < 0.2f) continue;
                Vector3 right = a.Transform.right, fwd = a.Transform.forward;
                for (int side = -1; side <= 1; side += 2)
                {
                    ep.position = p + right * (0.12f * side) + Vector3.up * 0.03f;
                    ep.velocity = right * (side * Random.Range(0.4f, 0.9f)) + Vector3.up * Random.Range(0.5f, 1.1f) * depth - fwd * 0.2f;
                    ep.startColor = new Color(0.75f, 0.8f, 0.78f, 0.55f);
                    _spray.Emit(ep, Mathf.CeilToInt(2 * depth * a.Speed));
                }
            }
        }
    }
}
