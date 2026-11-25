using System.Collections.Generic;
using TaxiSim.Core;
using UnityEngine;

namespace TaxiSim.World
{
    /// <summary>
    /// Ambient city traffic on the 3D road graph (left-hand driving, as in India).
    /// v0.7 driving model:
    ///  * every driver has a personality (aggression) and a noisy desired speed, so traffic flows
    ///    irregularly: autos dawdle, scooters weave, buses and trucks lumber, taxis push;
    ///  * car-following: each vehicle keeps a gap of minGap + speed x headway to the one in front in
    ///    its lane and brakes smoothly instead of driving through it;
    ///  * traffic signals: vehicles stop at the stop line on red (and on amber if they can stop);
    ///  * lane changes when stuck behind a slower vehicle and the next lane is free;
    ///  * our autonomous fleet starts from the AT Fleet Hub depot, the rival RapidCab fleet roams.
    /// Visual only; the dispatch simulation will drive the fleet taxis later.
    /// </summary>
    public sealed class TrafficAmbient : MonoBehaviour
    {
        public sealed class Agent
        {
            public Transform Transform;
            public RoadPath Path;
            public bool Forward;
            public int Lane;
            public float LateralLane;   // smoothed lane position (for lane-change animation)
            public float Distance;      // along the path in the travel direction
            public float Speed;         // current speed (units / s)
            public float BaseSpeed;     // cruising speed of this vehicle type and driver
            public float Aggression;    // 0 calm .. 1 pushy (accel, gaps, lane changes)
            public float NoiseSeed;
            public float Length;
            public float Weave;         // scooters drift inside the lane
            public VehicleKind Kind;
            public string Label;
            public Light Headlight;
            public float LaneChangeCooldown;
            public float BlockedTime;   // seconds stuck behind a road blocker (procession) before a U-turn
            /// <summary>Driven by the dispatch simulation (SimulationDriver) instead of the ambient driving model.</summary>
            public bool External;
            public float DistanceFromA => Forward ? Distance : Path.Length - Distance;
        }

        /// <summary>Part of a road that vehicles cannot pass (e.g. a festival procession), measured from node A.</summary>
        public sealed class RoadBlock
        {
            public int EdgeId;
            public float FromA, ToA;
        }

        /// <summary>Optional speed factor (0..1) at a position on a road, e.g. waterlogging. Args: edge id, distance from A.</summary>
        public System.Func<int, float, float> SpeedFactor;
        public readonly List<RoadBlock> Blocks = new List<RoadBlock>();

        public float TimeScale = 1f;
        public TrafficSignals Signals;
        public readonly List<Agent> Agents = new List<Agent>();
        public readonly List<Agent> Fleet = new List<Agent>();

        private CityGraph _graph;
        private RoadNetwork _roads;
        private Pcg32 _rng;
        private readonly Dictionary<long, List<Agent>> _buckets = new Dictionary<long, List<Agent>>();
        private readonly Stack<List<Agent>> _pool = new Stack<List<Agent>>();
        private static readonly System.Comparison<Agent> ByDistance = (a, b) => a.Distance.CompareTo(b.Distance);

