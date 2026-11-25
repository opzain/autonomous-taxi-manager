using System.Collections.Generic;
using TaxiSim.Core;
using UnityEngine;

namespace TaxiSim.World
{
    /// <summary>
    /// Live minimap (K: off / small / large). The base map is rasterised once from the city data
    /// (sea, river, parks, buildings, roads by class, railway, the AT Fleet Hub); every frame it shows
    /// the autonomous fleet (cyan), RapidCab (orange), signal states, a festival procession, flooded
    /// stretches, closed roads and the camera's position and heading. Click the map to fly there.
    /// It will become the corner map of the dispatch HUD.
    /// </summary>
    public sealed class MinimapView : MonoBehaviour
    {
        public enum Size { Off, Small, Large }

        public Camera Cam;
        public CityCameraRig Rig;
        public TrafficAmbient Traffic;
        public TrafficSignals Signals;
        public FestivalSystem Festivals;
        public FloodSystem Floods;
        public WorldOverlay Overlay;

        public Size Mode = Size.Small;

        private WorldContext _ctx;
        private Texture2D _map, _dot, _arrow;
        private float _minX, _minZ, _sizeX, _sizeZ;
        private const int Res = 768;
        private GUIStyle _small;

        public void Init(WorldContext ctx)
        {
            _ctx = ctx;
            CityGeography geo = ctx.Map.Geography;
            _minX = (float)geo.MinX; _minZ = (float)geo.MinZ;
            _sizeX = (float)(geo.MaxX - geo.MinX); _sizeZ = (float)(geo.MaxZ - geo.MinZ);
            Mode = (Size)PlayerPrefs.GetInt("TaxiSim.Minimap", (int)Size.Small);
            BuildBaseMap(geo);
            _dot = Solid(Color.white);
            _arrow = BuildArrow();
        }

        public void Cycle()
        {
            Mode = (Size)(((int)Mode + 1) % 3);
            PlayerPrefs.SetInt("TaxiSim.Minimap", (int)Mode);
        }

        // ------------------------------------------------------------------ base map

        private Vector2 ToPixel(float x, float z) => new Vector2((x - _minX) / _sizeX * Res, (z - _minZ) / _sizeZ * Res);

