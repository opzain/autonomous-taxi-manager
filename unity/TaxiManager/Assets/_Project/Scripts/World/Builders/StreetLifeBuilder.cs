using System.Collections.Generic;
using TaxiSim.Core;
using UnityEngine;

namespace TaxiSim.World
{
    /// <summary>
    /// v0.7 street life: what makes a Mumbai street feel inhabited.
    ///  * roadside stalls: vada-pav / pani-puri carts with umbrellas, fruit and vegetable stalls under
    ///    blue tarps, chai / paan kiosks, with bulbs strung under the canopy at night and people around;
    ///  * BEST bus shelters with benches, glass backs and lit ad panels, and commuters waiting;
    ///  * pole-mounted hoardings along the arterials (with flood lamps), rooftop billboards and
    ///    big vinyl adverts on the side walls of mid-rise buildings.
    /// Run after the buildings (it avoids them) and before the trees (trees avoid stalls / shelters).
    /// </summary>
    public static class StreetLifeBuilder
    {
        private static readonly Color[] Umbrellas =
        {
            new Color(0.85f, 0.15f, 0.12f), new Color(0.12f, 0.35f, 0.8f), new Color(0.95f, 0.75f, 0.1f),
            new Color(0.15f, 0.6f, 0.3f), new Color(0.95f, 0.45f, 0.1f), new Color(0.9f, 0.9f, 0.88f)
        };

        private static readonly Color[] Produce =
        {
            new Color(0.95f, 0.55f, 0.1f), new Color(0.8f, 0.12f, 0.1f), new Color(0.35f, 0.65f, 0.15f),
            new Color(0.95f, 0.85f, 0.2f), new Color(0.5f, 0.15f, 0.35f), new Color(0.2f, 0.45f, 0.12f)
        };

        private static readonly Color[] KioskPaint =
        {
            new Color(0.2f, 0.45f, 0.75f), new Color(0.85f, 0.25f, 0.2f), new Color(0.2f, 0.6f, 0.45f),
            new Color(0.95f, 0.7f, 0.15f), new Color(0.6f, 0.3f, 0.6f)
        };

        private static readonly Color Steel = new Color(0.3f, 0.31f, 0.33f);
        private static readonly Color BestRed = new Color(0.72f, 0.1f, 0.1f);

        public struct Stats { public int Stalls, Shelters, Hoardings, RoofBoards, WallAds, People; }

