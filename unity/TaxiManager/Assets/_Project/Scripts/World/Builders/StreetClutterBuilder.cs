using System.Collections.Generic;
using TaxiSim.Core;
using UnityEngine;

namespace TaxiSim.World
{
    /// <summary>
    /// v0.10 realism pass: the everyday clutter that makes a Mumbai street look real.
    ///  * cars, kaali-peelis and autos parked half on the kerb; rows of parked scooters on the footpath;
    ///  * concrete electric poles with sagging, tangled overhead cables, transformers and cables across the road;
    ///  * black-and-yellow painted kerbs on main roads and near every junction; speed breakers on side streets;
    ///  * BMC garbage bins with overflowing bags, hand carts (thelas), stray dogs and the odd cow;
    ///  * aluminium fins on glass towers (real depth and shadows instead of a flat curtain wall).
    /// Runs after the street life (stalls / shelters) and before the trees, so trees avoid everything here.
    /// </summary>
    public static class StreetClutterBuilder
    {
        public struct Stats { public int ParkedCars, Scooters, Poles, Wires, Bins, Dogs, Cows, Carts, Breakers, Fins; }

        private static readonly Color PoleGrey = new Color(0.42f, 0.42f, 0.4f);
        private static readonly Color Cable = new Color(0.04f, 0.04f, 0.045f);
        private static readonly Color KerbYellow = new Color(0.95f, 0.78f, 0.08f);
        private static readonly Color KerbBlack = new Color(0.06f, 0.06f, 0.06f);

