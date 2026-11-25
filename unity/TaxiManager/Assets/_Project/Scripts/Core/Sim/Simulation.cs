using System;
using System.Collections.Generic;

namespace TaxiSim.Core.Sim
{
    public enum Outcome
    {
        Running,
        ShiftComplete,
        Bankrupt,
        Fired          // satisfaction collapsed
    }

    public enum EventType
    {
        RoadClosure,
        HeavyTraffic,
        Rain,
        Festival,
        VehicleIssue,
        ChargingQueue,
        DemandSurge
    }

    public sealed class TrafficEvent
    {
        public int Id;
        public EventType Type;
        public int Edge = -1, Node = -1, Taxi = -1;
        public int StartTick, EndTick;
        public bool Started, Ended;
        public string Description = "";
    }

    public sealed class ScoreCard
    {
        public double Profit, ProfitScore, Satisfaction, Efficiency, Score;
        public int Stars, Completed, Abandoned, Rejected, LostToRival, Declined;
        public Outcome Outcome;
        public override string ToString() =>
            $"{Outcome}: {Stars}★ score {Score:0.00} (profit ₹{Profit:0}, satisfaction {Satisfaction:0}, efficiency {Efficiency:P0}, {Completed} trips)";
    }

    /// <summary>
    /// The deterministic fleet-dispatch simulation (spec §4, §18). No UnityEngine: the same code runs in
    /// Unity, in the xUnit tests and in the replay of a save file. <see cref="Step"/> is the only thing that
    /// advances time; <see cref="Submit"/> is the only way the player changes anything. Same seed + same
    /// command log ⇒ bit-identical state (checked with <see cref="StateHash"/>).
    /// </summary>
    public sealed class Simulation
    {
        public SimConfig Config { get; }
        public Difficulty Difficulty { get; }
        public ulong Seed { get; }
        public CityGraph Graph { get; }
        public int Tick { get; private set; }
        public Outcome Outcome { get; private set; } = Outcome.Running;
        public bool IsOver => Outcome != Outcome.Running;

        public double Cash { get; private set; }
        public double Revenue { get; private set; }
        public double Costs { get; private set; }
        public double Penalties { get; private set; }
        public double Satisfaction { get; private set; }
        public int SurgeLevel { get; private set; }
        public int RivalSurgeLevel { get; private set; }
        public double RivalRevenue { get; private set; }
        public int Completed, Abandoned, Rejected, LostToRival, Declined, Dropped, RivalTrips;

        public readonly List<Taxi> Taxis = new List<Taxi>();       // player fleet
        public readonly List<Taxi> Rivals = new List<Taxi>();      // RapidCab
        public readonly List<RideRequest> Requests = new List<RideRequest>();
        public readonly List<TrafficEvent> Events = new List<TrafficEvent>();
        public readonly List<Command> CommandLog = new List<Command>();
        public readonly List<SimMessage> Log = new List<SimMessage>();
        public int MaxLog = 400;

        public double MinutesElapsed => Tick * Config.MinutesPerTick;
        public double Hour => Config.ShiftStartHour + MinutesElapsed / 60.0;
        public string ClockText { get { int h = (int)Hour, m = (int)((Hour - h) * 60.0); return $"{h:00}:{m:00}"; } }
        public bool Raining { get; private set; }

        private readonly Pcg32 _rng;
        private readonly double[] _edgeNoise;
        private readonly List<int>[] _charging;  // per node: taxis using a charger
        private readonly List<int>[] _queue;     // per node: taxis waiting for a charger
        private readonly int _hospital, _depot;
        private int _nextRequestId, _nextEventId;
        private double _busyMinutes;

        public Simulation(ulong seed, Difficulty difficulty = Difficulty.Normal, SimConfig config = null)
        {
            Seed = seed;
            Difficulty = difficulty;
            Config = config ?? SimConfig.For(difficulty);
            Graph = BkcMap.Create().Graph;
            _rng = new Pcg32(seed, 1013UL);
            int n = Graph.Nodes.Count;
            _charging = new List<int>[n];
            _queue = new List<int>[n];
            for (int i = 0; i < n; i++) { _charging[i] = new List<int>(); _queue[i] = new List<int>(); }
            _edgeNoise = new double[Graph.Edges.Count];
            for (int i = 0; i < _edgeNoise.Length; i++) _edgeNoise[i] = _rng.Range(0.92, 1.08);
            _hospital = FindNode("asian_heart");
            _depot = FindNode(Config.DepotKey);

            Cash = Config.StartCash;
            Satisfaction = Config.StartSatisfaction;
            for (int i = 0; i < Config.PlayerTaxis; i++)
            {
                Taxis.Add(new Taxi
                {
                    Id = i, Label = $"AT-{i + 1:00}", Owner = Owner.Player, Node = _depot >= 0 ? _depot : 0,
                    Battery = Config.StartBattery - i * 2.0
                });
            }
            for (int i = 0; i < Config.RivalTaxis; i++)
                Rivals.Add(new Taxi { Id = 100 + i, Label = $"RC-{i + 1:00}", Owner = Owner.Rival, Node = _rng.Range(0, n), Battery = 100 });

            double fee = Config.DailyFeePerTaxi * Config.PlayerTaxis;
            Cash -= fee;
            Costs += fee;
            UpdateTraffic();
            Say(MessageKind.Info, $"Shift starts at {ClockText}. {Config.PlayerTaxis} autonomous taxis ready at the AT Fleet Hub. Daily fee ₹{fee:0}.");
        }

        private int FindNode(string key)
        {
            foreach (CityNode node in Graph.Nodes) if (node.Key == key) return node.Id;
            return -1;
        }

        // ================================================================== commands

        /// <summary>Applies a player command now (at the current tick) and records it in the log.</summary>
        public CommandResult Submit(Command cmd)
        {
            Command stamped = cmd.At(Tick);
            CommandLog.Add(stamped);
            CommandResult r = Apply(stamped);
            Say(r.Ok ? MessageKind.Info : MessageKind.Warning, r.Message);
            return r;
        }