        public static Stats Build(WorldContext ctx)
        {
            var st = new Stats();
            var rng = new Pcg32(9091UL);
            var personBuilders = new MeshBuilder[PedestrianFactory.VariantCount];
            for (int i = 0; i < personBuilders.Length; i++) personBuilders[i] = PedestrianFactory.Build(i, 501UL + (ulong)i * 13UL);
            float density = Mathf.Clamp(ctx.Density, 0.3f, 1f);

            foreach (RoadPath path in ctx.Roads.Paths)
            {
                if (path.Edge.Elevated) continue;
                float jA = ctx.Roads.JunctionRadius[path.NodeA.Id] + 1.4f;
                float jB = ctx.Roads.JunctionRadius[path.NodeB.Id] + 1.4f;
                bool core = path.LedLighting;
                RoadClass cls = path.Edge.Class;

                for (int side = -1; side <= 1; side += 2)
                {
                    // ---- Stalls (busy everywhere except the corporate BKC core) ----
                    float stallSpacing = (core ? 16f : cls == RoadClass.Local ? 4.5f : 7f) / density;
                    for (float s = jA + rng.Range(0f, stallSpacing); s < path.Length - jB; s += stallSpacing * rng.Range(0.6f, 1.5f))
                    {
                        if (!Frame(path, s, side, 0.66f, out Vector3 pos, out Vector3 along, out Vector3 outward)) continue;
                        Vector2 p2 = new Vector2(pos.x, pos.z);
                        if (!ctx.FootpathFree(p2, 0.6f)) continue;
                        Stall(ctx, rng, pos, along, outward, personBuilders);
                        ctx.FootpathReserved.Add(p2);
                        st.Stalls++;
                        st.People += 2;
                    }

                    // ---- BEST bus shelters on main roads ----
                    if (cls != RoadClass.Local)
                    {
                        float shelterSpacing = 34f;
                        for (float s = jA + 4f + rng.Range(0f, shelterSpacing * 0.5f); s < path.Length - jB - 2f; s += shelterSpacing * rng.Range(0.8f, 1.3f))
                        {
                            if (!rng.Chance(0.75)) continue;
                            if (!Frame(path, s, side, 0.6f, out Vector3 pos, out Vector3 along, out Vector3 outward)) continue;
                            Vector2 p2 = new Vector2(pos.x, pos.z);
                            if (!ctx.FootpathFree(p2, 1.0f)) continue;
                            Shelter(ctx, rng, pos, along, outward, personBuilders, ref st);
                            ctx.FootpathReserved.Add(p2);
                            ctx.FootpathReserved.Add(p2 + new Vector2(along.x, along.z) * 0.45f);
                            ctx.FootpathReserved.Add(p2 - new Vector2(along.x, along.z) * 0.45f);
                            st.Shelters++;
                        }
                    }

                    // ---- Pole hoardings just behind the footpath on arterials ----
                    if (cls == RoadClass.Arterial || cls == RoadClass.Expressway || (cls == RoadClass.Main && !core))
                    {
                        float hoardSpacing = 22f / density;
                        for (float s = jA + rng.Range(0f, hoardSpacing); s < path.Length - jB; s += hoardSpacing * rng.Range(0.7f, 1.4f))
                        {
                            if (!rng.Chance(0.55)) continue;
                            if (!Frame(path, s, side, 1.75f, out Vector3 pos, out Vector3 along, out Vector3 outward)) continue;
                            Vector2 p2 = new Vector2(pos.x, pos.z);
                            if (!ctx.Space.IsFree(p2, 0.55f, 0.05f)) continue;
                            if (ctx.GroundY(pos.x, pos.z) < -0.05f) continue;
                            Hoarding(ctx, rng, pos, along, outward);
                            ctx.Space.AddCircle(p2, 0.55f);
                            st.Hoardings++;
                        }
                    }
                }
            }

            BuildingAds(ctx, rng, ref st);
            return st;
        }

        /// <summary>A footpath position: 'offset' beyond the kerb line on 'side', with the local frame.</summary>
        private static bool Frame(RoadPath path, float s, int side, float offset, out Vector3 pos, out Vector3 along, out Vector3 outward)
        {
            Vector3 p = path.Sample(s, out Vector3 fwd);
            along = new Vector3(fwd.x, 0, fwd.z).normalized;
            Vector3 right = new Vector3(along.z, 0, -along.x);
            outward = right * side;
            pos = p + outward * (path.Width * 0.5f + offset) + Vector3.up * 0.05f;
            return p.y <= 0.1f;
        }

