using System;
using System.Collections.Generic;
using System.Linq;
using TaxiSim.Core.Sim;
using Xunit;

namespace TaxiSim.Core.Tests
{
    /// <summary>
    /// The dispatch simulation against the spec's test matrix (docs/07-test-plan.md):
    /// T06 state machine, T07/T08 routing + reroute, T10/T11 energy and charging, T12/T13 money,
    /// T14 save/replay, T15 boundaries, T16 long runs, T18 golden regression, plus the game-theory layer.
    /// </summary>
    public class SimulationTests
    {
        /// <summary>A quiet world for unit scenarios: no random demand, no random events, no rival.</summary>
        private static Simulation Quiet(ulong seed = 1)
        {
            var c = new SimConfig { BaseRequestsPerHour = 0, EventsPerHour = 0, RivalEnabled = false, RivalTaxis = 0 };
            return new Simulation(seed, Difficulty.Normal, c);
        }

        private static int Node(Simulation sim, string key) => sim.Graph.Node(key).Id;

        private static void RunUntil(Simulation sim, Func<bool> done, int maxTicks = 4000)
        {
            for (int i = 0; i < maxTicks && !done() && !sim.IsOver; i++) sim.Step();
            Assert.True(done(), "condition not reached in time");
        }

        // ------------------------------------------------------------------ T06 state machine

        [Fact]
        public void T06_StateMachine_AllowsOnlyLegalTransitions()
        {
            Assert.True(TaxiStateMachine.CanTransition(TaxiState.Idle, TaxiState.ToPickup));
            Assert.True(TaxiStateMachine.CanTransition(TaxiState.ToPickup, TaxiState.OnRide));
            Assert.True(TaxiStateMachine.CanTransition(TaxiState.OnRide, TaxiState.Idle));
            Assert.True(TaxiStateMachine.CanTransition(TaxiState.ToCharger, TaxiState.Charging));
            Assert.True(TaxiStateMachine.CanTransition(TaxiState.Offline, TaxiState.Charging));
            Assert.False(TaxiStateMachine.CanTransition(TaxiState.Charging, TaxiState.OnRide));
            Assert.False(TaxiStateMachine.CanTransition(TaxiState.Idle, TaxiState.OnRide));
            Assert.False(TaxiStateMachine.CanTransition(TaxiState.Offline, TaxiState.Idle));
            Assert.False(TaxiStateMachine.CanTransition(TaxiState.OnRide, TaxiState.ToCharger));

            var t = new Taxi();
            Assert.False(t.TrySetState(TaxiState.OnRide));
            Assert.Equal(TaxiState.Idle, t.State);
        }

        [Fact]
        public void T06_TripGoesThroughPickupRideAndBackToIdle()
        {
            Simulation sim = Quiet();
            RideRequest r = sim.AddRequest(Node(sim, "jio_world"), Node(sim, "kurla_stn"), patience: 60);
            Assert.True(sim.Submit(Command.Assign(0, r.Id)).Ok);
            Taxi t = sim.Taxis[0];
            Assert.Equal(TaxiState.ToPickup, t.State);
            var seen = new HashSet<TaxiState>();
            RunUntil(sim, () => { seen.Add(t.State); return r.Status == RequestStatus.Completed; });
            Assert.Contains(TaxiState.OnRide, seen);
            Assert.Equal(TaxiState.Idle, t.State);
            Assert.Equal(Node(sim, "kurla_stn"), t.Node);
        }

        // ------------------------------------------------------------------ T08 closures and reroute

        [Fact]
        public void T08_ClosureOnRoute_DelaysTaxi_RerouteAvoidsClosedRoad()
        {
            Simulation sim = Quiet();
            int dest = Node(sim, "bandra_stn");
            Assert.True(sim.Submit(Command.Relocate(0, dest)).Ok);
            Taxi t = sim.Taxis[0];
            int closed = t.RouteEdges[t.RouteEdges.Count - 1]; // the last road into Bandra station
            sim.Schedule(EventType.RoadClosure, sim.Tick, 120, edge: closed);
            sim.Step();
            Assert.Equal(TaxiState.Delayed, t.State);
            Assert.True(t.NeedsReroute);

            CommandResult reroute = sim.Submit(Command.Reroute(0));
            Assert.True(reroute.Ok, reroute.Message);
            Assert.Equal(TaxiState.Relocating, t.State);
            Assert.DoesNotContain(closed, t.RouteEdges.Skip(t.RouteIndex + (t.Progress > 0 ? 1 : 0)));
            RunUntil(sim, () => t.State == TaxiState.Idle);
            Assert.Equal(dest, t.Node);
        }

