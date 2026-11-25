using System.Collections.Generic;
using TaxiSim.Core.Dispatch;
using TaxiSim.Core.Sim;
using UnityEngine;

namespace TaxiSim.World
{
    /// <summary>
    /// Connects the player's inputs to the DispatchConsole and the console's output to the hardware:
    ///  * ESP32 lines (buttons, encoder) and the keyboard stand-in → DispatchConsole.Handle;
    ///  * the console page → the TFT (only changed lines are sent, full refresh every 4 s and after a hello);
    ///  * cues → the ESP32 buzzer (Z|…) and, without a console, short UI beeps on the PC;
    ///  * toasts → the TFT footer and the HUD;  taxi states → the LED stick (L|…);
    ///  * the two joysticks (J,…) → the city camera: left stick moves over the map, right stick turns (x) and zooms (y).
    /// </summary>
    public sealed class ConsoleBridge : MonoBehaviour
    {
        public SimulationDriver Game;
        public DispatchHud Hud;
        public GameMenu Menu;
        public bool BeepsWithoutConsole = true;
        [Tooltip("Left stick: map speed multiplier.")] public float StickPanSpeed = 1f;
        [Tooltip("Right stick X: degrees per second at full tilt.")] public float StickTurnSpeed = 90f;
        [Tooltip("Right stick Y: zoom steps per second at full tilt.")] public float StickZoomSpeed = 2.5f;

        public SerialLink Link { get; private set; }
        public string FirmwareVersion { get; private set; } = "";

        private readonly Dictionary<string, string> _sent = new Dictionary<string, string>();
        private float _nextSend, _nextFull, _cueCooldown;
        private AudioSource _audio;
        private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        private string _lastPort = "";
        private Vector2 _stickL, _stickR;
        private float _stickAt;

        private void Start()
        {
            Link = new SerialLink();
            Link.Start(PlayerPrefs.GetString("TaxiSim.ConsolePort", ""));
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;
            _audio.volume = 0.35f;
            _audio.ignoreListenerPause = true;
        }

        private void OnDestroy() => Link?.Stop();
        private void OnApplicationQuit() => Link?.Stop();

        private void Update()
        {
            if (Game == null || Link == null) return;
            DispatchConsole con = Game.Console;
            bool menuOpen = Menu != null && Menu.IsOpen;
            bool title = Menu != null && Menu.AtTitle;

            // ---- 1. hardware console ----
            while (Link.TryRead(out string line))
            {
                if (!ConsoleProtocol.TryParse(line, out ConsoleInput input)) continue;
                if (input.Kind == ConsoleInputKind.Hello) { FirmwareVersion = input.Text; _sent.Clear(); continue; }
                if (input.Kind == ConsoleInputKind.KeepAlive) continue;
                if (input.Kind == ConsoleInputKind.Sticks)
                {
                    _stickL = new Vector2(input.LX, input.LY) / 100f;
                    _stickR = new Vector2(input.RX, input.RY) / 100f;
                    _stickAt = Time.unscaledTime;
                    continue;
                }
                if (menuOpen && !title) continue;                  // pause / settings menu owns the game
                if (title && con.Sim != null) continue;
                con.Handle(input);
            }
            if (Link.Connected && Link.PortName != _lastPort)
            {
                _lastPort = Link.PortName;
                PlayerPrefs.SetString("TaxiSim.ConsolePort", _lastPort);
                if (Hud != null) Hud.ShowToast(RowColor.Green, $"Dispatch console connected on {_lastPort}");
            }

            // ---- 1b. joysticks → camera (works in free-roam and during a shift; not while a menu is open) ----
            if (!Link.Connected || Time.unscaledTime - _stickAt > 0.4f) _stickL = _stickR = Vector2.zero; // link quiet: stop
            if (!menuOpen) DriveCamera(Game.Rig, Time.unscaledDeltaTime);

            // ---- 2. keyboard stand-in ----
            if (!menuOpen) Keyboard(con);
            else if (title && con.Sim == null && InputCompat.Pressed(TsKey.Enter)) con.Tap(ConsoleButton.Accept);

            // ---- 3. cues and toasts ----
            _cueCooldown -= Time.unscaledDeltaTime;
            if (con.Cues.Count > 0)
            {
                string cue = Pick(con.Cues);
                con.Cues.Clear();
                if (_cueCooldown <= 0f || cue != "click")
                {
                    if (Link.Connected) Link.Send(ConsoleProtocol.Buzz(cue));
                    else if (BeepsWithoutConsole) Beep(cue);
                    _cueCooldown = 0.08f;
                }
            }
            foreach (KeyValuePair<RowColor, string> t in con.Toasts)
            {
                if (Hud != null) Hud.ShowToast(t.Key, t.Value);
                if (Link.Connected) Link.Send(ConsoleProtocol.Toast(t.Key, t.Value));
            }
            con.Toasts.Clear();

            // ---- 4. screen → TFT ----
            if (Link.Connected && Time.unscaledTime >= _nextSend)
            {
                _nextSend = Time.unscaledTime + 0.12f;
                if (Time.unscaledTime >= _nextFull) { _sent.Clear(); _nextFull = Time.unscaledTime + 4f; }
                SendScreen(con);
            }
        }

