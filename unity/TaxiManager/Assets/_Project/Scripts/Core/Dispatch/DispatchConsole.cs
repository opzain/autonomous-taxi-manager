using System;
using System.Collections.Generic;
using TaxiSim.Core.Sim;

namespace TaxiSim.Core.Dispatch
{
    public enum ConsolePage
    {
        Dispatch,
        Fleet,
        Finance,
        Events
    }

    public sealed class ConsoleRow
    {
        public string Text = "";
        public RowColor Color = RowColor.White;
        public bool Selected;
        public bool Large;

        public ConsoleRow() { }

        public ConsoleRow(string text, RowColor color = RowColor.White, bool selected = false, bool large = false)
        {
            Text = text ?? "";
            Color = color;
            Selected = selected;
            Large = large;
        }
    }

    /// <summary>What the TFT (and the HUD's console mirror) shows right now.</summary>
    public sealed class ConsoleScreen
    {
        public string Clock = "--:--", Cash = "", Satisfaction = "", Surge = "", Speed = "";
        public int Page, PageCount = 1;
        public string Title = "";
        public readonly ConsoleRow[] Rows = new ConsoleRow[ConsoleProtocol.Rows];
        public string Footer = "";

        public ConsoleScreen()
        {
            for (int i = 0; i < Rows.Length; i++) Rows[i] = new ConsoleRow();
        }
    }

    /// <summary>
    /// The dispatch console's brain, engine-free so it is unit-tested with the simulation. It turns button /
    /// encoder input (from the ESP32 or the keyboard) into simulation commands, keeps the selection
    /// (request, candidate taxi, fleet taxi, price), builds the TFT pages and tells the Unity side what to
    /// do with the camera, the clock speed and the buzzer.
    ///
    /// Controls (docs/03-hardware.md §9 and the in-game help):
    ///   LEFT / RIGHT           change page (Dispatch · Fleet · Money · Events)      long press: slower / faster
    ///   UP / DOWN              select a request / taxi / scroll
    ///   KNOB turn              Dispatch: choose the taxi · Fleet: choose where to send it · Money: choose the price
    ///   KNOB press             show the selection in the 3D city              long press: camera mode
    ///   ACCEPT                 assign · charge / reroute / release · apply price
    ///   REJECT                 decline the request · send the taxi to the chosen spot
    ///   BACK                   back to the best taxi                           long press: pause
    ///   INFO                   help line for this page                         long press: camera home view
    ///   NEXT (right-stick click, v0.15: replaces the knob)  next taxi / spot / price / scroll   long: show it in the city
    ///   Camera mode            arrows pan · knob zooms · ACCEPT / REJECT turn · knob press follows a taxi · BACK leaves
    /// </summary>
    public sealed class DispatchConsole
    {
        public static readonly int[] Speeds = { 1, 2, 4, 8 };
        public const int PageCount = 4;
        private static readonly string[] PageNames = { "DISPATCH", "FLEET", "MONEY & PRICE", "CITY EVENTS" };

        public Simulation Sim { get; private set; }
        public ConsolePage Page { get; private set; } = ConsolePage.Dispatch;
        public bool CameraMode { get; private set; }
        public bool Paused { get; set; }
        public int SpeedIndex { get; set; }
        public int Speed => Speeds[Math.Max(0, Math.Min(Speeds.Length - 1, SpeedIndex))];

        // ---- selection ----
        public int SelectedRequestId { get; private set; } = -1;
        /// <summary>Taxi chosen with the knob for the selected request; -1 = the best one.</summary>
        public int ChosenTaxiId { get; private set; } = -1;
        public int SelectedTaxi { get; private set; }
        public int PendingSurge { get; private set; }
        public int RelocateIndex { get; private set; }
        public int EventScroll { get; private set; }
        public Difficulty MenuDifficulty { get; private set; } = Difficulty.Normal;

        // ---- requests to the Unity side (read, then cleared by the caller) ----
        public bool StartRequested;
        public int ZoomSteps;
        public bool FollowNextRequested;
        /// <summary>INFO long press: fly the city camera back to the home view.</summary>
        public bool HomeRequested;
        public int FocusNodeRequest = -1;
        public int FollowTaxiRequest = -1;
        public readonly List<string> Cues = new List<string>();
        public readonly List<KeyValuePair<RowColor, string>> Toasts = new List<KeyValuePair<RowColor, string>>();

        /// <summary>Camera pan from held arrow buttons (camera mode): x = right, y = forward.</summary>
        public int PanX => CameraMode ? (Held(ConsoleButton.Right) ? 1 : 0) - (Held(ConsoleButton.Left) ? 1 : 0) : 0;
        public int PanY => CameraMode ? (Held(ConsoleButton.Up) ? 1 : 0) - (Held(ConsoleButton.Down) ? 1 : 0) : 0;
        /// <summary>Camera orbit from held ACCEPT (left) / REJECT (right) in camera mode.</summary>
        public int Orbit => CameraMode ? (Held(ConsoleButton.Reject) ? 1 : 0) - (Held(ConsoleButton.Accept) ? 1 : 0) : 0;