        public static Stats Build(WorldContext ctx)
        {
            var st = new Stats();
            var rng = new Pcg32(6067UL);
            float density = Mathf.Clamp(ctx.Density, 0.3f, 1f);
            var clutter = new ChunkedMeshBuilder();
            var paint = new ChunkedMeshBuilder();
            var glass = new ChunkedMeshBuilder();
            var lampXZ = new List<Vector2>();
            foreach (Vector3 l in ctx.Roads.LampHeads) lampXZ.Add(new Vector2(l.x, l.z));

            foreach (RoadPath path in ctx.Roads.Paths)
            {
                if (path.Edge.Elevated) continue;
                bool core = path.LedLighting;
                RoadClass cls = path.Edge.Class;
                float jA = ctx.Roads.JunctionRadius[path.NodeA.Id], jB = ctx.Roads.JunctionRadius[path.NodeB.Id];
                Kerbs(path, clutter.Next(), jA, jB, cls != RoadClass.Local);

                for (int side = -1; side <= 1; side += 2)
                {
                    // ---- Parked cars, half on the kerb (not in the BKC office core: basement parking there)
                    if (!core && cls != RoadClass.Expressway)
                    {
                        float spacing = (cls == RoadClass.Local ? 1.4f : 2.2f) / density;
                        for (float s = jA + 3.2f + rng.Range(0f, spacing); s < path.Length - jB - 3.2f; s += spacing * rng.Range(0.7f, 1.8f))
                        {
                            if (!rng.Chance(0.55)) continue;
                            if (!Frame(path, s, side, 0.04f, out Vector3 pos, out Vector3 along, out Vector3 outward)) continue;
                            if (Near(lampXZ, pos, 0.5f) || !ctx.FootpathFree(new Vector2(pos.x, pos.z), 0.55f)) continue;
                            pos.y -= 0.05f; // half on the road, half on the kerb
                            double k = rng.NextDouble();
                            VehicleKind kind = k < 0.55 ? VehicleKind.Sedan : k < 0.8 ? VehicleKind.KaaliPeeli : VehicleKind.AutoRickshaw;
                            Color col = VehicleFactory.CarPaints[rng.Range(0, VehicleFactory.CarPaints.Length)] * rng.Range(0.7f, 1f);
                            col.a = 1f;
                            Vector3 facing = along * -side; // nose in the direction of the near-side traffic
                            Vector3 up = (Vector3.up - outward * 0.16f).normalized; // kerb side sits higher
                            Matrix4x4 m = Matrix4x4.TRS(pos + Vector3.up * 0.022f, Quaternion.LookRotation(facing, up), Vector3.one);
                            AppendVehicle(kind, col, m, paint, glass);
                            st.ParkedCars++;
                        }
                    }

                    // ---- Parked scooter rows on the footpath (outside the core)
                    if (!core)
                    {
                        float spacing = 7f / density;
                        for (float s = jA + 2f + rng.Range(0f, spacing); s < path.Length - jB - 2f; s += spacing * rng.Range(0.7f, 1.5f))
                        {
                            if (!rng.Chance(0.4)) continue;
                            if (!Frame(path, s, side, 0.62f, out Vector3 pos, out Vector3 along, out Vector3 outward)) continue;
                            var c2 = new Vector2(pos.x, pos.z);
                            if (!ctx.FootpathFree(c2, 0.55f)) continue;
                            int n = rng.Range(3, 8);
                            for (int i = 0; i < n; i++)
                            {
                                Vector3 sp = pos + along * ((i - (n - 1) * 0.5f) * 0.12f);
                                Vector3 dir = Quaternion.AngleAxis(side * 60f + rng.Range(-8f, 8f), Vector3.up) * along;
                                Color col = VehicleFactory.CarPaints[rng.Range(0, VehicleFactory.CarPaints.Length)];
                                AppendVehicle(VehicleKind.Scooter, col, Matrix4x4.TRS(sp, Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(0, 0, rng.Range(-6f, 6f)), Vector3.one), paint, glass);
                                st.Scooters++;
                            }
                            ctx.FootpathReserved.Add(c2);
                        }
                    }
                }

                // ---- Electric poles and overhead cables (one side of every non-core street)
                if (!core && cls != RoadClass.Expressway) Poles(ctx, rng, path, clutter, jA, jB, ref st);

                // ---- Speed breakers near both ends of side streets
                if (cls == RoadClass.Local && path.Length > 12f)
                {
                    SpeedBreaker(path, clutter.Next(), jA + 2.4f);
                    SpeedBreaker(path, clutter.Next(), path.Length - jB - 2.4f);
                    st.Breakers += 2;
                }

                // ---- Bins at the corners, carts, dogs, cows
                for (int end = 0; end < 2; end++)
                {
                    if (!rng.Chance(core ? 0.25 : 0.6)) continue;
                    float s = end == 0 ? jA + 1.3f : path.Length - jB - 1.3f;
                    int side = rng.Chance(0.5) ? 1 : -1;
                    if (!Frame(path, s, side, 0.66f, out Vector3 pos, out Vector3 along, out _)) continue;
                    if (!ctx.FootpathFree(new Vector2(pos.x, pos.z), 0.45f)) continue;
                    Bins(clutter.Next(), rng, pos, along);
                    ctx.FootpathReserved.Add(new Vector2(pos.x, pos.z));
                    st.Bins++;
                }
                if (!core)
                {
                    for (float s = jA + 4f + rng.Range(0f, 10f); s < path.Length - jB - 4f; s += rng.Range(8f, 18f) / density)
                    {
                        int side = rng.Chance(0.5) ? 1 : -1;
                        double roll = rng.NextDouble();
                        if (roll < 0.5)
                        {
                            if (!Frame(path, s, side, rng.Range(0.35f, 0.6f), out Vector3 pos, out Vector3 along, out _)) continue;
                            Dog(clutter.Next(), rng, pos, along);
                            st.Dogs++;
                        }
                        else if (roll < 0.72 && cls == RoadClass.Local)
                        {
                            if (!Frame(path, s, side, 0.68f, out Vector3 pos, out Vector3 along, out _)) continue;
                            var cc = new Vector2(pos.x, pos.z);
                            if (!ctx.FootpathFree(cc, 0.5f)) continue;
                            Cart(clutter.Next(), rng, pos, along);
                            ctx.FootpathReserved.Add(cc);
                            st.Carts++;
                        }
                        else if (roll < 0.78 && st.Cows < 18)
                        {
                            if (!Frame(path, s, side, 0.5f, out Vector3 pos, out Vector3 along, out _)) continue;
                            var c2 = new Vector2(pos.x, pos.z);
                            if (!ctx.FootpathFree(c2, 0.6f)) continue;
                            Cow(clutter.Next(), rng, pos, along);
                            ctx.FootpathReserved.Add(c2);
                            st.Cows++;
                        }
                    }
                }
            }

            st.Fins = TowerFins(ctx, clutter);

            var root = new GameObject("Street Clutter").transform;
            root.SetParent(ctx.Root, false);
            clutter.Emit("Clutter", root, ctx.Mats.Concrete);
            paint.Emit("ParkedVehicles", root, ctx.Mats.VehiclePaint);
            glass.Emit("ParkedVehicleGlass", root, ctx.Mats.Glass, false);
            return st;
        }

