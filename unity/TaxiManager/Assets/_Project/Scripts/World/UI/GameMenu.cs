using UnityEngine;

namespace TaxiSim.World
{
    /// <summary>
    /// Title screen, pause menu (Esc) and settings, drawn with IMGUI in the same dark-glass style as the overlay.
    ///  * Title: the city slowly orbits behind the menu. Explore the city / Cinematic tour / Settings / Quit.
    ///    ("Start shift" joins the list once the dispatch game is connected.)
    ///  * Pause: freezes the simulation (time scale 0) and the sound; Resume / Settings / Main menu / Quit.
    ///  * Settings: volume, graphics tier, visual style, time of day, time-lapse, weather, festival,
    ///    monsoon flooding, minimap size and labels. Volume, minimap and flooding are remembered (PlayerPrefs);
    ///    the graphics tier is remembered by GraphicsQuality.
    /// </summary>
    public sealed class GameMenu : MonoBehaviour
    {
        private enum Screen2 { None, Title, Pause, Settings }

        public BkcWorldBootstrap World;
        public CityCameraRig Rig;
        public CinematicDirector Director;
        public WorldOverlay Overlay;
        public CitySoundscape Sound;
        public TimeOfDay Clock;
        public WeatherSystem Weather;
        public FestivalSystem Festivals;
        public FloodSystem Floods;
        public MinimapView Minimap;
        public bool ShowTitleOnStart = true;
        public SimulationDriver Game;

        public bool IsOpen => _screen != Screen2.None;
        public bool AtTitle => _screen == Screen2.Title;

        /// <summary>Closes whatever menu is open (used when the console starts a shift from the title screen).</summary>
        public void CloseAll() => Close();

        private Screen2 _screen = Screen2.None, _settingsReturn = Screen2.Pause;
        private float _savedTimeScale = 1f;
        private bool _paused;
        private GUIStyle _panel, _title, _subtitle, _button, _label, _small, _section;
        private Texture2D _panelTex, _accentTex, _shadeTex, _buttonTex, _buttonHover;
        private Vector2 _scroll;

        private void Start()
        {
            if (Sound != null) Sound.MasterVolume = PlayerPrefs.GetFloat("TaxiSim.Volume", 0.85f);
            if (Floods != null) Floods.Enabled = PlayerPrefs.GetInt("TaxiSim.Flooding", 1) == 1;
            if (ShowTitleOnStart) Open(Screen2.Title);
        }

        private void Open(Screen2 s)
        {
            if (s == Screen2.Pause && !_paused)
            {
                _paused = true;
                _savedTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
                Time.timeScale = 0f;
                AudioListener.pause = true;
            }
            _screen = s;
            if (Overlay != null) Overlay.Suppressed = true;
        }

        private void Close()
        {
            Unpause();
            _screen = Screen2.None;
            _settingsReturn = Screen2.Pause;
            if (Overlay != null) Overlay.Suppressed = Director != null && Director.Active;
        }

        private void Update()
        {
            if (_screen == Screen2.Title && Rig != null) Rig.Orbit(Time.unscaledDeltaTime * 3f, 0f);

            if (!InputCompat.Pressed(TsKey.Escape)) return;
            if (_screen == Screen2.Settings) { _screen = _settingsReturn; return; }
            if (_screen == Screen2.Pause) { Close(); return; }
            if (_screen != Screen2.None) return;
            // Esc first leaves cinematic mode, follow mode or the street camera; only then pauses
            if (Director != null && Director.Active) return;
            if (Rig != null && (Rig.FollowTarget != null || Rig.StreetMode)) return;
            Open(Screen2.Pause);
        }

        // ------------------------------------------------------------------ drawing

        private void OnGUI()
        {
            if (_screen == Screen2.None) return;
            EnsureStyles();
            float s = Mathf.Max(0.75f, UnityEngine.Screen.height / 1080f);
            ScaleStyles(s);
            GUI.depth = -10;

            switch (_screen)
            {
                case Screen2.Title: DrawTitle(s); break;
                case Screen2.Pause: DrawPause(s); break;
                case Screen2.Settings: DrawSettings(s); break;
            }
        }

