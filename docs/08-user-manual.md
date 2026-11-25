# 08 — User manual

*Autonomous Taxi Manager: run Mumbai's robot taxi fleet for one day.*

## 1. What you need
- A Windows laptop with the project open in Unity 6 (installation: `docs/12-windows-setup-guide.md`).
- The **dispatch console** (ESP32 with screen, 7 buttons, a knob and a buzzer) on a **USB-C data cable**. Without it you can play on the keyboard.

## 2. Start
1. Plug in the console. Its screen shows **SELF-TEST**. Press each button and turn the knob if you want to check it.
2. Make sure **Arduino IDE is closed**: only one program can use the console's USB port.
3. In Unity press **▶ Play**. The console screen changes to **TAXI MANAGER** within a few seconds.
4. Choose **Easy, Normal or Hard** with UP/DOWN (or click on the screen), then press **ACCEPT** (or click **START DISPATCH SHIFT**).

## 3. Your job
You have **6 autonomous electric taxis** (AT-01 … AT-06) at the AT Fleet Hub. From **06:00 to 22:00**:
- **send taxis to waiting passengers** (glowing pins: green normal, gold priority, red medical);
- **keep the batteries charged**: a taxi needs enough charge for the whole trip plus 10 %;
- **react to city events** such as road closures, jams, monsoon rain, festival crowds and charger faults;
- **set your price** against the rival **RapidCab** (orange cars).

**You lose** if cash drops below −₹2,000 or satisfaction below 20 %. **Your score:** profit 50 % + satisfaction 30 % + how busy the taxis were 20 %, shown as 1–5 stars.

## 4. The console
| Control | Short press | Hold (0.6 s) |
|---|---|---|
| **UP / DOWN** | choose a passenger / taxi / scroll | — |
| **LEFT / RIGHT** | previous / next page | slower / faster game (1× 2× 4× 8×) |
| **ACCEPT** | send the taxi · charge / reroute / release · apply the price | — |
| **REJECT** | turn the passenger down · send the taxi to the chosen place | — |
| **BACK** | go back to the suggested taxi / undo | pause / continue |
| **KNOB turn** | choose another taxi · choose a place · choose a price | — |
| **KNOB press** | show it in the city (fly or follow) | camera mode (arrows move, knob zooms, BACK returns) |

**Pages:**
1. **DISPATCH:** waiting passengers, most urgent first. Below them, the suggested taxi with its ETA and battery before → after. ACCEPT sends it.
2. **FLEET:** your six taxis. ACCEPT does the right thing for the selected taxi (charge, reroute or release). REJECT sends it to the busy place shown (turn the knob to change it).
3. **MONEY & PRICE:** cash, income, costs and fines; your price vs RapidCab's. Turn the knob to a new price and press ACCEPT.
4. **CITY EVENTS:** what's happening, plus the log. ACCEPT reroutes every taxi stuck behind a closure.

**Sounds:**
| Sound | Meaning |
|---|---|
| short high double beep | new passenger |
| fast triple beep | medical |
| falling two-tone | city event |
| two low beeps | warning |
| rising chirp | OK |
| low buzz | refused |
| "ka-ching" | trip paid |

## 4·. No knob since console v0.7: click the right stick (NEXT) instead
| Old knob | Now |
|---|---|
| Turn | **Click the right stick**: next taxi (Dispatch), next spot (Fleet), next price (Money), scroll (Events), or difficulty before a shift |
| Press | **Hold the right-stick click**: show the selection in the 3D city |
| Hold (camera mode) | No longer needed: the joysticks always move the camera |

The keyboard still works: `,` `.` act like the old knob turn, and `/` like its press.

## 4a. INFO button (console v0.6 and later)
- **Short press:** a help line for the current page appears on the console screen and the PC screen.
- **Long press:** the city camera flies back to the home view (G-Block, BKC).
- On the keyboard, **I** does the short press.

## 4b. Joysticks (console v0.5 and later)
| Stick | Push | Camera |
|---|---|---|
| Left | up / down / left / right | moves over the city map |
| Right | left / right | turns the view |
| Right | up / down | zooms in / out |

