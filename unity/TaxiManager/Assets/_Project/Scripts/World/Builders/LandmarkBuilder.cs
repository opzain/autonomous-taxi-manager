using TaxiSim.Core;
using UnityEngine;

namespace TaxiSim.World
{
    /// <summary>
    /// Hand-designed, recognisable BKC landmarks (stylised, not exact replicas):
    /// Jio World Centre + Fountain of Joy, NSE Exchange Plaza, One BKC, Trident, US Consulate,
    /// Bharat Diamond Bourse + bullet-train (HSR) hall, MMRDA Grounds festival, Asian Heart Institute,
    /// Bandra and Kurla stations, EV hubs, Mumbai University (Kalina), MTNL telecom tower,
    /// and the BKC → Kurla corridor: Equinox Business Park + ATLAS SkillTech University, Phoenix Marketcity, Kohinoor City,
    /// plus holographic beacons at every dispatch location.
    /// </summary>
    public static class LandmarkBuilder
    {
        /// <summary>Local frame for a landmark: +z points towards its road junction.</summary>
        private struct Frame
        {
            public Vector2 C;
            public float Yaw;
            public float Ground;
            public Vector2 L(float x, float z)
            {
                float r = Yaw * Mathf.Deg2Rad;
                float cs = Mathf.Cos(r), sn = Mathf.Sin(r);
                return C + new Vector2(x * cs + z * sn, -x * sn + z * cs);
            }
            public Vector3 L3(float x, float y, float z) { Vector2 p = L(x, z); return new Vector3(p.x, Ground + y, p.y); }
            public Quaternion Rot => Quaternion.Euler(0f, Yaw, 0f);
        }

        public static void Build(WorldContext ctx)
        {
            CityGraph g = ctx.Map.Graph;
            JioWorldCentre(ctx, g.Node("jio_world"));
            EquinoxAtlas(ctx, g.Node("equinox"));
            PhoenixMarketcity(ctx, g.Node("phoenix"));
            KohinoorCity(ctx, g.Node("kohinoor"));
            NseExchangePlaza(ctx, g.Node("nse"));
            OneBkc(ctx, g.Node("one_bkc"));
            Trident(ctx, g.Node("trident"));
            UsConsulate(ctx, g.Node("us_consulate"));
            DiamondBourse(ctx, g.Node("diamond_bourse"));
            MmrdaGrounds(ctx, g.Node("mmrda"));
            AsianHeart(ctx, g.Node("asian_heart"));
            MtnlTower(ctx, g.Node("west_gate"));
            ChargingHub(ctx, g.Node("ev_hub_jio"), 7f, 6);
            ChargingHub(ctx, g.Node("ev_depot"), 10f, 10);
            FleetHub(ctx, g.Node("fleet_hub"));
            KalinaCampus(ctx, g.Node("kalina"));
            Station(ctx, g.Node("bandra_stn"), new Vector2(-66f, 95f), "Bandra Station", heritage: true);
            Station(ctx, g.Node("kurla_stn"), new Vector2(364.5f, 98f), "Kurla Station", heritage: false);
            NodeBeacons(ctx);
        }

        // ------------------------------------------------------------------ placement helpers

        private static Frame Place(WorldContext ctx, CityNode node, float radius, float preferredAngle)
        {
            Vector2 n = node.Position.XZ.ToVector2();
            float start = ctx.Roads.JunctionRadius.TryGetValue(node.Id, out float jr) ? jr : 2f;
            for (float r = start + radius + 1.2f; r < start + radius + 40f; r += 1.5f)
            {
                for (int i = 0; i < 24; i++)
                {
                    float a = (preferredAngle + (i % 2 == 0 ? 1 : -1) * ((i + 1) / 2) * 15f) * Mathf.Deg2Rad;
                    Vector2 c = n + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                    if (!ctx.Space.IsFree(c, radius, 0.5f)) continue;
                    if (!OnLand(ctx, c, radius)) continue;
                    return MakeFrame(ctx, c, n, radius);
                }
            }
            Debug.LogWarning($"[TaxiSim] No free spot found for landmark at {node.Name}; placing it anyway.");
            return MakeFrame(ctx, n + new Vector2(radius + start + 2f, 0f), n, radius);
        }

        private static Frame MakeFrame(WorldContext ctx, Vector2 c, Vector2 lookAt, float radius)
        {
            Vector2 d = lookAt - c;
            float yaw = Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;
            ctx.Space.AddCircle(c, radius);
            return new Frame { C = c, Yaw = yaw, Ground = ctx.GroundY(c.x, c.y) };
        }

        private static bool OnLand(WorldContext ctx, Vector2 c, float r)
        {
            if (ctx.GroundY(c.x, c.y) < -0.05f) return false;
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                if (ctx.GroundY(c.x + Mathf.Cos(a) * r, c.y + Mathf.Sin(a) * r) < -0.05f) return false;
            }
            return true;
        }

        private static TowerStyle Style(float style, Color frame, Color glass, float height, bool accent, float seed)
        {
            return new TowerStyle { Style = style, Frame = frame, Glass = glass, TotalHeight = height, Accent = accent, Seed = seed };
        }

        private static void AddLabel(WorldContext ctx, string name, string subtitle, Vector3 pos, Color accent, int nodeId = -1)
        {
            ctx.Landmarks.Add(new LandmarkInfo { Name = name, Subtitle = subtitle, Position = pos, Accent = accent, NodeId = nodeId });
        }

        // ------------------------------------------------------------------ landmarks

        private static void JioWorldCentre(WorldContext ctx, CityNode node)
        {
            Frame f = Place(ctx, node, 13f, 250f);
            var white = new Color(0.9f, 0.89f, 0.86f);
            var bronze = new Color(0.22f, 0.18f, 0.12f);
            ctx.Paving.AddDisc(f.L3(0, 0.02f, 0), 12.5f, 40, Vector3.up, Color.white);

            // Convention centre with rounded ends and a floating roof
            ctx.Towers.AddBox(f.L(0, -1), f.Ground, 18f, 9f, 1.4f, f.Yaw, Style(0.12f, white, bronze, 1.4f, true, 0.11f));
            ctx.Towers.AddCylinder(f.L(-9f, -1), f.Ground, 4.5f, 1.4f, 24, Style(0.12f, white, bronze, 1.4f, true, 0.12f));
            ctx.Towers.AddCylinder(f.L(9f, -1), f.Ground, 4.5f, 1.4f, 24, Style(0.12f, white, bronze, 1.4f, true, 0.13f));
            ctx.Concrete.AddBox(f.L3(0, 1.62f, -1), f.Rot, new Vector3(21f, 0.22f, 10.5f), white);
            ctx.Lights.AddBox(f.L3(0, 1.5f, -1 + 5.3f), f.Rot, new Vector3(20f, 0.05f, 0.05f), new Color(1f, 0.85f, 0.6f));

            // Jio World Towers (offices) behind the centre
            ctx.Towers.AddBox(f.L(-5.5f, -8.8f), f.Ground, 4f, 4f, 11f, f.Yaw, Style(0.05f, new Color(0.7f, 0.72f, 0.75f), new Color(0.1f, 0.2f, 0.3f), 11f, true, 0.21f));
            ctx.Towers.AddBox(f.L(5.5f, -8.8f), f.Ground, 4f, 4f, 12.5f, f.Yaw, Style(0.05f, new Color(0.7f, 0.72f, 0.75f), new Color(0.1f, 0.2f, 0.3f), 12.5f, true, 0.22f));

            // Fountain of Joy: pool, rim, underwater lights, water jets
            Vector3 fountain = f.L3(0, 0f, 8.5f);
            ctx.Concrete.AddCylinder(fountain, 2.9f, 2.9f, 0.18f, 36, new Color(0.85f, 0.84f, 0.8f), capTop: false);
            ctx.Concrete.AddCylinder(fountain, 2.6f, 2.6f, 0.18f, 36, new Color(0.85f, 0.84f, 0.8f), capTop: false);
            ctx.NightLights.AddCylinder(fountain + Vector3.up * 0.1f, 2.55f, 2.55f, 0.03f, 36, new Color(0.3f, 0.8f, 1f), capTop: false);
            var water = new MeshBuilder();
            water.AddDisc(Vector3.zero, 2.6f, 36, Vector3.up, Color.white);
            ctx.Spawn("FountainOfJoy_Water", water.ToMesh("FountainWater"), ctx.Mats.Fountain, fountain + Vector3.up * 0.12f, Quaternion.identity, null, false);
            Effects.Fountain(ctx, fountain + Vector3.up * 0.12f, 2.2f);

            AddLabel(ctx, "Jio World Centre", "Convention centre · Fountain of Joy", f.L3(0, 4f, 0), new Color(1f, 0.8f, 0.4f), node.Id);
        }