        private static readonly int ButtonCount = Enum.GetValues(typeof(ConsoleButton)).Length;
        private readonly bool[] _held = new bool[ButtonCount];
        private readonly bool[] _long = new bool[ButtonCount];
        private long _seenMessages;
        private int[] _hotspots = new int[0];

        public bool Held(ConsoleButton b) => _held[(int)b];

        // ================================================================== shift lifecycle

        /// <summary>Starts controlling a (new) shift; null detaches (back to the menu).</summary>
        public void Attach(Simulation sim)
        {
            Sim = sim;
            Page = ConsolePage.Dispatch;
            CameraMode = false;
            Paused = false;
            SelectedRequestId = -1;
            ChosenTaxiId = -1;
            SelectedTaxi = 0;
            EventScroll = 0;
            RelocateIndex = 0;
            Array.Clear(_held, 0, _held.Length);
            Array.Clear(_long, 0, _long.Length);
            if (sim == null) return;
            MenuDifficulty = sim.Difficulty;
            PendingSurge = sim.SurgeLevel;
            _seenMessages = sim.MessageCount;
            var spots = new List<int>();
            foreach (string key in AutoDispatcher.Hotspots)
                foreach (CityNode n in sim.Graph.Nodes)
                    if (n.Key == key) spots.Add(n.Id);
            _hotspots = spots.ToArray();
        }

        public bool Running => Sim != null && !Sim.IsOver;

        // ================================================================== input

        public void Handle(ConsoleInput input)
        {
            switch (input.Kind)
            {
                case ConsoleInputKind.Encoder: Turn(input.Steps); break;
                case ConsoleInputKind.Button:
                    int i = (int)input.Button;
                    switch (input.Action)
                    {
                        case ButtonAction.Press:
                            _held[i] = true;
                            _long[i] = false;
                            OnPress(input.Button);
                            break;
                        case ButtonAction.Long:
                            if (!_held[i]) break;
                            _long[i] = true;
                            OnLong(input.Button);
                            break;
                        case ButtonAction.Release:
                            bool wasLong = _long[i], wasHeld = _held[i];
                            _held[i] = false;
                            _long[i] = false;
                            if (wasHeld && !wasLong) OnShortRelease(input.Button);
                            break;
                    }
                    break;
            }
        }

        /// <summary>Convenience for keyboards and the HUD: a complete short press.</summary>
        public void Tap(ConsoleButton b)
        {
            Handle(ConsoleInput.Press(b));
            Handle(ConsoleInput.Release(b));
        }

        /// <summary>Convenience for keyboards: a complete long press.</summary>
        public void HoldLong(ConsoleButton b)
        {
            Handle(ConsoleInput.Press(b));
            Handle(ConsoleInput.Long(b));
            Handle(ConsoleInput.Release(b));
        }

        private void OnPress(ConsoleButton b)
        {
            if (Sim == null)
            {
                if (b == ConsoleButton.Up || b == ConsoleButton.Down) ChangeDifficulty(b == ConsoleButton.Up ? -1 : 1);
                else if (b == ConsoleButton.Accept) { StartRequested = true; Cue("start"); }
                return;
            }
            if (Sim.IsOver)
            {
                if (b == ConsoleButton.Accept) { StartRequested = true; Cue("start"); }
                return;
            }
            if (CameraMode) return; // camera mode reads held buttons

            switch (b)
            {
                case ConsoleButton.Up: Move(-1); break;
                case ConsoleButton.Down: Move(1); break;
                case ConsoleButton.Accept: Accept(); break;
                case ConsoleButton.Reject: Reject(); break;
            }
        }

        private void OnLong(ConsoleButton b)
        {
            if (!Running) return;
            switch (b)
            {
                case ConsoleButton.Knob:
                    CameraMode = !CameraMode;
                    Array.Clear(_held, 0, _held.Length);
                    _held[(int)ConsoleButton.Knob] = true; // still physically down; its release is ignored (long)
                    _long[(int)ConsoleButton.Knob] = true;
                    Cue("click");
                    Toast(RowColor.Cyan, CameraMode ? "Camera mode: arrows move, knob zooms" : "Back to dispatch");
                    break;
                case ConsoleButton.Back:
                    Paused = !Paused;
                    Cue("click");
                    Toast(RowColor.Yellow, Paused ? "Paused - hold BACK to continue" : "Running");
                    break;
                case ConsoleButton.Left:
                case ConsoleButton.Right:
                    if (CameraMode) break;
                    ChangeSpeed(b == ConsoleButton.Right ? 1 : -1);
                    break;
                case ConsoleButton.Next:
                    ShowInCity();
                    break;
                case ConsoleButton.Info:
                    HomeRequested = true;
                    Cue("click");
                    Toast(RowColor.Cyan, "Camera: home view (G-Block, BKC)");
                    break;
            }
        }

