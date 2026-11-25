using System.Collections.Generic;
using TaxiSim.Core;
using UnityEngine;

namespace TaxiSim.World
{
    /// <summary>Mumbai street and park species.</summary>
    public enum TreeSpecies
    {
        RainTree,     // huge umbrella canopy, BKC avenues
        Gulmohar,     // flame-red flowers
        Ashoka,       // tall columnar avenue tree (Polyalthia), very common in Mumbai
        Neem,         // round canopy
        CoconutPalm,  // coast, medians, Bandra
        Bush,         // shrubs along footpaths and lawns
        Hedge,        // 1-unit median / boundary hedge segment
        Mangrove      // low dense riverbank mangrove
    }

    /// <summary>
    /// Builds realistic low-cost trees the way real-time games do: a branching trunk plus a canopy made
    /// of many alpha-cut "leaf cards" (TaxiSim/Foliage shader). Each species has 3 variants; instances are
    /// merged into big meshes by NatureBuilder. Units: 1 unit = 10 m.
    /// </summary>
    public static class TreeFactory
    {
        public sealed class Template
        {
            public MeshBuilder Leaves = new MeshBuilder();
            public MeshBuilder Wood = new MeshBuilder();
        }

        private static readonly Dictionary<(TreeSpecies, int), Template> Cache = new Dictionary<(TreeSpecies, int), Template>();

        private static readonly Color Bark = new Color(0.36f, 0.3f, 0.24f);
        private static readonly Color PalmBark = new Color(0.5f, 0.44f, 0.36f);

        public static Template Get(TreeSpecies species, int variant)
        {
            variant = Mathf.Abs(variant) % 3;
            if (Cache.TryGetValue((species, variant), out Template t)) return t;
            t = new Template();
            var rng = new Pcg32((ulong)((int)species * 97 + variant * 13 + 7));
            switch (species)
            {
                case TreeSpecies.RainTree:
                    Broadleaf(t, rng, trunkH: 0.36f, trunkR: 0.075f, canopyCenterY: 0.78f, radii: new Vector3(1.0f, 0.32f, 1.0f),
                        cards: 36, cardSize: 0.5f, green: new Color(0.17f, 0.33f, 0.12f), flowers: 0f, branches: 5);
                    break;
                case TreeSpecies.Gulmohar:
                    Broadleaf(t, rng, 0.3f, 0.06f, 0.62f, new Vector3(0.78f, 0.28f, 0.78f), 30, 0.42f, new Color(0.24f, 0.4f, 0.14f), 0.4f, 4);
                    break;
                case TreeSpecies.Ashoka:
                    Broadleaf(t, rng, 0.14f, 0.045f, 0.78f, new Vector3(0.24f, 0.72f, 0.24f), 30, 0.3f, new Color(0.1f, 0.26f, 0.09f), 0f, 0);
                    break;
                case TreeSpecies.Neem:
                    Broadleaf(t, rng, 0.32f, 0.06f, 0.72f, new Vector3(0.56f, 0.46f, 0.56f), 30, 0.4f, new Color(0.22f, 0.38f, 0.13f), 0f, 3);
                    break;
                case TreeSpecies.CoconutPalm:
                    Palm(t, rng);
                    break;
                case TreeSpecies.Bush:
                    Canopy(t.Leaves, rng, new Vector3(0, 0.16f, 0), new Vector3(0.3f, 0.18f, 0.3f), 11, 0.26f, new Color(0.2f, 0.36f, 0.12f), 0f, 0.35f, 0f);
                    break;
                case TreeSpecies.Hedge:
                    for (int i = 0; i < 12; i++)
                    {
                        Vector3 c = new Vector3(rng.Range(-0.05f, 0.05f), rng.Range(0.08f, 0.2f), rng.Range(-0.5f, 0.5f));
                        AddCard(t.Leaves, rng, c, new Vector3(0, 0.05f, c.z), rng.Range(0.18f, 0.24f), new Color(0.18f, 0.34f, 0.1f), 0.2f, 0f);
                    }
                    break;
                case TreeSpecies.Mangrove:
                    Canopy(t.Leaves, rng, new Vector3(0, 0.22f, 0), new Vector3(0.55f, 0.22f, 0.55f), 14, 0.34f, new Color(0.1f, 0.22f, 0.09f), 0f, 0.3f, 0f);
                    for (int i = 0; i < 5; i++)
                    {
                        float a = rng.Range(0f, 6.283f);
                        t.Wood.AddTube(new Vector3(Mathf.Cos(a) * 0.3f, -0.05f, Mathf.Sin(a) * 0.3f), new Vector3(0, 0.2f, 0), 0.02f, 0.015f, 4, Bark);
                    }
                    break;
            }
            Cache[(species, variant)] = t;
            return t;
        }

