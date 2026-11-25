using System.Collections.Generic;
using TaxiSim.Core;
using TaxiSim.Core.Dispatch;
using TaxiSim.Core.Sim;
using UnityEngine;

namespace TaxiSim.World
{
    /// <summary>
    /// The dispatcher's screen on the laptop while a shift runs (IMGUI, scales with resolution):
    ///  * top bar: clock and speed, cash, satisfaction, your price vs RapidCab, console link status;
    ///  * left: the console's current page (Dispatch / Fleet / Money / Events) with the same selection as the TFT,
    ///    plus the utility breakdown of the suggested taxi (game-theory scoring made visible);
    ///  * right: the fleet with battery bars;  bottom: the event ticker;  centre: toasts and the end-of-shift card;
    ///  * labels over the fleet taxis and the passenger pins in the 3D city.
    /// Rows can be clicked with the mouse; everything else is driven by the console or the keyboard.
    /// </summary>
    public sealed class DispatchHud : MonoBehaviour
    {
        public SimulationDriver Game;
        public ConsoleBridge Bridge;
        public Camera Cam;
        public TrafficAmbient Traffic;
        public GameMenu Menu;
        public WorldOverlay Overlay;

        private struct ToastItem { public string Text; public Color Color; public float Until; }
        private readonly List<ToastItem> _toasts = new List<ToastItem>();

        private GUIStyle _panel, _title, _text, _small, _mono, _big, _tag, _row, _label;
        private Texture2D _panelTex, _accentTex, _white, _rowSel, _rowHover;
        private float _s = 1f;

        private static readonly string[] PageTabs = { "DISPATCH", "FLEET", "MONEY", "EVENTS" };

        public void ShowToast(RowColor c, string text)
        {
            _toasts.Add(new ToastItem { Text = text, Color = ColorOf(c), Until = Time.unscaledTime + 3.5f });
            if (_toasts.Count > 4) _toasts.RemoveAt(0);
        }

        // ================================================================== styles