        /// <summary>Equinox Business Park (four glass towers on a podium) with ATLAS SkillTech University in Tower 1.</summary>
        private static void EquinoxAtlas(WorldContext ctx, CityNode node)
        {
            Frame f = Place(ctx, node, 14.5f, 225f);
            var frame = new Color(0.74f, 0.76f, 0.79f);
            var glass = new Color(0.08f, 0.2f, 0.27f);
            const float podium = 0.9f;
            ctx.Paving.AddDisc(f.L3(0, 0.02f, 0), 12.2f, 40, Vector3.up, Color.white);
            ctx.Towers.AddBox(f.L(0, -3f), f.Ground, 19f, 15f, podium, f.Yaw, Style(0.35f, frame, glass, podium, false, 0.61f));

            float[] heights = { 7.5f, 8.5f, 7f, 8f };
            Vector2[] spots = { new Vector2(-5.2f, 1.2f), new Vector2(5.2f, 1.2f), new Vector2(-5.2f, -7.2f), new Vector2(5.2f, -7.2f) };
            for (int i = 0; i < 4; i++)
            {
                float h = heights[i];
                bool atlas = i == 0;
                TowerStyle ts = Style(0.08f, frame, glass, podium + h, atlas, 0.62f + i * 0.01f);
                ts.NeonHue = atlas ? 3 : 2;      // amber trims on ATLAS, cyan on the other Equinox towers
                ts.HoloAd = i == 1 || i == 3;
                ctx.Towers.AddBox(f.L(spots[i].x, spots[i].y), f.Ground + podium, 5f, 4.6f, h, f.Yaw, ts, podium);
            }

            // ATLAS SkillTech University: vertical LED fin on Tower 1, campus lawn and entrance canopy
            Vector3 fin = f.L3(-5.2f, podium + 4.2f, 1.2f + 2.33f);
            ctx.Lights.AddBox(fin, f.Rot, new Vector3(0.18f, 6.2f, 0.05f), new Color(1f, 0.55f, 0.15f));
            ctx.Grass.AddQuad(f.L3(-11f, 0.03f, 4.5f), f.L3(-1f, 0.03f, 4.5f), f.L3(-1f, 0.03f, 10f), f.L3(-11f, 0.03f, 10f), Vector3.up, Color.white);
            ctx.Concrete.AddBox(f.L3(-5.2f, 1.2f, 4.3f), f.Rot, new Vector3(4f, 0.08f, 1.6f), new Color(0.9f, 0.9f, 0.92f));
            ctx.Metal.AddBox(f.L3(-6.9f, 0.6f, 4.9f), f.Rot, new Vector3(0.08f, 1.2f, 0.08f), new Color(0.7f, 0.7f, 0.72f));
            ctx.Metal.AddBox(f.L3(-3.5f, 0.6f, 4.9f), f.Rot, new Vector3(0.08f, 1.2f, 0.08f), new Color(0.7f, 0.7f, 0.72f));
            ctx.NightLights.AddBox(f.L3(-5.2f, 1.14f, 4.3f), f.Rot, new Vector3(3.6f, 0.02f, 1.3f), new Color(1f, 0.85f, 0.6f));

            AddLabel(ctx, "ATLAS SkillTech University", "Equinox Tower 1 · student demand", f.L3(-5.2f, podium + 10f, 1.2f), new Color(1f, 0.6f, 0.2f), node.Id);
            AddLabel(ctx, "Equinox Business Park", "Offices · LBS Marg, Kurla West", f.L3(5.2f, podium + 11f, -3f), new Color(0.45f, 0.85f, 1f));
        }

        private static void PhoenixMarketcity(WorldContext ctx, CityNode node)
        {
            Frame f = Place(ctx, node, 17f, 150f);
            var warm = new Color(0.88f, 0.85f, 0.8f);
            var bronze = new Color(0.22f, 0.17f, 0.1f);
            ctx.Paving.AddQuad(f.L3(-13f, 0.02f, -9f), f.L3(13f, 0.02f, -9f), f.L3(13f, 0.02f, 11f), f.L3(-13f, 0.02f, 11f), Vector3.up, Color.white);
            TowerStyle mall = Style(0.2f, warm, bronze, 2.7f, true, 0.71f);
            mall.NeonHue = 1;
            ctx.Towers.AddBox(f.L(0, -1f), f.Ground, 24f, 13f, 1.8f, f.Yaw, mall);
            ctx.Towers.AddBox(f.L(0, -2f), f.Ground + 1.8f, 19f, 9f, 0.9f, f.Yaw, Style(0.25f, warm, bronze, 2.7f, true, 0.72f), 1.8f);
            ctx.Glass.AddEllipsoid(f.L3(0, 2.7f, -2f), new Vector3(3.2f, 1.4f, 3.2f), 16, 8, Color.white, 1f, hemisphere: true);
            // Giant LED facade facing LBS Marg
            ctx.Lights.AddBox(f.L3(0, 1.05f, 5.53f), f.Rot, new Vector3(9f, 1.3f, 0.05f), new Color(1f, 0.3f, 0.7f));
            ctx.Lights.AddBox(f.L3(-8f, 1.05f, 5.53f), f.Rot, new Vector3(3.5f, 0.4f, 0.05f), new Color(0.3f, 0.9f, 1f));
            ctx.Lights.AddBox(f.L3(8f, 1.05f, 5.53f), f.Rot, new Vector3(3.5f, 0.4f, 0.05f), new Color(1f, 0.85f, 0.3f));
            // Multi-level car park behind the mall
            ctx.Towers.AddBox(f.L(-8.5f, -10.5f), f.Ground, 7f, 6f, 1.3f, f.Yaw, Style(1f, new Color(0.6f, 0.6f, 0.58f), new Color(0.05f, 0.05f, 0.06f), 1.3f, false, 0.73f));
            AddLabel(ctx, "Phoenix Marketcity", "Mall · LBS Marg, Kurla", f.L3(0, 6f, 0), new Color(1f, 0.4f, 0.75f), node.Id);
        }

