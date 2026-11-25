using System.Collections.Generic;
using UnityEngine;

namespace TaxiSim.World
{
    /// <summary>
    /// Lightweight on-screen overlay for the 3D world preview (IMGUI, no assets needed):
    /// title card, clock/weather/graphics status, floating landmark labels, fleet taxi tags,
    /// route info and a controls help panel. The full dispatch HUD (UI Toolkit) comes in a later phase.
    /// </summary>
    public sealed class WorldOverlay : MonoBehaviour
    {
        public Camera Cam;
        public TimeOfDay Clock;
        public WeatherSystem Weather;
        public CityCameraRig Rig;
        public RouteShowcase Routes;
        public CityPulse Pulse;
        public TrafficAmbient Traffic;
        [System.NonSerialized] public List<LandmarkInfo> Landmarks = new List<LandmarkInfo>();
        public GraphicsTier Tier;
        public int BuildingCount;
        public string StyleName = "Auto";
        public string TextureReport = "";
        public int MaxLabels = 9;
        private readonly List<LandmarkInfo> _visible = new List<LandmarkInfo>();

        public bool ShowLabels = true;
        public bool ShowHelp = true;
        public bool HideAll;
        /// <summary>Set by the cinematic director: draw nothing at all (it draws its own letterbox and captions).</summary>
        [System.NonSerialized] public bool Suppressed;
        /// <summary>A dispatch shift is running: its HUD replaces the status, route and help panels (place labels stay).</summary>
        [System.NonSerialized] public bool GameMode;

        private GUIStyle _panel, _title, _text, _small, _label, _labelSub, _key;
        private Texture2D _panelTex, _accentTex, _pillTex;
        private float _fps = 60f;
        private float _helpTimer = 20f;

        private void Update()
        {
            _fps = Mathf.Lerp(_fps, 1f / Mathf.Max(Time.unscaledDeltaTime, 1e-4f), 0.05f);
            if (_helpTimer > 0f)
            {
                _helpTimer -= Time.unscaledDeltaTime;
                if (_helpTimer <= 0f) ShowHelp = false;
            }
        }