        [Fact]
        public void T08_IgnoredClosure_IsAutoReroutedAfterAWhile()
        {
            Simulation sim = Quiet();
            Assert.True(sim.Submit(Command.Relocate(0, Node(sim, "bandra_stn"))).Ok);
            Taxi t = sim.Taxis[0];
            sim.Schedule(EventType.RoadClosure, sim.Tick, 300, edge: t.RouteEdges[t.RouteEdges.Count - 1]);
            sim.Step();
            Assert.Equal(TaxiState.Delayed, t.State);
            RunUntil(sim, () => t.State != TaxiState.Delayed, 200);
            Assert.Equal(TaxiState.Relocating, t.State);
        }

        // ------------------------------------------------------------------ T10 energy

        [Fact]
        public void T10_EnergyUseIsProportionalToDistance()
        {
            Simulation sim = Quiet();
            Taxi t = sim.Taxis[0];
            double before = t.Battery;
            Assert.True(sim.Submit(Command.Relocate(0, Node(sim, "kurla_stn"))).Ok);
            RunUntil(sim, () => t.State == TaxiState.Idle);
            double used = before - t.Battery;
            Assert.True(t.OdometerKm > 1.0);
            double perKm = used / t.OdometerKm;
            Assert.InRange(perKm, sim.Config.EnergyPerKm * 0.999, sim.Config.EnergyPerKm * 2.0 * sim.Config.ElevatedEnergyFactor);
        }

        [Fact]
        public void T10_DispatchIsRefusedWhenChargeIsTooLow()
        {
            Simulation sim = Quiet();
            sim.Taxis[0].Battery = 12;
            RideRequest r = sim.AddRequest(Node(sim, "jio_world"), Node(sim, "sclr_airport"));
            CommandResult res = sim.Submit(Command.Assign(0, r.Id));
            Assert.False(res.Ok);
            Assert.Contains("charge", res.Message);
            Assert.Equal(RequestStatus.Waiting, r.Status);
        }

        // ------------------------------------------------------------------ T11 charging

        [Fact]
        public void T11_ChargingAddsTwoPercentPerMinuteAndCostsMoney()
        {
            Simulation sim = Quiet();
            Taxi t = sim.Taxis[0];
            t.Battery = 40;
            Assert.True(sim.Submit(Command.SendCharge(0)).Ok);
            RunUntil(sim, () => t.State == TaxiState.Charging && !t.InChargerQueue);
            sim.Step(); // first charging tick
            double b0 = t.Battery, cash0 = sim.Cash;
            sim.RunTicks(4); // one sim-minute
            Assert.Equal(sim.Config.ChargePerMinute, t.Battery - b0, 6);
            Assert.Equal(-sim.Config.ChargePerMinute * sim.Config.ChargeCostPerPercent, sim.Cash - cash0, 6);
            RunUntil(sim, () => t.State == TaxiState.Idle);
            Assert.Equal(100, t.Battery, 6);
        }

        [Fact]
        public void T11_FullChargerQueuesTheNextTaxi()
        {
            Simulation sim = Quiet();
            int station = Node(sim, "ev_hub_jio"); // 2 chargers
            for (int i = 0; i < 3; i++)
            {
                sim.Taxis[i].Battery = 30;
                Assert.True(sim.Submit(Command.SendCharge(i, station)).Ok);
            }
            RunUntil(sim, () => sim.Taxis.Take(3).All(x => x.State == TaxiState.Charging));
            Assert.Equal(1, sim.Taxis.Take(3).Count(x => x.InChargerQueue));
        }

        // ------------------------------------------------------------------ T12 / T13 money