        private CommandResult Apply(Command c)
        {
            if (IsOver) return CommandResult.Fail("The shift is over.");
            switch (c.Type)
            {
                case CommandType.Assign: return Assign(c.A, c.B);
                case CommandType.Reject: return Reject(c.A);
                case CommandType.SendCharge: return SendCharge(c.A, c.B);
                case CommandType.Reroute: return Reroute(c.A);
                case CommandType.Relocate: return Relocate(c.A, c.B);
                case CommandType.ReleaseCharger: return ReleaseCharger(c.A);
                case CommandType.SetSurge:
                    if (c.A < 0 || c.A >= SurgePricing.Levels.Length) return CommandResult.Fail("Unknown surge level.");
                    SurgeLevel = c.A;
                    return CommandResult.Success($"Surge set to ×{SurgePricing.Multiplier(c.A):0.00}.");
            }
            return CommandResult.Fail("Unknown command.");
        }

        private Taxi PlayerTaxi(int id) => id >= 0 && id < Taxis.Count ? Taxis[id] : null;
        public RideRequest Request(int id) => id >= 0 && id < Requests.Count ? Requests[id] : null;

        private CommandResult Assign(int taxiId, int requestId)
        {
            Taxi t = PlayerTaxi(taxiId);
            RideRequest r = Request(requestId);
            if (t == null) return CommandResult.Fail("Unknown taxi.");
            if (r == null || r.Status != RequestStatus.Waiting) return CommandResult.Fail("That request is no longer waiting.");
            DispatchCandidate cand = Evaluate(t, r);
            if (!cand.Feasible) return CommandResult.Fail($"{t.Label} can't take request #{r.Id}: {cand.Reason}");

            r.Status = RequestStatus.Assigned;
            r.TaxiId = t.Id;
            t.RequestId = r.Id;
            if (!t.TrySetState(TaxiState.ToPickup)) return CommandResult.Fail($"{t.Label} is {t.State}.");
            SetRoute(t, r.Origin);
            if (!t.IsMoving) Arrive(t); // already at the pickup point
            return CommandResult.Success($"{t.Label} → request #{r.Id} ({Graph.Node(r.Origin).ShortName} → {Graph.Node(r.Destination).ShortName}), ETA {cand.EtaMinutes:0} min.");
        }

        private CommandResult Reject(int requestId)
        {
            RideRequest r = Request(requestId);
            if (r == null || !r.IsOpen) return CommandResult.Fail("That request is no longer open.");
            if (r.TaxiId >= 0) StandDown(PlayerTaxi(r.TaxiId));
            r.Status = RequestStatus.Rejected;
            Rejected++;
            ChangeSatisfaction(-1);
            return CommandResult.Success($"Request #{r.Id} rejected.");
        }

        private CommandResult SendCharge(int taxiId, int station)
        {
            Taxi t = PlayerTaxi(taxiId);
            if (t == null) return CommandResult.Fail("Unknown taxi.");
            if (t.State == TaxiState.OnRide || t.State == TaxiState.ToPickup) return CommandResult.Fail($"{t.Label} has a passenger job.");
            if (t.State == TaxiState.Charging || t.State == TaxiState.ToCharger) return CommandResult.Fail($"{t.Label} is already charging.");
            if (t.State == TaxiState.Offline) return CommandResult.Fail($"{t.Label} is being towed.");
            if (station < 0) station = NearestCharger(t);
            if (station < 0 || Graph.Node(station).ChargerSlots <= 0) return CommandResult.Fail("No charging station.");
            RouteResult route = RouteFinder.FindFastest(Graph, StartNode(t), station);
            if (!route.Success) return CommandResult.Fail(route.FailureReason);
            if (!t.TrySetState(TaxiState.ToCharger)) return CommandResult.Fail($"{t.Label} is {t.State}.");
            t.NeedsReroute = false;
            t.ChargerNode = station;
            SetRoute(t, station);
            if (!t.IsMoving) Arrive(t);
            return CommandResult.Success($"{t.Label} → charging at {Graph.Node(station).ShortName}.");
        }

        private CommandResult Reroute(int taxiId)
        {
            Taxi t = PlayerTaxi(taxiId);
            if (t == null) return CommandResult.Fail("Unknown taxi.");
            if (t.TargetNode < 0) return CommandResult.Fail($"{t.Label} has nowhere to go.");
            bool delayed = t.State == TaxiState.Delayed;
            if (delayed && !t.NeedsReroute) return CommandResult.Fail($"{t.Label} is stopped ({t.LastIssue}); it will continue by itself.");
            RouteResult route = RouteFinder.FindFastest(Graph, StartNode(t), t.TargetNode);
            if (!route.Success) return CommandResult.Fail($"{t.Label}: {route.FailureReason}");
            SetRoute(t, t.TargetNode);
            if (delayed)
            {
                t.NeedsReroute = false;
                t.TrySetState(t.ResumeState);
            }
            if (!t.IsMoving) Arrive(t);
            return CommandResult.Success($"{t.Label} rerouted: {route.Minutes:0} min to {Graph.Node(t.TargetNode).ShortName}.");
        }

        private CommandResult Relocate(int taxiId, int node)
        {
            Taxi t = PlayerTaxi(taxiId);
            if (t == null) return CommandResult.Fail("Unknown taxi.");
            if (node < 0 || node >= Graph.Nodes.Count) return CommandResult.Fail("Unknown location.");
            if (t.State != TaxiState.Idle && t.State != TaxiState.Relocating) return CommandResult.Fail($"{t.Label} is busy ({t.State}).");
            RouteResult route = RouteFinder.FindFastest(Graph, StartNode(t), node);
            if (!route.Success) return CommandResult.Fail(route.FailureReason);
            t.TrySetState(TaxiState.Relocating);
            SetRoute(t, node);
            if (!t.IsMoving) Arrive(t);
            return CommandResult.Success($"{t.Label} repositioning to {Graph.Node(node).ShortName}.");
        }

        private CommandResult ReleaseCharger(int taxiId)
        {
            Taxi t = PlayerTaxi(taxiId);
            if (t == null || t.State != TaxiState.Charging) return CommandResult.Fail("That taxi isn't charging.");
            if (t.Battery < Config.LowBattery) return CommandResult.Fail($"{t.Label} needs at least {Config.LowBattery:0}% first.");
            LeaveCharger(t);
            t.TrySetState(TaxiState.Idle);
            return CommandResult.Success($"{t.Label} released at {t.Battery:0}%.");
        }

