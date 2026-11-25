import re, os, sys, urllib.request, posixpath
BASE="https://raw.githubusercontent.com/Unity-Technologies/Graphics/6000.0/staging/"
import os
ROOT=os.path.join(os.path.dirname(os.path.abspath(__file__)),"..",".cache","graphics")
seen=set(); missing=[]
inc=re.compile(r'^\s*#\s*include(?:_with_pragmas)?\s+"([^"]+)"',re.M)
def fetch(path):
    if path in seen: return
    seen.add(path)
    local=os.path.join(ROOT,path)
    if not os.path.exists(local):
        try:
            data=urllib.request.urlopen(BASE+path,timeout=30).read().decode('utf-8','replace')
        except Exception as e:
            missing.append(path); return
        os.makedirs(os.path.dirname(local),exist_ok=True); open(local,'w').write(data)
    text=open(local).read()
    for m in inc.findall(text):
        if m.startswith("Packages/"): fetch(m)
        else: fetch(posixpath.normpath(posixpath.join(posixpath.dirname(path),m)))
seeds=["Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl",
"Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl",
"Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl",
"Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"]
for s in seeds: fetch(s)
print("fetched",len(seen)-len(missing),"missing",len(missing)); print("\n".join(missing[:40]))
