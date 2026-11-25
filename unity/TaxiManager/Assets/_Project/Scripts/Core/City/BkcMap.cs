namespace TaxiSim.Core
{
    public sealed class CityMap
    {
        public string Name { get; }
        public CityGraph Graph { get; }
        public CityGeography Geography { get; }

        public CityMap(string name, CityGraph graph, CityGeography geography)
        {
            Name = name;
            Graph = graph;
            Geography = geography;
        }
    }

    /// <summary>
    /// Stylised, simplified map of Bandra Kurla Complex (BKC), Mumbai and its surroundings.
    /// The layout is inspired by the real area (G-Block business district, Mithi River, Kurla and
    /// Bandra stations, the BKC–Chunabhatti connector and SCLR flyovers, and the corridor from G-Block East
    /// through Equinox Business Park / ATLAS SkillTech University along LBS Marg to Kurla Station) but is NOT to scale.
    /// Coordinates: x = east, z = north, 1 unit = 10 m.
    /// </summary>
    public static class BkcMap
    {
        public static CityMap Create()
        {
            var g = new CityGraph();

            // ---- Nodes (23): 10–15+ locations required by the spec ----
            g.AddNode("bandra_stn", "Bandra Station (East)", "Bandra Stn", ZoneType.RailHub, -55, 95);
            g.AddNode("kalanagar", "Kalanagar Junction", "Kalanagar", ZoneType.Junction, -12, 158);
            g.AddNode("west_gate", "BKC West Gate / MTNL", "West Gate", ZoneType.Commercial, 52, 140);
            g.AddNode("asian_heart", "Asian Heart Institute", "Asian Heart", ZoneType.Hospital, 72, 100);
            g.AddNode("mmrda", "MMRDA Grounds", "MMRDA Grounds", ZoneType.Event, 118, 78);
            g.AddNode("nse", "NSE Exchange Plaza", "NSE", ZoneType.Financial, 112, 162);
            g.AddNode("one_bkc", "One BKC Towers", "One BKC", ZoneType.Financial, 165, 178);
            g.AddNode("jio_world", "Jio World Centre", "Jio World", ZoneType.Commercial, 182, 128);
            g.AddNode("trident", "Trident & Sofitel BKC", "Trident", ZoneType.Hotel, 228, 168);
            g.AddNode("us_consulate", "US Consulate / G-Block East", "US Consulate", ZoneType.Government, 258, 134);
            g.AddNode("diamond_bourse", "Bharat Diamond Bourse + HSR Station", "Diamond Bourse", ZoneType.TransitHub, 160, 40);
            g.AddNode("ev_hub_jio", "EV Hub – Jio World Drive", "EV Hub A", ZoneType.Charging, 208, 92, chargerSlots: 2);
            g.AddNode("ev_depot", "EV Depot – BKC Bus Depot", "EV Depot B", ZoneType.Charging, 268, 66, chargerSlots: 3);
            g.AddNode("kurla_stn", "Kurla Station (LBS Marg)", "Kurla Stn", ZoneType.RailHub, 348, 98);
            g.AddNode("chunabhatti", "Chunabhatti / Eastern Freeway", "Chunabhatti", ZoneType.TransitHub, 400, 0);
            g.AddNode("dharavi", "Dharavi T-Junction", "Dharavi", ZoneType.Residential, 78, -38);
            g.AddNode("kalina", "Kalina – Mumbai University", "Kalina Univ", ZoneType.Education, 215, 258);
            g.AddNode("vakola", "Vakola / Santacruz East", "Vakola", ZoneType.Residential, 92, 268);
            g.AddNode("sclr_airport", "SCLR → CSMIA Airport", "Airport Link", ZoneType.Airport, 335, 285);

            // ---- BKC → Kurla corridor (v0.3): Equinox / ATLAS SkillTech University, LBS Marg, Kurla West ----
            g.AddNode("equinox", "Equinox Business Park / ATLAS SkillTech University", "Equinox / ATLAS", ZoneType.Education, 300, 120);
            g.AddNode("phoenix", "Phoenix Marketcity (LBS Marg)", "Phoenix Mall", ZoneType.Commercial, 330, 175);
            g.AddNode("kohinoor", "Kohinoor City / Kurla West", "Kohinoor City", ZoneType.Commercial, 326, 52);

            // ---- AT Fleet Hub (v0.7): depot / warehouse where the autonomous taxis board, charge and launch ----
            g.AddNode("fleet_hub", "AT Fleet Hub (Autonomous Taxi Depot)", "AT Fleet Hub", ZoneType.FleetDepot, 235, 18, chargerSlots: 12);

            // ---- Roads (38) ----
            g.AddRoad("bandra_stn", "kalanagar", "Western Express Hwy (South)", RoadClass.Arterial);
            g.AddRoad("kalanagar", "west_gate", "BKC Road – Kalanagar Bridge", RoadClass.Arterial);
            g.AddRoad("kalanagar", "vakola", "Western Express Highway", RoadClass.Expressway);
            g.AddRoad("bandra_stn", "dharavi", "Mahim–Dharavi Link (Mithi Bridge)", RoadClass.Arterial);
            g.AddRoad("west_gate", "asian_heart", "Avenue 1", RoadClass.Main);
            g.AddRoad("west_gate", "nse", "G-Block Road", RoadClass.Main);
            g.AddRoad("asian_heart", "mmrda", "Avenue 2", RoadClass.Main);
            g.AddRoad("asian_heart", "dharavi", "Dharavi Approach Road", RoadClass.Main);
            g.AddRoad("mmrda", "nse", "MMRDA Road", RoadClass.Main);
            g.AddRoad("mmrda", "jio_world", "Avenue 3", RoadClass.Main);
            g.AddRoad("mmrda", "diamond_bourse", "Diamond Bourse Road", RoadClass.Main);
            g.AddRoad("nse", "one_bkc", "G-Block North", RoadClass.Main);
            g.AddRoad("nse", "jio_world", "Bandra Kurla Complex Road", RoadClass.Main);
            g.AddRoad("one_bkc", "jio_world", "One BKC Link", RoadClass.Main);
            g.AddRoad("one_bkc", "trident", "C-61 Road", RoadClass.Main);
            g.AddRoad("jio_world", "trident", "Jio World Drive", RoadClass.Main);
            g.AddRoad("jio_world", "ev_hub_jio", "Jio Service Lane", RoadClass.Local);
            g.AddRoad("trident", "us_consulate", "Consulate Road", RoadClass.Main);
            g.AddRoad("us_consulate", "ev_hub_jio", "G-Block East Link", RoadClass.Main);
            g.AddRoad("us_consulate", "equinox", "BKC–Kurla Connector Road", RoadClass.Arterial);
            g.AddRoad("equinox", "kurla_stn", "Kurla Station Road (West)", RoadClass.Main);
            g.AddRoad("phoenix", "equinox", "LBS Marg (North)", RoadClass.Arterial);
            g.AddRoad("equinox", "kohinoor", "LBS Marg (South)", RoadClass.Arterial);
            g.AddRoad("kohinoor", "kurla_stn", "Kurla West Station Approach", RoadClass.Main);
            g.AddRoad("kohinoor", "ev_depot", "Kurla Depot Link", RoadClass.Main);
            g.AddRoad("phoenix", "kurla_stn", "Phoenix Marketcity Road", RoadClass.Main);
            g.AddRoad("fleet_hub", "ev_depot", "Fleet Hub Road (East)", RoadClass.Main);
            g.AddRoad("fleet_hub", "diamond_bourse", "Fleet Hub Road (West)", RoadClass.Main);
            g.AddRoad("ev_hub_jio", "ev_depot", "Depot Road", RoadClass.Main);
            g.AddRoad("diamond_bourse", "ev_hub_jio", "Bourse Link", RoadClass.Main);
            g.AddRoad("diamond_bourse", "ev_depot", "BKC South Road", RoadClass.Main);
            g.AddRoad("diamond_bourse", "dharavi", "Sion–Bandra Link Road", RoadClass.Arterial);
            g.AddRoad("ev_depot", "chunabhatti", "BKC–Chunabhatti Connector (Flyover)", RoadClass.Expressway, elevated: true);
            g.AddRoad("kurla_stn", "chunabhatti", "LBS–SCLR Flyover", RoadClass.Arterial, elevated: true);
            g.AddRoad("one_bkc", "kalina", "CST Road – Mithi Bridge", RoadClass.Arterial);
            g.AddRoad("kalina", "vakola", "Kalina–Vakola Road", RoadClass.Arterial);
            g.AddRoad("kalina", "sclr_airport", "SCLR Elevated (West)", RoadClass.Expressway, elevated: true);
            g.AddRoad("kurla_stn", "sclr_airport", "SCLR Double-Decker", RoadClass.Expressway, elevated: true);

            var geo = new CityGeography();

            // Mithi River: flows from the airport side (NE) around BKC and out to Mahim Creek (SW).
            AddPoints(geo.River,
                420, 300, 340, 250, 260, 214, 190, 206, 120, 204, 70, 192, 38, 165,
                28, 120, 20, 70, 8, 25, -20, -10, -70, -50, -150, -90, -230, -120);

            AddPoints(geo.WesternRailway, -75, 660, -75, 300, -73, 95, -78, -120, -90, -520);
            AddPoints(geo.CentralRailway, 372, 660, 372, 300, 372, 98, 377, -120, 388, -520);
            AddPoints(geo.SeaLink, -238, 320, -236, 120, -232, -120, -226, -330);

            geo.MetroNodeKeys.AddRange(new[] { "vakola", "kalanagar", "west_gate", "nse", "jio_world", "us_consulate", "equinox", "kurla_stn" });

            // Procedural districts: name, centre, radius, style, count, height range (units), seed.
            geo.Districts.Add(new District("G-Block CBD", 178, 150, 62, DistrictStyle.GlassTowers, 46, 5.5, 15, 101));
            geo.Districts.Add(new District("BKC E-Block", 232, 92, 48, DistrictStyle.MixedCommercial, 30, 3.5, 10, 102));
            geo.Districts.Add(new District("BKC South", 110, 30, 40, DistrictStyle.MixedCommercial, 18, 2.5, 7, 103));
            // v0.6 infill so no open land is left between the BKC roads
            geo.Districts.Add(new District("BKC Central", 150, 100, 32, DistrictStyle.MixedCommercial, 0, 3.5, 9, 120));
            geo.Districts.Add(new District("G-Block West", 80, 150, 26, DistrictStyle.GlassTowers, 0, 5, 12, 121));
            geo.Districts.Add(new District("G-Block East", 232, 145, 22, DistrictStyle.GlassTowers, 0, 5, 13, 122));
            geo.Districts.Add(new District("Bandra East Colony", 42, 65, 26, DistrictStyle.Residential, 0, 2, 6, 123));
            geo.Districts.Add(new District("Kalina East", 292, 238, 24, DistrictStyle.Residential, 0, 2, 6, 124));
            geo.Districts.Add(new District("BKC South-East", 215, 55, 26, DistrictStyle.MixedCommercial, 0, 3, 8, 125));
            // BKC → Kurla corridor: continuous city from G-Block East through Equinox to Kurla Station
            geo.Districts.Add(new District("BKC–Kurla Connector", 282, 150, 26, DistrictStyle.MixedCommercial, 22, 3.5, 9, 115));
            geo.Districts.Add(new District("Equinox / LBS Business Strip", 305, 95, 34, DistrictStyle.GlassTowers, 26, 4, 11, 116));
            geo.Districts.Add(new District("Kurla West (Kapadia Nagar)", 345, 140, 26, DistrictStyle.Residential, 40, 2.5, 7.5, 104));
            geo.Districts.Add(new District("Kurla West South", 330, 25, 34, DistrictStyle.Residential, 42, 2.5, 8, 117));
            geo.Districts.Add(new District("Kurla Station Market", 357, 122, 12, DistrictStyle.DenseLowRise, 70, 0.5, 1.4, 118));
            geo.Districts.Add(new District("Phoenix / LBS North", 318, 205, 22, DistrictStyle.MixedCommercial, 18, 3, 8, 119));
            geo.Districts.Add(new District("Kurla East", 420, 120, 50, DistrictStyle.Residential, 40, 2.5, 8, 105));
            geo.Districts.Add(new District("Chunabhatti", 405, -45, 50, DistrictStyle.Residential, 36, 3, 9, 106));
            geo.Districts.Add(new District("Dharavi", 55, -62, 72, DistrictStyle.DenseLowRise, 300, 0.4, 1.5, 107));
            geo.Districts.Add(new District("Kalanagar / Bandra East", -22, 128, 48, DistrictStyle.Residential, 40, 2, 6.5, 108));
            geo.Districts.Add(new District("Bandra West", -120, 70, 55, DistrictStyle.Residential, 55, 3, 9.5, 109));
            geo.Districts.Add(new District("Vakola / Santacruz", 85, 300, 62, DistrictStyle.Residential, 58, 2, 7, 110));
            geo.Districts.Add(new District("Kalina Campus", 255, 290, 46, DistrictStyle.Campus, 22, 0.8, 2.2, 111));
            geo.Districts.Add(new District("Lower Parel / Worli Skyline", -90, -300, 125, DistrictStyle.FarSkyline, 34, 12, 32, 112));
            geo.Districts.Add(new District("Andheri / Powai Skyline", 200, 520, 150, DistrictStyle.FarSkyline, 28, 8, 22, 113));
            geo.Districts.Add(new District("Ghatkopar Skyline", 580, 110, 95, DistrictStyle.FarSkyline, 22, 6, 16, 114));

            return new CityMap("Bandra Kurla Complex, Mumbai", g, geo);
        }

        private static void AddPoints(System.Collections.Generic.List<Vec2> list, params double[] xz)
        {
            for (int i = 0; i + 1 < xz.Length; i += 2) list.Add(new Vec2(xz[i], xz[i + 1]));
        }
    }
}