        private void OnShortRelease(ConsoleButton b)
        {
            if (b == ConsoleButton.Next) { Turn(1); return; }    // right-stick click = one knob step (also picks difficulty before a shift)
            if (!Running) return;
            if (CameraMode)
            {
                if (b == ConsoleButton.Knob) { FollowNextRequested = true; Cue("click"); }
                else if (b == ConsoleButton.Info) { Cue("click"); Toast(RowColor.Cyan, HelpLine()); }
                else if (b == ConsoleButton.Back) { CameraMode = false; Cue("click"); Toast(RowColor.Cyan, "Back to dispatch"); }
                return;
            }
            switch (b)
            {
                case ConsoleButton.Left: ChangePage(-1); break;
                case ConsoleButton.Right: ChangePage(1); break;
                case ConsoleButton.Back:
                    ChosenTaxiId = -1;
                    PendingSurge = Sim.SurgeLevel;
                    EventScroll = 0;
                    Cue("click");
                    break;
                case ConsoleButton.Knob: ShowInCity(); break;
                case ConsoleButton.Info: Cue("click"); Toast(RowColor.Cyan, HelpLine()); break;
            }
        }

        /// <summary>INFO: one line (fits the 52-character TFT footer) saying what the buttons do right now.</summary>
        public string HelpLine()
        {
            if (CameraMode) return "Sticks move the camera, R-click zooms, BACK leaves";
            switch (Page)
            {
                case ConsolePage.Dispatch: return "UP/DN request, R-click taxi, ACCEPT send, REJECT no";
                case ConsolePage.Fleet: return "UP/DN taxi, ACCEPT fix, R-click+REJECT relocate";
                case ConsolePage.Finance: return "UP/DN or R-click price, ACCEPT applies, BACK resets";
                default: return "UP/DN scroll, ACCEPT reroutes blocked taxis";
            }
        }

        private void Turn(int steps)
        {
            if (Sim == null) { ChangeDifficulty(Math.Sign(steps)); return; }
            if (!Running) return;
            if (CameraMode) { ZoomSteps += steps; return; }
            switch (Page)
            {
                case ConsolePage.Dispatch: CycleCandidate(steps); break;
                case ConsolePage.Fleet:
                    if (_hotspots.Length > 0) RelocateIndex = Mod(RelocateIndex + steps, _hotspots.Length);
                    Cue("click");
                    break;
                case ConsolePage.Finance:
                    PendingSurge = Math.Max(0, Math.Min(SurgePricing.Levels.Length - 1, PendingSurge + steps));
                    Cue("click");
                    break;
                case ConsolePage.Events: EventScroll = Math.Max(0, EventScroll + steps); break;
            }
        }

        // ================================================================== actions

        public void ChangePage(int dir)
        {
            Page = (ConsolePage)Mod((int)Page + dir, PageCount);
            if (Page == ConsolePage.Finance && Sim != null) PendingSurge = Sim.SurgeLevel;
            EventScroll = 0;
            Cue("click");
        }

        public void ChangeSpeed(int dir)
        {
            SpeedIndex = Math.Max(0, Math.Min(Speeds.Length - 1, SpeedIndex + dir));
            Cue("click");
            Toast(RowColor.Cyan, $"Speed {Speed}x");
        }

        public void SetMenuDifficulty(Difficulty d) => MenuDifficulty = d;

        /// <summary>Mouse / HUD selection of a waiting request (switches to the Dispatch page).</summary>
        public void SelectRequest(int id)
        {
            if (Sim == null || Sim.Request(id) == null || Sim.Request(id).Status != RequestStatus.Waiting) return;
            Page = ConsolePage.Dispatch;
            if (SelectedRequestId != id) ChosenTaxiId = -1;
            SelectedRequestId = id;
            Cue("click");
        }

        /// <summary>Mouse / HUD selection of a fleet taxi (switches to the Fleet page).</summary>
        public void SelectTaxi(int index)
        {
            if (Sim == null || index < 0 || index >= Sim.Taxis.Count) return;
            Page = ConsolePage.Fleet;
            SelectedTaxi = index;
            Cue("click");
        }

        /// <summary>Mouse / HUD: choose this taxi for the selected request.</summary>
        public void ChooseTaxi(int taxiId)
        {
            ChosenTaxiId = taxiId;
            Cue("click");
        }

        private void ChangeDifficulty(int dir)
        {
            MenuDifficulty = (Difficulty)Math.Max(0, Math.Min(2, (int)MenuDifficulty + dir));
            Cue("click");
        }

