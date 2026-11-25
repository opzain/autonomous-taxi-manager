using System.Collections.Generic;
using TaxiSim.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace TaxiSim.World
{
    /// <summary>One road (graph edge) in 3D: centre line with real heights (ground, bridge or flyover deck).</summary>
    public sealed class RoadPath
    {
        public RoadEdge Edge;
        public CityNode NodeA;
        public CityNode NodeB;
        public Vector3[] Points;
        public float[] Cumulative;
        public float Length;
        public float Width;
        public int LanesPerDirection;
        public bool LedLighting;
        public MeshRenderer Renderer;

        /// <summary>Position and direction at distance d from NodeA.</summary>
        public Vector3 Sample(float d, out Vector3 forward)
        {
            d = Mathf.Clamp(d, 0f, Length);
            int lo = 0, hi = Cumulative.Length - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (Cumulative[mid] <= d) lo = mid; else hi = mid;
            }
            float seg = Mathf.Max(Cumulative[hi] - Cumulative[lo], 1e-5f);
            float t = (d - Cumulative[lo]) / seg;
            forward = (Points[hi] - Points[lo]).normalized;
            return Vector3.Lerp(Points[lo], Points[hi], t);
        }

        public Vector3 Start => Points[0];
        public Vector3 End => Points[Points.Length - 1];
    }

    public sealed class RoadNetwork
    {
        public readonly List<RoadPath> Paths = new List<RoadPath>();
        public readonly Dictionary<int, RoadPath> ByEdge = new Dictionary<int, RoadPath>();
        public readonly Dictionary<int, float> JunctionRadius = new Dictionary<int, float>();
        public readonly List<Vector3> LampHeads = new List<Vector3>();
        public readonly List<Color> LampColors = new List<Color>();

        private MaterialPropertyBlock _mpb;
        private static readonly int HeatColorId = Shader.PropertyToID("_HeatColor");
        private static readonly int HeatAmountId = Shader.PropertyToID("_HeatAmount");
        private static readonly int BlockedId = Shader.PropertyToID("_Blocked");

        /// <summary>Updates the per-road gameplay overlay (traffic heat colour and closure stripes).</summary>
        public void ApplyOverlay(RoadPath path, Color heatColor, float heatAmount, bool blocked)
        {
            if (path.Renderer == null) return;
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            path.Renderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(HeatColorId, heatColor);
            _mpb.SetFloat(HeatAmountId, heatAmount);
            _mpb.SetFloat(BlockedId, blocked ? 1f : 0f);
            path.Renderer.SetPropertyBlock(_mpb);
        }
    }

    /// <summary>
    /// Turns the city graph into 3D roads: asphalt ribbons, junction plazas, automatic bridges where a
    /// road crosses the Mithi River, elevated flyovers on pillars (the y / z-axis of the city),
    /// crash barriers, sidewalks and street lamps (white LED in BKC, sodium orange outside).
    /// </summary>
    public static class RoadNetworkBuilder
    {
        public const float RoadY = 0.03f;
        public const float JunctionY = 0.045f;
        public const float BridgeDeck = 0.8f;
        public const float FlyoverDeck = 2.6f;

        private static readonly HashSet<string> BkcCore = new HashSet<string>
        {
            "west_gate", "asian_heart", "mmrda", "nse", "one_bkc", "jio_world", "trident",
            "us_consulate", "diamond_bourse", "ev_hub_jio", "ev_depot", "equinox"
        };

        private static readonly Color LedWhite = new Color(0.85f, 0.92f, 1.0f);
        private static readonly Color Sodium = new Color(1.0f, 0.62f, 0.28f);

        public static RoadNetwork Build(CityMap map, MaterialLibrary mats, Transform parent, SpaceRegistry space)
        {
            CityGraph graph = map.Graph;
            CityGeography geo = map.Geography;
            var net = new RoadNetwork();

            var root = new GameObject("Roads").transform;
            root.SetParent(parent, false);

            var structures = new MeshBuilder();
            var sidewalks = new MeshBuilder();
            var poles = new MeshBuilder();
            var heads = new MeshBuilder();

            foreach (RoadEdge e in graph.Edges)
            {
                CityNode na = graph.Node(e.A), nb = graph.Node(e.B);
                Vector2 a = na.Position.XZ.ToVector2(), b = nb.Position.XZ.ToVector2();
                float len = Vector2.Distance(a, b);
                int n = Mathf.Max(2, Mathf.CeilToInt(len / 1.0f));
                float width = (float)RoadClassInfo.WidthUnits(e.Class);
                bool led = BkcCore.Contains(na.Key) && BkcCore.Contains(nb.Key);

                float[] h = ComputeHeights(geo, a, b, n, len, e.Elevated);
                var pts = new Vector3[n + 1];
                var cum = new float[n + 1];
                for (int k = 0; k <= n; k++)
                {
                    Vector2 p = Vector2.Lerp(a, b, (float)k / n);
                    pts[k] = new Vector3(p.x, h[k], p.y);
                    if (k > 0) cum[k] = cum[k - 1] + Vector3.Distance(pts[k], pts[k - 1]);
                }

                var path = new RoadPath
                {
                    Edge = e,
                    NodeA = na,
                    NodeB = nb,
                    Points = pts,
                    Cumulative = cum,
                    Length = cum[n],
                    Width = width,
                    LanesPerDirection = RoadClassInfo.LanesPerDirection(e.Class),
                    LedLighting = led
                };

                // Asphalt ribbon (one object per road so gameplay overlays can be set per road)
                var asphalt = new MeshBuilder();
                asphalt.AddRibbon(pts, width, Color.white, 0f, new Vector4(path.LanesPerDirection, width, 1f, path.Length));
                GameObject roadGo = asphalt.Emit($"Road_{e.Id}_{Sanitize(e.Name)}", root, mats.Road, castShadows: e.Elevated);
                path.Renderer = roadGo.GetComponent<MeshRenderer>();

                BuildStructure(geo, path, structures, poles, heads, net, led);
                BuildSidewalksAndLamps(path, sidewalks, poles, heads, net, led);

                space.AddSegment(a, b, width * 0.5f + 0.95f);
                net.Paths.Add(path);
                net.ByEdge[e.Id] = path;
            }

            // Junction plazas
            var junctions = new MeshBuilder();
            foreach (CityNode node in graph.Nodes)
            {
                float maxW = 1f;
                foreach (RoadEdge e in graph.EdgesAt(node.Id)) maxW = Mathf.Max(maxW, (float)RoadClassInfo.WidthUnits(e.Class));
                float radius = maxW * 0.5f * 1.25f + 0.15f;
                net.JunctionRadius[node.Id] = radius;
                Vector3 c = node.Position.XZ.ToVector3(JunctionY);
                junctions.AddDisc(c, radius, 28, Vector3.up, Color.white);
                sidewalks.AddDisc(c + Vector3.down * 0.012f, radius + 0.35f, 28, Vector3.up, Color.white);
                space.AddCircle(node.Position.XZ.ToVector2(), radius + 0.8f);
            }

            junctions.Emit("Junctions", root, mats.Road, castShadows: false);
            sidewalks.Emit("Sidewalks", root, mats.Paving, castShadows: false);
            structures.Emit("Bridges_Flyovers", root, mats.Concrete);
            poles.Emit("StreetLamp_Poles", root, mats.Metal);
            heads.Emit("StreetLamp_Heads", root, mats.LampHeads, castShadows: false);
            return net;
        }

        private static float[] ComputeHeights(CityGeography geo, Vector2 a, Vector2 b, int n, float len, bool elevated)
        {
            var target = new float[n + 1];
            bool anyWater = false;
            for (int k = 0; k <= n; k++)
            {
                Vector2 p = Vector2.Lerp(a, b, (float)k / n);
                float g = geo.GroundY(p.x, p.y);
                bool overWater = g < -0.12f;
                anyWater |= overWater;
                target[k] = overWater ? BridgeDeck : RoadY;
            }

            var h = new float[n + 1];
            if (elevated)
            {
                const float rampLength = 12f;
                for (int k = 0; k <= n; k++)
                {
                    float dA = (float)k / n * len, dB = len - dA;
                    float t = Mathf.Clamp01(Mathf.Min(dA, dB) / rampLength);
                    float s = t * t * (3f - 2f * t);
                    h[k] = Mathf.Max(target[k], Mathf.Lerp(RoadY, FlyoverDeck, s));
                }
                return h;
            }

            if (!anyWater)
            {
                for (int k = 0; k <= n; k++) h[k] = RoadY;
                return h;
            }

            // Bridge approach ramps: dilate the deck, then smooth it.
            const int r = 6;
            var dil = new float[n + 1];
            for (int k = 0; k <= n; k++)
            {
                float m = RoadY;
                for (int j = Mathf.Max(0, k - r); j <= Mathf.Min(n, k + r); j++) m = Mathf.Max(m, target[j]);
                dil[k] = m;
            }
            for (int k = 0; k <= n; k++)
            {
                float sum = 0f;
                int cnt = 0;
                for (int j = Mathf.Max(0, k - r); j <= Mathf.Min(n, k + r); j++) { sum += dil[j]; cnt++; }
                h[k] = Mathf.Max(sum / cnt, target[k]);
            }
            h[0] = RoadY;
            h[n] = RoadY;
            return h;
        }

        private static void BuildStructure(CityGeography geo, RoadPath path, MeshBuilder structures, MeshBuilder poles,
                                           MeshBuilder heads, RoadNetwork net, bool led)
        {
            Vector3[] p = path.Points;
            float w = path.Width;
            for (int k = 0; k < p.Length - 1; k++)
            {
                float hk = Mathf.Min(p[k].y, p[k + 1].y);
                if (hk < 0.25f) continue;
                Vector3 seg = p[k + 1] - p[k];
                float segLen = seg.magnitude;
                Quaternion rot = Quaternion.LookRotation(seg / segLen, Vector3.up);
                Vector3 mid = (p[k] + p[k + 1]) * 0.5f;
                Vector3 right = rot * Vector3.right;

                // Deck slab and crash barriers
                structures.AddBox(mid + Vector3.down * 0.17f, rot, new Vector3(w + 0.2f, 0.3f, segLen + 0.05f), new Color(0.66f, 0.65f, 0.62f));
                structures.AddBox(mid + right * (w * 0.5f + 0.05f) + Vector3.up * 0.06f, rot, new Vector3(0.08f, 0.16f, segLen + 0.02f), new Color(0.75f, 0.74f, 0.7f));
                structures.AddBox(mid - right * (w * 0.5f + 0.05f) + Vector3.up * 0.06f, rot, new Vector3(0.08f, 0.16f, segLen + 0.02f), new Color(0.75f, 0.74f, 0.7f));

                // Pillars with pier caps
                if (k % 5 == 2 && p[k].y > 0.5f)
                {
                    float g = geo.GroundY(p[k].x, p[k].z);
                    float bottom = g < (float)CityGeography.WaterLevel ? g - 0.3f : g - 0.1f;
                    float top = p[k].y - 0.32f;
                    float pw = path.Edge.Elevated ? 0.6f : 0.45f;
                    structures.AddBox(new Vector3(p[k].x, (bottom + top) * 0.5f, p[k].z), rot, new Vector3(pw, top - bottom, pw), new Color(0.6f, 0.59f, 0.56f));
                    structures.AddBox(new Vector3(p[k].x, top - 0.09f, p[k].z), rot, new Vector3(w * 0.85f, 0.18f, 0.55f), new Color(0.62f, 0.61f, 0.58f));
                }

                // Lamps on the deck barrier
                if (k % 8 == 4)
                {
                    float side = (k / 8) % 2 == 0 ? 1f : -1f;
                    Vector3 basePos = p[k] + right * side * (w * 0.5f + 0.05f);
                    AddLamp(basePos, -right * side, poles, heads, net, led);
                }
            }
        }

        private static void BuildSidewalksAndLamps(RoadPath path, MeshBuilder sidewalks, MeshBuilder poles, MeshBuilder heads,
                                                   RoadNetwork net, bool led)
        {
            Vector3[] p = path.Points;
            float w = path.Width;
            const float walkWidth = 0.8f;
            int lampEvery = path.Edge.Class == RoadClass.Local ? 6 : 4;
            for (int k = 0; k < p.Length - 1; k++)
            {
                if (Mathf.Max(p[k].y, p[k + 1].y) > 0.1f) continue;
                Vector3 seg = p[k + 1] - p[k];
                Vector3 dir = new Vector3(seg.x, 0, seg.z).normalized;
                Vector3 right = new Vector3(dir.z, 0, -dir.x);
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 o0 = right * side * (w * 0.5f);
                    Vector3 o1 = right * side * (w * 0.5f + walkWidth);
                    Vector3 lift = Vector3.up * 0.05f;
                    sidewalks.AddQuad(p[k] + o0 + lift, p[k + 1] + o0 + lift, p[k + 1] + o1 + lift, p[k] + o1 + lift, Vector3.up, Color.white);
                    // Kerb face (raised footpath edge facing the road)
                    sidewalks.AddQuad(p[k] + o0, p[k + 1] + o0, p[k + 1] + o0 + lift, p[k] + o0 + lift, -right * side, new Color(0.8f, 0.8f, 0.78f));
                }

                if (k % lampEvery == lampEvery / 2 && k > 2 && k < p.Length - 3)
                {
                    float side = (k / lampEvery) % 2 == 0 ? 1f : -1f;
                    Vector3 basePos = p[k] + right * side * (w * 0.5f + 0.15f) + Vector3.up * 0.05f;
                    AddLamp(basePos, -right * side, poles, heads, net, led);
                }
            }
        }

        private static void AddLamp(Vector3 basePos, Vector3 towardRoad, MeshBuilder poles, MeshBuilder heads, RoadNetwork net, bool led)
        {
            const float height = 0.9f;
            Color pole = new Color(0.32f, 0.33f, 0.35f);
            poles.AddBox(basePos + Vector3.up * height * 0.5f, Quaternion.LookRotation(towardRoad), new Vector3(0.05f, height, 0.05f), pole);
            Vector3 armEnd = basePos + Vector3.up * height + towardRoad * 0.35f;
            poles.AddBeam(basePos + Vector3.up * height, armEnd, new Vector2(0.04f, 0.04f), pole);
            Color col = led ? LedWhite : Sodium;
            heads.AddBox(armEnd + Vector3.down * 0.03f, Quaternion.LookRotation(towardRoad), new Vector3(0.12f, 0.04f, 0.2f), col);
            net.LampHeads.Add(armEnd + Vector3.down * 0.08f);
            net.LampColors.Add(col);
        }

        private static string Sanitize(string s)
        {
            var chars = s.ToCharArray();
            for (int i = 0; i < chars.Length; i++) if (!char.IsLetterOrDigit(chars[i])) chars[i] = '_';
            return new string(chars);
        }
    }
}