        [Fact]
        public void T12_FareTimesSurgeIsCreditedOnCompletion()
        {
            Simulation sim = Quiet();
            Assert.True(sim.Submit(Command.SetSurge(2)).Ok); // ×1.5
            RideRequest r = sim.AddRequest(Node(sim, "fleet_hub"), Node(sim, "jio_world"), patience: 60);
            Assert.Equal(1.5, r.Surge);
            double cash0 = sim.Cash;
            Assert.True(sim.Submit(Command.Assign(0, r.Id)).Ok);
            RunUntil(sim, () => r.Status == RequestStatus.Completed);
            Assert.Equal(r.Fare * 1.5, sim.Revenue, 6);
            Assert.True(sim.Cash - cash0 <= r.Fare * 1.5 + 1e-6);
            Assert.True(sim.Cash - cash0 >= r.Fare * 1.5 - sim.Config.MaxLatePenalty - 1e-6);
        }

        [Fact]
        public void T13_AbandonedRequestCostsMoneyAndSatisfaction()
        {
            Simulation sim = Quiet();
            RideRequest r = sim.AddRequest(Node(sim, "nse"), Node(sim, "kalina"), patience: 2);
            double cash0 = sim.Cash, sat0 = sim.Satisfaction;
            sim.RunTicks(12);
            Assert.Equal(RequestStatus.Abandoned, r.Status);
            Assert.Equal(-sim.Config.AbandonPenalty, sim.Cash - cash0, 6);
            Assert.True(sim.Satisfaction < sat0);
        }

        [Fact]
        public void T13_RejectionLowersSatisfactionButCostsNoMoney()
        {
            Simulation sim = Quiet();
            RideRequest r = sim.AddRequest(Node(sim, "nse"), Node(sim, "kalina"));
            double cash0 = sim.Cash, sat0 = sim.Satisfaction;
            Assert.True(sim.Submit(Command.Reject(r.Id)).Ok);
            Assert.Equal(RequestStatus.Rejected, r.Status);
            Assert.Equal(cash0, sim.Cash);
            Assert.Equal(sat0 - 1, sim.Satisfaction, 6);
        }

        // ------------------------------------------------------------------ T15 boundaries

        [Fact]
        public void T15_EmptyBatteryMeansTowFeeThenCharging()
        {
            Simulation sim = Quiet();
            Taxi t = sim.Taxis[0];
            t.Battery = 1.0;
            double cash0 = sim.Cash;
            Assert.True(sim.Submit(Command.Relocate(0, Node(sim, "sclr_airport"))).Ok);
            RunUntil(sim, () => t.State == TaxiState.Offline);
            Assert.Equal(0, t.Battery);
            Assert.Equal(-sim.Config.TowFee, sim.Cash - cash0, 6);
            RunUntil(sim, () => t.State == TaxiState.Charging);
            Assert.True(sim.Graph.Node(t.Node).ChargerSlots > 0);
        }

        [Fact]
        public void T15_BusyTaxiCannotBeAssignedAgain()
        {
            Simulation sim = Quiet();
            RideRequest a = sim.AddRequest(Node(sim, "nse"), Node(sim, "kalina"), patience: 60);
            RideRequest b = sim.AddRequest(Node(sim, "trident"), Node(sim, "kurla_stn"), patience: 60);
            Assert.True(sim.Submit(Command.Assign(0, a.Id)).Ok);
            CommandResult res = sim.Submit(Command.Assign(0, b.Id));
            Assert.False(res.Ok);
            Assert.Contains("busy", res.Message);
            Assert.False(sim.Submit(Command.Assign(1, a.Id)).Ok); // already assigned
            Assert.False(sim.Submit(Command.Assign(99, b.Id)).Ok); // unknown taxi
        }

        [Fact]
        public void T15_QueueNeverExceedsTheLimit()
        {
            var c = new SimConfig { BaseRequestsPerHour = 1500, EventsPerHour = 0, RivalEnabled = false, RivalTaxis = 0, PriceElasticity = 0 };
            var sim = new Simulation(3, Difficulty.Normal, c);
            for (int i = 0; i < 120; i++)
            {
                sim.Step();
                Assert.True(sim.Requests.Count(r => r.Status == RequestStatus.Waiting) <= c.MaxQueue);
            }
            Assert.True(sim.Dropped > 0);
        }

