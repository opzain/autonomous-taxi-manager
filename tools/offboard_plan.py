# Breadboard plan v0.15 (8 panel push buttons; INFO on D19; two joysticks; knob removed, right-stick click on D4) — based on v0.10.6 — ESP32 OFF the breadboard (the team's 30-pin board is 1" wide: on their breadboard
# it covers rows b..j and leaves no free hole beside one header). Every ESP32 connection is a jumper with its
# FEMALE end pushed onto the ESP32 pin. The breadboard holds the buttons, buzzer driver, 330 ohm and the rails.
# 1) builds the hole-by-hole plan, 2) VERIFIES the netlist, 3) renders the SVG + a markdown wire list.
import html, sys

OUT_SVG, OUT_MD = sys.argv[1], sys.argv[2]
ROWS_TOP = "abcde"

def strip(h):
    if h[0] == "R": return ("R", h[1])
    return ("S", h[2], "top" if h[1] in ROWS_TOP else "bot")
def H(r, c): return ("H", r, c)
def R(r, c): return ("R", r, c)
def E(p): return ("P", "ESP32", p)          # an ESP32 pin (off-board, female jumper end on it)
def X(dev, p): return ("X", dev, p)         # a pin on the TFT / encoder / joystick (female end on it)
def LUG(btn, n): return ("L", "BTN_" + btn, n)   # a panel-button solder lug (MALE pin pushed through its hole, or soldered)

LEFT_HDR = ["EN", "VP", "VN", "D34", "D35", "D32", "D33", "D25", "D26", "D27", "D14", "D12", "D13", "GND.L", "VIN"]
RIGHT_HDR = ["D23", "D22", "TX0", "RX0", "D21", "D19", "D18", "D5", "TX2", "RX2", "D4", "D2", "D15", "GND.R", "3V3"]

# ---------------- parts on the breadboard ----------------
# ESP32 lies LEFT of the breadboard, turned so USB-C points LEFT (away from the board). Seen from above, its
# EN..VIN header is then the TOP edge (VIN at the left) and its D23..3V3 header the BOTTOM edge (3V3 at the left).
TOP_ROW = ["VIN", "GND.L", "D13", "D12", "D14", "D27", "D26", "D25", "D33", "D32", "D35", "D34", "VN", "VP", "EN"]
BOT_ROW = ["3V3", "GND.R", "D15", "D2", "D4", "RX2", "TX2", "D5", "D18", "D19", "D21", "RX0", "TX0", "D22", "D23"]
pins = {}
# 12 mm panel push buttons (2 lugs, no polarity): lug 1 -> ESP32 pin, lug 2 -> top blue rail (GND) at column g
buttons = [("UP", "D15", "UP", 4), ("DOWN", "D22", "DN", 7), ("LEFT", "D25", "LT", 10), ("RIGHT", "D26", "RT", 13),
           ("BACK", "D27", "BK", 16), ("ACCEPT", "D14", "AC", 19), ("REJECT", "D13", "RJ", 22), ("INFO", "D19", "IN", 25)]
# Buzzer driver in the top half: 2N2222 E-B-C in c45 / c46 / c47; 1 kΩ from h46 (D21 strip) across the gap to d46 (base)
pins[("Q1", "E")], pins[("Q1", "B")], pins[("Q1", "C")] = H("c", 45), H("c", 46), H("c", 47)
pins[("R1k", "1")], pins[("R1k", "2")] = H("h", 46), H("d", 46)
pins[("BUZZER", "-")], pins[("BUZZER", "+")] = H("b", 47), H("b", 50)

# ---------------- wires ----------------
# Rails: top red = 5V (VIN), top blue = GND, bottom red = 3V3, bottom blue = GND
wires = []   # (a, b, kind, label)  kind: M-F (female end = a), F-F, M-M
def W(a, b, kind, label): wires.append((a, b, kind, label))
W(E("VIN"), R("T+", 53), "M-F", "ESP32 VIN → top red rail (5V)")
W(E("GND.L"), R("T-", 52), "M-F", "ESP32 GND (next to VIN) → top blue rail (GND)")
for name, gpio, tag, g in buttons:
    W(E(gpio), LUG(name, "1"), "M-F", f"ESP32 {gpio} → {name} button lug 1 (tag {tag})")
