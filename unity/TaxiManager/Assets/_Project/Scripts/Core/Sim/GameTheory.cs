using System;
using System.Collections.Generic;

namespace TaxiSim.Core.Sim
{
    /// <summary>
    /// Surge pricing and price competition with the rival fleet "RapidCab".
    ///  * Acceptance:  P(accept | s, wait) = clamp(1 − e·(s − 1) − w·wait/patience, 0.05, 1)
    ///  * Provider choice (logit on generalised cost  C = fare·s + VOT·ETA):
    ///        P(player) = 1 / (1 + exp(k·(C_player − C_rival)))
    ///  * Expected profit of a provider for a price pair, and best responses / Nash equilibrium of the
    ///    resulting 5×5 pricing game (Bertrand competition with differentiated service).
    /// </summary>
    public static class SurgePricing
    {
        public static readonly double[] Levels = { 1.0, 1.25, 1.5, 1.75, 2.0 };

        public static double Multiplier(int level) => Levels[Math.Max(0, Math.Min(Levels.Length - 1, level))];

        public static double Acceptance(SimConfig c, double surge, double waitFraction = 0.0)
        {
            double p = 1.0 - c.PriceElasticity * (surge - 1.0) - c.WaitSensitivity * waitFraction;
            return Math.Max(0.05, Math.Min(1.0, p));
        }

        public static double GeneralisedCost(SimConfig c, double fare, double surge, double etaMinutes) =>
            fare * surge + c.ValueOfTimePerMinute * etaMinutes;

        /// <summary>Probability the passenger chooses the player's fleet over RapidCab.</summary>
        public static double PlayerShare(SimConfig c, double fare, double playerSurge, double playerEta, double rivalSurge, double rivalEta)
        {
            double diff = GeneralisedCost(c, fare, playerSurge, playerEta) - GeneralisedCost(c, fare, rivalSurge, rivalEta);
            return 1.0 / (1.0 + Math.Exp(c.ChoiceSharpness * diff));
        }

        /// <summary>Expected revenue per request for "me" at level <paramref name="mine"/> against level <paramref name="theirs"/> (equal ETAs).</summary>
        public static double ExpectedRevenue(SimConfig c, double fare, int mine, int theirs)
        {
            double sm = Multiplier(mine), st = Multiplier(theirs);
            double share = PlayerShare(c, fare, sm, 6.0, st, 6.0);
            return fare * sm * share * Acceptance(c, sm);
        }

        /// <summary>The level that maximises my expected revenue given the other fleet's level.</summary>
        public static int BestResponse(SimConfig c, double fare, int theirs)
        {
            int best = 0;
            double bestValue = double.MinValue;
            for (int l = 0; l < Levels.Length; l++)
            {
                double v = ExpectedRevenue(c, fare, l, theirs);
                if (v > bestValue + 1e-9) { bestValue = v; best = l; }
            }
            return best;
        }

        /// <summary>Payoff matrix [mine, theirs] (for the report / viva and the HUD hint).</summary>
        public static double[,] PayoffMatrix(SimConfig c, double fare)
        {
            var m = new double[Levels.Length, Levels.Length];
            for (int i = 0; i < Levels.Length; i++)
                for (int j = 0; j < Levels.Length; j++)
                    m[i, j] = ExpectedRevenue(c, fare, i, j);
            return m;
        }

        /// <summary>Pure-strategy Nash equilibria of the symmetric pricing game.</summary>
        public static List<(int player, int rival)> NashEquilibria(SimConfig c, double fare)
        {
            var result = new List<(int, int)>();
            for (int i = 0; i < Levels.Length; i++)
                for (int j = 0; j < Levels.Length; j++)
                    if (BestResponse(c, fare, j) == i && BestResponse(c, fare, i) == j) result.Add((i, j));
            return result;
        }
    }

    /// <summary>
    /// The per-taxi utility used to rank which taxi should serve a request (shown as the console's
    /// auto-suggestion; the encoder cycles the top 3, the player can override):
    ///     U = fare·s − cE·energy₹ − cT·ETA·urgency − cR·P(abandon) + β·ΔSatisfaction
    /// </summary>
    public sealed class DispatchCandidate
    {
        public int TaxiId;
        public bool Feasible;
        public string Reason = "";
        public double EtaMinutes, PickupKm, TripKm, EnergyNeeded, Utility;
        public double Revenue, EnergyCost, TimeCost, AbandonRisk, SatisfactionGain;

        public override string ToString() =>
            Feasible ? $"taxi {TaxiId}: U={Utility:0} (fare {Revenue:0} − energy {EnergyCost:0} − time {TimeCost:0} − risk {AbandonRisk:0.00} + sat {SatisfactionGain:0.0})"
                     : $"taxi {TaxiId}: not possible — {Reason}";
    }
}
