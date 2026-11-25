using System.Collections.Generic;
using TaxiSim.Core;
using UnityEngine;

namespace TaxiSim.World
{
    /// <summary>
    /// The sound of the city, mixed from the camera's point of view:
    ///  * beds: traffic rumble (louder near the street and at rush hour), wind (louder up high),
    ///    rain (follows the monsoon), crickets at night, street crowd murmur where people are;
    ///  * vehicle horns from real nearby vehicles — mostly ones stuck at a red light or in a jam —
    ///    with a different horn for cars, scooters, autos, trucks and buses;
    ///  * places: Kurla and Bandra stations (crowd, chime + PA announcement, train horn), the AT Fleet
    ///    Hub (electrical hum, charger chirps), rumbling local trains, crossing beeps at signals;
    ///  * thunder after every lightning flash, crows by day.
    /// N mutes / unmutes. Every clip is synthesised (ProceduralAudio) unless a real recording is supplied.
    /// </summary>
    public sealed class CitySoundscape : MonoBehaviour
    {
        public Transform Listener;
        public TrafficAmbient Traffic;
        public PedestrianSystem Pedestrians;
        public TrafficSignals Signals;
        public WeatherSystem Weather;
        public TimeOfDay Clock;
        [Range(0f, 1f)] public float MasterVolume = 0.85f;

        public int Overrides { get; private set; }
        public bool Muted { get; private set; }

        private Dictionary<string, AudioClip> _clips;
        private AudioSource _traffic, _wind, _rain, _crickets, _crowd;
        private AudioLowPassFilter _trafficLp;
        private readonly List<AudioSource> _pool = new List<AudioSource>();
        private int _poolNext;
        private readonly List<(Vector3 pos, float next)> _stations = new List<(Vector3, float)>();
        private Vector3 _hub;
        private bool _hasHub;
        private float _hornTimer = 1f, _crowTimer = 8f, _chirpTimer = 3f, _beepTimer;
        private float _thunderDelay = -1f, _lastLightning;
        private Pcg32 _rng;
        private WorldContext _ctx;

        public void Init(WorldContext ctx)
        {
            _ctx = ctx;
            _rng = new Pcg32(3131UL);
            _clips = ProceduralAudio.BuildAll(out int overrides);
            Overrides = overrides;

            _traffic = Bed("traffic_bed");
            _trafficLp = _traffic.gameObject.AddComponent<AudioLowPassFilter>();
            _wind = Bed("wind_loop");
            _rain = Bed("rain_loop");
            _crickets = Bed("crickets_loop");
            _crowd = Bed("crowd_loop");

            for (int i = 0; i < 16; i++)
            {
                var go = new GameObject($"OneShot_{i}");
                go.transform.SetParent(transform, false);
                var src = go.AddComponent<AudioSource>();
                Spatial(src, 2.5f, 60f);
                _pool.Add(src);
            }

            foreach (LandmarkInfo lm in ctx.Landmarks)
            {
                var ground = new Vector3(lm.Position.x, ctx.GroundY(lm.Position.x, lm.Position.z) + 0.5f, lm.Position.z);
                if (lm.Name.Contains("Station"))
                {
                    Loop3D($"Crowd_{lm.Name}", "crowd_loop", ground, 0.75f, 4f, 45f);
                    _stations.Add((ground, Time.time + _rng.Range(8f, 25f)));
                }
                else if (lm.Name == "AT Fleet Hub")
                {
                    _hub = ground;
                    _hasHub = true;
                    Loop3D("FleetHub_Hum", "hub_hum", ground + Vector3.down * 0.3f, 0.45f, 3f, 28f);
                }
            }

            foreach (TrainMover train in FindObjectsByType<TrainMover>(FindObjectsSortMode.None))
            {
                AudioSource s = Loop3D("Train_Rumble", "train_rumble", train.transform.position, 0.8f, 4f, 55f);
                s.transform.SetParent(train.transform, true);
                s.transform.localPosition = Vector3.zero;
            }
        }

        private AudioSource Bed(string clip)
        {
            var go = new GameObject($"Bed_{clip}");
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.clip = Clip(clip);
            src.loop = true;
            src.spatialBlend = 0f;
            src.volume = 0f;
            src.playOnAwake = false;
            src.time = _rng.Range(0f, src.clip != null ? src.clip.length * 0.9f : 0f);
            if (src.clip != null) src.Play();
            return src;
        }

        private AudioSource Loop3D(string name, string clip, Vector3 pos, float volume, float minD, float maxD)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = pos;
            var src = go.AddComponent<AudioSource>();
            Spatial(src, minD, maxD);
            src.clip = Clip(clip);
            src.loop = true;
            src.volume = volume;
            if (src.clip != null)
            {
                src.time = _rng.Range(0f, src.clip.length * 0.9f);
                src.Play();
            }
            return src;
        }