W(E("3V3"), R("B+", 53), "M-F", "ESP32 3V3 → bottom red rail (3V3)")
W(E("GND.R"), R("B-", 52), "M-F", "ESP32 GND (next to 3V3) → bottom blue rail (GND)")
W(E("D21"), H("j", 46), "M-F", "ESP32 D21 → j46 (1 kΩ, buzzer)")
# devices -> breadboard (female on the device pin)
DEVNAME = {"TFT": "TFT", "ENC": "Encoder", "JL": "Left stick", "JR": "Right stick"}
for dev, col in (("JL", 38), ("JR", 39)):
    W(X(dev, "GND"), R("B-", col), "M-F", f"{DEVNAME[dev]} GND → bottom blue rail (GND)")
    W(X(dev, "+5V"), R("B+", col), "M-F", f"{DEVNAME[dev]} +5V pin → bottom red rail (3V3, NOT 5V)")
W(X("TFT", "VCC"), R("T+", 42), "M-F", "TFT VCC → top red rail (5V; J1 stays open)")
W(X("TFT", "GND"), R("T-", 39), "M-F", "TFT GND → top blue rail (GND)")
W(X("TFT", "LED"), R("B+", 42), "M-F", "TFT LED → bottom red rail (3V3)")
# direct device <-> ESP32 (female-female, no breadboard)
for dev, p, g in (("TFT", "CS", "D5"), ("TFT", "RESET", "TX2"), ("TFT", "D/C", "RX2"), ("TFT", "SDI(MOSI)", "D23"),
                  ("TFT", "SCK", "D18"), ("JR", "SW", "D4"),
                  ("JL", "VRx", "D32"), ("JL", "VRy", "D33"), ("JR", "VRx", "VP"), ("JR", "VRy", "VN")):
    W(X(dev, p), E(g), "F-F", f"{DEVNAME[dev]} {p} ↔ ESP32 {g}" + (" (GPIO17)" if g == "TX2" else " (GPIO16)" if g == "RX2" else " (GPIO36)" if g == "VP" else " (GPIO39)" if g == "VN" else ""))
# on-board links (male-male)
for name, gpio, tag, g in buttons:
    W(LUG(name, "2"), R("T-", g), "M-M", f"{name} button lug 2 → top blue rail (GND)")
W(H("a", 45), R("T-", 45), "M-M", "Transistor emitter strip a45 → top blue rail (GND)")
W(H("a", 50), R("T+", 50), "M-M", "Buzzer + strip a50 → top red rail (5V)")

# ---------------- verification ----------------
class DSU:
    def __init__(s): s.p = {}
    def f(s, x):
        s.p.setdefault(x, x)
        while s.p[x] != x: s.p[x] = s.p[s.p[x]]; x = s.p[x]
        return x
    def u(s, a, b): s.p[s.f(a)] = s.f(b)
def node(e): return strip(e) if e[0] in "HR" else e
def build(pressed):
    d = DSU()
    for (part, pin), h in pins.items(): d.u(("P", part, pin), strip(h))
    d.u(E("GND.L"), E("GND.R"))
    for a, b, *_ in wires: d.u(node(a), node(b))
    if pressed:
        for name, *_ in buttons: d.u(LUG(name, "1"), LUG(name, "2"))   # a 2-lug push button just joins its lugs
    return d
problems = []
same = lambda d, a, b: d.f(a) == d.f(b)
for pressed in (False, True):
    d = build(pressed)
    for a, b in (("3V3", "GND.R"), ("VIN", "GND.R"), ("3V3", "VIN")):
        if same(d, E(a), E(b)): problems.append(f"SHORT {a}-{b} (pressed={pressed})")
    for name, gpio, *_ in buttons:
        if same(d, E(gpio), E("GND.R")) != pressed: problems.append(f"Button {name} wrong (pressed={pressed})")
        if same(d, E(gpio), E("VIN")) or same(d, E(gpio), E("3V3")): problems.append(f"Button {name} on a power rail")
        for _, g2, *_ in buttons:
            if not pressed and g2 != gpio and same(d, E(gpio), E(g2)): problems.append(f"{gpio} shorted to {g2}")
d = build(False)
expect = [(X("TFT", "VCC"), E("VIN")), (X("TFT", "GND"), E("GND.R")), (X("TFT", "CS"), E("D5")), (X("TFT", "RESET"), E("TX2")),
          (X("TFT", "D/C"), E("RX2")), (X("TFT", "SDI(MOSI)"), E("D23")), (X("TFT", "SCK"), E("D18")), (X("TFT", "LED"), E("3V3")),
          (X("JR", "SW"), E("D4")), (("P", "R1k", "1"), E("D21")), (("P", "R1k", "2"), ("P", "Q1", "B")),
          (("P", "Q1", "E"), E("GND.R")), (("P", "Q1", "C"), ("P", "BUZZER", "-")), (("P", "BUZZER", "+"), E("VIN")),
          (X("JL", "VRx"), E("D32")), (X("JL", "VRy"), E("D33")), (X("JR", "VRx"), E("VP")), (X("JR", "VRy"), E("VN")),
          (X("JL", "+5V"), E("3V3")), (X("JR", "+5V"), E("3V3")), (X("JL", "GND"), E("GND.R")), (X("JR", "GND"), E("GND.R"))]
