using System.Collections.Generic;
using TaxiSim.Core;
using UnityEngine;

namespace TaxiSim.World
{
    /// <summary>
    /// Route-engine demo in 3D (spec tests T07/T08): press P for a random fastest route (Dijkstra on
    /// minutes × traffic), Shift+P to close a road on that route and watch it reroute, Shift+O to reopen
    /// all roads. The route is drawn as a glowing animated line with start/end beacons.
    /// </summary>
    public sealed class RouteShowcase : MonoBehaviour
    {
        [System.NonSerialized] public CityGraph Graph;
        [System.NonSerialized] public RoadNetwork Roads;
        [System.NonSerialized] public MaterialLibrary Mats;
        public CityPulse Pulse;

        public RouteResult Current { get; private set; }
        public string Info { get; private set; }

        private readonly Pcg32 _rng = new Pcg32(31337UL);
        private readonly List<GameObject> _visuals = new List<GameObject>();

        public void ShowRandom()
        {
            int n = Graph.Nodes.Count;
            int a = _rng.Range(0, n);
            int b = _rng.Range(0, n - 1);
            if (b >= a) b++;
            Show(a, b);
        }

        public void Show(int from, int to)
        {
            Clear();
            Current = RouteFinder.FindFastest(Graph, from, to);
            string fromName = Graph.Node(from).ShortName, toName = Graph.Node(to).ShortName;
            if (!Current.Success)
            {
                Info = $"{fromName} → {toName}: {Current.FailureReason}";
                return;
            }
            Info = $"{fromName} → {toName} · {Current.Km:0.0} km · {Current.Minutes:0.0} min · {Current.Edges.Count} roads (Dijkstra)";

            var pts = new List<Vector3>();
            int node = from;
            foreach (int edgeId in Current.Edges)
            {
                RoadPath path = Roads.ByEdge[edgeId];
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

            var mb = new MeshBuilder();
            mb.AddRibbon(pts, 0.75f, Color.white, 0.12f);
            _visuals.Add(Spawn("Route", mb.ToMesh("Route"), Mats.RouteCyan, Vector3.zero));
            _visuals.Add(Beacon(Graph.Node(from).Position.ToVector3(), Mats.HoloGreen));
            _visuals.Add(Beacon(Graph.Node(to).Position.ToVector3(), Mats.HoloGold));
        }

        /// <summary>Closes the middle road of the current route and recomputes (reroute demo).</summary>
        public void CloseRoadOnRoute()
        {
            if (Current == null || !Current.Success || Current.Edges.Count == 0) return;
            RoadEdge e = Graph.Edges[Current.Edges[Current.Edges.Count / 2]];
            e.Blocked = true;
            if (Pulse != null) Pulse.Recompute();
            int from = Current.From, to = Current.To;
            Show(from, to);
            Info = $"Closed '{e.Name}' → rerouted: " + Info;
        }

        public void ReopenAll()
        {
            foreach (RoadEdge e in Graph.Edges) e.Blocked = false;
            if (Pulse != null) Pulse.Recompute();
            if (Current != null && Current.Edges.Count > 0) Show(Current.From, Current.To);
        }

        public void Clear()
        {
            foreach (GameObject g in _visuals) if (g != null) Destroy(g);
            _visuals.Clear();
            Info = null;
        }

        private GameObject Beacon(Vector3 at, Material m)
        {
            var mb = new MeshBuilder();
            mb.AddCylinder(Vector3.zero, 0.7f, 0.7f, 9f, 20, Color.white, capTop: false);
            return Spawn("RouteBeacon", mb.ToMesh("RouteBeacon"), m, at + Vector3.up * 0.05f);
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
    }
}
