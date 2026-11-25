import math, os, sys
from geom import *
from draw import *

OUT = sys.argv[1] if len(sys.argv) > 1 else "out"
os.makedirs(OUT, exist_ok=True)

# ---------------------------------------------------------------- inside layout (plan view)
def inside_layout():
    g = [f'<clipPath id="inner"><path d="{outline_d()}"/></clipPath>']
    g.append(f'<path d="{outline_d()}" fill="#fbfbfb" stroke="#000" stroke-width="0.6"/>')
    g.append(f'<path d="{outline_d()}" fill="none" stroke="#c8a165" stroke-width="6" clip-path="url(#inner)"/>')
    x, y, w, h = BB
    g.append(rect(x, y, w, h, fill="#f4efe6", stroke="#8a6d3b", stroke_width=0.5, rx=2))
    for r in range(0, 63):
        g.append(circle((x+5+r*2.54, y+8), 0.35, fill="#b9a98a")); g.append(circle((x+5+r*2.54, y+h-8), 0.35, fill="#b9a98a"))
    g.append(text((0, y+h/2+1.5), "BREADBOARD (on the base)", 3.4, fill="#6b5323", weight=700))
    x, y, w, h = ESP
    g.append(rect(x, y, w, h, fill="#1f2a44", stroke="#000", stroke_width=0.4, rx=1.5))
    g.append(text((0, y+h/2), "ESP32", 3.2, fill="#fff", weight=700))
    g.append(text((0, y+h/2+4.2), "(base)", 2.6, fill="#c9d3e3"))
    g.append(rect(-5, 0, 10, 6.5, fill="#f2b705", stroke="#000", stroke_width=0.3))
    g.append(text((14, -2.4), "USB slot 14 × 10 in top wall", 3, fill="#8a5a00", weight=700, anchor="start"))
    # lid-mounted parts, dashed
    D = dict(fill="none", stroke="#1b5e9b", stroke_width=0.45, stroke_dasharray="2 1.2")
    g.append(rect(-36, 19, 86, 50, **D)); g.append(text((7, 74.5), "screen board 86 × 50 (under lid)", 2.8, fill="#1b5e9b", weight=600))
    for c in (CL, CR):
        g.append(rect(c[0]-BED/2, c[1]-BED/2, BED, BED, **D))
    g.append(text((CL[0], CL[1]+BED/2+4), "button bed", 2.8, fill="#1b5e9b", weight=600))
    g.append(text((CR[0], CR[1]+BED/2+4), "button bed", 2.8, fill="#1b5e9b", weight=600))
    for nm, c in STICKS:
        g.append(rect(c[0]-13, c[1]-14, 26, 34, **D)); g.append(circle(c, STICK_D/2, **D))
        g.append(text((c[0], c[1]+26), "joystick board", 2.8, fill="#1b5e9b", weight=600))
    g.append(rect(KNOB[0]-9.5, KNOB[1]-13, 19, 26, **D))
    g.append(circle(GRILLE_C, 6, **D))
    g.append(text((0, GRILLE_C[1]+10), "knob + buzzer", 2.8, fill="#1b5e9b", weight=600))
    g.append(text((-106, 172), "brown band = 3 mm wall", 2.8, fill="#8a6d3b", weight=600))
    return "".join(g)

