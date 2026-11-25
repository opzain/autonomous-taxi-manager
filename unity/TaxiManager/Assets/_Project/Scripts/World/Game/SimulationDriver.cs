using System.Collections.Generic;
using TaxiSim.Core;
using TaxiSim.Core.Dispatch;
using TaxiSim.Core.Sim;
using UnityEngine;

namespace TaxiSim.World
{
    /// <summary>
    /// Runs the dispatch simulation (TaxiSim.Core) inside the 3D city:
    ///  * steps the deterministic simulation in fixed ticks (1 tick = 15 sim-seconds) at 1× / 2× / 4× / 8×;
    ///  * drives the six white AT fleet taxis and the orange RapidCab cars along their simulated routes
    ///    (smoothly interpolated between ticks; the rest of the traffic queues behind them);
    ///  * shows passenger requests as glowing pins and the selected job's routes as ribbons;
    ///  * mirrors simulation traffic, road closures and monsoon rain onto the world, and the sim clock onto the sky;
    ///  * carries out the console's camera requests.
    /// The 3D world only DRAWS the simulation; every decision goes through Simulation.Submit (via DispatchConsole).
    /// </summary>
    public sealed class SimulationDriver : MonoBehaviour
    {
        [Tooltip("Real seconds per simulated minute at 1x speed (the 06:00-22:00 shift then lasts 48 minutes).")]
        public float SecondsPerSimMinute = 3f;

        [System.NonSerialized] public WorldContext Ctx;
        public TrafficAmbient Traffic;
        public TimeOfDay Clock;
        public WeatherSystem Weather;
        public CityCameraRig Rig;
        public CityPulse Pulse;
        public WorldOverlay Overlay;
        public GameMenu Menu;

        public Simulation Sim { get; private set; }
        public readonly DispatchConsole Console = new DispatchConsole();
        public bool ShiftLoaded => Sim != null;
        /// <summary>0..1 progress towards the next simulation tick (for smooth motion).</summary>
        public float TickAlpha { get; private set; }
        /// <summary>While true (default) the sky shows the shift time; Settings can free it (looks only, the shift clock is unchanged).</summary>
        public bool SkyFollowsShift = true;
        /// <summary>A shift is running, so by default it drives the sky clock.</summary>
        public bool OwnsClock => Sim != null && !Sim.IsOver;
        private bool _clockReleased;

        private float _acc;
        private bool _weatherOverridden;
        private WeatherMode _weatherBefore;
        private bool _timeLapseBefore = true;
        private readonly Dictionary<int, GameObject> _pins = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, TrafficAmbient.RoadBlock> _blocks = new Dictionary<int, TrafficAmbient.RoadBlock>();
        private readonly Dictionary<int, GameObject> _barriers = new Dictionary<int, GameObject>();
        private readonly List<GameObject> _ribbons = new List<GameObject>();
        private List<TrafficAmbient.Agent> _rivalAgents = new List<TrafficAmbient.Agent>();
        private int _ribbonRequest = -2, _ribbonTaxi = -2;
        private float _ribbonTimer;
        private Mesh _pinMesh, _pinSmallMesh;
        private GameObject _destPin;

        // ================================================================== shift lifecycle

        public void BeginShift(Difficulty difficulty, ulong seed = 0)
        {
            EndShift();
            if (seed == 0) seed = (ulong)System.DateTime.Now.Ticks;
            Sim = new Simulation(seed, difficulty);
            Console.Attach(Sim);
            Console.SpeedIndex = 0;
            _acc = 0f;
            SkyFollowsShift = true;
            _clockReleased = false;

            if (Clock != null) { _timeLapseBefore = Clock.TimeLapse; Clock.TimeLapse = false; }
            if (Pulse != null) { Pulse.Source = Sim.Graph; Pulse.Recompute(); }
            if (Rig != null) Rig.ArrowsReserved = true;
            if (Overlay != null) Overlay.GameMode = true;

            // The fleet and the rival cars now follow the simulation
            if (Traffic != null)
            {
                for (int i = 0; i < Traffic.Fleet.Count; i++) Traffic.Fleet[i].External = i < Sim.Taxis.Count;
                _rivalAgents = Traffic.RivalAgents();
                for (int i = 0; i < _rivalAgents.Count; i++) _rivalAgents[i].External = i < Sim.Rivals.Count;
            }
            if (Rig != null && Ctx != null)
            {
                CityNode hub = FindNode("fleet_hub");
                if (hub != null) Rig.FlyTo(hub.Position.ToVector3(), 70f, "AT Fleet Hub — shift starting");
            }
            Debug.Log($"[TaxiSim] Dispatch shift started: {difficulty}, seed {seed}.");
        }

