using System.Linq;
using TaxiSim.Core;
using TaxiSim.Core.Dispatch;
using TaxiSim.Core.Sim;
using Xunit;

namespace TaxiSim.Core.Tests
{
    /// <summary>The ESP32 ⇄ PC protocol and the dispatch console logic (spec tests T02–T05, T17).</summary>
    public class ConsoleTests
    {
        // ------------------------------------------------------------------ protocol

        [Theory]
        [InlineData("B,UP,P", ConsoleButton.Up, ButtonAction.Press)]
        [InlineData("B,ACCEPT,L", ConsoleButton.Accept, ButtonAction.Long)]
        [InlineData("B,KNOB,R\r", ConsoleButton.Knob, ButtonAction.Release)]
        [InlineData("B,REJECT,P", ConsoleButton.Reject, ButtonAction.Press)]
        public void Parses_button_lines(string line, ConsoleButton b, ButtonAction a)
        {
            Assert.True(ConsoleProtocol.TryParse(line, out ConsoleInput i));
            Assert.Equal(ConsoleInputKind.Button, i.Kind);
            Assert.Equal(b, i.Button);
            Assert.Equal(a, i.Action);
        }

        [Fact]
        public void Parses_encoder_hello_and_keepalive()
        {
            Assert.True(ConsoleProtocol.TryParse("E,+3", out ConsoleInput e));
            Assert.Equal(3, e.Steps);
            Assert.True(ConsoleProtocol.TryParse("E,-1", out e));
            Assert.Equal(-1, e.Steps);
            Assert.True(ConsoleProtocol.TryParse("H,TAXI-CONSOLE,0.4-game", out ConsoleInput h));
            Assert.Equal(ConsoleInputKind.Hello, h.Kind);
            Assert.Equal("0.4-game", h.Text);
            Assert.True(ConsoleProtocol.TryParse("K", out ConsoleInput k));
            Assert.Equal(ConsoleInputKind.KeepAlive, k.Kind);
        }

        [Fact]
        public void Parses_joystick_lines()
        {
            Assert.True(ConsoleProtocol.TryParse("J,25,-100,0,73", out ConsoleInput j));
            Assert.Equal(ConsoleInputKind.Sticks, j.Kind);
            Assert.Equal((25, -100, 0, 73), (j.LX, j.LY, j.RX, j.RY));
            Assert.True(ConsoleProtocol.TryParse("J,0,0,0,0\r", out j));
            Assert.Equal((0, 0, 0, 0), (j.LX, j.LY, j.RX, j.RY));
            Assert.False(ConsoleProtocol.TryParse("J,1,2,3", out _));         // too few
            Assert.False(ConsoleProtocol.TryParse("J,1,2,3,4,5", out _));     // too many
            Assert.False(ConsoleProtocol.TryParse("J,101,0,0,0", out _));     // out of range
            Assert.False(ConsoleProtocol.TryParse("J,a,0,0,0", out _));       // not a number
        }

        [Fact]
        public void Parses_info_button()
        {
            Assert.True(ConsoleProtocol.TryParse("B,INFO,P", out ConsoleInput i));
            Assert.Equal(ConsoleButton.Info, i.Button);
            Assert.Equal(ButtonAction.Press, i.Action);
        }

        [Fact]
        public void Next_right_stick_click_replaces_the_knob()
        {
            Assert.True(ConsoleProtocol.TryParse("B,NEXT,P", out ConsoleInput n));
            Assert.Equal(ConsoleButton.Next, n.Button);

            var pre = new DispatchConsole();                       // before a shift: picks the difficulty
            Difficulty d0 = pre.MenuDifficulty;
            pre.Tap(ConsoleButton.Next);
            Assert.NotEqual(d0, pre.MenuDifficulty);

            var sim = new Simulation(7UL);
            var con = new DispatchConsole();
            con.Attach(sim);
            con.ChangePage(1);                                     // Fleet: NEXT moves the relocation spot, like a knob step
            int spot = con.RelocateIndex;
            con.Tap(ConsoleButton.Next);
            Assert.NotEqual(spot, con.RelocateIndex);
            con.HoldLong(ConsoleButton.Next);                      // long press: show (follow) the selected taxi in the city
            Assert.Equal(con.SelectedTaxi, con.FollowTaxiRequest);
        }

