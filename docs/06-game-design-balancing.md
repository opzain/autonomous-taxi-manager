# 06 — Game design and balancing

All numbers live in `Scripts/Core/Sim/SimConfig.cs`. The tables below show the **Normal** preset.

## 1. Player goal

Run the AT autonomous fleet (6 EV taxis) from 06:00 to 22:00. You keep the business alive by managing:
- **cash**, which must stay above −₹2,000;
- **customer satisfaction**, which must stay at or above 20.

The final score is

`score = 0.5 · clamp(profit / ₹25,000) + 0.3 · satisfaction/100 + 0.2 · efficiency`, then **1 + 4·score**, rounded, gives **1–5 stars**. Efficiency is busy taxi-time divided by available taxi-time. Losing halves the score.

## 2. Formulas

| Quantity | Formula / value |
|---|---|
| Travel time of a road | `t = km / speed(class) × 60 + 0.3 min`, × traffic multiplier m ∈ [1, 3] |
| Rush hour | `m = (1 + 0.9 · rush(h) · classFactor) × noise(0.92–1.08)`. The rush peaks at 09:15 and 18:45, with a small lunch bump. classFactor is 0.5 for local roads, 0.8 for main roads and 1 for arterials. |
| Events | heavy traffic ×2.2 (×1.35 on neighbouring roads), rain ×1.35 everywhere, festival ×1.8 around the venue plus one road closed |
| Energy | `2.5 %/km × (1 + 0.5 (m − 1)) × 1.05 on flyovers` |
| Dispatch check | refused unless `battery − (energy to pickup + trip) ≥ 10 %` |
| Charging | +2 %/sim-min, ₹2 per %, slots per station (EV Hub A 2, EV Depot B 3, AT Fleet Hub 12) with a FIFO queue |
| Empty battery | OFFLINE, ₹500 tow and −3 satisfaction; after 20 min the taxi is put on the nearest charger |
| Demand | `30 req/h × (0.6 + 0.9·peak9 + 0.8·peak18.5 + 0.3·lunch)` × 1.3 when raining. Origins and destinations are weighted by zone and time: stations in the morning, offices in the evening, and so on. |
| Urgency | 70 % normal, 22 % priority (fare ×1.25), 8 % medical (fare ×1.5, always to Asian Heart) |
| Patience | normal 12–20 min, priority 9–13, medical 7–10 (×0.9 in rain) |
| Fare | `₹60 + ₹22/km + ₹2/free-flow-min` × urgency × the surge locked at booking |
| Late | the ride takes longer than `1.2 × quoted + 1.5 min`. Penalty ₹10/min (max ₹300) and −2 satisfaction (−4 medical). |
| Satisfaction | on-time drop +1 (priority +2, medical +3); abandoned −2 (medical −4) and −₹100; rejected −1; dropped from a full queue −0.25; surge ≥ ×1.75 costs −1 per ride |
| Daily cost | ₹300 per taxi at shift start |

## 3. Dispatch suggestion (utility)

`U = fare·s − 1.0·energy₹ − 8·ETA·urgencyWeight − 150·P(abandon) + 15·ΔSat`

- urgencyWeight is 1, 1.8 or 3;
- `P(abandon) = σ((ETA − patience left)/1.5)`;
- ΔSat is +2×urgency when on time, −5 when late.

The console shows the top-ranked taxi, and the encoder cycles through the next options. Infeasible taxis are listed with the reason.

## 4. Pricing game against RapidCab

- **Passenger acceptance:** `P = clamp(1 − 0.6 (s − 1) − 0.3 · wait/patience, 0.05, 1)`.
- **Provider choice:** each passenger picks a provider with a logit on the generalised cost `C = fare·s + ₹6/min · ETA`:

  `P(player) = 1 / (1 + e^{0.035 (C_player − C_rival)})`

- **RapidCab:** every sim-hour it plays its **best response** to your surge.

Expected revenue per request for a ₹150 fare, assuming equal ETAs (computed by `SurgePricing.PayoffMatrix`):

| You \ RapidCab | ×1.0 | ×1.25 | ×1.5 | ×1.75 | ×2.0 |
|---|---|---|---|---|---|
| **×1.0** | **75.0** | 118.2 | 139.9 | 147.1 | 149.2 |
| ×1.25 | 33.8 | 79.7 | 125.6 | 148.6 | 156.3 |
| ×1.5 | 10.6 | 33.4 | 78.8 | 124.1 | 146.9 |
| ×1.75 | 2.8 | 9.8 | 30.6 | 72.2 | 113.8 |
| ×2.0 | 0.6 | 2.3 | 8.1 | 25.4 | 60.0 |

- **Best responses:** against ×1.0, ×1.25 or ×1.5 the best answer is ×1.0; against ×1.75 or ×2.0 it is ×1.25.
- **Unique pure Nash equilibrium: (×1.0, ×1.0)**, shown in bold. This is Bertrand competition.
- It is a **Prisoner's Dilemma**. If both fleets "collude" at ×1.25, each earns 79.7, which is more than 75.0. But each fleet gains by undercutting (118.2), so the collusion is unstable.

  `NashEquilibria` and the test `Rival_UndercutsAnExpensivePlayer_AndPlayIsANashEquilibrium` check this. Iterated best responses from (×2, ×2) converge to it.

**Why surge is still worth using in play:** the table assumes the fleet has spare capacity. In the morning and evening rush you can't serve everyone anyway. Raising the surge thins demand and pushes the overflow to RapidCab, instead of letting passengers abandon. Each abandonment costs ₹100 and satisfaction. The autopilot does exactly this (`AutoDispatcher`): it raises the surge when `queue > free taxis + 2` and lowers it when the queue is empty.

## 5. Balance results (autopilot, 20 seeds per preset, `AutoDispatcher.PlayShift`)

| Preset | Shifts completed | Stars | Profit | Trips | Abandoned | Rival share | Satisfaction |
|---|---|---|---|---|---|---|---|
| Easy (24 req/h, patience ×1.3, 3 rivals, ₹8,000) | 20/20 | 4.0 | ₹16,410 | 160 | 4.3 | 42 % | 100 |
| Normal (30 req/h, 4 rivals, ₹5,000) | 20/20 | 4.0 | ₹17,048 | 174 | 11.5 | 48 % | 100 |
| Hard (38 req/h, patience ×0.8, 5 rivals, ₹3,000) | 20/20 | 4.0 | ₹17,193 | 185 | 20.1 | 54 % | 99 |
| No dispatching at all | 0/5 (fired at about 08:36) | 1 | — | 0 | — | — | < 20 |

The autopilot plays almost perfectly: it decides every 4 ticks and never hesitates. Human players will score lower. **Still to do in playtests (T17):**
- tune Hard so it differs in stars as well as in abandonments;
- check that 48 minutes of real time at 1× (shift length) feels right — the speed keys allow 2×/4×/8×;
- decide whether satisfaction should decay towards 70 so the score doesn't pin at 100.
