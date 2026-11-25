#include <Arduino.h>
// =====================================================================================
//  Autonomous Taxi Manager — ESP32 Dispatch Console firmware  (v0.7: 8 panel buttons + 2 joysticks, no knob)
//  Real console: ESP32 DevKit V1 30-pin, 2.8" ILI9341 (KMRTM28028-SPI), 8 panel push buttons,
//  buzzer via 2N2222, two HW-504 / KY-023 joysticks (powered from 3V3!), right-stick click = NEXT.
//  Wiring: docs/03-hardware.md and docs/images/breadboard-layout.png (v0.15).
//
//  Two modes:
//   * SELF-TEST (at power-on): every button lights a box and beeps, the sticks move their dots,
//     "ALL INPUTS OK" when everything was used. Works without the PC game.
//   * CONSOLE (as soon as the game sends a screen line): the TFT shows the game's pages (header,
//     title, 8 rows, footer, toasts), the buzzer plays the game's cues, the LED stick shows the
//     fleet. "LINK LOST" if the game goes quiet for 4 s.
//  Buttons and sticks are reported the same way in both modes.
//
//  USB serial protocol, 115200 baud (docs/03-hardware.md §9, Core/Dispatch/ConsoleProtocol.cs):
//   ESP32 -> PC:  H,TAXI-CONSOLE,<ver>   B,<BUTTON>,P|L|R   K (every second)
//                 J,lx,ly,rx,ry  joysticks -100..100 (20x a second while a stick is off-centre, then 0,0,0,0)
//   PC -> ESP32:  S|clock|cash|sat|surge|speed   T|page|pages|title   R|row|style|text   F|text
//                 M|colour|text   Z|cue   L|rrggbb x8        style = colour letter (+ '*' selected, '2' large)
//
//  Libraries: Adafruit GFX Library + Adafruit ILI9341 (+ Adafruit NeoPixel only when HAS_LEDS is 1).
//  Pin map = docs/03-hardware.md (GPIO12 avoided; buzzer moved off GPIO23 which is SPI MOSI).
//
//  v0.7: the KY-040 knob is removed (no E lines any more). The right joystick's click (SW pin) on D4 is the
//   NEXT button: short = what one knob step did (next taxi / spot / price / scroll), long = show in the city.
//   D34 and D35 (the old knob CLK / DT) are now free.
//
//  v0.6: 12 mm panel push buttons (2 lugs each: one lug to the ESP32 pin, the other to GND).
//   INFO is back as the 8th button on D19 (short press: help line, long press: camera home view).
//   D19 is the screen's unused MISO pin, so the SPI bus is started without MISO (SPI.begin(18, -1, 23)).
//   All other pins are the same as v0.5.
//
//  v0.5: two joysticks. Left stick moves over the city map, right stick turns (left/right) and zooms (up/down).
//   Wiring changes from v0.4.1: UP button D32 -> D15, DOWN button D33 -> D22 (the LED stick is not fitted),
//   left stick VRx D32, VRy D33, right stick VRx VP (GPIO36), VRy VN (GPIO39), both sticks +5V pin -> 3V3 rail.
//   Leave the sticks untouched while the ESP32 starts: it measures their centre then (or type cal).
//
//  v0.4.1 (review): same pins and protocol as v0.4, so no rewiring.
//   * Sound engine rewritten: every sound is non-blocking and the buzzer pin is driven LOW when quiet.
//     Set BUZZER_ACTIVE to 1 if your buzzer beeps on its own when connected straight to 5V.
//   * Serial Monitor test commands (type them, then press Enter): beep, pins, cal, help.
//   * Only real game lines (S T R F M Z L) switch the screen to console mode.
//   * Screen SPI clock 27 MHz (safer over 15-20 cm jumper wires than the library's 40 MHz).
// =====================================================================================
// ---- What is connected right now? Set to 0 for a part you haven't wired yet, 1 once it is wired. ----
#define HAS_LEDS    0   // WS2812B-8 LED stick: must stay 0 now (its pin D22 is the DOWN button since v0.5)
#define HAS_STICKS  1   // two joysticks on D32/D33 (left) and VP/VN (right). 0 = ignore them
#ifndef BUZZER_ACTIVE
#define BUZZER_ACTIVE 1 // 1 = active buzzer (beeps by itself on 5V: the team's buzzer). 0 = passive buzzer / not sure
#endif

#include <SPI.h>
#include <Adafruit_GFX.h>
#include <Adafruit_ILI9341.h>
#if HAS_LEDS
#include <Adafruit_NeoPixel.h>   // only needed once the LED stick is wired
#endif

#define FW_VERSION "0.7-noknob"
const uint32_t TFT_SPI_HZ = 27000000;   // 27 MHz: reliable over jumper wires

