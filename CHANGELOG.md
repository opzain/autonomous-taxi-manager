# Changelog

## v0.15 — Knob removed; the right-stick click is NEXT (2026-11-25)
- **Hardware:**
  - KY-040 rotary encoder and its 5 wires removed.
  - Right joystick SW → D4 (F-F wire F). D34 and D35 are now free.
  - Wiring checker: 40 wires, no shorts, D34 and D35 must stay free; layout regenerated (knob card removed).
- **Firmware v0.7:**
  - encoder code and `E` lines removed;
  - new NEXT button on D4 (internal pull-up);
  - self-test right-hand box is now "STICKS" with L / R pads and a legend.
  - Harness: 33/33 checks, including NEXT on D4 and no `E` lines.
- **Game:**
  - `ConsoleButton.Next` (`B,NEXT,…`): short press = one knob step (`Turn(+1)`: next taxi / spot / price / scroll, or difficulty before a shift); long press = show in the city.
  - Every "knob" hint on the TFT footers, help lines and HUD now says R-click (all ≤ 52 characters).
  - KNOB is still understood, for older consoles.
  - 73 tests pass (1 new); the Unity type-check is clean.
- **Wokwi:** encoder removed, `joyR:SEL` → D4.
- **Docs:** 03, 08 (knob → R-click table), 10.

## v0.14 — 8 panel push buttons, INFO is back (2026-11-25)
- **Hardware:** 12 mm round panel push buttons (2 lugs) replace the breadboard tactile switches.
  - Lug 1 → ESP32 pin, lug 2 → top blue GND rail.
  - **INFO** is the 8th button, on **D19**.
  - `tools/offboard_plan.py` models 2-lug buttons. It verifies 44 wires, all 8 buttons pressed and released, no shorts, and no button on a power rail; a planted mistake was caught.
  - New layout: tags at every ESP32 button pin and a "panel push buttons" strip.
- **Firmware v0.6:**
  - INFO button on D19;
  - `SPI.begin(18, -1, 23)` starts the screen bus without MISO, so D19 stays a plain input;
  - INFO box on the self-test screen.
  - Harness: 31/31 checks, including INFO, UP on D15, and SPI started without MISO.
- **Game:**
  - `ConsoleButton.Info` (`B,INFO,…`).
  - Short press shows a help line for the page (all lines ≤ 52 characters, so they fit the TFT footer).
  - Long press sets `HomeRequested`, which flies the camera home (`SimulationDriver`).
  - The I key is the keyboard stand-in.
  - The button arrays are now sized from the enum.
  - 72 tests pass (2 new); the Unity type-check is clean with both input systems.
- **Wokwi:** `btnInfo` on D19.
- **Docs:** 03, 08 §4a, 10 (panel buttons need no button bed; Ø12 is the snug hole).

## v0.13 — Two joysticks explore the city (2026-11-25)
- **Hardware:** two HW-504 / KY-023 joysticks.
  - Left stick VRx/VRy → D32/D33; right stick → VP/VN (all ADC1 pins).
  - Both sticks are powered from **3V3**.
  - UP moved D32 → D15 and DOWN D33 → D22. The LED stick (D22) is dropped.
  - `docs/images/breadboard-layout.png` was regenerated from the checked netlist (42 wires, no shorts). A new check fails if a stick is powered from 5V, and a planted mistake was caught.
- **Firmware v0.5** (`firmware/wokwi/sketch.ino` = `dispatch_console/src/main.cpp`):
  - joysticks are read with 4-sample averaging and smoothing;
  - the centre is measured at start-up, with a 12 % dead zone;
  - new `J,lx,ly,rx,ry` line, sent 20× a second while a stick is off-centre, then a single zero line;
  - self-test shows an L / R stick pad, and "ALL INPUTS OK" now needs both sticks;
  - new Serial Monitor command `cal`; `pins` also prints the stick readings;
  - INVERT / SWAP options for sticks mounted another way round.
- **Game:**
  - `ConsoleProtocol` parses `J` lines (new `ConsoleInputKind.Sticks`), and `SerialLink` accepts them.
  - `ConsoleBridge` drives the city camera: left stick pans, right stick X turns and Y zooms, with a gentle response curve.
  - The camera stops if the link goes quiet for 0.4 s.
  - Speeds can be tuned in the Inspector (`StickPanSpeed`, `StickTurnSpeed`, `StickZoomSpeed`).
- **Checks:**
  - 70 simulation tests pass (new joystick parse test);
  - the Unity type-check passes with both input systems;
  - firmware harness 27/27 (8 new joystick checks).
  - Wokwi `diagram.json` updated with two analog joysticks.
- **Docs:** 03 hardware, 08 user manual (§4b Joysticks), 10 enclosure pin plan.

## v0.12.4 — Console firmware v0.4.1 (2026-11-24)
- **Firmware reviewed and updated to v0.4.1** (`firmware/wokwi/sketch.ino`, same source in `firmware/dispatch_console/src/main.cpp`). Pins and protocol unchanged: no rewiring.
  - New sound engine: every sound is non-blocking (start-up beeps no longer use delay), and the buzzer pin is held LOW when quiet. `BUZZER_ACTIVE 1` option for active buzzers.
  - Serial Monitor commands `beep` (sound test with a wiring hint), `pins` (live button states) and `help`. Replies start with '#', so the game ignores them.
  - The encoder's starting state is read from the pins, so the first turn is never miscounted.
  - Only real game lines (S T R F M Z L) switch to console mode; junk typed in the Serial Monitor no longer does.
  - Screen SPI clock set to 27 MHz, which is more reliable over jumper wires.
- Firmware harness: 7 new checks (19 in total, all pass), including the start-up beeps, the `beep` command, buzzer off after every sound, and junk lines staying in self-test. The active-buzzer build is also compile-checked.

## v0.12.3 — Cardboard controller enclosure (2026-11-24)
- **New enclosure design (v2):** a PS5-style two-grip controller in 3 mm corrugated card, 268 × 186 × 66 mm.
  - Holds the 2.8″ screen (60 × 46 window), 8 buttons (12 × 12 tactile + 12 mm caps, Ø13 holes), 2 KY-023 joysticks (Ø28), the KY-040 knob (Ø7) and a buzzer grille.
  - The full-size breadboard and the ESP32 sit inside; the USB cable exits at the top centre.