        // ------------------------------------------------------------------ helpers

        private static bool Frame(RoadPath path, float s, int side, float offset, out Vector3 pos, out Vector3 along, out Vector3 outward)
        {
            Vector3 p = path.Sample(s, out Vector3 fwd);
            along = new Vector3(fwd.x, 0f, fwd.z).normalized;
            Vector3 right = new Vector3(along.z, 0f, -along.x);
            outward = right * side;
            pos = p + outward * (path.Width * 0.5f + offset) + Vector3.up * (offset > 0f ? 0.05f : 0f);
            return p.y <= 0.1f;
        }

        private static bool Near(List<Vector2> points, Vector3 p, float r)
        {
            var q = new Vector2(p.x, p.z);
            float r2 = r * r;
            foreach (Vector2 x in points) if ((x - q).sqrMagnitude < r2) return true;
            return false;
        }

        private static void AppendVehicle(VehicleKind kind, Color col, Matrix4x4 m, ChunkedMeshBuilder paint, ChunkedMeshBuilder glass)
        {
            var p = new MeshBuilder();
            var g = new MeshBuilder();
            VehicleFactory.Fill(kind, col, p, g, new MeshBuilder(), parked: true); // parked: no lit lamps
            paint.Next().Append(p, m, Color.white);
            if (!g.IsEmpty) glass.Next().Append(g, m, Color.white);
        }

        /// <summary>Mumbai's black-and-yellow kerbs: main roads end to end, side streets near junctions.</summary>
        private static void Kerbs(RoadPath path, MeshBuilder mb, float jA, float jB, bool fullLength)
        {
            float half = path.Width * 0.5f;
            const float stripe = 0.22f;
            int index = 0;
            for (float s = 0.2f; s < path.Length - 0.2f; s += stripe, index++)
            {
                bool nearJunction = s < jA + 3f || s > path.Length - jB - 3f;
                if (!fullLength && !nearJunction) continue;
                Vector3 a = path.Sample(s, out Vector3 fwd), b = path.Sample(Mathf.Min(s + stripe, path.Length), out _);
                if (Mathf.Max(a.y, b.y) > 0.1f) continue;
                Vector3 along = new Vector3(fwd.x, 0f, fwd.z).normalized;
                Vector3 right = new Vector3(along.z, 0f, -along.x);
                Color c = index % 2 == 0 ? KerbYellow : KerbBlack;
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 o = right * side * (half - 0.003f);
                    Vector3 o2 = right * side * (half + 0.045f);
                    Vector3 lift = Vector3.up * 0.052f;
                    // Kerb face (towards the road) and a strip on the kerb top
                    mb.AddQuad(a + o, b + o, b + o + lift, a + o + lift, -right * side, c);
                    mb.AddQuad(a + o + lift, b + o + lift, b + o2 + lift, a + o2 + lift, Vector3.up, c);
                }
            }
        }

        private static void SpeedBreaker(RoadPath path, MeshBuilder mb, float s)
        {
            Vector3 p = path.Sample(s, out Vector3 fwd);
            if (p.y > 0.1f) return;
            Vector3 along = new Vector3(fwd.x, 0f, fwd.z).normalized;
            Vector3 right = new Vector3(along.z, 0f, -along.x);
            Quaternion q = Quaternion.LookRotation(along, Vector3.up);
            float half = path.Width * 0.5f;
            int n = Mathf.Max(4, Mathf.RoundToInt(path.Width / 0.14f));
            for (int i = 0; i < n; i++)
            {
                float x = Mathf.Lerp(-half + 0.05f, half - 0.05f, (i + 0.5f) / n);
                mb.AddBox(p + right * x + Vector3.up * 0.007f, q, new Vector3(path.Width / n, 0.014f, 0.16f), i % 2 == 0 ? KerbYellow : KerbBlack);
            }
        }

