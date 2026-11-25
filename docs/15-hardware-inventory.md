# 15 — Hardware inventory (from the team's photos)

**Status:** complete (photo sets 1 and 2, 2026-11-10). The final parts table, wire budget and checked wiring are in **`docs/03-hardware.md`**. The layout is in `docs/images/breadboard-layout.png`, and the Wokwi files for the 30-pin board are in `firmware/wokwi/`. Set 2 confirmed the 30-pin ESP32 (USB-C), the TFT pinout, the KY-040, 2 TO-92 transistors, a 12 mm buzzer, an **MC74HC125AN** and a third resistor bundle (probably 330 Ω). No WS2812B LEDs were in sets 1–2.

**Update (photo set 3, 2026-11-10):** a **WS2812B-8 LED stick** (input pads GND · VCC · IN · GND, output pads GND · VCC · OUT · GND, no header yet) is now wired in on GPIO22 through the 330 Ω. **One push button broke, so 7 buttons remain**; INFO was removed and LEFT/RIGHT now change the screen page.

Photos: `docs/hardware-photos/`.

## Photo set 1

| # | Photo | Part | What I can see | Notes for the design |
|---|---|---|---|---|
| 1 | `01-tft-back.jpg` | **2.8" TFT SPI 240×320, KMRTM28028-SPI** (red board), with a stylus | 14-pin header: VCC, GND, CS, RESET, D/C, SDI(MOSI), SCK, LED, SDO(MISO), T_CLK, T_CS, T_DIN, T_OUT, T_IRQ. 4-pin SD header (SD_CS, SD_MOSI, SD_MISO, SD_SCK). Regulator U1 with jumper J1. Backlight transistor Q1. Touch controller U2 (XPT2046-type). | **It has a resistive touchscreen.** That's optional extra input; the T_ pins share the SPI bus and need one more CS pin. The display pins match the plan. J1 open means VCC can be 5 V through the onboard regulator. **Final plan: VCC on 5 V, J1 left open** (see docs/03). The SD card slot is not used. |
| 2 | `02-ribbon-jumpers.jpg` | 10-wire rainbow ribbon jumpers | One end male pins, the other end female | Good for TFT → breadboard (TFT header pins are male, so the female end goes on the display) |
| 3 | `03-mf-jumpers.jpg` | 3 jumpers (white, black, brown), male–female | | |
| 4 | `04-loose-jumpers.jpg` | About 25 loose jumpers, mixed male–male, male–female, female–female | | Enough for buttons, encoder, LEDs, buzzer |
| 5 | `05-breadboard-buttons-esp32-resistors.jpg` | Full-size breadboard (830-point) | 8 red tactile push buttons already placed | **Check the button orientation** (see below) |
| 5 | same | **ESP32 board "ESP-32", 30-pin DOIT DevKit V1** | Bottom row labels: D23, D22, TX0, RX0, D21, D19, D18, D5, TX2, RX2, D4, D2, D15 | ⚠️ **This is the 30-pin board, not the 38-pin board** the current diagram shows. All the GPIOs we use exist, but the physical pin positions differ. GPIO16 = **RX2**, GPIO17 = **TX2**, and 5 V = **VIN**. The diagram and Wokwi file will be redrawn for the 30-pin layout. |
| 5 | same | Resistors, taped and labelled **10k** (about 5) and **1k** (about 6–7) | | 1 kΩ is right for the 2N2222 base. **No 330 Ω seen yet** for the LED data line. Alternatives: two 1 kΩ in parallel (500 Ω), or 1 kΩ with a short data wire. 10 kΩ is spare (extra pull-ups if needed). |
| 5 | same | Small 3-leg black part (TO-92) | | Probably the **2N2222** transistor. Confirm the marking on its flat face. |

## Early observations (to confirm with the next photos)
- **Buttons:** 4-leg tactile buttons connect their legs in pairs. In the photo they sit within one half of the breadboard, not across the centre gap. Each button must be placed so that its **two switched sides land in different breadboard rows**. Otherwise it reads as "always pressed" or "never pressed". I'll mark the exact placement on the updated diagram.
- **ESP32 on a breadboard:** the 30-pin DOIT board is wide. On a single breadboard it leaves only one free row on one side. Common fix: straddle the centre gap and use female jumpers on the other side, or use two breadboards side by side.

## Still to confirm
The 2N2222 marking (E-B-C or C-B-E) · the third resistor bundle's value (330 Ω?) · whether the buzzer is active or passive · whether the USB-C cable carries data.