        // ================================================================== dispatch ranking

        /// <summary>All player taxis ranked by utility for a request (best first). Infeasible ones are at the end with a reason.</summary>
        public List<DispatchCandidate> Rank(int requestId)
        {
            var list = new List<DispatchCandidate>();
            RideRequest r = Request(requestId);
            if (r == null) return list;
            foreach (Taxi t in Taxis) list.Add(Evaluate(t, r));
            list.Sort((a, b) =>
            {
                if (a.Feasible != b.Feasible) return a.Feasible ? -1 : 1;
                int c = b.Utility.CompareTo(a.Utility);
                return c != 0 ? c : a.TaxiId.CompareTo(b.TaxiId);
            });
            return list;
        }

        public DispatchCandidate Evaluate(Taxi t, RideRequest r)
        {
            var c = new DispatchCandidate { TaxiId = t.Id };
            if (t.State != TaxiState.Idle && t.State != TaxiState.Relocating) { c.Reason = $"busy ({t.State})"; return c; }
            if (!r.IsOpen) { c.Reason = "request closed"; return c; }
            RouteResult toPickup = RouteFinder.FindFastest(Graph, StartNode(t), r.Origin);
            RouteResult trip = RouteFinder.FindFastest(Graph, r.Origin, r.Destination);
            if (!toPickup.Success || !trip.Success) { c.Reason = "no open road"; return c; }
            double partialMin = 0, partialKm = 0;
            if (OnEdge(t))
            {
                RoadEdge e = Graph.Edges[t.CurrentEdge];
                partialMin = (1 - t.Progress) * e.CurrentMinutes;
                partialKm = (1 - t.Progress) * e.LengthKm;
            }
            c.EtaMinutes = partialMin + toPickup.Minutes;
            c.PickupKm = partialKm + toPickup.Km;
            c.TripKm = trip.Km;
            c.EnergyNeeded = EnergyFor(toPickup, partialKm) + EnergyFor(trip, 0);
            if (t.Battery - c.EnergyNeeded < Config.DispatchReserve)
            {
                c.Reason = $"not enough charge (needs {c.EnergyNeeded + Config.DispatchReserve:0}%, has {t.Battery:0}%)";
                return c;
            }
            c.Feasible = true;
            c.Revenue = r.Fare * r.Surge;
            c.EnergyCost = c.EnergyNeeded * Config.ChargeCostPerPercent;
            c.TimeCost = c.EtaMinutes * r.UrgencyWeight;
            c.AbandonRisk = 1.0 / (1.0 + Math.Exp(-(c.EtaMinutes - r.PatienceLeft) / 1.5));
            c.SatisfactionGain = c.EtaMinutes < r.PatienceLeft ? 2.0 * r.UrgencyWeight : -5.0;
            c.Utility = c.Revenue - Config.UtilityEnergyWeight * c.EnergyCost - Config.UtilityTimeWeight * c.TimeCost
                        - Config.UtilityAbandonWeight * c.AbandonRisk + Config.UtilitySatisfactionWeight * c.SatisfactionGain;
            return c;
        }

        private double EnergyFor(RouteResult route, double extraKm)
        {
            double e = extraKm * Config.EnergyPerKm;
            foreach (int id in route.Edges) e += EdgeEnergy(Graph.Edges[id], 1.0);
            return e;
        }

        private double EdgeEnergy(RoadEdge e, double fraction)
        {
            double km = e.LengthKm * fraction;
            return km * Config.EnergyPerKm * (1.0 + Config.CongestionEnergyFactor * (e.TrafficMultiplier - 1.0)) * (e.Elevated ? Config.ElevatedEnergyFactor : 1.0);
        }

        // ================================================================== time step

        /// <summary>Advances the world by one tick (15 sim-seconds).</summary>
        public void Step()
        {
            if (IsOver) return;
            double dt = Config.MinutesPerTick;

            UpdateEvents();
            UpdateTraffic();
            if (Tick % (int)(60 / dt) == 0 && Tick > 0) RivalPricing();
            GenerateRequests(dt);
            UpdateRequests(dt);
            foreach (Taxi t in Taxis) UpdateTaxi(t, dt);
            foreach (Taxi t in Rivals) UpdateTaxi(t, dt);

            Tick++;
            if (Cash < Config.BankruptcyCash) End(Outcome.Bankrupt, "Out of money — the fleet operator has gone bankrupt.");
            else if (Satisfaction < Config.FireBelowSatisfaction) End(Outcome.Fired, "Customer satisfaction collapsed — the city cancelled your licence.");
            else if (Tick >= Config.TicksPerShift) End(Outcome.ShiftComplete, $"Shift complete at {ClockText}. {Score()}");
        }

        public void RunTicks(int ticks)
        {
            for (int i = 0; i < ticks && !IsOver; i++) Step();
        }

        private void End(Outcome o, string text)
        {
            Outcome = o;
            Say(MessageKind.GameOver, text);
        }

        // ------------------------------------------------------------------ traffic and events

        private static double Bump(double x, double centre, double width) => Math.Max(0.0, 1.0 - Math.Abs(x - centre) / width);

        private void UpdateTraffic()
        {
            double h = Hour;
            double rush = Math.Max(Bump(h, 9.25, 1.75), Bump(h, 18.75, 2.25)) + 0.3 * Bump(h, 13.5, 1.2);
            foreach (RoadEdge e in Graph.Edges)
            {
                double classFactor = e.Class == RoadClass.Local ? 0.5 : e.Class == RoadClass.Main ? 0.8 : 1.0;
                double m = (1.0 + Config.RushAmplitude * rush * classFactor) * _edgeNoise[e.Id];
                bool blocked = false;
                foreach (TrafficEvent ev in Events)
                {
                    if (!ev.Started || ev.Ended) continue;
                    switch (ev.Type)
                    {
                        case EventType.RoadClosure: if (ev.Edge == e.Id) blocked = true; break;
                        case EventType.HeavyTraffic:
                            if (ev.Edge == e.Id) m *= 2.2;
                            else if (Graph.Edges[ev.Edge].Touches(e.A) || Graph.Edges[ev.Edge].Touches(e.B)) m *= 1.35;
                            break;
                        case EventType.Rain: m *= 1.35; break;
                        case EventType.Festival:
                            if (ev.Edge == e.Id) blocked = true;
                            else if (e.Touches(ev.Node)) m *= 1.8;
                            break;
                    }
                }
                e.TrafficMultiplier = Math.Max(1.0, Math.Min(3.0, m));
                e.Blocked = blocked;
            }
        }