        [Fact]
        public void LosingConditions_EndTheGame()
        {
            var poor = new Simulation(1, Difficulty.Normal, new SimConfig { StartCash = -500, EventsPerHour = 0 });
            poor.Step();
            Assert.Equal(Outcome.Bankrupt, poor.Outcome);

            var idle = new Simulation(7);
            while (!idle.IsOver) idle.Step();
            Assert.Equal(Outcome.Fired, idle.Outcome); // nobody dispatched → passengers abandon → licence cancelled
            Assert.Equal(1, idle.Score().Stars);
        }

        // ------------------------------------------------------------------ dispatch utility

        [Fact]
        public void Utility_PrefersTheNearerTaxi()
        {
            Simulation sim = Quiet();
            int origin = Node(sim, "jio_world");
            sim.Taxis[3].Node = origin;              // right there
            sim.Taxis[1].Node = Node(sim, "vakola"); // far away
            RideRequest r = sim.AddRequest(origin, Node(sim, "kurla_stn"));
            List<DispatchCandidate> ranked = sim.Rank(r.Id);
            Assert.Equal(3, ranked[0].TaxiId);
            Assert.True(ranked[0].Feasible);
            Assert.True(ranked[0].EtaMinutes < 0.01);
            Assert.True(ranked.First(x => x.TaxiId == 1).Utility < ranked[0].Utility);
        }

        [Fact]
        public void Utility_MedicalRequestsWeighTimeMore()
        {
            Simulation sim = Quiet();
            int o = Node(sim, "nse"), d = Node(sim, "asian_heart");
            RideRequest normal = sim.AddRequest(o, d, Urgency.Normal, 30);
            RideRequest medical = sim.AddRequest(o, d, Urgency.Medical, 30);
            DispatchCandidate cn = sim.Evaluate(sim.Taxis[0], normal), cm = sim.Evaluate(sim.Taxis[0], medical);
            Assert.Equal(cn.EtaMinutes, cm.EtaMinutes, 9);
            Assert.Equal(3.0, cm.TimeCost / cn.TimeCost, 6);
            Assert.True(medical.Fare > normal.Fare);
        }

        // ------------------------------------------------------------------ game theory

        [Fact]
        public void Surge_AcceptanceFallsWithPriceAndWaiting()
        {
            var c = new SimConfig();
            double prev = 2;
            foreach (double s in SurgePricing.Levels)
            {
                double a = SurgePricing.Acceptance(c, s);
                Assert.True(a < prev);
                prev = a;
            }
            Assert.True(SurgePricing.Acceptance(c, 1.0, 0.8) < SurgePricing.Acceptance(c, 1.0, 0.1));
        }

        [Fact]
        public void Rival_UndercutsAnExpensivePlayer_AndPlayIsANashEquilibrium()
        {
            var c = new SimConfig();
            Assert.True(SurgePricing.BestResponse(c, 150, 4) < 4); // vs ×2.0, RapidCab charges less

            List<(int player, int rival)> nash = SurgePricing.NashEquilibria(c, 150);
            Assert.NotEmpty(nash);
            foreach ((int p, int r) in nash)
            {
                for (int dev = 0; dev < SurgePricing.Levels.Length; dev++)
                    Assert.True(SurgePricing.ExpectedRevenue(c, 150, dev, r) <= SurgePricing.ExpectedRevenue(c, 150, p, r) + 1e-9);
            }

            // Iterated best responses from the most expensive corner converge to an equilibrium
            int a = 4, b = 4;
            for (int i = 0; i < 20; i++) { a = SurgePricing.BestResponse(c, 150, b); b = SurgePricing.BestResponse(c, 150, a); }
            Assert.Contains((a, b), nash);
        }

        [Fact]
        public void Rival_WinsSomeRequests_OnlyWhenEnabled()
        {
            Simulation with = AutoDispatcher.PlayShift(5);
            Assert.True(with.LostToRival > 0);
            Assert.True(with.RivalTrips > 0);

            var off = new Simulation(5, Difficulty.Normal, new SimConfig { RivalEnabled = false, RivalTaxis = 0 });
            off.RunTicks(800);
            Assert.Equal(0, off.LostToRival);
        }

