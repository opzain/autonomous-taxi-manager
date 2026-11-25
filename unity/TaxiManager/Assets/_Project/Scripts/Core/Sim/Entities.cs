using System.Collections.Generic;

namespace TaxiSim.Core.Sim
{
    /// <summary>Taxi states (spec §11) plus RELOCATING.</summary>
    public enum TaxiState
    {
        Idle,
        ToPickup,
        OnRide,
        ToCharger,
        Charging,
        Relocating,
        Delayed,
        Offline
    }

    public enum Owner
    {
        Player,
        Rival
    }

    /// <summary>The only legal state changes. Anything else is a bug and is rejected (spec test T06).</summary>
    public static class TaxiStateMachine
    {
        private static readonly Dictionary<TaxiState, TaxiState[]> Allowed = new Dictionary<TaxiState, TaxiState[]>
        {
            { TaxiState.Idle,       new[] { TaxiState.ToPickup, TaxiState.ToCharger, TaxiState.Relocating, TaxiState.Offline, TaxiState.Delayed } },
            { TaxiState.ToPickup,   new[] { TaxiState.OnRide, TaxiState.Delayed, TaxiState.Offline, TaxiState.Relocating, TaxiState.Idle } },
            { TaxiState.OnRide,     new[] { TaxiState.Idle, TaxiState.Delayed, TaxiState.Offline } },
            { TaxiState.ToCharger,  new[] { TaxiState.Charging, TaxiState.Delayed, TaxiState.Offline } },
            { TaxiState.Charging,   new[] { TaxiState.Idle } },
            { TaxiState.Relocating, new[] { TaxiState.Idle, TaxiState.ToPickup, TaxiState.ToCharger, TaxiState.Delayed, TaxiState.Offline } },
            { TaxiState.Delayed,    new[] { TaxiState.ToPickup, TaxiState.OnRide, TaxiState.ToCharger, TaxiState.Relocating, TaxiState.Idle, TaxiState.Offline } },
            { TaxiState.Offline,    new[] { TaxiState.Charging } }
        };

        public static bool CanTransition(TaxiState from, TaxiState to)
        {
            if (from == to) return true;
            return Allowed.TryGetValue(from, out TaxiState[] next) && System.Array.IndexOf(next, to) >= 0;
        }
    }

    public sealed class Taxi
    {
        public int Id;
        public string Label;
        public Owner Owner;
        public TaxiState State { get; private set; } = TaxiState.Idle;
        public double Battery;                 // 0..100 %
        public int Node;                       // node the taxi is at, or the node it last left
        public readonly List<int> RouteEdges = new List<int>();
        public readonly List<int> RouteNodes = new List<int>();
        public int RouteIndex;                 // edge currently being driven
        public double Progress;                // 0..1 along that edge
        public int RequestId = -1;
        public int TargetNode = -1;            // where the current route ends
        public int ChargerNode = -1;
        public bool InChargerQueue;
        public double DelayLeft;               // minutes (vehicle issue / tow)
        public bool NeedsReroute;              // stopped by a closure on its route
        public TaxiState ResumeState;          // state to return to after a delay
        public double OdometerKm, BusyMinutes, Earnings;
        public int TripsCompleted;
        public string LastIssue = "";

        public bool IsMoving => RouteIndex < RouteEdges.Count;
        public int CurrentEdge => IsMoving ? RouteEdges[RouteIndex] : -1;
        public int NextNode => IsMoving ? RouteNodes[RouteIndex + 1] : Node;
        public bool IsLowBattery(SimConfig c) => Battery < c.LowBattery;

        /// <summary>Changes state only along a legal transition; returns false otherwise.</summary>
        public bool TrySetState(TaxiState to)
        {
            if (!TaxiStateMachine.CanTransition(State, to)) return false;
            State = to;
            return true;
        }

        internal void ForceState(TaxiState to) => State = to; // loading / towing only

        public void ClearRoute()
        {
            RouteEdges.Clear();
            RouteNodes.Clear();
            RouteIndex = 0;
            Progress = 0;
        }
    }

    public enum Urgency
    {
        Normal,
        Priority,
        Medical
    }

    public enum RequestStatus
    {
        Waiting,
        Assigned,
        PickedUp,
        Completed,
        Abandoned,
        Rejected,
        TakenByRival,
        Declined,      // passenger refused the surge price
        Dropped        // queue was full
    }

    public sealed class RideRequest
    {
        public int Id;
        public int Origin, Destination;
        public Urgency Urgency;
        public double Fare;              // before surge
        public double Surge = 1.0;       // locked at creation
        public double Patience;          // minutes the passenger will wait for pickup
        public double Waited;
        public RequestStatus Status;
        public int TaxiId = -1;
        public int CreatedTick, PickupTick = -1, DropTick = -1;
        public double FreeFlowMinutes;   // trip time without traffic
        public double QuotedMinutes;     // trip time quoted at pickup (current traffic) — lateness reference
        public double TripKm;

        public bool IsOpen => Status == RequestStatus.Waiting || Status == RequestStatus.Assigned;
        public double PatienceLeft => Patience - Waited;
        public double UrgencyWeight => Urgency == Urgency.Medical ? 3.0 : Urgency == Urgency.Priority ? 1.8 : 1.0;
    }
}
