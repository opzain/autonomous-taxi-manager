#pragma once
// Host (PC) stand-in for the Arduino-ESP32 core: enough to RUN the console firmware in a test harness.
#include <stdint.h>
#include <string.h>
#include <stdio.h>
#include <stdlib.h>
#include <stdarg.h>
#include <limits.h>
#include <string>
#include <deque>
#include <vector>
#define IRAM_ATTR
#define HIGH 1
#define LOW 0
#define INPUT 0x01
#define OUTPUT 0x03
#define INPUT_PULLUP 0x05
#define CHANGE 0x03
typedef bool boolean;
extern uint32_t g_now;
extern int g_pins[64];
extern int g_analog[64];
#include <algorithm>
using std::max;
using std::min;
extern std::vector<std::string> g_tones;
inline int digitalRead(uint8_t pin) { return g_pins[pin]; }
inline int analogRead(uint8_t pin) { return g_analog[pin]; }
inline void analogReadResolution(uint8_t) {}
inline void pinMode(uint8_t, uint8_t) {}
inline void digitalWrite(uint8_t, uint8_t) {}
inline void attachInterrupt(uint8_t, void (*)(void), int) {}
inline uint8_t digitalPinToInterrupt(uint8_t p) { return p; }
inline void noInterrupts() {}
inline void interrupts() {}
inline unsigned long millis() { return g_now; }
inline void delay(uint32_t ms) { g_now += ms; }
inline void tone(uint8_t, unsigned int f, unsigned long d = 0) { g_tones.push_back("tone " + std::to_string(f) + " " + std::to_string(d)); }
inline void noTone(uint8_t) { g_tones.push_back("off"); }
struct HardwareSerial {
  std::deque<char> in;
  std::string out;
  void begin(unsigned long) {}
  int available() { return (int)in.size(); }
  int read() { if (in.empty()) return -1; char c = in.front(); in.pop_front(); return (unsigned char)c; }
  size_t println(const char* s) { out += s; out += "\n"; return strlen(s) + 1; }
  size_t print(const char* s) { out += s; return strlen(s); }
  size_t printf(const char* fmt, ...) __attribute__((format(printf, 2, 3))) {
    char buf[256]; va_list ap; va_start(ap, fmt); int n = vsnprintf(buf, sizeof buf, fmt, ap); va_end(ap); out += buf; return n;
  }
  void feed(const std::string& s) { for (char c : s) in.push_back(c); }
};
extern HardwareSerial Serial;
