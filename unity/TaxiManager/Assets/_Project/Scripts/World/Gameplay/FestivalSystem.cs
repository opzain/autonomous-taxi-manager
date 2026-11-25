using System.Collections.Generic;
using TaxiSim.Core;
using UnityEngine;

namespace TaxiSim.World
{
    public enum Festival
    {
        None,
        Ganpati,
        Diwali
    }

    /// <summary>
    /// Mumbai festivals as world events (Y cycles None → Ganpati → Diwali):
    ///  * Ganeshotsav: a procession walks down a main road: a decorated truck carrying a large Ganesha
    ///    idol under a lit arch, dhol-tasha drummers and dancing devotees throwing pink gulal. The road is
    ///    closed (Edge.Blocked, hazard stripes) and traffic queues behind the procession or turns back.
    ///  * Diwali: strings of coloured bulbs along the rooftops, akash-kandil star lanterns on street
    ///    facades, and fireworks bursting over the city at night (with sound).
    /// Gameplay hook: <see cref="ClosedEdgeId"/> is the spec's "festival" road-closure event.
    /// </summary>
    public sealed class FestivalSystem : MonoBehaviour
    {
        public TrafficAmbient Traffic;
        public CityPulse Pulse;
        public CitySoundscape Sound;
        public TimeOfDay Clock;
        public Transform CameraTransform;

        public Festival Active { get; private set; }
        public int ClosedEdgeId { get; private set; } = -1;
        public bool ProcessionActive => _procession != null && _procession.gameObject.activeSelf;
        public Vector3 ProcessionPosition => _procession != null ? _procession.position : Vector3.zero;
        public string ProcessionRoad => _route != null ? _route.Edge.Name : "";

        private WorldContext _ctx;
        private Pcg32 _rng;

        // Ganpati
        private RoadPath _route;
        private Transform _procession, _float;
        private readonly List<(Transform t, float along, float lateral)> _walkers = new List<(Transform, float, float)>();
        private TrafficAmbient.RoadBlock _block;
        private float _s;
        private int _dir = 1;
        private ParticleSystem _gulal;
        private AudioSource _dhol, _crowd;

        // Diwali
        private Transform _diwaliRoot;
        private ParticleSystem _fireworks;
        private readonly List<(Vector3 pos, Vector3 vel, float burstAt, Color col)> _rockets = new List<(Vector3, Vector3, float, Color)>();
        private float _nextRocket;

        private static readonly Color[] Festive =
        {
            new Color(1f, 0.85f, 0.45f), new Color(1f, 0.55f, 0.1f), new Color(1f, 0.2f, 0.25f),
            new Color(0.3f, 1f, 0.4f), new Color(0.3f, 0.5f, 1f), new Color(1f, 0.3f, 0.9f), new Color(1f, 1f, 0.9f)
        };

        public void Init(WorldContext ctx)
        {
            _ctx = ctx;
            _rng = new Pcg32(1909UL);
            BuildProcession();
            BuildDiwali();
            SetFestival(Festival.None);
        }

        public void Cycle() => SetFestival((Festival)(((int)Active + 1) % 3));

        public void SetFestival(Festival f)
        {
            Active = f;
            bool ganpati = f == Festival.Ganpati && _route != null;
            if (_procession != null) _procession.gameObject.SetActive(ganpati);
            if (_diwaliRoot != null) _diwaliRoot.gameObject.SetActive(f == Festival.Diwali);
            if (_route != null)
            {
                _route.Edge.Blocked = ganpati;
                ClosedEdgeId = ganpati ? _route.Edge.Id : -1;
                if (Traffic != null)
                {
                    Traffic.Blocks.Remove(_block);
                    if (ganpati) Traffic.Blocks.Add(_block);
                }
                if (ganpati) { _s = _route.Length * 0.12f; _dir = 1; }
                if (Pulse != null) Pulse.Recompute();
            }
            if (_dhol != null) { if (ganpati) { _dhol.Play(); _crowd.Play(); } else { _dhol.Stop(); _crowd.Stop(); } }
            _rockets.Clear();
        }

        // ------------------------------------------------------------------ Ganpati procession