        private static void Poles(WorldContext ctx, Pcg32 rng, RoadPath path, ChunkedMeshBuilder clutter, float jA, float jB, ref Stats st)
        {
            int side = (path.Edge.Id % 2 == 0) ? 1 : -1;
            float spacing = rng.Range(5f, 6.5f);
            Vector3? prevTop = null;
            Vector3 prevRight = Vector3.zero;
            for (float s = jA + 1.5f; s < path.Length - jB - 1.0f; s += spacing)
            {
                if (!Frame(path, s, side, 0.74f, out Vector3 pos, out Vector3 along, out Vector3 outward)) { prevTop = null; continue; }
                var c2 = new Vector2(pos.x, pos.z);
                if (!ctx.FootpathFree(c2, 0.25f)) { prevTop = null; continue; }
                ctx.FootpathReserved.Add(c2);
                MeshBuilder mb = clutter.Next();
                const float h = 1.15f;
                mb.AddCylinder(pos, 0.026f, 0.018f, h, 6, PoleGrey);
                Vector3 top = pos + Vector3.up * (h - 0.06f);
                Vector3 armDir = new Vector3(along.x, 0f, along.z);
                Quaternion armRot = Quaternion.LookRotation(Vector3.Cross(Vector3.up, armDir), Vector3.up);
                mb.AddBox(top, armRot, new Vector3(0.012f, 0.012f, 0.32f), PoleGrey);
                for (int k = -1; k <= 1; k++)
                    mb.AddBox(top + Vector3.Cross(Vector3.up, armDir).normalized * (k * 0.14f) + Vector3.up * 0.015f, armRot, new Vector3(0.012f, 0.025f, 0.012f), new Color(0.75f, 0.72f, 0.65f));
                if (rng.Chance(0.15))
                    mb.AddBox(pos + Vector3.up * 0.75f + outward * 0.06f, armRot, new Vector3(0.1f, 0.16f, 0.09f), new Color(0.3f, 0.32f, 0.3f)); // transformer
                if (rng.Chance(0.3))
                    mb.AddEllipsoid(pos + Vector3.up * 0.55f - outward * 0.03f, new Vector3(0.04f, 0.03f, 0.04f), 6, 4, Cable, 0f); // coiled cable bundle
                st.Poles++;

                Vector3 right = Vector3.Cross(Vector3.up, armDir).normalized;
                if (prevTop.HasValue)
                {
                    // 3 power lines + 2-4 telephone / TV cables with different sag: the "tangle"
                    MeshBuilder wires = clutter.Next();
                    for (int k = -1; k <= 1; k++)
                        Catenary(wires, prevTop.Value + prevRight * (k * 0.14f) + Vector3.up * 0.03f, top + right * (k * 0.14f) + Vector3.up * 0.03f, 0.06f + 0.02f * k * k, 0.0035f);
                    int extra = rng.Range(2, 5);
                    for (int k = 0; k < extra; k++)
                    {
                        Vector3 a = prevTop.Value + Vector3.down * rng.Range(0.12f, 0.35f) + prevRight * rng.Range(-0.05f, 0.05f);
                        Vector3 b = top + Vector3.down * rng.Range(0.12f, 0.35f) + right * rng.Range(-0.05f, 0.05f);
                        Catenary(wires, a, b, rng.Range(0.12f, 0.3f), 0.004f);
                    }
                    st.Wires += 3 + extra;
                }
                // Now and then a cable straight across the street to the buildings opposite
                if (rng.Chance(0.3))
                {
                    Vector3 far = pos - outward * (path.Width + 1.65f) + Vector3.up * rng.Range(0.75f, 1.0f);
                    Catenary(clutter.Next(), top + Vector3.down * 0.1f, far, rng.Range(0.1f, 0.2f), 0.004f);
                    st.Wires++;
                }
                prevTop = top;
                prevRight = right;
            }
        }

