import re, sys
s=open(sys.argv[1]).read()
PB='```{=openxml}\n<w:p><w:r><w:br w:type="page"/></w:r></w:p>\n```'
title_end=s.index('## Abstract')
head=s[:title_end].replace('\n---\n','\n')
head=head.replace('# Autonomous Taxi Manager\n\n## A deterministic','# Autonomous Taxi Manager\n\n**A deterministic',1).replace('physical dispatch console\n','physical dispatch console**\n',1)
head='\n'.join((l+'\\' if l.strip() and not l.startswith('#') and i+1<len(head.split('\n')) and head.split('\n')[i+1].strip() else l) for i,l in enumerate(head.split('\n')))
body=s[title_end:]
secs=re.findall(r'^## (\d+\.[^\n]+)$', body, re.M)
toc='**Contents**\n\n'+'\\\n'.join(['Abstract']+[re.sub(r'^(\d+)\.', r'\1\\.', x) for x in secs]+['Appendix A: Document index','Appendix B: 4-minute demo script','Appendix C: Keyboard and mouse controls'])+'\n'
body=body.replace('\n---\n','\n')
body=body.replace('## 18. References', PB+'\n\n## 18. References',1)
# protocol table -> list (pipes inside code spans)
a=body.index('| Direction | Lines | Meaning |'); b=body.index('\n\n',a)
rows=[r for r in body[a:b].split('\n')[2:]]
items=[]
for r in rows:
    cells=[c.strip() for c in re.split(r'(?<!\\)\|',r)[1:-1]]
    d,line,m=cells
    if d: items.append(f'- **{d}**')
    items.append(f'    - {line.replace(chr(92)+"|","|")}: {m}')
body=body[:a]+'\n'.join(items)+body[b:]
s=head+'\n'+toc+'\n'+PB+'\n\n'+body
# blank line before a list that directly follows a paragraph line
lines=s.split('\n'); out=[]
for i,l in enumerate(lines):
    is_item=re.match(r'^(- |\d+\. )',l)
    if is_item and out and out[-1].strip() and not re.match(r'^(\s*- |\s*\d+\. |\s{2,}\S)',out[-1]) and not out[-1].startswith('|'):
        out.append('')
    out.append(l)
s='\n'.join(out)
s=re.sub(r'!\[[^\]]*\]\(([^)]+)\)\n\*([^*\n]+)\*', lambda m: f'![{m.group(2)}]({m.group(1)})', s)
open(sys.argv[2],'w').write(s)