for a, b in expect:
    if not same(d, a, b): problems.append(f"MISSING {a} ↔ {b}")
for dev in ("JL", "JR"):     # the ESP32 can only read up to 3.3 V: a stick on 5V would overdrive its pins
    if same(d, X(dev, "+5V"), E("VIN")): problems.append(f"{dev} powered from 5V")
for name, *_ in buttons: expect.append((LUG(name, "2"), E("GND.R")))   # every button GND lug reaches GND
for a, b in expect[-len(buttons):]:
    if not same(d, a, b): problems.append(f"MISSING {a} ↔ {b}")
for p in ("D12", "D2", "TX0", "RX0", "EN", "D34", "D35"):     # D34 / D35: the old knob pins, now free
    if any(d.f(x) == d.f(E(p)) and x != E(p) for x in list(d.p)): problems.append(f"{p} must stay free")
# one female end per ESP32 / device pin, holes used once, nothing under a button body
ends = {}
for a, b, kind, label in wires:
    for e in (a, b):
        if e[0] in "PX":
            if e in ends: problems.append(f"Two wires on {e[1]} {e[2]}")
            ends[e] = label
used = {}
for (part, pin), h in pins.items():
    if h in used: problems.append(f"Hole {h} used twice")
    used[h] = part
for a, b, *_ in wires:
    for e in (a, b):
        if e[0] == "H":
            if e in used: problems.append(f"Hole {e[1]}{e[2]} used twice ({used[e]})")
            used[e] = "wire"
for h in used:
    if not 1 <= h[2] <= 63: problems.append(f"Hole {h} off the board")
rail_used = {}
for a, b, *_ in wires:
    for e in (a, b):
        if e[0] == "R":
            if e in rail_used: problems.append(f"Rail hole {e[1]}{e[2]} used twice")
            rail_used[e] = True
# wire kinds consistent with the ends (female on ESP32/device pins, male in holes/rails)
for a, b, kind, label in wires:
    fem = [e[0] in "PX" for e in (a, b)]
    want = {"M-F": 1, "F-F": 2, "M-M": 0}[kind]
    if sum(fem) != want: problems.append(f"Wire kind wrong: {label}")
if problems:
    print("VERIFICATION FAILED"); [print("  ", p) for p in problems]; sys.exit(1)
cnt = {k: sum(w[2] == k for w in wires) for k in ("M-F", "F-F", "M-M")}
print(f"VERIFIED: {len(wires)} wires ({cnt['M-F']} male-female, {cnt['F-F']} female-female, {cnt['M-M']} male-male), "
      "8 panel buttons pressed and released, no shorts, no button on a power rail.")

# ---------------- render: every ESP32 wire drawn as a line, nested so none cross ----------------
S = []
add = S.append
def text(x, y, t, size=12, col="#222", anchor="middle", weight="normal"):
    add(f'<text x="{x}" y="{y}" font-size="{size}" fill="{col}" text-anchor="{anchor}" font-weight="{weight}" font-family="Segoe UI,Arial,sans-serif">{html.escape(str(t))}</text>')
SEGS = []
def line(pts, col, w=3.4, track=False):
    if track: SEGS.append(pts)
    add('<polyline points="' + " ".join(f"{x},{y}" for x, y in pts) + f'" fill="none" stroke="{col}" stroke-width="{w}" stroke-linejoin="round" stroke-linecap="round"/>')