        /// <summary>A sagging cable (parabola) as a thin 3-sided tube.</summary>
        private static void Catenary(MeshBuilder mb, Vector3 a, Vector3 b, float sag, float radius)
        {
            const int segs = 8;
            Vector3 prev = a;
            for (int i = 1; i <= segs; i++)
            {
                float t = i / (float)segs;
                Vector3 p = Vector3.Lerp(a, b, t) + Vector3.down * (4f * sag * t * (1f - t));
                mb.AddTube(prev, p, radius, radius, 3, Cable);
                prev = p;
            }
        }

        private static void Bins(MeshBuilder mb, Pcg32 rng, Vector3 pos, Vector3 along)
        {
            Quaternion q = Quaternion.LookRotation(along, Vector3.up);
            Color[] cols = { new Color(0.15f, 0.45f, 0.2f), new Color(0.15f, 0.3f, 0.6f) };
            for (int i = 0; i < 2; i++)
            {
                Vector3 b = pos + along * (i * 0.13f - 0.065f);
                mb.AddBox(b + Vector3.up * 0.055f, q, new Vector3(0.1f, 0.11f, 0.1f), cols[i]);
                mb.AddBox(b + Vector3.up * 0.113f, q, new Vector3(0.105f, 0.008f, 0.105f), cols[i] * 0.8f);
            }
            int bags = rng.Range(2, 6);
            for (int i = 0; i < bags; i++)
                mb.AddEllipsoid(pos + along * rng.Range(-0.18f, 0.18f) + new Vector3(rng.Range(-0.05f, 0.05f), 0.025f, rng.Range(-0.05f, 0.05f)),
                                new Vector3(0.035f, 0.028f, 0.035f), 6, 4, rng.Chance(0.6) ? new Color(0.06f, 0.06f, 0.07f) : new Color(0.75f, 0.75f, 0.72f), 0f);
        }