        // ------------------------------------------------------------------------------------------
        private static void Stall(WorldContext ctx, Pcg32 rng, Vector3 pos, Vector3 along, Vector3 outward, MeshBuilder[] people)
        {
            Quaternion q = Quaternion.LookRotation(along, Vector3.up);
            Vector3 roadSide = -outward;
            MeshBuilder c = ctx.Concrete;
            double kind = rng.NextDouble();

            if (kind < 0.42)
            {
                // Cart with umbrella (vada pav, pani puri, bhutta, juice)
                Color body = rng.Chance(0.5) ? new Color(0.75f, 0.76f, 0.78f) : rng.Pick(KioskPaint);
                c.AddBox(pos + Vector3.up * 0.1f, q, new Vector3(0.13f, 0.08f, 0.26f), body);
                c.AddBox(pos + Vector3.up * 0.145f, q, new Vector3(0.14f, 0.01f, 0.27f), new Color(0.82f, 0.82f, 0.84f)); // steel top
                for (int i = -1; i <= 1; i += 2)
                    c.AddDisc(pos + along * 0.07f * i + outward * 0.07f + Vector3.up * 0.04f, 0.04f, 10, outward, new Color(0.1f, 0.1f, 0.1f));
                for (int i = 0; i < 4; i++)
                    c.AddEllipsoid(pos + Vector3.up * 0.16f + along * rng.Range(-0.1f, 0.1f) + outward * rng.Range(-0.04f, 0.04f),
                                   new Vector3(0.022f, 0.016f, 0.022f), 6, 4, rng.Pick(Produce), 0f);
                Color umb = rng.Pick(Umbrellas);
                ctx.Metal.AddBeam(pos + Vector3.up * 0.15f, pos + Vector3.up * 0.34f, new Vector2(0.008f, 0.008f), Steel);
                c.AddCylinder(pos + Vector3.up * 0.3f, 0.21f, 0.01f, 0.07f, 12, umb, capTop: false);
                c.AddDisc(pos + Vector3.up * 0.3f, 0.21f, 12, Vector3.down, umb * 0.7f);
                Bulb(ctx, pos + Vector3.up * 0.285f + along * 0.08f);
                Bulb(ctx, pos + Vector3.up * 0.285f - along * 0.08f);
            }
            else if (kind < 0.75)
            {
                // Fruit / vegetable stall under a tarpaulin
                c.AddBox(pos + Vector3.up * 0.075f, q, new Vector3(0.2f, 0.02f, 0.34f), new Color(0.45f, 0.32f, 0.2f));
                for (int i = -1; i <= 1; i += 2)
                    for (int j = -1; j <= 1; j += 2)
                        ctx.Metal.AddBeam(pos + along * 0.16f * i + outward * 0.09f * j, pos + along * 0.16f * i + outward * 0.09f * j + Vector3.up * (j > 0 ? 0.32f : 0.27f),
                                          new Vector2(0.008f, 0.008f), new Color(0.35f, 0.25f, 0.15f));
                int piles = rng.Range(3, 6);
                for (int i = 0; i < piles; i++)
                {
                    Color col = rng.Pick(Produce);
                    Vector3 pc = pos + Vector3.up * 0.09f + along * Mathf.Lerp(-0.13f, 0.13f, (i + 0.5f) / piles);
                    c.AddEllipsoid(pc, new Vector3(0.032f, 0.022f, 0.06f), 6, 4, col, 0f, hemisphere: true);
                }
                Color tarp = rng.Chance(0.7) ? new Color(0.1f, 0.3f, 0.72f) : new Color(0.85f, 0.45f, 0.1f);
                Vector3 roofC = pos + Vector3.up * 0.3f;
                // Sloping tarp (higher at the back, away from the road)
                Vector3 a0 = roofC - along * 0.19f + roadSide * 0.13f + Vector3.down * 0.03f;
                Vector3 a1 = roofC + along * 0.19f + roadSide * 0.13f + Vector3.down * 0.03f;
                Vector3 b1 = roofC + along * 0.19f + outward * 0.12f + Vector3.up * 0.02f;
                Vector3 b0 = roofC - along * 0.19f + outward * 0.12f + Vector3.up * 0.02f;
                Vector3 n = Vector3.Cross(a1 - a0, b0 - a0).normalized;
                if (n.y < 0) n = -n;
                c.AddQuad(a0, a1, b1, b0, n, tarp);
                c.AddQuad(a0, a1, b1, b0, -n, tarp * 0.75f);
                Bulb(ctx, roofC + Vector3.down * 0.04f);
            }
            else
            {
                // Chai / paan kiosk with a printed sign on top
                Color paint = rng.Pick(KioskPaint);
                c.AddBox(pos + Vector3.up * 0.13f, q, new Vector3(0.2f, 0.26f, 0.22f), paint);
                c.AddBox(pos + roadSide * 0.101f + Vector3.up * 0.16f, q, new Vector3(0.004f, 0.1f, 0.16f), new Color(0.05f, 0.05f, 0.05f)); // serving hatch
                c.AddBox(pos + roadSide * 0.13f + Vector3.up * 0.22f, q, new Vector3(0.07f, 0.008f, 0.24f), paint * 0.7f); // awning
                c.AddBox(pos + roadSide * 0.06f + Vector3.up * 0.1f, q, new Vector3(0.06f, 0.006f, 0.18f), new Color(0.7f, 0.7f, 0.72f)); // counter
                Poster(ctx, rng, pos + Vector3.up * 0.26f + roadSide * 0.111f, roadSide, 0.2f, 0.07f);
                Bulb(ctx, pos + roadSide * 0.14f + Vector3.up * 0.205f);
            }

            // Vendor behind, one or two customers on the road side
            Person(ctx, rng, people, pos + outward * 0.16f, roadSide);
            int customers = rng.Range(0, 3);
            for (int i = 0; i < customers; i++)
                Person(ctx, rng, people, pos + roadSide * rng.Range(0.16f, 0.24f) + along * rng.Range(-0.15f, 0.15f), outward);
        }