// ---------------- Pins ----------------
const uint8_t PIN_TFT_CS = 5, PIN_TFT_DC = 16, PIN_TFT_RST = 17;   // SCK 18, MOSI 23 (VSPI)
const int8_t PIN_SPI_SCK = 18, PIN_SPI_MOSI = 23;                  // MISO not used: D19 is the INFO button
const uint8_t PIN_NEXT = 4;                                         // right joystick SW (click) -> D4, other side GND
const uint8_t PIN_LEDS = 22;                                       // (LED stick, not fitted: D22 is now DOWN)
#if HAS_LEDS
#error "D22 is the DOWN button since v0.5. Move the LED stick to a free pin before setting HAS_LEDS to 1."
#endif
// Joysticks (HW-504 / KY-023): power them from 3V3, never 5V (the ESP32 reads at most 3.3 V)
const uint8_t PIN_LX = 32, PIN_LY = 33;   // left stick  VRx, VRy  (ADC1)
const uint8_t PIN_RX = 36, PIN_RY = 39;   // right stick VRx -> VP, VRy -> VN  (ADC1, input-only)
// If a stick moves the wrong way on the self-test screen, flip it here (1 = reverse that direction)
#define INVERT_LX 0
#define INVERT_LY 1
#define INVERT_RX 0
#define INVERT_RY 1
#define SWAP_LEFT_XY  0   // 1 if pushing the LEFT stick up moves its dot sideways
#define SWAP_RIGHT_XY 0   // 1 if pushing the RIGHT stick up moves its dot sideways
const int STICK_DEADZONE = 12;            // percent of travel ignored around the centre
const uint8_t PIN_BUZZER = 21;                                     // via 2N2222 on the real board
const uint8_t NUM_LEDS = 8;
const uint8_t LED_BRIGHTNESS = 64;                                 // ~25 % (power budget)

// Types used in function signatures are declared up here: the Arduino IDE inserts function
// prototypes above the first function, so these must exist before that point.
struct Note { uint16_t freq; uint16_t ms; };   // freq 0 = rest; {0,0} ends a pattern

struct Button {
  const char* name;
  uint8_t pin;
  uint16_t colour;
  int16_t x, y;
  bool stable = true, lastRead = true, longSent = false, tested = false;
  uint32_t changedAt = 0, pressedAt = 0;
  uint16_t presses = 0;
};

Button buttons[] = {
  // Self-test layout, landscape 320 x 240: D-pad in the middle, action row below, NEXT and INFO beside UP
  {"UP",     15, ILI9341_BLUE,   80,  58},   // v0.5: moved from D32 (now the left joystick)
  {"DOWN",   22, ILI9341_BLUE,   80, 138},   // v0.5: moved from D33 (now the left joystick)
  {"LEFT",   25, ILI9341_BLUE,    8,  98},
  {"RIGHT",  26, ILI9341_BLUE,  152,  98},
  {"BACK",   27, ILI9341_LIGHTGREY, 8, 182},
  {"ACCEPT", 14, ILI9341_GREEN,  80, 182},
  {"REJECT", 13, ILI9341_RED,   152, 182},
  {"NEXT",   PIN_NEXT, ILI9341_CYAN, 152, 58},   // v0.7: right-stick click (replaces the knob)
  {"INFO",   19, ILI9341_MAGENTA,  8,  58},   // v0.6: 8th button (D19 = unused screen MISO)
};
const uint8_t NUM_BUTTONS = sizeof(buttons) / sizeof(buttons[0]);
const uint16_t BTN_W = 66, BTN_H = 34;
const uint32_t DEBOUNCE_MS = 25, LONG_MS = 600;
const uint32_t LINK_TIMEOUT_MS = 4000;

Adafruit_ILI9341 tft(PIN_TFT_CS, PIN_TFT_DC, PIN_TFT_RST);
#if HAS_LEDS
Adafruit_NeoPixel strip(NUM_LEDS, PIN_LEDS, NEO_GRB + NEO_KHZ800);
#else
// No LED stick yet: a do-nothing stand-in so the rest of the code compiles without the NeoPixel library
struct NoStrip {
  void begin() {} void show() {} void clear() {} void setBrightness(uint8_t) {}
  void setPixelColor(uint16_t, uint32_t) {}
  static uint32_t Color(uint8_t r, uint8_t g, uint8_t b) { return ((uint32_t)r << 16) | ((uint32_t)g << 8) | b; }
} strip;
#endif

enum Mode : uint8_t { MODE_SELFTEST, MODE_CONSOLE };
Mode mode = MODE_SELFTEST;