        /// <summary>Adds a scripted event (scenario files, tests, the demo). Random events are added the same way.</summary>
        public TrafficEvent Schedule(EventType type, int startTick, double minutes, int edge = -1, int node = -1, int taxi = -1)
        {
            var ev = new TrafficEvent
            {
                Id = _nextEventId++, Type = type, Edge = edge, Node = node, Taxi = taxi,
                StartTick = startTick, EndTick = startTick + (int)Math.Round(minutes / Config.MinutesPerTick)
            };
            Events.Add(ev);
            return ev;
        }

        private void UpdateEvents()
        {
            // Random events
            int active = 0;
            bool rainActive = false, festivalActive = false;
            foreach (TrafficEvent ev in Events)
                if (!ev.Ended) { active++; rainActive |= ev.Type == EventType.Rain; festivalActive |= ev.Type == EventType.Festival; }
            if (active < Config.MaxActiveEvents && _rng.Chance(Config.EventsPerHour * Config.MinutesPerTick / 60.0))
            {
                double roll = _rng.NextDouble();
                if (roll < 0.18) Schedule(EventType.RoadClosure, Tick, _rng.Range(20.0, 60.0), edge: _rng.Range(0, Graph.Edges.Count));
                else if (roll < 0.40) Schedule(EventType.HeavyTraffic, Tick, _rng.Range(20.0, 50.0), edge: _rng.Range(0, Graph.Edges.Count));
                else if (roll < 0.52 && !rainActive) Schedule(EventType.Rain, Tick, _rng.Range(60.0, 150.0));
                else if (roll < 0.60 && !festivalActive) Schedule(EventType.Festival, Tick, _rng.Range(90.0, 180.0), node: FindNode("mmrda"));
                else if (roll < 0.75) Schedule(EventType.VehicleIssue, Tick, _rng.Range(10.0, 20.0), taxi: _rng.Range(0, Taxis.Count));
                else if (roll < 0.85) Schedule(EventType.ChargingQueue, Tick, _rng.Range(30.0, 60.0), node: ChargerNodes()[_rng.Range(0, ChargerNodes().Count)]);
                else Schedule(EventType.DemandSurge, Tick, _rng.Range(30.0, 60.0), node: _rng.Range(0, Graph.Nodes.Count));
            }

            foreach (TrafficEvent ev in Events)
            {
                if (!ev.Started && Tick >= ev.StartTick) StartEvent(ev);
                if (ev.Started && !ev.Ended && Tick >= ev.EndTick)
                {
                    ev.Ended = true;
                    if (ev.Type == EventType.Rain) Raining = false;
                    Say(MessageKind.Event, $"Cleared: {ev.Description}");
                }
            }
        }

        private void StartEvent(TrafficEvent ev)
        {
            ev.Started = true;
            switch (ev.Type)
            {
                case EventType.RoadClosure:
                    ev.Description = $"Road closed: {Graph.Edges[ev.Edge].Name}";
                    break;
                case EventType.HeavyTraffic:
                    ev.Description = $"Heavy traffic on {Graph.Edges[ev.Edge].Name}";
                    break;
                case EventType.Rain:
                    ev.Description = "Monsoon downpour: slower roads, more requests, less patience";
                    Raining = true;
                    break;
                case EventType.Festival:
                    if (ev.Node < 0) ev.Node = 0;
                    IReadOnlyList<RoadEdge> roads = Graph.EdgesAt(ev.Node);
                    ev.Edge = roads[_rng.Range(0, roads.Count)].Id;
                    ev.Description = $"Festival at {Graph.Node(ev.Node).ShortName}: {Graph.Edges[ev.Edge].Name} closed, crowds";
                    break;
                case EventType.VehicleIssue:
                {
                    Taxi t = PlayerTaxi(ev.Taxi);
                    ev.Description = t != null ? $"{t.Label}: sensor fault, stopped for checks" : "Vehicle issue";
                    if (t != null && t.IsMoving && t.State != TaxiState.Delayed)
                    {
                        t.ResumeState = t.State;
                        if (t.TrySetState(TaxiState.Delayed)) { t.DelayLeft = (ev.EndTick - ev.StartTick) * Config.MinutesPerTick; t.LastIssue = "sensor fault"; }
                    }
                    break;
                }
                case EventType.ChargingQueue:
                    ev.Description = $"Charger fault at {Graph.Node(ev.Node).ShortName}: one charger out of service";
                    break;
                case EventType.DemandSurge:
                    ev.Description = $"Demand surge at {Graph.Node(ev.Node).ShortName}";
                    break;
            }
            Say(MessageKind.Event, ev.Description);
            UpdateTraffic();
            // Taxis whose route now crosses a closed road stop and ask for a reroute
            foreach (Taxi t in Taxis)
            {
                if (!t.IsMoving || t.State == TaxiState.Delayed) continue;
                for (int i = t.RouteIndex + (t.Progress > 0 ? 1 : 0); i < t.RouteEdges.Count; i++)
                {
                    if (!Graph.Edges[t.RouteEdges[i]].Blocked) continue;
                    StopForReroute(t);
                    break;
                }
            }
        }

        private void StopForReroute(Taxi t)
        {
            t.ResumeState = t.State;
            if (!t.TrySetState(TaxiState.Delayed)) return;
            t.NeedsReroute = true;
            t.DelayLeft = 10.0; // auto-reroute if the player doesn't
            t.LastIssue = "road closed ahead";
            Say(MessageKind.Warning, $"{t.Label} is blocked by a closure — press REROUTE.");
        }

        private List<int> ChargerNodes()
        {
            var list = new List<int>();
            foreach (CityNode n in Graph.Nodes) if (n.ChargerSlots > 0) list.Add(n.Id);
            return list;
        }

        private int ChargerCapacity(int node)
        {
            int slots = Graph.Node(node).ChargerSlots;
            foreach (TrafficEvent ev in Events)
                if (ev.Started && !ev.Ended && ev.Type == EventType.ChargingQueue && ev.Node == node) slots--;
            return Math.Max(1, slots);
        }