        private void Move(int dir)
        {
            switch (Page)
            {
                case ConsolePage.Dispatch:
                {
                    List<RideRequest> list = WaitingRequests();
                    if (list.Count == 0) return;
                    int idx = list.FindIndex(r => r.Id == SelectedRequestId);
                    idx = Math.Max(0, Math.Min(list.Count - 1, (idx < 0 ? 0 : idx) + dir));
                    SelectedRequestId = list[idx].Id;
                    ChosenTaxiId = -1;
                    break;
                }
                case ConsolePage.Fleet:
                    SelectedTaxi = Mod(SelectedTaxi + dir, Sim.Taxis.Count);
                    break;
                case ConsolePage.Finance:
                    PendingSurge = Math.Max(0, Math.Min(SurgePricing.Levels.Length - 1, PendingSurge - dir));
                    break;
                case ConsolePage.Events:
                    EventScroll = Math.Max(0, EventScroll + dir);
                    break;
            }
            Cue("click");
        }

        private void CycleCandidate(int steps)
        {
            RideRequest r = SelectedRequest();
            if (r == null) return;
            List<DispatchCandidate> c = FeasibleCandidates(r);
            if (c.Count == 0) { Cue("err"); return; }
            int idx = Math.Max(0, c.FindIndex(x => x.TaxiId == ChosenTaxiId));
            ChosenTaxiId = c[Mod(idx + steps, c.Count)].TaxiId;
            Cue("click");
        }

        private void Accept()
        {
            switch (Page)
            {
                case ConsolePage.Dispatch:
                {
                    RideRequest r = SelectedRequest();
                    if (r == null) { Fail("No request selected"); return; }
                    DispatchCandidate c = ChosenCandidate(r);
                    if (c == null)
                    {
                        List<DispatchCandidate> all = Sim.Rank(r.Id);
                        Fail(all.Count > 0 ? $"No taxi can go: {all[0].Reason}" : "No taxi can go");
                        return;
                    }
                    Run(Command.Assign(c.TaxiId, r.Id));
                    ChosenTaxiId = -1;
                    break;
                }
                case ConsolePage.Fleet:
                {
                    Taxi t = Sim.Taxis[SelectedTaxi];
                    Command? cmd = SmartCommand(t, out string why);
                    if (cmd == null) { Fail(why); return; }
                    Run(cmd.Value);
                    break;
                }
                case ConsolePage.Finance:
                    if (PendingSurge == Sim.SurgeLevel) { Fail("Pick a new price first (UP/DN or R-click)"); return; }
                    Run(Command.SetSurge(PendingSurge));
                    break;
                case ConsolePage.Events:
                {
                    // Quick fix-up: reroute every taxi stopped by a closure
                    int n = 0;
                    foreach (Taxi t in Sim.Taxis)
                        if (t.State == TaxiState.Delayed && t.NeedsReroute && Sim.Submit(Command.Reroute(t.Id)).Ok) n++;
                    _seenMessages = Sim.MessageCount;
                    if (n > 0) { Cue("ok"); Toast(RowColor.Green, $"Rerouted {n} blocked taxi(s)"); }
                    else Fail("No taxi needs a reroute");
                    break;
                }
            }
        }

        private void Reject()
        {
            switch (Page)
            {
                case ConsolePage.Dispatch:
                {
                    RideRequest r = SelectedRequest();
                    if (r == null) { Fail("No request selected"); return; }
                    Run(Command.Reject(r.Id));
                    break;
                }
                case ConsolePage.Fleet:
                    if (_hotspots.Length == 0) return;
                    Run(Command.Relocate(Sim.Taxis[SelectedTaxi].Id, _hotspots[RelocateIndex]));
                    break;
                default:
                    Cue("err");
                    break;
            }
        }

        private void ShowInCity()
        {
            switch (Page)
            {
                case ConsolePage.Dispatch:
                {
                    RideRequest r = SelectedRequest();
                    if (r != null) FocusNodeRequest = r.Origin;
                    break;
                }
                case ConsolePage.Fleet:
                    FollowTaxiRequest = SelectedTaxi;
                    break;
                default:
                    FollowNextRequested = true;
                    break;
            }
            Cue("click");
        }

        /// <summary>The one sensible fleet action for a taxi's state (shown as "ACCEPT: …" on the Fleet page).</summary>
        public Command? SmartCommand(Taxi t, out string label)
        {
            if (t.State == TaxiState.Delayed && t.NeedsReroute) { label = "reroute"; return Command.Reroute(t.Id); }
            if (t.State == TaxiState.Charging) { label = "release charger"; return Command.ReleaseCharger(t.Id); }
            if (t.State == TaxiState.Idle || t.State == TaxiState.Relocating) { label = "charge"; return Command.SendCharge(t.Id); }
            label = t.State == TaxiState.ToCharger ? "already going to charge" : t.State == TaxiState.Offline ? "being towed" :
                    t.State == TaxiState.Delayed ? $"stopped ({t.LastIssue})" : "busy with a passenger";
            return null;
        }

        private void Run(Command c)
        {
            PollMessages();                 // queue cues for anything that happened before this command
            CommandResult r = Sim.Submit(c);
            _seenMessages = Sim.MessageCount; // the result is shown as a toast, not again from the log
            if (r.Ok) { Cue("ok"); Toast(RowColor.Green, r.Message); }
            else { Cue("err"); Toast(RowColor.Red, r.Message); }
        }