        public void Init(WorldContext ctx, int ambientCount, bool fleetHeadlights)
        {
            _graph = ctx.Map.Graph;
            _roads = ctx.Roads;
            _rng = new Pcg32(2024UL);
            var root = new GameObject("Vehicles").transform;
            root.SetParent(transform, false);

            for (int i = 0; i < ambientCount; i++)
            {
                double r = _rng.NextDouble();
                VehicleKind kind = r < 0.26 ? VehicleKind.Sedan
                                 : r < 0.42 ? VehicleKind.KaaliPeeli
                                 : r < 0.62 ? VehicleKind.AutoRickshaw
                                 : r < 0.82 ? VehicleKind.Scooter
                                 : r < 0.90 ? VehicleKind.Truck
                                 : VehicleKind.BestBus;
                Color paint = VehicleFactory.CarPaints[_rng.Range(0, VehicleFactory.CarPaints.Length)];
                Agents.Add(Spawn(ctx, root, kind, paint, CruiseSpeed(kind), null, false, null));
            }

            // Our autonomous fleet leaves from the AT Fleet Hub depot
            CityNode hub = FindNode("fleet_hub");
            for (int i = 0; i < 6; i++)
            {
                Agent a = Spawn(ctx, root, VehicleKind.FleetEv, Color.white, 3.3f, $"AT-0{i + 1}", fleetHeadlights, hub);
                a.Distance = 1.5f + i * 2.2f; // queue out of the depot gate one after another
                a.Aggression = 0.35f;         // the autonomous fleet drives smoothly
                Agents.Add(a);
                Fleet.Add(a);
                AddBeacon(a.Transform, ctx.Mats.HoloCyan);
            }
            for (int i = 0; i < 4; i++)
            {
                Agent a = Spawn(ctx, root, VehicleKind.RivalCab, Color.white, 3.4f, $"RC-0{i + 1}", false, null);
                Agents.Add(a);
                AddBeacon(a.Transform, ctx.Mats.HoloGold);
            }
            foreach (Agent a in Agents) Place(a, 1f);
        }

        private static float CruiseSpeed(VehicleKind k)
        {
            switch (k)
            {
                case VehicleKind.BestBus: return 2.1f;
                case VehicleKind.Truck: return 2.0f;
                case VehicleKind.AutoRickshaw: return 2.3f;
                case VehicleKind.Scooter: return 3.0f;
                case VehicleKind.KaaliPeeli: return 3.0f;
                default: return 3.2f;
            }
        }

        private CityNode FindNode(string key)
        {
            foreach (CityNode n in _graph.Nodes) if (n.Key == key) return n;
            return null;
        }

        private Agent Spawn(WorldContext ctx, Transform root, VehicleKind kind, Color paint, float speed, string label, bool headlight, CityNode startAt)
        {
            GameObject go = VehicleFactory.Spawn(ctx.Mats, kind, paint, root, label ?? kind.ToString());
            var agent = new Agent
            {
                Transform = go.transform,
                Kind = kind,
                Label = label,
                Aggression = _rng.NextFloat(),
                NoiseSeed = _rng.Range(0f, 1000f),
                Length = VehicleFactory.Length(kind),
                Weave = kind == VehicleKind.Scooter ? _rng.Range(0.3f, 1f) : 0f
            };
            agent.BaseSpeed = speed * Mathf.Lerp(0.82f, 1.18f, agent.Aggression);

            if (startAt != null && _graph.EdgesAt(startAt.Id).Count > 0)
            {
                IReadOnlyList<RoadEdge> edges = _graph.EdgesAt(startAt.Id);
                RoadEdge e = edges[_rng.Range(0, edges.Count)];
                agent.Path = _roads.ByEdge[e.Id];
                agent.Forward = e.A == startAt.Id; // drive away from the depot
                agent.Distance = 0f;
            }
            else
            {
                agent.Path = _roads.Paths[_rng.Range(0, _roads.Paths.Count)];
                agent.Forward = _rng.Chance(0.5);
                agent.Distance = _rng.Range(0f, agent.Path.Length);
            }
            // Heavy vehicles keep to the left (outer) lane, scooters and taxis anywhere
            bool heavy = kind == VehicleKind.BestBus || kind == VehicleKind.Truck || kind == VehicleKind.AutoRickshaw;
            agent.Lane = heavy ? agent.Path.LanesPerDirection - 1 : _rng.Range(0, agent.Path.LanesPerDirection);
            agent.LateralLane = agent.Lane;
            agent.Speed = agent.BaseSpeed * 0.6f;

            if (headlight)
            {
                var l = new GameObject("Headlight").AddComponent<Light>();
                l.transform.SetParent(go.transform, false);
                l.transform.localPosition = new Vector3(0, 0.2f, 0.4f);
                l.transform.localRotation = Quaternion.Euler(12f, 0, 0);
                l.type = LightType.Spot;
                l.spotAngle = 70f;
                l.range = 6f;
                l.intensity = 3f;
                l.color = new Color(0.85f, 0.95f, 1f);
                l.shadows = LightShadows.None;
                agent.Headlight = l;
            }
            return agent;
        }

