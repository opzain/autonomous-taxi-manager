using System.Collections.Generic;
using UnityEngine;

namespace TaxiSim.World
{
    /// <summary>Per-building look, baked into vertex data so thousands of buildings share one material.</summary>
    public struct TowerStyle
    {
        public float Seed;          // 0..1 random, drives lit rooms, grime, roof details
        public float Style;         // 0 = glass curtain wall .. 1 = punched concrete windows
        public float TotalHeight;   // overall building height (for crown LEDs / aviation lights)
        public bool Accent;         // LED crown band
        public bool TarpRoof;       // blue tarpaulin roof (dense low-rise)
        public Color Frame;         // frame / concrete colour
        public Color Glass;         // glass tint
        public int NeonHue;         // 0 = no neon, 1..5 = neon tube colour (Neon Mumbai 2077 style)
        public bool HoloAd;         // animated holographic advert on the upper facade
        public bool Shopfront;      // Mumbai ground-floor shops (signboards, display windows, shutters)

        public Vector4 Uv1 => new Vector4(Seed, Style, TotalHeight, Accent ? 1f : 0f);
        public Vector4 Uv2 => new Vector4(Glass.r, Glass.g, Glass.b, TarpRoof ? 1f : 0f);
        public Vector4 Uv3(float faceLength) => new Vector4(faceLength, NeonHue, HoloAd ? 1f : 0f, Shopfront ? 1f : 0f);
    }

    /// <summary>Where each building block is (used to place rooftop billboards, signs and drone pads).</summary>
    public struct BuildingRecord
    {
        public Vector2 Center;
        public float BaseY;
        public float Width;
        public float Depth;
        public float Height;
        public float RotationY;
        public bool Cylinder;
        public TowerStyle Style;
        public float Top => BaseY + Height;
    }

    /// <summary>
    /// Writes building geometry for the TaxiSim/CityTower shader: facade coordinates in uv0
    /// (u along the wall, v = height above ground), style data in uv1/uv2/uv3, frame colour in vertex colour.
    /// </summary>
    public sealed class TowerMeshBuilder
    {
        private readonly ChunkedMeshBuilder _chunks = new ChunkedMeshBuilder(60000);
        public readonly List<BuildingRecord> Records = new List<BuildingRecord>();
        public int BuildingCount { get; private set; }

        /// <summary>Rectangular block. baseY = world height of its base; heightOffset = height of this block's base above ground (for stacked tiers).</summary>
        public void AddBox(Vector2 centerXZ, float baseY, float width, float depth, float height, float rotationY, TowerStyle style, float heightOffset = 0f)
        {
            MeshBuilder mb = _chunks.Next();
            Quaternion rot = Quaternion.Euler(0f, rotationY, 0f);
            Vector3 center = new Vector3(centerXZ.x, baseY, centerXZ.y);
            float hw = width * 0.5f, hd = depth * 0.5f;
            Vector3[] local = { new Vector3(-hw, 0, -hd), new Vector3(hw, 0, -hd), new Vector3(hw, 0, hd), new Vector3(-hw, 0, hd) };
            Vector4 uv1 = style.Uv1, uv2 = style.Uv2;
            float y0 = heightOffset, y1 = heightOffset + height;
            Vector3 up = Vector3.up * height;

            for (int i = 0; i < 4; i++)
            {
                Vector3 la = local[i], lb = local[(i + 1) % 4];
                Vector3 edge = lb - la;
                float faceLen = edge.magnitude;
                Vector3 nLocal = new Vector3(edge.z, 0, -edge.x).normalized;
                Vector3 a = center + rot * la, b = center + rot * lb;
                Vector3 n = rot * nLocal;
                Vector3 t = (rot * edge).normalized;
                var tangent = new Vector4(t.x, t.y, t.z, 1f);
                mb.AddQuad(a, b, b + up, a + up, n, style.Frame,
                    new Vector2(0, y0), new Vector2(faceLen, y0), new Vector2(faceLen, y1), new Vector2(0, y1),
                    tangent, uv1, uv2, style.Uv3(faceLen));
            }

            Vector3 r0 = center + rot * local[0] + up, r1 = center + rot * local[1] + up;
            Vector3 r2 = center + rot * local[2] + up, r3 = center + rot * local[3] + up;
            mb.AddQuad(r0, r1, r2, r3, Vector3.up, style.Frame,
                new Vector2(-hw, -hd), new Vector2(hw, -hd), new Vector2(hw, hd), new Vector2(-hw, hd),
                new Vector4(1, 0, 0, 1), uv1, uv2, style.Uv3(0f));
            BuildingCount++;
            Records.Add(new BuildingRecord { Center = centerXZ, BaseY = baseY, Width = width, Depth = depth, Height = height, RotationY = rotationY, Style = style });
        }

        /// <summary>Cylindrical (or tapered) tower: curved glass hotels, telecom towers, domes' drums.</summary>
        public void AddCylinder(Vector2 centerXZ, float baseY, float radius, float height, int segments, TowerStyle style, float heightOffset = 0f, float topRadius = -1f)
        {
            MeshBuilder mb = _chunks.Next();
            if (topRadius < 0f) topRadius = radius;
            Vector3 c = new Vector3(centerXZ.x, baseY, centerXZ.y);
            Vector4 uv1 = style.Uv1, uv2 = style.Uv2;
            // The whole circumference is one "face" so neon trims don't appear on every segment seam
            Vector4 uv3 = style.Uv3(Mathf.PI * 2f * radius);
            float y0 = heightOffset, y1 = heightOffset + height;
            for (int s = 0; s < segments; s++)
            {
                float a0 = s * Mathf.PI * 2f / segments, a1 = (s + 1) * Mathf.PI * 2f / segments;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                Vector3 n = (d0 + d1).normalized;
                Vector3 t = new Vector3(-n.z, 0, n.x);
                float u0 = a0 * radius, u1 = a1 * radius;
                mb.AddQuad(c + d0 * radius, c + d1 * radius, c + d1 * topRadius + Vector3.up * height, c + d0 * topRadius + Vector3.up * height,
                    n, style.Frame,
                    new Vector2(u0, y0), new Vector2(u1, y0), new Vector2(u1, y1), new Vector2(u0, y1),
                    new Vector4(t.x, t.y, t.z, 1f), uv1, uv2, uv3);
            }

            Vector3 top = c + Vector3.up * height;
            Vector4 roofUv3 = style.Uv3(0f);
            int centre = mb.AddVertex(top, Vector3.up, Vector2.zero, style.Frame, new Vector4(1, 0, 0, 1), uv1, uv2, roofUv3);
            int first = -1, prev = -1;
            for (int s = 0; s < segments; s++)
            {
                float a = s * Mathf.PI * 2f / segments;
                Vector3 off = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * topRadius;
                int v = mb.AddVertex(top + off, Vector3.up, new Vector2(off.x, off.z), style.Frame, new Vector4(1, 0, 0, 1), uv1, uv2, roofUv3);
                if (prev >= 0) mb.AddOrientedTriangle(centre, prev, v, Vector3.up); else first = v;
                prev = v;
            }
            mb.AddOrientedTriangle(centre, prev, first, Vector3.up);
            BuildingCount++;
            Records.Add(new BuildingRecord { Center = centerXZ, BaseY = baseY, Width = radius * 2f, Depth = radius * 2f, Height = height, Cylinder = true, Style = style });
        }

        public void Emit(string name, Transform parent, Material towerMaterial) => _chunks.Emit(name, parent, towerMaterial, true);
    }
}
