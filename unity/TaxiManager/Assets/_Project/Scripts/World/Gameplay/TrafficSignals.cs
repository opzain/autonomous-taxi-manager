using System.Collections.Generic;
using TaxiSim.Core;
using UnityEngine;

namespace TaxiSim.World
{
    public enum SignalState
    {
        Green,
        Amber,
        Red
    }

    /// <summary>
    /// Traffic signals at every junction with 3 or more ground-level roads.
    ///  * Each approach has a pole on the left kerb (India drives on the left) at its stop line, with
    ///    red / amber / green vehicle lamps on an arm and a red / green "walk" lamp for pedestrians.
    ///  * Approaches get green one at a time (green 11 s, amber 2.5 s, all-red 1 s); after the last
    ///    approach comes an all-red pedestrian phase (9 s, walk lamp blinks for the last 2.5 s).
    ///  * Real zebra crossings and stop lines are painted on every approach. Pedestrians report when they
    ///    are on a crossing, and vehicles yield to an occupied crossing even on green.
    /// </summary>
    public sealed class TrafficSignals : MonoBehaviour
    {
        /// <summary>Crossing centre and stop line, measured from the junction edge (junction radius).</summary>
        public const float CrosswalkCentre = 0.55f;
        public const float CrosswalkHalfLength = 0.35f;
        public const float StopLineGap = 0.5f;

        private sealed class Head
        {
            public int EdgeId;
            public MeshRenderer Red, Amber, Green, WalkRed, WalkGreen;
        }

        private sealed class Junction
        {
            public int Node;
            public float Radius;
            public readonly List<int> Approaches = new List<int>();
            public readonly List<Head> Heads = new List<Head>();
            public int Phase;
            public float Timer;
            public int Stage; // 0 green, 1 amber, 2 all-red, 3 pedestrian walk
        }

        public float GreenSeconds = 11f;
        public float AmberSeconds = 2.5f;
        public float AllRedSeconds = 1f;
        public float WalkSeconds = 9f;

        private readonly Dictionary<int, Junction> _junctions = new Dictionary<int, Junction>();
        private readonly Dictionary<long, int> _onCrosswalk = new Dictionary<long, int>();
        private Material _redOn, _redOff, _amberOn, _amberOff, _greenOn, _greenOff;

        public int Count => _junctions.Count;
        public IEnumerable<int> Nodes => _junctions.Keys;

