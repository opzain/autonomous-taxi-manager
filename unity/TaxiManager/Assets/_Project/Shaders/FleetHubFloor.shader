// AT Fleet Hub apron (v0.7): polished epoxy depot floor with painted charging bays, an animated
// chevron launch lane that streams taxis out of the gate, pulsing charge rings under bays that are
// charging, and a faint hex grid that only shows in the neon look.
// Mesh data (LandmarkBuilder.FleetHub): uv0 = local floor position in units (x across, y towards the gate).
Shader "TaxiSim/FleetHubFloor"
{
    Properties
    {
        _BaseColor ("Epoxy Colour", Color) = (0.2, 0.22, 0.25, 1)
        _LineColor ("Bay Paint", Color) = (0.92, 0.92, 0.9, 1)
        _GlowColor ("Guide Glow (HDR)", Color) = (0.2, 2.2, 3.0, 1)
        _LaneHalfWidth ("Launch Lane Half Width", Float) = 1.6
        _BayWidth ("Bay Width", Float) = 1.7
        _BayRow ("Bay Rows (z min, z max)", Vector) = (3.2, 6.6, 0, 0)
        _ApronHalfWidth ("Apron Half Width", Float) = 12.6
        _GateZ ("Gate Z", Float) = 9.0
        _Smoothness ("Smoothness", Range(0, 1)) = 0.78
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _LineColor;
            half4 _GlowColor;
            float _LaneHalfWidth;
            float _BayWidth;
            float4 _BayRow;
            float _ApronHalfWidth;
            float _GateZ;
            half _Smoothness;
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
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fog

            #include "TaxiSimLighting.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float fogFactor : TEXCOORD3;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv;
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            // Hexagon edge distance (for the neon-only hex grid)
            float HexEdge(float2 p)
            {
                const float2 s = float2(1.0, 1.7320508);
                float2 a = fmod(abs(p), s) - s * 0.5;
                float2 b = fmod(abs(p - s * 0.5), s) - s * 0.5;
                float2 g = dot(a, a) < dot(b, b) ? a : b;
                float2 q = abs(g);
                return 0.5 - max(dot(q, s * 0.5), q.x);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 uv = i.uv;
                float t = _Time.y;
                float x = uv.x, z = uv.y;
                float ax = abs(x);

                // Base epoxy with subtle trowel noise and tyre marks in the lane
                float n = TS_Fbm(uv * 1.7);
                half3 albedo = _BaseColor.rgb * (0.9 + 0.2 * n);
                float tyre = TS_Line(frac(x * 1.1 + 0.5) - 0.5, 0.08) * step(ax, _LaneHalfWidth) * 0.25 * TS_ValueNoise(float2(z * 0.6, x));
                albedo *= 1.0 - tyre;

                // Painted bays: dividers, back line and a numbered block
                float inRow = step(_BayRow.x, z) * step(z, _BayRow.y);
                float inBays = step(_LaneHalfWidth + 0.6, ax) * step(ax, _ApronHalfWidth);
                float bu = (ax - (_LaneHalfWidth + 0.6)) / _BayWidth;
                float divider = TS_Line(frac(bu + 0.5) - 0.5, 0.03 / _BayWidth) * inRow * inBays;
                float backLine = TS_Line(z - _BayRow.x, 0.04) * inBays;
                float bayIdx = floor(bu) + (x < 0 ? 0.0 : 37.0);
                float numBlock = step(abs(frac(bu) - 0.5), 0.16) * step(abs(z - (_BayRow.x + 0.35)), 0.14) * inBays;
                float paint = saturate(divider + backLine + numBlock * 0.8);
                albedo = lerp(albedo, _LineColor.rgb, paint);

                // Lane edges (solid yellow) and centre dashes
                float laneEdge = TS_Line(ax - _LaneHalfWidth, 0.05) * step(0.0, z) * step(z, _GateZ);
                albedo = lerp(albedo, half3(0.95, 0.75, 0.1), laneEdge);

                float night = smoothstep(0.15, 0.7, _TS_Night);
                float glowK = lerp(0.45, 1.0, night);
                half3 emission = 0;

                // Animated chevrons streaming out towards the gate (outbound launch lane)
                float inLane = step(ax, _LaneHalfWidth - 0.15) * step(0.3, z) * step(z, _GateZ);
                float chev = frac((z - ax * 0.6) * 0.55 - t * 0.9);
                float chevMask = TS_Line(chev - 0.5, 0.12) * inLane;
                float headFade = 0.35 + 0.65 * frac(z * 0.11 - t * 0.25);
                emission += _GlowColor.rgb * chevMask * headFade * glowK;
                albedo = lerp(albedo, _GlowColor.rgb * 0.15, chevMask * 0.6);

                // Charging rings under bays that are currently charging (pulsing), idle bays show a dim outline
                float charging = step(0.35, TS_Hash12(float2(bayIdx, floor(t / 40.0))));
                float2 bc = float2(frac(bu) - 0.5, (z - (_BayRow.x + _BayRow.y) * 0.5) / (_BayRow.y - _BayRow.x));
                float rect = max(abs(bc.x) * 2.0, abs(bc.y) * 2.0);
                float outline = TS_Line(rect - 0.82, 0.03) * inRow * inBays;
                float pulse = 0.55 + 0.45 * sin(t * 2.4 + bayIdx * 1.7);
                half3 chargeCol = lerp(half3(1.0, 0.55, 0.1), half3(0.2, 2.4, 0.9), charging);
                emission += chargeCol * outline * lerp(0.35, 1.0, charging * pulse) * glowK;

                // Gate threshold scan line
                float scan = TS_Line(z - (_GateZ - 0.4), 0.06) * step(ax, _LaneHalfWidth) * (0.6 + 0.4 * sin(t * 6.0));
                emission += _GlowColor.rgb * scan * glowK;

                // Neon-style hex grid (only in the Neon Mumbai 2077 look)
                float hex = 1.0 - smoothstep(0.0, 0.04, HexEdge(uv * 0.9));
                emission += _GlowColor.rgb * 0.18 * hex * _TS_Style * night * (1.0 - inBays * inRow);

                half smoothness = lerp(_Smoothness, 0.95, _TS_Wetness);
                return TS_ShadePBR(i.positionWS, normalize(i.normalWS), i.positionCS, i.fogFactor, albedo, 0.0, smoothness, 1, emission, 1);
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
            #pragma vertex TS_DepthVert
            #pragma fragment TS_DepthFrag
            #include "TaxiSimPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
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
