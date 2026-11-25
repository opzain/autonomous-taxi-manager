namespace TaxiSim.Core.Sim
{
    public enum Difficulty
    {
        Easy,
        Normal,
        Hard
    }

    /// <summary>
    /// Every tunable number of the dispatch simulation in one place (see docs/06-game-design-balancing.md).
    /// Money is in rupees, time in sim-minutes, energy in % of battery, distance in km.
    /// </summary>
    public sealed class SimConfig
    {
        // ---- Clock ----
        public double MinutesPerTick = 0.25;          // 1 tick = 15 sim-seconds
        public double ShiftStartHour = 6.0;
        public double ShiftEndHour = 22.0;

        // ---- Fleet ----
        public int PlayerTaxis = 6;
        public int RivalTaxis = 4;
        public string DepotKey = "fleet_hub";
        public double StartBattery = 90.0;

        // ---- Energy and charging ----
        public double EnergyPerKm = 2.5;              // % per km in free flow
        public double CongestionEnergyFactor = 0.5;   // × (traffic multiplier − 1)
        public double ElevatedEnergyFactor = 1.05;    // flyovers / bridges climb
        public double DispatchReserve = 10.0;         // % that must remain after a trip
        public double LowBattery = 20.0;
        public double ChargePerMinute = 2.0;          // % per sim-minute
        public double ChargeCostPerPercent = 2.0;     // ₹
        public double TowFee = 500.0;
        public double TowMinutes = 20.0;

        // ---- Requests ----
        public int MaxQueue = 8;
        public double BaseRequestsPerHour = 30.0;
        public double PriorityShare = 0.22;
        public double MedicalShare = 0.08;
        public double FareBase = 60.0, FarePerKm = 22.0, FarePerMinute = 2.0;
        public double PriorityFareMultiplier = 1.25, MedicalFareMultiplier = 1.5;
        public double PatienceScale = 1.0;
        public double LateGrace = 1.2;                // trip may take 20% longer than quoted at pickup (+1.5 min) before it is "late"
        public double LatePenaltyPerMinute = 10.0, MaxLatePenalty = 300.0;
        public double AbandonPenalty = 100.0;

        // ---- Economy ----
        public double StartCash = 5000.0;
        public double DailyFeePerTaxi = 300.0;
        public double BankruptcyCash = -2000.0;
        public double ProfitTarget = 25000.0;         // normalises the score

        // ---- Satisfaction (0..100) ----
        public double StartSatisfaction = 70.0;
        public double FireBelowSatisfaction = 20.0;

        // ---- Events ----
        public double EventsPerHour = 1.3;
        public int MaxActiveEvents = 3;
        public double RushAmplitude = 0.9;            // extra traffic multiplier at the peak of rush hour

        // ---- Pricing / game theory ----
        public double PriceElasticity = 0.6;          // acceptance = 1 − e·(s − 1) − w·wait
        public double WaitSensitivity = 0.3;
        public double ValueOfTimePerMinute = 6.0;     // ₹ a passenger "pays" per minute of waiting
        public double ChoiceSharpness = 0.035;        // logit sharpness of provider choice (per ₹)
        public bool RivalEnabled = true;

        // ---- Dispatch utility weights U = fare·s − cE·energy₹ − cT·ETA·urgency − cR·P(abandon) + β·ΔSat ----
        public double UtilityEnergyWeight = 1.0;
        public double UtilityTimeWeight = 8.0;
        public double UtilityAbandonWeight = 150.0;
        public double UtilitySatisfactionWeight = 15.0;

        public static SimConfig For(Difficulty d)
        {
            var c = new SimConfig();
            switch (d)
            {
                case Difficulty.Easy:
                    c.BaseRequestsPerHour = 24; c.PatienceScale = 1.3; c.EventsPerHour = 0.8; c.StartCash = 8000; c.RivalTaxis = 3;
                    break;
                case Difficulty.Hard:
                    c.BaseRequestsPerHour = 38; c.PatienceScale = 0.8; c.EventsPerHour = 2.0; c.StartCash = 3000; c.RivalTaxis = 5;
                    break;
            }
            return c;
        }

        public int TicksPerShift => (int)System.Math.Round((ShiftEndHour - ShiftStartHour) * 60.0 / MinutesPerTick);
    }
}