// ---------------- Buzzer: non-blocking pattern player ----------------
static const Note P_CLICK[]  = {{2000, 8}, {0, 0}};
static const Note P_OK[]     = {{988, 60}, {1319, 90}, {0, 0}};
static const Note P_ERR[]    = {{330, 120}, {247, 160}, {0, 0}};
static const Note P_NEW[]    = {{1175, 70}, {1568, 80}, {0, 0}};
static const Note P_URGENT[] = {{1760, 80}, {0, 50}, {1760, 80}, {0, 50}, {1760, 120}, {0, 0}};
static const Note P_EVENT[]  = {{659, 100}, {523, 140}, {0, 0}};
static const Note P_WARN[]   = {{440, 120}, {0, 50}, {440, 120}, {0, 0}};
static const Note P_COIN[]   = {{1319, 50}, {1976, 120}, {0, 0}};
static const Note P_OVER[]   = {{784, 150}, {659, 150}, {523, 300}, {0, 0}};
static const Note P_START[]  = {{523, 80}, {659, 80}, {784, 140}, {0, 0}};

static const Note P_BOOT[]   = {{880, 80}, {0, 40}, {1175, 120}, {0, 0}};
static const Note P_TEST[]   = {{523, 150}, {0, 60}, {784, 150}, {0, 60}, {1047, 150}, {0, 60}, {2000, 300}, {0, 0}};

// Low level: start / stop the buzzer. tone() works for both buzzer types; an active buzzer can
// instead be switched fully on (BUZZER_ACTIVE 1), which sounds cleaner. When quiet, the pin is LOW
// so the transistor is fully off.
bool soundOn = false;
uint32_t soundOffAt = 0;

void soundStop() {
#if !BUZZER_ACTIVE
  noTone(PIN_BUZZER);
#endif
  pinMode(PIN_BUZZER, OUTPUT);
  digitalWrite(PIN_BUZZER, LOW);
  soundOn = false;
}

void soundStart(uint16_t freq, uint16_t ms, uint32_t now) {
  if (freq == 0 || ms == 0) { soundStop(); return; }
#if BUZZER_ACTIVE
  digitalWrite(PIN_BUZZER, HIGH);
#else
  tone(PIN_BUZZER, freq);
#endif
  soundOn = true;
  soundOffAt = now + ms;
}

// Pattern player: one note at a time, never blocks the loop
const Note* playing = nullptr;
uint8_t noteIndex = 0;
uint32_t noteEnds = 0;

void play(const Note* pattern) {
  playing = pattern;
  noteIndex = 0;
  noteEnds = 0;
}

void buzzerUpdate(uint32_t now) {
  if (soundOn && (int32_t)(now - soundOffAt) >= 0) soundStop();
  if (playing == nullptr || (int32_t)(now - noteEnds) < 0) return;
  const Note& n = playing[noteIndex];
  if (n.ms == 0) { soundStop(); playing = nullptr; return; }
  if (n.freq > 0) soundStart(n.freq, n.ms, now);
  else soundStop();
  noteEnds = now + n.ms + 4;
  noteIndex++;
}

const Note* cueByName(const char* name) {
  if (!strcmp(name, "click")) return P_CLICK;
  if (!strcmp(name, "ok")) return P_OK;
  if (!strcmp(name, "err")) return P_ERR;
  if (!strcmp(name, "new")) return P_NEW;
  if (!strcmp(name, "urgent")) return P_URGENT;
  if (!strcmp(name, "event")) return P_EVENT;
  if (!strcmp(name, "warn")) return P_WARN;
  if (!strcmp(name, "coin")) return P_COIN;
  if (!strcmp(name, "over")) return P_OVER;
  if (!strcmp(name, "start")) return P_START;
  return nullptr;
}

// Single beep (self-test feedback). It replaces whatever was playing.
void beep(uint16_t freq, uint16_t ms) { playing = nullptr; soundStart(freq, ms, millis()); }

// ---------------- Joysticks ----------------
// Raw 0..4095 -> -100..100 with the centre measured at start-up, a dead zone, and light smoothing.
const uint8_t STICK_PINS[4] = {PIN_LX, PIN_LY, PIN_RX, PIN_RY};
const bool STICK_INVERT[4] = {INVERT_LX, INVERT_LY, INVERT_RX, INVERT_RY};
int stickCentre[4] = {2048, 2048, 2048, 2048};
int stickRaw[4] = {0, 0, 0, 0};
float stickSmooth[4] = {2048, 2048, 2048, 2048};
int stickVal[4] = {0, 0, 0, 0};          // LX, LY, RX, RY after mapping
int stickSent[4] = {0, 0, 0, 0};
bool stickTested[2] = {false, false};
uint32_t lastStickSend = 0;

int readAxis(uint8_t pin) {                // 4-sample average
  int sum = 0;
  for (uint8_t k = 0; k < 4; k++) sum += analogRead(pin);
  return sum / 4;
}

void calibrateSticks() {
  for (uint8_t a = 0; a < 4; a++) {
    long sum = 0;
    for (uint8_t k = 0; k < 16; k++) sum += readAxis(STICK_PINS[a]);
    stickCentre[a] = (int)(sum / 16);
    stickSmooth[a] = (float)stickCentre[a];
  }
}