def dot(x, y, col): add(f'<circle cx="{x}" cy="{y}" r="4.2" fill="{col}" stroke="#fff" stroke-width="1.2"/>')
NCOL = 53
X0, DX = 540, 22
cx = lambda c: X0 + (c - 1) * DX
ROWY = {"T+": 268, "T-": 290, "a": 326, "b": 346, "c": 366, "d": 386, "e": 406, "f": 450, "g": 470, "h": 490, "i": 510, "j": 530, "B+": 566, "B-": 588}
pos = lambda e: (cx(e[2]), ROWY[e[1]])
W_, H_ = 1780, 1470
add(f'<rect width="{W_}" height="{H_}" fill="#f6f4ef"/>')
text(30, 38, "ESP32 dispatch console — wiring with the ESP32 beside the breadboard", 24, "#111", "start", "bold")
text(30, 62, "Every line that starts at the ESP32 is a male–female wire: FEMALE end pushed onto the ESP32 pin, MALE end into the breadboard hole where the line ends. No two lines cross.", 12.5, "#555", "start")
text(30, 80, "Yellow letters A–J = female–female wires from the ESP32 straight to the screen / joysticks. Numbered dots = their power wires (male end goes there). Blue tags (UP DN LT RT BK AC RJ IN) = panel buttons, see the bottom.", 12.5, "#555", "start")
# board
bx1, bx2 = X0 - 34, cx(NCOL) + 30
add(f'<rect x="{bx1}" y="250" width="{bx2-bx1}" height="356" rx="10" fill="#fdfdfb" stroke="#c9c5bb" stroke-width="2"/>')
add(f'<rect x="{bx1}" y="421" width="{bx2-bx1}" height="14" fill="#e9e5dc"/>')
RC = {"T+": "#e74c3c", "T-": "#2c6fbb", "B+": "#e74c3c", "B-": "#2c6fbb"}
RL = {"T+": "5V", "T-": "GND", "B+": "3V3", "B-": "GND"}
for rail in RC:
    y = ROWY[rail]; yy = y + (-10 if "+" in rail else 10)
    add(f'<line x1="{X0-20}" y1="{yy}" x2="{cx(NCOL)+10}" y2="{yy}" stroke="{RC[rail]}" stroke-width="2"/>')
    for c in range(1, NCOL + 1): add(f'<rect x="{cx(c)-3}" y="{y-3}" width="6" height="6" rx="1" fill="#a8a39a"/>')
    text(bx2 + 8, y + 4, RL[rail], 12, RC[rail], "start", "bold")
for r in "abcdefghij":
    text(X0 - 22, ROWY[r] + 4, r, 11, "#999")
    for c in range(1, NCOL + 1): add(f'<rect x="{cx(c)-3}" y="{ROWY[r]-3}" width="6" height="6" rx="1" fill="#c4c0b6"/>')
for c in (1, 5, 10, 15, 20, 25, 30, 35, 40, 45, 50): text(cx(c), ROWY["a"] - 12, c, 9, "#aaa")
text(cx(18), (ROWY["e"] + ROWY["f"]) / 2 + 5, "No buttons on the breadboard any more: the 8 panel buttons are wired straight to the ESP32 and the GND rail (bottom of the page).", 11, "#9a9286", "middle")
# transistor (top half): FLAT face down = towards row e = towards YOU; rounded back towards row a
cy = ROWY["c"]
add(f'<path d="M {cx(45)-10} {cy+7} L {cx(45)-10} {cy-9} Q {cx(46)} {cy-22} {cx(47)+10} {cy-9} L {cx(47)+10} {cy+7} Z" fill="#222"/>')
add(f'<line x1="{cx(45)-11}" y1="{cy+8}" x2="{cx(47)+11}" y2="{cy+8}" stroke="#f1c40f" stroke-width="3"/>')
for c, l in ((45, "E"), (46, "B"), (47, "C")):
    add(f'<circle cx="{cx(c)}" cy="{cy}" r="3.2" fill="#d9d9d9"/>'); text(cx(c), cy - 6, l, 9.5, "#fff", "middle", "bold")
text(cx(44) - 4, cy + 2, "2N2222", 10, "#222", "end", "bold")
text(cx(44) - 4, cy + 14, "flat face (yellow) → towards you", 8.5, "#9a7d0a", "end", "bold")
add(f'<line x1="{cx(46)}" y1="{ROWY["d"]}" x2="{cx(46)}" y2="{ROWY["h"]}" stroke="#888" stroke-width="2"/>')
add(f'<rect x="{cx(46)-6}" y="{ROWY["f"]-14}" width="12" height="34" rx="4" fill="#d8c08a" stroke="#97824a"/>')
text(cx(46) + 10, ROWY["f"] + 8, "1 kΩ", 10, "#6b5a2a", "start", "bold")
# buzzer: body between its legs so every hole stays visible; legs drawn to b47 (−) and b50 (+)
bxm, by = (cx(47) + cx(50)) / 2, ROWY["b"]
add(f'<line x1="{cx(47)}" y1="{by}" x2="{bxm-14}" y2="{by}" stroke="#555" stroke-width="2.5"/>')
add(f'<line x1="{cx(50)}" y1="{by}" x2="{bxm+14}" y2="{by}" stroke="#555" stroke-width="2.5"/>')
add(f'<circle cx="{bxm}" cy="{by}" r="19" fill="#222" fill-opacity="0.9"/>')
add(f'<circle cx="{bxm}" cy="{by}" r="4" fill="#555"/>')
text(bxm, by - 23, "BUZZER", 8.5, "#222", "middle", "bold")
for c in (47, 50): add(f'<circle cx="{cx(c)}" cy="{by}" r="3.8" fill="#d9d9d9" stroke="#555" stroke-width="1.5"/>')
text(cx(47) - 7, by - 6, "−", 13, "#222", "end", "bold"); text(cx(50) + 7, by - 6, "+", 13, "#c0392b", "start", "bold")
# on-board male-male links
def tag(x, y, t):
    add(f'<rect x="{x-10}" y="{y-8}" width="20" height="16" rx="3" fill="#1e5aa8" stroke="#fff" stroke-width="1"/>'); text(x, y + 3.5, t, 8.5, "#fff", "middle", "bold")