        private static void AddBeacon(Transform parent, Material material)
        {
            var mb = new MeshBuilder();
            mb.AddCylinder(Vector3.zero, 0.22f, 0.22f, 0.5f, 16, Color.white, capTop: false);
            var go = new GameObject("Beacon");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0, 0.32f, 0);
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("VehicleBeacon");
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        public void SetHeadlights(bool on)
        {
            foreach (Agent a in Fleet) if (a.Headlight != null) a.Headlight.enabled = on;
        }

        // ------------------------------------------------------------------------------------------
        private static long Key(int edge, bool forward, int lane) => ((long)edge * 2 + (forward ? 1 : 0)) * 8 + lane;

        private void RebuildBuckets()
        {
            foreach (List<Agent> l in _buckets.Values) { l.Clear(); _pool.Push(l); }
            _buckets.Clear();
            foreach (Agent a in Agents)
            {
                long k = Key(a.Path.Edge.Id, a.Forward, a.Lane);
                if (!_buckets.TryGetValue(k, out List<Agent> list))
                {
                    list = _pool.Count > 0 ? _pool.Pop() : new List<Agent>();
                    _buckets[k] = list;
                }
                list.Add(a);
            }
            foreach (List<Agent> l in _buckets.Values) l.Sort(ByDistance);
        }

        /// <summary>Distance to the next vehicle ahead in a lane (and its speed); large if the lane is clear.</summary>
        private float GapAhead(RoadPath path, bool forward, int lane, float distance, Agent self, out float leaderSpeed)
        {
            leaderSpeed = 99f;
            if (!_buckets.TryGetValue(Key(path.Edge.Id, forward, lane), out List<Agent> list)) return 999f;
            for (int i = 0; i < list.Count; i++)
            {
                Agent o = list[i];
                if (o == self || o.Distance < distance) continue;
                if (o.Distance == distance && self != null && o.GetHashCode() < self.GetHashCode()) continue;
                leaderSpeed = o.Speed;
                return o.Distance - distance - 0.5f * (o.Length + (self != null ? self.Length : 0.6f));
            }
            return 999f;
        }

        /// <summary>Free space behind a position in a lane (for safe lane changes).</summary>
        private float GapBehind(RoadPath path, bool forward, int lane, float distance)
        {
            if (!_buckets.TryGetValue(Key(path.Edge.Id, forward, lane), out List<Agent> list)) return 999f;
            float best = 999f;
            foreach (Agent o in list)
                if (o.Distance <= distance) best = Mathf.Min(best, distance - o.Distance - o.Length);
            return best;
        }

