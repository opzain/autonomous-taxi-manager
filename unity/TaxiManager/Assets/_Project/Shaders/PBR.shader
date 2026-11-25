// General-purpose PBR shader for vehicles, trees, bridges, poles and landmark details.
//  * Optional vertex colours (one material can paint many objects -> fewer draw calls)
//  * Emission that can follow the day/night cycle (_NightEmission) and blink (_BlinkRate)
//  * Wind sway for foliage (_Wind, uses uv0.y as the bend weight)
//  * Monsoon wetness response
Shader "TaxiSim/PBR"
{
    Properties
    {
        _BaseColor ("Base Colour", Color) = (0.7, 0.7, 0.7, 1)
        _UseVertexColor ("Use Vertex Colour (0/1)", Float) = 1
        _Metallic ("Metallic", Range(0, 1)) = 0
        _Smoothness ("Smoothness", Range(0, 1)) = 0.4
        [HDR] _EmissionColor ("Emission", Color) = (0, 0, 0, 1)
        _NightEmission ("Emission Follows Night (0..1)", Range(0, 1)) = 0
        _BlinkRate ("Blink Rate (Hz, 0 = steady)", Float) = 0
        _Wind ("Wind Sway", Float) = 0
        _WetResponse ("Wet Response", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half _UseVertexColor;
            half _Metallic;
            half _Smoothness;
            half4 _EmissionColor;
            half _NightEmission;
            half _BlinkRate;
            half _Wind;
            half _WetResponse;
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
            #pragma multi_compile_instancing

            #include "TaxiSimLighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv0        : TEXCOORD0;
                half4  color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                half4  color      : TEXCOORD2;
                float  fogFactor  : TEXCOORD3;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float sway = sin(_Time.y * 1.7 + positionWS.x * 0.35 + positionWS.z * 0.27) + 0.4 * sin(_Time.y * 3.1 + positionWS.z);
                positionWS.xz += float2(sway, sway * 0.6) * _Wind * v.uv0.y * (1.0 + _TS_Rain * 1.5);
                o.positionWS = positionWS;
                o.positionCS = TransformWorldToHClip(positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = v.color;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half3 tint = lerp(half3(1, 1, 1), i.color.rgb, _UseVertexColor);
                half3 albedo = _BaseColor.rgb * tint;
                float wet = _TS_Wetness * _WetResponse;
                half smoothness = lerp(_Smoothness, max(_Smoothness, 0.8), wet);
                albedo *= lerp(1.0, 0.7, wet);

                float nightScale = lerp(1.0, smoothstep(0.15, 0.65, _TS_Night), _NightEmission);
                float blink = _BlinkRate > 0.0 ? step(0.5, frac(_Time.y * _BlinkRate + i.positionWS.x * 0.13)) : 1.0;
                half3 emission = _EmissionColor.rgb * tint * nightScale * blink;

                return TS_ShadePBR(i.positionWS, i.normalWS, i.positionCS, i.fogFactor, albedo, _Metallic, smoothness, 1, emission, 1);
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
            #pragma multi_compile_instancing
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
            #pragma multi_compile_instancing
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
            #pragma multi_compile_instancing
            #include "TaxiSimPasses.hlsl"
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
