using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TaxiSim.World
{
    /// <summary>
    /// Small procedural mesh toolkit. Every helper takes the outward normal it wants and fixes the
    /// triangle winding automatically, so faces are never accidentally inside-out.
    /// Unity front faces: Vector3.Cross(b - a, c - a) points along the face normal.
    /// </summary>
    public sealed class MeshBuilder
    {
        private readonly List<Vector3> _positions = new List<Vector3>();
        private readonly List<Vector3> _normals = new List<Vector3>();
        private readonly List<Vector4> _tangents = new List<Vector4>();
        private readonly List<Vector2> _uv0 = new List<Vector2>();
        private readonly List<Vector4> _uv1 = new List<Vector4>();
        private readonly List<Vector4> _uv2 = new List<Vector4>();
        private readonly List<Vector4> _uv3 = new List<Vector4>();
        private readonly List<Color> _colors = new List<Color>();
        private readonly List<int> _triangles = new List<int>();

        public int VertexCount => _positions.Count;
        public bool IsEmpty => _positions.Count == 0;

        public int AddVertex(Vector3 position, Vector3 normal, Vector2 uv0, Color color, Vector4 tangent = default, Vector4 uv1 = default, Vector4 uv2 = default, Vector4 uv3 = default)
        {
            _positions.Add(position);
            _normals.Add(normal);
            _tangents.Add(tangent);
            _uv0.Add(uv0);
            _uv1.Add(uv1);
            _uv2.Add(uv2);
            _uv3.Add(uv3);
            _colors.Add(color);
            return _positions.Count - 1;
        }

        /// <summary>Sets uv1 on every vertex added since <paramref name="fromVertex"/> (e.g. limb ids for animated characters).</summary>
        public void OverrideUv1(int fromVertex, Vector4 uv1)
        {
            for (int i = fromVertex; i < _uv1.Count; i++) _uv1[i] = uv1;
        }

        /// <summary>Multiplies uv1.y of every vertex (pivot heights after scaling a character).</summary>
        public void ScaleUv1Y(float k)
        {
            for (int i = 0; i < _uv1.Count; i++) { Vector4 v = _uv1[i]; v.y *= k; _uv1[i] = v; }
        }

        public void AddTriangleIndices(int a, int b, int c)
        {
            _triangles.Add(a);
            _triangles.Add(b);
            _triangles.Add(c);
        }

        /// <summary>Adds a triangle facing 'normal' (winding fixed automatically).</summary>
        public void AddOrientedTriangle(int a, int b, int c, Vector3 normal)
        {
            Vector3 pa = _positions[a], pb = _positions[b], pc = _positions[c];
            if (Vector3.Dot(Vector3.Cross(pb - pa, pc - pa), normal) >= 0f) AddTriangleIndices(a, b, c);
            else AddTriangleIndices(a, c, b);
        }

        /// <summary>Quad a-b-c-d given in loop order.</summary>
        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, Color color,
                            Vector2 uva, Vector2 uvb, Vector2 uvc, Vector2 uvd,
                            Vector4 tangent = default, Vector4 uv1 = default, Vector4 uv2 = default, Vector4 uv3 = default)
        {
            int i0 = AddVertex(a, normal, uva, color, tangent, uv1, uv2, uv3);
            int i1 = AddVertex(b, normal, uvb, color, tangent, uv1, uv2, uv3);
            int i2 = AddVertex(c, normal, uvc, color, tangent, uv1, uv2, uv3);
            int i3 = AddVertex(d, normal, uvd, color, tangent, uv1, uv2, uv3);
            AddOrientedTriangle(i0, i1, i2, normal);
            AddOrientedTriangle(i0, i2, i3, normal);
        }

        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, Color color)
        {
            AddQuad(a, b, c, d, normal, color, Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
        }

        /// <summary>Box with arbitrary rotation. uv0.y holds the local height fraction (used for wind sway).</summary>
        public void AddBox(Vector3 center, Quaternion rotation, Vector3 size, Color color, bool bottom = false)
        {
            Vector3 h = size * 0.5f;
            Vector3 Corner(float x, float y, float z) => center + rotation * new Vector3(x * h.x, y * h.y, z * h.z);
            Vector3[] c =
            {
                Corner(-1, -1, -1), Corner(1, -1, -1), Corner(1, -1, 1), Corner(-1, -1, 1),
                Corner(-1, 1, -1), Corner(1, 1, -1), Corner(1, 1, 1), Corner(-1, 1, 1)
            };
            Vector3 right = rotation * Vector3.right, up = rotation * Vector3.up, fwd = rotation * Vector3.forward;
            AddQuad(c[4], c[5], c[6], c[7], up, color, new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(0, 1));
            if (bottom) AddQuad(c[0], c[1], c[2], c[3], -up, color, Vector2.zero, Vector2.right, Vector2.right, Vector2.zero);
            AddQuad(c[0], c[1], c[5], c[4], -fwd, color, Vector2.zero, Vector2.right, new Vector2(1, 1), new Vector2(0, 1));
            AddQuad(c[3], c[2], c[6], c[7], fwd, color, Vector2.zero, Vector2.right, new Vector2(1, 1), new Vector2(0, 1));
            AddQuad(c[0], c[3], c[7], c[4], -right, color, Vector2.zero, Vector2.right, new Vector2(1, 1), new Vector2(0, 1));
            AddQuad(c[1], c[2], c[6], c[5], right, color, Vector2.zero, Vector2.right, new Vector2(1, 1), new Vector2(0, 1));
        }

        public void AddBox(Vector3 center, Vector3 size, Color color) => AddBox(center, Quaternion.identity, size, color);

        /// <summary>Box spanning from a to b (e.g. a beam, a cable, a rail). 'thickness' is (width, height).</summary>
        public void AddBeam(Vector3 a, Vector3 b, Vector2 thickness, Color color)
        {
            Vector3 dir = b - a;
            float len = dir.magnitude;
            if (len < 1e-4f) return;
            Quaternion rot = Quaternion.LookRotation(dir / len, Mathf.Abs(Vector3.Dot(dir / len, Vector3.up)) > 0.99f ? Vector3.right : Vector3.up);
            AddBox((a + b) * 0.5f, rot, new Vector3(thickness.x, thickness.y, len), color);
        }

        /// <summary>Vertical cylinder (optionally tapered). uv0.y = 0 at the base, 1 at the top.</summary>
        public void AddCylinder(Vector3 baseCenter, float radiusBottom, float radiusTop, float height, int segments, Color color, bool capTop = true)
        {
            segments = Mathf.Max(3, segments);
            float slope = (radiusBottom - radiusTop) / Mathf.Max(height, 1e-4f);
            for (int s = 0; s < segments; s++)
            {
                float a0 = s * Mathf.PI * 2f / segments;
                float a1 = (s + 1) * Mathf.PI * 2f / segments;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0));
                Vector3 d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                Vector3 n = ((d0 + d1) * 0.5f + Vector3.up * slope).normalized;
                Vector3 b0 = baseCenter + d0 * radiusBottom, b1 = baseCenter + d1 * radiusBottom;
                Vector3 t0 = baseCenter + d0 * radiusTop + Vector3.up * height, t1 = baseCenter + d1 * radiusTop + Vector3.up * height;
                AddQuad(b0, b1, t1, t0, n, color, new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1));
            }
            if (capTop && radiusTop > 1e-4f) AddDisc(baseCenter + Vector3.up * height, radiusTop, segments, Vector3.up, color, 1f);
        }

        public void AddDisc(Vector3 center, float radius, int segments, Vector3 normal, Color color, float uvY = 0f)
        {
            int c = AddVertex(center, normal, new Vector2(0.5f, uvY), color);
            Quaternion rot = Quaternion.FromToRotation(Vector3.up, normal);
            int first = -1, prev = -1;
            for (int s = 0; s < segments; s++)
            {
                float a = s * Mathf.PI * 2f / segments;
                int v = AddVertex(center + rot * new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius), normal, new Vector2(0.5f, uvY), color);
                if (prev >= 0) AddOrientedTriangle(c, prev, v, normal);
                else first = v;
                prev = v;
            }
            AddOrientedTriangle(c, prev, first, normal);
        }

        /// <summary>Low-poly ellipsoid (tree canopies, mangroves, domes). uv0.y = height weight for wind sway.</summary>
        public void AddEllipsoid(Vector3 center, Vector3 radii, int slices, int stacks, Color color, float windBase = 0.4f, bool hemisphere = false)
        {
            int startStack = hemisphere ? stacks / 2 : 0;
            int baseIndex = VertexCount;
            for (int st = startStack; st <= stacks; st++)
            {
                float v = (float)st / stacks;
                float phi = Mathf.Lerp(-Mathf.PI * 0.5f, Mathf.PI * 0.5f, v);
                for (int sl = 0; sl <= slices; sl++)
                {
                    float theta = (float)sl / slices * Mathf.PI * 2f;
                    Vector3 unit = new Vector3(Mathf.Cos(phi) * Mathf.Cos(theta), Mathf.Sin(phi), Mathf.Cos(phi) * Mathf.Sin(theta));
                    Vector3 p = center + Vector3.Scale(unit, radii);
                    Vector3 n = new Vector3(unit.x / radii.x, unit.y / radii.y, unit.z / radii.z).normalized;
                    AddVertex(p, n, new Vector2(0, windBase + (unit.y * 0.5f + 0.5f) * (1f - windBase)), color);
                }
            }
            int rows = stacks - startStack;
            for (int st = 0; st < rows; st++)
            {
                for (int sl = 0; sl < slices; sl++)
                {
                    int a = baseIndex + st * (slices + 1) + sl;
                    int b = a + 1;
                    int c = a + slices + 1;
                    int d = c + 1;
                    Vector3 n = _normals[a] + _normals[d];
                    AddOrientedTriangle(a, c, d, n);
                    AddOrientedTriangle(a, d, b, n);
                }
            }
        }

        /// <summary>Gable roof prism along local z.</summary>
        public void AddGableRoof(Vector3 baseCenter, Quaternion rotation, float width, float length, float height, Color color)
        {
            float hw = width * 0.5f, hl = length * 0.5f;
            Vector3 P(float x, float y, float z) => baseCenter + rotation * new Vector3(x, y, z);
            Vector3 l0 = P(-hw, 0, -hl), l1 = P(-hw, 0, hl), r0 = P(hw, 0, -hl), r1 = P(hw, 0, hl);
            Vector3 t0 = P(0, height, -hl), t1 = P(0, height, hl);
            Vector3 nl = rotation * new Vector3(-height, hw, 0).normalized;
            Vector3 nr = rotation * new Vector3(height, hw, 0).normalized;
            AddQuad(l0, l1, t1, t0, nl, color);
            AddQuad(r0, r1, t1, t0, nr, color);
            Vector3 back = rotation * Vector3.back, front = rotation * Vector3.forward;
            int a = AddVertex(l0, back, Vector2.zero, color), b = AddVertex(r0, back, Vector2.zero, color), c = AddVertex(t0, back, Vector2.zero, color);
            AddOrientedTriangle(a, b, c, back);
            a = AddVertex(l1, front, Vector2.zero, color); b = AddVertex(r1, front, Vector2.zero, color); c = AddVertex(t1, front, Vector2.zero, color);
            AddOrientedTriangle(a, b, c, front);
        }

        /// <summary>Flat ribbon following a polyline (route lines, rails, sidewalks). uv0 = (0..1 across, distance along).</summary>
        public void AddRibbon(IList<Vector3> points, float width, Color color, float lift = 0f, Vector4 uv1 = default)
        {
            if (points.Count < 2) return;
            float along = 0f;
            int prevL = -1, prevR = -1;
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 dir = i < points.Count - 1 ? points[i + 1] - points[i] : points[i] - points[i - 1];
                if (i > 0 && i < points.Count - 1) dir = points[i + 1] - points[i - 1];
                dir.y = 0;
                if (dir.sqrMagnitude < 1e-8f) dir = Vector3.forward;
                dir.Normalize();
                Vector3 right = new Vector3(dir.z, 0, -dir.x);
                if (i > 0) along += Vector3.Distance(points[i], points[i - 1]);
                Vector3 p = points[i] + Vector3.up * lift;
                int l = AddVertex(p - right * width * 0.5f, Vector3.up, new Vector2(0, along), color, new Vector4(dir.x, dir.y, dir.z, 1), uv1);
                int r = AddVertex(p + right * width * 0.5f, Vector3.up, new Vector2(1, along), color, new Vector4(dir.x, dir.y, dir.z, 1), uv1);
                if (prevL >= 0)
                {
                    AddOrientedTriangle(prevL, l, r, Vector3.up);
                    AddOrientedTriangle(prevL, r, prevR, Vector3.up);
                }
                prevL = l;
                prevR = r;
            }
        }

        /// <summary>Tapered tube from a to b (tree trunks and branches). uv1.x = wind weight at each end.</summary>
        public void AddTube(Vector3 a, Vector3 b, float radiusA, float radiusB, int segments, Color color, float windA = 0f, float windB = 0f)
        {
            Vector3 axis = b - a;
            float len = axis.magnitude;
            if (len < 1e-4f) return;
            axis /= len;
            Vector3 side = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.95f ? Vector3.up : Vector3.right).normalized;
            Vector3 side2 = Vector3.Cross(axis, side);
            for (int s = 0; s < segments; s++)
            {
                float a0 = s * Mathf.PI * 2f / segments, a1 = (s + 1) * Mathf.PI * 2f / segments;
                Vector3 d0 = side * Mathf.Cos(a0) + side2 * Mathf.Sin(a0);
                Vector3 d1 = side * Mathf.Cos(a1) + side2 * Mathf.Sin(a1);
                Vector3 n = (d0 + d1).normalized;
                int i0 = AddVertex(a + d0 * radiusA, d0, new Vector2(0, 0), color, default, new Vector4(windA, 0, 0, 0));
                int i1 = AddVertex(a + d1 * radiusA, d1, new Vector2(1, 0), color, default, new Vector4(windA, 0, 0, 0));
                int i2 = AddVertex(b + d1 * radiusB, d1, new Vector2(1, 1), color, default, new Vector4(windB, 0, 0, 0));
                int i3 = AddVertex(b + d0 * radiusB, d0, new Vector2(0, 1), color, default, new Vector4(windB, 0, 0, 0));
                AddOrientedTriangle(i0, i1, i2, n);
                AddOrientedTriangle(i0, i2, i3, n);
            }
        }

        /// <summary>Copies another builder's geometry, transformed and colour-tinted (instancing into big merged meshes).</summary>
        public void Append(MeshBuilder src, Matrix4x4 m, Color tint, float seedOffset = 0f)
        {
            int baseIndex = _positions.Count;
            for (int i = 0; i < src._positions.Count; i++)
            {
                Color c = src._colors[i];
                c = new Color(c.r * tint.r, c.g * tint.g, c.b * tint.b, c.a);
                Vector4 uv1 = src._uv1[i];
                uv1.z += seedOffset;
                Vector3 n = m.MultiplyVector(src._normals[i]);
                AddVertex(m.MultiplyPoint3x4(src._positions[i]), n.sqrMagnitude > 1e-8f ? n.normalized : Vector3.up,
                          src._uv0[i], c, src._tangents[i], uv1, src._uv2[i], src._uv3[i]);
            }
            foreach (int t in src._triangles) _triangles.Add(t + baseIndex);
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            if (_positions.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(_positions);
            mesh.SetNormals(_normals);
            mesh.SetTangents(_tangents);
            mesh.SetUVs(0, _uv0);
            mesh.SetUVs(1, _uv1);
            mesh.SetUVs(2, _uv2);
            mesh.SetUVs(3, _uv3);
            mesh.SetColors(_colors);
            mesh.SetTriangles(_triangles, 0, true);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Combines several builders into one mesh with one sub-mesh per builder (one material each).</summary>
        public static Mesh ToMultiMaterialMesh(string name, params MeshBuilder[] parts)
        {
            var mesh = new Mesh { name = name };
            var positions = new List<Vector3>();
            var normals = new List<Vector3>();
            var tangents = new List<Vector4>();
            var uv0 = new List<Vector2>();
            var colors = new List<Color>();
            var subTriangles = new List<List<int>>();
            foreach (MeshBuilder part in parts)
            {
                int offset = positions.Count;
                positions.AddRange(part._positions);
                normals.AddRange(part._normals);
                tangents.AddRange(part._tangents);
                uv0.AddRange(part._uv0);
                colors.AddRange(part._colors);
                var tris = new List<int>(part._triangles.Count);
                foreach (int t in part._triangles) tris.Add(t + offset);
                subTriangles.Add(tris);
            }
            if (positions.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(positions);
            mesh.SetNormals(normals);
            mesh.SetTangents(tangents);
            mesh.SetUVs(0, uv0);
            mesh.SetColors(colors);
            mesh.subMeshCount = subTriangles.Count;
            for (int i = 0; i < subTriangles.Count; i++) mesh.SetTriangles(subTriangles[i], i, false);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Creates a static child object that renders this builder's mesh.</summary>
        public GameObject Emit(string name, Transform parent, Material material, bool castShadows = true)
        {
            if (IsEmpty) return null;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.isStatic = true;
            go.AddComponent<MeshFilter>().sharedMesh = ToMesh(name);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }
    }

    /// <summary>Splits huge geometry into several meshes (better culling, below index limits).</summary>
    public sealed class ChunkedMeshBuilder
    {
        private readonly List<MeshBuilder> _done = new List<MeshBuilder>();
        private readonly int _maxVerts;
        public MeshBuilder Current { get; private set; } = new MeshBuilder();

        public ChunkedMeshBuilder(int maxVertsPerChunk = 60000) { _maxVerts = maxVertsPerChunk; }

        /// <summary>Call before adding an object; starts a new chunk if the current one is full.</summary>
        public MeshBuilder Next()
        {
            if (Current.VertexCount > _maxVerts)
            {
                _done.Add(Current);
                Current = new MeshBuilder();
            }
            return Current;
        }

        public void Emit(string name, Transform parent, Material material, bool castShadows = true)
        {
            var all = new List<MeshBuilder>(_done) { Current };
            for (int i = 0; i < all.Count; i++) all[i].Emit($"{name}_{i}", parent, material, castShadows);
        }
    }
}
