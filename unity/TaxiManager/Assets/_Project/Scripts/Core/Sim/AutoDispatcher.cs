using System.Collections.Generic;

namespace TaxiSim.Core.Sim
{
    /// <summary>
    /// A simple "autopilot" dispatcher that plays the game with the same commands a player has:
    /// reroute blocked taxis, charge low ones, give each waiting request (most urgent first) to the taxi with
    /// the highest utility, decline requests nobody can reach in time (a rejection costs less than an
    /// abandonment), and spread idle taxis over the demand hotspots. Used by the long-run tests, as the AI
    /// baseline for balancing, and as an optional autopilot / hint in the game.
    /// </summary>
    public static class AutoDispatcher
    {
        public static readonly string[] Hotspots = { "kurla_stn", "jio_world", "bandra_stn", "diamond_bourse", "equinox", "one_bkc", "phoenix", "kalina" };

        public static int Run(Simulation sim, double chargeBelow = 28.0)
        {
            int commands = 0;

            // Surge control: price the queue down when demand outruns the fleet, back up when it clears
            int queued = 0, free = 0;
            foreach (RideRequest r in sim.Requests) if (r.Status == RequestStatus.Waiting) queued++;
            foreach (Taxi t in sim.Taxis) if (t.State == TaxiState.Idle || t.State == TaxiState.Relocating) free++;
            int surge = sim.SurgeLevel;
            if (queued > free + 2 && surge < 3) surge++;
            else if (queued == 0 && free >= 2 && surge > 0) surge--;
            if (surge != sim.SurgeLevel && sim.Submit(Command.SetSurge(surge)).Ok) commands++;

            foreach (Taxi t in sim.Taxis)
            {
                if (t.State == TaxiState.Delayed && t.NeedsReroute && sim.Submit(Command.Reroute(t.Id)).Ok) commands++;
                else if (t.State == TaxiState.Idle && t.Battery < chargeBelow && sim.Submit(Command.SendCharge(t.Id)).Ok) commands++;
            }

            var waiting = new List<RideRequest>();
            foreach (RideRequest r in sim.Requests) if (r.Status == RequestStatus.Waiting) waiting.Add(r);
            waiting.Sort((a, b) =>
            {
                int c = b.Urgency.CompareTo(a.Urgency);
                return c != 0 ? c : a.PatienceLeft.CompareTo(b.PatienceLeft);
            });
            foreach (RideRequest r in waiting)
            {
                List<DispatchCandidate> ranked = sim.Rank(r.Id);
                if (ranked.Count == 0 || !ranked[0].Feasible) continue;
                DispatchCandidate best = ranked[0];
                if (best.EtaMinutes > r.PatienceLeft + 1.0)
                {
                    // Nobody can get there in time: decline now rather than let the passenger give up
                    bool anyBusySoon = false;
                    foreach (Taxi t in sim.Taxis) if (t.State == TaxiState.OnRide) anyBusySoon = true;
                    if (!anyBusySoon || r.PatienceLeft < 3.0) { if (sim.Submit(Command.Reject(r.Id)).Ok) commands++; }
                    continue;
                }
                if (sim.Submit(Command.Assign(best.TaxiId, r.Id)).Ok) commands++;
            }

            // Spread idle taxis over the hotspots (one per hotspot, in order)
            var covered = new HashSet<int>();
            foreach (Taxi t in sim.Taxis)
                if (t.State == TaxiState.Idle || t.State == TaxiState.Relocating) covered.Add(t.TargetNode >= 0 && t.State == TaxiState.Relocating ? t.TargetNode : t.Node);
            foreach (Taxi t in sim.Taxis)
            {
                if (t.State != TaxiState.Idle || t.Battery < chargeBelow + 10) continue;
                if (IsHotspot(sim, t.Node)) continue;
                foreach (string key in Hotspots)
                {
                    int node = sim.Graph.Node(key).Id;
                    if (covered.Contains(node)) continue;
                    if (sim.Submit(Command.Relocate(t.Id, node)).Ok) { commands++; covered.Add(node); }
                    break;
                }
            }
            return commands;
        }

        private static bool IsHotspot(Simulation sim, int node)
        {
            foreach (string key in Hotspots) if (sim.Graph.Node(key).Id == node) return true;
            return false;
        }

        /// <summary>Plays a whole shift with the autopilot (dispatching every <paramref name="everyTicks"/> ticks).</summary>
        public static Simulation PlayShift(ulong seed, Difficulty d = Difficulty.Normal, int everyTicks = 4)
        {
            var sim = new Simulation(seed, d);
            while (!sim.IsOver)
            {
                if (sim.Tick % everyTicks == 0) Run(sim);
                sim.Step();
            }
            return sim;
        }
    }
}
