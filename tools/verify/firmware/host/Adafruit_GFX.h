#pragma once
#include <Arduino.h>
// Text-mode TFT model: 320x240 px as a 53x30 grid of 6x8 character cells, so a test can "see" the screen.
class Adafruit_GFX {
public:
  static const int COLS = 53, ROWS = 30;
  char cells[ROWS][COLS + 1];
  int16_t cx = 0, cy = 0; uint8_t size = 1; uint16_t color = 0xFFFF; bool wrap = true; uint8_t rot = 0;
  long drawCalls = 0;
  Adafruit_GFX() { fillScreen(0); }
  void clearPx(int16_t x, int16_t y, int16_t w, int16_t h) {
    drawCalls++;
    if (rot != 1) { fprintf(stderr, "drawing before setRotation(1)\n"); exit(2); }
    if (x < 0 || y < 0 || x + w > 320 || y + h > 240) { fprintf(stderr, "rect off screen %d,%d %dx%d\n", x, y, w, h); exit(3); }
    for (int r = y / 8; r <= (y + h - 1) / 8 && r < ROWS; r++)
      for (int c = x / 6; c <= (x + w - 1) / 6 && c < COLS; c++) cells[r][c] = ' ';
  }
  void fillRoundRect(int16_t x, int16_t y, int16_t w, int16_t h, int16_t, uint16_t) { clearPx(x, y, w, h); }
  void drawRoundRect(int16_t, int16_t, int16_t, int16_t, int16_t, uint16_t) { drawCalls++; }
  void fillRect(int16_t x, int16_t y, int16_t w, int16_t h, uint16_t) { clearPx(x, y, w, h); }
  void drawRect(int16_t, int16_t, int16_t, int16_t, uint16_t) { drawCalls++; }
  void fillCircle(int16_t, int16_t, int16_t, uint16_t) { drawCalls++; }
  void drawFastHLine(int16_t, int16_t, int16_t, uint16_t) { drawCalls++; }
  void drawFastVLine(int16_t, int16_t, int16_t, uint16_t) { drawCalls++; }
  void drawCircle(int16_t, int16_t, int16_t, uint16_t) { drawCalls++; }
  void fillScreen(uint16_t) { for (int r = 0; r < ROWS; r++) { memset(cells[r], ' ', COLS); cells[r][COLS] = 0; } }
  void setTextColor(uint16_t c) { color = c; }
  void setTextSize(uint8_t s) { size = s; }
  void setTextWrap(bool w) { wrap = w; }
  void setCursor(int16_t x, int16_t y) { cx = x; cy = y; }
  void setRotation(uint8_t r) { rot = r; }
  size_t print(const char* s) {
    for (; *s; s++) {
      int c = cx / 6, r = (cy + 2) / 8;
      if (cx + 6 * size <= 320 && r >= 0 && r < ROWS && c >= 0 && c < COLS) cells[r][c] = *s;
      else if (cx < 320 && wrap) { fprintf(stderr, "text wrapped off the right edge\n"); exit(4); }
      cx += 6 * size;
    }
    return 0;
  }
  size_t print(int v) { char b[16]; snprintf(b, sizeof b, "%d", v); return print(b); }
  size_t print(unsigned int v) { char b[16]; snprintf(b, sizeof b, "%u", v); return print(b); }
  size_t print(long v) { char b[24]; snprintf(b, sizeof b, "%ld", v); return print(b); }
};