int mapAxis(uint8_t a, int raw) {
  int c = stickCentre[a];
  long v = raw >= c ? (long)(raw - c) * 100 / max(1, 4095 - c) : -(long)(c - raw) * 100 / max(1, c);
  if (v > 100) v = 100;
  if (v < -100) v = -100;
  if (labs(v) < STICK_DEADZONE) return 0;
  v = (v > 0 ? v - STICK_DEADZONE : v + STICK_DEADZONE) * 100 / (100 - STICK_DEADZONE);  // full range after the dead zone
  return STICK_INVERT[a] ? -(int)v : (int)v;
}

void readSticks() {
  for (uint8_t a = 0; a < 4; a++) {
    stickRaw[a] = readAxis(STICK_PINS[a]);
    stickSmooth[a] += (stickRaw[a] - stickSmooth[a]) * 0.5f;
  }
  int v[4];
  for (uint8_t a = 0; a < 4; a++) v[a] = mapAxis(a, (int)(stickSmooth[a] + 0.5f));
  if (SWAP_LEFT_XY)  { int t = v[0]; v[0] = v[1]; v[1] = t; }
  if (SWAP_RIGHT_XY) { int t = v[2]; v[2] = v[3]; v[3] = t; }
  for (uint8_t a = 0; a < 4; a++) stickVal[a] = v[a];
  if (abs(v[0]) > 60 || abs(v[1]) > 60) stickTested[0] = true;
  if (abs(v[2]) > 60 || abs(v[3]) > 60) stickTested[1] = true;
}

bool sticksCentred(const int* v) { return v[0] == 0 && v[1] == 0 && v[2] == 0 && v[3] == 0; }

// J line: 20x a second while any stick is off-centre (the game stops the camera if they stop), one 0-line after
void sendSticks(uint32_t now) {
  bool centred = sticksCentred(stickVal);
  bool wasCentred = sticksCentred(stickSent);
  if (centred && wasCentred) return;
  if (!centred && now - lastStickSend < 50) return;
  lastStickSend = now;
  Serial.printf("J,%d,%d,%d,%d\n", stickVal[0], stickVal[1], stickVal[2], stickVal[3]);
  for (uint8_t a = 0; a < 4; a++) stickSent[a] = stickVal[a];
}

// =====================================================================================
//  SELF-TEST SCREEN
// =====================================================================================
void drawButton(const Button& b, bool down) {
  uint16_t fill = down ? b.colour : ILI9341_BLACK;
  uint16_t text = down ? ILI9341_BLACK : (b.tested ? ILI9341_GREEN : ILI9341_WHITE);
  tft.fillRoundRect(b.x, b.y, BTN_W, BTN_H, 6, fill);
  tft.drawRoundRect(b.x, b.y, BTN_W, BTN_H, 6, b.tested ? ILI9341_GREEN : b.colour);
  tft.setTextColor(text);
  tft.setTextSize(1);
  int16_t tx = b.x + (BTN_W - (int16_t)strlen(b.name) * 6) / 2;
  tft.setCursor(tx, b.y + 9);
  tft.print(b.name);
  tft.setCursor(b.x + BTN_W / 2 - 6, b.y + 21);
  tft.print(b.presses);
}

void drawSelfTestHeader() {
  tft.fillRect(0, 0, 320, 28, 0x0A2E);
  tft.setTextColor(ILI9341_CYAN);
  tft.setTextSize(2);
  tft.setCursor(8, 6);
  tft.print("TAXI CONSOLE  SELF-TEST");
  tft.setTextSize(1);
  tft.setTextColor(ILI9341_LIGHTGREY);
  tft.setCursor(8, 36);
  tft.print("Press all 8 buttons, click and move both sticks.");
  tft.setCursor(8, 46);
  tft.print("fw " FW_VERSION "  | Serial: beep / pins / cal");
}

void drawStickPad(uint8_t s, bool full);   // defined just below
void drawSticksBox() {
  tft.fillRect(228, 58, 86, 158, ILI9341_BLACK);
  tft.drawRect(228, 58, 86, 158, ILI9341_CYAN);
  tft.setTextColor(ILI9341_CYAN);
  tft.setTextSize(1);
  tft.setCursor(253, 66);
  tft.print("STICKS");
  tft.setTextColor(ILI9341_LIGHTGREY);
  tft.setCursor(234, 162); tft.print("L: move map");
  tft.setCursor(234, 174); tft.print("R: turn/zoom");
  tft.setCursor(234, 186); tft.print("R click=NEXT");
  if (HAS_STICKS) { drawStickPad(0, true); drawStickPad(1, true); }
}