        private void DrawTitle(float s)
        {
            float w = 520 * s;
            // Gradient shade on the left so the city stays visible on the right
            GUI.DrawTexture(new Rect(0, 0, w * 1.6f, UnityEngine.Screen.height), _shadeTex);
            float x = 70 * s, y = UnityEngine.Screen.height * 0.22f;
            GUI.DrawTexture(new Rect(x - 20 * s, y + 8 * s, 5 * s, 120 * s), _accentTex);
            GUI.Label(new Rect(x, y, w * 1.4f, 70 * s), "AUTONOMOUS\nTAXI MANAGER", _title);
            GUI.Label(new Rect(x, y + 140 * s, w * 1.4f, 30 * s), "Fleet dispatch simulation  ·  BKC → Kurla, Mumbai", _subtitle);
            y += 210 * s;
            if (Game != null)
            {
                if (Button(new Rect(x, y, 340 * s, 52 * s), "START DISPATCH SHIFT"))
                {
                    Close();
                    Game.BeginShift(Game.Console.MenuDifficulty);
                }
                y += 58 * s;
                string[] levels = { "Easy", "Normal", "Hard" };
                int d = Choice(new Rect(x, y, 340 * s, 40 * s), (int)Game.Console.MenuDifficulty, levels);
                if (d != (int)Game.Console.MenuDifficulty) Game.Console.SetMenuDifficulty((TaxiSim.Core.Sim.Difficulty)d);
                y += 56 * s;
            }
            if (Button(new Rect(x, y, 340 * s, 52 * s), "EXPLORE THE CITY")) Close();
            y += 64 * s;
            if (Button(new Rect(x, y, 340 * s, 52 * s), "CINEMATIC TOUR"))
            {
                Close();
                if (Director != null) Director.Begin();
            }
            y += 64 * s;
            if (Button(new Rect(x, y, 340 * s, 52 * s), "SETTINGS")) { _settingsReturn = Screen2.Title; _screen = Screen2.Settings; }
            y += 64 * s;
            if (Button(new Rect(x, y, 340 * s, 52 * s), "QUIT")) Quit();
            GUI.Label(new Rect(x, UnityEngine.Screen.height - 60 * s, w * 1.6f, 24 * s),
                "Pinnacle 1 · Project 15  ·  Tirth · Sarthak · Zain · Sahil   ·   Console: press ACCEPT to start a shift", _small);
        }

        private void DrawPause(float s)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(0, 0, UnityEngine.Screen.width, UnityEngine.Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            float w = 400 * s, h = 380 * s;
            var r = new Rect((UnityEngine.Screen.width - w) * 0.5f, (UnityEngine.Screen.height - h) * 0.5f, w, h);
            GUI.Box(r, GUIContent.none, _panel);
            GUI.DrawTexture(new Rect(r.x, r.y, 4 * s, r.height), _accentTex);
            GUI.Label(new Rect(r.x + 30 * s, r.y + 22 * s, w, 40 * s), "PAUSED", _section);
            float y = r.y + 80 * s, bx = r.x + 30 * s, bw = w - 60 * s;
            if (Button(new Rect(bx, y, bw, 50 * s), "RESUME")) Close();
            y += 62 * s;
            if (Button(new Rect(bx, y, bw, 50 * s), "SETTINGS")) { _settingsReturn = Screen2.Pause; _screen = Screen2.Settings; }
            y += 62 * s;
            if (Button(new Rect(bx, y, bw, 50 * s), "MAIN MENU"))
            {
                Unpause();
                if (Game != null) Game.EndShift();
                _screen = Screen2.Title;
            }
            y += 62 * s;
            if (Button(new Rect(bx, y, bw, 50 * s), "QUIT")) Quit();
        }