        private static void KohinoorCity(WorldContext ctx, CityNode node)
        {
            Frame f = Place(ctx, node, 10f, 10f);
            var pastel = new Color(0.9f, 0.84f, 0.74f);
            var glass = new Color(0.1f, 0.16f, 0.22f);
            ctx.Towers.AddBox(f.L(0, 3f), f.Ground, 14f, 7f, 1.3f, f.Yaw, Style(0.3f, new Color(0.8f, 0.8f, 0.82f), glass, 1.3f, true, 0.81f));
            float[] heights = { 9f, 10.5f, 8f };
            for (int i = 0; i < 3; i++)
            {
                Vector2 p = f.L(-5f + i * 5f, -4.5f);
                ctx.Towers.AddBox(p, f.Ground, 3.6f, 3.6f, heights[i], f.Yaw + 8f * (i - 1), Style(0.85f, pastel, glass, heights[i], false, 0.82f + i * 0.01f));
            }
            AddLabel(ctx, "Kohinoor City", "Mall · residences · Kurla West", f.L3(0, 12.5f, -4.5f), new Color(1f, 0.85f, 0.5f), node.Id);
        }

        private static void NseExchangePlaza(WorldContext ctx, CityNode node)
        {
            Frame f = Place(ctx, node, 6f, 120f);
            var frame = new Color(0.78f, 0.77f, 0.73f);
            var glass = new Color(0.1f, 0.17f, 0.28f);
            TowerStyle nse = Style(0.3f, frame, glass, 14.4f, true, 0.31f);
            nse.HoloAd = true;   // live market ticker hologram on the NSE tower
            nse.NeonHue = 5;
            ctx.Towers.AddBox(f.C, f.Ground, 6f, 6f, 13f, f.Yaw, nse);
            ctx.Towers.AddBox(f.C, f.Ground + 13f, 4f, 4f, 1.4f, f.Yaw, Style(0.1f, frame, glass, 14.4f, true, 0.32f), 13f);
            // Stock-ticker LED ring around the podium
            for (int i = 0; i < 4; i++)
            {
                Quaternion r = f.Rot * Quaternion.Euler(0, i * 90f, 0);
                Vector3 pos = new Vector3(f.C.x, f.Ground + 0.95f, f.C.y) + r * new Vector3(0, 0, 3.04f);
                ctx.Lights.AddBox(pos, r, new Vector3(6f, 0.28f, 0.04f), i % 2 == 0 ? new Color(0.2f, 1f, 0.45f) : new Color(1f, 0.25f, 0.2f));
            }
            AddLabel(ctx, "NSE Exchange Plaza", "National Stock Exchange", new Vector3(f.C.x, f.Ground + 16f, f.C.y), new Color(0.4f, 1f, 0.6f), node.Id);
        }

        private static void OneBkc(WorldContext ctx, CityNode node)
        {
            Frame f = Place(ctx, node, 9f, 60f);
            var frame = new Color(0.62f, 0.65f, 0.68f);
            var glass = new Color(0.08f, 0.24f, 0.28f);
            ctx.Towers.AddBox(f.L(-5f, -1f), f.Ground, 4.4f, 4.4f, 11f, f.Yaw + 12f, Style(0.04f, frame, glass, 11f, false, 0.41f));
            ctx.Towers.AddBox(f.L(0f, -3f), f.Ground, 4.6f, 4.6f, 12.5f, f.Yaw, Style(0.04f, frame, glass, 12.5f, true, 0.42f));
            ctx.Towers.AddBox(f.L(5f, -1f), f.Ground, 4.4f, 4.4f, 10.5f, f.Yaw - 12f, Style(0.04f, frame, glass, 10.5f, false, 0.43f));
            ctx.Glass.AddBeam(f.L3(-3f, 7.6f, -1.8f), f.L3(3f, 7.6f, -1.8f), new Vector2(0.9f, 0.5f), Color.white);
            AddLabel(ctx, "One BKC", "Glass office towers", f.L3(0, 15f, -3f), new Color(0.4f, 0.9f, 1f), node.Id);
        }

        private static void Trident(WorldContext ctx, CityNode node)
        {
            Frame f = Place(ctx, node, 9f, 30f);
            var cream = new Color(0.87f, 0.81f, 0.7f);
            var bronze = new Color(0.2f, 0.16f, 0.1f);
            for (int i = 0; i < 8; i++)
            {
                float a = Mathf.Lerp(-60f, 60f, i / 7f) * Mathf.Deg2Rad;
                Vector2 p = f.L(Mathf.Sin(a) * 6f, -Mathf.Cos(a) * 6f + 3f);
                ctx.Towers.AddBox(p, f.Ground, 2.5f, 1.7f, 6.8f, f.Yaw + a * Mathf.Rad2Deg, Style(0.55f, cream, bronze, 6.8f, false, 0.5f + i * 0.01f));
            }
            ctx.Towers.AddBox(f.L(-6.5f, 5f), f.Ground, 4f, 3.5f, 5.2f, f.Yaw, Style(0.45f, new Color(0.8f, 0.78f, 0.74f), bronze, 5.2f, false, 0.6f));
            AddLabel(ctx, "Trident & Sofitel BKC", "Hotels", f.L3(0, 9f, 0), new Color(1f, 0.75f, 0.5f), node.Id);
        }

        private static void UsConsulate(WorldContext ctx, CityNode node)
        {
            Frame f = Place(ctx, node, 10.5f, 330f);
            var wall = new Color(0.8f, 0.78f, 0.72f);
            float hw = 9f, hd = 6.5f;
            ctx.Concrete.AddBox(f.L3(0, 0.18f, -hd), f.Rot, new Vector3(hw * 2, 0.36f, 0.15f), wall);
            ctx.Concrete.AddBox(f.L3(0, 0.18f, hd), f.Rot, new Vector3(hw * 2, 0.36f, 0.15f), wall);
            ctx.Concrete.AddBox(f.L3(-hw, 0.18f, 0), f.Rot, new Vector3(0.15f, 0.36f, hd * 2), wall);
            ctx.Concrete.AddBox(f.L3(hw, 0.18f, 0), f.Rot, new Vector3(0.15f, 0.36f, hd * 2), wall);
            ctx.Grass.AddQuad(f.L3(-hw, 0.02f, -hd), f.L3(hw, 0.02f, -hd), f.L3(hw, 0.02f, hd), f.L3(-hw, 0.02f, hd), Vector3.up, Color.white);
            ctx.Towers.AddBox(f.L(0, -1.5f), f.Ground, 9f, 5.5f, 2.6f, f.Yaw, Style(0.75f, new Color(0.86f, 0.84f, 0.8f), new Color(0.1f, 0.13f, 0.16f), 2.6f, false, 0.7f));
            ctx.Towers.AddBox(f.L(5.5f, 3.5f), f.Ground, 4.5f, 3.5f, 1.8f, f.Yaw, Style(0.8f, new Color(0.84f, 0.82f, 0.78f), new Color(0.1f, 0.13f, 0.16f), 1.8f, false, 0.71f));
            ctx.Metal.AddCylinder(f.L3(-4f, 0, 4f), 0.05f, 0.03f, 3.2f, 8, new Color(0.8f, 0.8f, 0.82f));
            ctx.Lights.AddBox(f.L3(-3.6f, 2.95f, 4f), f.Rot, new Vector3(0.7f, 0.42f, 0.02f), new Color(0.9f, 0.9f, 0.95f));
            AddLabel(ctx, "US Consulate", "G-Block East", f.L3(0, 4.5f, 0), new Color(0.6f, 0.75f, 1f), node.Id);
        }

