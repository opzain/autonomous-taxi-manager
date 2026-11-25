"""SVG building blocks for the cardboard controller. Units are mm in lid coordinates
(x = 0 on the centre line, y = 0 on the top edge, y grows towards the grips)."""
import math
from geom import *

FONT = "Inter, 'DejaVu Sans', Arial, sans-serif"

def fmt(v): return f"{v:.2f}".rstrip("0").rstrip(".")

def rounded_poly(pts, r):
    """Closed path through pts with every corner filleted at radius r (pts listed clockwise on screen)."""
    n = len(pts); out = []
    for i in range(n):
        P, A, B = pts[i], pts[i-1], pts[(i+1) % n]
        u1 = (A[0]-P[0], A[1]-P[1]); l1 = math.hypot(*u1); u1 = (u1[0]/l1, u1[1]/l1)
        u2 = (B[0]-P[0], B[1]-P[1]); l2 = math.hypot(*u2); u2 = (u2[0]/l2, u2[1]/l2)
        th = math.acos(max(-1, min(1, u1[0]*u2[0]+u1[1]*u2[1])))
        t = r/math.tan(th/2)
        s = (P[0]+u1[0]*t, P[1]+u1[1]*t); e = (P[0]+u2[0]*t, P[1]+u2[1]*t)
        out.append((s, e))
    d = f"M {fmt(out[0][0][0])} {fmt(out[0][0][1])} "
    for i, (s, e) in enumerate(out):
        if i: d += f"L {fmt(s[0])} {fmt(s[1])} "
        d += f"A {r} {r} 0 0 1 {fmt(e[0])} {fmt(e[1])} "
    return d + "Z"

BEZEL_PTS = [(-BEZ_TW/2, BEZ_TOP), (BEZ_TW/2, BEZ_TOP), (BEZ_BW/2, BEZ_BOT), (-BEZ_BW/2, BEZ_BOT)]
def bezel_d(dx=0, dy=0):
    return rounded_poly([(x+dx, y+dy) for x, y in BEZEL_PTS], BEZ_R)

def win_rect(dx=0, dy=0, **kw):
    x, y = WIN_C[0]-WIN_W/2+dx, WIN_C[1]-WIN_H/2+dy
    return f'<rect x="{fmt(x)}" y="{fmt(y)}" width="{WIN_W}" height="{WIN_H}" {attrs(kw)}/>'

def attrs(kw): return " ".join(f'{k.replace("_", "-")}="{v}"' for k, v in kw.items())
def circle(c, r, **kw): return f'<circle cx="{fmt(c[0])}" cy="{fmt(c[1])}" r="{fmt(r)}" {attrs(kw)}/>'
def line(a, b, **kw): return f'<line x1="{fmt(a[0])}" y1="{fmt(a[1])}" x2="{fmt(b[0])}" y2="{fmt(b[1])}" {attrs(kw)}/>'
def text(p, s, size=3.2, anchor="middle", fill="#333", weight=400, **kw):
    return (f'<text x="{fmt(p[0])}" y="{fmt(p[1])}" font-size="{size}" text-anchor="{anchor}" '
            f'font-family="{FONT}" font-weight="{weight}" fill="{fill}" {attrs(kw)}>{s}</text>')
def cross(c, s=2.2, w=0.18, col="#000"):
    return line((c[0]-s, c[1]), (c[0]+s, c[1]), stroke=col, stroke_width=w) + line((c[0], c[1]-s), (c[0], c[1]+s), stroke=col, stroke_width=w)
def rect(x, y, w, h, **kw): return f'<rect x="{fmt(x)}" y="{fmt(y)}" width="{fmt(w)}" height="{fmt(h)}" {attrs(kw)}/>'

CUT = dict(fill="none", stroke="#000", stroke_width=0.45)
HOLE = dict(fill="#f2f2f2", stroke="#000", stroke_width=0.35)
GUIDE = dict(fill="none", stroke="#888", stroke_width=0.25, stroke_dasharray="2 1.5")