        private void BuildProcession()
        {
            // Route: the longest ordinary main road outside the BKC office core (closest to how the
            // big processions run along LBS Marg and the Kurla / Bandra East streets)
            float best = 0f;
            foreach (RoadPath p in _ctx.Roads.Paths)
            {
                if (p.Edge.Elevated || p.LedLighting || p.Edge.Class == RoadClass.Expressway || p.Length < 22f) continue;
                Vector3 mid = p.Sample(p.Length * 0.5f, out _);
                if (mid.y > 0.1f) continue;
                float score = p.Length * (p.Edge.Class == RoadClass.Main ? 1.3f : 1f);
                if (score > best) { best = score; _route = p; }
            }
            if (_route == null) return;

            _procession = new GameObject("Ganpati Procession").transform;
            _procession.SetParent(transform, false);

            // Decorated truck with the idol
            GameObject truck = VehicleFactory.Spawn(_ctx.Mats, VehicleKind.Truck, new Color(0.95f, 0.75f, 0.2f), _procession, "Ganpati_Float");
            _float = truck.transform;
            var body = new MeshBuilder();
            var gold = new MeshBuilder();
            var lights = new MeshBuilder();
            BuildIdol(body, gold, lights);
            Spawn("Idol", body, _ctx.Mats.Concrete, _float);
            Spawn("IdolGold", gold, _ctx.Mats.Metal, _float);
            Spawn("IdolLights", lights, _ctx.Mats.FestiveLights, _float, false);

            // Devotees, dancers and dhol-tasha drummers walking ahead of the float
            Mesh[] people = PedestrianSystem.BuildVariantMeshes();
            int count = 34;
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Devotee");
                go.transform.SetParent(_procession, false);
                go.AddComponent<MeshFilter>().sharedMesh = people[_rng.Range(0, people.Length)];
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = _ctx.Mats.Character;
                bool drummer = i < 8;
                float along = drummer ? 0.9f + (i % 4) * 0.22f : _rng.Range(1.8f, 4.6f);
                float lateral = drummer ? ((i / 4) == 0 ? -0.35f : 0.35f) + (i % 2) * 0.12f : _rng.Range(-0.9f, 0.9f);
                if (drummer)
                {
                    var drum = new MeshBuilder();
                    drum.AddCylinder(Vector3.zero, 0.035f, 0.035f, 0.07f, 10, new Color(0.55f, 0.25f, 0.1f));
                    GameObject d = Spawn("Dhol", drum, _ctx.Mats.Concrete, go.transform);
                    d.transform.localPosition = new Vector3(0f, 0.15f, 0.04f);
                    d.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                }
                _walkers.Add((go.transform, along, lateral));
            }