# ---------------------------------------------------------------- side section (heights to scale)
def section():
    g = []
    L, T, B = 270, 0, 66  # lid top y=0, lid 0..3, inside 3..63, base 63..66
    card = dict(fill="#d9b27c", stroke="#8a6d3b", stroke_width=0.3)
    # lid with holes: button hole 45.5..58.5, window 106..166, stick hole 203..231
    for a, b in [(0, 45.5), (58.5, 106), (166, 203), (231, L)]:
        g.append(rect(a, 0, b-a, 3, **card))
    g.append(rect(0, 63, L, 3, **card))
    g.append(rect(0, 3, 3, 60, **card)); g.append(rect(L-3, 3, 3, 60, **card))
    # bezel on top of lid
    g.append(rect(92, -3, 14, 3, fill="#222")); g.append(rect(166, -3, 14, 3, fill="#222"))
    # button bed + corner blocks + switch + cap
    g.append(rect(22, 9, 60, 3, **card)); g.append(rect(22, 3, 12, 6, **card)); g.append(rect(70, 3, 12, 6, **card))
    g.append(rect(46, 5.5, 12, 3.5, fill="#333")); g.append(rect(49.5, 1, 5, 4.5, fill="#555"))
    g.append(f'<path d="M 46 -3 h 12 v 8.5 h -12 z" fill="#2e9d4f" stroke="#000" stroke-width="0.2"/>')
    g.append(line((40, 9.2), (46, 7.5), stroke="#d63a3a", stroke_width=0.6)); g.append(line((58, 7.5), (64, 9.2), stroke="#222", stroke_width=0.6))
    # TFT: glass 103..172 y 3..7.5, PCB 100..186 y 7.5..9.1, blocks, header + connectors
    g.append(rect(103, 3, 69, 4.5, fill="#0b1830")); g.append(rect(100, 7.5, 86, 1.6, fill="#c0392b"))
    g.append(rect(100, 3, 3, 4.5, **card)); g.append(rect(183, 3, 3, 4.5, **card))
    g.append(rect(179, 9.1, 3, 6, fill="#999")); g.append(rect(178.5, 15, 4, 14, fill="#222"))
    g.append(f'<path d="M 180.5 29 C 180 40, 170 44, 160 50" fill="none" stroke="#e67e22" stroke-width="0.8"/>')
    # joystick: fences 200..203 & 231..234 y 3..19, PCB 200..234 y 19..20.6, housing 207..227 y 3..19, shaft, cap
    g.append(rect(197, 3, 3, 16, **card)); g.append(rect(234, 3, 3, 16, **card))
    g.append(rect(197, 19, 40, 1.6, fill="#1d6fb8")); g.append(rect(207, 4, 20, 15, fill="#2b2b2b"))
    g.append(rect(215, -8, 4, 12, fill="#444")); g.append(f'<path d="M 204.5 -8 h 25 v -7 q -12.5 -4 -25 0 z" fill="#1b1d22"/>')
    # base: breadboard 30..195, ESP32 upside down at 210..238.5 on a pad
    g.append(rect(30, 53.5, 165, 9.5, fill="#f4efe6", stroke="#8a6d3b", stroke_width=0.3))
    for xx in (60, 90, 120, 150):
        g.append(rect(xx, 39.5, 2.5, 14, fill="#222"))
        g.append(f'<path d="M {xx+1.25} 39.5 C {xx+1} 33, {xx+8} 31, {xx+14} 30" fill="none" stroke="#27ae60" stroke-width="0.8"/>')
    g.append(rect(210, 57, 28.5, 6, **card)); g.append(rect(212, 53.5, 24, 3.5, fill="#aaa"))
    g.append(rect(210, 51.9, 28.5, 1.6, fill="#1f2a44"))
    g.append(rect(211, 43.5, 2.5, 8.4, fill="#c8c8c8")); g.append(rect(235, 43.5, 2.5, 8.4, fill="#c8c8c8"))
    g.append(rect(210.6, 29.5, 3.3, 14, fill="#222")); g.append(rect(234.6, 29.5, 3.3, 14, fill="#222"))
    # dimension call-outs
    R = L + 6
    g.append(dim_v(0, 3, R+4, "3", size=2.8)); g.append(dim_v(3, 63, R+4, "60 wall", size=2.8)); g.append(dim_v(63, 66, R+4, "3", size=2.8))
    g.append(dim_v(0, 66, R+14, "66 total", size=3))
    g.append(dim_v(3, 9, 16, "6", size=2.6))
    g.append(dim_v(3, 19, 241, "16", size=2.6))
    g.append(dim_v(-3, 0, 42, "≈3", size=2.4))
    g.append(dim_v(53.5, 63, 26, "9.5", size=2.6))
    lab = [((52, -9), "12 mm cap pokes ~3 mm out (Ø13 hole)"), ((52, 17.5), "button bed + 2-layer corner blocks"),
           ((136, -6), "black bezel on top"), ((137, 16), "screen glass touches lid, 1-layer blocks at board corners"),
           ((217, -18), "stick cap above lid (Ø28 hole)"), ((216, 26), "fences 34 × 16 on edge"),
           ((112, 72), "breadboard + jumpers ≈ 33 tall"), ((224, 72), "ESP32 upside down on a 6 mm pad")]
    for p, s in lab:
        g.append(text(p, s, 2.8, fill="#222", weight=600))
    return "".join(g)