        private static void Spatial(AudioSource src, float minD, float maxD)
        {
            src.spatialBlend = 1f;
            src.rolloffMode = AudioRolloffMode.Logarithmic;
            src.minDistance = minD;
            src.maxDistance = maxD;
            src.dopplerLevel = 0f;
            src.playOnAwake = false;
        }

        private AudioClip Clip(string name) => _clips != null && _clips.TryGetValue(name, out AudioClip c) ? c : null;

        public AudioClip GetClip(string name) => Clip(name);

        public void PlayAt(string clip, Vector3 pos, float volume, float pitch = 1f, float maxD = 60f)
        {
            AudioClip c = Clip(clip);
            if (c == null) return;
            AudioSource src = _pool[_poolNext];
            _poolNext = (_poolNext + 1) % _pool.Count;
            src.transform.position = pos;
            src.maxDistance = maxD;
            src.pitch = pitch;
            src.PlayOneShot(c, volume);
        }

        public void ToggleMute()
        {
            Muted = !Muted;
            AudioListener.volume = Muted ? 0f : MasterVolume;
        }

        // ------------------------------------------------------------------ mixing

        private void Update()
        {
            if (Listener == null || _clips == null) return;
            if (InputCompat.Pressed(TsKey.N)) ToggleMute();
            if (!Muted) AudioListener.volume = MasterVolume;

            float dt = Time.deltaTime;
            Vector3 cam = Listener.position;
            float ground = _ctx != null ? Mathf.Max(_ctx.GroundY(cam.x, cam.z), 0f) : 0f;
            float height = Mathf.Max(0f, cam.y - ground);
            float street = 1f - Mathf.Clamp01((height - 2f) / 120f);   // 1 at street level .. 0 high above
            float hour = Clock != null ? Clock.Hour : 15f;
            float night = Clock != null ? Clock.Night : 0f;
            float rush = Mathf.Max(Bump(hour, 9.5f, 2f), Bump(hour, 19f, 2.5f));
            float busy = Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(1f - Bump(hour, 3.5f, 3f) * 0.8f)) * (0.8f + 0.25f * rush);

            // Beds
            Fade(_traffic, Mathf.Lerp(0.12f, 0.55f, street) * busy, dt);
            if (_trafficLp != null) _trafficLp.cutoffFrequency = Mathf.Lerp(700f, 6000f, street * street);
            Fade(_wind, Mathf.Lerp(0.04f, 0.4f, 1f - street) + (Weather != null ? Weather.Rain * 0.15f : 0f), dt);
            Fade(_rain, Weather != null ? Weather.Rain * Mathf.Lerp(0.35f, 0.7f, street) : 0f, dt);
            Fade(_crickets, Mathf.Clamp01((night - 0.5f) * 2f) * 0.22f * (Weather != null ? 1f - Weather.Rain : 1f) * Mathf.Lerp(0.4f, 1f, street), dt);
            int people = NearbyPeople(cam, 14f);
            Fade(_crowd, Mathf.Clamp01(people / 18f) * 0.45f * street, dt);

