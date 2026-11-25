using TaxiSim.Core;
using UnityEngine;

namespace TaxiSim.World
{
    /// <summary>
    /// Fills each district (G-Block CBD, Kurla, Dharavi, Bandra, far skylines...) with procedural
    /// buildings. Fully deterministic: the same seed always gives the same city.
    /// Buildings are aligned to the nearest street and never overlap roads, rails, water or landmarks.
    /// </summary>
    public static class CityBlockBuilder
    {
        private static readonly Color[] GlassTints =
        {
            new Color(0.10f, 0.20f, 0.30f), new Color(0.08f, 0.24f, 0.26f), new Color(0.22f, 0.18f, 0.12f),
            new Color(0.12f, 0.16f, 0.22f), new Color(0.14f, 0.22f, 0.18f), new Color(0.18f, 0.20f, 0.24f)
        };

        private static readonly Color[] CorporateFrames =
        {
            new Color(0.72f, 0.74f, 0.76f), new Color(0.55f, 0.57f, 0.6f), new Color(0.82f, 0.8f, 0.76f),
            new Color(0.35f, 0.37f, 0.4f), new Color(0.62f, 0.6f, 0.56f),
            new Color(0.2f, 0.2f, 0.22f), new Color(0.42f, 0.33f, 0.24f), new Color(0.9f, 0.89f, 0.86f),
            new Color(0.74f, 0.66f, 0.52f), new Color(0.3f, 0.36f, 0.42f)
        };

        // Mumbai apartment pastels (weathered by the monsoon)
        private static readonly Color[] Pastels =
        {
            new Color(0.88f, 0.84f, 0.72f), new Color(0.92f, 0.78f, 0.66f), new Color(0.86f, 0.86f, 0.62f),
            new Color(0.72f, 0.84f, 0.76f), new Color(0.72f, 0.8f, 0.88f), new Color(0.9f, 0.72f, 0.7f),
            new Color(0.9f, 0.9f, 0.88f), new Color(0.8f, 0.74f, 0.82f),
            new Color(0.78f, 0.52f, 0.4f), new Color(0.86f, 0.7f, 0.42f), new Color(0.5f, 0.68f, 0.7f),
            new Color(0.94f, 0.66f, 0.56f), new Color(0.62f, 0.72f, 0.86f), new Color(0.7f, 0.42f, 0.36f),
            new Color(0.66f, 0.7f, 0.5f), new Color(0.96f, 0.88f, 0.62f), new Color(0.84f, 0.84f, 0.84f)
        };

        private static readonly Color[] Vivid =
        {
            new Color(0.85f, 0.35f, 0.3f), new Color(0.3f, 0.55f, 0.8f), new Color(0.9f, 0.75f, 0.3f),
            new Color(0.4f, 0.7f, 0.45f), new Color(0.8f, 0.5f, 0.75f), new Color(0.75f, 0.72f, 0.68f),
            new Color(0.55f, 0.45f, 0.4f), new Color(0.95f, 0.6f, 0.35f)
        };

        /// <summary>Rooftop clutter, balconies and sunshades (Sintex tanks, AC units, stair heads, parapets).</summary>
        private static ChunkedMeshBuilder _props;
        private static float _propDensity = 1f;

        public static void Build(CityMap map, TowerMeshBuilder towers, SpaceRegistry space, float densityScale, ChunkedMeshBuilder props = null)
        {
            _props = props;
            _propDensity = densityScale;
            CityGeography geo = map.Geography;
            foreach (District district in geo.Districts)
            {
                var rng = new Pcg32(district.Seed * 7919UL + 13UL);
                if (district.Style == DistrictStyle.FarSkyline) RandomFill(geo, district, rng, towers, space, densityScale);
                else GridFill(geo, district, rng, towers, space, densityScale);
            }
            _props = null;
        }

