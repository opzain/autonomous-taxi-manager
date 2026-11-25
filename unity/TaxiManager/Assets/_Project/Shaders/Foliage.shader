// Realistic foliage for trees, palms, bushes and hedges (v0.6).
// Trees are built from many "leaf cards" (like real-time games do). Each card cuts out a cluster of
// leaf shapes procedurally (no textures), palm fronds cut out feather-shaped leaflets.
//  * Alpha-clipped in every pass -> correct leafy silhouettes and dappled shadows
//  * Spherical canopy normals + baked ambient occlusion (vertex colour) -> soft, volumetric canopies
//  * Translucency: leaves glow when the sun is behind them
//  * Wind sway (whole tree) + leaf flutter (per card), stronger in the monsoon
// Mesh data (TreeFactory.cs): uv0 = card uv, uv1 = (wind weight, pattern 0 leaves / 1 frond / 2 grass, card seed),
// colour = leaf tint * AO.
Shader "TaxiSim/Foliage"
{
    Properties
    {
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Smoothness ("Smoothness", Range(0, 1)) = 0.25
        _Translucency ("Translucency", Range(0, 2)) = 0.7
        _Wind ("Wind Sway", Float) = 0.05
        _Flutter ("Leaf Flutter", Float) = 0.02
        _LeafSize ("Leaf Size", Range(0.5, 1.6)) = 1.0
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" }
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "TaxiSimCommon.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _Tint;
            half _Smoothness;
            half _Translucency;
            half _Wind;
            half _Flutter;
            half _LeafSize;
        CBUFFER_END

        float3 TS_FoliageWind(float3 positionWS, float weight, float seed)
        {
            float t = _Time.y;
            float gust = 1.0 + _TS_Rain * 1.8;
            float sway = sin(t * 1.3 + positionWS.x * 0.21 + positionWS.z * 0.17) + 0.35 * sin(t * 2.7 + positionWS.z * 0.4);
            float flutter = sin(t * 7.0 + seed * 40.0) * _Flutter;
            positionWS.xz += float2(sway, sway * 0.55) * _Wind * weight * gust;
            positionWS.y += flutter * weight * gust;
            return positionWS;
        }

        // > 0 inside a leaf
        float TS_LeafMask(float2 uv, float pattern, float seed)
        {
            // Palm frond: central rib + angled leaflets, narrowing to the tip
            float rib = abs(uv.x - 0.5) * 2.0;
            float width = 1.0 - pow(saturate(uv.y), 1.6);
            float leaflet = step(0.35, frac(uv.y * 26.0 + rib * 2.5));
            float frond = step(rib, width) * max(leaflet, step(rib, 0.06)) * step(0.04, uv.y);

            // Broadleaf cluster: 7 rotated ellipses inside a soft round card
            float m = -1.0;
            [unroll] for (int k = 0; k < 7; k++)
            {
                float2 h = TS_Hash22(float2(k * 3.7 + 1.3, seed * 13.1 + k));
                float2 c = 0.22 + h * 0.56;
                float ang = TS_Hash12(float2(seed + k, k * 1.7)) * 6.2831;
                float2 d = uv - c;
                float cs = cos(ang), sn = sin(ang);
                float2 q = float2(d.x * cs - d.y * sn, d.x * sn + d.y * cs);
                float a = 0.17 * _LeafSize, b = 0.075 * _LeafSize;
                float e = (q.x * q.x) / (a * a) + (q.y * q.y) / (b * b);
                m = max(m, 1.0 - e);
            }
            float leaves = m;

            // Grass / weed tuft: 7 tapering blades leaning out from the bottom of the card
            float grass = -1.0;
            [unroll] for (int j = 0; j < 7; j++)
            {
                float2 hb = TS_Hash22(float2(j * 2.3 + 0.7, seed * 7.1 + j));
                float hgt = 0.5 + 0.5 * hb.y;
                float xc = 0.15 + hb.x * 0.7 + (hb.y - 0.5) * 0.6 * uv.y * uv.y;
                float wdt = 0.05 * (1.0 - uv.y / hgt);
                grass = max(grass, min(wdt - abs(uv.x - xc), hgt - uv.y) * 12.0);
            }
            return pattern > 1.5 ? grass : pattern > 0.5 ? frond - 0.5 : leaves;
        }

        struct FAttributes
        {
            float4 positionOS : POSITION;
            float3 normalOS   : NORMAL;
            float2 uv0        : TEXCOORD0;
            float4 uv1        : TEXCOORD1;
            half4  color      : COLOR;
        };
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
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fog

            #include "TaxiSimLighting.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float2 uv         : TEXCOORD2;
                float4 data       : TEXCOORD3;
                half4  color      : TEXCOORD4;
                float  fogFactor  : TEXCOORD5;
            };

            Varyings vert(FAttributes v)
            {
                Varyings o;
                float3 positionWS = TS_FoliageWind(TransformObjectToWorld(v.positionOS.xyz), v.uv1.x, v.uv1.z);
                o.positionWS = positionWS;
                o.positionCS = TransformWorldToHClip(positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv0;
                o.data = v.uv1;
                o.color = v.color;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float mask = TS_LeafMask(i.uv, i.data.y, i.data.z);
                clip(mask);

                float wet = _TS_Wetness;
                half3 albedo = i.color.rgb * _Tint.rgb;
                // subtle per-leaf variation and darker leaf edges
                albedo *= 0.85 + 0.3 * TS_Hash12(floor(i.uv * 6.0) + i.data.z * 17.0);
                albedo *= lerp(0.75, 1.0, saturate(mask * 3.0));
                albedo *= lerp(1.0, 0.75, wet);

                // Translucency: back-lit leaves glow (sun behind the canopy)
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                float3 toCam = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float back = pow(saturate(dot(-toCam, mainLight.direction)), 3.0);
                half3 translucency = albedo * mainLight.color * back * _Translucency * mainLight.shadowAttenuation * 0.6;

                half smooth = lerp(_Smoothness, 0.7, wet);
                return TS_ShadePBR(i.positionWS, i.normalWS, i.positionCS, i.fogFactor, albedo, 0, smooth, 1, translucency, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vertShadow
            #pragma fragment fragClip
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct SVaryings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float4 data : TEXCOORD1; };

            SVaryings vertShadow(FAttributes v)
            {
                SVaryings o;
                float3 positionWS = TS_FoliageWind(TransformObjectToWorld(v.positionOS.xyz), v.uv1.x, v.uv1.z);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
            #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                o.positionCS = positionCS;
                o.uv = v.uv0;
                o.data = v.uv1;
                return o;
            }

            half4 fragClip(SVaryings i) : SV_Target
            {
                clip(TS_LeafMask(i.uv, i.data.y, i.data.z));
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vertDepth
            #pragma fragment fragDepth

            struct DVaryings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float2 uv : TEXCOORD1; float4 data : TEXCOORD2; };

            DVaryings vertDepth(FAttributes v)
            {
                DVaryings o;
                float3 positionWS = TS_FoliageWind(TransformObjectToWorld(v.positionOS.xyz), v.uv1.x, v.uv1.z);
                o.positionCS = TransformWorldToHClip(positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv0;
                o.data = v.uv1;
                return o;
            }

            half fragDepth(DVaryings i) : SV_Target
            {
                clip(TS_LeafMask(i.uv, i.data.y, i.data.z));
                return i.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vertDN
            #pragma fragment fragDN
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            struct NVaryings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float2 uv : TEXCOORD1; float4 data : TEXCOORD2; };

            NVaryings vertDN(FAttributes v)
            {
                NVaryings o;
                float3 positionWS = TS_FoliageWind(TransformObjectToWorld(v.positionOS.xyz), v.uv1.x, v.uv1.z);
                o.positionCS = TransformWorldToHClip(positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv0;
                o.data = v.uv1;
                return o;
            }

            half4 fragDN(NVaryings i) : SV_Target
            {
                clip(TS_LeafMask(i.uv, i.data.y, i.data.z));
                float3 normalWS = NormalizeNormalPerPixel(i.normalWS);
            #if defined(_GBUFFER_NORMALS_OCT)
                float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
                float2 remapped = saturate(octNormalWS * 0.5 + 0.5);
                return half4(PackFloat2To888(remapped), 0.0);
            #else
                return half4(normalWS, 0.0);
            #endif
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