        private void EnsureStyles()
        {
            if (_panel != null) return;
            _panelTex = Solid(new Color(0.03f, 0.06f, 0.1f, 0.84f));
            _accentTex = Solid(new Color(0.2f, 0.85f, 1f, 1f));
            _white = Solid(Color.white);
            _rowSel = Solid(new Color(0.2f, 0.75f, 1f, 0.28f));
            _rowHover = Solid(new Color(1f, 1f, 1f, 0.07f));
            _panel = new GUIStyle { normal = { background = _panelTex } };
            _title = new GUIStyle { fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.55f, 0.95f, 1f) } };
            _text = new GUIStyle { richText = true, normal = { textColor = new Color(0.92f, 0.95f, 1f) } };
            _small = new GUIStyle { richText = true, wordWrap = true, normal = { textColor = new Color(0.68f, 0.76f, 0.86f) } };
            _mono = new GUIStyle { richText = true, font = Font.CreateDynamicFontFromOSFont(new[] { "Consolas", "Cascadia Mono", "Courier New" }, 14), normal = { textColor = Color.white } };
            _big = new GUIStyle { fontStyle = FontStyle.Bold, richText = true, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            _tag = new GUIStyle { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            _row = new GUIStyle { richText = true, clipping = TextClipping.Clip, normal = { textColor = Color.white } };
            _label = new GUIStyle { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
        }

        private void Scale(float s)
        {
            _title.fontSize = Mathf.RoundToInt(17 * s);
            _text.fontSize = Mathf.RoundToInt(15 * s);
            _small.fontSize = Mathf.RoundToInt(13 * s);
            _mono.fontSize = Mathf.RoundToInt(14 * s);
            _big.fontSize = Mathf.RoundToInt(30 * s);
            _tag.fontSize = Mathf.RoundToInt(13 * s);
            _row.fontSize = Mathf.RoundToInt(14 * s);
            _label.fontSize = Mathf.RoundToInt(12 * s);
        }

        private static Texture2D Solid(Color c)
        {
            var t = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        public static Color ColorOf(RowColor c)
        {
            switch (c)
            {
                case RowColor.Grey: return new Color(0.62f, 0.68f, 0.76f);
                case RowColor.Green: return new Color(0.35f, 1f, 0.55f);
                case RowColor.Yellow: return new Color(1f, 0.88f, 0.3f);
                case RowColor.Orange: return new Color(1f, 0.6f, 0.2f);
                case RowColor.Red: return new Color(1f, 0.32f, 0.3f);
                case RowColor.Cyan: return new Color(0.35f, 0.92f, 1f);
                case RowColor.Blue: return new Color(0.45f, 0.65f, 1f);
                case RowColor.Magenta: return new Color(1f, 0.45f, 0.95f);
                default: return Color.white;
            }
        }

        private static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

        // ================================================================== drawing

        private void OnGUI()
        {
            if (Game == null) return;
            EnsureStyles();
            _s = Mathf.Max(0.75f, Screen.height / 1080f);
            Scale(_s);
            Simulation sim = Game.Sim;
            bool menuOpen = Menu != null && Menu.IsOpen;
            if (Overlay != null && Overlay.HideAll) return;

            if (sim == null)
            {
                if (!menuOpen) StartHint();
                return;
            }
            if (menuOpen) return;

            DispatchConsole con = Game.Console;
            WorldLabels(sim, con);
            TopBar(sim, con);
            LeftPanel(sim, con);
            FleetPanel(sim, con);
            Ticker(sim);
            Toasts();
            if (con.Paused) Banner("PAUSED", "hold BACK on the console  ·  P on the keyboard");
            if (sim.IsOver) GameOver(sim);
            ControlsHint(con);
        }

        private void StartHint()
        {
            float s = _s;
            var r = new Rect(Screen.width * 0.5f - 250 * s, Screen.height - 70 * s, 500 * s, 40 * s);
            GUI.Box(r, GUIContent.none, _panel);
            GUI.DrawTexture(new Rect(r.x, r.y, 4 * s, r.height), _accentTex);
            GUI.Label(new Rect(r.x + 16 * s, r.y + 10 * s, r.width, 22 * s),
                $"<b>Enter</b> or console <b>ACCEPT</b>: start a dispatch shift   ·   {LinkText()}", _text);
        }

        private string LinkText()
        {
            if (Bridge == null || Bridge.Link == null) return "<color=#9aa6b2>console: keyboard only</color>";
            SerialLink l = Bridge.Link;
            switch (l.State)
            {
                case SerialLink.LinkState.Connected: return $"<color=#5cff8a>console on {l.PortName}</color>";
                case SerialLink.LinkState.Searching: return "<color=#ffd24a>looking for the console…</color>";
                case SerialLink.LinkState.Unsupported: return "<color=#ff8a5c>serial off (run Taxi Manager → Setup)</color>";
                default: return "<color=#9aa6b2>keyboard</color>";
            }
        }

        private void TopBar(Simulation sim, DispatchConsole con)
        {
            float s = _s, w = 900 * s, h = 46 * s;
            var r = new Rect(Screen.width * 0.5f - w * 0.5f, 12 * s, w, h);
            GUI.Box(r, GUIContent.none, _panel);
            GUI.DrawTexture(new Rect(r.x, r.yMax - 3 * s, r.width, 3 * s), _accentTex);
            float x = r.x + 16 * s, y = r.y + 12 * s;
            string speed = con.Paused ? "<color=#ffd24a>❚❚ paused</color>" : $"▶ {con.Speed}×";
            Label(ref x, y, $"<b>{sim.ClockText}</b>  {speed}", 150);
            string cashCol = sim.Cash < 0 ? "#ff6a5c" : "#5cff8a";
            Label(ref x, y, $"Cash <b><color={cashCol}>₹{sim.Cash:#,0}</color></b>", 175);
            // satisfaction bar
            GUI.Label(new Rect(x, y, 60 * s, 22 * s), "Happy", _text);
            var bar = new Rect(x + 56 * s, y + 5 * s, 110 * s, 12 * s);
            GUI.color = new Color(1, 1, 1, 0.15f); GUI.DrawTexture(bar, _white);
            float sat = (float)sim.Satisfaction / 100f;
            GUI.color = sat < 0.35f ? new Color(1f, 0.35f, 0.3f) : sat < 0.6f ? new Color(1f, 0.8f, 0.3f) : new Color(0.35f, 1f, 0.55f);
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * sat, bar.height), _white);
            GUI.color = Color.white;
            GUI.Label(new Rect(bar.xMax + 6 * s, y, 50 * s, 22 * s), $"{sim.Satisfaction:0}%", _text);
            x += 230 * s;
            Label(ref x, y, $"Price <b>×{SurgePricing.Multiplier(sim.SurgeLevel):0.00}</b>  <color=#ffb35c>RapidCab ×{SurgePricing.Multiplier(sim.RivalSurgeLevel):0.00}</color>", 255);
            GUI.Label(new Rect(x, y, r.xMax - x - 10 * s, 22 * s), LinkText(), _small);
        }

        private void Label(ref float x, float y, string text, float width)
        {
            GUI.Label(new Rect(x, y, width * _s, 22 * _s), text, _text);
            x += width * _s;
        }

        // ------------------------------------------------------------------ left panel (mirrors the console page)

        private void LeftPanel(Simulation sim, DispatchConsole con)
        {
            float s = _s, w = 520 * s;
            float h = Mathf.Min(Screen.height - 250 * s, 640 * s);
            var r = new Rect(16 * s, 72 * s, w, h);
            GUI.Box(r, GUIContent.none, _panel);
            GUI.DrawTexture(new Rect(r.x, r.y, 4 * s, r.height), _accentTex);

            // page tabs
            float tx = r.x + 14 * s, tw = (w - 28 * s) / 4f;
            for (int i = 0; i < 4; i++)
            {
                var tr = new Rect(tx + i * tw, r.y + 10 * s, tw - 4 * s, 26 * s);
                bool on = (int)con.Page == i && !con.CameraMode;
                GUI.color = on ? new Color(0.2f, 0.75f, 1f, 0.9f) : new Color(1f, 1f, 1f, 0.08f);
                GUI.DrawTexture(tr, _white);
                GUI.color = on ? Color.black : new Color(0.8f, 0.86f, 0.93f);
                GUI.Label(tr, PageTabs[i], _tag);
                GUI.color = Color.white;
                if (!on && GUI.Button(tr, GUIContent.none, GUIStyle.none)) con.ChangePage(i - (int)con.Page);
            }
            var body = new Rect(r.x + 14 * s, r.y + 46 * s, w - 28 * s, h - 56 * s);
            if (con.CameraMode) { CameraHelp(body); return; }
            switch (con.Page)
            {
                case ConsolePage.Dispatch: DispatchBody(sim, con, body); break;
                case ConsolePage.Fleet: FleetBody(sim, con, body); break;
                case ConsolePage.Finance: MoneyBody(sim, con, body); break;
                case ConsolePage.Events: EventsBody(sim, con, body); break;
            }
        }

        private void CameraHelp(Rect b)
        {
            float y = b.y;
            GUI.Label(new Rect(b.x, y, b.width, 24 * _s), "CAMERA MODE (from the console)", _title);
            y += 34 * _s;
            foreach (string line in new[] { "Arrows: move the camera", "Knob: zoom", "ACCEPT / REJECT: turn left / right", "Knob press: follow the next taxi", "BACK or hold KNOB: back to dispatch" })
            {
                GUI.Label(new Rect(b.x, y, b.width, 22 * _s), line, _text);
                y += 26 * _s;
            }
        }

        private void DispatchBody(Simulation sim, DispatchConsole con, Rect b)
        {
            float s = _s, y = b.y, lh = 25 * s;
            List<RideRequest> list = con.WaitingRequests();
            RideRequest sel = con.SelectedRequest();
            int assigned = 0;
            foreach (RideRequest q in sim.Requests) if (q.Status == RequestStatus.Assigned || q.Status == RequestStatus.PickedUp) assigned++;
            GUI.Label(new Rect(b.x, y, b.width, 24 * s), $"PASSENGERS WAITING  {list.Count}   <color=#9aa6b2>· {assigned} on their way</color>", _title);
            y += 30 * s;
            if (list.Count == 0)
            {
                GUI.Label(new Rect(b.x, y, b.width, 60 * s), "No one is waiting. Good moment to charge low taxis (Fleet page) or move idle ones to busy places.", _small);
                return;
            }
            int maxRows = Mathf.Clamp(Mathf.FloorToInt((b.height * 0.5f) / lh), 3, 10);
            int selIdx = Mathf.Max(0, list.IndexOf(sel));
            int first = Mathf.Clamp(selIdx - maxRows / 2, 0, Mathf.Max(0, list.Count - maxRows));
            for (int i = first; i < list.Count && i < first + maxRows; i++)
            {
                RideRequest q = list[i];
                var rr = new Rect(b.x, y, b.width, lh - 2 * s);
                Row(rr, q == sel);
                Color c = ColorOf(DispatchConsole.UrgencyColor(q));
                string urg = q.Urgency == Urgency.Medical ? "MEDICAL" : q.Urgency == Urgency.Priority ? "PRIORITY" : "";
                GUI.Label(new Rect(rr.x + 8 * s, rr.y + 3 * s, rr.width - 16 * s, lh), $"<color=#{Hex(c)}><b>#{q.Id}</b> {Node(sim, q.Origin)} → {Node(sim, q.Destination)}</color>", _row);
                GUI.Label(new Rect(rr.xMax - 205 * s, rr.y + 3 * s, 200 * s, lh),
                    $"<color=#{Hex(c)}>{urg}</color>  ₹{q.Fare * q.Surge:0}  <b>{Mathf.Max(0, (float)q.PatienceLeft):0}</b> min", _row);
                if (GUI.Button(rr, GUIContent.none, GUIStyle.none)) con.SelectRequest(q.Id);
                y += lh;
            }
            if (list.Count > first + maxRows) { GUI.Label(new Rect(b.x, y, b.width, 20 * s), $"… {list.Count - first - maxRows} more (DOWN)", _small); y += 20 * s; }

            // Candidate breakdown
            y += 10 * s;
            GUI.DrawTexture(new Rect(b.x, y, b.width, 1 * s), _accentTex);
            y += 8 * s;
            if (sel == null) return;
            List<DispatchCandidate> cands = con.FeasibleCandidates(sel);
            DispatchCandidate c0 = con.ChosenCandidate(sel);
            if (c0 == null)
            {
                List<DispatchCandidate> all = sim.Rank(sel.Id);
                GUI.Label(new Rect(b.x, y, b.width, 24 * s), "<color=#ff6a5c>NO TAXI CAN TAKE THIS REQUEST</color>", _title);
                y += 28 * s;
                foreach (DispatchCandidate d in all)
                {
                    if (y > b.yMax - 22 * s) break;
                    GUI.Label(new Rect(b.x, y, b.width, 22 * s), $"{sim.Taxis[d.TaxiId].Label}: <color=#ffb35c>{d.Reason}</color>", _small);
                    y += 20 * s;
                }
                return;
            }
            Taxi t = sim.Taxis[c0.TaxiId];
            bool best = cands.Count > 0 && cands[0].TaxiId == c0.TaxiId;
            GUI.Label(new Rect(b.x, y, b.width, 24 * s), $"SEND <color=#5cff8a>{t.Label}</color> {(best ? "(best)" : "(your choice)")}  ·  ETA {c0.EtaMinutes:0} min  ·  battery {t.Battery:0}% → {t.Battery - c0.EnergyNeeded:0}%", _title);
            y += 30 * s;
            double eW = sim.Config.UtilityEnergyWeight, tW = sim.Config.UtilityTimeWeight, aW = sim.Config.UtilityAbandonWeight, sW = sim.Config.UtilitySatisfactionWeight;
            string[,] terms =
            {
                { "Fare × surge", $"+₹{c0.Revenue:0}" },
                { "Energy cost", $"−₹{eW * c0.EnergyCost:0}" },
                { "Waiting time × urgency", $"−{tW * c0.TimeCost:0}" },
                { $"Risk the rider gives up ({c0.AbandonRisk * 100:0}%)", $"−{aW * c0.AbandonRisk:0}" },
                { "Satisfaction effect", $"{(sW * c0.SatisfactionGain >= 0 ? "+" : "−")}{System.Math.Abs(sW * c0.SatisfactionGain):0}" },
            };
            for (int i = 0; i < terms.GetLength(0); i++)
            {
                GUI.Label(new Rect(b.x + 8 * s, y, b.width - 120 * s, 22 * s), terms[i, 0], _small);
                GUI.Label(new Rect(b.xMax - 110 * s, y, 110 * s, 22 * s), terms[i, 1], _mono);
                y += 20 * s;
            }
            GUI.Label(new Rect(b.x + 8 * s, y, b.width - 120 * s, 22 * s), "<b>Utility (higher = better)</b>", _text);
            GUI.Label(new Rect(b.xMax - 110 * s, y, 110 * s, 22 * s), $"<b>{c0.Utility:+0;−0}</b>", _mono);
            y += 28 * s;
            // other candidates (click to choose)
            GUI.Label(new Rect(b.x, y, b.width, 20 * s), "Other taxis (R-click, , . or click):", _small);
            y += 22 * s;
            float x = b.x;
            foreach (DispatchCandidate d in cands)
            {
                string txt = $"{sim.Taxis[d.TaxiId].Label} {d.EtaMinutes:0}m {d.Utility:+0;−0}";
                float bw = 150 * s;
                if (x + bw > b.xMax) { x = b.x; y += 28 * s; }
                if (y > b.yMax - 26 * s) break;
                var br = new Rect(x, y, bw - 6 * s, 24 * s);
                Row(br, d.TaxiId == c0.TaxiId);
                GUI.Label(new Rect(br.x + 6 * s, br.y + 3 * s, br.width, 20 * s), txt, _row);
                if (GUI.Button(br, GUIContent.none, GUIStyle.none)) con.ChooseTaxi(d.TaxiId);
                x += bw;
            }
        }

        private void FleetBody(Simulation sim, DispatchConsole con, Rect b)
        {
            float s = _s, y = b.y;
            Taxi t = sim.Taxis[con.SelectedTaxi];
            Color c = ColorOf(DispatchConsole.StateColor(t, sim.Config));
            GUI.Label(new Rect(b.x, y, b.width, 26 * s), $"<color=#{Hex(c)}>{t.Label}</color>  ·  {DispatchConsole.StateName(t.State)}  ·  {t.Battery:0}%", _title);
            y += 30 * s;
            GUI.Label(new Rect(b.x, y, b.width, 44 * s), con.TaxiDetail(t), _text);
            y += 30 * s;
            GUI.Label(new Rect(b.x, y, b.width, 22 * s), $"At / heading to: {Node(sim, t.IsMoving && t.TargetNode >= 0 ? t.TargetNode : t.Node)}", _small);
            y += 22 * s;
            GUI.Label(new Rect(b.x, y, b.width, 22 * s), $"Trips today {t.TripsCompleted}  ·  earned ₹{t.Earnings:0}  ·  driven {t.OdometerKm:0.0} km", _small);
            y += 34 * s;
            Command? cmd = con.SmartCommand(t, out string label);
            GUI.Label(new Rect(b.x, y, b.width, 24 * s), $"ACCEPT / Enter: <b>{(cmd != null ? label : "—")}</b>{(cmd == null ? $"  <color=#9aa6b2>({label})</color>" : "")}", _text);
            y += 26 * s;
            string spot = con.HotspotNode >= 0 ? sim.Graph.Node(con.HotspotNode).Name : "—";
            GUI.Label(new Rect(b.x, y, b.width, 24 * s), $"REJECT / Backspace: <b>move to {spot}</b>  <color=#9aa6b2>(R-click or , . to change)</color>", _text);
            y += 26 * s;
            GUI.Label(new Rect(b.x, y, b.width, 24 * s), "Hold R-click or / : follow this taxi with the camera", _small);
            y += 34 * s;
            GUI.Label(new Rect(b.x, y, b.width, 60 * s),
                "Taxis below 20% are flagged LOW. Each trip needs its energy plus a 10% reserve. Charging costs ₹2 per %. An empty battery means a ₹500 tow.", _small);
        }

        private void MoneyBody(Simulation sim, DispatchConsole con, Rect b)
        {
            float s = _s, y = b.y;
            ScoreCard sc = sim.Score();
            string[,] rows =
            {
                { "Cash", $"₹{sim.Cash:#,0}" },
                { "Revenue (fares)", $"₹{sim.Revenue:#,0}" },
                { "Costs (fees, charging)", $"−₹{sim.Costs:#,0}" },
                { "Penalties (late, abandoned, tows)", $"−₹{sim.Penalties:#,0}" },
                { "Trips completed / abandoned / rejected", $"{sim.Completed} / {sim.Abandoned} / {sim.Rejected}" },
                { "Lost to RapidCab / refused the price", $"{sim.LostToRival} / {sim.Declined}" },
                { "Fleet busy time (efficiency)", $"{sim.Efficiency * 100:0}%" },
                { "Score now", $"{sc.Score * 100:0} ({sc.Stars}★)" },
            };
            for (int i = 0; i < rows.GetLength(0); i++)
            {
                GUI.Label(new Rect(b.x, y, b.width - 150 * s, 22 * s), rows[i, 0], _text);
                GUI.Label(new Rect(b.xMax - 150 * s, y, 150 * s, 22 * s), rows[i, 1], _mono);
                y += 24 * s;
            }
            y += 10 * s;
            GUI.DrawTexture(new Rect(b.x, y, b.width, 1 * s), _accentTex);
            y += 10 * s;
            GUI.Label(new Rect(b.x, y, b.width, 24 * s), "PRICE WAR WITH RAPIDCAB", _title);
            y += 28 * s;
            bool changed = con.PendingSurge != sim.SurgeLevel;
            GUI.Label(new Rect(b.x, y, b.width, 24 * s),
                $"Yours ×{SurgePricing.Multiplier(sim.SurgeLevel):0.00}   ·   RapidCab ×{SurgePricing.Multiplier(sim.RivalSurgeLevel):0.00}   ·   " +
                $"{(changed ? $"<color=#ffd24a>new ×{SurgePricing.Multiplier(con.PendingSurge):0.00} — ACCEPT to apply</color>" : "R-click / , . to pick a new price")}", _text);
            y += 28 * s;
            // Payoff table: your expected revenue per request for each of your prices vs RapidCab's current price
            float cw = (b.width - 120 * s) / SurgePricing.Levels.Length;
            GUI.Label(new Rect(b.x, y, 120 * s, 22 * s), "Your price", _small);
            for (int i = 0; i < SurgePricing.Levels.Length; i++)
                GUI.Label(new Rect(b.x + 120 * s + i * cw, y, cw, 22 * s), $"×{SurgePricing.Levels[i]:0.00}", i == con.PendingSurge ? _title : _small);
            y += 22 * s;
            GUI.Label(new Rect(b.x, y, 120 * s, 22 * s), "₹ per request", _small);
            int bestReply = SurgePricing.BestResponse(sim.Config, 150.0, sim.RivalSurgeLevel);
            for (int i = 0; i < SurgePricing.Levels.Length; i++)
            {
                double ev = SurgePricing.ExpectedRevenue(sim.Config, 150.0, i, sim.RivalSurgeLevel);
                string txt = i == bestReply ? $"<color=#5cff8a><b>{ev:0}</b></color>" : $"{ev:0}";
                GUI.Label(new Rect(b.x + 120 * s + i * cw, y, cw, 22 * s), txt, _mono);
            }
            y += 26 * s;
            var ne = SurgePricing.NashEquilibria(sim.Config, 150.0);
            var parts = new List<string>();
            foreach (var p in ne) parts.Add($"you ×{SurgePricing.Multiplier(p.player):0.00} / them ×{SurgePricing.Multiplier(p.rival):0.00}");
            GUI.Label(new Rect(b.x, y, b.width, 44 * s),
                $"Green = best reply to RapidCab's price. Nash equilibrium (neither side gains by changing alone): {(parts.Count > 0 ? string.Join("; ", parts) : "none in pure prices")}. " +
                "Higher prices earn more per trip but more riders refuse or go to RapidCab, and satisfaction drops above ×1.75.", _small);
        }

        private void EventsBody(Simulation sim, DispatchConsole con, Rect b)
        {
            float s = _s, y = b.y;
            GUI.Label(new Rect(b.x, y, b.width, 24 * s), "ACTIVE CITY EVENTS", _title);
            y += 28 * s;
            int n = 0;
            foreach (TrafficEvent ev in sim.Events)
            {
                if (!ev.Started || ev.Ended) continue;
                double left = (ev.EndTick - sim.Tick) * sim.Config.MinutesPerTick;
                GUI.Label(new Rect(b.x, y, b.width, 22 * s), $"<color=#ff8af0>●</color> {ev.Description}  <color=#9aa6b2>({left:0} min left)</color>", _text);
                y += 24 * s;
                n++;
            }
            if (n == 0) { GUI.Label(new Rect(b.x, y, b.width, 22 * s), "<color=#5cff8a>All clear.</color>", _text); y += 24 * s; }
            int blocked = 0;
            foreach (Taxi t in sim.Taxis) if (t.State == TaxiState.Delayed && t.NeedsReroute) blocked++;
            if (blocked > 0) { GUI.Label(new Rect(b.x, y, b.width, 22 * s), $"<color=#ffb35c>{blocked} taxi(s) blocked by a closure — ACCEPT / Enter reroutes them</color>", _text); y += 26 * s; }
            y += 8 * s;
            GUI.Label(new Rect(b.x, y, b.width, 24 * s), "LOG", _title);
            y += 26 * s;
            int skip = con.EventScroll;
            for (int i = sim.Log.Count - 1; i >= 0 && y < b.yMax - 20 * s; i--)
            {
                if (skip-- > 0) continue;
                SimMessage m = sim.Log[i];
                GUI.Label(new Rect(b.x, y, b.width, 20 * s), $"<color=#{Hex(MessageColor(m.Kind))}>{Clock(sim, m.Tick)}  {m.Text}</color>", _small);
                y += 19 * s;
            }
        }

        // ------------------------------------------------------------------ fleet panel (always visible)

        private void FleetPanel(Simulation sim, DispatchConsole con)
        {
            float s = _s, w = 400 * s, lh = 46 * s;
            var r = new Rect(Screen.width - w - 16 * s, 72 * s, w, 40 * s + lh * sim.Taxis.Count);
            GUI.Box(r, GUIContent.none, _panel);
            GUI.DrawTexture(new Rect(r.xMax - 4 * s, r.y, 4 * s, r.height), _accentTex);
            int busy = 0;
            foreach (Taxi t in sim.Taxis) if (t.State == TaxiState.ToPickup || t.State == TaxiState.OnRide) busy++;
            GUI.Label(new Rect(r.x + 14 * s, r.y + 10 * s, w, 24 * s), $"AT FLEET  <color=#9aa6b2>{busy}/{sim.Taxis.Count} on jobs</color>", _title);
            float y = r.y + 40 * s;
            for (int i = 0; i < sim.Taxis.Count; i++)
            {
                Taxi t = sim.Taxis[i];
                var rr = new Rect(r.x + 8 * s, y, w - 20 * s, lh - 4 * s);
                Row(rr, con.Page == ConsolePage.Fleet && con.SelectedTaxi == i);
                Color c = ColorOf(DispatchConsole.StateColor(t, sim.Config));
                GUI.Label(new Rect(rr.x + 8 * s, rr.y + 3 * s, 200 * s, 20 * s), $"<b><color=#{Hex(c)}>{t.Label}</color></b>  {DispatchConsole.StateName(t.State)}", _row);
                string where = t.IsMoving && t.TargetNode >= 0 ? "→ " + Node(sim, t.TargetNode) : Node(sim, t.Node);
                GUI.Label(new Rect(rr.x + 8 * s, rr.y + 22 * s, 260 * s, 20 * s), $"<color=#9aa6b2>{where}</color>", _row);
                // battery bar
                var bar = new Rect(rr.xMax - 110 * s, rr.y + 8 * s, 70 * s, 10 * s);
                GUI.color = new Color(1, 1, 1, 0.15f); GUI.DrawTexture(bar, _white);
                float bat = Mathf.Clamp01((float)t.Battery / 100f);
                GUI.color = bat < 0.2f ? new Color(1f, 0.35f, 0.3f) : bat < 0.4f ? new Color(1f, 0.8f, 0.3f) : new Color(0.35f, 1f, 0.55f);
                GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * bat, bar.height), _white);
                GUI.color = Color.white;
                GUI.Label(new Rect(bar.xMax + 6 * s, rr.y + 3 * s, 40 * s, 20 * s), $"{t.Battery:0}%", _row);
                if (t.IsLowBattery(sim.Config)) GUI.Label(new Rect(bar.x, rr.y + 22 * s, 100 * s, 20 * s), "<color=#ff6a5c>LOW</color>", _row);
                if (GUI.Button(rr, GUIContent.none, GUIStyle.none)) con.SelectTaxi(i);
                y += lh;
            }
        }

        private void Ticker(Simulation sim)
        {
            float s = _s, w = Mathf.Min(760 * s, Screen.width - 460 * s), lh = 20 * s;
            int n = 5;
            var r = new Rect(16 * s, Screen.height - n * lh - 58 * s, w, n * lh + 16 * s);
            GUI.Box(r, GUIContent.none, _panel);
            float y = r.y + 8 * s;
            int start = Mathf.Max(0, sim.Log.Count - n);
            for (int i = start; i < sim.Log.Count; i++)
            {
                SimMessage m = sim.Log[i];
                GUI.Label(new Rect(r.x + 12 * s, y, r.width - 20 * s, lh), $"<color=#{Hex(MessageColor(m.Kind))}>{Clock(sim, m.Tick)}  {m.Text}</color>", _small);
                y += lh;
            }
        }

        private void Toasts()
        {
            float s = _s, y = 70 * s;
            for (int i = _toasts.Count - 1; i >= 0; i--)
                if (Time.unscaledTime > _toasts[i].Until) _toasts.RemoveAt(i);
            foreach (ToastItem t in _toasts)
            {
                float w = 620 * s;
                var r = new Rect(Screen.width * 0.5f - w * 0.5f, y, w, 30 * s);
                GUI.Box(r, GUIContent.none, _panel);
                GUI.color = t.Color;
                GUI.DrawTexture(new Rect(r.x, r.y, 4 * s, r.height), _white);
                GUI.color = Color.white;
                GUI.Label(new Rect(r.x + 14 * s, r.y + 6 * s, w - 20 * s, 22 * s), $"<color=#{Hex(t.Color)}>{t.Text}</color>", _text);
                y += 34 * s;
            }
        }

        private void Banner(string title, string sub)
        {
            float s = _s, w = 460 * s;
            var r = new Rect(Screen.width * 0.5f - w * 0.5f, Screen.height * 0.38f, w, 90 * s);
            GUI.Box(r, GUIContent.none, _panel);
            GUI.Label(new Rect(r.x, r.y + 12 * s, w, 40 * s), title, _big);
            GUI.Label(new Rect(r.x, r.y + 56 * s, w, 22 * s), $"<color=#9aa6b2>{sub}</color>", _big);
        }

        private void GameOver(Simulation sim)
        {
            ScoreCard sc = sim.Score();
            float s = _s, w = 560 * s, h = 360 * s;
            var r = new Rect(Screen.width * 0.5f - w * 0.5f, Screen.height * 0.5f - h * 0.5f, w, h);
            GUI.Box(r, GUIContent.none, _panel);
            GUI.DrawTexture(new Rect(r.x, r.y, w, 4 * s), _accentTex);
            string head = sc.Outcome == Outcome.ShiftComplete ? "SHIFT COMPLETE" : sc.Outcome == Outcome.Bankrupt ? "BANKRUPT" : "LICENCE CANCELLED";
            GUI.Label(new Rect(r.x, r.y + 18 * s, w, 40 * s), head, _big);
            GUI.Label(new Rect(r.x, r.y + 62 * s, w, 44 * s), $"<color=#ffd24a>{new string('★', sc.Stars)}</color><color=#55606c>{new string('★', 5 - sc.Stars)}</color>", _big);
            float y = r.y + 120 * s;
            string[] lines =
            {
                $"Score {sc.Score * 100:0} / 100   (profit 50% · satisfaction 30% · efficiency 20%)",
                $"Profit ₹{sc.Profit:#,0}   ·   Satisfaction {sc.Satisfaction:0}%   ·   Efficiency {sc.Efficiency * 100:0}%",
                $"Trips {sc.Completed}   ·   abandoned {sc.Abandoned}   ·   rejected {sc.Rejected}   ·   to RapidCab {sc.LostToRival}",
                $"Seed {sim.Seed}  ·  {sim.Difficulty}  ·  {sim.CommandLog.Count} decisions logged (replayable save)",
            };
            foreach (string line in lines)
            {
                GUI.Label(new Rect(r.x + 30 * s, y, w - 60 * s, 24 * s), line, _text);
                y += 30 * s;
            }
            GUI.Label(new Rect(r.x, r.yMax - 52 * s, w, 30 * s), "<color=#5ff0ff>ACCEPT / Enter: new shift   ·   Esc: menu</color>", _big);
        }

        private void ControlsHint(DispatchConsole con)
        {
            float s = _s;
            string txt = con.CameraMode ? "Camera mode — console arrows move, knob zooms, BACK returns" :
                "↑↓ select  ← → page  Enter accept  Backspace reject  Del back  , . next  / show  P pause  − = speed  ·  WASD + mouse camera";
            GUI.Label(new Rect(16 * s, Screen.height - 30 * s, Screen.width - 32 * s, 22 * s), $"<color=#9aa6b2>{txt}</color>", _small);
        }

        // ------------------------------------------------------------------ labels in the 3D city

        private void WorldLabels(Simulation sim, DispatchConsole con)
        {
            if (Cam == null) return;
            float s = _s;
            if (Traffic != null)
            {
                for (int i = 0; i < sim.Taxis.Count && i < Traffic.Fleet.Count; i++)
                {
                    Taxi t = sim.Taxis[i];
                    Vector3 sp = Cam.WorldToScreenPoint(Traffic.Fleet[i].Transform.position + Vector3.up * 1.2f);
                    if (sp.z <= 0f || sp.z > 700f) continue;
                    Color c = ColorOf(DispatchConsole.StateColor(t, sim.Config));
                    string txt = $"{t.Label} {t.Battery:0}%";
                    var rect = new Rect(sp.x - 48 * s, Screen.height - sp.y - 22 * s, 96 * s, 20 * s);
                    GUI.color = new Color(0f, 0f, 0f, 0.6f);
                    GUI.DrawTexture(rect, _white);
                    GUI.color = c;
                    GUI.DrawTexture(new Rect(rect.x, rect.yMax - 2 * s, rect.width, 2 * s), _white);
                    GUI.color = Color.white;
                    GUI.Label(rect, $"<color=#{Hex(c)}>{txt}</color>", _label);
                }
            }
            RideRequest sel = con.Page == ConsolePage.Dispatch ? sim.Request(con.SelectedRequestId) : null;
            var count = new Dictionary<int, int>();
            foreach (RideRequest r in sim.Requests)
            {
                if (r.Status != RequestStatus.Waiting) continue;
                count.TryGetValue(r.Origin, out int k);
                count[r.Origin] = k + 1;
                Vector3 sp = Cam.WorldToScreenPoint(Game.NodePos(r.Origin) + Vector3.up * (7.6f + k * 1.4f));
                if (sp.z <= 0f || sp.z > 900f) continue;
                Color c = ColorOf(DispatchConsole.UrgencyColor(r));
                bool on = sel != null && sel.Id == r.Id;
                var rect = new Rect(sp.x - 60 * s, Screen.height - sp.y - 10 * s, 120 * s, 20 * s);
                GUI.color = on ? new Color(0.1f, 0.5f, 0.7f, 0.85f) : new Color(0f, 0f, 0f, 0.55f);
                GUI.DrawTexture(rect, _white);
                GUI.color = Color.white;
                GUI.Label(rect, $"<color=#{Hex(c)}>#{r.Id} ₹{r.Fare * r.Surge:0} {Mathf.Max(0, (float)r.PatienceLeft):0}m</color>", _label);
            }
        }

        // ------------------------------------------------------------------ helpers

        private void Row(Rect r, bool selected)
        {
            if (selected) GUI.DrawTexture(r, _rowSel);
            else if (r.Contains(Event.current.mousePosition)) GUI.DrawTexture(r, _rowHover);
            if (selected) GUI.DrawTexture(new Rect(r.x, r.y, 3 * _s, r.height), _accentTex);
        }

        private static string Node(Simulation sim, int id) => id >= 0 ? sim.Graph.Node(id).ShortName : "—";

        private static string Clock(Simulation sim, int tick)
        {
            double h = sim.Config.ShiftStartHour + tick * sim.Config.MinutesPerTick / 60.0;
            int hh = (int)h, mm = (int)((h - hh) * 60.0);
            return $"{hh:00}:{mm:00}";
        }

        private static Color MessageColor(MessageKind k)
        {
            switch (k)
            {
                case MessageKind.Warning: return new Color(1f, 0.62f, 0.3f);
                case MessageKind.Event: return new Color(1f, 0.5f, 0.95f);
                case MessageKind.Money: return new Color(0.4f, 1f, 0.6f);
                case MessageKind.Request: return new Color(0.9f, 0.95f, 1f);
                case MessageKind.GameOver: return new Color(1f, 0.88f, 0.3f);
                default: return new Color(0.68f, 0.76f, 0.86f);
            }
        }
    }
}