        /// <summary>Dense jittered-grid infill: every free plot in the district gets a building (no empty land).</summary>
        private static void GridFill(CityGeography geo, District district, Pcg32 rng, TowerMeshBuilder towers, SpaceRegistry space, float densityScale)
        {
            float spacing = district.Style switch
            {
                DistrictStyle.GlassTowers => 7.5f,
                DistrictStyle.MixedCommercial => 5.5f,
                DistrictStyle.Residential => 4.2f,
                DistrictStyle.DenseLowRise => 2.0f,
                _ => 9f
            } / Mathf.Sqrt(Mathf.Clamp(densityScale, 0.4f, 1f));
            float R = (float)district.Radius;
            for (float gx = -R; gx <= R; gx += spacing)
            {
                for (float gz = -R; gz <= R; gz += spacing)
                {
                    var p = new Vector2((float)district.Center.X + gx + rng.Range(-0.3f, 0.3f) * spacing,
                                        (float)district.Center.Y + gz + rng.Range(-0.3f, 0.3f) * spacing);
                    float rad = Vector2.Distance(p, district.Center.ToVector2());
                    if (rad > R) continue;
                    TryPlace(geo, district, rng, towers, space, p, 1f - rad / R, spacing * 0.92f);
                }
            }
        }

        private static void RandomFill(CityGeography geo, District district, Pcg32 rng, TowerMeshBuilder towers, SpaceRegistry space, float densityScale)
        {
            int target = Mathf.Max(1, Mathf.RoundToInt(district.Count * densityScale));
            int placed = 0;
            for (int attempts = 0; placed < target && attempts < target * 14; attempts++)
            {
                double ang = rng.NextDouble() * System.Math.PI * 2.0;
                double rad = System.Math.Sqrt(rng.NextDouble()) * district.Radius;
                var p = new Vector2((float)(district.Center.X + System.Math.Cos(ang) * rad), (float)(district.Center.Y + System.Math.Sin(ang) * rad));
                if (TryPlace(geo, district, rng, towers, space, p, 1f - (float)(rad / district.Radius), 99f)) placed++;
            }
        }

        private static bool TryPlace(CityGeography geo, District district, Pcg32 rng, TowerMeshBuilder towers, SpaceRegistry space,
                                     Vector2 p, float centreFactor, float maxSize)
        {
            GetFootprint(district.Style, rng, out float w, out float d);
            w = Mathf.Min(w, maxSize);
            d = Mathf.Min(d, maxSize);
            float footprintRadius = 0.5f * Mathf.Sqrt(w * w + d * d) * 0.85f;
            float margin = district.Style == DistrictStyle.DenseLowRise ? 0.1f : 0.25f;
            if (!space.IsFree(p, footprintRadius, margin)) return false;
            if (!OnLand(geo, p, footprintRadius)) return false;

            space.NearestSegment(p, out Vector2 streetDir);
            float rot = Mathf.Atan2(streetDir.x, streetDir.y) * Mathf.Rad2Deg;
            if (district.Style == DistrictStyle.DenseLowRise) rot += rng.Range(-12f, 12f);

            float hT = Mathf.Pow(rng.NextFloat(), 1.6f);
            float height = Mathf.Lerp((float)district.MinHeight, (float)district.MaxHeight, Mathf.Lerp(hT, Mathf.Max(hT, centreFactor), 0.5f));
            float ground = geo.GroundY(p.x, p.y);

            TowerStyle style = MakeStyle(district.Style, rng, height);
            style.Shopfront = district.Style == DistrictStyle.DenseLowRise || (district.Style == DistrictStyle.Residential && rng.Chance(0.4));
            PlaceBuilding(towers, district.Style, rng, p, ground, w, d, height, rot, style);
            space.AddCircle(p, footprintRadius);
            return true;
        }