        [Fact]
        public void Info_shows_help_and_long_press_asks_for_home_view()
        {
            var sim = new Simulation(7UL);
            var con = new DispatchConsole();
            con.Attach(sim);
            con.Tap(ConsoleButton.Info);
            Assert.Contains(con.Toasts, t => t.Value == con.HelpLine());
            Assert.StartsWith("UP/DN request", con.HelpLine());
            con.ChangePage(1);                                   // Fleet page: a different line
            Assert.StartsWith("UP/DN taxi", con.HelpLine());
            Assert.All(new[] { 0, 1, 2, 3 }, _ => { Assert.True(con.HelpLine().Length <= ConsoleProtocol.SmallChars); con.ChangePage(1); });
            Assert.False(con.HomeRequested);
            con.HoldLong(ConsoleButton.Info);
            Assert.True(con.HomeRequested);
        }

        [Theory]
        [InlineData("")]
        [InlineData("B,HELP,P")]
        [InlineData("B,UP")]
        [InlineData("B,UP,X")]
        [InlineData("E,0")]
        [InlineData("E,abc")]
        [InlineData("H,OTHER,1")]
        [InlineData("garbage ets Jun  8 2016 00:22:57")]
        public void Rejects_bad_lines(string line) => Assert.False(ConsoleProtocol.TryParse(line, out _));

        [Fact]
        public void Outgoing_text_is_plain_ascii_without_separators()
        {
            string row = ConsoleProtocol.Row(3, new ConsoleRow("₹340 → Kurla | Asian Heart · ok", RowColor.Red, selected: true));
            Assert.Equal("R|3|R*|Rs340 > Kurla / Asian Heart - ok", row);
            Assert.All(row, c => Assert.InRange(c, ' ', '~'));
            Assert.Equal(4, row.Split('|').Length);
            string longText = new string('x', 200);
            Assert.Equal(ConsoleProtocol.SmallChars, ConsoleProtocol.Clean(longText, ConsoleProtocol.SmallChars).Length);
            Assert.Equal("Rs 12,345", ConsoleProtocol.Money(12345));
            Assert.Equal("-Rs 300", ConsoleProtocol.Money(-300));
            Assert.Equal("L|FF000000FF00000000000000000000000000000000000000", ConsoleProtocol.Leds(new uint[] { 0xFF0000, 0x00FF00 }));
        }

        [Fact]
        public void Every_screen_line_fits_the_tft()
        {
            var sim = new Simulation(7UL);
            var con = new DispatchConsole();
            con.Attach(sim);
            sim.RunTicks(4 * 60 * 3); // 3 sim-hours: requests, events, jobs
            for (int p = 0; p < DispatchConsole.PageCount; p++)
            {
                ConsoleScreen s = con.BuildScreen();
                foreach (ConsoleRow r in s.Rows)
                {
                    string line = ConsoleProtocol.Row(0, r);
                    Assert.True(line.Length <= 8 + ConsoleProtocol.SmallChars, line);
                    Assert.Equal(4, line.Split('|').Length);
                }
                con.ChangePage(1);
            }
        }

        // ------------------------------------------------------------------ console logic

        private static (Simulation sim, DispatchConsole con, RideRequest req) Setup()
        {
            var cfg = SimConfig.For(Difficulty.Normal);
            cfg.BaseRequestsPerHour = 0; // only the scripted request
            cfg.EventsPerHour = 0;
            var sim = new Simulation(42UL, Difficulty.Normal, cfg);
            var con = new DispatchConsole();
            con.Attach(sim);
            RideRequest req = sim.AddRequest(sim.Graph.Node("jio_world").Id, sim.Graph.Node("kurla_stn").Id);
            return (sim, con, req);
        }

