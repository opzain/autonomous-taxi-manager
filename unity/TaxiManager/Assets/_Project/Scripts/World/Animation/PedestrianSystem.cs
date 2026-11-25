using System.Collections.Generic;
using TaxiSim.Core;
using UnityEngine;

namespace TaxiSim.World
{
    /// <summary>
    /// Pedestrians walking on the footpaths of every ground-level street. Each walker keeps to a
    /// lane of the footpath (clear of lamp posts and tree pits) and walks at their own pace.
    /// At a signalised junction many of them wait at the kerb by the zebra crossing, cross on the
    /// green "walk" signal (vehicles yield to them) and carry on along the other footpath; elsewhere
    /// they turn back or continue along another street. The walk cycle is animated on the GPU by the
    /// Character shader; people waiting switch to the standing material.
    /// </summary>
    public sealed class PedestrianSystem : MonoBehaviour
    {
        private enum Mode { Walking, Waiting, Crossing }

        private sealed class Walker
        {
            public Transform T;
            public MeshRenderer R;
            public RoadPath Path;
            public int Side;       // -1 / +1: which footpath of the road
            public float S;        // distance along the path
            public int Dir;        // +1 towards B, -1 towards A
            public float Offset;   // distance from the kerb line (inside the footpath)
            public float Speed;
            public Mode Mode;
            public int CrossNode;
            public float Timer;    // waiting time / crossing progress (0..1)
        }

        public float TimeScale = 1f;
        public TrafficSignals Signals;
        [Range(0f, 1f)] public float CrossChance = 0.6f;
        public float CrossSpeed = 0.42f;

        private readonly List<Walker> _walkers = new List<Walker>();
        private RoadNetwork _roads;
        private CityGraph _graph;
        private Pcg32 _rng;
        private Mesh[] _variants;
        private Material _walkMat, _idleMat;

        public int Count => _walkers.Count;
        public int Crossing { get; private set; }

        public static Mesh[] BuildVariantMeshes()
        {
            var meshes = new Mesh[PedestrianFactory.VariantCount];
            for (int i = 0; i < meshes.Length; i++) meshes[i] = PedestrianFactory.Build(i, 31UL + (ulong)i * 7UL).ToMesh($"Pedestrian_{i}");
            return meshes;
        }

        /// <summary>Position of a random walker (for the cinematic street shots).</summary>
        public bool RandomWalker(Pcg32 rng, out Transform t)
        {
            t = null;
            if (_walkers.Count == 0) return false;
            t = _walkers[rng.Range(0, _walkers.Count)].T;
            return true;
        }

        public void Init(WorldContext ctx, int count)
        {
            _roads = ctx.Roads;
            _graph = ctx.Map.Graph;
            _rng = new Pcg32(4711UL);
            _walkMat = ctx.Mats.Character;
            _idleMat = ctx.Mats.CharacterIdle;
            _variants = BuildVariantMeshes();

            // Walkable footpaths, weighted by length (side streets and the BKC core are busier)
            var paths = new List<RoadPath>();
            var weights = new List<float>();
            float total = 0f;
            foreach (RoadPath p in _roads.Paths)
            {
                if (p.Edge.Elevated) continue;
                float w = p.Length * (p.Edge.Class == RoadClass.Local ? 1.6f : 1f) * (p.LedLighting ? 1.4f : 1f);
                paths.Add(p);
                weights.Add(w);
                total += w;
            }
            if (paths.Count == 0) return;

            for (int attempts = 0; _walkers.Count < count && attempts < count * 4; attempts++)
            {
                float pick = _rng.Range(0f, total);
                int k = 0;
                while (k < paths.Count - 1 && pick > weights[k]) { pick -= weights[k]; k++; }
                RoadPath path = paths[k];
                var w = new Walker
                {
                    Path = path,
                    Side = _rng.Chance(0.5) ? 1 : -1,
                    Dir = _rng.Chance(0.5) ? 1 : -1,
                    Offset = _rng.Range(0.24f, 0.48f),
                    Speed = _rng.Range(0.17f, 0.3f)
                };
                float jA = Junction(path.NodeA.Id), jB = Junction(path.NodeB.Id);
                w.S = _rng.Range(jA, Mathf.Max(jA + 0.01f, path.Length - jB));
                if (!OnFootpath(path, w.S)) continue;

                var go = new GameObject("Pedestrian");
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = _variants[_rng.Range(0, _variants.Length)];
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = _walkMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                w.T = go.transform;
                w.R = mr;
                _walkers.Add(w);
                Place(w, 1f);
            }
        }

        private float Junction(int node) => (_roads.JunctionRadius.TryGetValue(node, out float r) ? r : 1.5f) + 0.6f;

        private static bool OnFootpath(RoadPath path, float s)
        {
            Vector3 p = path.Sample(Mathf.Clamp(s, 0f, path.Length), out _);
            return p.y <= 0.1f;
        }