TAG_OF = {name: t for name, gpio, t, g in buttons}
for a, b, kind, label in wires:
    if kind != "M-M": continue
    if a[0] == "L":    # button GND lug -> rail: a small tag sitting on the rail hole
        xb, yb = pos(b); add(f'<circle cx="{xb}" cy="{yb}" r="4.5" fill="#222"/>')
        add(f'<rect x="{xb-12}" y="{yb-27}" width="24" height="15" rx="3" fill="#37474f"/>'); text(xb, yb - 16, TAG_OF[a[1][4:]], 9, "#fff", "middle", "bold")
        continue
    (xa, ya), (xb, yb) = pos(a), pos(b)
    col = "#222" if "GND" in label else "#e67e22"
    line([(xa, ya), (xb, yb)], col); dot(xa, ya, col); dot(xb, yb, col)
# ESP32 (left of the board, USB-C pointing left)
EX0, EP = 120, 22
ex = lambda i: EX0 + i * EP
ETOP, EBOT = ROWY["b"], ROWY["i"]
add(f'<rect x="{ex(0)-18}" y="{ETOP-20}" width="{ex(14)-ex(0)+36}" height="{EBOT-ETOP+40}" rx="10" fill="#1b1f24"/>')
add(f'<rect x="{ex(0)-48}" y="{(ETOP+EBOT)/2-17}" width="32" height="34" rx="5" fill="#a6b0b6"/>')
text(ex(0) - 32, (ETOP + EBOT) / 2 + 4, "USB", 9, "#111", "middle", "bold")
text((ex(0) + ex(14)) / 2, (ETOP + EBOT) / 2 - 4, "ESP32 DevKit V1", 15, "#fff", "middle", "bold")
text((ex(0) + ex(14)) / 2, (ETOP + EBOT) / 2 + 14, "beside the board · USB-C points left", 10.5, "#9fb3c8", "middle")
WIRECOL = {"VIN": "#ff9933", "GND.L": "#222", "GND.R": "#222", "3V3": "#e74c3c", "D21": "#d4ac0d", "D22": "#8e44ad",
           "D32": "#2980b9", "D33": "#16a085", "D25": "#8e44ad", "D26": "#d35400", "D27": "#7f8c8d", "D14": "#27ae60", "D13": "#c0392b"}
L = "ABCDEFGHIJKL"
BADGE = {E(gpio): t for name, gpio, t, g in buttons}   # button wires: tag at the ESP32 pin = tag on the button
letter_of = {}
for i, (a, b, kind, label) in enumerate([w for w in wires if w[2] == "F-F"]): letter_of[a] = letter_of[b] = L[i]
def esp_pin(i, p, top):
    x, y = ex(i), (ETOP if top else EBOT)
    add(f'<circle cx="{x}" cy="{y}" r="4.5" fill="#d4af37"/>')
    used_pin = any(E(p) in (w[0], w[1]) for w in wires)
    lab = p.split(".")[0]
    text(x, y + (18 if top else -10), lab, 8.5, "#ff8a80" if p == "D12" else ("#fff" if used_pin else "#6b7c8d"), "middle", "bold")
    if E(p) in letter_of:
        yy = y - 20 if top else y + 20
        add(f'<rect x="{x-9}" y="{yy-9}" width="18" height="18" rx="4" fill="#f1c40f"/>'); text(x, yy + 4, letter_of[E(p)], 10.5, "#222", "middle", "bold")
for i, p in enumerate(TOP_ROW): esp_pin(i, p, True)
for i, p in enumerate(BOT_ROW): esp_pin(i, p, False)
# top U wires (up, right, down): leftmost pin = outermost lane
top_w = [w for w in wires if w[2] == "M-F" and w[0][0] == "P" and w[0][2] in TOP_ROW and w[0] not in BADGE]
top_w.sort(key=lambda w: TOP_ROW.index(w[0][2]))
for depth, (a, b, kind, label) in enumerate(top_w):
    x1 = ex(TOP_ROW.index(a[2])); x2, y2 = pos(b)
    yc = 118 + depth * 14
    col = WIRECOL[a[2]]
    line([(x1, ETOP), (x1, yc), (x2, yc), (x2, y2)], col, track=True); dot(x1, ETOP, col); dot(x2, y2, col)
    tgt = label.split("→ ")[1]
    text(x2 - 6, yc - 3, f"{a[2].split('.')[0]} → {tgt}", 9.5, col, "end", "bold")