// Two 36 x 36 joystick pads inside the right-hand box (L at x 234, R at x 274)
const int16_t PAD_Y = 90, PAD_S = 36;
int16_t padDot[2][2] = {{-999, -999}, {-999, -999}};
void drawStickPad(uint8_t s, bool full) {
  int16_t x0 = 234 + s * 40, cxp = x0 + PAD_S / 2, cyp = PAD_Y + PAD_S / 2;
  uint16_t col = stickTested[s] ? ILI9341_GREEN : ILI9341_CYAN;
  if (full) {
    tft.fillRect(x0, PAD_Y, PAD_S, PAD_S, ILI9341_BLACK);
    tft.drawRect(x0, PAD_Y, PAD_S, PAD_S, col);
    tft.setTextSize(1);
    tft.setTextColor(col);
    tft.setCursor(x0 + 15, PAD_Y + PAD_S + 6);
    tft.print(s == 0 ? "L" : "R");
  }
  int16_t dx = cxp + stickVal[s * 2] * (PAD_S / 2 - 4) / 100;
  int16_t dy = cyp - stickVal[s * 2 + 1] * (PAD_S / 2 - 4) / 100;   // +Y = up on screen
  if (!full && dx == padDot[s][0] && dy == padDot[s][1]) return;
  if (padDot[s][0] != -999) tft.fillCircle(padDot[s][0], padDot[s][1], 3, ILI9341_BLACK);
  tft.drawFastHLine(x0 + 2, cyp, PAD_S - 4, 0x2104);
  tft.drawFastVLine(cxp, PAD_Y + 2, PAD_S - 4, 0x2104);
  tft.fillCircle(dx, dy, 3, col);
  padDot[s][0] = dx; padDot[s][1] = dy;
}

bool allTestedShown = false;
void drawStatus() {
  bool all = true;
  for (uint8_t i = 0; i < NUM_BUTTONS; i++) all &= buttons[i].tested;
  if (HAS_STICKS) all &= stickTested[0] && stickTested[1];
  if (all == allTestedShown) return;
  allTestedShown = all;
  tft.fillRect(0, 220, 320, 20, all ? ILI9341_DARKGREEN : ILI9341_BLACK);
  tft.setTextSize(2);
  tft.setTextColor(ILI9341_WHITE);
  tft.setCursor(80, 222);
  tft.print(all ? "ALL INPUTS OK" : "");
  if (all) play(P_START);
}

void drawSelfTest() {
  tft.fillScreen(ILI9341_BLACK);
  drawSelfTestHeader();
  for (uint8_t i = 0; i < NUM_BUTTONS; i++) drawButton(buttons[i], false);
  drawSticksBox();
  allTestedShown = false;
  drawStatus();
}

// ---------------- Self-test LEDs ----------------
uint32_t flashUntil = 0;
uint32_t flashColour = 0;

void updateSelfTestLeds(int32_t enc) {
  int pos = ((enc % NUM_LEDS) + NUM_LEDS) % NUM_LEDS;
  bool flashing = millis() < flashUntil;
  for (int i = 0; i < NUM_LEDS; i++) {
    uint32_t c = strip.Color(2, 4, 6);                    // dim idle
    if (i == pos) c = strip.Color(0, 180, 255);            // encoder cursor
    if (flashing) c = flashColour;
    strip.setPixelColor(i, c);
  }
  strip.show();
}

uint32_t ledColourFor(uint16_t tftColour) {
  uint8_t r = (tftColour >> 11) << 3, g = ((tftColour >> 5) & 0x3F) << 2, b = (tftColour & 0x1F) << 3;
  return strip.Color(r, g, b);
}

// =====================================================================================
//  CONSOLE SCREEN (drawn from the game's lines)
// =====================================================================================
const uint16_t BG = ILI9341_BLACK, HEADER_BG = 0x0A2E, TITLE_BG = 0x10A2, FOOTER_BG = 0x1082, SELECT_BG = 0x2124;
const int16_t ROW_Y0 = 48, ROW_H = 21, FOOTER_Y = 220;
const uint8_t ROWS = 8;

struct RowState { char text[56]; char style[4]; };
RowState rows[ROWS];
char hdrClock[8] = "", hdrCash[14] = "", hdrSat[8] = "", hdrSurge[8] = "", hdrSpeed[8] = "";
char titleText[28] = "";
uint8_t titlePage = 0, titlePages = 1;
char footerText[56] = "";
char toastText[56] = "";
char toastColour = 'W';
uint32_t toastUntil = 0;
uint32_t lastHostLine = 0;
bool linkLost = false;

uint16_t colourOf(char c) {
  switch (c) {
    case 'D': return 0x9CF3;          // grey
    case 'G': return ILI9341_GREEN;
    case 'Y': return ILI9341_YELLOW;
    case 'O': return ILI9341_ORANGE;
    case 'R': return 0xF9A6;          // soft red (readable on black)
    case 'C': return ILI9341_CYAN;
    case 'B': return 0x5D1F;          // light blue
    case 'M': return ILI9341_MAGENTA;
    default:  return ILI9341_WHITE;
  }
}

