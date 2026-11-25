#ifndef TAXISIM_COMMON_INCLUDED
#define TAXISIM_COMMON_INCLUDED

// ---------------------------------------------------------------------------------------------
// Global values written every frame by TimeOfDay.cs and WeatherSystem.cs (Shader.SetGlobal*).
// They are declared outside UnityPerMaterial, so they do not break SRP Batcher compatibility.
// ---------------------------------------------------------------------------------------------
float  _TS_Night;        // 0 = full day .. 1 = full night
float  _TS_Wetness;      // 0 = dry .. 1 = soaked (monsoon)
float  _TS_Rain;         // 0 .. 1 rain intensity (ripples)
float4 _TS_SunDir;       // xyz: direction towards the sun (world space)
float4 _TS_MoonDir;      // xyz: direction towards the moon

// ----------------------------------- Hash & noise ------------------------------------------
float TS_Hash12(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float TS_Hash13(float3 p3)
{
    p3 = frac(p3 * 0.1031);
    p3 += dot(p3, p3.zyx + 31.32);
    return frac((p3.x + p3.y) * p3.z);
}

float2 TS_Hash22(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.xx + p3.yz) * p3.zy);
}

float TS_ValueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    float a = TS_Hash12(i);
    float b = TS_Hash12(i + float2(1, 0));
    float c = TS_Hash12(i + float2(0, 1));
    float d = TS_Hash12(i + float2(1, 1));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

float TS_Fbm(float2 p)
{
    float v = 0.0;
    float a = 0.5;
    [unroll] for (int i = 0; i < 4; i++)
    {
        v += a * TS_ValueNoise(p);
        p = p * 2.03 + 17.1;
        a *= 0.5;
    }
    return v;
}

// Anti-aliased line mask: 1 inside |x| < halfWidth, with a one-pixel soft edge.
float TS_Line(float x, float halfWidth)
{
    float w = max(fwidth(x), 1e-4);
    return 1.0 - smoothstep(halfWidth - w, halfWidth + w, abs(x));
}

// Rain ripples: returns a tangent-space normal rippleOffset (xy) for a flat surface at world xz.
float2 TS_RainRipples(float2 p, float time, float intensity)
{
    if (intensity <= 0.001) return float2(0, 0);
    float2 rippleOffset = float2(0, 0);
    float2 cellScale = float2(1.6, 1.6);
    [unroll] for (int layer = 0; layer < 2; layer++)
    {
        float2 q = p * cellScale + layer * 7.31;
        float2 cell = floor(q);
        float2 f = frac(q) - 0.5;
        float2 h = TS_Hash22(cell + layer * 13.7);
        float phase = frac(time * (0.9 + h.x * 0.6) + h.y);
        float2 center = (h - 0.5) * 0.5;
        float2 d = f - center;
        float r = length(d);
        float ring = sin((r - phase * 0.55) * 40.0) * smoothstep(0.55, 0.0, r) * (1.0 - phase);
        rippleOffset += (r > 1e-4 ? d / r : float2(0, 0)) * ring * 0.35;
        cellScale *= 1.37;
    }
    return rippleOffset * intensity;
}

float TS_NightCurve(float lo, float hi)
{
    return smoothstep(lo, hi, _TS_Night);
}

// ---------------------------------------------------------------------------------------------
// Neon Mumbai 2077 style (v0.4). Globals written by VisualStyle / TimeOfDay.
// ---------------------------------------------------------------------------------------------
float  _TS_Style;        // 0 = realistic Mumbai, 1 = Neon Mumbai 2077
float  _TS_FogDensity;   // custom height fog density (0 = off)
float  _TS_FogHeight;    // height fog falloff (units)
float4 _TS_FogColor;     // base fog colour
float4 _TS_NeonFog;      // neon-scattered fog colour near street level at night
float4 _TS_SkyZenith;     // current sky colours (for fallback reflections)
float4 _TS_SkyHorizon;
float4 _TS_SunColor;     // v0.10: sun colour × strength (0 at night) for haze in-scattering

// Neon tube palette: 1 magenta, 2 cyan, 3 amber, 4 violet, 5 lime
half3 TS_NeonColor(float idx)
{
    float k = fmod(max(idx, 1.0) - 1.0, 5.0);
    return k < 0.5 ? half3(1.0, 0.12, 0.75)
         : k < 1.5 ? half3(0.1, 0.9, 1.0)
         : k < 2.5 ? half3(1.0, 0.55, 0.1)
         : k < 3.5 ? half3(0.62, 0.25, 1.0)
         :           half3(0.55, 1.0, 0.25);
}