- The sticks work in free-roam and during a shift, but not while a menu is open.
- Leave both sticks untouched while the console starts, because it measures their centre then. If the camera creeps on its own, type `cal` in the Serial Monitor.

## 5. Keyboard and mouse (every key)
You can click rows, tabs and taxis on the screen with the mouse.

| Key | What it does |
|---|---|
| **Dispatch (during a shift; same as the console)** | |
| ↑ / ↓ | UP / DOWN: select a request or taxi, scroll |
| ← / → | LEFT / RIGHT: previous / next page (Dispatch · Fleet · Money · Events) |
| Enter | ACCEPT: send the taxi · charge / reroute / release · apply the price; also starts a shift |
| Backspace | REJECT: decline the request · send the taxi to the chosen place |
| Delete | BACK: back to the suggested taxi / undo the price |
| , and . | knob turn: choose another taxi / place / price |
| / | knob press: show the selection in the city |
| P | hold BACK: pause / continue the shift |
| − and = | hold LEFT / RIGHT: slower / faster (1× 2× 4× 8×) |
| **Camera (always)** | |
| W A S D | move the camera (arrow keys too, when no shift is running) |
| Q / E | turn left / right |
| R / F | tilt up / down |
| Z / X, mouse wheel | zoom in / out |
| right / middle mouse drag | turn and tilt / move |
| Tab / Shift+Tab | fly to the next / previous landmark |
| Space | follow the next fleet taxi |
| H | home view |
| G | street-level chase camera |
| C | cinematic tour on / off |
| J | next cinematic shot |
| Esc | stop following / leave the tour, then the pause menu |
| **City and look** | |
| T | time-lapse on / off |
| [ / ] | time of day −/+ 30 min |
| 1 / 2 / 3 / 4 | morning / noon / evening / night |
| M | weather: Clear → Haze → Monsoon |
| Y | festival: None → Ganpati → Diwali |
| V | look: Auto → Neon 2077 → Realistic |
| B | miniature (tilt-shift) effect |
| O | traffic heat overlay (free-roam only) |
| P / Shift+P / Shift+O | route demo / close a road on it / reopen all roads (free-roam only) |
| **Screen and settings** | |
| I | controls help (free-roam) |
| L | place labels on / off |
| U | hide / show the whole interface |
| K | minimap: Small → Large → Off |
| N | sound mute / unmute |
| F1 – F5 | graphics: Low · Medium · High · Ultra · Cinematic |
| F9 | save a screenshot |

During a shift the arrow keys belong to the dispatch controls (move the camera with WASD), and P / O belong to the game. The sky follows the shift clock, so T, [ ], 1–4 and the Settings time slider do nothing until you click **Sky follows the shift clock – click to set it freely** in Settings (this changes the look only; the shift keeps its own clock).

## 6. Tips
- Medical riders first: they pay 1.5×, but wait only 7–10 minutes.
- Charge below about 30 % when the queue is empty. An empty battery costs ₹500 for a tow.
- Before 09:00 and 18:30, park idle taxis at Bandra or Kurla station (Fleet page, REJECT).
- A higher price earns more per ride, but riders refuse or pick RapidCab. Above ×1.75, satisfaction drops.
- The suggested taxi has the best **utility**. The laptop panel shows the sum: fare, minus energy, time and risk, plus satisfaction.

## 7. Problems
| Problem | Fix |
|---|---|
| Console stays on SELF-TEST | Close Arduino IDE; check the top bar says "console on COM…"; run **Taxi Manager → Setup** once if it says "serial off". |
| LINK LOST on the console | Unity stopped or another program took the port; it reconnects by itself. |
| A button does nothing | Check it on the self-test screen (replug USB). If it shows as always pressed, turn it 90° on the breadboard. |
| Game too fast | Hold LEFT (or press −) to slow down; hold BACK (or P) to pause. |
| Settings > Time of day doesn't move during a shift | The sky follows the shift clock: click **Sky follows the shift clock – click to set it freely** first (fixed in v0.12.1). |