        private void Fail(string why)
        {
            Cue("err");
            Toast(RowColor.Red, why);
        }

        private void Cue(string name) => Cues.Add(name);

        private void Toast(RowColor c, string text) => Toasts.Add(new KeyValuePair<RowColor, string>(c, text));

        /// <summary>Turns new simulation messages into buzzer cues and console toasts. Call once a frame.</summary>
        public void PollMessages()
        {
            if (Sim == null) return;
            long fresh = Sim.MessageCount - _seenMessages;
            if (fresh <= 0) return;
            int start = Math.Max(0, Sim.Log.Count - (int)Math.Min(fresh, Sim.Log.Count));
            for (int i = start; i < Sim.Log.Count; i++)
            {
                SimMessage m = Sim.Log[i];
                switch (m.Kind)
                {
                    case MessageKind.Request:
                        Cue(m.Text.Contains("MEDICAL") ? "urgent" : "new");
                        break;
                    case MessageKind.Event:
                        Cue("event");
                        Toast(RowColor.Magenta, m.Text);
                        break;
                    case MessageKind.Warning:
                        Cue("warn");
                        Toast(RowColor.Orange, m.Text);
                        break;
                    case MessageKind.Money:
                        if (m.Text.Contains("completed") || m.Text.Contains("dropped")) Cue("coin");
                        break;
                    case MessageKind.GameOver:
                        Cue("over");
                        Toast(RowColor.Yellow, m.Text);
                        break;
                }
            }
            _seenMessages = Sim.MessageCount;
        }

        // ================================================================== queries (also used by the HUD)

        /// <summary>Waiting requests, most urgent first (medical, priority, then least patience left).</summary>
        public List<RideRequest> WaitingRequests()
        {
            var list = new List<RideRequest>();
            if (Sim == null) return list;
            foreach (RideRequest r in Sim.Requests) if (r.Status == RequestStatus.Waiting) list.Add(r);
            list.Sort((a, b) =>
            {
                int u = ((int)b.Urgency).CompareTo((int)a.Urgency);
                if (u != 0) return u;
                int p = a.PatienceLeft.CompareTo(b.PatienceLeft);
                return p != 0 ? p : a.Id.CompareTo(b.Id);
            });
            return list;
        }

        public RideRequest SelectedRequest()
        {
            if (Sim == null) return null;
            RideRequest r = Sim.Request(SelectedRequestId);
            if (r != null && r.Status == RequestStatus.Waiting) return r;
            List<RideRequest> list = WaitingRequests();
            SelectedRequestId = list.Count > 0 ? list[0].Id : -1;
            ChosenTaxiId = -1;
            return list.Count > 0 ? list[0] : null;
        }

        public List<DispatchCandidate> FeasibleCandidates(RideRequest r)
        {
            var list = new List<DispatchCandidate>();
            if (Sim == null || r == null) return list;
            foreach (DispatchCandidate c in Sim.Rank(r.Id)) if (c.Feasible) list.Add(c);
            return list;
        }

        /// <summary>The taxi ACCEPT would send: the one chosen with the knob, else the best by utility.</summary>
        public DispatchCandidate ChosenCandidate(RideRequest r)
        {
            List<DispatchCandidate> c = FeasibleCandidates(r);
            if (c.Count == 0) return null;
            foreach (DispatchCandidate x in c) if (x.TaxiId == ChosenTaxiId) return x;
            return c[0];
        }

        public int HotspotNode => _hotspots.Length > 0 ? _hotspots[RelocateIndex] : -1;

        // ================================================================== screen

        public ConsoleScreen BuildScreen()
        {
            var s = new ConsoleScreen { PageCount = PageCount, Page = (int)Page };
            if (Sim == null) { MenuScreen(s); return s; }
            s.Clock = Sim.ClockText;
            s.Cash = ConsoleProtocol.Money(Sim.Cash);
            s.Satisfaction = $"{Sim.Satisfaction:0}%";
            s.Surge = $"x{SurgePricing.Multiplier(Sim.SurgeLevel):0.00}";
            s.Speed = Paused ? "PAUSE" : $"{Speed}x";
            if (Sim.IsOver) { OverScreen(s); return s; }
            if (CameraMode) { CameraScreen(s); return s; }
            s.Title = PageNames[(int)Page];
            switch (Page)
            {
                case ConsolePage.Dispatch: DispatchPage(s); break;
                case ConsolePage.Fleet: FleetPage(s); break;
                case ConsolePage.Finance: FinancePage(s); break;
                case ConsolePage.Events: EventsPage(s); break;
            }
            return s;
        }

