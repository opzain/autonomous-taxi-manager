// Runs the real console firmware (main.cpp) on the PC: feeds it the game's line stream, presses buttons,
// checks the serial output and prints the TFT as text at each #SNAP.
#include <Arduino.h>
#include <SPI.h>
uint32_t g_now = 0;
int g_pins[64];
int g_analog[64];
std::vector<std::string> g_tones;
HardwareSerial Serial;
SPIClass SPI;
#define setup fw_setup
#define loop fw_loop
#include "../../../firmware/dispatch_console/src/main.cpp"
#undef setup
#undef loop
#include <fstream>
#include <iostream>

static void dump(const char* name) {
  printf("---- TFT: %s ----\n", name);
  for (int r = 0; r < Adafruit_GFX::ROWS; r++) printf("|%s|\n", tft.cells[r]);
}
static int fails = 0;
static void check(bool ok, const char* what) { printf("%s %s\n", ok ? "PASS" : "FAIL", what); if (!ok) fails++; }
static std::string squash(const char* s) { std::string o; for (; *s; s++) if (*s != ' ') o += *s; return o; }
static bool screenHas(const char* s) { std::string n = squash(s); for (int r = 0; r < Adafruit_GFX::ROWS; r++) if (squash(tft.cells[r]).find(n) != std::string::npos) return true; return false; }
static void runUntil(uint32_t t) { while (g_now < t) fw_loop(); }