        /// <summary>Stops the shift and gives the city back to free-roam mode.</summary>
        public void EndShift()
        {
            if (Sim == null) return;
            Sim = null;
            Console.Attach(null);
            if (Clock != null) Clock.TimeLapse = _timeLapseBefore;
            if (Pulse != null)
            {
                Pulse.Source = null;
                foreach (RoadEdge e in Pulse.Graph.Edges) e.Blocked = false;
                Pulse.Recompute();
            }
            if (Rig != null) Rig.ArrowsReserved = false;
            if (Overlay != null) Overlay.GameMode = false;
            if (_weatherOverridden && Weather != null) Weather.Mode = _weatherBefore;
            _weatherOverridden = false;
            if (Traffic != null)
            {
                foreach (TrafficAmbient.Agent a in Traffic.Agents) a.External = false;
                foreach (TrafficAmbient.RoadBlock b in _blocks.Values) Traffic.Blocks.Remove(b);
            }
            _blocks.Clear();
            foreach (GameObject g in _pins.Values) if (g != null) Destroy(g);
            _pins.Clear();
            foreach (GameObject g in _barriers.Values) if (g != null) Destroy(g);
            _barriers.Clear();
            ClearRibbons();
        }

        // ================================================================== frame

        private void Update()
        {
            if (Console.StartRequested)
            {
                Console.StartRequested = false;
                if (Menu != null && Menu.IsOpen) Menu.CloseAll();
                BeginShift(Console.MenuDifficulty);
            }
            if (Sim == null) return;

            float dt = Time.deltaTime; // 0 while the pause menu is open
            if (!Console.Paused && !Sim.IsOver && dt > 0f)
            {
                float interval = Mathf.Max(0.02f, SecondsPerSimMinute * (float)Sim.Config.MinutesPerTick);
                _acc += Mathf.Min(dt, 0.1f) * Console.Speed;
                int steps = 0;
                while (_acc >= interval && steps < 40)
                {
                    Sim.Step();
                    _acc -= interval;
                    steps++;
                }
                if (steps > 0) OnTicks();
                if (steps >= 40) _acc = 0f;
                TickAlpha = Mathf.Clamp01(_acc / interval);
            }
            Console.PollMessages();

            if (Clock != null)
            {
                if (OwnsClock && SkyFollowsShift)
                    Clock.Hour = Mathf.Repeat((float)Sim.Hour + TickAlpha * (float)Sim.Config.MinutesPerTick / 60f, 24f);
                else if (!OwnsClock && !_clockReleased)
                {
                    // Shift over: give the sky back (time-lapse as it was before the shift)
                    Clock.TimeLapse = _timeLapseBefore;
                    _clockReleased = true;
                }
            }
            CameraRequests(Time.unscaledDeltaTime);
            UpdateVehicles(dt);
            UpdatePins();
            UpdateRibbons(Time.unscaledDeltaTime);
        }

        private void OnTicks()
        {
            if (Pulse != null) Pulse.Recompute();
            SyncWeather();
            SyncClosures();
        }

        private void SyncWeather()
        {
            if (Weather == null) return;
            if (Sim.Raining && !_weatherOverridden)
            {
                _weatherBefore = Weather.Mode;
                Weather.Mode = WeatherMode.Monsoon;
                _weatherOverridden = true;
            }
            else if (!Sim.Raining && _weatherOverridden)
            {
                Weather.Mode = _weatherBefore;
                _weatherOverridden = false;
            }
        }