        [Fact]
        public void Accept_assigns_the_best_taxi_and_reports_ok()
        {
            var (sim, con, req) = Setup();
            DispatchCandidate best = sim.Rank(req.Id)[0];
            con.Tap(ConsoleButton.Accept);
            Assert.Equal(RequestStatus.Assigned, req.Status);
            Assert.Equal(best.TaxiId, req.TaxiId);
            Assert.Contains("ok", con.Cues);
            Assert.Equal(TaxiState.ToPickup, sim.Taxis[best.TaxiId].State);
        }

        [Fact]
        public void Knob_chooses_another_taxi()
        {
            var (sim, con, req) = Setup();
            var cands = con.FeasibleCandidates(req);
            Assert.True(cands.Count >= 2);
            Assert.Contains($"(1/{cands.Count})", con.BuildScreen().Rows[5].Text); // best = first of N
            con.Handle(ConsoleInput.Turn(1));
            Assert.Equal(cands[1].TaxiId, con.ChosenCandidate(req).TaxiId);
            ConsoleScreen scr = con.BuildScreen();
            Assert.Contains($"(2/{cands.Count})", scr.Rows[5].Text);
            Assert.DoesNotContain(con.Sim.Taxis[cands[1].TaxiId].Label + " ", scr.Rows[7].Text); // chosen one isn't listed as "other"
            con.Tap(ConsoleButton.Back); // back to the best
            Assert.Equal(cands[0].TaxiId, con.ChosenCandidate(req).TaxiId);
            con.Handle(ConsoleInput.Turn(-1)); // wraps to the last
            Assert.Equal(cands[cands.Count - 1].TaxiId, con.ChosenCandidate(req).TaxiId);
            con.Tap(ConsoleButton.Accept);
            Assert.Equal(cands[cands.Count - 1].TaxiId, req.TaxiId);
        }

        [Fact]
        public void Reject_declines_and_costs_satisfaction()
        {
            var (sim, con, req) = Setup();
            double before = sim.Satisfaction;
            con.Tap(ConsoleButton.Reject);
            Assert.Equal(RequestStatus.Rejected, req.Status);
            Assert.True(sim.Satisfaction < before);
        }

        [Fact]
        public void Up_down_walk_the_queue_most_urgent_first()
        {
            var (sim, con, req) = Setup();
            RideRequest med = sim.AddRequest(sim.Graph.Node("bandra_stn").Id, sim.Graph.Node("asian_heart").Id, Urgency.Medical, 8);
            Assert.Equal(med.Id, con.SelectedRequest().Id); // medical goes first
            con.Tap(ConsoleButton.Down);
            Assert.Equal(req.Id, con.SelectedRequestId);
            con.Tap(ConsoleButton.Down); // clamps at the end
            Assert.Equal(req.Id, con.SelectedRequestId);
            con.Tap(ConsoleButton.Up);
            Assert.Equal(med.Id, con.SelectedRequestId);
        }

        [Fact]
        public void Left_right_change_page_but_long_press_changes_speed_only()
        {
            var (_, con, _) = Setup();
            con.Tap(ConsoleButton.Right);
            Assert.Equal(ConsolePage.Fleet, con.Page);
            con.Tap(ConsoleButton.Left);
            con.Tap(ConsoleButton.Left);
            Assert.Equal(ConsolePage.Events, con.Page); // wraps
            con.HoldLong(ConsoleButton.Right);
            Assert.Equal(ConsolePage.Events, con.Page); // long press did not also flip the page
            Assert.Equal(2, con.Speed);
            con.HoldLong(ConsoleButton.Left);
            con.HoldLong(ConsoleButton.Left);
            Assert.Equal(1, con.Speed);
        }

        [Fact]
        public void Long_back_pauses_and_long_knob_enters_camera_mode()
        {
            var (_, con, _) = Setup();
            con.HoldLong(ConsoleButton.Back);
            Assert.True(con.Paused);
            con.HoldLong(ConsoleButton.Back);
            Assert.False(con.Paused);

            con.HoldLong(ConsoleButton.Knob);
            Assert.True(con.CameraMode);
            con.Handle(ConsoleInput.Press(ConsoleButton.Up));
            con.Handle(ConsoleInput.Press(ConsoleButton.Reject));
            Assert.Equal(1, con.PanY);
            Assert.Equal(1, con.Orbit);
            con.Handle(ConsoleInput.Release(ConsoleButton.Up));
            con.Handle(ConsoleInput.Release(ConsoleButton.Reject));
            Assert.Equal(0, con.PanY);
            Assert.Equal(ConsolePage.Dispatch, con.Page); // arrows did not change pages
            con.Handle(ConsoleInput.Turn(2));
            Assert.Equal(2, con.ZoomSteps);
            con.Tap(ConsoleButton.Knob);
            Assert.True(con.FollowNextRequested);
            con.Tap(ConsoleButton.Back);
            Assert.False(con.CameraMode);
        }