# bottom U wires (down, right, up): leftmost pin = outermost lane (lowest)
bot_w = [w for w in wires if w[2] == "M-F" and w[0][0] == "P" and w[0][2] in BOT_ROW and w[0] not in BADGE]
bot_w.sort(key=lambda w: BOT_ROW.index(w[0][2]))
n_b = len(bot_w)
for depth, (a, b, kind, label) in enumerate(bot_w):
    x1 = ex(BOT_ROW.index(a[2])); x2, y2 = pos(b)
    yc = 620 + (n_b - 1 - depth) * 16 + 14
    col = WIRECOL[a[2]]
    line([(x1, EBOT), (x1, yc), (x2, yc), (x2, y2)], col, track=True); dot(x1, EBOT, col); dot(x2, y2, col)
    tgt = label.split("→ ")[1]
    text(x2 - 6, yc + 12, f"{a[2].split('.')[0]} → {tgt}", 9.5, col, "end", "bold")
# button tags at the ESP32 pins (the same tag is printed next to each panel button at the bottom)
for a, b, kind, label in wires:
    if a in BADGE:
        if a[2] in BOT_ROW: tag(ex(BOT_ROW.index(a[2])), EBOT + 22, BADGE[a])
        else: tag(ex(TOP_ROW.index(a[2])), ETOP - 22, BADGE[a])
# drawing check: no two ESP32 wires cross or overlap
def segs(pts): return list(zip(pts, pts[1:]))
def hit(s1, s2):
    (a, b), (c, d) = s1, s2
    h1, h2 = a[1] == b[1], c[1] == d[1]
    if h1 == h2:   # parallel: overlap only if collinear and ranges overlap
        if h1 and a[1] == c[1]: return max(min(a[0], b[0]), min(c[0], d[0])) <= min(max(a[0], b[0]), max(c[0], d[0]))
        if not h1 and a[0] == c[0]: return max(min(a[1], b[1]), min(c[1], d[1])) <= min(max(a[1], b[1]), max(c[1], d[1]))
        return False
    if not h1: (a, b), (c, d) = (c, d), (a, b)
    return min(a[0], b[0]) <= c[0] <= max(a[0], b[0]) and min(c[1], d[1]) <= a[1] <= max(c[1], d[1])
cross = [(i, j) for i in range(len(SEGS)) for j in range(i + 1, len(SEGS)) if any(hit(p, q) for p in segs(SEGS[i]) for q in segs(SEGS[j]))]
if cross: print("DRAWING: wires cross:", cross); sys.exit(1)
print(f"DRAWING: {len(SEGS)} ESP32 wires drawn, none cross.")
# numbered badges for the device power / LED-IN wires
FILL = {"TFT": "#c0392b", "ENC": "#0097a7", "JL": "#37474f", "JR": "#5d4037"}
badge_of, num = {}, 1
for a, b, kind, label in wires:
    if kind == "M-F" and a[0] == "X":
        badge_of[a] = num
        x, y = pos(b)
        add(f'<circle cx="{x}" cy="{y}" r="9.5" fill="{FILL[a[1]]}" stroke="#fff" stroke-width="1.5"/>')
        text(x, y + 4, num, 10, "#fff", "middle", "bold"); num += 1
def card(x, y, title, order, dev, fill):
    w = 34 * len(order) + 30
    add(f'<rect x="{x}" y="{y}" width="{w}" height="130" rx="10" fill="{fill}"/>')
    text(x + w / 2, y + 22, title, 13, "#fff", "middle", "bold")
    for i, p in enumerate(order):
        px = x + 32 + i * 34
        add(f'<rect x="{px-5}" y="{y+34}" width="10" height="10" fill="#d4af37"/>')
        add(f'<text x="{px}" y="{y+56}" font-size="10" fill="#fff" text-anchor="end" font-weight="bold" font-family="Segoe UI,Arial,sans-serif" transform="rotate(-60 {px} {y+56})">{html.escape(p.strip())}</text>')
        e = X(dev, p.strip())
        if p == p.strip() and e in badge_of:
            add(f'<circle cx="{px}" cy="{y+112}" r="10" fill="#fff"/>'); text(px, y + 116, badge_of[e], 10, fill, "middle", "bold")
        elif p == p.strip() and e in letter_of:
            add(f'<rect x="{px-10}" y="{y+102}" width="20" height="20" rx="4" fill="#f1c40f"/>'); text(px, y + 116, letter_of[e], 11, "#222", "middle", "bold")
        else: text(px, y + 116, "–", 12, "#ffffffaa")
    return w
