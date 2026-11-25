// Procedural skyscraper / building facade for the BKC city.
//  * Curtain-wall glass (style 0) .. punched concrete windows (style 1), per building
//  * Interior mapping: fake 3D rooms behind every window (parallax correct)
//  * Night: rooms switch on/off over time, warm/cool office lights, lobby glow, LED crown bands
//  * Monsoon: wet darkening, higher gloss, grime streaks on concrete
// Mesh data written by TowerMeshBuilder.cs:
//  uv0 = (u along facade, height above ground) in world units, roof = local xz
//  uv1 = (seed, style, total height, accent)     uv2 = (glass tint rgb, tarp roof flag)
//  uv3 = (facade length, neon hue 0..5, holo-ad flag, 0)   (v0.4 Neon Mumbai 2077)
//  color = frame / concrete colour, tangent = facade horizontal direction
Shader "TaxiSim/CityTower"
{
    Properties
    {
        _FloorHeight ("Floor Height (units)", Float) = 0.36
        _BayWidth ("Window Bay Width (units)", Float) = 0.42
        _GlassSmoothness ("Glass Smoothness", Range(0, 1)) = 0.94
        _GlassMetallic ("Glass Metallic", Range(0, 1)) = 0.55
        _FrameSmoothness ("Frame Smoothness", Range(0, 1)) = 0.35
        _RoofColor ("Roof Color", Color) = (0.21, 0.22, 0.24, 1)
        _WarmLight ("Warm Window Light", Color) = (1.0, 0.72, 0.42, 1)
        _CoolLight ("Cool Window Light", Color) = (0.66, 0.84, 1.0, 1)
        _WindowIntensity ("Window Light Intensity", Float) = 3.2
        _LitRatio ("Lit Window Ratio", Range(0, 1)) = 0.55
        _AccentColor ("Crown LED Colour", Color) = (0.25, 0.85, 1.0, 1)
        _AccentIntensity ("Crown LED Intensity", Float) = 5.0
        _InteriorDepth ("Interior Room Depth", Range(0.2, 2)) = 0.9
        _Grime ("Monsoon Grime", Range(0, 1)) = 0.4
        _NeonIntensity ("Neon Trim Intensity", Float) = 6.0
        _AdIntensity ("Holo Ad Intensity", Float) = 2.2
        [NoScaleOffset] _FacadeTex ("Facade Detail Albedo (optional)", 2D) = "white" {}
        [NoScaleOffset] _FacadeNormal ("Facade Normal (optional)", 2D) = "bump" {}
        _UseFacadeTex ("Use Facade Textures (0/1)", Float) = 0
        _FacadeTiling ("Facade Texture Size (units)", Float) = 1.5
        _FacadeNormalStrength ("Facade Normal Strength", Float) = 1.0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float _FloorHeight;
            float _BayWidth;
            half _GlassSmoothness;
            half _GlassMetallic;
            half _FrameSmoothness;
            half4 _RoofColor;
            half4 _WarmLight;
            half4 _CoolLight;
            half _WindowIntensity;
            half _LitRatio;
            half4 _AccentColor;
            half _AccentIntensity;
            half _InteriorDepth;
            half _Grime;
            half _NeonIntensity;
            half _AdIntensity;
            half _UseFacadeTex;
            float _FacadeTiling;
            half _FacadeNormalStrength;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _FORWARD_PLUS _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fog

            #include "TaxiSimLighting.hlsl"

            TEXTURE2D(_FacadeTex);    SAMPLER(sampler_FacadeTex);
            TEXTURE2D(_FacadeNormal); SAMPLER(sampler_FacadeNormal);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv0        : TEXCOORD0;
                float4 uv1        : TEXCOORD1;
                float4 uv2        : TEXCOORD2;
                float4 uv3        : TEXCOORD3;
                half4  color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 tangentWS  : TEXCOORD2;
                float2 facade     : TEXCOORD3;
                float4 params     : TEXCOORD4;
                half4  glass      : TEXCOORD5;
                half3  frame      : TEXCOORD6;
                float  fogFactor  : TEXCOORD7;
                float4 neon       : TEXCOORD8;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.tangentWS = TransformObjectToWorldDir(v.tangentOS.xyz);
                o.facade = v.uv0;
                o.params = v.uv1;
                o.glass = v.uv2;
                o.frame = v.color.rgb;
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                o.neon = v.uv3;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float seed = i.params.x;
                float style = i.params.y;
                float totalH = i.params.z;
                float accent = i.params.w;
                float wet = _TS_Wetness;
                float t = _Time.y;
                float nightK = smoothstep(0.15, 0.7, _TS_Night);

                // No early returns / derivative calls inside branches: roof and facade are both
                // evaluated and blended at the end (keeps fwidth() valid on every GPU/compiler).
                float isRoof = step(0.6, n.y);

                // ------------------------------- Roof -----------------------------------
                float2 r = i.facade;
                float grain = TS_ValueNoise(r * 4.0 + seed * 31.0);
                float hvac = step(0.86, TS_Hash12(floor(r * 1.4) + seed * 13.1));
                half3 roof = lerp(_RoofColor.rgb, i.frame * 0.55, 0.35);
                half3 tarp = half3(0.07, 0.24, 0.62) * (0.75 + 0.5 * TS_ValueNoise(r * 6.0 + seed * 3.0));
                roof = lerp(roof, tarp, step(0.5, i.glass.w));
                roof *= 0.8 + 0.35 * grain;
                roof = lerp(roof, roof * 0.45, hvac);
                roof *= lerp(1.0, 0.6, wet);
                // Aviation obstruction light (red, blinking) on towers taller than ~90 m
                float blink = step(0.82, frac(t * 0.6 + seed * 7.0));
                float dotMask = 1.0 - smoothstep(0.12, 0.22, length(r));
                half3 roofEmission = half3(5.0, 0.2, 0.12) * blink * dotMask * (0.2 + 0.8 * nightK) * step(9.0, totalH);
                half roofSmooth = lerp(0.25, 0.85, wet);

                // ------------------------------- Facade grid ----------------------------
                float bay = _BayWidth * lerp(1.0, 1.5, style);
                float2 f = i.facade / float2(bay, _FloorHeight);
                float2 cell = floor(f);
                float2 g = frac(f);
                float mull = lerp(0.05, 0.26, style);
                float slab = lerp(0.14, 0.34, style);
                float2 w = fwidth(f);
                float gx = smoothstep(mull * 0.5 - w.x, mull * 0.5 + w.x, g.x) * smoothstep(mull * 0.5 - w.x, mull * 0.5 + w.x, 1.0 - g.x);
                float gy = smoothstep(slab - w.y, slab + w.y, g.y) * smoothstep(0.03 - w.y, 0.03 + w.y, 1.0 - g.y);
                float glassMask = gx * gy;
                // Far away: blend to the average coverage so the grid never shimmers
                float avgGlass = (1.0 - mull) * (1.0 - slab - 0.03);
                float farBlend = saturate(max(w.x, w.y) * 1.5 - 0.25);
                // Distance LOD: beyond ~70 units fade every per-window detail (grid, random lit rooms,
                // interior parallax) into its average, so distant towers never turn into "TV static".
                float camDist = distance(i.positionWS, _WorldSpaceCameraPos);
                float lod = saturate(max(farBlend, (camDist - 70.0) / 160.0));
                // Separate LOD per axis: window columns fade early, but floor bands stay visible far away
                // so distant buildings still read as many-storey buildings (not flat boxes).
                float lodX = saturate(max(saturate(w.x * 1.5 - 0.25), (camDist - 70.0) / 160.0));
                float lodY = saturate(w.y * 1.5 - 0.35);
                glassMask = lerp(gx, 1.0 - mull, lodX) * lerp(gy, 1.0 - slab - 0.03, lodY);

                float heightAbove = i.facade.y;
                float lobby = step(heightAbove, _FloorHeight * 1.4);
                float topDist = totalH - heightAbove;

                // ------------------------------- Interior mapping -----------------------
                float3 T = normalize(i.tangentWS);
                float3 B = float3(0, 1, 0);
                float3 V = normalize(i.positionWS - _WorldSpaceCameraPos);
                float3 d = float3(dot(V, T) / bay, dot(V, B) / _FloorHeight, dot(V, -n) / max(_InteriorDepth, 0.05));
                d += sign(d + 1e-7) * 1e-5;
                float3 p0 = float3(g, 0.0);
                float3 tt = (step(0.0, d) - p0) / d;
                float tmin = min(tt.x, min(tt.y, tt.z));
                float3 hit = p0 + d * tmin;
                half wallShade;
                if (tmin == tt.z)      wallShade = 0.75;                 // back wall
                else if (tmin == tt.y) wallShade = d.y > 0 ? 1.0 : 0.35; // ceiling / floor
                else                   wallShade = 0.55;                 // side walls
                float depthFade = 1.0 - saturate(hit.z) * 0.45;

                float roomRand = TS_Hash12(cell * 1.37 + seed * 91.7);
                float epoch = floor(t / 45.0 + roomRand * 7.0);
                float litChance = _LitRatio * (0.75 + 0.5 * TS_Hash12(float2(seed * 3.1, 3.0)));
                float roomLit = step(TS_Hash12(cell + epoch * 3.17 + seed), litChance);
                roomLit = max(roomLit, lobby);
                float coolPick = step(0.55, TS_Hash12(float2(cell.y, seed * 5.0))) * (1.0 - style);
                half3 lightCol = lerp(_WarmLight.rgb, _CoolLight.rgb, coolPick);

                half3 interiorDay = half3(0.05, 0.055, 0.06) * wallShade * depthFade * 2.0;
                // Mumbai apartment balconies (residential styles): railing across the lower part of
                // alternate window bays, with the room behind in shadow.
                float balconyOn = step(0.8, style) * step(0.4, frac(seed * 5.3)) * (1.0 - lodX);
                float balconyBay = step(0.5, frac(cell.x * 0.5 + seed * 3.0));
                float railZone = step(g.y, slab + 0.3) * balconyOn * balconyBay;
                float bars = step(0.55, frac(g.x * 14.0)) * 0.6 + step(g.y, slab + 0.3) * step(slab + 0.26, g.y);
                half3 interiorNight = lightCol * wallShade * depthFade;
                half3 glassAlbedo = lerp(interiorDay, i.glass.rgb * 1.4 + 0.08, 0.6);
                glassAlbedo *= lerp(1.0, 0.3, nightK);
                half3 windowEmission = interiorNight * roomLit * nightK * _WindowIntensity;
                half3 avgLight = lerp(_WarmLight.rgb, _CoolLight.rgb, 0.5 * (1.0 - style));
                // Distant night: lit / unlit clusters (4 bays x 3 floors) instead of a flat glow,
                // fading to a dimmer average only very far away.
                float2 cluster = floor(f / float2(4.0, 3.0));
                float clusterLit = step(TS_Hash12(cluster * 1.91 + seed * 23.0 + floor(t / 90.0)), litChance * 0.9);
                float farFlat = saturate((camDist - 260.0) / 240.0);
                half3 avgEmission = avgLight * lerp(clusterLit * 0.8, litChance * 0.45, farFlat) * nightK * _WindowIntensity
                                  * (0.7 + 0.6 * TS_Hash12(cluster + seed));
                windowEmission = lerp(windowEmission, avgEmission, lod);
                glassAlbedo = lerp(glassAlbedo, lerp(half3(0.07, 0.075, 0.08), i.glass.rgb * 1.4 + 0.08, 0.6), lod);
                windowEmission += lobby * _WarmLight.rgb * (0.4 + 1.2 * nightK) * wallShade;

                // ------------------------------- Frame / concrete -----------------------
                half3 frame = i.frame;
                float streak = smoothstep(0.45, 0.95, TS_ValueNoise(float2(i.facade.x * 3.0 + seed * 11.0, i.facade.y * 0.35)));
                frame *= 1.0 - _Grime * 0.55 * streak * style;
                frame *= 0.92 + 0.16 * TS_ValueNoise(i.facade * 2.3 + seed);
                frame *= lerp(1.0, 0.62, wet);
                // Ledges: a lit lip on top of every floor slab and a shadow line under it
                float slabLip = smoothstep(slab * 0.55, slab, g.y) * step(g.y, slab);
                float underShadow = 1.0 - smoothstep(0.0, slab * 0.35, g.y);
                frame *= lerp(1.0, lerp(1.08, 0.78, underShadow) * lerp(1.0, 1.1, slabLip), (1.0 - lodY) * step(0.5, style));

                // Optional real facade texture (concrete / plaster / brick from Poly Haven or ambientCG).
                // Its brightness modulates the per-building tint so every building keeps its own colour.
                float2 ftuv = i.facade / max(_FacadeTiling, 0.01);
                half3 ftex = SAMPLE_TEXTURE2D(_FacadeTex, sampler_FacadeTex, ftuv).rgb;
                half texLum = dot(ftex, half3(0.299, 0.587, 0.114));
                half3 ftexAvg = SAMPLE_TEXTURE2D_LOD(_FacadeTex, sampler_FacadeTex, float2(0.5, 0.5), 11).rgb;
                half avgLum = max(dot(ftexAvg, half3(0.299, 0.587, 0.114)), 0.05);
                half3 texDetail = clamp(texLum / avgLum, 0.55, 1.45) * lerp(half3(1, 1, 1), ftex / max(texLum, 0.05), 0.2);
                frame *= lerp(half3(1, 1, 1), texDetail, _UseFacadeTex);
                half3 nT = UnpackNormalScale(SAMPLE_TEXTURE2D(_FacadeNormal, sampler_FacadeNormal, ftuv), _FacadeNormalStrength);
                float3 nFrame = normalize(T * nT.x + B * nT.y + n * nT.z);

                // ------------------------------- Crown LED bands ------------------------
                float band = TS_Line(topDist - 0.18, 0.05) + TS_Line(topDist - 0.55, 0.025);
                float sweep = 0.6 + 0.4 * sin(t * 1.5 + i.facade.x * 0.8 + seed * 6.0);
                half3 accentEmission = _AccentColor.rgb * band * _AccentIntensity * (0.15 + 0.85 * nightK) * sweep * step(0.5, accent);

                // ------------------------------- Mumbai shopfronts -----------------------
                // Ground floor of street-facing buildings (uv3.w = 1): a coloured signboard band
                // over bays that are either lit display windows or rolling metal shutters.
                float shopOn = step(0.5, i.neon.w) * (1.0 - isRoof);
                float shopTop = 0.42;
                float inShop = shopOn * step(heightAbove, shopTop);
                float signBand = step(0.3, heightAbove) * inShop;
                float bayU = i.facade.x / 0.7;
                float bayId = floor(bayU);
                float bayF = frac(bayU);
                float shopRand = TS_Hash12(float2(bayId, seed * 37.0));
                half3 signCol = TS_NeonColor(1.0 + floor(TS_Hash12(float2(floor(i.facade.x / 2.1), seed * 11.0)) * 5.0));
                signCol = lerp(signCol, half3(0.95, 0.92, 0.85), 0.25);
                float signText = step(0.55, TS_Hash12(floor(float2(i.facade.x * 9.0, heightAbove * 30.0)) + seed)) * step(0.33, heightAbove) * step(heightAbove, 0.4);
                float shutter = step(0.62, shopRand);
                float bayEdge = 1.0 - smoothstep(0.02, 0.05, min(bayF, 1.0 - bayF));
                half3 shopWin = lerp(half3(0.2, 0.22, 0.24), half3(0.55, 0.56, 0.58) * (0.85 + 0.15 * step(0.5, frac(heightAbove * 40.0))), shutter);
                shopWin = lerp(shopWin, half3(0.12, 0.12, 0.13), bayEdge);
                half3 shopAlbedo = lerp(shopWin, lerp(signCol * 0.8, half3(0.05, 0.05, 0.06), signText), signBand);
                half3 shopEmission = (1.0 - shutter) * (1.0 - signBand) * (1.0 - bayEdge) * _WarmLight.rgb * (0.25 + 2.2 * nightK)
                                   + signBand * lerp(signCol, half3(1, 1, 1), signText) * (0.15 + 1.6 * nightK);

                // Fallback sky reflection on glass so curtain walls read as glass even before the
                // reflection probe has rendered (fresnel-weighted, fades at night).
                float3 Vc = normalize(_WorldSpaceCameraPos - i.positionWS);
                float3 Rv = reflect(-Vc, n);
                half3 skyRefl = lerp(_TS_SkyHorizon.rgb, _TS_SkyZenith.rgb, saturate(Rv.y * 1.5));
                float fres = 0.25 + 0.75 * pow(1.0 - saturate(dot(n, Vc)), 4.0);
                half3 glassSky = skyRefl * fres * 0.45 * (1.0 - 0.8 * nightK);

                half3 albedo = lerp(frame, glassAlbedo, glassMask);
                // Balcony railings over the glass (metal/painted), room behind darker
                half3 railCol = lerp(frame * 0.85, half3(0.25, 0.26, 0.27), 0.5);
                albedo = lerp(albedo, lerp(albedo * 0.6, railCol, saturate(bars)), railZone * glassMask);
                // v0.10 Mumbai window grilles (MS bars, often painted) on residential windows without a balcony
                float grilleOn = step(0.8, style) * (1.0 - railZone) * step(0.3, frac(seed * 7.7)) * (1.0 - lodX);
                float grille = max(step(0.86, frac(g.x * 8.0)), TS_Line(g.y - (slab + (1.0 - slab) * 0.55), 0.012));
                half3 grilleCol = lerp(half3(0.1, 0.1, 0.11), half3(0.18, 0.32, 0.26), step(0.6, frac(seed * 3.9)));
                albedo = lerp(albedo, grilleCol, grille * grilleOn * glassMask * 0.9);
                // v0.10 Monsoon moss and black algae on older concrete (strongest low down and under ledges)
                float mossN = TS_Fbm(i.facade * float2(1.6, 0.55) + seed * 7.0);
                float moss = smoothstep(0.55, 0.82, mossN) * step(0.75, style) * (1.0 - glassMask)
                           * (0.35 + 0.65 * saturate(1.0 - heightAbove / max(totalH * 0.6, 0.3))) * _Grime;
                albedo = lerp(albedo, half3(0.11, 0.14, 0.09) * (0.8 + 0.4 * mossN), saturate(moss * 0.75));
                // Ground contact: walls darken towards the street (bounce light is blocked)
                albedo *= lerp(0.62, 1.0, smoothstep(0.0, 0.45, heightAbove));
                half metallic = lerp(0.05, _GlassMetallic * (1.0 - style * 0.6), glassMask);
                half smoothness = lerp(lerp(_FrameSmoothness, 0.82, wet), _GlassSmoothness, glassMask);
                half3 emission = windowEmission * glassMask + accentEmission + glassSky * glassMask * (1.0 - inShop);
                // Shopfront replaces the facade on the ground floor
                albedo = lerp(albedo, shopAlbedo, inShop);
                metallic = lerp(metallic, 0.1, inShop);
                smoothness = lerp(smoothness, lerp(0.3, 0.85, (1.0 - shutter) * (1.0 - signBand)), inShop);
                emission = lerp(emission, shopEmission, inShop);


                // ------------------------------- Neon Mumbai 2077 ------------------------
                // Neon tube trims on the vertical corners and the roof line, plus animated
                // holographic adverts on some facades. Scaled by _TS_Style (0 = realistic).
                float faceLen = max(i.neon.x, 0.01);
                float hueIdx = i.neon.y;
                float neonOn = step(0.5, hueIdx) * _TS_Style * nightK;
                half3 neonCol = TS_NeonColor(hueIdx);
                float edgeU = min(i.facade.x, faceLen - i.facade.x);
                float trim = TS_Line(edgeU - 0.06, 0.035);
                float crownLine = TS_Line(topDist - 0.08, 0.03);
                float flickerOn = step(0.6, frac(seed * 7.3));
                float flicker = 1.0 - step(0.97, TS_Hash12(float2(floor(t * 12.0), seed * 91.0))) * flickerOn;
                float neonPulse = 0.8 + 0.2 * sin(t * 2.0 + seed * 20.0);
                half3 neonEmission = neonCol * (trim + crownLine) * neonOn * flicker * neonPulse * _NeonIntensity;

                float adU = i.facade.x / faceLen;
                float adV = (heightAbove - totalH * 0.55) / max(totalH * 0.3, 0.01);
                float inAd = step(0.12, adU) * step(adU, 0.88) * step(0.0, adV) * step(adV, 1.0) * step(2.5, faceLen) * step(0.5, i.neon.z);
                float adFade = 1.0 - saturate((camDist - 120.0) / 80.0);
                half3 adEmission = TS_AdPattern(float2(adU, adV), seed, t) * inAd * _TS_Style * (0.35 + 0.65 * nightK) * _AdIntensity * adFade;

                // ------------------------------- Roof / facade blend --------------------
                albedo = lerp(albedo, half3(0.02, 0.02, 0.025), saturate(inAd * _TS_Style));
                emission += neonEmission + adEmission;
                albedo = lerp(albedo, roof, isRoof);
                metallic = lerp(metallic, 0, isRoof);
                smoothness = lerp(smoothness, roofSmooth, isRoof);
                emission = lerp(emission, roofEmission, isRoof);
                float3 nFinal = normalize(lerp(n, nFrame, (1.0 - glassMask) * _UseFacadeTex * (1.0 - isRoof)));
                return TS_ShadePBR(i.positionWS, nFinal, i.positionCS, i.fogFactor, albedo, metallic, smoothness, 1, emission, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull Back
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex TS_ShadowVert
            #pragma fragment TS_ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "TaxiSimPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R Cull Back
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex TS_DepthVert
            #pragma fragment TS_DepthFrag
            #include "TaxiSimPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On Cull Back
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex TS_DepthVert
            #pragma fragment TS_DepthNormalsFrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "TaxiSimPasses.hlsl"
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