        [Fact]
        public void Fleet_page_sends_a_taxi_to_charge_and_relocates()
        {
            var (sim, con, _) = Setup();
            con.Tap(ConsoleButton.Right); // Fleet
            con.Tap(ConsoleButton.Accept);
            // The taxis start at the AT Fleet Hub, which has chargers: it plugs in straight away
            Assert.Contains(sim.Taxis[0].State, new[] { TaxiState.ToCharger, TaxiState.Charging });
            con.Tap(ConsoleButton.Down);
            Assert.Equal(1, con.SelectedTaxi);
            con.Handle(ConsoleInput.Turn(1));
            int spot = con.HotspotNode;
            con.Tap(ConsoleButton.Reject);
            Assert.Equal(TaxiState.Relocating, sim.Taxis[1].State);
            Assert.Equal(spot, sim.Taxis[1].TargetNode);
        }

        [Fact]
        public void Money_page_sets_the_price_only_on_accept()
        {
            var (sim, con, _) = Setup();
            con.Tap(ConsoleButton.Right);
            con.Tap(ConsoleButton.Right); // Money
            Assert.Equal(ConsolePage.Finance, con.Page);
            con.Handle(ConsoleInput.Turn(2));
            Assert.Equal(0, sim.SurgeLevel);
            con.Tap(ConsoleButton.Accept);
            Assert.Equal(2, sim.SurgeLevel);
            Assert.Contains(sim.CommandLog, c => c.Type == CommandType.SetSurge && c.A == 2);
        }

        [Fact]
        public void Menu_and_game_over_start_a_shift_on_accept()
        {
            var con = new DispatchConsole();
            con.Tap(ConsoleButton.Down);
            Assert.Equal(Difficulty.Hard, con.MenuDifficulty);
            con.Tap(ConsoleButton.Accept);
            Assert.True(con.StartRequested);
            Assert.Equal("TAXI MANAGER", con.BuildScreen().Title);

            var sim = new Simulation(3UL, Difficulty.Easy);
            con.Attach(sim);
            con.StartRequested = false;
            sim.RunTicks(sim.Config.TicksPerShift + 5);
            Assert.True(sim.IsOver);
            Assert.Equal("SHIFT OVER", con.BuildScreen().Title);
            con.Tap(ConsoleButton.Accept);
            Assert.True(con.StartRequested);
        }

        [Fact]
        public void New_requests_and_events_become_buzzer_cues()
        {
            var sim = new Simulation(11UL);
            var con = new DispatchConsole();
            con.Attach(sim);
            sim.RunTicks(4 * 60); // one sim-hour
            con.PollMessages();
            Assert.Contains(con.Cues, c => c == "new" || c == "urgent");
            int count = con.Cues.Count;
            con.PollMessages(); // nothing new
            Assert.Equal(count, con.Cues.Count);
        }

        [Fact]
        public void Console_commands_are_logged_so_saves_replay_them()
        {
            var sim = new Simulation(5UL);
            var con = new DispatchConsole();
            con.Attach(sim);
            int assigned = 0;
            for (int i = 0; i < 4 * 60 * 2; i++)
            {
                sim.Step();
                if (i % 8 == 0 && con.SelectedRequest() != null && con.ChosenCandidate(con.SelectedRequest()) != null)
                {
                    con.Tap(ConsoleButton.Accept);
                    assigned++;
                }
            }
            Assert.True(assigned > 3);
            Simulation again = SaveGame.Load(SaveGame.Write(sim), verify: true);
            Assert.Equal(sim.StateHash(), again.StateHash());
        }
    }
}