            // Pink gulal clouds
            _gulal = Effects.Create("Gulal", _procession, Vector3.zero, Quaternion.identity, _ctx.Mats.Spray);
            var main = _gulal.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
            main.gravityModifier = -0.02f;
            main.maxParticles = 600;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.3f, 0.65f, 0.35f), new Color(1f, 0.55f, 0.2f, 0.3f));
            var emission = _gulal.emission;
            emission.rateOverTime = 45f;
            var shape = _gulal.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.8f;
            _gulal.Play();

            _block = new TrafficAmbient.RoadBlock { EdgeId = _route.Edge.Id };

            if (Sound != null)
            {
                _dhol = Source3D("Dhol_Tasha", Sound.GetClip("dhol_loop"), 0.9f, 3f, 70f);
                _crowd = Source3D("Procession_Crowd", Sound.GetClip("crowd_loop"), 0.6f, 3f, 40f);
            }
        }

        private AudioSource Source3D(string name, AudioClip clip, float volume, float minD, float maxD)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_procession, false);
            go.transform.localPosition = Vector3.up * 0.5f;
            var s = go.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = true;
            s.volume = volume;
            s.spatialBlend = 1f;
            s.minDistance = minD;
            s.maxDistance = maxD;
            s.rolloffMode = AudioRolloffMode.Logarithmic;
            s.playOnAwake = false;
            return s;
        }

        private GameObject Spawn(string name, MeshBuilder mb, Material mat, Transform parent, bool shadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh(name);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        /// <summary>A large seated Ganesha on a gold pedestal under a lit arch, garlanded with marigolds (truck local space).</summary>
        private void BuildIdol(MeshBuilder body, MeshBuilder gold, MeshBuilder lights)
        {
            var o = new Vector3(0f, 0.3f, -0.12f);
            var saffron = new Color(0.98f, 0.5f, 0.12f);
            var skin = new Color(1f, 0.62f, 0.38f);
            var red = new Color(0.8f, 0.08f, 0.1f);
            var goldC = new Color(1f, 0.78f, 0.25f);
            var marigold = new Color(1f, 0.6f, 0.05f);

            gold.AddCylinder(o, 0.24f, 0.2f, 0.08f, 16, goldC);                                       // pedestal
            body.AddEllipsoid(o + new Vector3(0, 0.08f, 0), new Vector3(0.2f, 0.06f, 0.16f), 12, 6, red, 0f); // seat cloth
            body.AddEllipsoid(o + new Vector3(0, 0.24f, 0), new Vector3(0.15f, 0.18f, 0.13f), 12, 8, saffron, 0f); // body
            body.AddEllipsoid(o + new Vector3(0, 0.2f, 0.06f), new Vector3(0.13f, 0.12f, 0.11f), 12, 8, skin, 0f); // belly
            body.AddEllipsoid(o + new Vector3(0, 0.46f, 0.02f), new Vector3(0.1f, 0.11f, 0.095f), 12, 8, skin, 0f); // head
            for (int s = -1; s <= 1; s += 2)
            {
                body.AddEllipsoid(o + new Vector3(s * 0.11f, 0.47f, 0f), new Vector3(0.09f, 0.1f, 0.02f), 10, 6, skin, 0f);   // ears
                body.AddEllipsoid(o + new Vector3(s * 0.15f, 0.27f, 0.04f), new Vector3(0.035f, 0.08f, 0.035f), 8, 5, skin, 0f); // upper arms
                body.AddEllipsoid(o + new Vector3(s * 0.19f, 0.36f, 0f), new Vector3(0.03f, 0.07f, 0.03f), 8, 5, skin, 0f);    // raised arms
                body.AddEllipsoid(o + new Vector3(s * 0.08f, 0.1f, 0.12f), new Vector3(0.06f, 0.04f, 0.08f), 8, 5, skin, 0f);   // folded legs
            }
            body.AddTube(o + new Vector3(0, 0.42f, 0.09f), o + new Vector3(0.03f, 0.3f, 0.15f), 0.03f, 0.016f, 8, skin);     // trunk
            body.AddTube(o + new Vector3(0.03f, 0.3f, 0.15f), o + new Vector3(0.07f, 0.28f, 0.12f), 0.016f, 0.012f, 6, skin);
            gold.AddCylinder(o + new Vector3(0, 0.54f, 0.01f), 0.07f, 0.008f, 0.16f, 10, goldC);                              // crown
            gold.AddDisc(o + new Vector3(0, 0.47f, -0.09f), 0.2f, 24, Vector3.back, goldC);                                    // halo
            gold.AddDisc(o + new Vector3(0, 0.47f, -0.088f), 0.2f, 24, Vector3.forward, goldC);

            // Halo bulbs and the lit arch over the float
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI * 2f / 16f;
                lights.AddEllipsoid(o + new Vector3(Mathf.Cos(a) * 0.21f, 0.47f + Mathf.Sin(a) * 0.21f, -0.08f), Vector3.one * 0.014f, 5, 3, Festive[i % Festive.Length], 0f);
            }
            for (int s = -1; s <= 1; s += 2) gold.AddBeam(new Vector3(s * 0.22f, 0.25f, -0.4f), new Vector3(s * 0.22f, 0.95f, -0.4f), new Vector2(0.025f, 0.025f), goldC);
            for (int i = 0; i <= 20; i++)
            {
                float a = Mathf.PI * i / 20f;
                var p = new Vector3(Mathf.Cos(a) * 0.22f, 0.95f + Mathf.Sin(a) * 0.12f, -0.4f);
                lights.AddEllipsoid(p, Vector3.one * 0.016f, 5, 3, Festive[i % Festive.Length], 0f);
            }
            // Marigold garlands along the truck bed
            for (int s = -1; s <= 1; s += 2)
                for (int i = 0; i < 14; i++)
                {
                    float z = Mathf.Lerp(-0.5f, 0.3f, i / 13f);
                    float sag = Mathf.Sin(i * Mathf.PI / 3.25f) * 0.03f;
                    body.AddEllipsoid(new Vector3(s * 0.2f, 0.27f - Mathf.Abs(sag), z), Vector3.one * 0.022f, 6, 4, i % 4 == 0 ? red : marigold, 0f);
                }
        }

        // ------------------------------------------------------------------ Diwali

        private void BuildDiwali()
        {
            _diwaliRoot = new GameObject("Diwali").transform;
            _diwaliRoot.SetParent(transform, false);
            var bulbs = new ChunkedMeshBuilder();
            int lit = 0, lanterns = 0;
            foreach (BuildingRecord r in _ctx.Towers.Records)
            {
                if (r.Cylinder || r.Height > 9f || r.Height < 0.6f || r.Width < 0.6f || r.Depth < 0.6f) continue;
                if (_ctx.Space.NearestSegment(r.Center, out _) > 4f) continue;   // street-facing buildings only
                if (!_rng.Chance(0.55)) continue;
                MeshBuilder mb = bulbs.Next();
                Quaternion q = Quaternion.Euler(0f, r.RotationY, 0f);
                float hx = r.Width * 0.5f + 0.02f, hz = r.Depth * 0.5f + 0.02f;
                Vector3 c = new Vector3(r.Center.x, r.Top - 0.02f, r.Center.y);
                Color a = Festive[_rng.Range(0, Festive.Length)], b = Festive[_rng.Range(0, Festive.Length)];
                Vector3[] corners = { c + q * new Vector3(-hx, 0, -hz), c + q * new Vector3(hx, 0, -hz), c + q * new Vector3(hx, 0, hz), c + q * new Vector3(-hx, 0, hz) };
                int k = 0;
                for (int e = 0; e < 4; e++)
                {
                    Vector3 p0 = corners[e], p1 = corners[(e + 1) % 4];
                    int n = Mathf.Max(2, Mathf.RoundToInt(Vector3.Distance(p0, p1) / 0.28f));
                    for (int i = 0; i < n; i++)
                    {
                        Vector3 p = Vector3.Lerp(p0, p1, i / (float)n) + Vector3.down * (0.03f * Mathf.Sin(Mathf.PI * (i % 6) / 6f));
                        mb.AddBox(p, q, Vector3.one * 0.032f, (k++ % 2 == 0) ? a : b);
                    }
                    // Vertical drops at the corners ("waterfall" strings)
                    if (r.Height < 4f)
                        for (float y = 0.3f; y < r.Height - 0.2f; y += 0.3f)
                            mb.AddBox(p0 + Vector3.down * y, q, Vector3.one * 0.03f, (k++ % 2 == 0) ? a : b);
                }
                lit++;

                // Akash kandil (star lantern) on the street face of some buildings
                if (_rng.Chance(0.4) && r.Height > 1f)
                {
                    Vector3[] normals = { q * Vector3.right, q * Vector3.left, q * Vector3.forward, q * Vector3.back };
                    float[] halves = { hx, hx, hz, hz };
                    int best = 0;
                    float bestD = float.MaxValue;
                    for (int i = 0; i < 4; i++)
                    {
                        Vector3 probe = new Vector3(r.Center.x, 0f, r.Center.y) + normals[i] * (halves[i] + 1f);
                        float d = _ctx.Space.NearestSegment(new Vector2(probe.x, probe.z), out _);
                        if (d < bestD) { bestD = d; best = i; }
                    }
                    Vector3 lp = new Vector3(r.Center.x, r.Top - Mathf.Min(0.9f, r.Height * 0.4f), r.Center.y) + normals[best] * (halves[best] + 0.12f);
                    Color lc = Festive[_rng.Range(0, Festive.Length)];
                    mb.AddEllipsoid(lp, new Vector3(0.07f, 0.09f, 0.07f), 5, 4, lc, 0f);           // star body (low-poly = faceted)
                    mb.AddBox(lp + Vector3.down * 0.13f, q, new Vector3(0.012f, 0.08f, 0.012f), lc); // tassel
                    mb.AddBox(lp + Vector3.up * 0.12f, q, new Vector3(0.006f, 0.08f, 0.006f), new Color(0.2f, 0.2f, 0.2f)); // cord
                    lanterns++;
                }
            }
            bulbs.Emit("DiwaliLights", _diwaliRoot, _ctx.Mats.FestiveLights, false);

            _fireworks = Effects.Create("Fireworks", _diwaliRoot, Vector3.zero, Quaternion.identity, _ctx.Mats.Fireworks);
            var main = _fireworks.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.0f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
            main.gravityModifier = 0.12f;
            main.maxParticles = 6000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = _fireworks.emission;
            emission.rateOverTime = 0f;
            var col = _fireworks.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
            var limit = _fireworks.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.drag = 1.2f;
            _fireworks.Play();
            Debug.Log($"[TaxiSim] Diwali: {lit} buildings with lights, {lanterns} akash kandils.");
        }

        // ------------------------------------------------------------------ update

        private void Update()
        {
            float dt = Time.deltaTime;
            if (Active == Festival.Ganpati && _route != null) UpdateProcession(dt);
            if (Active == Festival.Diwali) UpdateFireworks(dt);
        }

        private void UpdateProcession(float dt)
        {
            _s += _dir * 0.11f * dt;
            float margin = 6f;
            if (_s > _route.Length - margin) { _s = _route.Length - margin; _dir = -1; }   // the procession turns back
            if (_s < margin) { _s = margin; _dir = 1; }

            Vector3 p = _route.Sample(_s, out Vector3 fwd);
            fwd.y = 0f; fwd.Normalize();
            fwd *= _dir;
            Vector3 right = new Vector3(fwd.z, 0f, -fwd.x);
            _procession.position = p;
            _float.SetPositionAndRotation(p + Vector3.up * 0.005f, Quaternion.LookRotation(fwd));

            float t = Time.time;
            foreach ((Transform w, float along, float lateral) in _walkers)
            {
                float sway = Mathf.Sin(t * 1.3f + along * 7f) * 0.15f;
                Vector3 pos = p + fwd * along + right * (lateral + sway) + Vector3.up * 0.005f;
                pos.y = _route.Sample(Mathf.Clamp(_s + _dir * along, 0f, _route.Length), out _).y + 0.005f;
                w.position = pos;
                // Dancers turn to face the idol now and then
                bool faceIdol = Mathf.Sin(t * 0.5f + lateral * 5f) > 0.6f && along > 1.5f;
                w.rotation = Quaternion.Slerp(w.rotation, Quaternion.LookRotation(faceIdol ? -fwd : fwd), dt * 3f);
            }
            if (_gulal != null) _gulal.transform.position = p + fwd * 2.8f + Vector3.up * 0.3f;

            // Vehicles can't pass: the whole stretch from behind the float to the front of the crowd
            float a0 = _s - _dir * 1.5f, a1 = _s + _dir * 5f;
            _block.FromA = Mathf.Min(a0, a1);
            _block.ToA = Mathf.Max(a0, a1);
        }

        private void UpdateFireworks(float dt)
        {
            float night = Clock != null ? Clock.Night : 1f;
            Vector3 cam = CameraTransform != null ? CameraTransform.position : new Vector3(175f, 0f, 120f);

            if (night > 0.45f)
            {
                _nextRocket -= dt;
                if (_nextRocket <= 0f)
                {
                    _nextRocket = _rng.Range(0.5f, 2.2f);
                    float ang = _rng.Range(0f, Mathf.PI * 2f), dist = _rng.Range(50f, 160f);
                    var launch = new Vector3(cam.x + Mathf.Cos(ang) * dist, 0f, cam.z + Mathf.Sin(ang) * dist);
                    launch.y = _ctx.GroundY(launch.x, launch.z) + 1f;
                    var vel = new Vector3(_rng.Range(-1f, 1f), _rng.Range(26f, 36f), _rng.Range(-1f, 1f));
                    _rockets.Add((launch, vel, Time.time + _rng.Range(0.9f, 1.2f), Festive[_rng.Range(0, Festive.Length)]));
                    if (Sound != null) Sound.PlayAt("firework", launch + Vector3.up * 20f, 0.9f, _rng.Range(0.9f, 1.1f), 400f);
                }
            }

            var ep = new ParticleSystem.EmitParams();
            for (int i = _rockets.Count - 1; i >= 0; i--)
            {
                (Vector3 pos, Vector3 vel, float burstAt, Color c) = _rockets[i];
                vel += Physics.gravity * 0.2f * dt;
                pos += vel * dt;
                // Rising spark trail
                ep.position = pos;
                ep.velocity = Vector3.down * 0.5f;
                ep.startColor = new Color(1f, 0.8f, 0.5f, 0.8f);
                ep.startLifetime = 0.4f;
                ep.startSize = 0.18f;
                _fireworks.Emit(ep, 1);
                if (Time.time >= burstAt)
                {
                    Burst(pos, c);
                    _rockets.RemoveAt(i);
                }
                else _rockets[i] = (pos, vel, burstAt, c);
            }
        }

        private void Burst(Vector3 at, Color c)
        {
            var ep = new ParticleSystem.EmitParams();
            bool twoTone = _rng.Chance(0.4);
            Color c2 = Festive[_rng.Range(0, Festive.Length)];
            int n = 160;
            float speed = _rng.Range(7f, 11f);
            for (int i = 0; i < n; i++)
            {
                Vector3 dir = new Vector3(_rng.Range(-1f, 1f), _rng.Range(-1f, 1f), _rng.Range(-1f, 1f));
                if (dir.sqrMagnitude < 1e-3f) dir = Vector3.up;
                ep.position = at;
                ep.velocity = dir.normalized * speed * _rng.Range(0.85f, 1f);
                ep.startColor = twoTone && i % 2 == 0 ? c2 : c;
                ep.startLifetime = _rng.Range(1.3f, 2.1f);
                ep.startSize = _rng.Range(0.3f, 0.5f);
                _fireworks.Emit(ep, 1);
            }
        }
    }
}
