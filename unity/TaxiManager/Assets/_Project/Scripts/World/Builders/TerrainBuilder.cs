using System;
using TaxiSim.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace TaxiSim.World
{
    /// <summary>
    /// Builds the ground and water. Three ground LOD rings (2 / 8 / 32 unit resolution) carve the
    /// Mithi River channel and the Arabian Sea coast; a water surface sits at the water level
    /// underneath, so water only shows where the ground dips below it.
    /// </summary>
    public static class TerrainBuilder
    {
        public static void Build(CityMap map, MaterialLibrary mats, Transform parent)
        {
            CityGeography geo = map.Geography;
            var root = new GameObject("Terrain").transform;
            root.SetParent(parent, false);

            float minX = (float)geo.MinX, maxX = (float)geo.MaxX, minZ = (float)geo.MinZ, maxZ = (float)geo.MaxZ;
            var inner = Rect.MinMaxRect(-200f, -136f, 464f, 344f);
            var bounds = Rect.MinMaxRect(minX, minZ, maxX, maxZ);
            var horizon = Rect.MinMaxRect(minX - 1600f, minZ - 1600f, maxX + 1600f, maxZ + 1600f);

            Func<float, float, float> height = (x, z) => (float)geo.GroundHeight(x, z);

            EmitGrid("Ground_Inner", root, mats.Ground, inner, 2f, null, height, true);
            EmitGrid("Ground_Outer", root, mats.Ground, bounds, 8f, inner, height, true);
            EmitGrid("Ground_Horizon", root, mats.Ground, horizon, 32f, bounds, height, false);

            float water = (float)CityGeography.WaterLevel;
            Func<float, float, float> flat = (x, z) => water;
            EmitGrid("Water_Main", root, mats.Water, bounds, 8f, null, flat, false, isWater: true);
            EmitGrid("Water_Horizon", root, mats.Water, horizon, 32f, bounds, flat, false, isWater: true);
        }

        private static void EmitGrid(string name, Transform parent, Material material, Rect area, float res, Rect? hole,
                                     Func<float, float, float> height, bool castShadows, bool isWater = false)
        {
            int nx = Mathf.RoundToInt(area.width / res);
            int nz = Mathf.RoundToInt(area.height / res);
            int vx = nx + 1, vz = nz + 1;
            var verts = new Vector3[vx * vz];
            var normals = new Vector3[vx * vz];
            var h = new float[vx * vz];

            for (int j = 0; j < vz; j++)
            {
                for (int i = 0; i < vx; i++)
                {
                    float x = area.xMin + i * res, z = area.yMin + j * res;
                    float y = height(x, z);
                    h[j * vx + i] = y;
                    verts[j * vx + i] = new Vector3(x, y, z);
                }
            }

            for (int j = 0; j < vz; j++)
            {
                for (int i = 0; i < vx; i++)
                {
                    float hl = h[j * vx + Mathf.Max(i - 1, 0)], hr = h[j * vx + Mathf.Min(i + 1, vx - 1)];
                    float hd = h[Mathf.Max(j - 1, 0) * vx + i], hu = h[Mathf.Min(j + 1, vz - 1) * vx + i];
                    normals[j * vx + i] = new Vector3(hl - hr, 2f * res, hd - hu).normalized;
                }
            }

            var tris = new System.Collections.Generic.List<int>(nx * nz * 6);
            for (int j = 0; j < nz; j++)
            {
                for (int i = 0; i < nx; i++)
                {
                    if (hole.HasValue)
                    {
                        var c = new Vector2(area.xMin + (i + 0.5f) * res, area.yMin + (j + 0.5f) * res);
                        if (hole.Value.Contains(c)) continue;
                    }
                    int a = j * vx + i, b = a + 1, c0 = a + vx, d = c0 + 1;
                    // Clockwise seen from above (Unity front face) -> normal points up.
                    tris.Add(a); tris.Add(c0); tris.Add(d);
                    tris.Add(a); tris.Add(d); tris.Add(b);
                }
            }

            var mesh = new Mesh { name = name, indexFormat = verts.Length > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.SetTriangles(tris, 0, true);
            mesh.RecalculateBounds();
            if (isWater)
            {
                // Waves move vertices a little; enlarge bounds so the surface is never culled early.
                Bounds b = mesh.bounds;
                b.Expand(new Vector3(0, 1f, 0));
                mesh.bounds = b;
            }

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.isStatic = !isWater;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            mr.receiveShadows = true;
        }
    }
}