        private static void DiamondBourse(WorldContext ctx, CityNode node)
        {
            Frame f = Place(ctx, node, 15f, 200f);
            var frame = new Color(0.8f, 0.8f, 0.82f);
            var glass = new Color(0.12f, 0.18f, 0.24f);
            ctx.Towers.AddBox(f.C, f.Ground, 26f, 12f, 1f, f.Yaw, Style(0.5f, frame, glass, 1f, false, 0.8f));
            int i = 0;
            for (int row = -1; row <= 1; row += 2)
            {
                for (int col = 0; col < 4; col++)
                {
                    Vector2 p = f.L(-9f + col * 6f, row * 2.9f);
                    ctx.Towers.AddBox(p, f.Ground + 1f, 4f, 3.6f, 7.5f, f.Yaw, Style(0.35f, frame, glass, 8.5f, i % 3 == 0, 0.81f + i * 0.01f), 1f);
                    i++;
                }
            }
            AddLabel(ctx, "Bharat Diamond Bourse", "8 towers · world's largest diamond exchange", f.L3(0, 11f, 0), new Color(0.75f, 0.9f, 1f), node.Id);

            // Mumbai–Ahmedabad bullet train (HSR) station hall
            Frame h = Place(ctx, node, 12f, 300f);
            ctx.Towers.AddBox(h.C, h.Ground, 22f, 6f, 1.1f, h.Yaw + 90f, Style(0.1f, new Color(0.85f, 0.86f, 0.88f), new Color(0.1f, 0.18f, 0.25f), 1.1f, true, 0.9f));
            AddArcRoof(ctx.Metal, h, 23f, 7f, 1.6f, 1.1f, new Color(0.78f, 0.8f, 0.83f));
            ctx.Lights.AddBox(h.L3(0, 1.05f, 3.05f), h.Rot, new Vector3(18f, 0.05f, 0.04f), new Color(0.4f, 0.8f, 1f));
            AddLabel(ctx, "BKC Bullet Train Station", "Mumbai–Ahmedabad HSR", h.L3(0, 4f, 0), new Color(0.9f, 0.5f, 1f));
        }

        private static void AddArcRoof(MeshBuilder mb, Frame f, float length, float width, float rise, float baseY, Color color)
        {
            const int segs = 10;
            for (int s = 0; s < segs; s++)
            {
                float a0 = Mathf.PI * s / segs, a1 = Mathf.PI * (s + 1) / segs;
                float x0 = Mathf.Cos(a0) * width * 0.5f, y0 = Mathf.Sin(a0) * rise;
                float x1 = Mathf.Cos(a1) * width * 0.5f, y1 = Mathf.Sin(a1) * rise;
                // Roof runs along the frame's local x axis; the arc spans local z.
                Vector3 p00 = f.L3(-length * 0.5f, baseY + y0, x0), p01 = f.L3(length * 0.5f, baseY + y0, x0);
                Vector3 p10 = f.L3(-length * 0.5f, baseY + y1, x1), p11 = f.L3(length * 0.5f, baseY + y1, x1);
                float am = (a0 + a1) * 0.5f;
                Vector3 n = f.Rot * new Vector3(0, Mathf.Sin(am), Mathf.Cos(am) * rise / (width * 0.5f)).normalized;
                mb.AddQuad(p00, p01, p11, p10, n, color);
            }
        }

        private static void MmrdaGrounds(WorldContext ctx, CityNode node)
        {
            Frame f = Place(ctx, node, 16f, 160f);
            float hw = 15f, hd = 10f;
            ctx.Paving.AddQuad(f.L3(-hw - 1, 0.015f, -hd - 1), f.L3(hw + 1, 0.015f, -hd - 1), f.L3(hw + 1, 0.015f, hd + 1), f.L3(-hw - 1, 0.015f, hd + 1), Vector3.up, Color.white);
            ctx.Grass.AddQuad(f.L3(-hw, 0.025f, -hd), f.L3(hw, 0.025f, -hd), f.L3(hw, 0.025f, hd), f.L3(-hw, 0.025f, hd), Vector3.up, Color.white);

            // Festival: stage, LED wall, truss, tents, string lights
            ctx.Concrete.AddBox(f.L3(0, 0.45f, -7.5f), f.Rot, new Vector3(7f, 0.9f, 3f), new Color(0.2f, 0.2f, 0.22f));
            ctx.Lights.AddBox(f.L3(0, 2.1f, -8.9f), f.Rot, new Vector3(6.4f, 2.4f, 0.08f), new Color(0.9f, 0.35f, 1f));
            ctx.Metal.AddBeam(f.L3(-3.6f, 0, -8.9f), f.L3(-3.6f, 3.6f, -8.9f), new Vector2(0.12f, 0.12f), new Color(0.6f, 0.6f, 0.62f));
            ctx.Metal.AddBeam(f.L3(3.6f, 0, -8.9f), f.L3(3.6f, 3.6f, -8.9f), new Vector2(0.12f, 0.12f), new Color(0.6f, 0.6f, 0.62f));
            ctx.Metal.AddBeam(f.L3(-3.6f, 3.6f, -8.9f), f.L3(3.6f, 3.6f, -8.9f), new Vector2(0.12f, 0.12f), new Color(0.6f, 0.6f, 0.62f));
            Color[] tents = { new Color(0.95f, 0.45f, 0.1f), new Color(0.95f, 0.85f, 0.2f), new Color(0.85f, 0.15f, 0.2f), new Color(0.2f, 0.6f, 0.9f), new Color(0.95f, 0.95f, 0.92f), new Color(0.4f, 0.75f, 0.3f) };
            for (int i = 0; i < 6; i++)
            {
                Vector3 p = f.L3(-11f + i * 4.4f, 0.02f, 6.5f);
                ctx.Concrete.AddCylinder(p, 1.5f, 1.5f, 0.6f, 6, tents[i], capTop: false);
                ctx.Concrete.AddCylinder(p + Vector3.up * 0.6f, 1.6f, 0f, 1.1f, 6, tents[i], capTop: false);
            }
            Color[] bulbs = { new Color(1f, 0.6f, 0.2f), new Color(1f, 0.25f, 0.4f), new Color(0.3f, 0.9f, 1f), new Color(1f, 0.95f, 0.5f) };
            for (int line = 0; line < 3; line++)
            {
                Vector3 a = f.L3(-hw, 1.8f, -4f + line * 4.5f), b = f.L3(hw, 1.8f, -4f + line * 4.5f);
                for (int k = 0; k <= 40; k++)
                {
                    float t = k / 40f;
                    Vector3 p = Vector3.Lerp(a, b, t) + Vector3.down * Mathf.Sin(t * Mathf.PI * 5f) * 0.25f;
                    ctx.NightLights.AddBox(p, Quaternion.identity, Vector3.one * 0.09f, bulbs[(k + line) % bulbs.Length]);
                }
                ctx.Metal.AddBeam(a + Vector3.down * 1.8f, a, new Vector2(0.06f, 0.06f), new Color(0.4f, 0.4f, 0.42f));
                ctx.Metal.AddBeam(b + Vector3.down * 1.8f, b, new Vector2(0.06f, 0.06f), new Color(0.4f, 0.4f, 0.42f));
            }
            Effects.Sparkles(ctx, f.L3(0, 2.5f, -7f), 5f);
            AddLabel(ctx, "MMRDA Grounds", "Festivals · concerts · events", f.L3(0, 4.5f, 0), new Color(1f, 0.5f, 0.9f), node.Id);
        }