lid = lid_template(); base = base_template()
OX, OY = 148.5, 12.0
bar = check_bar(-50, 177)

def page_land(inner):
    return f'<div class="page land">{svg_wrap(inner, (-OX, -OY, 297, 210), 297, 210)}</div>'

templates_html = f"""<!doctype html><html><head><meta charset="utf-8"><title>Controller templates 1:1</title>
<style>
@page land {{ size: 297mm 210mm; margin: 0 }}
@page port {{ size: 210mm 297mm; margin: 0 }}
html, body {{ margin: 0; padding: 0; background: #fff; }}
.page {{ overflow: hidden; break-after: page; }}
.page:last-child {{ break-after: auto; }}
.land {{ page: land; width: 297mm; height: 210mm; }}
.port {{ page: port; width: 210mm; height: 297mm; }}
svg {{ display: block; }}
</style></head><body>
{page_land(lid + bar)}
{page_land(base + bar)}
<div class="page port">{svg_wrap(parts_page(), (0, 0, 210, 297), 210, 297)}</div>
</body></html>"""
open(f"{OUT}/templates.html", "w").write(templates_html)

# ---------------------------------------------------------------- guide
def S(inner, vb, extra=""): return svg_wrap(inner, vb, width_css="100%", extra=extra)
V_LID = (-XMAX-8, -10, 2*XMAX+16, H+18)
render_svg = S(render(), (-XMAX-6, -6, 2*XMAX+14, H+14))
cons_svg = S(construction(), (-XMAX-18, -10, 2*XMAX+30, H+28))
holes_svg = S(holes_drawing(), (-XMAX-6, -12, 2*XMAX+12, H+18))
inside_svg = S(inside_layout(), (-XMAX-6, -8, 2*XMAX+12, H+14))
section_svg = S(section(), (-4, -22, 310, 100))

for nm, svg in [("render", render_svg), ("construction", cons_svg), ("holes", holes_svg), ("inside", inside_svg), ("section", section_svg)]:
    open(f"{OUT}/{nm}.svg", "w").write(svg.replace('width="100%"', 'width="1600"'))

def r1(v): return fmt(round(v, 1))
hole_rows = []
sym_side = lambda x: "centre" if x == 0 else ("left" if x < 0 else "right")
for name, c, col, lab in BUTTONS:
    hole_rows.append((lab, c, "Ø13 circle", "12 × 12 tactile + 12 mm cap"))
for name, c in STICKS:
    hole_rows.append((name.title(), c, "Ø28 circle", "KY-023 joystick"))
hole_rows.append(("Knob", KNOB, "Ø7 circle", "KY-040 shaft + nut"))
hole_rows.append(("Buzzer grille", GRILLE_C, "7 × Ø3", "centre hole + 6 around it, 5 mm apart"))
hole_rows.append(("Screen window", WIN_C, "60 × 46 rectangle", "corners at x ±30, y 21 and 67"))
hole_tr = "".join(f"<tr><td>{n}</td><td>{fmt(c[0])}</td><td>{fmt(c[1])}</td><td>{s}</td><td>{p}</td></tr>" for n, c, s, p in hole_rows)

pts_tr = "".join(f"<tr><td><b>{n}</b></td><td>{r1(p[0])}</td><td>{r1(p[1])}</td><td>{d}</td></tr>" for n, p, d in [
    ("P", TOPC1, "end of the straight top edge; the shoulder circle starts here"),
    ("C1", C1, "shoulder circle centre, radius 40"),
    ("T1", T1, "where the side line leaves the shoulder circle"),
    ("T2", T2, "where the side line meets the grip circle"),
    ("C2", C2, "grip circle centre, radius 28"),
    ("T3", T3, "where the inner grip line leaves the grip circle"),
    ("T4", T4, "where the inner grip line meets the arch circle"),
    ("C3", C3, "arch circle centre, radius 40 (curves INTO the body)"),
    ("A", APEX, "middle of the arch, between the grips")])

