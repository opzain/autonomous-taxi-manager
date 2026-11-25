// Mithi River / Mahim Creek / Arabian Sea water.
//  * Vertex waves + analytic wave normals + procedural detail normals (no textures)
//  * Depth-based colour (shallow -> deep) and soft shoreline foam using the camera depth texture
//  * Fresnel reflections of the sky and city (reflection probe) and sun/moon highlights
//  * Monsoon rain ripples
Shader "TaxiSim/Water"
{
    Properties
    {
        _ShallowColor ("Shallow Colour (A = opacity)", Color) = (0.26, 0.36, 0.28, 0.72)
        _DeepColor ("Deep Colour (A = opacity)", Color) = (0.07, 0.15, 0.13, 0.96)
        _DepthFade ("Depth Fade", Float) = 1.3
        _FoamColor ("Foam Colour", Color) = (0.8, 0.84, 0.8, 1)
        _FoamDistance ("Foam Distance", Float) = 0.3
        _WaveAmplitude ("Wave Amplitude", Float) = 0.035
        _WaveScale ("Wave Scale", Float) = 0.35
        _WaveSpeed ("Wave Speed", Float) = 0.7
        _NormalStrength ("Normal Strength", Float) = 0.55
        _Smoothness ("Smoothness", Range(0, 1)) = 0.95
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _ShallowColor;
            half4 _DeepColor;
            float _DepthFade;
            half4 _FoamColor;
            float _FoamDistance;
            float _WaveAmplitude;
            float _WaveScale;
            float _WaveSpeed;
            float _NormalStrength;
            half _Smoothness;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _FORWARD_PLUS _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fog

            #include "TaxiSimLighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screenPos  : TEXCOORD1;
                float  fogFactor  : TEXCOORD2;
            };

            float WaveHeight(float2 p, float t)
            {
                float2 d1 = normalize(float2(1.0, 0.3));
                float2 d2 = normalize(float2(-0.4, 1.0));
                float2 d3 = normalize(float2(0.7, -0.8));
                float h = sin(dot(d1, p) * 1.3 + t * 1.1) * 0.5
                        + sin(dot(d2, p) * 2.1 + t * 1.6) * 0.3
                        + sin(dot(d3, p) * 3.3 + t * 2.3) * 0.2;
                return h;
            }

            float2 WaveGradient(float2 p, float t)
            {
                float2 d1 = normalize(float2(1.0, 0.3));
                float2 d2 = normalize(float2(-0.4, 1.0));
                float2 d3 = normalize(float2(0.7, -0.8));
                float2 g = d1 * (cos(dot(d1, p) * 1.3 + t * 1.1) * 0.5 * 1.3)
                         + d2 * (cos(dot(d2, p) * 2.1 + t * 1.6) * 0.3 * 2.1)
                         + d3 * (cos(dot(d3, p) * 3.3 + t * 2.3) * 0.2 * 3.3);
                return g;
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float t = _Time.y * _WaveSpeed;
                positionWS.y += WaveHeight(positionWS.xz * _WaveScale, t) * _WaveAmplitude;
                o.positionWS = positionWS;
                o.positionCS = TransformWorldToHClip(positionWS);
                o.screenPos = ComputeScreenPos(o.positionCS);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = _Time.y * _WaveSpeed;
                float2 p = i.positionWS.xz;

                // Normals: large waves + two scrolling detail layers + rain ripples
                float2 grad = WaveGradient(p * _WaveScale, t) * _WaveScale * _WaveAmplitude * 6.0;
                float e = 0.06;
                float2 q1 = p * 1.7 + float2(t * 0.35, t * 0.12);
                float n0 = TS_ValueNoise(q1);
                grad += float2(TS_ValueNoise(q1 + float2(e, 0)) - n0, TS_ValueNoise(q1 + float2(0, e)) - n0) / e * 0.05;
                float2 q2 = p * 4.3 - float2(t * 0.22, t * 0.41);
                float m0 = TS_ValueNoise(q2);
                grad += float2(TS_ValueNoise(q2 + float2(e, 0)) - m0, TS_ValueNoise(q2 + float2(0, e)) - m0) / e * 0.025;
                grad += TS_RainRipples(p, _Time.y, _TS_Rain) * 1.5;
                float3 n = normalize(float3(-grad.x * _NormalStrength, 1.0, -grad.y * _NormalStrength));

                // Depth below the surface (scene depth vs water surface depth)
                float2 screenUV = i.screenPos.xy / i.screenPos.w;
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float waterDepth = i.screenPos.w;
                float depthDiff = max(0.0, sceneDepth - waterDepth);

                float depthT = saturate(depthDiff / _DepthFade);
                half3 color = lerp(_ShallowColor.rgb, _DeepColor.rgb, depthT);
                half alpha = lerp(_ShallowColor.a, _DeepColor.a, depthT);
                color *= lerp(1.0, 0.75, _TS_Wetness);

                // Shoreline foam
                float shore = saturate(1.0 - depthDiff / _FoamDistance);
                float foamNoise = TS_ValueNoise(p * 6.0 + t * 0.4);
                float foam = smoothstep(0.45, 0.75, shore * 0.8 + foamNoise * 0.4) * shore;
                color = lerp(color, _FoamColor.rgb, foam);
                alpha = max(alpha, foam);

                // Fresnel: water becomes a mirror at grazing angles
                float3 viewDir = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float fresnel = pow(1.0 - saturate(dot(n, viewDir)), 5.0);
                alpha = saturate(alpha + fresnel * 0.6);

                half4 shaded = TS_ShadePBR(i.positionWS, n, i.positionCS, i.fogFactor, color, 0, lerp(_Smoothness, 0.6, foam), 1, half3(0, 0, 0), alpha);
                return half4(shaded.rgb, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