        /// <summary>
        /// Continuous street walls: a row of shop-houses (Mumbai style: shops on the ground floor, 2–8 floors
        /// of flats above) along both sides of every street, set back behind the footpath. Inside BKC the
        /// frontage is office blocks instead. This is what makes the streets read as a real city.
        /// </summary>
        public static void StreetFrontage(CityMap map, RoadNetwork roads, TowerMeshBuilder towers, SpaceRegistry space, float densityScale, ChunkedMeshBuilder props = null)
        {
            _props = props;
            _propDensity = densityScale;
            CityGeography geo = map.Geography;
            var rng = new Pcg32(5150UL);
            var placed = new System.Collections.Generic.List<(Vector2 c, float r)>();
            foreach (RoadPath path in roads.Paths)
            {
                if (path.Edge.Elevated) continue;
                bool core = path.LedLighting;
                float jA = roads.JunctionRadius[path.NodeA.Id] + 3.2f;
                float jB = roads.JunctionRadius[path.NodeB.Id] + 3.2f;
                for (int side = -1; side <= 1; side += 2)
                {
                    float s = jA + rng.Range(0f, 0.6f);
                    while (s < path.Length - jB)
                    {
                        float frontage = core ? rng.Range(2.6f, 4.5f) : rng.Range(0.75f, 1.5f);
                        float depth = core ? rng.Range(2.4f, 3.8f) : rng.Range(1.3f, 2.4f);
                        float mid = s + frontage * 0.5f;
                        s += frontage + rng.Range(0.04f, 0.14f);
                        if (mid > path.Length - jB) break;
                        if (rng.Chance(core ? 0.12 : 0.06)) { s += 0.5f; continue; } // gully / driveway gap
                        if (!rng.Chance(Mathf.Clamp(densityScale, 0.5f, 1f))) continue;

                        Vector3 p = path.Sample(mid, out Vector3 fwd);
                        if (p.y > 0.12f) continue; // bridge or ramp
                        Vector3 flat = new Vector3(fwd.x, 0, fwd.z).normalized;
                        Vector3 right = new Vector3(flat.z, 0, -flat.x);
                        float off = path.Width * 0.5f + 0.95f + 0.12f + depth * 0.5f;
                        Vector3 c3 = p + right * side * off;
                        var c = new Vector2(c3.x, c3.z);
                        Vector2 across = new Vector2(right.x, right.z) * side, along = new Vector2(flat.x, flat.z);
                        if (!PlotIsFree(space, c, across, along, depth, frontage)) continue;
                        if (!OnLand(geo, c, Mathf.Max(depth, frontage) * 0.5f)) continue;

                        float h = core ? rng.Range(3f, 9f) : rng.Range(0.5f, 3.2f) * (rng.Chance(0.15) ? 1.8f : 1f);
                        TowerStyle style = core
                            ? MakeStyle(rng.Chance(0.5) ? DistrictStyle.GlassTowers : DistrictStyle.MixedCommercial, rng, h)
                            : MakeStyle(DistrictStyle.Residential, rng, h);
                        style.Shopfront = !core || rng.Chance(0.35);
                        float rotY = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
                        float gy = geo.GroundY(c.x, c.y);
                        towers.AddBox(c, gy, depth, frontage, h, rotY, style);
                        placed.Add((c, Mathf.Max(depth, frontage) * 0.5f));
                        if (_props != null)
                        {
                            if (!core)
                            {
                                StreetBalconies(rng, c - across * (depth * 0.5f), -across, along, gy, frontage, h, style.Frame);
                                FacadeExtras(rng, c - across * (depth * 0.5f), -across, along, gy, frontage, h, style.Shopfront);
                            }
                            RoofClutter(rng, c, gy + h, depth, frontage, rotY, !core);
                        }
                    }
                }
            }
            // Register afterwards so neighbouring plots in a row can touch, but districts and trees avoid them
            foreach (var pl in placed) space.AddCircle(pl.c, pl.r);
            _props = null;
        }

        private const float FloorHeight = 0.36f; // matches CityTower.shader _FloorHeight