        private void SyncClosures()
        {
            if (Traffic == null || Ctx == null) return;
            foreach (RoadEdge e in Sim.Graph.Edges)
            {
                bool has = _blocks.ContainsKey(e.Id);
                if (e.Blocked == has) continue;
                if (e.Blocked && Ctx.Roads.ByEdge.TryGetValue(e.Id, out RoadPath path))
                {
                    var b = new TrafficAmbient.RoadBlock { EdgeId = e.Id, FromA = path.Length * 0.46f, ToA = path.Length * 0.54f };
                    _blocks[e.Id] = b;
                    Traffic.Blocks.Add(b);
                    _barriers[e.Id] = Barrier(path);
                }
                else if (has)
                {
                    Traffic.Blocks.Remove(_blocks[e.Id]);
                    _blocks.Remove(e.Id);
                    if (_barriers.TryGetValue(e.Id, out GameObject g) && g != null) Destroy(g);
                    _barriers.Remove(e.Id);
                }
            }
        }

        // ================================================================== vehicles

        private void UpdateVehicles(float dt)
        {
            if (Traffic == null || Ctx == null) return;
            for (int i = 0; i < Sim.Taxis.Count && i < Traffic.Fleet.Count; i++) Drive(Traffic.Fleet[i], Sim.Taxis[i], dt, 0);
            for (int i = 0; i < Sim.Rivals.Count && i < _rivalAgents.Count; i++) Drive(_rivalAgents[i], Sim.Rivals[i], dt, 1);
        }

        private static bool Advancing(Taxi t) =>
            t.IsMoving && (t.State == TaxiState.ToPickup || t.State == TaxiState.OnRide || t.State == TaxiState.ToCharger || t.State == TaxiState.Relocating);

        private void Drive(TrafficAmbient.Agent a, Taxi t, float dt, int lane)
        {
            float frame = Mathf.Max(dt, 1e-4f);
            if (t.IsMoving)
            {
                RoadEdge e = Sim.Graph.Edges[t.CurrentEdge];
                if (!Ctx.Roads.ByEdge.TryGetValue(e.Id, out RoadPath path)) return;
                bool forward = e.A == t.RouteNodes[t.RouteIndex];
                double progress = t.Progress;
                float speed = 0f;
                if (Advancing(t) && !Console.Paused)
                {
                    double edgeMinutes = System.Math.Max(0.05, e.CurrentMinutes);
                    progress = System.Math.Min(1.0, progress + TickAlpha * Sim.Config.MinutesPerTick / edgeMinutes);
                    speed = path.Length / (float)(edgeMinutes * SecondsPerSimMinute) * Console.Speed;
                }
                float target = (float)progress * path.Length;
                // Smooth out the tiny jump when a tick lands (never drive backwards on the same road)
                float shown = a.Path == path && a.Forward == forward ? Mathf.MoveTowards(a.Distance, target, Mathf.Max(speed * 2f, 2f) * frame) : target;
                if (Mathf.Abs(shown - target) > 3f) shown = target;
                Traffic.PlaceExternal(a, path, forward, shown, lane, speed, dt);
            }
            else
            {
                // Parked at a node (idle, charging, waiting): stand just before the junction on a road that touches it
                RoadPath path = a.Path;
                bool touches = path != null && (path.Edge.A == t.Node || path.Edge.B == t.Node);
                if (!touches)
                {
                    IReadOnlyList<RoadEdge> roads = Sim.Graph.EdgesAt(t.Node);
                    if (roads.Count == 0 || !Ctx.Roads.ByEdge.TryGetValue(roads[0].Id, out path)) return;
                }
                bool forward = path.Edge.B == t.Node;            // drive towards the node
                float d = Mathf.Max(0f, path.Length - 0.9f - lane * 0.8f - (t.Id % 6) * 0.7f);
                float shown = a.Path == path && a.Forward == forward ? Mathf.MoveTowards(a.Distance, d, 4f * frame) : d;
                Traffic.PlaceExternal(a, path, forward, shown, path.LanesPerDirection - 1, 0f, dt);
            }
        }

        // ================================================================== request pins and routes

