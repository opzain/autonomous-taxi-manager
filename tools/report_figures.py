# Generates the report / documentation figures as SVG (rendered to PNG separately):
#   fig-taxi-states.svg, fig-request-states.svg, fig-architecture.svg, fig-enclosure.svg, fig-balance.svg
# Usage: python3 tools/report_figures.py docs/images
import html, math, sys, os

OUT = sys.argv[1]
FONT = "Segoe UI,Arial,sans-serif"


class Svg:
    def __init__(self, w, h, bg="#ffffff"):
        self.w, self.h, self.s = w, h, []
        self.s.append(f'<rect width="{w}" height="{h}" fill="{bg}"/>')
        self.s.append('<defs><marker id="ar" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse">'
                      '<path d="M0,0 L10,5 L0,10 z" fill="#37474f"/></marker>'
                      '<marker id="arb" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse">'
                      '<path d="M0,0 L10,5 L0,10 z" fill="#1565c0"/></marker></defs>')

    def text(self, x, y, t, size=13, col="#1d2733", anchor="middle", weight="normal", italic=False):
        st = ' font-style="italic"' if italic else ""
        self.s.append(f'<text x="{x}" y="{y}" font-size="{size}" fill="{col}" text-anchor="{anchor}" font-weight="{weight}" font-family="{FONT}"{st}>{html.escape(str(t))}</text>')

    def box(self, x, y, w, h, fill, stroke, r=10, sw=2):
        self.s.append(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="{r}" fill="{fill}" stroke="{stroke}" stroke-width="{sw}"/>')

    def line(self, pts, col="#37474f", w=2, arrow=True, dash=None, marker="ar"):
        d = " ".join(f"{x},{y}" for x, y in pts)
        da = f' stroke-dasharray="{dash}"' if dash else ""
        m = f' marker-end="url(#{marker})"' if arrow else ""
        self.s.append(f'<polyline points="{d}" fill="none" stroke="{col}" stroke-width="{w}"{da}{m} stroke-linejoin="round"/>')

    def save(self, name):
        open(os.path.join(OUT, name), "w").write(
            f'<svg xmlns="http://www.w3.org/2000/svg" width="{self.w}" height="{self.h}" viewBox="0 0 {self.w} {self.h}">' + "".join(self.s) + "</svg>")


def edge_point(cx, cy, w, h, tx, ty):
    """Point on a box border (centre cx,cy) towards (tx,ty)."""
    dx, dy = tx - cx, ty - cy
    if dx == 0 and dy == 0: return cx, cy
    sx = (w / 2) / abs(dx) if dx else 1e9
    sy = (h / 2) / abs(dy) if dy else 1e9
    s = min(sx, sy)
    return cx + dx * s, cy + dy * s


def state_diagram(name, title, states, edges, w, h, note=""):
    g = Svg(w, h)
    g.text(24, 34, title, 20, "#111", "start", "bold")
    if note: g.text(24, 56, note, 12.5, "#555", "start")
    BW, BH = 150, 46
    for key, (x, y, label, fill, stroke) in states.items():
        g.box(x - BW / 2, y - BH / 2, BW, BH, fill, stroke)
        g.text(x, y + 5, label, 14, "#102027", "middle", "bold")
    for a, b, label, bend in edges:
        ax, ay = states[a][0], states[a][1]
        bx, by = states[b][0], states[b][1]
        if bend:  # curved: use a midpoint offset perpendicular
            mx, my = (ax + bx) / 2, (ay + by) / 2
            nx, ny = -(by - ay), bx - ax
            ln = math.hypot(nx, ny) or 1
            mx, my = mx + nx / ln * bend, my + ny / ln * bend
            p1 = edge_point(ax, ay, BW + 4, BH + 4, mx, my)
            p2 = edge_point(bx, by, BW + 8, BH + 8, mx, my)
            g.s.append(f'<path d="M{p1[0]},{p1[1]} Q{mx},{my} {p2[0]},{p2[1]}" fill="none" stroke="#37474f" stroke-width="1.8" marker-end="url(#ar)"/>')
            lx, ly = (p1[0] + 2 * mx + p2[0]) / 4, (p1[1] + 2 * my + p2[1]) / 4
        else:
            p1 = edge_point(ax, ay, BW + 4, BH + 4, bx, by)
            p2 = edge_point(bx, by, BW + 8, BH + 8, ax, ay)
            g.line([p1, p2], w=1.8)
            lx, ly = (p1[0] + p2[0]) / 2, (p1[1] + p2[1]) / 2
        if label:
            tw = len(label) * 6.4 + 10
            g.s.append(f'<rect x="{lx - tw / 2}" y="{ly - 11}" width="{tw}" height="17" rx="4" fill="#ffffff" fill-opacity="0.92"/>')
            g.text(lx, ly + 2, label, 11, "#37474f")
    g.save(name)


# ------------------------------------------------------------------ taxi state machine (hand-placed, orthogonal arrows)
C = {"idle": ("#e8f5e9", "#2e7d32"), "job": ("#fff8e1", "#f9a825"), "ride": ("#e3f2fd", "#1565c0"),
     "chg": ("#f3e5f5", "#8e24aa"), "warn": ("#fff3e0", "#ef6c00"), "bad": ("#ffebee", "#c62828"), "mov": ("#eceff1", "#546e7a")}
g = Svg(1240, 760)
g.text(24, 34, "Taxi state machine — enforced by TaxiStateMachine (illegal changes are refused, test T06)", 20, "#111", "start", "bold")
g.text(24, 58, "Spec §11 mapping: ASSIGNED + EN_ROUTE_PICKUP = TO_PICKUP · PICKUP + EN_ROUTE_DROPOFF = ON_RIDE · COMPLETED = the drop-off event.", 12.5, "#555", "start")
g.text(24, 78, "Any moving state (TO_PICKUP, ON_RIDE, TO_CHARGER, RELOCATING) can also become DELAYED or OFFLINE; one arrow of each is drawn.", 12.5, "#555", "start")
BW, BH = 170, 50
pos = {"IDLE": (160, 330, "idle"), "TO_PICKUP": (520, 330, "job"), "ON_RIDE": (880, 330, "ride"), "OFFLINE": (1130, 600, "bad"),
       "RELOCATING": (160, 600, "mov"), "TO_CHARGER": (520, 600, "chg"), "CHARGING": (880, 600, "chg"), "DELAYED": (520, 160, "warn")}
for k, (x, y, c) in pos.items():
    g.box(x - BW / 2, y - BH / 2, BW, BH, C[c][0], C[c][1])
    g.text(x, y + 5, k, 15, "#102027", "middle", "bold")
def lab(x, y, t, col="#37474f"):
    tw = len(t) * 6.6 + 12
    g.s.append(f'<rect x="{x - tw / 2}" y="{y - 12}" width="{tw}" height="18" rx="4" fill="#fff" fill-opacity="0.95"/>')
    g.text(x, y + 2, t, 11.5, col)
def arrow(pts, t=None, at=None):
    g.line(pts, w=2)
    if t:
        lx, ly = at if at else ((pts[0][0] + pts[-1][0]) / 2, (pts[0][1] + pts[-1][1]) / 2)
        lab(lx, ly, t)
L, R, T, B = (lambda k: pos[k][0] - BW / 2), (lambda k: pos[k][0] + BW / 2), (lambda k: pos[k][1] - BH / 2), (lambda k: pos[k][1] + BH / 2)
X, Y = (lambda k: pos[k][0]), (lambda k: pos[k][1])
arrow([(R("IDLE"), Y("IDLE") - 8), (L("TO_PICKUP"), Y("TO_PICKUP") - 8)], "Assign (ACCEPT)")
arrow([(R("TO_PICKUP"), Y("TO_PICKUP") - 8), (L("ON_RIDE"), Y("ON_RIDE") - 8)], "reached passenger")
arrow([(X("ON_RIDE"), B("ON_RIDE")), (X("ON_RIDE"), 420), (X("IDLE"), 420), (X("IDLE"), B("IDLE"))], "drop-off: fare, satisfaction", (700, 420))
arrow([(X("TO_PICKUP") - 30, T("TO_PICKUP")), (X("DELAYED") - 30, B("DELAYED"))], "closure / fault", (440, 245))
arrow([(X("DELAYED") + 30, B("DELAYED")), (X("TO_PICKUP") + 30, T("TO_PICKUP"))], "Reroute (auto 10 min)", (620, 245))
arrow([(R("ON_RIDE"), Y("ON_RIDE")), (X("OFFLINE"), Y("ON_RIDE")), (X("OFFLINE"), T("OFFLINE"))], "battery 0 %", (1130, 470))
arrow([(L("OFFLINE"), Y("OFFLINE")), (R("CHARGING"), Y("CHARGING"))], "towed ₹500, 20 min")
arrow([(X("IDLE") + 40, B("IDLE")), (X("IDLE") + 40, 470), (X("TO_CHARGER") - 40, 470), (X("TO_CHARGER") - 40, T("TO_CHARGER"))], "SendCharge", (330, 470))
arrow([(R("TO_CHARGER"), Y("TO_CHARGER")), (L("CHARGING"), Y("CHARGING"))], "arrived / queued")
arrow([(X("CHARGING"), B("CHARGING")), (X("CHARGING"), 700), (60, 700), (60, Y("IDLE")), (L("IDLE"), Y("IDLE"))], "full / Release charger", (470, 700))
arrow([(X("IDLE") - 30, B("IDLE")), (X("IDLE") - 30, T("RELOCATING"))], "Relocate", (110, 460))
arrow([(X("RELOCATING") + 10, T("RELOCATING")), (X("RELOCATING") + 10, B("IDLE"))], None)
lab(205, 520, "arrived")
arrow([(R("RELOCATING"), Y("RELOCATING") - 12), (300, Y("RELOCATING") - 12), (300, Y("TO_PICKUP") + 14), (L("TO_PICKUP"), Y("TO_PICKUP") + 14)], "Assign", (300, 525))
g.save("fig-taxi-states.svg")

# ------------------------------------------------------------------ request lifecycle
rs = {
    "New": (130, 300, "generated", "#eceff1", "#546e7a"),
    "Waiting": (420, 300, "WAITING", *C["job"]),
    "Assigned": (700, 300, "ASSIGNED", *C["job"]),
    "Picked": (970, 300, "PICKED_UP", *C["ride"]),
    "Done": (1180, 300, "COMPLETED", *C["idle"]),
    "Rival": (130, 520, "TAKEN_BY_RIVAL", "#fff3e0", "#ef6c00"),
    "Declined": (130, 110, "DECLINED", "#fff3e0", "#ef6c00"),
    "Dropped": (360, 520, "DROPPED", *C["bad"]),
    "Rejected": (420, 110, "REJECTED", *C["bad"]),
    "Abandoned": (700, 520, "ABANDONED", *C["bad"]),
}
redges = [
    ("New", "Waiting", "accepts our price", 0), ("New", "Rival", "prefers RapidCab (logit)", 0), ("New", "Declined", "surge too high", 0),
    ("New", "Dropped", "queue full (8)", 0), ("Waiting", "Assigned", "ACCEPT / Assign", 0), ("Assigned", "Picked", "taxi arrives", 0),
    ("Picked", "Done", "drop-off", 0), ("Waiting", "Rejected", "REJECT", 0), ("Waiting", "Abandoned", "patience ran out", 0),
    ("Assigned", "Abandoned", "ETA too long", 0),
]
state_diagram("fig-request-states.svg", "Passenger request lifecycle", rs, redges, 1290, 600,
              "Abandoned: −₹100 and −2 satisfaction (−4 medical). Rejected: −1. Completed late (> 1.2 × quoted + 1.5 min): −₹10/min and −2.")

# ------------------------------------------------------------------ architecture
g = Svg(1400, 780)
g.text(24, 34, "System and software architecture (v0.11)", 20, "#111", "start", "bold")
g.text(24, 56, "The simulation core is the dominant component (spec §4); Unity only draws it; the ESP32 is the physical input/feedback device.", 12.5, "#555", "start")
# ESP32 column
g.box(30, 90, 330, 640, "#fbe9e7", "#c62828")
g.text(195, 118, "ESP32 dispatch console (firmware v0.4)", 15, "#b71c1c", "middle", "bold")
esp = [("7 buttons + KY-040 knob", "debounce 25 ms, long press 600 ms, encoder ISR"),
       ("Self-test mode", "T01–T04 evidence, works without the PC"),
       ("Console mode", "draws S/T/R/F/M lines on the 2.8\" ILI9341"),
       ("Buzzer (2N2222)", "non-blocking cue patterns (Z|…)"),
       ("WS2812B-8 stick", "fleet colours (L|…), optional"),
       ("Link watchdog", "LINK LOST after 4 s of silence")]
for i, (a, b) in enumerate(esp):
    y = 140 + i * 95
    g.box(50, y, 290, 78, "#ffffff", "#e57373", 8, 1.5)
    g.text(195, y + 30, a, 14, "#111", "middle", "bold")
    g.text(195, y + 54, b, 11.5, "#555")
# USB link
g.box(390, 330, 170, 130, "#fffde7", "#f9a825")
g.text(475, 360, "USB serial", 15, "#8d6e00", "middle", "bold")
g.text(475, 382, "115200 baud, ASCII lines", 11.5, "#555")
g.text(475, 404, "→ B, E, K, H", 12, "#333")
g.text(475, 426, "← S T R F M Z L", 12, "#333")
g.text(475, 448, "~1.2 KB/s, diff only", 11.5, "#555")
g.line([(360, 370), (388, 370)]); g.line([(390, 420), (362, 420)])
# Unity column
g.box(590, 90, 780, 640, "#e3f2fd", "#1565c0")
g.text(980, 118, "PC: Unity 6 (URP) — C#", 15, "#0d47a1", "middle", "bold")
uw = [  # (x, y, w, h, title, sub, fill, stroke)
    (610, 140, 230, 90, "SerialLink", "background thread, COM-port auto search", "#ffffff", "#64b5f6"),
    (610, 250, 230, 90, "ConsoleBridge", "input → console, page → TFT, cues", "#ffffff", "#64b5f6"),
    (610, 360, 230, 90, "Keyboard / mouse", "same actions without hardware", "#ffffff", "#64b5f6"),
    (880, 250, 220, 90, "DispatchConsole", "Core: pages, selection, commands", "#e8f5e9", "#2e7d32"),
    (1130, 140, 220, 200, "Simulation", "Core: requests, fleet, routes (Dijkstra), traffic events, energy, economy, satisfaction, rival pricing, save/replay", "#e8f5e9", "#2e7d32"),
    (880, 470, 220, 100, "SimulationDriver", "ticks · taxis on roads · pins, ribbons, closures, rain, sky", "#ffffff", "#64b5f6"),
    (1130, 470, 220, 100, "DispatchHud", "top bar, page mirror, fleet, ticker, labels", "#ffffff", "#64b5f6"),
    (880, 610, 470, 90, "Procedural 3D BKC world", "roads, 7,500 buildings, traffic, people, weather, sound, camera, menus", "#ffffff", "#90a4ae"),
]
for x, y, w, h, t, sub, f, st in uw:
    g.box(x, y, w, h, f, st, 8, 1.5)
    g.text(x + w / 2, y + 30, t, 14, "#111", "middle", "bold")
    words, line_, yy = sub.split(" "), "", y + 52
    for wd in words:
        if len(line_ + " " + wd) * 6.3 > w - 20:
            g.text(x + w / 2, yy, line_.strip(), 11.5, "#555"); yy += 16; line_ = ""
        line_ += " " + wd
    g.text(x + w / 2, yy, line_.strip(), 11.5, "#555")
g.line([(560, 395), (608, 185)]); g.line([(725, 230), (725, 248)]); g.line([(840, 295), (878, 295)]); g.line([(840, 405), (878, 310)])
g.line([(1100, 280), (1128, 260)], marker="arb", col="#1565c0"); g.text(1112, 300, "Submit", 11, "#1565c0")
g.line([(1240, 340), (1240, 468)], col="#1565c0", marker="arb"); g.line([(1160, 340), (1000, 468)], col="#1565c0", marker="arb")
g.line([(990, 570), (990, 608)]); g.line([(880, 280), (842, 280)], dash="5 4")
g.text(1010, 760, "Green = engine-free TaxiSim.Core (unit-tested with dotnet test). Blue = Unity World layer.", 12, "#333")
g.save("fig-architecture.svg")

# ------------------------------------------------------------------ enclosure (top panel, mm → px ×4)
S = 4.0
W_mm, D_mm = 240, 140
g = Svg(int(W_mm * S + 160), int(D_mm * S + 330))
ox, oy = 60, 90
g.text(24, 34, "Dispatch console enclosure — top panel (1:4, dimensions in mm)", 20, "#111", "start", "bold")
g.text(24, 58, "240 × 140 mm footprint, 65 mm tall, 3 mm acrylic / MDF top on 4 × M3 × 25 mm standoffs. Rear: USB-C cut-out for the ESP32.", 12.5, "#555", "start")
g.box(ox, oy, W_mm * S, D_mm * S, "#f5f0e6", "#5d4037", 18, 3)
def mm(x, y): return ox + x * S, oy + y * S
# TFT window: module 86 x 50, active area 57.6 x 43.2 (2.8"); place window 60 x 46 centred at (52, 62)
tx, ty = mm(22, 20)
g.box(tx, ty, 60 * S, 46 * S, "#263238", "#000", 4, 2)
g.text(tx + 30 * S, ty + 23 * S + 5, "TFT window 60 × 46", 13, "#fff")
g.text(tx + 30 * S, ty + 46 * S + 18, "2.8\" ILI9341 (module 86 × 50, M3 holes 80 × 44)", 11, "#5d4037")
# D-pad buttons (12 mm caps on 6x6 switch extenders) pitch 22 mm, centred at (135, 70)
cx, cy = 135, 72
for (bx, by, lab) in [(0, -22, "UP"), (0, 22, "DOWN"), (-22, 0, "LEFT"), (22, 0, "RIGHT")]:
    px, py = mm(cx + bx, cy + by)
    g.s.append(f'<circle cx="{px}" cy="{py}" r="{6 * S}" fill="#90caf9" stroke="#1565c0" stroke-width="2"/>')
    g.text(px, py + 4, lab, 11, "#0d47a1", "middle", "bold")
# Action buttons row at y=112
for (bx, lab, f, s_) in [(112, "BACK", "#e0e0e0", "#616161"), (140, "ACCEPT", "#a5d6a7", "#2e7d32"), (168, "REJECT", "#ef9a9a", "#c62828")]:
    px, py = mm(bx, 116)
    g.s.append(f'<circle cx="{px}" cy="{py}" r="{7 * S}" fill="{f}" stroke="{s_}" stroke-width="2"/>')
    g.text(px, py + 4, lab, 10.5, "#111", "middle", "bold")
# Encoder knob 25 mm at (205, 72)
px, py = mm(207, 70)
g.s.append(f'<circle cx="{px}" cy="{py}" r="{12.5 * S}" fill="#cfd8dc" stroke="#37474f" stroke-width="3"/>')
g.s.append(f'<circle cx="{px}" cy="{py}" r="{3.6 * S}" fill="#fff" stroke="#37474f" stroke-width="2"/>')
g.text(px, py + 12.5 * S + 18, "KNOB Ø25 (hole Ø7)", 11.5, "#37474f", "middle", "bold")
# LED stick window 53 x 10 at (22, 108)
lx, ly = mm(22, 104)
g.box(lx, ly, 56 * S, 11 * S, "#212121", "#000", 3, 1.5)
for i in range(8):
    g.s.append(f'<circle cx="{lx + (3.5 + i * 7) * S}" cy="{ly + 5.5 * S}" r="{2.3 * S}" fill="hsl({i * 40},90%,60%)"/>')
g.text(lx + 28 * S, ly + 11 * S + 16, "WS2812B-8 window 56 × 11", 11, "#5d4037")
# Buzzer grille
bx_, by_ = mm(205, 117)
for i in range(4):
    g.s.append(f'<circle cx="{bx_ + (i - 1.5) * 3.5 * S}" cy="{by_}" r="{1.2 * S}" fill="#5d4037"/>')
g.text(bx_, by_ + 18, "buzzer holes", 11, "#5d4037")
# Corner holes
for (x, y) in [(6, 6), (234, 6), (6, 134), (234, 134)]:
    px, py = mm(x, y)
    g.s.append(f'<circle cx="{px}" cy="{py}" r="{1.6 * S}" fill="#fff" stroke="#5d4037" stroke-width="1.5"/>')
# Dimension lines
g.line([(ox, oy + D_mm * S + 30), (ox + W_mm * S, oy + D_mm * S + 30)], arrow=False, col="#5d4037", w=1.5)
g.text(ox + W_mm * S / 2, oy + D_mm * S + 52, "240 mm", 13, "#5d4037", "middle", "bold")
g.line([(ox + W_mm * S + 30, oy), (ox + W_mm * S + 30, oy + D_mm * S)], arrow=False, col="#5d4037", w=1.5)
g.text(ox + W_mm * S + 46, oy + D_mm * S / 2, "140", 13, "#5d4037", "middle", "bold")
notes = ["Button pitch 22 mm D-pad, 28 mm action row (spec: ≥ 18–20 mm). 12 mm caps on 6 × 6 mm switches (or 12 mm panel switches, Ø12 holes).",
         "Labels engraved or printed next to each control: ↑ ↓ ← → BACK ACCEPT REJECT; colour-coded caps match the TFT colours.",
         "Inside: breadboard on the base plate, ESP32 on female jumpers / header strip, cable tie anchors; USB-C slot 12 × 7 mm in the rear wall.",
         "Sides: 4 × MDF strips 65 mm high, or a 3D-printed tray; vent slots behind the ESP32."]
for i, n in enumerate(notes):
    g.text(24, oy + D_mm * S + 90 + i * 24, "• " + n, 12.5, "#333", "start")
g.save("fig-enclosure.svg")

# ------------------------------------------------------------------ balance chart (human-paced player)
data = [("Easy", [6, 6, 6], [3.5, 3.3, 3.2]), ("Normal", [5, 5, 4], [3.0, 2.8, 2.3]), ("Hard", [2, 4, 0], [1.7, 2.5, 1.0])]
paces = ["every 2.3 s", "every 4.5 s", "every 9 s"]
cols = ["#2e7d32", "#f9a825", "#c62828"]
g = Svg(900, 470)
g.text(24, 34, "Balance check at human speed (6 seeds per bar)", 20, "#111", "start", "bold")
g.text(24, 56, "Test player presses ACCEPT on the suggested taxi at the given pace (1× speed) and charges taxis below 30 %; no repositioning or pricing.", 12, "#555", "start")
x0, y0, hgt = 90, 400, 300
for k in range(0, 7):
    y = y0 - k * hgt / 6
    g.line([(x0, y), (860, y)], arrow=False, col="#e0e0e0", w=1)
    g.text(x0 - 10, y + 4, k, 12, "#555", "end")
g.text(30, y0 - hgt / 2, "shifts", 12, "#555", "middle")
g.text(30, y0 - hgt / 2 + 16, "completed", 12, "#555", "middle")
for gi, (diff, done, stars) in enumerate(data):
    gx = x0 + 30 + gi * 260
    for pi in range(3):
        bx = gx + pi * 66
        bh = done[pi] / 6 * hgt
        g.box(bx, y0 - bh, 52, max(bh, 2), cols[pi], cols[pi], 3, 1)
        g.text(bx + 26, y0 - bh - 8, f"{stars[pi]}★", 12, "#333")
    g.text(gx + 92, y0 + 24, diff, 15, "#111", "middle", "bold")
for pi in range(3):
    g.box(560 + pi * 110, 70, 16, 16, cols[pi], cols[pi], 3, 1)
    g.text(582 + pi * 110, 83, paces[pi], 12, "#333", "start")
g.save("fig-balance.svg")
print("figures written")