        private void MenuScreen(ConsoleScreen s)
        {
            s.Title = "TAXI MANAGER";
            s.PageCount = 1;
            Set(s, 0, "Start a dispatch shift", RowColor.Cyan, large: true);
            Set(s, 2, $"Difficulty:  < {MenuDifficulty} >", RowColor.Yellow, selected: true, large: true);
            Set(s, 4, MenuDifficulty == Difficulty.Easy ? "More cash, patient passengers, calm city" :
                      MenuDifficulty == Difficulty.Hard ? "Little cash, impatient city, more events" :
                      "The intended challenge: 06:00 to 22:00 in BKC", RowColor.Grey);
            Set(s, 5, "6 autonomous taxis vs RapidCab - keep them", RowColor.Grey);
            Set(s, 6, "charged, earn money, keep Mumbai happy.", RowColor.Grey);
            s.Footer = "ACCEPT start   UP/DOWN or R-click: difficulty";
        }

        private void OverScreen(ConsoleScreen s)
        {
            ScoreCard sc = Sim.Score();
            s.Title = "SHIFT OVER";
            s.PageCount = 1;
            string stars = new string('*', sc.Stars) + new string('.', 5 - sc.Stars);
            Set(s, 0, $"{stars}  score {sc.Score * 100:0}", RowColor.Yellow, large: true);
            Set(s, 1, sc.Outcome == Outcome.ShiftComplete ? "Shift complete!" : sc.Outcome == Outcome.Bankrupt ? "Bankrupt - out of money" : "Licence cancelled - customers unhappy",
                sc.Outcome == Outcome.ShiftComplete ? RowColor.Green : RowColor.Red);
            Set(s, 2, $"Profit {ConsoleProtocol.Money(sc.Profit)}", sc.Profit >= 0 ? RowColor.Green : RowColor.Red);
            Set(s, 3, $"Satisfaction {sc.Satisfaction:0}%   Efficiency {sc.Efficiency * 100:0}%");
            Set(s, 4, $"Trips {sc.Completed}   Abandoned {sc.Abandoned}   Rejected {sc.Rejected}");
            Set(s, 5, $"Lost to RapidCab {sc.LostToRival}   Declined price {sc.Declined}", RowColor.Grey);
            Set(s, 7, "ACCEPT = new shift", RowColor.Cyan, selected: true);
            s.Footer = "Thanks for playing - ACCEPT for a new shift";
        }

        private static void CameraScreen(ConsoleScreen s)
        {
            s.Title = "CAMERA";
            s.PageCount = 1;
            Set(s, 0, "Arrows      move the camera", RowColor.Cyan);
            Set(s, 1, "Knob        zoom in / out", RowColor.Cyan);
            Set(s, 2, "ACCEPT      turn left", RowColor.Cyan);
            Set(s, 3, "REJECT      turn right", RowColor.Cyan);
            Set(s, 4, "Knob press  follow the next taxi", RowColor.Cyan);
            Set(s, 6, "BACK or hold KNOB: back to dispatch", RowColor.Yellow);
            s.Footer = "Camera mode - the shift keeps running";
        }

        private void DispatchPage(ConsoleScreen s)
        {
            List<RideRequest> list = WaitingRequests();
            RideRequest sel = SelectedRequest();
            s.Title = $"DISPATCH  {list.Count} waiting";
            if (sel == null)
            {
                int busy = 0;
                foreach (Taxi t in Sim.Taxis) if (t.State == TaxiState.ToPickup || t.State == TaxiState.OnRide) busy++;
                Set(s, 0, "No passengers waiting", RowColor.Green, large: true);
                Set(s, 2, $"{busy} of {Sim.Taxis.Count} taxis on jobs.", RowColor.Grey);
                Set(s, 3, "Tip: charge low taxis now (Fleet page)", RowColor.Grey);
                Set(s, 4, "or move idle ones to busy spots.", RowColor.Grey);
                s.Footer = "< > pages   hold < > speed   hold BACK pause";
                return;
            }
            int selIdx = list.IndexOf(sel);
            int first = Math.Max(0, Math.Min(selIdx - 2, list.Count - 5));
            for (int row = 0; row < 5 && first + row < list.Count; row++)
            {
                RideRequest r = list[first + row];
                Set(s, row, RequestLine(r), UrgencyColor(r), selected: r.Id == sel.Id);
            }
            if (list.Count > 5 && first + 5 < list.Count) s.Rows[4].Text = Pad(s.Rows[4].Text, 48) + " ...";

            List<DispatchCandidate> cands = FeasibleCandidates(sel);
            DispatchCandidate c = ChosenCandidate(sel);
            if (c == null)
            {
                List<DispatchCandidate> all = Sim.Rank(sel.Id);
                Set(s, 5, "No taxi can take this one:", RowColor.Red);
                Set(s, 6, all.Count > 0 ? $"{Sim.Taxis[all[0].TaxiId].Label}: {all[0].Reason}" : "-", RowColor.Orange);
                Set(s, 7, "REJECT it, or free / charge a taxi", RowColor.Grey);
            }
            else
            {
                Taxi t = Sim.Taxis[c.TaxiId];
                int k = cands.FindIndex(x => x.TaxiId == c.TaxiId) + 1;
                Set(s, 5, $"> {t.Label}  ETA {c.EtaMinutes:0} min  {t.Battery:0}%>{t.Battery - c.EnergyNeeded:0}%  ({k}/{cands.Count})", RowColor.Green, large: false);
                Set(s, 6, $"  fare Rs{c.Revenue:0}  energy -Rs{c.EnergyCost:0}  value {c.Utility:+0;-0}", RowColor.Cyan);
                var alt = new List<string>();
                foreach (DispatchCandidate o in cands)
                    if (o.TaxiId != c.TaxiId && alt.Count < 3) alt.Add($"{Sim.Taxis[o.TaxiId].Label} {o.EtaMinutes:0}m");
                Set(s, 7, alt.Count > 0 ? "  others: " + string.Join(", ", alt) : "  only this taxi can go", RowColor.Grey);
            }
            s.Footer = "ACCEPT send  REJECT decline  R-click taxi  < >";
        }