void copyField(char* dst, size_t size, const char* src) {
  strncpy(dst, src ? src : "", size - 1);
  dst[size - 1] = '\0';
}

void drawHeaderBar() {
  tft.fillRect(0, 0, 320, 22, HEADER_BG);
  tft.setTextWrap(false);
  tft.setTextSize(2);
  tft.setTextColor(ILI9341_WHITE);
  tft.setCursor(4, 4);
  tft.print(hdrClock);
  tft.setTextColor(ILI9341_GREEN);
  tft.setCursor(72, 4);
  tft.print(hdrCash);
  tft.setTextSize(1);
  tft.setTextColor(ILI9341_LIGHTGREY);
  tft.setCursor(206, 3);
  tft.print("Happy ");
  tft.setTextColor(ILI9341_WHITE);
  tft.print(hdrSat);
  tft.setTextColor(ILI9341_LIGHTGREY);
  tft.setCursor(206, 13);
  tft.print("Price ");
  tft.setTextColor(ILI9341_CYAN);
  tft.print(hdrSurge);
  tft.setTextColor(strcmp(hdrSpeed, "PAUSE") == 0 ? ILI9341_YELLOW : ILI9341_WHITE);
  tft.setCursor(286, 8);
  tft.print(hdrSpeed);
}

void drawTitleBar() {
  tft.fillRect(0, 23, 320, 22, TITLE_BG);
  tft.setTextWrap(false);
  tft.setTextSize(2);
  tft.setTextColor(ILI9341_CYAN);
  tft.setCursor(4, 27);
  tft.print(titleText);
  if (titlePages > 1) {
    for (uint8_t i = 0; i < titlePages && i < 6; i++) {
      int16_t x = 320 - (titlePages - i) * 9;
      if (i == titlePage) tft.fillCircle(x, 34, 3, ILI9341_CYAN);
      else tft.drawCircle(x, 34, 3, 0x6B6D);
    }
  }
}

void drawRow(uint8_t i) {
  if (i >= ROWS) return;
  const RowState& r = rows[i];
  int16_t y = ROW_Y0 + i * ROW_H;
  char colour = r.style[0] ? r.style[0] : 'W';
  bool selected = strchr(r.style, '*') != nullptr;
  bool large = strchr(r.style, '2') != nullptr;
  uint16_t fg = colourOf(colour);
  tft.fillRect(0, y, 320, ROW_H, selected ? SELECT_BG : BG);
  if (selected) tft.fillRect(0, y + 1, 3, ROW_H - 2, fg);
  tft.setTextWrap(false);
  tft.setTextSize(large ? 2 : 1);
  tft.setTextColor(fg);
  tft.setCursor(6, y + (large ? 3 : 7));
  tft.print(r.text);
}

void drawFooterBar(uint32_t now) {
  tft.fillRect(0, FOOTER_Y, 320, 240 - FOOTER_Y, FOOTER_BG);
  tft.setTextWrap(false);
  tft.setTextSize(1);
  tft.setCursor(4, FOOTER_Y + 7);
  if (linkLost) {
    tft.fillRect(0, FOOTER_Y, 320, 240 - FOOTER_Y, 0x8000);
    tft.setTextColor(ILI9341_WHITE);
    tft.print("LINK LOST - is the game running on the PC?");
  } else if (now < toastUntil) {
    tft.setTextColor(colourOf(toastColour));
    tft.print(toastText);
  } else {
    tft.setTextColor(0x9CF3);
    tft.print(footerText);
  }
}

void enterConsole() {
  mode = MODE_CONSOLE;
  for (uint8_t i = 0; i < ROWS; i++) { rows[i].text[0] = '\0'; copyField(rows[i].style, sizeof(rows[i].style), "W"); }
  tft.fillScreen(BG);
  drawHeaderBar();
  drawTitleBar();
  drawFooterBar(millis());
  strip.clear();
  strip.show();
}

// Splits "A|b|c" in place. The last field keeps everything after the previous separator.
uint8_t splitFields(char* line, char** fields, uint8_t maxFields) {
  uint8_t n = 0;
  char* p = line;
  fields[n++] = p;
  while (*p && n < maxFields) {
    if (*p == '|') { *p = '\0'; fields[n++] = p + 1; }
    p++;
  }
  return n;
}

uint8_t hexNibble(char c) {
  if (c >= '0' && c <= '9') return c - '0';
  if (c >= 'A' && c <= 'F') return c - 'A' + 10;
  if (c >= 'a' && c <= 'f') return c - 'a' + 10;
  return 0;
}