        // ------------------------------------------------------------------ determinism, save / replay

        [Fact]
        public void Determinism_SameSeedSameCommands_SameState()
        {
            Simulation a = AutoDispatcher.PlayShift(11);
            Simulation b = AutoDispatcher.PlayShift(11);
            Assert.Equal(a.StateHash(), b.StateHash());
            Assert.Equal(a.Cash, b.Cash);
            Assert.NotEqual(a.StateHash(), AutoDispatcher.PlayShift(12).StateHash());
        }

        [Fact]
        public void T14_SaveThenLoad_ReplaysToTheIdenticalState()
        {
            var sim = new Simulation(21, Difficulty.Hard);
            for (int i = 0; i < 900; i++) { if (i % 4 == 0) AutoDispatcher.Run(sim); sim.Step(); }
            sim.Submit(Command.SetSurge(1)); // a command at the save tick itself

            string save = SaveGame.Write(sim);
            Simulation loaded = SaveGame.Load(save);
            Assert.Equal(sim.Tick, loaded.Tick);
            Assert.Equal(sim.StateHash(), loaded.StateHash());

            // Both continue identically
            for (int i = 0; i < 300; i++)
            {
                if (i % 4 == 0) { AutoDispatcher.Run(sim); AutoDispatcher.Run(loaded); }
                sim.Step();
                loaded.Step();
            }
            Assert.Equal(sim.StateHash(), loaded.StateHash());
        }

        [Fact]
        public void T14_TamperedSaveIsDetected()
        {
            var sim = new Simulation(4);
            for (int i = 0; i < 400; i++) { if (i % 4 == 0) AutoDispatcher.Run(sim); sim.Step(); }
            string save = SaveGame.Write(sim);
            string tampered = save.Replace("seed=4", "seed=5");
            Assert.Throws<InvalidOperationException>(() => SaveGame.Load(tampered));
            Assert.Throws<FormatException>(() => SaveGame.Load("hello"));
        }

        // ------------------------------------------------------------------ T16 long runs, T18 golden

        [Fact]
        public void T16_TenFullShifts_RunCleanly_WithSaneInvariants()
        {
            for (ulong seed = 100; seed < 110; seed++)
            {
                Difficulty d = (Difficulty)(seed % 3);
                Simulation sim = AutoDispatcher.PlayShift(seed, d);
                Assert.True(sim.IsOver);
                if (sim.Outcome == Outcome.ShiftComplete) Assert.Equal(sim.Config.TicksPerShift, sim.Tick);
                Assert.InRange(sim.Satisfaction, 0, 100);
                foreach (Taxi t in sim.Taxis) Assert.InRange(t.Battery, 0, 100);
                Assert.True(sim.Completed > 50, $"seed {seed}: only {sim.Completed} trips");
                Assert.Equal(sim.Completed, sim.Requests.Count(r => r.Status == RequestStatus.Completed));
                ScoreCard s = sim.Score();
                Assert.InRange(s.Stars, 1, 5);
            }
        }

        [Fact]
        public void AutopilotBeatsDoingNothing()
        {
            var idle = new Simulation(8);
            while (!idle.IsOver) idle.Step();
            Simulation played = AutoDispatcher.PlayShift(8);
            Assert.True(played.Score().Score > idle.Score().Score);
            Assert.Equal(Outcome.ShiftComplete, played.Outcome);
        }

        /// <summary>
        /// T18 regression: a whole autopilot shift must produce exactly this state. If you change the rules on
        /// purpose, update the constant (and note the change in CHANGELOG / docs/06-game-design-balancing.md).
        /// </summary>
        [Fact]
        public void T18_GoldenShiftHash()
        {
            Simulation sim = AutoDispatcher.PlayShift(42);
            Assert.Equal(GoldenHash, sim.StateHash().ToString("X16"));
        }

        public const string GoldenHash = "82BB4CC3BD90BD1C";
    }
}