        private void EnsureStyles()
        {
            if (_panel != null) return;
            _panelTex = Solid(new Color(0.03f, 0.05f, 0.08f, 0.78f));
            _accentTex = Solid(new Color(0.15f, 0.85f, 1f, 1f));
            _pillTex = Solid(new Color(0.02f, 0.04f, 0.07f, 0.72f));
            _panel = new GUIStyle { normal = { background = _panelTex }, padding = new RectOffset(16, 16, 12, 12) };
            _title = new GUIStyle { fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.55f, 0.95f, 1f) } };
            _text = new GUIStyle { normal = { textColor = new Color(0.92f, 0.95f, 0.98f) }, richText = true };
            _small = new GUIStyle { normal = { textColor = new Color(0.65f, 0.72f, 0.8f) }, richText = true, wordWrap = true };
            _label = new GUIStyle { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            _labelSub = new GUIStyle { alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.75f, 0.82f, 0.9f) } };
            _key = new GUIStyle { normal = { textColor = new Color(0.55f, 0.95f, 1f) }, fontStyle = FontStyle.Bold };
        }

        private static Texture2D Solid(Color c)
        {
            var t = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        private void OnGUI()
        {
            if (Suppressed) return;
            EnsureStyles();
            float s = Mathf.Max(0.75f, Screen.height / 1080f);
            _title.fontSize = Mathf.RoundToInt(22 * s);
            _text.fontSize = Mathf.RoundToInt(15 * s);
            _small.fontSize = Mathf.RoundToInt(13 * s);
            _label.fontSize = Mathf.RoundToInt(14 * s);
            _labelSub.fontSize = Mathf.RoundToInt(11 * s);
            _key.fontSize = Mathf.RoundToInt(13 * s);

            if (HideAll)
            {
                GUI.Label(new Rect(12 * s, Screen.height - 26 * s, 400 * s, 22 * s), "U = show interface", _small);
                return;
            }

            if (ShowLabels) DrawLabels(s);
            if (GameMode) return;
            DrawStatus(s);
            if (Routes != null && !string.IsNullOrEmpty(Routes.Info)) DrawRoute(s);
            if (ShowHelp) DrawHelp(s);
            else GUI.Label(new Rect(12 * s, Screen.height - 26 * s, 500 * s, 22 * s), "I = controls   ·   U = hide interface", _small);
        }

        private void DrawStatus(float s)
        {
            var r = new Rect(16 * s, 16 * s, 560 * s, 168 * s);
            GUI.Box(r, GUIContent.none, _panel);
            GUI.DrawTexture(new Rect(r.x, r.y, 4 * s, r.height), _accentTex);
            float x = r.x + 18 * s, y = r.y + 12 * s;
            GUI.Label(new Rect(x, y, r.width, 28 * s), "AUTONOMOUS TAXI MANAGER", _title);
            y += 30 * s;
            GUI.Label(new Rect(x, y, r.width, 20 * s), "Bandra Kurla Complex · Mumbai  —  3D World Preview", _small);
            y += 26 * s;
            string clock = Clock != null ? Clock.ClockText : "--:--";
            string lapse = Clock != null && Clock.TimeLapse ? "  > time-lapse" : "  || paused";
            string weather = Weather != null ? Weather.Mode.ToString() : "Clear";
            GUI.Label(new Rect(x, y, r.width, 22 * s), $"<b>{clock}</b>{lapse}    ·    Weather: <b>{weather}</b>", _text);
            y += 24 * s;
            string styleName = StyleName;
            GUI.Label(new Rect(x, y, r.width, 22 * s), $"Style: <b>{styleName}</b>   ·   Graphics: <b>{Tier}</b>    ·    {_fps:0} FPS    ·    {BuildingCount} buildings", _text);
            y += 24 * s;
            string focus = Rig != null ? Rig.FocusName : "";
            string heat = Pulse != null && Pulse.HeatOverlay ? "  ·  traffic overlay ON" : "";
            GUI.Label(new Rect(x, y, r.width, 22 * s), $"View: {focus}{heat}    ·    Textures: {TextureReport}", _small);
        }

        private void DrawRoute(float s)
        {
            var r = new Rect(Screen.width * 0.5f - 330 * s, 16 * s, 660 * s, 44 * s);
            GUI.Box(r, GUIContent.none, _panel);
            GUI.DrawTexture(new Rect(r.x, r.y, 4 * s, r.height), _accentTex);
            GUI.Label(new Rect(r.x + 16 * s, r.y + 12 * s, r.width - 24 * s, 22 * s), "ROUTE  " + Routes.Info, _text);
        }

        private void DrawLabels(float s)
        {
            if (Cam == null) return;
            Vector3 camPos = Cam.transform.position;
            if (Rig != null && Rig.StreetMode) return; // keep the street-level view clean
            // Only the nearest few labels, so the city is not buried under text
            _visible.Clear();
            foreach (LandmarkInfo l in Landmarks)
            {
                float d = Vector3.Distance(camPos, l.Position);
                if (d <= 260f) _visible.Add(l);
            }
            _visible.Sort((a, b) => Vector3.Distance(camPos, a.Position).CompareTo(Vector3.Distance(camPos, b.Position)));
            if (_visible.Count > MaxLabels) _visible.RemoveRange(MaxLabels, _visible.Count - MaxLabels);
            foreach (LandmarkInfo lm in _visible)
            {
                float dist = Vector3.Distance(camPos, lm.Position);
                Vector3 sp = Cam.WorldToScreenPoint(lm.Position);
                if (sp.z <= 0f) continue;
                float alpha = Mathf.Clamp01(1.3f - dist / 220f);
                if (alpha <= 0.02f) continue;
                float y = Screen.height - sp.y;
                Vector2 size = _label.CalcSize(new GUIContent(lm.Name));
                var pill = new Rect(sp.x - size.x * 0.5f - 10 * s, y - 30 * s, size.x + 20 * s, 40 * s);
                Color old = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, alpha);
                GUI.DrawTexture(pill, _pillTex);
                GUI.color = new Color(lm.Accent.r, lm.Accent.g, lm.Accent.b, alpha);
                GUI.DrawTexture(new Rect(pill.x, pill.yMax - 2 * s, pill.width, 2 * s), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(sp.x - 1 * s, pill.yMax, 2 * s, 14 * s), Texture2D.whiteTexture);
                GUI.color = new Color(1f, 1f, 1f, alpha);
                GUI.Label(new Rect(pill.x, pill.y + 3 * s, pill.width, 20 * s), lm.Name, _label);
                GUI.Label(new Rect(pill.x, pill.y + 21 * s, pill.width, 16 * s), lm.Subtitle, _labelSub);
                GUI.color = old;
            }

            if (Traffic == null) return;
            foreach (TrafficAmbient.Agent a in Traffic.Fleet)
            {
                Vector3 p = a.Transform.position + Vector3.up * 1.2f;
                float dist = Vector3.Distance(camPos, p);
                if (dist > 160f) continue;
                Vector3 sp = Cam.WorldToScreenPoint(p);
                if (sp.z <= 0f) continue;
                var rect = new Rect(sp.x - 26 * s, Screen.height - sp.y - 10 * s, 52 * s, 18 * s);
                GUI.DrawTexture(rect, _pillTex);
                GUI.Label(rect, a.Label, _labelSub);
            }
        }

        private void DrawHelp(float s)
        {
            string[,] rows =
            {
                { "W A S D / arrows", "Pan the camera (x / z)" },
                { "Mouse wheel · Z / X", "Zoom / height (y)" },
                { "Right-drag · Q / E · R / F", "Orbit and tilt" },
                { "Middle-drag", "Drag the map" },
                { "Tab / Shift+Tab", "Fly to next landmark" },
                { "Space · Esc", "Follow a fleet taxi · stop" },
                { "C · J · H", "Cinematic mode · next shot · home view" },
                { "T · [ ]", "Time-lapse · change hour" },
                { "1 2 3 4", "Dawn · Noon · Sunset · Night" },
                { "M · Y", "Weather (monsoon floods roads) · festival" },
                { "P · Shift+P · Shift+O", "Random route · close a road · reopen" },
                { "O", "Traffic heat overlay" },
                { "V · G", "Style: Auto / Neon / Realistic · street camera" },
                { "B", "Tilt-shift miniature mode" },
                { "F1–F5", "Graphics Low → Cinematic" },
                { "L · U · F9", "Labels · hide UI · screenshot" },
                { "N · K · Esc", "Sound · minimap · pause / settings" }
            };
            float w = 470 * s, lh = 21 * s;
            var r = new Rect(Screen.width - w - 16 * s, 16 * s, w, (rows.GetLength(0) + 2) * lh + 18 * s);
            GUI.Box(r, GUIContent.none, _panel);
            GUI.DrawTexture(new Rect(r.x, r.y, 4 * s, r.height), _accentTex);
            GUI.Label(new Rect(r.x + 18 * s, r.y + 10 * s, w, lh), "CONTROLS  (I to hide)", _key);
            for (int i = 0; i < rows.GetLength(0); i++)
            {
                float y = r.y + 14 * s + (i + 1) * lh;
                GUI.Label(new Rect(r.x + 18 * s, y, 200 * s, lh), rows[i, 0], _key);
                GUI.Label(new Rect(r.x + 220 * s, y, w - 230 * s, lh), rows[i, 1], _text);
            }
        }
    }
}
