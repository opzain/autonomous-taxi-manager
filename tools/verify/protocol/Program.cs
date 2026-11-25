// Plays a scripted shift through the real Simulation + DispatchConsole and writes the exact PC -> ESP32
// line stream (same diff / full-refresh rules as ConsoleBridge) with timestamps, plus #SNAP markers.
using System;
using System.Collections.Generic;
using System.IO;
using TaxiSim.Core.Dispatch;
using TaxiSim.Core.Sim;

var sim = new Simulation(2024UL);
var con = new DispatchConsole();
con.Attach(sim);
var sent = new Dictionary<string, string>();
var w = new StreamWriter(args[0]);
long t = 3000; // ms since the ESP32 booted
int maxLen = 0, lines = 0, bytes = 0;
void Put(string key, string line) {
    if (sent.TryGetValue(key, out var old) && old == line) return;
    sent[key] = line; w.WriteLine($"{t}\t{line}"); maxLen = Math.Max(maxLen, line.Length); lines++; bytes += line.Length + 1;
}
void Frame() {
    con.PollMessages();
    foreach (var c in con.Cues) Put("Z" + lines, ConsoleProtocol.Buzz(c));
    con.Cues.Clear();
    foreach (var m in con.Toasts) Put("M" + lines, ConsoleProtocol.Toast(m.Key, m.Value));
    con.Toasts.Clear();
    var s = con.BuildScreen();
    Put("S", ConsoleProtocol.Header(s.Clock, s.Cash, s.Satisfaction, s.Surge, s.Speed));
    Put("T", ConsoleProtocol.Title(s.Page, s.PageCount, s.Title));
    for (int i = 0; i < 8; i++) Put("R" + i, ConsoleProtocol.Row(i, s.Rows[i]));
    Put("F", ConsoleProtocol.Footer(s.Footer));
    Put("L", ConsoleProtocol.Leds(new uint[] { 0x003300, 0x004010, 0x403000, 0x001040, 0x300030, 0x401800, 0x400000, 0 }));
}
void Snap(string name) { w.WriteLine($"{t}\t#SNAP {name}"); }
void Run(int ticks, bool dispatch = false) {
    for (int i = 0; i < ticks; i++) {
        sim.Step();
        // a normal player: sends the suggested taxi every 6 ticks and charges low idle taxis
        if (dispatch && i % 6 == 0 && i < ticks - 24 && con.SelectedRequest() != null && con.ChosenCandidate(con.SelectedRequest()) != null) con.Tap(ConsoleButton.Accept);
        if (dispatch && i % 48 == 0) foreach (var tx in sim.Taxis) if (tx.State == TaxiState.Idle && tx.Battery < 30) { sim.Submit(Command.SendCharge(tx.Id)); break; }
        if (i % 2 == 0) { t += 120; Frame(); }
    }
}

Frame(); Snap("dispatch-start");
Run(4 * 60 * 3, dispatch: true);     // 3 sim-hours of normal play into the morning rush
Frame(); Snap("dispatch-rush");
con.Handle(ConsoleInput.Turn(1)); Frame(); Snap("dispatch-knob-other-taxi");
con.Tap(ConsoleButton.Accept); Frame(); Snap("after-accept");
con.Tap(ConsoleButton.Right); Frame(); Snap("fleet");
con.Tap(ConsoleButton.Right); con.Handle(ConsoleInput.Turn(2)); Frame(); Snap("money");
con.Tap(ConsoleButton.Right); Frame(); Snap("events");
con.HoldLong(ConsoleButton.Knob); Frame(); Snap("camera");
con.Tap(ConsoleButton.Back); con.Tap(ConsoleButton.Right); Frame();
con.HoldLong(ConsoleButton.Back); Frame(); Snap("paused");
con.HoldLong(ConsoleButton.Back);
for (int k = 0; k < 400 && !sim.IsOver; k++) { if (con.SelectedRequest() != null) con.Tap(ConsoleButton.Accept); Run(16); }
Frame(); Snap("shift-over");
w.WriteLine($"{t}\t#END");
w.Close();
Console.WriteLine($"lines {lines}, bytes {bytes}, longest line {maxLen} chars, sim minutes {sim.MinutesElapsed:0}, bytes per real second at 1x ~ {bytes / Math.Max(1.0, t / 1000.0):0}");