        // ------------------------------------------------------------------ pricing competition

        private void RivalPricing()
        {
            if (!Config.RivalEnabled || Rivals.Count == 0) return;
            int best = SurgePricing.BestResponse(Config, 150.0, SurgeLevel);
            if (best != RivalSurgeLevel)
            {
                RivalSurgeLevel = best;
                Say(MessageKind.Money, $"RapidCab changed its price to ×{SurgePricing.Multiplier(best):0.00} (your price ×{SurgePricing.Multiplier(SurgeLevel):0.00}).");
            }
        }

        // ------------------------------------------------------------------ demand

        private double OriginWeight(CityNode n, double h, out double destWeight)
        {
            double m = Bump(h, 9.0, 2.5), e = Bump(h, 18.5, 3.0);
            double o, d;
            switch (n.Zone)
            {
                case ZoneType.RailHub: o = 1.4 + 2.0 * m + 0.8 * e; d = 1.0 + 0.4 * m + 2.0 * e; break;
                case ZoneType.TransitHub: o = 1.0 + 1.2 * m + 0.6 * e; d = 0.8 + 0.4 * m + 1.4 * e; break;
                case ZoneType.Financial:
                case ZoneType.Commercial:
                case ZoneType.Government: o = 0.6 + 0.2 * m + 2.2 * e; d = 0.6 + 2.2 * m + 0.3 * e; break;
                case ZoneType.Residential: o = 0.8 + 2.0 * m + 0.3 * e; d = 0.6 + 0.3 * m + 2.0 * e; break;
                case ZoneType.Education: o = 0.8 + 0.6 * m + 1.2 * e; d = 0.6 + 1.6 * m + 0.3 * e; break;
                case ZoneType.Hospital: o = 0.8; d = 0.9; break;
                case ZoneType.Event:
                case ZoneType.Hotel: o = 0.5 + 1.2 * e; d = 0.6 + 1.0 * e; break;
                case ZoneType.Airport: o = 1.2; d = 1.2; break;
                case ZoneType.Junction: o = 0.3; d = 0.3; break;
                default: o = 0.1; d = 0.1; break;
            }
            foreach (TrafficEvent ev in Events)
            {
                if (!ev.Started || ev.Ended || ev.Node != n.Id) continue;
                if (ev.Type == EventType.DemandSurge) o *= 2.5;
                if (ev.Type == EventType.Festival) { o *= 3.0; d *= 1.5; }
            }
            destWeight = d;
            return o;
        }

        private int PickWeighted(double[] w)
        {
            double total = 0;
            foreach (double v in w) total += v;
            double r = _rng.NextDouble() * total;
            for (int i = 0; i < w.Length; i++) { r -= w[i]; if (r < 0) return i; }
            return w.Length - 1;
        }

        private void GenerateRequests(double dt)
        {
            double h = Hour;
            double profile = 0.6 + 0.9 * Bump(h, 9.0, 1.5) + 0.8 * Bump(h, 18.5, 2.0) + 0.3 * Bump(h, 13.5, 1.2);
            double mean = Config.BaseRequestsPerHour * profile * (Raining ? 1.3 : 1.0) * dt / 60.0;
            int count = (int)mean;
            if (_rng.Chance(mean - count)) count++;
            for (int k = 0; k < count; k++) NewRequest(h);
        }

        private void NewRequest(double h)
        {
            int n = Graph.Nodes.Count;
            var ow = new double[n];
            var dw = new double[n];
            for (int i = 0; i < n; i++) ow[i] = OriginWeight(Graph.Node(i), h, out dw[i]);
            int origin = PickWeighted(ow);
            dw[origin] = 0;
            double u = _rng.NextDouble();
            Urgency urgency = u < Config.MedicalShare ? Urgency.Medical : u < Config.MedicalShare + Config.PriorityShare ? Urgency.Priority : Urgency.Normal;
            int dest = urgency == Urgency.Medical && origin != _hospital && _hospital >= 0 ? _hospital : PickWeighted(dw);

            RouteResult trip = RouteFinder.FindFastest(Graph, origin, dest);
            if (!trip.Success) return;
            double freeFlow = 0;
            foreach (int id in trip.Edges) freeFlow += Graph.Edges[id].BaseMinutes;
            double fare = Config.FareBase + Config.FarePerKm * trip.Km + Config.FarePerMinute * freeFlow;
            fare *= urgency == Urgency.Medical ? Config.MedicalFareMultiplier : urgency == Urgency.Priority ? Config.PriorityFareMultiplier : 1.0;
            double patience = urgency == Urgency.Medical ? _rng.Range(7.0, 10.0) : urgency == Urgency.Priority ? _rng.Range(9.0, 13.0) : _rng.Range(12.0, 20.0);
            patience *= Config.PatienceScale * (Raining ? 0.9 : 1.0);

            var r = new RideRequest
            {
                Id = _nextRequestId++, Origin = origin, Destination = dest, Urgency = urgency, Fare = Math.Round(fare),
                Patience = patience, CreatedTick = Tick, FreeFlowMinutes = freeFlow, TripKm = trip.Km,
                Surge = SurgePricing.Multiplier(SurgeLevel)
            };
            Requests.Add(r);

            // Queue full?
            int waiting = 0;
            foreach (RideRequest q in Requests) if (q.Status == RequestStatus.Waiting) waiting++;
            if (waiting > Config.MaxQueue)
            {
                r.Status = RequestStatus.Dropped;
                Dropped++;
                ChangeSatisfaction(-0.25);
                return;
            }

            // Passenger chooses a provider (player vs RapidCab) and checks the price
            double playerEta = NearestEta(Taxis, origin, out _);
            Taxi rival = null;
            double rivalEta = Config.RivalEnabled ? NearestEta(Rivals, origin, out rival) : double.PositiveInfinity;
            bool medical = urgency == Urgency.Medical;
            if (rival != null && !medical)
            {
                double share = SurgePricing.PlayerShare(Config, r.Fare, r.Surge, Math.Min(playerEta, 30.0), SurgePricing.Multiplier(RivalSurgeLevel), rivalEta);
                if (!_rng.Chance(share))
                {
                    r.Status = RequestStatus.TakenByRival;
                    r.Surge = SurgePricing.Multiplier(RivalSurgeLevel);
                    r.TaxiId = rival.Id;
                    LostToRival++;
                    RivalTake(rival, r);
                    return;
                }
            }
            double accept = medical ? 1.0 : SurgePricing.Acceptance(Config, r.Surge, Math.Min(1.0, playerEta / Math.Max(1.0, patience)));
            if (!_rng.Chance(accept))
            {
                r.Status = RequestStatus.Declined;
                Declined++;
                return;
            }
            r.Status = RequestStatus.Waiting;
            string tag = urgency == Urgency.Medical ? " MEDICAL" : urgency == Urgency.Priority ? " priority" : "";
            Say(MessageKind.Request, $"New{tag} request #{r.Id}: {Graph.Node(origin).ShortName} → {Graph.Node(dest).ShortName}, ₹{r.Fare * r.Surge:0}, waits {patience:0} min.");
        }

