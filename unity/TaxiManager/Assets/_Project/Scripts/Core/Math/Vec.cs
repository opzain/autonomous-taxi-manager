using System;

namespace TaxiSim.Core
{
    /// <summary>Engine-free 2D vector (x = east, y = north). 1 unit = 10 m.</summary>
    public readonly struct Vec2 : IEquatable<Vec2>
    {
        public readonly double X;
        public readonly double Y;

        public Vec2(double x, double y) { X = x; Y = y; }

        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Y + b.Y);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Y - b.Y);
        public static Vec2 operator *(Vec2 a, double s) => new Vec2(a.X * s, a.Y * s);

        public double Length => Math.Sqrt(X * X + Y * Y);
        public Vec2 Normalized { get { double l = Length; return l > 1e-9 ? new Vec2(X / l, Y / l) : new Vec2(0, 0); } }

        public static double Dot(Vec2 a, Vec2 b) => a.X * b.X + a.Y * b.Y;
        public static double Distance(Vec2 a, Vec2 b) => (a - b).Length;
        public static Vec2 Lerp(Vec2 a, Vec2 b, double t) => new Vec2(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

        /// <summary>Distance from p to segment ab; t returns the 0..1 projection parameter.</summary>
        public static double DistanceToSegment(Vec2 p, Vec2 a, Vec2 b, out double t)
        {
            Vec2 ab = b - a;
            double len2 = Dot(ab, ab);
            t = len2 > 1e-12 ? Math.Max(0, Math.Min(1, Dot(p - a, ab) / len2)) : 0;
            return Distance(p, a + ab * t);
        }

        public bool Equals(Vec2 other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is Vec2 v && Equals(v);
        public override int GetHashCode() => X.GetHashCode() * 397 ^ Y.GetHashCode();
        public override string ToString() => $"({X:0.##}, {Y:0.##})";
    }

    /// <summary>Engine-free 3D vector in Unity's convention: x = east, y = up, z = north.</summary>
    public readonly struct Vec3 : IEquatable<Vec3>
    {
        public readonly double X;
        public readonly double Y;
        public readonly double Z;

        public Vec3(double x, double y, double z) { X = x; Y = y; Z = z; }

        public static Vec3 operator +(Vec3 a, Vec3 b) => new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vec3 operator -(Vec3 a, Vec3 b) => new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vec3 operator *(Vec3 a, double s) => new Vec3(a.X * s, a.Y * s, a.Z * s);

        public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);
        public Vec2 XZ => new Vec2(X, Z);

        public static double Distance(Vec3 a, Vec3 b) => (a - b).Length;
        public static double DistanceXZ(Vec3 a, Vec3 b) => Vec2.Distance(a.XZ, b.XZ);
        public static Vec3 Lerp(Vec3 a, Vec3 b, double t) => new Vec3(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);

        public bool Equals(Vec3 other) => X == other.X && Y == other.Y && Z == other.Z;
        public override bool Equals(object obj) => obj is Vec3 v && Equals(v);
        public override int GetHashCode() => (X.GetHashCode() * 397 ^ Y.GetHashCode()) * 397 ^ Z.GetHashCode();
        public override string ToString() => $"({X:0.##}, {Y:0.##}, {Z:0.##})";
    }
}