// Animated holographic advert / digital signage, uv in 0..1. No textures: fake text rows,
// rings, scrolling bars, scanlines and random glitch bands. Returns HDR colour.
half3 TS_AdPattern(float2 uv, float seed, float t)
{
    float kind = floor(frac(seed * 13.7) * 4.0);
    half3 c1 = TS_NeonColor(1.0 + floor(frac(seed * 3.1) * 5.0));
    half3 c2 = TS_NeonColor(1.0 + floor(frac(seed * 7.9) * 5.0));

    // glitch: random horizontal offsets in short bursts
    float glitch = step(0.965, TS_Hash12(float2(floor(t * 14.0), floor(uv.y * 18.0) + seed * 31.0)));
    uv.x = frac(uv.x + glitch * 0.08);

    float2 g = uv * float2(18.0, 7.0);
    float2 gc = floor(g);
    float2 gf = frac(g);
    float glyph = step(0.45, TS_Hash12(gc + floor(t * 0.5) * 3.1 + seed * 7.0))
                * step(0.15, gf.x) * step(gf.x, 0.85) * step(0.2, gf.y) * step(gf.y, 0.8);
    float rows = step(0.5, frac(gc.y * 0.5 + 0.25));

    float2 cc = uv - 0.5;
    float ring = 1.0 - smoothstep(0.0, 0.035, abs(length(cc * float2(1.6, 1.0)) - 0.28 - 0.04 * sin(t * 2.0 + seed * 9.0)));
    float bars = step(0.5, frac(uv.x * 6.0 - t * 0.6 + seed));

    float shape = kind < 0.5 ? ring
                : kind < 1.5 ? bars * 0.6
                : kind < 2.5 ? glyph * rows
                : max(ring * 0.7, glyph * rows * 0.8);

    float scan = 0.75 + 0.25 * sin(uv.y * 220.0 + t * 8.0);
    float edge = min(min(uv.x, 1.0 - uv.x), min(uv.y, 1.0 - uv.y));
    float border = 1.0 - step(0.03, edge);
    half3 col = lerp(c1, c2, saturate(uv.y + 0.2 * sin(t + uv.x * 3.0)));
    return col * (shape * 1.6 + 0.22 + border * 1.5) * scan * (1.0 + glitch * 2.0);
}

// Procedural printed advert (billboards / hoardings / bus shelters), uv 0..1, LDR albedo.
half3 TS_PosterPattern(float2 uv, float seed)
{
    float kind = floor(frac(seed * 7.13) * 4.0);
    half3 c1 = TS_NeonColor(1.0 + floor(frac(seed * 2.7) * 5.0)) * 0.75 + 0.1;
    half3 c2 = TS_NeonColor(1.0 + floor(frac(seed * 5.9) * 5.0));
    half3 bright = lerp(half3(0.98, 0.97, 0.94), half3(0.98, 0.85, 0.2), step(0.6, frac(seed * 3.3)));
    half3 dark = half3(0.06, 0.06, 0.08);

    half3 col = lerp(c1, c1 * 0.55, uv.y);
    col = lerp(col, lerp(bright, c1 * 0.4, 0.15), step(0.5, frac(seed * 9.1)) * step(uv.x, 0.55) * 0.8);

    // Product / hero shape on the right
    float2 p = uv - float2(0.74, 0.48);
    float circle = 1.0 - smoothstep(0.24, 0.25, length(p * float2(1.6, 1.0)));
    float bottle = step(abs(p.x), 0.07) * step(abs(p.y + 0.05), 0.3) + (1.0 - smoothstep(0.06, 0.07, length((p - float2(0, 0.28)) * float2(1, 1.5))));
    float car = step(abs(p.x), 0.22) * step(abs(p.y + 0.08), 0.08) + step(abs(p.x + 0.02), 0.12) * step(abs(p.y - 0.03), 0.06);
    float person = (1.0 - smoothstep(0.06, 0.07, length(p - float2(0, 0.2)))) + step(abs(p.x), 0.11 - p.y * 0.1) * step(abs(p.y + 0.1), 0.22);
    float hero = kind < 0.5 ? circle : kind < 1.5 ? saturate(bottle) : kind < 2.5 ? saturate(car) : saturate(person);
    col = lerp(col, c2 * 0.9 + 0.05, saturate(hero));

    // Headline (thick block letters) and sub-text
    float2 h = float2(uv.x * 14.0, uv.y * 6.0);
    float headRow = step(0.62, uv.y) * step(uv.y, 0.86) * step(0.06, uv.x) * step(uv.x, 0.52);
    float glyph = step(0.35, TS_Hash12(floor(float2(h.x * 1.5, h.y * 2.0)) + seed * 13.0)) * step(0.12, frac(h.x * 1.5));
    float subRow = step(0.36, uv.y) * step(uv.y, 0.5) * step(0.06, uv.x) * step(uv.x, 0.48) * step(0.5, frac(uv.y * 28.0));
    float sub = subRow * step(0.25, TS_Hash12(floor(float2(uv.x * 40.0, uv.y * 14.0)) + seed));
    col = lerp(col, lerp(dark, bright, step(0.5, frac(seed * 4.4))), saturate(headRow * glyph + sub * 0.8));

    // Logo badge and white border
    float logo = step(0.06, uv.x) * step(uv.x, 0.18) * step(0.08, uv.y) * step(uv.y, 0.22);
    col = lerp(col, c2, logo);
    float edge = min(min(uv.x, 1.0 - uv.x), min(uv.y, 1.0 - uv.y));
    col = lerp(half3(0.95, 0.95, 0.93), col, step(0.025, edge));
    return col;
}

#endif