# ---------------------------------------------------------------- 1:1 cutting templates
def lid_template():
    g = [f'<path d="{outline_d()}" {attrs(CUT)}/>']
    g.append(f'<path d="{bezel_d()}" {attrs(GUIDE)}/>')
    g.append(text((0, BEZ_TOP-1.6), "screen bezel glues here (dashed) - do not cut", 2.4, fill="#777"))
    g.append(line((0, -4), (0, 4), stroke="#000", stroke_width=0.3))
    g.append(line((0, APEX[1]-4), (0, APEX[1]+4), stroke="#000", stroke_width=0.3))
    g.append(win_rect(**HOLE))
    g.append(line((WIN_C[0]-WIN_W/2, WIN_C[1]-WIN_H/2), (WIN_C[0]+WIN_W/2, WIN_C[1]+WIN_H/2), stroke="#bbb", stroke_width=0.2))
    g.append(line((WIN_C[0]+WIN_W/2, WIN_C[1]-WIN_H/2), (WIN_C[0]-WIN_W/2, WIN_C[1]+WIN_H/2), stroke="#bbb", stroke_width=0.2))
    g.append(text((WIN_C[0], WIN_C[1]-3), "SCREEN WINDOW", 3.4, weight=600))
    g.append(text((WIN_C[0], WIN_C[1]+2), "60 × 46 mm - cut out", 2.8, fill="#555"))
    for name, c, col, lab in BUTTONS:
        g.append(circle(c, BTN_D/2, **HOLE)); g.append(cross(c))
        g.append(text((c[0], c[1]+BTN_D/2+3.2), lab, 2.6, fill="#444", weight=600))
    for name, c in STICKS:
        g.append(circle(c, STICK_D/2, **HOLE)); g.append(cross(c, 3))
        g.append(text((c[0], c[1]+STICK_D/2+3.4), f"{name} Ø28", 2.6, fill="#444", weight=600))
    g.append(circle(KNOB, KNOB_D/2, **HOLE)); g.append(cross(KNOB, 4.5))
    g.append(text((KNOB[0], KNOB[1]-6.5), "KNOB Ø7", 2.6, fill="#444", weight=600))
    for p in GRILLE: g.append(circle(p, GRILLE_D/2, **HOLE))
    g.append(text((GRILLE_C[0], GRILLE_C[1]+9.5), "BUZZER 7 × Ø3", 2.4, fill="#444", weight=600))
    g.append(text((-85, 92), "buttons Ø13", 2.4, fill="#777")); g.append(text((85, 92), "buttons Ø13", 2.4, fill="#777"))
    g.append(text((2.5, -1.6), "TOP EDGE (USB cable exits here)", 2.4, fill="#777", anchor="start"))
    g.append(text((-106, 150), "LID", 9, fill="#bbb", weight=700))
    g.append(text((-106, 158), "top face · 3 mm card", 2.6, fill="#999"))
    g.append(text((106, 150), "1:1", 9, fill="#bbb", weight=700))
    g.append(text((106, 158), "print at 100 %", 2.6, fill="#999"))
    return "".join(g)

def base_template():
    g = [f'<path d="{outline_d()}" {attrs(CUT)}/>']
    x, y, w, h = BB
    g.append(rect(x, y, w, h, **GUIDE, rx=2))
    g.append(text((0, y+h/2-2), "FULL-SIZE BREADBOARD 165 × 55", 3.4, fill="#777", weight=600))
    g.append(text((0, y+h/2+3), "stick it here (its own tape)", 2.6, fill="#888"))
    x, y, w, h = ESP
    g.append(rect(x, y, w, h, **GUIDE, rx=1.5))
    g.append(text((0, y+h/2-3), "ESP32", 3.2, fill="#777", weight=600))
    g.append(text((0, y+h/2+1.5), "pins up,", 2.4, fill="#888")); g.append(text((0, y+h/2+5), "USB end", 2.4, fill="#888"))
    g.append(text((0, y+h/2+8.5), "to top wall", 2.4, fill="#888"))
    for fx, fy in [(-100, 30), (100, 30), (-106, 165), (106, 165)]:
        g.append(circle((fx, fy), 10, **GUIDE)); g.append(text((fx, fy+1), "foot", 2.4, fill="#999"))
    g.append(line((0, -4), (0, 4), stroke="#000", stroke_width=0.3))
    g.append(line((0, APEX[1]-4), (0, APEX[1]+4), stroke="#000", stroke_width=0.3))
    g.append(text((2.5, -1.6), "TOP EDGE (centre)", 2.4, fill="#777", anchor="start"))
    g.append(text((-104, 112), "BASE", 9, fill="#bbb", weight=700))
    g.append(text((-104, 119), "inside face · no holes", 2.6, fill="#999"))
    g.append(text((104, 112), "1:1", 9, fill="#bbb", weight=700))
    g.append(text((104, 119), "same outline as lid", 2.6, fill="#999"))
    return "".join(g)