        private static void Dog(MeshBuilder mb, Pcg32 rng, Vector3 pos, Vector3 along)
        {
            Color[] coats = { new Color(0.62f, 0.45f, 0.28f), new Color(0.35f, 0.24f, 0.15f), new Color(0.12f, 0.1f, 0.09f), new Color(0.82f, 0.76f, 0.66f) };
            Color c = coats[rng.Range(0, coats.Length)];
            Quaternion q = Quaternion.AngleAxis(rng.Range(0f, 360f), Vector3.up);
            bool lying = rng.Chance(0.7);
            Vector3 body = pos + Vector3.up * (lying ? 0.022f : 0.06f);
            mb.AddEllipsoid(body, q * new Vector3(0.025f, 0.022f, 0.055f), 8, 5, c, 0f);
            Vector3 fwd = q * Vector3.forward;
            mb.AddEllipsoid(body + fwd * 0.055f + Vector3.up * (lying ? 0.005f : 0.025f), new Vector3(0.017f, 0.016f, 0.02f), 6, 4, c, 0f);
            if (!lying)
                for (int sx = -1; sx <= 1; sx += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                        mb.AddBox(pos + q * new Vector3(sx * 0.014f, 0.03f, sz * 0.035f), q, new Vector3(0.008f, 0.06f, 0.008f), c * 0.9f);
        }

        private static void Cow(MeshBuilder mb, Pcg32 rng, Vector3 pos, Vector3 along)
        {
            Color c = rng.Chance(0.6) ? new Color(0.88f, 0.86f, 0.82f) : new Color(0.55f, 0.48f, 0.4f);
            Quaternion q = Quaternion.LookRotation(Quaternion.AngleAxis(rng.Range(-40f, 40f), Vector3.up) * along, Vector3.up);
            Vector3 f = q * Vector3.forward;
            mb.AddEllipsoid(pos + Vector3.up * 0.15f, q * new Vector3(0.06f, 0.06f, 0.13f), 10, 6, c, 0f);
            mb.AddEllipsoid(pos + Vector3.up * 0.2f - f * 0.03f, q * new Vector3(0.045f, 0.04f, 0.05f), 8, 5, c, 0f); // hump
            mb.AddEllipsoid(pos + Vector3.up * 0.16f + f * 0.15f, q * new Vector3(0.03f, 0.035f, 0.045f), 8, 5, c * 0.95f, 0f);
            for (int s = -1; s <= 1; s += 2)
            {
                mb.AddBeam(pos + Vector3.up * 0.19f + f * 0.15f + q * Vector3.right * (s * 0.02f), pos + Vector3.up * 0.23f + f * 0.14f + q * Vector3.right * (s * 0.04f), new Vector2(0.006f, 0.006f), new Color(0.3f, 0.25f, 0.2f));
                for (int z = -1; z <= 1; z += 2)
                    mb.AddBox(pos + q * new Vector3(s * 0.035f, 0.05f, z * 0.08f), q, new Vector3(0.016f, 0.1f, 0.016f), c * 0.85f);
            }
        }

        private static void Cart(MeshBuilder mb, Pcg32 rng, Vector3 pos, Vector3 along)
        {
            Quaternion q = Quaternion.LookRotation(along, Vector3.up);
            var wood = new Color(0.45f, 0.3f, 0.18f);
            mb.AddBox(pos + Vector3.up * 0.09f, q, new Vector3(0.16f, 0.015f, 0.3f), wood);
            for (int s = -1; s <= 1; s += 2)
            {
                mb.AddDisc(pos + q * new Vector3(s * 0.09f, 0.065f, 0f), 0.065f, 12, q * Vector3.right * s, new Color(0.15f, 0.12f, 0.1f));
                mb.AddBeam(pos + q * new Vector3(s * 0.06f, 0.09f, 0.15f), pos + q * new Vector3(s * 0.06f, 0.07f, 0.3f), new Vector2(0.01f, 0.01f), wood);
            }
            int sacks = rng.Range(2, 6);
            for (int i = 0; i < sacks; i++)
                mb.AddEllipsoid(pos + q * new Vector3(rng.Range(-0.05f, 0.05f), 0.12f, rng.Range(-0.1f, 0.1f)), new Vector3(0.04f, 0.03f, 0.05f), 6, 4,
                                rng.Chance(0.5) ? new Color(0.75f, 0.68f, 0.5f) : new Color(0.85f, 0.4f, 0.15f), 0f);
        }

        /// <summary>Vertical aluminium fins on glass towers: real depth and shadow lines on curtain walls.</summary>
        private static int TowerFins(WorldContext ctx, ChunkedMeshBuilder clutter)
        {
            int count = 0;
            foreach (BuildingRecord r in ctx.Towers.Records)
            {
                if (r.Cylinder || r.Style.Style > 0.3f || r.Height < 4f || r.Width < 2f || r.Depth < 2f) continue;
                MeshBuilder mb = clutter.Next();
                Quaternion q = Quaternion.Euler(0f, r.RotationY, 0f);
                Color c = r.Style.Frame * 0.95f;
                c.a = 1f;
                float spacing = 0.9f;
                float y = r.BaseY + r.Height * 0.5f + 0.1f;
                float h = r.Height - 0.5f;
                for (int face = 0; face < 4; face++)
                {
                    bool xFace = face < 2;
                    float sign = face % 2 == 0 ? 1f : -1f;
                    float faceLen = xFace ? r.Depth : r.Width;
                    float offset = (xFace ? r.Width : r.Depth) * 0.5f + 0.03f;
                    int n = Mathf.Max(2, Mathf.FloorToInt(faceLen / spacing));
                    for (int i = 1; i < n; i++)
                    {
                        float u = -faceLen * 0.5f + i * faceLen / n;
                        Vector3 local = xFace ? new Vector3(sign * offset, 0f, u) : new Vector3(u, 0f, sign * offset);
                        Vector3 size = xFace ? new Vector3(0.07f, h, 0.025f) : new Vector3(0.025f, h, 0.07f);
                        mb.AddBox(new Vector3(r.Center.x, y, r.Center.y) + q * local, q, size, c);
                        count++;
                    }
                }
            }
            return count;
        }
    }
}