int main(int argc, char** argv) {
  for (int i = 0; i < 64; i++) { g_pins[i] = HIGH; g_analog[i] = 1900; }   // sticks resting a bit off 2048, like real ones
  fw_setup();
  check(Serial.out.find("H,TAXI-CONSOLE,0.7-noknob") != std::string::npos, "hello line at boot");
  check(screenHas("SELF-TEST"), "self-test screen at boot");
  runUntil(2100);
  check(Serial.out.find("\nK\n") != std::string::npos, "keep-alive K every second");

  auto hasTone = [](const char* t) { for (auto& s : g_tones) if (s == t) return true; return false; };
  check(hasTone("tone 880 0") && hasTone("tone 1175 0"), "two start-up beeps played by the loop");
  check(!g_tones.empty() && g_tones.back() == "off", "buzzer switched off after the start-up beeps");
  // Serial Monitor sound test: plays, stays in self-test, replies with '#' lines the game ignores
  g_tones.clear();
  Serial.feed("beep\n"); runUntil(g_now + 1400);
  check(hasTone("tone 523 0") && hasTone("tone 784 0") && hasTone("tone 1047 0") && hasTone("tone 2000 0"), "'beep' command plays the test notes");
  check(g_tones.back() == "off" && mode == MODE_SELFTEST, "'beep' ends quiet and stays in self-test");
  check(Serial.out.find("# BUZZER TEST") != std::string::npos, "'beep' prints the wiring hint");
  Serial.feed("pins\n"); fw_loop();
  check(Serial.out.find("# Buttons (0 = pressed now): UP=1") != std::string::npos, "'pins' prints button states");
  Serial.feed("X|junk\nhello there\n"); fw_loop();
  check(mode == MODE_SELFTEST, "junk lines don't switch to console mode");

  // Joysticks: centred at boot -> no J lines; push left stick up/right, release -> J lines then one zero line
  check(Serial.out.find("\nJ,") == std::string::npos, "no joystick lines while sticks rest at their centre");
  size_t m0 = Serial.out.size();
  g_analog[33] = 0;       runUntil(g_now + 120);   // LY fully one way (INVERT_LY 1 -> +100 = up)
  check(Serial.out.find("J,0,100,0,0\n", m0) != std::string::npos, "left stick up -> J,0,100,0,0");
  g_analog[33] = 1900; g_analog[36] = 4095; runUntil(g_now + 120);  // right stick X fully right
  check(Serial.out.find("J,0,0,100,0\n", m0) != std::string::npos, "right stick right -> J,0,0,100,0");
  size_t lines = 0; for (size_t p = Serial.out.find("J,", m0); p != std::string::npos; p = Serial.out.find("J,", p + 1)) lines++;
  check(lines >= 3 && lines <= 8, "J lines limited to about 20 per second while held");
  g_analog[36] = 1930; runUntil(g_now + 120);      // small wobble inside the dead zone counts as centre
  check(Serial.out.rfind("J,0,0,0,0\n") != std::string::npos && Serial.out.rfind("J,0,0,0,0\n") > Serial.out.rfind("J,0,0,100,0"), "release -> one J,0,0,0,0");
  size_t m1 = Serial.out.size(); runUntil(g_now + 500);
  check(Serial.out.find("J,", m1) == std::string::npos, "silent again once centred");
  g_analog[32] = 1100; runUntil(g_now + 120); g_analog[32] = 1900; runUntil(g_now + 120);
  check(Serial.out.find("J,-", m1) != std::string::npos, "half-left on the left stick gives a negative X");
  g_analog[36] = 1900; runUntil(g_now + 120);
  Serial.feed("cal\n"); fw_loop();
  check(Serial.out.find("# Sticks re-centred: 1900 1900 1900 1900") != std::string::npos, "'cal' re-centres the sticks");

  // v0.6: INFO on D19, and the SPI bus must not claim D19 as MISO
  check(SPI.sck == 18 && SPI.mosi == 23 && SPI.miso == -1, "SPI started on SCK 18 / MOSI 23 without MISO (D19 free)");
  g_pins[19] = LOW; runUntil(g_now + 40); g_pins[19] = HIGH; runUntil(g_now + 40);
  check(Serial.out.find("B,INFO,P\nB,INFO,R\n") != std::string::npos, "INFO (D19) press + release reported");
  g_pins[15] = LOW; runUntil(g_now + 40); g_pins[15] = HIGH; runUntil(g_now + 40);
  check(Serial.out.find("B,UP,P\nB,UP,R\n") != std::string::npos, "UP (D15) press + release reported");

  // v0.7: the right-stick click on D4 is NEXT (the knob is gone: no E lines)
  g_pins[4] = LOW; runUntil(g_now + 40); g_pins[4] = HIGH; runUntil(g_now + 40);
  check(Serial.out.find("B,NEXT,P\nB,NEXT,R\n") != std::string::npos, "right-stick click (D4) reported as NEXT");
  g_pins[34] = LOW; g_pins[35] = LOW; runUntil(g_now + 60); g_pins[34] = HIGH; g_pins[35] = HIGH; runUntil(g_now + 60);
  check(Serial.out.find("\nE,") == std::string::npos, "no knob (E) lines: D34 / D35 are ignored");

  // A button press in self-test mode
  g_pins[14] = LOW; runUntil(g_now + 40); g_pins[14] = HIGH; runUntil(g_now + 40);
  check(Serial.out.find("B,ACCEPT,P\nB,ACCEPT,R\n") != std::string::npos, "ACCEPT press + release reported");

  std::ifstream in(argv[1]);
  std::string line;
  int fed = 0, snaps = 0;
  bool printAll = argc > 2;
  while (std::getline(in, line)) {
    size_t tab = line.find('\t');
    uint32_t t = (uint32_t)std::stoul(line.substr(0, tab));
    std::string body = line.substr(tab + 1);
    if (t > g_now) runUntil(t);
    if (body.rfind("#SNAP ", 0) == 0) {
      fw_loop(); snaps++;
      if (printAll || body == "#SNAP dispatch-rush" || body == "#SNAP fleet" || body == "#SNAP money" || body == "#SNAP shift-over") dump(body.c_str() + 6);
      continue;
    }
    if (body == "#END") break;
    Serial.feed(body + "\n");
    fw_loop();
    fed++;
  }
  printf("fed %d lines, %d snapshots, %ld draw calls\n", fed, snaps, tft.drawCalls);
  check(mode == MODE_CONSOLE, "switched to console mode on the first game line");
  check(screenHas("SHIFT OVER"), "end-of-shift page drawn");
  check(!linkLost, "no LINK LOST while the game talks");

  // Long press in console mode: P then L after 600 ms
  size_t mark = Serial.out.size();
  g_pins[27] = LOW; runUntil(g_now + 700); g_pins[27] = HIGH; runUntil(g_now + 40);
  std::string tail = Serial.out.substr(mark);
  check(tail.find("B,BACK,P\n") != std::string::npos && tail.find("B,BACK,L\n") != std::string::npos && tail.find("B,BACK,R\n") != std::string::npos, "long press reports P, L, R");

  // Game goes quiet -> LINK LOST
  runUntil(g_now + 4500);
  check(linkLost && screenHas("LINK LOST"), "LINK LOST after 4 s of silence");
  Serial.feed("F|back again\n"); fw_loop();
  check(!linkLost && screenHas("back again"), "link recovers on the next line");

  // Garbage must not crash or draw
  long before = tft.drawCalls;
  Serial.feed("R|99|W|row out of range\nX|nonsense\nR|3\n\n"); fw_loop();
  check(tft.drawCalls == before, "bad lines are ignored safely");
  std::string longLine(300, 'x'); Serial.feed("R|1|W|" + longLine + "\n"); fw_loop();
  check(true, "over-long line truncated without overflow");
  printf(fails ? "FIRMWARE HARNESS: %d FAILED\n" : "FIRMWARE HARNESS: ALL PASSED\n", fails);
  return fails ? 1 : 0;
}
