using System;
using System.Collections.Generic;

namespace TaxiSim.Core
{
    /// <summary>A procedural building district (visual only, but kept here so the city is data-driven).</summary>
    public sealed class District
    {
        public string Name { get; }
        public Vec2 Center { get; }
        public double Radius { get; }
        public DistrictStyle Style { get; }
        public int Count { get; }
        public double MinHeight { get; }
        public double MaxHeight { get; }
        public ulong Seed { get; }

        public District(string name, double x, double z, double radius, DistrictStyle style, int count, double minHeight, double maxHeight, ulong seed)
        {
            Name = name;
            Center = new Vec2(x, z);
            Radius = radius;
            Style = style;
            Count = count;
            MinHeight = minHeight;
            MaxHeight = maxHeight;
            Seed = seed;
        }
    }

    /// <summary>
    /// Terrain and landscape of the map: the Mithi River, the Arabian Sea coast, rail corridors,
    /// the metro line and building districts. All heights are in world units (1 unit = 10 m),
    /// with y = 0 as street level.
    /// </summary>
    public sealed class CityGeography
    {
        public const double WaterLevel = -0.35;
        public const double BankWidth = 3.5;

        // Map bounds (multiples of 32 units so the terrain LOD rings line up exactly).
        public double MinX { get; set; } = -424;
        public double MaxX { get; set; } = 728;
        public double MinZ { get; set; } = -520;
        public double MaxZ { get; set; } = 664;

        public List<Vec2> River { get; } = new List<Vec2>();
        public double RiverHalfWidthUpstream { get; set; } = 4.5;
        public double RiverHalfWidthMouth { get; set; } = 9.0;

        /// <summary>Mean x of the Arabian Sea coastline (west of Bandra).</summary>
        public double CoastBaseX { get; set; } = -150;

        public List<Vec2> WesternRailway { get; } = new List<Vec2>();
        public List<Vec2> CentralRailway { get; } = new List<Vec2>();
        /// <summary>Node keys the Metro Line 2B viaduct follows.</summary>
        public List<string> MetroNodeKeys { get; } = new List<string>();
        public List<Vec2> SeaLink { get; } = new List<Vec2>();
        public List<District> Districts { get; } = new List<District>();

        private double _riverLength = -1;

        public double CoastX(double z) => CoastBaseX + 10.0 * Math.Sin(z * 0.013) + 6.0 * Math.Sin(z * 0.041 + 1.3);

        /// <summary>Distance (xz) to the river centre line; t = 0 upstream .. 1 at the mouth.</summary>
        public double DistanceToRiver(double x, double z, out double t)
        {
            t = 0;
            if (River.Count < 2) return double.MaxValue;
            if (_riverLength < 0)
            {
                _riverLength = 0;
                for (int i = 0; i < River.Count - 1; i++) _riverLength += Vec2.Distance(River[i], River[i + 1]);
            }

            var p = new Vec2(x, z);
            double best = double.MaxValue;
            double walked = 0;
            for (int i = 0; i < River.Count - 1; i++)
            {
                double segLen = Vec2.Distance(River[i], River[i + 1]);
                double d = Vec2.DistanceToSegment(p, River[i], River[i + 1], out double segT);
                if (d < best)
                {
                    best = d;
                    t = (walked + segT * segLen) / _riverLength;
                }
                walked += segLen;
            }
            return best;
        }

        public double RiverHalfWidthAt(double t) => RiverHalfWidthUpstream + (RiverHalfWidthMouth - RiverHalfWidthUpstream) * t;

        /// <summary>Terrain height: 0 on land, below <see cref="WaterLevel"/> in the river channel and the sea.</summary>
        public double GroundHeight(double x, double z)
        {
            double h = 0;

            double d = DistanceToRiver(x, z, out double t);
            double hw = RiverHalfWidthAt(t);
            double bank = hw + BankWidth;
            if (d < bank)
            {
                double rh;
                if (d < hw)
                {
                    double k = d / hw;
                    rh = -0.5 - 1.1 * (1 - k * k);
                }
                else
                {
                    double s = (d - hw) / (bank - hw);
                    rh = -0.5 * (1 - SmoothStep(s));
                }
                h = Math.Min(h, rh);
            }

            double fromCoast = x - CoastX(z);
            const double beach = 8.0;
            if (fromCoast < beach)
            {
                double sh = fromCoast >= 0
                    ? -0.5 * (1 - SmoothStep(fromCoast / beach))
                    : -0.5 - Math.Min(2.5, -fromCoast * 0.04);
                h = Math.Min(h, sh);
            }

            return h;
        }

        public bool IsWater(double x, double z) => GroundHeight(x, z) < WaterLevel;

        public static double SmoothStep(double x)
        {
            x = Math.Max(0, Math.Min(1, x));
            return x * x * (3 - 2 * x);
        }
    }
}