        private static void AsianHeart(WorldContext ctx, CityNode node)
        {
            Frame f = Place(ctx, node, 8f, 200f);
            var white = new Color(0.93f, 0.93f, 0.91f);
            var teal = new Color(0.08f, 0.22f, 0.24f);
            ctx.Towers.AddBox(f.L(0, -1f), f.Ground, 10f, 6f, 4.2f, f.Yaw, Style(0.6f, white, teal, 4.2f, false, 0.95f));
            ctx.Towers.AddBox(f.L(-4f, 4f), f.Ground, 5f, 4f, 2.8f, f.Yaw, Style(0.7f, white, teal, 2.8f, false, 0.96f));
            Vector3 sign = f.L3(0, 3.4f, 2.04f);
            ctx.Lights.AddBox(sign, f.Rot, new Vector3(1.0f, 0.3f, 0.05f), new Color(1f, 0.1f, 0.1f));
            ctx.Lights.AddBox(sign, f.Rot, new Vector3(0.3f, 1.0f, 0.05f), new Color(1f, 0.1f, 0.1f));
            Vector3 pad = f.L3(1.5f, 4.22f, -1f);
            ctx.Paving.AddDisc(pad, 2f, 24, Vector3.up, Color.white);
            ctx.NightLights.AddCylinder(pad, 2.02f, 2.02f, 0.04f, 24, new Color(0.3f, 1f, 0.4f), capTop: false);
            ctx.Lights.AddBox(pad + Vector3.up * 0.02f, f.Rot, new Vector3(0.2f, 0.01f, 1.4f), Color.white);
            ctx.Lights.AddBox(pad + Vector3.up * 0.02f + f.Rot * new Vector3(0.6f, 0, 0), f.Rot, new Vector3(0.2f, 0.01f, 1.4f), Color.white);
            ctx.Lights.AddBox(pad + Vector3.up * 0.02f + f.Rot * new Vector3(0.3f, 0, 0), f.Rot, new Vector3(0.6f, 0.01f, 0.2f), Color.white);
            AddLabel(ctx, "Asian Heart Institute", "Hospital · urgent requests", f.L3(0, 6.5f, 0), new Color(1f, 0.35f, 0.35f), node.Id);
        }

        private static void MtnlTower(WorldContext ctx, CityNode node)
        {
            Frame f = Place(ctx, node, 4.5f, 300f);
            ctx.Towers.AddBox(f.C, f.Ground, 4f, 4f, 8.5f, f.Yaw, Style(0.8f, new Color(0.7f, 0.66f, 0.6f), new Color(0.1f, 0.12f, 0.14f), 8.5f, false, 0.33f));
            Vector3 top = new Vector3(f.C.x, f.Ground + 8.5f, f.C.y);
            ctx.Metal.AddCylinder(top, 0.35f, 0.08f, 6.5f, 8, new Color(0.75f, 0.2f, 0.15f));
            for (int i = 0; i < 3; i++)
            {
                Quaternion r = Quaternion.Euler(0, i * 120f + f.Yaw, 0);
                ctx.Concrete.AddEllipsoid(top + Vector3.up * (2.5f + i * 0.9f) + r * new Vector3(0, 0, 0.45f), new Vector3(0.35f, 0.35f, 0.12f), 10, 6, new Color(0.9f, 0.9f, 0.9f), 1f);
            }
            var warn = new MeshBuilder();
            warn.AddBox(Vector3.zero, Vector3.one * 0.18f, Color.white);
            ctx.Spawn("MTNL_AviationLight", warn.ToMesh("Warning"), ctx.Mats.Warning, top + Vector3.up * 6.55f, Quaternion.identity, null, false);
            AddLabel(ctx, "MTNL Telecom Tower", "BKC West Gate", top + Vector3.up * 8f, new Color(1f, 0.6f, 0.4f), node.Id);
        }

