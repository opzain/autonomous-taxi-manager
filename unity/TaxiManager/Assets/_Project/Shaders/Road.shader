// Procedural asphalt road for the BKC network.
//  * Asphalt grain, repair patches, tyre tracks
//  * Indian road markings: double yellow centre, dashed white lane lines, edge lines, zebra crossings
//  * Monsoon: wet sheen, puddles with animated rain ripples
//  * Gameplay overlays (per-road MaterialPropertyBlock): traffic heat glow, closure hazard stripes
// Mesh data written by RoadNetworkBuilder.cs:
//  uv0 = (0..1 across, distance along in units)   uv1 = (lanes per direction, width, markings on/off, length)
Shader "TaxiSim/Road"
{
    Properties
    {
        _AsphaltColor ("Asphalt", Color) = (0.15, 0.155, 0.16, 1)
        _LineWhite ("White Paint", Color) = (0.85, 0.85, 0.82, 1)
        _LineYellow ("Yellow Paint", Color) = (0.92, 0.68, 0.08, 1)
        _Smoothness ("Dry Smoothness", Range(0, 1)) = 0.22
        _HeatColor ("Traffic Heat Colour", Color) = (0, 1, 0.4, 1)
        _HeatAmount ("Traffic Heat Amount", Range(0, 2)) = 0
        _Blocked ("Road Closed", Range(0, 1)) = 0
        [NoScaleOffset] _AsphaltTex ("Asphalt Albedo (optional)", 2D) = "white" {}
        [NoScaleOffset] _AsphaltNormal ("Asphalt Normal (optional)", 2D) = "bump" {}
        [NoScaleOffset] _AsphaltRough ("Asphalt Roughness (optional)", 2D) = "white" {}
        _UseTex ("Use Asphalt Textures (0/1)", Float) = 0
        _TexTiling ("Texture Size (units)", Float) = 0.6
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _AsphaltColor;
            half4 _LineWhite;
            half4 _LineYellow;
            half _Smoothness;
            half4 _HeatColor;
            half _HeatAmount;
            half _Blocked;
            half _UseTex;
            float _TexTiling;
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

            TEXTURE2D(_AsphaltTex);    SAMPLER(sampler_AsphaltTex);
            TEXTURE2D(_AsphaltNormal); SAMPLER(sampler_AsphaltNormal);
            TEXTURE2D(_AsphaltRough);  SAMPLER(sampler_AsphaltRough);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv0        : TEXCOORD0;
                float4 uv1        : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float2 uv         : TEXCOORD2;
                float4 road       : TEXCOORD3;
                float  fogFactor  : TEXCOORD4;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv0;
                o.road = v.uv1;
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float u = i.uv.x;
                float along = i.uv.y;
                float lanes = max(1.0, i.road.x);
                float width = max(0.5, i.road.y);
                float markings = i.road.z;
                float roadLength = i.road.w;
                float2 wp = i.positionWS.xz;
                float wet = _TS_Wetness;

                // Asphalt
                float n1 = TS_Fbm(wp * 0.8);
                float n2 = TS_ValueNoise(wp * 9.0);
                half3 albedo = _AsphaltColor.rgb * (0.8 + 0.3 * n1 + 0.1 * n2);
                float patchMask = step(0.78, TS_ValueNoise(wp * 0.35 + 9.1));
                albedo = lerp(albedo, albedo * 0.72, patchMask * 0.6);

                // Optional photographed asphalt (world-space tiling) under the procedural markings
                float2 tuv = wp / max(_TexTiling, 0.01);
                half3 atex = SAMPLE_TEXTURE2D(_AsphaltTex, sampler_AsphaltTex, tuv).rgb;
                half arough = SAMPLE_TEXTURE2D(_AsphaltRough, sampler_AsphaltRough, tuv).r;
                half3 an = UnpackNormal(SAMPLE_TEXTURE2D(_AsphaltNormal, sampler_AsphaltNormal, tuv));
                albedo = lerp(albedo, atex * (0.85 + 0.3 * n1) * lerp(1.0, 0.72, patchMask * 0.6), _UseTex);

                float x = (u - 0.5) * width;
                float halfW = width * 0.5;
                float laneW = max((halfW - 0.12) / lanes, 0.05);

                // Tyre tracks
                float lanePos = frac((abs(x) - 0.08) / laneW);
                float tracks = (1.0 - smoothstep(0.0, 0.12, abs(lanePos - 0.3))) + (1.0 - smoothstep(0.0, 0.12, abs(lanePos - 0.7)));
                albedo *= 1.0 - 0.1 * tracks * markings;

                // Markings (left-hand traffic, Indian style)
                float centre = TS_Line(abs(x) - 0.05, 0.018) * markings;
                float edgeLine = TS_Line(abs(x) - (halfW - 0.09), 0.02) * markings;
                float dashes = 0.0;
                [unroll] for (int k = 1; k < 3; k++)
                {
                    float onLane = step((float)k + 0.5, lanes);
                    dashes += onLane * TS_Line(abs(x) - (0.08 + laneW * k), 0.015) * step(frac(along / 1.3), 0.5);
                }
                dashes *= markings;
                // v0.8: zebra crossings and stop lines are real geometry at signalised junctions (TrafficSignals.cs),
                // placed exactly where pedestrians cross and vehicles stop.
                float zebra = 0.0;

                float wear = 0.65 + 0.35 * TS_ValueNoise(wp * 3.0);
                albedo = lerp(albedo, _LineYellow.rgb * wear, saturate(centre));
                albedo = lerp(albedo, _LineWhite.rgb * wear, saturate(edgeLine + dashes + zebra));

                // v0.10 Mumbai road wear (local streets are rougher than arterials)
                float roughRoad = lanes < 1.5 ? 1.0 : lanes < 2.5 ? 0.55 : 0.3;
                // Oil stains down the middle of each lane
                float oil = smoothstep(0.62, 0.78, TS_Fbm(wp * 1.3 + 7.7)) * (1.0 - smoothstep(0.0, 0.18, abs(lanePos - 0.5))) * markings;
                albedo *= 1.0 - 0.28 * oil;
                // Hairline cracks
                float crack = TS_Line(TS_ValueNoise(wp * 2.2 + 4.4) - 0.5, 0.006) * step(0.45, TS_ValueNoise(wp * 0.7)) * roughRoad;
                albedo *= 1.0 - 0.4 * crack;
                // Gutter dirt and debris along both kerbs
                float gutter = smoothstep(halfW - 0.2, halfW, abs(x));
                albedo = lerp(albedo, half3(0.16, 0.14, 0.12) * (0.7 + 0.6 * n2), gutter * 0.45);
                // Potholes: sparse cells in road space, with a broken rim; they fill with water in rain
                float2 pc = float2(along * 2.5, x * 2.5);
                float2 pcell = floor(pc);
                float2 ph = TS_Hash22(pcell + 17.3);
                float2 pf = frac(pc) - (0.3 + 0.4 * ph);
                float pr = length(pf * float2(1.0, 0.75)) + 0.1 * (TS_ValueNoise(wp * 18.0) - 0.5);
                float potExists = step(1.0 - 0.045 * roughRoad, TS_Hash12(pcell + 3.7)) * step(abs(x), halfW - 0.12);
                float pothole = potExists * (1.0 - smoothstep(0.15, 0.2, pr));
                float potRim = potExists * (smoothstep(0.13, 0.2, pr) - smoothstep(0.2, 0.27, pr));
                albedo = lerp(albedo, albedo * 0.4 + half3(0.035, 0.03, 0.025), pothole);
                albedo *= 1.0 - 0.18 * potRim;
                // Manhole covers (cast iron, cross-hatched) in alternating lanes every ~9 units
                float mcell = floor(along / 9.0);
                float mh = TS_Hash12(float2(mcell, 5.1 + lanes));
                float2 mpos = float2((mcell + 0.3 + 0.4 * mh) * 9.0, (mh > 0.5 ? 1.0 : -1.0) * (0.08 + laneW * 0.5));
                float2 md = float2(along, x) - mpos;
                float mr = length(md);
                float manhole = (1.0 - smoothstep(0.055, 0.062, mr)) * step(0.3, mh) * markings;
                float hatch = abs(step(0.5, frac(md.x * 55.0)) - step(0.5, frac(md.y * 55.0)));
                float mring = smoothstep(0.045, 0.05, mr);
                albedo = lerp(albedo, half3(0.06, 0.058, 0.055) * (0.75 + 0.35 * hatch + 0.3 * mring), manhole);

                // Monsoon wetness and puddles
                float puddle = max(smoothstep(0.58, 0.72, TS_Fbm(wp * 0.33 + 3.1)), pothole) * wet;
                half smoothness = lerp(lerp(_Smoothness, (1.0 - arough) * 0.55, _UseTex), 0.78, wet);
                smoothness = lerp(smoothness, 0.97, puddle);
                albedo *= lerp(1.0, 0.55, wet);
                albedo = lerp(albedo, albedo * 0.45, puddle);
                float2 rip = TS_RainRipples(wp, _Time.y, _TS_Rain * puddle);
                float3 baseN = normalize(i.normalWS);
                float3 texN = normalize(float3(1, 0, 0) * an.x + float3(0, 0, 1) * an.y + baseN * an.z);
                baseN = normalize(lerp(baseN, texN, _UseTex * (1.0 - puddle)));
                float3 nrm = normalize(baseN + float3(rip.x, 0.0, rip.y));

                // Traffic heat overlay along both kerbs
                float kerb = smoothstep(halfW - 0.45, halfW - 0.05, abs(x));
                float flow = 0.5 + 0.5 * sin(along * 1.2 - _Time.y * 3.0);
                half3 emission = _HeatColor.rgb * _HeatAmount * (kerb * (0.6 + 0.4 * flow) + 0.08) * (0.8 + 1.6 * _TS_Night);

                // Road closure hazard
                if (_Blocked > 0.5)
                {
                    float stripes = step(0.5, frac((along + x) * 0.9));
                    half3 hazard = lerp(half3(0.9, 0.05, 0.03), half3(0.95, 0.95, 0.9), stripes);
                    float pulse = 0.55 + 0.45 * sin(_Time.y * 5.0);
                    albedo = lerp(albedo, hazard, 0.7);
                    emission += hazard * pulse * 1.6;
                }

                smoothness = lerp(smoothness, 0.55, manhole * (1.0 - wet));
                return TS_ShadePBR(i.positionWS, nrm, i.positionCS, i.fogFactor, albedo, manhole * 0.7, smoothness, 1, emission, 1);
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