// One complete line from the game. Returns false if it wasn't a console line.
bool handleHostLine(char* line, uint32_t now) {
  if (line[0] == '\0' || line[1] != '|' || strchr("STRFMZL", line[0]) == nullptr) return false;
  char* f[6];
  uint8_t n;
  bool first = mode != MODE_CONSOLE;
  if (first) enterConsole();
  lastHostLine = now;
  if (linkLost) { linkLost = false; drawFooterBar(now); }

  switch (line[0]) {
    case 'S':
      n = splitFields(line, f, 6);
      if (n < 6) return false;
      copyField(hdrClock, sizeof(hdrClock), f[1]);
      copyField(hdrCash, sizeof(hdrCash), f[2]);
      copyField(hdrSat, sizeof(hdrSat), f[3]);
      copyField(hdrSurge, sizeof(hdrSurge), f[4]);
      copyField(hdrSpeed, sizeof(hdrSpeed), f[5]);
      drawHeaderBar();
      return true;
    case 'T':
      n = splitFields(line, f, 4);
      if (n < 4) return false;
      titlePage = (uint8_t)atoi(f[1]);
      titlePages = (uint8_t)atoi(f[2]);
      copyField(titleText, sizeof(titleText), f[3]);
      drawTitleBar();
      return true;
    case 'R': {
      n = splitFields(line, f, 4);
      if (n < 4) return false;
      int row = atoi(f[1]);
      if (row < 0 || row >= ROWS) return false;
      copyField(rows[row].style, sizeof(rows[row].style), f[2]);
      copyField(rows[row].text, sizeof(rows[row].text), f[3]);
      drawRow((uint8_t)row);
      return true;
    }
    case 'F':
      n = splitFields(line, f, 2);
      if (n < 2) return false;
      copyField(footerText, sizeof(footerText), f[1]);
      if (now >= toastUntil) drawFooterBar(now);
      return true;
    case 'M':
      n = splitFields(line, f, 3);
      if (n < 3) return false;
      toastColour = f[1][0] ? f[1][0] : 'W';
      copyField(toastText, sizeof(toastText), f[2]);
      toastUntil = now + 2500;
      drawFooterBar(now);
      return true;
    case 'Z': {
      n = splitFields(line, f, 2);
      if (n < 2) return false;
      const Note* p = cueByName(f[1]);
      if (p) play(p);
      return true;
    }
    case 'L': {
      n = splitFields(line, f, 2);
      if (n < 2 || strlen(f[1]) < 6 * NUM_LEDS) return false;
      for (uint8_t i = 0; i < NUM_LEDS; i++) {
        const char* h = f[1] + i * 6;
        uint8_t r = (hexNibble(h[0]) << 4) | hexNibble(h[1]);
        uint8_t g = (hexNibble(h[2]) << 4) | hexNibble(h[3]);
        uint8_t b = (hexNibble(h[4]) << 4) | hexNibble(h[5]);
        strip.setPixelColor(i, strip.Color(r, g, b));
      }
      strip.show();
      return true;
    }
  }
  return false;
}

// ---------------- Serial Monitor test commands (lines start with '#' so the game ignores the replies) ----------------
bool handleTestCommand(const char* cmd) {
  if (!strcmp(cmd, "beep")) {
    play(P_TEST);
    Serial.println("# BUZZER TEST on D21: 3 rising notes, then a high beep (about 1 s).");
    Serial.println("# Silent? 2N2222 flat face towards you in c45/c46/c47 (E-B-C), 1k d46-h46, D21->j46,");
    Serial.println("#   buzzer short leg b47, long leg b50, a45->GND rail, a50->5V rail.");
    return true;
  }
  if (!strcmp(cmd, "pins")) {
    Serial.print("# Buttons (0 = pressed now):");
    for (uint8_t i = 0; i < NUM_BUTTONS; i++) Serial.printf(" %s=%d", buttons[i].name, digitalRead(buttons[i].pin));
    Serial.printf("\n# Mode %s\n", mode == MODE_CONSOLE ? "CONSOLE" : "SELF-TEST");
    if (HAS_STICKS)
      Serial.printf("# Sticks raw LX=%d LY=%d RX=%d RY=%d (centre %d %d %d %d) -> J,%d,%d,%d,%d\n",
                    stickRaw[0], stickRaw[1], stickRaw[2], stickRaw[3], stickCentre[0], stickCentre[1], stickCentre[2],
                    stickCentre[3], stickVal[0], stickVal[1], stickVal[2], stickVal[3]);
    return true;
  }
  if (!strcmp(cmd, "cal")) {
    calibrateSticks();
    Serial.printf("# Sticks re-centred: %d %d %d %d (keep them untouched while typing cal)\n",
                  stickCentre[0], stickCentre[1], stickCentre[2], stickCentre[3]);
    return true;
  }
  if (!strcmp(cmd, "help") || !strcmp(cmd, "?")) {
    Serial.println("# Commands: beep (sound test), pins (buttons + sticks), cal (re-centre sticks), help");
    return true;
  }
  return false;
}

// ---------------- Serial line reader ----------------
char rxLine[128];
uint8_t rxLen = 0;

