#pragma once
#include <stdint.h>
// Host stand-in for the ESP32 SPI class: records the pins the firmware asks for.
struct SPIClass {
  int8_t sck = -2, miso = -2, mosi = -2;
  void begin(int8_t s = -1, int8_t mi = -1, int8_t mo = -1, int8_t = -1) { if (sck != -2) return; sck = s; miso = mi; mosi = mo; }
};
extern SPIClass SPI;