        /// <summary>
        /// Real balconies on the street face of shop-houses: a concrete slab with a solid parapet
        /// on every floor above the shop (or thin chhajja sunshades over the windows).
        /// </summary>
        private static void StreetBalconies(Pcg32 rng, Vector2 face, Vector2 outward, Vector2 along, float ground, float frontage, float height, Color frame)
        {
            if (!rng.Chance(0.75 * Mathf.Clamp01(_propDensity))) return;
            int floors = Mathf.FloorToInt(height / FloorHeight);
            if (floors < 2) return;
            MeshBuilder mb = _props.Next();
            var o3 = new Vector3(outward.x, 0, outward.y);
            Quaternion rot = Quaternion.LookRotation(new Vector3(along.x, 0, along.y), Vector3.up);
            bool parapets = rng.Chance(0.6);
            float width = frontage * rng.Range(0.55f, 0.85f);
            Color slab = frame * 0.82f; slab.a = 1f;
            Color wall = rng.Chance(0.5) ? new Color(0.9f, 0.9f, 0.88f) : frame * 1.05f; wall.a = 1f;
            Color rail = new Color(0.16f, 0.17f, 0.18f);
            var basePos = new Vector3(face.x, ground, face.y);
            for (int f = 1; f < floors; f++)
            {
                float y = f * FloorHeight;
                if (parapets)
                {
                    Vector3 c = basePos + o3 * 0.075f + Vector3.up * (y + 0.012f);
                    mb.AddBox(c, rot, new Vector3(0.15f, 0.024f, width), slab);
                    bool railing = rng.Chance(0.35);
                    Vector3 pc = basePos + o3 * 0.145f + Vector3.up * (y + 0.07f);
                    if (railing) mb.AddBox(pc + Vector3.up * 0.04f, rot, new Vector3(0.012f, 0.012f, width), rail);
                    else mb.AddBox(pc, rot, new Vector3(0.012f, 0.1f, width), wall);
                    // Potted plants on the parapet
                    if (rng.Chance(0.3))
                    {
                        Vector3 pot = basePos + o3 * 0.12f + Vector3.up * (y + 0.04f) + new Vector3(along.x, 0, along.y) * rng.Range(-width * 0.35f, width * 0.35f);
                        mb.AddBox(pot, rot, new Vector3(0.035f, 0.035f, 0.035f), new Color(0.62f, 0.3f, 0.18f));
                        mb.AddEllipsoid(pot + Vector3.up * 0.04f, new Vector3(0.035f, 0.03f, 0.035f), 6, 4, new Color(0.2f, 0.42f, 0.15f), 0f);
                    }
                    // Laundry / plants on some balconies
                    if (rng.Chance(0.25))
                        mb.AddBox(basePos + o3 * 0.1f + Vector3.up * (y + 0.06f) + new Vector3(along.x, 0, along.y) * rng.Range(-width * 0.3f, width * 0.3f),
                                  rot, new Vector3(0.01f, 0.06f, 0.12f), VehicleFactory.CarPaints[rng.Range(0, VehicleFactory.CarPaints.Length)]);
                }
                else
                {
                    // Chhajja: thin projecting sunshade over each floor's windows
                    mb.AddBox(basePos + o3 * 0.05f + Vector3.up * (y - 0.06f), rot, new Vector3(0.1f, 0.016f, frontage * 0.92f), slab);
                }
            }
        }