y0 = 730
w1 = card(40, y0, "2.8\" TFT pins", ["VCC", "GND", "CS", "RESET", "D/C", "SDI(MOSI)", "SCK", "LED", "SDO(MISO)", "T_CLK", "T_CS", "T_DIN", "T_OUT", "T_IRQ"], "TFT", "#c0392b")
w2 = -20   # (the KY-040 knob card is gone since v0.15)
w3a = card(40 + w1 + w2 + 40, y0, "LEFT joystick", ["GND", "+5V", "VRx", "VRy", "SW"], "JL", "#37474f")
w3b = card(40 + w1 + w2 + w3a + 60, y0, "RIGHT joystick (SW = NEXT)", ["GND", "+5V", "VRx", "VRy", "SW"], "JR", "#5d4037")
w3 = w3a + w3b + 20
# ---- buzzer detail panel ----
px0, py0, pw, ph = 40 + w1 + w2 + w3 + 70, y0, 1740 - (40 + w1 + w2 + w3 + 70), 130
add(f'<rect x="{px0}" y="{py0}" width="{pw}" height="{ph}" rx="10" fill="#fff" stroke="#d4ac0d" stroke-width="2"/>')
text(px0 + 14, py0 + 22, "BUZZER CIRCUIT — check hole by hole", 13, "#7d6608", "start", "bold")
# mini TO-92, flat face towards the reader
tx, tyy = px0 + 60, py0 + 70
add(f'<path d="M {tx-26} {tyy+8} L {tx-26} {tyy-2} Q {tx} {tyy-30} {tx+26} {tyy-2} L {tx+26} {tyy+8} Z" fill="#222"/>')
add(f'<line x1="{tx-28}" y1="{tyy+9}" x2="{tx+28}" y2="{tyy+9}" stroke="#f1c40f" stroke-width="3"/>')
for i, l in enumerate("EBC"):
    lx = tx - 16 + i * 16
    add(f'<line x1="{lx}" y1="{tyy+9}" x2="{lx}" y2="{tyy+34}" stroke="#888" stroke-width="2.5"/>'); text(lx, tyy + 48, l, 11, "#222", "middle", "bold")
text(tx, tyy - 34, "flat face = you", 9.5, "#9a7d0a", "middle", "bold")
rows = ["2N2222 / PN2222A / S8050: E-B-C → E c45, B c46, C c47",
        "P2N2222A / BC547 read C-B-E → swap the two outer legs",
        "1 kΩ: d46 ↔ h46 (must cross the gap) · ESP32 D21 → j46",
        "Buzzer: short leg (−) b47, long leg (+) b50",
        "a45 → GND (top blue) · a50 → 5V (top red)"]
