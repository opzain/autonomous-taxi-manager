import re, os, sys, subprocess, shutil, glob
HERE=os.path.dirname(os.path.abspath(__file__))
SH=os.path.abspath(os.path.join(HERE,"../../../unity/TaxiManager/Assets/_Project/Shaders"))
GR=os.path.abspath(os.path.join(HERE,"..",".cache","graphics"))
WORK=os.path.abspath(os.path.join(HERE,"..",".cache","work")); shutil.rmtree(WORK,ignore_errors=True); shutil.copytree(SH,WORK)
RICH={"_MAIN_LIGHT_SHADOWS_CASCADE","_ADDITIONAL_LIGHTS","_FORWARD_PLUS","_ADDITIONAL_LIGHT_SHADOWS","_SHADOWS_SOFT",
"_SCREEN_SPACE_OCCLUSION","_REFLECTION_PROBE_BLENDING","_REFLECTION_PROBE_BOX_PROJECTION","_CASTING_PUNCTUAL_LIGHT_SHADOW","_GBUFFER_NORMALS_OCT"}
only=sys.argv[1:] 
results=[]
for path in sorted(glob.glob(os.path.join(SH,"*.shader"))):
    name=os.path.basename(path)[:-7]
    if only and name not in only: continue
    text=open(path).read()
    text_nc=re.sub(r'//[^\n]*','',text)
    includes="\n".join(re.findall(r'HLSLINCLUDE(.*?)ENDHLSL',text_nc,re.S))
    passes=re.findall(r'HLSLPROGRAM(.*?)ENDHLSL',text_nc,re.S)
    pnames=re.findall(r'Name\s+"([^"]+)"',text_nc) or ["Pass"]
    for pi,prog in enumerate(passes):
        pname=pnames[pi] if pi<len(pnames) else f"Pass{pi}"
        vert=re.search(r'#pragma\s+vertex\s+(\w+)',prog).group(1)
        frag=re.search(r'#pragma\s+fragment\s+(\w+)',prog).group(1)
        mcs=re.findall(r'#pragma\s+multi_compile(?:_vertex|_fragment|_local)?\s+([^\n]+)',prog)
        rich=[]
        for line in mcs:
            opts=line.split()
            pick=[o for o in opts if o in RICH]
            if pick: rich.append(pick[0])
        if "multi_compile_fog" in prog: rich.append("FOG_EXP2")
        body=re.sub(r'#pragma[^\n]*','',prog)
        for variant,keys in (("base",[]),("rich",rich)):
            src="\n".join(f"#define {k} 1" for k in keys)+"\n"+includes+"\n"+body
            fn=os.path.join(WORK,f"{name}__{pname}__{variant}.hlsl")
            open(fn,"w").write(src)
            for stage,entry,sdef in (("vert",vert,"SHADER_STAGE_VERTEX"),("frag",frag,"SHADER_STAGE_FRAGMENT")):
                cmd=["glslangValidator","-D","-V","--target-env","vulkan1.1","-S",stage,"-e",entry,f"-I{WORK}",f"-I{GR}",
                     "-DSHADER_API_VULKAN=1","-DUNITY_VERSION=600000","-DSHADER_TARGET=45",f"-D{sdef}=1","-DUNITY_COMPILER_DXC=1",
                     "-o","/dev/null",fn]
                r=subprocess.run(cmd,capture_output=True,text=True)
                out=(r.stdout+r.stderr)
                errs=[l for l in out.splitlines() if "ERROR" in l and "compilation terminated" not in l]
                results.append((name,pname,variant,stage,r.returncode,errs[:6]))
ok=sum(1 for r in results if r[4]==0)
print(f"{ok}/{len(results)} stage compiles OK")
for r in results:
    if r[4]!=0:
        print(f"--- {r[0]} / {r[1]} / {r[2]} / {r[3]}")
        for e in r[5]: print("   ",e.replace(WORK+'/','').replace(GR+'/',''))
sys.exit(0 if ok==len(results) else 1)