        private static void Broadleaf(Template t, Pcg32 rng, float trunkH, float trunkR, float canopyCenterY, Vector3 radii,
                                      int cards, float cardSize, Color green, float flowers, int branches)
        {
            Vector3 lean = new Vector3(rng.Range(-0.04f, 0.04f), 0, rng.Range(-0.04f, 0.04f));
            Vector3 top = new Vector3(0, trunkH, 0) + lean;
            t.Wood.AddTube(Vector3.zero, top, trunkR, trunkR * 0.75f, 7, Bark, 0f, 0.1f);
            for (int b = 0; b < branches; b++)
            {
                float a = b * 6.283f / branches + rng.Range(-0.3f, 0.3f);
                Vector3 end = new Vector3(Mathf.Cos(a) * radii.x * 0.55f, canopyCenterY + rng.Range(-0.05f, 0.08f), Mathf.Sin(a) * radii.z * 0.55f);
                t.Wood.AddTube(top, end, trunkR * 0.6f, trunkR * 0.25f, 5, Bark, 0.1f, 0.5f);
            }
            if (branches == 0) t.Wood.AddTube(top, new Vector3(0, canopyCenterY + radii.y * 0.6f, 0), trunkR * 0.7f, trunkR * 0.2f, 5, Bark, 0.1f, 0.6f);
            Canopy(t.Leaves, rng, new Vector3(0, canopyCenterY, 0), radii, cards, cardSize, green, flowers, 1f, 0f);
        }

        /// <summary>Leaf cards distributed in an ellipsoid shell, with baked AO (darker inside / underneath).</summary>
        private static void Canopy(MeshBuilder mb, Pcg32 rng, Vector3 center, Vector3 radii, int cards, float cardSize,
                                   Color green, float flowers, float windScale, float pattern)
        {
            var flower = new Color(0.86f, 0.2f, 0.05f);
            for (int i = 0; i < cards; i++)
            {
                Vector3 dir = new Vector3(rng.Range(-1f, 1f), rng.Range(-0.7f, 1f), rng.Range(-1f, 1f));
                if (dir.sqrMagnitude < 1e-4f) dir = Vector3.up;
                dir.Normalize();
                float shell = Mathf.Lerp(0.55f, 1f, Mathf.Sqrt(rng.NextFloat()));
                Vector3 p = center + Vector3.Scale(dir, radii) * shell;
                float heightAO = Mathf.Lerp(0.55f, 1.05f, Mathf.InverseLerp(center.y - radii.y, center.y + radii.y, p.y));
                float outer = Mathf.Lerp(0.75f, 1f, shell);
                Color c = rng.Chance(flowers) ? flower : green * rng.Range(0.85f, 1.2f);
                c *= heightAO * outer;
                c.a = 1f;
                float wind = Mathf.Clamp01(p.y / (center.y + radii.y)) * windScale;
                AddCard(mb, rng, p, center, cardSize * rng.Range(0.8f, 1.2f), c, wind, pattern);
            }
        }