        private static void Bulb(WorldContext ctx, Vector3 p)
        {
            ctx.StallBulbs.AddEllipsoid(p, new Vector3(0.012f, 0.016f, 0.012f), 6, 4, Color.white, 0f);
        }

        private static void Person(WorldContext ctx, Pcg32 rng, MeshBuilder[] people, Vector3 feet, Vector3 facing)
        {
            Vector3 f = facing + new Vector3(rng.Range(-0.4f, 0.4f), 0, rng.Range(-0.4f, 0.4f));
            f.y = 0;
            if (f.sqrMagnitude < 1e-4f) f = Vector3.forward;
            Matrix4x4 m = Matrix4x4.TRS(feet, Quaternion.LookRotation(f.normalized, Vector3.up), Vector3.one);
            ctx.IdlePeople.Append(people[rng.Range(0, people.Length)], m, Color.white);
        }

        // ------------------------------------------------------------------------------------------
        private static void Shelter(WorldContext ctx, Pcg32 rng, Vector3 pos, Vector3 along, Vector3 outward, MeshBuilder[] people, ref Stats st)
        {
            Quaternion q = Quaternion.LookRotation(along, Vector3.up);
            Vector3 back = pos + outward * 0.14f;
            const float len = 0.8f;
            // Roof with a slight forward tilt and a red BEST fascia
            ctx.Metal.AddBox(pos + Vector3.up * 0.34f, q, new Vector3(0.36f, 0.02f, len + 0.06f), new Color(0.55f, 0.57f, 0.6f));
            ctx.Concrete.AddBox(pos - outward * 0.17f + Vector3.up * 0.325f, q, new Vector3(0.02f, 0.05f, len + 0.06f), BestRed);
            // Posts
            for (int i = -1; i <= 1; i++)
                ctx.Metal.AddBeam(back + along * (len * 0.5f * i), back + along * (len * 0.5f * i) + Vector3.up * 0.33f, new Vector2(0.014f, 0.014f), Steel);
            // Glass back panel and bench
            ctx.Glass.AddBox(back + Vector3.up * 0.19f, q, new Vector3(0.008f, 0.24f, len), Color.white);
            ctx.Concrete.AddBox(back - outward * 0.05f + Vector3.up * 0.08f, q, new Vector3(0.08f, 0.012f, len * 0.7f), new Color(0.6f, 0.6f, 0.62f));
            // Double-sided ad panel at the end
            Vector3 adPos = pos + along * (len * 0.5f + 0.06f) + Vector3.up * 0.2f;
            ctx.Metal.AddBox(adPos, q, new Vector3(0.26f, 0.32f, 0.02f), Steel);
            Poster(ctx, rng, adPos + along * 0.011f, along, 0.22f, 0.28f, bottomAt: adPos.y - 0.14f);
            Poster(ctx, rng, adPos - along * 0.011f, -along, 0.22f, 0.28f, bottomAt: adPos.y - 0.14f);
            // Bus stop sign
            Vector3 signBase = pos - outward * 0.2f - along * (len * 0.5f + 0.1f);
            ctx.Metal.AddBeam(signBase, signBase + Vector3.up * 0.38f, new Vector2(0.01f, 0.01f), Steel);
            ctx.Concrete.AddBox(signBase + Vector3.up * 0.38f, q, new Vector3(0.1f, 0.06f, 0.01f), BestRed);

            int waiting = rng.Range(1, 5);
            for (int i = 0; i < waiting; i++)
                Person(ctx, rng, people, pos + along * rng.Range(-len * 0.4f, len * 0.4f) - outward * rng.Range(-0.02f, 0.12f), -outward);
            st.People += waiting;
        }