        /// <summary>
        /// The street face of a Mumbai shop-house: window AC units hanging under the windows, a drainage
        /// pipe down one side and a tin / tarpaulin awning over the shops.
        /// </summary>
        private static void FacadeExtras(Pcg32 rng, Vector2 face, Vector2 outward, Vector2 along, float ground, float frontage, float height, bool shop)
        {
            if (_props == null || !rng.Chance(0.85 * Mathf.Clamp01(_propDensity))) return;
            MeshBuilder mb = _props.Next();
            var o3 = new Vector3(outward.x, 0, outward.y);
            var a3 = new Vector3(along.x, 0, along.y);
            Quaternion rot = Quaternion.LookRotation(a3, Vector3.up);
            var basePos = new Vector3(face.x, ground, face.y);
            int floors = Mathf.FloorToInt(height / FloorHeight);

            // Window AC units (outdoor units bolted under the windows)
            for (int f = 1; f < floors; f++)
            {
                int units = rng.Chance(0.45) ? rng.Range(1, 3) : 0;
                for (int k = 0; k < units; k++)
                {
                    Vector3 ac = basePos + o3 * 0.032f + a3 * rng.Range(-frontage * 0.4f, frontage * 0.4f) + Vector3.up * (f * FloorHeight + 0.07f);
                    mb.AddBox(ac, rot, new Vector3(0.05f, 0.055f, 0.085f), new Color(0.86f, 0.86f, 0.83f));
                    mb.AddBox(ac + o3 * 0.026f, rot, new Vector3(0.004f, 0.04f, 0.06f), new Color(0.2f, 0.2f, 0.21f)); // fan grille
                }
            }

            // Drainage pipe down one side
            if (rng.Chance(0.7))
            {
                Vector3 pipe = basePos + o3 * 0.016f + a3 * (frontage * 0.5f - 0.05f) * (rng.Chance(0.5) ? 1f : -1f);
                mb.AddCylinder(pipe, 0.012f, 0.012f, height - 0.02f, 6, rng.Chance(0.5) ? new Color(0.45f, 0.45f, 0.44f) : new Color(0.12f, 0.12f, 0.12f), capTop: false);
            }

            // Awning over the shops (tin, blue tarpaulin or coloured canvas)
            if (shop && rng.Chance(0.6))
            {
                Color[] awn = { new Color(0.6f, 0.62f, 0.64f), new Color(0.12f, 0.32f, 0.72f), new Color(0.15f, 0.5f, 0.3f), new Color(0.75f, 0.2f, 0.15f), new Color(0.9f, 0.7f, 0.2f) };
                Color c = awn[rng.Range(0, awn.Length)];
                float w = frontage * 0.46f;
                Vector3 top = basePos + Vector3.up * 0.45f + o3 * 0.01f;
                Vector3 low = basePos + Vector3.up * 0.4f + o3 * 0.18f;
                Vector3 n = Vector3.Cross(a3, low - top).normalized;
                if (n.y < 0) n = -n;
                mb.AddQuad(top - a3 * w, top + a3 * w, low + a3 * w, low - a3 * w, n, c);
                mb.AddQuad(top - a3 * w, top + a3 * w, low + a3 * w, low - a3 * w, -n, c * 0.7f);
                mb.AddBox(low + Vector3.down * 0.015f, rot, new Vector3(0.006f, 0.03f, w * 2f), c * 0.85f); // valance
            }
        }

