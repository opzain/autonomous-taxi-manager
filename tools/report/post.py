import docx, sys
from docx.shared import Inches, Pt
from docx.oxml.ns import qn
from docx.oxml import OxmlElement
f=sys.argv[1]
d=docx.Document(f)
for t in d.tables:
    tblPr=t._tbl.tblPr
    b=OxmlElement('w:tblBorders')
    for e in ['top','left','bottom','right','insideH','insideV']:
        el=OxmlElement(f'w:{e}'); el.set(qn('w:val'),'single'); el.set(qn('w:sz'),'4'); el.set(qn('w:color'),'A0AAB4'); b.append(el)
    tblPr.append(b)
    for c in t.rows[0].cells:
        tcPr=c._tc.get_or_add_tcPr(); sh=OxmlElement('w:shd'); sh.set(qn('w:val'),'clear'); sh.set(qn('w:fill'),'DCE6F0'); tcPr.append(sh)
        for p in c.paragraphs:
            for r in p.runs: r.bold=True
    for row in t.rows:
        for c in row.cells:
            for p in c.paragraphs:
                for r in p.runs: r.font.size=Pt(9.5)
for s in d.inline_shapes:
    if s.width>Inches(6.3): r=Inches(6.3)/s.width; s.width=Inches(6.3); s.height=int(s.height*r)
    if s.height>Inches(4.6): r=Inches(4.6)/s.height; s.height=Inches(4.6); s.width=int(s.width*r)
d.save(f)