        public void Build(WorldContext ctx)
        {
            MaterialLibrary m = ctx.Mats;
            _redOn = m.SignalRedOn; _redOff = m.SignalRedOff;
            _amberOn = m.SignalAmberOn; _amberOff = m.SignalAmberOff;
            _greenOn = m.SignalGreenOn; _greenOff = m.SignalGreenOff;

            var lampMesh = new MeshBuilder();
            lampMesh.AddEllipsoid(Vector3.zero, new Vector3(0.035f, 0.035f, 0.02f), 10, 6, Color.white, 0f);
            Mesh lamp = lampMesh.ToMesh("SignalLamp");
            var walkMesh = new MeshBuilder();
            walkMesh.AddBox(Vector3.zero, new Vector3(0.045f, 0.045f, 0.01f), Color.white);
            Mesh walkLamp = walkMesh.ToMesh("WalkLamp");
            var poles = new MeshBuilder();
            var paint = new MeshBuilder();
            var rng = new Pcg32(808UL);
            var steel = new Color(0.2f, 0.21f, 0.22f);
            var housing = new Color(0.08f, 0.08f, 0.09f);
            var white = new Color(0.9f, 0.9f, 0.88f);

            CityGraph g = ctx.Map.Graph;
            foreach (CityNode node in g.Nodes)
            {
                IReadOnlyList<RoadEdge> edges = g.EdgesAt(node.Id);
                if (edges.Count < 3) continue;
                float jr = ctx.Roads.JunctionRadius.TryGetValue(node.Id, out float r) ? r : 1.5f;
                var j = new Junction { Node = node.Id, Radius = jr, Timer = rng.Range(0f, GreenSeconds) };
                foreach (RoadEdge e in edges)
                {
                    RoadPath path = ctx.Roads.ByEdge[e.Id];
                    if (path.Edge.Elevated) continue; // flyovers bypass the junction
                    bool nodeIsA = e.A == node.Id;
                    float stopD = jr + CrosswalkCentre + CrosswalkHalfLength + StopLineGap * 0.3f;
                    if (path.Length < stopD * 2f + 1f) continue;
                    j.Approaches.Add(e.Id);

                    // Local frame at the crossing: 'travel' points towards the junction (inbound traffic)
                    Vector3 cw = path.Sample(nodeIsA ? jr + CrosswalkCentre : path.Length - jr - CrosswalkCentre, out Vector3 fwd);
                    Vector3 travel = nodeIsA ? -fwd : fwd;
                    travel.y = 0;
                    travel.Normalize();
                    Vector3 right = new Vector3(travel.z, 0, -travel.x);
                    float halfW = path.Width * 0.5f;
                    Quaternion along = Quaternion.LookRotation(travel, Vector3.up);

                    // Zebra bars (parallel to traffic) and the stop line across the inbound (left) half
                    for (float x = -halfW + 0.12f; x <= halfW - 0.1f; x += 0.16f)
                        paint.AddBox(cw + right * x + Vector3.up * 0.008f, along, new Vector3(0.08f, 0.004f, CrosswalkHalfLength * 2f), white);
                    Vector3 stop = cw - travel * (CrosswalkHalfLength + StopLineGap * 0.3f);
                    paint.AddBox(stop - right * (halfW * 0.5f) + Vector3.up * 0.008f, along, new Vector3(halfW - 0.05f, 0.004f, 0.05f), white);

                    // Signal pole at the stop line on the left kerb
                    Vector3 kerb = stop - right * (halfW + 0.22f) + Vector3.up * 0.05f;
                    Quaternion face = Quaternion.LookRotation(-travel, Vector3.up);
                    poles.AddBox(kerb + Vector3.up * 0.4f, face, new Vector3(0.05f, 0.8f, 0.05f), steel);
                    Vector3 armEnd = kerb + Vector3.up * 0.78f + right * 0.55f;
                    poles.AddBeam(kerb + Vector3.up * 0.78f, armEnd, new Vector2(0.035f, 0.035f), steel);
                    Vector3 headPos = armEnd + Vector3.down * 0.12f;
                    poles.AddBox(headPos, face, new Vector3(0.1f, 0.28f, 0.07f), housing);

                    var head = new Head { EdgeId = e.Id };
                    Vector3 front = -travel * 0.04f;
                    head.Red = MakeLamp(lamp, headPos + Vector3.up * 0.085f + front, face, _redOff);
                    head.Amber = MakeLamp(lamp, headPos + front, face, _amberOff);
                    head.Green = MakeLamp(lamp, headPos + Vector3.down * 0.085f + front, face, _greenOff);

                    // Pedestrian head on the pole, facing across the road
                    Quaternion across = Quaternion.LookRotation(right, Vector3.up);
                    Vector3 ped = kerb + Vector3.up * 0.5f + right * 0.03f;
                    poles.AddBox(ped, across, new Vector3(0.07f, 0.15f, 0.05f), housing);
                    head.WalkRed = MakeLamp(walkLamp, ped + Vector3.up * 0.035f + right * 0.027f, across, _redOff);
                    head.WalkGreen = MakeLamp(walkLamp, ped + Vector3.down * 0.035f + right * 0.027f, across, _greenOff);
                    j.Heads.Add(head);
                }
                if (j.Approaches.Count >= 2)
                {
                    j.Phase = rng.Range(0, j.Approaches.Count);
                    _junctions[node.Id] = j;
                    Apply(j);
                }
            }
            poles.Emit("SignalPoles", transform, ctx.Mats.Metal);
            paint.Emit("ZebraCrossings", transform, ctx.Mats.Concrete, false);
        }

        private MeshRenderer MakeLamp(Mesh mesh, Vector3 pos, Quaternion rot, Material mat)
        {
            var go = new GameObject("Lamp");
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(pos, rot);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return mr;
        }

        // ------------------------------------------------------------------ queries

        public bool IsSignalised(int nodeId, int edgeId) =>
            _junctions.TryGetValue(nodeId, out Junction j) && j.Approaches.Contains(edgeId);