- `docs/enclosure/controller-templates-1to1.pdf` holds the A4 1:1 templates (lid, base, small parts), with check bars. The printed scale was verified: the outline measures 268 mm and the check bar 100 mm.
- `docs/enclosure/controller-build-guide.pdf` (10 pages): materials, cutting list, outline construction for drawing by hand, hole table, inside layout, side section, build steps, wiring.
- `docs/10-enclosure.md` now leads with v2 (v1 MDF kept below it). Generator in `tools/enclosure/`.
- **Wiring drawing fix (buzzer):** `docs/images/breadboard-layout.png` drew the 2N2222 with its flat face pointing AWAY from the reader, while the note said towards; following the picture puts E and C the wrong way round and the buzzer stays silent. The transistor is now drawn flat face (yellow edge) towards row e, the buzzer legs (− b47, + b50) are visible, and a 'Buzzer circuit — check hole by hole' panel was added. Netlist re-verified (38 wires, no shorts).
- **Planned pin plan for the joysticks and the 8th button (INFO)** is documented, but not active until firmware v0.5.

## v0.12.2 — Report and docs updated (2026-11-21)
- Final report (Word + PDF): now describes v0.12.1; bug history has 19 entries including B019; version list extended to v0.12.1; new **Appendix C: Keyboard and mouse controls** (every key) with the shift-clock note.
- docs/08 user manual §5 now lists every key; docs/16 and docs/07 (test log) updated for the B019 fix.