        /// <summary>Mumbai roof-scape: black Sintex water tanks, AC units, stair heads, dishes and parapets.</summary>
        private static void RoofClutter(Pcg32 rng, Vector2 c, float roofY, float w, float d, float rotY, bool residential)
        {
            if (_props == null || !rng.Chance(0.85 * Mathf.Clamp01(_propDensity))) return;
            if (w < 0.6f || d < 0.6f) return;
            MeshBuilder mb = _props.Next();
            Quaternion q = Quaternion.Euler(0f, rotY, 0f);
            Vector3 P(float x, float z) => new Vector3(c.x, roofY, c.y) + q * new Vector3(x, 0f, z);
            float hx = w * 0.5f, hz = d * 0.5f;

            // Parapet wall around the roof edge
            Color par = new Color(0.62f, 0.6f, 0.56f);
            const float ph = 0.07f;
            mb.AddBox(P(0, hz - 0.015f) + Vector3.up * ph * 0.5f, q, new Vector3(w, ph, 0.03f), par);
            mb.AddBox(P(0, -hz + 0.015f) + Vector3.up * ph * 0.5f, q, new Vector3(w, ph, 0.03f), par);
            mb.AddBox(P(hx - 0.015f, 0) + Vector3.up * ph * 0.5f, q, new Vector3(0.03f, ph, d), par);
            mb.AddBox(P(-hx + 0.015f, 0) + Vector3.up * ph * 0.5f, q, new Vector3(0.03f, ph, d), par);

            // Stair / lift head
            if (rng.Chance(0.7))
            {
                float sx = Mathf.Min(0.42f, w * 0.35f), sz = Mathf.Min(0.36f, d * 0.35f);
                Vector3 sp = P(rng.Range(-hx + sx, hx - sx) * 0.6f, rng.Range(-hz + sz, hz - sz) * 0.6f);
                mb.AddBox(sp + Vector3.up * 0.16f, q, new Vector3(sx, 0.32f, sz), new Color(0.7f, 0.68f, 0.63f));
            }

            // Sintex tanks (black plastic, Mumbai's signature roof object)
            int tanks = residential ? rng.Range(1, 4) : rng.Range(0, 2);
            for (int i = 0; i < tanks; i++)
            {
                Vector3 tp = P(rng.Range(-hx + 0.15f, hx - 0.15f), rng.Range(-hz + 0.15f, hz - 0.15f));
                mb.AddBox(tp + Vector3.up * 0.03f, q, new Vector3(0.2f, 0.06f, 0.2f), new Color(0.3f, 0.3f, 0.3f)); // steel stand
                Color tc = rng.Chance(0.8) ? new Color(0.06f, 0.06f, 0.065f) : new Color(0.75f, 0.73f, 0.68f);
                mb.AddCylinder(tp + Vector3.up * 0.06f, 0.085f, 0.075f, 0.15f, 10, tc);
            }

            // AC condensers / chillers
            int acs = residential ? rng.Range(0, 3) : rng.Range(2, 6);
            for (int i = 0; i < acs; i++)
            {
                Vector3 ap = P(rng.Range(-hx + 0.1f, hx - 0.1f), rng.Range(-hz + 0.1f, hz - 0.1f));
                Vector3 size = residential ? new Vector3(0.1f, 0.07f, 0.06f) : new Vector3(rng.Range(0.18f, 0.4f), 0.12f, rng.Range(0.15f, 0.3f));
                mb.AddBox(ap + Vector3.up * size.y * 0.5f, q, size, new Color(0.78f, 0.79f, 0.8f));
            }

            // Dish / antenna
            if (rng.Chance(residential ? 0.5 : 0.25))
            {
                Vector3 dp = P(rng.Range(-hx + 0.1f, hx - 0.1f), rng.Range(-hz + 0.1f, hz - 0.1f));
                mb.AddBeam(dp, dp + Vector3.up * rng.Range(0.15f, 0.4f), new Vector2(0.012f, 0.012f), new Color(0.35f, 0.35f, 0.37f));
                mb.AddDisc(dp + Vector3.up * 0.12f + q * new Vector3(0.03f, 0, 0), 0.05f, 10, (q * new Vector3(1f, 0.6f, 0f)).normalized, new Color(0.85f, 0.85f, 0.85f));
            }
        }

        /// <summary>Plot test using its centre and four corners (rows of narrow plots don't fit circle tests).</summary>
        private static bool PlotIsFree(SpaceRegistry space, Vector2 c, Vector2 across, Vector2 along, float depth, float frontage)
        {
            if (space.Clearance(c) < 0.2f) return false;
            for (int i = -1; i <= 1; i += 2)
                for (int j = -1; j <= 1; j += 2)
                    if (space.Clearance(c + across * (i * depth * 0.5f) + along * (j * frontage * 0.5f)) < 0.02f) return false;
            return true;
        }

        private static void GetFootprint(DistrictStyle s, Pcg32 rng, out float w, out float d)
        {
            switch (s)
            {
                case DistrictStyle.GlassTowers: w = rng.Range(3.5f, 6.5f); d = rng.Range(3.5f, 6.5f); break;
                case DistrictStyle.MixedCommercial: w = rng.Range(3f, 6f); d = rng.Range(2.5f, 5f); break;
                case DistrictStyle.Residential: w = rng.Range(2.4f, 4.4f); d = rng.Range(2f, 3.8f); break;
                case DistrictStyle.DenseLowRise: w = rng.Range(0.7f, 1.6f); d = rng.Range(0.7f, 1.5f); break;
                case DistrictStyle.Campus: w = rng.Range(3f, 8f); d = rng.Range(2.5f, 4.5f); break;
                default: w = rng.Range(4f, 8f); d = rng.Range(4f, 8f); break;
            }
        }