            Horns(cam, dt, street, rush);
            Places(cam, dt, night);
            TimedEvents(cam, dt);
            CrossingBeeps(cam, dt);
        }

        private static float Bump(float hour, float centre, float width)
        {
            float d = Mathf.Abs(Mathf.DeltaAngle(hour * 15f, centre * 15f)) / 15f;
            return Mathf.Clamp01(1f - d / width);
        }

        private static void Fade(AudioSource s, float target, float dt)
        {
            if (s == null) return;
            s.volume = Mathf.MoveTowards(s.volume, target, dt * 0.6f);
        }

        private int NearbyPeople(Vector3 cam, float radius)
        {
            // Walkers are children of the pedestrian system; a cheap count is good enough for the mix
            if (Pedestrians == null) return 0;
            int n = 0;
            float r2 = radius * radius;
            Transform root = Pedestrians.transform;
            for (int i = 0; i < root.childCount; i += 2) // sample every second walker
                if ((root.GetChild(i).position - cam).sqrMagnitude < r2) n += 2;
            return n;
        }

        private void Horns(Vector3 cam, float dt, float street, float rush)
        {
            if (Traffic == null || street < 0.15f) return;
            _hornTimer -= dt;
            if (_hornTimer > 0f) return;
            _hornTimer = _rng.Range(0.6f, 2.8f) / (0.6f + rush);

            // Pick a nearby vehicle; drivers stuck in traffic honk far more (very Mumbai)
            TrafficAmbient.Agent best = null;
            float bestScore = 0f;
            foreach (TrafficAmbient.Agent a in Traffic.Agents)
            {
                float d = Vector3.Distance(a.Transform.position, cam);
                if (d > 35f) continue;
                if (a.Kind == VehicleKind.FleetEv) continue; // our autonomous taxis never honk
                float score = (a.Speed < 0.3f ? 3f : 1f) * (0.5f + a.Aggression) * _rng.NextFloat() / (1f + d * 0.05f);
                if (score > bestScore) { bestScore = score; best = a; }
            }
            if (best == null) return;
            string clip;
            float pitch = _rng.Range(0.94f, 1.06f);
            switch (best.Kind)
            {
                case VehicleKind.Scooter: clip = "horn_scooter"; break;
                case VehicleKind.AutoRickshaw: clip = "horn_auto"; break;
                case VehicleKind.Truck: clip = "horn_truck"; break;
                case VehicleKind.BestBus: clip = "horn_bus"; break;
                default: clip = _rng.Chance(0.5) ? "horn_car" : "horn_car_double"; pitch *= _rng.Range(0.9f, 1.12f); break;
            }
            PlayAt(clip, best.Transform.position + Vector3.up * 0.2f, _rng.Range(0.45f, 0.8f), pitch);
        }

        private void Places(Vector3 cam, float dt, float night)
        {
            float now = Time.time;
            for (int i = 0; i < _stations.Count; i++)
            {
                (Vector3 pos, float next) = _stations[i];
                if (now < next) continue;
                float roll = _rng.NextFloat();
                if (roll < 0.55f)
                {
                    PlayAt("station_chime", pos + Vector3.up * 2f, 0.7f, 1f, 90f);
                    QueueAnnouncement(pos + Vector3.up * 2f, 2.0f);
                }
                else
                {
                    PlayAt("train_horn", pos + new Vector3(_rng.Range(-15f, 15f), 1f, _rng.Range(-15f, 15f)), 0.9f, _rng.Range(0.95f, 1.05f), 160f);
                }
                _stations[i] = (pos, now + _rng.Range(35f, 70f));
            }

            if (_hasHub)
            {
                _chirpTimer -= dt;
                if (_chirpTimer <= 0f)
                {
                    _chirpTimer = _rng.Range(3f, 8f);
                    PlayAt("charge_chirp", _hub + new Vector3(_rng.Range(-4f, 4f), 0f, _rng.Range(-2f, 3f)), 0.35f, _rng.Range(0.95f, 1.1f), 25f);
                }
            }

            // Crows by day, near the camera when it is low
            _crowTimer -= dt;
            if (_crowTimer <= 0f)
            {
                _crowTimer = _rng.Range(6f, 18f);
                if (night < 0.4f && (Weather == null || Weather.Rain < 0.3f))
                    PlayAt("crow", cam + new Vector3(_rng.Range(-12f, 12f), _rng.Range(2f, 6f), _rng.Range(-12f, 12f)), 0.4f, _rng.Range(0.92f, 1.1f), 40f);
            }
        }

        // The PA announcement follows the chime after a short pause
        private readonly List<(Vector3 pos, float at)> _pending = new List<(Vector3, float)>();
        private void QueueAnnouncement(Vector3 pos, float delay) => _pending.Add((pos, Time.time + delay));

        private void TimedEvents(Vector3 cam, float dt)
        {
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                if (Time.time < _pending[i].at) continue;
                PlayAt("pa_announcement", _pending[i].pos, 0.65f, 1f, 90f);
                _pending.RemoveAt(i);
            }

            if (Weather == null) return;
            if (Weather.Lightning > 0.05f && _lastLightning <= 0.05f && _thunderDelay < 0f)
                _thunderDelay = _rng.Range(0.4f, 2.8f); // sound arrives after the flash
            _lastLightning = Weather.Lightning;
            if (_thunderDelay >= 0f)
            {
                _thunderDelay -= dt;
                if (_thunderDelay < 0f)
                    PlayAt("thunder", cam + new Vector3(_rng.Range(-40f, 40f), 30f, _rng.Range(-40f, 40f)), 1f, _rng.Range(0.8f, 1.1f), 400f);
            }
        }

        /// <summary>"Tick-tick" accessible crossing beeps at the nearest signal while pedestrians may walk.</summary>
        private void CrossingBeeps(Vector3 cam, float dt)
        {
            if (Signals == null || _ctx == null) return;
            _beepTimer -= dt;
            if (_beepTimer > 0f) return;
            int nearest = -1;
            float bestD = 18f;
            foreach (int node in Signals.Nodes)
            {
                Vector3 p = _ctx.Map.Graph.Node(node).Position.XZ.ToVector3(0f);
                float d = Vector3.Distance(p, cam);
                if (d < bestD) { bestD = d; nearest = node; }
            }
            if (nearest < 0) { _beepTimer = 0.5f; return; }
            float left = Signals.WalkTimeLeft(nearest);
            if (left <= 0f) { _beepTimer = 0.25f; return; }
            _beepTimer = left < 2.5f ? 0.15f : 0.5f; // speeds up when time is running out
            Vector3 at = _ctx.Map.Graph.Node(nearest).Position.XZ.ToVector3(0.6f);
            PlayAt("ped_beep", at, 0.35f, 1f, 22f);
        }
    }
}