for i, t in enumerate(rows): text(px0 + 112, py0 + 46 + i * 17, "• " + t, 11, "#222", "start")
ty = 892
text(40, ty, "Numbered dots: female end on the device pin, male end where the dot is", 13, "#111", "start", "bold")
devw = [w for w in wires if w[2] == "M-F" and w[0][0] == "X"]
for i, (a, b, kind, label) in enumerate(devw):
    xx, yy = 40 + (i // 5) * 320, ty + 26 + (i % 5) * 20
    add(f'<circle cx="{xx+8}" cy="{yy-4}" r="8" fill="{FILL[a[1]]}"/>'); text(xx + 8, yy, i + 1, 9, "#fff", "middle", "bold")
    text(xx + 22, yy, label.split(" (")[0], 11, "#222", "start")
fx = 700
text(fx, ty, "Letters: female–female, ESP32 pin ↔ device pin (no breadboard)", 13, "#111", "start", "bold")
for i, (a, b, kind, label) in enumerate([w for w in wires if w[2] == "F-F"]):
    xx, yy = fx + (i // 4) * 330, ty + 26 + (i % 4) * 20
    add(f'<rect x="{xx}" y="{yy-12}" width="16" height="16" rx="3" fill="#f1c40f"/>'); text(xx + 8, yy, L[i], 10, "#222", "middle", "bold")
    text(xx + 22, yy, label, 11, "#222", "start")
# ---- the 8 panel push buttons ----
py = 1030
text(40, py, "Panel push buttons (12 mm, 2 lugs, no polarity) — each needs 2 wires", 15, "#111", "start", "bold")
text(40, py + 20, "Lug 1 → its ESP32 pin with a male–female wire (female on the ESP32, male pin pushed through the lug hole and bent, or soldered).  Lug 2 → top blue rail (GND) with a male–male wire.", 12, "#555", "start")
for i, (name, gpio, t, g) in enumerate(buttons):
    bxp = 106 + i * 208; byp = py + 95
    add(f'<rect x="{bxp-56}" y="{byp-58}" width="198" height="142" rx="10" fill="#fff" stroke="#d6d1c7" stroke-width="1.5"/>')
    add(f'<circle cx="{bxp}" cy="{byp}" r="25" fill="#222"/>'); add(f'<circle cx="{bxp}" cy="{byp}" r="16" fill="#e74c3c"/>')
    text(bxp, byp + 4, t, 11, "#fff", "middle", "bold")
    text(bxp + 35, byp - 38, name, 13, "#111", "start", "bold")
    for k, (lx, lab, col) in enumerate(((bxp - 9, "1", "#1e5aa8"), (bxp + 9, "2", "#222"))):
        add(f'<rect x="{lx-3}" y="{byp+25}" width="6" height="16" fill="#b0b0b0"/>'); text(lx, byp + 55, lab, 10, "#555", "middle", "bold")
    tag(bxp + 52, byp - 10, t); text(bxp + 64, byp - 6, f"lug 1 → {gpio}", 11, "#1e5aa8", "start", "bold")
    add(f'<rect x="{bxp+40}" y="{byp+8}" width="24" height="15" rx="3" fill="#37474f"/>'); text(bxp + 52, byp + 19, t, 9, "#fff", "middle", "bold")
    text(bxp + 64, byp + 20, "lug 2 → GND", 11, "#222", "start", "bold")
    text(bxp + 64, byp + 34, f"top blue, col {g}", 10, "#777", "start")
notes = ["NEW v0.15: the knob (KY-040) and its 5 wires are removed. The RIGHT joystick's SW pin → ESP32 D4 (female–female wire F): clicking the right stick = NEXT. D34 / D35 are free now.",
         "v0.14: remove the 7 old breadboard buttons and their 14 wires. Each panel button: lug 1 → its ESP32 pin (blue tag), lug 2 → top blue GND rail (dark tag). INFO is the 8th button, on D19. Sticks' +5V pin goes to 3V3, never 5V.",
         "Turn the ESP32 so its USB-C points LEFT, away from the breadboard (its printed labels will look upside down — that's expected). Rest it pins-down on its wires, never on metal.",
         "Rails in THIS plan: top red = 5V (from VIN), top blue = GND, bottom red = 3V3, bottom blue = GND. Check each rail runs the full length. Columns 54–63 stay empty.",
         "Panel buttons have no polarity: either lug can be lug 1. Never use D12. Leave both sticks untouched while the ESP32 starts (it measures their centre).",
         "2N2222 in c45/c46/c47 with its FLAT face (the side with writing) towards row e, i.e. towards you: E-B-C left to right. Buzzer long leg (+) in b50. TFT J1 stays open."]
for i, t in enumerate(notes): text(40, 1290 + i * 24, "• " + t, 12.5, "#b03a2e" if i == 0 else "#333", "start", "bold" if i == 0 else "normal")
open(OUT_SVG, "w").write(f'<svg xmlns="http://www.w3.org/2000/svg" width="{W_}" height="{H_}" viewBox="0 0 {W_} {H_}">' + "".join(S) + "</svg>")

def nm(e):
    if e[0] == "H": return f"{e[1]}{e[2]}"
    if e[0] == "R": return {"T+": "top red rail (5V)", "T-": "top blue rail (GND)", "B+": "bottom red rail (3V3)", "B-": "bottom blue rail (GND)"}[e[1]]
    if e[0] == "P": return "ESP32 " + e[2].replace(".L", " (VIN side)").replace(".R", " (3V3 side)")
    if e[0] == "L": return f"{e[1][4:]} button lug {e[2]}"
    return DEVNAME[e[1]] + " " + e[2]
mf = [w for w in wires if w[2] == "M-F"]
md = ["| # | Wire | Female end on | Other end | Type |", "|---|---|---|---|---|"]
n = 1
for a, b, kind, label in [w for w in mf if w[0][0] == "P"]:
    md.append(f"| line | {label} | {nm(a)} | {nm(b)} | M-F |")
for a, b, kind, label in [w for w in mf if w[0][0] == "X"]:
    md.append(f"| dot {n} | {label} | {nm(a)} | {nm(b)} | M-F |"); n += 1
for i, (a, b, kind, label) in enumerate([w for w in wires if w[2] == "F-F"]):
    md.append(f"| {L[i]} | {label} | {nm(a)} | {nm(b)} (also female) | F-F |")
for a, b, kind, label in [w for w in wires if w[2] == "M-M"]:
    md.append(f"| – | {label} | (male) {nm(a)} | {nm(b)} | M-M |")
open(OUT_MD, "w").write("\n".join(md) + "\n")
print("rendered", OUT_SVG, "and", OUT_MD)