## v0.12.1 — Time-of-day slider fix (2026-11-19)
- Fix B019: during a shift, Settings > Time of day now shows 'Sky follows the shift clock - click to set it freely'. One click frees the slider (it changes the look only, not the shift's clock). When a shift ends, the sky clock is released and time-lapse is restored. Changed `SimulationDriver.cs` and `GameMenu.cs`.

## v0.12 — Complete documentation set and final report (2026-11-11)
- **Final report (D9):** `docs/report/final-report.md`, plus a styled **Word file and PDF** (`Autonomous_Taxi_Manager_Final_Report.docx/.pdf`, 22 pages: 20 pages of report plus appendices).
  - It covers all 18 sections of spec §32 and has 9 figures and 18 IEEE references.
  - Appendix A maps every deliverable D1–D10 to a file; Appendix B is a demo script.
  - `tools/report/build_report.sh` rebuilds the Word file and PDF.
- **New docs:** 01 concept (D1), 04 software architecture (D4), 05 BOM and budget (D5), 07 test plan and results (D7, the T01–T18 matrix with evidence and status, plus a playtest form), 08 user manual, 10 enclosure (with a drawn top panel).
- **New figures** (`tools/report_figures.py`, `tools/tft_mockups.py`):
  - taxi and request state machines;
  - the architecture;
  - the enclosure;
  - the balance chart;
  - TFT pages drawn from the game's real output;
  - evidence photos and screenshots.
- **Corrections:**
  - shift pacing (48 minutes at 1×) in docs 02/06;
  - bug log reordered, with B016–B018 added;
  - protocol stream generator now plays normally, so the figures show realistic play.
- **Still for the team to fill in:**
  - names in the roles table;
  - the T17 playtest results and the T04/T16 records;
  - receipt prices in docs/05;
  - a screenshot of the HUD during a shift;
  - access dates on the online references.

## v0.11.1 — Verified end to end on the real console (2026-11-11)
- **The team confirmed it works on the real hardware:** firmware 0.4-game uploaded from the Arduino IDE, the TFT switches from self-test to the game, and a dispatch shift in Unity is fully controllable from the console's buttons and knob. This is evidence for spec tests T01–T05 and T17 (LED stick not fitted).
- Fix B015: `struct Note` moved above the first function, because the Arduino IDE's automatic prototypes failed with "'Note' does not name a type". The check script now emulates the IDE's prototype insertion.
- SerialLink is enabled under both `NET_4_6` and `NET_UNITY_4_8` (Unity's .NET Framework defines).

## v0.11 — The dispatch game is playable with the ESP32 console (2026-11-11)
- **`Core/Dispatch/DispatchConsole.cs`** is the console's brain. It's engine-free and unit-tested.
  - Buttons and the knob become simulation commands across four pages: Dispatch, Fleet, Money (price war), Events. Camera mode, pause and 1×/2×/4×/8× speed are included.
  - It builds the TFT pages and turns simulation messages into buzzer cues and toasts.
  - Long and short presses are kept apart: LEFT/RIGHT change page, and holding them changes the speed.
- **`Core/Dispatch/ConsoleProtocol.cs`:** the PC ⇄ ESP32 protocol. It parses B/E/H/K and encodes S/T/R/F/M/Z/L as ASCII-only lines.
- **`World/Game/SimulationDriver.cs`** runs the simulation in the 3D city:
  - fixed ticks, with 1× = 3 s per sim-minute;
  - the six AT taxis and the RapidCab cars follow their simulated routes, smoothly between ticks, and other traffic queues behind them;
  - passenger pins (green, gold, red), cyan trip and orange pickup ribbons, closure barriers that ambient traffic U-turns at;
  - sim rain switches on the monsoon, sim traffic drives the heat overlay, and the sim clock drives the sky.
- **`World/Game/DispatchHud.cs`:**
  - a top bar and the console page mirror, with the utility-formula breakdown and a price payoff table plus the Nash equilibrium;
  - a fleet panel with battery bars, an event ticker, toasts, a pause banner and the end-of-shift star card;
  - labels over taxis and pins. Rows are clickable.
- **`World/Game/SerialLink.cs` + `ConsoleBridge.cs`:**
  - background-thread USB serial at 115200 that finds the console's COM port by itself, with DTR/RTS off and LINK LOST after 3.5 s;
  - only changed lines go to the TFT (about 1.2 KB/s);
  - keyboard stand-in: arrows, Enter, Backspace, Delete, `, . /`, P and `- =`;
  - PC beeps when no console is connected.
- **Title menu:** START DISPATCH SHIFT plus a difficulty choice. Console ACCEPT or Enter also starts a shift. Esc → Main menu ends it.
- **Setup menu** now sets **Api Compatibility Level = .NET Framework** (needed for System.IO.Ports).
- **Firmware v0.4-game:** self-test at power-on, then game console mode on the first game line.
  - Header, title with page dots, 8 rows that redraw only when changed, footer, toasts, LINK LOST after 4 s.
  - Non-blocking buzzer patterns, and LED colours when `HAS_LEDS` is 1.
- **Verification:**
  - 69 tests (26 new console and protocol tests, including a save/replay test of console commands);
  - all Unity scripts type-checked with both input systems and with System.IO.Ports;
  - new check step 5 runs the firmware on the PC (ASan/UBSan) against the game's real line stream. All 12 harness checks pass, including LINK LOST, recovery and garbage lines.
  - The harness caught a bug: the "(k/n)" taxi counter compared candidates by reference. Fixed, with a regression test.
- **Balance check at human speed:** pressing ACCEPT every 2–9 s at 1× completes Easy 6/6 and Normal 4–5/6 shifts. Hard needs the Fleet and Money pages.
- **New docs/16-dispatch-game.md** (how to play). docs/03 §9 has the full protocol; the setup guide, README and handoff plan are updated.

## v0.10.10 — Console hardware verified on the real parts (2026-11-11)
- **Self-test passes on the team's console:** ESP32 (CP2102, COM8), ILI9341 screen, 7 buttons, KY-040 knob (turn + press) and buzzer all work, and the screen shows **ALL INPUTS OK**. Serial protocol confirmed at 115200 (`H,TAXI-CONSOLE,0.3-selftest`, `K`, `B,…`, `E,…`). This is evidence for spec tests T01–T04 (LED stick not fitted yet).
- Bug log: B013 (two button GND links in the wrong holes), B014 (missing CP2102 driver).

## v0.10.7 — Screen-first bring-up (2026-11-11)
- **Firmware v0.3-selftest:** new switches `HAS_ENCODER` and `HAS_LEDS` at the top of `sketch.ino` / `main.cpp`. Both default to **0**, so the screen, buttons and buzzer can be tested before the knob and LED stick are wired. Without this, the unconnected D34/D35 float and send junk encoder steps. With the encoder off, "ALL INPUTS OK" no longer waits for KNOB. Set both to **1** once those parts are wired, and for the Wokwi simulation.
- Bring-up note: TFT SDO(MISO) is never needed. The TFT LED (backlight) wire *is* needed, so borrow a male–female wire from the not-yet-wired encoder or LED stick.

## v0.10.6 — ESP32 beside the breadboard (2026-11-11)
- On the team's breadboard the 1-inch-wide ESP32 covers rows b to j. Pulling the halves apart (v0.10.5) was not an option, so **the ESP32 now sits off the board**. Every wire to it has a female end on the pin.
- **New `tools/offboard_plan.py`** (replaces `breadboard_plan.py`):
  - The breadboard keeps the buttons (straddling the gap, diagonal legs, so they work either way round), the 2N2222 + 1 kΩ + buzzer, the 330 Ω and the rails.
  - Wires: **38 = 21 male–female + 8 female–female + 9 male–male**. The screen's CS / RESET / D/C / MOSI / SCK and the encoder's CLK / DT / SW go straight to the ESP32 with female–female wires.
  - Verified: every connection, buttons in both orientations, no power shorts, D12 and other boot pins free, one wire per pin, wire types match their ends.
  - Mutation tests catch: buzzer + on GND, a button wire in the wrong strip, the encoder on 5V, and D12 used instead of D22.
- **The drawing shows every ESP32 → breadboard wire as a coloured line.** To keep any lines from crossing, the ESP32 sits to the **left** with USB-C pointing left, the button order is now UP … REJECT left to right, the buzzer driver is in the top half and the 330 Ω in the bottom half.
  - Rails are now: top red = 5V, bottom red = 3V3.
  - A drawing check confirms the 13 ESP32 wires don't cross or overlap.
  - Letters A–H mark the female–female wires, and numbered dots mark the device power wires.
- **`docs/03-hardware.md`:** new Stage 0 (take the ESP32 off), the wire budget (8 female–female needed; buy a mixed jumper pack if short), rewritten Stages 1–6 and a regenerated wire list. Firmware, Wokwi and the pin map are unchanged.

## v0.10.5 — Layout for the real ESP32 width (2026-11-11), superseded by v0.10.6
- **Problem found from the team's photo:** the 30-pin board's pin rows are **1 inch** apart. On a closed breadboard it fits only in rows b + j, which leaves no free hole beside the EN … VIN header (buttons, encoder, 5 V).
- **Fix:** the team's breadboard is modular, so its halves are pulled **one hole apart**. The ESP32 then sits in rows **b + i** and rows a + j stay free (new **Stage 0** in `docs/03-hardware.md`). Fallback if the halves won't separate: a second breadboard.
- **Buttons move inside the bottom half** (legs in rows f / h), because 6 × 6 mm buttons can't straddle the wider gap. Each button's GND link now goes to the **bottom** GND rail from row j.
- **`tools/breadboard_plan.py`:**
  - new checks: nothing plugged under the ESP32 or a button body, no button straddling the gap, every hole on the board, no button pin touching 3V3 or 5V;
  - a button turned 90° is now modelled as harmlessly "always pressed".
  - Verified: 38 wires (22 male–male, 16 male–female).
  - Mutation tests catch: a GND link to 5V, a GPIO wire in the wrong strip, both wires on one strip, a button straddling the gap, and a wire under the ESP32.
- The breadboard drawing, wire table and connection-diagram note are regenerated. Firmware and Wokwi are unchanged; the pin map is the same.

## v0.10.4 — 7 buttons + WS2812B-8 LED stick (2026-11-10)
- **One button broke → the console now has 7 buttons.** INFO is gone and its job moves to **LEFT / RIGHT = change screen page**. In camera mode, pressing the knob follows the next taxi. GPIO15 (a boot pin) is now free.
- **The team's WS2812B-8 stick is wired in:** GPIO22 (a50) → a48 → **330 Ω (d48–d44)** → stick IN. Stick VCC goes to the 5V rail and GND to the GND rail. You first solder a header to the stick's IN-end pads.
- **`tools/breadboard_plan.py`:**
  - The layout is re-verified: 38 wires (22 male–male, 16 male–female), both button orientations, no shorts.
  - New mutation tests catch: a wire skipping the resistor, the stick on 3V3, D22 to the wrong hole, and a resistor with both legs in one strip.
  - The drawing adds the 330 Ω, a purple D22 channel and an LED pin card. The dot table now has 16 entries.
- **`connection_diagram.png`:** the INFO button is removed. D22 → 330 Ω → LED IN is drawn, and the stick appears with its IN / VCC / GND tags.
- **Firmware v0.2-selftest:** 7 buttons; the LED pin comment is updated. It passes the syntax check against the stub headers.
- **Wokwi:** `btnInfo` removed. A `wokwi-resistor` (330) and a `wokwi-neopixel-matrix` (1×8) are added on D22 / VIN / GND.1. All 35 connections are checked against Wokwi's devkit-v1 pin names.
- **`docs/03-hardware.md`:**
  - a new **Stage 6** for the LED stick: soldering, placement and the test;
  - §7 rewritten as LED troubleshooting, in order: shorter wire / capacitor, then power the stick from 3V3, then the 74HC125 buffer (HC caveat);
  - the wire budget, the power budget and the 7-button control map.
- Handoff plan pin map/controls and the inventory are updated.

## v0.10.3 — Clean wiring diagrams (2026-11-10)
- **New `docs/images/connection-diagram.png`** has no crossing wires: the ESP32 is drawn like the real 30-pin board (USB-C at the bottom), and every part sits beside the pins it uses.
- **`breadboard-layout.png` redrawn cleanly:**
  - straight right-angle wires in channels;
  - buttons reordered so their 7 signal wires nest without crossing;
  - the screen and encoder shown as numbered dots, with pin cards and a lookup table.

  The netlist checker passes on the new layout, and the mutation test still catches a planted 5V–GND short.
- The wire table in `docs/03-hardware.md` is regenerated from the checked layout.

## v0.10.2 — Wiring for the team's actual parts (2026-11-10)
- **Hardware inventory from photos** (`docs/15-hardware-inventory.md`, `docs/hardware-photos/`):
  - an ESP32 DevKit V1 **30-pin** (USB-C), a KMRTM28028-SPI ILI9341 with touch, a KY-040 and 8 buttons;
  - 2 TO-92 NPN transistors, a 12 mm buzzer and an MC74HC125AN;
  - 10k, 1k and (probably) 330 Ω resistors, about 39 jumpers and an 830-point breadboard;
  - no WS2812B LEDs.
- **Checked breadboard layout** (`tools/breadboard_plan.py` → `docs/images/breadboard-layout.png` plus a 36-wire table):
  - The netlist check covers every connection, both orientations of each button, pressed and released, power isolation, D12 left free and no hole used twice.
  - A mutation test confirmed the checker catches planted faults.
- **`docs/03-hardware.md` rewritten** for the real parts: wire budget (23 male–male + 13 male–female), four part checks before wiring, a 5-stage bring-up with expected serial output at each stage, Arduino IDE / driver steps, and an optional LED add-on with the 74HC125 caveat.
- **Wokwi `diagram.json` switched to `wokwi-esp32-devkit-v1`** (30-pin) with the TFT VCC on VIN. 33 connections are checked against Wokwi's part pin names. The 38-pin wiring diagram was removed.

## v0.10.1 — Console hardware design: Wokwi simulation, wiring diagram, self-test firmware (2026-11-06)
- **`firmware/wokwi/`**: a complete Wokwi simulation of the console (ESP32 DevKitC, ILI9341, 8 buttons, KY-040, 8-LED NeoPixel ring and buzzer) in `diagram.json`, with `sketch.ino` and `libraries.txt`. All 36 connections are checked against Wokwi's own part definitions.
- **Self-test firmware** (`sketch.ino`, also in `firmware/dispatch_console/` for PlatformIO):
  - debounced buttons (25 ms) with long press (600 ms), and an interrupt-driven quadrature encoder;
  - an LED sweep and cursor, a different beep for each button, and an "ALL INPUTS OK" screen;
  - the serial protocol (`H`, `B`, `E`, `K`).

  It compiles cleanly against signature-exact stand-in headers.
- **`docs/03-hardware.md`** (deliverable D3): the parts list, Wokwi steps, a wire-by-wire breadboard checklist (74AHCT125 pin numbers, 2N2222 driver, decoupling), flashing steps for Arduino IDE and PlatformIO, the power budget and the protocol table.
- **`docs/images/console-wiring.png` / `.svg`**: the real-hardware wiring diagram, with the ESP32 pins in their physical 38-pin order.

## v0.10 — Realism pass: streets, facades, light and greenery (2026-11-05)
The team asked for a more realistic 3D city before connecting the game to it. All four areas are improved.

### Streets and clutter (`Builders/StreetClutterBuilder.cs`, Road shader)
- **Parking:** cars, kaali-peelis and autos parked half on the kerb (tilted, nose with the near-side traffic), plus rows of parked scooters on the footpaths.
- **Overhead cables:** concrete electric poles on every non-BKC street with sagging, tangled cables (3 power lines plus 2–4 telephone/TV cables per span), cables across the street, transformers and coiled cable bundles.
- **Road markings:** black-and-yellow painted kerbs on main roads and near every junction; speed breakers near both ends of side streets.
- **Street objects:** BMC garbage bins with overflowing bags at corners, hand carts (thelas), stray dogs (lying or standing), and a few cows in the older neighbourhoods.
- **Road shader:** potholes that fill with water and ripple in rain, cast-iron manhole covers, oil stains down each lane, hairline cracks, and gutter dirt. Side streets are rougher than arterials.

### Building facades
- **Shop-houses:** window AC units, drainage pipes, and tin / tarpaulin / canvas awnings over the shops, plus potted plants on balconies.
- **CityTower shader:** window grilles (black or green painted) on residential windows, monsoon moss and black algae low down and under ledges, and walls that darken towards the street.
- **Glass towers:** aluminium fins for real depth and shadow lines.

### Lighting and atmosphere
- **Haze:** it now glows warm towards the sun (Mie in-scattering), strongest at golden hour. A new global `_TS_SunColor` is set by TimeOfDay.
- **Filmic grading** (Shadows/Midtones/Highlights):
  - day: cool shadows, neutral-warm highlights;
  - golden hour: amber highlights, teal shadows;
  - blue hour and night: deep blue shadows, sodium-warm highlights;
  - monsoon: flattened.

### Ground and greenery
- **New grass-blade pattern** in the Foliage shader. Grass and weed tufts appear in:
  - parks and groves, and along the medians;
  - in gutters and at the foot of walls (outside BKC);
  - on vacant land, with reeds on the river banks.
- **Bare ground:** dry-earth patches and scattered litter on open ground; lawns and paving are left clean.

### Verified
- `tools/verify/run_checks.sh`: 43/43 tests, 0 C# errors (both input systems plus the editor), 148/148 shader stages, 22/22 sounds.

## v0.9 — The simulation core, plus menus, minimap, festivals and monsoon flooding (2026-11-05)
### Added: the dispatch game itself (`Scripts/Core/Sim/`, pure C#, deterministic)
- **`Simulation`:** 15-second ticks over the 06:00–22:00 shift.
  - 6 AT taxis start at the AT Fleet Hub; 3–5 RapidCab rival taxis.
  - **Requests:** a time- and zone-based demand model, Normal/Priority/Medical urgency, patience, fares and a 8-request queue limit.
  - **Battery:** energy use rises with congestion and flyovers, a dispatch is refused below a 10 % reserve, and charging stations have slots and a queue.
  - **Breakdowns:** a taxi that runs out of battery is towed (₹500).
  - **Events:** 7 kinds — closure, heavy traffic, rain, festival, vehicle issue, charger fault, demand surge. Taxis go DELAYED and ask for a reroute, which happens automatically after 10 min if ignored.
  - **Economy:** cash, satisfaction, lateness against the quoted time, a score with 1–5 stars, and the bankrupt / fired / shift-complete outcomes.
- **Commands** (`Assign`, `Reject`, `SendCharge`, `Reroute`, `Relocate`, `ReleaseCharger`, `SetSurge`) are the only way the player changes the game, and every command is logged.
- **`TaxiStateMachine`:** the spec's states plus RELOCATING; illegal changes are refused.
- **Dispatch suggestion:** `Rank()` scores each taxi with `U = fare·s − energy − time·urgency − P(abandon) + ΔSat`, explains infeasible taxis, and lets the encoder cycle the top 3.
- **Game theory** (`SurgePricing`): price acceptance, logit provider choice, best responses, the 5×5 payoff matrix and its Nash equilibria. It turns out to be a Prisoner's Dilemma (see `docs/06`). RapidCab re-prices every sim-hour.
- **`SaveGame`:** event-sourced saves (seed + command log + state hash). Loading replays the game and rejects a tampered file.
- **`AutoDispatcher`:** an autopilot that plays the game. It is the AI baseline for balancing and the long-run tests, and can be used as an in-game hint.
- **Tests:** 26 new, 43 in total. T06 state machine, T08 reroute and auto-reroute, T10 energy and low-charge refusal, T11 charging and queues, T12 fare × surge, T13 abandon/reject penalties, T14 save → load → identical continuation and tamper detection, T15 tow / busy taxi / full queue / losing conditions, T16 ten full shifts, T18 golden hash, plus utility, game-theory and determinism tests.
- **Docs:** `docs/02-simulation-architecture.md` (game loop, state diagrams, saves) and `docs/06-game-design-balancing.md` (every formula, the payoff matrix, balance results).

### Added: the extras approved by the team
- **Main menu, pause and settings** (`UI/GameMenu.cs`):
  - The title screen slowly orbits the city. Options: Explore, Cinematic tour, Settings, Quit.
  - **Esc** pauses (freezes time and sound) and offers Resume, Settings, Main menu and Quit.
  - Settings cover volume, graphics tier, look, time of day, time-lapse, weather, flooding, festival, minimap and labels. They are remembered.
- **Live minimap** (`UI/MinimapView.cs`, **K** = off / small / large):
  - The base map is rasterised from the real city data: sea, river, buildings, roads by class, the Fleet Hub and chargers.
  - It shows the AT fleet, RapidCab, all traffic and signal states (large map only), festival and flood markers, and the camera arrow.
  - Click the map to fly there.
- **Festivals** (`Gameplay/FestivalSystem.cs`, **Y** cycles them):
  - **Ganpati procession:** a lit truck carrying a large Ganesha idol, dhol-tasha drummers, dancing devotees and pink gulal clouds. The road is closed and traffic queues behind the procession or turns back. A new dhol-tasha drum loop plays.
  - **Diwali:** coloured light strings on street-facing buildings, akash-kandil star lanterns, and fireworks over the city at night with a whistle-boom-crackle sound.
- **Monsoon waterlogging** (`Gameplay/FloodSystem.cs`):
  - Six low-lying stretches (along the Mithi River and around Kurla) fill with water during heavy monsoon rain and drain afterwards.
  - Vehicles slow to as little as 30 % and throw spray.
  - It can be turned off in Settings.
- **Traffic hooks:** a `SpeedFactor` per road position (used by flooding) and `RoadBlock`s (used by the procession; stuck vehicles U-turn after 6 s).

### Fixed
- **B012:** the Normal morning rush made a shift unwinnable even with perfect dispatch. Satisfaction penalties are softer, patience is longer, lateness is now measured against the time quoted at pickup, and the autopilot uses surge to ration demand. Balance tables are in `docs/06`.

### Verified
- `tools/verify/run_checks.sh`: **43/43** tests, 0 C# errors (both input systems plus the editor), 148/148 shader stages, **22/22** sounds.

## v0.8 — Polish: city sound, cinematic mode, pedestrians crossing at signals (2026-10-31)
### Added
- **City sound** (`Audio/ProceduralAudio.cs`, `Audio/CitySoundscape.cs`). 20 sounds are synthesised in code at start-up; no audio files are needed. Any clip can be replaced with a real recording in `Resources/TaxiSimAudio/`.
  - Beds: traffic (by camera height and rush hour), wind (by height), rain (by monsoon), crickets (night), crowd (near walkers).
  - Horns come from real nearby vehicles: car, double-tap, scooter, auto bulb horn, the truck "Horn OK Please" tune and the bus. Stuck drivers honk more; the autonomous fleet never honks.
  - Kurla and Bandra stations have crowds, a chime followed by a PA voice, and train horns. Moving trains rumble with wheel clacks. The AT Fleet Hub hums and its chargers chirp.
  - Thunder follows each lightning flash after a delay, crows call by day, and signals beep while pedestrians may cross.
  - **N** mutes. Preview WAVs are in `docs/audio-preview/`.
- **Cinematic mode, C** (`Camera/CinematicDirector.cs`). This replaces the old landmark tour with an automatic director:
  - 10 shot types: title aerial, landmark orbit, highway flyover, taxi tracking shot, chase cam, street dolly, crane-up, Fleet Hub push-in, signal junction, and a time-lapse
  - 2.39:1 letterbox, an opening title card, lower-third captions, a subject-focused depth of field, motion blur and grain
  - cuts every 6.5–10 s, alternating hard cuts and dips to black
  - J skips a shot; any movement key or Esc hands the camera back where it is
- **Pedestrians cross at signals:**
  - an all-red pedestrian walk phase (9 s, the walk lamp blinks at the end)
  - pedestrian lamps on every signal pole, and real zebra crossings and stop lines on every approach
  - walkers wait at the kerb and cross on "walk", and vehicles yield to anyone on a crossing
- `docs/14-sound-cinematic-video.md`: the sound list, how to use real recordings, the cinematic shots, and how to record the demo video with Unity Recorder.
- Verification step 4: every procedural sound is rendered outside Unity and checked for NaN, silence and clipping.

### Changed
- Vehicles now stop at a stop line before the zebra crossing instead of at the junction edge. The road shader's old fixed-position zebras were removed.

### Verified
- `tools/verify/run_checks.sh`: 17/17 tests, 0 C# errors (both input systems plus the editor), 148/148 shader stages, **20/20 sounds**.

## v0.7 — Street life, traffic signals, the AT Fleet Hub (2026-10-31)
What the team asked for after the v0.6 screenshots: NPCs, roadside stalls, traffic signals, more vehicles moving at irregular speeds, more realistic buildings, billboards, and a separate depot that the autonomous taxis leave from.

### Added
- **Traffic signals** (`TrafficSignals.cs`) at every junction where 3 or more roads meet.
  - Each approach has a pole on the left kerb with an overhanging arm and red, amber and green lamps.
  - Approaches get green one at a time: 11 s green, 2.5 s amber, 1 s all-red.
- **New driving model** (`TrafficAmbient.cs`, rewritten):
  - Each driver has their own aggression and a speed that wanders over time, so traffic flows unevenly.
  - Vehicles follow each other: they keep a gap and brake smoothly.
  - They stop at red lights, and at amber when they can still stop.
  - When stuck behind a slower vehicle, they change lane if the next lane has a gap.
  - Scooters weave inside their lane. Buses, trucks and autos keep to the left lane.
  - Vehicles wait at the end of a road when the next road is jammed.
- **More vehicles:**
  - New **scooters with riders** and colourful **goods trucks**.
  - Traffic counts raised to 60, 110, 170 and 240 per graphics tier.
  - The autonomous fleet now starts from the AT Fleet Hub.
- **Pedestrians (NPCs):**
  - New `TaxiSim/Character` shader that animates the walk cycle on the GPU.
  - `PedestrianFactory` makes 16 outfit variants: office workers, shirt and trousers, saree, salwar kurta, school kids and labourers.
  - `PedestrianSystem` puts 60 to 400 walkers on the footpaths. They keep clear of lamps and tree pits, turn back or carry on at the end of a block, and walk at their own pace.
  - Standing people wait at stalls and bus stops.
- **Roadside stalls** (`StreetLifeBuilder.cs`):
  - vada-pav and pani-puri carts with umbrellas
  - fruit and vegetable stalls under blue tarps
  - chai and paan kiosks with signs
  - bulbs under the canopies that light up at night
  - vendors and customers
- **Advertising:**
  - New `TaxiSim/Billboard` shader: procedural printed posters with flood-lamp lighting at night and a steel back.
  - Pole hoardings along the arterials, angled towards traffic, with catwalks and lamps.
  - Rooftop billboards on lattice frames, and large vinyl adverts on the side walls of mid-rise buildings.
  - **BEST bus shelters** with a glass back, bench, double-sided lit ad panel, stop sign and waiting commuters.
- **More realistic buildings:**
  - New shapes: L-shaped, U-courtyard, twin towers on a podium, and stepped "wedding cake" towers.
  - Twice as many facade colours (terracotta, mustard, teal, salmon, sky blue, brick, olive, and more).
  - **Real balconies on the street face** of shop-houses: concrete slabs with solid parapets or railings and laundry, or *chhajja* sunshades.
  - **Rooftop clutter:** black Sintex water tanks on stands, AC units and chillers, stair heads, dish antennas and parapet walls.
  - The CityTower shader also draws balcony rails, floor ledges and lit or unlit window clusters at night.
- **AT Fleet Hub**, a new graph node `fleet_hub` (zone `FleetDepot`, 12 chargers) linked to EV Depot B and the Diamond Bourse:
  - Sawtooth-roof warehouse with glowing north-light skylights, LED-framed roller-door bays and an LED eave strip.
  - New `TaxiSim/FleetHubFloor` shader: polished epoxy apron with painted bays, an animated chevron launch lane, pulsing charge rings (green when charging, amber when idle), a gate scan line, wet-look reflections, and a hex grid in the neon look.
  - 12 charging pillars with parked fleet taxis, a glass control office with an LED crown, floodlight masts and a perimeter fence.
  - A gate gantry with an "AT FLEET HUB" sign, boom barriers, and a holographic guide line to the junction.

### Fixed
- **B011:** distant buildings at night showed as flat white or grey blocks, and buildings looked grey-boxy by day. Windows now fade per axis, there are lit/unlit clusters at night, more palette variety, and real balconies, ledges and roof clutter.

### Verified
- `tools/verify/run_checks.sh`: **17/17** tests, 0 C# errors with both input systems plus the editor scripts, **148/148** shader stages (including Billboard, Character and FleetHubFloor).

## v0.6 — "Make it real": filled streets, shops, footpaths, realistic trees (2026-10-31)
Fixes everything visible in the team's day and night screenshots.

### Fixed
- **B007: buildings almost black by day.** The glass was set up like dark metal. It is now light, coated, reflective glass. As a fallback, sky colours (passed in as shader globals) are reflected even before the reflection probe renders.
- **B008: building walls too dark with a real texture.** The texture detail is now normalised by the texture's own average brightness, so each building keeps its colour.
- **B009: big empty "desert" ground.** The general ground now prefers the Concrete texture over the dirt-like Ground texture. More importantly, buildings now fill the city:
  - **Street frontage:** continuous rows of Mumbai shop-houses (shops below, 2–8 floors of flats above) along both sides of every street, and office blocks inside BKC. That's about 3,000 plots.
  - **Grid infill:** districts are filled on a dense jittered grid instead of a few random samples.
  - **6 new infill districts:** BKC Central, G-Block West/East, Bandra East Colony, Kalina East, BKC South-East.
- **B010: pink "V" shapes at night.** The neon look is weaker in Auto (45%, starting later at night), so night is now mostly warm windows, shops and street lights.

### Added
- **Shopfronts** on ground floors (CityTower shader): coloured signboards with "text", lit display windows and rolling shutters, glowing at night.
- **Footpaths:** 8 m wide with a raised kerb. Street lights are twice as dense and stand at the kerb.
- **Planted medians** on 3-lane roads: a raised kerb with hedges and coconut palms.
- **Realistic trees:**
  - new `TaxiSim/Foliage` shader with alpha-cut leaf cards, translucency, wind and leaf flutter, and dappled shadows
  - new `TreeFactory` with branching trunks; Mumbai species are rain tree, gulmohar (flame-red), columnar ashoka, neem, coconut palm, bushes, hedges and mangroves
  - about 1,700 avenue trees in tree pits along the footpaths, parks with bushes, and mangroves on both river banks

### Verified
- `tools/verify/run_checks.sh`: 16/16 tests, 0 C# errors, **108/108** shader stages (including Foliage).

## v0.5.1 (2026-10-31)
- Building walls now prefer a plain **concrete/plaster** texture over a *Facade* photo (which has its own windows that clash with the procedural ones). With the team's ambientCG set: roads = Road009C, sidewalks = PavingStones151, grass = Grass004, ground = Ground079L, walls = Concrete044C, rail bed = Rocks006.

## v0.5 — Realistic day + neon night, real textures, street camera (2026-10-30)
Based on the team's first Play-mode screenshot and their reference images (GTA V daytime waterfront and a cyberpunk wet street).

### Fixed
- **B005:** distant buildings looked like colourful "TV static". The windows, random lit rooms, interior parallax and holo-ads now blend smoothly into averages with distance (distance level of detail in the CityTower shader).
- **B006:** too many labels, glitchy ads and too much blue neon fog. The view now shows only the nearest 9 labels, and none in street view. Neon trims, ads and billboards are about 50–60% less frequent. Neon fog density is halved and the colour grading softened.
- Roads were hard to see: the asphalt is lighter.

### Added
- **Visual style V = Auto / Neon / Realistic.** Auto, the new default, gives realistic GTA-style daytime Mumbai, with the neon look fading in only after dark at 75% strength. Billboards, searchlights, the hologram and air-taxis only appear while neon is active.
- **Real-texture support (CC0, Poly Haven / ambientCG):**
  - Downloaded textures in `Assets/_Project/Resources/TaxiSimTextures/` are found automatically by file name and applied to roads, sidewalks, grass, ground, building facades, track bed and sand (albedo + normal + roughness)
  - An HDRI in `…/Sky/` becomes the daytime sky
  - `TaxiSimTextureImporter` sets the import settings
  - Guide: `docs/13-textures-guide.md`
- **Street camera (G):** a low chase camera behind a fleet taxi with a 58° lens, like a driving game. Press G again to go back to the city view.
- **Look and timing:** a more natural greenish Mithi/sea water colour, and the world now starts at 15:30 (afternoon sun).

### Verified
- `tools/verify/run_checks.sh`: 16/16 tests pass, all C# has 0 errors, 92/92 shader stages compile.

## v0.4.1 — Refinement & verification (2026-10-30)

### Fixed
- **B001:** the world started in full night instead of dusk. `StartHour` is now 17.6, golden hour.
- **B002:** air-taxis could target the floating hologram as a landing pad.

### Fixed (from the first Unity 6.6 open)
- Unity warning UAC1001 ("field skipped by serialization"): runtime-only fields are now marked `[System.NonSerialized]`. The first real compile in Unity 6.6 had **0 errors**.

### Verified
- **Simulation core:** 16/16 unit tests pass on .NET 8.
- **Unity C# scripts:** all of them type-check with 0 errors and 0 warnings against real UnityEngine/UnityEditor reference assemblies, in both input modes.
- **Shaders:** all 9 compile in 92/92 pass/stage/variant combinations against Unity 6's URP shader library (glslang).

### Added
- `tools/verify/run_checks.sh` reruns all the checks above without Unity.

## v0.4 — "Neon Mumbai 2077" graphics upgrade (2026-10-30)

### Added
- **Visual style switch (V):** *Neon Mumbai 2077*, the new default, or *Realistic Mumbai*.
- **Neon tubes and holographic adverts on buildings** (CityTower shader + uv3 data):
  - corner and roofline neon in 5 colours, some with flicker
  - animated holo-ads with fake text rows, rings, bars, scanlines and glitch bursts
  - landmark neon: amber ATLAS tower, cyan Equinox towers, NSE market-ticker hologram, magenta Phoenix Marketcity
- **New shaders:**
  - `TaxiSim/HoloAd`: rooftop billboards and vertical blade signs with an RGB split
  - `TaxiSim/VolumeBeam`: volumetric light cones under every street lamp, rooftop searchlights and a hologram projector beam, all with soft depth fade
- **Atmosphere:**
  - height fog with drifting smog and magenta/cyan neon scattering near street level (applied in every TaxiSim lit shader)
  - city light pollution lighting the clouds and horizon
  - streets that stay glossy-wet at night in the neon style
- **NeonBuilder:**
  - rooftop billboards, blade signs, and neon shop strips in Kurla, Dharavi and residential areas
  - 5 sweeping searchlights
  - a giant rotating hologram of the AT fleet taxi above Jio World Centre
- **Flying autonomous eVTOL air-taxis** (`AirTraffic`, new `VehicleKind.AirTaxi`): rooftop-to-landmark flights with lift-off, cruise, banking and landing.
- **Post-processing:** teal/magenta split toning, procedural lens dirt on bloom, stronger night bloom, chromatic aberration and vignette, camera motion blur (Ultra and above), and screen-space lens flares when the URP version supports them.
- **Rendering:**
  - Cinematic tier (F5) now uses Temporal AA at 1.5× render scale
  - box-projected local reflection probes over G-Block, the Equinox/LBS corridor and Kurla (Ultra and above)
  - box projection enabled in all lit shaders
- The world now starts at 17:36 (golden hour), so it turns to night in about a minute.

### Honest note
This brings the *look* closer to Cyberpunk-style night cities. Matching Cyberpunk 2077's actual fidelity (hand-made assets, path tracing, HDRP) is outside the scope of a procedural student project on a laptop. See `docs/11-3d-world-bkc.md` §8.

### Still not verified
- Unity compile and Play mode, and `dotnet test`.

## v0.3 — BKC → Kurla corridor (2026-10-29)

### Added
- **3 new locations:** Equinox Business Park / ATLAS SkillTech University (node 19, Education), Phoenix Marketcity (node 20), Kohinoor City / Kurla West (node 21). The map now has 22 locations and 36 roads.
- **New roads:** BKC–Kurla Connector Road, LBS Marg (North and South), Kurla Station Road (West), Kurla West Station Approach, Kurla Depot Link, Phoenix Marketcity Road. The direct US Consulate → Kurla road was replaced by the connector via Equinox.
- **6 new districts** filling the corridor: BKC–Kurla Connector, Equinox / LBS Business Strip, Kurla West (Kapadia Nagar), Kurla West South, Kurla Station Market (dense), Phoenix / LBS North. That adds about 220 buildings.
- **New landmarks:**
  - the four Equinox towers on a podium, with ATLAS SkillTech University in Tower 1 (LED fin, campus lawn, entrance canopy)
  - Phoenix Marketcity (LED facade, glass atrium, car park)
  - Kohinoor City (three towers and a mall)
- **Metro Line 2B** now runs via Equinox to Kurla.
- **Test:** `Corridor_BkcToKurla_RunsThroughEquinoxAtlas`. The count test was updated to 22 / 36.

### Still not verified
- Unity compile and `dotnet test` (no Unity Editor or .NET SDK in the build environment).

## v0.2 — 3D BKC world (2026-10-29)

### Added
- **Simulation core** (`Scripts/Core`, pure C#):
  - `Vec2`/`Vec3`, the deterministic `Pcg32` random number generator, and `CityGraph`
  - `CityGeography`: Mithi River channel, Arabian Sea coast, rail lines, metro, Sea Link, 14 districts
  - `BkcMap`: 19 locations, 30 roads, 4 elevated, bridges detected automatically
  - `RouteFinder`: Dijkstra with traffic and closures, plus failure reasons
- **Shaders** (9 custom URP HLSL): CityTower (interior mapping, night windows, LED crowns), Road (Indian markings, puddles, traffic heat, closures), Water (depth colour, foam, Fresnel, ripples), Surface, PBR (wind, night emission), Sky (stars, moon, clouds, haze), FlowLine, Holo, Particle.
- **World generation:**
  - terrain LOD rings with the carved river and sea
  - roads with junctions, bridges, flyovers on pillars, barriers, sidewalks, LED/sodium lamps
  - about 800 procedural buildings
  - 14 BKC landmarks with holographic beacons
  - trees, gulmohar, palms, mangroves
- **Transit:** Western and Central locals, the Metro 2B viaduct with stations and a train, the Bandra–Worli Sea Link with neon cables and traffic, the airport runway with a landing aircraft.
- **Live systems:**
  - day/night cycle (Mumbai sun path)
  - weather: clear, haze or monsoon, with rain and lightning
  - post-FX (bloom, ACES tonemapping, grading, tilt-shift)
  - real street lights
  - left-hand ambient traffic: kaali-peeli taxis, autos, BEST buses, 6 fleet EVs, 4 RapidCab rivals
  - rush-hour traffic multipliers and the heat overlay
  - route and road-closure demo
- **Camera:** x/y/z pan, zoom and orbit; follow a taxi; fly to landmarks; cinematic tour.
- **Graphics tiers** F1–F5, screenshots (F9), and an IMGUI overlay with labels and help.
- **Editor menu** *Taxi Manager → Setup BKC 3D World (Ultra)*.
- **Tests:** xUnit tests for the map, terrain, random number generator and routing (T07/T08).
- **Docs:** README, the 3D world design doc, the updated handoff plan, the report draft, and the city graph image.

### Not yet verified
- Unity compile and Play mode, because there was no Unity Editor in the build environment.
- `dotnet test`, because there was no .NET SDK in the build environment.

## v0.1 — Plan (2026-10-29)
- Read the spec and chose Unity 6 + ESP32 over USB serial, full game theory, and docs plus a report draft.
- Wrote the master plan and handoff.
