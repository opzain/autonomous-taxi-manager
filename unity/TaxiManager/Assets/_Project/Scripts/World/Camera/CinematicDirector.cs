using System.Collections.Generic;
using TaxiSim.Core;
using UnityEngine;

namespace TaxiSim.World
{
    /// <summary>
    /// Cinematic mode (C) for the demo video and viva: an automatic film director.
    ///  * 10 shot types: title aerial, landmark orbit, highway flyover, tracking shot of an autonomous
    ///    taxi, chase cam behind a bus / truck / auto, street-level dolly through the stalls and crowds,
    ///    crane-up from a signalised junction to the skyline, push-in on the AT Fleet Hub gate,
    ///    a low signal-junction shot, and a sunset/night time-lapse of the skyline.
    ///  * Cuts every 6–10 s with alternating hard cuts and dips to black; never repeats a shot type twice.
    ///  * 2.39:1 letterbox, an opening title card, lower-third captions naming the place or vehicle,
    ///    shallow depth of field on the subject, motion blur and film grain.
    ///  * Aerial shots stay above the rooftops (a height grid from the building records).
    /// C toggles, J skips to the next shot, any movement key or Esc hands the camera back.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class CinematicDirector : MonoBehaviour
    {
        private enum ShotType { TitleAerial, LandmarkOrbit, Flyover, TrackingTaxi, Chase, StreetDolly, CraneUp, FleetHubPush, SignalJunction, Timelapse }

        private struct Shot
        {
            public ShotType Type;
            public float Duration;
            public Vector3 A, B;           // start / end camera positions (static shots) or orbit centre
            public Vector3 LookA, LookB;   // start / end look-at points
            public float FovA, FovB;
            public float Radius, Height, Yaw0, Sweep;
            public Transform Subject;
            public bool AerialSafe;        // keep above rooftops
            public string Title, Subtitle;
        }

        public Camera Cam;
        public CityCameraRig Rig;
        public PostFxRig Post;
        public WorldOverlay Overlay;
        public TimeOfDay Clock;
        public TrafficAmbient Traffic;
        public PedestrianSystem Pedestrians;
        public TrafficSignals Signals;
        public GameMenu Menu;

        public bool Active { get; private set; }

        private WorldContext _ctx;
        private Pcg32 _rng;
        private Shot _shot;
        private float _t;
        private float _fade, _letterbox, _captionAlpha, _titleCard;
        private bool _dipToBlack;
        private ShotType _last = ShotType.Timelapse;
        private int _shotCount;
        private readonly Dictionary<long, float> _roofs = new Dictionary<long, float>();
        private const float Cell = 6f;

        // State restored when leaving
        private bool _prevMotion, _prevExtras, _prevTimeLapse;
        private float _prevHoursPerSecond, _prevNear, _prevFov;

        private GUIStyle _titleStyle, _subStyle, _bigTitle, _bigSub;

        public void Init(WorldContext ctx)
        {
            _ctx = ctx;
            _rng = new Pcg32(2401UL);
            foreach (BuildingRecord r in ctx.Towers.Records)
            {
                float rad = 0.5f * Mathf.Sqrt(r.Width * r.Width + r.Depth * r.Depth);
                for (float x = r.Center.x - rad; x <= r.Center.x + rad + Cell; x += Cell)
                    for (float z = r.Center.y - rad; z <= r.Center.y + rad + Cell; z += Cell)
                    {
                        long k = CellKey(x, z);
                        if (!_roofs.TryGetValue(k, out float top) || r.Top > top) _roofs[k] = r.Top;
                    }
            }
        }

        private static long CellKey(float x, float z) => ((long)Mathf.FloorToInt(x / Cell) << 32) ^ (uint)Mathf.FloorToInt(z / Cell);

        private float RoofHeight(Vector3 p)
        {
            float top = 0f;
            for (int i = -1; i <= 1; i++)
                for (int j = -1; j <= 1; j++)
                    if (_roofs.TryGetValue(CellKey(p.x + i * Cell, p.z + j * Cell), out float h)) top = Mathf.Max(top, h);
            return top;
        }

        // ------------------------------------------------------------------ on / off

        public void Toggle()
        {
            if (Active) Stop(); else Begin();
        }

        public void Begin()
        {
            if (Cam == null || _ctx == null) return;
            Active = true;
            if (Rig != null) { Rig.StopFollow(); Rig.ExternalControl = true; }
            if (Overlay != null) Overlay.Suppressed = true;
            if (Post != null)
            {
                _prevMotion = Post.MotionBlurOn;
                _prevExtras = Post.CinematicExtras;
                Post.SetMotionBlur(true);
                Post.SetCinematicExtras(true);
            }
            if (Clock != null) { _prevTimeLapse = Clock.TimeLapse; _prevHoursPerSecond = Clock.HoursPerSecond; }
            _prevNear = Cam.nearClipPlane;
            _prevFov = Cam.fieldOfView;
            _shotCount = 0;
            _titleCard = 5.5f;
            _letterbox = 0f;
            NextShot(ShotType.TitleAerial);
        }

        public void Stop()
        {
            if (!Active) return;
            Active = false;
            RestoreClock();
            if (Post != null)
            {
                Post.SetMotionBlur(_prevMotion);
                Post.SetCinematicExtras(_prevExtras);
                Post.SetCinematicLens(false);
            }
            Cam.nearClipPlane = _prevNear;
            Cam.fieldOfView = _prevFov;
            if (Overlay != null) Overlay.Suppressed = false;
            if (Rig != null) { Rig.ExternalControl = false; Rig.SyncFromCamera(); }
        }

        private void RestoreClock()
        {
            if (Clock == null) return;
            Clock.TimeLapse = _prevTimeLapse;
            Clock.HoursPerSecond = _prevHoursPerSecond;
        }

        // ------------------------------------------------------------------ update

        private void Update()
        {
            if (InputCompat.Pressed(TsKey.C) && (Menu == null || !Menu.IsOpen)) Toggle();
            if (!Active) return;
            if (InputCompat.Pressed(TsKey.J)) _t = _shot.Duration; // skip
            if (InputCompat.Pressed(TsKey.Escape) || InputCompat.Pressed(TsKey.W) || InputCompat.Pressed(TsKey.A) ||
                InputCompat.Pressed(TsKey.S) || InputCompat.Pressed(TsKey.D) || InputCompat.Pressed(TsKey.Up) ||
                InputCompat.Pressed(TsKey.Down) || InputCompat.Pressed(TsKey.Left) || InputCompat.Pressed(TsKey.Right))
                Stop();
        }

        private void LateUpdate()
        {
            if (!Active) return;
            float dt = Time.unscaledDeltaTime;
            _t += dt;
            _letterbox = Mathf.MoveTowards(_letterbox, 1f, dt * 1.2f);
            _titleCard = Mathf.Max(0f, _titleCard - dt);

            // Transitions: dip to black over the last / first 0.45 s, or a hard cut
            float remaining = _shot.Duration - _t;
            if (_dipToBlack && remaining < 0.45f) _fade = Mathf.Clamp01(1f - remaining / 0.45f);
            else _fade = Mathf.MoveTowards(_fade, 0f, dt / 0.45f);
            _captionAlpha = Mathf.Clamp01(Mathf.Min((_t - 0.6f) / 0.6f, (remaining - 0.3f) / 0.6f));

            if (_t >= _shot.Duration || (_shot.Subject == null && NeedsSubject(_shot.Type)))
            {
                RestoreClock();
                NextShot(Pick());
            }
            Frame(dt);
        }

        private static bool NeedsSubject(ShotType t) => t == ShotType.TrackingTaxi || t == ShotType.Chase;

        private ShotType Pick()
        {
            ShotType[] pool =
            {
                ShotType.LandmarkOrbit, ShotType.Flyover, ShotType.TrackingTaxi, ShotType.Chase, ShotType.StreetDolly,
                ShotType.CraneUp, ShotType.FleetHubPush, ShotType.SignalJunction, ShotType.LandmarkOrbit, ShotType.TrackingTaxi,
                ShotType.StreetDolly, ShotType.Timelapse
            };
            for (int i = 0; i < 10; i++)
            {
                ShotType t = pool[_rng.Range(0, pool.Length)];
                if (t != _last) return t;
            }
            return ShotType.LandmarkOrbit;
        }

        // ------------------------------------------------------------------ shot set-up

        private void NextShot(ShotType type)
        {
            _t = 0f;
            _shotCount++;
            _dipToBlack = _shotCount % 2 == 0;
            var s = new Shot { Type = type, Duration = _rng.Range(6.5f, 9.5f), FovA = 40f, FovB = 40f };
            bool ok = Setup(ref s);
            if (!ok)
            {
                s = new Shot { Type = ShotType.LandmarkOrbit, Duration = 8f, FovA = 40f, FovB = 40f };
                Setup(ref s);
            }
            _shot = s;
            _last = s.Type;
        }

        private bool Setup(ref Shot s)
        {
            List<LandmarkInfo> lms = _ctx.Landmarks;
            switch (s.Type)
            {
                case ShotType.TitleAerial:
                {
                    var centre = new Vector3(175f, 8f, 120f);
                    s.Duration = 9f;
                    s.A = centre; s.Radius = 110f; s.Height = 55f; s.Yaw0 = 200f; s.Sweep = 25f;
                    s.LookA = s.LookB = centre;
                    s.FovA = 38f; s.FovB = 34f;
                    s.AerialSafe = true;
                    s.Title = "Bandra Kurla Complex"; s.Subtitle = "Mumbai · BKC → Equinox / ATLAS → Kurla";
                    return true;
                }
                case ShotType.LandmarkOrbit:
                {
                    if (lms.Count == 0) return false;
                    LandmarkInfo lm = lms[_rng.Range(0, lms.Count)];
                    Vector3 c = lm.Position; c.y *= 0.55f;
                    s.A = c; s.Radius = _rng.Range(32f, 60f); s.Height = _rng.Range(14f, 30f);
                    s.Yaw0 = _rng.Range(0f, 360f); s.Sweep = _rng.Range(-40f, 40f);
                    s.LookA = s.LookB = c;
                    s.FovA = 36f; s.FovB = 32f;
                    s.AerialSafe = true;
                    s.Title = lm.Name; s.Subtitle = lm.Subtitle;
                    return true;
                }
                case ShotType.Flyover:
                {
                    RoadPath path = PickRoad(r => r.Edge.Class >= RoadClass.Main && r.Length > 30f);
                    if (path == null) return false;
                    bool fwd = _rng.Chance(0.5);
                    float s0 = path.Length * 0.15f, s1 = path.Length * 0.85f;
                    Vector3 p0 = path.Sample(fwd ? s0 : s1, out Vector3 dir);
                    Vector3 p1 = path.Sample(fwd ? s1 : s0, out _);
                    Vector3 side = Vector3.Cross(Vector3.up, (p1 - p0).normalized) * _rng.Range(-8f, 8f);
                    float h = _rng.Range(16f, 28f);
                    s.A = p0 + side + Vector3.up * h;
                    s.B = Vector3.Lerp(p0, p1, 0.75f) + side + Vector3.up * (h + 4f);
                    s.LookA = Vector3.Lerp(p0, p1, 0.35f);
                    s.LookB = p1;
                    s.FovA = 45f; s.FovB = 42f;
                    s.AerialSafe = true;
                    s.Title = path.Edge.Name; s.Subtitle = $"{path.NodeA.ShortName} ↔ {path.NodeB.ShortName}";
                    return true;
                }
                case ShotType.TrackingTaxi:
                {
                    if (Traffic == null || Traffic.Fleet.Count == 0) return false;
                    TrafficAmbient.Agent a = Traffic.Fleet[_rng.Range(0, Traffic.Fleet.Count)];
                    s.Subject = a.Transform;
                    s.FovA = 30f; s.FovB = 34f;
                    s.Title = a.Label; s.Subtitle = "Autonomous EV taxi · AT Fleet";
                    return true;
                }
                case ShotType.Chase:
                {
                    if (Traffic == null) return false;
                    TrafficAmbient.Agent pick = null;
                    for (int i = 0; i < 30 && pick == null; i++)
                    {
                        TrafficAmbient.Agent a = Traffic.Agents[_rng.Range(0, Traffic.Agents.Count)];
                        if (a.Kind == VehicleKind.BestBus || a.Kind == VehicleKind.Truck || a.Kind == VehicleKind.AutoRickshaw ||
                            a.Kind == VehicleKind.KaaliPeeli || a.Kind == VehicleKind.Scooter) pick = a;
                    }
                    if (pick == null) return false;
                    s.Subject = pick.Transform;
                    s.FovA = 52f; s.FovB = 48f;
                    s.Title = KindName(pick.Kind); s.Subtitle = pick.Path.Edge.Name;
                    return true;
                }
                case ShotType.StreetDolly:
                {
                    RoadPath path = PickRoad(r => !r.Edge.Elevated && r.Length > 12f);
                    if (path == null) return false;
                    float d = _rng.Range(path.Length * 0.2f, path.Length * 0.8f);
                    Vector3 p = path.Sample(d, out Vector3 fwd);
                    if (p.y > 0.1f) return false;
                    fwd.y = 0f; fwd.Normalize();
                    int side = _rng.Chance(0.5) ? 1 : -1;
                    Vector3 right = new Vector3(fwd.z, 0, -fwd.x) * side;
                    float lat = path.Width * 0.5f - 0.2f;
                    s.A = p + right * lat + Vector3.up * 0.32f;
                    s.B = s.A + fwd * 3.5f;
                    s.LookA = s.A + fwd * 6f + right * 1.6f + Vector3.up * 0.1f;
                    s.LookB = s.B + fwd * 6f + right * 1.2f + Vector3.up * 0.1f;
                    s.FovA = 46f; s.FovB = 44f;
                    s.Title = "Street life"; s.Subtitle = path.Edge.Name;
                    return true;
                }
                case ShotType.CraneUp:
                case ShotType.SignalJunction:
                {
                    if (Signals == null || Signals.Count == 0) return false;
                    var nodes = new List<int>(Signals.Nodes);
                    CityNode node = _ctx.Map.Graph.Node(nodes[_rng.Range(0, nodes.Count)]);
                    Vector3 c = node.Position.XZ.ToVector3(0.1f);
                    float jr = _ctx.Roads.JunctionRadius.TryGetValue(node.Id, out float r) ? r : 1.5f;
                    float ang = _rng.Range(0f, 360f) * Mathf.Deg2Rad;
                    Vector3 dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                    if (s.Type == ShotType.CraneUp)
                    {
                        s.A = c + dir * (jr + 2.2f) + Vector3.up * 0.45f;
                        s.B = c + dir * (jr + 14f) + Vector3.up * 34f;
                        s.LookA = c + Vector3.up * 0.3f;
                        s.LookB = new Vector3(175f, 12f, 120f);
                        s.FovA = 50f; s.FovB = 38f;
                        s.Duration = 10f;
                        s.Title = node.Name; s.Subtitle = "Signalised junction";
                    }
                    else
                    {
                        Vector3 corner = c + dir * (jr + 1.6f) + Quaternion.Euler(0, 90f, 0) * dir * (jr + 0.6f);
                        s.A = corner + Vector3.up * 0.55f;
                        s.B = s.A + Quaternion.Euler(0, 90f, 0) * dir * 0.8f;
                        s.LookA = c + Vector3.up * 0.4f;
                        s.LookB = c + Vector3.up * 0.4f + dir * 1.2f;
                        s.FovA = 55f; s.FovB = 52f;
                        s.Title = node.ShortName; s.Subtitle = "Traffic signal · pedestrians cross on green";
                    }
                    return true;
                }
                case ShotType.FleetHubPush:
                {
                    LandmarkInfo hub = lms.Find(l => l.Name == "AT Fleet Hub");
                    if (hub == null) return false;
                    Vector3 target = hub.Position; target.y = _ctx.GroundY(target.x, target.z) + 0.8f;
                    CityNode node = hub.NodeId >= 0 ? _ctx.Map.Graph.Node(hub.NodeId) : null;
                    Vector3 approach = node != null ? (node.Position.XZ.ToVector3(0f) - new Vector3(target.x, 0f, target.z)) : Vector3.forward;
                    approach.y = 0f;
                    approach = approach.sqrMagnitude > 0.01f ? approach.normalized : Vector3.forward;
                    Vector3 side = Vector3.Cross(Vector3.up, approach);
                    s.A = target + approach * 24f + side * 6f + Vector3.up * 7f;
                    s.B = target + approach * 10f + side * 1.5f + Vector3.up * 2.2f;
                    s.LookA = s.LookB = target;
                    s.FovA = 42f; s.FovB = 38f;
                    s.Duration = 9f;
                    s.Title = "AT Fleet Hub"; s.Subtitle = "Where every autonomous taxi begins its shift";
                    return true;
                }
                case ShotType.Timelapse:
                {
                    var centre = new Vector3(175f, 6f, 120f);
                    float yaw = _rng.Range(0f, 360f) * Mathf.Deg2Rad;
                    Vector3 off = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw));
                    s.A = centre + off * 120f + Vector3.up * 38f;
                    s.B = centre + off * 112f + Vector3.up * 40f;
                    s.LookA = s.LookB = centre;
                    s.FovA = 36f; s.FovB = 34f;
                    s.Duration = 10f;
                    s.AerialSafe = true;
                    if (Clock != null) { Clock.TimeLapse = true; Clock.HoursPerSecond = 0.3f; }
                    s.Title = "Mumbai never sleeps"; s.Subtitle = "Time-lapse";
                    return true;
                }
            }
            return false;
        }

        private RoadPath PickRoad(System.Predicate<RoadPath> ok)
        {
            List<RoadPath> paths = _ctx.Roads.Paths;
            for (int i = 0; i < 40; i++)
            {
                RoadPath p = paths[_rng.Range(0, paths.Count)];
                if (ok(p)) return p;
            }
            return null;
        }

        private static string KindName(VehicleKind k)
        {
            switch (k)
            {
                case VehicleKind.BestBus: return "BEST bus";
                case VehicleKind.Truck: return "Goods truck";
                case VehicleKind.AutoRickshaw: return "Auto-rickshaw";
                case VehicleKind.KaaliPeeli: return "Kaali-peeli taxi";
                case VehicleKind.Scooter: return "Scooter";
                default: return k.ToString();
            }
        }

        // ------------------------------------------------------------------ framing

        private void Frame(float dt)
        {
            float u = Mathf.Clamp01(_t / _shot.Duration);
            float e = u * u * (3f - 2f * u); // ease in / out
            Vector3 pos, look;
            float focus;
            switch (_shot.Type)
            {
                case ShotType.TitleAerial:
                case ShotType.LandmarkOrbit:
                {
                    float yaw = (_shot.Yaw0 + _shot.Sweep * e) * Mathf.Deg2Rad;
                    pos = _shot.A + new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw)) * _shot.Radius + Vector3.up * _shot.Height;
                    look = _shot.LookA;
                    break;
                }
                case ShotType.TrackingTaxi:
                case ShotType.Chase:
                {
                    Transform t = _shot.Subject;
                    Vector3 fwd = t.forward; fwd.y = 0f; fwd.Normalize();
                    Vector3 right = new Vector3(fwd.z, 0f, -fwd.x);
                    Vector3 want = _shot.Type == ShotType.TrackingTaxi
                        ? t.position + right * 0.95f + Vector3.up * 0.42f + fwd * Mathf.Lerp(-1.6f, 0.7f, e)   // slides from behind to alongside
                        : t.position - fwd * 2.4f + Vector3.up * 0.7f + right * 0.15f;
                    pos = _t < 0.05f ? want : Vector3.Lerp(Cam.transform.position, want, 1f - Mathf.Exp(-dt * 6f));
                    look = t.position + Vector3.up * 0.15f + fwd * (_shot.Type == ShotType.Chase ? 3f : 0.2f);
                    break;
                }
                default:
                    pos = Vector3.Lerp(_shot.A, _shot.B, e);
                    look = Vector3.Lerp(_shot.LookA, _shot.LookB, e);
                    break;
            }

            // Never inside the ground; aerial shots stay above the rooftops
            float ground = Mathf.Max(_ctx.GroundY(pos.x, pos.z), (float)CityGeography.WaterLevel);
            pos.y = Mathf.Max(pos.y, ground + 0.2f);
            if (_shot.AerialSafe) pos.y = Mathf.Max(pos.y, RoofHeight(pos) + 4f);

            Cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos, Vector3.up));
            Cam.fieldOfView = Mathf.Lerp(_shot.FovA, _shot.FovB, e);
            bool street = pos.y - ground < 3f;
            Cam.nearClipPlane = street ? 0.03f : 0.3f;
            focus = Vector3.Distance(pos, look);
            if (Post != null)
            {
                float focal = Mathf.Lerp(85f, 35f, Mathf.InverseLerp(25f, 60f, Cam.fieldOfView));
                Post.SetCinematicLens(true, focus, street ? 2.0f : 5.6f, focal);
                Post.SetFocus(focus);
            }
        }

        // ------------------------------------------------------------------ letterbox, title and captions

        private void OnGUI()
        {
            if (!Active) return;
            EnsureStyles();
            float s = Mathf.Max(0.75f, Screen.height / 1080f);
            Color old = GUI.color;

            // 2.39:1 letterbox
            float barH = Mathf.Max(0f, (Screen.height - Screen.width / 2.39f) * 0.5f) * Mathf.SmoothStep(0f, 1f, _letterbox);
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, barH), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, Screen.height - barH, Screen.width, barH), Texture2D.whiteTexture);

            // Dip to black
            if (_fade > 0.001f)
            {
                GUI.color = new Color(0f, 0f, 0f, _fade);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            }

            // Opening title card
            if (_titleCard > 0f)
            {
                float a = Mathf.Clamp01(Mathf.Min((5.5f - _titleCard) / 1.2f, _titleCard / 1.2f));
                _bigTitle.fontSize = Mathf.RoundToInt(58 * s);
                _bigSub.fontSize = Mathf.RoundToInt(20 * s);
                GUI.color = new Color(1f, 1f, 1f, a);
                GUI.Label(new Rect(0, Screen.height * 0.4f, Screen.width, 70 * s), "AUTONOMOUS TAXI MANAGER", _bigTitle);
                GUI.color = new Color(0.55f, 0.95f, 1f, a);
                GUI.Label(new Rect(0, Screen.height * 0.4f + 72 * s, Screen.width, 30 * s), "Pinnacle 1 · Project 15  ·  Fleet dispatch simulation · BKC, Mumbai", _bigSub);
            }
            else if (_captionAlpha > 0.01f && !string.IsNullOrEmpty(_shot.Title))
            {
                // Lower third, inside the letterbox picture area
                float x = 60 * s, y = Screen.height - barH - 110 * s;
                _titleStyle.fontSize = Mathf.RoundToInt(30 * s);
                _subStyle.fontSize = Mathf.RoundToInt(17 * s);
                GUI.color = new Color(0.3f, 0.95f, 1f, _captionAlpha);
                GUI.DrawTexture(new Rect(x - 16 * s, y + 6 * s, 4 * s, 62 * s), Texture2D.whiteTexture);
                GUI.color = new Color(1f, 1f, 1f, _captionAlpha);
                GUI.Label(new Rect(x, y, Screen.width * 0.7f, 40 * s), _shot.Title.ToUpperInvariant(), _titleStyle);
                GUI.color = new Color(0.85f, 0.9f, 0.95f, _captionAlpha * 0.9f);
                GUI.Label(new Rect(x, y + 40 * s, Screen.width * 0.7f, 28 * s), _shot.Subtitle ?? "", _subStyle);
            }

            GUI.color = new Color(1f, 1f, 1f, 0.35f);
            _subStyle.fontSize = Mathf.RoundToInt(12 * s);
            GUI.Label(new Rect(16 * s, Screen.height - barH - 24 * s, 600 * s, 20 * s), "CINEMATIC  ·  J next shot  ·  C / Esc exit", _subStyle);
            GUI.color = old;
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null) return;
            _titleStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            _subStyle = new GUIStyle(GUI.skin.label) { normal = { textColor = Color.white } };
            _bigTitle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            _bigSub = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
        }
    }
}