        private static void AddCard(MeshBuilder mb, Pcg32 rng, Vector3 p, Vector3 canopyCenter, float size, Color color, float wind, float pattern)
        {
            Quaternion q = Quaternion.Euler(rng.Range(0f, 360f), rng.Range(0f, 360f), rng.Range(0f, 360f));
            Vector3 r = q * Vector3.right * size * 0.5f, u = q * Vector3.up * size * 0.5f;
            Vector3 n = (p - canopyCenter + Vector3.up * 0.15f).normalized;
            var uv1 = new Vector4(wind, pattern, rng.NextFloat() * 10f, 0f);
            int i0 = mb.AddVertex(p - r - u, n, new Vector2(0, 0), color, default, uv1);
            int i1 = mb.AddVertex(p + r - u, n, new Vector2(1, 0), color, default, uv1);
            int i2 = mb.AddVertex(p + r + u, n, new Vector2(1, 1), color, default, uv1);
            int i3 = mb.AddVertex(p - r + u, n, new Vector2(0, 1), color, default, uv1);
            mb.AddTriangleIndices(i0, i1, i2);
            mb.AddTriangleIndices(i0, i2, i3);
        }

        private static void Palm(Template t, Pcg32 rng)
        {
            float height = rng.Range(1.3f, 1.8f);
            Vector3 lean = new Vector3(rng.Range(-0.25f, 0.25f), 0, rng.Range(-0.25f, 0.25f));
            Vector3 prev = Vector3.zero;
            const int segs = 6;
            for (int s = 1; s <= segs; s++)
            {
                float k = (float)s / segs;
                Vector3 p = new Vector3(0, height * k, 0) + lean * (k * k);
                t.Wood.AddTube(prev, p, Mathf.Lerp(0.055f, 0.035f, k - 1f / segs), Mathf.Lerp(0.055f, 0.035f, k), 6, PalmBark, k * 0.4f, k * 0.5f);
                prev = p;
            }
            Vector3 crown = prev;
            for (int c = 0; c < 4; c++)
            {
                float a = c * 1.57f;
                t.Wood.AddEllipsoid(crown + new Vector3(Mathf.Cos(a) * 0.05f, -0.06f, Mathf.Sin(a) * 0.05f), Vector3.one * 0.035f, 6, 4, new Color(0.35f, 0.3f, 0.12f), 0.5f);
            }
            int fronds = rng.Range(9, 12);
            var green = new Color(0.22f, 0.4f, 0.13f);
            for (int f = 0; f < fronds; f++)
            {
                float a = f * 6.283f / fronds + rng.Range(-0.2f, 0.2f);
                Vector3 dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                Vector3 side = new Vector3(-dir.z, 0, dir.x);
                float lift = rng.Range(0.15f, 0.55f);
                float len = rng.Range(0.8f, 1.05f);
                const int fs = 4;
                Vector3 p0 = crown;
                Color col = green * rng.Range(0.85f, 1.15f);
                col.a = 1f;
                float seed = rng.NextFloat() * 10f;
                for (int s = 0; s < fs; s++)
                {
                    float k0 = (float)s / fs, k1 = (float)(s + 1) / fs;
                    Vector3 p1 = crown + dir * len * k1 + Vector3.up * (lift * k1 - 0.75f * k1 * k1 * len);
                    float w0 = 0.32f * (1f - k0 * 0.6f), w1 = 0.32f * (1f - k1 * 0.6f);
                    Vector3 n = Vector3.Cross(p1 - p0, side).normalized;
                    if (n.y < 0) n = -n;
                    var uvA = new Vector4(0.5f + 0.5f * k0, 1f, seed, 0);
                    var uvB = new Vector4(0.5f + 0.5f * k1, 1f, seed, 0);
                    int i0 = t.Leaves.AddVertex(p0 - side * w0 * 0.5f, n, new Vector2(0, k0), col, default, uvA);
                    int i1 = t.Leaves.AddVertex(p0 + side * w0 * 0.5f, n, new Vector2(1, k0), col, default, uvA);
                    int i2 = t.Leaves.AddVertex(p1 + side * w1 * 0.5f, n, new Vector2(1, k1), col, default, uvB);
                    int i3 = t.Leaves.AddVertex(p1 - side * w1 * 0.5f, n, new Vector2(0, k1), col, default, uvB);
                    t.Leaves.AddTriangleIndices(i0, i1, i2);
                    t.Leaves.AddTriangleIndices(i0, i2, i3);
                    p0 = p1;
                }
            }
        }
    }
}