        /// <summary>
        /// Adds a scripted request (tutorials, demo scenarios, tests). It goes straight into the player's queue:
        /// no price check and no rival competition.
        /// </summary>
        public RideRequest AddRequest(int origin, int destination, Urgency urgency = Urgency.Normal, double patience = 15.0)
        {
            RouteResult trip = RouteFinder.FindFastest(Graph, origin, destination);
            if (!trip.Success) return null;
            double freeFlow = 0;
            foreach (int id in trip.Edges) freeFlow += Graph.Edges[id].BaseMinutes;
            double fare = Config.FareBase + Config.FarePerKm * trip.Km + Config.FarePerMinute * freeFlow;
            fare *= urgency == Urgency.Medical ? Config.MedicalFareMultiplier : urgency == Urgency.Priority ? Config.PriorityFareMultiplier : 1.0;
            var r = new RideRequest
            {
                Id = _nextRequestId++, Origin = origin, Destination = destination, Urgency = urgency, Fare = Math.Round(fare),
                Patience = patience, CreatedTick = Tick, FreeFlowMinutes = freeFlow, TripKm = trip.Km,
                Surge = SurgePricing.Multiplier(SurgeLevel), Status = RequestStatus.Waiting
            };
            Requests.Add(r);
            Say(MessageKind.Request, $"Scripted request #{r.Id}: {Graph.Node(origin).ShortName} → {Graph.Node(destination).ShortName}.");
            return r;
        }

        /// <summary>ETA of the nearest available taxi in a fleet (∞ if none).</summary>
        private double NearestEta(List<Taxi> fleet, int node, out Taxi best)
        {
            best = null;
            double bestEta = double.PositiveInfinity;
            foreach (Taxi t in fleet)
            {
                if (t.State != TaxiState.Idle && t.State != TaxiState.Relocating) continue;
                if (t.Owner == Owner.Player && t.IsLowBattery(Config)) continue;
                RouteResult r = RouteFinder.FindFastest(Graph, StartNode(t), node);
                if (!r.Success) continue;
                double eta = r.Minutes + (OnEdge(t) ? (1 - t.Progress) * Graph.Edges[t.CurrentEdge].CurrentMinutes : 0);
                if (eta < bestEta) { bestEta = eta; best = t; }
            }
            return bestEta;
        }

        private void RivalTake(Taxi rival, RideRequest r)
        {
            rival.RequestId = r.Id;
            rival.TrySetState(TaxiState.ToPickup);
            SetRoute(rival, r.Origin);
            if (!rival.IsMoving) Arrive(rival);
        }

        private void UpdateRequests(double dt)
        {
            foreach (RideRequest r in Requests)
            {
                if (!r.IsOpen) continue;
                r.Waited += dt;
                if (r.Waited <= r.Patience) continue;
                // The passenger gives up
                if (r.TaxiId >= 0) StandDown(PlayerTaxi(r.TaxiId));
                r.Status = RequestStatus.Abandoned;
                Abandoned++;
                Pay(-Config.AbandonPenalty, penalty: true);
                ChangeSatisfaction(r.Urgency == Urgency.Medical ? -4 : -2);
                Say(MessageKind.Warning, $"Request #{r.Id} abandoned after {r.Waited:0} min (−₹{Config.AbandonPenalty:0}).");
            }
        }

        /// <summary>A taxi loses its job: it finishes the road it's on, then waits there.</summary>
        private void StandDown(Taxi t)
        {
            if (t == null) return;
            t.RequestId = -1;
            if (t.State == TaxiState.Delayed) { t.NeedsReroute = false; t.DelayLeft = 0; t.ResumeState = OnEdge(t) ? TaxiState.Relocating : TaxiState.Idle; }
            if (OnEdge(t))
            {
                int edge = t.CurrentEdge, from = t.Node, to = t.NextNode;
                t.ClearRoute();
                t.RouteEdges.Add(edge);
                t.RouteNodes.Add(from);
                t.RouteNodes.Add(to);
                t.TargetNode = to;
                if (t.State != TaxiState.Delayed) t.TrySetState(TaxiState.Relocating);
            }
            else
            {
                t.ClearRoute();
                if (t.State != TaxiState.Delayed && !t.TrySetState(TaxiState.Idle)) t.TrySetState(TaxiState.Relocating);
            }
        }

        // ------------------------------------------------------------------ taxis

        /// <summary>True while part-way along a road (a taxi waiting at the start of a road can still turn).</summary>
        private static bool OnEdge(Taxi t) => t.IsMoving && t.Progress > 0;

        private int StartNode(Taxi t) => OnEdge(t) ? t.NextNode : t.Node;

        /// <summary>Sets the taxi's route to <paramref name="target"/>, keeping the road it is currently on.</summary>
        private void SetRoute(Taxi t, int target)
        {
            int start = StartNode(t);
            RouteResult r = RouteFinder.FindFastest(Graph, start, target);
            bool moving = OnEdge(t);
            int curEdge = t.CurrentEdge, from = t.Node, next = t.NextNode;
            double progress = t.Progress;
            t.ClearRoute();
            t.TargetNode = target;
            if (moving)
            {
                t.RouteEdges.Add(curEdge);
                t.RouteNodes.Add(from);
                t.RouteNodes.Add(next);
                t.Progress = progress;
            }
            else t.RouteNodes.Add(start);
            if (!r.Success) return;
            foreach (int e in r.Edges) t.RouteEdges.Add(e);
            for (int i = 1; i < r.Nodes.Count; i++) t.RouteNodes.Add(r.Nodes[i]);
        }