        private void Keyboard(DispatchConsole con)
        {
            if (con.Sim == null)
            {
                if (InputCompat.Pressed(TsKey.Enter)) con.Tap(ConsoleButton.Accept); // start a shift from free-roam
                return;
            }
            if (InputCompat.Pressed(TsKey.Up)) con.Tap(ConsoleButton.Up);
            if (InputCompat.Pressed(TsKey.Down)) con.Tap(ConsoleButton.Down);
            if (InputCompat.Pressed(TsKey.Left)) con.Tap(ConsoleButton.Left);
            if (InputCompat.Pressed(TsKey.Right)) con.Tap(ConsoleButton.Right);
            if (InputCompat.Pressed(TsKey.Enter)) con.Tap(ConsoleButton.Accept);
            if (InputCompat.Pressed(TsKey.Backspace)) con.Tap(ConsoleButton.Reject);
            if (InputCompat.Pressed(TsKey.Delete)) con.Tap(ConsoleButton.Back);
            if (InputCompat.Pressed(TsKey.Slash)) con.Tap(ConsoleButton.Knob);
            if (InputCompat.Pressed(TsKey.I)) con.Tap(ConsoleButton.Info);
            if (InputCompat.Pressed(TsKey.Comma)) con.Handle(ConsoleInput.Turn(-1));
            if (InputCompat.Pressed(TsKey.Period)) con.Handle(ConsoleInput.Turn(1));
            if (InputCompat.Pressed(TsKey.P) && con.Running) con.HoldLong(ConsoleButton.Back);       // pause
            if (InputCompat.Pressed(TsKey.Minus) && con.Running) con.ChangeSpeed(-1);
            if (InputCompat.Pressed(TsKey.EqualSign) && con.Running) con.ChangeSpeed(1);
        }

        /// <summary>Left stick pans over the map, right stick X orbits, right stick Y zooms (up = closer).</summary>
        private void DriveCamera(CityCameraRig rig, float dt)
        {
            if (rig == null || rig.ExternalControl) return;
            if (_stickL != Vector2.zero)
            {
                Vector2 a = Curve(_stickL);
                if (a.sqrMagnitude > 1f) a.Normalize();
                rig.Pan(a * StickPanSpeed, dt);
            }
            if (_stickR.x != 0f) rig.Orbit(Curve(_stickR.x) * StickTurnSpeed * dt, 0f);
            if (_stickR.y != 0f) rig.Zoom(Curve(_stickR.y) * StickZoomSpeed * dt);
        }

        // Gentle response: small tilts move slowly, full tilt moves at full speed.
        private static float Curve(float v) => v * Mathf.Abs(v);
        private static Vector2 Curve(Vector2 v) => new Vector2(Curve(v.x), Curve(v.y));

        private static string Pick(List<string> cues)
        {
            string[] priority = { "over", "urgent", "err", "warn", "event", "new", "start", "ok", "coin", "click" };
            foreach (string p in priority) if (cues.Contains(p)) return p;
            return cues[cues.Count - 1];
        }

        private void SendScreen(DispatchConsole con)
        {
            ConsoleScreen s = con.BuildScreen();
            Put("S", ConsoleProtocol.Header(s.Clock, s.Cash, s.Satisfaction, s.Surge, s.Speed));
            Put("T", ConsoleProtocol.Title(s.Page, s.PageCount, s.Title));
            for (int i = 0; i < s.Rows.Length; i++) Put("R" + i, ConsoleProtocol.Row(i, s.Rows[i]));
            Put("F", ConsoleProtocol.Footer(s.Footer));
            Put("L", ConsoleProtocol.Leds(LedColours(con)));
        }