        // ------------------------------------------------------------------------------------------
        /// <summary>Unipole hoarding angled towards oncoming traffic, with flood lamps and a catwalk.</summary>
        private static void Hoarding(WorldContext ctx, Pcg32 rng, Vector3 pos, Vector3 along, Vector3 outward)
        {
            float width = rng.Range(1.5f, 2.3f), height = width * rng.Range(0.36f, 0.45f);
            float poleH = rng.Range(1.1f, 1.7f);
            // Angled ~22° towards the near-side traffic coming at it (left-hand driving: on the right-hand
            // footpath of 'along' the nearest lanes travel against 'along')
            float sideSign = Mathf.Sign(Vector3.Dot(outward, new Vector3(along.z, 0f, -along.x)));
            Vector3 facing = (-outward + along * (0.4f * sideSign)).normalized;
            Vector3 boardRight = Vector3.Cross(facing, Vector3.up).normalized;
            Quaternion q = Quaternion.LookRotation(facing, Vector3.up);

            ctx.Metal.AddCylinder(pos, 0.07f, 0.05f, poleH, 10, Steel, capTop: false);
            Vector3 centre = pos + Vector3.up * (poleH + height * 0.5f);
            // Steel back frame, then the printed face
            ctx.Metal.AddBox(centre - facing * 0.03f, q, new Vector3(width + 0.06f, height + 0.06f, 0.04f), Steel);
            ctx.Metal.AddBox(centre - facing * 0.07f, q, new Vector3(0.05f, height, 0.05f), Steel);
            Poster(ctx, rng, centre + facing * 0.001f, facing, width, height, bottomAt: centre.y - height * 0.5f);
            // Catwalk and flood lamps
            ctx.Metal.AddBox(pos + Vector3.up * (poleH - 0.02f) + facing * 0.06f, q, new Vector3(width, 0.015f, 0.12f), Steel);
            for (int i = -1; i <= 1; i++)
            {
                Vector3 armBase = pos + Vector3.up * (poleH - 0.02f) + boardRight * (width * 0.33f * i) + facing * 0.1f;
                Vector3 head = armBase + facing * 0.18f + Vector3.up * 0.04f;
                ctx.Metal.AddBeam(armBase, head, new Vector2(0.012f, 0.012f), Steel);
                ctx.NightLights.AddBox(head, q, new Vector3(0.07f, 0.025f, 0.04f), Color.white);
            }
        }

        /// <summary>
        /// A printed advert quad facing 'facing' (u runs left to right as seen by the viewer).
        /// pos is the bottom-centre unless bottomAt is given (then pos is any point on the centre line).
        /// </summary>
        private static void Poster(WorldContext ctx, Pcg32 rng, Vector3 pos, Vector3 facing, float width, float height, float bottomAt = float.NaN)
        {
            Vector3 b = float.IsNaN(bottomAt) ? pos : new Vector3(pos.x, bottomAt, pos.z);
            facing.y = 0f;
            Vector3 r = Vector3.Cross(facing.normalized, Vector3.up).normalized * (width * 0.5f);
            Vector3 up = Vector3.up * height;
            var seed = new Vector4(rng.NextFloat(), 0, 0, 0);
            ctx.Posters.AddQuad(b - r, b + r, b + r + up, b - r + up, facing.normalized, Color.white,
                                new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1), default, seed);
        }