        public string RequestLine(RideRequest r)
        {
            string tag = r.Urgency == Urgency.Medical ? "MED" : r.Urgency == Urgency.Priority ? "PRI" : "   ";
            string from = Short(Sim.Graph.Node(r.Origin).ShortName, 11), to = Short(Sim.Graph.Node(r.Destination).ShortName, 11);
            return $"#{r.Id,-3} {tag} {from,-11}>{to,-11} Rs{r.Fare * r.Surge,4:0} {Math.Max(0, r.PatienceLeft),2:0}m";
        }

        public static RowColor UrgencyColor(RideRequest r)
        {
            if (r.PatienceLeft < 3) return RowColor.Orange;
            return r.Urgency == Urgency.Medical ? RowColor.Red : r.Urgency == Urgency.Priority ? RowColor.Yellow : RowColor.White;
        }

        private void FleetPage(ConsoleScreen s)
        {
            int n = Math.Min(6, Sim.Taxis.Count);
            int first = Math.Max(0, Math.Min(SelectedTaxi - 5, Sim.Taxis.Count - n));
            for (int row = 0; row < n; row++)
            {
                Taxi t = Sim.Taxis[first + row];
                Set(s, row, TaxiLine(t), StateColor(t, Sim.Config), selected: first + row == SelectedTaxi);
            }
            Taxi sel = Sim.Taxis[SelectedTaxi];
            Set(s, 6, "  " + TaxiDetail(sel), RowColor.Grey);
            Command? cmd = SmartCommand(sel, out string label);
            string spot = HotspotNode >= 0 ? Sim.Graph.Node(HotspotNode).ShortName : "-";
            Set(s, 7, $"  ACCEPT {(cmd != null ? label : "-")}   REJECT go {spot}", RowColor.Cyan);
            s.Footer = "UP/DN taxi  R-click place  hold R-click follow";
        }

        public string TaxiLine(Taxi t)
        {
            string where = t.IsMoving && t.TargetNode >= 0 ? ">" + Short(Sim.Graph.Node(t.TargetNode).ShortName, 11)
                                                           : " " + Short(Sim.Graph.Node(t.Node).ShortName, 11);
            string low = t.IsLowBattery(Sim.Config) ? " LOW" : "";
            return $"{t.Label} {StateName(t.State),-8} {where,-12} {t.Battery,3:0}%{low}";
        }

        public string TaxiDetail(Taxi t)
        {
            RideRequest r = Sim.Request(t.RequestId);
            switch (t.State)
            {
                case TaxiState.ToPickup when r != null: return $"Picking up #{r.Id} at {Sim.Graph.Node(r.Origin).ShortName}";
                case TaxiState.OnRide when r != null: return $"Driving #{r.Id} to {Sim.Graph.Node(r.Destination).ShortName}";
                case TaxiState.Charging: return t.InChargerQueue ? "Waiting for a free charger" : $"Charging, {t.Battery:0}%";
                case TaxiState.Delayed: return t.NeedsReroute ? "Road closed ahead - ACCEPT to reroute" : $"Stopped: {t.LastIssue}, {t.DelayLeft:0} min";
                case TaxiState.Offline: return $"Battery empty - towing, {t.DelayLeft:0} min";
                case TaxiState.ToCharger: return "Heading to a charger";
                case TaxiState.Relocating: return "Repositioning";
            }
            return $"Idle - trips {t.TripsCompleted}, earned Rs{t.Earnings:0}";
        }

        public static string StateName(TaxiState s)
        {
            switch (s)
            {
                case TaxiState.ToPickup: return "Pickup";
                case TaxiState.OnRide: return "Ride";
                case TaxiState.ToCharger: return "ToCharge";
                case TaxiState.Charging: return "Charging";
                case TaxiState.Relocating: return "Moving";
                case TaxiState.Delayed: return "Delayed";
                case TaxiState.Offline: return "Towed";
                default: return "Idle";
            }
        }