        private void DrawSettings(float s)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.5f);
            GUI.DrawTexture(new Rect(0, 0, UnityEngine.Screen.width, UnityEngine.Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            float w = 760 * s, h = Mathf.Min(820 * s, UnityEngine.Screen.height - 40 * s);
            var r = new Rect((UnityEngine.Screen.width - w) * 0.5f, (UnityEngine.Screen.height - h) * 0.5f, w, h);
            GUI.Box(r, GUIContent.none, _panel);
            GUI.DrawTexture(new Rect(r.x, r.y, 4 * s, r.height), _accentTex);
            GUI.Label(new Rect(r.x + 30 * s, r.y + 20 * s, w, 40 * s), "SETTINGS", _section);

            float lx = r.x + 30 * s, cx = r.x + 230 * s, cw = w - 260 * s, rowH = 54 * s;
            var view = new Rect(r.x, r.y + 70 * s, w, h - 140 * s);
            float contentH = rowH * 11;
            _scroll = GUI.BeginScrollView(view, _scroll, new Rect(view.x, view.y, w - 20 * s, contentH));
            float y = view.y;

            // Volume
            Row(lx, y, s, "Volume");
            if (Sound != null)
            {
                float v = GUI.HorizontalSlider(new Rect(cx, y + 18 * s, cw - 70 * s, 20 * s), Sound.MasterVolume, 0f, 1f);
                if (!Mathf.Approximately(v, Sound.MasterVolume)) { Sound.MasterVolume = v; PlayerPrefs.SetFloat("TaxiSim.Volume", v); }
                GUI.Label(new Rect(cx + cw - 60 * s, y + 10 * s, 60 * s, 30 * s), $"{Mathf.RoundToInt(Sound.MasterVolume * 100)}%", _label);
            }
            y += rowH;

            // Graphics tier
            Row(lx, y, s, "Graphics");
            if (World != null)
            {
                string[] tiers = { "Low", "Medium", "High", "Ultra", "Cinematic" };
                int t = Choice(new Rect(cx, y + 6 * s, cw, 40 * s), (int)World.CurrentTier, tiers);
                if (t != (int)World.CurrentTier) World.ApplyTier((GraphicsTier)t);
            }
            y += rowH;

            // Visual style
            Row(lx, y, s, "Look");
            if (World != null)
            {
                string[] styles = { "Auto", "Neon 2077", "Realistic" };
                int st = Choice(new Rect(cx, y + 6 * s, cw, 40 * s), (int)World.CurrentStyle, styles);
                if (st != (int)World.CurrentStyle) World.SetStyle((VisualStyle)st);
            }
            y += rowH;

            // Time of day
            Row(lx, y, s, "Time of day");
            if (Clock != null)
            {
                if (Game != null && Game.OwnsClock && Game.SkyFollowsShift)
                {
                    // During a shift the sky shows the shift time; one click frees it (looks only)
                    if (GUI.Button(new Rect(cx, y + 6 * s, cw - 110 * s, 40 * s), "Sky follows the shift clock - click to set it freely", _button))
                        Game.SkyFollowsShift = false;
                }
                else
                    Clock.Hour = GUI.HorizontalSlider(new Rect(cx, y + 18 * s, cw - 110 * s, 20 * s), Clock.Hour, 0f, 23.99f);
                int hh = Mathf.FloorToInt(Clock.Hour), mm = Mathf.FloorToInt((Clock.Hour - hh) * 60f);
                GUI.Label(new Rect(cx + cw - 100 * s, y + 10 * s, 100 * s, 30 * s), $"{hh:00}:{mm:00}", _label);
            }
            y += rowH;

            Row(lx, y, s, "Time-lapse");
            if (Clock != null) Clock.TimeLapse = Toggle(new Rect(cx, y + 6 * s, 200 * s, 40 * s), Clock.TimeLapse);
            y += rowH;

            // Weather
            Row(lx, y, s, "Weather");
            if (Weather != null)
            {
                string[] modes = { "Clear", "Haze", "Monsoon" };
                Weather.Mode = (WeatherMode)Choice(new Rect(cx, y + 6 * s, cw, 40 * s), (int)Weather.Mode, modes);
            }
            y += rowH;

            Row(lx, y, s, "Monsoon flooding");
            if (Floods != null)
            {
                bool f = Toggle(new Rect(cx, y + 6 * s, 200 * s, 40 * s), Floods.Enabled);
                if (f != Floods.Enabled) { Floods.Enabled = f; PlayerPrefs.SetInt("TaxiSim.Flooding", f ? 1 : 0); }
            }
            y += rowH;

            // Festival
            Row(lx, y, s, "Festival");
            if (Festivals != null)
            {
                string[] fests = { "None", "Ganpati", "Diwali" };
                int fe = Choice(new Rect(cx, y + 6 * s, cw, 40 * s), (int)Festivals.Active, fests);
                if (fe != (int)Festivals.Active) Festivals.SetFestival((Festival)fe);
            }
            y += rowH;

            Row(lx, y, s, "Minimap");
            if (Minimap != null)
            {
                string[] sizes = { "Off", "Small", "Large" };
                int m = Choice(new Rect(cx, y + 6 * s, cw, 40 * s), (int)Minimap.Mode, sizes);
                if (m != (int)Minimap.Mode) { Minimap.Mode = (MinimapView.Size)m; PlayerPrefs.SetInt("TaxiSim.Minimap", m); }
            }
            y += rowH;

            Row(lx, y, s, "Place labels");
            if (Overlay != null) Overlay.ShowLabels = Toggle(new Rect(cx, y + 6 * s, 200 * s, 40 * s), Overlay.ShowLabels);
            y += rowH;

            GUI.EndScrollView();

            if (Button(new Rect(r.x + w - 230 * s, r.yMax - 64 * s, 200 * s, 46 * s), "BACK")) _screen = _settingsReturn;
            GUI.Label(new Rect(r.x + 30 * s, r.yMax - 52 * s, w - 280 * s, 30 * s), "Esc = back", _small);
        }

        private void Unpause()
        {
            if (!_paused) return;
            _paused = false;
            Time.timeScale = _savedTimeScale;
            AudioListener.pause = false;
        }

        private void Row(float x, float y, float s, string text) => GUI.Label(new Rect(x, y + 10 * s, 200 * s, 30 * s), text, _label);

        private bool Button(Rect r, string text) => GUI.Button(r, text, _button);

        private int Choice(Rect r, int value, string[] options)
        {
            float bw = r.width / options.Length;
            for (int i = 0; i < options.Length; i++)
            {
                var br = new Rect(r.x + i * bw + 2, r.y, bw - 4, r.height);
                Color old = GUI.backgroundColor;
                if (i == value) GUI.backgroundColor = new Color(0.25f, 0.9f, 1f);
                if (GUI.Button(br, options[i], _button)) value = i;
                GUI.backgroundColor = old;
            }
            return value;
        }

        private bool Toggle(Rect r, bool value) => Choice(r, value ? 0 : 1, new[] { "On", "Off" }) == 0;

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ------------------------------------------------------------------ styles

        private void EnsureStyles()
        {
            if (_panel != null) return;
            _panelTex = Tex(new Color(0.05f, 0.07f, 0.09f, 0.92f));
            _accentTex = Tex(new Color(0.25f, 0.92f, 1f, 1f));
            _buttonTex = Tex(new Color(0.12f, 0.16f, 0.2f, 0.95f));
            _buttonHover = Tex(new Color(0.16f, 0.32f, 0.4f, 0.98f));
            _shadeTex = new Texture2D(64, 1) { wrapMode = TextureWrapMode.Clamp, name = "MenuShade" };
            for (int i = 0; i < 64; i++) _shadeTex.SetPixel(i, 0, new Color(0.02f, 0.03f, 0.05f, Mathf.Lerp(0.88f, 0f, i / 63f)));
            _shadeTex.Apply();

            _panel = new GUIStyle(GUI.skin.box) { normal = { background = _panelTex } };
            _title = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            _subtitle = new GUIStyle(GUI.skin.label) { normal = { textColor = new Color(0.6f, 0.95f, 1f) } };
            _section = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            _label = new GUIStyle(GUI.skin.label) { normal = { textColor = new Color(0.88f, 0.92f, 0.96f) } };
            _small = new GUIStyle(GUI.skin.label) { normal = { textColor = new Color(0.65f, 0.7f, 0.75f) } };
            _button = new GUIStyle(GUI.skin.button)
            {
                fontStyle = FontStyle.Bold,
                normal = { background = _buttonTex, textColor = Color.white },
                hover = { background = _buttonHover, textColor = Color.white },
                active = { background = _accentTex, textColor = Color.black },
                alignment = TextAnchor.MiddleCenter
            };
        }

        private void ScaleStyles(float s)
        {
            _title.fontSize = Mathf.RoundToInt(56 * s);
            _subtitle.fontSize = Mathf.RoundToInt(20 * s);
            _section.fontSize = Mathf.RoundToInt(28 * s);
            _label.fontSize = Mathf.RoundToInt(18 * s);
            _small.fontSize = Mathf.RoundToInt(13 * s);
            _button.fontSize = Mathf.RoundToInt(17 * s);
        }

        private static Texture2D Tex(Color c)
        {
            var t = new Texture2D(1, 1) { name = "MenuTex" };
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }
    }
}
