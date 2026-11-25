using TaxiSim.Core;
using UnityEngine;

namespace TaxiSim.World
{
    /// <summary>Conversions between the engine-free simulation core and Unity types.</summary>
    public static class CoreConvert
    {
        public static Vector3 ToVector3(this Vec3 v) => new Vector3((float)v.X, (float)v.Y, (float)v.Z);

        /// <summary>Core Vec2 is (x = east, y = north) which maps to Unity (x, z).</summary>
        public static Vector3 ToVector3(this Vec2 v, float y = 0f) => new Vector3((float)v.X, y, (float)v.Y);

        public static Vector2 ToVector2(this Vec2 v) => new Vector2((float)v.X, (float)v.Y);

        public static Vector2 XZ(this Vector3 v) => new Vector2(v.x, v.z);

        public static Vector3 X0Z(this Vector2 v, float y = 0f) => new Vector3(v.x, y, v.y);

        public static float GroundY(this CityGeography geo, float x, float z) => (float)geo.GroundHeight(x, z);

        /// <summary>Unity colour from 0..255 sRGB bytes.</summary>
        public static Color Rgb(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f, 1f);
    }
}
