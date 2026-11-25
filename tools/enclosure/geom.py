import math
# All units mm. Origin: centre line (x=0) on the top edge (y=0). y grows DOWN (towards the grips).
C1, R1 = (88.0, 40.0), 40.0     # shoulder corner (convex)
C2, R2 = (106.0, 158.0), 28.0   # grip end (convex)
C3, R3 = (0.0, 180.0), 40.0     # arch between the grips (concave), apex y = 140

def _sols(c1, c2, k):
    dx, dy = c2[0]-c1[0], c2[1]-c1[1]; D = math.hypot(dx, dy); a = math.atan2(dy, dx); b = math.acos(k/D)
    return [(math.cos(a+s*b), math.sin(a+s*b)) for s in (1, -1)]

# outer side: external tangent C1 -> C2, pick the normal pointing outwards (+x)
n_out = max(_sols(C1, C2, R1-R2), key=lambda n: n[0])
T1 = (C1[0]+R1*n_out[0], C1[1]+R1*n_out[1]); T2 = (C2[0]+R2*n_out[0], C2[1]+R2*n_out[1])
# inner grip edge: cross tangent C2 (convex) -> C3 (concave); normal points down-left
n_in = [n for n in _sols(C2, C3, R2+R3) if n[0] < 0 and n[1] > 0][0]
T3 = (C2[0]+R2*n_in[0], C2[1]+R2*n_in[1]); T4 = (C3[0]-R3*n_in[0], C3[1]-R3*n_in[1])
APEX = (0.0, C3[1]-R3)
TOPC1 = (C1[0], C1[1]-R1)

def ang(c, p): return math.atan2(p[1]-c[1], p[0]-c[0])
a1 = ang(C1, T1) - ang(C1, TOPC1)                         # shoulder arc sweep
a2 = (ang(C2, T3) - ang(C2, T2)) % (2*math.pi)            # grip arc sweep
a3 = (ang(C3, T4) - ang(C3, APEX)) % (2*math.pi)          # arch half sweep
seg = {'top': TOPC1[0], 'shoulder': R1*a1, 'side': math.dist(T1, T2), 'grip': R2*a2,
       'inner': math.dist(T3, T4), 'arch': R3*a3}
PERIM = 2*sum(seg.values())
W = 2*(C2[0]+R2*1.0) if C2[0]+R2 > C1[0]+R1 else 2*(C1[0]+R1)
# true extreme x: outer side line may bulge past circles? no (tangent line between them) -> max of circles
XMAX = max(C1[0]+R1, C2[0]+R2); H = C2[1]+R2

def mx(p): return (-p[0], p[1])
def outline_d(ox=0.0, oy=0.0):
    f = lambda p: f"{p[0]+ox:.3f} {p[1]+oy:.3f}"
    large2 = 1 if a2 > math.pi else 0
    d = [f"M {f((0,0))}", f"L {f(TOPC1)}", f"A {R1} {R1} 0 0 1 {f(T1)}", f"L {f(T2)}",
         f"A {R2} {R2} 0 {large2} 1 {f(T3)}", f"L {f(T4)}", f"A {R3} {R3} 0 0 0 {f(APEX)}",
         f"A {R3} {R3} 0 0 0 {f(mx(T4))}", f"L {f(mx(T3))}", f"A {R2} {R2} 0 {large2} 1 {f(mx(T2))}",
         f"L {f(mx(T1))}", f"A {R1} {R1} 0 0 1 {f(mx(TOPC1))}", "Z"]
    return " ".join(d)

def sample(n=4000):
    """Polygon of the outline (for inside tests)."""
    pts = []
    def arc(c, r, t0, t1, k):
        for i in range(k+1):
            t = t0 + (t1-t0)*i/k; pts.append((c[0]+r*math.cos(t), c[1]+r*math.sin(t)))
    pts.append((0, 0)); pts.append(TOPC1)
    arc(C1, R1, ang(C1, TOPC1), ang(C1, TOPC1)+a1, 60)
    pts.append(T2); arc(C2, R2, ang(C2, T2), ang(C2, T2)+a2, 80)
    pts.append(T4); arc(C3, R3, ang(C3, T4), ang(C3, T4)-a3, 40)
    right = pts[:]
    left = [mx(p) for p in reversed(right)]
    return right + left[1:]

def inside(pt, poly):
    x, y = pt; c = False
    for i in range(len(poly)):
        x1, y1 = poly[i]; x2, y2 = poly[(i+1) % len(poly)]
        if (y1 > y) != (y2 > y) and x < (x2-x1)*(y-y1)/(y2-y1)+x1: c = not c
    return c
