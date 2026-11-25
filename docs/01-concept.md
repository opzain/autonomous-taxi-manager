# 01 — Concept (D1)

**Autonomous Taxi Manager** · Pinnacle 1 Project 15 (Simulation) · Team: Tirth, Sarthak, Zain, Sahil

## 1. One-line pitch
You are the night-and-day dispatcher of a six-car **autonomous electric taxi fleet** in Bandra Kurla Complex, Mumbai. Using a physical dispatch console, you decide which robot taxi picks up whom, when to charge, how to dodge closures and monsoon traffic, and what to charge riders. A rival fleet, RapidCab, competes with you for every fare.

## 2. Player and role
- **Player:** one person at a desk with the ESP32 dispatch console, and the 3D city on a laptop.
- **Role:** a fleet operations manager. The player does not drive, because the taxis are autonomous. The player makes the *decisions a fleet manager makes*:
  - accept or reject a request;
  - which taxi to send;
  - when to charge;
  - where to wait;
  - what price to set.

## 3. Objective, win and lose
Run one shift, **06:00 → 22:00**:
- **Win:** reach 22:00 with the best score. Score = 50 % profit + 30 % passenger satisfaction + 20 % fleet efficiency, shown as 1–5 stars.
- **Lose (game over):** cash falls below −₹2,000 (bankrupt), or satisfaction falls below 20 % (the city cancels the licence).

## 4. Core loop (every few seconds)
**Request appears → choose the request → choose the taxi (suggested by a utility score) → ACCEPT → the taxi drives the route → pickup → drop-off → fare + satisfaction → watch batteries, events and price → repeat.**

Every decision is made on the console's buttons and knob. The console's TFT, buzzer and LEDs give feedback, so the hardware is part of play, not decoration (handbook rule).

## 5. Scope
| In scope (built) | Out of scope (deliberately) |
|---|---|
| 23-location graph of BKC → Equinox / ATLAS → Kurla, 38 roads, Dijkstra routing | A real-scale or real-data map, real autonomous-driving AI |
| 6 player EV taxis with a strict state machine; 3–5 rival RapidCab taxis | More than one fleet depot / vehicle types with different stats |
| Rule-based demand by time, zone and events; 3 urgency levels; up to 8 queued requests | Machine-learning demand (not required in Semester I) |
| 7 event types (spec §16): closure, heavy traffic, rain, festival, vehicle issue, charger queue, demand surge | Multi-day campaigns, staff management |
| Battery, charging stations with slots and queues, towing | Fuel price fluctuations |
| Economy, satisfaction, surge pricing and a price war with game theory | Online multiplayer |
| ESP32 console: 7 buttons, encoder, 2.8" TFT, buzzer, optional 8-LED stick | Wireless (BLE) console |
| Save files that replay the shift exactly (save menu UI is future work) | Cloud saves |

## 6. Setting and tone
Mumbai's business district on a working day: the morning rush to the offices, the evening rush to Bandra and Kurla stations, monsoon downpours, festival crowds at the MMRDA Grounds, and medical runs to the Asian Heart Institute.

The 3D world is procedural and stylised but recognisable. It includes Jio World Centre, NSE, the Bharat Diamond Bourse, the Equinox Business Park with ATLAS SkillTech University, Phoenix Marketcity and Kurla Station. It switches between a realistic day look and a neon "Mumbai 2077" night look.

## 7. Why it is interesting (design pillars)
1. **Readable decisions:** the game always suggests a taxi and *shows why* (the utility breakdown), so you learn the model by playing.
2. **Trade-offs, not reflexes:**
   - a near taxi or a charged one;
   - a fare now, or a medical passenger in two minutes;
   - a high price that earns more per ride, or a low price that keeps riders away from RapidCab.
3. **A physical dispatcher's desk:** buttons with fixed jobs, a knob for choosing, and a screen that mirrors the page.
4. **Deterministic and testable:** the same seed and the same decisions give the same shift, so bugs can be reproduced and saves replayed.

## 8. Similar games and references studied
- *Mini Metro* (Dinosaur Polo Club): minimal transit management with a clear graph.
- *SimCity* / *Cities: Skylines*: traffic as a system the player shapes.
- *Transport Tycoon*: routing, vehicles and economy.
- Ride-hailing dispatch: nearest-car dispatch with surge pricing, simplified and documented here as a utility formula.
- See the report's reference list (IEEE) for the technical sources: Dijkstra, PCG random numbers, the ESP32/ILI9341/WS2812B datasheets, and the Unity URP documentation.