        private int NearestCharger(Taxi t)
        {
            int best = -1;
            double bestMin = double.PositiveInfinity;
            foreach (int node in ChargerNodes())
            {
                RouteResult r = RouteFinder.FindFastest(Graph, StartNode(t), node);
                if (r.Success && r.Minutes < bestMin) { bestMin = r.Minutes; best = node; }
            }
            return best;
        }

        private void UpdateTaxi(Taxi t, double dt)
        {
            if (t.State == TaxiState.ToPickup || t.State == TaxiState.OnRide)
            {
                t.BusyMinutes += dt;
                if (t.Owner == Owner.Player) _busyMinutes += dt;
            }
            switch (t.State)
            {
                case TaxiState.Delayed:
                    t.DelayLeft -= dt;
                    if (t.DelayLeft > 0) return;
                    if (t.NeedsReroute)
                    {
                        CommandResult r = Reroute(t.Id);
                        Say(MessageKind.Info, $"{t.Label} auto-rerouted after waiting. {r.Message}");
                        if (!r.Ok) t.DelayLeft = 5.0;
                    }
                    else t.TrySetState(t.ResumeState);
                    return;
                case TaxiState.Offline:
                    t.DelayLeft -= dt;
                    if (t.DelayLeft <= 0) FinishTow(t);
                    return;
                case TaxiState.Charging:
                    Charge(t, dt);
                    return;
                case TaxiState.Idle:
                    return;
            }
            Advance(t, dt);
        }

        private void Advance(Taxi t, double dt)
        {
            while (dt > 1e-9 && t.IsMoving)
            {
                RoadEdge e = Graph.Edges[t.CurrentEdge];
                if (t.Progress <= 0 && e.Blocked && t.Owner == Owner.Player)
                {
                    StopForReroute(t);
                    return;
                }
                double edgeMin = Math.Max(0.05, e.CurrentMinutes);
                double step = Math.Min(dt, (1.0 - t.Progress) * edgeMin);
                double frac = step / edgeMin;
                t.OdometerKm += e.LengthKm * frac;
                if (t.Owner == Owner.Player) t.Battery -= EdgeEnergy(e, frac);
                t.Progress += frac;
                dt -= step;
                if (t.Progress >= 1.0 - 1e-9)
                {
                    t.Node = t.RouteNodes[t.RouteIndex + 1];
                    t.RouteIndex++;
                    t.Progress = 0;
                }
                if (t.Owner == Owner.Player && t.Battery <= 0)
                {
                    t.Battery = 0;
                    GoOffline(t);
                    return;
                }
            }
            if (!t.IsMoving) Arrive(t);
        }

        private void Arrive(Taxi t)
        {
            t.ClearRoute();
            switch (t.State)
            {
                case TaxiState.ToPickup:
                {
                    RideRequest r = Request(t.RequestId);
                    bool rivalJob = t.Owner == Owner.Rival && r != null && r.Status == RequestStatus.TakenByRival;
                    if (r == null || (!r.IsOpen && !rivalJob)) { t.TrySetState(TaxiState.Idle); t.RequestId = -1; return; }
                    if (!rivalJob) r.Status = RequestStatus.PickedUp;
                    r.PickupTick = Tick;
                    RouteResult quote = RouteFinder.FindFastest(Graph, t.Node, r.Destination);
                    r.QuotedMinutes = quote.Success ? quote.Minutes : r.FreeFlowMinutes;
                    t.TrySetState(TaxiState.OnRide);
                    SetRoute(t, r.Destination);
                    if (!t.IsMoving && t.Node == r.Destination) CompleteTrip(t, r);
                    else if (!t.IsMoving && t.Owner == Owner.Player) StopForReroute(t); // every road to the destination is closed
                    break;
                }
                case TaxiState.OnRide:
                    CompleteTrip(t, Request(t.RequestId));
                    break;
                case TaxiState.ToCharger:
                    t.TrySetState(TaxiState.Charging);
                    t.ChargerNode = t.Node;
                    if (_charging[t.Node].Count < ChargerCapacity(t.Node)) _charging[t.Node].Add(t.Id);
                    else { _queue[t.Node].Add(t.Id); t.InChargerQueue = true; Say(MessageKind.Info, $"{t.Label} queues for a charger at {Graph.Node(t.Node).ShortName}."); }
                    break;
                case TaxiState.Relocating:
                    t.TrySetState(TaxiState.Idle);
                    break;
            }
        }

        private void CompleteTrip(Taxi t, RideRequest r)
        {
            t.TrySetState(TaxiState.Idle);
            t.RequestId = -1;
            if (r == null) return;
            r.DropTick = Tick;
            if (t.Owner == Owner.Rival)
            {
                RivalRevenue += r.Fare * r.Surge;
                RivalTrips++;
                return;
            }
            r.Status = RequestStatus.Completed;
            double revenue = r.Fare * r.Surge;
            t.Earnings += revenue;
            t.TripsCompleted++;
            Completed++;
            Revenue += revenue;
            Cash += revenue;
            double rideMinutes = (r.DropTick - r.PickupTick) * Config.MinutesPerTick;
            double late = rideMinutes - (r.QuotedMinutes * Config.LateGrace + 1.5);
            if (late > 0)
            {
                double penalty = Math.Min(Config.MaxLatePenalty, late * Config.LatePenaltyPerMinute);
                Pay(-penalty, penalty: true);
                ChangeSatisfaction(r.Urgency == Urgency.Medical ? -4 : -2);
                Say(MessageKind.Money, $"{t.Label} dropped #{r.Id} {late:0} min late: +₹{revenue:0} −₹{penalty:0}.");
            }
            else
            {
                ChangeSatisfaction(r.Urgency == Urgency.Medical ? 3 : r.Urgency == Urgency.Priority ? 2 : 1);
                Say(MessageKind.Money, $"{t.Label} completed #{r.Id}: +₹{revenue:0}.");
            }
            if (r.Surge >= 1.75) ChangeSatisfaction(-1);
        }

