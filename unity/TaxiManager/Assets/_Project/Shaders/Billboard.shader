// Printed advertising hoardings, rooftop billboards and bus-shelter ad panels (v0.7).
// Front face: a procedural poster (TS_PosterPattern, seed = uv1.x), lit by the sun by day and by
// its own flood lamps at night. Back face: painted steel sheet.
Shader "TaxiSim/Billboard"
{
    Properties
    {
        _NightIllumination ("Night Flood-lamp Strength", Float) = 1.4
        _Smoothness ("Smoothness", Range(0, 1)) = 0.35
        _BackColor ("Back Side Colour", Color) = (0.32, 0.33, 0.35, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half _NightIllumination;
            half _Smoothness;
            half4 _BackColor;
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
            #pragma multi_compile_fog

            #include "TaxiSimLighting.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; float4 uv1 : TEXCOORD1; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float seed : TEXCOORD3;
                float fogFactor : TEXCOORD4;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv;
                o.seed = v.uv1.x;
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 frag(Varyings i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                float front = IS_FRONT_VFACE(face, 1.0, 0.0);
                float3 n = normalize(i.normalWS) * (front > 0.5 ? 1.0 : -1.0);
                half3 poster = TS_PosterPattern(i.uv, i.seed);
                half3 albedo = lerp(_BackColor.rgb, poster, front);
                float nightK = smoothstep(0.2, 0.7, _TS_Night);
                // Flood lamps mounted along the bottom edge: brightest at the bottom, fading upwards
                float lamp = lerp(1.0, 0.55, i.uv.y) * (0.85 + 0.15 * sin(i.uv.x * 20.0));
                half3 emission = poster * _NightIllumination * nightK * lamp * front;
                half smoothness = lerp(_Smoothness, 0.75, _TS_Wetness);
                return TS_ShadePBR(i.positionWS, n, i.positionCS, i.fogFactor, albedo, 0.05, smoothness, 1, emission, 1);
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
