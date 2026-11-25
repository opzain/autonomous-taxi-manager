using System.Collections.Generic;
using TaxiSim.Core;
using UnityEngine;

namespace TaxiSim.World
{
    /// <summary>
    /// Street trees, medians, parks and mangroves (v0.6, realistic):
    ///  * avenue trees in tree pits along every footpath (rain trees in BKC, ashoka / gulmohar / neem
    ///    elsewhere, coconut palms towards the coast)
    ///  * planted medians with hedges and palms on wide roads
    ///  * park and campus groves with bushes
    ///  * mangroves along both Mithi River banks
    /// All instances are merged into a few big meshes (leaf cards + bark).
    /// </summary>
    public static class NatureBuilder
    {
        public static void Build(WorldContext ctx)
        {
            var rng = new Pcg32(424242UL);
            var leaves = new ChunkedMeshBuilder(60000);
            var wood = new ChunkedMeshBuilder(60000);
            CityGeography geo = ctx.Map.Geography;
            float density = Mathf.Clamp(ctx.Density, 0.3f, 1f);

            // 1) Avenue trees in tree pits on the footpath, plus planted medians on wide roads
            foreach (RoadPath path in ctx.Roads.Paths)
            {
                if (path.Edge.Elevated) continue;
                bool bkc = path.LedLighting;
                float spacing = (path.Edge.Class == RoadClass.Local ? 3.4f : 2.8f) / density;
                float jA = ctx.Roads.JunctionRadius[path.NodeA.Id] + 1.5f;
                float jB = ctx.Roads.JunctionRadius[path.NodeB.Id] + 1.5f;
                for (int side = -1; side <= 1; side += 2)
                {
                    for (float s = jA + rng.Range(0f, spacing); s < path.Length - jB; s += spacing * rng.Range(0.85f, 1.15f))
                    {
                        Vector3 p = path.Sample(s, out Vector3 fwd);
                        if (p.y > 0.1f) continue; // bridge / ramp
                        Vector3 flat = new Vector3(fwd.x, 0, fwd.z).normalized;
                        Vector3 right = new Vector3(flat.z, 0, -flat.x);
                        Vector3 pos = p + right * side * (path.Width * 0.5f + 0.58f);
                        if (!ctx.FootpathFree(new Vector2(pos.x, pos.z), 0.5f)) continue; // stall / bus shelter here
                        pos.y = Mathf.Max(geo.GroundY(pos.x, pos.z), 0f) + 0.05f;
                        if (geo.GroundY(pos.x, pos.z) < -0.05f) continue;
                        TreeSpecies sp = PickAvenue(rng, bkc, pos.x < (float)geo.CoastBaseX + 70f);
                        Place(leaves, wood, rng, sp, pos, rng.Range(0.85f, 1.2f));
                    }
                }

                // Weeds in the gutter and at the foot of walls (not in the manicured BKC core)
                if (!bkc)
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        for (float s = jA; s < path.Length - jB; s += rng.Range(0.6f, 1.4f))
                        {
                            if (!rng.Chance(0.4 * density)) continue;
                            Vector3 p = path.Sample(s, out Vector3 fwd);
                            if (p.y > 0.1f) continue;
                            Vector3 right = new Vector3(fwd.z, 0, -fwd.x).normalized;
                            bool wall = rng.Chance(0.5);
                            Vector3 pos = p + right * side * (path.Width * 0.5f + (wall ? 0.78f : -0.02f)) + Vector3.up * (wall ? 0.05f : 0.002f);
                            GrassTuft(leaves.Next(), rng, pos, rng.Range(0.05f, 0.09f), DryGreen(rng));
                        }
                    }
                }

                if (RoadClassInfo.LanesPerDirection(path.Edge.Class) >= 3)
                {
                    // Raised median kerb with hedges and the odd palm
                    for (int k = 0; k < path.Points.Length - 1; k++)
                    {
                        Vector3 a = path.Points[k], b = path.Points[k + 1];
                        if (Mathf.Max(a.y, b.y) > 0.1f) continue;
                        float d0 = path.Cumulative[k], d1 = path.Cumulative[k + 1];
                        if (d0 < jA || d1 > path.Length - jB) continue;
                        ctx.Concrete.AddBeam(a + Vector3.up * 0.05f, b + Vector3.up * 0.05f, new Vector2(0.24f, 0.06f), new Color(0.75f, 0.74f, 0.7f));
                        Vector3 mid = (a + b) * 0.5f + Vector3.up * 0.08f;
                        float yaw = Mathf.Atan2(b.x - a.x, b.z - a.z) * Mathf.Rad2Deg;
                        if (rng.Chance(0.85 * density)) PlaceRotated(leaves, wood, rng, TreeSpecies.Hedge, mid, yaw, 1f);
                        for (int g = 0; g < 2; g++)
                            GrassTuft(leaves.Next(), rng, Vector3.Lerp(a, b, rng.NextFloat()) + Vector3.up * 0.08f + new Vector3(rng.Range(-0.08f, 0.08f), 0f, rng.Range(-0.08f, 0.08f)), rng.Range(0.07f, 0.11f), LushGreen(rng));
                        if (k % 9 == 4 && rng.Chance(0.7)) Place(leaves, wood, rng, TreeSpecies.CoconutPalm, mid, rng.Range(0.8f, 1f));
                    }
                }
            }