        public static RowColor StateColor(Taxi t, SimConfig c)
        {
            switch (t.State)
            {
                case TaxiState.ToPickup: return RowColor.Yellow;
                case TaxiState.OnRide: return RowColor.Blue;
                case TaxiState.ToCharger:
                case TaxiState.Charging: return RowColor.Magenta;
                case TaxiState.Delayed: return RowColor.Orange;
                case TaxiState.Offline: return RowColor.Red;
            }
            return t.IsLowBattery(c) ? RowColor.Orange : RowColor.Green;
        }

        private void FinancePage(ConsoleScreen s)
        {
            ScoreCard sc = Sim.Score();
            Set(s, 0, $"Cash {ConsoleProtocol.Money(Sim.Cash)}", Sim.Cash >= 0 ? RowColor.Green : RowColor.Red, large: true);
            Set(s, 1, $"In {ConsoleProtocol.Money(Sim.Revenue)}  costs {ConsoleProtocol.Money(Sim.Costs)}  fines {ConsoleProtocol.Money(Sim.Penalties)}");
            Set(s, 2, $"Happy {Sim.Satisfaction:0}%  trips {Sim.Completed}  stars now {sc.Stars}/5",
                Sim.Satisfaction < 40 ? RowColor.Orange : RowColor.White);
            Set(s, 3, $"Lost: waited {Sim.Abandoned}  to RapidCab {Sim.LostToRival}  price {Sim.Declined}", RowColor.Grey);
            Set(s, 4, $"RapidCab x{SurgePricing.Multiplier(Sim.RivalSurgeLevel):0.00}  ({Sim.RivalTrips} trips)", RowColor.Orange);
            int best = SurgePricing.BestResponse(Sim.Config, 150.0, Sim.RivalSurgeLevel);
            Set(s, 5, $"Your price x{SurgePricing.Multiplier(Sim.SurgeLevel):0.00}   best reply x{SurgePricing.Multiplier(best):0.00}", RowColor.Cyan);
            bool changed = PendingSurge != Sim.SurgeLevel;
            Set(s, 6, $"New price  < x{SurgePricing.Multiplier(PendingSurge):0.00} >", changed ? RowColor.Yellow : RowColor.Grey, selected: changed, large: true);
            double accept = SurgePricing.Acceptance(Sim.Config, SurgePricing.Multiplier(PendingSurge));
            Set(s, 7, $"  at that price ~{accept * 100:0}% of riders say yes", RowColor.Grey);
            s.Footer = "UP/DN or R-click price  ACCEPT apply  BACK undo";
        }

        private void EventsPage(ConsoleScreen s)
        {
            var lines = new List<ConsoleRow>();
            foreach (TrafficEvent ev in Sim.Events)
            {
                if (!ev.Started || ev.Ended) continue;
                double left = (ev.EndTick - Sim.Tick) * Sim.Config.MinutesPerTick;
                lines.Add(new ConsoleRow($"! {ev.Description} ({left:0}m)", RowColor.Magenta));
            }
            int blocked = 0;
            foreach (Taxi t in Sim.Taxis) if (t.State == TaxiState.Delayed && t.NeedsReroute) blocked++;
            if (blocked > 0) lines.Insert(0, new ConsoleRow($"{blocked} taxi(s) blocked - ACCEPT reroutes all", RowColor.Orange));
            if (lines.Count == 0) lines.Add(new ConsoleRow("No active city events", RowColor.Green));
            for (int i = Sim.Log.Count - 1; i >= 0 && lines.Count < 40; i--)
            {
                SimMessage m = Sim.Log[i];
                RowColor c = m.Kind == MessageKind.Warning ? RowColor.Orange : m.Kind == MessageKind.Event ? RowColor.Magenta :
                             m.Kind == MessageKind.Money ? RowColor.Green : m.Kind == MessageKind.Request ? RowColor.White : RowColor.Grey;
                lines.Add(new ConsoleRow(m.Text, c));
            }
            EventScroll = Math.Min(EventScroll, Math.Max(0, lines.Count - ConsoleProtocol.Rows));
            for (int row = 0; row < ConsoleProtocol.Rows && EventScroll + row < lines.Count; row++)
            {
                ConsoleRow r = lines[EventScroll + row];
                Set(s, row, r.Text, r.Color);
            }
            s.Footer = "UP/DN or R-click scroll   ACCEPT fix blocked";
        }

        // ================================================================== helpers

        private static void Set(ConsoleScreen s, int row, string text, RowColor color = RowColor.White, bool selected = false, bool large = false)
        {
            ConsoleRow r = s.Rows[row];
            r.Text = text;
            r.Color = color;
            r.Selected = selected;
            r.Large = large;
        }

        private static string Short(string s, int max) => s.Length <= max ? s : s.Substring(0, max);

        private static string Pad(string s, int len) => s.Length >= len ? s.Substring(0, len) : s.PadRight(len);

        private static int Mod(int a, int n) => n <= 0 ? 0 : ((a % n) + n) % n;
    }
}
