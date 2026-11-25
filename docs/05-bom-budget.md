# 05 — Bill of materials and budget (D5)

**Budget band (spec §7):** ₹12,000–₹16,000. That is a *ceiling*, not a target.

**Prices below** are indicative Indian online prices (Nov 2026) for the exact parts in our photos. **Replace each with the receipt amount** and keep the receipts for the viva.

## 1. Console electronics (bought)
| # | Item | Exact part (from our photos) | Qty | Unit ₹ (est.) | Total ₹ (est.) | Why this part |
|---|---|---|---|---|---|---|
| 1 | Microcontroller | ESP32 DevKit V1, 30-pin, CP2102 USB-C | 1 | 450 | 450 | Mandatory platform; enough GPIO for 7 buttons + encoder + SPI TFT + buzzer + LEDs; USB serial to the PC |
| 2 | Display | 2.8" SPI TFT 240×320, ILI9341, KMRTM28028-SPI (touch + SD) | 1 | 750 | 750 | Spec 2.4–2.8"; large enough for 8 text rows; 5 V input via its own regulator |
| 3 | Push buttons | 6 × 6 mm 4-leg tactile switches (8 bought, 1 broke) | 8 | 5 | 40 | Accept/reject/navigation; 7 used |
| 4 | Rotary encoder | KY-040 module with push switch | 1 | 90 | 90 | Taxi / price / place selection, zoom; built-in pull-ups |
| 5 | Status LEDs | WS2812B-8 RGB stick | 1 | 120 | 120 | One data pin for 8 colours (fleet status); not fitted yet (needs soldering) |
| 6 | Buzzer | 12 mm buzzer, 5 V | 1 | 20 | 20 | Request, urgent, event, completed and error cues |
| 7 | Transistors | 2N2222 NPN (TO-92) | 2 | 5 | 10 | Drives the buzzer from GPIO21 (1 spare) |
| 8 | Resistors | 1 kΩ, 10 kΩ, 330 Ω assortments | 1 set | 50 | 50 | Transistor base (1 kΩ), LED data line (330 Ω), spare pull-ups |
| 9 | Level shifter | MC74HC125AN | 1 | 40 | 40 | Optional LED data buffer (fallback) |
| 10 | Breadboard | 830-point | 1 | 120 | 120 | Prototype board |
| 11 | Jumpers (first set) | 10-wire M-F ribbon + mixed loose | 1 set | 150 | 150 | Initial wiring |
| 12 | Jumpers (second set) | 40 M-F + 40 F-F (+ M-M) | 1 set | 200 | 200 | Needed once the ESP32 had to sit beside the breadboard (bug B018) |
| 13 | USB-C data cable | — | 1 | 150 | 150 | Power + programming + game link |
| | **Electronics subtotal** | | | | **≈ ₹2,190** | |

## 2. Enclosure and finishing (planned, docs/10)
| # | Item | Spec | Qty | Total ₹ (est.) |
|---|---|---|---|---|
| 14 | Top panel | 3 mm acrylic or MDF, laser-cut 240 × 140 mm | 1 | 400–700 |
| 15 | Sides and base | MDF strips 65 mm / 3D-printed tray | 1 set | 200–600 |
| 16 | Fasteners | M3 × 25 standoffs, screws, nuts | 1 set | 150 |
| 17 | Button caps / panel switches | 12 mm caps or 12 mm panel push buttons | 7 | 100–250 |
| 18 | Knob | 25 mm encoder knob | 1 | 40 |
| 19 | Misc | header pins, labels, solder, heat-shrink, cable ties | 1 set | 200 |
| | **Enclosure subtotal** | | | **≈ ₹1,100–1,950** |

## 3. Software and tools (no cost)
| Item | Cost | Note |
|---|---|---|
| Unity 6 (Personal), Visual Studio Community, Arduino IDE 2, Wokwi, Git | ₹0 | free licences |
| Silicon Labs CP210x driver | ₹0 | |
| Lab PC / laptop, soldering iron | ₹0 | shared institutional tools (spec §7) |
| *Optional:* digital multimeter | 350 | strongly recommended for fault-finding |

## 4. Totals
| Part | ₹ (est.) |
|---|---|
| Electronics | ≈ 2,190 |
| Enclosure and finishing | ≈ 1,100–1,950 |
| Optional multimeter | 350 |
| **Total** | **≈ ₹3,300–4,500** |

**Result:** about **25–35 % of the ₹12,000 lower bound**. The spend is low because:
- one ESP32 dev board does all the I/O;
- the 3D city runs on the lab laptop, so no extra display is needed;
- all art and sound are procedural, so there are no asset licences;
- every tool is free.

**Not bought, and why:**
- **Vibration motor** (spec item 8, optional): the buzzer and LEDs already cover urgent alerts.
- **Separate 5 V supply** (spec item 11): the laptop's USB port supplies a regulated 5 V. The measured draw is about 300–400 mA without LEDs, plus about 120 mA for the LED stick at 25 % brightness.

## 5. Mistakes that cost money or time (lessons)
- **One button broke** during placement. The design was changed to 7 buttons by merging INFO into LEFT/RIGHT, so nothing was re-bought.
- **The 30-pin ESP32 is 1 inch wide**, so on one breadboard it leaves only one row of free holes. We had to buy more female jumpers (row 12 above). With hindsight, buy a second breadboard or an ESP32 expansion board from the start.
- **The LED stick came without header pins.** Budget for a soldering session.
