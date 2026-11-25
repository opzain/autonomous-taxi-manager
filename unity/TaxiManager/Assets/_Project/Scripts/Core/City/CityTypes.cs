namespace TaxiSim.Core
{
    /// <summary>What a location is used for. Drives demand rules, event rules and visuals.</summary>
    public enum ZoneType
    {
        RailHub,
        Junction,
        Commercial,
        Financial,
        Hospital,
        Event,
        Hotel,
        Government,
        TransitHub,
        Charging,
        Residential,
        Education,
        Airport,
        FleetDepot   // AT Fleet Hub: where the autonomous taxis are stored, charged and launched
    }

    public enum RoadClass
    {
        Local,
        Main,
        Arterial,
        Expressway
    }

    public static class RoadClassInfo
    {
        public static int LanesPerDirection(RoadClass c)
        {
            switch (c)
            {
                case RoadClass.Local: return 1;
                case RoadClass.Main: return 2;
                default: return 3;
            }
        }

        /// <summary>Free-flow speed used for base travel time.</summary>
        public static double SpeedKmh(RoadClass c)
        {
            switch (c)
            {
                case RoadClass.Local: return 22;
                case RoadClass.Main: return 30;
                case RoadClass.Arterial: return 38;
                default: return 55;
            }
        }

        /// <summary>Carriageway width in world units (1 unit = 10 m, slightly exaggerated for readability).</summary>
        public static double WidthUnits(RoadClass c)
        {
            switch (c)
            {
                case RoadClass.Local: return 1.3;
                case RoadClass.Main: return 1.9;
                case RoadClass.Arterial: return 2.5;
                default: return 2.7;
            }
        }
    }

    /// <summary>Visual building style of a procedural district.</summary>
    public enum DistrictStyle
    {
        GlassTowers,
        MixedCommercial,
        Residential,
        DenseLowRise,
        Campus,
        FarSkyline
    }
}