        private void Update()
        {
            if (_roads == null) return;
            float dt = Mathf.Min(Time.deltaTime, 0.1f) * TimeScale;
            if (dt <= 0f) return;
            RebuildBuckets();
            float t = Time.time;

            foreach (Agent a in Agents)
            {
                if (a.External) continue; // positioned by the dispatch simulation (still blocks the cars behind it)
                RoadPath path = a.Path;
                float mult = Mathf.Max((float)path.Edge.TrafficMultiplier, 0.5f);

                // Irregular desired speed: slow drift (driver mood) + quicker wobble
                float mood = Mathf.PerlinNoise(a.NoiseSeed, t * 0.07f);
                float wobble = Mathf.PerlinNoise(a.NoiseSeed + 37.1f, t * 0.6f) - 0.5f;
                float desired = a.BaseSpeed * (0.72f + 0.5f * mood + 0.18f * wobble) / mult;
                if (SpeedFactor != null) desired *= Mathf.Clamp(SpeedFactor(path.Edge.Id, a.DistanceFromA), 0.1f, 1f);

                // Car following
                float headway = Mathf.Lerp(0.75f, 0.32f, a.Aggression);
                float minGap = Mathf.Lerp(0.3f, 0.12f, a.Aggression);
                float gap = GapAhead(path, a.Forward, a.Lane, a.Distance, a, out float leaderSpeed);
                bool blocked = false;
                foreach (RoadBlock b in Blocks)
                {
                    if (b.EdgeId != path.Edge.Id) continue;
                    float start = a.Forward ? b.FromA : path.Length - b.ToA; // block start in travel direction
                    float bg = start - a.Distance - a.Length * 0.5f - 0.15f;
                    if (bg > -0.3f && bg < gap) { gap = Mathf.Max(bg, 0f); leaderSpeed = 0f; blocked = bg < 1.2f; }
                }
                float safe = minGap + a.Speed * headway;
                float target = desired;
                if (gap < safe) target = Mathf.Min(target, Mathf.Max(0f, leaderSpeed * Mathf.Clamp01((gap - minGap) / Mathf.Max(safe - minGap, 0.01f))));
                if (gap < minGap) target = 0f;

                // Traffic signal at the end of this road
                int nodeAhead = a.Forward ? path.Edge.B : path.Edge.A;
                if (Signals != null && Signals.IsSignalised(nodeAhead, path.Edge.Id))
                {
                    // Stop line sits just before the zebra crossing
                    float stopAt = path.Length - Signals.StopDistance(nodeAhead) - a.Length * 0.5f;
                    float toStop = stopAt - a.Distance;
                    if (toStop > -0.05f && toStop < 6f)
                    {
                        SignalState s = Signals.State(nodeAhead, path.Edge.Id);
                        float brakeDist = a.Speed * a.Speed / (2f * 3f);
                        bool mustStop = s == SignalState.Red || (s == SignalState.Amber && toStop > brakeDist * 0.8f)
                                        || Signals.AnyCrosswalkBusy(nodeAhead); // yield to people still crossing
                        if (mustStop) target = Mathf.Min(target, toStop <= 0.02f ? 0f : Mathf.Sqrt(2f * 2.4f * Mathf.Max(toStop, 0f)));
                    }
                }

                // Leaving the road: don't drive into a jammed next segment
                // (handled when switching roads; vehicles wait at the end if the next lane start is full)

                // Accelerate / brake
                float accel = Mathf.Lerp(0.9f, 2.0f, a.Aggression) * (a.Kind == VehicleKind.BestBus || a.Kind == VehicleKind.Truck ? 0.55f : a.Kind == VehicleKind.Scooter ? 1.4f : 1f);
                float brake = 4.5f;
                a.Speed = target > a.Speed ? Mathf.Min(target, a.Speed + accel * dt) : Mathf.Max(target, a.Speed - brake * dt);

                // Lane change when stuck behind someone slower
                // Stuck behind a procession for a while: turn around (drivers in Mumbai would!)
                a.BlockedTime = blocked && a.Speed < 0.05f ? a.BlockedTime + dt : 0f;
                if (a.BlockedTime > 6f)
                {
                    a.BlockedTime = 0f;
                    a.Forward = !a.Forward;
                    a.Distance = path.Length - a.Distance;
                    a.Lane = 0;
                    a.Speed = 0f;
                }

                a.LaneChangeCooldown -= dt;
                if (a.LaneChangeCooldown <= 0f && path.LanesPerDirection > 1 && gap < safe * 1.6f && leaderSpeed < desired * 0.75f)
                {
                    a.LaneChangeCooldown = Mathf.Lerp(4f, 1.5f, a.Aggression);
                    int dir = a.Lane == 0 ? 1 : a.Lane == path.LanesPerDirection - 1 ? -1 : (_rng.Chance(0.5) ? 1 : -1);
                    int lane = a.Lane + dir;
                    if (lane >= 0 && lane < path.LanesPerDirection &&
                        GapAhead(path, a.Forward, lane, a.Distance, a, out _) > safe &&
                        GapBehind(path, a.Forward, lane, a.Distance) > 0.4f + a.Speed * 0.3f)
                    {
                        a.Lane = lane;
                    }
                }
                a.LateralLane = Mathf.MoveTowards(a.LateralLane, a.Lane, dt * 0.9f);

                a.Distance += a.Speed * dt;
                if (a.Distance >= path.Length) NextRoad(a);
                Place(a, dt);
            }
        }