            // 2) Parks, campus and colony groves (with bushes underneath)
            Grove(ctx, leaves, wood, rng, ctx.Map.Graph.Node("kalina").Position.XZ.ToVector2() + new Vector2(25f, 15f), 24f, (int)(70 * density));
            Grove(ctx, leaves, wood, rng, new Vector2(-140f, 140f), 42f, (int)(90 * density));
            Grove(ctx, leaves, wood, rng, new Vector2(95f, 315f), 36f, (int)(60 * density));
            Grove(ctx, leaves, wood, rng, new Vector2(300f, 160f), 26f, (int)(40 * density));
            Grove(ctx, leaves, wood, rng, new Vector2(40f, 60f), 22f, (int)(40 * density));

            // 3) Mangroves on both banks of the Mithi River
            float step = density >= 1f ? 2.0f : 3.0f;
            for (int i = 0; i + 1 < geo.River.Count; i++)
            {
                Vector2 a = geo.River[i].ToVector2(), b = geo.River[i + 1].ToVector2();
                float segLen = Vector2.Distance(a, b);
                Vector2 dir = (b - a) / segLen;
                Vector2 normal = new Vector2(-dir.y, dir.x);
                for (float s = 0; s < segLen; s += step)
                {
                    Vector2 c = a + dir * s;
                    geo.DistanceToRiver(c.x, c.y, out double t);
                    float hw = (float)geo.RiverHalfWidthAt(t);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vector2 m = c + normal * side * (hw + rng.Range(0.2f, 3.0f)) + dir * rng.Range(-0.6f, 0.6f);
                        float gy = geo.GroundY(m.x, m.y);
                        if (gy < -0.42f || gy > 0.01f) continue;
                        if (ctx.Space.Clearance(m) < 0.3f) continue;
                        Place(leaves, wood, rng, TreeSpecies.Mangrove, new Vector3(m.x, Mathf.Max(gy, -0.34f), m.y), rng.Range(0.8f, 1.3f));
                    }
                }
            }

            // 4) Grass and weeds on every patch of open ground (vacant plots, verges, river banks)
            VacantLand(ctx, leaves, rng, density);

            var root = new GameObject("Nature").transform;
            root.SetParent(ctx.Root, false);
            leaves.Emit("Foliage", root, ctx.Mats.Leaves);
            wood.Emit("TrunksAndBranches", root, ctx.Mats.Bark);
        }

        private static TreeSpecies PickAvenue(Pcg32 rng, bool bkc, bool coast)
        {
            if (coast && rng.Chance(0.6)) return TreeSpecies.CoconutPalm;
            double r = rng.NextDouble();
            if (bkc) return r < 0.55 ? TreeSpecies.RainTree : r < 0.85 ? TreeSpecies.Ashoka : TreeSpecies.Gulmohar;
            return r < 0.35 ? TreeSpecies.Ashoka : r < 0.6 ? TreeSpecies.Neem : r < 0.8 ? TreeSpecies.Gulmohar : r < 0.92 ? TreeSpecies.RainTree : TreeSpecies.CoconutPalm;
        }

        private static void Grove(WorldContext ctx, ChunkedMeshBuilder leaves, ChunkedMeshBuilder wood, Pcg32 rng, Vector2 center, float radius, int count)
        {
            CityGeography geo = ctx.Map.Geography;
            for (int i = 0, tries = 0; i < count && tries < count * 8; tries++)
            {
                float a = rng.Range(0f, Mathf.PI * 2f), r = Mathf.Sqrt(rng.NextFloat()) * radius;
                Vector2 p = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                if (!ctx.Space.IsFree(p, 0.7f, 0.1f)) continue;
                float gy = geo.GroundY(p.x, p.y);
                if (gy < -0.05f) continue;
                double k = rng.NextDouble();
                TreeSpecies sp = k < 0.3 ? TreeSpecies.RainTree : k < 0.55 ? TreeSpecies.Neem : k < 0.75 ? TreeSpecies.Gulmohar : TreeSpecies.Ashoka;
                if (p.x < (float)geo.CoastBaseX + 70f && rng.Chance(0.5)) sp = TreeSpecies.CoconutPalm;
                Place(leaves, wood, rng, sp, new Vector3(p.x, gy, p.y), rng.Range(0.8f, 1.25f));
                for (int b = 0; b < 3; b++)
                {
                    Vector2 q = p + new Vector2(rng.Range(-1.2f, 1.2f), rng.Range(-1.2f, 1.2f));
                    if (ctx.Space.IsFree(q, 0.3f, 0.05f)) Place(leaves, wood, rng, TreeSpecies.Bush, new Vector3(q.x, geo.GroundY(q.x, q.y), q.y), rng.Range(0.7f, 1.3f));
                }
                for (int g = 0; g < 6; g++)
                {
                    Vector2 q = p + new Vector2(rng.Range(-1.4f, 1.4f), rng.Range(-1.4f, 1.4f));
                    GrassTuft(leaves.Next(), rng, new Vector3(q.x, geo.GroundY(q.x, q.y), q.y), rng.Range(0.08f, 0.14f), LushGreen(rng));
                }
                ctx.Space.AddCircle(p, 0.7f);
                i++;
            }
        }

        private static Color LushGreen(Pcg32 rng) => Color.Lerp(new Color(0.18f, 0.36f, 0.1f), new Color(0.32f, 0.48f, 0.16f), rng.NextFloat());
        private static Color DryGreen(Pcg32 rng) => Color.Lerp(new Color(0.3f, 0.4f, 0.14f), new Color(0.55f, 0.5f, 0.25f), rng.NextFloat());

        private static void VacantLand(WorldContext ctx, ChunkedMeshBuilder leaves, Pcg32 rng, float density)
        {
            CityGeography geo = ctx.Map.Geography;
            int samples = Mathf.RoundToInt(9000 * density);
            for (int i = 0; i < samples; i++)
            {
                var p = new Vector2(rng.Range((float)geo.MinX + 20f, (float)geo.MaxX - 20f), rng.Range((float)geo.MinZ + 20f, (float)geo.MaxZ - 20f));
                float gy = geo.GroundY(p.x, p.y);
                if (gy < -0.2f || !ctx.Space.IsFree(p, 0.25f, 0.05f)) continue;
                bool bank = gy < -0.02f; // river bank mud: reeds
                int n = rng.Range(2, 6);
                for (int k = 0; k < n; k++)
                {
                    Vector2 q = p + new Vector2(rng.Range(-0.35f, 0.35f), rng.Range(-0.35f, 0.35f));
                    GrassTuft(leaves.Next(), rng, new Vector3(q.x, geo.GroundY(q.x, q.y), q.y), rng.Range(0.07f, bank ? 0.2f : 0.13f), bank ? LushGreen(rng) : DryGreen(rng));
                }
            }
        }

        /// <summary>Two crossed grass cards (Foliage shader pattern 2): blades sway at the tips only.</summary>
        public static void GrassTuft(MeshBuilder mb, Pcg32 rng, Vector3 pos, float size, Color color)
        {
            float yaw = rng.Range(0f, Mathf.PI);
            float seed = rng.NextFloat() * 10f;
            for (int c = 0; c < 2; c++)
            {
                float a = yaw + c * Mathf.PI * 0.5f;
                Vector3 r = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (size * 0.6f);
                Vector3 u = Vector3.up * size;
                var low = new Vector4(0f, 2f, seed, 0f);
                var high = new Vector4(0.6f, 2f, seed, 0f);
                int i0 = mb.AddVertex(pos - r, Vector3.up, new Vector2(0, 0), color, default, low);
                int i1 = mb.AddVertex(pos + r, Vector3.up, new Vector2(1, 0), color, default, low);
                int i2 = mb.AddVertex(pos + r + u, Vector3.up, new Vector2(1, 1), color, default, high);
                int i3 = mb.AddVertex(pos - r + u, Vector3.up, new Vector2(0, 1), color, default, high);
                mb.AddTriangleIndices(i0, i1, i2); // the Foliage shader renders both sides (Cull Off)
                mb.AddTriangleIndices(i0, i2, i3);
            }
        }

        private static void Place(ChunkedMeshBuilder leaves, ChunkedMeshBuilder wood, Pcg32 rng, TreeSpecies sp, Vector3 pos, float scale)
        {
            PlaceRotated(leaves, wood, rng, sp, pos, rng.Range(0f, 360f), scale);
        }

        private static void PlaceRotated(ChunkedMeshBuilder leaves, ChunkedMeshBuilder wood, Pcg32 rng, TreeSpecies sp, Vector3 pos, float yaw, float scale)
        {
            TreeFactory.Template t = TreeFactory.Get(sp, rng.Range(0, 3));
            Matrix4x4 m = Matrix4x4.TRS(pos, Quaternion.Euler(0f, yaw, 0f), Vector3.one * scale);
            Color tint = Color.white * rng.Range(0.88f, 1.12f);
            tint.a = 1f;
            float seedOffset = rng.NextFloat() * 50f;
            leaves.Next().Append(t.Leaves, m, tint, seedOffset);
            if (!t.Wood.IsEmpty) wood.Next().Append(t.Wood, m, Color.white, 0f);
        }
    }
}