        private void UpdatePins()
        {
            if (Ctx == null) return;
            if (_pinMesh == null)
            {
                var mb = new MeshBuilder();
                mb.AddCylinder(Vector3.zero, 0.55f, 0.55f, 7f, 16, Color.white, capTop: false);
                _pinMesh = mb.ToMesh("RequestPin");
                var mb2 = new MeshBuilder();
                mb2.AddCylinder(Vector3.zero, 0.8f, 0.8f, 3f, 20, Color.white, capTop: false);
                _pinSmallMesh = mb2.ToMesh("DestinationPin");
            }

            var stack = new Dictionary<int, int>();
            var live = new HashSet<int>();
            foreach (RideRequest r in Sim.Requests)
            {
                if (r.Status != RequestStatus.Waiting && r.Status != RequestStatus.Assigned) continue;
                live.Add(r.Id);
                if (!_pins.TryGetValue(r.Id, out GameObject pin) || pin == null)
                {
                    Material m = r.Urgency == Urgency.Medical ? Ctx.Mats.HoloRed : r.Urgency == Urgency.Priority ? Ctx.Mats.HoloGold : Ctx.Mats.HoloGreen;
                    pin = Spawn($"Request #{r.Id}", _pinMesh, m, Vector3.zero);
                    _pins[r.Id] = pin;
                }
                stack.TryGetValue(r.Origin, out int k);
                stack[r.Origin] = k + 1;
                Vector3 p = NodePos(r.Origin);
                float ang = k * 1.9f;
                p += new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * (k == 0 ? 0f : 1.6f + 0.4f * k);
                bool selected = r.Id == Console.SelectedRequestId;
                float pulse = selected ? 1.25f + 0.25f * Mathf.Sin(Time.unscaledTime * 6f) : r.Status == RequestStatus.Assigned ? 0.55f : 1f;
                pin.transform.position = p;
                pin.transform.localScale = new Vector3(pulse, r.Status == RequestStatus.Assigned ? 0.5f : 1f, pulse);
            }
            var dead = new List<int>();
            foreach (KeyValuePair<int, GameObject> kv in _pins) if (!live.Contains(kv.Key)) dead.Add(kv.Key);
            foreach (int id in dead) { if (_pins[id] != null) Destroy(_pins[id]); _pins.Remove(id); }

            // Destination marker of the selected request
            RideRequest sel = Console.Page == ConsolePage.Dispatch && !Console.CameraMode ? Sim.Request(Console.SelectedRequestId) : null;
            if (sel != null && sel.Status == RequestStatus.Waiting)
            {
                if (_destPin == null) _destPin = Spawn("Destination", _pinSmallMesh, Ctx.Mats.HoloCyan, Vector3.zero);
                _destPin.SetActive(true);
                _destPin.transform.position = NodePos(sel.Destination);
            }
            else if (_destPin != null) _destPin.SetActive(false);
        }

        private void UpdateRibbons(float dt)
        {
            RideRequest sel = Console.Page == ConsolePage.Dispatch && !Console.CameraMode ? Console.SelectedRequest() : null;
            DispatchCandidate cand = sel != null ? Console.ChosenCandidate(sel) : null;
            int reqId = sel != null ? sel.Id : -1, taxiId = cand != null ? cand.TaxiId : -1;
            _ribbonTimer -= dt;
            if (reqId == _ribbonRequest && taxiId == _ribbonTaxi && _ribbonTimer > 0f) return;
            _ribbonRequest = reqId;
            _ribbonTaxi = taxiId;
            _ribbonTimer = 1.5f;
            ClearRibbons();
            if (sel == null) return;

            RouteResult trip = RouteFinder.FindFastest(Sim.Graph, sel.Origin, sel.Destination);
            if (trip.Success) _ribbons.Add(Ribbon("Trip route", trip, Ctx.Mats.RouteCyan, 0.7f));
            if (cand != null)
            {
                Taxi t = Sim.Taxis[cand.TaxiId];
                int start = t.IsMoving ? t.NextNode : t.Node;
                RouteResult pickup = RouteFinder.FindFastest(Sim.Graph, start, sel.Origin);
                if (pickup.Success && pickup.Edges.Count > 0) _ribbons.Add(Ribbon("Pickup route", pickup, Ctx.Mats.RouteOrange, 0.45f));
            }
        }