void readHost(uint32_t now) {
  while (Serial.available() > 0) {
    char c = (char)Serial.read();
    if (c == '\r') continue;
    if (c == '\n') {
      rxLine[rxLen] = '\0';
      if (rxLen > 0 && !handleTestCommand(rxLine)) handleHostLine(rxLine, now);
      rxLen = 0;
    } else if (rxLen < sizeof(rxLine) - 1) {
      rxLine[rxLen++] = c;
    }
  }
}

void consoleUpdate(uint32_t now) {
  static bool toastShown = false;
  bool toastNow = now < toastUntil;
  if (toastShown && !toastNow) drawFooterBar(now);   // toast expired: back to the hint
  toastShown = toastNow;
  if (!linkLost && now - lastHostLine > LINK_TIMEOUT_MS) {
    linkLost = true;
    drawFooterBar(now);
    play(P_WARN);
  }
}

// =====================================================================================
//  SETUP / LOOP
// =====================================================================================
void setup() {
  Serial.begin(115200);
  for (uint8_t i = 0; i < NUM_BUTTONS; i++) {
    pinMode(buttons[i].pin, INPUT_PULLUP);
    buttons[i].stable = buttons[i].lastRead = digitalRead(buttons[i].pin);
  }
  soundStop();   // buzzer pin LOW straight away: transistor off, no hum
  if (HAS_STICKS) {
    analogReadResolution(12);
    for (uint8_t a = 0; a < 4; a++) pinMode(STICK_PINS[a], INPUT);
    calibrateSticks();               // the sticks must be untouched at power-on
  }

  if (HAS_LEDS) {
    strip.begin();
    strip.setBrightness(LED_BRIGHTNESS);
    // Power-on LED sweep: red, green, blue, white
    const uint32_t sweep[] = {strip.Color(255, 0, 0), strip.Color(0, 255, 0), strip.Color(0, 0, 255), strip.Color(255, 255, 255)};
    for (uint32_t c : sweep) {
      for (int i = 0; i < NUM_LEDS; i++) { strip.setPixelColor(i, c); strip.show(); delay(25); }
    }
    strip.clear();
    strip.show();
  }

  SPI.begin(PIN_SPI_SCK, -1, PIN_SPI_MOSI);   // no MISO, so D19 stays a button (the library's own begin() is then a no-op)
  tft.begin(TFT_SPI_HZ);
  tft.setRotation(1); // landscape 320 x 240
  drawSelfTest();

  play(P_BOOT);   // two start-up beeps (played by the loop)
  Serial.println("H,TAXI-CONSOLE," FW_VERSION);
  Serial.println("# Serial Monitor at 115200: type beep, pins, cal or help, then press Enter.");
}

uint32_t lastKeepAlive = 0;

void loop() {
  uint32_t now = millis();

  // Buttons: 25 ms debounce, P on press, L after 600 ms, R on release (same in both modes)
  for (uint8_t i = 0; i < NUM_BUTTONS; i++) {
    Button& b = buttons[i];
    bool raw = digitalRead(b.pin);
    if (raw != b.lastRead) { b.lastRead = raw; b.changedAt = now; }
    if (raw != b.stable && now - b.changedAt >= DEBOUNCE_MS) {
      b.stable = raw;
      bool down = (raw == LOW);
      if (down) {
        b.pressedAt = now;
        b.longSent = false;
        b.presses++;
        b.tested = true;
        Serial.printf("B,%s,P\n", b.name);
        if (mode == MODE_SELFTEST) {
          beep(600 + i * 120, 40);
          flashColour = ledColourFor(b.colour);
          flashUntil = now + 150;
        }
      } else {
        Serial.printf("B,%s,R\n", b.name);
      }
      if (mode == MODE_SELFTEST) { drawButton(b, down); drawStatus(); }
    }
    if (b.stable == LOW && !b.longSent && now - b.pressedAt >= LONG_MS) {
      b.longSent = true;
      Serial.printf("B,%s,L\n", b.name);
      if (mode == MODE_SELFTEST) beep(400, 60);
    }
  }

  // Joysticks
  if (HAS_STICKS) {
    bool before0 = stickTested[0], before1 = stickTested[1];
    readSticks();
    sendSticks(now);
    if (mode == MODE_SELFTEST) {
      drawStickPad(0, before0 != stickTested[0]);
      drawStickPad(1, before1 != stickTested[1]);
      if (before0 != stickTested[0] || before1 != stickTested[1]) drawStatus();
    }
  }

  readHost(now);
  if (mode == MODE_CONSOLE) consoleUpdate(now);
  else if (HAS_LEDS) updateSelfTestLeds(0);
  buzzerUpdate(now);

  // Keep-alive so the game can detect the console (and "LINK LOST")
  if (now - lastKeepAlive >= 1000) {
    lastKeepAlive = now;
    Serial.println("K");
  }
  delay(1);
}
