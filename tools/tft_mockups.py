# Replays the game's PC -> ESP32 line stream and draws the TFT as the firmware lays it out
# (header 22 px, title 22 px, 8 rows x 21 px, footer 20 px; same colours), saving a PNG at each #SNAP.
# Usage: python3 tools/tft_mockups.py <protocol_stream.txt> <out_dir>
import sys, os
from PIL import Image, ImageDraw, ImageFont
stream, out = sys.argv[1], sys.argv[2]
SC = 3
def rgb565(v): return (((v >> 11) & 31) * 255 // 31, ((v >> 5) & 63) * 255 // 63, (v & 31) * 255 // 31)
COL = {'W': 0xFFFF, 'D': 0x9CF3, 'G': 0x07E0, 'Y': 0xFFE0, 'O': 0xFD20, 'R': 0xF9A6, 'C': 0x07FF, 'B': 0x5D1F, 'M': 0xF81F}
font_path = "/usr/share/fonts/truetype/dejavu/DejaVuSansMono-Bold.ttf"
f1 = ImageFont.truetype(font_path, int(8.6 * SC)); f2 = ImageFont.truetype(font_path, int(17 * SC))
hdr = ["", "", "", "", ""]; title = ["0", "1", ""]; rows = [["W", ""] for _ in range(8)]; footer = ""; toast = None
def draw(name):
    im = Image.new("RGB", (320 * SC, 240 * SC), (0, 0, 0)); d = ImageDraw.Draw(im)
    def R(x, y, w, h, c): d.rectangle([x * SC, y * SC, (x + w) * SC - 1, (y + h) * SC - 1], fill=c)
    def T(x, y, s, c, big=False): d.text((x * SC, y * SC), s, fill=c, font=f2 if big else f1)
    R(0, 0, 320, 22, rgb565(0x0A2E)); T(4, 3, hdr[0], (255, 255, 255), True); T(72, 3, hdr[1], rgb565(0x07E0), True)
    T(206, 2, "Happy " + hdr[2], (198, 195, 198)); T(206, 12, "Price " + hdr[3], rgb565(0x07FF)); T(286, 7, hdr[4], (255, 255, 255))
    R(0, 23, 320, 22, rgb565(0x10A2)); T(4, 26, title[2], rgb565(0x07FF), True)
    n = int(title[1] or 1)
    for i in range(min(n, 6)):
        x = 320 - (n - i) * 9
        if n > 1:
            if i == int(title[0]): d.ellipse([(x - 3) * SC, 31 * SC, (x + 3) * SC, 37 * SC], fill=rgb565(0x07FF))
            else: d.ellipse([(x - 3) * SC, 31 * SC, (x + 3) * SC, 37 * SC], outline=(110, 110, 110), width=SC)
    for i, (st, tx) in enumerate(rows):
        y = 48 + i * 21; c = rgb565(COL.get(st[:1] or 'W', 0xFFFF)); sel = '*' in st; big = '2' in st
        if sel: R(0, y, 320, 21, rgb565(0x2124)); R(0, y + 1, 3, 19, c)
        T(6, y + (2 if big else 6), tx, c, big)
    R(0, 220, 320, 20, rgb565(0x1082))
    if toast: T(4, 225, toast[1], rgb565(COL.get(toast[0], 0xFFFF)))
    else: T(4, 225, footer, rgb565(0x9CF3))
    im.save(os.path.join(out, f"tft-{name}.png"))
for line in open(stream):
    _, body = line.rstrip("\n").split("\t", 1)
    if body.startswith("#SNAP "): draw(body[6:]); toast = None; continue
    p = body.split("|")
    if p[0] == "S": hdr = p[1:6]
    elif p[0] == "T": title = p[1:4]
    elif p[0] == "R": rows[int(p[1])] = [p[2], p[3] if len(p) > 3 else ""]
    elif p[0] == "F": footer = p[1]; toast = None
    elif p[0] == "M": toast = (p[1], p[2])
print("mockups written")