        // ------------------------------------------------------------------------------------------
        /// <summary>Rooftop billboards and big wall adverts on mid-rise buildings that face a street.</summary>
        private static void BuildingAds(WorldContext ctx, Pcg32 rng, ref Stats st)
        {
            float density = Mathf.Clamp(ctx.Density, 0.3f, 1f);
            var used = new HashSet<long>();
            foreach (BuildingRecord r in ctx.Towers.Records)
            {
                if (r.Cylinder || r.Height < 1.2f || r.Height > 10f || r.Width < 1f || r.Depth < 1f) continue;
                long key = ((long)Mathf.RoundToInt(r.Center.x * 4f) << 32) ^ (uint)Mathf.RoundToInt(r.Center.y * 4f);
                if (used.Contains(key)) continue;
                bool roof = rng.Chance(0.06 * density);
                bool wall = !roof && r.Height > 2.4f && rng.Chance(0.07 * density);
                if (!roof && !wall) continue;
                if (ctx.Space.NearestSegment(r.Center, out _) > 5f) continue; // only buildings that face a street
                used.Add(key);

                // Which side faces the street?
                Quaternion q = Quaternion.Euler(0f, r.RotationY, 0f);
                Vector3[] normals = { q * Vector3.right, q * Vector3.left, q * Vector3.forward, q * Vector3.back };
                float[] halves = { r.Width * 0.5f, r.Width * 0.5f, r.Depth * 0.5f, r.Depth * 0.5f };
                int best = 0;
                float bestD = float.MaxValue;
                for (int i = 0; i < 4; i++)
                {
                    Vector3 probe = new Vector3(r.Center.x, 0, r.Center.y) + normals[i] * (halves[i] + 1f);
                    float d = ctx.Space.NearestSegment(new Vector2(probe.x, probe.z), out _);
                    if (d < bestD) { bestD = d; best = i; }
                }
                Vector3 n = normals[best];
                Vector3 c = new Vector3(r.Center.x, r.Top, r.Center.y);

                if (roof)
                {
                    float faceLen = best < 2 ? r.Depth : r.Width;
                    float w = Mathf.Min(faceLen * 0.9f, 2.6f), h = Mathf.Clamp(w * 0.4f, 0.4f, 0.9f);
                    Vector3 right = Vector3.Cross(n, Vector3.up).normalized;
                    Vector3 basePos = c + n * (halves[best] * 0.4f);
                    // Lattice legs and a back frame
                    for (int i = -1; i <= 1; i += 2)
                    {
                        Vector3 leg = basePos + right * (w * 0.4f * i);
                        ctx.Metal.AddBeam(leg, leg + Vector3.up * (0.25f + h), new Vector2(0.03f, 0.03f), Steel);
                        ctx.Metal.AddBeam(leg - n * 0.3f, leg + Vector3.up * (0.2f + h * 0.8f), new Vector2(0.02f, 0.02f), Steel);
                    }
                    Quaternion fq = Quaternion.LookRotation(n, Vector3.up);
                    ctx.Metal.AddBox(basePos + Vector3.up * (0.25f + h * 0.5f) - n * 0.025f, fq, new Vector3(w + 0.05f, h + 0.05f, 0.03f), Steel);
                    Poster(ctx, rng, basePos + n * 0.001f + Vector3.up * 0.25f, n, w, h);
                    for (int i = -1; i <= 1; i += 2)
                        ctx.NightLights.AddBox(basePos + right * (w * 0.3f * i) + n * 0.15f + Vector3.up * 0.2f, fq, new Vector3(0.06f, 0.02f, 0.04f), Color.white);
                    st.RoofBoards++;
                }
                else
                {
                    // Vinyl advert on a side wall (perpendicular to the street face), upper floors
                    int sideFace = best < 2 ? 2 + rng.Range(0, 2) : rng.Range(0, 2);
                    Vector3 sn = normals[sideFace];
                    float faceLen = sideFace < 2 ? r.Depth : r.Width;
                    float w = Mathf.Min(faceLen * 0.75f, 2.2f), h = Mathf.Min(r.Height * 0.45f, w * 1.3f);
                    Vector3 wallPos = new Vector3(r.Center.x, r.Top - h - 0.25f, r.Center.y) + sn * (halves[sideFace] + 0.012f);
                    Poster(ctx, rng, wallPos, sn, w, h);
                    st.WallAds++;
                }
            }
        }
    }
}