def check_bar(x, y, length=100):
    g = [line((x, y), (x+length, y), stroke="#000", stroke_width=0.5)]
    for i in range(0, length+1, 10):
        h = 3 if i % 50 == 0 else 1.8
        g.append(line((x+i, y-h), (x+i, y), stroke="#000", stroke_width=0.3))
    g.append(text((x+length/2, y+4.2), f"CHECK: this bar must measure exactly {length} mm. If not, reprint at 100 % / Actual size.", 2.7, fill="#000", weight=600))
    return "".join(g)

def bed(x, y, title, names):
    """Button bed, 64 x 64, top-left at (x, y). names = (top, bottom, left, right)."""
    s = BED; c = (x+s/2, y+s/2)
    g = [rect(x, y, s, s, **CUT)]
    pos = [(0, -OFF), (0, OFF), (-OFF, 0), (OFF, 0)]
    for (dx, dy), nm in zip(pos, names):
        g.append(rect(c[0]+dx-6, c[1]+dy-6, 12, 12, fill="none", stroke="#555", stroke_width=0.3))
        g.append(cross((c[0]+dx, c[1]+dy), 1.6, 0.15))
        g.append(text((c[0]+dx, c[1]+dy+9.2 if dy >= 0 else c[1]+dy-7.4), nm, 2.2, fill="#555", weight=600))
    for sx in (-1, 1):
        for sy in (-1, 1):
            g.append(rect(c[0]+sx*(s/2-6)-6, c[1]+sy*(s/2-6)-6, 12, 12, **GUIDE))
    g.append(text((c[0], c[1]+1), title, 2.4, fill="#000", weight=700))
    g.append(text((c[0], y+s+3.6), "dashed = corner blocks (2 layers each)", 2.2, fill="#777"))
    return "".join(g)