        private static TowerStyle MakeStyle(DistrictStyle s, Pcg32 rng, float height)
        {
            var st = new TowerStyle { Seed = rng.NextFloat(), TotalHeight = height };
            switch (s)
            {
                case DistrictStyle.GlassTowers:
                    st.Style = rng.Range(0f, 0.25f);
                    st.Frame = rng.Pick(CorporateFrames);
                    st.Glass = rng.Pick(GlassTints);
                    st.Accent = height > 10f && rng.Chance(0.45);
                    break;
                case DistrictStyle.MixedCommercial:
                    st.Style = rng.Range(0.25f, 0.7f);
                    st.Frame = rng.Pick(CorporateFrames);
                    st.Glass = rng.Pick(GlassTints);
                    st.Accent = height > 8f && rng.Chance(0.25);
                    break;
                case DistrictStyle.Residential:
                    st.Style = rng.Range(0.85f, 1f);
                    st.Frame = rng.Pick(Pastels);
                    st.Glass = new Color(0.12f, 0.13f, 0.14f);
                    break;
                case DistrictStyle.DenseLowRise:
                    st.Style = 1f;
                    st.Frame = rng.Pick(Vivid) * rng.Range(0.75f, 1f);
                    st.Glass = new Color(0.08f, 0.08f, 0.09f);
                    st.TarpRoof = rng.Chance(0.45);
                    break;
                case DistrictStyle.Campus:
                    st.Style = 0.9f;
                    st.Frame = rng.Chance(0.5) ? new Color(0.62f, 0.34f, 0.26f) : new Color(0.86f, 0.8f, 0.68f);
                    st.Glass = new Color(0.1f, 0.12f, 0.12f);
                    break;
                default:
                    st.Style = rng.Range(0f, 0.6f);
                    st.Frame = rng.Pick(CorporateFrames);
                    st.Glass = rng.Pick(GlassTints);
                    st.Accent = height > 18f && rng.Chance(0.5);
                    break;
            }
            // Neon Mumbai 2077: neon tube trims and holographic adverts (only visible in the neon style)
            double neonChance = s == DistrictStyle.GlassTowers ? 0.22 : s == DistrictStyle.MixedCommercial ? 0.2
                : s == DistrictStyle.DenseLowRise ? 0.15 : s == DistrictStyle.Residential ? 0.06
                : s == DistrictStyle.Campus ? 0.0 : 0.15;
            if (rng.Chance(neonChance)) st.NeonHue = rng.Range(1, 6);
            bool commercial = s == DistrictStyle.GlassTowers || s == DistrictStyle.MixedCommercial || s == DistrictStyle.FarSkyline;
            st.HoloAd = commercial && height > 8f && rng.Chance(0.08);

            st.Frame.a = 1f;
            return st;
        }