def dist_to_edge(pt, poly):
    best = 1e9
    for i in range(len(poly)):
        (x1, y1), (x2, y2) = poly[i], poly[(i+1) % len(poly)]
        dx, dy = x2-x1, y2-y1; L = dx*dx+dy*dy
        t = 0 if L == 0 else max(0, min(1, ((pt[0]-x1)*dx+(pt[1]-y1)*dy)/L))
        best = min(best, math.hypot(pt[0]-(x1+t*dx), pt[1]-(y1+t*dy)))
    return best

# ---------------- Lid features ----------------
WALL_T = 3.0; WALL_H = 60.0; PLATE_T = 3.0
BTN_D = 13.0; STICK_D = 28.0; KNOB_D = 7.0; GRILLE_D = 3.0
CL = (-85.0, 58.0); CR = (85.0, 58.0); OFF = 20.0
BUTTONS = [  # name, centre, cap colour, short label
    ("UP",     (CL[0], CL[1]-OFF), "#2b2f36", "UP"),
    ("DOWN",   (CL[0], CL[1]+OFF), "#2b2f36", "DOWN"),
    ("LEFT",   (CL[0]-OFF, CL[1]), "#2b2f36", "LEFT"),
    ("RIGHT",  (CL[0]+OFF, CL[1]), "#2b2f36", "RIGHT"),
    ("INFO",   (CR[0], CR[1]-OFF), "#2f6fd6", "INFO"),
    ("ACCEPT", (CR[0], CR[1]+OFF), "#2e9d4f", "ACCEPT"),
    ("BACK",   (CR[0]-OFF, CR[1]), "#e8b100", "BACK"),
    ("REJECT", (CR[0]+OFF, CR[1]), "#d63a3a", "REJECT"),
]
STICKS = [("LEFT STICK", (-52.0, 104.0)), ("RIGHT STICK", (52.0, 104.0))]
KNOB = (0.0, 96.0)
GRILLE_C = (0.0, 124.0)
GRILLE = [(GRILLE_C[0]+dx, GRILLE_C[1]+dy) for dx, dy in
          [(0, 0), (5, 0), (-5, 0), (2.5, 4.33), (-2.5, 4.33), (2.5, -4.33), (-2.5, -4.33)]]
WIN_C, WIN_W, WIN_H = (0.0, 44.0), 60.0, 46.0
# Decorative screen bezel (glued ON TOP of the lid), rounded trapezoid
BEZ_TOP, BEZ_BOT, BEZ_TW, BEZ_BW, BEZ_R = 12.0, 76.0, 108.0, 92.0, 8.0
# Inside, on the base
BB = (-82.5, 62.0, 165.0, 55.0)     # full-size breadboard x, y, w, h
ESP = (-14.25, 4.0, 28.5, 51.5)     # ESP32 30-pin DOIT, USB end at the top wall
BED = 60.0                          # button bed square

if __name__ == "__main__":
    print("T1", [round(v, 2) for v in T1], "T2", [round(v, 2) for v in T2])
    print("T3", [round(v, 2) for v in T3], "T4", [round(v, 2) for v in T4], "APEX", APEX)
    print("size", 2*XMAX, "x", H, " perimeter", round(PERIM, 1), {k: round(v, 1) for k, v in seg.items()})
    poly = sample()
    for nm, p in [("bb corners", None)]: pass
    x, y, w, h = BB
    for c in [(x, y), (x+w, y), (x, y+h), (x+w, y+h), (0, y+h)]:
        print("breadboard corner", c, inside(c, poly), round(dist_to_edge(c, poly), 1))
    x, y, w, h = ESP
    for c in [(x, y), (x+w, y), (x, y+h), (x+w, y+h)]:
        print("esp corner", c, inside(c, poly), round(dist_to_edge(c, poly), 1))
    for nm, c, *_ in BUTTONS: print(nm, c, round(dist_to_edge(c, poly), 1))
    for nm, c in STICKS: print(nm, c, round(dist_to_edge(c, poly), 1))
    for s in (-1, 1):
        for cx, cy in [(CR[0]*s-BED/2, CR[1]-BED/2), (CR[0]*s+BED/2, CR[1]-BED/2), (CR[0]*s+BED/2, CR[1]+BED/2)]:
            print("bed corner", (cx, cy), inside((cx, cy), poly), round(dist_to_edge((cx, cy), poly), 1))
    # clearances between holes
    holes = [(n, c, BTN_D/2) for n, c, *_ in BUTTONS] + [(n, c, STICK_D/2) for n, c in STICKS] + [("KNOB(25 cap)", KNOB, 12.5)]
    m = min((math.dist(a[1], b[1]) - a[2] - b[2], a[0], b[0]) for i, a in enumerate(holes) for b in holes[i+1:])
    print("min hole gap", m)
