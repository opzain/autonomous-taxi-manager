"""Patches the LOCAL CACHED copy of Unity's shader library so glslang (a stand-in for Unity's
DXC/FXC compilers) accepts it. Never touches the project's own shaders.
 - drops min16float template overloads (glslang treats min16float == float -> duplicates)
 - #include_with_pragmas -> #include
 - HALF_IS_FLOAT uses defined() inside a macro (DXC accepts, glslang does not) -> 1
 - removes remaining duplicate half/float overloads reported by glslang"""
import glob, os, re, subprocess
HERE = os.path.dirname(os.path.abspath(__file__))
GR = os.path.join(HERE, "..", ".cache", "graphics")
CORE = os.path.join(GR, "Packages/com.unity.render-pipelines.core/ShaderLibrary")

p = os.path.join(CORE, "Macros.hlsl")
lines = open(p).read().split("\n")
open(p, "w").write("\n".join(l for l in lines if not ("min16" in l and "FunctionName(" in l)))
for f in glob.glob(os.path.join(GR, "**/*.hlsl"), recursive=True):
    t = open(f).read()
    if "#include_with_pragmas" in t:
        open(f, "w").write(t.replace("#include_with_pragmas", "#include"))
p = os.path.join(CORE, "Common.hlsl")
t = open(p).read().replace("#define HALF_IS_FLOAT (!defined(UNITY_DEVICE_SUPPORTS_NATIVE_16BIT))", "#define HALF_IS_FLOAT 1")
open(p, "w").write(t)

probe = os.path.join(HERE, "..", ".cache", "probe.hlsl")
open(probe, "w").write('#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"\n'
                       '#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"\n'
                       'float4 frag() : SV_Target { return 1; }\n')
dup = re.compile(r"ERROR: (\S+?):(\d+): '(\w+)' : function already has a body")
for defs in ([], ["_MAIN_LIGHT_SHADOWS_CASCADE", "_ADDITIONAL_LIGHTS", "_FORWARD_PLUS", "_SHADOWS_SOFT", "_SCREEN_SPACE_OCCLUSION",
                  "_REFLECTION_PROBE_BLENDING", "_REFLECTION_PROBE_BOX_PROJECTION", "FOG_EXP2"]):
    for _ in range(80):
        cmd = ["glslangValidator", "-D", "-V", "-S", "frag", "-e", "frag", f"-I{GR}", "-DSHADER_API_VULKAN=1", "-DUNITY_VERSION=600000",
               "-DSHADER_TARGET=45", "-DSHADER_STAGE_FRAGMENT=1"] + [f"-D{d}=1" for d in defs] + ["-o", os.devnull, probe]
        r = subprocess.run(cmd, capture_output=True, text=True)
        out = r.stdout + r.stderr
        if r.returncode == 0:
            print("Unity URP library OK for glslang", defs[:1]); break
        m = dup.search(out)
        if not m:
            print("Unexpected library error:\n" + "\n".join(out.splitlines()[:8])); break
        f, line, name = m.group(1), int(m.group(2)), m.group(3)
        src = open(f).read().split("\n")
        i = line - 1
        sig = re.compile(r"^[A-Za-z_][\w<>, ]*\b" + name + r"\s*\(")
        while i > 0 and not sig.match(src[i]): i -= 1
        depth, started, j = 0, False, i
        while j < len(src):
            depth += src[j].count("{")
            started = started or "{" in src[j]
            depth -= src[j].count("}")
            if started and depth <= 0: break
            j += 1
        del src[i:j + 1]
        open(f, "w").write("\n".join(src))
