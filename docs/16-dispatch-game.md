# 16 — Playing the dispatch game (v0.11)

You run the **AT autonomous taxi fleet** in BKC → Kurla for one shift, **06:00 to 22:00**. You have six electric taxis based at the AT Fleet Hub, and the rival **RapidCab** competes with you on every ride.

The aim is to:
- earn money;
- keep Mumbai happy;
- keep the batteries out of the red;
- finish the shift without going bankrupt or losing your licence.

Every decision goes through the simulation, which is deterministic. Each shift is saved as a replayable log of your commands.

## 1. Starting a shift
1. In Unity, press **▶ Play**. The title screen appears.
2. Pick **Easy / Normal / Hard**, then click **START DISPATCH SHIFT**. You can also press **ACCEPT** on the console, or **Enter** on the keyboard.
   - Without the console plugged in, the game runs on the keyboard alone.
3. The camera flies to the AT Fleet Hub and the clock starts at 06:00.

**Speed:** at 1×, one simulated minute takes 3 real seconds, so a full shift lasts 48 minutes. Use 2×, 4× or 8× to go faster, or pause.

**Ending early:** press **Esc → Main menu**. That ends the shift and returns you to free-roam.

**The USB console** is found automatically. Unity tries each COM port and keeps the one that answers like the console. When it connects, the HUD shows "console on COM8" (or another port number), and the ESP32 screen switches from its self-test to the game. If you unplug it, the game carries on with the keyboard, and the console shows **LINK LOST**.

## 2. What you see
| Where | What |
|---|---|
| **Top bar** | Clock and speed, cash, satisfaction bar, your price vs RapidCab's, console status |
| **Left panel** | The console's current page, with tabs DISPATCH · FLEET · MONEY · EVENTS (the TFT shows the same page) |
| **Right panel** | The six taxis: state colour, where they're going, battery bar, LOW warning |
| **Bottom** | Event ticker (requests, money, warnings, city events) |
| **In the city** | Glowing **pins** for waiting passengers (green normal, gold priority, red medical) with fare and minutes left. A label above each AT taxi. The **cyan ribbon** is the selected trip; the **orange ribbon** is the chosen taxi's way to the pickup. Red-and-white **barriers** mark closed roads. |

**Taxi colours** (HUD, TFT and LED stick):

| Colour | Meaning |
|---|---|
| Green | idle |
| Yellow | going to a pickup |
| Blue | passenger on board |
| Magenta | charging |
| Orange | delayed or low battery |
| Red | battery empty (being towed) |

## 3. Controls
| Console | Keyboard | What it does |
|---|---|---|
| **LEFT / RIGHT** | ← / → | Change page (Dispatch · Fleet · Money · Events) |
| hold LEFT / RIGHT | − / = | Slower / faster (1× 2× 4× 8×) |
| **UP / DOWN** | ↑ / ↓ | Select a request / a taxi / scroll |
| **Knob turn** | , / . | Dispatch: choose another taxi · Fleet: choose where to move it · Money: choose a price |
| **Knob press** | / | Show the selection in the city (fly to the pickup / follow the taxi) |
| hold **Knob** | — | Camera mode: arrows move, knob zooms, ACCEPT / REJECT turn, knob press follows a taxi, BACK returns |
| **ACCEPT** | Enter | Send the taxi · Fleet: charge / reroute / release the charger · Money: apply the price · Events: reroute all blocked taxis |
| **REJECT** | Backspace | Decline the request (costs a little satisfaction) · Fleet: move the taxi to the chosen place |
| **BACK** | Delete | Back to the suggested taxi / undo a price you haven't applied |
| hold **BACK** | P | Pause / continue |
| — | WASD, mouse, wheel | Camera (always available) |
| — | Esc | Pause menu |

The mouse also works on the HUD: click a request, a taxi, a page tab or an alternative taxi. The complete key list (camera, city, look, screen) is in docs/08 §5 and the report's Appendix C.

During a shift the arrow keys belong to the dispatch controls (move the camera with WASD), and P / O belong to the game. The sky follows the shift clock, so T, [ ], 1–4 and the Settings time slider do nothing until you click **Sky follows the shift clock – click to set it freely** in Settings (this changes the look only; the shift keeps its own clock).