        private void Charge(Taxi t, double dt)
        {
            if (t.InChargerQueue)
            {
                if (_queue[t.Node].Count > 0 && _queue[t.Node][0] == t.Id && _charging[t.Node].Count < ChargerCapacity(t.Node))
                {
                    _queue[t.Node].RemoveAt(0);
                    _charging[t.Node].Add(t.Id);
                    t.InChargerQueue = false;
                }
                else return;
            }
            double add = Math.Min(100.0 - t.Battery, Config.ChargePerMinute * dt);
            t.Battery += add;
            Pay(-add * Config.ChargeCostPerPercent);
            if (t.Battery >= 99.999)
            {
                t.Battery = 100;
                LeaveCharger(t);
                t.TrySetState(TaxiState.Idle);
                Say(MessageKind.Info, $"{t.Label} fully charged at {Graph.Node(t.Node).ShortName}.");
            }
        }

        private void LeaveCharger(Taxi t)
        {
            if (t.ChargerNode < 0) return;
            _charging[t.ChargerNode].Remove(t.Id);
            _queue[t.ChargerNode].Remove(t.Id);
            t.InChargerQueue = false;
            t.ChargerNode = -1;
        }

        private void GoOffline(Taxi t)
        {
            RideRequest r = Request(t.RequestId);
            if (r != null && r.IsOpen) { r.Status = RequestStatus.Waiting; r.TaxiId = -1; }
            else if (r != null && r.Status == RequestStatus.PickedUp)
            {
                r.Status = RequestStatus.Abandoned;
                Abandoned++;
                ChangeSatisfaction(-5);
            }
            t.RequestId = -1;
            t.ForceState(TaxiState.Offline);
            t.ClearRoute();
            t.DelayLeft = Config.TowMinutes;
            t.LastIssue = "battery empty";
            Pay(-Config.TowFee, penalty: true);
            ChangeSatisfaction(-3);
            Say(MessageKind.Warning, $"{t.Label} ran out of battery! Tow truck called (−₹{Config.TowFee:0}).");
        }

        private void FinishTow(Taxi t)
        {
            // Towed to the nearest charging station (straight-line distance)
            int best = -1;
            double bestD = double.PositiveInfinity;
            foreach (int node in ChargerNodes())
            {
                double d = Vec3.DistanceXZ(Graph.Node(node).Position, Graph.Node(t.Node).Position);
                if (d < bestD) { bestD = d; best = node; }
            }
            if (best < 0) best = t.Node;
            t.Node = best;
            t.TrySetState(TaxiState.Charging);
            t.ChargerNode = best;
            if (_charging[best].Count < ChargerCapacity(best)) _charging[best].Add(t.Id);
            else { _queue[best].Add(t.Id); t.InChargerQueue = true; }
            Say(MessageKind.Info, $"{t.Label} towed to {Graph.Node(best).ShortName} for charging.");
        }

        // ------------------------------------------------------------------ money, satisfaction, messages

        private void Pay(double amount, bool penalty = false)
        {
            Cash += amount;
            if (amount < 0)
            {
                if (penalty) Penalties -= amount;
                else Costs -= amount;
            }
        }

        private void ChangeSatisfaction(double d) => Satisfaction = Math.Max(0, Math.Min(100, Satisfaction + d));

        /// <summary>Total messages ever logged (the Log list is capped, this keeps counting) — lets the UI find new ones.</summary>
        public long MessageCount { get; private set; }

        private void Say(MessageKind kind, string text)
        {
            MessageCount++;
            Log.Add(new SimMessage(Tick, kind, text));
            if (Log.Count > MaxLog) Log.RemoveAt(0);
        }

        // ================================================================== results

        public IEnumerable<RideRequest> OpenRequests
        {
            get { foreach (RideRequest r in Requests) if (r.IsOpen) yield return r; }
        }

        public double Efficiency => MinutesElapsed > 0 ? _busyMinutes / (Taxis.Count * MinutesElapsed) : 0;

        public ScoreCard Score()
        {
            var s = new ScoreCard
            {
                Profit = Cash - Config.StartCash,
                Satisfaction = Satisfaction,
                Efficiency = Efficiency,
                Completed = Completed, Abandoned = Abandoned, Rejected = Rejected, LostToRival = LostToRival, Declined = Declined,
                Outcome = Outcome
            };
            s.ProfitScore = Math.Max(0, Math.Min(1, s.Profit / Config.ProfitTarget));
            s.Score = 0.5 * s.ProfitScore + 0.3 * Satisfaction / 100.0 + 0.2 * Math.Min(1.0, s.Efficiency);
            if (Outcome == Outcome.Bankrupt || Outcome == Outcome.Fired) s.Score *= 0.5;
            s.Stars = (int)Math.Max(1, Math.Min(5, Math.Round(1 + 4 * s.Score)));
            return s;
        }

        /// <summary>FNV-1a hash of the whole simulation state (determinism, save validation, regression tests).</summary>
        public ulong StateHash()
        {
            ulong h = 14695981039346656037UL;
            void Mix(long v)
            {
                unchecked
                {
                    for (int i = 0; i < 8; i++) { h ^= (byte)(v >> (i * 8)); h *= 1099511628211UL; }
                }
            }
            Mix(Tick);
            Mix((long)_rng.State);
            Mix((long)Math.Round(Cash * 100));
            Mix((long)Math.Round(Satisfaction * 1000));
            Mix(SurgeLevel);
            Mix(RivalSurgeLevel);
            Mix(Completed); Mix(Abandoned); Mix(Rejected); Mix(LostToRival); Mix(Declined); Mix(Dropped);
            foreach (Taxi t in Taxis) MixTaxi(t);
            foreach (Taxi t in Rivals) MixTaxi(t);
            Mix(Requests.Count);
            foreach (RideRequest r in Requests) { Mix(r.Id); Mix((int)r.Status); Mix(r.TaxiId); Mix((long)Math.Round(r.Waited * 100)); }
            Mix(Events.Count);
            return h;

            void MixTaxi(Taxi t)
            {
                Mix((int)t.State); Mix(t.Node); Mix(t.RouteIndex); Mix(t.RouteEdges.Count); Mix(t.RequestId);
                Mix((long)Math.Round(t.Progress * 1e6)); Mix((long)Math.Round(t.Battery * 1e4));
            }
        }
    }
}