        private GameObject Ribbon(string name, RouteResult route, Material m, float width)
        {
            var pts = new List<Vector3>();
            int node = route.From;
            foreach (int edgeId in route.Edges)
            {
                if (!Ctx.Roads.ByEdge.TryGetValue(edgeId, out RoadPath path)) continue;
                bool forward = path.Edge.A == node;
                int count = path.Points.Length;
                for (int i = 0; i < count; i++)
                {
                    Vector3 p = path.Points[forward ? i : count - 1 - i];
                    if (pts.Count > 0 && (pts[pts.Count - 1] - p).sqrMagnitude < 1e-4f) continue;
                    pts.Add(p);
                }
                node = path.Edge.Other(node);
            }
            if (pts.Count < 2) return null;
            var mb = new MeshBuilder();
            mb.AddRibbon(pts, width, Color.white, 0.14f);
            return Spawn(name, mb.ToMesh(name), m, Vector3.zero);
        }

        private void ClearRibbons()
        {
            foreach (GameObject g in _ribbons) if (g != null) Destroy(g);
            _ribbons.Clear();
            _ribbonRequest = _ribbonTaxi = -2;
        }

        private GameObject Barrier(RoadPath path)
        {
            Vector3 mid = path.Sample(path.Length * 0.5f, out Vector3 fwd);
            var mb = new MeshBuilder();
            Quaternion rot = Quaternion.LookRotation(new Vector3(fwd.x, 0f, fwd.z).sqrMagnitude > 1e-6f ? new Vector3(fwd.x, 0f, fwd.z) : Vector3.forward);
            mb.AddBox(rot * new Vector3(0f, 0.18f, -0.5f), rot, new Vector3(path.Width + 0.4f, 0.22f, 0.12f), Color.white);
            mb.AddBox(rot * new Vector3(0f, 0.18f, 0.5f), rot, new Vector3(path.Width + 0.4f, 0.22f, 0.12f), Color.white);
            GameObject g = Spawn("Road closed", mb.ToMesh("Barrier"), Ctx.Mats.Warning, mid);
            var beam = new MeshBuilder();
            beam.AddCylinder(Vector3.zero, 0.35f, 0.35f, 5f, 12, Color.white, capTop: false);
            GameObject b = Spawn("Closure beacon", beam.ToMesh("ClosureBeacon"), Ctx.Mats.HoloRed, mid);
            b.transform.SetParent(g.transform, true);
            return g;
        }

        private GameObject Spawn(string name, Mesh mesh, Material m, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = pos;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = m;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        // ================================================================== camera

        private void CameraRequests(float dt)
        {
            if (Rig == null) return;
            if (Console.CameraMode)
            {
                if (Console.PanX != 0 || Console.PanY != 0) Rig.Pan(new Vector2(Console.PanX, Console.PanY).normalized, dt);
                if (Console.Orbit != 0) Rig.Orbit(Console.Orbit * 70f * dt, 0f);
            }
            if (Console.ZoomSteps != 0) { Rig.Zoom(Console.ZoomSteps * 0.6f); Console.ZoomSteps = 0; }
            if (Console.FollowNextRequested) { Console.FollowNextRequested = false; Rig.FollowNextTaxi(); }
            if (Console.HomeRequested) { Console.HomeRequested = false; Rig.Home(); }
            if (Console.FollowTaxiRequest >= 0) { Rig.FollowFleet(Console.FollowTaxiRequest); Console.FollowTaxiRequest = -1; }
            if (Console.FocusNodeRequest >= 0)
            {
                CityNode n = Sim.Graph.Node(Console.FocusNodeRequest);
                Rig.FlyTo(NodePos(n.Id), 40f, n.Name);
                Console.FocusNodeRequest = -1;
            }
        }

        // ================================================================== helpers

        public Vector3 NodePos(int node)
        {
            Vector3 p = Sim != null ? Sim.Graph.Node(node).Position.ToVector3() : Ctx.Map.Graph.Node(node).Position.ToVector3();
            if (Ctx != null) p.y = Mathf.Max(Ctx.GroundY(p.x, p.z), (float)CityGeography.WaterLevel) + 0.05f;
            return p;
        }

        private CityNode FindNode(string key)
        {
            foreach (CityNode n in Ctx.Map.Graph.Nodes) if (n.Key == key) return n;
            return null;
        }
    }
}
