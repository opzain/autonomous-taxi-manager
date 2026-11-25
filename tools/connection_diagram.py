# Clean, crossing-free connection diagram of the ESP32 dispatch console (team's real parts).
# The ESP32 is drawn like the real 30-pin board held with USB-C at the bottom; every part sits
# beside the pins it uses, so every wire is a short straight line.
import html, sys
OUT = sys.argv[1]
S = []
def add(x): S.append(x)
def T(x, y, t, size=14, col="#1d2733", anchor="middle", weight="normal"):
    add(f'<text x="{x}" y="{y}" font-size="{size}" fill="{col}" text-anchor="{anchor}" font-weight="{weight}" font-family="Segoe UI,Arial,sans-serif">{html.escape(str(t))}</text>')
def L(pts, col, w=3):
    add('<polyline points="' + " ".join(f"{x},{y}" for x, y in pts) + f'" fill="none" stroke="{col}" stroke-width="{w}" stroke-linejoin="round" stroke-linecap="round"/>')
def BOX(x, y, w, h, fill, stroke, r=12):
    add(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="{r}" fill="{fill}" stroke="{stroke}" stroke-width="2"/>')
def DOT(x, y, col): add(f'<circle cx="{x}" cy="{y}" r="5" fill="{col}"/>')
def TAG(x, y, t, fill, anchor="start", tcol="#fff"):
    w = 8.2 * len(t) + 18
    x0 = x if anchor == "start" else x - w
    add(f'<rect x="{x0}" y="{y-13}" width="{w}" height="26" rx="13" fill="{fill}"/>')
    T(x0 + w / 2, y + 5, t, 13, tcol, "middle", "bold")
def GND(x, y):
    L([(x, y), (x, y + 10)], "#333", 3)
    for i, w in enumerate((22, 14, 6)):
        L([(x - w / 2, y + 10 + i * 5), (x + w / 2, y + 10 + i * 5)], "#333", 3)
def BUTTON(x, y, col, label):
    # Two terminals and a push bar (horizontal symbol), x = left terminal
    DOT(x, y, "#333"); DOT(x + 50, y, "#333")
    L([(x, y), (x + 8, y)], "#333", 3); L([(x + 42, y), (x + 50, y)], "#333", 3)
    L([(x + 8, y - 12), (x + 42, y - 12)], col, 4); L([(x + 25, y - 12), (x + 25, y - 22)], col, 4)
    T(x + 25, y - 26, label, 12, col, "middle", "bold")

W, H = 1600, 1060
add(f'<rect width="{W}" height="{H}" fill="#f7f8fa"/>')
T(40, 46, "ESP32 dispatch console — connection diagram", 26, "#111", "start", "bold")
T(40, 72, "Your parts: ESP32 DevKit V1 30-pin (USB-C at the bottom) · 2.8\" ILI9341 TFT · KY-040 encoder · 7 buttons · buzzer + 2N2222 + 1 kΩ · WS2812B-8 stick + 330 Ω", 14, "#556", "start")

# Power colours
C5, C33, CG = "#e67e22", "#e74c3c", "#2c3e50"

# ---------------- ESP32 ----------------
ex, ew, ey, pitch = 640, 260, 150, 44
left = ["EN", "VP", "VN", "D34", "D35", "D32", "D33", "D25", "D26", "D27", "D14", "D12", "D13", "GND", "VIN"]
right = ["D23", "D22", "TX0", "RX0", "D21", "D19", "D18", "D5", "TX2", "RX2", "D4", "D2", "D15", "GND", "3V3"]
used_l = {"D34", "D35", "D32", "D33", "D25", "D26", "D27", "D14", "D13", "GND", "VIN"}
used_r = {"D23", "D22", "D21", "D18", "D5", "TX2", "RX2", "D4", "GND", "3V3"}
BOX(ex, ey - 30, ew, pitch * 15 + 40, "#1f2a36", "#0f1720")
T(ex + ew / 2, ey + pitch * 1.25, "ESP32", 30, "#fff", "middle", "bold")
T(ex + ew / 2, ey + pitch * 1.25 + 26, "DevKit V1 · 30 pin", 14, "#9fb3c8")
add(f'<rect x="{ex+ew/2-28}" y="{ey+pitch*15-2}" width="56" height="34" rx="6" fill="#aab4be"/>')
T(ex + ew / 2, ey + pitch * 15 + 20, "USB-C", 12, "#111", "middle", "bold")
LY = {}
for i, p in enumerate(left):
    y = ey + i * pitch
    LY[p if p != "GND" else "GND.L"] = y
    c = "#ffffff" if p in used_l else "#6b7c8d"
    if p == "D12": c = "#ff8a80"
    add(f'<rect x="{ex-10}" y="{y-7}" width="14" height="14" fill="#d4af37"/>')
    T(ex + 16, y + 5, p + ("  (GPIO" + p[1:] + ")" if p.startswith("D") and p != "D12" else ""), 13, c, "start", "bold" if p in used_l else "normal")
RY = {}
gp = {"TX2": "GPIO17", "RX2": "GPIO16"}
for i, p in enumerate(right):
    y = ey + i * pitch
    RY[p if p != "GND" else "GND.R"] = y
    c = "#ffffff" if p in used_r else "#6b7c8d"
    add(f'<rect x="{ex+ew-4}" y="{y-7}" width="14" height="14" fill="#d4af37"/>')
    lab = p + (f"  ({gp[p]})" if p in gp else "")
    T(ex + ew - 16, y + 5, lab, 13, c, "end", "bold" if p in used_r else "normal")
T(ex + 16, LY["D12"] + 20, "do not use (boot pin)", 11, "#ff8a80", "start")
LX, RX = ex - 10, ex + ew + 10

# ---------------- LEFT: encoder + buttons ----------------
# Encoder block aligned with D34 / D35
bx = 300
BOX(bx, LY["D34"] - 60, 170, 120, "#263238", "#00acc1")
T(bx + 85, LY["D34"] - 36, "KY-040 encoder", 14, "#fff", "middle", "bold")
for pin, y in (("CLK", LY["D34"]), ("DT", LY["D35"])):
    T(bx + 158, y + 5, pin, 13, "#fff", "end", "bold")
    L([(bx + 170, y), (LX, y)], "#00acc1")
    DOT(LX, y, "#00acc1")
for i, (pin, tag, col) in enumerate((("+", "3V3", C33), ("GND", "GND", CG), ("SW", "→ ESP32 D4", "#00acc1"))):
    y = LY["D34"] - 22 + i * 30 + 10
    T(bx + 12, y + 5, pin, 13, "#fff", "start", "bold")
    L([(bx, y), (bx - 40, y)], col)
    TAG(bx - 42, y, tag, col, "end")
# Buttons D32..D13 (skip D12) to a GND bus
bus_x = 260
btns = [("D32", "UP"), ("D33", "DOWN"), ("D25", "LEFT"), ("D26", "RIGHT"), ("D27", "BACK"), ("D14", "ACCEPT"), ("D13", "REJECT")]
for gpio, name in btns:
    y = LY[gpio]
    BUTTON(470, y, "#27ae60", name)
    L([(520, y), (LX, y)], "#27ae60")
    L([(470, y), (bus_x, y)], CG)
    DOT(LX, y, "#27ae60")
L([(bus_x, LY["D32"]), (bus_x, LY["GND.L"] + 40)], CG)
GND(bus_x, LY["GND.L"] + 40)
T(bus_x - 8, LY["D33"] + 26, "GND bus", 12, CG, "end", "bold")
T(440, LY["VIN"] + 50, "Buttons: internal pull-ups, no resistors · always pressed? turn it 90°", 12, "#556", "middle")
# ESP32 GND / VIN (left)
L([(LX, LY["GND.L"]), (560, LY["GND.L"])], CG); GND(560, LY["GND.L"])
L([(LX, LY["VIN"]), (560, LY["VIN"])], C5); TAG(558, LY["VIN"], "5V (VIN)", C5, "end")

# ---------------- RIGHT: TFT, buzzer, LED stick ----------------
tx = 1200
tft_rows = [("SDI(MOSI)", "D23"), ("SCK", "D18"), ("CS", "D5"), ("RESET", "TX2"), ("D/C", "RX2")]
BOX(tx, RY["D23"] - 40, 220, RY["RX2"] - RY["D23"] + 80, "#c0392b", "#922b21")
T(tx + 110, RY["D23"] - 16, "2.8\" TFT  ILI9341", 14, "#fff", "middle", "bold")
colmap = {"D23": "#16a085", "D18": "#27ae60", "D5": "#2980b9", "TX2": "#8e44ad", "RX2": "#8e44ad"}
for pin, g in tft_rows:
    y = RY[g]
    T(tx + 14, y + 5, pin, 13, "#fff", "start", "bold")
    L([(RX, y), (tx, y)], colmap[g])
    DOT(RX, y, colmap[g])
for pin, tag, col, y in (("VCC", "5V", C5, RY["D22"]), ("GND", "GND", CG, RY["TX0"]), ("LED", "3V3", C33, RY["RX0"])):
    T(tx + 206, y + 5, pin, 13, "#fff", "end", "bold")
    L([(tx + 220, y), (tx + 270, y)], col)
    TAG(tx + 272, y, tag, col)
T(tx + 110, RY["RX2"] + 32, "MISO / touch / SD pins: not used", 11, "#fff")
T(tx + 110, RY["D19"] + 5, "jumper J1 on the back: leave OPEN", 11, "#ffe0d9")
# Buzzer driver on D21 (between ESP32 and TFT, rows D22..D19)
y21 = RY["D21"]
L([(RX, y21), (960, y21)], "#f1c40f"); DOT(RX, y21, "#f1c40f")
add(f'<rect x="960" y="{y21-9}" width="56" height="18" rx="3" fill="#fff" stroke="#b7950b" stroke-width="2"/>')
T(988, y21 - 14, "1 kΩ", 12, "#7d6608", "middle", "bold")
L([(1016, y21), (1062, y21)], "#f1c40f")
add(f'<circle cx="1080" cy="{y21}" r="24" fill="#fff" stroke="#333" stroke-width="2"/>')
L([(1062, y21 - 14), (1062, y21 + 14)], "#333", 4)
L([(1062, y21 - 6), (1088, y21 - 22)], "#333", 3); L([(1062, y21 + 6), (1088, y21 + 22)], "#333", 3)
T(1080, y21 + 44, "2N2222", 12, "#333", "middle", "bold")
T(1112, y21 - 22, "C", 11, "#333", "start", "bold"); T(1112, y21 + 30, "E", 11, "#333", "start", "bold"); T(1046, y21 - 4, "B", 11, "#333", "end", "bold")
L([(1088, y21 + 22), (1088, y21 + 52)], CG); GND(1088, y21 + 52)
L([(1088, y21 - 22), (1088, y21 - 50), (1130, y21 - 50)], "#333")
add(f'<circle cx="1150" cy="{y21-50}" r="17" fill="#222"/>')
T(1150, y21 - 46, "BZ", 11, "#fff", "middle", "bold")
T(1128, y21 - 56, "−", 13, "#333", "end", "bold"); T(1172, y21 - 56, "+", 13, C5, "start", "bold")
L([(1167, y21 - 50), (1178, y21 - 50), (1178, y21 - 78), (1120, y21 - 78)], C5); TAG(1118, y21 - 78, "5V", C5, "end")
T(990, y21 + 72, "Buzzer driver", 13, "#7d6608", "middle", "bold")
# D4 -> encoder SW tag
L([(RX, RY["D4"]), (960, RY["D4"])], "#00acc1"); DOT(RX, RY["D4"], "#00acc1"); TAG(962, RY["D4"], "→ Encoder SW", "#00acc1")
# D22 -> 330 ohm -> LED stick IN (stick drawn lower right, in the free D15 rows)
y22 = RY["D22"]
L([(RX, y22), (960, y22)], "#8e44ad"); DOT(RX, y22, "#8e44ad")
add(f'<rect x="960" y="{y22-9}" width="56" height="18" rx="3" fill="#fff" stroke="#6c3483" stroke-width="2"/>')
T(988, y22 - 14, "330 Ω", 12, "#6c3483", "middle", "bold")
L([(1016, y22), (1040, y22)], "#8e44ad"); TAG(1042, y22, "→ LED IN", "#8e44ad")
ys = RY["D2"] - 10
BOX(1090, ys, 400, 96, "#2c1338", "#8e44ad", 10)
T(1290, ys + 22, "WS2812B-8 LED stick (IN end)", 14, "#fff", "middle", "bold")
for i in range(8):
    add(f'<circle cx="{1150 + i * 40}" cy="{ys + 46}" r="11" fill="#{["e74c3c","e67e22","f1c40f","2ecc71","1abc9c","3498db","9b59b6","ecf0f1"][i]}"/>')
for i, (pin, tag, col) in enumerate((("IN", "← 330 Ω (D22)", "#8e44ad"), ("VCC", "5V", C5), ("GND", "GND", CG))):
    xx = 1110 + i * 155
    T(xx, ys + 84, pin, 12, "#fff", "start", "bold")
    L([(xx + 4, ys + 96), (xx + 4, ys + 126)], col)
    TAG(xx + 10, ys + 126, tag, col)
T(1290, ys - 10, "solder a header to the GND / VCC / IN pads first", 11, "#6c3483", "middle", "bold")
# GND / 3V3 right
L([(RX, RY["GND.R"]), (960, RY["GND.R"])], CG); GND(960, RY["GND.R"])
L([(RX, RY["3V3"]), (960, RY["3V3"])], C33); TAG(962, RY["3V3"], "3V3", C33)
# Free pins notes
for p, note in (("D15", "free (was INFO; boot pin)"), ("TX0", "USB serial to the game (keep free)"), ("D19", "free (touch MISO later)"), ("D2", "free (onboard blue LED)")):
    T(RX + 12, RY[p] - 9, note, 10, "#8a97a6", "start")

# ---------------- Legend ----------------
ly = 900
BOX(40, ly, 1520, 140, "#ffffff", "#d5dbe1")
T(60, ly + 30, "How to read it", 15, "#111", "start", "bold")
items = [(C5, "5V tag = ESP32 VIN pin (USB power) → TFT VCC, buzzer +, LED stick VCC"), (C33, "3V3 tag = ESP32 3V3 pin → encoder +, TFT LED (never 5V on the encoder)"),
         (CG, "Ground symbol = any GND pin; all grounds are joined"), ("#00acc1", "Tags with → are the same wire drawn in two places (e.g. ESP32 D4 ↔ encoder SW)")]
for i, (c, t) in enumerate(items):
    yy = ly + 58 + (i % 2) * 34
    xx = 60 + (i // 2) * 760
    add(f'<rect x="{xx}" y="{yy-12}" width="22" height="22" rx="11" fill="{c}"/>')
    T(xx + 32, yy + 4, t, 13, "#333", "start")
T(60, ly + 128, "Breadboard positions for every wire: docs/images/breadboard-layout.png and the wire table in docs/03-hardware.md.", 12, "#667", "start")
open(OUT, "w").write(f'<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}">' + "".join(S) + "</svg>")
print("written", OUT)