        private void Update()
        {
            if (_roads == null) return;
            float dt = Mathf.Min(Time.deltaTime, 0.1f) * TimeScale;
            if (dt <= 0f) return;
            foreach (Walker w in _walkers)
            {
                switch (w.Mode)
                {
                    case Mode.Walking:
                    {
                        w.S += w.Dir * w.Speed * dt;
                        float jA = Junction(w.Path.NodeA.Id), jB = Junction(w.Path.NodeB.Id);
                        if (w.S < jA || w.S > w.Path.Length - jB || !OnFootpath(w.Path, w.S)) EndOfBlock(w, jA, jB);
                        break;
                    }
                    case Mode.Waiting:
                        w.Timer += dt;
                        if (Signals.WalkTimeLeft(w.CrossNode) > 3.2f)
                        {
                            w.Mode = Mode.Crossing;
                            w.Timer = 0f;
                            w.R.sharedMaterial = _walkMat;
                            Signals.EnterCrosswalk(w.CrossNode, w.Path.Edge.Id);
                            Crossing++;
                        }
                        else if (w.Timer > 50f) // gave up: walk back the way they came
                        {
                            w.Mode = Mode.Walking;
                            w.Dir = -w.Dir;
                            w.R.sharedMaterial = _walkMat;
                        }
                        break;
                    case Mode.Crossing:
                    {
                        float crossLen = w.Path.Width + 0.3f;
                        w.Timer += CrossSpeed * dt / crossLen;
                        if (w.Timer >= 1f) FinishCrossing(w);
                        break;
                    }
                }
                Place(w, dt);
            }
        }

        private void EndOfBlock(Walker w, float jA, float jB)
        {
            bool atB = w.S > w.Path.Length * 0.5f;
            int node = atB ? w.Path.Edge.B : w.Path.Edge.A;

            // Signalised junction: wait at the kerb by the zebra crossing, then cross this road
            if (Signals != null && Signals.IsSignalised(node, w.Path.Edge.Id) && _rng.Chance(CrossChance))
            {
                float d = Signals.CrosswalkDistance(node);
                w.S = atB ? w.Path.Length - d : d;
                w.Offset = _rng.Range(0.08f, 0.22f);
                w.Mode = Mode.Waiting;
                w.CrossNode = node;
                w.Timer = 0f;
                w.R.sharedMaterial = _idleMat;
                return;
            }

            if (_rng.Chance(0.55))
            {
                // Carry on along another street from this junction (same side of the road)
                IReadOnlyList<RoadEdge> options = _graph.EdgesAt(node);
                RoadEdge e = options[_rng.Range(0, options.Count)];
                RoadPath next = _roads.ByEdge[e.Id];
                if (!next.Edge.Elevated && e.Id != w.Path.Edge.Id)
                {
                    bool fromA = e.A == node;
                    float s = fromA ? Junction(e.A) + 0.05f : next.Length - Junction(e.B) - 0.05f;
                    if (s > 0f && s < next.Length && OnFootpath(next, s))
                    {
                        w.Path = next;
                        w.S = s;
                        w.Dir = fromA ? 1 : -1;
                        return;
                    }
                }
            }
            // Turn back, maybe moving to another footpath lane
            w.Dir = -w.Dir;
            w.S = Mathf.Clamp(w.S, jA, Mathf.Max(jA, w.Path.Length - jB));
            w.Offset = _rng.Range(0.24f, 0.48f);
        }

        private void FinishCrossing(Walker w)
        {
            Signals.LeaveCrosswalk(w.CrossNode, w.Path.Edge.Id);
            Crossing--;
            bool atB = w.CrossNode == w.Path.Edge.B;
            w.Side = -w.Side;
            w.Dir = atB ? -1 : 1; // walk away from the junction on the far footpath
            w.S = atB ? w.Path.Length - Junction(w.CrossNode) - 0.02f : Junction(w.CrossNode) + 0.02f;
            w.Offset = _rng.Range(0.24f, 0.48f);
            w.Mode = Mode.Walking;
        }

        private static void Place(Walker w, float dt)
        {
            Vector3 p = w.Path.Sample(Mathf.Clamp(w.S, 0f, w.Path.Length), out Vector3 fwd);
            Vector3 flat = new Vector3(fwd.x, 0, fwd.z).normalized;
            Vector3 right = new Vector3(flat.z, 0, -flat.x);
            float half = w.Path.Width * 0.5f;
            Vector3 pos, dir;
            if (w.Mode == Mode.Crossing)
            {
                float from = w.Side * (half + 0.15f), to = -from;
                float lat = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, w.Timer));
                pos = p + right * lat + Vector3.up * Mathf.Lerp(0.05f, 0.012f, Mathf.Clamp01(Mathf.Min(w.Timer, 1f - w.Timer) * 12f));
                dir = right * -w.Side;
            }
            else
            {
                // Walkers keep to their footpath lane; waiting people face the road
                pos = p + right * w.Side * (half + w.Offset) + Vector3.up * 0.05f;
                dir = w.Mode == Mode.Waiting ? right * -w.Side : flat * w.Dir;
            }
            w.T.position = pos;
            if (dir.sqrMagnitude > 1e-6f)
                w.T.rotation = Quaternion.Slerp(w.T.rotation, Quaternion.LookRotation(dir, Vector3.up), Mathf.Clamp01(dt * 6f));
        }
    }
}