        /// <summary>Signal shown to traffic on <paramref name="edgeId"/> that is about to enter <paramref name="nodeId"/>.</summary>
        public SignalState State(int nodeId, int edgeId)
        {
            if (!_junctions.TryGetValue(nodeId, out Junction j)) return SignalState.Green;
            int idx = j.Approaches.IndexOf(edgeId);
            if (idx < 0) return SignalState.Green;
            if (j.Stage >= 2 || idx != j.Phase) return SignalState.Red;
            return j.Stage == 0 ? SignalState.Green : SignalState.Amber;
        }

        /// <summary>Distance from the node at which inbound vehicles stop (front bumper).</summary>
        public float StopDistance(int nodeId)
        {
            float jr = _junctions.TryGetValue(nodeId, out Junction j) ? j.Radius : 1.5f;
            return jr + CrosswalkCentre + CrosswalkHalfLength + StopLineGap * 0.3f + 0.05f;
        }

        /// <summary>Distance from the node to the middle of the zebra crossing.</summary>
        public float CrosswalkDistance(int nodeId) =>
            (_junctions.TryGetValue(nodeId, out Junction j) ? j.Radius : 1.5f) + CrosswalkCentre;

        /// <summary>Seconds of "walk" left at this junction (0 when pedestrians must wait).</summary>
        public float WalkTimeLeft(int nodeId) =>
            _junctions.TryGetValue(nodeId, out Junction j) && j.Stage == 3 ? Mathf.Max(0f, WalkSeconds - j.Timer) : 0f;

        private static long Key(int node, int edge) => (long)node * 100000 + edge;

        public void EnterCrosswalk(int node, int edge)
        {
            long k = Key(node, edge);
            _onCrosswalk.TryGetValue(k, out int n);
            _onCrosswalk[k] = n + 1;
        }

        public void LeaveCrosswalk(int node, int edge)
        {
            long k = Key(node, edge);
            if (_onCrosswalk.TryGetValue(k, out int n)) _onCrosswalk[k] = Mathf.Max(0, n - 1);
        }

        public bool AnyCrosswalkBusy(int node)
        {
            if (!_junctions.TryGetValue(node, out Junction j)) return false;
            foreach (int e in j.Approaches) if (CrosswalkBusy(node, e)) return true;
            return false;
        }

        public bool CrosswalkBusy(int node, int edge) => _onCrosswalk.TryGetValue(Key(node, edge), out int n) && n > 0;

        // ------------------------------------------------------------------ cycle

        private void Update()
        {
            float dt = Time.deltaTime;
            foreach (Junction j in _junctions.Values)
            {
                j.Timer += dt;
                float limit = j.Stage == 0 ? GreenSeconds : j.Stage == 1 ? AmberSeconds : j.Stage == 2 ? AllRedSeconds : WalkSeconds;
                if (j.Timer < limit)
                {
                    if (j.Stage == 3 && limit - j.Timer < 2.5f) Apply(j); // blinking walk lamp
                    continue;
                }
                j.Timer = 0f;
                if (j.Stage == 2 && j.Phase == j.Approaches.Count - 1) j.Stage = 3;   // pedestrian phase after the last approach
                else if (j.Stage >= 2) { j.Stage = 0; j.Phase = (j.Phase + 1) % j.Approaches.Count; }
                else j.Stage++;
                Apply(j);
            }
        }

        private void Apply(Junction j)
        {
            bool walk = j.Stage == 3;
            bool walkLit = walk && (WalkSeconds - j.Timer > 2.5f || Mathf.Repeat(j.Timer * 2f, 1f) < 0.5f);
            foreach (Head h in j.Heads)
            {
                SignalState s = State(j.Node, h.EdgeId);
                h.Red.sharedMaterial = s == SignalState.Red ? _redOn : _redOff;
                h.Amber.sharedMaterial = s == SignalState.Amber ? _amberOn : _amberOff;
                h.Green.sharedMaterial = s == SignalState.Green ? _greenOn : _greenOff;
                h.WalkRed.sharedMaterial = walk ? _redOff : _redOn;
                h.WalkGreen.sharedMaterial = walkLit ? _greenOn : _greenOff;
            }
        }
    }
}