        /// <summary>
        /// Puts a simulation-driven vehicle at a place on a road (distance measured in its travel direction).
        /// Other traffic still sees it and queues behind it.
        /// </summary>
        public void PlaceExternal(Agent a, RoadPath path, bool forward, float distance, int lane, float speed, float dt)
        {
            a.Path = path;
            a.Forward = forward;
            a.Distance = Mathf.Clamp(distance, 0f, path.Length);
            a.Lane = Mathf.Clamp(lane, 0, path.LanesPerDirection - 1);
            a.LateralLane = Mathf.MoveTowards(a.LateralLane, a.Lane, dt * 0.9f);
            a.Speed = speed;
            Place(a, dt);
        }

        /// <summary>The ambient agents of the rival RapidCab fleet, in creation order.</summary>
        public List<Agent> RivalAgents()
        {
            var list = new List<Agent>();
            foreach (Agent a in Agents) if (a.Kind == VehicleKind.RivalCab) list.Add(a);
            return list;
        }

        private void NextRoad(Agent a)
        {
            int atNode = a.Forward ? a.Path.Edge.B : a.Path.Edge.A;
            IReadOnlyList<RoadEdge> options = _graph.EdgesAt(atNode);
            RoadEdge next = null;
            for (int tries = 0; tries < 8 && next == null; tries++)
            {
                RoadEdge c = options[_rng.Range(0, options.Count)];
                if (c.Blocked) continue;
                if (c.Id == a.Path.Edge.Id && options.Count > 1) continue;
                next = c;
            }
            if (next == null) next = a.Path.Edge; // dead end or everything closed: U-turn

            RoadPath nextPath = _roads.ByEdge[next.Id];
            bool forward = next.A == atNode;
            int lane = Mathf.Min(a.Lane, nextPath.LanesPerDirection - 1);
            float carry = Mathf.Clamp(a.Distance - a.Path.Length, 0f, 1f);

            // If the entry of the next road is jammed, wait at the end of this one
            if (GapAhead(nextPath, forward, lane, carry, a, out _) < 0.2f)
            {
                a.Distance = a.Path.Length - 0.01f;
                a.Speed = 0f;
                return;
            }

            a.Distance = carry;
            a.Path = nextPath;
            a.Forward = forward;
            a.Lane = lane;
            a.LateralLane = Mathf.Min(a.LateralLane, nextPath.LanesPerDirection - 1);
        }

        private void Place(Agent a, float dt)
        {
            float d = a.Forward ? a.Distance : a.Path.Length - a.Distance;
            Vector3 pos = a.Path.Sample(d, out Vector3 fwd);
            if (!a.Forward) fwd = -fwd;
            Vector3 flat = new Vector3(fwd.x, 0, fwd.z).normalized;
            Vector3 right = new Vector3(flat.z, 0, -flat.x);
            float laneWidth = (a.Path.Width * 0.5f - 0.12f) / a.Path.LanesPerDirection;
            float weave = a.Weave > 0f ? (Mathf.PerlinNoise(a.NoiseSeed + 11f, Time.time * 0.35f) - 0.5f) * laneWidth * 0.7f * a.Weave : 0f;
            // Keep LEFT (India): offset to the left of the travel direction
            Vector3 offset = -right * (0.1f + laneWidth * (a.LateralLane + 0.5f) + weave);
            a.Transform.position = pos + offset + Vector3.up * 0.005f;
            if (fwd.sqrMagnitude > 1e-6f)
            {
                // Lean into lane changes a little
                float laneDelta = a.Lane - a.LateralLane;
                Quaternion look = Quaternion.LookRotation(fwd, Vector3.up) * Quaternion.Euler(0f, -laneDelta * 12f, 0f);
                a.Transform.rotation = Quaternion.Slerp(a.Transform.rotation, look, Mathf.Clamp01(dt * 8f));
            }
        }
    }
}