        private void BuildBaseMap(CityGeography geo)
        {
            var px = new Color32[Res * Res];
            var land = new Color32(30, 34, 38, 235);
            var water = new Color32(18, 52, 78, 235);
            var shore = new Color32(40, 70, 70, 235);
            for (int j = 0; j < Res; j++)
            {
                float z = _minZ + (j + 0.5f) / Res * _sizeZ;
                for (int i = 0; i < Res; i++)
                {
                    float x = _minX + (i + 0.5f) / Res * _sizeX;
                    double h = geo.GroundHeight(x, z);
                    px[j * Res + i] = h < CityGeography.WaterLevel + 0.05 ? water : h < 0.0 ? shore : land;
                }
            }

            // Buildings (darker grey, landmarks a bit lighter)
            foreach (BuildingRecord r in _ctx.Towers.Records)
            {
                byte v = (byte)Mathf.Clamp(58 + r.Height * 3f, 58, 110);
                FillRect(px, r.Center, r.Width, r.Depth, r.RotationY, new Color32(v, v, (byte)(v + 6), 255));
            }

            // Roads by class
            foreach (RoadPath p in _ctx.Roads.Paths)
            {
                Color32 c = p.Edge.Class == RoadClass.Expressway ? new Color32(235, 190, 90, 255)
                          : p.Edge.Class == RoadClass.Arterial ? new Color32(220, 210, 170, 255)
                          : p.Edge.Class == RoadClass.Main ? new Color32(190, 192, 198, 255)
                          : new Color32(150, 152, 158, 255);
                float w = Mathf.Max(1.5f, p.Width / _sizeX * Res * 1.3f);
                for (int k = 0; k < p.Points.Length - 1; k++)
                    Line(px, ToPixel(p.Points[k].x, p.Points[k].z), ToPixel(p.Points[k + 1].x, p.Points[k + 1].z), w, c);
            }

            // Junction dots and the AT Fleet Hub
            foreach (CityNode n in _ctx.Map.Graph.Nodes)
            {
                Vector2 q = ToPixel((float)n.Position.X, (float)n.Position.Z);
                Color32 c = n.Zone == ZoneType.FleetDepot ? new Color32(60, 230, 255, 255)
                          : n.IsChargingStation ? new Color32(80, 230, 120, 255)
                          : new Color32(200, 200, 205, 255);
                Disc(px, q, n.Zone == ZoneType.FleetDepot ? 6f : 3f, c);
            }

            _map = new Texture2D(Res, Res, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Minimap" };
            _map.SetPixels32(px);
            _map.Apply(false, true);
        }

        private void FillRect(Color32[] px, Vector2 c, float w, float d, float rotY, Color32 col)
        {
            Quaternion q = Quaternion.Euler(0f, rotY, 0f);
            Vector3 ax = q * Vector3.right, az = q * Vector3.forward;
            float rad = 0.5f * Mathf.Sqrt(w * w + d * d);
            Vector2 lo = ToPixel(c.x - rad, c.y - rad), hi = ToPixel(c.x + rad, c.y + rad);
            for (int j = Mathf.Max(0, (int)lo.y); j <= Mathf.Min(Res - 1, (int)hi.y); j++)
                for (int i = Mathf.Max(0, (int)lo.x); i <= Mathf.Min(Res - 1, (int)hi.x); i++)
                {
                    float x = _minX + (i + 0.5f) / Res * _sizeX - c.x;
                    float z = _minZ + (j + 0.5f) / Res * _sizeZ - c.y;
                    if (Mathf.Abs(x * ax.x + z * ax.z) <= w * 0.5f && Mathf.Abs(x * az.x + z * az.z) <= d * 0.5f) px[j * Res + i] = col;
                }
        }

        private static void Line(Color32[] px, Vector2 a, Vector2 b, float width, Color32 c)
        {
            float len = Vector2.Distance(a, b);
            int steps = Mathf.Max(1, Mathf.CeilToInt(len * 2f));
            for (int s = 0; s <= steps; s++) Disc(px, Vector2.Lerp(a, b, s / (float)steps), width * 0.5f, c);
        }

        private static void Disc(Color32[] px, Vector2 c, float r, Color32 col)
        {
            int r0 = Mathf.CeilToInt(r);
            for (int j = -r0; j <= r0; j++)
                for (int i = -r0; i <= r0; i++)
                {
                    if (i * i + j * j > r * r + 0.5f) continue;
                    int x = (int)c.x + i, y = (int)c.y + j;
                    if (x >= 0 && y >= 0 && x < Res && y < Res) px[y * Res + x] = col;
                }
        }

        private static Texture2D Solid(Color c)
        {
            var t = new Texture2D(1, 1) { name = "MinimapDot" };
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        private static Texture2D BuildArrow()
        {
            const int n = 32;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "MinimapArrow", filterMode = FilterMode.Bilinear };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    // Triangle pointing up (towards -y in GUI space = forward)
                    float u = (x + 0.5f) / n - 0.5f, v = (y + 0.5f) / n;
                    bool inside = v > 0.1f && v < 0.95f && Mathf.Abs(u) < (v - 0.1f) * 0.5f;
                    t.SetPixel(x, n - 1 - y, inside ? Color.white : Color.clear);
                }
            t.Apply();
            return t;
        }

        // ------------------------------------------------------------------ input and drawing

        private void Update()
        {
            if (InputCompat.Pressed(TsKey.K)) Cycle();
        }

        private void OnGUI()
        {
            if (Mode == Size.Off || _map == null) return;
            if (Overlay != null && (Overlay.Suppressed || Overlay.HideAll)) return;
            float s = Mathf.Max(0.75f, Screen.height / 1080f);
            float size = (Mode == Size.Large ? 520f : 260f) * s;
            float aspect = _sizeX / _sizeZ;
            var r = new Rect(Screen.width - size * aspect - 16 * s, Screen.height - size - 40 * s, size * aspect, size);

            Color old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(r.x - 4 * s, r.y - 4 * s, r.width + 8 * s, r.height + 8 * s), _dot);
            GUI.color = Color.white;
            GUI.DrawTexture(r, _map, ScaleMode.StretchToFill);

