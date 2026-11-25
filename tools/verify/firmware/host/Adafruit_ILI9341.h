#pragma once
#include <Adafruit_GFX.h>
// Colour values copied from the real Adafruit_ILI9341.h
#define ILI9341_BLACK 0x0000
#define ILI9341_BLUE 0x001F
#define ILI9341_RED 0xF800
#define ILI9341_GREEN 0x07E0
#define ILI9341_CYAN 0x07FF
#define ILI9341_MAGENTA 0xF81F
#define ILI9341_YELLOW 0xFFE0
#define ILI9341_WHITE 0xFFFF
#define ILI9341_ORANGE 0xFD20
#define ILI9341_LIGHTGREY 0xC618
#define ILI9341_DARKGREEN 0x03E0
class Adafruit_ILI9341 : public Adafruit_GFX {
public:
  Adafruit_ILI9341(int8_t, int8_t, int8_t = -1) {}
  void begin(uint32_t = 0) {}
};