GPC = r1(PERIM)
guide_html = f"""<!doctype html><html><head><meta charset="utf-8"><title>Cardboard Controller Build Pack</title>
<style>
@page {{ size: A4; margin: 14mm 14mm 16mm; @bottom-center {{ content: counter(page); font: 9pt Inter; color: #777; }} }}
:root {{ --ink: #14213D; --teal: #1B7F7A; --amber: #F2B705; --muted: #4A5568; --line: #D9D4C7; }}
body {{ font-family: Inter, 'DejaVu Sans', sans-serif; color: var(--ink); font-size: 10pt; line-height: 1.45; margin: 0; }}
h1 {{ font-size: 25pt; margin: 0 0 2mm; letter-spacing: -0.5px; line-height: 1.1; }}
h2 {{ font-size: 15pt; margin: 0 0 3mm; padding-top: 1mm; border-bottom: 2px solid var(--amber); padding-bottom: 1.5mm; }}
h3 {{ font-size: 11pt; margin: 4mm 0 1.5mm; color: var(--teal); }}
p {{ margin: 0 0 2.5mm; }}
.eyebrow {{ font-size: 8.5pt; letter-spacing: 2px; text-transform: uppercase; color: var(--teal); font-weight: 700; }}
.lead {{ font-size: 11.5pt; color: var(--muted); }}
.brk {{ break-before: page; }}
.stats {{ display: grid; grid-template-columns: repeat(4, 1fr); gap: 3mm; margin: 4mm 0; }}
.stat {{ border-top: 3px solid var(--teal); background: #f6f4ef; padding: 2.5mm 3mm; }}
.stat b {{ display: block; font-size: 13pt; }}
.stat span {{ font-size: 8.5pt; color: var(--muted); }}
table {{ border-collapse: collapse; width: 100%; margin: 2mm 0 4mm; font-size: 9pt; }}
th, td {{ text-align: left; padding: 1.6mm 2mm; border-bottom: 1px solid var(--line); vertical-align: top; }}
th {{ background: #e9eef5; font-weight: 700; }}
td:nth-child(2), td:nth-child(3) {{ font-variant-numeric: tabular-nums; }}
.note {{ break-inside: avoid; background: #fff6d6; border-left: 4px solid var(--amber); padding: 2.5mm 3.5mm; margin: 3mm 0; }}
.warn {{ background: #fdecea; border-left: 4px solid #c0392b; padding: 2.5mm 3.5mm; margin: 3mm 0; }}
ol {{ padding-left: 6mm; margin: 0 0 2mm; }} li {{ margin-bottom: 1.6mm; }}
ul {{ padding-left: 5mm; margin: 0 0 2mm; }}
figure {{ margin: 2mm 0 3mm; }} figure.s svg {{ width: 78%; display: block; margin: 0 auto; }} table.tight td, table.tight th {{ padding: 1.1mm 1.6mm; font-size: 8.5pt; }} figcaption {{ font-size: 8.5pt; color: var(--muted); margin-top: 1mm; }}
.two {{ display: grid; grid-template-columns: 1fr 1fr; gap: 6mm; }}
.step {{ break-inside: avoid; }}
.tag {{ display: inline-block; font-size: 8pt; font-weight: 700; background: #e9eef5; padding: 0.3mm 1.6mm; border-radius: 2px; }}
</style></head><body>

<p class="eyebrow">Autonomous Taxi Manager · dispatch console v2</p>
<h1>Cardboard controller build pack</h1>
<p class="lead">A PS5-style two-grip controller made from corrugated cardboard. It holds the 2.8″ screen, 8 big buttons, 2 joysticks, the rotary knob and the buzzer. The ESP32 and the breadboard go inside. Every measurement is in millimetres.</p>
<figure>{render_svg}<figcaption>How it will look: white card shell, black screen bezel, coloured caps for ACCEPT / REJECT / BACK / INFO, dark caps for the arrows. The blue light-bar strips are just decoration (blue tape).</figcaption></figure>
<div class="stats">
<div class="stat"><b>268 × 186 × 66</b><span>width × length × thickness, mm</span></div>
<div class="stat"><b>8 × Ø13</b><span>button holes, 20 mm from each cluster centre</span></div>
<div class="stat"><b>2 × Ø28</b><span>joystick holes</span></div>
<div class="stat"><b>60 × 46</b><span>screen window</span></div>
</div>
<h3>What's in this pack</h3>
<ul>
<li><b>controller-templates-1to1.pdf</b>: page 1 is the lid, page 2 the base, page 3 the small parts. Print them on A4 at <b>100 % / Actual size</b>, never "Fit to page". Each page has a 100 mm check bar.</li>
<li><b>This guide</b> gives the shopping list, the cutting list, both measured drawings (so you can draw without a printer), the inside layout, the build steps and the new pin plan.</li>
</ul>

<h2 class="brk">1. Parts and materials</h2>
<h3>Buy</h3>
<table><tr><th>Item</th><th>Qty</th><th>Notes</th></tr>
<tr><td>12 × 12 × 7.3 mm tactile switch with round cap</td><td>8 (+2 spare)</td><td>Caps in four colours if possible: green ACCEPT, red REJECT, yellow BACK, blue INFO, and black or grey for the arrows.</td></tr>
<tr><td>Joystick module KY-023 ("PS2 joystick module")</td><td>2</td><td>5 pins: GND, +5V, VRx, VRy, SW. It will run on <b>3.3 V</b>.</td></tr>
<tr><td>Knob cap for the KY-040, 6 mm D-shaft</td><td>1</td><td>About 25 mm across. Optional: the bare shaft also works.</td></tr>
</table>
<h3>Tools and card</h3>
<ul>
<li>2–3 large corrugated boxes, <b>3 mm single-wall</b> (an Amazon or Flipkart box is ideal). 5 mm card also works; see step 0.</li>
<li>Craft knife or box cutter, steel ruler, cutting mat (or old card underneath), compass from a geometry box, set square, pencil.</li>
<li>Hot-glue gun and 6–8 sticks, masking tape, small pliers to bend switch legs.</li>
<li>Optional finish: white chart paper and glue stick, black poster paint or a black marker, blue tape.</li>
</ul>
<h3>Cutting list</h3>
<table><tr><th>Piece</th><th>Size, mm</th><th>Qty</th><th>Where</th></tr>
<tr><td>Lid</td><td>268 × 186 outline</td><td>1</td><td>Template page 1. Cut all holes.</td></tr>
<tr><td>Base</td><td>same outline</td><td>1</td><td>Template page 2. No holes.</td></tr>
<tr><td>Wall strip</td><td>60 tall, {GPC} long in total</td><td>3 × (60 × 300)</td><td>Ruler. <b>Corrugation lines must run across the 60 mm</b>, so the strip bends smoothly.</td></tr>
<tr><td>Wall joint patch</td><td>30 × 60</td><td>3</td><td>Ruler</td></tr>
<tr><td>Screen bezel</td><td>108 / 92 × 64, window 60 × 46</td><td>1</td><td>Template page 3</td></tr>
<tr><td>Button bed</td><td>60 × 60</td><td>2</td><td>Template page 3</td></tr>
<tr><td>Corner block</td><td>12 × 12</td><td>16</td><td>Template page 3</td></tr>
<tr><td>Joystick fence</td><td>34 × 16</td><td>4</td><td>Template page 3 (16 = your measured height, step 0)</td></tr>
<tr><td>Screen block</td><td>10 × 10</td><td>4</td><td>Template page 3</td></tr>
<tr><td>ESP32 pad</td><td>30 × 50</td><td>2 layers</td><td>Ruler</td></tr>
<tr><td>Feet (optional)</td><td>Ø20</td><td>4</td><td>Template page 3</td></tr>
</table>

<h2 class="brk">2. Drawing A: the outline (draw it by hand)</h2>
<p>If you can't print, draw the outline with a ruler, set square and compass. It is made of <b>3 circles per side and straight lines</b> only. Every point is written as <b>(x, y)</b>:</p>
<ul>
<li><b>x</b> is measured sideways from the centre line (right is +, left is −);</li>
<li><b>y</b> is measured down from the top edge.</li>
</ul>
<figure class="s">{cons_svg}<figcaption>Red dashed circles are construction circles: draw them lightly, then keep only the parts that form the outline.</figcaption></figure>
<table class="tight"><tr><th>Point</th><th>x</th><th>y</th><th>What it is</th></tr>{pts_tr}</table>
<ol>
<li>Draw a straight 300 mm line: this is the <b>top edge (y = 0)</b>. Mark its middle, then draw the <b>centre line</b> straight down from it, 200 mm long, with the set square.</li>
<li>Mark the centres C1, C2 and C3 for the right side, and the mirror points C1′ (−88, 40) and C2′ (−106, 158) for the left side. Measure x along the top line, then y straight down.</li>
<li>Draw the circles with the compass: <b>R40</b> at C1 and C1′, <b>R28</b> at C2 and C2′, <b>R40</b> at C3.</li>
<li>Join the points with the ruler:
  <ul>
  <li>top edge P′ (−88, 0) → P (88, 0);</li>
  <li>side lines T1 → T2 and T1′ → T2′;</li>
  <li>inner grip lines T3 → T4 and T3′ → T4′.</li>
  </ul>
  Each line should just touch both of its circles.</li>
<li>Go over the outline:
  <ul>
  <li>the shoulder arc from P to T1;</li>
  <li>the grip arc from T2 round the bottom to T3;</li>
  <li>the short arch from T4 through A (0, 140) to T4′.</li>
  </ul>
  Rub out the rest of the construction circles.</li>
<li><b>Check:</b> the width is 268 at the widest point (the grips) and it is 186 from the top edge down to the bottom of each grip.</li>
</ol>

<h2 class="brk">3. Drawing B: the holes in the lid</h2>
<figure class="s">{holes_svg}<figcaption>Blue numbers are millimetres. The left side is a mirror image of the right. The dashed shape is where the black bezel is glued on top (not cut).</figcaption></figure>
<table class="tight"><tr><th>Hole</th><th>x</th><th>y</th><th>Size</th><th>For</th></tr>{hole_tr}</table>
<div class="note"><b>Cutting holes in corrugated card:</b>
<ul>
<li>Draw each circle with the compass and cut it with many short knife strokes, going round twice.</li>
<li>The 3 mm grille holes: poke them with a ballpoint refill or a small screwdriver.</li>
<li>The 7 mm knob hole: poke it with a pencil, then trim it round.</li>
<li>The window: cut along a steel ruler.</li>
</ul></div>

<h2 class="brk">4. Inside layout and heights</h2>
<figure class="s">{inside_svg}<figcaption>Solid = sits on the base. Blue dashed = hangs under the lid. The full-size breadboard fits between the grips with about 20 mm to spare. The ESP32's USB end sits at the top wall, in the middle, like the charging port of a real controller.</figcaption></figure>
<figure>{section_svg}<figcaption>Side section, heights to scale (the left-to-right positions are only illustrative). Total thickness is 66 mm: 3 lid + 60 wall + 3 base. A real PS5 controller is about 65 mm thick at the grips.</figcaption></figure>

<h2 class="brk">5. Build steps</h2>
<div class="step"><h3>Step 0 · Test and measure before cutting the real pieces (10 min)</h3>
<ol>
<li>On scrap card, cut one <b>Ø13</b> and one <b>Ø28</b> hole.
  <ul>
  <li>A button cap should pass through the Ø13 hole without rubbing.</li>
  <li>A joystick pushed up through the Ø28 hole should tilt fully in every direction. If it scrapes, use <b>Ø30</b> on the real lid.</li>
  </ul></li>
<li>Measure three things with a ruler and write them down:
  <ul>
  <li><b>(a)</b> a switch with its cap on, from the bottom of the body to the top of the cap. This is usually 10–13 mm.</li>
  <li><b>(b)</b> a joystick, from the top of its board to the top of the black plastic housing. This is usually about 16 mm, and becomes the fence height.</li>
  <li><b>(c)</b> the screen, from the glass face to the red board. This is usually about 4 mm.</li>
  </ul></li>
<li>Measure your card thickness. All stacks in this guide assume 3 mm. With 5 mm card, use one layer where it says two.</li>
</ol></div>
<div class="step"><h3>Step 1 · Print and transfer</h3>
<ol>
<li>Print the templates PDF at <b>100 % / Actual size</b> and check the 100 mm bar with a ruler.</li>
<li>Tip for a clean white finish: glue white chart paper onto the lid card <i>first</i> with a glue stick, then cut through both.</li>
<li>Tape the template on the card and cut through it. Alternatively, prick every hole centre and the outline corners through the paper with a compass point, then join the marks.</li>
</ol></div>
<div class="step"><h3>Step 2 · Lid and base</h3>
<p>Cut the lid outline and all its holes. Cut the base (outline only). Mark the small centre ticks at the top edge and the arch on both pieces.</p></div>
<div class="step"><h3>Step 3 · Wall</h3>
<ol>
<li>Cut 3 strips of 60 × 300. The corrugation lines must run across the 60 mm side, so the strip curls easily.</li>
<li>Lay the lid <b>top face down</b>. Start at the arch tick. Hot-glue the strip's edge onto the underside of the lid, with its outer face flush with the lid's edge. Glue 8–10 cm at a time and press it into the curves.</li>
<li>Butt the next strip against the end of the last one, then glue a 30 × 60 patch inside across the joint.</li>
<li>Trim the last strip to meet the start. The whole wall is {GPC} mm.</li>
</ol></div>
<div class="step"><h3>Step 4 · Screen</h3>
<ol>
<li>Switch the console on so the screen lights up. Lay the screen face down on the window from the inside, so the lit area fills the window evenly. Mark the board's corners.</li>
<li>Glue the four 10 × 10 blocks to the lid at the corners. Keep them clear of the 14 header pins.</li>
<li>Hot-glue the red board to the blocks. Never glue on the glass. If the glass doesn't touch the lid, add a thin shim on each block.</li>
</ol></div>
<div class="step"><h3>Step 5 · Buttons</h3>
<ol>
<li>Glue two layers of corner blocks on each dashed square of both beds, so each corner stands 6 mm tall.</li>
<li>Bend each switch's 4 legs flat outwards with pliers. Glue each switch, cap on, onto its 12 × 12 square with a dot of hot glue under the body.</li>
<li>Push a female jumper onto <b>two diagonal legs</b> of every switch. Diagonal legs work whichever way round the switch is. Lock each jumper with a small dab of glue, and flag the wires with masking tape (UP, DOWN …).</li>
<li>Offer the bed up under the lid so the caps rise through the Ø13 holes. The caps should stand <b>2–4 mm</b> proud and click freely.
  <ul>
  <li>Too low: add a card layer under the switch.</li>
  <li>Stuck down: remove one corner-block layer.</li>
  </ul>
  Then glue the corner blocks to the lid.</li>
</ol>
<p>Cap height works out as (a) − 6 bed gap − 3 lid. A 12 mm switch with its cap leaves 3 mm showing.</p></div>
<div class="step"><h3>Step 6 · Joysticks</h3>
<ol>
<li>From inside, push the stick up through the Ø28 hole until the black housing touches the lid. Turn both boards so their <b>pins point towards the grips</b>. That way both sticks read the same way round in the firmware.</li>
<li>Glue two fences (34 × 16, height = your measurement (b)) on their long edge to the lid, one on each side of the board. Then hot-glue the board's edges to the fences.</li>
<li>Test: the stick moves fully in all directions and springs back to the centre.</li>
</ol></div>
<div class="step"><h3>Step 7 · Knob and buzzer</h3>
<ol>
<li>Take the nut and washer off the encoder. Push the shaft up through the Ø7 hole from inside, put the washer and nut back on top, tighten gently and push on the knob. Turn it so its pins point towards the grips. With 5 mm card the nut may not reach the thread; then glue the encoder from inside instead.</li>
<li>Hot-glue the buzzer under the grille, centred. Push two male–female jumpers onto its legs (female end on the legs) and take them to the old buzzer holes on the breadboard: <b>− to b47, + to b50</b>.</li>
</ol></div>
<div class="step"><h3>Step 8 · Base, USB slot and closing</h3>
<ol>
<li>Stick the breadboard to the base on its dashed area. Glue the 2-layer ESP32 pad, then lay the ESP32 <b>upside down (pins up)</b> on it with its USB end at the top wall position.</li>
<li>Measure the height of the middle of the USB port above the base. Cut a <b>14 × 10</b> slot in the top wall at the centre tick, with its middle at that height from the wall's bottom edge. Test with the cable.</li>
<li>Wire everything (next page). Then put the base on and hold it with 4 strips of cloth or masking tape, so you can open it again. Add the feet.</li>
<li>Finish: glue on the bezel, paint it black, add the blue light-bar tape and stick on small labels next to the buttons.</li>
</ol></div>

<h2 class="brk">6. Wiring and new pin plan</h2>
<div class="warn"><b>Don't move any wires yet.</b> The firmware on your ESP32 still uses the old pins. Build the box first, then ask for the <b>firmware v0.5 / game update</b>. It adds the INFO button, reads both joysticks (left stick pans the city camera, right stick rotates and zooms) and switches to the pins below. Rewire only once that update is on the board.</div>
<p>The 30-pin ESP32 has 23 usable pins. The screen and buzzer keep their 6. The other 17 are shared out like this, with the LED stick left out. Pins D2 and D12 stay empty, because they are boot pins.</p>
<table class="tight"><tr><th>Control</th><th>ESP32 pin</th><th>Wires</th><th>Change from today</th></tr>
<tr><td>UP</td><td>D19</td><td>F-F to the pin, M-F to GND</td><td>moved from D32</td></tr>
<tr><td>DOWN</td><td>D22</td><td>same</td><td>moved from D33 (D22 was for the LED stick)</td></tr>
<tr><td>LEFT / RIGHT</td><td>D25 / D26</td><td>same</td><td>same pins</td></tr>
<tr><td>BACK / ACCEPT / REJECT</td><td>D27 / D14 / D13</td><td>same</td><td>same pins</td></tr>
<tr><td>INFO (8th button)</td><td>D15</td><td>same</td><td><span class="tag">NEW</span></td></tr>
<tr><td>Knob CLK / DT</td><td>D32 / D33</td><td>F-F</td><td>moved from D34 / D35</td></tr>
<tr><td>Knob SW, + and GND</td><td>D4, 3V3 rail, GND</td><td>as now</td><td>no change</td></tr>
<tr><td>Left stick VRx / VRy</td><td>VP (GPIO36) / VN (GPIO39)</td><td>F-F</td><td><span class="tag">NEW</span></td></tr>
<tr><td>Right stick VRx / VRy</td><td>D34 / D35</td><td>F-F</td><td><span class="tag">NEW</span></td></tr>
<tr><td>Both sticks "+5V" and GND</td><td><b>3V3 rail</b> and GND rail</td><td>M-F</td><td><span class="tag">NEW</span> never 5 V: the ESP32 can only read up to 3.3 V</td></tr>
<tr><td>Stick clicks (SW)</td><td>not connected</td><td>–</td><td>The knob press does that job. A click can be added later by moving the screen's RESET wire to 3V3, which frees TX2.</td></tr>
<tr><td>Screen, buzzer</td><td>as now</td><td>–</td><td>Only the buzzer gets the 2 extension wires from step 7.</td></tr>
</table>
<h3>Every button, the same way</h3>
<ul>
<li>One diagonal leg goes to its ESP32 pin with a <b>female–female</b> wire.</li>
<li>The other diagonal leg goes to the <b>GND (blue) rail</b> with a <b>male–female</b> wire.</li>
<li>The 7 old buttons and their 14 wires come off the breadboard. The rails, the buzzer transistor and the resistors stay where they are.</li>
</ul>
<h3>Wire count</h3>
<table class="tight"><tr><th>Type</th><th>New</th><th>Freed from old buttons</th><th>You have</th></tr>
<tr><td>Female–female</td><td>12 (8 buttons + 4 stick signals)</td><td>–</td><td>40 bought, 8 in use</td></tr>
<tr><td>Male–female</td><td>14 (8 button GND + 4 stick power + 2 buzzer)</td><td>7</td><td>40 bought</td></tr>
</table>
<div class="note"><b>Checklist before closing the box:</b>
<ul>
<li>Every cap clicks and springs back.</li>
<li>Both sticks centre themselves.</li>
<li>The knob turns freely.</li>
<li>The USB cable plugs in without pushing the ESP32 off its pad.</li>
<li>No bare leg touches another.</li>
<li>Then run the self-test screen and press every control through the lid.</li>
</ul></div>
</body></html>"""
open(f"{OUT}/guide.html", "w").write(guide_html)
print("ok", PERIM)