        private static void ChargingHub(WorldContext ctx, CityNode node, float radius, int bays)
        {
            Frame f = Place(ctx, node, radius, 270f);
            float len = radius * 1.5f, dep = radius * 0.8f;
            ctx.Paving.AddQuad(f.L3(-len * 0.5f, 0.02f, -dep * 0.5f), f.L3(len * 0.5f, 0.02f, -dep * 0.5f), f.L3(len * 0.5f, 0.02f, dep * 0.5f), f.L3(-len * 0.5f, 0.02f, dep * 0.5f), Vector3.up, Color.white);
            // Solar canopy on poles
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0 ? -1 : 1) * (len * 0.5f - 0.6f), z = (i < 2 ? -1 : 1) * (dep * 0.5f - 0.6f);
                ctx.Metal.AddBox(f.L3(x, 0.55f, z), f.Rot, new Vector3(0.12f, 1.1f, 0.12f), new Color(0.75f, 0.76f, 0.78f));
            }
            ctx.Concrete.AddBox(f.L3(0, 1.15f, 0), f.Rot, new Vector3(len, 0.08f, dep), new Color(0.85f, 0.86f, 0.88f));
            ctx.Glass.AddBox(f.L3(0, 1.21f, 0), f.Rot * Quaternion.Euler(-4f, 0, 0), new Vector3(len - 0.3f, 0.03f, dep - 0.3f), Color.white);
            ctx.NightLights.AddBox(f.L3(0, 1.1f, 0), f.Rot, new Vector3(len - 1f, 0.02f, 0.15f), new Color(0.75f, 0.95f, 1f));
            // Charger pillars with green status strips + parking bays
            for (int b = 0; b < bays; b++)
            {
                float x = Mathf.Lerp(-len * 0.5f + 1f, len * 0.5f - 1f, bays == 1 ? 0.5f : b / (bays - 1f));
                ctx.Concrete.AddBox(f.L3(x, 0.22f, -dep * 0.5f + 0.5f), f.Rot, new Vector3(0.25f, 0.44f, 0.18f), new Color(0.92f, 0.93f, 0.95f));
                ctx.Lights.AddBox(f.L3(x, 0.3f, -dep * 0.5f + 0.6f), f.Rot, new Vector3(0.16f, 0.16f, 0.02f), new Color(0.2f, 1f, 0.45f));
                ctx.Lights.AddBox(f.L3(x + 0.55f, 0.025f, 0), f.Rot, new Vector3(0.03f, 0.005f, dep - 1.5f), new Color(0.6f, 0.9f, 0.7f));
                var bay = new GameObject($"ChargingBay_{node.Key}_{b}").transform;
                bay.SetParent(ctx.Root, false);
                bay.SetPositionAndRotation(f.L3(x, 0.03f, 0.2f), f.Rot);
                ctx.ChargingBays.Add(bay);
            }
            AddLabel(ctx, node.Name, $"EV charging · {node.ChargerSlots} fast chargers", f.L3(0, 4f, 0), new Color(0.3f, 1f, 0.5f), node.Id);
        }

        /// <summary>
        /// AT Fleet Hub: the depot every autonomous taxi launches from. A sawtooth-roof warehouse with
        /// glowing skylights and LED-framed roller-door bays, an epoxy apron (FleetHubFloor shader:
        /// animated launch lane, pulsing charge rings), 12 charging bays with parked taxis, a control
        /// office, floodlight masts, a perimeter fence and a gantry over the gate. A holographic guide
        /// line leads from the gate to the road junction.
        /// </summary>
        private static void FleetHub(WorldContext ctx, CityNode node)
        {
            Frame f = Place(ctx, node, 9f, 270f);
            var root = new GameObject("AT Fleet Hub").transform;
            root.SetParent(ctx.Root, false);
            var led = new MeshBuilder();
            var cladding = new Color(0.27f, 0.29f, 0.32f);
            var roofSteel = new Color(0.62f, 0.64f, 0.66f);
            var white = new Color(0.92f, 0.93f, 0.95f);
            const float hw = 7f, wz0 = -7.5f, wz1 = -0.5f, wallH = 1.6f, gateZ = 4.5f;

            // ---- Apron (custom shader) ----
            var floor = new MeshBuilder();
            const int cells = 8;
            for (int i = 0; i < cells; i++)
            {
                for (int j = 0; j < cells; j++)
                {
                    float x0 = Mathf.Lerp(-hw, hw, i / (float)cells), x1 = Mathf.Lerp(-hw, hw, (i + 1) / (float)cells);
                    float z0 = Mathf.Lerp(wz1, gateZ, j / (float)cells), z1 = Mathf.Lerp(wz1, gateZ, (j + 1) / (float)cells);
                    floor.AddQuad(f.L3(x0, 0.025f, z0), f.L3(x1, 0.025f, z0), f.L3(x1, 0.025f, z1), f.L3(x0, 0.025f, z1), Vector3.up, Color.white,
                                  new Vector2(x0, z0 - wz1), new Vector2(x1, z0 - wz1), new Vector2(x1, z1 - wz1), new Vector2(x0, z1 - wz1));
                }
            }
            GameObject floorGo = ctx.Spawn("FleetHub_Apron", floor.ToMesh("FleetHubApron"), ctx.Mats.HubFloor, Vector3.zero, Quaternion.identity, root, false);
            floorGo.GetComponent<MeshRenderer>().receiveShadows = true;
            Material hubMat = ctx.Mats.HubFloor;
            if (hubMat.HasProperty("_LaneHalfWidth"))
            {
                hubMat.SetFloat("_LaneHalfWidth", 0.8f);
                hubMat.SetFloat("_BayWidth", 0.55f);
                hubMat.SetVector("_BayRow", new Vector4(1.4f, 2.4f, 0, 0));
                hubMat.SetFloat("_ApronHalfWidth", 0.8f + 0.6f + 6 * 0.55f);
                hubMat.SetFloat("_GateZ", gateZ - wz1);
            }

            // ---- Warehouse shell ----
            ctx.Concrete.AddBox(f.L3(0, wallH * 0.5f, (wz0 + wz1) * 0.5f), f.Rot, new Vector3(hw * 2f, wallH, wz1 - wz0), cladding);
            // Vertical cladding ribs on the long walls
            for (float x = -hw + 0.25f; x < hw; x += 0.5f)
            {
                ctx.Metal.AddBox(f.L3(x, wallH * 0.5f, wz1 + 0.012f), f.Rot, new Vector3(0.05f, wallH, 0.02f), cladding * 1.25f);
                ctx.Metal.AddBox(f.L3(x, wallH * 0.5f, wz0 - 0.012f), f.Rot, new Vector3(0.05f, wallH, 0.02f), cladding * 1.25f);
            }
            // Sawtooth roof: sloped steel + north-light glazing that glows at night
            const int teeth = 7;
            float toothW = hw * 2f / teeth, rise = 0.55f;
            for (int i = 0; i < teeth; i++)
            {
                float x0 = -hw + i * toothW, x1 = x0 + toothW;
                Vector3 a0 = f.L3(x0, wallH, wz0), a1 = f.L3(x0, wallH, wz1);
                Vector3 b0 = f.L3(x1, wallH + rise, wz0), b1 = f.L3(x1, wallH + rise, wz1);
                Vector3 c0 = f.L3(x1, wallH, wz0), c1 = f.L3(x1, wallH, wz1);
                Vector3 slopeN = Vector3.Cross(b0 - a0, a1 - a0).normalized;
                if (slopeN.y < 0f) slopeN = -slopeN;
                ctx.Metal.AddQuad(a0, a1, b1, b0, slopeN, roofSteel);
                Vector3 glassN = f.Rot * Vector3.right; // north-light glazing on the steep side of each tooth
                ctx.Glass.AddQuad(c0, c1, b1, b0, glassN, Color.white);
                ctx.NightLights.AddQuad(c0 - glassN * 0.03f, c1 - glassN * 0.03f, b1 - glassN * 0.03f + Vector3.down * 0.05f, b0 - glassN * 0.03f + Vector3.down * 0.05f,
                                        glassN, new Color(0.75f, 0.92f, 1f));
                // Gable triangles at both ends
                foreach (float z in new[] { wz0, wz1 })
                {
                    Vector3 n = f.Rot * (z == wz0 ? Vector3.back : Vector3.forward);
                    int ia = ctx.Concrete.AddVertex(f.L3(x0, wallH, z), n, Vector2.zero, cladding);
                    int ib = ctx.Concrete.AddVertex(f.L3(x1, wallH, z), n, Vector2.right, cladding);
                    int ic = ctx.Concrete.AddVertex(f.L3(x1, wallH + rise, z), n, Vector2.one, cladding);
                    ctx.Concrete.AddOrientedTriangle(ia, ib, ic, n);
                }
            }
            // LED eave strip along the front
            led.AddBox(f.L3(0, wallH - 0.04f, wz1 + 0.03f), f.Rot, new Vector3(hw * 2f, 0.035f, 0.02f), Color.white);

            // Roller-door bays with LED portals
            const int doors = 5;
            for (int d = 0; d < doors; d++)
            {
                float x = Mathf.Lerp(-hw + 1.4f, hw - 1.4f, d / (doors - 1f));
                ctx.Concrete.AddBox(f.L3(x, 0.4f, wz1 + 0.012f), f.Rot, new Vector3(1.3f, 0.8f, 0.02f), new Color(0.03f, 0.035f, 0.04f)); // open bay (dark interior)
                ctx.NightLights.AddBox(f.L3(x, 0.78f, wz1 - 0.02f), f.Rot, new Vector3(1.1f, 0.02f, 0.02f), new Color(0.85f, 0.95f, 1f));  // interior lights
                ctx.Metal.AddBox(f.L3(x, 0.88f, wz1 + 0.06f), f.Rot, new Vector3(1.36f, 0.12f, 0.1f), roofSteel * 0.8f);                // rolled shutter drum
                led.AddBox(f.L3(x, 0.82f, wz1 + 0.03f), f.Rot, new Vector3(1.42f, 0.03f, 0.02f), Color.white);
                led.AddBox(f.L3(x - 0.7f, 0.41f, wz1 + 0.03f), f.Rot, new Vector3(0.03f, 0.82f, 0.02f), Color.white);
                led.AddBox(f.L3(x + 0.7f, 0.41f, wz1 + 0.03f), f.Rot, new Vector3(0.03f, 0.82f, 0.02f), Color.white);
                ctx.Paving.AddQuad(f.L3(x - 0.65f, 0.03f, wz1), f.L3(x + 0.65f, 0.03f, wz1), f.L3(x + 0.65f, 0.03f, wz1 + 0.25f), f.L3(x - 0.65f, 0.03f, wz1 + 0.25f), Vector3.up, new Color(0.9f, 0.8f, 0.2f));
            }

            // ---- Charging bays (12) with parked fleet taxis ----
            const float bayW = 0.55f, rowZ0 = 1.4f, rowZ1 = 2.4f;
            for (int side = -1; side <= 1; side += 2)
            {
                for (int k = 0; k < 6; k++)
                {
                    float x = side * (0.8f + 0.6f + (k + 0.5f) * bayW);
                    float zBack = wz1 + rowZ0 - 0.12f, zMid = wz1 + (rowZ0 + rowZ1) * 0.5f;
                    ctx.Concrete.AddBox(f.L3(x, 0.12f, zBack), f.Rot, new Vector3(0.1f, 0.24f, 0.07f), white);
                    ctx.Lights.AddBox(f.L3(x, 0.17f, zBack + 0.036f), f.Rot, new Vector3(0.06f, 0.07f, 0.005f), new Color(0.2f, 1f, 0.6f));
                    ctx.Metal.AddBeam(f.L3(x + 0.04f, 0.2f, zBack + 0.03f), f.L3(x + 0.08f, 0.04f, zBack + 0.3f), new Vector2(0.01f, 0.01f), new Color(0.1f, 0.1f, 0.1f)); // cable
                    var bay = new GameObject($"ChargingBay_fleet_hub_{(side < 0 ? k : 6 + k)}").transform;
                    bay.SetParent(root, false);
                    bay.SetPositionAndRotation(f.L3(x, 0.03f, zMid), f.Rot * Quaternion.Euler(0, 180f, 0));
                    ctx.ChargingBays.Add(bay);
                    if ((k + (side > 0 ? 1 : 0)) % 3 != 2)
                    {
                        GameObject taxi = VehicleFactory.Spawn(ctx.Mats, VehicleKind.FleetEv, Color.white, root, $"Parked_AT_{side}_{k}");
                        taxi.transform.SetPositionAndRotation(f.L3(x, 0.03f, zMid + 0.05f), f.Rot * Quaternion.Euler(0, 180f, 0));
                    }
                }
            }

            // ---- Control office (glass) with LED crown ----
            ctx.Towers.AddBox(f.L(5.8f, 3.0f), f.Ground, 1.6f, 1.4f, 2.4f, f.Yaw,
                              Style(0.1f, new Color(0.2f, 0.22f, 0.25f), new Color(0.08f, 0.2f, 0.26f), 2.4f, true, 0.71f));
            ctx.Metal.AddBeam(f.L3(5.8f, 2.4f, 3.0f), f.L3(5.8f, 3.3f, 3.0f), new Vector2(0.03f, 0.03f), roofSteel);
            led.AddBox(f.L3(5.8f, 2.38f, 3.0f), f.Rot, new Vector3(1.64f, 0.04f, 1.44f), Color.white);

            // ---- Floodlight masts ----
            foreach (Vector2 m in new[] { new Vector2(-6.7f, 4.2f), new Vector2(6.7f, 4.2f), new Vector2(-6.7f, 0.2f), new Vector2(6.7f, 0.2f) })
            {
                ctx.Metal.AddCylinder(f.L3(m.x, 0f, m.y), 0.05f, 0.035f, 2.6f, 8, roofSteel, false);
                ctx.Metal.AddBox(f.L3(m.x, 2.6f, m.y), f.Rot, new Vector3(0.4f, 0.04f, 0.06f), roofSteel);
                for (int i = -1; i <= 1; i += 2)
                    ctx.NightLights.AddBox(f.L3(m.x + i * 0.13f, 2.57f, m.y - Mathf.Sign(m.y - 2f) * 0.04f), f.Rot, new Vector3(0.11f, 0.05f, 0.04f), Color.white);
            }

            // ---- Perimeter fence with a gate gap ----
            var fence = new Color(0.45f, 0.47f, 0.5f);
            void FenceRun(Vector2 a, Vector2 b)
            {
                float len = Vector2.Distance(a, b);
                int posts = Mathf.Max(1, Mathf.RoundToInt(len / 0.5f));
                for (int i = 0; i <= posts; i++)
                {
                    Vector2 p = Vector2.Lerp(a, b, i / (float)posts);
                    ctx.Metal.AddBeam(f.L3(p.x, 0f, p.y), f.L3(p.x, 0.3f, p.y), new Vector2(0.015f, 0.015f), fence);
                }
                ctx.Metal.AddBeam(f.L3(a.x, 0.28f, a.y), f.L3(b.x, 0.28f, b.y), new Vector2(0.012f, 0.012f), fence);
                ctx.Metal.AddBeam(f.L3(a.x, 0.14f, a.y), f.L3(b.x, 0.14f, b.y), new Vector2(0.012f, 0.012f), fence);
            }
            FenceRun(new Vector2(-hw, wz1), new Vector2(-hw, gateZ));
            FenceRun(new Vector2(hw, wz1), new Vector2(hw, gateZ));
            FenceRun(new Vector2(-hw, gateZ), new Vector2(-1.2f, gateZ));
            FenceRun(new Vector2(1.2f, gateZ), new Vector2(hw, gateZ));

            // ---- Gate gantry with the depot sign and boom barriers ----
            foreach (float x in new[] { -1.15f, 1.15f })
                ctx.Metal.AddBox(f.L3(x, 0.5f, gateZ), f.Rot, new Vector3(0.08f, 1.0f, 0.08f), white);
            ctx.Concrete.AddBox(f.L3(0, 1.08f, gateZ), f.Rot, new Vector3(2.5f, 0.34f, 0.1f), new Color(0.06f, 0.07f, 0.08f));
            led.AddBox(f.L3(0, 0.9f, gateZ + 0.055f), f.Rot, new Vector3(2.5f, 0.02f, 0.01f), Color.white);
            led.AddBox(f.L3(0, 1.26f, gateZ + 0.055f), f.Rot, new Vector3(2.5f, 0.02f, 0.01f), Color.white);
            ctx.Concrete.AddBox(f.L3(-0.55f, 0.22f, gateZ + 0.15f), f.Rot, new Vector3(0.9f, 0.025f, 0.025f), new Color(0.9f, 0.15f, 0.12f));
            ctx.Concrete.AddBox(f.L3(0.55f, 0.22f, gateZ - 0.15f), f.Rot, new Vector3(0.9f, 0.025f, 0.025f), new Color(0.9f, 0.15f, 0.12f));
            AddSignText(root, "AT FLEET HUB", f.L3(0, 1.08f, gateZ + 0.056f), f.Rot * Quaternion.Euler(0f, 180f, 0f), new Color(0.35f, 0.95f, 1f));
            AddSignText(root, "AUTONOMOUS TAXI DEPOT", f.L3(0, 1.08f, gateZ - 0.056f), f.Rot, new Color(0.35f, 0.95f, 1f), 0.6f);

            ctx.Spawn("FleetHub_LED", led.ToMesh("FleetHubLed"), ctx.Mats.FleetLed, Vector3.zero, Quaternion.identity, root, false);

            // ---- Access road with a holographic guide line to the junction ----
            Vector2 gate2 = f.L(0f, gateZ + 0.1f);
            Vector2 node2 = node.Position.XZ.ToVector2();
            float jr = ctx.Roads.JunctionRadius.TryGetValue(node.Id, out float r) ? r : 1.5f;
            Vector2 toNode = node2 - gate2;
            float dist = toNode.magnitude;
            if (dist > jr + 0.5f)
            {
                Vector2 end = gate2 + toNode / dist * (dist - jr * 0.8f);
                Vector3 g3 = new Vector3(gate2.x, ctx.GroundY(gate2.x, gate2.y) + 0.03f, gate2.y);
                Vector3 e3 = new Vector3(end.x, ctx.GroundY(end.x, end.y) + 0.03f, end.y);
                Vector3 side = Vector3.Cross(Vector3.up, (e3 - g3).normalized) * 0.85f;
                ctx.Paving.AddQuad(g3 - side, g3 + side, e3 + side, e3 - side, Vector3.up, new Color(0.42f, 0.42f, 0.44f));
                ctx.Space.AddSegment(gate2, end, 1.1f);
                var guide = new MeshBuilder();
                guide.AddRibbon(new[] { g3, e3 }, 0.22f, Color.white, 0.02f);
                ctx.Spawn("FleetHub_GuideLine", guide.ToMesh("FleetHubGuide"), ctx.Mats.RouteCyan, Vector3.zero, Quaternion.identity, root, false);
            }

            AddLabel(ctx, "AT Fleet Hub", "Autonomous taxi depot · 12 chargers · fleet launch", f.L3(0, 4.5f, -2f), new Color(0.3f, 0.95f, 1f), node.Id);
        }

        /// <summary>3D sign text (built-in font, unlit, readable day and night).</summary>
        private static void AddSignText(Transform parent, string text, Vector3 pos, Quaternion rot, Color color, float scale = 1f)
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) return;
            var go = new GameObject($"Sign_{text}");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, rot);
            var tm = go.AddComponent<TextMesh>();
            tm.font = font;
            tm.text = text;
            tm.fontSize = 100;
            tm.characterSize = 0.022f * scale;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }

        private static void KalinaCampus(WorldContext ctx, CityNode node)
        {
            Frame f = Place(ctx, node, 13f, 90f);
            var brick = new Color(0.66f, 0.36f, 0.27f);
            var stone = new Color(0.88f, 0.83f, 0.7f);
            ctx.Grass.AddQuad(f.L3(-12, 0.02f, -2), f.L3(12, 0.02f, -2), f.L3(12, 0.02f, 9), f.L3(-12, 0.02f, 9), Vector3.up, Color.white);
            ctx.Towers.AddBox(f.L(0, -6f), f.Ground, 14f, 5f, 1.9f, f.Yaw, Style(0.9f, stone, new Color(0.1f, 0.12f, 0.12f), 1.9f, false, 0.15f));
            ctx.Towers.AddCylinder(f.L(0, -6f), f.Ground + 1.9f, 1.8f, 0.8f, 20, Style(0.9f, stone, new Color(0.1f, 0.12f, 0.12f), 2.7f, false, 0.16f), 1.9f);
            ctx.Concrete.AddEllipsoid(f.L3(0, 2.7f, -6f), new Vector3(1.8f, 1.7f, 1.8f), 18, 10, new Color(0.9f, 0.9f, 0.88f), 1f, hemisphere: true);
            ctx.Towers.AddBox(f.L(-9f, 4f), f.Ground, 5f, 5f, 1.6f, f.Yaw, Style(0.9f, brick, new Color(0.1f, 0.12f, 0.12f), 1.6f, false, 0.17f));
            ctx.Towers.AddBox(f.L(9f, 4f), f.Ground, 5f, 4f, 1.4f, f.Yaw, Style(0.9f, brick, new Color(0.1f, 0.12f, 0.12f), 1.4f, false, 0.18f));
            AddLabel(ctx, "Mumbai University (Kalina)", "Campus · student demand", f.L3(0, 6f, -4f), new Color(1f, 0.85f, 0.5f), node.Id);
        }

        private static void Station(WorldContext ctx, CityNode node, Vector2 at, string name, bool heritage)
        {
            float ground = ctx.GroundY(at.x, at.y);
            ctx.Space.AddCircle(at, 7f);
            if (heritage)
            {
                var cream = new Color(0.9f, 0.82f, 0.64f);
                var tile = new Color(0.56f, 0.25f, 0.17f);
                ctx.Towers.AddBox(at, ground, 10f, 4f, 1.6f, 0f, Style(0.95f, cream, new Color(0.12f, 0.1f, 0.08f), 1.6f, false, 0.05f));
                ctx.Concrete.AddGableRoof(new Vector3(at.x, ground + 1.6f, at.y), Quaternion.Euler(0, 90f, 0), 4.3f, 10.4f, 1.1f, tile);
                Vector2 towerPos = at + new Vector2(0.5f, 0f);
                ctx.Towers.AddBox(towerPos, ground, 1.5f, 1.5f, 3.6f, 0f, Style(0.95f, cream, new Color(0.12f, 0.1f, 0.08f), 3.6f, false, 0.06f));
                ctx.Concrete.AddCylinder(new Vector3(towerPos.x, ground + 3.6f, towerPos.y), 0.8f, 0.05f, 1.4f, 4, tile, false);
                ctx.Lights.AddDisc(new Vector3(towerPos.x - 0.77f, ground + 3.0f, towerPos.y), 0.35f, 16, Vector3.left, new Color(1f, 0.95f, 0.8f));
            }
            else
            {
                ctx.Towers.AddBox(at, ground, 3.5f, 12f, 1.5f, 0f, Style(0.7f, new Color(0.82f, 0.8f, 0.74f), new Color(0.1f, 0.12f, 0.14f), 1.5f, false, 0.07f));
            }
            AddLabel(ctx, name, heritage ? "Western Railway · heritage station" : "Central Railway · LBS Marg", new Vector3(at.x, ground + 6f, at.y), new Color(1f, 0.9f, 0.5f), node.Id);
        }

        // ------------------------------------------------------------------ beacons

        private static void NodeBeacons(WorldContext ctx)
        {
            var root = new GameObject("Beacons").transform;
            root.SetParent(ctx.Root, false);
            foreach (CityNode node in ctx.Map.Graph.Nodes)
            {
                float r = ctx.Roads.JunctionRadius.TryGetValue(node.Id, out float jr) ? jr : 1.5f;
                Material m;
                float h = 0.35f;
                switch (node.Zone)
                {
                    case ZoneType.Charging: m = ctx.Mats.HoloGreen; h = 0.8f; break;
                    case ZoneType.FleetDepot: m = ctx.Mats.HoloCyan; h = 0.9f; break;
                    case ZoneType.Hospital: m = ctx.Mats.HoloRed; h = 0.6f; break;
                    case ZoneType.RailHub:
                    case ZoneType.TransitHub:
                    case ZoneType.Airport: m = ctx.Mats.HoloGold; h = 0.5f; break;
                    default: m = ctx.Mats.HoloCyan; break;
                }
                var ring = new MeshBuilder();
                ring.AddCylinder(Vector3.zero, r + 0.25f, r + 0.25f, h, 40, Color.white, capTop: false);
                Vector3 pos = node.Position.XZ.ToVector3(RoadNetworkBuilder.JunctionY + 0.01f);
                ctx.Spawn($"Beacon_{node.Key}", ring.ToMesh("BeaconRing"), m, pos, Quaternion.identity, root, false);

                if (node.Zone == ZoneType.Charging || node.Zone == ZoneType.Hospital || node.Zone == ZoneType.FleetDepot)
                {
                    var column = new MeshBuilder();
                    column.AddCylinder(Vector3.zero, 0.5f, 0.5f, 7f, 16, Color.white, capTop: false);
                    ctx.Spawn($"BeaconColumn_{node.Key}", column.ToMesh("BeaconColumn"), m, pos, Quaternion.identity, root, false);
                }
            }
        }
    }
}