        private void Put(string key, string line)
        {
            if (_sent.TryGetValue(key, out string old) && old == line) return;
            _sent[key] = line;
            Link.Send(line);
        }

        /// <summary>LED0 link heartbeat · LED1–6 the six taxis by state · LED7 city event / game over.</summary>
        private uint[] LedColours(DispatchConsole con)
        {
            var c = new uint[8];
            c[0] = (Time.unscaledTime % 1f) < 0.15f ? 0x003300u : 0x000800u;
            Simulation sim = con.Sim;
            if (sim == null) { c[0] = 0x000033u; return c; }
            for (int i = 0; i < 6 && i < sim.Taxis.Count; i++)
            {
                Taxi t = sim.Taxis[i];
                bool blink = (Time.unscaledTime % 0.8f) < 0.4f;
                switch (t.State)
                {
                    case TaxiState.ToPickup: c[i + 1] = 0x403000u; break;
                    case TaxiState.OnRide: c[i + 1] = 0x001040u; break;
                    case TaxiState.ToCharger:
                    case TaxiState.Charging: c[i + 1] = 0x300030u; break;
                    case TaxiState.Delayed: c[i + 1] = blink ? 0x401800u : 0u; break;
                    case TaxiState.Offline: c[i + 1] = blink ? 0x400000u : 0u; break;
                    default: c[i + 1] = t.IsLowBattery(sim.Config) ? 0x401800u : 0x004010u; break;
                }
            }
            bool events = false;
            foreach (TrafficEvent ev in sim.Events) if (ev.Started && !ev.Ended) { events = true; break; }
            c[7] = sim.IsOver ? 0x404000u : events ? 0x300030u : 0u;
            return c;
        }

        // ------------------------------------------------------------------ PC beeps (no console plugged in)

        private void Beep(string cue)
        {
            if (_audio == null) return;
            if (!_clips.TryGetValue(cue, out AudioClip clip))
            {
                clip = MakeCue(cue);
                _clips[cue] = clip;
            }
            if (clip != null) _audio.PlayOneShot(clip);
        }

        private static AudioClip MakeCue(string cue)
        {
            float[][] notes; // (frequency, seconds)
            switch (cue)
            {
                case "ok": notes = new[] { new[] { 988f, 0.06f }, new[] { 1319f, 0.09f } }; break;
                case "err": notes = new[] { new[] { 330f, 0.12f }, new[] { 247f, 0.16f } }; break;
                case "new": notes = new[] { new[] { 1175f, 0.07f }, new[] { 1568f, 0.08f } }; break;
                case "urgent": notes = new[] { new[] { 1760f, 0.08f }, new[] { 0f, 0.05f }, new[] { 1760f, 0.08f }, new[] { 0f, 0.05f }, new[] { 1760f, 0.12f } }; break;
                case "event": notes = new[] { new[] { 659f, 0.1f }, new[] { 523f, 0.14f } }; break;
                case "warn": notes = new[] { new[] { 440f, 0.12f }, new[] { 0f, 0.05f }, new[] { 440f, 0.12f } }; break;
                case "coin": notes = new[] { new[] { 1319f, 0.05f }, new[] { 1976f, 0.12f } }; break;
                case "over": notes = new[] { new[] { 784f, 0.15f }, new[] { 659f, 0.15f }, new[] { 523f, 0.3f } }; break;
                case "start": notes = new[] { new[] { 523f, 0.08f }, new[] { 659f, 0.08f }, new[] { 784f, 0.14f } }; break;
                default: notes = new[] { new[] { 2000f, 0.012f } }; break; // click
            }
            const int rate = 22050;
            int total = 0;
            foreach (float[] n in notes) total += Mathf.CeilToInt(n[1] * rate);
            var data = new float[total];
            int at = 0;
            foreach (float[] n in notes)
            {
                int len = Mathf.CeilToInt(n[1] * rate);
                for (int i = 0; i < len; i++)
                {
                    float env = Mathf.Min(1f, Mathf.Min(i / 220f, (len - i) / 440f));
                    data[at + i] = n[0] > 0f ? Mathf.Sin(2f * Mathf.PI * n[0] * i / rate) * 0.5f * env : 0f;
                }
                at += len;
            }
            AudioClip clip = AudioClip.Create("cue_" + cue, total, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