def parts_page():
    """A4 portrait, 1:1. Coordinates are page mm."""
    g = []
    g.append(text((105, 16), "CONTROLLER - SMALL PARTS (1:1)", 5, fill="#000", weight=700))
    g.append(text((105, 22), "Cut on the solid lines. Dashed lines are glue positions. 3 mm corrugated card.", 2.8, fill="#444"))
    bx, by = 105, 28 - BEZ_TOP
    g.append(f'<path d="{bezel_d(bx, by)}" {attrs(CUT)}/>')
    g.append(win_rect(bx, by, **HOLE))
    g.append(text((bx, by+WIN_C[1]), "SCREEN BEZEL - cut window 60 × 46", 2.8, fill="#000", weight=700))
    g.append(text((bx, by+WIN_C[1]+4.5), "glue on top of the lid; paint black", 2.4, fill="#555"))
    g.append(text((bx, by+BEZ_BOT+4), "top 108 · bottom 92 · height 64 · corners R8", 2.4, fill="#777"))
    g.append(check_bar(55, 108))
    g.append(bed(22, 120, "LEFT BED", ("UP", "DOWN", "LEFT", "RIGHT")))
    g.append(bed(128, 120, "RIGHT BED", ("INFO", "ACCEPT", "BACK", "REJECT")))
    g.append(text((105, 148), "60 × 60 each.", 2.4, fill="#555")); g.append(text((105, 151.5), "12 × 12 squares =", 2.4, fill="#555"))
    g.append(text((105, 155), "where each switch", 2.4, fill="#555")); g.append(text((105, 158.5), "is glued", 2.4, fill="#555"))
    g.append(text((14, 196), "CORNER BLOCKS 12 × 12 - cut 16 (2 layers × 4 corners × 2 beds)", 2.6, fill="#000", weight=700, anchor="start"))
    for i in range(16):
        g.append(rect(14+(i % 8)*15, 199+(i//8)*15, 12, 12, **CUT))
    g.append(text((140, 196), "SCREEN BLOCKS 10 × 10 - cut 4", 2.6, fill="#000", weight=700, anchor="start"))
    for i in range(4):
        g.append(rect(140+i*13, 199, 10, 10, **CUT))
    g.append(text((14, 236), "JOYSTICK FENCES 34 × 16 - cut 4 (2 per stick, glued on their long edge)", 2.6, fill="#000", weight=700, anchor="start"))
    for i in range(4):
        g.append(rect(14+i*38, 239, 34, 16, **CUT))
        g.append(text((14+i*38+17, 248), "16 = your height (b)", 2.2, fill="#888"))
    g.append(text((14, 263), "FEET Ø20 - cut 4 (optional)", 2.6, fill="#000", weight=700, anchor="start"))
    for i in range(4):
        g.append(circle((24+i*23, 276), 10, **CUT))
    g.append(text((112, 263), "BY RULER (not drawn here):", 2.6, fill="#000", weight=700, anchor="start"))
    for i, s in enumerate(["wall strips 60 × 300 - cut 3", "(corrugation lines across the 60)", "wall joint patches 30 × 60 - cut 3", "ESP32 pad 30 × 50 - cut 2"]):
        g.append(text((112, 268.5+i*4.6), s, 2.5, fill="#444", anchor="start"))
    return "".join(g)

# ---------------------------------------------------------------- colour preview
def render():
    g = []
    g.append(f'<path d="{outline_d(1.6, 2.6)}" fill="#000" opacity="0.18" filter="url(#blur)"/>')
    g.append(f'<path d="{outline_d()}" fill="url(#shell)" stroke="#c7cbd1" stroke-width="0.8"/>')
    # light bars
    for s in (-1, 1):
        g.append(f'<path d="M {s*57} 16 L {s*50} 74" stroke="#4f8cff" stroke-width="2.2" stroke-linecap="round" opacity="0.9"/>')
    g.append(f'<path d="{bezel_d()}" fill="#16181d"/>')
    g.append(win_rect(fill="#0b1830", rx=1))
    # fake console UI
    x0, y0 = WIN_C[0]-WIN_W/2, WIN_C[1]-WIN_H/2
    g.append(rect(x0, y0, WIN_W, 6, fill="#132a52"))
    g.append(text((x0+2.5, y0+4.3), "DISPATCH", 3.2, anchor="start", fill="#f2b705", weight=700))
    g.append(text((x0+WIN_W-2.5, y0+4.3), "09:42", 3.2, anchor="end", fill="#c9d3e3", weight=600))
    for i, (a, b, col) in enumerate([("REQ 14  BKC → Kurla", "ETA 4m", "#e9eef7"), ("TX-03  battery 62%", "₹ 210", "#7fd49b"), ("TX-05  charging", "18m", "#c9d3e3"), ("TX-01  idle at G Block", "", "#c9d3e3")]):
        yy = y0+12+i*7.5
        if i == 0: g.append(rect(x0+1.5, yy-4.6, WIN_W-3, 6.4, fill="#1f3d72", rx=0.8))
        g.append(text((x0+3, yy), a, 2.9, anchor="start", fill=col))
        g.append(text((x0+WIN_W-3, yy), b, 2.9, anchor="end", fill="#f2b705"))
    # D-pad caps
    sym = {"UP": "▲", "DOWN": "▼", "LEFT": "◀", "RIGHT": "▶", "INFO": "i", "ACCEPT": "✓", "BACK": "↩", "REJECT": "✕"}
    for name, c, col, lab in BUTTONS:
        g.append(circle((c[0]+0.4, c[1]+0.8), 6.3, fill="#000", opacity="0.25"))
        g.append(circle(c, 6, fill=col, stroke="#00000055", stroke_width=0.4))
        g.append(circle((c[0]-1.5, c[1]-1.8), 2.2, fill="#ffffff", opacity="0.18"))
        g.append(text((c[0], c[1]+2.2), sym[name], 5.8 if name in ("INFO",) else 5, fill="#ffffff", weight=700))
        g.append(text((c[0], c[1]+11), lab, 2.6, fill="#5a606b", weight=600))
    for name, c in STICKS:
        g.append(circle(c, 14, fill="#1b1d22"))
        g.append(circle((c[0]+0.6, c[1]+1), 12.5, fill="#000", opacity="0.35"))
        g.append(circle(c, 12.5, fill="url(#cap)"))
        g.append(circle(c, 8, fill="none", stroke="#3a3e46", stroke_width=0.6))
    g.append(circle((KNOB[0]+0.5, KNOB[1]+1), 12.5, fill="#000", opacity="0.3"))
    g.append(circle(KNOB, 12.5, fill="url(#cap)"))
    for i in range(24):
        a = 2*math.pi*i/24
        g.append(line((KNOB[0]+11*math.cos(a), KNOB[1]+11*math.sin(a)), (KNOB[0]+12.5*math.cos(a), KNOB[1]+12.5*math.sin(a)), stroke="#555", stroke_width=0.5))
    g.append(line(KNOB, (KNOB[0], KNOB[1]-8), stroke="#f2b705", stroke_width=1.2, stroke_linecap="round"))
    for p in GRILLE: g.append(circle(p, 1.5, fill="#2a2d33"))
    g.append(text((0, 136.5), "TAXI DISPATCH", 3.2, fill="#8a9099", weight=700, letter_spacing="0.6"))
    return "".join(g)

DEFS = """<defs>
<filter id="blur" x="-10%" y="-10%" width="120%" height="120%"><feGaussianBlur stdDeviation="2.2"/></filter>
<radialGradient id="shell" cx="50%" cy="35%" r="70%"><stop offset="0" stop-color="#fbfbfc"/><stop offset="1" stop-color="#e3e6ea"/></radialGradient>
<radialGradient id="cap" cx="40%" cy="35%" r="70%"><stop offset="0" stop-color="#4a4f58"/><stop offset="1" stop-color="#16181c"/></radialGradient>
<marker id="ar" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="5" markerHeight="5" orient="auto-start-reverse"><path d="M0 1 L10 5 L0 9 z" fill="#1b5e9b"/></marker>
</defs>"""

# ---------------------------------------------------------------- dimensioned drawings
BLUE = "#1b5e9b"
def dim_h(x1, x2, y, label, ext_from=None, size=3.4):
    g = [line((x1, y), (x2, y), stroke=BLUE, stroke_width=0.3, marker_start="url(#ar)", marker_end="url(#ar)")]
    if ext_from is not None:
        for x in (x1, x2): g.append(line((x, ext_from), (x, y + (1.5 if y > ext_from else -1.5)), stroke=BLUE, stroke_width=0.2))
    g.append(text(((x1+x2)/2, y-1.2), label, size, fill=BLUE, weight=600))
    return "".join(g)
def dim_v(y1, y2, x, label, ext_from=None, size=3.4):
    g = [line((x, y1), (x, y2), stroke=BLUE, stroke_width=0.3, marker_start="url(#ar)", marker_end="url(#ar)")]
    if ext_from is not None:
        for y in (y1, y2): g.append(line((ext_from, y), (x + (1.5 if x > ext_from else -1.5), y), stroke=BLUE, stroke_width=0.2))
    g.append(text((x-1.4, (y1+y2)/2), label, size, fill=BLUE, weight=600, transform=f"rotate(-90 {fmt(x-1.4)} {fmt((y1+y2)/2)})"))
    return "".join(g)

def construction():
    g = [f'<path d="{outline_d()}" fill="#fafafa" stroke="#000" stroke-width="0.6"/>']
    red = "#c0392b"
    g.append(line((0, -8), (0, H+8), stroke="#999", stroke_width=0.25, stroke_dasharray="4 1.5 1 1.5"))
    g.append(line((-XMAX-6, 0), (XMAX+6, 0), stroke="#999", stroke_width=0.25, stroke_dasharray="4 1.5 1 1.5"))
    for s in (1, -1):
        for c, r, nm in [(C1, R1, "C1"), (C2, R2, "C2"), (C3, R3, "C3")]:
            if s == -1 and nm == "C3": continue
            cc = (s*c[0], c[1])
            g.append(circle(cc, r, fill="none", stroke=red, stroke_width=0.25, stroke_dasharray="1.5 1"))
            g.append(cross(cc, 2, 0.3, red))
    lab = [(C1, f"C1 ({fmt(C1[0])}, {fmt(C1[1])}) R{fmt(R1)}", (4, -3)),
           (C2, f"C2 ({fmt(C2[0])}, {fmt(C2[1])}) R{fmt(R2)}", (0, -4)),
           (C3, f"C3 (0, {fmt(C3[1])}) R{fmt(R3)}", (0, 6))]
    for c, s, (dx, dy) in lab:
        g.append(text((c[0]+dx, c[1]+dy), s, 3.2, fill=red, weight=700, anchor="middle"))
    pts = [("T1", T1, (-3, -2), "end"), ("T2", T2, (-3, 2), "end"), ("T3", T3, (-2, 6), "middle"), ("T4", T4, (3, -3), "start"), ("A", APEX, (0, -3), "middle"), ("P", TOPC1, (0, -2.5), "middle")]
    for nm, p, (dx, dy), anc in pts:
        g.append(circle(p, 1.1, fill="#000"))
        g.append(text((p[0]+dx, p[1]+dy), f"{nm} ({fmt(round(p[0], 1))}, {fmt(round(p[1], 1))})", 3, fill="#000", weight=700, anchor=anc))
    g.append(dim_h(-XMAX, XMAX, H+12, f"{fmt(2*XMAX)} overall", ext_from=H-20))
    g.append(dim_v(0, H, -XMAX-10, f"{fmt(H)} overall", ext_from=-XMAX+30))
    g.append(text((-70, 120), "Left half = mirror image", 3.4, fill="#666", weight=600))
    g.append(text((-70, 125), "(same numbers with x negative)", 3, fill="#666"))
    return "".join(g)

def holes_drawing():
    g = [f'<path d="{outline_d()}" fill="#fafafa" stroke="#000" stroke-width="0.6"/>']
    g.append(line((0, -6), (0, H+4), stroke="#999", stroke_width=0.25, stroke_dasharray="4 1.5 1 1.5"))
    g.append(f'<path d="{bezel_d()}" fill="none" stroke="#999" stroke-width="0.3" stroke-dasharray="2 1.5"/>')
    g.append(win_rect(fill="#e9eef7", stroke="#000", stroke_width=0.4))
    short = {"UP": "UP", "DOWN": "DN", "LEFT": "LT", "RIGHT": "RT", "INFO": "INF", "ACCEPT": "ACC", "BACK": "BK", "REJECT": "REJ"}
    for name, c, col, lab in BUTTONS:
        g.append(circle(c, BTN_D/2, fill="#e9eef7", stroke="#000", stroke_width=0.4))
        g.append(text((c[0], c[1]+0.95), short[name], 2.6, fill="#000", weight=700))
    for name, c in STICKS:
        g.append(circle(c, STICK_D/2, fill="#e9eef7", stroke="#000", stroke_width=0.4)); g.append(cross(c, 2.5, 0.2))
    g.append(circle(KNOB, KNOB_D/2, fill="#e9eef7", stroke="#000", stroke_width=0.4))
    for p in GRILLE: g.append(circle(p, GRILLE_D/2, fill="#e9eef7", stroke="#000", stroke_width=0.3))
    sx, sy = STICKS[1][1]
    g.append(dim_h(-WIN_W/2, WIN_W/2, WIN_C[1]+WIN_H/2+5, "60", size=3))
    g.append(dim_v(WIN_C[1]-WIN_H/2, WIN_C[1]+WIN_H/2, WIN_W/2+4, "46", size=3))
    g.append(dim_v(0, WIN_C[1]-WIN_H/2, -24, "21", size=3))
    g.append(dim_h(0, CR[0], 16, "85", size=3))
    g.append(dim_h(CR[0], CR[0]+OFF, 46, "20", size=3))
    g.append(dim_v(CR[1], CR[1]+OFF, CR[0]+14, "20", size=3))
    g.append(dim_v(0, CR[1], 118, "58", ext_from=CR[0]+OFF+7, size=3))
    g.append(dim_h(0, sx, 86, f"{fmt(sx)}", size=3))
    g.append(line((sx, sy), (75.5, sy), stroke=BLUE, stroke_width=0.2))
    g.append(dim_v(0, sy, 74, f"{fmt(sy)}", size=3))
    g.append(line((0, KNOB[1]), (-35.5, KNOB[1]), stroke=BLUE, stroke_width=0.2))
    g.append(dim_v(0, KNOB[1], -34, f"{fmt(KNOB[1])}", size=3))
    g.append(dim_v(KNOB[1], GRILLE_C[1], -9, f"{fmt(GRILLE_C[1]-KNOB[1])}", size=3))
    g.append(text((sx, sy+19), "Ø28", 3.2, fill=BLUE, weight=700))
    g.append(text((-sx, sy+19), "Ø28", 3.2, fill=BLUE, weight=700))
    g.append(text((CR[0]+25, CR[1]-21), "all 8 buttons Ø13", 3, fill=BLUE, weight=700))
    g.append(text((KNOB[0]+8, KNOB[1]-5), "Ø7", 3, fill=BLUE, weight=700))
    g.append(text((GRILLE_C[0], GRILLE_C[1]+10.5), "7 × Ø3, 5 apart", 3, fill=BLUE, weight=700))
    return "".join(g)

def svg_wrap(inner, vb, width_mm=None, height_mm=None, width_css=None, extra=""):
    x, y, w, h = vb
    size = f'width="{fmt(width_mm)}mm" height="{fmt(height_mm)}mm"' if width_mm else f'width="{width_css}"'
    return f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="{fmt(x)} {fmt(y)} {fmt(w)} {fmt(h)}" {size} {extra}>{DEFS}{inner}</svg>'
