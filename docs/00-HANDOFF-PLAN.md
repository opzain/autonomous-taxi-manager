# Autonomous Taxi Manager — Handoff & Master Plan
**Course:** Pinnacle 1 (Game Design Engineering), Project 15: Simulation – Autonomous Taxi Manager.
**Team:** Tirth, Sarthak, Zain, Sahil.
**Co-ordinator:** Dr. Rishika Aggrawal. **Supervisor:** Prof. Dr. Rabinder Henry.

> **For the next AI agent or teammate:** this file holds every decision, the current status and the next steps. Read it top to bottom, then continue from **§9 Next steps**. Attach the spec PDF (`Pinnacle1_Project_15_Autonomous_Taxi_Manager_Detailed_Specification.pdf`) as well if you have it. A copy of this file also sits at the repository root as `HANDOFF-PLAN.md`.

---

## 0. Status and progress log

| Date | Version | What was done |
|---|---|---|
| 2026-10-29 | v0.1 | Read the spec, made the architecture decisions, wrote the master plan. |
| 2026-10-29 | v0.2 | **3D BKC world built:** the simulation core (map, terrain, Dijkstra, PCG32 random numbers), 9 custom URP shaders, procedural BKC city (terrain, river, roads, bridges, flyovers, ~800 buildings, 14 landmarks), transit (locals, Metro 2B, Sea Link, planes), ambient traffic with the fleet showcase, day/night cycle, monsoon, post-FX, camera with x/y/z movement, overlay, route and closure demo, graphics tiers, one-click editor setup, xUnit tests, docs. |
| 2026-10-29 | **v0.3** | **BKC → Kurla corridor:** the city now runs continuously from G-Block East through **Equinox Business Park / ATLAS SkillTech University** along **LBS Marg** to **Kurla Station**. Added 3 locations (Equinox/ATLAS, Phoenix Marketcity, Kohinoor City) for 22 in total, 7 new roads (36 in total), 6 corridor districts (about 220 more buildings), new landmarks, the metro rerouted via Equinox, and an updated test. |
| 2026-10-30 | **v0.4** | **Neon Mumbai 2077 graphics:** a style switch (V); neon trims and holo-ads in the CityTower shader; new HoloAd and VolumeBeam shaders; smog height fog; wet neon streets; local box-projected probes; rooftop billboards, blade signs, searchlights, a giant fleet hologram; eVTOL air-taxis; split toning, lens dirt, motion blur, lens flare; TAA in Cinematic. Design notes are in `docs/11` §7–8. |
| 2026-10-30 | **v0.4.1** | **Refinement & verification:** fixed B001 (start hour) and B002 (air-taxi pads). **Verified:** 16/16 unit tests pass; all Unity C# type-checks with 0 errors; 92/92 shader stages compile against the Unity 6 URP library. Added `tools/verify/run_checks.sh`. |
| 2026-10-30 | **v0.5** | First Play-mode run on the team's Unity 6.6: the world generated in 2.1 s at 166 FPS (Ultra). Feedback, with reference images (GTA V day, cyberpunk wet street), led to: fixing B005 (distant window noise → distance LOD) and B006 (clutter and too much neon); the **Auto/Neon/Realistic** style (V, Auto default); **real CC0 texture + HDRI support** (`Resources/TaxiSimTextures`, `docs/13`); a **street camera** (G); a 15:30 start. All checks pass. |
| 2026-10-31 | **v0.6** | The team's ambientCG textures (Road009C, PavingStones151, Grass004, Concrete044C, Ground079L, Rocks006) are in use. Feedback that it looked 'very bad' was fixed: reflective glass (B007), wall brightness (B008), empty land filled by street-frontage shop-house rows + grid infill (B009), less neon at night (B010), shopfronts, 8 m footpaths with kerbs, planted medians, and realistic leaf-card trees in Mumbai species (Foliage shader + TreeFactory). Checks: 16/16, 0 errors, 108/108. |
| 2026-10-31 | **v0.7** | **Street life + AT Fleet Hub:** traffic signals at every 3+ way junction; a rewritten driving model (car-following, red-light stops, irregular per-driver speeds, lane changes, scooters + trucks, 60–240 vehicles); pedestrians (Character shader with GPU walk cycle, 16 outfits, 60–400 walkers + standing crowds); roadside stalls, BEST bus shelters, pole/rooftop/wall billboards (Billboard shader); L/U/twin/stepped buildings, wider palette, real balconies, Sintex tanks, AC units, parapets; new node `fleet_hub` (23 locations, 38 roads) with a sawtooth warehouse depot and the FleetHubFloor shader. B011 fixed. Real map data was considered and **dropped by the team** (the stylised map stays). Checks: 17/17, 0 errors, 148/148. |
| 2026-10-31 | **v0.8** | **Polish:** procedural city sound (20 synthesised sounds: traffic, horns by vehicle type, rain, thunder, wind, stations with chime + PA, train rumble, Fleet Hub hum, crows, crickets, crossing beeps; N mutes; real recordings can override); **cinematic mode C** (10 shot types, letterbox, captions, DOF, auto cuts; J next shot); **pedestrians cross at signals** (walk phase, zebra crossings, stop lines, vehicles yield). Docs: `docs/14-sound-cinematic-video.md`. Checks: 17/17, 0 errors, 148/148, 20/20 sounds. |
| 2026-11-05 | **v0.9** | **Simulation core done** (`Scripts/Core/Sim`): taxis, requests, energy and charging, economy, satisfaction, 7 event types, utility dispatch, surge + RapidCab best response (Nash equilibrium, Prisoner's Dilemma), event-sourced save/replay, autopilot. **Extras:** title / pause / settings menus, live minimap (K), festivals (Y: Ganpati procession, Diwali lights and fireworks), monsoon waterlogging. Docs 02 and 06 written. Checks: **43/43** tests, 0 errors, 148/148, 22/22 sounds. |
| 2026-11-06 | **v0.10.1** | **Console hardware design:** Wokwi simulation (`firmware/wokwi`), self-test firmware, wiring diagram and breadboard checklist (`docs/03-hardware.md`). The team's Unity editor and project in `Downloads\harness` were deleted; they are reinstalling into `C:\Unity\` (keep everything out of Downloads). |
| 2026-11-05 | **v0.10** | **Realism pass** (requested before wiring the game): parked cars and scooters, electric poles with tangled cables, black-yellow kerbs, speed breakers, bins, carts, dogs, cows; potholes, manholes, oil and cracks in the road shader; window ACs, pipes, awnings, grilles, monsoon moss, glass-tower fins; sun-glow haze and filmic grading; grass and weed tufts, dirt and litter. Checks unchanged and passing. |

### Phase checklist

- [x] Concept and architecture decisions (D1 input)
- [x] City map/graph — 23 locations, 38 roads: BKC plus the corridor via Equinox / ATLAS to Kurla Station, plus the AT Fleet Hub depot
- [x] Route engine — Dijkstra with traffic and closures, plus tests T07/T08
- [x] 3D world, shaders, lighting, weather, camera (this phase)
- [x] Offline verification: C# type-check plus shader compile (`tools/verify`, v0.4.1)
- [x] **First open in the Unity Editor** — Unity 6000.6.8f1, no Console errors, city runs at 59 FPS on Ultra (v0.10.10)
- [x] `dotnet test sim-tests` — 17/17 pass (v0.7)
- [x] Street life: signals, traffic model, pedestrians, stalls, billboards, building variety, AT Fleet Hub (v0.7)
- [x] Polish: city sound, cinematic mode, pedestrian crossings (v0.8)
- [x] Simulation core: taxi state machine, requests/demand, trips, energy/charging, economy, satisfaction, events, game theory, save/replay, autopilot (v0.9, 43 tests)
- [x] Extras: menus, minimap, festivals, monsoon flooding (v0.9)
- [x] Connect the fleet in the 3D world to the real simulation: SimulationDriver drives the AT taxis and RapidCab, pins, ribbons, closures, rain and the sky clock (v0.11)
- [x] Dispatch HUD (IMGUI): page mirror with the utility breakdown, fleet panel, ticker, toasts, end card (v0.11)
- [x] ESP32 firmware v0.4 (game console + self-test) and the USB link: auto COM-port search, TFT pages, buzzer cues, keyboard fallback (v0.11)
- [~] Hardware: built off-board (ESP32 beside the breadboard), self-test passes on the real parts (v0.10.10); LED stick needs soldering; enclosure still to do
- [ ] Tests T01–T18, balancing, playtests
- [x] Final report (docs/report, .docx/.pdf) and all deliverable docs D1–D10 (v0.12); demo script in the report's Appendix B
- [ ] Team to complete: names, T17 playtests, receipts, a HUD screenshot, the demo video, viva preparation

### Where the files are

| Path | Contents |
|---|---|
| `unity/TaxiManager/Assets/_Project/Scripts/Core/` | Pure C#: `BkcMap`, `CityGraph`, `CityGeography`, `RouteFinder`, `Pcg32`, `Vec`, and **`Sim/`** (`Simulation`, `SimConfig`, `Entities`, `Commands`, `GameTheory`, `SaveGame`, `AutoDispatcher`) |
| `unity/TaxiManager/Assets/_Project/Scripts/World/` | The 3D world (see `docs/11-3d-world-bkc.md`) |
| `unity/TaxiManager/Assets/_Project/Scripts/Editor/TaxiSimSetup.cs` | The menu **Taxi Manager → Setup BKC 3D World (Ultra)** |
| `unity/TaxiManager/Assets/_Project/Shaders/` | CityTower, Road, Surface, PBR, Water, Sky, FlowLine, Holo, Particle, HoloAd, VolumeBeam, Foliage, Billboard, Character, FleetHubFloor + shared HLSL |
| `sim-tests/` | xUnit tests (`dotnet test sim-tests`) |
| `docs/` | This plan, the 3D world design, the report draft, images |

## 1. Decisions made by the team

| Topic | Decision |
|---|---|
| Game engine | **Unity 6 LTS**: C#, URP renderer, Input System; IMGUI overlay now, UI Toolkit HUD later |
| Rejected | **Roblox Studio**: no USB/serial access, HTTP calls to localhost are blocked, it would need a tunnel, add lag and depend on the internet |
| Laptop OS | **Windows** for the demo: `System.IO.Ports` talks to the ESP32 over a COM port. Development on a Mac also works for the 3D world. |
| 3D | A 3D city with camera movement on x/y/z (pan, height/zoom, orbit). The ESP32 TFT is the console screen, not the 3D map. |
| City | **Mumbai BKC** (stylised), decided in v0.2. **Extended in v0.3** at the user's request to run continuously through Equinox Business Park / ATLAS SkillTech University to Kurla Station |
| Game theory | **Full**: a per-taxi utility score, surge pricing, and a rival fleet ("RapidCab") |
| Docs | Engineering docs plus a 15–20 page draft of the final report |
| Graphics approach (v0.2) | Procedural geometry and custom HLSL shaders, with no paid or texture assets. This keeps the project lightweight and explainable. |
| Graphics style (v0.5) | The default became **Auto**: realistic GTA-style day, neon after dark. Photorealism comes from CC0 textures the team downloads (the build machine cannot reach Poly Haven / ambientCG). |
| Graphics style (v0.4) | The user asked for "Cyberpunk 2077 level" graphics. We implemented a switchable **Neon Mumbai 2077** look in URP. HDRP and ray tracing were rejected for now (it needs a shader rewrite and an RTX GPU, which the demo laptop is unlikely to have). |
| Camera (v0.2) | Our own `CityCameraRig` instead of Cinemachine, so there's one less package dependency and the ESP32 can call the same methods |

## 2. Spec summary (from the PDF)

- **Minimum scope:**
  - a city of 10–15+ nodes
  - at least 6 taxis
  - at least 5 queued requests
  - accept, reject, assign and reroute using physical controls
  - weighted shortest path
  - fuel and charging
  - at least 5 event types
  - economy and satisfaction
  - a finite shift
  - LED and buzzer feedback
  - save and load
- **Taxi states (§11):** IDLE, ASSIGNED, EN_ROUTE_PICKUP, PICKUP, EN_ROUTE_DROPOFF, COMPLETED, CHARGING, DELAYED, OFFLINE.
- **Events (§16):** road closure, heavy traffic, rain, festival, vehicle issue, charging queue, demand surge.
- **Modules (§18):** InputManager, GameStateManager, CityManager, RequestManager, FleetManager, RouteManager, TrafficManager, TripManager, EconomyManager, SatisfactionManager, UIManager, FeedbackManager, SaveManager, Test/Debug.
- **Tests T01–T18:** power, buttons, encoder, debounce, request generation, assignment, route, blocked road, traffic delay, fuel, charging, trip completion, economy, save/load, boundary cases, long run, playtest, regression.
- **Deliverables D1–D10:** concept, simulation architecture, hardware experiment, software architecture, BOM, vertical slice, testing evidence, final build, report (15–20 pages, IEEE references), demo and viva.

## 3. Hardware already bought

| Qty | Item |
|---|---|
| 1 | ESP32 DevKit V1, 38-pin |
| 1 | 2.8" ILI9341 SPI TFT |
| 8 | 6×6×5 mm tactile buttons |
| 1 | KY-040 rotary encoder |
| 1 | WS2812B LED module (4- or 8-LED) |
| 1 | 5 V active buzzer |
| 2 | 2N2222 / S8050 transistors |
| 1 | Resistor kit |
| 1 | 830-point breadboard |
| 1 | Jumper wire set (M-M and M-F) |
| 1 | 5 V USB adapter |
| 1 | 74AHCT125 (level shifter for the LED data line) |

Optional extras: a 1000 µF capacitor across the LED 5 V supply, and a 1N4148 diode across the buzzer.

## 4. Architecture

```
[7 buttons + KY-040] → ESP32 (debounce, TFT, WS2812, buzzer) ⇄ USB serial 115200 ⇄ Unity (SerialLink → Commands → TaxiSim.Core → 3D world + HUD)
```

- The simulation is pure C# (`TaxiSim.Core.asmdef`, `noEngineReferences`). It is deterministic and unit-tested.
- Unity draws the simulation. The ESP32 handles input and feedback.

## 5. Simulation core design (to implement next)

- **Determinism**
  - Uses PCG32 random numbers only.
  - Fixed step: 1 tick = 15 sim-seconds. The shift runs 06:00–22:00, about 16 real minutes at 1× speed.
  - Player inputs become `Command` objects, each stamped with the tick it applies on.
- **Save/load:** a save is the seed, the command log and a state hash. Loading replays the log (covers T14 and T18).
- **Routing:** already implemented in `RouteFinder` — Dijkstra on `BaseMinutes × TrafficMultiplier`, skipping `Blocked` roads, with a failure reason when no route exists.
- **Fleet:** 6 EVs, using the §11 states plus RELOCATING, with a table that rejects illegal transitions.
- **Requests**
  - Demand rate λ(time of day, zone, events), using the zone meanings in `docs/11` §2.
  - Each request has urgency 1–3, patience and fare.
  - Queue holds up to 8 requests.
- **Energy**
  - Each km uses 2.5% × (1 + 0.5·(mult − 1)) × grade.
  - Dispatch is refused if the trip needs more than the remaining charge minus a 10% reserve.
  - Below 20% is flagged low. At 0% the taxi goes OFFLINE and the player pays a ₹500 tow fee.
  - Charging adds +2% per minute. Stations have 2 or 3 bays (`ChargerSlots`).
- **Economy:** start cash ₹5,000. Income is fare × surge. Costs are energy, charging and a daily fee. Penalties for lateness, abandonment and towing.
- **Satisfaction (0–100):** affected by on-time trips, lateness, abandonment, rejections and surge.
- **Game theory**
  - Utility: U(i,j) = fare·s − c_E·energy − c_T·ETA·urgency − c_R·P(abandon) + β·Δsat. The best taxi is suggested; the top 3 can be cycled with the encoder.
  - Surge s ∈ {1.0 … 2.0}. Probability a passenger accepts = 1 − e·(s − 1) − w·wait.
  - Rival fleet RapidCab: 4 greedy taxis. Passengers pick the lower generalized cost (price + value-of-time × ETA), which is Bertrand-style competition. The rival best-responds every hour. The design doc should include a 2×2 payoff matrix and its Nash equilibrium.
- **Win/lose:** the shift ends at 22:00. The player loses if cash < −₹2,000 or satisfaction < 20%. Score = 0.5·profit + 0.3·satisfaction + 0.2·efficiency, shown as 1–5 stars.

## 6. ESP32 dispatch console (to implement)

**Pin map (ESP32 DevKit V1 30-pin; the final hole-by-hole wiring is in `docs/03-hardware.md`):**

| Function | GPIO | Notes |
|---|---|---|
| Up / Down / Left / Right | 32 / 33 / 25 / 26 | INPUT_PULLUP |
| Back / Accept / Reject | 27 / 14 / 13 | INPUT_PULLUP (INFO was dropped: one button broke; GPIO15 is free) |
| Encoder CLK / DT | 34 / 35 | Power the KY-040 from **3.3 V** (onboard pull-ups) |
| Encoder SW | 4 | INPUT_PULLUP |
| TFT SCK / MOSI / CS / DC / RST | 18 / 23 / 5 / 16 (RX2) / 17 (TX2) | VCC to 5 V (J1 open), backlight to 3.3 V |
| WS2812B-8 stick data | 22 → 330 Ω → IN | Stick VCC = 5 V; brightness capped at about 25%. Fallbacks in docs/03 §7 |
| Buzzer | 21 → 1 kΩ → 2N2222 base | Buzzer between 5 V and the collector |

**Input handling:** 25 ms debounce, 600 ms long press, interrupt-driven encoder.

**Dispatch mode:**
- Up / Down: select a request
- Encoder turn: cycle candidate taxis, ranked by utility
- Accept / Reject: assign / decline
- Left / Right: change TFT page (Dispatch / Fleet / Finance + Surge / Events); on the Fleet page Accept sends the selected taxi to charge or reroutes it
- Long-press Back: pause

**Camera mode** (long-press the encoder) uses the methods that already exist in `CityCameraRig`:
- Arrows: `Pan`
- Encoder: `Zoom`
- Accept / Reject: `Orbit`
- Press the encoder: `FollowNextTaxi` (long-press again leaves camera mode)

**Protocol** (ASCII lines, 115200 baud):

| Direction | Messages |
|---|---|
| ESP32 → PC | `H,TAXI-CONSOLE,ver` (hello), `B,btn,P\|L\|R` (button), `E,±n` (encoder), `K` (keepalive) |
| PC → ESP32, 5 times a second | `S` (header), `T` (page), `R,row,color,text`, `L,8×rgb`, `Z,pattern` |

If either side hears nothing for 3 s, it shows "LINK LOST" and the game falls back to the keyboard. Turn off DTR/RTS so opening the port doesn't reset the ESP32.

## 7. 3D world summary (v0.2, done)

Full details are in `docs/11-3d-world-bkc.md`.

1. In Unity Hub, create a project with the **Unity 6 "Universal 3D"** template.
2. Copy in `Assets/_Project`.
3. Run the menu **Taxi Manager → Setup BKC 3D World (Ultra)**.
4. Press Play.

## 8. Verification plan

- `dotnet test sim-tests` should pass.
- The project should open in Unity 6 with zero Console errors.
- The world should generate in under 3 s, and the FPS should be acceptable on the demo laptop (start at Ultra, drop to High if needed).
- Hardware tests T01–T05 go through the ESP32 self-test page; the rest use automated tests plus the playtest.

## 9. Next steps (in order)

> **Status v0.11:** steps 1–6 below are done; the game is playable with the console (docs/16-dispatch-game.md). Unity 6000.6.8f1 runs the city at 59 FPS on Ultra on the team's laptop (v0.10.10), and the console hardware passes the self-test. **Verified 2026-11-11:** the game runs in Unity and is fully controllable from the real ESP32 console. **Next:** screenshots and a demo video for the report, Then come the save/load menu (the SaveGame core is ready), a tutorial scenario, the enclosure (docs/10) and the final report.

1. **Open in Unity and fix any compile errors.** Do this first: the C# and HLSL have never been compiled. Record each problem in `docs/09-bug-log.md`.
2. Run `dotnet test sim-tests`.
3. **Simulation core:**
   - add `Simulation.Step()`, `Command`, `Taxi` + state machine, `RequestManager`, `TripManager`, energy, economy, satisfaction, `TrafficManager`, events, game theory, save/replay
   - add tests for each part
4. **Bind the fleet in the 3D world** to `Simulation` (replace `TrafficAmbient` fleet roaming). Draw active routes with `RouteShowcase`-style flow lines and request pins with the Holo material.
5. **Dispatch HUD:** request queue, fleet panel, finance and surge, event ticker.
6. **ESP32 firmware** (PlatformIO, including the self-test page), then `SerialLink`/`ConsoleBridge` in Unity.
7. **Remaining docs:**

   | File | Content |
   |---|---|
   | `01-concept` | Concept |
   | `02-simulation-architecture` | Simulation architecture |
   | `03-hardware` | Hardware |
   | `04-software-architecture` | Software architecture |
   | `05-bom-budget` | BOM and budget |
   | `06-game-design-balancing` | Game design and balancing |
   | `07-test-plan` | Test plan |
   | `08-user-manual` | User manual |
   | `09-bug-log` | Bug log |
   | `10-enclosure` | Enclosure |

   Also fill in the report draft.

### Prompt to paste into the next agent

> I'm continuing the Autonomous Taxi Manager project (Pinnacle 1, Project 15): Mumbai BKC through Equinox / ATLAS SkillTech University to Kurla Station. The attached repository contains `HANDOFF-PLAN.md` (all decisions, status and the plan) and `docs/11-3d-world-bkc.md` (the 3D world design). Continue from "§9 Next steps": first help me fix any Unity compile errors I paste, then implement the simulation core with tests, keeping `Scripts/Core` free of UnityEngine and deterministic.