            // Closed (festival) and flooded roads
            if (Festivals != null && Festivals.ProcessionActive)
            {
                Pulse(r, Festivals.ProcessionPosition, new Color(1f, 0.3f, 0.8f), 9f * s);
            }
            if (Floods != null)
                foreach (FloodSystem.Zone z in Floods.Zones)
                    if (z.Depth > 0.005f)
                        Pulse(r, z.Path.Sample((z.FromA + z.ToA) * 0.5f, out _), new Color(0.3f, 0.6f, 1f), 8f * s);

            // Signals: small red / green squares
            if (Signals != null && _ctx != null && Mode == Size.Large)
                foreach (int node in Signals.Nodes)
                {
                    CityNode n = _ctx.Map.Graph.Node(node);
                    bool walk = Signals.WalkTimeLeft(node) > 0f;
                    Dot(r, new Vector3((float)n.Position.X, 0f, (float)n.Position.Z), walk ? new Color(0.3f, 1f, 0.4f) : new Color(1f, 0.3f, 0.25f), 4f * s);
                }

            // Vehicles: everyone faint, fleet and rivals bright
            if (Traffic != null)
            {
                if (Mode == Size.Large)
                    foreach (TrafficAmbient.Agent a in Traffic.Agents)
                        if (a.Kind != VehicleKind.FleetEv && a.Kind != VehicleKind.RivalCab)
                            Dot(r, a.Transform.position, new Color(0.85f, 0.85f, 0.85f, 0.45f), 2.5f * s);
                foreach (TrafficAmbient.Agent a in Traffic.Agents)
                {
                    if (a.Kind == VehicleKind.RivalCab) Dot(r, a.Transform.position, new Color(1f, 0.55f, 0.1f), 5f * s);
                    else if (a.Kind == VehicleKind.FleetEv) Dot(r, a.Transform.position, new Color(0.25f, 0.95f, 1f), 6f * s);
                }
            }

            // Camera position and heading
            if (Cam != null)
            {
                Vector3 cp = Cam.transform.position;
                Vector2 g = ToGui(r, cp);
                float yaw = Cam.transform.eulerAngles.y;
                Matrix4x4 m = GUI.matrix;
                GUIUtility.RotateAroundPivot(yaw, g);
                GUI.color = new Color(1f, 1f, 1f, 0.95f);
                float a = 18f * s;
                GUI.DrawTexture(new Rect(g.x - a * 0.5f, g.y - a * 0.5f, a, a), _arrow);
                GUI.matrix = m;
            }

            // Legend and click-to-fly
            if (_small == null) _small = new GUIStyle(GUI.skin.label) { normal = { textColor = new Color(0.85f, 0.9f, 0.95f) } };
            _small.fontSize = Mathf.RoundToInt(11 * s);
            GUI.color = Color.white;
            GUI.Label(new Rect(r.x, r.yMax + 4 * s, r.width, 18 * s),
                "<color=#40F0FF>■</color> AT fleet   <color=#FF8C1A>■</color> RapidCab   <color=#FF4DCC>●</color> procession   <color=#4D99FF>●</color> flood   K map", RichSmall());
            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition) && Rig != null)
            {
                float u = (e.mousePosition.x - r.x) / r.width, v = 1f - (e.mousePosition.y - r.y) / r.height;
                var world = new Vector3(_minX + u * _sizeX, 0f, _minZ + v * _sizeZ);
                Rig.FlyTo(world, 60f, "Map");
                e.Use();
            }
            GUI.color = old;
        }

        private GUIStyle RichSmall()
        {
            _small.richText = true;
            return _small;
        }

        private Vector2 ToGui(Rect r, Vector3 world)
        {
            float u = (world.x - _minX) / _sizeX, v = (world.z - _minZ) / _sizeZ;
            return new Vector2(r.x + u * r.width, r.y + (1f - v) * r.height);
        }

        private void Dot(Rect r, Vector3 world, Color c, float size)
        {
            Vector2 g = ToGui(r, world);
            if (!r.Contains(g)) return;
            GUI.color = c;
            GUI.DrawTexture(new Rect(g.x - size * 0.5f, g.y - size * 0.5f, size, size), _dot);
        }

        private void Pulse(Rect r, Vector3 world, Color c, float size)
        {
            float k = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 5f);
            Dot(r, world, new Color(c.r, c.g, c.b, 0.35f), size * (1.6f + k));
            Dot(r, world, c, size);
        }
    }
}