        private static void PlaceBuilding(TowerMeshBuilder towers, DistrictStyle s, Pcg32 rng, Vector2 p, float ground,
                                          float w, float d, float height, float rot, TowerStyle style)
        {
            bool tall = s == DistrictStyle.GlassTowers || s == DistrictStyle.FarSkyline;
            if (tall && height > 8f && rng.Chance(0.35))
            {
                // Podium + slimmer tower
                float podium = rng.Range(0.9f, 1.4f);
                towers.AddBox(p, ground, w * 1.35f, d * 1.35f, podium, rot, style);
                towers.AddBox(p, ground + podium, w, d, height - podium, rot, style, podium);
                return;
            }

            if (tall && height > 9f && rng.Chance(0.4))
            {
                // Setback crown
                float lower = height * rng.Range(0.62f, 0.8f);
                towers.AddBox(p, ground, w, d, lower, rot, style);
                towers.AddBox(p, ground + lower, w * 0.72f, d * 0.72f, height - lower, rot, style, lower);
                return;
            }

            if (tall && height > 9f && rng.Chance(0.15))
            {
                towers.AddCylinder(p, ground, Mathf.Min(w, d) * 0.55f, height, 20, style);
                return;
            }

            Quaternion q = Quaternion.Euler(0f, rot, 0f);
            Vector2 Off(float x, float z) { Vector3 v = q * new Vector3(x, 0f, z); return p + new Vector2(v.x, v.z); }
            bool residential = s == DistrictStyle.Residential || s == DistrictStyle.DenseLowRise;
            double shape = rng.NextDouble();

            if (tall && height > 10f && shape < 0.18 && w > 4f)
            {
                // Twin towers on a shared podium
                float podium = rng.Range(0.8f, 1.2f);
                towers.AddBox(p, ground, w, d, podium, rot, style);
                float h2 = height * rng.Range(0.82f, 1f);
                towers.AddBox(Off(-w * 0.27f, 0), ground + podium, w * 0.4f, d * 0.8f, height - podium, rot, style, podium);
                towers.AddBox(Off(w * 0.27f, 0), ground + podium, w * 0.4f, d * 0.8f, h2 - podium, rot, style, podium);
                RoofClutter(rng, Off(-w * 0.27f, 0), ground + height, w * 0.4f, d * 0.8f, rot, false);
                return;
            }
            if (tall && height > 8f && shape < 0.36)
            {
                // Stepped "wedding cake" tower
                float h1 = height * rng.Range(0.45f, 0.6f), h2 = height * rng.Range(0.75f, 0.88f);
                towers.AddBox(p, ground, w, d, h1, rot, style);
                towers.AddBox(p, ground + h1, w * 0.8f, d * 0.8f, h2 - h1, rot, style, h1);
                towers.AddBox(p, ground + h2, w * 0.58f, d * 0.58f, height - h2, rot, style, h2);
                RoofClutter(rng, p, ground + height, w * 0.58f, d * 0.58f, rot, false);
                return;
            }
            if (!tall && s != DistrictStyle.DenseLowRise && w > 2.6f && d > 2.2f && shape < 0.3)
            {
                // L-shaped block: main bar plus a wing of a different height
                float hb = height * rng.Range(0.7f, 1.25f);
                towers.AddBox(Off(0, -d * 0.22f), ground, w, d * 0.56f, height, rot, style);
                towers.AddBox(Off(-w * 0.28f, d * 0.22f), ground, w * 0.44f, d * 0.56f, hb, rot, style);
                RoofClutter(rng, Off(0, -d * 0.22f), ground + height, w, d * 0.56f, rot, residential);
                return;
            }
            if (!tall && s != DistrictStyle.DenseLowRise && w > 3f && d > 2.6f && shape < 0.45)
            {
                // U-shaped courtyard block
                towers.AddBox(Off(0, d * 0.32f), ground, w, d * 0.36f, height, rot, style);
                towers.AddBox(Off(-w * 0.35f, -d * 0.18f), ground, w * 0.3f, d * 0.64f, height * rng.Range(0.8f, 1f), rot, style);
                towers.AddBox(Off(w * 0.35f, -d * 0.18f), ground, w * 0.3f, d * 0.64f, height * rng.Range(0.8f, 1f), rot, style);
                RoofClutter(rng, Off(0, d * 0.32f), ground + height, w, d * 0.36f, rot, residential);
                return;
            }

            towers.AddBox(p, ground, w, d, height, rot, style);
            RoofClutter(rng, p, ground + height, w, d, rot, residential);
        }

        private static bool OnLand(CityGeography geo, Vector2 p, float r)
        {
            const float minY = -0.05f;
            if (geo.GroundY(p.x, p.y) < minY) return false;
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f;
                if (geo.GroundY(p.x + Mathf.Cos(a) * r, p.y + Mathf.Sin(a) * r) < minY) return false;
            }
            return true;
        }
    }
}