## 4. The pages
- **DISPATCH:** waiting passengers, most urgent first. For the selected one, the game suggests the taxi with the highest **utility**:

  > U = fare×surge − energy cost − 8 × ETA × urgency − 150 × P(passenger gives up) + 15 × satisfaction effect

  The HUD shows every term, so you can see why that taxi is suggested. Turn the knob to pick another taxi, then press ACCEPT. A taxi can only take a trip if its battery will still have 10% left at the end.
- **FLEET:** the selected taxi's job, battery and earnings.
  - **ACCEPT** does the sensible thing for its state: charge, reroute (after a road closure) or release the charger.
  - **REJECT** sends an idle taxi to the busy spot chosen with the knob (Kurla Station, Jio World, Bandra Station, …).
- **MONEY:** cash, revenue, costs and fines, satisfaction, lost riders, and the **price war**.
  - Turn the knob to a new surge price and press ACCEPT. A higher price earns more per trip, but more riders refuse or choose RapidCab, and satisfaction drops above ×1.75.
  - RapidCab re-prices every sim-hour as a best response to yours.
  - The HUD shows your expected ₹ per request for each price against RapidCab's (best reply in green) and the Nash equilibrium.
- **EVENTS:** active city events (closures, heavy traffic, monsoon rain, festival crowds at MMRDA, charger faults, demand surges, vehicle faults) and the full log. ACCEPT reroutes every taxi stopped by a closure.

## 5. Winning
The shift ends at 22:00, or earlier if:
- cash falls below **−₹2,000** (bankrupt), or
- satisfaction falls below **20%** (licence cancelled).

**Score** = 50% profit (₹25,000 = full marks) + 30% satisfaction + 20% fleet efficiency, shown as 1–5 stars. The end card also shows the seed and how many decisions were logged.

**Tips:**
- Dispatch medical requests first. They pay ×1.5, but their patience is short.
- Charge taxis below about 30% when it's quiet. An empty battery costs a ₹500 tow and a stranded passenger.
- Before the rush (09:00 and 18:30), move idle taxis to the rail stations.
- Don't over-price. RapidCab will undercut you and riders will leave.

**Balance check** (`tools`, 6 seeds per row). The test player presses ACCEPT on the suggested taxi every 2–9 real seconds at 1×, charges low taxis, and never repositions or re-prices:

| Difficulty | Shifts completed | Stars |
|---|---|---|
| Easy | 6/6 | 3.2–3.5 |
| Normal | 4–5/6 | 2.3–3.0 |
| Hard | 0–4/6 | — |

Hard needs the Fleet and Money pages.

## 6. How the pieces fit
```
ESP32 console ──USB 115200──► SerialLink (background thread, auto COM-port search)
  B / E / K lines                    │
                                     ▼
Keyboard / mouse ───────────► DispatchConsole (engine-free, unit-tested) ──Submit──► Simulation (deterministic core)
                                     │  pages, cues, toasts, camera requests             │
            ┌────────────────────────┘                                                    │ ticks, taxis, requests, events
            ▼                                                                             ▼
   ConsoleBridge ──S/T/R/F/M/Z/L lines──► ESP32 TFT, buzzer, LEDs            SimulationDriver ──► 3D city: fleet taxis,
   DispatchHud (laptop screen)                                                pins, ribbons, closures, rain, sky clock
```
- **Protocol:** see docs/03-hardware.md §9 and `Core/Dispatch/ConsoleProtocol.cs`.
- **Checks:**
  - 69 unit tests cover the protocol and the console logic;
  - the console firmware is run on the PC against the game's real line stream (`tools/verify/run_checks.sh` step 5);
  - the USB link code is type-checked with System.IO.Ports.

## 7. Troubleshooting
| Problem | Fix |
|---|---|
| HUD says "serial off (run Taxi Manager → Setup)" | Run **Taxi Manager → Setup BKC 3D World** once. It sets **Api Compatibility Level = .NET Framework**, which USB serial needs. Unity recompiles, then press Play again. |
| "looking for the console…" forever | Close **Arduino IDE's Serial Monitor**: only one program can use COM8 at a time. Then check the cable and that Device Manager shows the CP210x port. |
| TFT stays on the self-test screen | The game isn't sending yet. Start a shift, or check the status in the top bar. |
| TFT shows LINK LOST | Unity was stopped, or the Serial Monitor took the port. Press Play / close the monitor. It recovers by itself. |
| A button does nothing in the game | Check it on the self-test screen first (unplug and replug the USB to restart the ESP32). |
| The knob counts the wrong way | Swap the CLK and DT wires. |
